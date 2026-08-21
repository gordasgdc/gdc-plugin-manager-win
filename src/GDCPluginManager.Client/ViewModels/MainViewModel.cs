using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;
using Wpf.Ui.Controls;

namespace GDCPluginManager.Client.ViewModels;

/// Un filtru din bara laterala — "Toate" (Type == null) plus una pe fiecare PluginType.
public sealed record CategoryFilter(string Label, PluginType? Type, SymbolRegular Symbol);

/// Ce se afiseaza in panoul principal — port 1:1 al SidebarSection din
/// ContentView.swift (fara .help, neportat inca).
public enum SidebarPage
{
    Catalog,
    Courses,
    Apps,
    License,
}

/// ViewModel-ul ferestrei principale — echivalentul ContentView-ului de pe
/// Mac. Leaga CatalogService (catalog + refresh), InstallManager (stare
/// instalare) si LicenseManager (deblocare per-produs) intr-o singura
/// lista filtrabila/cautabila de ProductViewModel, plus paginile separate
/// Cursuri/Aplicatii/Licenta.
public sealed partial class MainViewModel : ObservableObject
{
    public ObservableCollection<ProductViewModel> Products { get; } = [];
    public ICollectionView ProductsView { get; }

    public ObservableCollection<CourseViewModel> Courses { get; } = [];
    public ObservableCollection<AppLinkViewModel> Apps { get; } = [];

    public LicensePaneViewModel LicensePane { get; }

    public IReadOnlyList<CategoryFilter> Categories { get; } =
    [
        new("Toate", null, SymbolRegular.Apps24),
        new(PluginType.Dctl.Label(), PluginType.Dctl, SymbolRegular.Wand24),
        new(PluginType.Lut.Label(), PluginType.Lut, SymbolRegular.Eyedropper24),
        new(PluginType.Fuse.Label(), PluginType.Fuse, SymbolRegular.PuzzlePiece24),
        new(PluginType.PowerGrade.Label(), PluginType.PowerGrade, SymbolRegular.PaintBrush24),
        new(PluginType.Ofx.Label(), PluginType.Ofx, SymbolRegular.Camera24),
    ];

    [ObservableProperty]
    private CategoryFilter _selectedCategory;

    [ObservableProperty]
    private SidebarPage _currentPage = SidebarPage.Catalog;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private string? _updateBannerText;

    [ObservableProperty]
    private string? _updateDownloadUrl;

    public string MachineIdDisplay => MachineID.Display;

    public MainViewModel()
    {
        _selectedCategory = Categories[0];
        LicensePane = new LicensePaneViewModel(
            allProductIds: () => Products.Select(p => p.Item.Id).ToList(),
            productName: id => Products.FirstOrDefault(p => p.Item.Id == id)?.Name ?? id);

        ProductsView = CollectionViewSource.GetDefaultView(Products);
        ProductsView.Filter = FilterProduct;

        // Cardul unui produs deblocheaza singur din ProductViewModel — dar
        // starea trebuie recalculata pe toate cardurile dupa orice
        // activare/dezactivare facuta din panoul Licenta.
        LicensePane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LicensePaneViewModel.OwnedLicenses) or nameof(LicensePaneViewModel.IsLicensed))
            {
                foreach (var p in Products) p.Refresh();
            }
        };

        // Catalogul deja are ce era in cache (CatalogService il incarca in
        // constructor) — populeaza lista imediat, apoi RefreshCommand
        // (declansat din code-behind la Loaded) aduce varianta live.
        RebuildFromCatalog();
    }

    partial void OnSelectedCategoryChanged(CategoryFilter value)
    {
        CurrentPage = SidebarPage.Catalog;
        ProductsView.Refresh();
    }

    partial void OnSearchTextChanged(string value) => ProductsView.Refresh();

    private bool FilterProduct(object obj)
    {
        if (obj is not ProductViewModel p) return false;
        if (SelectedCategory.Type is { } type && p.Item.Type != type) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        await UpdateChecker.Shared.CheckAsync();
        var info = UpdateChecker.Shared.AvailableUpdate;
        if (info is not null)
        {
            UpdateBannerText = $"Versiune noua disponibila: {info.Version}" +
                                (string.IsNullOrWhiteSpace(info.Changes) ? "" : $" — {info.Changes}");
            UpdateDownloadUrl = info.DownloadUrl.GetValueOrDefault("windows") ?? info.DownloadUrl.Values.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await CatalogService.Shared.RefreshAsync();
            RebuildFromCatalog();
            LoadError = CatalogService.Shared.LoadError;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectCategory(CategoryFilter category) => SelectedCategory = category;

    [RelayCommand]
    private void ShowCourses() => CurrentPage = SidebarPage.Courses;

    [RelayCommand]
    private void ShowApps() => CurrentPage = SidebarPage.Apps;

    [RelayCommand]
    private void ShowLicense() => CurrentPage = SidebarPage.License;

    [RelayCommand]
    private void DismissUpdateBanner()
    {
        UpdateChecker.Shared.Dismiss();
        UpdateBannerText = null;
    }

    private void RebuildFromCatalog()
    {
        Products.Clear();
        foreach (var item in CatalogService.Shared.Items)
        {
            Products.Add(new ProductViewModel(item));
        }

        Courses.Clear();
        foreach (var course in CatalogService.Shared.Courses)
        {
            Courses.Add(new CourseViewModel(course));
        }

        Apps.Clear();
        foreach (var app in CatalogService.Shared.Apps)
        {
            Apps.Add(new AppLinkViewModel(app));
        }

        LicensePane.RebuildOwnedLicenses();
    }
}

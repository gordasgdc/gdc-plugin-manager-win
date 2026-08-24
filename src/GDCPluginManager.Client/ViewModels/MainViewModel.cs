using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;   // Process.Start — deschide linkul APK in browser
using System.Windows;       // Clipboard — copiaza linkul APK
using System.Diagnostics;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;
using Wpf.Ui.Controls;

namespace GDCPluginManager.Client.ViewModels;

/// Un filtru din bara laterala — "Toate" (Type == null) plus una pe fiecare PluginType.
public sealed record CategoryFilter(string Label, PluginType? Type, SymbolRegular Symbol);

/// Filtru rapid Toate/Gratuite/Premium — cerut explicit 2026-08-24, ca
/// cine vrea doar uneltele gratuite sa le gaseasca instant. Port 1:1 al
/// PriceFilter din CatalogGrid (ContentView.swift, Mac).
public enum PriceFilter { All, Free, Paid }

/// Ce se afiseaza in panoul principal — port 1:1 al SidebarSection din
/// ContentView.swift (fara .help, neportat inca).
public enum SidebarPage
{
    Catalog,
    Courses,
    EducationalResources,
    Events,
    PartnerStores,
    Apps,
    Android,
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
    public ObservableCollection<EducationalResourceViewModel> EducationalResources { get; } = [];
    public ObservableCollection<EventViewModel> Events { get; } = [];
    public ObservableCollection<PartnerStoreViewModel> PartnerStores { get; } = [];
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
    private PriceFilter _priceFilter = PriceFilter.All;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private string? _updateBannerText;

    /// URL-ul de descarcare al versiunii noi de APLICATIE (nu de produs),
    /// citit din update.json.
    ///
    /// WARNING: proprietatea asta a existat o vreme fara sa fie legata de
    /// nimic in MainWindow.xaml — banner-ul anunta versiunea noua si nu
    /// oferea nicio cale de a o lua. Daca modifici banner-ul, verifica si
    /// legatura cu DownloadUpdateCommand.
    [ObservableProperty]
    private string? _updateDownloadUrl;

    // Aplicatia companion de Android (APK). Datele vin din android.json prin
    // AndroidReleaseService — vezi nota de arhitectura de acolo despre de ce nu
    // folosim "releases/latest/download/...".
    [ObservableProperty]
    private AndroidRelease? _androidRelease;

    [ObservableProperty]
    private bool _androidFailed;

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
    partial void OnPriceFilterChanged(PriceFilter value) => ProductsView.Refresh();

    [RelayCommand]
    private void SetPriceFilter(PriceFilter filter) => PriceFilter = filter;

    private bool FilterProduct(object obj)
    {
        if (obj is not ProductViewModel p) return false;
        if (SelectedCategory.Type is { } type && p.Item.Type != type) return false;
        if (PriceFilter == PriceFilter.Free && !p.Item.IsFree) return false;
        if (PriceFilter == PriceFilter.Paid && p.Item.IsFree) return false;
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

        // Nu blocam pornirea daca android.json nu e disponibil: panoul isi arata
        // singur mesajul de eroare, restul aplicatiei merge normal.
        await AndroidReleaseService.Shared.LoadAsync();
        AndroidRelease = AndroidReleaseService.Shared.Release;
        AndroidFailed = AndroidReleaseService.Shared.Failed;
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
    private void ShowEducationalResources() => CurrentPage = SidebarPage.EducationalResources;

    [RelayCommand]
    private void ShowEvents() => CurrentPage = SidebarPage.Events;

    [RelayCommand]
    private void ShowPartnerStores() => CurrentPage = SidebarPage.PartnerStores;

    [RelayCommand]
    private void ShowApps() => CurrentPage = SidebarPage.Apps;

    [RelayCommand]
    private void ShowAndroid() => CurrentPage = SidebarPage.Android;

    /// Deschide pagina de release a APK-ului in browserul implicit.
    [RelayCommand]
    private void OpenAndroidPage()
    {
        var url = AndroidRelease?.ReleasePage ?? AndroidRelease?.ApkUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        // UseShellExecute e obligatoriu pe .NET pentru a deschide un URL.
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// Copiaza linkul .apk, ca sa poata fi trimis pe telefon prin orice canal.
    [RelayCommand]
    private void CopyAndroidLink()
    {
        var url = AndroidRelease?.ApkUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Clipboard.SetText(url); }
        catch { /* clipboard-ul poate fi blocat de alt proces — nu e fatal */ }
    }

    [RelayCommand]
    private void ShowLicense() => CurrentPage = SidebarPage.License;

    /// Deschide in browser pagina de descarcare a versiunii noi de aplicatie.
    ///
    /// NOTE — de ce browser si nu instalare automata: actualizarea APLICATIEI
    /// nu e (inca) un self-update in-app. Pe Windows instalarea se face cu
    /// GDCPluginManagerSetup.exe, iar un instalator nu poate suprascrie un
    /// .exe care ruleaza — ar fi nevoie de un proces separat care asteapta
    /// iesirea aplicatiei, ruleaza setup-ul si o reporneste.
    ///
    /// WARNING: a NU se confunda cu actualizarea PRODUSELOR (LUT/DCTL/OFX/
    /// PowerGrade), care e complet in-app, cu un click, prin
    /// InstallManager.InstallAsync — vezi butonul "Actualizeaza" de pe card,
    /// afisat cand versiunea din catalog e mai noua decat cea instalata.
    /// Sunt doua fluxuri diferite, cu doua surse diferite:
    ///   produse   -> catalog.json  -> InstallManager (1 click, in-app)
    ///   aplicatia -> update.json   -> acest buton     (deschide browserul)
    [RelayCommand]
    private void DownloadUpdate()
    {
        var url = UpdateDownloadUrl;
        // Doar http/https: update.json e al nostru, dar un URL invalid aici
        // ar ajunge direct in ShellExecute.
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

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

        EducationalResources.Clear();
        foreach (var resource in CatalogService.Shared.EducationalResources)
        {
            EducationalResources.Add(new EducationalResourceViewModel(resource));
        }

        Events.Clear();
        foreach (var ev in CatalogService.Shared.Events)
        {
            Events.Add(new EventViewModel(ev));
        }

        PartnerStores.Clear();
        foreach (var store in CatalogService.Shared.PartnerStores)
        {
            PartnerStores.Add(new PartnerStoreViewModel(store));
        }

        Apps.Clear();
        foreach (var app in CatalogService.Shared.Apps)
        {
            Apps.Add(new AppLinkViewModel(app));
        }

        LicensePane.RebuildOwnedLicenses();
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;   // Process.Start — deschide linkul APK in browser
using System.Windows;       // Clipboard — copiaza linkul APK
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Client.Services;
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
    ServiceCenters,
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
    public ObservableCollection<ServiceCenterViewModel> ServiceCenters { get; } = [];
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

    /// Dependinte de sistem lipsa OBLIGATORII (ex. DaVinci Resolve
    /// neinstalat) — vezi SystemDependencyChecker.cs. Port 1:1 al
    /// DependencyBanner din ContentView.swift. Cele optionale NU intra
    /// aici (nu declanseaza bannerul de sus), doar in AllDependencies
    /// (panoul dedicat).
    public ObservableCollection<SystemDependency> MissingDependencies { get; } = [];

    /// TOATE componentele (inclusiv optionale) — sursa panoului dedicat
    /// "Verificare & Dependinte Sistem", deschis din indicatorul
    /// 🔴/🟢 din header. Port 1:1 al DependencyPanel.swift (Mac).
    public ObservableCollection<SystemDependency> AllDependencies { get; } = [];

    /// Indicatorul global e verde DOAR daca toate componentele
    /// OBLIGATORII (IsOptional == false) sunt prezente — cele optionale
    /// (foldere, Scripting API) nu blocheaza starea globala.
    public bool IsDependenciesReady => AllDependencies.Count == 0 || AllDependencies.Where(d => !d.IsOptional).All(d => d.IsPresent);

    // Aplicatia mobila companion (fost APK/TWA, RETRAS 2026-08-24 — cerut
    // explicit de Cristi: "scapam complet de problemele cu certificatele,
    // erorile de instalare pe Android si fisierele APK"). Acum e direct
    // PWA-ul gordas.dev/app.html, deschis in browser — merge pe Android SI
    // iPhone, fara instalare, fara avertisment de certificat. Link fix, nu
    // mai exista android.json/AndroidReleaseService/versiune de verificat.
    public const string MobileAppUrl = "https://gordas.dev/app.html";

    /// Cod QR spre PWA — generat o singura data (link fix, nu se schimba),
    /// nu mai depinde de nicio cerere de retea.
    public System.Windows.Media.Imaging.BitmapImage? MobileAppQrImage { get; } = QrCodeImageGenerator.Generate(MobileAppUrl);

    public string MachineIdDisplay => MachineID.Display;

    /// Versiune vizibila in UI, obligatoriu (CLAUDE.md, Partea 1, Regula 7) -
    /// lipsea complet din sidebar-ul Windows (gasit la audit 2026-08-26).
    public string AppVersionDisplay =>
        $"v{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?"}";

    /// Profil Utilizator opțional in sidebar (vezi CLAUDE.md, Partea 1,
    /// Regula 12) — port 1:1 al ProfileSidebarBlock.swift (Mac).
    public string ProfileDisplayName => UserProfileStore.Shared.DisplayName;
    public string ProfileEmail => UserProfileStore.Shared.Email;
    public string ProfileMachineId => UserProfileStore.Shared.MachineId;

    /// Apelată din ProfileEditorWindow după salvare — reface bindings
    /// derivate (DisplayName/Email) fără să reîncarce tot ViewModel-ul.
    public void NotifyProfileChanged()
    {
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(ProfileEmail));
    }

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

        RefreshDependencies();
        await GDCPluginManager.Core.Services.LicenseManager.Shared.RefreshRevocationsAsync();
    }

    /// Reverifica toate componentele — apelata la lansare (InitializeAsync)
    /// si din butonul "Reverifica tot" al panoului dedicat.
    [RelayCommand]
    private void RefreshDependencies()
    {
        var all = SystemDependencyChecker.CheckAll();

        AllDependencies.Clear();
        foreach (var dep in all) AllDependencies.Add(dep);
        OnPropertyChanged(nameof(IsDependenciesReady));

        MissingDependencies.Clear();
        foreach (var dep in all.Where(d => !d.IsPresent && !d.IsOptional))
        {
            MissingDependencies.Add(dep);
        }
    }

    [RelayCommand]
    private void InstallDependency(SystemDependency dependency)
    {
        if (dependency.DownloadUrl is { } url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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
    private void ShowEducationalResources() => CurrentPage = SidebarPage.EducationalResources;

    [RelayCommand]
    private void ShowEvents() => CurrentPage = SidebarPage.Events;

    [RelayCommand]
    private void ShowPartnerStores() => CurrentPage = SidebarPage.PartnerStores;

    [RelayCommand]
    private void ShowServiceCenters() => CurrentPage = SidebarPage.ServiceCenters;

    [RelayCommand]
    private void ShowApps() => CurrentPage = SidebarPage.Apps;

    [RelayCommand]
    private void ShowAndroid() => CurrentPage = SidebarPage.Android;

    /// Deschide PWA-ul (gordas.dev/app.html) in browserul implicit.
    [RelayCommand]
    private void OpenAndroidPage()
    {
        // UseShellExecute e obligatoriu pe .NET pentru a deschide un URL.
        Process.Start(new ProcessStartInfo(MobileAppUrl) { UseShellExecute = true });
    }

    /// Copiaza linkul PWA-ului, ca sa poata fi trimis pe telefon prin orice canal.
    [RelayCommand]
    private void CopyAndroidLink()
    {
        try { Clipboard.SetText(MobileAppUrl); }
        catch { /* clipboard-ul poate fi blocat de alt proces — nu e fatal */ }
    }

    [RelayCommand]
    private void ShowLicense() => CurrentPage = SidebarPage.License;

    /// Descarca si instaleaza automat versiunea noua de aplicatie — vezi
    /// SelfUpdater.cs pentru fluxul complet (descarcare, dezarhivare,
    /// redenumire cu versiunea, lansare instalator).
    ///
    /// PROCESS (2026-08-26): pana acum deschidea doar pagina de descarcare
    /// in browser. Portat 1:1 dupa DataMover (Mac), care a verificat live
    /// ca reteta functioneaza. Aici, spre deosebire de Mac, instalatorul
    /// Inno ramane NESILENTIOS — vezi WARNING din SelfUpdater.cs despre
    /// AppMutex/CloseApplications, care nu sunt configurate in installer.iss.
    ///
    /// WARNING: a NU se confunda cu actualizarea PRODUSELOR (LUT/DCTL/OFX/
    /// PowerGrade), care e complet in-app, cu un click, prin
    /// InstallManager.InstallAsync — vezi butonul "Actualizeaza" de pe card,
    /// afisat cand versiunea din catalog e mai noua decat cea instalata.
    /// Sunt doua fluxuri diferite, cu doua surse diferite:
    ///   produse   -> catalog.json  -> InstallManager (1 click, in-app)
    ///   aplicatia -> update.json   -> acest buton     -> SelfUpdater
    [RelayCommand]
    private async Task DownloadUpdate()
    {
        var info = UpdateChecker.Shared.AvailableUpdate;
        if (info is null) return;
        await SelfUpdater.DownloadAndInstallAsync(info);
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

        ServiceCenters.Clear();
        foreach (var center in CatalogService.Shared.ServiceCenters)
        {
            ServiceCenters.Add(new ServiceCenterViewModel(center));
        }

        Apps.Clear();
        foreach (var app in CatalogService.Shared.Apps)
        {
            Apps.Add(new AppLinkViewModel(app));
        }

        LicensePane.RebuildOwnedLicenses();

        // BUG REAL gasit 2026-08-26: Products.Clear() + Add() individual
        // (mai sus) nu re-aplica intotdeauna filtrul activ al ProductsView
        // (ListCollectionView cu Filter setat) - daca SelectedCategory
        // ramane "Toate" (neschimbata) intre doua rebuild-uri, OnSelectedCategoryChanged
        // nu mai declanseaza ProductsView.Refresh(), iar lista ramane goala/
        // stale pana userul comuta manual pe alta categorie si inapoi.
        // Fix: Refresh() explicit, necondiționat, de fiecare data cand
        // Products se reconstruieste.
        ProductsView?.Refresh();
    }
}

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

/// Filtru de compatibilitate OS (Etapa 1) — port 1:1 al `OSFilter` din
/// CatalogGrid (ContentView.swift, Mac). Regula cheie: o intrare
/// `CrossPlatform` apare la ORICE filtru, nu doar la "Toate" — chiar ruleaza
/// pe ambele platforme, deci ascunderea ei la "Mac"/"Windows" ar fi gresita.
public enum OSFilter { All, Mac, Windows }

/// Ce se afiseaza in panoul principal — port 1:1 al SidebarSection din
/// ContentView.swift (fara .help, neportat inca).
public enum SidebarPage
{
    Catalog,
    AudioTracks,
    Courses,
    EducationalResources,
    Events,
    PartnerStores,
    ServiceCenters,
    Apps,
    Android,
    License,
    /// Cele 4 rubrici noi de Resurse Download (Etapa 2, 2026-08-29) — una per
    /// DownloadCategory, exact ca `SidebarSection.download(DownloadCategory)`
    /// de pe Mac.
    DownloadLut,
    DownloadSfx,
    DownloadVfx,
    DownloadPlugin,
    /// "Aplicatiile Mele" (Etapa 3, 2026-08-29) — aplicatiile GDC gasite
    /// instalate pe masina asta + scurtaturi personalizate.
    MyApps,
    /// Pseudo-pagina (Etapa 1): nu e o rubrica din sidebar, ci starea
    /// "cautare globala activa". Vezi MainViewModel.ContentPage — cand
    /// campul de cautare e nevid, ACEASTA e pagina randata, indiferent ce
    /// rubrica ramane selectata (si evidentiata) in sidebar.
    GlobalSearch,
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
    public ObservableCollection<AudioTrackViewModel> AudioTracks { get; } = [];

    // ---- Resurse Download (Etapa 2) --------------------------------------
    // Cate o colectie per categorie, ca fiecare rubrica din sidebar sa lege
    // direct la lista ei (fara un CollectionView filtrat in plus). Populate
    // toate din aceeasi sursa, in RebuildFromCatalog.
    public ObservableCollection<DownloadResourceViewModel> DownloadLuts { get; } = [];
    public ObservableCollection<DownloadResourceViewModel> DownloadSfx { get; } = [];
    public ObservableCollection<DownloadResourceViewModel> DownloadVfx { get; } = [];
    public ObservableCollection<DownloadResourceViewModel> DownloadPlugins { get; } = [];

    /// Toate resursele, in ordinea din catalog — sursa pentru cautarea
    /// globala si pentru lista de ID-uri candidate la activarea unei licente.
    private readonly List<DownloadResourceViewModel> _allDownloadResources = [];

    // ---- Cautare GLOBALA (Etapa 1) ----------------------------------------
    // Port 1:1 al `GlobalSearchResults` din ContentView.swift (Mac): cand
    // campul de cautare e NEVID, continutul rubricii curente e inlocuit
    // complet de rezultate din TOATE colectiile catalogului. Fiecare colectie
    // e filtrata cu FuzzySearch pe campurile ei relevante. O sectiune fara
    // nicio potrivire nu se randa deloc (vezi NonZeroToVisibilityConverter).
    //
    // Colectii SEPARATE de cele de mai sus (nu un filtru pe aceleasi) pentru
    // ca rubrica selectata trebuie sa ramana intacta — la golirea campului de
    // cautare revenim instant la ea, fara sa reconstruim nimic.
    public ObservableCollection<ProductViewModel> GlobalProducts { get; } = [];
    public ObservableCollection<CourseViewModel> GlobalCourses { get; } = [];
    public ObservableCollection<EducationalResourceViewModel> GlobalEducationalResources { get; } = [];
    public ObservableCollection<EventViewModel> GlobalEvents { get; } = [];
    public ObservableCollection<PartnerStoreViewModel> GlobalPartnerStores { get; } = [];
    public ObservableCollection<ServiceCenterViewModel> GlobalServiceCenters { get; } = [];
    public ObservableCollection<AppLinkViewModel> GlobalApps { get; } = [];
    public ObservableCollection<AudioTrackViewModel> GlobalAudioTracks { get; } = [];
    /// Etapa 2: a 9-a colectie din cautarea globala (ca pe Mac, unde
    /// `GlobalSearchResults` a trecut de la 8 la 9 sectiuni).
    public ObservableCollection<DownloadResourceViewModel> GlobalDownloadResources { get; } = [];

    /// Istoric de cautari recente, persistat local (max 8, fara duplicate) —
    /// vezi SearchHistoryStore.cs.
    private readonly SearchHistoryStore _searchHistory = new("global");
    public ObservableCollection<string> RecentSearches { get; } = [];

    /// True cand campul de cautare e nevid — comuta intreg panoul de continut
    /// pe rezultatele globale, indiferent ce rubrica e selectata in sidebar.
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    /// Ce se randa EFECTIV in panoul de continut. Rubricile din XAML se leaga
    /// de asta, nu de `CurrentPage` — asa o cautare activa le ascunde pe toate
    /// dintr-un singur loc, fara sa dubleze conditia "si nu se cauta" pe
    /// fiecare panou. `CurrentPage` ramane neschimbata (sidebar-ul isi
    /// pastreaza selectia evidentiata), deci la golirea campului revenim
    /// instant exact la rubrica de dinainte.
    public SidebarPage ContentPage => IsSearching ? SidebarPage.GlobalSearch : CurrentPage;

    /// True cand cautarea globala nu a gasit absolut nimic, in nicio colectie.
    public bool HasNoSearchResults => IsSearching
        && GlobalProducts.Count == 0 && GlobalCourses.Count == 0
        && GlobalEducationalResources.Count == 0 && GlobalEvents.Count == 0
        && GlobalPartnerStores.Count == 0 && GlobalServiceCenters.Count == 0
        && GlobalApps.Count == 0 && GlobalAudioTracks.Count == 0
        && GlobalDownloadResources.Count == 0;

    public LicensePaneViewModel LicensePane { get; }

    /// "Aplicatiile Mele" (Etapa 3) — pagina proprie, cu propriul ViewModel.
    public MyAppsViewModel MyApps { get; } = new();

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

    /// Filtru de compatibilitate OS (Etapa 1) — aplicat pe TOATE colectiile,
    /// atat in rubrica curenta cat si in rezultatele cautarii globale.
    [ObservableProperty]
    private OSFilter _selectedOS = OSFilter.All;

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
        // Etapa 2: un cod lipit in panoul Licenta trebuie sa valideze si
        // pentru Resursele Download, nu doar pentru produse — acelasi store
        // de licente, cheiat generic dupa ID (vezi LicenseManager.IsUnlocked).
        // Fara asta, o resursa platita ar fi imposibil de deblocat.
        LicensePane = new LicensePaneViewModel(
            allProductIds: () => Products.Select(p => p.Item.Id)
                .Concat(_allDownloadResources.Select(r => r.Resource.Id))
                .ToList(),
            productName: id => Products.FirstOrDefault(p => p.Item.Id == id)?.Name
                ?? _allDownloadResources.FirstOrDefault(r => r.Resource.Id == id)?.Name
                ?? id);

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
                // Etapa 2: resursele download folosesc acelasi store de
                // licente, deci si ele trebuie recalculate dupa o activare.
                foreach (var r in _allDownloadResources) r.Refresh();
            }
        };

        ReloadRecentSearches();

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

    partial void OnCurrentPageChanged(SidebarPage value) => OnPropertyChanged(nameof(ContentPage));

    partial void OnSearchTextChanged(string value)
    {
        ProductsView.Refresh();
        RebuildGlobalSearch();
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(ContentPage));
    }

    partial void OnPriceFilterChanged(PriceFilter value) => ProductsView.Refresh();

    partial void OnSelectedOSChanged(OSFilter value)
    {
        ProductsView.Refresh();
        // Filtrul OS se aplica si peste rezultatele globale, nu doar peste
        // rubrica curenta — altfel o cautare activa ar ignora filtrul.
        RebuildGlobalSearch();
    }

    [RelayCommand]
    private void SetPriceFilter(PriceFilter filter) => PriceFilter = filter;

    [RelayCommand]
    private void SetOSFilter(OSFilter filter) => SelectedOS = filter;

    /// Salveaza termenul curent in istoric — apelata cand userul confirma
    /// cautarea (Enter), NU la fiecare tasta apasata (altfel istoricul s-ar
    /// umple cu prefixe: "l", "lu", "lut").
    [RelayCommand]
    private void CommitSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return;
        _searchHistory.Record(SearchText);
        ReloadRecentSearches();
    }

    [RelayCommand]
    private void ApplyRecentSearch(string term) => SearchText = term;

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void ClearSearchHistory()
    {
        _searchHistory.Clear();
        ReloadRecentSearches();
    }

    private void ReloadRecentSearches()
    {
        RecentSearches.Clear();
        foreach (var term in _searchHistory.Recent) RecentSearches.Add(term);
    }

    /// Regula OS (Etapa 1): `CrossPlatform` apare la ORICE filtru — chiar
    /// ruleaza pe ambele platforme, deci ascunderea ei ar fi gresita.
    private bool MatchesOS(SupportedOS os) => SelectedOS switch
    {
        OSFilter.Mac => os is SupportedOS.MacOS or SupportedOS.CrossPlatform,
        OSFilter.Windows => os is SupportedOS.Windows or SupportedOS.CrossPlatform,
        _ => true,
    };

    private bool FilterProduct(object obj)
    {
        if (obj is not ProductViewModel p) return false;
        if (SelectedCategory.Type is { } type && p.Item.Type != type) return false;
        if (PriceFilter == PriceFilter.Free && !p.Item.IsFree) return false;
        if (PriceFilter == PriceFilter.Paid && p.Item.IsFree) return false;
        if (!MatchesOS(p.Item.SupportedOS)) return false;
        // FuzzySearch (Etapa 1) inlocuieste `Contains` — acelasi comportament
        // ca pe Mac: substring fara diacritice + toleranta la typo-uri.
        return FuzzySearch.MatchesAny(SearchText, p.Name, p.Description, p.Item.Id, p.TypeLabel);
    }

    /// Reconstruieste rezultatele cautarii globale din TOATE colectiile.
    ///
    /// NOTA DE SCOP (nu o omisiune): doar `PluginItem` are `supportedOS` in
    /// model — Cursuri/Materiale/Evenimente/Magazine/Service/Aplicatii/Audio
    /// nu au acest camp nici pe Mac, deci sunt tratate implicit ca
    /// `CrossPlatform` (apar la orice filtru OS), exact ca pe Mac. Filtrul OS
    /// e deci "aplicat pe toate colectiile", dar are efect real doar acolo
    /// unde modelul poarta informatia.
    private void RebuildGlobalSearch()
    {
        GlobalProducts.Clear();
        GlobalCourses.Clear();
        GlobalEducationalResources.Clear();
        GlobalEvents.Clear();
        GlobalPartnerStores.Clear();
        GlobalServiceCenters.Clear();
        GlobalApps.Clear();
        GlobalAudioTracks.Clear();
        GlobalDownloadResources.Clear();

        var query = SearchText;
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var p in Products)
            {
                if (!MatchesOS(p.Item.SupportedOS)) continue;
                if (FuzzySearch.MatchesAny(query, p.Name, p.Description, p.Item.Id, p.TypeLabel))
                    GlobalProducts.Add(p);
            }

            foreach (var c in Courses)
            {
                if (FuzzySearch.MatchesAny(query, c.Course.Name, c.Course.Description, c.Course.Id))
                    GlobalCourses.Add(c);
            }

            foreach (var r in EducationalResources)
            {
                if (FuzzySearch.MatchesAny(query, r.Resource.Name, r.Resource.Description, r.Resource.Id, r.Resource.Kind.Label()))
                    GlobalEducationalResources.Add(r);
            }

            foreach (var e in Events)
            {
                if (FuzzySearch.MatchesAny(query, e.Event.Title, e.Event.Description, e.Event.Id, e.Event.Location, e.Event.DateDisplay))
                    GlobalEvents.Add(e);
            }

            foreach (var s in PartnerStores)
            {
                if (FuzzySearch.MatchesAny(query, s.Store.Name, s.Store.Description, s.Store.Id))
                    GlobalPartnerStores.Add(s);
            }

            foreach (var s in ServiceCenters)
            {
                if (FuzzySearch.MatchesAny(query, s.Center.Name, s.Center.Specialization, s.Center.Id, s.Center.Category.ToString()))
                    GlobalServiceCenters.Add(s);
            }

            foreach (var a in Apps)
            {
                if (FuzzySearch.MatchesAny(query, a.App.Name, a.App.Id))
                    GlobalApps.Add(a);
            }

            foreach (var t in AudioTracks)
            {
                if (FuzzySearch.MatchesAny(query, t.Track.Name, t.Track.Description, t.Track.Id))
                    GlobalAudioTracks.Add(t);
            }

            // A 9-a colectie (Etapa 2) — respecta si filtrul OS, ca Produsele
            // (DownloadableResource poarta `supportedOS` in model).
            foreach (var r in _allDownloadResources)
            {
                if (!MatchesOS(r.Resource.SupportedOS)) continue;
                if (FuzzySearch.MatchesAny(query, r.Name, r.Description, r.Resource.Id, r.CategoryLabel))
                    GlobalDownloadResources.Add(r);
            }
        }

        OnPropertyChanged(nameof(HasNoSearchResults));
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

    // Plasata imediat langa categoriile LUT/DCTL/OFX/PowerGrade in sidebar
    // (nu langa Cursuri/Materiale/etc.) — la fel ca pe Mac (SidebarSection.audio,
    // ContentView.swift), chiar daca arhitectural AudioTrack e mai aproape de
    // AppLink (fara PluginType/install/licenta).
    [RelayCommand]
    private void ShowAudioTracks() => CurrentPage = SidebarPage.AudioTracks;

    /// Cele 4 rubrici de Resurse Download (Etapa 2). O singura comanda cu
    /// parametru, nu 4 comenzi separate — butoanele din sidebar trimit
    /// direct pagina tinta.
    [RelayCommand]
    private void ShowDownloadCategory(SidebarPage page) => CurrentPage = page;

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

    /// Deschide "Aplicatiile Mele" si redetecteaza la fiecare intrare —
    /// userul poate fi instalat/dezinstalat ceva intre doua vizite, iar
    /// detectarea din Registry e ieftina (fara retea).
    [RelayCommand]
    private async Task ShowMyApps()
    {
        CurrentPage = SidebarPage.MyApps;
        await MyApps.RefreshAsync();
    }

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

        AudioTracks.Clear();
        foreach (var track in CatalogService.Shared.AudioTracks)
        {
            AudioTracks.Add(new AudioTrackViewModel(track));
        }

        // Resurse Download (Etapa 2) — o singura trecere prin catalog,
        // distribuita in cele 4 colectii per categorie plus lista completa
        // (folosita de cautarea globala si de candidatii pentru licenta).
        _allDownloadResources.Clear();
        DownloadLuts.Clear();
        DownloadSfx.Clear();
        DownloadVfx.Clear();
        DownloadPlugins.Clear();
        foreach (var resource in CatalogService.Shared.DownloadableResources)
        {
            var vm = new DownloadResourceViewModel(resource);
            _allDownloadResources.Add(vm);
            switch (resource.Category)
            {
                case DownloadCategory.Lut: DownloadLuts.Add(vm); break;
                case DownloadCategory.Sfx: DownloadSfx.Add(vm); break;
                case DownloadCategory.Vfx: DownloadVfx.Add(vm); break;
                case DownloadCategory.Plugin: DownloadPlugins.Add(vm); break;
            }
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

        // Rezultatele globale sunt derivate din colectiile de mai sus — daca
        // o cautare e activa in timpul unui refresh de catalog, trebuie
        // recalculate, altfel ar ramane sa arate ViewModel-uri din catalogul
        // vechi (deja inlocuite mai sus).
        RebuildGlobalSearch();
    }
}

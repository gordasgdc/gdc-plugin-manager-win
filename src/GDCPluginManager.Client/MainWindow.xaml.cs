using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using GDCPluginManager.Client.Services;
using GDCPluginManager.Client.ViewModels;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client;

public partial class MainWindow : Window
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

    // Dimensiunea "de referinta" a ferestrei (scara 1.0) - vezi ApplyTextScale.
    // Trebuie sa ramana in sincron cu Height/Width din MainWindow.xaml.
    private const double BaseWindowWidth = 1180;
    private const double BaseWindowHeight = 780;

    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        Log("MainWindow() constructor entered.");

        // Construit explicit in corpul constructorului (nu ca field
        // initializer) ca sa putem loga daca pica exact aici — un field
        // initializer ruleaza inainte de orice cod din constructor si
        // orice exceptie acolo ar fi complet muta in log-ul de mai sus.
        _viewModel = new MainViewModel();
        Log("MainViewModel construit cu succes.");

        InitializeComponent();
        Log("InitializeComponent() finalizat.");

        DataContext = _viewModel;
        Log("DataContext setat. MainWindow() constructor complet.");

        LaunchBannerChecker.Shared.Updated += () => Dispatcher.Invoke(UpdateLaunchBanner);
    }

    /// Banner de lansare (2026-08-31) - vezi LaunchBannerChecker.cs. Actualizat
    /// pe UI thread la fiecare fetch reusit (initial sau din cache offline).
    private void UpdateLaunchBanner()
    {
        var config = LaunchBannerChecker.Shared.Config;
        var image = LaunchBannerChecker.Shared.Image;
        if (config is null || !config.IsDisplayable)
        {
            LaunchBannerGrid.Visibility = Visibility.Collapsed;
            return;
        }
        LaunchBannerTopText.Text = config.TopText;
        LaunchBannerMainText.Text = config.MainText;

        // Pozitia benzii de text (sus/jos) e o optiune aleasa de Cristi din
        // Furnizor (config.TextOnTop), nu fixa in XAML - reordonam cele doua
        // elemente in StackPanel-ul parinte. Port 1:1 al if/else din
        // LaunchOfferBanner.swift (Mac).
        LaunchBannerGrid.Children.Remove(LaunchBannerTextBand);
        LaunchBannerGrid.Children.Remove(LaunchBannerImage);

        // 2026-09-05, port 1:1 al fix-ului Mac: imaginea e OPTIONALA - fara
        // ea, ramane doar banda de text (Image.Source ramane null, Height
        // colapsat la 0 ca sa nu lase spatiu gol).
        if (image is not null)
        {
            LaunchBannerImage.Source = image;
            LaunchBannerImage.Visibility = Visibility.Visible;
            if (config.TextOnTop)
            {
                LaunchBannerGrid.Children.Add(LaunchBannerTextBand);
                LaunchBannerGrid.Children.Add(LaunchBannerImage);
            }
            else
            {
                LaunchBannerGrid.Children.Add(LaunchBannerImage);
                LaunchBannerGrid.Children.Add(LaunchBannerTextBand);
            }
        }
        else
        {
            LaunchBannerImage.Source = null;
            LaunchBannerImage.Visibility = Visibility.Collapsed;
            LaunchBannerGrid.Children.Add(LaunchBannerTextBand);
        }

        LaunchBannerGrid.Visibility = Visibility.Visible;
    }

    /// Mărime text (2026-08-29, CLAUDE.md Partea 1, Regula 24) — port al
    /// selectorului de pe Mac (`dynamicTypeSize`). WPF n-are un echivalent
    /// nativ de "dynamic type" per-text, deci scalăm tot arborele vizual
    /// printr-un `LayoutTransform` pe `RootGrid` — spre deosebire de un
    /// `RenderTransform`, `LayoutTransform` participă la calculul de
    /// layout, deci fereastra chiar are nevoie de mai mult spațiu la
    /// scară >1.0. De-asta redimensionăm și fereastra proporțional
    /// (`BaseWindowWidth/Height` × scară), clampat la aria de lucru a
    /// ecranului curent, ca nimic să nu iasă tăiat în afara vizibilului.
    public void ApplyTextScale(TextScalePreference preference)
    {
        var scale = preference.ScaleFactor();
        RootGrid.LayoutTransform = scale == 1.0 ? Transform.Identity : new ScaleTransform(scale, scale);

        var workArea = SystemParameters.WorkArea;
        var targetWidth = Math.Min(BaseWindowWidth * scale, workArea.Width);
        var targetHeight = Math.Min(BaseWindowHeight * scale, workArea.Height);
        if (targetWidth >= MinWidth) Width = targetWidth;
        if (targetHeight >= MinHeight) Height = targetHeight;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log("Window.Loaded declansat.");
        ApplyTextScale(TextScaleStore.Load());
        try
        {
            await _viewModel.InitializeCommand.ExecuteAsync(null);
            Log("InitializeCommand finalizat cu succes.");
        }
        catch (Exception ex)
        {
            Log($"InitializeCommand a aruncat: {ex}");
        }

        await MaybeShowUpdatePopupAsync();

        _ = LaunchBannerChecker.Shared.RefreshAsync();
        _ = AppPricingFetcher.Shared.RefreshAsync();

        // Onboarding opțional la prima pornire - port 1:1 al
        // "gdcpm_onboarded" din ContentView.swift (Mac). Lipsea complet
        // pe Windows (gasit la audit 2026-08-26); reutilizeaza
        // ProfileEditorWindow ca modal de onboarding - inchiderea ei in
        // orice fel (Salveaza sau X) marcheaza onboarding-ul facut, nu
        // mai reapare la urmatoarele porniri.
        if (!UserProfileStore.Shared.HasOnboarded)
        {
            Views.ProfileEditorWindow.ShowFor(_viewModel);
            UserProfileStore.Shared.MarkOnboarded();
        }
    }

    /// Pop-up modal, pe lânga bannerul din antet (nu în locul lui): bannerul
    /// e discret și poate fi ratat; pop-up-ul întrerupe o singură dată, la
    /// apariția unei versiuni noi, și explică răspicat că nu e self-update
    /// automat. Perechea de pe Mac e alertele din ContentView.swift — dacă
    /// schimbi textul/comportamentul într-o parte, schimbă-l și în cealaltă.
    ///
    /// De ce Wpf.Ui.Controls.MessageBox și nu System.Windows.MessageBox:
    /// cel nativ are doar butoane fixe (Yes/No/OK/Cancel), nu poate arăta
    /// "Descarcă"/"Mai târziu" — cel din Wpf.Ui suportă text custom pe
    /// butoane și se potrivește cu tema aplicației.
    ///
    /// Citește direct din UpdateChecker.Shared.AvailableUpdate (nu din
    /// MainViewModel) — e aceeași sursă pe care InitializeCommand a folosit-o
    /// deja ca să populeze bannerul, deci nu mai trebuie dus un al doilea
    /// obiect prin ViewModel doar pentru popup.
    /// Deschide DependencyPanelWindow — indicatorul 🔴/🟢 din header (vezi
    /// CLAUDE.md, Partea 1, Regula 4). Fereastra citeste live din
    /// _viewModel.AllDependencies (DataContext), deci "Reverifica tot" din
    /// panou actualizeaza fara sa inchida/redeschida nimic.
    private void DependencyBadge_Click(object sender, RoutedEventArgs e)
    {
        Views.DependencyPanelWindow.ShowFor(_viewModel);
    }

    private void ProfileEditor_Click(object sender, RoutedEventArgs e)
    {
        Views.ProfileEditorWindow.ShowFor(_viewModel);
    }

    /// Buton manual "Cauta actualizari" (footer sidebar) - mereu arata
    /// rezultatul real, chiar daca versiunea a fost deja inchisa/dismissed
    /// anterior (altfel butonul manual ar minti "esti la zi" pe o versiune
    /// reala doar respinsa candva). Port 1:1 al comportamentului cerut in
    /// gdc-vault-win/CLAUDE.md pentru acelasi buton.
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        Views.SettingsWindow.ShowFor(this);
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        await UpdateChecker.Shared.CheckAsync();
        // PITFALL FIXED 2026-08-26: citea AvailableUpdate, care e null si
        // pentru "nu exista versiune noua" SI pentru "exista, dar a fost
        // deja respinsa candva" — butonul manual minea "Esti la zi" pe o
        // versiune reala doar inchisa anterior din popup/banner. Reprodus
        // live cu un log real (info.Version=1.3.0, IsNewer=True,
        // dismissed=1.3.0). `LatestInfo` nu e filtrat de dismissal — sursa
        // corecta pentru orice verificare declansata explicit de user.
        // [2026-09-03] BUG REAL, gasit direct din incidentul de azi: pana
        // acum, `info is null` insemna FIE "esti la zi" FIE "verificarea a
        // esuat" (retea sau format nou de update.json neparsabil) — cele
        // doua cazuri aratau IDENTIC userului, "Esti la zi". Un client mai
        // vechi, stricat de o schimbare de format, ar fi confirmat gresit
        // "esti la zi" la infinit, fara nicio indicatie ca trebuie sa
        // descarce manual. `CheckFailed` distinge explicit cele doua cazuri.
        if (UpdateChecker.Shared.CheckFailed)
        {
            var failedBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Nu am putut verifica actualizările",
                Content = "Verificarea automată a eșuat (posibil o schimbare de format sau o problemă de rețea) — " +
                          "nu putem confirma dacă ai ultima versiune. Descarcă manual de pe gordas.dev, ca să fii sigur.",
                PrimaryButtonText = "Deschide gordas.dev",
                CloseButtonText = "Mai târziu",
            };
            var failedResult = await failedBox.ShowDialogAsync();
            if (failedResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                Process.Start(new ProcessStartInfo("https://gordas.dev/") { UseShellExecute = true });
            }
            return;
        }

        var info = UpdateChecker.Shared.LatestInfo;
        if (info is null)
        {
            var upToDateBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Actualizări",
                Content = $"Ești la zi — rulezi deja ultima versiune ({_viewModel.AppVersionDisplay}).",
                CloseButtonText = "OK",
            };
            await upToDateBox.ShowDialogAsync();
            return;
        }
        await MaybeShowUpdatePopupAsync(info);
    }

    private async Task MaybeShowUpdatePopupAsync(UpdateInfo? info = null)
    {
        // Apelul pasiv (din OnLoaded, la lansare) tot respecta dismissal-ul
        // — foloseste AvailableUpdate. Apelul din CheckForUpdates_Click
        // trece explicit LatestInfo (vezi comentariul de mai sus).
        info ??= UpdateChecker.Shared.AvailableUpdate;
        if (info is null) return;

        var url = info.DownloadUrl;

        // Faza 4 (vezi CLAUDE.md Partea 1 Regula 13): rezumatul modificarilor
        // (Release Notes) din update.json (`Changes`) - camp optional,
        // degradeaza elegant daca lipseste. Buton redenumit "Actualizeaza
        // acum" (nu doar "Descarca") - tot NU e self-update silentios (vezi
        // WARNING din UpdateChecker.cs), doar o denumire de actiune clara.
        var content = $"Este disponibilă o nouă versiune! Te rugăm să descarci ultimul installer " +
                      $"și să îl instalezi peste versiunea actuală. (v{info.Version})";
        if (!string.IsNullOrWhiteSpace(info.Changes))
        {
            content += $"\n\nNoutăți:\n{info.Changes}";
        }

        // Update marcat mandatory (docs/update.json): fara "Mai tarziu" —
        // vezi UpdateChecker.Dismiss(), nu se mai persista inchiderea
        // pentru mandatory, deci butonul ar fi oricum inutil aici.
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Actualizare disponibilă",
            Content = content,
            PrimaryButtonText = url is not null ? "Actualizează acum" : string.Empty,
            CloseButtonText = info.Mandatory == true ? string.Empty : "Mai târziu",
        };

        var result = await box.ShowDialogAsync();
        // Nu mai deschide browserul — vezi SelfUpdater.cs. `info` e acelasi
        // obiect citit mai sus din UpdateChecker.Shared.AvailableUpdate.
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary && url is not null)
        {
            await Services.SelfUpdater.DownloadAndInstallAsync(info);
        }

        // Inchiderea popup-ului (din orice buton) ascunde si bannerul si
        // marcheaza versiunea ca "vazuta" — exact ca pe Mac, unde popup-ul
        // si bannerul citesc aceeasi stare (`availableUpdate`), deci
        // inchiderea unuia le inchide pe amandoua. Fara asta, popup-ul ar
        // reaparea la fiecare pornire cat timp userul nu apasa si "Ascunde"
        // separat pe banner.
        _viewModel.DismissUpdateBannerCommand.Execute(null);
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch
        {
            // best-effort
        }
    }
}

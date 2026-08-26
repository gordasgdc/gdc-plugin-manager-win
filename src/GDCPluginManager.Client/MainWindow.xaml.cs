using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GDCPluginManager.Client.ViewModels;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client;

public partial class MainWindow : Window
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

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
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log("Window.Loaded declansat.");
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

    private async Task MaybeShowUpdatePopupAsync()
    {
        var info = UpdateChecker.Shared.AvailableUpdate;
        if (info is null) return;

        var url = info.DownloadUrl.GetValueOrDefault("windows") ?? info.DownloadUrl.Values.FirstOrDefault();

        // Update marcat mandatory (docs/update.json): fara "Mai tarziu" —
        // vezi UpdateChecker.Dismiss(), nu se mai persista inchiderea
        // pentru mandatory, deci butonul ar fi oricum inutil aici.
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Actualizare disponibilă",
            Content = "Este disponibilă o nouă versiune! Te rugăm să descarci ultimul installer " +
                      $"și să îl instalezi peste versiunea actuală. (v{info.Version})",
            PrimaryButtonText = url is not null ? "Descarcă" : string.Empty,
            CloseButtonText = info.Mandatory == true ? string.Empty : "Mai târziu",
        };

        var result = await box.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary && url is not null)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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

using System.IO;
using System.Windows;
using GDCPluginManager.Client.ViewModels;

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

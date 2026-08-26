using System.Diagnostics;
using System.Windows;
using GDCPluginManager.Client.ViewModels;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.Views;

/// Panoul dedicat "Verificare & Dependențe Sistem", deschis la click pe
/// indicatorul 🔴/🟢 din header (vezi CLAUDE.md, Partea 1, Regula 4).
/// Port 1:1 al DependencyPanel.swift (Mac) — fereastra modală separată,
/// la fel ca LightboxWindow (nu un overlay in-canvas). DataContext e
/// direct MainViewModel, deci "Reverifică tot" actualizează live lista
/// (ObservableCollection), fără să închidă/redeschidă fereastra.
public partial class DependencyPanelWindow : Window
{
    private DependencyPanelWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public static void ShowFor(MainViewModel viewModel)
    {
        var window = new DependencyPanelWindow(viewModel);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    private void InstallDependency_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SystemDependency dep } && dep.DownloadUrl is { } url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

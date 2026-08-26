using System.Windows;

namespace GDCPluginManager.Client.Views;

/// Fereastra minimala de progres, afisata cat timp `SelfUpdater` descarca
/// si dezarhiveaza actualizarea. Vezi SelfUpdater.cs pentru fluxul complet.
public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow(string version)
    {
        InitializeComponent();
        TitleText.Text = $"GDC Plugin Manager {version}";

        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, this))
        {
            Owner = owner;
        }
    }

    public void SetStatus(string text) => StatusText.Text = text;
}

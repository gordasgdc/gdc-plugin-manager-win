using System.Windows;
using GDCPluginManager.Client.ViewModels;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.Views;

/// Editorul de Profil Utilizator (Nume/Email opționale) din sidebar —
/// port 1:1 al popover-ului din ProfileSidebarBlock.swift (Mac).
public partial class ProfileEditorWindow : Window
{
    private readonly MainViewModel _viewModel;

    private ProfileEditorWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        NameBox.Text = UserProfileStore.Shared.Name;
        EmailBox.Text = UserProfileStore.Shared.Email;
        MachineIdBox.Text = UserProfileStore.Shared.MachineId;
    }

    public static void ShowFor(MainViewModel viewModel)
    {
        var window = new ProfileEditorWindow(viewModel);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UserProfileStore.Shared.Save(NameBox.Text, EmailBox.Text, sendTelemetry: true);
        _viewModel.NotifyProfileChanged();
        Close();
    }
}

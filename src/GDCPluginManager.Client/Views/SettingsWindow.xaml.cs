using System.Windows;
using System.Windows.Controls;
using GDCPluginManager.Client.Services;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.Views;

/// Fereastra de Setări — "Mărime text" + "Temă" (CLAUDE.md Partea 1,
/// Regula 18/24, port al selectoarelor de pe Mac). Urmează exact tiparul
/// `ProfileEditorWindow` (fereastră modală mică, `ShowFor` static).
public partial class SettingsWindow : Window
{
    private bool _isLoadingSelection;

    private SettingsWindow()
    {
        InitializeComponent();
        _isLoadingSelection = true;
        var current = TextScaleStore.Load();
        foreach (ComboBoxItem item in TextScaleCombo.Items)
        {
            if ((string)item.Tag == current.ToString())
            {
                TextScaleCombo.SelectedItem = item;
                break;
            }
        }
        foreach (ComboBoxItem item in ThemeCombo.Items)
        {
            if ((string)item.Tag == WindowsThemeManager.Current.ToString())
            {
                ThemeCombo.SelectedItem = item;
                break;
            }
        }
        _isLoadingSelection = false;
    }

    public static void ShowFor(MainWindow owner)
    {
        var window = new SettingsWindow();
        if (owner.IsLoaded)
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    private void TextScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Se declanșează și la populare (SelectedItem setat în constructor)
        // — ignorăm acel prim eveniment, ca să nu rescriem fișierul local
        // fără ca userul să fi ales de fapt ceva.
        if (_isLoadingSelection) return;
        if (TextScaleCombo.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<TextScalePreference>((string)item.Tag, out var preference)) return;

        TextScaleStore.Save(preference);
        // Aplicare INSTANT, fără repornire — la fel ca selectorul de temă
        // de pe Mac (Regula 18). Fereastra principală ascultă direct.
        if (Owner is MainWindow mainWindow)
        {
            mainWindow.ApplyTextScale(preference);
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSelection) return;
        if (ThemeCombo.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<AppThemePreference>((string)item.Tag, out var preference)) return;

        // WindowsThemeManager.Apply() salvează local ȘI aplică instant
        // (rescrie dicționarul de culori din Application.Resources) — nicio
        // acțiune suplimentară necesară aici, spre deosebire de mărimea
        // textului (aceea cere apel explicit pe fereastra principală).
        WindowsThemeManager.Apply(preference);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

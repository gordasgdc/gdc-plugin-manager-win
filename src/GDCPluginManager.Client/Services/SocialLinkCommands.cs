using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace GDCPluginManager.Client.Services;

/// Comandă STATICĂ, partajată — deschide orice link social (Facebook/
/// YouTube/Instagram/TikTok/LinkedIn) primit ca `CommandParameter`. Un
/// singur `ICommand` reutilizat de toate cardurile, în loc de câte 5
/// comenzi `[RelayCommand]` per ViewModel (ar fi însemnat 30 de metode
/// identice pe cele 6 tipuri de card) — vezi `SocialLinksPanel.xaml`.
public static class SocialLinkCommands
{
    public static ICommand Open { get; } = new RelayCommand<string>(url =>
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
}

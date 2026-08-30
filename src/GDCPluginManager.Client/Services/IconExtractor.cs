using System.Drawing;
using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GDCPluginManager.Client.Services;

/// Extrage iconita REALA a unui .exe deja instalat pe disc — echivalent
/// `NSWorkspace.icon(forFile:)` de pe Mac (`MyAppsLauncher.swift`). Citește
/// direct iconita asociata executabilului (exact ce face Explorer), NU
/// bundle-uieste nicio siglă terță (Adobe/Blackmagic etc.) în cod — evită
/// orice risc de marcă înregistrată, și rămâne mereu la zi cu orice update
/// de branding al aplicației sursă.
public static class IconExtractor
{
    public static ImageSource? Extract(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return null;
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            // Executabil sters/corupt intre detectare si extragere — cardul
            // cade pe simbolul generic din XAML, nu e o eroare fatala.
            return null;
        }
    }
}

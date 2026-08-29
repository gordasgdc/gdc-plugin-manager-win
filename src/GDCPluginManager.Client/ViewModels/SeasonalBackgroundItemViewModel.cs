using System.Windows;
using System.Windows.Media;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.ViewModels;

/// Un filigran GATA de randat — imaginea deja încărcată + poziția lui
/// tradusă în valori WPF (`HorizontalAlignment`/`VerticalAlignment`/
/// `Thickness`). Port al bibliotecii de pe Mac (`SeasonalBackgroundConfig`
/// + `activeNowDeduplicated`) — `MainViewModel` populează o LISTĂ din
/// aceste obiecte (nu mai un singur `ImageSource`), câte unul pentru
/// fiecare filigran activ, la poziția lui.
public sealed class SeasonalBackgroundItemViewModel
{
    public required ImageSource Source { get; init; }
    public required double Opacity { get; init; }
    public required HorizontalAlignment HorizontalAlignment { get; init; }
    public required VerticalAlignment VerticalAlignment { get; init; }
    public required Thickness Margin { get; init; }

    /// Traduce `SeasonalPosition` (Core) în ancorare WPF + marginea de 48px
    /// — [2026-08-29, mărit de la 24px la cererea lui Cristi] "24pt îl lipea
    /// prea aproape de margine, ca și cum l-ar tăia" — port 1:1 al aceleiași
    /// corecții de pe Mac (`ContentView.swift`, `.padding(... : 48)`).
    public static SeasonalBackgroundItemViewModel Create(SeasonalBackgroundConfig config, ImageSource source)
    {
        const double margin = 48;
        var (h, v, m) = config.Position switch
        {
            SeasonalPosition.BottomTrailing => (HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, margin, margin)),
            SeasonalPosition.BottomLeading => (HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(margin, 0, 0, margin)),
            SeasonalPosition.TopTrailing => (HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, margin, margin, 0)),
            SeasonalPosition.TopLeading => (HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(margin, margin, 0, 0)),
            SeasonalPosition.Center => (HorizontalAlignment.Center, VerticalAlignment.Center, new Thickness(0)),
            _ => (HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, margin, margin)),
        };
        return new SeasonalBackgroundItemViewModel
        {
            Source = source,
            Opacity = config.Opacity,
            HorizontalAlignment = h,
            VerticalAlignment = v,
            Margin = m,
        };
    }
}

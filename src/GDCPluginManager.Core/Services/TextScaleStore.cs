namespace GDCPluginManager.Core.Services;

/// Port 1:1 al `TextScalePreference`/`TextScaleManager` (Mac,
/// `Sources/GDCPluginManagerCore/AppTheme.swift`, 2026-08-29) — CLAUDE.md
/// Partea 1, Regula 24 ("Setare explicita Marime Text, standard obligatoriu
/// alaturi de selectorul de tema").
///
/// DIFERENTA deliberata fata de Mac: SwiftUI are `dynamicTypeSize()`, o
/// infrastructura nativa de accesibilitate care reflowa automat orice text
/// semantic. WPF nu are echivalent direct — aici scalam UNIFORM tot
/// arborele vizual printr-un `ScaleTransform` aplicat ca `LayoutTransform`
/// pe elementul radacina din `MainWindow.xaml` (vezi `MainWindow.xaml.cs`,
/// `ApplyTextScale()`). E un singur punct de schimbare, cu risc minim de
/// rupere fata de umblat prin fiecare `FontSize` hardcodat din XAML.
public enum TextScalePreference
{
    Small,
    Normal,
    Large,
    XLarge,
}

public static class TextScalePreferenceExtensions
{
    /// Factor de scalare aplicat ca `ScaleTransform`. `Normal` = 1.0 (fara
    /// nicio schimbare vizuala fata de azi) — valorile celelalte sunt
    /// simetrice in jurul lui, ca la Mac (`.small`/`.large`/`.xlarge`).
    public static double ScaleFactor(this TextScalePreference preference) => preference switch
    {
        TextScalePreference.Small => 0.9,
        TextScalePreference.Normal => 1.0,
        TextScalePreference.Large => 1.15,
        TextScalePreference.XLarge => 1.3,
        _ => 1.0,
    };

    public static string DisplayName(this TextScalePreference preference) => preference switch
    {
        TextScalePreference.Small => "Mic",
        TextScalePreference.Normal => "Normal",
        TextScalePreference.Large => "Mare",
        TextScalePreference.XLarge => "Foarte mare",
        _ => "Normal",
    };
}

public static class TextScaleStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "text-scale.txt");

    public static TextScalePreference Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return TextScalePreference.Normal;
            var raw = File.ReadAllText(FilePath).Trim();
            return Enum.TryParse<TextScalePreference>(raw, out var value) ? value : TextScalePreference.Normal;
        }
        catch
        {
            return TextScalePreference.Normal;
        }
    }

    public static void Save(TextScalePreference preference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, preference.ToString());
        }
        catch
        {
            // Nescrierea pe disc nu trebuie sa blocheze sesiunea curenta —
            // preferinta ramane doar in memorie pana la urmatoarea pornire.
        }
    }
}

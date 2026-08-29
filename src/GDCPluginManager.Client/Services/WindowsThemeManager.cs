using System.Windows;
using Microsoft.Win32;
using GDCPluginManager.Core.Services;
using WpfTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;

namespace GDCPluginManager.Client.Services;

/// Aplică selectorul de temă System/Light/Dark (CLAUDE.md Partea 1, Regula
/// 18/24) — echivalentul `ThemeManager` de pe Mac
/// (`Sources/GDCPluginManagerCore/AppTheme.swift`, `NSApp.appearance`).
///
/// DIFERENȚĂ REALĂ față de Mac: pe SwiftUI, culorile sistemului
/// (`.background`/`.secondary`) sunt SEMANTICE — schimbă singure valoarea
/// când se schimbă `NSApp.appearance`, fără cod suplimentar. Pe WPF,
/// `Theme.xaml` (stilurile "Shift") folosește culori HARDCODATE — de-aia
/// paleta de culori a fost extrasă separat, în `Theme.Dark.xaml`/
/// `Theme.Light.xaml` (aceleași 8 chei, valori diferite), iar TOATE
/// referințele din stiluri/ferestre au fost convertite la
/// `DynamicResource` (NU `StaticResource` — acela se rezolvă o singură
/// dată, la parse; `DynamicResource` re-rezolvă la fiecare schimbare de
/// dicționar). `Apply()` înlocuiește intrarea de la indexul 2 din
/// `Application.Resources.MergedDictionaries` (vezi App.xaml — ordinea e
/// fixată intenționat: 0=Themes, 1=Controls, 2=Colors, 3=Styles).
public static class WindowsThemeManager
{
    private const string ColorsDictDarkPath = "Styles/Theme.Dark.xaml";
    private const string ColorsDictLightPath = "Styles/Theme.Light.xaml";
    private const int ColorsDictIndex = 2;

    public static AppThemePreference Current { get; private set; } = AppThemeStore.Load();

    /// De apelat o dată, la pornire (App.xaml.cs, `Startup`), și din nou la
    /// fiecare schimbare din `SettingsWindow`.
    public static void Apply(AppThemePreference preference)
    {
        Current = preference;
        AppThemeStore.Save(preference);

        var effectiveDark = preference switch
        {
            AppThemePreference.Dark => true,
            AppThemePreference.Light => false,
            AppThemePreference.System => SystemPrefersDark(),
            _ => true,
        };

        // Tema Wpf.Ui (controale native: SymbolIcon, MessageBox etc.) —
        // separată de paleta noastră "Shift", dar trebuie ținută în sincron
        // ca ferestrele native (ex. Wpf.Ui.Controls.MessageBox de la
        // update-uri) să nu rămână închise la culoare pe fundal deschis.
        WpfThemeManager.Apply(effectiveDark ? WpfTheme.Dark : WpfTheme.Light);

        var app = Application.Current;
        if (app is null) return;
        var dict = app.Resources.MergedDictionaries;
        var newSource = new Uri(effectiveDark ? ColorsDictDarkPath : ColorsDictLightPath, UriKind.Relative);
        var newDict = new ResourceDictionary { Source = newSource };

        if (dict.Count > ColorsDictIndex)
        {
            dict[ColorsDictIndex] = newDict;
        }
        else
        {
            // Defensiv — n-ar trebui să se întâmple dacă App.xaml păstrează
            // ordinea documentată, dar mai bine adăugăm decât să aruncăm.
            dict.Add(newDict);
        }
    }

    /// Apelat o dată la pornire, cu preferința salvată — dacă nu există
    /// încă niciuna, `AppThemeStore.Load()` întoarce `System`.
    public static void ApplyNow() => Apply(AppThemeStore.Load());

    private static bool SystemPrefersDark()
    {
        try
        {
            // HKCU\...\Personalize\AppsUseLightTheme — 0 = Dark, 1 = Light
            // (lipsă cheie => Windows implicit Light pe instalări noi, deci
            // fallback la `1` dacă valoarea nu există).
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int i && i == 0;
        }
        catch
        {
            // Fail-safe pe Dark — e tema implicită de dinainte de Regula 24,
            // deci un eșec de citire a registry-ului nu schimbă nimic
            // vizual pentru un user care n-a atins niciodată setarea asta.
            return true;
        }
    }
}

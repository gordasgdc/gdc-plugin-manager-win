namespace GDCPluginManager.Core.Services;

/// Port al `AppTheme` (Mac, `AppTheme.swift`) — selector explicit
/// System/Light/Dark, CLAUDE.md Partea 1, Regula 18. Definit în Core
/// (fără nicio dependință WPF) ca să rămână simplu de testat; aplicarea
/// efectivă asupra `Application.Resources` trăiește în Client
/// (`WindowsThemeManager.cs`, singurul loc care știe despre WPF).
public enum AppThemePreference
{
    System,
    Light,
    Dark,
}

public static class AppThemeStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "theme.txt");

    public static AppThemePreference Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return AppThemePreference.System;
            var raw = File.ReadAllText(FilePath).Trim();
            return Enum.TryParse<AppThemePreference>(raw, out var value) ? value : AppThemePreference.System;
        }
        catch
        {
            return AppThemePreference.System;
        }
    }

    public static void Save(AppThemePreference preference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, preference.ToString());
        }
        catch
        {
            // Nescrierea pe disc nu trebuie sa blocheze sesiunea curenta.
        }
    }
}

using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al UserProfileStore.swift (Mac) — Nume/Email opționale,
/// persistate local (nu doar trimise o dată către Supabase și uitate),
/// pentru a putea fi afișate în sidebar (vezi CLAUDE.md, Partea 1,
/// Regula 12) și editate oricând.
[SupportedOSPlatform("windows")]
public sealed class UserProfileStore
{
    public static readonly UserProfileStore Shared = new();

    private const string NameKey = "gdcpm_profile_name";
    private const string EmailKey = "gdcpm_profile_email";
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "profile.txt");

    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string MachineId => MachineID.Display;

    public string DisplayName
    {
        get
        {
            var trimmed = Name.Trim();
            return string.IsNullOrEmpty(trimmed) ? "Anonim" : trimmed;
        }
    }

    private UserProfileStore()
    {
        Load();
    }

    /// Salvează local ȘI trimite (fire-and-forget, opțional) către
    /// Supabase — la fel ca varianta Mac. Telemetria rămâne strict
    /// opțională: dacă numele e gol, nu se trimite nimic.
    public void Save(string name, string email, bool sendTelemetry)
    {
        Name = name;
        Email = email;
        Persist();
        if (sendTelemetry && !string.IsNullOrWhiteSpace(name))
        {
            AnalyticsClient.RegisterDevice(name, email);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var lines = File.ReadAllLines(SettingsPath);
            foreach (var line in lines)
            {
                var idx = line.IndexOf('=');
                if (idx < 0) continue;
                var key = line[..idx];
                var value = line[(idx + 1)..];
                if (key == NameKey) Name = value;
                else if (key == EmailKey) Email = value;
            }
        }
        catch { /* fisier lipsa/corupt - ramane profil gol */ }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllLines(SettingsPath, new[] { $"{NameKey}={Name}", $"{EmailKey}={Email}" });
        }
        catch { /* nu bloca UI-ul daca scrierea esueaza */ }
    }
}

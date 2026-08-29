using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;

namespace GDCPluginManager.Client.Services;

/// De unde se afla versiunea PUBLICATA a unei aplicatii GDC.
public enum VersionSourceKind
{
    /// `api.github.com/repos/<owner>/<repo>/releases/latest` -> `tag_name`.
    GitHubRelease,
    /// Un `update.json` propriu, servit de gordas.dev -> campul `version`.
    UpdateJson,
}

/// O aplicatie GDC cunoscuta, cu tot ce trebuie ca s-o gasim instalata pe
/// Windows si sa-i aflam versiunea publicata.
public sealed record KnownGdcApp(
    string Id,
    string Name,
    /// AppId-ul din `installer.iss` al acelei aplicatii. Inno Setup scrie
    /// cheia de dezinstalare la `...\Uninstall\<AppId>_is1` — deci asta e
    /// identificatorul dupa care cautam, nu numele afisat (care se poate
    /// schimba fara sa strice nimic).
    string InnoAppId,
    string ExeName,
    /// Cale de rezerva sub Program Files, folosita doar daca nu gasim nimic
    /// in Registry (ex. o copie dezarhivata manual, fara installer).
    string FallbackRelativeDir,
    VersionSourceKind VersionSource,
    /// "owner/repo" pentru GitHubRelease, URL complet pentru UpdateJson.
    string VersionSourceValue,
    string SiteUrl);

/// O aplicatie GDC gasita instalata pe masina asta.
public sealed record InstalledGdcApp(
    KnownGdcApp App,
    string InstalledVersion,
    string ExecutablePath);

/// Port al `MyAppsLauncher.swift` (Etapa 3, 2026-08-29) — "Aplicatiile Mele".
///
/// DIFERENTA REALA DE PLATFORMA fata de Mac: acolo detectarea se face cu
/// `NSWorkspace.urlForApplication(withBundleIdentifier:)`. Windows n-are un
/// echivalent — sursa de adevar e cheia de dezinstalare scrisa de Inno Setup
/// in Registry:
///     HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\<AppId>_is1
/// Cautam in TREI locuri, pentru ca depinde cum a fost instalata aplicatia:
///   - HKLM, vederea pe 64 de biti (instalare per-masina, cazul obisnuit),
///   - HKLM, vederea pe 32 de biti (`WOW6432Node`, installer pe 32 de biti),
///   - HKCU (instalare doar pentru utilizatorul curent, `PrivilegesRequired=lowest`).
/// Doar `DisplayVersion` din acea cheie e "versiunea instalata" — NU citim
/// versiunea din fisierul .exe (ar putea diferi de ce a inregistrat
/// installer-ul).
///
/// **De ce doar 3 aplicatii, nu 4**: Mac-ul listeaza DataMover, CursorPro GDC,
/// GDC Vault si MediaFlow Monitor. Verificat direct in `~/Developer`
/// (2026-08-29), NU presupus: `CursorPro` are doar `Package.swift`/`.icns`/
/// `build_app.sh` — niciun `.csproj`, `.iss` sau folder Windows. Nu exista
/// build Windows de detectat, deci a fost EXCLUS deliberat. Celelalte trei au
/// `installer.iss` real (identitatile de mai jos sunt copiate din ele, nu
/// ghicite).
[SupportedOSPlatform("windows")]
public static class MyAppsService
{
    /// Identitati preluate VERBATIM din `installer.iss`-ul fiecarui repo
    /// (2026-08-29). AppId apare acolo ca `AppId={{X}}` — Inno interpreteaza
    /// `{{` ca un `{` literal, deci cheia reala din Registry e `{X}_is1`.
    ///
    /// Endpoint-urile de versiune au fost verificate LIVE, nu presupuse:
    /// DataMover -> v2.7.1 (HTTP 200), GDC Vault -> v0.5.4 (HTTP 200),
    /// MediaFlow Monitor -> update.json propriu, 1.8.0, cu `download_url.windows`
    /// prezent (confirma ca exista build Windows).
    public static readonly IReadOnlyList<KnownGdcApp> KnownApps =
    [
        new(
            Id: "datamover",
            Name: "DataMover",
            InnoAppId: "{A4E1C3F0-2F0F-4B0E-9C1A-DATAMOVERSETUP1}",
            ExeName: "DataMover.exe",
            FallbackRelativeDir: @"DataMover",
            VersionSource: VersionSourceKind.GitHubRelease,
            VersionSourceValue: "gordasgdc/datamover",
            SiteUrl: "https://gordas.dev/datamover"),
        new(
            Id: "gdc-vault",
            Name: "GDC Vault",
            InnoAppId: "{E4A9C2D1-7B3F-4E5A-9F0C-GDCVAULT00001}",
            ExeName: "GDCVault.exe",
            FallbackRelativeDir: @"GDC\GDC Vault",
            VersionSource: VersionSourceKind.GitHubRelease,
            VersionSourceValue: "gordasgdc/gdc-vault-win",
            SiteUrl: "https://gordas.dev/gdc-vault"),
        new(
            Id: "mediaflow-monitor",
            Name: "MediaFlow Monitor",
            InnoAppId: "{A3F1D9E4-6B27-4C88-9A45-MEDIAFLOWMON01}",
            ExeName: "MediaFlowMonitor.exe",
            FallbackRelativeDir: @"GDC\MediaFlow Monitor",
            // Are propriul update.json, ca pe Mac — NU citeste GitHub releases.
            VersionSource: VersionSourceKind.UpdateJson,
            VersionSourceValue: "https://gordas.dev/media-flow-monitor/update.json",
            SiteUrl: "https://gordas.dev/media-flow-monitor"),
    ];

    /// Cauta fiecare aplicatie cunoscuta in Registry; cade pe Program Files
    /// doar daca nu gaseste cheia. Complet sincron si local — nicio cerere de
    /// retea aici (verificarea versiunii publicate e separata).
    public static IReadOnlyList<InstalledGdcApp> DetectInstalled()
    {
        var found = new List<InstalledGdcApp>();
        foreach (var app in KnownApps)
        {
            var installed = DetectOne(app);
            if (installed is not null) found.Add(installed);
        }
        return found;
    }

    private static InstalledGdcApp? DetectOne(KnownGdcApp app)
    {
        var subKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.InnoAppId}_is1";

        foreach (var (hive, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser, RegistryView.Default),
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key is null) continue;

                var version = key.GetValue("DisplayVersion") as string ?? "0.0.0";
                var location = key.GetValue("InstallLocation") as string;
                var exe = ResolveExecutable(app, location);
                if (exe is null) continue;

                return new InstalledGdcApp(app, version, exe);
            }
            catch
            {
                // O vedere de registry inaccesibila (politici, permisiuni) nu
                // trebuie sa opreasca cautarea in celelalte doua.
            }
        }

        // Fara cheie de dezinstalare: poate o copie dezarhivata manual.
        var fallback = FallbackExecutable(app);
        return fallback is null ? null : new InstalledGdcApp(app, "0.0.0", fallback);
    }

    private static string? ResolveExecutable(KnownGdcApp app, string? installLocation)
    {
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            var candidate = Path.Combine(installLocation, app.ExeName);
            if (File.Exists(candidate)) return candidate;
        }
        return FallbackExecutable(app);
    }

    private static string? FallbackExecutable(KnownGdcApp app)
    {
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var candidate = Path.Combine(root, app.FallbackRelativeDir, app.ExeName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// Versiunea PUBLICATA a unei aplicatii. Returneaza null la orice esec de
    /// retea/parsare — badge-ul "ACTUALIZARE" pur si simplu nu apare, in loc
    /// sa arate o eroare pentru ceva pur informativ.
    public static async Task<string?> FetchLatestVersionAsync(KnownGdcApp app, HttpClient http)
    {
        try
        {
            if (app.VersionSource == VersionSourceKind.GitHubRelease)
            {
                var url = $"https://api.github.com/repos/{app.VersionSourceValue}/releases/latest";
                using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
                var tag = doc.RootElement.GetProperty("tag_name").GetString();
                // Tag-urile GDC poarta prefixul "v" — fara normalizare,
                // "v2.7.1" s-ar parsa ca 0.7.1 si ar parea mereu mai veche.
                return GDCPluginManager.Core.Services.VersionCompare.NormalizeTag(tag);
            }

            using var updateDoc = JsonDocument.Parse(await http.GetStringAsync(app.VersionSourceValue));
            return updateDoc.RootElement.GetProperty("version").GetString();
        }
        catch
        {
            return null;
        }
    }
}

/// O scurtatura personalizata adaugata de user (orice .exe: Resolve, Premiere,
/// Photoshop...). Port al `CustomLauncher` de pe Mac.
public sealed record CustomLauncher(string Name, string Path);

/// Persistenta scurtaturilor personalizate — fisier JSON local, ca la
/// SearchHistoryStore. Pe Mac echivalentul e `UserDefaults`.
public static class CustomLauncherStore
{
    private static string FilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "custom-launchers.json");

    public static List<CustomLauncher> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<CustomLauncher>>(File.ReadAllBytes(FilePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<CustomLauncher> launchers)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, JsonSerializer.SerializeToUtf8Bytes(launchers.ToList()));
        }
        catch
        {
            // Nescrierea pe disc nu trebuie sa blocheze sesiunea curenta.
        }
    }
}

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Services;

file static class Log
{
    private static readonly string Path_ = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

    public static void Write(string message)
    {
        try { File.AppendAllText(Path_, $"[{DateTime.Now:HH:mm:ss.fff}] [UpdateChecker] {message}\n"); }
        catch { /* best-effort */ }
    }
}

public sealed class UpdateInfo
{
    public required string Version { get; init; }
    public string? ReleaseDate { get; init; }
    public string? Changes { get; init; }

    [JsonPropertyName("download_url")]
    public required string DownloadUrl { get; init; }

    public bool? Mandatory { get; init; }

    [JsonPropertyName("min_version")]
    public string? MinVersion { get; init; }
}

/// [2026-09-03] `update.json` a trecut de la un singur camp `version`
/// comun ambelor platforme la doua sectiuni separate (`mac`/`windows`) —
/// motiv real: un fix Windows-only obliga inainte la un "bump doar de
/// versiune" si pe Mac (fara nicio schimbare de cod), doar ca sa ramana
/// numerele sincronizate — un release Mac inutil de fiecare data cand doar
/// Windows se schimba, si invers. Fiecare platforma isi are acum propriul
/// numar de versiune, complet independent. Pereche: UpdateChecker.swift.
file sealed class UpdateManifest
{
    public UpdateInfo? Mac { get; init; }
    public UpdateInfo? Windows { get; init; }
}

/// Port 1:1 al UpdateChecker.swift — verifica docs/update.json (acelasi
/// pattern JSON static ca pe Mac) pentru o versiune mai noua a aplicatiei.
/// Doar informativ — nu blocheaza nimic, doar ofera un banner + link.
public sealed class UpdateChecker : INotifyPropertyChanged
{
    public static readonly UpdateChecker Shared = new();

    public static readonly Uri UpdateUri = new("https://gordas.dev/update.json");

    private readonly HttpClient _http = HttpClientFactory.Create();
    private const string DismissedVersionKey = "gdcpm_dismissed_update_version";

    public UpdateInfo? AvailableUpdate { get; private set; }

    /// PITFALL FIXED 2026-08-26: `AvailableUpdate` respecta filtrul de
    /// dismissal (corect pentru bannerul/pop-up-ul PASIV, care nu trebuie
    /// sa reaparea pe o versiune deja inchisa). Dar butonul MANUAL "Cauta
    /// actualizari" citea tot `AvailableUpdate` — deci daca o versiune
    /// fusese respinsa o data (chiar din greseala), butonul manual minea
    /// "Esti la zi" desi exista clar o versiune mai noua. Reprodus live:
    /// log real cu `info.Version=1.3.0, IsNewer=True, dismissed=1.3.0`,
    /// urmat de popup/banner ascunse — exact ce comentariul de la
    /// CheckForUpdates_Click avertiza sa NU se intample.
    /// `LatestInfo` e sursa ADEVARATA, necenzurata de dismissal — orice
    /// verificare declansata manual de user trebuie sa citeasca DE AICI,
    /// niciodata din `AvailableUpdate`.
    public UpdateInfo? LatestInfo { get; private set; }

    /// [2026-09-03] Distinct de "ești la zi" — cerut explicit de Cristi,
    /// direct din incidentul de azi: cand formatul `update.json` s-a
    /// schimbat (versiuni Windows/Mac separate), un client mai vechi
    /// (v1.27.0) nu mai putea PARSA raspunsul deloc. `CheckAsync` prindea
    /// exceptia, o scria in log, si se oprea in tacere — `AvailableUpdate`
    /// ramanea `null`, exact ca la "nu exista update", desi realitatea era
    /// "nu am de unde sti daca exista un update". Userul a ramas ore
    /// intregi pe o versiune stricata, fara NICIUN semn ca ceva nu merge.
    ///
    /// Cand verificarea automata esueaza (retea, parsare), `CheckFailed`
    /// devine true — MainWindow arata un banner separat, care trimite
    /// direct la pagina de descarcare (gordas.dev), nu la GitHub (Regula
    /// 20: clientul nu vede niciodata GitHub). Se reseteaza la urmatoarea
    /// verificare reusita, ca sa nu ramana agatat dupa ce problema trece.
    public bool CheckFailed { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0";

    public async Task CheckAsync()
    {
        Log.Write($"CheckAsync start. CurrentVersion={CurrentVersion}");
        UpdateManifest? manifest;
        // Cache-buster pe fiecare cerere: GitHub Pages (Fastly) serveste
        // docs/update.json cu max-age=600 - un nod CDN care a raspuns o
        // data la un update.json vechi il tine cache-uit pana la 10 minute,
        // indiferent ce publicam intre timp. Acelasi pitfall deja documentat
        // pentru coperti (CoverImageStore); aici avem nevoie de proaspat la
        // fiecare check, nu de stabilitate, deci timestamp, nu hash.
        var bustedUri = new Uri($"{UpdateUri}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        try
        {
            using var response = await _http.GetAsync(bustedUri);
            Log.Write($"GET {bustedUri} -> {(int)response.StatusCode} {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                SetCheckFailed(true);
                return;
            }
            var data = await response.Content.ReadAsByteArrayAsync();
            Log.Write($"Body: {System.Text.Encoding.UTF8.GetString(data)}");
            manifest = JsonSerializer.Deserialize<UpdateManifest>(data, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (Exception ex)
        {
            Log.Write($"Exceptie la fetch/parse: {ex}");
            SetCheckFailed(true);
            return;
        }
        if (manifest?.Windows is not { } info)
        {
            Log.Write("Deserializare a intors null sau update.json nu are sectiunea \"windows\".");
            SetCheckFailed(true);
            return;
        }
        SetCheckFailed(false);

        var isNewer = IsNewer(info.Version, CurrentVersion);
        Log.Write($"info.Version={info.Version}, CurrentVersion={CurrentVersion}, IsNewer={isNewer}");
        if (!isNewer)
        {
            AvailableUpdate = null;
            LatestInfo = null;
            Raise(nameof(AvailableUpdate));
            Raise(nameof(LatestInfo));
            return;
        }

        // PITFALL FIXED 2026-08-24: `Mandatory` exista in JSON de la
        // inceput dar nu era citit nicaieri — port 1:1 al fix-ului din
        // UpdateChecker.swift: un update mandatory ignora inchiderea
        // anterioara si reapare la fiecare CheckAsync() (lansare/refresh)
        // cat timp versiunea instalata ramane veche.
        LatestInfo = info;
        Raise(nameof(LatestInfo));

        var dismissed = ReadDismissedVersion();
        Log.Write($"dismissed={dismissed ?? "(none)"}");
        var alreadyDismissed = dismissed == info.Version && info.Mandatory != true;
        AvailableUpdate = alreadyDismissed ? null : info;
        Raise(nameof(AvailableUpdate));
    }

    private void SetCheckFailed(bool failed)
    {
        if (CheckFailed == failed) return;
        CheckFailed = failed;
        Raise(nameof(CheckFailed));
    }

    public void Dismiss()
    {
        if (AvailableUpdate is null) return;
        if (AvailableUpdate.Mandatory != true)
        {
            WriteDismissedVersion(AvailableUpdate.Version);
        }
        AvailableUpdate = null;
        Raise(nameof(AvailableUpdate));
    }

    // Echivalentul UserDefaults.standard pe Windows: un fisier text simplu
    // in AppData\Roaming — evita dependenta de registry pentru o singura valoare.
    private static string DismissedVersionFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GDCPluginManager", "dismissed-update-version.txt");

    private static string? ReadDismissedVersion()
    {
        try { return File.Exists(DismissedVersionFilePath) ? File.ReadAllText(DismissedVersionFilePath).Trim() : null; }
        catch { return null; }
    }

    private static void WriteDismissedVersion(string version)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DismissedVersionFilePath)!);
            File.WriteAllText(DismissedVersionFilePath, version);
        }
        catch { /* nescriere nu trebuie sa blocheze UI-ul */ }
    }

    /// Comparatie simpla pe segmente numerice punctate (1.10.0 > 1.2.0 se
    /// compara numeric per segment, nu lexicografic) — identic cu Swift.
    /// Logica traieste acum in VersionCompare (Core), refolosita si de
    /// "Aplicatiile Mele" (Etapa 3); comportamentul e neschimbat.
    private static bool IsNewer(string a, string b) => VersionCompare.IsNewer(a, b);

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

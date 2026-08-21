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
    public required Dictionary<string, string> DownloadUrl { get; init; }

    public bool? Mandatory { get; init; }

    [JsonPropertyName("min_version")]
    public string? MinVersion { get; init; }
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0";

    public async Task CheckAsync()
    {
        Log.Write($"CheckAsync start. CurrentVersion={CurrentVersion}");
        UpdateInfo? info;
        try
        {
            using var response = await _http.GetAsync(UpdateUri);
            Log.Write($"GET {UpdateUri} -> {(int)response.StatusCode} {response.StatusCode}");
            if (!response.IsSuccessStatusCode) return;
            var data = await response.Content.ReadAsByteArrayAsync();
            Log.Write($"Body: {System.Text.Encoding.UTF8.GetString(data)}");
            info = JsonSerializer.Deserialize<UpdateInfo>(data, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (Exception ex)
        {
            Log.Write($"Exceptie la fetch/parse: {ex}");
            return;
        }
        if (info is null)
        {
            Log.Write("Deserializare a intors null.");
            return;
        }

        var isNewer = IsNewer(info.Version, CurrentVersion);
        Log.Write($"info.Version={info.Version}, CurrentVersion={CurrentVersion}, IsNewer={isNewer}");
        if (!isNewer)
        {
            AvailableUpdate = null;
            Raise(nameof(AvailableUpdate));
            return;
        }

        var dismissed = ReadDismissedVersion();
        Log.Write($"dismissed={dismissed ?? "(none)"}");
        AvailableUpdate = dismissed == info.Version ? null : info;
        Raise(nameof(AvailableUpdate));
    }

    public void Dismiss()
    {
        if (AvailableUpdate is null) return;
        WriteDismissedVersion(AvailableUpdate.Version);
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

    /// Comparatie simpla pe segmente numerice punctate (1.2.0 > 1.10.0 se
    /// compara numeric per segment, nu lexicografic) — identic cu Swift.
    private static bool IsNewer(string a, string b)
    {
        var partsA = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var partsB = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var len = Math.Max(partsA.Length, partsB.Length);
        for (var i = 0; i < len; i++)
        {
            var x = i < partsA.Length ? partsA[i] : 0;
            var y = i < partsB.Length ? partsB[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

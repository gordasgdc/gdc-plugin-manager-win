using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Services;

/// Ce publicam in docs/android.json din repo-ul gdc-plugin-manager.
/// Camp nou acolo => camp nou aici SI in AndroidPane.swift (Mac).
public sealed class AndroidRelease
{
    public required string Version { get; init; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    public string? MinAndroid { get; init; }
    public double? SizeMB { get; init; }

    [JsonPropertyName("apkURL")]
    public required string ApkUrl { get; init; }

    public string? ReleasePage { get; init; }
    public string? Changes { get; init; }
}

/// Port 1:1 al AndroidReleaseLoader din AndroidPane.swift (Mac) — citeste
/// docs/android.json ca sa anunte aplicatia companion de Android (APK).
///
/// ARCHITECTURE NOTE — de ce android.json si NU "releases/latest/download/...":
/// release-urile de APK sunt marcate deliberat ca non-latest, ca `latest` sa
/// ramana al aplicatiei desktop (vezi UpdateChecker). Un link "latest" aici ar
/// descarca instalatorul de Windows, nu APK-ul. Tagul fix exista intr-un SINGUR
/// loc — docs/android.json — si e citit dinamic. Nu hardcoda versiunea aici.
public sealed class AndroidReleaseService : INotifyPropertyChanged
{
    public static readonly AndroidReleaseService Shared = new();

    public static readonly Uri AndroidUri = new("https://gordas.dev/android.json");

    private readonly HttpClient _http = HttpClientFactory.Create();

    private AndroidRelease? _release;
    public AndroidRelease? Release
    {
        get => _release;
        private set { _release = value; OnPropertyChanged(); }
    }

    private bool _failed;
    public bool Failed
    {
        get => _failed;
        private set { _failed = value; OnPropertyChanged(); }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            // NoCache explicit: fara el, un android.json vechi din cache ar
            // ascunde versiuni noi de APK zile intregi.
            using var req = new HttpRequestMessage(HttpMethod.Get, AndroidUri);
            req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Release = JsonSerializer.Deserialize<AndroidRelease>(json, CatalogJsonOptions.Default);
            Failed = Release is null;
        }
        catch
        {
            // Eroarea nu e fatala: panoul isi arata varianta fara link.
            Failed = true;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

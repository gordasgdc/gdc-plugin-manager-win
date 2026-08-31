using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media.Imaging;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.Services;

/// Port 1:1 al `LaunchBannerChecker.swift` (Mac) - fetch + retry + cache
/// local pe disc, controlat de Cristi din Furnizor (Mac) fara recompilare.
public sealed class LaunchBannerChecker
{
    public static readonly LaunchBannerChecker Shared = new();

    private static readonly Uri JsonUrl = new("https://gordas.dev/launch-banner.json");
    private static readonly HttpClient Http = HttpClientFactory.Create();

    public event Action? Updated;
    public LaunchBannerConfig? Config { get; private set; }
    public BitmapImage? Image { get; private set; }

    private static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GDCPluginManager");
    private static string JsonCachePath => Path.Combine(CacheDirectory, "launch-banner-cache.json");
    private static string ImageCachePath => Path.Combine(CacheDirectory, "launch-banner-cache-image");

    public async Task RefreshAsync()
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await Http.GetAsync(JsonUrl);
                if (!response.IsSuccessStatusCode)
                {
                    DiagnosticLog.Write("LaunchBanner", $"HTTP {(int)response.StatusCode} la incercarea {attempt}");
                    if (attempt == 1) await Task.Delay(800);
                    continue;
                }
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var decoded = JsonSerializer.Deserialize<LaunchBannerConfig>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (decoded is null) { continue; }

                Config = decoded;
                Directory.CreateDirectory(CacheDirectory);
                await File.WriteAllBytesAsync(JsonCachePath, bytes);
                await LoadImageAsync(decoded);
                DiagnosticLog.Write("LaunchBanner", $"OK, enabled={decoded.Enabled}");
                Updated?.Invoke();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                DiagnosticLog.Write("LaunchBanner", $"fetch ESUAT la incercarea {attempt}: {ex}");
                if (attempt == 1) await Task.Delay(800);
            }
        }

        // Fetch esuat de 2 ori - cade pe ultimul config cunoscut, cache-uit
        // pe disc (offline-first, ca la restul checker-elor din ecosistem).
        try
        {
            if (File.Exists(JsonCachePath))
            {
                var cached = await File.ReadAllBytesAsync(JsonCachePath);
                var decoded = JsonSerializer.Deserialize<LaunchBannerConfig>(cached, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (decoded is not null)
                {
                    DiagnosticLog.Write("LaunchBanner", $"fetch esuat ({lastError}), fallback pe cache local");
                    Config = decoded;
                    await LoadImageAsync(decoded);
                    Updated?.Invoke();
                    return;
                }
            }
        }
        catch { /* fallback esuat - tratam ca "niciun cache", vezi mai jos */ }
        DiagnosticLog.Write("LaunchBanner", $"fetch esuat ({lastError}) SI niciun cache local - banner ascuns");
    }

    private async Task LoadImageAsync(LaunchBannerConfig config)
    {
        if (!config.IsDisplayable || config.ImageUrl is null)
        {
            Image = null;
            return;
        }
        try
        {
            var response = await Http.GetAsync(config.ImageUrl);
            if (!response.IsSuccessStatusCode)
            {
                DiagnosticLog.Write("LaunchBanner", $"imagine HTTP {(int)response.StatusCode} - fallback cache");
                Image = TryLoadCachedImage();
                return;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Image = BytesToBitmapImage(bytes);
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllBytesAsync(ImageCachePath, bytes);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("LaunchBanner", $"descarcare imagine esuata: {ex}");
            Image = TryLoadCachedImage();
        }
    }

    private static BitmapImage? TryLoadCachedImage()
    {
        if (!File.Exists(ImageCachePath)) return null;
        try { return BytesToBitmapImage(File.ReadAllBytes(ImageCachePath)); }
        catch { return null; }
    }

    /// `BitmapImage.UriSource` (WinINet) e cunoscut nesigur pe acest
    /// ecosistem (bug critic de imagini gasit anterior, vezi CLAUDE.md) -
    /// se decodeaza mereu dintr-un `MemoryStream`, ca in tot restul Client.
    private static BitmapImage BytesToBitmapImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

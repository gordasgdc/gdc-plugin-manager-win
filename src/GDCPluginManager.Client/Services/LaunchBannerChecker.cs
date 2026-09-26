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

    // Faza 6 (port al LaunchBannerChecker.swift): campania activă și imaginile ei, decodate O SINGURĂ DATĂ
    // (BitmapImage înghețat), cu cache pe disc sub nume SHA-256 stabil (fallback offline).
    public PromoBannerCampaign? ActiveCampaign { get; private set; }
    public PromoBannerBitmaps PromoImages { get; private set; } = new();
    private readonly Dictionary<string, BitmapImage> _decoded = new();
    private static string PromoCacheDirectory => Path.Combine(CacheDirectory, "promo-banner-images");

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
                await LoadCampaignAsync(decoded);
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
                    await LoadCampaignAsync(decoded);
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

    private async Task LoadCampaignAsync(LaunchBannerConfig config)
    {
#if DEBUG
        // GDC_PROMO_FIXTURE=text|imageText|image: campanie de test cu grafică generată local (capturi în laborator).
        if (Environment.GetEnvironmentVariable("GDC_PROMO_FIXTURE") is { Length: > 0 } fixture)
        {
            (ActiveCampaign, PromoImages) = PromoBannerFixtures.Make(fixture);
            return;
        }
#endif
        var campaign = config.ActiveCampaign(DateTime.UtcNow);
        if (campaign is null) { ActiveCampaign = null; PromoImages = new(); return; }
        var images = new PromoBannerBitmaps();
        if (campaign.Mode != PromoBannerMode.Text)
        {
            images = images with { Light = await ImageAtAsync(campaign.ImagePath), Dark = await ImageAtAsync(campaign.ImagePathDark) };
            if (campaign.Mode == PromoBannerMode.Image)
                images = images with { WideLight = await ImageAtAsync(campaign.ImagePathWide), WideDark = await ImageAtAsync(campaign.ImagePathWideDark) };
            // Imagine obligatorie, dar nedescărcabilă și fără cache → campania nu se afișează pe jumătate.
            if (images.Light is null)
            {
                DiagnosticLog.Write("LaunchBanner", $"campania {campaign.Id}: imaginea lipseste - banner ascuns");
                ActiveCampaign = null;
                return;
            }
        }
        PromoImages = images;
        ActiveCampaign = campaign;
        DiagnosticLog.Write("LaunchBanner", $"campanie activa {campaign.Id} ({campaign.Mode})");
    }

    private async Task<BitmapImage?> ImageAtAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || CatalogAssets.ImageUrl(path) is not { } url) return null;
        if (_decoded.TryGetValue(path, out var cached)) return cached;
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..16].ToLowerInvariant();
        var file = Path.Combine(PromoCacheDirectory, digest + "-" + Path.GetFileName(url.AbsolutePath));
        byte[]? bytes = null;
        try
        {
            var response = await Http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                bytes = await response.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(PromoCacheDirectory);
                await File.WriteAllBytesAsync(file, bytes);
            }
        }
        catch (Exception ex) { DiagnosticLog.Write("LaunchBanner", $"imagine campanie: {ex.Message} - incerc cache-ul"); }
        if (bytes is null && File.Exists(file)) bytes = await File.ReadAllBytesAsync(file);
        if (bytes is null) return null;
        try
        {
            var image = BytesToBitmapImage(bytes);
            _decoded[path] = image;
            return image;
        }
        catch (Exception ex) { DiagnosticLog.Write("LaunchBanner", $"imagine campanie nedecodabila: {ex.Message}"); return null; }
    }
}

/// Imaginile campaniei active (null = lipsă).
public sealed record PromoBannerBitmaps
{
    public BitmapImage? Light { get; init; }
    public BitmapImage? Dark { get; init; }
    public BitmapImage? WideLight { get; init; }
    public BitmapImage? WideDark { get; init; }
}

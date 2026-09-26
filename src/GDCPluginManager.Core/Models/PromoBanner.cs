using System.Text.Json;
using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Models;

// Port 1:1 al Sources/GDCPluginManagerCore/PromoBanner.swift (Mac, Faza 5) — același contract JSON
// (`launch-banner.json` → `campaigns`), aceleași reguli. Câmpurile vechi rămân; fără campanii → bannerul clasic.

[JsonConverter(typeof(PromoBannerModeConverter))]
public enum PromoBannerMode { Text, ImageText, Image }

/// Un mod necunoscut (versiune viitoare) cade pe Text, nu strică decodarea — ca pe Mac.
public sealed class PromoBannerModeConverter : JsonConverter<PromoBannerMode>
{
    public override PromoBannerMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? reader.GetString() switch
        {
            "imageText" => PromoBannerMode.ImageText,
            "image" => PromoBannerMode.Image,
            _ => PromoBannerMode.Text,
        } : PromoBannerMode.Text;

    public override void Write(Utf8JsonWriter writer, PromoBannerMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch { PromoBannerMode.ImageText => "imageText", PromoBannerMode.Image => "image", _ => "text" });
}

public sealed record PromoBannerText
{
    public string Top { get; init; } = "";
    public string Main { get; init; } = "";
    [JsonIgnore] public bool IsEmpty => string.IsNullOrWhiteSpace(Top) && string.IsNullOrWhiteSpace(Main);
}

public sealed record PromoBannerCampaign
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = "";
    public PromoBannerMode Mode { get; init; } = PromoBannerMode.Text;
    public Dictionary<string, PromoBannerText> Texts { get; init; } = new();
    public string? ImagePath { get; init; }
    public string? ImagePathDark { get; init; }
    public string? ImagePathWide { get; init; }
    public string? ImagePathWideDark { get; init; }
    [JsonPropertyName("linkURL")] public string? LinkUrl { get; init; }
    public Scheduling? Scheduling { get; init; }

    /// Textul pentru limba cerută, altfel RO; null dacă nu există niciunul.
    public PromoBannerText? TextFor(string language)
    {
        if (Texts.TryGetValue(language, out var t) && !t.IsEmpty) return t;
        if (Texts.TryGetValue("ro", out var ro) && !ro.IsEmpty) return ro;
        return null;
    }

    public bool IsActive(DateTime utcNow) =>
        !(Scheduling?.StartDate is { } s && utcNow < s) && !(Scheduling?.EndDate is { } e && utcNow > e);

    [JsonIgnore]
    public bool HasRequiredContent
    {
        get
        {
            var hasImage = !string.IsNullOrEmpty(ImagePath);
            var hasText = Texts.TryGetValue("ro", out var ro) && !string.IsNullOrWhiteSpace(ro.Main);
            return Mode switch
            {
                PromoBannerMode.Text => hasText,
                PromoBannerMode.ImageText => hasText && hasImage,
                _ => hasImage,
            };
        }
    }

    [JsonIgnore] internal DateTime IntervalStart => Scheduling?.StartDate ?? DateTime.MinValue;
}

/// Specificația imaginilor și a layout-ului (aceleași valori ca PromoBannerSpec din Swift).
public static class PromoBannerSpec
{
    public const double StandardAspect = 6, WideAspect = 12, SplitAspect = 3;
    public const double WideMinWidth = 900, TextBandHeight = 56, SplitHeight = 72, MaxImageHeight = 160;

    /// „Doar imagine”: ce variantă și ce înălțime, ca grafica să fie integral vizibilă.
    public static (bool UseWide, double Height) ImageOnlyLayout(double width, bool hasWide)
    {
        var useWide = hasWide && width >= WideMinWidth;
        var aspect = useWide ? WideAspect : StandardAspect;
        return (useWide, Math.Min(Math.Max(width / aspect, 40), MaxImageHeight));
    }
}

/// O listă stricată de campanii devine null — bannerul clasic rămâne vizibil (ca `try?` din Swift).
public sealed class TolerantCampaignListConverter : JsonConverter<List<PromoBannerCampaign>?>
{
    public override List<PromoBannerCampaign>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
        try { return doc.RootElement.Deserialize<List<PromoBannerCampaign>>(options); }
        catch (JsonException) { return null; }
    }

    public override void Write(Utf8JsonWriter writer, List<PromoBannerCampaign>? value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}

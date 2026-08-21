using System.Text.Json;
using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Models;

/// Port 1:1 al PluginType.swift. Valorile string trebuie sa ramana identice
/// cu cele din catalog.json (dctl, lut, fuse, powerGrade, ofx) — acelasi
/// catalog e citit si de clientul Mac si de cel Windows.
public enum PluginType
{
    Dctl,
    Lut,
    Fuse,
    PowerGrade,
    Ofx,
}

/// Mapeaza PluginType <-> stringul exact din JSON (System.Text.Json nu are
/// un naming policy nativ pentru "powerGrade" camelCase pe un enum PascalCase).
public sealed class PluginTypeJsonConverter : JsonConverter<PluginType>
{
    public override PluginType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        return raw switch
        {
            "dctl" => PluginType.Dctl,
            "lut" => PluginType.Lut,
            "fuse" => PluginType.Fuse,
            "powerGrade" => PluginType.PowerGrade,
            "ofx" => PluginType.Ofx,
            _ => throw new JsonException($"Unknown PluginType: {raw}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, PluginType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PluginType.Dctl => "dctl",
            PluginType.Lut => "lut",
            PluginType.Fuse => "fuse",
            PluginType.PowerGrade => "powerGrade",
            PluginType.Ofx => "ofx",
            _ => throw new JsonException($"Unknown PluginType: {value}"),
        });
    }
}

public static class PluginTypeExtensions
{
    public static string Label(this PluginType type) => type switch
    {
        PluginType.Dctl => "DCTL",
        PluginType.Lut => "LUT",
        PluginType.Fuse => "Fuse",
        PluginType.PowerGrade => "PowerGrade",
        PluginType.Ofx => "OFX",
        _ => type.ToString(),
    };

    /// Unde citeste DaVinci Resolve efectiv fiecare tip de fisier, pe Windows.
    /// Echivalentul direct al installDirectory din PluginType.swift:
    /// - DCTL si LUT: acelasi folder (Resolve le distinge dupa extensie).
    /// - Fuse: sub folderul Fusion al lui Resolve.
    /// - PowerGrade: fara folder scanat de Resolve — doar staging local,
    ///   importul real se face prin scripting (vezi PowerGradeImporter, de portat separat).
    /// - OFX: locatia standard cross-host pe Windows (Program Files\Common Files\OFX\Plugins).
    public static string InstallDirectory(this PluginType type)
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        switch (type)
        {
            case PluginType.Dctl:
            case PluginType.Lut:
                return Path.Combine(programData, "Blackmagic Design", "DaVinci Resolve", "Support", "LUT");
            case PluginType.Fuse:
                return Path.Combine(programData, "Blackmagic Design", "DaVinci Resolve", "Support", "Fusion", "Fuses");
            case PluginType.PowerGrade:
                var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                return Path.Combine(videos, "GDC PowerGrades");
            case PluginType.Ofx:
                var commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
                return Path.Combine(commonFiles, "OFX", "Plugins");
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}

/// Port 1:1 al PluginFile.swift — un fisier apartinand unui PluginItem, asa
/// cum sta in repo-ul privat gdc-plugin-manager-files (path complet in repo,
/// fetch prin GitHub Contents API autentificat — vezi InstallManager, de portat).
public sealed record PluginFile
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }

    /// Numele sub care se salveaza fisierul pe disc — ultima componenta din Path.
    [JsonIgnore]
    public string Filename => System.IO.Path.GetFileName(Path);
}

/// Port 1:1 al PluginItem.swift — o intrare din catalog. `Id` e intrarea in
/// hash-ul SHA-512 al licentei (vezi LicenseCore.productHash pe Mac) — NU se
/// schimba niciodata dupa prima vanzare.
public sealed class PluginItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required PluginType Type { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }

    /// Un fisier (un singur DCTL/LUT) sau mai multe (un pack). Vezi IsPack.
    public required IReadOnlyList<PluginFile> Files { get; init; }

    public string? IconSymbol { get; init; }
    public required double PriceEUR { get; init; }

    /// Daca e true, nu e nevoie de licenta — client-ul instaleaza/actualizeaza
    /// direct, gratuit. PriceEUR e ignorat (tratat ca 0) cand e true.
    public bool IsFree { get; init; }

    /// Varianta watermarked de proba a unui produs platit, publicata ca
    /// intrare separata in catalog — mereu insotita de IsFree = true, dar
    /// afisata cu badge "Proba" in loc de "Gratuit".
    public bool IsTrial { get; init; }

    public string? YoutubeURL { get; init; }

    /// Doar pentru OFX: numele exact al folderului .ofx.bundle original.
    /// Resolve identifica un plugin OFX dupa acest nume literal de folder.
    public string? BundleFolderName { get; init; }

    /// True pentru un pack cu mai multe fisiere — se instaleaza intr-un
    /// subfolder propriu, nu liber la radacina folderului Resolve.
    [JsonIgnore]
    public bool IsPack => Files.Count > 1;

    [JsonIgnore]
    public string PriceDisplay => PriceEUR.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });
}

/// Converter custom pentru PluginItem — suporta atat formatul curent (`files`
/// array) cat si formatul vechi cu un singur fisier (`filePath` + `sha256`,
/// fara isFree/isTrial/youtubeURL/bundleFolderName). Port 1:1 al init(from:)
/// din CatalogModel.swift.
public sealed class PluginItemJsonConverter : JsonConverter<PluginItem>
{
    public override PluginItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var id = root.GetProperty("id").GetString()!;
        var name = root.GetProperty("name").GetString()!;
        var type = JsonSerializer.Deserialize<PluginType>(root.GetProperty("type").GetRawText(), options);
        var description = root.GetProperty("description").GetString()!;
        var version = root.GetProperty("version").GetString()!;

        List<PluginFile> files;
        if (root.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
        {
            files = JsonSerializer.Deserialize<List<PluginFile>>(filesElement.GetRawText(), options)!;
        }
        else
        {
            // Intrare legacy, un singur fisier.
            var legacyPath = root.GetProperty("filePath").GetString()!;
            var legacySha = root.GetProperty("sha256").GetString()!;
            files = new List<PluginFile> { new() { Path = legacyPath, Sha256 = legacySha } };
        }

        return new PluginItem
        {
            Id = id,
            Name = name,
            Type = type,
            Description = description,
            Version = version,
            Files = files,
            IconSymbol = root.TryGetProperty("iconSymbol", out var icon) ? icon.GetString() : null,
            PriceEUR = root.GetProperty("priceEUR").GetDouble(),
            IsFree = root.TryGetProperty("isFree", out var free) && free.GetBoolean(),
            IsTrial = root.TryGetProperty("isTrial", out var trial) && trial.GetBoolean(),
            YoutubeURL = root.TryGetProperty("youtubeURL", out var yt) ? yt.GetString() : null,
            BundleFolderName = root.TryGetProperty("bundleFolderName", out var bfn) ? bfn.GetString() : null,
        };
    }

    public override void Write(Utf8JsonWriter writer, PluginItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("name", value.Name);
        writer.WritePropertyName("type");
        JsonSerializer.Serialize(writer, value.Type, options);
        writer.WriteString("description", value.Description);
        writer.WriteString("version", value.Version);
        writer.WritePropertyName("files");
        JsonSerializer.Serialize(writer, value.Files, options);
        if (value.IconSymbol is not null) writer.WriteString("iconSymbol", value.IconSymbol);
        writer.WriteNumber("priceEUR", value.PriceEUR);
        writer.WriteBoolean("isFree", value.IsFree);
        writer.WriteBoolean("isTrial", value.IsTrial);
        if (value.YoutubeURL is not null) writer.WriteString("youtubeURL", value.YoutubeURL);
        if (value.BundleFolderName is not null) writer.WriteString("bundleFolderName", value.BundleFolderName);
        writer.WriteEndObject();
    }
}

/// Port 1:1 al CourseOption.swift — o optiune de pret pe un Course.
public sealed record CourseOption
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string Label { get; init; }
    public required double PriceEUR { get; init; }

    [JsonIgnore]
    public string PriceDisplay => PriceEUR.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });
}

/// Port 1:1 al Course.swift — sesiune rezervabila, nu produs descarcabil.
public sealed record Course
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<CourseOption> Options { get; init; }
}

/// Port 1:1 al AppLink.swift — link catre o alta aplicatie GDC, afisat in
/// sectiunea "Aplicatii" a clientului.
public sealed record AppLink
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string? YoutubeURL { get; init; }
}

/// Port 1:1 al Catalog.swift. `Courses`/`Apps` default la lista goala daca
/// lipsesc din JSON (catalog mai vechi, fara aceste chei inca).
public sealed class Catalog
{
    public string? UpdatedAt { get; init; }
    public IReadOnlyList<PluginItem> Items { get; init; } = [];
    public IReadOnlyList<Course> Courses { get; init; } = [];
    public IReadOnlyList<AppLink> Apps { get; init; } = [];
}

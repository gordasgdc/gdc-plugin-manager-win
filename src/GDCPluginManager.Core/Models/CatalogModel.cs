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
            case PluginType.Lut:
                return Path.Combine(programData, "Blackmagic Design", "DaVinci Resolve", "Support", "LUT");
            case PluginType.Dctl:
                // Bug real (acelasi ca pe Mac): DCTL-urile foloseau exact
                // folderul de LUT-uri, fara subfolderul "DCTL" dedicat pe
                // care Resolve il cauta specific pentru fisierele .dctl.
                return Path.Combine(programData, "Blackmagic Design", "DaVinci Resolve", "Support", "LUT", "DCTL");
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

/// Port 1:1 al CatalogAssets din CatalogModel.swift — de unde se incarca
/// imaginile de prezentare (coperti) ale catalogului.
///
/// SISTEM HIBRID — un singur camp `CoverImage`, doua surse posibile:
///
///   1. UPLOAD LOCAL: cale relativa ("covers/&lt;id&gt;.jpg"). Furnizor (Mac) a
///      comprimat imaginea si a publicat-o in repo-ul public, langa
///      catalog.json. Se rezolva fata de BaseUrl.
///
///   2. URL EXTERN: link absolut ("https://cdn.exemplu.com/x.jpg"), gazduit
///      de furnizor pe CDN-ul lui. Se foloseste ca atare.
///
/// ARCHITECTURE NOTE: NU exista Furnizor pe Windows (repo-ul asta are doar
/// Client + Core) — publicarea si compresia se fac exclusiv de pe Mac, cu
/// ImageProcessor.swift. Aici doar CONSUMAM imaginile, deci nu avem nevoie
/// de nicio librarie de procesare imagini (fara ImageSharp, fara
/// System.Drawing). Daca vreodata apare un Furnizor pe Windows, ATUNCI
/// trebuie portat si ImageProcessor.swift, cu aceleasi praguri.
///
/// WARNING (varianta 2): un URL extern e in afara controlului nostru — daca
/// furnizorul sterge fisierul de pe CDN, coperta dispare fara sa aflam.
/// UI-ul TREBUIE sa trateze esecul de incarcare ca pe un caz normal si sa
/// cada inapoi pe IconSymbol, nu sa arate un chenar spart.
public static class CatalogAssets
{
    /// Acelasi domeniu ca CatalogService / UpdateChecker.
    public static readonly Uri BaseUrl = new("https://gordas.dev/");

    /// True daca valoarea e un link extern, nu o cale relativa gazduita de noi.
    public static bool IsExternal(string? coverImage)
    {
        if (string.IsNullOrEmpty(coverImage)) return false;
        return coverImage.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || coverImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// Transforma valoarea din catalog intr-un URL descarcabil, indiferent de
    /// varianta. null daca produsul nu are inca o coperta.
    public static Uri? ImageUrl(string? coverImage)
    {
        if (string.IsNullOrEmpty(coverImage)) return null;
        // Uri(BaseUrl, x) ignora oricum base-ul cand x e absolut, dar
        // verificam explicit ca sa fie evident ca sistemul hibrid e
        // intentionat, nu un efect secundar.
        return IsExternal(coverImage)
            ? new Uri(coverImage)
            : new Uri(BaseUrl, coverImage);
    }
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

    /// Coperta produsului: cale relativa ("covers/&lt;id&gt;.jpg") sau URL extern
    /// absolut — vezi CatalogAssets. null daca nu are inca una, caz in care
    /// cardul cade pe IconSymbol.
    public string? CoverImage { get; init; }

    /// URL-ul absolut al copertii, gata de incarcat (null daca nu are).
    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);

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
            // Cheie noua (2026-08): intrarile publicate inainte de sistemul
            // de coperti nu o au deloc -> null, fara eroare.
            CoverImage = root.TryGetProperty("coverImage", out var cover) ? cover.GetString() : null,
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
        if (value.CoverImage is not null) writer.WriteString("coverImage", value.CoverImage);
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

    /// Coperta cursului — vezi CatalogAssets. Publicata de pe Mac cu presetul
    /// `.cover` (max 1600px), ca sa se vada detaliul intr-un preview marit.
    public string? CoverImage { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al AppLink.swift — link catre o alta aplicatie GDC, afisat in
/// sectiunea "Aplicatii" a clientului.
public sealed record AppLink
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string? YoutubeURL { get; init; }

    /// Coperta aplicatiei — preset `.icon`, adaugat 2026-08-24. Catalogul
    /// vechi (fara aceasta cheie) decodeaza cu null automat.
    public string? CoverImage { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al EducationalResource.Kind din CatalogModel.swift. CamelCase
/// naming policy pe un enum cu un singur cuvant produce exact "course" /
/// "book" / "guide" (litera intai coborata), identic cu rawValue-ul din
/// Swift — vezi converterul aplicat pe proprietatea Kind mai jos.
public enum EducationalResourceKind
{
    Course,
    Book,
    Guide,
}

public static class EducationalResourceKindExtensions
{
    public static string Label(this EducationalResourceKind kind) => kind switch
    {
        EducationalResourceKind.Course => "Curs",
        EducationalResourceKind.Book => "Carte",
        EducationalResourceKind.Guide => "Ghid",
        _ => kind.ToString(),
    };
}

/// Port 1:1 al EducationalResource.swift — carte/curs online/ghid vandut de
/// o terta parte (Amazon, Gumroad, Udemy...). Spre deosebire de Course, NU
/// e rezervabil prin WhatsApp: clientul leaga direct spre ExternalURL.
public sealed record EducationalResource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter<EducationalResourceKind>))]
    public required EducationalResourceKind Kind { get; init; }
    public required string ExternalURL { get; init; }
    public string? YoutubeURL { get; init; }

    /// Coperta materialului (coperta cartii/cursului) — preset `.cover`.
    public string? CoverImage { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al Event.swift — anunt de comunitate (workshop, curs, festival).
/// DateDisplay e text liber intentionat (ex. "15-17 martie 2026") — nu se
/// face nicio logica de calendar pe el, nici in Furnizor, nici aici.
public sealed record Event
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string DateDisplay { get; init; }
    public required string Location { get; init; }
    public required string ExternalURL { get; init; }
    public string? YoutubeURL { get; init; }

    /// Afisul evenimentului — preset `.cover`. Aici imaginea chiar poarta
    /// informatie (data, program, invitati), deci e cazul in care preview-ul
    /// marit conteaza cel mai mult.
    public string? CoverImage { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al PartnerStore.swift — magazin partener de echipament
/// foto-video, doar nume/descriere/link, nimic de instalat.
public sealed record PartnerStore
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Url { get; init; }

    /// Logo-ul magazinului — preset `.icon` (patrat 512x512). Daca logo-ul e
    /// PNG cu fundal transparent, ImageProcessor (Mac) il pastreaza PNG in
    /// loc sa-l aplatizeze pe alb.
    public string? CoverImage { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al ServiceCategory.swift. JsonStringEnumConverter e case-
/// insensitive la citire, deci "drone"/"camera"/etc. din catalog.json
/// (valorile raw ale enum-ului Swift) se potrivesc direct cu Drone/Camera.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ServiceCategory
{
    Drone,
    Camera,
    Optics,
    Urgent,
}

/// Port 1:1 al ServiceCenter.swift — partener de service/reparatii
/// echipament foto-video (drone/camere/optica/urgente). Doar informativ,
/// niciun fisier, nicio licenta.
public sealed record ServiceCenter
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ServiceCategory Category { get; init; }
    public required string Specialization { get; init; }
    /// Link de contact rapid — `tel:`, `https://wa.me/...` sau `mailto:`.
    public required string ContactURL { get; init; }
    /// Site sau locatie (Google Maps) — optional.
    public string? WebsiteURL { get; init; }
    public string? CoverImage { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al Catalog.swift. Fiecare colectie default la lista goala daca
/// lipseste din JSON (catalog mai vechi, fara acea cheie inca) — System.Text.Json
/// lasa proprietatea la valoarea implicita din initializator cand cheia
/// lipseste complet din payload, exact ca decodeIfPresent(...) ?? [] pe Mac.
public sealed class Catalog
{
    public string? UpdatedAt { get; init; }
    public IReadOnlyList<PluginItem> Items { get; init; } = [];
    public IReadOnlyList<Course> Courses { get; init; } = [];
    public IReadOnlyList<AppLink> Apps { get; init; } = [];
    public IReadOnlyList<EducationalResource> EducationalResources { get; init; } = [];
    public IReadOnlyList<Event> Events { get; init; } = [];
    public IReadOnlyList<PartnerStore> PartnerStores { get; init; } = [];
    public IReadOnlyList<ServiceCenter> ServiceCenters { get; init; } = [];
}

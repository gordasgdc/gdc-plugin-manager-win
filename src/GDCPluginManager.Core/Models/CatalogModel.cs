using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

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

/// Port 1:1 al SupportedOS din CatalogModel.swift — compatibilitatea de
/// sistem de operare a unui produs. Implicit CrossPlatform: toate
/// produsele existente inainte de acest camp chiar ruleaza pe ambele
/// platforme, deci o intrare veche (fara aceasta cheie) trebuie sa
/// decodeze ca "merge oriunde", nu "ascunde-l pe toata lumea".
public enum SupportedOS
{
    MacOS,
    Windows,
    CrossPlatform,
}

public static class SupportedOSExtensions
{
    /// Platforma curenta — mereu Windows in acest client (Mac are
    /// propria implementare .macOS in LicenseCore/CatalogModel.swift).
    public static readonly SupportedOS Current = SupportedOS.Windows;

    public static bool Allows(this SupportedOS self, SupportedOS current) =>
        self == SupportedOS.CrossPlatform || self == current;

    /// Simbol Fluent (Wpf.Ui SymbolRegular) pentru badge-ul de pe card —
    /// inlocuieste emoji-urile 🍎/🪟/🔄 (2026-08-29, cerut explicit, port
    /// 1:1 al fix-ului identic de pe Mac/badgeSymbol, SF Symbols). Vectorial,
    /// tint-uibil, nativ Fluent Design — nu emoji color.
    public static string BadgeSymbol(this SupportedOS self) => self switch
    {
        SupportedOS.MacOS => "DesktopMac24",
        SupportedOS.Windows => "DesktopTower24",
        SupportedOS.CrossPlatform => "ArrowSync24",
        _ => "Circle24",
    };
}

/// Mapeaza SupportedOS <-> stringul exact din JSON ("macOS", "windows",
/// "crossPlatform" — identic cu rawValue-ul enum-ului Swift).
public sealed class SupportedOSJsonConverter : JsonConverter<SupportedOS>
{
    public override SupportedOS Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        return raw switch
        {
            "macOS" => SupportedOS.MacOS,
            "windows" => SupportedOS.Windows,
            "crossPlatform" => SupportedOS.CrossPlatform,
            _ => throw new JsonException($"Unknown SupportedOS: {raw}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, SupportedOS value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            SupportedOS.MacOS => "macOS",
            SupportedOS.Windows => "windows",
            SupportedOS.CrossPlatform => "crossPlatform",
            _ => throw new JsonException($"Unknown SupportedOS: {value}"),
        });
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
    ///
    /// FIX explicit de encoding (2026-08-25): desi `new Uri(base, relativ)`
    /// codifica deja corect spatiile (verificat cu teste .NET dedicate),
    /// nu ne bazam pe comportamentul implicit de combinare pentru ORICE
    /// caracter special posibil intr-un nume de fisier ales liber de
    /// furnizor (apostrof, diacritice etc.) — escapam explicit fiecare
    /// segment din PATH, dar pastram query-ul ("?v=hash") neatins, ca sa
    /// nu stricam parametrul de cache-busting.
    public static Uri? ImageUrl(string? coverImage)
    {
        if (string.IsNullOrEmpty(coverImage)) return null;
        // Uri(BaseUrl, x) ignora oricum base-ul cand x e absolut, dar
        // verificam explicit ca sa fie evident ca sistemul hibrid e
        // intentionat, nu un efect secundar.
        if (IsExternal(coverImage)) return new Uri(coverImage);

        var queryIndex = coverImage.IndexOf('?');
        var path = queryIndex >= 0 ? coverImage[..queryIndex] : coverImage;
        var query = queryIndex >= 0 ? coverImage[queryIndex..] : string.Empty;
        var escapedPath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        return new Uri(BaseUrl, escapedPath + query);
    }
}

/// Converter pentru datele scrise de Furnizorul Mac (Etapa 4, 2026-08-29).
///
/// ATENTIE — CAPCANA DE ENCODING, VERIFICATA PE CATALOGUL LIVE, nu presupusa:
/// Furnizorul serializeaza cu `JSONEncoder()` FARA `dateEncodingStrategy`.
/// Strategia implicita a lui Foundation e `.deferredToDate`, care scrie un
/// `Date` ca NUMAR — secunde (cu fractiuni) de la **2001-01-01 00:00:00 UTC**,
/// referinta `NSDate`/Core Data. NU e ISO-8601 si NU e epoch Unix.
///
/// Dovada directa (catalog.json live, 2026-08-29): `startDate: 809661338.59`.
///   - citit ca epoch Unix  -> 1995-08-29  (absurd)
///   - citit ca referinta 2001 -> 2026-08-29  (exact ziua curenta)
///
/// O legare naiva la `DateTimeOffset`/ISO ar fi plasat TACUT toate datele in
/// 1970/1995, iar `IsActiveNow` ar fi returnat false peste tot -> fiecare
/// element programat (evenimente, oferte, pachete) ar fi devenit INVIZIBIL in
/// client, fara nicio eroare. De-asta conversia e explicita si testata.
public sealed class SwiftDateJsonConverter : JsonConverter<DateTime>
{
    /// 2001-01-01 00:00:00 UTC — referinta `Date`-urilor codate de Swift.
    private static readonly DateTime SwiftEpoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Valorile au fractiuni de secunda (ex. 809661338.592533), deci Double,
        // nu Int64.
        if (reader.TokenType == JsonTokenType.Number)
        {
            return SwiftEpoch.AddSeconds(reader.GetDouble());
        }

        // Tolerant, defensiv: daca vreodata Furnizorul trece pe ISO-8601,
        // citirea nu trebuie sa crape brusc pe clientii deja instalati.
        if (reader.TokenType == JsonTokenType.String
            && DateTime.TryParse(reader.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed;
        }

        throw new JsonException("Data nu a putut fi interpretata (nici numar, nici ISO-8601).");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((value.ToUniversalTime() - SwiftEpoch).TotalSeconds);
}

/// Port 1:1 al Scheduling.swift (Etapa 4, 2026-08-29) — valabilitate temporala
/// optionala (From - To), aplicata pe TOATE modelele. `null` (struct absent) =
/// mereu vizibil, comportament identic cu inainte de Etapa 4.
public sealed record Scheduling
{
    [JsonConverter(typeof(SwiftDateJsonConverter))]
    public DateTime? StartDate { get; init; }

    [JsonConverter(typeof(SwiftDateJsonConverter))]
    public DateTime? EndDate { get; init; }

    /// True daca acest continut ar trebui sa fie vizibil ACUM. Fara StartDate
    /// = deja pornit; fara EndDate = nu expira niciodata.
    ///
    /// Comparat cu ora DISPOZITIVULUI (UTC), nu cu a serverului — exact ca pe
    /// Mac; suficient pentru acest caz, fara sincronizare de timp.
    [JsonIgnore]
    public bool IsActiveNow
    {
        get
        {
            var now = DateTime.UtcNow;
            if (StartDate is { } start && now < start) return false;
            if (EndDate is { } end && now > end) return false;
            return true;
        }
    }

    [JsonIgnore]
    public bool IsEmpty => StartDate is null && EndDate is null;
}

/// Helper pentru filtrarea oricarei colectii dupa `Scheduling` — un singur loc
/// care stie regula "fara scheduling = mereu vizibil", ca sa nu fie rescrisa
/// la fiecare punct de randare.
public static class SchedulingExtensions
{
    public static bool IsVisibleNow(this Scheduling? scheduling) => scheduling?.IsActiveNow ?? true;
}

/// Port 1:1 al SeasonalPosition (Mac, CatalogModel.swift, 2026-08-29) —
/// unde pe ecran se randează un filigran din bibliotecă. `JsonStringEnumConverter`
/// (fără naming policy explicit) e case-insensitive la CITIRE, deci
/// "bottomTrailing" din catalog.json se potrivește direct cu `BottomTrailing`
/// — același tipar ca `ServiceCategory` (vezi comentariul de-acolo).
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeasonalPosition
{
    BottomTrailing,
    BottomLeading,
    TopTrailing,
    TopLeading,
    Center,
}

/// Port 1:1 al `SeasonalBackgroundConfig` (Mac, 2026-08-29) — o intrare din
/// BIBLIOTECA de filigrane sezoniere. Înlocuiește vechiul
/// `Catalog.SeasonalBackground` (String, un singur slot, fără scheduling/
/// poziție/intensitate — Etapa 6). Cheia veche NU mai e scrisă de Furnizor
/// de la prima republicare, dar rămâne pe `Catalog` (mai jos) ca fallback
/// pasiv — nu s-a scris un `JsonConverter` dedicat de migrare pe Windows
/// (spre deosebire de Mac): Windows n-are Furnizor, deci nu publică
/// niciodată formatul vechi, iar catalogul live e deja migrat la formatul
/// plural. Simplificare deliberată, documentată — nu o omisiune.
public sealed record SeasonalBackgroundConfig
{
    public required string Id { get; init; }
    public string Label { get; init; } = "";
    public required string ImagePath { get; init; }
    public Scheduling? Scheduling { get; init; }
    public SeasonalPosition Position { get; init; } = SeasonalPosition.BottomTrailing;
    public bool IsEnabled { get; init; } = true;
    /// Intensitate (opacitate) reglabilă per filigran — 0.07 = valoarea
    /// implicită de dinainte (fostă constantă hardcodată). Retrocompatibil:
    /// lipsă în JSON => System.Text.Json lasă valoarea din initializator.
    public double Opacity { get; init; } = 0.07;

    [JsonIgnore]
    public Uri? ImageUrl => CatalogAssets.ImageUrl(ImagePath);

    [JsonIgnore]
    public bool IsActiveNow => IsEnabled && Scheduling.IsVisibleNow();
}

public static class SeasonalBackgroundConfigExtensions
{
    /// Filigranele active ACUM, deduplicate pe poziție — port 1:1 al
    /// `activeNowDeduplicated` (Mac). La coliziune (mai multe active pe
    /// aceeași poziție), câștigă ULTIMUL din listă — comportament stabil,
    /// niciodată o eroare, identic cu Mac.
    public static IReadOnlyList<SeasonalBackgroundConfig> ActiveNowDeduplicated(
        this IReadOnlyList<SeasonalBackgroundConfig> configs)
    {
        var byPosition = new Dictionary<SeasonalPosition, SeasonalBackgroundConfig>();
        foreach (var config in configs)
        {
            if (config.IsActiveNow) byPosition[config.Position] = config;
        }
        return Enum.GetValues<SeasonalPosition>()
            .Where(byPosition.ContainsKey)
            .Select(p => byPosition[p])
            .ToList();
    }
}

/// Port 1:1 al MapsLink.swift (Etapa 5, 2026-08-29) — link direct catre Google
/// Maps, dintr-un text de adresa liber (nu coordonate). Foloseste endpoint-ul
/// public de cautare (`api=1`), care NU necesita cheie API.
public static class MapsLink
{
    /// Termeni fara sens ca adresa fizica — un curs/eveniment/service "Online"
    /// n-are unde deschide o harta. Semnalat explicit pe Mac: mai bine ascundem
    /// butonul decat sa trimitem la o cautare Google Maps absurda pentru
    /// cuvantul "online".
    ///
    /// Lista e comparata pe textul NORMALIZAT (fara diacritice, lowercase) —
    /// de-asta apar aici si "la distanta"/"distanta" fara diacritice: intrarea
    /// "la distanță" din catalog se normalizeaza exact la "la distanta".
    private static readonly HashSet<string> NonPhysicalTerms = new(StringComparer.Ordinal)
    {
        "online", "webinar", "virtual", "remote", "la distanta", "distanta",
        "zoom", "internet", "n/a", "-",
    };

    /// null daca adresa e goala sau e un termen non-fizic — cardul nu randeaza
    /// deloc butonul de harta in acest caz (nu il dezactiveaza).
    public static Uri? Url(string? address)
    {
        var trimmed = address?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        // Aceeasi normalizare ca la cautare (fara diacritice + lowercase),
        // ca "Online"/"ONLINE"/"la distanță" sa fie toate prinse.
        var normalized = Services.FuzzySearch.Normalize(trimmed);
        if (NonPhysicalTerms.Contains(normalized)) return null;

        return new Uri($"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(trimmed)}");
    }
}

/// Port 1:1 al SocialLinks.swift (Etapa 2, 2026-08-29) — set optional de
/// linkuri catre retelele sociale ale unui produs/resurse. Toate 100%
/// optionale: daca un camp e null, iconita corespunzatoare NU apare deloc pe
/// card (niciodata dezactivata/goala). Struct separat (nu 4 campuri direct pe
/// PluginItem) ca sa fie reutilizat 1:1 pe DownloadableResource si pe
/// celelalte tipuri, fara sa dubleze cele 4 chei peste tot.
public sealed record SocialLinks
{
    // Numele cheilor sunt fixate EXPLICIT (nu lasate pe seama lui
    // PropertyNameCaseInsensitive) — acela ajuta doar la CITIRE; la scriere,
    // fara politica de denumire, System.Text.Json ar emite "FacebookURL"
    // (PascalCase) in loc de "facebookURL", divergent de ce scrie Furnizorul
    // Mac. Windows nu publica azi, dar modelul trebuie sa ramana simetric.
    [JsonPropertyName("facebookURL")]
    public string? FacebookURL { get; init; }

    [JsonPropertyName("youtubeURL")]
    public string? YoutubeURL { get; init; }

    [JsonPropertyName("instagramURL")]
    public string? InstagramURL { get; init; }

    [JsonPropertyName("tiktokURL")]
    public string? TiktokURL { get; init; }

    [JsonPropertyName("linkedinURL")]
    public string? LinkedinUrl { get; init; }

    /// True daca niciunul dintre cele 4 linkuri nu e completat — folosit ca sa
    /// nu afisam un rand gol de iconite pe card.
    [JsonIgnore]
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(FacebookURL) && string.IsNullOrWhiteSpace(YoutubeURL)
        && string.IsNullOrWhiteSpace(InstagramURL) && string.IsNullOrWhiteSpace(TiktokURL)
        && string.IsNullOrWhiteSpace(LinkedinUrl);
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

    /// Compatibilitate OS — vezi SupportedOS. Implicit CrossPlatform
    /// (toate produsele existente pana acum ruleaza pe ambele platforme).
    public SupportedOS SupportedOS { get; init; } = SupportedOS.CrossPlatform;

    /// Link optional catre magazinul/achizitia externa a produsului (Etapa 2)
    /// — afisat ca buton separat pe card doar daca nu e null. Complet
    /// independent de PriceEUR/IsFree (un produs poate fi si vandut direct
    /// prin GDC, si listat extern).
    public string? PurchaseURL { get; init; }

    /// Link optional catre un demo/preview (Etapa 2). Distinct de YoutubeURL:
    /// acela e tutorial de UTILIZARE, acesta e o PREZENTARE dinainte de achizitie.
    public string? DemoURL { get; init; }

    /// Linkuri optionale catre retelele sociale ale produsului (Etapa 2).
    /// null (nu doar toate campurile interne null) pentru orice produs
    /// publicat inainte de Etapa 2.
    public SocialLinks? SocialLinks { get; init; }

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29).
    public Scheduling? Scheduling { get; init; }

    /// Suma de sustinere PROMOTIONALA, temporara — activa DOAR cat timp
    /// `Scheduling` e activ (vezi EffectivePriceEUR).
    ///
    /// CONFORMITATE (Regula 3, Partea 1): ramane 100% DONATIE. Se afiseaza cu
    /// suma veche taiata + badge "Sustinere promotionala" — NICIODATA
    /// "reducere"/"discount"/"-X% OFF". Limbajul de discount e rezervat
    /// EXCLUSIV lui PartnerOffer (relatie comerciala cu un brand tert).
    public double? PromoPriceEUR { get; init; }

    /// URL-ul absolut al copertii, gata de incarcat (null daca nu are).
    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);

    /// Suma de afisat ACUM — cea promotionala daca e setata SI scheduling-ul e
    /// activ; altfel cea normala.
    [JsonIgnore]
    public double EffectivePriceEUR =>
        PromoPriceEUR is { } promo && (Scheduling?.IsActiveNow ?? false) ? promo : PriceEUR;

    [JsonIgnore]
    public bool IsPromoActive => PromoPriceEUR is not null && (Scheduling?.IsActiveNow ?? false);

    [JsonIgnore]
    public string EffectivePriceDisplay =>
        EffectivePriceEUR.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });

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
            // Cheie noua (2026-08-25): intrarile vechi nu o au -> CrossPlatform,
            // pastrand comportamentul actual (instalabil pe ambele platforme).
            SupportedOS = root.TryGetProperty("supportedOS", out var os)
                ? JsonSerializer.Deserialize<SupportedOS>(os.GetRawText(), options)
                : SupportedOS.CrossPlatform,
            // Chei noi (Etapa 2, 2026-08-29) — orice produs publicat inainte
            // nu le are deloc -> null, fara eroare.
            PurchaseURL = root.TryGetProperty("purchaseURL", out var purchase) ? purchase.GetString() : null,
            DemoURL = root.TryGetProperty("demoURL", out var demo) ? demo.GetString() : null,
            SocialLinks = root.TryGetProperty("socialLinks", out var social) && social.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<SocialLinks>(social.GetRawText(), options)
                : null,
            // Chei noi (Etapa 4, 2026-08-29) — retrocompatibile.
            Scheduling = root.TryGetProperty("scheduling", out var sched) && sched.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<Scheduling>(sched.GetRawText(), options)
                : null,
            PromoPriceEUR = root.TryGetProperty("promoPriceEUR", out var promo) && promo.ValueKind == JsonValueKind.Number
                ? promo.GetDouble()
                : null,
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
        writer.WritePropertyName("supportedOS");
        JsonSerializer.Serialize(writer, value.SupportedOS, options);
        if (value.PurchaseURL is not null) writer.WriteString("purchaseURL", value.PurchaseURL);
        if (value.DemoURL is not null) writer.WriteString("demoURL", value.DemoURL);
        if (value.SocialLinks is not null)
        {
            writer.WritePropertyName("socialLinks");
            JsonSerializer.Serialize(writer, value.SocialLinks, options);
        }
        if (value.Scheduling is not null)
        {
            writer.WritePropertyName("scheduling");
            JsonSerializer.Serialize(writer, value.Scheduling, options);
        }
        if (value.PromoPriceEUR is { } promoOut) writer.WriteNumber("promoPriceEUR", promoOut);
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

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }

    /// Linkuri sociale (2026-08-29) — port al extinderii de pe Mac
    /// ("rețelele sociale la toate rubricile"). Retrocompatibil (null).
    public SocialLinks? SocialLinks { get; init; }

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

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }

    /// Linkuri sociale (2026-08-29) — port al extinderii de pe Mac.
    public SocialLinks? SocialLinks { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al AudioTrack.swift — element din sectiunea "Audio", modelat
/// pe AppLink dar cu Description in plus (un fisier/pachet audio are
/// nevoie de mai mult context decat un simplu nume+link: format, metadate).
public sealed record AudioTrack
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Url { get; init; }
    public string? YoutubeURL { get; init; }

    /// Coperta — preset `.icon`, la fel ca AppLink.
    public string? CoverImage { get; init; }

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }

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

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }

    /// Linkuri sociale (2026-08-29) — port al extinderii de pe Mac.
    public SocialLinks? SocialLinks { get; init; }

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

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }


    /// Link Maps generat din `Location`-ul deja existent — Etapa 5. Event NU
    /// primeste un camp `Address` nou (spre deosebire de PartnerStore/
    /// ServiceCenter): locatia lui e deja acolo, exact ca pe Mac.
    [JsonIgnore]
    public Uri? MapsUrl => MapsLink.Url(Location);

    /// Linkuri sociale (2026-08-29) — port al extinderii de pe Mac.
    public SocialLinks? SocialLinks { get; init; }

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

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }


    /// Adresa fizica optionala (text liber) — Etapa 5 (2026-08-29). Daca e
    /// completata, cardul afiseaza un buton care deschide Google Maps cu acest
    /// text cautat. Distincta de site/URL (acela e site-ul, nu locatia).
    public string? Address { get; init; }

    /// Link Maps generat din Address — null daca lipseste sau e "Online" etc.
    [JsonIgnore]
    public Uri? MapsUrl => MapsLink.Url(Address);

    /// Linkuri sociale (2026-08-29) — port al extinderii de pe Mac.
    public SocialLinks? SocialLinks { get; init; }

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

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29). Vezi Scheduling.
    /// null = mereu vizibil (retrocompatibil).
    public Scheduling? Scheduling { get; init; }


    /// Adresa fizica optionala (text liber) — Etapa 5 (2026-08-29). Daca e
    /// completata, cardul afiseaza un buton care deschide Google Maps cu acest
    /// text cautat. Distincta de site/URL (acela e site-ul, nu locatia).
    public string? Address { get; init; }

    /// Link Maps generat din Address — null daca lipseste sau e "Online" etc.
    [JsonIgnore]
    public Uri? MapsUrl => MapsLink.Url(Address);

    /// Linkuri sociale (2026-08-29) — port al extinderii de pe Mac.
    public SocialLinks? SocialLinks { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al DownloadCategory.swift (Etapa 2, 2026-08-29) — categoria unei
/// resurse de download direct. DISTINCTA de PluginType (acela e specific
/// Resolve, cu auto-instalare): resursele astea sunt cross-host (Premiere/
/// FCP/Resolve), userul le descarca si le importa manual, ca AudioTrack.
public enum DownloadCategory
{
    Lut,
    Sfx,
    Vfx,
    Plugin,
}

/// Mapeaza DownloadCategory <-> stringul exact din JSON ("lut"/"sfx"/"vfx"/
/// "plugin" — rawValue-ul enum-ului Swift).
public sealed class DownloadCategoryJsonConverter : JsonConverter<DownloadCategory>
{
    public override DownloadCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        return raw switch
        {
            "lut" => DownloadCategory.Lut,
            "sfx" => DownloadCategory.Sfx,
            "vfx" => DownloadCategory.Vfx,
            "plugin" => DownloadCategory.Plugin,
            _ => throw new JsonException($"Unknown DownloadCategory: {raw}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, DownloadCategory value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            DownloadCategory.Lut => "lut",
            DownloadCategory.Sfx => "sfx",
            DownloadCategory.Vfx => "vfx",
            DownloadCategory.Plugin => "plugin",
            _ => throw new JsonException($"Unknown DownloadCategory: {value}"),
        });
    }
}

public static class DownloadCategoryExtensions
{
    /// Eticheta afisata in sidebar/pe card (RO), pereche a `label`-ului
    /// rezolvat in Client pe Mac.
    public static string Label(this DownloadCategory category) => category switch
    {
        DownloadCategory.Lut => "LUT-uri",
        DownloadCategory.Sfx => "Efecte Audio",
        DownloadCategory.Vfx => "Efecte Video",
        DownloadCategory.Plugin => "Plugin-uri",
        _ => category.ToString(),
    };

    /// Simbol Fluent per categorie — echivalentul `defaultSymbol` (SF Symbols)
    /// de pe Mac. Toate patru sunt nume deja folosite in acest client
    /// (Eyedropper24/PuzzlePiece24) sau confirmate prezente in Wpf.Ui 3.0.5.
    public static string Symbol(this DownloadCategory category) => category switch
    {
        DownloadCategory.Lut => "Eyedropper24",
        DownloadCategory.Sfx => "MusicNote224",
        DownloadCategory.Vfx => "Sparkle24",
        DownloadCategory.Plugin => "PuzzlePiece24",
        _ => "Circle24",
    };
}

/// Port 1:1 al DownloadableResource.swift (Etapa 2, 2026-08-29) — o resursa de
/// download direct (LUT/SFX/VFX/Plugin pentru Premiere Pro, Final Cut Pro sau
/// DaVinci Resolve). NU auto-instaleaza nicaieri, spre deosebire de
/// PluginItem: userul descarca fisierul de la Url si il importa manual.
/// Model 1:1 pe AudioTrack + campurile de linkuri/social din Etapa 2 +
/// SupportedOS + licentiere completa (vezi mai jos).
public sealed class DownloadableResource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required DownloadCategory Category { get; init; }
    public required string Url { get; init; }
    public string? YoutubeURL { get; init; }
    public string? CoverImage { get; init; }
    public SupportedOS SupportedOS { get; init; } = SupportedOS.CrossPlatform;
    public string? PurchaseURL { get; init; }
    public string? DemoURL { get; init; }
    public SocialLinks? SocialLinks { get; init; }

    /// Licentiere — port 1:1 al modelului de pe PluginItem: acces prin Ed25519
    /// (LicenseCore), aceeasi cheie publica din ecosistem, ACELASI flux
    /// WhatsApp + ID masina.
    ///
    /// ATENTIE (capcana reala, verificata in Swift): IsFree decodeaza implicit
    /// TRUE aici, spre deosebire de PluginItem.IsFree care decodeaza FALSE.
    /// Orice resursa publicata INAINTE ca acest camp sa existe trebuie sa
    /// ramana exact ce era — libera, descarcabila direct, fara cod — nu sa
    /// devina silentios "produs platit fara licenta activabila". NU inversa.
    public bool IsFree { get; init; } = true;
    public bool IsTrial { get; init; }
    public double PriceEUR { get; init; }

    /// Valabilitate temporala optionala — Etapa 4 (2026-08-29).
    public Scheduling? Scheduling { get; init; }

    /// Suma de sustinere promotionala — aceleasi reguli de conformitate ca la
    /// PluginItem.PromoPriceEUR (ramane donatie, niciodata "reducere").
    public double? PromoPriceEUR { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);

    [JsonIgnore]
    public string PriceDisplay => PriceEUR.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });

    [JsonIgnore]
    public double EffectivePriceEUR =>
        PromoPriceEUR is { } promo && (Scheduling?.IsActiveNow ?? false) ? promo : PriceEUR;

    [JsonIgnore]
    public bool IsPromoActive => PromoPriceEUR is not null && (Scheduling?.IsActiveNow ?? false);

    [JsonIgnore]
    public string EffectivePriceDisplay =>
        EffectivePriceEUR.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });
}

/// Converter custom pentru DownloadableResource — necesar (nu se poate lasa pe
/// deserializarea implicita) tocmai pentru default-ul TRUE al lui `isFree`
/// cand cheia lipseste complet din JSON.
public sealed class DownloadableResourceJsonConverter : JsonConverter<DownloadableResource>
{
    public override DownloadableResource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        return new DownloadableResource
        {
            Id = root.GetProperty("id").GetString()!,
            Name = root.GetProperty("name").GetString()!,
            Description = root.GetProperty("description").GetString()!,
            Category = JsonSerializer.Deserialize<DownloadCategory>(root.GetProperty("category").GetRawText(), options),
            Url = root.GetProperty("url").GetString()!,
            YoutubeURL = root.TryGetProperty("youtubeURL", out var yt) ? yt.GetString() : null,
            CoverImage = root.TryGetProperty("coverImage", out var cover) ? cover.GetString() : null,
            SupportedOS = root.TryGetProperty("supportedOS", out var os)
                ? JsonSerializer.Deserialize<SupportedOS>(os.GetRawText(), options)
                : SupportedOS.CrossPlatform,
            PurchaseURL = root.TryGetProperty("purchaseURL", out var purchase) ? purchase.GetString() : null,
            DemoURL = root.TryGetProperty("demoURL", out var demo) ? demo.GetString() : null,
            SocialLinks = root.TryGetProperty("socialLinks", out var social) && social.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<SocialLinks>(social.GetRawText(), options)
                : null,
            // Vezi comentariul de pe proprietate: default TRUE, deliberat.
            IsFree = !root.TryGetProperty("isFree", out var free) || free.GetBoolean(),
            IsTrial = root.TryGetProperty("isTrial", out var trial) && trial.GetBoolean(),
            PriceEUR = root.TryGetProperty("priceEUR", out var price) ? price.GetDouble() : 0,
            // Chei noi (Etapa 4, 2026-08-29) — retrocompatibile.
            Scheduling = root.TryGetProperty("scheduling", out var sched) && sched.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<Scheduling>(sched.GetRawText(), options)
                : null,
            PromoPriceEUR = root.TryGetProperty("promoPriceEUR", out var promo) && promo.ValueKind == JsonValueKind.Number
                ? promo.GetDouble()
                : null,
        };
    }

    public override void Write(Utf8JsonWriter writer, DownloadableResource value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("name", value.Name);
        writer.WriteString("description", value.Description);
        writer.WritePropertyName("category");
        JsonSerializer.Serialize(writer, value.Category, options);
        writer.WriteString("url", value.Url);
        if (value.YoutubeURL is not null) writer.WriteString("youtubeURL", value.YoutubeURL);
        if (value.CoverImage is not null) writer.WriteString("coverImage", value.CoverImage);
        writer.WritePropertyName("supportedOS");
        JsonSerializer.Serialize(writer, value.SupportedOS, options);
        if (value.PurchaseURL is not null) writer.WriteString("purchaseURL", value.PurchaseURL);
        if (value.DemoURL is not null) writer.WriteString("demoURL", value.DemoURL);
        if (value.SocialLinks is not null)
        {
            writer.WritePropertyName("socialLinks");
            JsonSerializer.Serialize(writer, value.SocialLinks, options);
        }
        if (value.Scheduling is not null)
        {
            writer.WritePropertyName("scheduling");
            JsonSerializer.Serialize(writer, value.Scheduling, options);
        }
        writer.WriteBoolean("isFree", value.IsFree);
        writer.WriteBoolean("isTrial", value.IsTrial);
        writer.WriteNumber("priceEUR", value.PriceEUR);
        if (value.PromoPriceEUR is { } promoOut) writer.WriteNumber("promoPriceEUR", promoOut);
        writer.WriteEndObject();
    }
}

/// Port 1:1 al PartnerOffer.swift (Etapa 4, 2026-08-29) — o oferta/promotie de
/// la un brand PARTENER (ex. discount la echipament foto/video).
///
/// DECIZIE DE SCOP EXPLICITA, portata de pe Mac: limbajul de "discount"/"%" e
/// PERMIS aici, spre deosebire de produsele/resursele PROPRII GDC. Regula 3
/// (Partea 1) acopera continutul propriu — acesta e o relatie comerciala cu un
/// tert, unde un discount chiar e un discount. Badge-ul rosu de reducere
/// exista DOAR pe acest model.
public sealed record PartnerOffer
{
    public required string Id { get; init; }
    /// Numele brandului/partenerului (ex. "Aputure", "Nanlite").
    public required string BrandName { get; init; }
    public required string Description { get; init; }
    /// Text LIBER de discount, afisat ca badge (ex. "-20%", "2 la pret de 1")
    /// — text, nu procent numeric, ca sa acopere si cazuri non-procentuale.
    public string? DiscountText { get; init; }
    public string? CouponCode { get; init; }
    /// Link catre magazinul/produsul partenerului.
    public required string Url { get; init; }
    public string? YoutubeURL { get; init; }
    public string? CoverImage { get; init; }
    public SocialLinks? SocialLinks { get; init; }
    public Scheduling? Scheduling { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);
}

/// Port 1:1 al BundleItemKind.swift (Etapa 9, 2026-08-29) — tipul de continut
/// referit dintr-un pachet. Un pachet poate combina categorii DIFERITE (ex. un
/// Curs + un pachet de LUT-uri), deci referinta trebuie sa spuna SI unde sa
/// caute ID-ul.
///
/// Cele 6 tipuri sunt exact cele confirmate pe Mac. Oferte Parteneri (terti) si
/// Evenimente (informativ) raman EXCLUSE deliberat — nu sunt produse proprii
/// vandute.
public enum BundleItemKind
{
    /// PluginItem (catalog.items)
    Product,
    /// DownloadableResource (catalog.downloadableResources)
    Download,
    /// Course (catalog.courses)
    Course,
    /// AudioTrack (catalog.audioTracks)
    Audio,
    /// AppLink (catalog.apps)
    App,
    /// EducationalResource (catalog.educationalResources)
    Material,
}

/// Mapeaza BundleItemKind <-> stringul exact din JSON (rawValue-ul Swift).
public sealed class BundleItemKindJsonConverter : JsonConverter<BundleItemKind>
{
    public override BundleItemKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        return raw switch
        {
            "product" => BundleItemKind.Product,
            "download" => BundleItemKind.Download,
            "course" => BundleItemKind.Course,
            "audio" => BundleItemKind.Audio,
            "app" => BundleItemKind.App,
            "material" => BundleItemKind.Material,
            _ => throw new JsonException($"Unknown BundleItemKind: {raw}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, BundleItemKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BundleItemKind.Product => "product",
            BundleItemKind.Download => "download",
            BundleItemKind.Course => "course",
            BundleItemKind.Audio => "audio",
            BundleItemKind.App => "app",
            BundleItemKind.Material => "material",
            _ => throw new JsonException($"Unknown BundleItemKind: {value}"),
        });
    }
}

public sealed record BundleItemRef
{
    public required BundleItemKind Kind { get; init; }
    public required string Id { get; init; }
}

/// Port 1:1 al ProductBundle.swift (Etapa 9, 2026-08-29) — un pachet/bundle.
///
/// DECIZIE ARHITECTURALA DELIBERATA, portata ca atare: e DOAR un construct de
/// PREZENTARE/MARKETING (grupare + pret total afisat), **NU un mecanism nou de
/// licentiere**. Achizitia ramane prin WhatsApp (ca la orice produs), iar
/// Furnizorul genereaza in continuare, manual, cate o licenta per produs
/// inclus. Fluxul de incredere bazat pe donatie+WhatsApp nu se schimba.
public sealed record ProductBundle
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<BundleItemRef> Items { get; init; } = [];
    /// Pretul TOTAL al pachetului (de obicei sub suma preturilor individuale)
    /// — afisat langa suma individuala taiata, pe card.
    public required double BundlePriceEUR { get; init; }
    public string? CoverImage { get; init; }
    public string? YoutubeURL { get; init; }
    public SocialLinks? SocialLinks { get; init; }
    public Scheduling? Scheduling { get; init; }

    [JsonIgnore]
    public Uri? CoverImageUrl => CatalogAssets.ImageUrl(CoverImage);

    [JsonIgnore]
    public string BundlePriceDisplay =>
        BundlePriceEUR.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });
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
    public IReadOnlyList<AudioTrack> AudioTracks { get; init; } = [];
    public IReadOnlyList<EducationalResource> EducationalResources { get; init; } = [];
    public IReadOnlyList<Event> Events { get; init; } = [];
    public IReadOnlyList<PartnerStore> PartnerStores { get; init; } = [];
    public IReadOnlyList<ServiceCenter> ServiceCenters { get; init; } = [];

    /// Resurse de download direct (LUT/SFX/VFX/Plugin) — Etapa 2 (2026-08-29).
    /// Default `[]`: orice catalog publicat inainte decodeaza curat.
    public IReadOnlyList<DownloadableResource> DownloadableResources { get; init; } = [];

    /// Oferte/Promotii de la branduri partenere — Etapa 4 (2026-08-29).
    public IReadOnlyList<PartnerOffer> PartnerOffers { get; init; } = [];

    /// Pachete/Bundle-uri — Etapa 9 (2026-08-29). Default `[]`: retrocompatibil.
    public IReadOnlyList<ProductBundle> ProductBundles { get; init; } = [];

    /// Filigran/fundal sezonier optional pentru Client — Etapa 6 (2026-08-29).
    /// NU un banner mic, ci o imagine mare, discreta, "gravata" in fundalul
    /// ferestrei. Cale relativa ("covers/seasonal/<nume>.svg") sau URL extern
    /// — acelasi sistem hibrid ca CoverImage (vezi CatalogAssets). null = fara
    /// filigran.
    public string? SeasonalBackground { get; init; }

    [JsonIgnore]
    public Uri? SeasonalBackgroundUrl => CatalogAssets.ImageUrl(SeasonalBackground);

    /// Biblioteca de filigrane sezoniere (2026-08-29) — înlocuiește slotul
    /// unic de mai sus (vezi doc-comment-ul de la `SeasonalBackgroundConfig`).
    /// Default `[]`: catalog vechi/fără chei încă => fără niciun filigran,
    /// nu o eroare.
    public IReadOnlyList<SeasonalBackgroundConfig> SeasonalBackgrounds { get; init; } = [];
}

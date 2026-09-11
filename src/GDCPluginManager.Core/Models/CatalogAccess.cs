using System.Text.Json;
using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Models;

// Port 1:1 al CatalogAccess.swift (Regula 31 — paritate Mac/Windows in
// aceeasi sesiune). Vezi fisierul Swift pentru rationamentul complet.
//
// PE SCURT: CatalogAccess e un strat DERIVAT, nu unul paralel. Modelele care
// au deja un camp nativ de gratuit/pret (PluginItem.IsFree,
// DownloadableResource.IsFree, Course.AccessType) raman sursa de adevar;
// Access.Kind/ReferencePriceEUR se consulta DOAR cand acel camp lipseste.
// Grup/etichete/aviz sunt pur aditive.

/// <summary>Tipul de acces — doar pentru modelele fara notiune proprie de gratuit/platit.</summary>
[JsonConverter(typeof(AccessKindJsonConverter))]
public enum AccessKind { Free, Paid, Trial, External }

/// <summary>Gruparea dupa origine/proprietar.</summary>
[JsonConverter(typeof(CatalogGroupJsonConverter))]
public enum CatalogGroup { Gdc, Partners, Mine, External }

// Convertoare explicite, pe tiparul deja folosit de SupportedOSJsonConverter:
// stringul din JSON trebuie sa fie IDENTIC cu rawValue-ul enum-ului Swift,
// altfel Mac-ul si Windows-ul ar scrie acelasi catalog in doua dialecte.
public sealed class AccessKindJsonConverter : JsonConverter<AccessKind>
{
    public override AccessKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "free" => AccessKind.Free,
            "paid" => AccessKind.Paid,
            "trial" => AccessKind.Trial,
            "external" => AccessKind.External,
            var raw => throw new JsonException($"Unknown AccessKind: {raw}")
        };

    public override void Write(Utf8JsonWriter writer, AccessKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            AccessKind.Free => "free",
            AccessKind.Paid => "paid",
            AccessKind.Trial => "trial",
            AccessKind.External => "external",
            _ => throw new JsonException($"Unknown AccessKind: {value}")
        });
}

public sealed class CatalogGroupJsonConverter : JsonConverter<CatalogGroup>
{
    public override CatalogGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "gdc" => CatalogGroup.Gdc,
            "partners" => CatalogGroup.Partners,
            "mine" => CatalogGroup.Mine,
            "external" => CatalogGroup.External,
            var raw => throw new JsonException($"Unknown CatalogGroup: {raw}")
        };

    public override void Write(Utf8JsonWriter writer, CatalogGroup value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            CatalogGroup.Gdc => "gdc",
            CatalogGroup.Partners => "partners",
            CatalogGroup.Mine => "mine",
            CatalogGroup.External => "external",
            _ => throw new JsonException($"Unknown CatalogGroup: {value}")
        });
}

public static class AccessKindExtensions
{
    public static bool IsFree(this AccessKind kind) => kind == AccessKind.Free;

    /// <summary>Cheia de resursa pentru badge — traducerile stau in Client.</summary>
    public static string LocalizationKey(this AccessKind kind) => kind switch
    {
        AccessKind.Free => "access.kind.free",
        AccessKind.Paid => "access.kind.paid",
        AccessKind.Trial => "access.kind.trial",
        AccessKind.External => "access.kind.external",
        _ => "access.kind.paid"
    };
}

public static class CatalogGroupExtensions
{
    public static string LocalizationKey(this CatalogGroup group) => group switch
    {
        CatalogGroup.Gdc => "access.group.gdc",
        CatalogGroup.Partners => "access.group.partners",
        CatalogGroup.Mine => "access.group.mine",
        CatalogGroup.External => "access.group.external",
        _ => "access.group.gdc"
    };

    public static string Label(this CatalogGroup group) => group switch
    {
        CatalogGroup.Gdc => "Proiecte GDC",
        CatalogGroup.Partners => "Parteneri",
        CatalogGroup.Mine => "Resursele mele",
        CatalogGroup.External => "Externe",
        _ => ""
    };
}

/// <summary>
/// Datele NOI, comune tuturor modelelor. Optional peste tot, deci catalogul
/// publicat decodeaza neschimbat. `Tags` are valoare implicita declarata —
/// System.Text.Json lasa proprietatea la valoarea ei cand cheia lipseste din
/// JSON, deci nu e nevoie de un convertor custom ca pe Swift.
/// </summary>
public sealed record CatalogAccess
{
    public AccessKind? Kind { get; init; }
    public double? ReferencePriceEUR { get; init; }
    public string? Note { get; init; }
    public CatalogGroup? Group { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsEmpty =>
        Kind is null && ReferencePriceEUR is null
        && string.IsNullOrWhiteSpace(Note) && Group is null && Tags.Count == 0;

    /// <summary>Formatare unica a pretului — „23 €" / „23.5 €"; 0 si null nu afiseaza nimic.</summary>
    public static string? FormatPrice(double? value)
    {
        if (value is not > 0) return null;
        var v = value.Value;
        var isWhole = Math.Abs(v % 1) < double.Epsilon;
        return (isWhole ? ((int)v).ToString() : v.ToString("0.##")) + " €";
    }
}

/// <summary>Rezultatul aplicarii regulii de precedenta — ce afiseaza UI-ul.</summary>
public sealed record ResolvedAccess
{
    /// <summary>null = NECUNOSCUT, deliberat — apare doar la filtrul „Toate".</summary>
    public bool? IsFree { get; init; }
    public string? PriceDisplay { get; init; }
    public string? KindKey { get; init; }
    public string? Note { get; init; }
    public CatalogGroup? Group { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public SupportedOS? SupportedOS { get; init; }
}

public interface IAccessDescribing
{
    CatalogAccess? Access { get; }
    ResolvedAccess ResolvedAccess { get; }
}

public static class AccessDefaults
{
    /// <summary>
    /// Pasul 2 al precedentei — pentru modelele fara camp nativ de pret/gratuit.
    /// </summary>
    public static ResolvedAccess FromAccessOnly(CatalogAccess? access) => new()
    {
        IsFree = access?.Kind?.IsFree(),
        PriceDisplay = CatalogAccess.FormatPrice(access?.ReferencePriceEUR),
        KindKey = access?.Kind?.LocalizationKey(),
        Note = access?.Note,
        Group = access?.Group,
        Tags = access?.Tags ?? []
    };

    /// <summary>
    /// Pasul 1 al precedentei — pentru modelele CU camp nativ de gratuit/pret.
    /// Access.Kind/ReferencePriceEUR sunt IGNORATE deliberat aici.
    /// </summary>
    public static ResolvedAccess FromNative(bool isFree, double effectivePrice, CatalogAccess? access, SupportedOS? os = null) => new()
    {
        IsFree = isFree,
        PriceDisplay = isFree ? null : CatalogAccess.FormatPrice(effectivePrice),
        KindKey = isFree ? AccessKind.Free.LocalizationKey() : AccessKind.Paid.LocalizationKey(),
        Note = access?.Note,
        Group = access?.Group,
        Tags = access?.Tags ?? [],
        SupportedOS = os
    };
}

// MARK: - Conformari per model (regula de precedenta aplicata)
//
// Port 1:1 al extensiilor din CatalogAccess.swift. C# nu are extensii care sa
// implementeze o interfata, deci `ResolvedAccess` e declarata pe fiecare model
// in CatalogModel.cs si deleaga aici — logica de precedenta traieste tot
// intr-un singur loc, exact ca pe Swift.

public static class AccessResolvers
{
    /// PluginItem / DownloadableResource — au `IsFree` + pret propriu.
    public static ResolvedAccess ForPriced(bool isFree, double effectivePrice, CatalogAccess? access, SupportedOS os) =>
        AccessDefaults.FromNative(isFree, effectivePrice, access, os);

    /// Course — sursa de adevar e CourseAccessType; pretul e cea mai mica optiune.
    public static ResolvedAccess ForCourse(CourseAccessType? accessType, IEnumerable<double> optionPrices, CatalogAccess? access)
    {
        var isFree = accessType == CourseAccessType.Free;
        double? cheapest = optionPrices.Any() ? optionPrices.Min() : null;
        return new ResolvedAccess
        {
            IsFree = isFree,
            PriceDisplay = isFree ? null : CatalogAccess.FormatPrice(cheapest),
            KindKey = isFree ? AccessKind.Free.LocalizationKey() : AccessKind.Paid.LocalizationKey(),
            Note = access?.Note,
            Group = access?.Group,
            Tags = access?.Tags ?? []
        };
    }

    /// AppLink — pasul 1 e Pricing Manager; pretul dinamic se rezolva in Client,
    /// deci aici PriceDisplay ramane null cand exista PricingProductID.
    public static ResolvedAccess ForApp(string? pricingProductID, CatalogAccess? access, SupportedOS? os) => new()
    {
        IsFree = access?.Kind?.IsFree(),
        PriceDisplay = pricingProductID is null ? CatalogAccess.FormatPrice(access?.ReferencePriceEUR) : null,
        KindKey = access?.Kind?.LocalizationKey(),
        Note = access?.Note,
        Group = access?.Group,
        Tags = access?.Tags ?? [],
        SupportedOS = os
    };

    /// ProductBundle — pret propriu, fara notiune de gratuit.
    public static ResolvedAccess ForBundle(double bundlePrice, CatalogAccess? access) =>
        AccessDefaults.FromNative(bundlePrice <= 0, bundlePrice, access);

    /// Tutorial — etichetele proprii se REUNESC cu cele din access (decizie
    /// explicita a lui Cristi: structura veche ramane, access o completeaza).
    public static ResolvedAccess ForTutorial(IReadOnlyList<string> ownTags, CatalogAccess? access)
    {
        var merged = new List<string>(ownTags);
        foreach (var tag in access?.Tags ?? [])
            if (!merged.Contains(tag)) merged.Add(tag);
        return AccessDefaults.FromAccessOnly(access) with { Tags = merged };
    }
}

using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Models;

/// Port 1:1 al `LaunchBannerConfig` (Mac, LaunchBannerModel.swift,
/// 2026-08-31) - banner de lansare publica, controlabil de Cristi din
/// Furnizor (Mac) FARA recompilare, dupa modelul `docs/pricing.json`.
public sealed record LaunchBannerConfig
{
    public bool Enabled { get; init; }

    /// Cale relativa (`covers/launch-banner.jpg?v=...`) sau URL extern -
    /// aceeasi conventie ca `CoverImage` din catalog.
    public string ImagePath { get; init; } = "";
    public string TopText { get; init; } = "";
    public string MainText { get; init; } = "";
    public string UpdatedAt { get; init; } = "";

    /// Valabilitate temporala optionala (2026-08-31) - aceeasi `Scheduling`
    /// folosita de tot restul catalogului. `null` = mereu vizibil cat timp
    /// `Enabled == true`.
    public Scheduling? Scheduling { get; init; }

    /// Pozitia benzii de text solide fata de imagine - true = deasupra,
    /// false = dedesubt (optiune aleasa de Cristi din Furnizor, nu fixa
    /// in cod). Implicit true (deasupra).
    public bool TextOnTop { get; init; } = true;

    /// Faza 5: campanii programate (opțional; clienții vechi îl ignoră). Vezi PromoBanner.cs.
    [JsonConverter(typeof(TolerantCampaignListConverter))]
    public List<PromoBannerCampaign>? Campaigns { get; init; }

    /// Campania afișată acum: activă, cu conținut complet; la suprapunere câștigă începutul cel mai recent (apoi id-ul).
    public PromoBannerCampaign? ActiveCampaign(DateTime utcNow) =>
        !Enabled || Campaigns is null ? null
        : Campaigns.Where(c => c.IsActive(utcNow) && c.HasRequiredContent)
                   .OrderByDescending(c => c.IntervalStart).ThenByDescending(c => c.Id, StringComparer.Ordinal)
                   .FirstOrDefault();

    [JsonIgnore]
    public Uri? ImageUrl => CatalogAssets.ImageUrl(ImagePath);

    // 2026-09-05, port 1:1 al fix-ului Mac: imaginea e OPȚIONALĂ — banda
    // de text trebuie să rămână vizibilă și fără fotografie.
    [JsonIgnore]
    public bool IsDisplayable => Enabled && !string.IsNullOrEmpty(TopText)
        && !string.IsNullOrEmpty(MainText) && (Scheduling?.IsActiveNow ?? true);
}

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

    [JsonIgnore]
    public Uri? ImageUrl => CatalogAssets.ImageUrl(ImagePath);

    // 2026-09-05, port 1:1 al fix-ului Mac: imaginea e OPȚIONALĂ — banda
    // de text trebuie să rămână vizibilă și fără fotografie.
    [JsonIgnore]
    public bool IsDisplayable => Enabled && !string.IsNullOrEmpty(TopText)
        && !string.IsNullOrEmpty(MainText) && (Scheduling?.IsActiveNow ?? true);
}

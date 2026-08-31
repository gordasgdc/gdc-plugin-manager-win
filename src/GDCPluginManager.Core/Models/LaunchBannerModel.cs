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

    [JsonIgnore]
    public Uri? ImageUrl => CatalogAssets.ImageUrl(ImagePath);

    [JsonIgnore]
    public bool IsDisplayable => Enabled && ImageUrl is not null && !string.IsNullOrEmpty(TopText)
        && !string.IsNullOrEmpty(MainText) && (Scheduling?.IsActiveNow ?? true);
}

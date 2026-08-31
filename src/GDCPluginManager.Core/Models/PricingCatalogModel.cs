using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GDCPluginManager.Core.Models;

/// Port 1:1 al `PricingCatalogModel.swift` (Mac) — model public al
/// `pricing.json` (Regula 27), servit static la `https://gordas.dev/pricing.json`.
public sealed class PricingCatalog
{
    [JsonPropertyName("products")]
    public Dictionary<string, ProductPricing> Products { get; set; } = new();
}

public sealed class ProductPricing
{
    [JsonPropertyName("basePrice")]
    public double BasePrice { get; set; }
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "EUR";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("promoSchedule")]
    public List<PricingPromo> PromoSchedule { get; set; } = new();

    [JsonIgnore]
    public PricingPromo? ActivePromo => PromoSchedule.FirstOrDefault(p => p.IsActiveNow);
    [JsonIgnore]
    public double EffectivePrice => ActivePromo?.Price ?? BasePrice;
}

public sealed class PricingPromo
{
    [JsonPropertyName("price")]
    public double Price { get; set; }
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
    [JsonPropertyName("startsAt")]
    public DateTimeOffset StartsAt { get; set; }
    [JsonPropertyName("endsAt")]
    public DateTimeOffset EndsAt { get; set; }
    [JsonPropertyName("showCountdown")]
    public bool ShowCountdown { get; set; }

    [JsonIgnore]
    public bool IsActiveNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now >= StartsAt && now <= EndsAt;
        }
    }

    /// Convertit la un `Scheduling` — permite reutilizarea badge-ului de
    /// countdown deja existent, fara cod UI nou.
    public Scheduling AsScheduling() => new()
    {
        StartDate = StartsAt.UtcDateTime,
        EndDate = EndsAt.UtcDateTime,
        ShowCountdown = ShowCountdown
    };
}

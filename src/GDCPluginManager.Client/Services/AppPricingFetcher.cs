using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.Services;

/// Port 1:1 al `AppPricingFetcher.swift` (Mac) — fetch la lansare al
/// `pricing.json` (Regula 27), folosit de cardurile din „Aplicatii” ca sa
/// arate pret/oferta/countdown, la fel cum apare deja la LUT/DCTL/PowerGrade
/// (`PriceEUR`/`PromoPriceEUR`). Fail-open: un card fara `PricingProductID`
/// sau fara raspuns de la gordas.dev ramane neschimbat, niciodata gol/eronat.
public sealed class AppPricingFetcher
{
    public static readonly AppPricingFetcher Shared = new();

    private static readonly Uri PricingUrl = new("https://gordas.dev/pricing.json");

    public PricingCatalog? Catalog { get; private set; }
    public event Action? Updated;

    private AppPricingFetcher() { }

    public async Task RefreshAsync()
    {
        try
        {
            using var client = HttpClientFactory.Create();
            var response = await client.GetAsync(PricingUrl);
            if (!response.IsSuccessStatusCode) return;
            var catalog = await response.Content.ReadFromJsonAsync<PricingCatalog>();
            if (catalog == null) return;
            Catalog = catalog;
            Updated?.Invoke();
        }
        catch
        {
            // Fail-open - cardurile raman neschimbate.
        }
    }
}

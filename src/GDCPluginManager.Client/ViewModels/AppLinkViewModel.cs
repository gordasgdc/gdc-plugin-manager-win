using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al AppCard din ContentView.swift — link catre o alta aplicatie
/// GDC (DataMover, CursorPro etc.), fara descriere in model (AppLink.swift
/// nu are camp de descriere — doar id/name/url/youtubeURL), doar nume +
/// buton Deschide + iconita optionala de tutorial.
public sealed partial class AppLinkViewModel : ObservableObject
{
    public AppLink App { get; }

    public AppLinkViewModel(AppLink app)
    {
        App = app;
        // Dupa atribuirea de mai sus: `App` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp
        // (acelasi pattern ca PartnerStoreViewModel).
        Cover = new CoverViewModel(app.CoverImageUrl, app.Name);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
        AppPricingFetcher.Shared.Updated += () =>
        {
            OnPropertyChanged(nameof(CountdownText));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(PromoPriceText));
            OnPropertyChanged(nameof(HasPromo));
            OnPropertyChanged(nameof(HasPrice));
        };
    }

    // Pret dinamic (Regula 27, 2026-08-31) - vezi AppPricingFetcher. Un
    // card fara `PricingProductID` (Clapperboard Digital, GDC Metadata
    // View Premium etc.) sau fara raspuns de la gordas.dev ramane
    // NESCHIMBAT - fail-open, nu un card gol/eronat.
    private Core.Models.ProductPricing? Pricing =>
        App.PricingProductID is { } id && AppPricingFetcher.Shared.Catalog is { } catalog && catalog.Products.TryGetValue(id, out var p)
            ? p : null;

    public bool HasPrice => Pricing != null;
    public bool HasPromo => Pricing?.ActivePromo != null;
    public string? PriceText => Pricing is { } p ? FormatPrice(p.BasePrice) : null;
    public string? PromoPriceText => Pricing?.ActivePromo is { } promo ? FormatPrice(promo.Price) : null;

    private static string FormatPrice(double value)
    {
        var isWhole = value % 1 == 0;
        return $"{(isWhole ? ((long)value).ToString() : value.ToString())} €";
    }

    /// Badge "Mai sunt Xz Yh" - preferă fereastra de preț activă (Regula 27)
    /// dacă există, altfel valabilitatea temporală proprie a cardului
    /// (Scheduling din catalog.json, ca înainte).
    public string? CountdownText => Pricing?.ActivePromo?.AsScheduling().CountdownText ?? App.Scheduling?.CountdownText;

    public string Name => App.Name;
    public bool HasYoutube => !string.IsNullOrWhiteSpace(App.YoutubeURL);

    /// Coperta cardului + acțiunea de mărire. Vezi CoverViewModel: o
    /// singură implementare, folosită de toate tipurile de card.
    public CoverViewModel Cover { get; }

    [RelayCommand]
    private void Open() => Process.Start(new ProcessStartInfo(App.Url) { UseShellExecute = true });

    [RelayCommand]
    private void OpenTutorial()
    {
        if (!string.IsNullOrWhiteSpace(App.YoutubeURL))
        {
            Process.Start(new ProcessStartInfo(App.YoutubeURL) { UseShellExecute = true });
        }
    }
}

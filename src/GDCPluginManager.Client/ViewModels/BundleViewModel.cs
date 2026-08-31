using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Un element inclus intr-un pachet, rezolvat LIVE din catalog.
public sealed record BundleEntryViewModel(string Name, string KindLabel, string Symbol);

/// Port 1:1 al BundleCard din ContentView.swift (Etapa 9, 2026-08-29).
///
/// Pachetul e DOAR un construct de prezentare: gruparea + pretul total.
/// Achizitia reuseste exact tiparul WhatsApp deja folosit de
/// ProductViewModel.Buy() — nu exista un mecanism nou de licentiere.
public sealed partial class BundleViewModel : ObservableObject
{
    public ProductBundle Bundle { get; }

    /// Produsele incluse, rezolvate din catalog la construirea cardului.
    public ObservableCollection<BundleEntryViewModel> Entries { get; } = [];

    /// Suma preturilor individuale ale elementelor incluse care AU un pret
    /// propriu in model (produse + resurse download + cursuri). Audio,
    /// Aplicatii si Materiale nu au pret in model — apar in lista de continut,
    /// dar NU contribuie la suma, exact ca pe Mac.
    private double _individualTotal;

    public BundleViewModel(ProductBundle bundle)
    {
        Bundle = bundle;
        Cover = new CoverViewModel(bundle.CoverImageUrl, bundle.Name);
        ResolveEntries();
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Bundle.Scheduling?.CountdownText;

    public CoverViewModel Cover { get; }

    public string Name => Bundle.Name;
    public string Description => Bundle.Description;
    public string BundlePriceDisplay => Bundle.BundlePriceDisplay;

    /// Suma individuala, afisata TAIATA langa pretul pachetului — doar daca e
    /// strict mai mare (altfel un "pachet mai scump decat suma partilor" ar
    /// arata absurd).
    public string IndividualTotalDisplay =>
        _individualTotal.ToString("C", new System.Globalization.CultureInfo("ro-RO") { NumberFormat = { CurrencySymbol = "EUR" } });

    public bool ShowIndividualTotal => _individualTotal > Bundle.BundlePriceEUR;

    public bool HasYoutube => !string.IsNullOrWhiteSpace(Bundle.YoutubeURL);

    /// Rezolva fiecare referinta din pachet in obiectul real din catalog.
    ///
    /// REGULA IMPORTANTA (portata ca atare): un ID care nu mai exista in
    /// catalog (produs retras intre timp) e OMIS SILENTIOS — nu crapa cardul
    /// si nu afiseaza un rand gol/eroare. Pachetul ramane utilizabil cu ce a
    /// mai ramas din el.
    private void ResolveEntries()
    {
        var catalog = CatalogService.Shared;
        foreach (var reference in Bundle.Items)
        {
            switch (reference.Kind)
            {
                case BundleItemKind.Product:
                    if (catalog.Items.FirstOrDefault(x => x.Id == reference.Id) is { } product)
                    {
                        Entries.Add(new BundleEntryViewModel(product.Name, "Produs", "Wand24"));
                        _individualTotal += product.EffectivePriceEUR;
                    }
                    break;

                case BundleItemKind.Download:
                    if (catalog.DownloadableResources.FirstOrDefault(x => x.Id == reference.Id) is { } download)
                    {
                        Entries.Add(new BundleEntryViewModel(download.Name, download.Category.Label(), download.Category.Symbol()));
                        _individualTotal += download.EffectivePriceEUR;
                    }
                    break;

                case BundleItemKind.Course:
                    if (catalog.Courses.FirstOrDefault(x => x.Id == reference.Id) is { } course)
                    {
                        Entries.Add(new BundleEntryViewModel(course.Name, "Curs", "HatGraduation24"));
                        // Un curs are mai multe optiuni de pret — luam cea mai
                        // mica, ca suma individuala sa fie o estimare
                        // CONSERVATOARE (niciodata umflata artificial).
                        if (course.Options.Count > 0) _individualTotal += course.Options.Min(o => o.PriceEUR);
                    }
                    break;

                case BundleItemKind.Audio:
                    if (catalog.AudioTracks.FirstOrDefault(x => x.Id == reference.Id) is { } audio)
                    {
                        // Fara pret propriu in model — apare in lista, nu in suma.
                        Entries.Add(new BundleEntryViewModel(audio.Name, "Audio", "MusicNote224"));
                    }
                    break;

                case BundleItemKind.App:
                    if (catalog.Apps.FirstOrDefault(x => x.Id == reference.Id) is { } app)
                    {
                        Entries.Add(new BundleEntryViewModel(app.Name, "Aplicatie", "Apps24"));
                    }
                    break;

                case BundleItemKind.Material:
                    if (catalog.EducationalResources.FirstOrDefault(x => x.Id == reference.Id) is { } material)
                    {
                        Entries.Add(new BundleEntryViewModel(material.Name, material.Kind.Label(), "Book24"));
                    }
                    break;
            }
        }
    }

    /// Acelasi tipar WhatsApp ca ProductViewModel.Buy() — mesaj specific
    /// pachetului, cu lista continutului inclus si ID-ul calculatorului.
    [RelayCommand]
    private void Buy()
    {
        var contents = Entries.Count > 0
            ? " Contine: " + string.Join(", ", Entries.Select(e => e.Name)) + "."
            : string.Empty;
        var text = $"Salut! Vreau sa cumpar pachetul {Bundle.Name} cu o donatie de {Bundle.BundlePriceDisplay}.{contents} ID calculator: {MachineID.Display}";
        var url = $"https://wa.me/34643109970?text={Uri.EscapeDataString(text)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenTutorial()
    {
        if (string.IsNullOrWhiteSpace(Bundle.YoutubeURL)) return;
        Process.Start(new ProcessStartInfo(Bundle.YoutubeURL) { UseShellExecute = true });
    }
}

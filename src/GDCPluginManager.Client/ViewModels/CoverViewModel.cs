using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Client.Views;

namespace GDCPluginManager.Client.ViewModels;

/// Coperta unui card, impreuna cu actiunea de marire (lightbox).
///
/// ARCHITECTURE NOTE — de ce o clasa separata si nu proprietati repetate pe
/// fiecare card: cele cinci tipuri de card (produs, curs, material,
/// eveniment, magazin) au exact acelasi comportament de imagine. Cu
/// proprietati duplicate ar fi trebuit sa tinem cinci copii sincronizate;
/// asa exista o singura implementare, expusa ca `Cover` pe fiecare
/// ViewModel, iar XAML-ul leaga peste tot la `Cover.Url` /
/// `Cover.HasImage` / `Cover.ShowCommand`.
///
/// Perechea de pe Mac e `CoverThumbnail` + `ImageLightbox` din
/// CoverImageViews.swift — daca schimbi comportamentul aici (sau acolo),
/// schimba-l in ambele, ca aplicatiile sa arate la fel.
///
/// NOTE: `Url` e legat direct la `Image.Source` in XAML. WPF descarca
/// singur un URI http/https si il tine in cache-ul lui de imagini, deci nu
/// avem nevoie de niciun cod de retea aici. Coperile sunt PUBLICE (vezi
/// CatalogAssets) — spre deosebire de fisierele vandabile, nu trec prin
/// PrivateCatalogAuth si nu au nevoie de token.
///
/// WARNING: o coperta poate lipsi din doua motive perfect normale —
/// produsul n-are inca una, sau e un URL extern (CDN-ul furnizorului) care
/// a disparut intre timp. Cardul trebuie sa cada pe iconita lui, niciodata
/// pe un chenar gol sau pe o eroare vizibila.
public sealed partial class CoverViewModel : ObservableObject
{
    /// URL-ul absolut al imaginii, sau null daca produsul n-are coperta.
    public Uri? Url { get; }

    /// Titlul aratat in fereastra de preview — de obicei numele produsului.
    public string Title { get; }

    public CoverViewModel(Uri? url, string title)
    {
        Url = url;
        Title = title;
    }

    /// True cand chiar exista o imagine de aratat. XAML-ul foloseste asta ca
    /// sa comute intre coperta si iconita de rezerva.
    public bool HasImage => Url is not null;

    /// Deschide previewul marit. Nu face nimic daca nu exista imagine —
    /// butonul e oricum ascuns in cazul asta, dar comanda ramane sigura
    /// daca e apelata din alta parte.
    [RelayCommand]
    private void Show()
    {
        if (Url is null) return;
        LightboxWindow.ShowFor(Url, Title);
    }
}

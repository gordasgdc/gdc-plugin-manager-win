using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Client.Views;
using GDCPluginManager.Core.Services;

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
/// NOTE — REVIZUIT 2026-08-25 (fix real: coperile la Materiale/Evenimente nu
/// se incarcau deloc pe Windows, in timp ce la Aplicatii "mergeau greu"):
/// vechea implementare lega `Image.Source` direct la `Url` printr-un
/// `IValueConverter` care crea un `BitmapImage` nou la fiecare evaluare de
/// binding, FARA sa asculte `DownloadFailed` — orice esec de descarcare
/// (timeout, hiccup de retea) ramanea complet silentios, iar `HasImage`
/// (bazat doar pe "are URL sau nu") tot arata cardul ca "are imagine", deci
/// XAML-ul ascundea iconita de rezerva si lasa un dreptunghi gol in locul
/// ei — vizibil mult mai mult la Materiale/Evenimente (Height 170-190) decat
/// la Aplicatii (Height 56, mai putin observabil chiar defect fiind).
///
/// Acum CoverViewModel isi gestioneaza singur descarcarea: creeaza un
/// singur BitmapImage, asculta explicit DownloadCompleted/DownloadFailed,
/// si expune `Bitmap` (null pana se termina) + `LoadFailed`. XAML-ul leaga
/// la `Cover.Bitmap` (nu la `Cover.Url` + converter), iar fallback-ul pe
/// iconita SF apare real cand `Bitmap` e null — fie ca nu exista URL, fie
/// ca descarcarea a esuat.
///
/// Coperile sunt PUBLICE (vezi CatalogAssets) — spre deosebire de
/// fisierele vandabile, nu trec prin PrivateCatalogAuth si nu au nevoie de
/// token.
public sealed partial class CoverViewModel : ObservableObject
{
    /// URL-ul absolut al imaginii, sau null daca produsul n-are coperta.
    public Uri? Url { get; }

    /// Titlul aratat in fereastra de preview — de obicei numele produsului.
    public string Title { get; }

    /// Bitmap-ul deja descarcat, gata de aratat — null pana se termina
    /// descarcarea (sau daca nu exista URL / a esuat). XAML-ul leaga direct
    /// aici, nu la `Url` printr-un converter.
    [ObservableProperty]
    private ImageSource? _bitmap;

    /// True daca a existat un URL dar descarcarea a esuat — separat de
    /// "nu are inca o coperta", ca sa putem loga distinct (desi vizual
    /// XAML-ul trateaza ambele cazuri la fel: cade pe iconita SF).
    [ObservableProperty]
    private bool _loadFailed;

    private int _attempt;
    private static readonly HttpClient Http = HttpClientFactory.Create();

    public CoverViewModel(Uri? url, string title)
    {
        Url = url;
        Title = title;
        if (url is not null) _ = LoadAsync(url);
    }

    /// True cand chiar avem o imagine gata de aratat. XAML-ul foloseste asta
    /// ca sa comute intre coperta si iconita de rezerva — bazat pe bitmap-ul
    /// REAL incarcat, nu doar pe existenta unui URL (fix 2026-08-25).
    public bool HasImage => Bitmap is not null;

    /// [2026-08-29, BUG MAJOR gasit si reparat] Vechea implementare lasa
    /// `BitmapImage.UriSource` sa descarce direct de la URL — pe Windows,
    /// asta trece prin **WinINet** (API-ul vechi de Internet al Windows-ului),
    /// un stack de retea COMPLET SEPARAT de `HttpClient` (folosit pt.
    /// catalog.json/update.json, care mergeau mereu perfect). Raportat live
    /// de Cristi: absolut NICIO imagine nu se mai incarca (Magazine, Cursuri,
    /// Materiale, Evenimente, Aplicatii) — chiar si cele care mersesera
    /// inainte — in timp ce catalogul (text) se incarca normal. Exact
    /// tiparul unui WinINet blocat/restrictionat la nivel de sistem (proxy,
    /// firewall, politica de retea) — HttpClient nu e afectat de el deloc.
    /// Fix: descarcam bytes-ii NOI cu ACELASI `HttpClient` deja dovedit
    /// functional (folosit si de `SeasonalBackgroundLoader`/`UpdateChecker`),
    /// apoi construim `BitmapImage` dintr-un `MemoryStream` local —
    /// WinINet nu mai e implicat deloc in randarea copertelor.
    private async Task LoadAsync(Uri url)
    {
        for (_attempt = 1; _attempt <= 2; _attempt++)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                if (bmp.CanFreeze) bmp.Freeze();
                Bitmap = bmp;
                DiagnosticLog.Write("CoverViewModel", $"OK, {bytes.Length} bytes (incercarea {_attempt}): {url}");
                return;
            }
            catch (Exception ex)
            {
                // [2026-08-29] `Debug.WriteLine` e INVIZIBIL cand aplicatia
                // ruleaza normal (fara debugger atasat) — exact cazul lui
                // Cristi. `DiagnosticLog` (Core, acum public — vezi
                // SeasonalBackgroundLoader.cs) scrie in %TEMP%\gdcpm-crash.log,
                // citibil chiar si dintr-un build normal.
                DiagnosticLog.Write("CoverViewModel", $"Esuat la incercarea {_attempt} pentru {url}: {DiagnosticLog.Describe(ex)}");
                if (_attempt == 1) await Task.Delay(800);
            }
        }
        LoadFailed = true;
    }

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

using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GDCPluginManager.Core.Services;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace GDCPluginManager.Client.Services;

/// Port al `SeasonalBackgroundLayer` (Etapa 6, 2026-08-29) — încarcă filigranul
/// sezonier și îl transformă într-un `ImageSource` randabil de WPF.
///
/// **De ce e nevoie de o librărie**: `catalog.seasonalBackground` din producție
/// e un **SVG** (verificat live: `covers/seasonal/background.svg`, HTTP 200,
/// `content-type: image/svg+xml`, 33 KB). WPF **nu are decodor SVG nativ** —
/// `BitmapImage` acceptă doar rastere (BMP/GIF/ICO/JPEG/PNG/TIFF/WMP). Fără o
/// librărie, filigranul ar fi eșuat TĂCUT și n-ar fi apărut niciodată — exact
/// bug-ul raportat pe Mac, unde `AsyncImage` nu randa SVG.
///
/// **De ce SharpVectors și nu Svg.Skia** (decizie documentată, nu preferință):
/// `Svg.Skia` depinde de SkiaSharp, care livrează **binare NATIVE per
/// arhitectură**. CLAUDE.md, Partea 1, Regula 22 documentează un bug REAL de pe
/// DataMover: pe host-ul Windows al lui Cristi (Parallels pe Mac Apple
/// Silicon) procesul rulează ca `win-arm64`, iar pachetele cu binare native
/// Skia NU au build pentru acea arhitectură — cad tăcut cu
/// `DllNotFoundException`/`TypeInitializationException` doar la RUNTIME,
/// niciodată la `dotnet build`. Ar fi fost exact aceeași clasă de eșec
/// silențios pe care încercăm s-o reparăm aici.
/// `SharpVectors.Reloaded` e **100% managed** (verificat: toate assembly-urile
/// din pachet sunt `Mono/.Net assembly`, pachetul n-are folder `runtimes/` cu
/// binare native), deci rulează identic pe x64 și pe ARM64 emulat. În plus
/// randează direct în `DrawingGroup`/`DrawingImage` WPF — rămâne VECTORIAL, nu
/// rasterizat, ceea ce contează pentru un filigran mare de ~480x480.
public static class SeasonalBackgroundLoader
{
    private static readonly HttpClient Http = HttpClientFactory.Create();

    /// Cache pe disc (Etapa 8, 2026-08-29) — același model ca
    /// `catalog-cache.json` din `CatalogService`. Fără el, filigranul se
    /// descărca de la zero la fiecare pornire și **dispărea complet offline**,
    /// deși restul aplicației funcționa din cache-ul de catalog. Gap identic
    /// cu cel găsit și reparat pe Mac la Etapa 8.
    private static string CacheFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "seasonal-background-cache");

    /// Descarcă și decodează filigranul. Întoarce null la ORICE eșec (rețea,
    /// format necunoscut, SVG invalid) — filigranul e pur decorativ, deci
    /// absența lui nu trebuie să producă nicio eroare vizibilă utilizatorului.
    ///
    /// Etapa 8: la succes salvează bytes pe disc; la eșec de rețea încearcă
    /// ultima variantă salvată, ca filigranul să rămână vizibil offline.
    public static async Task<ImageSource?> LoadAsync(Uri? url)
    {
        if (url is null) return null;

        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var image = Decode(bytes, url);
            // Salvam DOAR ce s-a si decodat cu succes — altfel am cache-ui un
            // raspuns corupt/HTML de eroare si l-am reincerca la infinit.
            if (image is not null) SaveToCache(bytes);
            return image;
        }
        catch
        {
            return LoadFromCache(url);
        }
    }

    private static ImageSource? LoadFromCache(Uri? url)
    {
        try
        {
            if (!File.Exists(CacheFilePath)) return null;
            return Decode(File.ReadAllBytes(CacheFilePath), url);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveToCache(byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
            File.WriteAllBytes(CacheFilePath, bytes);
        }
        catch
        {
            // Nescrierea pe disc nu trebuie sa blocheze afisarea din memorie.
        }
    }

    /// Decodează bytes deja obținuți (din rețea sau din cache-ul de pe disc —
    /// vezi Etapa 8). Separat de descărcare tocmai ca să poată fi refolosit de
    /// ambele căi.
    public static ImageSource? Decode(byte[] bytes, Uri? sourceUrl)
    {
        if (bytes.Length == 0) return null;

        try
        {
            return LooksLikeSvg(bytes, sourceUrl) ? DecodeSvg(bytes) : DecodeRaster(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// Detectăm SVG după CONȚINUT, nu doar după extensia din URL: valoarea din
    /// catalog poate fi un URL extern fără extensie, sau cu query
    /// ("?v=27081ef5", cazul real din producție).
    private static bool LooksLikeSvg(byte[] bytes, Uri? sourceUrl)
    {
        var head = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512));
        if (head.Contains("<svg", StringComparison.OrdinalIgnoreCase)) return true;

        var path = sourceUrl?.AbsolutePath ?? string.Empty;
        return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static ImageSource DecodeSvg(byte[] bytes)
    {
        var settings = new WpfDrawingSettings
        {
            IncludeRuntime = false,
            TextAsGeometry = true,   // fara dependinta de fonturile instalate
            OptimizePath = true,
        };

        using var reader = new FileSvgReader(settings);
        using var stream = new MemoryStream(bytes);
        var drawing = reader.Read(stream);
        if (drawing is null) throw new InvalidOperationException("SVG nedecodabil.");

        var image = new DrawingImage(drawing);
        image.Freeze(); // folosit de UI thread; Freeze il face safe si mai rapid
        return image;
    }

    private static ImageSource DecodeRaster(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = new MemoryStream(bytes);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

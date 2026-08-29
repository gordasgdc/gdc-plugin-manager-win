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
    ///
    /// [2026-08-29] CHEIAT PER FILIGRAN, nu mai un singur fișier global —
    /// biblioteca poate avea acum mai multe filigrane simultan (portul
    /// pluralului de pe Mac); un singur fișier ar fi însemnat că ultimul
    /// descărcat suprascrie cache-ul tuturor celorlalte offline.
    private static string CacheFilePath(string id) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "seasonal-cache",
        string.Join("_", id.Split(Path.GetInvalidFileNameChars())));

    /// Descarcă și decodează filigranul cu id-ul dat. Întoarce null la ORICE
    /// eșec (rețea, format necunoscut, SVG invalid) — filigranul e pur
    /// decorativ, deci absența lui nu trebuie să producă nicio eroare
    /// vizibilă utilizatorului.
    ///
    /// Etapa 8: la succes salvează bytes pe disc; la eșec de rețea încearcă
    /// ultima variantă salvată, ca filigranul să rămână vizibil offline.
    /// [2026-08-29] RETRY + eroare reala in log — gasit live (raportat de
    /// Cristi, reprodus pe Mac cu acelasi simptom): un filigran esua
    /// consecvent la fetch in timp ce altul, publicat in acelasi minut,
    /// mergea perfect — verificat direct ca fisierul era disponibil pe
    /// server (HTTP 200, `curl`) exact cat timp aplicatia raporta esec.
    /// Concluzie: nu era un bug de cod, ci un blip TRANZITORIU de retea/CDN
    /// (gordas.dev trece prin Cloudflare SI Fastly/GitHub Pages — un nod de
    /// edge poate rata o cerere fara ca alta, milisecunde mai tarziu, s-o
    /// rateze). `catch` generic ascundea eroarea REALA — acum se
    /// logheaza explicit. Un singur retry, cu pauza scurta, rezolva marea
    /// majoritate a acestor blip-uri.
    public static async Task<ImageSource?> LoadAsync(string id, Uri? url)
    {
        if (url is null) return null;

        Exception? lastError = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                var image = Decode(bytes, url);
                if (image is not null)
                {
                    DiagnosticLog.Write("SeasonalBackground", $"id={id}: OK, {bytes.Length} bytes (incercarea {attempt})");
                    // Salvam DOAR ce s-a si decodat cu succes — altfel am cache-ui un
                    // raspuns corupt/HTML de eroare si l-am reincerca la infinit.
                    SaveToCache(id, bytes);
                    return image;
                }
                DiagnosticLog.Write("SeasonalBackground", $"id={id}: fetch OK ({bytes.Length} bytes) dar Decode() a esuat (incercarea {attempt})");
                break; // date corupte/format necunoscut - un retry nu ajuta.
            }
            catch (Exception ex)
            {
                lastError = ex;
                DiagnosticLog.Write("SeasonalBackground", $"id={id}: fetch ESUAT la incercarea {attempt}: {DiagnosticLog.Describe(ex)}");
                if (attempt == 1) await Task.Delay(800);
            }
        }

        var cached = LoadFromCache(id, url);
        DiagnosticLog.Write("SeasonalBackground", cached is not null
            ? $"id={id}: fetch esuat de 2 ori ({lastError?.GetType().Name}), fallback pe cache local reusit"
            : $"id={id}: fetch esuat de 2 ori ({lastError?.GetType().Name}) SI niciun cache local disponibil");
        return cached;
    }

    private static ImageSource? LoadFromCache(string id, Uri? url)
    {
        try
        {
            var path = CacheFilePath(id);
            if (!File.Exists(path)) return null;
            return Decode(File.ReadAllBytes(path), url);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveToCache(string id, byte[] bytes)
    {
        try
        {
            var path = CacheFilePath(id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
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

namespace GDCPluginManager.Core.Services;

/// Log de diagnostic pe disc, la %TEMP%\gdcpm-crash.log — singura sursa de
/// adevar reala cand un client Windows raporteaza un bug fara sa avem acces
/// live la masina lui (vezi CLAUDE.md "Unde se ruleaza testele reale").
/// Extras din PowerGradeImporter.cs (era `file`-scoped acolo, invizibil
/// pentru restul codului) intr-un fisier propriu, `internal`, ca sa poata
/// fi folosit si de InstallManager.cs — instalarea normala (LUT/DCTL/Fuse/
/// OFX) nu avea NICIUN logging pana acum, deci un raport ca "arata Installed
/// dar fisierele nu apar" nu putea fi diagnosticat de la distanta.
public static class DiagnosticLog
{
    private static readonly string Path_ = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

    public static void Write(string tag, string message)
    {
        try { File.AppendAllText(Path_, $"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] {message}\n"); }
        catch { /* best-effort */ }
    }

    /// [2026-08-29] Desface lanțul complet de `InnerException` — un
    /// `HttpRequestException: The SSL connection could not be established`
    /// e doar un wrapper generic; cauza REALĂ (certificat expirat/invalid,
    /// versiune TLS incompatibilă, interceptare de antivirus/proxy) stă în
    /// `InnerException`, ignorat până acum de orice `ex.Message` simplu.
    /// Folosit de orice fetch de imagine (coperți, filigran, lightbox) —
    /// fără asta, un eșec SSL raportat de client rămâne la fel de opac ca
    /// "Debug.WriteLine invizibil" era înainte de introducerea acestui log.
    public static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");
        return string.Join(" <- ", parts);
    }
}

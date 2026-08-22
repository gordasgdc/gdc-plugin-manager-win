namespace GDCPluginManager.Core.Services;

/// Log de diagnostic pe disc, la %TEMP%\gdcpm-crash.log — singura sursa de
/// adevar reala cand un client Windows raporteaza un bug fara sa avem acces
/// live la masina lui (vezi CLAUDE.md "Unde se ruleaza testele reale").
/// Extras din PowerGradeImporter.cs (era `file`-scoped acolo, invizibil
/// pentru restul codului) intr-un fisier propriu, `internal`, ca sa poata
/// fi folosit si de InstallManager.cs — instalarea normala (LUT/DCTL/Fuse/
/// OFX) nu avea NICIUN logging pana acum, deci un raport ca "arata Installed
/// dar fisierele nu apar" nu putea fi diagnosticat de la distanta.
internal static class DiagnosticLog
{
    private static readonly string Path_ = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

    public static void Write(string tag, string message)
    {
        try { File.AppendAllText(Path_, $"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] {message}\n"); }
        catch { /* best-effort */ }
    }
}

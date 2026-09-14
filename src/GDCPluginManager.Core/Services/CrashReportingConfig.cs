using System.Reflection;

namespace GDCPluginManager.Core.Services;

/// Unde se trimit rapoartele de erori si in ce mediu ruleaza aplicatia.
/// Port 1:1 al `CrashReportingConfig.swift` de pe Mac — aceleasi reguli,
/// acelasi nume de variabila de mediu, ca sa nu existe doua configurari
/// diferite pentru acelasi proiect Sentry.
///
/// DSN-ul e o adresa de INTRARE, nu o cheie de acces: permite doar TRIMITEREA
/// de evenimente, nu citirea a nimic. Ajunge oricum in binar la fiecare
/// client, deci ascunderea lui n-ar schimba nimic.
public static class CrashReportingConfig
{
    private const string EnvironmentOverrideKey = "GDC_SENTRY_DSN";

    /// PUNE AICI DSN-ul din Sentry -> Settings -> Client Keys (DSN).
    /// Gol = raportarea e OPRITA complet.
    private const string CompiledDsn = "";

    public static string Dsn
    {
        get
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentOverrideKey)?.Trim();
            return !string.IsNullOrEmpty(fromEnvironment) ? fromEnvironment : CompiledDsn.Trim();
        }
    }

    /// Fara DSN nu se porneste NIMIC — deliberat, nu o eroare: un DSN
    /// necompletat e starea normala pana cand proiectul Sentry exista, iar o
    /// aplicatie care crapa la pornire fiindca lipseste configurarea unei
    /// unelte de diagnostic ar fi exact opusul scopului.
    public static bool IsEnabled => !string.IsNullOrWhiteSpace(Dsn);

    public static string Environment_ =>
        System.Diagnostics.Debugger.IsAttached ? "development" : "production";

    public static string Version =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    public static string ReleaseName => $"gdc-plugin-manager@{Version}";
}

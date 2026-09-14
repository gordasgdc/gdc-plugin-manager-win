using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using GDCPluginManager.Core.Services;
using Sentry;

namespace GDCPluginManager.Client.Services;

/// Pornirea raportarii de erori si contextul atasat fiecarui raport.
/// Port al `CrashReporter.swift` de pe Mac — aceleasi etichete si acelasi
/// context, ca rapoartele de pe cele doua platforme sa fie comparabile.
///
/// CE NU SE TRIMITE, deliberat: `SendDefaultPii` ramane oprit (fara adrese IP
/// sau nume de utilizator Windows), iar `SetBeforeSend` taie calea catre
/// profilul utilizatorului din orice mesaj — altfel o simpla eroare de fisier
/// ar trimite numele real al persoanei, care apare in fiecare cale
/// `C:\Users\<nume>\...`.
public static class CrashReporter
{
    private static IDisposable? _sdk;

    public static void Start()
    {
        if (!CrashReportingConfig.IsEnabled) return;

        _sdk = SentrySdk.Init(options =>
        {
            options.Dsn = CrashReportingConfig.Dsn;
            options.Environment = CrashReportingConfig.Environment_;
            options.Release = CrashReportingConfig.ReleaseName;

            // Stiva completa si pentru mesajele simple, nu doar pentru
            // exceptii — altfel un mesaj ajunge fara niciun indiciu despre
            // locul din care a plecat.
            options.AttachStacktrace = true;

            // Sesiuni: cate porniri s-au terminat cu bine si cate cu o eroare.
            options.AutoSessionTracking = true;

            options.SendDefaultPii = false;
            options.TracesSampleRate = 0.0;

            options.SetBeforeSend((SentryEvent e) => ScrubUserProfilePath(e));
        });

        ApplyStaticContext();
    }

    /// De chemat la inchidere: SDK-ul trimite pe un fir de fundal, iar un
    /// proces care se termina imediat poate muri inainte ca ultimul eveniment
    /// sa plece. Verificat ca problema reala pe Mac, unde prima incercare de
    /// testare n-a primit nimic exact din acest motiv.
    public static void Stop()
    {
        if (_sdk is null) return;
        SentrySdk.Flush(TimeSpan.FromSeconds(3));
        _sdk.Dispose();
        _sdk = null;
    }

    // MARK: Context

    private static void ApplyStaticContext()
    {
        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag("environment", CrashReportingConfig.Environment_);
            scope.SetTag("os_version", Environment.OSVersion.VersionString);
            scope.SetTag("app_language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

            scope.Contexts["gdc"] = new Dictionary<string, string>
            {
                ["version"] = CrashReportingConfig.Version,
                ["os"] = Environment.OSVersion.VersionString,
                ["language"] = CultureInfo.CurrentUICulture.Name,
                // Arhitectura separa un crash care apare doar pe ARM de unul
                // general — nu se poate deduce altfel din raport.
                ["architecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                ["dotnet"] = Environment.Version.ToString(),
            };
        });
    }

    public static void Breadcrumb(string message, string category, IDictionary<string, string>? data = null)
    {
        if (!CrashReportingConfig.IsEnabled) return;
        SentrySdk.AddBreadcrumb(message, category, data: data);
    }

    // MARK: Exceptii neprinse

    /// Trimite o exceptie, marcata cu locul din care a fost prinsa.
    ///
    /// DELIBERAT fara subscrieri proprii la `DispatcherUnhandledException` /
    /// `AppDomain.UnhandledException`: aplicatia are deja handler-ele ei (vezi
    /// App.xaml.cs), care scriu in %TEMP%\gdcpm-crash.log si arata un mesaj
    /// utilizatorului. O a doua subscriere ar fi rulat in paralel cu ele,
    /// pentru aceeasi exceptie, si ar fi facut ordinea si numarul de rapoarte
    /// greu de urmarit. Raportarea se leaga IN handler-ele existente, deci
    /// comportamentul vizibil al aplicatiei ramane exact cum era.
    public static void Capture(Exception exception, string source, bool flushNow = false)
    {
        if (!CrashReportingConfig.IsEnabled) return;
        exception.Data["gdc.source"] = source;
        SentrySdk.CaptureException(exception);
        // La `AppDomain.UnhandledException` procesul moare imediat dupa —
        // acolo coada trebuie golita sincron, altfel raportul nu mai pleaca.
        if (flushNow) SentrySdk.Flush(TimeSpan.FromSeconds(3));
    }

    // MARK: Curatare

    /// Inlocuieste calea profilului cu `%USERPROFILE%`.
    /// Fara asta, orice eroare de fisier ar trimite numele real al
    /// utilizatorului Windows — o data personala pe care n-am cerut-o.
    private static SentryEvent? ScrubUserProfilePath(SentryEvent e)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return e;

        if (e.Message?.Formatted is { } formatted && formatted.Contains(home, StringComparison.OrdinalIgnoreCase))
        {
            e.Message = new SentryMessage
            {
                Formatted = formatted.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
            };
        }
        return e;
    }
}

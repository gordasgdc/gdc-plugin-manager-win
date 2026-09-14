using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Threading;

namespace GDCPluginManager.Client;

public partial class App : Application
{
    // %TEMP%\gdcpm-crash.log — jurnal de diagnostic pentru orice esec care
    // scapa de DispatcherUnhandledException (crash pe thread de fundal,
    // crash in constructor inainte ca dispatcher-ul sa porneasca, etc.).
    // Prima linie e scrisa la pornire necondiționat, ca sa stim macar daca
    // App a apucat sa porneasca deloc.
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

    public App()
    {
        // [2026-09-03] Windows 10 poate avea TLS 1.2 dezactivat la nivel
        // de sistem (registry SChannel) — HttpClientFactory deja forteaza
        // Tls12|Tls13 pe SocketsHttpHandler, dar ServicePointManager
        // (folosit de orice cale legacy/WinINet ramasa in .NET/WPF) poate
        // ramane pe un default mai vechi. Fortam explicit aici, cat mai
        // devreme, ca sa eliminam aceasta variabila pentru bug-ul cu
        // imaginile care nu se incarca pe Windows 10 (raportat 2026-09-03,
        // neconfirmat inca prin log real — vezi gdcpm-crash.log).
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        // Raportarea porneste cat mai devreme: o eroare aparuta in timpul
        // pornirii e exact genul pe care nu-l vede nimeni altfel. Fara DSN
        // configurat nu se porneste nimic (vezi CrashReportingConfig).
        Services.CrashReporter.Start();

        Log("App() constructor started.");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log($"AppDomain.UnhandledException (fatal={e.IsTerminating}): {e.ExceptionObject}");
            // `flushNow`: procesul moare imediat dupa acest handler, deci
            // raportul trebuie trimis sincron sau nu mai pleaca deloc.
            if (e.ExceptionObject is Exception ex)
                Services.CrashReporter.Capture(ex, "AppDomain.UnhandledException", flushNow: true);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log($"TaskScheduler.UnobservedTaskException: {e.Exception}");
            Services.CrashReporter.Capture(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        // O eroare neasteptata intr-un handler async (Task.Run/ICommand)
        // nu trebuie sa arunce toata aplicatia intr-un crash mut — se
        // afiseaza un mesaj, la fel cum orice throw pe Mac ajunge intr-un
        // alert vizibil, nu doar in consola de debug.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        Startup += (_, _) =>
        {
            Log("App.Startup event fired.");
            // Tema salvată (Regula 24) — trebuie aplicată AICI (Startup),
            // nu în constructor: `Application.Resources` nu e populat
            // decât după ce `InitializeComponent()` a rulat, ceea ce se
            // întâmplă între constructor și evenimentul Startup.
            Services.WindowsThemeManager.ApplyNow();
        };
        Exit += (_, e) =>
        {
            Log($"App.Exit event fired, ExitCode={e.ApplicationExitCode}.");
            // Golire la inchidere: ultimul eveniment ar ramane netrimis daca
            // procesul se termina inaintea firului de fundal al SDK-ului.
            Services.CrashReporter.Stop();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"DispatcherUnhandledException: {e.Exception}");
        Services.CrashReporter.Capture(e.Exception, "DispatcherUnhandledException");
        MessageBox.Show(
            $"A aparut o eroare neasteptata:\n\n{e.Exception}",
            "GDC Plugin Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch
        {
            // Daca nici logging-ul nu merge, nu mai avem ce face aici.
        }
    }
}

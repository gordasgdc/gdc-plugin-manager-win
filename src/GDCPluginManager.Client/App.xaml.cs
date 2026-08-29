using System.IO;
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
        Log("App() constructor started.");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log($"AppDomain.UnhandledException (fatal={e.IsTerminating}): {e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log($"TaskScheduler.UnobservedTaskException: {e.Exception}");
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
        Exit += (_, e) => Log($"App.Exit event fired, ExitCode={e.ApplicationExitCode}.");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"DispatcherUnhandledException: {e.Exception}");
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

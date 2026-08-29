using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using GDCPluginManager.Core.Services;
using GDCPluginManager.Client.Views;

namespace GDCPluginManager.Client.Services;

/// Descarca si instaleaza automat un update de APLICATIE, fara sa mai
/// treaca prin browser/pagina de GitHub.
///
/// ARCHITECTURE NOTE (2026-08-26): pana acum, atat bannerul (MainViewModel.
/// DownloadUpdate) cat si pop-up-ul (MainWindow.xaml.cs) chemau
/// `Process.Start(url) { UseShellExecute = true }` — asta deschidea arhiva
/// .zip in browser, userul trebuia s-o dezarhiveze si sa ruleze
/// GDCPluginManagerSetup.exe manual. Cristi a cerut explicit scoaterea
/// self-updater-ului din backlog, dupa ce reteta a fost verificata deja
/// end-to-end pe DataMover (Mac) si portata apoi pe GDC Plugin Manager Mac
/// (`Sources/GDCPluginManager/SelfUpdater.swift`). Fisierul asta e portul
/// Windows, adaptat la ce exista deja:
///
///   1. `docs/update.json` -> `DownloadUrl["windows"]` (deja fetch-uit de
///      `UpdateChecker.CheckAsync()`) — indica `GDCPluginManager-Windows.zip`,
///      care contine un singur `GDCPluginManagerSetup.exe` (Inno Setup).
///   2. Instalatorul e lansat NESILENTIOS (fereastra lui Inno ramane
///      vizibila) — installer.iss NU are `AppMutex`/`CloseApplications`
///      configurat, deci Setup.exe NU poate suprascrie singur
///      `GDCPluginManager.exe` cat timp ruleaza. In loc sa construim un
///      helper .bat separat (ca DataMover, care avea nevoie de asta pentru
///      un update SILENTIOS complet), ne bazam pe ce e deja dovedit sa
///      functioneze aici: `[Run] ... Flags: nowait postinstall skipifsilent`
///      din installer.iss RELANSEAZA aplicatia dupa instalare. Noi doar
///      trebuie sa inchidem aplicatia curenta INAINTE ca userul sa ajunga
///      la pasul de copiere de fisiere din wizard-ul Inno — timpul cat
///      userul apasa Next/Install e suficient.
///   3. Copia locala a `.exe`-ului extras se redenumeste cu versiunea
///      INAINTE de lansare — Regula 17 din CLAUDE.md ("orice fisier
///      descarcat/creat local, in afara mecanismului `releases/latest/
///      download/...`, trebuie sa poarte versiunea in nume").
///
/// WARNING: pasul de instalare efectiva (wizard-ul Inno, click-urile
/// userului) NU poate fi verificat automat de Claude. Verificat automat
/// doar pana la "arhiva descarcata + dezarhivata + exe-ul gasit si
/// redenumit corect, integru pe disc".
public static class SelfUpdater
{
    // [BUG REAL 2026-08-29, gasit din raportul lui Cristi: "de ce trebuie
    // tot timpul sa descarc de pe pagina web"] Aici era `new HttpClient()`
    // simplu, NU `HttpClientFactory.Create()` — deci fara User-Agent (bug
    // deja documentat chiar in acest fisier, `HttpClientFactory.cs`: GitHub
    // respinge cu 403 orice cerere fara el) si fara niciuna din fix-urile
    // recente (reciclare conexiune, validare TLS diagnosticabila). Orice
    // esec cadea tacut pe fallback-ul "Deschide pagina" din PresentFailure,
    // fara sa apara NICIODATA in log (Regula 25 - lipsea complet aici) -
    // userul vedea doar ca update-ul "nu merge niciodata din program",
    // exact ca la GDC Vault/DataMover ÎNAINTE sa aiba self-updater corect.
    // Arhiva Windows are ~60+ MB (installer self-contained .NET) — mult mai
    // mare decat orice am descarcat pana acum pe Mac, de-asta Timeout-ul
    // implicit (mostenit din HttpClientFactory) e suprascris aici, generos.
    private static readonly HttpClient Http = HttpClientFactory.Create();

    static SelfUpdater()
    {
        Http.Timeout = TimeSpan.FromMinutes(5);
    }

    public static async Task DownloadAndInstallAsync(UpdateInfo info)
    {
        var url = info.DownloadUrl.GetValueOrDefault("windows");
        if (string.IsNullOrWhiteSpace(url)
            || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            PresentFailure("Lipsește link-ul de descărcare pentru Windows în update.json.");
            return;
        }

        var progress = new UpdateProgressWindow(info.Version);
        progress.Show();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "gdcpm-update-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            progress.SetStatus("Se descarcă actualizarea…");
            var zipPath = Path.Combine(tempDir, "GDCPluginManager-Windows.zip");
            await DownloadAsync(url, zipPath);

            progress.SetStatus("Se dezarhivează…");
            var extractDir = Path.Combine(tempDir, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var extractedExe = Directory.GetFiles(extractDir, "*.exe").FirstOrDefault()
                ?? throw new InvalidOperationException("Arhiva descărcată nu conține un instalator .exe.");

            // Regula 17: redenumim cu versiunea INAINTE de lansare — arhiva
            // sursa are un nume stabil (necesar pt. releases/latest/download),
            // dar copia locala nu mai are acea constrangere.
            var versionedExe = Path.Combine(tempDir, $"GDCPluginManagerSetup-{info.Version}.exe");
            File.Move(extractedExe, versionedExe, overwrite: true);

            progress.SetStatus("Se lansează instalatorul…");
            Process.Start(new ProcessStartInfo(versionedExe) { UseShellExecute = true });

            // Instalatorul Inno preia de aici — fereastra lui apare peste a
            // noastra. Ne inchidem noi acum: fara AppMutex/CloseApplications
            // configurat in installer.iss, Setup.exe NU poate suprascrie
            // singur GDCPluginManager.exe cat timp ruleaza. Timpul cat
            // userul parcurge wizard-ul (Next/Install) e suficient ca
            // procesul nostru sa se inchida complet inainte de pasul real
            // de copiere. [Run] din installer.iss relanseaza aplicatia dupa
            // instalare — nu mai avem nimic de facut dupa asta.
            progress.Close();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            progress.Close();
            DiagnosticLog.Write("SelfUpdater", $"Actualizare la v{info.Version} esuata: {DiagnosticLog.Describe(ex)}");
            PresentFailure(ex.Message);
            // NOTE: nu stergem `tempDir` aici — daca eroarea a picat DUPA ce
            // Setup.exe a fost deja lansat (putin probabil, dar posibil intre
            // Process.Start si Shutdown), instalatorul inca citeste
            // `versionedExe` din el; stergerea l-ar intrerupe la jumatate.
            // Ramane in %TEMP%, curatat automat de Windows la un moment dat.
        }
    }

    private static async Task DownloadAsync(string url, string destination)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Descărcarea a eșuat: HTTP {(int)response.StatusCode}");
        }
        await using var httpStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(destination);
        await httpStream.CopyToAsync(fileStream);
    }

    private static void PresentFailure(string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Actualizarea a eșuat",
            Content = $"{message}\n\nPoți descărca manual ultima versiune de pe pagina de GitHub.",
            PrimaryButtonText = "Deschide pagina",
            CloseButtonText = "OK",
        };
        _ = ShowFailureAsync(box);
    }

    private static async Task ShowFailureAsync(Wpf.Ui.Controls.MessageBox box)
    {
        var result = await box.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            Process.Start(new ProcessStartInfo("https://github.com/gordasgdc/gdc-plugin-manager/releases/latest")
            {
                UseShellExecute = true,
            });
        }
    }
}

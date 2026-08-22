using System.Diagnostics;
using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

file static class Log
{
    private static readonly string Path_ = Path.Combine(Path.GetTempPath(), "gdcpm-crash.log");

    public static void Write(string message)
    {
        try { File.AppendAllText(Path_, $"[{DateTime.Now:HH:mm:ss.fff}] [PowerGradeImporter] {message}\n"); }
        catch { /* best-effort */ }
    }
}

/// Port al PowerGradeImporter.swift — bridge catre API-ul de scripting
/// extern al DaVinci Resolve, ca sa importe/elimine PowerGrade-uri direct
/// in Gallery, fara pas manual.
///
/// IMPORTANT: caile RESOLVE_SCRIPT_API / RESOLVE_SCRIPT_LIB de mai jos sunt
/// cele DOCUMENTATE oficial de Blackmagic pentru Windows — spre deosebire
/// de varianta Mac (verificata live, cu DaVinci Resolve Studio chiar
/// rulator, inainte de a scrie fisierul), NU au fost inca verificate pe o
/// instalare Windows reala. Design-ul e sigur oricum: orice discrepanta
/// (cale gresita, python lipsa, eroare de script) cade automat pe
/// `stagedOnly` — fisierele tot ajung verificate pe disc, userul primeste
/// doar mesajul de import manual, niciodata o eroare dura. Cere confirmare
/// pe o masina cu Resolve Studio instalat inainte sa consideri asta gata.
[SupportedOSPlatform("windows")]
// WARNING: PowerGrade import happens EXCLUSIVELY through Resolve's official
// Scripting API below - never write directly into %APPDATA%\...\Gallery\ (no
// "index.xml"/.drx path is documented by Blackmagic for Windows). A direct
// write risks corrupting the user's project database. See CLAUDE.md.
public static class PowerGradeImporter
{
    public enum ImportResultKind { ImportedToGallery, StagedOnly }
    public readonly record struct ImportResult(ImportResultKind Kind, string? AlbumName, string? StagingFolder);

    public enum RemoveResultKind { RemovedFromGallery, RemovedFilesOnly }

    /// Fiecare produs primeste propriul album, cu prefix "GDC — " ca sa nu
    /// se poata coincide vreodata cu un album personal al lui Cristi.
    public static string AlbumName(string productName) => "GDC — " + productName.Trim();

    public static ImportResult ImportIntoGallery(string productName, IReadOnlyList<string> filePaths, string stagingFolder)
    {
        var albumName = AlbumName(productName);
        Log.Write($"ImportIntoGallery start: product={productName}, files={filePaths.Count}");

        if (!ResolveProcessCheck.IsRunning)
        {
            Log.Write("Resolve nu ruleaza (ResolveProcessCheck.IsRunning=false) -> stagedOnly");
            return new ImportResult(ImportResultKind.StagedOnly, null, stagingFolder);
        }

        var drxPaths = filePaths.Where(p => Path.GetExtension(p).Equals(".drx", StringComparison.OrdinalIgnoreCase)).ToList();
        if (drxPaths.Count == 0)
        {
            Log.Write($"Niciun fisier .drx in lista primita ({string.Join(", ", filePaths)}) -> stagedOnly");
            return new ImportResult(ImportResultKind.StagedOnly, null, stagingFolder);
        }

        var python = FindPython();
        Log.Write(python is null ? "FindPython: niciun candidat gasit" : $"FindPython: folosesc '{python.Value.Exe} {python.Value.FixedArgs}'");
        if (python is null || !Directory.Exists(ScriptModulesPath))
        {
            Log.Write($"python null? {python is null}, ScriptModulesPath exista? {Directory.Exists(ScriptModulesPath)} ({ScriptModulesPath}) -> stagedOnly");
            return new ImportResult(ImportResultKind.StagedOnly, null, stagingFolder);
        }

        var drxPathsLiteral = string.Join(", ", drxPaths.Select(p => "r\"" + p + "\""));
        var script = $$"""
        import sys
        sys.path.append(r"{{ScriptModulesPath}}")
        try:
            import DaVinciResolveScript as dvr
            resolve = dvr.scriptapp("Resolve")
            if resolve is None:
                print("FAIL:no_scripting_access")
                sys.exit(0)
            project = resolve.GetProjectManager().GetCurrentProject()
            gallery = project.GetGallery()

            def find_album():
                for album in gallery.GetGalleryPowerGradeAlbums():
                    if gallery.GetAlbumName(album) == {{PyString(albumName)}}:
                        return album
                return None

            target = find_album() or find_album()
            if target is None:
                before = len(gallery.GetGalleryPowerGradeAlbums())
                gallery.CreateGalleryPowerGradeAlbum()
                albums = gallery.GetGalleryPowerGradeAlbums()
                if len(albums) <= before:
                    print("FAIL:create_album_failed")
                    sys.exit(0)
                target = albums[-1]
                gallery.SetAlbumName(target, {{PyString(albumName)}})

            ok = target.ImportStills([{{drxPathsLiteral}}])
            print("OK" if ok else "FAIL:import_returned_false")
        except Exception as e:
            print("FAIL:" + str(e))
        """;

        var output = RunPython(python.Value, script);
        Log.Write($"RunPython output: {output ?? "(null)"}");
        if (output is null || !output.StartsWith("OK", StringComparison.Ordinal))
        {
            return new ImportResult(ImportResultKind.StagedOnly, null, stagingFolder);
        }
        return new ImportResult(ImportResultKind.ImportedToGallery, albumName, null);
    }

    public static RemoveResultKind RemoveFromGallery(string productName)
    {
        var albumName = AlbumName(productName);
        var python = FindPython();
        if (!ResolveProcessCheck.IsRunning || python is null || !Directory.Exists(ScriptModulesPath))
        {
            return RemoveResultKind.RemovedFilesOnly;
        }

        var script = $$"""
        import sys
        sys.path.append(r"{{ScriptModulesPath}}")
        try:
            import DaVinciResolveScript as dvr
            resolve = dvr.scriptapp("Resolve")
            if resolve is None:
                print("FAIL:no_scripting_access")
                sys.exit(0)
            project = resolve.GetProjectManager().GetCurrentProject()
            gallery = project.GetGallery()

            target = None
            for album in gallery.GetGalleryPowerGradeAlbums():
                if gallery.GetAlbumName(album) == {{PyString(albumName)}}:
                    target = album
                    break
            if target is None:
                print("FAIL:album_not_found")
                sys.exit(0)

            stills = target.GetStills()
            if len(stills) == 0:
                print("OK")
                sys.exit(0)
            ok = target.DeleteStills(list(range(len(stills))))
            print("OK" if ok else "FAIL:delete_returned_false")
        except Exception as e:
            print("FAIL:" + str(e))
        """;

        var output = RunPython(python.Value, script);
        return output is not null && output.StartsWith("OK", StringComparison.Ordinal)
            ? RemoveResultKind.RemovedFromGallery
            : RemoveResultKind.RemovedFilesOnly;
    }

    // MARK: - Mediul de scripting Resolve (cai DOCUMENTATE pentru Windows, neverificate live)

    private static readonly string ScriptApiPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Blackmagic Design", "DaVinci Resolve", "Support", "Developer", "Scripting");

    private static string ScriptModulesPath => Path.Combine(ScriptApiPath, "Modules");

    private static readonly string ScriptLibPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Blackmagic Design", "DaVinci Resolve", "fusionscript.dll");

    /// Calea Python-ului embeddable bundle-uit langa exe (vezi PythonRuntime\
    /// in GDCPluginManager.Client.csproj — python.org, oficial, ~22MB). Cu
    /// asta clientul NU mai depinde de un Python instalat separat de user —
    /// motivul intreg pentru care exista bundle-ul: eroarea "Python was not
    /// found" (alias-ul Microsoft Store) a fost exact ce a blocat prima
    /// testare reala a importului automat PowerGrade.
    private static string BundledPythonPath =>
        Path.Combine(AppContext.BaseDirectory, "PythonRuntime", "python.exe");

    /// [0] = executabil (cale completa sau nume din PATH), [1] = argumentele
    /// fixe care preced scriptul citit din stdin (vezi RunPython). Python-ul
    /// bundle-uit e incercat primul (nu are nevoie de "-3", e deja Python 3
    /// de sine statator); "py"/"python" din PATH raman fallback pentru cazul
    /// (neasteptat) in care bundle-ul lipseste sau e corupt.
    private static (string Exe, string FixedArgs)[] PythonCandidates() =>
    [
        (BundledPythonPath, ""),
        ("py", "-3"),
        ("python", ""),
    ];

    private static (string Exe, string FixedArgs)? FindPython()
    {
        foreach (var candidate in PythonCandidates())
        {
            // Python-ul bundle-uit e propriul nostru fisier, cu cale
            // completa — daca exista pe disc, e garantat valid, nu mai are
            // rost sa il si probam cu un proces separat (mai rapid, mai
            // putine moduri de a esua).
            if (Path.IsPathRooted(candidate.Exe))
            {
                if (File.Exists(candidate.Exe))
                {
                    Log.Write($"FindPython: folosesc Python bundle-uit ({candidate.Exe})");
                    return candidate;
                }
                Log.Write($"FindPython: Python bundle-uit lipseste ({candidate.Exe}) — incerc PATH.");
                continue;
            }

            try
            {
                var probeArgs = string.IsNullOrEmpty(candidate.FixedArgs)
                    ? "-c \"import sys\""
                    : $"{candidate.FixedArgs} -c \"import sys\"";
                var psi = new ProcessStartInfo(candidate.Exe, probeArgs)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var probe = Process.Start(psi);
                probe?.WaitForExit(3000);
                if (probe is { ExitCode: 0 }) return candidate;
                Log.Write($"FindPython: '{candidate.Exe}' a raspuns cu exit code {probe?.ExitCode}");
            }
            catch (Exception ex)
            {
                Log.Write($"FindPython: '{candidate.Exe}' indisponibil ({ex.GetType().Name}: {ex.Message})");
            }
        }
        return null;
    }

    /// Un string literal Python, cu orice caracter non-ASCII (ex. em-dash-ul
    /// "—" din prefixul "GDC — ") scris ca escape \uXXXX in loc de caracterul
    /// brut. Motiv real, nu prudenta: .NET scrie pe stdin-ul procesului cu
    /// codepage-ul implicit al consolei Windows (nu UTF-8), iar Python
    /// decodeaza acel stdin cu propriul default de sistem — quasi-garantat
    /// sa nu coincida, ceea ce arunca exact "SyntaxError: Non-UTF-8 code" pe
    /// orice caracter peste ASCII (confirmat live, byte 0x97 = em-dash in
    /// cp1252). \uXXXX e text ASCII pur, deci ambele parti il transmit
    /// identic indiferent de encoding-ul pipe-ului.
    private static string PyString(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("'", "\\'");
        var sb = new System.Text.StringBuilder("'");
        foreach (var ch in escaped)
        {
            if (ch > 127) sb.Append($"\\u{(int)ch:x4}");
            else sb.Append(ch);
        }
        sb.Append('\'');
        return sb.ToString();
    }

    /// Ruleaza un script Python embedded, cu timeout — bridge-ul de
    /// scripting Resolve poate ramane blocat, un apel agatat nu trebuie sa
    /// inghete aplicatia. Argumentul "-" (nu "-c") ii spune lui Python sa
    /// citeasca scriptul din stdin, nu dintr-un literal de linie de comanda
    /// — mai sigur pentru un script multi-linie cu ghilimele/backslash-uri.
    private static string? RunPython((string Exe, string FixedArgs) python, string script, int timeoutMs = 20000)
    {
        var args = string.IsNullOrEmpty(python.FixedArgs) ? "-" : $"{python.FixedArgs} -";
        var psi = new ProcessStartInfo(python.Exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // PYTHONUTF8=1: forteaza Python sa decodeze stdin/stdout/stderr ca
        // UTF-8 in loc de codepage-ul implicit Windows — plasa de siguranta
        // suplimentara fata de PyString (\uXXXX), pentru orice alt text
        // care ar putea ajunge in script fara sa treaca prin PyString.
        psi.EnvironmentVariables["PYTHONUTF8"] = "1";
        psi.EnvironmentVariables["RESOLVE_SCRIPT_API"] = ScriptApiPath;
        psi.EnvironmentVariables["RESOLVE_SCRIPT_LIB"] = ScriptLibPath;
        psi.EnvironmentVariables["PYTHONPATH"] = ScriptModulesPath;

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                Log.Write($"RunPython: Process.Start('{python.Exe} {args}') a intors null");
                return null;
            }

            // Citeste stdout/stderr pe fire separate INAINTE de WaitForExit —
            // altfel, daca scriptul scrie destul pe oricare din cele doua
            // (ex. un traceback lung pe stderr), buffer-ul pipe-ului se
            // umple si copilul se blocheaza in scriere -> deadlock cu noi,
            // care asteptam WaitForExit fara sa mai citim nimic.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            process.StandardInput.Write(script);
            process.StandardInput.Close();

            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill();
                Log.Write($"RunPython: timeout dupa {timeoutMs}ms, proces omorat.");
                return null;
            }

            var stdout = stdoutTask.GetAwaiter().GetResult().Trim();
            var stderr = stderrTask.GetAwaiter().GetResult().Trim();

            if (process.ExitCode != 0)
            {
                Log.Write($"RunPython: exit code {process.ExitCode}. stdout=[{stdout}] stderr=[{stderr}]");
                return null;
            }
            if (!string.IsNullOrEmpty(stderr))
            {
                Log.Write($"RunPython: exit 0 dar stderr non-gol: [{stderr}]");
            }
            return stdout;
        }
        catch (Exception ex)
        {
            Log.Write($"RunPython: exceptie: {ex}");
            return null;
        }
    }
}

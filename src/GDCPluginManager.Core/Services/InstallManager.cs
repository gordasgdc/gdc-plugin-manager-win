using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Core.Services;

public enum InstallOutcomeKind { Installed, InstalledToGallery, InstalledNeedsManualStep }

/// Port 1:1 al InstallOutcome din InstallManager.swift — enum cu valori
/// asociate in Swift, aici un record struct: AlbumName populat doar pentru
/// InstalledToGallery, StagingFolder doar pentru InstalledNeedsManualStep.
public readonly record struct InstallOutcome(InstallOutcomeKind Kind, string? AlbumName = null, string? StagingFolder = null)
{
    public static readonly InstallOutcome Installed = new(InstallOutcomeKind.Installed);
    public static InstallOutcome ToGallery(string albumName) => new(InstallOutcomeKind.InstalledToGallery, AlbumName: albumName);
    public static InstallOutcome NeedsManualStep(string stagingFolder) => new(InstallOutcomeKind.InstalledNeedsManualStep, StagingFolder: stagingFolder);
}

public enum RemoveOutcome
{
    Removed,
    RemovedNeedsManualGalleryCleanup,
}

public sealed class InstallException(string message) : Exception(message)
{
    public static InstallException DownloadFailed() => new("Download failed.");
    public static InstallException AuthenticationFailed() => new(
        "Couldn't authenticate with the file server — contact support.");
    public static InstallException LicenseRejected() => new(
        "Licența pentru acest produs nu a fost acceptată (invalidă, expirată, revocată sau pentru alt calculator). Verifică pagina Licență.");
    public static InstallException RateLimited() => new(
        "Prea multe descărcări într-un timp scurt. Încearcă din nou peste câteva minute.");
    public static InstallException ChecksumMismatch() => new("Downloaded file doesn't match the expected checksum.");
    public static InstallException WriteFailed(string detail) => new($"Couldn't write the file: {detail}");
}

/// SECURITATE (raportat de Cristi 2026-08-24, port 1:1 al InstallError.
/// paidResourceInstallFailed din InstallManager.swift): eroare dedicata,
/// aruncata DOAR cand importul automat in Gallery esueaza pentru un
/// PowerGrade PLATIT — vezi comentariul din InstallAsync mai jos. Tip
/// separat (nu doar InstallException cu alt mesaj) ca ProductViewModel sa
/// poata face catch specific si sa arate butonul de contact WhatsApp, nu
/// doar textul de eroare.
public sealed class PaidResourceInstallException() : Exception(
    "A aparut o eroare la incarcarea resursei platite. Te rugam sa contactezi suportul pentru asistenta.");

/// Port 1:1 al InstallManager.swift — descarca un plugin, il verifica, si il
/// copiaza in folderul DaVinci Resolve corespunzator tipului sau (vezi
/// PluginTypeExtensions.InstallDirectory). Incearca intai o scriere directa —
/// pe Windows, spre deosebire de Mac, folderele Resolve (ProgramData) sunt de
/// obicei scriabile de userul curent fara elevare; OFX (Program Files\Common
/// Files) poate cere UAC, caz in care se face fallback la un proces elevat
/// (verb "runas"), echivalentul osascript-ului "with administrator privileges"
/// de pe Mac.
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class InstallManager : INotifyPropertyChanged
{
    public static readonly InstallManager Shared = new();

    private readonly HttpClient _http = HttpClientFactory.Create();

    /// [pluginId: installedVersion]
    public IReadOnlyDictionary<string, string> InstalledVersions => _installedVersions;
    private readonly Dictionary<string, string> _installedVersions = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string StateFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GDCPluginManager", "installed.json");

    private InstallManager()
    {
        LoadState();
    }

    public bool IsInstalled(PluginItem item) => _installedVersions.ContainsKey(item.Id);

    public bool HasUpdate(PluginItem item) =>
        _installedVersions.TryGetValue(item.Id, out var installed) && installed != item.Version;

    /// Un pack se instaleaza in propriul subfolder (numit dupa id, sau dupa
    /// BundleFolderName pentru OFX — acel nume literal e cum il identifica
    /// Resolve). Un item cu un singur fisier se instaleaza direct, flat.
    private static string DestinationDirectory(PluginItem item)
    {
        var baseDir = item.Type.InstallDirectory();
        // [2026-09-14] Scripturile NU intra intr-un subfolder numit dupa id
        // (cum fac pack-urile): Resolve construieste meniul Scripts din
        // subfolderele lui fixe, iar un folder in plus ar insemna un submeniu
        // in plus, cu numele produsului. Merg direct in subfolderul ales.
        if (item.Type == PluginType.Scripts)
        {
            var folder = (item.ScriptFolder ?? Models.ScriptFolder.Utility).ToString();
            return Path.Combine(baseDir, folder);
        }
        if (!item.IsPack) return baseDir;
        return Path.Combine(baseDir, item.BundleFolderName ?? item.Id);
    }

    /// Calea unui fisier relativa la RADACINA PRODUSULUI (nu doar numele de
    /// fisier) - reconstruita din file.Path, mereu in formatul
    /// "id/versiune/rest..." la publicare. Pentru un produs cu un singur
    /// fisier fara subfoldere da acelasi rezultat ca file.Filename dinainte;
    /// conteaza doar pentru pack-uri cu structura de foldere (OFX in
    /// special). Foloseste '/' explicit (nu Path.DirectorySeparatorChar) la
    /// gasirea prefixului, pentru ca file.Path e mereu scris cu '/' (git/
    /// GitHub), indiferent de platforma pe care ruleaza clientul.
    private static string RelativeInstallPath(PluginFile file, PluginItem item)
    {
        var prefix = $"{item.Id}/{item.Version}/";
        if (!file.Path.StartsWith(prefix, StringComparison.Ordinal)) return file.Filename;
        var relative = file.Path[prefix.Length..];
        // Convertim separatorii '/' din repo in cei nativi Windows ('\'),
        // ca Path.Combine sa creeze subfolderele corecte.
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }

    public async Task<InstallOutcome> InstallAsync(PluginItem item)
    {
        var destinationDir = DestinationDirectory(item);
        var tempFiles = new List<string>();
        DiagnosticLog.Write("InstallManager", $"InstallAsync start: item={item.Id} ({item.Name}), type={item.Type}, files={item.Files.Count}, destinationDir={destinationDir}");
        try
        {
            // Verifica checksum-ul FIECARUI fisier inainte sa scrie ceva, ca
            // un fisier corupt intr-un pack sa nu lase in urma un folder
            // pe jumatate instalat.
            foreach (var file in item.Files)
            {
                var data = await FetchAuthorizedFileDataAsync(item.Id, file.Path, file.Sha256);
                var actualSha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                if (actualSha != file.Sha256.ToLowerInvariant())
                {
                    DiagnosticLog.Write("InstallManager", $"Checksum mismatch pe {file.Path}: asteptat {file.Sha256}, primit {actualSha}");
                    throw InstallException.ChecksumMismatch();
                }
                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                await File.WriteAllBytesAsync(tempPath, data);
                tempFiles.Add(tempPath);
            }

            var writtenPaths = new List<string>();
            for (var i = 0; i < item.Files.Count; i++)
            {
                // Bug real, gasit la implementarea OFX (acelasi ca pe Mac,
                // vezi InstallManager.swift): Filename e doar ultima
                // componenta din Path, deci un pack cu subfoldere (un
                // .ofx.bundle intreg pe Windows se distribuie ca un folder
                // de fisiere, nu neaparat plat) se scria FARA structura de
                // foldere, riscand coliziuni de nume intre subfoldere.
                // Fix: reconstruim calea relativa la produs din Files[i].Path
                // (format "id/versiune/rest..." - vezi PublishView.swift pe
                // Mac / echivalentul Furnizor) si o pastram la instalare.
                var relativePath = RelativeInstallPath(item.Files[i], item);
                var destinationPath = Path.Combine(destinationDir, relativePath);
                WriteFile(tempFiles[i], destinationPath, Path.GetDirectoryName(destinationPath) ?? destinationDir);

                // WARNING: nu marca niciodata "Installed" doar pt. ca
                // File.Copy nu a aruncat exceptie - verifica REAL ca
                // fisierul exista pe disc dupa scriere. Gasit 2026-08-22
                // dupa un raport de client: UI arata "Installed" dar
                // fisierele lipseau din DaVinci Resolve. Cauza exacta inca
                // neconfirmata (VirtualStore exclus - vezi app.manifest
                // asInvoker - dar poate exista alt scenariu neasteptat pe
                // masina clientului), insa indiferent de cauza, statusul nu
                // are voie sa minta: daca fisierul chiar nu e acolo, e
                // WriteFailed, nu Installed.
                if (!File.Exists(destinationPath))
                {
                    DiagnosticLog.Write("InstallManager", $"WriteFile pentru {destinationPath} nu a aruncat exceptie, dar File.Exists intoarce false imediat dupa scriere!");
                    throw InstallException.WriteFailed($"Fisierul nu exista dupa scriere: {destinationPath}");
                }
                DiagnosticLog.Write("InstallManager", $"Scris si verificat: {destinationPath}");
                writtenPaths.Add(destinationPath);
            }

            _installedVersions[item.Id] = item.Version;
            SaveState();
            Raise(nameof(InstalledVersions));
            DiagnosticLog.Write("InstallManager", $"InstallAsync succes: {item.Id}, {writtenPaths.Count} fisiere in {destinationDir}");

            if (item.Type != PluginType.PowerGrade) return InstallOutcome.Installed;

            var result = PowerGradeImporter.ImportIntoGallery(item.Name, writtenPaths, destinationDir);
            if (result.Kind == PowerGradeImporter.ImportResultKind.ImportedToGallery)
            {
                return InstallOutcome.ToGallery(result.AlbumName!);
            }

            if (!item.IsFree)
            {
                // SECURITATE: pentru un produs GRATUIT, "staged only" e
                // inofensiv. Pentru unul PLATIT, fisierul .drx verificat
                // ajungea pe disc chiar si cand importul automat esua —
                // exact ce comentariul din PowerGradeImporter.cs ("EXCLUSIV
                // prin Scripting API") voia sa evite: un client putea
                // provoca intentionat esecul (nu deschide Resolve, sau
                // Resolve Free) ca sa obtina fisierul brut si sa-l
                // distribuie neautorizat. Fix: stergem tot ce am scris (nu
                // ramane NIMIC recuperabil pe disc), dezinstalam din
                // starea locala, si aruncam o exceptie dedicata — fara
                // cale de fisier sau instructiuni de instalare manuala
                // (ProductViewModel.cs arata in schimb butonul de contact
                // WhatsApp).
                var stagingFolder = result.StagingFolder ?? destinationDir;
                try { Directory.Delete(stagingFolder, recursive: true); } catch { /* best-effort cleanup */ }
                _installedVersions.Remove(item.Id);
                SaveState();
                Raise(nameof(InstalledVersions));
                DiagnosticLog.Write("InstallManager", $"PowerGrade platit '{item.Id}': import Gallery esuat -> stergere completa (securitate), nu stagedOnly.");
                throw new PaidResourceInstallException();
            }

            return InstallOutcome.NeedsManualStep(result.StagingFolder ?? destinationDir);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("InstallManager", $"InstallAsync EROARE pentru {item.Id}: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            foreach (var temp in tempFiles)
            {
                try { File.Delete(temp); } catch { /* best-effort cleanup */ }
            }
        }
    }

    public RemoveOutcome Remove(PluginItem item)
    {
        var galleryOutcome = item.Type == PluginType.PowerGrade
            ? PowerGradeImporter.RemoveFromGallery(item.Name) switch
            {
                PowerGradeImporter.RemoveResultKind.RemovedFromGallery => RemoveOutcome.Removed,
                _ => RemoveOutcome.RemovedNeedsManualGalleryCleanup,
            }
            : RemoveOutcome.Removed;

        // Scripturile stau intr-un folder COMUN cu al userului — se sterg
        // fisier cu fisier, niciodata tot folderul (vezi nota de pe Mac).
        if (item.IsPack && item.Type != PluginType.Scripts)
        {
            DeleteDirectory(DestinationDirectory(item));
        }
        else if (item.Files.Count > 0)
        {
            DeleteFile(Path.Combine(DestinationDirectory(item), item.Files[0].Filename));
        }

        _installedVersions.Remove(item.Id);
        SaveState();
        Raise(nameof(InstalledVersions));
        return galleryOutcome;
    }

    // MARK: - Descarcarea fisierelor de produs: vezi FetchAuthorizedFileDataAsync (S1)

    /// [2026-09-14] Descarca fisierul unei resurse (PDF/ghid/carte) incarcat
    /// direct in repo-ul privat si il salveaza local — FARA browser.
    /// Port 1:1 al `downloadResourceFile` din InstallManager.swift: acelasi
    /// mecanism autentificat de aducere a octetilor, aceeasi verificare SHA-256,
    /// aceeasi destinatie (folderul ales de user, altfel Downloads).
    /// Intoarce calea locala a fisierului salvat.
    public async Task<string> DownloadResourceFileAsync(DownloadableResource resource, string? preferredFolder = null)
    {
        // [2026-09-14] O resursa poate fi un PACHET (folder cu subfoldere).
        // Forma veche (FilePath) ramane suportata pentru ce e deja publicat.
        List<PluginFile> toDownload;
        if (resource.Files.Count > 0)
        {
            toDownload = resource.Files.ToList();
        }
        else if (!string.IsNullOrWhiteSpace(resource.FilePath))
        {
            toDownload = [new PluginFile { Path = resource.FilePath!, Sha256 = resource.FileSHA256 ?? "", Repo = resource.FileRepo }];
        }
        else
        {
            throw InstallException.DownloadFailed();
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallback = string.IsNullOrEmpty(home) ? Path.GetTempPath() : Path.Combine(home, "Downloads");
        var folder = string.IsNullOrWhiteSpace(preferredFolder) ? fallback : preferredFolder!;
        Directory.CreateDirectory(folder);

        // Un pachet ajunge intr-un folder propriu, ca sa nu imprastie zeci de
        // fisiere direct in Downloads. Un singur fisier ramane un fisier.
        var root = toDownload.Count > 1 ? Path.Combine(folder, resource.Id) : folder;
        Directory.CreateDirectory(root);

        string? firstWritten = null;
        foreach (var file in toDownload)
        {
            var data = await FetchAuthorizedFileDataAsync(resource.Id, file.Path, file.Sha256);
            // Verificarea de integritate nu e optionala doar pentru ca e "doar un
            // PDF": un fisier trunchiat se deschide si arata gol, iar userul ar da
            // vina pe continut, nu pe descarcare.
            if (!string.IsNullOrEmpty(file.Sha256))
            {
                var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
                if (actual != file.Sha256.ToLowerInvariant()) throw InstallException.ChecksumMismatch();
            }
            // Calea relativa la resursa se pastreaza, ca structura pachetului sa
            // ajunga intacta la user.
            var prefix = resource.Id + "/";
            var relative = file.Path.StartsWith(prefix, StringComparison.Ordinal)
                ? file.Path[prefix.Length..]
                : file.Filename;
            var destination = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllBytesAsync(destination, data);
            firstWritten ??= destination;
        }
        return toDownload.Count > 1 ? root : (firstWritten ?? root);
    }

    /// S1 (2026-09-25): octetii unui fisier de produs vin prin `authorize-download`;
    /// clientul nu mai detine niciun credential pentru repo-urile private. Serialul
    /// (daca exista) e reverificat pe server; SHA-256 se verifica si aici, si de apelant.
    private async Task<byte[]> FetchAuthorizedFileDataAsync(string productID, string path, string? sha256)
    {
        var version = typeof(InstallManager).Assembly.GetName().Version?.ToString(3);
        var authorizer = new DownloadAuthorizer(_http, clientVersion: version);
        try
        {
            return await authorizer.FetchAsync(productID, path, sha256, LicenseManager.Shared.SerialFor(productID));
        }
        catch (DownloadAuthorizer.AuthorizationException e)
        {
            DiagnosticLog.Write("InstallManager", $"Autorizare/descarcare esuata pentru {productID}:{path} — {e.Failure} ({e.Status})");
            throw e.Failure switch
            {
                DownloadAuthorizer.Failure.InvalidLicense or DownloadAuthorizer.Failure.RevokedLicense
                    or DownloadAuthorizer.Failure.UnauthorizedPlatform => InstallException.LicenseRejected(),
                DownloadAuthorizer.Failure.RateLimited => InstallException.RateLimited(),
                DownloadAuthorizer.Failure.ChecksumMismatch => InstallException.ChecksumMismatch(),
                _ => InstallException.DownloadFailed(),
            };
        }
    }

    // MARK: - Filesystem, cu fallback la elevare (UAC)

    private void WriteFile(string sourcePath, string destinationPath, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
        catch (Exception) when (IsPermissionIssue())
        {
            // Scriere directa esuata (cel mai probabil OFX sub Program Files) —
            // fallback la un proces elevat (UAC), o singura data pentru aceasta
            // copiere, echivalentul elevatedCopy de pe Mac.
            ElevatedCopy(sourcePath, destinationPath, directory);
        }
    }

    private void DeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception) when (IsPermissionIssue())
        {
            ElevatedRemove(path, recursive: false);
        }
    }

    /// Sterge un intreg subfolder de pack (si tot ce contine).
    private void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception) when (IsPermissionIssue())
        {
            ElevatedRemove(path, recursive: true);
        }
    }

    private static bool IsPermissionIssue() => true; // catch-when generic: orice exceptie de I/O aici e tratata ca permisiuni, la fel ca pe Mac (catch generic -> elevatedCopy)

    private void ElevatedCopy(string sourcePath, string destinationPath, string directory)
    {
        var script = $"mkdir \"{directory}\" 2>nul & copy /Y \"{sourcePath}\" \"{destinationPath}\"";

        // Cererea userului: "o singura parola, nu la fiecare instalare
        // OFX" - implementata cu icacls in loc de a slabi permisiunile pe
        // toata masina. Daca scrierea are loc sub radacina OFX (singurul
        // tip care chiar cere elevare - Program Files\Common Files), in
        // ACELASI script deja elevat (un singur prompt UAC) acordam si
        // userului curent drepturi Modify recursive pe radacina OFX
        // Plugins. Instalarile OFX urmatoare scriu direct, fara UAC.
        var ofxRoot = PluginType.Ofx.InstallDirectory();
        if (directory.StartsWith(ofxRoot, StringComparison.OrdinalIgnoreCase))
        {
            script += $" & icacls \"{ofxRoot}\" /grant \"{Environment.UserName}\":(OI)(CI)M /T";
        }

        RunElevated(script);
    }

    private void ElevatedRemove(string path, bool recursive)
    {
        var script = recursive ? $"rmdir /S /Q \"{path}\"" : $"del /F /Q \"{path}\"";
        RunElevated(script);
    }

    /// Ruleaza o comanda cmd.exe cu privilegii ridicate (declanseaza promptul
    /// UAC nativ) — echivalentul osascript "with administrator privileges" de pe Mac.
    private void RunElevated(string cmdScript)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {cmdScript}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
            if (process is null || process.ExitCode != 0)
            {
                throw InstallException.WriteFailed($"exit code {process?.ExitCode}");
            }
        }
        catch (Win32Exception ex)
        {
            // Utilizatorul a respins promptul UAC.
            throw InstallException.WriteFailed(ex.Message);
        }
    }

    // MARK: - Stare instalare persistata

    private void LoadState()
    {
        try
        {
            if (!File.Exists(StateFilePath)) return;
            var data = File.ReadAllBytes(StateFilePath);
            var decoded = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
            if (decoded is null) return;
            _installedVersions.Clear();
            foreach (var (k, v) in decoded) _installedVersions[k] = v;
        }
        catch
        {
            // Fisier de stare absent/corupt — pornim curat, la fel ca pe Mac.
        }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);
            var data = JsonSerializer.SerializeToUtf8Bytes(_installedVersions);
            File.WriteAllBytes(StateFilePath, data);
        }
        catch
        {
            // Nescriere pe disc nu trebuie sa blocheze UI-ul.
        }
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

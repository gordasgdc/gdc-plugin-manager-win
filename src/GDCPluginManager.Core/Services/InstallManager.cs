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
        "Couldn't authenticate with the file server — contact support, the access token may need renewing.");
    public static InstallException ChecksumMismatch() => new("Downloaded file doesn't match the expected checksum.");
    public static InstallException WriteFailed(string detail) => new($"Couldn't write the file: {detail}");
}

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
        try
        {
            // Verifica checksum-ul FIECARUI fisier inainte sa scrie ceva, ca
            // un fisier corupt intr-un pack sa nu lase in urma un folder
            // pe jumatate instalat.
            foreach (var file in item.Files)
            {
                var data = await FetchPrivateFileDataAsync(file.Path);
                var actualSha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                if (actualSha != file.Sha256.ToLowerInvariant())
                {
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
                writtenPaths.Add(destinationPath);
            }

            _installedVersions[item.Id] = item.Version;
            SaveState();
            Raise(nameof(InstalledVersions));

            if (item.Type != PluginType.PowerGrade) return InstallOutcome.Installed;

            var result = PowerGradeImporter.ImportIntoGallery(item.Name, writtenPaths, destinationDir);
            return result.Kind switch
            {
                PowerGradeImporter.ImportResultKind.ImportedToGallery => InstallOutcome.ToGallery(result.AlbumName!),
                _ => InstallOutcome.NeedsManualStep(result.StagingFolder ?? destinationDir),
            };
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

        if (item.IsPack)
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

    // MARK: - Fetch autentificat din repo-ul privat de fisiere

    /// Fetch al bytes-ilor unui fisier din repo-ul privat gdc-plugin-manager-files
    /// prin GitHub Contents API, cu token-ul read-only (vezi PrivateCatalogAuth).
    /// catalog.json NU se ia asa — doar fisierele produs, care nu stau
    /// niciodata la un URL public.
    private async Task<byte[]> FetchPrivateFileDataAsync(string path)
    {
        var encodedPath = Uri.EscapeDataString(path).Replace("%2F", "/");
        var url = $"https://api.github.com/repos/{PrivateCatalogAuth.Owner}/{PrivateCatalogAuth.Repo}/contents/{encodedPath}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrivateCatalogAuth.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _http.SendAsync(request);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw InstallException.AuthenticationFailed();
        }
        if (!response.IsSuccessStatusCode)
        {
            throw InstallException.DownloadFailed();
        }
        return await response.Content.ReadAsByteArrayAsync();
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

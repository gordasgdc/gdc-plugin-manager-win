using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al SystemDependencyChecker.swift (Mac). O dependenta de sistem
/// verificata la lansare — orice ce NU poate fi bundle-uit direct in
/// aplicatie (.NET e deja bundle-uit self-contained prin CI, deci NU
/// trebuie verificat separat — vezi build-windows.yml).
///
/// Extins 2026-08-26 (Faza 1, "Manager Modular de Dependinte" — vezi
/// CLAUDE.md Partea 1, Regula 4): `IsOptional`/`Detail` adaugate, plus
/// verificari noi pentru folderele de instalare DaVinci Resolve si
/// Scripting API — aliniat 1:1 cu varianta Mac.
public sealed record SystemDependency(
    string Id, string Name, bool IsPresent, string? DownloadUrl,
    bool IsOptional = false, string Detail = "");

[SupportedOSPlatform("windows")]
public static class SystemDependencyChecker
{
    public static IReadOnlyList<SystemDependency> CheckAll() =>
    [
        CheckResolve(),
        CheckVCRedist(),
        CheckFolder("ofx-folder", "Efecte OFX (FX)", OfxPaths.PluginsFolder, "Foldere efecte/DVE — create automat la prima instalare de OFX."),
        CheckFolder("lut-folder", "LUT / DCTL", OfxPaths.LutFolder, "Depozit LUT-uri si DCTL — creat automat la prima instalare."),
        CheckFolder("fusion-folder", "Fusion (Fuse)", OfxPaths.FusionFolder, "Depozit Fuse-uri — creat automat la prima instalare."),
        CheckScriptingApi(),
    ];

    private static SystemDependency CheckResolve()
    {
        string[] candidates =
        [
            @"C:\Program Files\Blackmagic Design\DaVinci Resolve\Resolve.exe",
        ];
        var present = candidates.Any(File.Exists);
        return new SystemDependency("davinci-resolve", "DaVinci Resolve", present,
            "https://www.blackmagicdesign.com/products/davinciresolve",
            IsOptional: false,
            Detail: present ? "Detectat." : "Necesar pentru orice instalare de plugin.");
    }

    /// Multe plugin-uri native (PowerGradeImporter foloseste Python embed,
    /// dar alte componente pot lega la runtime-ul C/C++ Microsoft) au
    /// nevoie de Visual C++ Redistributable — nu vine cu Windows implicit
    /// pe toate versiunile/editiile. Verificam prezenta unei DLL-uri
    /// reprezentative din System32 (vcruntime140.dll), la fel cum verifica
    /// oficial multe installere.
    private static SystemDependency CheckVCRedist()
    {
        var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var present = File.Exists(Path.Combine(systemDir, "vcruntime140.dll"));
        return new SystemDependency("vc-redist", "Visual C++ Redistributable", present,
            "https://aka.ms/vs/17/release/vc_redist.x64.exe",
            IsOptional: false,
            Detail: present ? "Detectat." : "Necesar pentru unele componente native.");
    }

    /// Foldere unde Resolve citeste efectiv fisierele — vezi
    /// PluginType.InstallDirectory() in CatalogModel.cs, aceleasi cai.
    /// Optionale: daca lipsesc acum, apar automat la prima instalare —
    /// nu e nevoie de nicio actiune manuala a userului.
    private static SystemDependency CheckFolder(string id, string name, string path, string detail)
    {
        var present = Directory.Exists(path);
        return new SystemDependency(id, name, present, DownloadUrl: null,
            IsOptional: true,
            Detail: present ? detail : detail + " (nu exista inca — normal, pana la prima instalare)");
    }

    /// Scripting API-ul lui Resolve (python + fusionscript.dll) — folosit
    /// EXCLUSIV pentru import automat de PowerGrade in Gallery (vezi
    /// PowerGradeImporter.cs). Optional: fara el, PowerGrade-urile tot se
    /// instaleaza (staged, import manual din Gallery) — niciodata blocat.
    private static SystemDependency CheckScriptingApi()
    {
        var scriptLib = @"C:\Program Files\Blackmagic Design\DaVinci Resolve\fusionscript.dll";
        var hasLib = File.Exists(scriptLib);
        var pythonRuntime = Path.Combine(AppContext.BaseDirectory, "PythonRuntime", "python.exe");
        var hasPython = File.Exists(pythonRuntime);
        var present = hasLib && hasPython;
        return new SystemDependency("scripting-api", "Scripting API (import automat PowerGrade)", present,
            DownloadUrl: null, IsOptional: true,
            Detail: present ? "Import automat in Gallery activ." : "Fara el, PowerGrade-urile se instaleaza oricum — doar import-ul in Gallery devine manual.");
    }
}

/// Caile foldere folosite si de PluginType.InstallDirectory()
/// (CatalogModel.cs) — duplicate aici intentionat (nu extrase intr-un
/// helper comun) ca sa nu introducem un cuplaj nou intre model si checker
/// pentru o singura folosinta; daca una din cele doua se schimba, cealalta
/// trebuie actualizata manual (verifica ambele la orice schimbare de cale).
[SupportedOSPlatform("windows")]
internal static class OfxPaths
{
    internal static string PluginsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "OFX", "Plugins");

    internal static string LutFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Blackmagic Design", "DaVinci Resolve", "Support", "LUT");

    internal static string FusionFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Blackmagic Design", "DaVinci Resolve", "Support", "Fusion", "Fuses");
}

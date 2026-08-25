using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al SystemDependencyChecker.swift. O dependenta de sistem
/// verificata la lansare — orice ce NU poate fi bundle-uit direct in
/// aplicatie (.NET e deja bundle-uit self-contained prin CI, deci NU
/// trebuie verificat separat — vezi build-windows.yml).
public sealed record SystemDependency(string Id, string Name, bool IsPresent, string? DownloadUrl);

[SupportedOSPlatform("windows")]
public static class SystemDependencyChecker
{
    public static IReadOnlyList<SystemDependency> CheckAll() => [CheckResolve(), CheckVCRedist()];

    private static SystemDependency CheckResolve()
    {
        string[] candidates =
        [
            @"C:\Program Files\Blackmagic Design\DaVinci Resolve\Resolve.exe",
        ];
        var present = candidates.Any(File.Exists);
        return new SystemDependency("davinci-resolve", "DaVinci Resolve", present,
            "https://www.blackmagicdesign.com/products/davinciresolve");
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
            "https://aka.ms/vs/17/release/vc_redist.x64.exe");
    }
}

using System.Diagnostics;
using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

/// Port al ResolveProcessCheck.swift — DaVinci Resolve citeste folderele
/// DCTL/LUT/Fuse doar la pornire, deci instalarea/eliminarea unui fisier
/// cat timp ruleaza nu are efect pana la urmatoarea repornire.
[SupportedOSPlatform("windows")]
public static class ResolveProcessCheck
{
    // Numele procesului pe Windows: "Resolve.exe" -> Process.ProcessName "Resolve".
    private const string ProcessName = "Resolve";

    public static bool IsRunning => Process.GetProcessesByName(ProcessName).Length > 0;
}

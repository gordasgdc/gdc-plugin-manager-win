namespace GDCPluginManager.Core.Services;

/// Numele cheii de dezinstalare scrise de Inno Setup: `...\Uninstall\<AppId>_is1`.
/// Installerele GDC declară `AppId={{X}}`. Inno tratează doar `{{` ca `{` literal; `}}` rămâne
/// neschimbat, deci cheia reală e `{X}}_is1` (verificat în registrul VM-ului, 2026-10-29: DataMover,
/// CursorPro GDC, GDC Plugin Manager). Forma `{X}_is1` rămâne ca rezervă pentru un installer scris altfel.
public static class InnoUninstallKey
{
    public static IReadOnlyList<string> SubKeyNames(string innoAppId) => [innoAppId + "}_is1", innoAppId + "_is1"];
}

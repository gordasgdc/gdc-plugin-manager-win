namespace GDCPluginManager.Core.Services;

/// Comparatie de versiuni pe segmente numerice punctate — extrasa din
/// UpdateChecker (2026-08-29, Etapa 3) ca sa fie folosita si de
/// "Aplicatiile Mele", care compara versiunea instalata a fiecarei aplicatii
/// GDC cu cea publicata. O a doua copie a aceleiasi logici ar fi putut
/// diverge tacut de cea folosita la self-update.
public static class VersionCompare
{
    /// True daca `a` e strict mai noua decat `b`. Compara NUMERIC per segment
    /// (deci 1.10.0 > 1.2.0, spre deosebire de o comparatie lexicografica de
    /// siruri) — identic cu implementarea din Swift.
    ///
    /// Segmentele nenumerice (ex. "1.2.0-beta") se citesc ca 0; suficient
    /// pentru schema de versionare a ecosistemului GDC (MAJOR.MINOR.PATCH,
    /// vezi Regula 14), care nu foloseste sufixe de pre-release.
    public static bool IsNewer(string a, string b)
    {
        var partsA = Parse(a);
        var partsB = Parse(b);
        var len = Math.Max(partsA.Length, partsB.Length);
        for (var i = 0; i < len; i++)
        {
            var x = i < partsA.Length ? partsA[i] : 0;
            var y = i < partsB.Length ? partsB[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    /// Normalizeaza un tag de release ("v2.7.1") la o versiune ("2.7.1").
    /// GitHub `tag_name` poarta prefixul `v` in tot ecosistemul GDC, dar
    /// `update.json` si `AssemblyVersion` nu — fara normalizare, "v2.7.1"
    /// s-ar parsa ca 0.7.1 si ar arata mereu ca fiind MAI VECHE.
    public static string NormalizeTag(string? tag)
    {
        var t = (tag ?? string.Empty).Trim();
        if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t[1..];
        return t;
    }

    private static int[] Parse(string v) =>
        v.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
}

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
        // [2026-09-12] Sufixul de build GDC (`-cg.N`) se desparte INAINTE de
        // parsarea numerica. Fara asta, "3.10.0.dev82-cg.3" si
        // "3.10.0.dev82" produc liste de lungimi diferite, iar numarul de
        // build ajunge comparat cu zero pe o pozitie de versiune — adica o
        // actualizare "disponibila" permanent, oricate s-ar instala.
        // Gasit pe clientul Mac (DisplayCAL-CG); portat aici desi aplicatia
        // nu e inca listata pe Windows, ca defectul sa nu apara la adaugarea ei.
        var (coreA, buildA) = SplitBuild(a);
        var (coreB, buildB) = SplitBuild(b);
        var partsA = Parse(coreA);
        var partsB = Parse(coreB);
        var len = Math.Max(partsA.Length, partsB.Length);
        for (var i = 0; i < len; i++)
        {
            var x = i < partsA.Length ? partsA[i] : 0;
            var y = i < partsB.Length ? partsB[i] : 0;
            if (x != y) return x > y;
        }
        return buildA > buildB;
    }

    /// "3.10.0.dev82-cg.3" -> ("3.10.0.dev82", 3). Fara sufix, build = 0.
    private static (string Core, int Build) SplitBuild(string v)
    {
        var idx = v.LastIndexOf("-cg.", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (v, 0);
        var build = int.TryParse(v[(idx + 4)..], out var n) ? n : 0;
        return (v[..idx], build);
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

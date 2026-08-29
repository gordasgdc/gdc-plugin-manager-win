using System.Globalization;
using System.Text;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al FuzzySearch.swift — cautare fuzzy simpla, fara dependinta
/// externa, potrivita pentru un catalog de marime mica/medie (zeci-sute de
/// intrari), nu pentru indexare de text la scara mare. Doua strategii
/// combinate, identice cu Mac:
/// 1. Substring pe textul normalizat (fara diacritice, fara majuscule) —
///    prinde orice cautare "corecta" sau partiala instant.
/// 2. Distanta Levenshtein marginita per-cuvant — prinde typo-uri (1-2
///    caractere gresite/lipsa/in plus), fara sa devina lenta pe texte lungi.
public static class FuzzySearch
{
    /// Elimina diacriticele si normalizeaza la lowercase, ca "Craciun" si
    /// "craciun" (sau "cafe"/"café") sa se potriveasca identic.
    ///
    /// Echivalentul C# al `folding(options: [.diacriticInsensitive,
    /// .caseInsensitive])` din Swift: descompunem in forma canonica (FormD),
    /// aruncam marcajele care nu ocupa spatiu (accentele devin caractere
    /// separate dupa descompunere), apoi recompunem si coboram la lowercase
    /// invariant (NU ToLower() dependent de cultura — pe o masina cu locale
    /// turceasca "I" ar deveni "ı" si cautarea s-ar rupe silentios).
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// True daca `query` se potriveste cu `text`, exact sau aproximativ.
    /// `query` gol se potriveste mereu (cazul "nimic tastat inca").
    public static bool Matches(string? query, string? text)
    {
        var q = Normalize(query).Trim();
        if (q.Length == 0) return true;

        var t = Normalize(text);
        if (t.Contains(q, StringComparison.Ordinal)) return true;

        // Typo tolerance: distanta de editare marginita, scalata cu lungimea
        // interogarii (un cuvant de 3 litere nu tolereaza 2 greseli — ar
        // deveni un "match cu orice").
        var maxDistance = q.Length <= 4 ? 1 : 2;
        foreach (var word in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Levenshtein(q, word, maxDistance) <= maxDistance) return true;
        }

        return false;
    }

    /// True daca `query` se potriveste in ORICARE dintre campurile date —
    /// helper pentru "cauta in titlu, descriere, tip, id" dintr-un singur loc.
    public static bool MatchesAny(string? query, params string?[] fields)
    {
        var q = Normalize(query).Trim();
        if (q.Length == 0) return true;

        foreach (var field in fields)
        {
            if (string.IsNullOrEmpty(field)) continue;
            if (Matches(q, field)) return true;
        }

        return false;
    }

    /// Distanta Levenshtein, cu iesire timpurie daca depaseste `limit` (nu
    /// conteaza cat de mare e distanta reala peste prag, doar ca a depasit)
    /// — suficient pentru "e un typo plauzibil sau nu".
    private static int Levenshtein(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit) return limit + 1;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }

            if (rowMin > limit) return limit + 1;
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}

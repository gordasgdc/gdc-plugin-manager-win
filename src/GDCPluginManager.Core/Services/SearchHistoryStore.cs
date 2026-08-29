using System.Text.Json;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al SearchHistoryStore din FuzzySearch.swift — istoric de cautari
/// recente, persistat local. Plafonat la 8 intrari (cea mai recenta prima),
/// fara duplicate (comparatie case-insensitive).
///
/// DIFERENTA DE PLATFORMA, nu de comportament: Mac foloseste `UserDefaults`;
/// Windows nu are un echivalent direct, deci scriem un fisier JSON in
/// %AppData%\GDCPluginManager\, exact ca `licenses.json` (LicenseManager) sau
/// `catalog-cache.json` (CatalogService). Starea ramane 100% LOCALA — nu face
/// parte din catalog.json, nu pleaca nicaieri prin retea.
public sealed class SearchHistoryStore
{
    /// Acelasi plafon ca pe Mac.
    private const int Limit = 8;

    private readonly string _filePath;
    private List<string> _recent = [];

    /// Cea mai recenta cautare prima.
    public IReadOnlyList<string> Recent => _recent;

    /// `key` identifica bara de cautare (o singura bara globala acum, dar
    /// pastram parametrul ca pe Mac, ca sa nu trebuiasca schimbata semnatura
    /// daca apare o a doua bara independenta).
    public SearchHistoryStore(string key)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appData, "GDCPluginManager", $"search-history-{key}.json");
        Load();
    }

    public void Record(string? term)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        _recent.RemoveAll(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        _recent.Insert(0, trimmed);
        if (_recent.Count > Limit) _recent = _recent.Take(Limit).ToList();
        Save();
    }

    public void Clear()
    {
        _recent = [];
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
        catch
        {
            // Stergerea de pe disc nu trebuie sa blocheze golirea in memorie.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var data = File.ReadAllBytes(_filePath);
            _recent = JsonSerializer.Deserialize<List<string>>(data) ?? [];
            if (_recent.Count > Limit) _recent = _recent.Take(Limit).ToList();
        }
        catch
        {
            // Fisier corupt/absent — pornim cu istoric gol, la fel ca pe Mac.
            _recent = [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllBytes(_filePath, JsonSerializer.SerializeToUtf8Bytes(_recent));
        }
        catch
        {
            // Nescrierea pe disc nu trebuie sa blocheze sesiunea curenta.
        }
    }
}

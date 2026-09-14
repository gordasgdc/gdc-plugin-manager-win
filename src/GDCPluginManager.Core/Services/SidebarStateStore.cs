using System.Text.Json;

namespace GDCPluginManager.Core.Services;

/// Ce sectiuni ale barei laterale sunt desfasurate. Persistat intre porniri,
/// ca pe Mac (@AppStorage) — altfel fiecare pornire ar cere aceleasi
/// click-uri de restrangere.
///
/// Acelasi tipar ca TextScaleStore: un fisier mic in %AppData%, citit
/// tolerant. Un fisier lipsa, gol sau stricat NU e o eroare — se cade pe
/// valorile implicite, fiindca o preferinta de afisare nu are voie sa
/// impiedice pornirea aplicatiei.
public static class SidebarStateStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "sidebar-state.json");

    /// Implicit: doar prima sectiune deschisa. Meniul porneste compact, dar
    /// niciodata complet gol — un sidebar in care nu se vede nimic la pornire
    /// pare stricat.
    private static readonly Dictionary<string, bool> Defaults = new()
    {
        ["resolveInstall"] = true,
        ["downloadResources"] = false,
        ["community"] = false,
        ["ecosystem"] = false,
        ["account"] = false,
    };

    public static Dictionary<string, bool> Load()
    {
        var state = new Dictionary<string, bool>(Defaults);
        try
        {
            if (!File.Exists(FilePath)) return state;
            var saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(FilePath));
            if (saved is null) return state;
            // Doar cheile cunoscute: una ramasa de la o versiune veche nu are
            // ce cauta in stare, iar una noua isi ia implicitul de mai sus.
            foreach (var pair in saved)
            {
                if (state.ContainsKey(pair.Key)) state[pair.Key] = pair.Value;
            }
        }
        catch
        {
            // Fisier stricat sau inaccesibil: implicitele sunt un raspuns
            // perfect valid, nu un caz de eroare.
        }
        return state;
    }

    public static void Save(Dictionary<string, bool> state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
        }
        catch
        {
            // Nescrierea pe disc nu blocheaza sesiunea curenta — preferinta
            // ramane in memorie pana la urmatoarea pornire.
        }
    }
}

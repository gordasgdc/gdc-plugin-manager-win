using System.Text.Json;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al DownloadLocationStore.swift (Etapa 5, 2026-08-29) — retine unde
/// si-a salvat userul fiecare resursa de download ("sa stie tot timpul unde
/// l-a descarcat").
///
/// Stare 100% LOCALA, cheiata dupa ID-ul resursei — NU face parte din
/// catalog.json (e per-client, nu continut publicat). Pe Mac echivalentul e
/// `UserDefaults`; aici un JSON in %AppData%, ca la SearchHistoryStore.
public static class DownloadLocationStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GDCPluginManager", "download-locations.json");

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(FilePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void Save(Dictionary<string, string> map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, JsonSerializer.SerializeToUtf8Bytes(map));
        }
        catch
        {
            // Nescrierea pe disc nu trebuie sa blocheze sesiunea curenta.
        }
    }

    /// Folderul retinut pentru resursa data, sau null daca userul n-a ales
    /// inca unul — SAU daca folderul a fost intre timp sters/mutat (verificam
    /// existenta, ca sa nu aratam o cale moarta si un buton "Deschide" care
    /// esueaza).
    public static string? Get(string resourceId)
    {
        var path = Load().GetValueOrDefault(resourceId);
        if (string.IsNullOrEmpty(path)) return null;
        return Directory.Exists(path) ? path : null;
    }

    public static void Set(string resourceId, string folderPath)
    {
        var map = Load();
        map[resourceId] = folderPath;
        Save(map);
    }

    public static void Clear(string resourceId)
    {
        var map = Load();
        if (map.Remove(resourceId)) Save(map);
    }
}

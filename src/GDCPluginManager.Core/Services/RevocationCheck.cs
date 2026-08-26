using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al RevocationCheck.swift (Mac) — verificare ONLINE, optionala,
/// peste licentierea existenta (Ed25519, 100% offline). Vezi CLAUDE.md,
/// Partea 1, Regula 12. Kill-switch declansabil manual din Furnizor
/// (Mac), fara nicio schimbare de format criptografic - pur aditiv.
///
/// FAIL-OPEN, niciodata fail-closed: absenta unui raspuns POZITIV de
/// revocare (eroare de retea, offline, request esuat) inseamna NErevocat.
/// O licenta deja activata local nu se blocheaza NICIODATA doar pentru ca
/// utilizatorul e offline.
///
/// Foloseste RPC-ul `is_license_revoked(machine_id, product_id)` din
/// Supabase - NICIODATA un SELECT direct pe tabel (RLS il blocheaza
/// oricum complet pentru cheia anon).
[SupportedOSPlatform("windows")]
public static class RevocationCheck
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly ConcurrentDictionary<string, byte> Revoked = new();

    public static bool IsRevoked(string productId) => Revoked.ContainsKey(productId);

    /// Reverifica toate produsele licentiate local. Apelata la pornire +
    /// dupa fiecare activare noua - niciodata blocanta pentru UI (fiecare
    /// verificare individuala esueaza silentios pe orice problema de retea).
    public static async Task RefreshAsync(IEnumerable<string> productIds)
    {
        foreach (var productId in productIds)
        {
            var revoked = await CheckOneAsync(MachineID.Display, productId);
            if (revoked == true)
            {
                Revoked[productId] = 0;
            }
        }
    }

    private static async Task<bool?> CheckOneAsync(string machineId, string productId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SupabaseConfig.RpcUrl("is_license_revoked"));
            var body = new Dictionary<string, string> { ["p_machine_id"] = machineId, ["p_product_id"] = productId };
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            request.Headers.Add("apikey", SupabaseConfig.AnonKey);
            request.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null; // fail-open

            var text = (await response.Content.ReadAsStringAsync()).Trim();
            return text == "true";
        }
        catch
        {
            return null; // orice eroare de retea -> fail-open, NU revocat
        }
    }
}

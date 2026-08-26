using System.Net.Http;
using System.Text;
using System.Text.Json;

using System.Runtime.Versioning;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al AnalyticsClient.swift (Mac) — scrieri fire-and-forget catre
/// Supabase (inregistrare dispozitiv + eveniment de download). Ambele
/// tabele accepta DOAR INSERT de la cheia anon (vezi SupabaseConfig.cs),
/// deci nu poate niciodata citi/suprascrie/sterge nimic, iar orice eroare
/// e inghitita silentios — analytics-ul nu trebuie sa poata bloca o
/// instalare sau sa strice aplicatia pe o retea proasta.
[SupportedOSPlatform("windows")]
public static class AnalyticsClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static void RegisterDevice(string name, string email)
    {
        var body = new Dictionary<string, string>
        {
            ["machine_id"] = MachineID.Display,
            ["name"] = name.Trim(),
            ["email"] = email.Trim(),
        };
        _ = PostAsync("devices", body);
    }

    public static void LogDownload(string productId, string productName)
    {
        var body = new Dictionary<string, string>
        {
            ["product_id"] = productId,
            ["product_name"] = productName,
            ["machine_id"] = MachineID.Display,
        };
        _ = PostAsync("download_events", body);
    }

    private static async Task PostAsync(string table, Dictionary<string, string> body)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SupabaseConfig.RestUrl(table));
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            request.Headers.Add("apikey", SupabaseConfig.AnonKey);
            request.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
            // Sare peste raspunsul implicit "randul inserat" al PostgREST -
            // fire-and-forget, nu citim niciodata rezultatul.
            request.Headers.Add("Prefer", "return=minimal");
            await Http.SendAsync(request);
        }
        catch
        {
            // Deliberat ignorat - vezi doc-ul de tip de mai sus.
        }
    }
}

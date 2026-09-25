using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GDCPluginManager.Core.Services;

/// S1 (2026-09-25), port 1:1 al DownloadAuthorizer.swift: descarcarea unui fisier
/// de produs prin functia Supabase `authorize-download`. Clientul NU detine niciun
/// credential de stocare: cere autorizare pentru UN fisier, primeste un URL
/// temporar, descarca fara antet de autentificare si verifica SHA-256.
/// Contract: gdc-plugin-manager/supabase/functions/authorize-download/CONTRACT.md.
public sealed class DownloadAuthorizer
{
    public enum Failure
    {
        InvalidLicense, RevokedLicense, UnauthorizedPlatform, UnauthorizedArtifact,
        UnknownProduct, ArtifactUnavailable, RateLimited, MalformedRequest, Server, Network, ChecksumMismatch,
    }

    public sealed class AuthorizationException(Failure failure, int status = 0)
        : Exception($"authorize-download: {failure} ({status})")
    {
        public Failure Failure { get; } = failure;
        public int Status { get; } = status;
    }

    public sealed record Authorization(string Url, string ProductID, string Path, string? Sha256, long? Size);

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _machineID;
    private readonly string _platform;
    private readonly string? _clientVersion;

    public DownloadAuthorizer(HttpClient http, string? endpoint = null, string? apiKey = null,
                              string? machineID = null, string platform = "windows", string? clientVersion = null)
    {
        _http = http;
        _endpoint = endpoint ?? $"{SupabaseConfig.ProjectUrl}/functions/v1/authorize-download";
        _apiKey = apiKey ?? SupabaseConfig.AnonKey;
        _machineID = machineID ?? MachineID.Display;
        _platform = platform;
        _clientVersion = clientVersion;
    }

    public async Task<Authorization> AuthorizeAsync(string productID, string path, string? serial)
    {
        var body = new Dictionary<string, string>
        {
            ["productID"] = productID, ["path"] = path, ["machineID"] = _machineID, ["platform"] = _platform,
        };
        if (!string.IsNullOrEmpty(serial)) body["serial"] = serial!;
        if (_clientVersion is not null) body["clientVersion"] = _clientVersion;

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("apikey", _apiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        HttpResponseMessage response;
        try { response = await _http.SendAsync(request); }
        catch (Exception) { throw new AuthorizationException(Failure.Network); }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK) throw new AuthorizationException(MapError(text), status);
            Authorization? auth = null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                auth = new Authorization(
                    root.GetProperty("url").GetString() ?? "",
                    root.GetProperty("productID").GetString() ?? "",
                    root.GetProperty("path").GetString() ?? "",
                    root.TryGetProperty("sha256", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null,
                    root.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : null);
            }
            catch (Exception) { /* auth ramane null */ }
            if (auth is null || auth.ProductID != productID || auth.Path != path
                || !Uri.TryCreate(auth.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new AuthorizationException(Failure.Server, status);
            }
            return auth;
        }
    }

    /// Autorizare → descarcare → SHA-256; o singura reautorizare daca URL-ul temporar a expirat (403/404).
    public async Task<byte[]> FetchAsync(string productID, string path, string? expectedSha256, string? serial)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var auth = await AuthorizeAsync(productID, path, serial);
            HttpResponseMessage response;
            try { response = await _http.GetAsync(auth.Url); }
            catch (Exception) { throw new AuthorizationException(Failure.Network); }
            using (response)
            {
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound && attempt == 0) continue;
                if (!response.IsSuccessStatusCode) throw new AuthorizationException(Failure.ArtifactUnavailable, (int)response.StatusCode);
                var data = await response.Content.ReadAsByteArrayAsync();
                var expected = new[] { expectedSha256, auth.Sha256 }.FirstOrDefault(s => !string.IsNullOrEmpty(s))?.ToLowerInvariant();
                if (expected is not null && Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant() != expected)
                {
                    throw new AuthorizationException(Failure.ChecksumMismatch);
                }
                return data;
            }
        }
        throw new AuthorizationException(Failure.ArtifactUnavailable);
    }

    internal static Failure MapError(string body)
    {
        string? code = null;
        try { using var doc = JsonDocument.Parse(body); code = doc.RootElement.GetProperty("error").GetString(); }
        catch (Exception) { /* corp necunoscut */ }
        return code switch
        {
            "invalid_license" => Failure.InvalidLicense,
            "revoked_license" => Failure.RevokedLicense,
            "unauthorized_platform" => Failure.UnauthorizedPlatform,
            "unauthorized_artifact" => Failure.UnauthorizedArtifact,
            "unknown_product" => Failure.UnknownProduct,
            "artifact_unavailable" => Failure.ArtifactUnavailable,
            "rate_limited" => Failure.RateLimited,
            "malformed_request" => Failure.MalformedRequest,
            _ => Failure.Server,
        };
    }
}

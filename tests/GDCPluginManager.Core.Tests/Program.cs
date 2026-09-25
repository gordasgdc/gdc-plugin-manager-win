using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GDCPluginManager.Core.Services;

// Teste Core — fără dependențe. Fiecare test aruncă la eșec; rezultatul final e codul de ieșire.
var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Func<Task> run) => tests.Add((name, run));
void Assert(bool cond, string message) { if (!cond) throw new Exception(message); }

const string Product = "gdc-demo";
const string PathInProduct = "gdc-demo/1.0/a.ofx";
var payload = Encoding.UTF8.GetBytes("continut-produs");
var sha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
const string Endpoint = "https://example.supabase.co/functions/v1/authorize-download";

string AuthJson(string? url = null, string? product = null, string? path = null) => JsonSerializer.Serialize(new Dictionary<string, object?>
{
    ["url"] = url ?? "https://raw.example/f?token=t", ["productID"] = product ?? Product, ["path"] = path ?? PathInProduct,
    ["sha256"] = sha, ["size"] = payload.Length, ["issuedAt"] = "x", ["useWithinSeconds"] = 60,
});

(DownloadAuthorizer Authorizer, StubHandler Handler) Make(Func<HttpRequestMessage, (HttpStatusCode, byte[])> respond)
{
    var handler = new StubHandler(respond);
    return (new DownloadAuthorizer(new HttpClient(handler), Endpoint, "anon-public", "AEBAGBAFAY", "windows", "9.9.9"), handler);
}

async Task<DownloadAuthorizer.Failure?> FailureOf(Func<Task> action)
{
    try { await action(); return null; }
    catch (DownloadAuthorizer.AuthorizationException e) { return e.Failure; }
}

Test("descărcare autorizată întoarce octeții verificați; artefactul fără antet de autentificare", async () =>
{
    var (a, h) = Make(r => r.RequestUri!.ToString() == Endpoint ? (HttpStatusCode.OK, Encoding.UTF8.GetBytes(AuthJson())) : (HttpStatusCode.OK, payload));
    var data = await a.FetchAsync(Product, PathInProduct, sha, "SERIAL-1");
    Assert(data.SequenceEqual(payload), "octeți diferiți");
    Assert(h.Requests.Count == 2, $"cereri: {h.Requests.Count}");
    var body = JsonSerializer.Deserialize<Dictionary<string, string>>(h.Bodies[0])!;
    Assert(body["productID"] == Product && body["path"] == PathInProduct && body["machineID"] == "AEBAGBAFAY"
        && body["platform"] == "windows" && body["serial"] == "SERIAL-1" && body["clientVersion"] == "9.9.9", "corp cerere greșit");
    Assert(h.Requests[1].Headers.Authorization is null, "artefactul nu trebuie să poarte autentificare");
});

Test("produs gratuit: fără serial în cerere", async () =>
{
    var (a, h) = Make(r => r.RequestUri!.ToString() == Endpoint ? (HttpStatusCode.OK, Encoding.UTF8.GetBytes(AuthJson())) : (HttpStatusCode.OK, payload));
    await a.FetchAsync(Product, PathInProduct, sha, null);
    Assert(!JsonSerializer.Deserialize<Dictionary<string, string>>(h.Bodies[0])!.ContainsKey("serial"), "serial trimis");
});

Test("erorile serverului se mapează corect", async () =>
{
    var cases = new (HttpStatusCode, string, DownloadAuthorizer.Failure)[]
    {
        (HttpStatusCode.Forbidden, "invalid_license", DownloadAuthorizer.Failure.InvalidLicense),
        (HttpStatusCode.Forbidden, "revoked_license", DownloadAuthorizer.Failure.RevokedLicense),
        (HttpStatusCode.Forbidden, "unauthorized_platform", DownloadAuthorizer.Failure.UnauthorizedPlatform),
        (HttpStatusCode.Forbidden, "unauthorized_artifact", DownloadAuthorizer.Failure.UnauthorizedArtifact),
        (HttpStatusCode.NotFound, "unknown_product", DownloadAuthorizer.Failure.UnknownProduct),
        (HttpStatusCode.NotFound, "artifact_unavailable", DownloadAuthorizer.Failure.ArtifactUnavailable),
        ((HttpStatusCode)429, "rate_limited", DownloadAuthorizer.Failure.RateLimited),
        (HttpStatusCode.BadRequest, "malformed_request", DownloadAuthorizer.Failure.MalformedRequest),
        (HttpStatusCode.InternalServerError, "internal_error", DownloadAuthorizer.Failure.Server),
    };
    foreach (var (status, code, expected) in cases)
    {
        var (a, _) = Make(_ => (status, Encoding.UTF8.GetBytes($"{{\"error\":\"{code}\",\"message\":\"m\"}}")));
        var f = await FailureOf(() => a.FetchAsync(Product, PathInProduct, sha, null));
        Assert(f == expected, $"{code}: {f}");
    }
});

Test("URL expirat: o singură reautorizare, apoi eșec", async () =>
{
    var downloads = 0;
    var (a, h) = Make(r => { if (r.RequestUri!.ToString() == Endpoint) return (HttpStatusCode.OK, Encoding.UTF8.GetBytes(AuthJson())); downloads++; return (HttpStatusCode.Forbidden, Array.Empty<byte>()); });
    var f = await FailureOf(() => a.FetchAsync(Product, PathInProduct, sha, null));
    Assert(f == DownloadAuthorizer.Failure.ArtifactUnavailable && downloads == 2, $"f={f} downloads={downloads}");
});

Test("URL expirat se recuperează cu o autorizare nouă", async () =>
{
    var downloads = 0;
    var (a, _) = Make(r => { if (r.RequestUri!.ToString() == Endpoint) return (HttpStatusCode.OK, Encoding.UTF8.GetBytes(AuthJson())); downloads++; return downloads == 1 ? (HttpStatusCode.NotFound, Array.Empty<byte>()) : (HttpStatusCode.OK, payload); });
    Assert((await a.FetchAsync(Product, PathInProduct, sha, null)).SequenceEqual(payload), "nerecuperat");
});

Test("SHA-256 diferit e respins chiar dacă descărcarea e autorizată", async () =>
{
    var (a, _) = Make(r => r.RequestUri!.ToString() == Endpoint ? (HttpStatusCode.OK, Encoding.UTF8.GetBytes(AuthJson())) : (HttpStatusCode.OK, Encoding.UTF8.GetBytes("alt")));
    Assert(await FailureOf(() => a.FetchAsync(Product, PathInProduct, sha, null)) == DownloadAuthorizer.Failure.ChecksumMismatch, "acceptat");
});

Test("autorizare pentru alt fișier/produs sau URL http e respinsă, fără descărcare", async () =>
{
    foreach (var bad in new[] { AuthJson(path: "gdc-demo/1.0/alt.ofx"), AuthJson(product: "alt"), AuthJson(url: "http://raw.example/f") })
    {
        var (a, h) = Make(r => r.RequestUri!.ToString() == Endpoint ? (HttpStatusCode.OK, Encoding.UTF8.GetBytes(bad)) : (HttpStatusCode.OK, payload));
        Assert(await FailureOf(() => a.FetchAsync(Product, PathInProduct, sha, null)) == DownloadAuthorizer.Failure.Server, "acceptat");
        Assert(h.Requests.Count == 1, "artefactul nu trebuia descărcat");
    }
});

Test("eroare de rețea", async () =>
{
    var (a, _) = Make(_ => throw new HttpRequestException("offline"));
    Assert(await FailureOf(() => a.AuthorizeAsync(Product, PathInProduct, null)) == DownloadAuthorizer.Failure.Network, "nu e Network");
});

Test("LicenseCore: product hash identic cu Swift/TypeScript (SHA-512(\"abc\")[:4])", () =>
{
    Assert(LicenseCore.ProductHash("abc").SequenceEqual(new byte[] { 0xDD, 0xAF, 0x35, 0xA1 }), "hash diferit");
    return Task.CompletedTask;
});

Test("LicenseCore: base32 round-trip", () =>
{
    var data = Enumerable.Range(0, 87).Select(i => (byte)(i * 7)).ToArray();
    Assert(LicenseCore.Base32Decode(LicenseCore.Base32Encode(data))!.SequenceEqual(data), "round-trip");
    return Task.CompletedTask;
});

Test("UpdatePackageVerifier: versiune, SHA-256, rezultat WinVerifyTrust, thumbprint", () =>
{
    Assert(UpdatePackageVerifier.IsValidVersion("1.37.1"), "1.37.1 validă");
    foreach (var bad in new[] { null, "", "1.37", "1.37.1-beta", "1.37.1\"; calc", "../1.0.0" })
        Assert(!UpdatePackageVerifier.IsValidVersion(bad), $"versiune invalidă acceptată: {bad}");
    var file = Path.GetTempFileName();
    File.WriteAllBytes(file, payload);
    try
    {
        var actual = UpdatePackageVerifier.Sha256Hex(file);
        Assert(actual == sha, "SHA-256 calculat greșit");
        UpdatePackageVerifier.CheckSha256(null, actual);          // manifest vechi: nimic de verificat
        UpdatePackageVerifier.CheckSha256(sha.ToUpperInvariant(), actual);
        foreach (var bad in new[] { new string('0', 64), "xyz" })
        {
            var threw = false;
            try { UpdatePackageVerifier.CheckSha256(bad, actual); } catch (UpdatePackageVerifier.VerificationException) { threw = true; }
            Assert(threw, $"SHA-256 greșit acceptat: {bad}");
        }
    }
    finally { File.Delete(file); }
    Assert(UpdatePackageVerifier.IsAcceptableTrustResult(0), "semnătură validă refuzată");
    Assert(UpdatePackageVerifier.IsAcceptableTrustResult(0x800B0109), "self-signed (rădăcină neîncrezută) refuzat");
    foreach (var bad in new uint[] { 0x80096010 /*digest greșit*/, 0x800B0100 /*nesemnat*/, 0x800B0101 /*expirat*/, 0x800B010C /*revocat*/ })
        Assert(!UpdatePackageVerifier.IsAcceptableTrustResult(bad), $"rezultat acceptat greșit: 0x{bad:X8}");
    UpdatePackageVerifier.CheckSignerThumbprint("fa7d4925035bab6ed01c778fb3c821d174ebd934");
    var foreign = false;
    try { UpdatePackageVerifier.CheckSignerThumbprint("0000000000000000000000000000000000000000"); } catch (UpdatePackageVerifier.VerificationException) { foreign = true; }
    Assert(foreign, "certificat străin acceptat");
    return Task.CompletedTask;
});

Test("UpdatePackageVerifier: exe-ul nesemnat e refuzat (doar pe Windows)", () =>
{
    if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
    var file = Path.Combine(Path.GetTempPath(), $"gdc-unsigned-{Guid.NewGuid()}.exe");
    File.WriteAllBytes(file, payload);
    try
    {
        var threw = false;
        try { UpdatePackageVerifier.VerifySignature(file); } catch (UpdatePackageVerifier.VerificationException) { threw = true; }
        Assert(threw, "fișier nesemnat acceptat");
    }
    finally { File.Delete(file); }
    return Task.CompletedTask;
});

// CI: instalatorul semnat în aceeași rulare (GDC_SIGNED_EXE) trebuie ACCEPTAT, iar o copie cu un
// octet modificat trebuie REFUZATĂ (digest greșit) — dovada că WinVerifyTrust verifică integritatea
// înainte de rădăcina neîncrezută a certificatului self-signed.
Test("UpdatePackageVerifier: instalatorul GDC semnat e acceptat, copia alterată e refuzată (CI)", () =>
{
    var signed = Environment.GetEnvironmentVariable("GDC_SIGNED_EXE");
    if (string.IsNullOrEmpty(signed) || !OperatingSystem.IsWindows()) { Console.WriteLine("  (sărit: GDC_SIGNED_EXE nesetat)"); return Task.CompletedTask; }
    Assert(File.Exists(signed), $"lipsește {signed}");
#pragma warning disable SYSLIB0057
    using (var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
        System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(signed)))
#pragma warning restore SYSLIB0057
        Assert(UpdatePackageVerifier.TrustedSignerThumbprints.Contains(cert.Thumbprint.ToUpperInvariant()), $"amprentă neașteptată: {cert.Thumbprint}");
    UpdatePackageVerifier.VerifySignature(signed);      // aruncă dacă e refuzat
    var tampered = Path.Combine(Path.GetTempPath(), $"gdc-tampered-{Guid.NewGuid()}.exe");
    var bytes = File.ReadAllBytes(signed);
    bytes[bytes.Length / 2] ^= 0xFF;                    // în conținut, departe de antet și de semnătura de la final
    File.WriteAllBytes(tampered, bytes);
    try
    {
        var threw = false;
        try { UpdatePackageVerifier.VerifySignature(tampered); } catch (UpdatePackageVerifier.VerificationException) { threw = true; }
        Assert(threw, "instalator alterat acceptat");
    }
    finally { File.Delete(tampered); }
    return Task.CompletedTask;
});

var failed = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"✓ {name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"✗ {name}: {e.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} teste trec");
return failed == 0 ? 0 : 1;

sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode, byte[])> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> Bodies { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        var (status, body) = respond(request);
        return new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
    }
}

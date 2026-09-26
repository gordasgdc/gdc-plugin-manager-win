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

// ---------- Faza 6: paritate funcțională cu Mac 1.40.0 / Furnizor 1.53.0 ----------
string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
var looseJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

Test("catalog: catalog.json publicat se decodează; câmpurile necunoscute sunt ignorate", async () =>
{
    var raw = await File.ReadAllTextAsync(Fixture("catalog.json"));
    var catalog = JsonSerializer.Deserialize<GDCPluginManager.Core.Models.Catalog>(raw, CatalogJsonOptions.Default);
    Assert(catalog is not null && catalog.Items.Count > 0, "catalogul real nu s-a decodat");
    var node = System.Text.Json.Nodes.JsonNode.Parse(raw)!.AsObject();
    node["campFuturTopLevel"] = 1;
    foreach (var item in node["items"]!.AsArray()) item!.AsObject()["campFuturProdus"] = new System.Text.Json.Nodes.JsonObject { ["x"] = 1 };
    var extended = JsonSerializer.Deserialize<GDCPluginManager.Core.Models.Catalog>(node.ToJsonString(), CatalogJsonOptions.Default);
    Assert(extended is not null && extended.Items.Count == catalog.Items.Count, "un câmp nou rupe decodarea catalogului");
});

Test("banner: launch-banner.json publicat (clasic) — fără campanii, bannerul clasic rămâne", async () =>
{
    var cfg = JsonSerializer.Deserialize<GDCPluginManager.Core.Models.LaunchBannerConfig>(await File.ReadAllBytesAsync(Fixture("launch-banner.json")), looseJson)!;
    Assert(cfg.Campaigns is null && cfg.ActiveCampaign(DateTime.UtcNow) is null, "nu există campanii în producție");
    Assert(cfg.IsDisplayable == (cfg.Enabled && cfg.TopText != "" && cfg.MainText != ""), "logica clasică neschimbată");
});

Test("banner: campanii în formatul scris de Furnizor (date Swift, linkURL, mod necunoscut)", async () =>
{
    var cfg = JsonSerializer.Deserialize<GDCPluginManager.Core.Models.LaunchBannerConfig>(await File.ReadAllBytesAsync(Fixture("launch-banner-campaigns.json")), looseJson)!;
    Assert(cfg.Campaigns?.Count == 4, "campaniile nu s-au decodat");
    var active = cfg.ActiveCampaign(new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc));
    Assert(active?.Id == "now", $"campania activă greșită: {active?.Id}");
    Assert(active!.Mode == GDCPluginManager.Core.Models.PromoBannerMode.ImageText, "modul imageText");
    Assert(active.LinkUrl == "https://gordas.dev/x", "linkURL nemapat");
    Assert(active.TextFor("en")?.Main == "Pachete festive", "EN gol trebuie să cadă pe RO");
    Assert(cfg.Campaigns![3].Mode == GDCPluginManager.Core.Models.PromoBannerMode.Text, "modul necunoscut → text");
    Assert(cfg.IsDisplayable, "clienții vechi văd textul de rezervă");
});

Test("banner: listă de campanii stricată nu ascunde bannerul clasic", () =>
{
    var json = """{"enabled":true,"topText":"A","mainText":"B","campaigns":"nu-e-listă"}""";
    var cfg = JsonSerializer.Deserialize<GDCPluginManager.Core.Models.LaunchBannerConfig>(json, looseJson)!;
    Assert(cfg.Campaigns is null && cfg.IsDisplayable, "lista stricată trebuie ignorată");
    var cfg2 = JsonSerializer.Deserialize<GDCPluginManager.Core.Models.LaunchBannerConfig>("""{"enabled":true,"topText":"A","mainText":"B","campaigns":[{"id":5}]}""", looseJson)!;
    Assert(cfg2.Campaigns is null && cfg2.IsDisplayable, "element invalid → toată lista ignorată, ca pe Mac");
    return Task.CompletedTask;
});

Test("banner: layout „Doar imagine” identic cu Mac (6:1 / 12:1 de la 900, plafon 160)", () =>
{
    var n = GDCPluginManager.Core.Models.PromoBannerSpec.ImageOnlyLayout(470, true);
    Assert(!n.UseWide && Math.Abs(n.Height - 470 / 6.0) < 0.01, "îngust");
    var w = GDCPluginManager.Core.Models.PromoBannerSpec.ImageOnlyLayout(1150, true);
    Assert(w.UseWide && Math.Abs(w.Height - 1150 / 12.0) < 0.01, "lat");
    Assert(GDCPluginManager.Core.Models.PromoBannerSpec.ImageOnlyLayout(1150, false).Height == 160, "plafon");
    return Task.CompletedTask;
});

Test("versiunea candidat e mai nouă decât cea publică (clientul 1.37.2 o va oferi)", () =>
{
    Assert(VersionCompare.IsNewer("1.38.1", "1.37.2") && !VersionCompare.IsNewer("1.37.2", "1.38.1"), "comparația de versiuni");
    return Task.CompletedTask;
});

Test("update.json publicat: secțiunea windows se decodează", async () =>
{
    using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(Fixture("update.json")));
    var windows = doc.RootElement.EnumerateObject().First(p => p.Name.Equals("windows", StringComparison.OrdinalIgnoreCase)).Value;
    var info = windows.Deserialize<UpdateInfo>(looseJson);
    Assert(info is not null && !string.IsNullOrEmpty(info.Version), "secțiunea windows lipsă/nedecodabilă");
});

Test("ProductActionState: aceeași ordine de prioritate ca pe Mac", () =>
{
    ProductActionState D(bool c = true, bool u = true, bool b = false, string? i = null, string v = "1.0", bool f = false, bool o = false) =>
        ProductActionState.Derive(c, u, b, i, v, f, o);
    Assert(D(false, false, true, "0.9") is ProductActionState.Incompatible, "incompatibil primul");
    Assert(D(true, false, true) is ProductActionState.LicenseRequired, "licența înaintea operației");
    Assert(D(b: true, f: true, o: true) is ProductActionState.Installing, "operația în curs");
    Assert(D(i: null, f: true) is ProductActionState.Failed { IsUpdate: false }, "eșec instalare");
    Assert(D(i: "0.9", f: true) is ProductActionState.Failed { IsUpdate: true }, "eșec actualizare");
    Assert(D(i: "1.0", f: true) is ProductActionState.Installed, "eșec la Elimină pe produs la zi → instalat");
    Assert(D(o: true) is ProductActionState.Offline && D() is ProductActionState.NotInstalled, "offline informativ");
    Assert(D(i: "2.0") is ProductActionState.UpdateAvailable { InstalledVersion: "2.0", Latest: "1.0" }, "orice diferență = actualizare");
    return Task.CompletedTask;
});

// Licențiere: aceiași vectori ca LicenseCoreTests.swift, cu o cheie de TEST (seed fix) — cheia de producție nu e atinsă.
var testSeed = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
var testKey = new Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters(testSeed, 0);
var testPub = Convert.ToBase64String(testKey.GeneratePublicKey().GetEncoded());
var thisMachine = new byte[] { 1, 2, 3, 4, 5, 6 };
string MakeSerial(string product = "gdc-demo", long expiresAt = 0, byte[]? machine = null, byte? platform = null,
                  Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters? signer = null)
{
    var payload = new List<byte>(LicenseCore.ProductHash(product));
    for (var shift = 56; shift >= 0; shift -= 8) payload.Add((byte)((expiresAt >> shift) & 0xFF));
    payload.AddRange(new byte[] { 9, 9, 9, 9 });
    payload.AddRange(machine ?? new byte[6]);
    if (platform is { } p) payload.Add(p);
    var s = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
    s.Init(true, signer ?? testKey);
    s.BlockUpdate(payload.ToArray(), 0, payload.Count);
    var raw = LicenseCore.Base32Encode(payload.Concat(s.GenerateSignature()).ToArray());
    return string.Join("-", Enumerable.Range(0, (raw.Length + 4) / 5).Select(i => raw.Substring(i * 5, Math.Min(5, raw.Length - i * 5))));
}
LicenseCore.ValidationErrorKind? Kind(string serial, string product = "gdc-demo", bool hwid = true, long now = 1_000_000)
{
    try { LicenseCore.Validate(serial, product, hwid, testPub, () => thisMachine, now); return null; }
    catch (LicenseCore.ValidationError e) { return e.Kind; }
}

Test("licență: vectorii Mac (v1, v2, expirare, produs, semnătură, mașină, platformă)", () =>
{
    Assert(Kind(MakeSerial()) is null, "v1 perpetuu valid");
    Assert(Kind(MakeSerial(machine: thisMachine, platform: 3)) is null, "v2 legat de mașină, cross-platform");
    Assert(Kind(MakeSerial().ToLowerInvariant().Replace("-", " ")) is null, "insensibil la majuscule și separatori");
    Assert(Kind(MakeSerial(expiresAt: 2_000_000)) is null && Kind(MakeSerial(expiresAt: 500)) == LicenseCore.ValidationErrorKind.Expired, "expirare");
    Assert(Kind(MakeSerial(), product: "alt-produs") == LicenseCore.ValidationErrorKind.WrongProduct, "alt produs");
    var foreign = new Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters(Enumerable.Repeat((byte)7, 32).ToArray(), 0);
    Assert(Kind(MakeSerial(signer: foreign)) == LicenseCore.ValidationErrorKind.BadSignature, "semnătură străină");
    Assert(Kind("ABC") == LicenseCore.ValidationErrorKind.MalformedCode, "cod malformat");
    Assert(Kind(MakeSerial(machine: new byte[] { 9, 9, 9, 9, 9, 9 })) == LicenseCore.ValidationErrorKind.WrongMachine, "alt calculator");
    Assert(Kind(MakeSerial(machine: thisMachine), hwid: false) == LicenseCore.ValidationErrorKind.HwidUnavailable, "HWID indisponibil ≠ alt calculator");
    Assert(Kind(MakeSerial(platform: 1)) == LicenseCore.ValidationErrorKind.WrongPlatform, "serial doar-Mac respins pe Windows");
    Assert(Kind(MakeSerial(platform: 2)) is null && Kind(MakeSerial(platform: 200)) is null, "doar-Windows valid; octet necunoscut = oricare");
    Console.WriteLine($"  vector comun Mac/Windows: pub={testPub} serial={MakeSerial(product: "gdc-parity-vector", platform: 3)}");
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

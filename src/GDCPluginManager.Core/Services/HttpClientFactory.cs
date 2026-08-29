using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Linq;

namespace GDCPluginManager.Core.Services;

/// HttpClient cu un User-Agent setat implicit — GitHub API respinge cu 403
/// ("Request forbidden by administrative rules") orice cerere fara acest
/// header. curl si URLSession (Mac) il trimit implicit; HttpClient in .NET
/// NU — bug real, confirmat direct (o cerere identica, doar fara User-Agent,
/// primeste exact acelasi 403 pe care InstallManager il trateaza — corect,
/// in general, dar gresit in cazul asta — ca "token invalid/expirat").
/// PUBLIC din 2026-08-29 (Etapa 3): "Aplicatiile Mele" (in proiectul Client)
/// interogheaza `api.github.com`, care REFUZA cu 403 orice cerere fara
/// User-Agent. Reutilizam acelasi factory in loc sa cream un HttpClient gol
/// in Client si sa uitam antetul.
///
/// [2026-08-29] `PooledConnectionLifetime` explicit — NU a rezolvat eroarea
/// SSL de la filigran (verificat: log-ul tot arata RemoteCertificateNameMismatch
/// dupa v1.19.9). Ipoteza initiala ("conexiune veche reutilizata") era gresita:
/// o conexiune TLS esuata NU ramane in pool-ul .NET, deci incercarea 2 (la
/// 800ms) e oricum o conexiune noua — si tot esueaza identic cu incercarea 1.
/// Ramane totusi corect ca practica generala (recomandat de Microsoft pentru
/// clienti care vorbesc cu servicii din spatele unui CDN/load balancer), doar
/// NU era cauza reala aici.
///
/// [2026-08-29, v2] `RemoteCertificateValidationCallback` de DIAGNOSTIC — in
/// loc sa mai ghicim (interceptare AV vs. nod Cloudflare prost configurat),
/// logam explicit certificatul REAL primit (Subject/Issuer/Thumbprint) si
/// motivul exact de refuz (`SslPolicyErrors`) la orice esec de validare.
/// NU schimba comportamentul de securitate — tot respinge orice certificat
/// invalid (return false la eroare), doar il face vizibil in log inainte.
///
/// [2026-08-29, v3] Certificat fals gasit (`core1.netops.test`/"Packetland"),
/// IDENTIC pe curl (mereu OK) si HttpClient (mereu esuat) catre EXACT acelasi
/// host `gordas.dev`, in aceeasi sesiune, in acelasi moment — deci nu mai e
/// retea/VPN/AV generic (verificat, eliminate pe rand). Singura diferenta
/// reala ramasa intre curl si .NET: curl pe aceasta masina NU suporta deloc
/// HTTP/2 (confirmat: `curl --http2` esueaza cu "libcurl version does not
/// support this"), in timp ce `SocketsHttpHandler` poate oferi ALPN h2.
/// Fortam explicit HTTP/1.1, fara ALPN h2 deloc — elimina complet aceasta
/// variabila, indiferent daca era cauza reala sau nu.
public static class HttpClientFactory
{
    public static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions =
            {
                RemoteCertificateValidationCallback = ValidateAndLog,
                ApplicationProtocols = [System.Net.Security.SslApplicationProtocol.Http11],
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            },
        };
        var client = new HttpClient(handler)
        {
            DefaultRequestVersion = new Version(1, 1),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GDCPluginManager", "1.0"));
        return client;
    }

    private static bool ValidateAndLog(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None) return true;

        var cert2 = certificate as X509Certificate2;
        var chainStatus = chain is null
            ? "(fara chain)"
            : string.Join(", ", chain.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation.Trim()}"));

        DiagnosticLog.Write("TLS", $"Certificat respins ({errors}). "
            + $"Subject=\"{certificate?.Subject}\" Issuer=\"{certificate?.Issuer}\" "
            + $"Thumbprint={cert2?.Thumbprint} ChainStatus=[{chainStatus}]");

        return false; // pastram comportamentul de securitate implicit — respingem tot.
    }
}

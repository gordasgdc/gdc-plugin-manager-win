using System.Net.Http.Headers;

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
/// [2026-08-29] `PooledConnectionLifetime` explicit — gasit real, din log:
/// `RemoteCertificateNameMismatch` intermitent DOAR pe `HttpClient` (static,
/// traieste cat aplicatia), niciodata pe `curl` (10/10 OK, conexiune noua de
/// fiecare data, verificat live). `gordas.dev` e in spatele Cloudflare
/// (anycast, IP-uri multiple) — implicit, `SocketsHttpHandler` tine o
/// conexiune TLS deschisa LA INFINIT; daca acea conexiune ajunge sa fie
/// servita de un nod/cert care nu se mai potriveste intre timp, TOATE
/// cererile ulterioare pe ea esueaza, in timp ce orice unealta cu conexiune
/// noua merge mereu. Reciclare la 5 minute = aceeasi robustete ca un
/// handshake nou per cerere, fara costul unei conexiuni noi la fiecare fetch.
public static class HttpClientFactory
{
    public static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GDCPluginManager", "1.0"));
        return client;
    }
}

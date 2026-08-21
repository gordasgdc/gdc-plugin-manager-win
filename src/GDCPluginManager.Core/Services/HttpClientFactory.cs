using System.Net.Http.Headers;

namespace GDCPluginManager.Core.Services;

/// HttpClient cu un User-Agent setat implicit — GitHub API respinge cu 403
/// ("Request forbidden by administrative rules") orice cerere fara acest
/// header. curl si URLSession (Mac) il trimit implicit; HttpClient in .NET
/// NU — bug real, confirmat direct (o cerere identica, doar fara User-Agent,
/// primeste exact acelasi 403 pe care InstallManager il trateaza — corect,
/// in general, dar gresit in cazul asta — ca "token invalid/expirat").
internal static class HttpClientFactory
{
    public static HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GDCPluginManager", "1.0"));
        return client;
    }
}

using System.ComponentModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al CatalogService.swift — fetch catalog de pe gordas.dev, cu
/// cache local ca app-ul sa arate ceva (marcat stale) si offline. Acelasi
/// catalog.json e citit si de clientul Mac, deci orice produs urcat din
/// Furnizor (Mac) apare automat aici, fara nicio schimbare pe partea asta.
public sealed class CatalogService : INotifyPropertyChanged
{
    public static readonly CatalogService Shared = new();

    // gordas.dev e domeniul custom pt. gdc-plugin-manager (root) — URL direct,
    // identic cu cel din clientul Mac.
    public static readonly Uri CatalogUri = new("https://gordas.dev/catalog.json");

    private readonly HttpClient _http = HttpClientFactory.Create();
    private readonly string _cacheFilePath;

    public IReadOnlyList<PluginItem> Items { get; private set; } = [];
    public IReadOnlyList<Course> Courses { get; private set; } = [];
    public IReadOnlyList<AppLink> Apps { get; private set; } = [];
    public IReadOnlyList<EducationalResource> EducationalResources { get; private set; } = [];
    public IReadOnlyList<Event> Events { get; private set; } = [];
    public IReadOnlyList<PartnerStore> PartnerStores { get; private set; } = [];
    public IReadOnlyList<ServiceCenter> ServiceCenters { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public string? LoadError { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private CatalogService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheFilePath = Path.Combine(appData, "GDCPluginManager", "catalog-cache.json");
        LoadFromCache();
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Raise(nameof(IsLoading));
        LoadError = null;
        Raise(nameof(LoadError));

        try
        {
            // catalog.json e servit cu cache-control: max-age=600 — un refresh
            // explicit trebuie sa il ocoleasca, altfel s-ar putea vedea o
            // varianta veche chiar dupa ce userul a cerut explicit reincarcare.
            using var request = new HttpRequestMessage(HttpMethod.Get, CatalogUri);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new CatalogFetchException(CatalogFetchErrorKind.BadStatus, (int)response.StatusCode);
            }

            var data = await response.Content.ReadAsByteArrayAsync();
            Catalog catalog;
            try
            {
                catalog = JsonSerializer.Deserialize<Catalog>(data, CatalogJsonOptions.Default)
                          ?? throw new JsonException("null catalog");
            }
            catch
            {
                throw new CatalogFetchException(CatalogFetchErrorKind.DecodeFailed, null);
            }

            Items = catalog.Items;
            Courses = catalog.Courses;
            Apps = catalog.Apps;
            EducationalResources = catalog.EducationalResources;
            Events = catalog.Events;
            PartnerStores = catalog.PartnerStores;
            ServiceCenters = catalog.ServiceCenters;
            Raise(nameof(Items));
            Raise(nameof(Courses));
            Raise(nameof(Apps));
            Raise(nameof(EducationalResources));
            Raise(nameof(Events));
            Raise(nameof(PartnerStores));
            Raise(nameof(ServiceCenters));
            SaveToCache(data);
        }
        catch (CatalogFetchException ex)
        {
            // Pastreaza ce era deja incarcat din cache — arata eroarea doar
            // daca nu avem absolut nimic de afisat. Distinge DE CE a esuat
            // (parse vs status server vs retea), la fel ca pe Mac.
            if (Items.Count > 0) return;
            LoadError = ex.Kind switch
            {
                CatalogFetchErrorKind.DecodeFailed => "Catalogul nu a putut fi interpretat (versiune veche de aplicatie?).",
                CatalogFetchErrorKind.BadStatus => $"Serverul a raspuns cu eroare ({ex.StatusCode}).",
                _ => "Nu s-a putut incarca catalogul.",
            };
            Raise(nameof(LoadError));
        }
        catch
        {
            if (Items.Count > 0) return;
            LoadError = "Nu s-a putut incarca catalogul. Verifica conexiunea la internet.";
            Raise(nameof(LoadError));
        }
        finally
        {
            IsLoading = false;
            Raise(nameof(IsLoading));
        }
    }

    private enum CatalogFetchErrorKind { BadStatus, DecodeFailed }

    private sealed class CatalogFetchException(CatalogFetchErrorKind kind, int? statusCode) : Exception
    {
        public CatalogFetchErrorKind Kind { get; } = kind;
        public int? StatusCode { get; } = statusCode;
    }

    private void LoadFromCache()
    {
        try
        {
            if (!File.Exists(_cacheFilePath)) return;
            var data = File.ReadAllBytes(_cacheFilePath);
            var catalog = JsonSerializer.Deserialize<Catalog>(data, CatalogJsonOptions.Default);
            if (catalog is null) return;
            Items = catalog.Items;
            Courses = catalog.Courses;
            Apps = catalog.Apps;
            EducationalResources = catalog.EducationalResources;
            Events = catalog.Events;
            PartnerStores = catalog.PartnerStores;
            ServiceCenters = catalog.ServiceCenters;
        }
        catch
        {
            // Cache corupt/absent — pornim cu liste goale, la fel ca pe Mac.
        }
    }

    private void SaveToCache(byte[] data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
            File.WriteAllBytes(_cacheFilePath, data);
        }
        catch
        {
            // Nescriere pe disc nu trebuie sa blocheze afisarea catalogului deja in memorie.
        }
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

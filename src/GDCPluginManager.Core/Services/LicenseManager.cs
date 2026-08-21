using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al LicenseManager.swift — detine starea de licenta per-produs.
/// Aplicatia insasi e gratuita — oricine poate instala si vedea catalogul —
/// doar instalarea/actualizarea unui produs anume cere licenta acelui produs.
/// Acelasi crypto/format ca orice alt produs GDC (LicenseCore), aplicat o
/// data per item, nu global pe toata aplicatia.
[SupportedOSPlatform("windows")]
public sealed class LicenseManager : INotifyPropertyChanged
{
    public static readonly LicenseManager Shared = new();

    /// [productId: payload verificat] — reconstruit de pe disc la pornire
    /// si dupa fiecare activare reusita. Un product id apare aici doar daca
    /// serialul stocat inca valideaza (expirarea/machine-lock se reverifica
    /// la fiecare incarcare, nu se cacheaza ca un simplu bool).
    public IReadOnlyDictionary<string, LicenseCore.Payload> LicensedProducts => _licensedProducts;
    private readonly Dictionary<string, LicenseCore.Payload> _licensedProducts = new();

    public string? ActivationError { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// [productId: cod serial brut] — singurul lucru persistat; payload-urile
    /// sunt mereu re-derivate prin re-validare la incarcare.
    private static string StoreFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GDCPluginManager", "licenses.json");

    private LicenseManager()
    {
        LoadSavedLicenses();
    }

    public bool IsLicensed => _licensedProducts.Count > 0;

    /// Verificarea dinaintea instalarii/actualizarii/eliminarii unui item —
    /// produsele gratuite nu au nevoie de nicio licenta; restul au nevoie
    /// de a lor proprie. Fara proba globala de aplicatie: aplicatia e
    /// gratuita, doar produsele platite se deblocheaza, iar cele gratuite
    /// sunt pur si simplu... gratuite.
    public bool IsUnlocked(PluginItem item) => item.IsFree || _licensedProducts.ContainsKey(item.Id);

    /// Valideaza un cod lipit fata de fiecare produs din catalog. Un serial
    /// contine doar un HASH al product id-ului (vezi formatul din
    /// LicenseCore), nu id-ul insusi, deci nu exista alta metoda sa aflam
    /// pentru ce produs e codul decat sa incercam candidati — ieftin si
    /// complet local pentru un catalog de dimensiunea asta.
    public bool Activate(string code, IReadOnlyList<string> candidateProductIds)
    {
        ActivationError = null;
        var trimmed = code.Trim();

        if (candidateProductIds.Count == 0)
        {
            ActivationError = "Catalogul nu a fost inca incarcat — asteapta reincarcarea, apoi incearca din nou.";
            Raise(nameof(ActivationError));
            return false;
        }

        // Malformed/bad-signature/wrong-machine/expired sunt toate
        // independente de candidatul verificat — raspunsul primului
        // candidat e autoritativ pentru ele. Doar .WrongProduct depinde
        // de candidat, deci doar pe acela merita sa iteram.
        var lastErrorKind = LicenseCore.ValidationErrorKind.WrongProduct;
        foreach (var productId in candidateProductIds)
        {
            try
            {
                var payload = LicenseCore.Validate(trimmed, productId);
                SaveLicense(productId, trimmed);
                _licensedProducts[productId] = payload;
                Raise(nameof(LicensedProducts));
                Raise(nameof(IsLicensed));
                return true;
            }
            catch (LicenseCore.ValidationError ex) when (ex.Kind == LicenseCore.ValidationErrorKind.WrongProduct)
            {
                lastErrorKind = ex.Kind;
            }
            catch (LicenseCore.ValidationError ex)
            {
                ActivationError = Message(ex.Kind);
                Raise(nameof(ActivationError));
                return false;
            }
        }
        ActivationError = Message(lastErrorKind);
        Raise(nameof(ActivationError));
        return false;
    }

    public void Deactivate(string productId)
    {
        _licensedProducts.Remove(productId);
        Raise(nameof(LicensedProducts));
        Raise(nameof(IsLicensed));

        var store = LoadStore();
        if (store is null) return;
        store.Remove(productId);
        WriteStore(store);
    }

    private void LoadSavedLicenses()
    {
        var store = LoadStore();
        if (store is null) return;
        foreach (var (productId, code) in store)
        {
            try
            {
                var payload = LicenseCore.Validate(code, productId);
                _licensedProducts[productId] = payload;
            }
            catch (LicenseCore.ValidationError)
            {
                // Cod stocat nu mai valideaza (expirat/masina schimbata) — il ignoram, ramane in fisier ca istoric.
            }
        }
    }

    private static void SaveLicense(string productId, string code)
    {
        var store = LoadStore() ?? new Dictionary<string, string>();
        store[productId] = code;
        WriteStore(store);
    }

    private static Dictionary<string, string>? LoadStore()
    {
        try
        {
            if (!File.Exists(StoreFilePath)) return null;
            var data = File.ReadAllBytes(StoreFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(data);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteStore(Dictionary<string, string> store)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StoreFilePath)!);
            File.WriteAllBytes(StoreFilePath, JsonSerializer.SerializeToUtf8Bytes(store));
        }
        catch
        {
            // Nescriere pe disc nu trebuie sa blocheze activarea in memorie pentru sesiunea curenta.
        }
    }

    private static string Message(LicenseCore.ValidationErrorKind kind) => kind switch
    {
        LicenseCore.ValidationErrorKind.MalformedCode => "Cod invalid — verifica sa fie copiat complet, fara spatii lipsa.",
        LicenseCore.ValidationErrorKind.BadSignature => "Cod invalid — semnatura nu corespunde.",
        LicenseCore.ValidationErrorKind.WrongProduct => "Acest cod nu e valabil pentru niciun produs din catalog.",
        LicenseCore.ValidationErrorKind.WrongMachine => "Acest cod e legat de o alta masina.",
        LicenseCore.ValidationErrorKind.Expired => "Acest cod a expirat.",
        _ => "Activare esuata.",
    };

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

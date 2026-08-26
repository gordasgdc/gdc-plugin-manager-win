using System.ComponentModel;
using System.Linq;
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

    /// Kill-switch diferentiat (decizie 2026-08-24): cate secunde tinem
    /// licentele anterior-valide "active" cand board UUID nu poate fi citit
    /// acum (WMI restrictionat, VM etc.) — global, nu per produs, fiindca
    /// board UUID e acelasi indiferent de produs.
    private const long GracePeriodSeconds = 5 * 24 * 60 * 60; // 5 zile

    private static string GraceFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GDCPluginManager", "last_good_hwid.txt");

    private static long ReadLastGoodTimestamp()
    {
        try { return long.Parse(File.ReadAllText(GraceFilePath).Trim()); }
        catch { return 0; }
    }

    private static void WriteLastGoodTimestamp(long ts)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GraceFilePath)!);
            File.WriteAllText(GraceFilePath, ts.ToString());
        }
        catch
        {
            // Nescriere pe disc nu trebuie sa blocheze sesiunea curenta.
        }
    }

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
    public bool IsUnlocked(PluginItem item) =>
        item.IsFree || (_licensedProducts.ContainsKey(item.Id) && !RevocationCheck.IsRevoked(item.Id));

    /// Reverifica revocarea online (fail-open, vezi RevocationCheck.cs)
    /// pentru toate produsele licentiate curent. Apelata la pornire -
    /// niciodata sincron/blocanta pentru UI.
    public Task RefreshRevocationsAsync() => RevocationCheck.RefreshAsync(_licensedProducts.Keys.ToList());

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
                _ = RevocationCheck.RefreshAsync(new[] { productId });
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

    /// Reincarca si reverifica fiecare cod stocat — niciodata un flag
    /// cache-uit (vezi GDC-SEC-05). Kill-switch diferentiat (decizie
    /// 2026-08-24) dupa tipul erorii:
    ///   - BadSignature/MalformedCode -> tamper evident: elimina din store
    ///     de pe disc (hard lock), nu doar din memorie.
    ///   - HwidUnavailable -> grace period: daca ultima verificare buna e
    ///     mai recenta de GracePeriodSeconds, ramane licentiat (foloseste
    ///     payload-ul atasat erorii); altfel demo (nu-l eliminam din store).
    ///   - WrongMachine/WrongProduct/Expired -> demo, codul ramane pe disc
    ///     (nu e tamper, poate fi hardware schimbat legitim).
    private void LoadSavedLicenses()
    {
        var store = LoadStore();
        if (store is null) return;

        var hwidAvailable = MachineID.IsAvailable;
        var lastGood = ReadLastGoodTimestamp();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var graceActive = lastGood != 0 && (now - lastGood) < GracePeriodSeconds;
        var anyValidated = false;
        var toRemove = new List<string>();

        foreach (var (productId, code) in store)
        {
            try
            {
                var payload = LicenseCore.Validate(code, productId, hwidAvailable);
                _licensedProducts[productId] = payload;
                anyValidated = true;
            }
            catch (LicenseCore.ValidationError ex) when (
                ex.Kind is LicenseCore.ValidationErrorKind.BadSignature
                        or LicenseCore.ValidationErrorKind.MalformedCode)
            {
                // Tamper evident — elimina codul falsificat/corupt de pe disc.
                toRemove.Add(productId);
            }
            catch (LicenseCore.ValidationError ex) when (
                ex.Kind == LicenseCore.ValidationErrorKind.HwidUnavailable && graceActive && ex.Payload is { } gracePayload)
            {
                _licensedProducts[productId] = gracePayload; // grace activ — pastreaza starea buna anterioara
            }
            catch (LicenseCore.ValidationError)
            {
                // HwidUnavailable (grace expirat) / WrongMachine / WrongProduct / Expired
                // -> mod demo, codul ramane pe disc ca istoric.
            }
        }

        if (anyValidated)
        {
            WriteLastGoodTimestamp(now);
        }
        if (toRemove.Count > 0)
        {
            foreach (var id in toRemove) store.Remove(id);
            WriteStore(store);
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
        LicenseCore.ValidationErrorKind.WrongPlatform => "Acest cod e valabil pentru alta platforma (Mac/Windows).",
        LicenseCore.ValidationErrorKind.WrongMachine => "Acest cod e legat de o alta masina.",
        LicenseCore.ValidationErrorKind.HwidUnavailable => "Nu am putut citi identificatorul hardware acum — incearca din nou.",
        LicenseCore.ValidationErrorKind.Expired => "Acest cod a expirat.",
        _ => "Activare esuata.",
    };

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

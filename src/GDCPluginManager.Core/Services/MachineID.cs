using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace GDCPluginManager.Core.Services;

/// Port al MachineID.swift pentru Windows — acelasi principiu (un ID hardware
/// stabil, SHA-512, primii 6 octeti, Base32 fara liniute), dar sursa ID-ului
/// e diferita: pe Mac e IOPlatformUUID (IOKit); pe Windows NU produce acelasi
/// hash ca pe Mac pentru aceeasi masina fizica (surse diferite) — asta e de
/// asteptat, fiecare platforma isi are propriul spatiu de coduri
/// machine-locked, generate separat din Furnizor.
///
/// GDC-SEC-02 (audit securitate 2026-08-24): board UUID singur (WMI
/// Win32_ComputerSystemProduct) era deja mai robust decat MachineGuid din
/// Registry folosit in celelalte componente Windows (Production Manager,
/// gdc-resolve-encoder), dar tot un singur factor. Acum combinam board UUID
/// + serialul discului fizic (Win32_DiskDrive) — schimbarea unuia singur nu
/// mai schimba ID-ul rezultat.
///
/// FORMULA STRICTA pe Windows (obligatorie identic in Python, C#, C++ — vezi
/// docs/MACHINE_ID.md sau echivalentul din celelalte repo-uri GDC):
///
///     raw = trim(Win32_ComputerSystemProduct.UUID) + "|" + trim(Win32_DiskDrive[0].SerialNumber)
///     hash = SHA-512(raw), primii 6 octeti, Base32
///
/// Orice implementare noua TREBUIE sa respecte exact acest format (inclusiv
/// trim-ul si separatorul "|"), altfel machine_id-ul afisat difera intre
/// componente pentru aceeasi masina, iar licentele Windows deja emise devin
/// invalide.
[SupportedOSPlatform("windows")]
public static class MachineID
{
    /// Interogheaza WMI pentru prima valoare a unei proprietati dintr-o clasa
    /// data. Null daca WMI e indisponibil (rulare fara privilegii, VM
    /// restrictionata etc.) — apelantul decide fallback-ul.
    private static string? QueryWmiFirst(string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var obj in searcher.Get())
            {
                var value = obj[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value) && value != "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
                {
                    return value;
                }
            }
        }
        catch
        {
            // WMI indisponibil (rulare fara privilegii, VM restrictionata, etc.)
        }
        return null;
    }

    /// Board UUID + serial disc, unite cu "|" — vezi formula stricta de mai
    /// sus. `available=false` inseamna ca board UUID n-a putut fi citit acum
    /// (WMI restrictionat, VM, etc.) — kill-switch-ul diferentiat (decizie
    /// 2026-08-24, vezi LicenseManager.cs) NU trateaza asta ca "alta masina".
    private static (string raw, bool available) RawPlatformUuid()
    {
        var boardUuid = QueryWmiFirst("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID")?.Trim();
        var diskSerial = QueryWmiFirst("SELECT SerialNumber FROM Win32_DiskDrive", "SerialNumber")?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(boardUuid))
        {
            return ("win-machine-id-unavailable", false);
        }
        return ($"{boardUuid}|{diskSerial}", true);
    }

    /// Hash-ul de 6 octeti folosit atat pentru afisare cat si pentru
    /// machine-locking-ul codurilor de licenta.
    public static byte[] HashBytes =>
        SHA512.HashData(Encoding.UTF8.GetBytes(RawPlatformUuid().raw))[..6];

    /// String Base32 scurt, lizibil (fara liniute) — ce copiaza userul din
    /// Preferinte -> Licenta si trimite inainte sa cumpere.
    public static string Display => LicenseCore.Base32Encode(HashBytes);

    /// True daca board UUID a putut fi citit efectiv acum — vezi nota despre
    /// kill-switch diferentiat de mai sus.
    public static bool IsAvailable => RawPlatformUuid().available;
}

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace GDCPluginManager.Core.Services;

/// Verificările SelfUpdater-ului înainte de a rula un instalator descărcat (pereche:
/// UpdatePackageVerifier.swift pe Mac). Un instalator se rulează DOAR dacă:
///   1. versiunea anunțată e X.Y.Z (intră în numele fișierului);
///   2. arhiva are SHA-256-ul din update.json, când manifestul îl declară;
///   3. semnătura Authenticode a exe-ului e intactă (WinVerifyTrust) și e făcută cu
///      certificatul GDC (thumbprint fixat). Certificatul e self-signed (Regula 34),
///      deci singurul refuz tolerat de WinVerifyTrust e „rădăcină neîncrezută”.
public static class UpdatePackageVerifier
{
    /// Certificatul comun GDC de semnare Windows (CN=CG Convertor (Self-Signed, testare interna),
    /// expiră 2031-09-06). La schimbarea certificatului, thumbprint-ul nou se adaugă aici
    /// ÎNAINTE de primul release semnat cu el.
    public static readonly IReadOnlyList<string> TrustedSignerThumbprints = ["FA7D4925035BAB6ED01C778FB3C821D174EBD934"];

    public sealed class VerificationException(string message) : Exception(message);

    public static bool IsValidVersion(string? version) =>
        version is not null && Regex.IsMatch(version, @"^\d{1,4}\.\d{1,4}\.\d{1,6}$");

    public static string Sha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// Fără valoare așteptată → nimic de verificat (manifeste vechi). Cu valoare → trebuie să fie
    /// un SHA-256 hex valid ȘI egal cu cel calculat.
    public static void CheckSha256(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected)) return;
        if (!Regex.IsMatch(expected, "^[0-9a-fA-F]{64}$"))
            throw new VerificationException("Suma de control din update.json nu e validă.");
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new VerificationException("Arhiva descărcată nu corespunde versiunii publicate (SHA-256 diferit).");
    }

    /// true = rezultatul WinVerifyTrust e acceptabil: semnătură validă (0) sau validă cu rădăcină
    /// neîncrezută (certificat self-signed neimportat). Orice altceva (digest greșit, nesemnat,
    /// certificat expirat/revocat, format invalid) = refuz.
    public static bool IsAcceptableTrustResult(uint result) => result is 0 or CertEUntrustedRoot;

    public static void CheckSignerThumbprint(string? thumbprint)
    {
        if (thumbprint is null || !TrustedSignerThumbprints.Contains(thumbprint.ToUpperInvariant()))
            throw new VerificationException("Instalatorul nu e semnat cu certificatul GDC.");
    }

    [SupportedOSPlatform("windows")]
    public static void VerifySignature(string exePath)
    {
        var result = WinVerifyTrustFile(exePath);
        if (!IsAcceptableTrustResult(result))
            throw new VerificationException($"Semnătura instalatorului nu e validă (0x{result:X8}).");
#pragma warning disable SYSLIB0057
        using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(exePath));
#pragma warning restore SYSLIB0057
        CheckSignerThumbprint(cert.Thumbprint);
    }

    /// Toate verificările, în ordine. Aruncă VerificationException la primul eșec.
    [SupportedOSPlatform("windows")]
    public static void Verify(string version, string archivePath, string? expectedSha256, string exePath)
    {
        if (!IsValidVersion(version)) throw new VerificationException("Versiunea anunțată nu e validă.");
        CheckSha256(expectedSha256, Sha256Hex(archivePath));
        VerifySignature(exePath);
    }

    // ---- WinVerifyTrust (wintrust.dll) ----
    const uint CertEUntrustedRoot = 0x800B0109;
    static readonly Guid ActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WintrustFileInfo
    {
        public uint cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WintrustData
    {
        public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData;
        public uint dwUIChoice; public uint fdwRevocationChecks; public uint dwUnionChoice; public IntPtr pFile;
        public uint dwStateAction; public IntPtr hWVTStateData; public IntPtr pwszURLReference;
        public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    static extern uint WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WintrustData data);

    [SupportedOSPlatform("windows")]
    static uint WinVerifyTrustFile(string path)
    {
        var file = new WintrustFileInfo { cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(), pcwszFilePath = path };
        var filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(file, filePtr, false);
            var data = new WintrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustData>(),
                dwUIChoice = 2,            // WTD_UI_NONE
                fdwRevocationChecks = 0,   // WTD_REVOKE_NONE (certificat self-signed, fără CRL)
                dwUnionChoice = 1,         // WTD_CHOICE_FILE
                pFile = filePtr,
                dwStateAction = 0,         // WTD_STATEACTION_IGNORE
                dwProvFlags = 0x00000010,  // WTD_CACHE_ONLY_URL_RETRIEVAL: fără acces la rețea
            };
            return WinVerifyTrust(new IntPtr(-1), ActionGenericVerifyV2, ref data);
        }
        finally
        {
            Marshal.DestroyStructure<WintrustFileInfo>(filePtr);
            Marshal.FreeHGlobal(filePtr);
        }
    }
}

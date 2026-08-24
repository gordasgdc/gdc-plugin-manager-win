using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace GDCPluginManager.Core.Services;

/// Port 1:1 al LicenseCore.swift — validator de seriale format GDC, aceeasi
/// schema binara ca license_core.py / license_check.cpp, ca un cod generat
/// cu sell.py sa mearga neschimbat pe orice client GDC (Mac, Windows,
/// gdc-resolve-encoder), toate impartind aceeasi cheie de semnare, acelasi
/// admin tool (Furnizor, doar pe Mac) si acelasi customers.csv.
///
/// Format: Base32(grupat cu liniute) din
/// [4 octeti hash produs][8 octeti expirare big-endian][4 octeti nonce]
/// [6 octeti hash masina][64 octeti semnatura Ed25519] — 22 + 64 = 86 octeti.
/// Hash produs = SHA-512(product id)[:4]. Hash masina = SHA-512(machine id)[:6],
/// sau all-zero daca codul nu e legat de o masina anume.
///
/// Verificarea foloseste DOAR cheia PUBLICA — cheia privata care semneaza
/// coduri noi ramane pe Mac-ul lui Cristi (Furnizor), niciodata aici.
[SupportedOSPlatform("windows")]
public static class LicenseCore
{
    public readonly record struct Payload(long ExpiresAt, bool MachineLocked); // ExpiresAt: unix seconds, 0 = nu expira niciodata

    public enum ValidationErrorKind
    {
        MalformedCode,
        BadSignature,
        WrongProduct,
        WrongMachine,
        /// GDC-SEC (kill-switch diferentiat, decizie 2026-08-24): board UUID
        /// n-a putut fi citit acum (WMI restrictionat, VM etc.) — distinct de
        /// WrongMachine, ca sa nu blocam un client cinstit pentru o eroare
        /// temporara. Vezi LicenseManager.cs pentru logica de grace period.
        HwidUnavailable,
        Expired,
    }

    public sealed class ValidationError(ValidationErrorKind kind, long expiredAt = 0, Payload? payload = null) : Exception
    {
        public ValidationErrorKind Kind { get; } = kind;
        public long ExpiredAt { get; } = expiredAt;

        /// Populat pentru WrongMachine/HwidUnavailable/Expired — cazuri unde
        /// codul e altfel valid criptografic si LicenseManager are nevoie de
        /// payload ca sa decida politica (ex. grace period) fara sa re-parseze.
        public Payload? Payload { get; } = payload;
    }

    /// Base64 al cheii PUBLICE Ed25519 din keygen.py (public_key.txt) al
    /// gdc-license-system — identica cu cea din LicenseCore.swift.
    private const string PublicKeyBase64 = "I1h23MNMRbOhc0ObKJrfa3oFHKA9w+SzbNrroAIy8hs=";

    public const int PayloadSize = 22;

    /// Valideaza un serial introdus/lipit de user fata de expectedProductID.
    /// hwidAvailable=false (grace-period / re-verificare periodica, vezi
    /// LicenseManager.cs) inseamna ca board UUID n-a putut fi citit acum —
    /// o nepotrivire de masina in acest caz arunca HwidUnavailable, nu
    /// WrongMachine (evita un fals-pozitiv pentru un client cinstit). La
    /// activarea interactiva (default hwidAvailable=true) comportamentul
    /// ramane neschimbat.
    public static Payload Validate(string serial, string expectedProductId, bool hwidAvailable = true)
    {
        var packed = Base32Decode(serial);
        if (packed is null || packed.Length != PayloadSize + 64)
        {
            throw new ValidationError(ValidationErrorKind.MalformedCode);
        }

        var payloadBytes = packed[..PayloadSize];
        var signature = packed[PayloadSize..];

        var publicKeyBytes = Convert.FromBase64String(PublicKeyBase64);
        var publicKey = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, publicKey);
        verifier.BlockUpdate(payloadBytes, 0, payloadBytes.Length);
        if (!verifier.VerifySignature(signature))
        {
            throw new ValidationError(ValidationErrorKind.BadSignature);
        }

        var storedProductHash = payloadBytes[..4];
        var expectedProductHash = ProductHash(expectedProductId);
        if (!storedProductHash.AsSpan().SequenceEqual(expectedProductHash))
        {
            throw new ValidationError(ValidationErrorKind.WrongProduct);
        }

        long expiresAt = 0;
        for (var i = 4; i < 12; i++) expiresAt = (expiresAt << 8) | payloadBytes[i];

        var storedMachineHash = payloadBytes[16..22];
        var isMachineLocked = storedMachineHash.Any(b => b != 0);
        var payload = new Payload(expiresAt, isMachineLocked);
        if (isMachineLocked)
        {
            if (!hwidAvailable)
            {
                throw new ValidationError(ValidationErrorKind.HwidUnavailable, payload: payload);
            }
            if (!storedMachineHash.AsSpan().SequenceEqual(MachineID.HashBytes))
            {
                throw new ValidationError(ValidationErrorKind.WrongMachine, payload: payload);
            }
        }

        if (expiresAt != 0 && expiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            throw new ValidationError(ValidationErrorKind.Expired, expiresAt, payload);
        }

        return payload;
    }

    public static byte[] ProductHash(string productId) =>
        SHA512.HashData(Encoding.UTF8.GetBytes(productId))[..4];

    // MARK: - Base32 (RFC 4648, compatibil cu base64.b32encode/decode din Python)

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        int bits = 0, value = 0;
        var output = new StringBuilder();
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Base32Alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0)
        {
            output.Append(Base32Alphabet[(value << (5 - bits)) & 0x1F]);
        }
        return output.ToString();
    }

    public static byte[]? Base32Decode(string input)
    {
        var cleaned = input.ToUpperInvariant().Replace("-", "").Replace(" ", "").Replace("=", "");
        int bits = 0, value = 0;
        var output = new List<byte>();
        foreach (var ch in cleaned)
        {
            var index = Base32Alphabet.IndexOf(ch);
            if (index < 0) return null;
            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}

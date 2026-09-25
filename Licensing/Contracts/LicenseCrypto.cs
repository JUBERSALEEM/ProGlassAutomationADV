// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseCrypto.cs
// PROJECT: ProGlass.Licensing
// MODULE: Cryptographic primitive — asymmetric signing / verification
//
// Pure .NET. No WPF, no app services, no logging, no DB, no stubs.
//
// ALGORITHM CHOICE
// ────────────────
// Ed25519 is the "preferred" algorithm, but it is NOT available on .NET 8
// (System.Security.Cryptography.Ed25519 ships in .NET 9+). To keep this
// library consumable by the current net8.0 main app and generator, RSA-2048
// with PSS/SHA-256 is used. It is a modern, secure asymmetric signature
// scheme, consistent with the rest of the app's crypto (Authenticode,
// password hashing). The algorithm is a documented constant and a `keyId`
// is embedded in every signature, so a different algorithm (Ed25519) can be
// introduced as a NEW keyId later without breaking existing licenses.
//
// CANONICAL BYTES
// ───────────────
// The signature is always over `LicenseCanonical.SerializeUtf8(document)` —
// the deterministic, whitespace-free, fixed-key-order representation. Two
// identical documents always produce the exact same bytes, so verification
// is exact (see LicenseCanonical).
// ═══════════════════════════════════════════════════════════════════════

using System.Security.Cryptography;

namespace ProGlass.Licensing;

/// <summary>
/// Cryptographic constants. The algorithm is part of the signed envelope so
/// a future reader knows exactly how a given key must be used.
/// </summary>
public static class LicenseCrypto
{
    public const string Algorithm = "RSA-PSS-SHA256";
    public const int KeySizeBits = 2048;

    public static HashAlgorithmName Hash => HashAlgorithmName.SHA256;

    /// <summary>Sign the canonical bytes with the given private key XML.</summary>
    public static string SignCanonical(byte[] canonical, string privateKeyXml)
    {
        if (canonical is null) throw new ArgumentNullException(nameof(canonical));
        if (string.IsNullOrWhiteSpace(privateKeyXml))
            throw new ArgumentException("Private key (RSA XML) is required.", nameof(privateKeyXml));

        using var rsa = RSA.Create(KeySizeBits);
        rsa.FromXmlString(privateKeyXml);
        var sig = rsa.SignData(canonical, Hash, RSASignaturePadding.Pss);
        return Convert.ToBase64String(sig);
    }

    /// <summary>
    /// Verify the canonical bytes against a public key XML. PSS ONLY —
    /// the PKCS#1 v1.5 acceptance fallback was removed in the 2026-08-29
    /// security cross-check: no shipped license uses v1.5, and keeping a
    /// second verification path widens the acceptance surface for no
    /// reason.
    /// </summary>
    public static bool VerifyCanonical(byte[] canonical, string signatureBase64, string publicKeyXml)
    {
        try
        {
            if (canonical is null || string.IsNullOrWhiteSpace(signatureBase64) || string.IsNullOrWhiteSpace(publicKeyXml))
                return false;

            using var rsa = RSA.Create(KeySizeBits);
            rsa.FromXmlString(publicKeyXml);
            // Production licenses are RSA-2048. A future rotation may add a
            // larger key beside it. Anything smaller is rejected here, and the
            // license document is never allowed to select a weaker algorithm.
            if (rsa.KeySize < KeySizeBits)
                return false;

            byte[] sig = Convert.FromBase64String(signatureBase64);
            if (sig.Length != rsa.KeySize / 8)
                return false;

            return rsa.VerifyData(canonical, sig, Hash, RSASignaturePadding.Pss);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Generate a fresh RSA-2048 key pair; returns (publicXml, privateXml).</summary>
    public static (string PublicKeyXml, string PrivateKeyXml) GenerateKeyPair()
    {
        using var rsa = RSA.Create(KeySizeBits);
        return (rsa.ToXmlString(includePrivateParameters: false),
                rsa.ToXmlString(includePrivateParameters: true));
    }

    /// <summary>
    /// Short, stable identifier for a public key (first 8 bytes of SHA-256 of
    /// the Modulus, hex). This is the `keyId` embedded in every signature and
    /// what the generator shows for the "which key signed this?" question.
    /// </summary>
    public static string Fingerprint(string publicKeyXml)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(publicKeyXml);
            var p = rsa.ExportParameters(false);
            if (p.Modulus is null || p.Modulus.Length == 0)
                return "UNKNOWN";

            var hash = SHA256.HashData(p.Modulus);
            var hex = new System.Text.StringBuilder(16);
            for (int i = 0; i < 8; i++) hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }
        catch
        {
            return "UNKNOWN";
        }
    }
}

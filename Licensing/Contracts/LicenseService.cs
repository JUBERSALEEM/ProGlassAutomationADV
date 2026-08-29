// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseService.cs
// PROJECT: ProGlass.Licensing
// MODULE: The single authoritative licensing API
//
// This is the ONLY place that generates and verifies licenses. Neither the
// generator UI nor the main app implements its own sign/verify logic — they
// call into here. It is pure: no logging, no DB, no WPF, no app services.
// The caller (generator or app) formats its own messages from structured
// results.
//
// GENERATION ALWAYS SELF-VERIFIES:
//   Create → Canonical Serialize → Sign → Serialize file → Read back →
//   Verify signature → Validate → only then Success. If ANY stage fails the
//   result is a failure — we never report a success for a broken license.
// ═══════════════════════════════════════════════════════════════════════

using System.IO;

namespace ProGlass.Licensing;

/// <summary>Input for creating a new license.</summary>
public sealed class LicenseRequest
{
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string? Company { get; set; }
    public string? Contact { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }

    public string LicenseType { get; set; } = "Subscription";
    public int DaysValid { get; set; } = 365;                  // for Trial / Subscription
    public DateTime? IssuedAtUtc { get; set; }                 // default = now
    public DateTime? ExpiresAtUtc { get; set; }                // explicit override

    public List<string> Features { get; set; } = new();

    // Device policy
    public string DeviceBinding { get; set; } = "Unbound";     // Unbound | SingleDevice | MultiDevice
    public string? DeviceFingerprint { get; set; }
    public int? DeviceLimit { get; set; }

    // Version policy (default = unlimited future compatibility)
    public string? MinimumApplicationVersion { get; set; } = "1.0.0";
    public string? MaximumApplicationVersion { get; set; }
    public List<int>? AllowedMajorVersions { get; set; }
}

public static class LicenseService
{
    /// <summary>
    /// Create, sign, serialize, re-read, re-verify and validate a license.
    /// Returns Success ONLY if every stage passes. Callers (generator) log
    /// the structured result; they never re-implement signing.
    /// </summary>
    public static LicenseGenerationResult Create(
        LicenseRequest req, string privateKeyXml, string keyId,
        string? outputPath = null)
    {
        try
        {
            if (req is null) return new LicenseGenerationResult { Success = false, ErrorCode = LicenseErrorCode.InvalidPayload, Message = "No request." };
            if (string.IsNullOrWhiteSpace(privateKeyXml))
                return new LicenseGenerationResult { Success = false, ErrorCode = LicenseErrorCode.MissingKey, Message = "No private key." };

            // Build the document. Expiry resolved by license type:
            //   Trial / Subscription → issuedAt + DaysValid (or explicit)
            //   Lifetime / PerpetualVersion / Enterprise(optional) → null expiry
            var issued = (req.IssuedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
            DateTime? expires = req.ExpiresAtUtc?.ToUniversalTime();

            if (!expires.HasValue && IsExpiringType(req.LicenseType))
                expires = issued.AddDays(Math.Max(1, req.DaysValid));

            var doc = new LicenseDocument
            {
                LicenseFormatVersion = LicenseFormat.Version,
                LicenseId = Guid.NewGuid().ToString(),
                ProductId = ProductIds.ProGlassAutomation,
                Customer = new CustomerInfo
                {
                    Id = req.CustomerId ?? "",
                    Name = req.CustomerName ?? "",
                    Company = req.Company,
                    Contact = req.Contact,
                    Email = req.Email,
                    Notes = req.Notes
                },
                LicenseType = req.LicenseType ?? "Subscription",
                IssuedAtUtc = issued,
                ExpiresAtUtc = expires,
                Features = (req.Features ?? new List<string>()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList(),
                DevicePolicy = new DevicePolicy
                {
                    Binding = string.IsNullOrWhiteSpace(req.DeviceBinding) ? "Unbound" : req.DeviceBinding,
                    DeviceLimit = req.DeviceLimit,
                    Fingerprint = req.DeviceFingerprint
                },
                VersionPolicy = new VersionPolicy
                {
                    MinimumApplicationVersion = req.MinimumApplicationVersion,
                    MaximumApplicationVersion = req.MaximumApplicationVersion,
                    AllowedMajorVersions = req.AllowedMajorVersions
                }
            };

            // Sign the canonical bytes.
            byte[] canonical = LicenseCanonical.SerializeUtf8(doc);
            string signature = LicenseCrypto.SignCanonical(canonical, privateKeyXml);

            var signed = new SignedLicense
            {
                LicenseFormatVersion = LicenseFormat.Version,
                Document = doc,
                Signature = new SignatureEnvelope
                {
                    Algorithm = LicenseCrypto.Algorithm,
                    KeyId = string.IsNullOrWhiteSpace(keyId) ? LicenseCrypto.Fingerprint(PrivateKeyToPublic(privateKeyXml)) : keyId,
                    Value = signature
                }
            };

            // Write file (optional) but ALWAYS re-read + verify + validate.
            if (!string.IsNullOrWhiteSpace(outputPath))
                LicenseWriter.WriteFile(signed, outputPath);

            var selfCheckJson = !string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath)
                ? File.ReadAllText(outputPath)
                : LicenseWriter.ToJson(signed);

            var selfCheck = VerifyAndValidate(
                selfCheckJson,
                appVersion: "1.0.0",
                utcNow: DateTime.UtcNow,
                trustedPublicKeys: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [signed.Signature.KeyId] = PrivateKeyToPublic(privateKeyXml)
                },
                productId: ProductIds.ProGlassAutomation);

            if (!selfCheck.IsValid)
                return new LicenseGenerationResult
                {
                    Success = false,
                    ErrorCode = selfCheck.ErrorCode,
                    Message = "Self-verification failed: " + selfCheck.Message,
                    KeyId = signed.Signature.KeyId,
                    OutputPath = outputPath,
                    Document = doc
                };

            return new LicenseGenerationResult
            {
                Success = true,
                KeyId = signed.Signature.KeyId,
                OutputPath = outputPath,
                Document = doc,
                LicenseJson = selfCheckJson
            };
        }
        catch (Exception ex)
        {
            return new LicenseGenerationResult { Success = false, ErrorCode = LicenseErrorCode.InvalidPayload, Message = "Create failed: " + ex.Message };
        }
    }

    /// <summary>
    /// Parse + verify signature + validate. This is the authoritative read
    /// path used by the app (and by the generator for self-verification).
    /// </summary>
    public static LicenseValidationResult VerifyAndValidate(
        string licenseJson,
        string appVersion,
        DateTime utcNow,
        IReadOnlyDictionary<string, string> trustedPublicKeys,
        string? currentFingerprint = null,
        int currentDeviceCount = 1,
        string productId = ProductIds.ProGlassAutomation)
    {
        var read = LicenseReader.Read(licenseJson);
        if (read.Status == LicenseReadStatus.UnsupportedFormat)
            return new LicenseValidationResult { IsValid = false, ErrorCode = LicenseErrorCode.UnsupportedFormat, Message = read.Error };

        if (read.License is null)
            return new LicenseValidationResult { IsValid = false, ErrorCode = LicenseErrorCode.InvalidPayload, Message = read.Error ?? "Could not parse license." };

        var signed = read.License;

        // Find the trusted key by keyId (or fall back to any single key).
        string keyId = signed.Signature.KeyId ?? "";
        string? pub = trustedPublicKeys.TryGetValue(keyId, out var k) ? k : (trustedPublicKeys.Count == 1 ? trustedPublicKeys.Values.First() : null);
        if (pub is null)
            return new LicenseValidationResult { IsValid = false, ErrorCode = LicenseErrorCode.UnknownKey, Message = "No trusted key for keyId '" + keyId + "'." };

        // Reconstruct canonical bytes for the DOCUMENT and verify.
        byte[] canonical = LicenseCanonical.SerializeUtf8(signed.Document);
        bool ok = LicenseCrypto.VerifyCanonical(canonical, signed.Signature.Value, pub);
        if (!ok)
            return new LicenseValidationResult { IsValid = false, ErrorCode = LicenseErrorCode.InvalidSignature, Message = "Signature does not match the document (tampered?).", License = signed };

        // Signature valid → apply business rules.
        var v = LicenseValidator.Validate(signed, appVersion, utcNow, currentFingerprint, productId, currentDeviceCount);
        return v;
    }

    /// <summary>Overload when the caller already parsed the license (e.g. app holds it in memory).</summary>
    public static LicenseValidationResult VerifyAndValidate(
        LicenseReadResult read,
        string appVersion,
        DateTime utcNow,
        IReadOnlyDictionary<string, string> trustedPublicKeys,
        string? currentFingerprint = null,
        int currentDeviceCount = 1,
        string productId = ProductIds.ProGlassAutomation)
    {
        if (read.Status == LicenseReadStatus.UnsupportedFormat)
            return new LicenseValidationResult { IsValid = false, ErrorCode = LicenseErrorCode.UnsupportedFormat, Message = read.Error };
        if (read.License is null)
            return new LicenseValidationResult { IsValid = false, ErrorCode = LicenseErrorCode.InvalidPayload, Message = read.Error ?? "Could not parse." };
        return VerifyAndValidate(LicenseWriter.ToJson(read.License), appVersion, utcNow, trustedPublicKeys, currentFingerprint, currentDeviceCount, productId);
    }

    private static bool IsExpiringType(string t)
    {
        return string.Equals(t, "Trial", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "Subscription", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "Enterprise", StringComparison.OrdinalIgnoreCase);
    }

    private static string PrivateKeyToPublic(string privateKeyXml)
    {
        using var rsa = System.Security.Cryptography.RSA.Create();
        rsa.FromXmlString(privateKeyXml);
        return rsa.ToXmlString(includePrivateParameters: false);
    }
}

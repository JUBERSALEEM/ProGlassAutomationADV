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
                currentFingerprint: doc.DevicePolicy?.Fingerprint,
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
    /// Clock-rollback window used when the caller supplies a last-seen time
    /// and does not pass its own tolerance. Matches the offline activation
    /// default (24 hours). The license document cannot widen this window.
    /// Small skew is tolerated so a laptop clock correction does not brick
    /// a genuine key. A larger rollback is rejected.
    /// </summary>
    public static readonly TimeSpan DefaultClockRollbackTolerance = TimeSpan.FromHours(24);

    /// <summary>
    /// Parse + verify signature + validate. This is the authoritative read
    /// path used by the app (and by the generator for self-verification).
    /// Every failure returns a structured rejection. This method never throws
    /// a successful result, and it never selects a public key or algorithm
    /// from anything other than the explicit allow-list below.
    /// </summary>
    public static LicenseValidationResult VerifyAndValidate(
        string licenseJson,
        string appVersion,
        DateTime utcNow,
        IReadOnlyDictionary<string, string> trustedPublicKeys,
        string? currentFingerprint = null,
        int currentDeviceCount = 1,
        string productId = ProductIds.ProGlassAutomation,
        DateTime? lastSeenUtc = null,
        TimeSpan? clockRollbackTolerance = null)
    {
        if (trustedPublicKeys is null || trustedPublicKeys.Count == 0)
            return Fail(LicenseErrorCode.MissingKey, "No trusted public keys were supplied.");

        try
        {
            var read = LicenseReader.Read(licenseJson);
            if (read.Status == LicenseReadStatus.UnsupportedFormat)
                return Fail(LicenseErrorCode.UnsupportedFormat, read.Error ?? "Unsupported license format.");

            if (read.License?.Document is null || read.License.Signature is null)
                return Fail(LicenseErrorCode.InvalidPayload, read.Error ?? "Could not parse license.");

            var signed = read.License;
            var signature = signed.Signature;

            // The algorithm is an allow-list, not a field the license may choose.
            // A changed algorithm string is rejected even when the old signature
            // bytes would still verify under the only permitted scheme.
            if (!string.Equals(signature.Algorithm, LicenseCrypto.Algorithm, StringComparison.Ordinal))
                return Fail(LicenseErrorCode.InvalidAlgorithm, "The license signature algorithm is not allowed.");

            if (string.IsNullOrWhiteSpace(signature.Value))
                return Fail(LicenseErrorCode.InvalidPayload, "License signature is missing.");

            // Exact keyId lookup only. A single trusted key must not be used
            // as a fallback for an unknown, missing, or rewritten keyId.
            if (string.IsNullOrWhiteSpace(signature.KeyId)
                || !trustedPublicKeys.TryGetValue(signature.KeyId, out string? pub)
                || string.IsNullOrWhiteSpace(pub))
            {
                return Fail(LicenseErrorCode.UnknownKey, "No trusted public key is registered for this license keyId.");
            }

            byte[] canonical = LicenseCanonical.SerializeUtf8(signed.Document);
            if (!LicenseCrypto.VerifyCanonical(canonical, signature.Value, pub))
                return Fail(LicenseErrorCode.InvalidSignature, "Signature does not match the document (tampered?).");

            var clock = CheckClockRollback(lastSeenUtc, utcNow, clockRollbackTolerance);
            if (clock != null)
                return clock;

            return LicenseValidator.Validate(
                signed, appVersion, utcNow, currentFingerprint, productId, currentDeviceCount);
        }
        catch (Exception)
        {
            return Fail(LicenseErrorCode.InvalidPayload, "License verification failed.");
        }
    }

    /// <summary>Overload when the caller already parsed the license (e.g. app holds it in memory).</summary>
    public static LicenseValidationResult VerifyAndValidate(
        LicenseReadResult read,
        string appVersion,
        DateTime utcNow,
        IReadOnlyDictionary<string, string> trustedPublicKeys,
        string? currentFingerprint = null,
        int currentDeviceCount = 1,
        string productId = ProductIds.ProGlassAutomation,
        DateTime? lastSeenUtc = null,
        TimeSpan? clockRollbackTolerance = null)
    {
        if (read is null)
            return Fail(LicenseErrorCode.InvalidPayload, "Could not parse license.");
        if (read.Status == LicenseReadStatus.UnsupportedFormat)
            return Fail(LicenseErrorCode.UnsupportedFormat, read.Error ?? "Unsupported license format.");
        if (read.License is null)
            return Fail(LicenseErrorCode.InvalidPayload, read.Error ?? "Could not parse.");
        return VerifyAndValidate(
            LicenseWriter.ToJson(read.License),
            appVersion,
            utcNow,
            trustedPublicKeys,
            currentFingerprint,
            currentDeviceCount,
            productId,
            lastSeenUtc,
            clockRollbackTolerance);
    }

    private static LicenseValidationResult? CheckClockRollback(
        DateTime? lastSeenUtc,
        DateTime utcNow,
        TimeSpan? clockRollbackTolerance)
    {
        if (!lastSeenUtc.HasValue || lastSeenUtc.Value == default)
            return null;

        TimeSpan tolerance = clockRollbackTolerance ?? DefaultClockRollbackTolerance;
        if (tolerance < TimeSpan.Zero)
            tolerance = TimeSpan.Zero;

        DateTime seen = lastSeenUtc.Value.ToUniversalTime();
        DateTime now = utcNow.ToUniversalTime();
        if (seen - tolerance > now)
        {
            return Fail(
                LicenseErrorCode.ClockRollbackDetected,
                "System clock rollback beyond the 24 hour tolerance was detected. Correct the system time and try again.");
        }

        return null;
    }

    private static LicenseValidationResult Fail(LicenseErrorCode code, string message) =>
        new() { IsValid = false, ErrorCode = code, Message = message };

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

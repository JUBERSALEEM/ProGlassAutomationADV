// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseValidator.cs
// PROJECT: ProGlass.Licensing
// MODULE: Deterministic license validation (product / version / expiry /
//         feature / device policy)
//
// All rules live HERE, not scattered through the app. The app and generator
// both call LicenseValidator.Validate(...). English strings are never used
// for control flow — every outcome maps to a stable LicenseErrorCode.
//
// VERSION POLICY RULES (the critical compatibility contract):
//   • default minimum = "1.0.0", maximum = null  →  the license is accepted
//     by ANY app version >= minimum, including future major versions. This
//     is how a 2026 license keeps working on 2027 / 2028 / 2.0 / 3.0 builds.
//   • maximum = "X.Y.Z"  →  accepted only up to that version (inclusive).
//   • allowedMajorVersions = [1]  →  accepted only for major version 1.
// The app NEVER rejects a newer build merely because the license was made
// for an older one — that behaviour requires an explicit restriction.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Licensing;

public static class LicenseValidator
{
    /// <summary>
    /// Validate a parsed (signature already verified) license against the
    /// product, the current app version, "now" and (optionally) the machine
    /// fingerprint / a known device count.
    /// </summary>
    public static LicenseValidationResult Validate(
        SignedLicense license,
        string appVersion,
        DateTime utcNow,
        string? currentFingerprint = null,
        string productId = ProductIds.ProGlassAutomation,
        int currentDeviceCount = 1)
    {
        if (license?.Document is null)
            return Fail(LicenseErrorCode.InvalidPayload, "License has no document.");

        var doc = license.Document;

        // Format version — the reader already rejects unsupported, but double-check.
        if (!LicenseFormat.SupportedVersions.Contains(doc.LicenseFormatVersion))
            return Fail(LicenseErrorCode.UnsupportedFormat,
                "Unsupported license format version: " + doc.LicenseFormatVersion);

        // Wrong product — permanent ProductId binding.
        if (!string.Equals(doc.ProductId, productId, StringComparison.Ordinal))
            return Fail(LicenseErrorCode.WrongProduct,
                "License is for product '" + doc.ProductId + "', expected '" + productId + "'.");

        if (!IsKnownLicenseType(doc.LicenseType))
            return Fail(LicenseErrorCode.InvalidLicenseType, "Unsupported license type.");

        if (doc.IssuedAtUtc == default)
            return Fail(LicenseErrorCode.InvalidPolicy, "License has no issue time.");

        DateTime issued = doc.IssuedAtUtc.ToUniversalTime();
        if (issued.Year < 2020 || issued.Year > 2100)
            return Fail(LicenseErrorCode.InvalidPolicy, "License issue time is outside the accepted range.");

        if (doc.ExpiresAtUtc.HasValue)
        {
            DateTime expires = doc.ExpiresAtUtc.Value.ToUniversalTime();
            if (expires <= issued || expires.Year > 2100)
                return Fail(LicenseErrorCode.InvalidPolicy, "License expiry is not after the issue time or is outside the accepted range.");
        }
        else if (RequiresExpiry(doc.LicenseType))
        {
            return Fail(LicenseErrorCode.InvalidPolicy, "This license type requires an expiry.");
        }

        var bindingError = CheckDeviceBinding(doc.DevicePolicy);
        if (bindingError != null)
            return bindingError;

        // Time / expiry (UTC internally).
        if (issued > utcNow.ToUniversalTime())
            return Fail(LicenseErrorCode.NotYetValid, "License is not valid yet (issued in the future).");

        if (doc.ExpiresAtUtc.HasValue && doc.ExpiresAtUtc.Value.ToUniversalTime() < utcNow.ToUniversalTime())
            return Fail(LicenseErrorCode.Expired,
                "License expired at " + doc.ExpiresAtUtc.Value.ToUniversalTime().ToString("O") + " UTC.");

        // Version policy — the big compatibility rule.
        var vpError = CheckVersionPolicy(doc.VersionPolicy, appVersion);
        if (vpError != null) return vpError;

        // Device policy (only applied when a fingerprint is provided and the
        // license is bound).
        if (doc.DevicePolicy is { IsBound: true } dp)
        {
            if (string.IsNullOrWhiteSpace(currentFingerprint))
                return Fail(LicenseErrorCode.UnboundFingerprintRequired,
                    "License is machine-bound but no fingerprint was supplied.");

            string? bound = dp.Fingerprint;
            if (!string.Equals(bound, currentFingerprint, StringComparison.OrdinalIgnoreCase))
                return Fail(LicenseErrorCode.MachineMismatch,
                    "License bound to a different machine.");

            if (String.Equals(dp.Binding, "MultiDevice", StringComparison.OrdinalIgnoreCase)
                && dp.DeviceLimit is > 0
                && currentDeviceCount > dp.DeviceLimit.Value)
            {
                return Fail(LicenseErrorCode.DeviceLimitExceeded,
                    "License device limit reached (" + dp.DeviceLimit + ").");
            }
        }

        return new LicenseValidationResult
        {
            IsValid = true,
            License = license,
            SigningKeyId = license.Signature.KeyId
        };
    }

    /// <summary>Which features the license grants (unknown ones are tolerated).</summary>
    public static bool HasFeature(LicenseDocument doc, string featureId)
    {
        if (doc?.Features is null || string.IsNullOrWhiteSpace(featureId)) return false;
        return doc.Features.Any(f => string.Equals(f, featureId, StringComparison.Ordinal))
            || doc.Features.Any(f => string.Equals(f, "*", StringComparison.Ordinal));
    }

    private static bool IsKnownLicenseType(string? licenseType) =>
        !string.IsNullOrWhiteSpace(licenseType)
        && Enum.GetNames<LicenseType>().Contains(licenseType, StringComparer.Ordinal);

    private static bool RequiresExpiry(string licenseType) =>
        string.Equals(licenseType, nameof(LicenseType.Trial), StringComparison.Ordinal)
        || string.Equals(licenseType, nameof(LicenseType.Subscription), StringComparison.Ordinal)
        || string.Equals(licenseType, nameof(LicenseType.Enterprise), StringComparison.Ordinal);

    private static LicenseValidationResult? CheckDeviceBinding(DevicePolicy? policy)
    {
        if (policy is null)
            return Fail(LicenseErrorCode.InvalidPolicy, "License has no device policy.");

        if (string.Equals(policy.Binding, "Unbound", StringComparison.Ordinal)
            || string.Equals(policy.Binding, "SingleDevice", StringComparison.Ordinal)
            || string.Equals(policy.Binding, "MultiDevice", StringComparison.Ordinal))
        {
            return null;
        }

        return Fail(LicenseErrorCode.InvalidPolicy, "Unsupported device binding.");
    }

    private static LicenseValidationResult? CheckVersionPolicy(VersionPolicy? vp, string appVersion)
    {
        if (vp is null) return null; // no restriction => unlimited future compatibility

        // Allowed major versions — if set, the app's major must be in the list.
        if (vp.AllowedMajorVersions is { Count: > 0 })
        {
            int appMajor = ParseMajor(appVersion);
            if (appMajor >= 0 && !vp.AllowedMajorVersions.Contains(appMajor))
                return Fail(LicenseErrorCode.VersionNotAllowed,
                    "Application major version " + appMajor + " is not allowed by this license.");
        }

        if (!string.IsNullOrWhiteSpace(vp.MinimumApplicationVersion))
        {
            if (CompareVersions(appVersion, vp.MinimumApplicationVersion!) < 0)
                return Fail(LicenseErrorCode.VersionNotAllowed,
                    "Application " + appVersion + " is older than the license minimum " +
                    vp.MinimumApplicationVersion + ".");
        }

        if (!string.IsNullOrWhiteSpace(vp.MaximumApplicationVersion))
        {
            if (CompareVersions(appVersion, vp.MaximumApplicationVersion!) > 0)
                return Fail(LicenseErrorCode.VersionNotAllowed,
                    "Application " + appVersion + " is newer than the license maximum " +
                    vp.MaximumApplicationVersion + ".");
        }

        return null;
    }

    private static int ParseMajor(string version)
    {
        var v = version.Trim().Split('.');
        return int.TryParse(v[0], out int major) ? major : -1;
    }

    /// <summary>Compare dot-separated numeric versions. Returns &lt;0, 0, &gt;0.</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        int max = Math.Max(pa.Count, pb.Count);
        for (int i = 0; i < max; i++)
        {
            int x = i < pa.Count ? pa[i] : 0;
            int y = i < pb.Count ? pb[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    private static List<int> Parse(string version)
    {
        var list = new List<int>();
        foreach (var part in version.Trim().Split('.'))
        {
            // Skip non-numeric tail segments (e.g. "2026.08.26-preview" -> 2026,8,26).
            var m = new System.Text.RegularExpressions.Regex(@"^\s*(\d+)").Match(part);
            list.Add(m.Success ? int.Parse(m.Groups[1].Value) : 0);
        }
        return list;
    }

    private static LicenseValidationResult Fail(LicenseErrorCode code, string message) =>
        new() { IsValid = false, ErrorCode = code, Message = message };
}

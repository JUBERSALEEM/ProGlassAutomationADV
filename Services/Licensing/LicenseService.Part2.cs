// ═══════════════════════════════════════════════════════════════════════
// FILE: LicenseService.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using System.ComponentModel;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Licensing
{
    public static partial class LicenseService
{
        private static LicenseVerificationResult ResolveStartupFallback(
        LicenseVerificationResult signedResult, string dataRoot, string machine)
    {
        if (signedResult.CanRun)
            return signedResult;

        LicenseVerificationResult fallback;
        try
        {
            fallback = AutoTrialManager.Resolve(dataRoot, machine);
        }
        catch (Exception ex)
        {
            return DegradeToReadOnly(signedResult,
                "The automatic trial could not be started (" + ex.Message + ").");
        }

        // Trial window still open → the customer keeps working.
        if (fallback.Status == LicenseStatus.Trial)
            return fallback;

        // Trial exhausted OR ledger hard-fail (clock rollback) →
        // degrade to read-only instead of blocking the app.
        string reason = fallback.Status == LicenseStatus.Invalid
                        && !string.IsNullOrWhiteSpace(fallback.ErrorDetail)
            ? fallback.ErrorDetail
            : "The trial window has ended.";
        return DegradeToReadOnly(signedResult, reason);
    }

    private static LicenseVerificationResult DegradeToReadOnly(
        LicenseVerificationResult signedResult, string reason)
    {
        string detail = string.IsNullOrWhiteSpace(signedResult.ErrorDetail)
            ? (string.IsNullOrWhiteSpace(reason)
                ? "The license state could not be verified."
                : reason) +
              " Paste a license key from your vendor to restore full access — until then the app opens in read-only mode."
            : signedResult.ErrorDetail +
              (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason) +
              " The app opens in read-only mode so your data remains available; paste a license key from your vendor to restore full access.";

        return new LicenseVerificationResult
        {
            Status = LicenseStatus.TrialExpired,
            Payload = signedResult.Payload,
            ErrorDetail = detail
        };
    }

    /// <summary>
    /// Project a signed license-key document into the runtime payload
    /// shape shared by all three licensing tracks.
    /// </summary>
    private static LicensePayload ProjectKeyPayload(LicenseDocument doc)
    {
        DateTime expires = doc.ExpiresAtUtc?.ToUniversalTime() ?? DateTime.MaxValue;
        bool isTrial = string.Equals(doc.LicenseType, "Trial", StringComparison.OrdinalIgnoreCase);
        return new LicensePayload
        {
            SchemaVersion = 1,
            LicenseId = doc.LicenseId ?? "",
            Edition = DeriveKeyEdition(doc),
            CustomerId = doc.Customer?.Id ?? "",
            CompanyName = doc.Customer?.Company ?? "",
            IssuedAt = doc.IssuedAtUtc == default
                ? DateTime.UtcNow
                : doc.IssuedAtUtc.ToUniversalTime(),
            ExpiresAt = expires,
            MaxActivations = doc.DevicePolicy?.DeviceLimit ?? 1,
            Features = (doc.Features ?? new List<string>()).ToArray(),
            MachineFingerprint = doc.DevicePolicy is { IsBound: true }
                ? doc.DevicePolicy.Fingerprint
                : null,
            Notes = doc.Customer?.Notes ?? "",
            TrialSource = isTrial ? "vendor-license-key" : "",
            TrialExpiresAt = isTrial ? expires : default
        };
    }

    /// <summary>
    /// The license-key contract carries a license TYPE (Trial /
    /// Subscription / Lifetime / …), not an edition label. The edition
    /// shown to the customer is derived deterministically from the type
    /// and the granted feature set — matching the presets the License
    /// Generator fills in for each edition.
    /// </summary>
    private static LicenseEdition DeriveKeyEdition(LicenseDocument doc)
    {
        string type = (doc.LicenseType ?? string.Empty).Trim();
        if (string.Equals(type, "Trial", StringComparison.OrdinalIgnoreCase))
            return LicenseEdition.Trial;
        if (string.Equals(type, "Enterprise", StringComparison.OrdinalIgnoreCase))
            return LicenseEdition.Enterprise;

        var features = doc.Features ?? new List<string>();
        bool Has(string prefix) => features.Any(f =>
            !string.IsNullOrWhiteSpace(f)
            && f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (Has("Cutting.")) return LicenseEdition.Factory;
        if (Has("Reports.") || Has("AI.")) return LicenseEdition.Business;
        return LicenseEdition.Basic;
    }

    private static LicenseVerificationResult BuildRuntimeStatus(
        OfflineActivationResponseDocument doc,
        LicensePayload payload)
    {
        DateTime now = DateTime.UtcNow;
        DateTime? expires = doc.ExpiresAtUtc?.ToUniversalTime();
        if (expires.HasValue)
        {
            double remainingDays = (expires.Value - now).TotalDays;
            if (remainingDays <= LicenseVerifier.ExpiryWarningDays)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.ExpiringSoon,
                    Payload = payload,
                    ErrorDetail = "The installed activation response expires soon. Request renewal before the signed window ends."
                };
            }
        }

        return new LicenseVerificationResult
        {
            Status = string.Equals(doc.Edition, "Trial", StringComparison.OrdinalIgnoreCase)
                ? LicenseStatus.Trial
                : LicenseStatus.Valid,
            Payload = payload
        };
    }

    private static LicenseVerificationResult MapVerificationFailure(
        OfflineActivationVerificationResult verify,
        LicensePayload? payload = null)
    {
        return verify.ErrorCode switch
        {
            OfflineActivationErrorCode.MachineMismatch => new LicenseVerificationResult
            {
                Status = LicenseStatus.MachineMismatch,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response is bound to a different machine."
            },
            OfflineActivationErrorCode.InstallationMismatch or OfflineActivationErrorCode.NonceMismatch => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response does not match this machine's latest activation request."
            },
            OfflineActivationErrorCode.WrongProduct or OfflineActivationErrorCode.UnknownKey or OfflineActivationErrorCode.InvalidSignature => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response could not be trusted."
            },
            OfflineActivationErrorCode.NotYetValid => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response is not valid yet."
            },
            OfflineActivationErrorCode.Expired => new LicenseVerificationResult
            {
                Status = LicenseStatus.Expired,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response has expired."
            },
            OfflineActivationErrorCode.InactiveStatus => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response is not active for runtime use."
            },
            OfflineActivationErrorCode.OversizedRequest or OfflineActivationErrorCode.OversizedResponse => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation artifact exceeds the safe size limit."
            },
            _ => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The activation response could not be verified."
            }
        };
    }

    private static string CheckClockRollback(
        DateTime lastValidatedUtc,
        OfflineActivationPolicy? policy,
        DateTime nowUtc)
    {
        if (lastValidatedUtc == default)
            return string.Empty;

        // A signed policy may tighten the window. It must not widen it past
        // the application maximum, or the license could disable rollback detection.
        int requestedMinutes = Math.Max(0, policy?.ClockRollbackToleranceMinutes ?? 1440);
        int toleranceMinutes = Math.Min(requestedMinutes, (int)ProGlass.Licensing.LicenseService.DefaultClockRollbackTolerance.TotalMinutes);
        if (lastValidatedUtc.ToUniversalTime().AddMinutes(-toleranceMinutes) > nowUtc.ToUniversalTime())
        {
            return
                "System clock rollback beyond the signed offline tolerance was detected. " +
                "The activation response is treated as invalid until the system time is corrected or the license is re-issued.";
        }

        return string.Empty;
    }

    private static LicensePayload ProjectPayload(OfflineActivationResponseDocument doc)
    {
        LicenseEdition edition = ParseEdition(doc.Edition);
        DateTime expires = doc.ExpiresAtUtc?.ToUniversalTime() ?? DateTime.MaxValue;
        return new LicensePayload
        {
            LicenseId = doc.LicenseId ?? "",
            Edition = edition,
            CustomerId = doc.CustomerId ?? "",
            CompanyName = doc.CompanyName ?? "",
            IssuedAt = doc.IssuedAtUtc == default ? DateTime.UtcNow : doc.IssuedAtUtc.ToUniversalTime(),
            ExpiresAt = expires,
            MaxActivations = 1,
            Features = (doc.FeatureEntitlements ?? new List<string>()).ToArray(),
            MachineFingerprint = string.IsNullOrWhiteSpace(doc.MachineFingerprint) ? null : doc.MachineFingerprint,
            Notes = string.IsNullOrWhiteSpace(doc.Notes)
                ? $"ActivationId={doc.ActivationId}"
                : doc.Notes,
            TrialSource = string.Equals(doc.Edition, "Trial", StringComparison.OrdinalIgnoreCase)
                ? "vendor-offline-activation"
                : string.Empty,
            TrialExpiresAt = string.Equals(doc.Edition, "Trial", StringComparison.OrdinalIgnoreCase)
                ? expires
                : default
        };
    }

    private static LicenseEdition ParseEdition(string? edition)
    {
        return Enum.TryParse<LicenseEdition>((edition ?? string.Empty).Trim(), true, out var parsed)
            ? parsed
            : LicenseEdition.Business;
    }

    /// <summary>
    /// Returns the path of the first legacy license artifact found in
    /// the data root (license.lic or license.json), or null when none
    /// exists. The offline-activation read path uses this to hand the
    /// migration inspector the exact artifact to describe. Never throws.
    /// </summary>
    private static string? FindLegacyLicenseArtifactPath(string dataRoot)
    {
        string[] candidates =
        {
            Path.Combine(dataRoot, DefaultLicenseFileName),
            Path.Combine(dataRoot, "license.json")
        };

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                    return path;
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }
        }

        return null;
    }

    private static string NormalizeLicenseId(string? input) =>
        (input ?? string.Empty).Trim();

    private static string RequireMachineFingerprint()
    {
        string fp = CurrentMachineFingerprint;
        if (string.IsNullOrWhiteSpace(fp))
            throw new InvalidOperationException(
                "This machine fingerprint could not be computed. Activation cannot continue until the hardware identity is available.");
        return fp;
    }
    }
}

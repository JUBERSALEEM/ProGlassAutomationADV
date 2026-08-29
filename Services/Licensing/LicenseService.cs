using System.ComponentModel;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Runtime licensing state for the offline max-security redesign.
///
/// Public surface is intentionally kept stable for the rest of the app:
/// - Current
/// - Initialize / Reload
/// - InstallFile
/// - FeatureAllowed
/// - IsReadOnlyMode
///
/// Internally, authority now comes from a protected, signed offline activation
/// response stored with DPAPI protection instead of trusting plaintext legacy
/// license files or client-side trial issuance.
/// </summary>
public static class LicenseService
{
    /// <summary>
    /// External response file name convention kept for operator familiarity.
    /// The imported response is NOT stored back as plaintext under this name;
    /// it is re-protected into the offline activation store.
    /// </summary>
    public const string DefaultLicenseFileName = "license.lic";

    /// <summary>
    /// Legacy constant kept only for binary/source compatibility.
    /// The max-security offline redesign no longer uses a local activations
    /// ledger file as a trust boundary.
    /// </summary>
    public const string ActivationsFileName = "activations.json";

    /// <summary>
    /// Activation-request export extension.
    /// </summary>
    public const string ActivationRequestFileExtension = ".pgrequest";

    /// <summary>
    /// Vendor response import extension. The response is a signed JSON
    /// envelope, not a base64-wrapped blob.
    /// </summary>
    public const string ActivationResponseFileExtension = ".lic";

    /// <summary>
    /// Legacy constants preserved so older callers still compile. New secure
    /// flow does not auto-issue trials or self-sign client-side artifacts.
    /// </summary>
    public const int AutoTrialDays = 30;
    public const string AutoTrialSourceMarker = "legacy-auto-trial-removed";

    private static readonly object _lock = new();
    private static LicenseVerificationResult _current =
        new() { Status = LicenseStatus.NotActivated, ErrorDetail = "Not initialized." };

    private static string? _licensePath;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? TestDataFolderOverride { get; set; }

    public static event EventHandler<LicenseVerificationResult>? StatusChanged;

    public static LicenseVerificationResult Current
    {
        get { lock (_lock) return _current; }
    }

    public static bool IsFullyActive
    {
        get
        {
            var s = Current.Status;
            return s == LicenseStatus.Valid
                || s == LicenseStatus.ExpiringSoon
                || s == LicenseStatus.Trial;
        }
    }

    public static bool IsReadOnlyMode => Current.ReadOnly;

    public static string? ResolvedLicensePath
    {
        get { lock (_lock) return _licensePath; }
    }

    public static string CurrentInstallationId =>
        OfflineActivationStore.GetInstallationId(DataRoot);

    public static string CurrentMachineFingerprint
    {
        get
        {
            try { return MachineFingerprint.ComputeCurrent(); }
            catch { return ""; }
        }
    }

    private static string DataRoot => TestDataFolderOverride ?? AppDataRoot.CurrentFolder;

    public static LicenseVerificationResult Initialize()
    {
        lock (_lock)
        {
            string dataRoot = DataRoot;
            _licensePath = OfflineActivationStore.ActivationStatePath(dataRoot);
            _current = LoadSecureOfflineLicense(dataRoot);

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Offline license checked",
                    $"status={_current.Status}; " +
                    $"customer='{_current.Payload?.CustomerId}'; " +
                    $"company='{_current.Payload?.CompanyName}'; " +
                    $"edition={_current.Payload?.Edition}; " +
                    $"statePath={_licensePath ?? "<none>"}");
            }
            catch
            {
                // audit must never block startup
            }

            return _current;
        }
    }

    public static LicenseVerificationResult Reload()
    {
        LicenseVerificationResult result = Initialize();
        try { StatusChanged?.Invoke(null, result); }
        catch { /* subscribers must not break licensing */ }
        return result;
    }

    public static string CreateActivationRequestJson(string licenseId)
    {
        string normalized = NormalizeLicenseId(licenseId);
        if (normalized.Length == 0)
            throw new InvalidOperationException("A vendor license reference is required before generating an activation request.");

        string installationId = OfflineActivationStore.GetInstallationId(DataRoot);
        string machine = RequireMachineFingerprint();
        var pending = OfflineActivationStore.SavePendingRequest(
            DataRoot,
            normalized,
            installationId,
            machine,
            ProGlassLicense.AppVersion);

        var request = new OfflineActivationRequestDocument
        {
            ProtocolVersion = OfflineActivationProtocol.Version,
            ProductId = ProGlassLicense.ProductId,
            LicenseId = pending.LicenseId,
            InstallationId = pending.InstallationId,
            MachineFingerprint = pending.MachineFingerprint,
            RequestNonce = pending.RequestNonce,
            RequestedAtUtc = pending.RequestedAtUtc,
            ApplicationVersion = pending.ApplicationVersion
        };

        return OfflineActivationService.CreateRequestJson(request);
    }

    public static string ExportActivationRequest(string licenseId, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new InvalidOperationException("An output path is required.");

        string json = CreateActivationRequestJson(licenseId);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? DataRoot);
        File.WriteAllText(outputPath, json, new System.Text.UTF8Encoding(false));
        return outputPath;
    }

    public static LicenseVerificationResult InstallText(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "Activation response JSON is empty."
            };
        }

        string trimmed = responseJson.Trim();
        if (trimmed.Length > 512_000)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "Activation response JSON exceeds the allowed size limit."
            };
        }

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "Base64-wrapped whole-license activation is no longer accepted. " +
                    "Paste the signed activation response JSON exactly as issued by your vendor, " +
                    "or import the response file they sent you."
            };
        }

        var pending = OfflineActivationStore.ReadPendingRequest(DataRoot);
        if (pending == null)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.NotActivated,
                ErrorDetail =
                    "No pending activation request was found on this machine. " +
                    "Generate a fresh activation request first, send it to your vendor, then import the signed response."
            };
        }

        string machine;
        try { machine = RequireMachineFingerprint(); }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = ex.Message
            };
        }

        var verify = OfflineActivationService.VerifyResponse(
            trimmed,
            TrustedLicenseKeys.All,
            expectedProductId: ProGlassLicense.ProductId,
            expectedInstallationId: pending.InstallationId,
            expectedMachineFingerprint: machine,
            expectedRequestNonce: pending.RequestNonce,
            utcNow: DateTime.UtcNow);

        if (!verify.IsValid || verify.Document == null || verify.Response == null)
            return MapVerificationFailure(verify);

        OfflineActivationStore.SaveActivationState(
            DataRoot,
            pending.InstallationId,
            verify.Document.LicenseId,
            verify.Document.ActivationId,
            verify.Document.MachineFingerprint,
            verify.Document.RequestNonce,
            verify.SigningKeyId ?? verify.Response.Signature.KeyId,
            trimmed,
            DateTime.UtcNow);
        OfflineActivationStore.ClearPendingRequest(DataRoot);

        return Reload();
    }

    public static LicenseVerificationResult InstallFile(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "Source file does not exist: " + sourcePath
            };
        }

        string text;
        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Length > OfflineActivationService.MaxResponseCharacters * 4L)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = "Activation response file exceeds the safe size limit."
                };
            }

            text = File.ReadAllText(sourcePath);
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "Could not read the activation response file: " + ex.Message
            };
        }

        var result = InstallText(text);
        if (result.CanRun)
        {
            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Offline activation response installed",
                    "source=" + sourcePath + "; installationId=" + CurrentInstallationId);
            }
            catch
            {
                // audit must not block import
            }
        }

        return result;
    }

    public static bool IsDeveloperMachine => false;

    public static LicenseVerificationResult DevResetActivations() => Reload();

    public static LicenseVerificationResult InstallTrial(int days = 30)
    {
        return new LicenseVerificationResult
        {
            Status = LicenseStatus.NotActivated,
            ErrorDetail =
                "Client-side self-signed trials were removed by the offline max-security redesign. " +
                "Generate an activation request and ask your vendor for a signed offline trial or full activation response."
        };
    }

    public static bool FeatureAllowed(string feature)
    {
        var p = Current.Payload;
        if (p == null || p.Features == null || p.Features.Count == 0)
            return false;

        foreach (var granted in p.Features)
        {
            if (string.IsNullOrWhiteSpace(granted))
                continue;
            if (granted == "*")
                return true;
            if (granted.EndsWith(".*", StringComparison.Ordinal))
            {
                string prefix = granted[..^1];
                if (feature.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            else if (string.Equals(granted, feature, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static LicenseVerificationResult LoadSecureOfflineLicense(string dataRoot)
    {
        var stored = OfflineActivationStore.ReadActivationState(dataRoot);
        if (stored == null)
        {
            string? legacyArtifactPath = FindLegacyLicenseArtifactPath(dataRoot);
            if (legacyArtifactPath != null)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = LegacyLicenseMigrationInspector.BuildMigrationPrompt(legacyArtifactPath)
                };
            }

            return new LicenseVerificationResult
            {
                Status = LicenseStatus.NotActivated,
                ErrorDetail =
                    "No offline activation response is installed. Generate an activation request and import the vendor-signed response."
            };
        }

        string machine;
        try { machine = RequireMachineFingerprint(); }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = ex.Message
            };
        }

        var verify = OfflineActivationService.VerifyResponse(
            stored.SignedResponseJson,
            TrustedLicenseKeys.All,
            expectedProductId: ProGlassLicense.ProductId,
            expectedInstallationId: stored.InstallationId,
            expectedMachineFingerprint: machine,
            expectedRequestNonce: stored.RequestNonce,
            utcNow: DateTime.UtcNow);

        return MapLoadVerificationToStatus(verify, stored, machine, dataRoot);
    }

    private static LicenseVerificationResult MapLoadVerificationToStatus(
        OfflineActivationVerificationResult verify,
        OfflineActivationStore.ActivationState stored,
        string machineFingerprint,
        string dataRoot)
    {
        var doc = verify.Document;
        var payload = doc == null ? null : ProjectPayload(doc);

        if (verify.IsValid && doc != null)
        {
            var rollback = CheckClockRollback(stored.LastValidatedUtc, doc.OfflinePolicy, DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(rollback))
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    Payload = payload,
                    ErrorDetail = rollback
                };
            }

            OfflineActivationStore.UpdateLastValidatedUtc(dataRoot, DateTime.UtcNow);
            return BuildRuntimeStatus(doc, payload);
        }

        if (verify.ErrorCode == OfflineActivationErrorCode.Expired && doc != null)
        {
            var rollback = CheckClockRollback(stored.LastValidatedUtc, doc.OfflinePolicy, DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(rollback))
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    Payload = payload,
                    ErrorDetail = rollback
                };
            }

            if (doc.ExpiresAtUtc.HasValue
                && doc.OfflinePolicy?.ReadOnlyOnExpiry == true
                && doc.OfflinePolicy.GraceDays > 0
                && DateTime.UtcNow <= doc.ExpiresAtUtc.Value.ToUniversalTime().AddDays(doc.OfflinePolicy.GraceDays))
            {
                return new LicenseVerificationResult
                {
                    Status = string.Equals(doc.Edition, "Trial", StringComparison.OrdinalIgnoreCase)
                        ? LicenseStatus.TrialExpired
                        : LicenseStatus.GracePeriod,
                    Payload = payload,
                    ErrorDetail =
                        string.Equals(doc.Edition, "Trial", StringComparison.OrdinalIgnoreCase)
                            ? "The offline trial has expired. The app remains in read-only mode until you import a renewed signed activation response."
                            : "The license is past expiry but within its signed offline grace window. The app is running in read-only mode until a renewed response is installed."
                };
            }

            return new LicenseVerificationResult
            {
                Status = string.Equals(doc.Edition, "Trial", StringComparison.OrdinalIgnoreCase)
                    ? LicenseStatus.TrialExpired
                    : LicenseStatus.Expired,
                Payload = payload,
                ErrorDetail = verify.Message ?? "The installed activation response has expired."
            };
        }

        return MapVerificationFailure(verify, payload);
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

        int toleranceMinutes = Math.Max(0, policy?.ClockRollbackToleranceMinutes ?? 1440);
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

    private static bool LegacyLicenseArtifactExists(string dataRoot)
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
                    return true;
            }
            catch
            {
                // best effort
            }
        }

        return false;
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

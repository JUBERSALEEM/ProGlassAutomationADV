// ═══════════════════════════════════════════════════════════════════════
// FILE: LicenseService.Part1.cs (Phase 3.8 split — member census verified)
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
    /// Length of the automatic evaluation trial, in days. Shared by the
    /// UI copy and <see cref="AutoTrialManager"/>.
    /// </summary>
    public const int AutoTrialDays = 30;

    /// <summary>
    /// Provenance marker written into the projected payload's
    /// TrialSource when the ACTIVE trial is the automatic evaluation
    /// trial (as opposed to a vendor-issued trial activation or trial
    /// license key). UI surfaces branch on this marker.
    /// </summary>
    public const string AutoTrialSourceMarker = "auto-trial";

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

    /// <summary>
    /// True when the active license source is a signed license key
    /// (the simple-key flow) rather than an activation response or the
    /// automatic trial. UI surfaces use this to label the license source.
    /// </summary>
    public static bool IsLicenseKeyActive =>
        OfflineActivationStore.ReadLicenseKeyState(DataRoot) != null;

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
            _licensePath =
                OfflineActivationStore.ReadLicenseKeyState(dataRoot) != null
                    ? OfflineActivationStore.LicenseKeyStatePath(dataRoot)
                    : OfflineActivationStore.ActivationStatePath(dataRoot);
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
                ProGlassAutomation.Services.Logging.AppLogService.Debug("lock suppressed");
            }

            return _current;
        }
    }

    public static LicenseVerificationResult Reload()
    {
        LicenseVerificationResult result = Initialize();
        try { StatusChanged?.Invoke(null, result); }
        catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("Reload suppressed");
            }
        return result;
    }

    public static string CreateActivationRequestJson(string licenseId)
    {
        // 2026 workflow simplification: a vendor license reference is NO
        // LONGER required. The request binds the installation identity,
        // machine fingerprint and a fresh nonce; an empty reference is
        // carried through as an empty (informational) licenseId field.
        string normalized = NormalizeLicenseId(licenseId);

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

        var pendingRead = OfflineActivationStore.ReadPendingRequestDetailed(DataRoot);
        if (pendingRead.Exists && !pendingRead.Success)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "The protected pending activation request state could not be read on this machine and cannot be trusted. " +
                    "Generate a fresh activation request and import the signed response again."
            };
        }
        var pending = pendingRead.Value;
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
        // One active license at a time: the new activation response
        // replaces any previously installed license key.
        OfflineActivationStore.ClearLicenseKeyState(DataRoot);

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
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
        }

        return result;
    }

    // ── Simple license key flow (2026 workflow simplification) ────────

    /// <summary>
    /// Install a vendor-signed license key from its single-line PGKEY1
    /// text block (or raw .proglasslicense JSON). The key is signature-
    /// verified against the embedded trusted key ring before anything is
    /// stored; invalid, expired, tampered or foreign keys are rejected
    /// outright and change nothing.
    /// </summary>
    public static LicenseVerificationResult InstallKeyText(string keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "License key text is empty."
            };
        }

        if (keyText.Length > 700_000)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "License key exceeds the allowed size limit."
            };
        }

        // App boundary: armored generator keys ONLY. Raw license JSON is
        // rejected even though the decoder below could read it — the
        // customer-facing contract is "paste the key the generator
        // produced" (2026-08-29 security cross-check).
        if (!LicenseKeyArmor.IsArmoredBlock(keyText))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "Only license keys produced by the License Key Generator are accepted. " +
                    "Paste the single-line block starting with \"" + LicenseKeyArmor.Prefix +
                    "\" exactly as generated (raw license JSON is not accepted)."
            };
        }

        string? licenseJson = LicenseKeyArmor.TryExtractJson(keyText);
        if (string.IsNullOrWhiteSpace(licenseJson))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "The pasted text is not a license key. Paste the single-line key block " +
                    "(starts with \"" + LicenseKeyArmor.Prefix + "\") exactly as your vendor sent it, " +
                    "or import the key file instead."
            };
        }

        if (licenseJson.Length > 512_000)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "License key exceeds the allowed size limit."
            };
        }

        LicenseValidationResult validation;
        try
        {
            validation = ProGlassLicense.VerifyText(licenseJson);
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "Could not verify the license key: " + ex.Message
            };
        }

        if (!validation.IsValid || validation.Document == null)
            return MapKeyValidationFailure(validation);

        var doc = validation.Document;
        OfflineActivationStore.SaveLicenseKeyState(
            DataRoot,
            doc.LicenseId ?? "",
            validation.SigningKeyId ?? "",
            licenseJson,
            DateTime.UtcNow);
        OfflineActivationStore.ClearActivationState(DataRoot);

        try
        {
            AuditService.Log(
                AuditSections.Settings,
                "License key installed",
                "licenseId=" + (doc.LicenseId ?? "") +
                "; keyId=" + (validation.SigningKeyId ?? "?") +
                "; licenseType=" + (doc.LicenseType ?? "?"));
        }
        catch
        {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("catch suppressed");
            }

        return Reload();
    }

    /// <summary>
    /// Install a license key from a file. Only armored PGKEY1 blocks are
    /// accepted (the same boundary as <see cref="InstallKeyText"/>); the
    /// file content goes through the identical verify-before-store path.
    /// </summary>
    public static LicenseVerificationResult InstallKeyFile(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "License key file does not exist: " + sourcePath
            };
        }

        string text;
        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Length > 700_000L)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = "License key file exceeds the safe size limit."
                };
            }

            text = File.ReadAllText(sourcePath);
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "Could not read the license key file: " + ex.Message
            };
        }

        LicenseVerificationResult result = InstallKeyText(text);
        if (result.CanRun)
        {
            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "License key file installed",
                    "source=" + sourcePath);
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
        }

        return result;
    }

    public static bool IsDeveloperMachine => false;

    public static LicenseVerificationResult DevResetActivations() => Reload();

    public static LicenseVerificationResult InstallTrial(int days = 30)
    {
        // 2026 workflow simplification: the automatic evaluation trial is
        // no longer a manually triggered action. This legacy entry point
        // resolves the machine's trial window (issuing it on first call)
        // and reloads, so any old callers get the current trial state.
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

        LicenseVerificationResult resolved = AutoTrialManager.Resolve(DataRoot, machine);
        if (resolved.Status != LicenseStatus.Trial)
            return resolved;

        return Reload();
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
        string machine;
        try { machine = RequireMachineFingerprint(); }
        catch (Exception ex)
        {
            return ResolveStartupFallback(
                new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = ex.Message
                },
                dataRoot,
                string.Empty);
        }

        // 1) Simple signed license key — the primary 2026 flow.
        var keyStateRead = OfflineActivationStore.ReadLicenseKeyStateDetailed(dataRoot);
        if (keyStateRead.Exists && !keyStateRead.Success)
        {
            // Corruption is tampering evidence: the key is discarded
            // from the running state and the app NEVER hard-blocks —
            // it falls back to the machine's automatic trial, or to
            // read-only mode when the trial window has passed. The
            // trial itself is governed by the tamper-evident ledger,
            // so corrupting this store cannot buy trial time.
            return ResolveStartupFallback(
                new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail =
                        "The protected installed license key state could not be read on this machine and was not used."
                },
                dataRoot,
                machine);
        }
        if (keyStateRead.Value != null)
        {
            return ResolveStartupFallback(
                VerifyInstalledKey(keyStateRead.Value, dataRoot),
                dataRoot,
                machine);
        }

        // 2) Signed offline activation response — the advanced PC-ID flow.
        var storedRead = OfflineActivationStore.ReadActivationStateDetailed(dataRoot);
        if (storedRead.Exists && !storedRead.Success)
        {
            return ResolveStartupFallback(
                new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail =
                        "The protected installed activation state could not be read on this machine and was not used."
                },
                dataRoot,
                machine);
        }
        var stored = storedRead.Value;
        if (stored == null)
        {
            string? legacyArtifactPath = FindLegacyLicenseArtifactPath(dataRoot);
            if (legacyArtifactPath != null)
            {
                return ResolveStartupFallback(
                    new LicenseVerificationResult
                    {
                        Status = LicenseStatus.Invalid,
                        ErrorDetail = LegacyLicenseMigrationInspector.BuildMigrationPrompt(legacyArtifactPath)
                    },
                    dataRoot,
                    machine);
            }

            // 3) Fresh install / nothing installed → automatic trial.
            //    Even a ledger hard-fail must not block the app: it
            //    degrades to read-only instead.
            LicenseVerificationResult trial = AutoTrialManager.Resolve(dataRoot, machine);
            return trial.Status == LicenseStatus.Invalid
                ? DegradeToReadOnly(trial, string.Empty)
                : trial;
        }

        var verify = OfflineActivationService.VerifyResponse(
            stored.SignedResponseJson,
            TrustedLicenseKeys.All,
            expectedProductId: ProGlassLicense.ProductId,
            expectedInstallationId: stored.InstallationId,
            expectedMachineFingerprint: machine,
            expectedRequestNonce: stored.RequestNonce,
            utcNow: DateTime.UtcNow);

        return ResolveStartupFallback(
            MapLoadVerificationToStatus(verify, stored, machine, dataRoot),
            dataRoot,
            machine);
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

    // ── Simple license key: load-time verification + projection ───────

    /// <summary>
    /// Verify the DPAPI-stored license key against the trusted key ring
    /// and project it into the runtime status model. Called on every
    /// Initialize so expiry / machine-binding are re-checked, never
    /// trusted from the install moment.
    /// </summary>
    private static LicenseVerificationResult VerifyInstalledKey(
        OfflineActivationStore.LicenseKeyState keyState, string dataRoot)
    {
        LicenseValidationResult validation;
        try
        {
            validation = ProGlassLicense.VerifyText(keyState.LicenseJson);
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = "The installed license key could not be verified: " + ex.Message
            };
        }

        if (!validation.IsValid || validation.Document == null)
            return MapKeyValidationFailure(validation);

        var doc = validation.Document;
        OfflineActivationStore.UpdateLicenseKeyLastValidatedUtc(dataRoot, DateTime.UtcNow);
        return BuildKeyRuntimeStatus(doc, ProjectKeyPayload(doc));
    }

    private static LicenseVerificationResult BuildKeyRuntimeStatus(
        LicenseDocument doc, LicensePayload payload)
    {
        DateTime now = DateTime.UtcNow;
        DateTime? expires = doc.ExpiresAtUtc?.ToUniversalTime();
        if (expires.HasValue && (expires.Value - now).TotalDays <= LicenseVerifier.ExpiryWarningDays)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.ExpiringSoon,
                Payload = payload,
                ErrorDetail =
                    "The installed license key expires soon. Ask your vendor for a renewed key before it expires."
            };
        }

        return new LicenseVerificationResult
        {
            Status = string.Equals(doc.LicenseType, "Trial", StringComparison.OrdinalIgnoreCase)
                ? LicenseStatus.Trial
                : LicenseStatus.Valid,
            Payload = payload
        };
    }

    private static LicenseVerificationResult MapKeyValidationFailure(LicenseValidationResult validation)
    {
        return validation.ErrorCode switch
        {
            LicenseErrorCode.Expired => new LicenseVerificationResult
            {
                Status = LicenseStatus.Expired,
                ErrorDetail = validation.Message ?? "The license key has expired."
            },
            LicenseErrorCode.MachineMismatch => new LicenseVerificationResult
            {
                Status = LicenseStatus.MachineMismatch,
                ErrorDetail = validation.Message ?? "The license key is bound to a different machine."
            },
            LicenseErrorCode.NotYetValid => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = validation.Message ?? "The license key is not valid yet."
            },
            _ => new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail = validation.Message ?? "The license key could not be verified."
            }
        };
    }

    /// <summary>
    /// Startup normalization: the app NEVER hard-blocks on a license
    /// state (product decision 2026-08-29). A runnable signed state
    /// wins; otherwise the machine's automatic trial takes over while
    /// its window is open; when the window has passed — or the ledger
    /// hard-fails on clock rollback — the app still opens, but in
    /// READ-ONLY mode with all data preserved. A pasted license key
    /// restores full access at any time.
    ///
    /// Security note: tamper evidence (corrupt store, ledger rollback)
    /// can therefore never GRANT anything — it only degrades access to
    /// read-only. The trial itself remains ledger-governed.
    /// </summary>
    }
}

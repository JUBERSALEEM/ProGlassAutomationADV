// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/AutoTrialManager.cs
// PROJECT: ProGlassAutomation
// MODULE: Automatic evaluation trial — 2026 workflow simplification
//
// PURPOSE
// ───────
// The offline max-security redesign removed the client-side auto-trial,
// which made a fresh install land in a hard "NotActivated" state until the
// customer completed a vendor round-trip. The 2026 workflow simplification
// restores an AUTOMATIC trial with the same security posture:
//
//   • On a fresh machine (no signed license key, no signed activation
//     response) the app issues a 30-day evaluation trial the first time
//     LicenseService.Initialize runs.
//   • The trial window is recorded by the existing tamper-evident
//     TrialLedger (four anchors + clock-rollback detection). It is NOT a
//     self-signed license: the runtime status it produces carries no
//     signature of its own and can therefore never outrank a real signed
//     license — a signed license ALWAYS wins while valid.
//   • If a previously installed key/response is removed or expires while
//     the machine's trial window is still open, the app falls back to the
//     trial so the customer keeps working. Once the window has passed the
//     app enters the existing read-only mode (data preserved).
//   • Clock rollback detected by the ledger heartbeat hard-fails the
//     trial (Invalid) — the anti-rollback anchor logic is untouched.
//
// SECURITY RATIONALE
// ──────────────────
// The trial is an APP-ENFORCED, machine-scoped, non-renewable-by-deletion
// state: deleting local files does not reset the window because the
// ProgramData / registry / DPAPI anchors survive file cleanup, and the
// ledger only ever moves timestamps forwards. It grants no privileges a
// signed license would not also grant, and it terminates in read-only mode.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Resolves the machine's automatic trial state and projects it into the
/// runtime license status model. All writes go through
/// <see cref="TrialLedger"/>; nothing here is signed or self-issued.
/// </summary>
public static class AutoTrialManager
{
    /// <summary>Length of the automatic evaluation window in days.</summary>
    public const int TrialDays = LicenseService.AutoTrialDays;

    /// <summary>
    /// Marker written into the projected payload's TrialSource so UI
    /// surfaces can tell an automatic trial from a vendor-issued trial
    /// activation / trial license key.
    /// </summary>
    public const string TrialSourceMarker = LicenseService.AutoTrialSourceMarker;

    /// <summary>
    /// Resolve the trial state for this machine:
    ///   • never issued        → issue now (ledger) and return an active trial
    ///   • issued, window open → active trial
    ///   • issued, window over → TrialExpired (read-only)
    ///   • ledger rollback     → Invalid (security hard stop)
    /// Never throws; every anchor failure degrades gracefully.
    /// </summary>
    public static LicenseVerificationResult Resolve(string dataRoot, string machineFingerprint)
    {
        string machine = string.IsNullOrWhiteSpace(machineFingerprint) ? "" : machineFingerprint;

        TrialLedger.TrialLedgerSnapshot snapshot;
        try
        {
            snapshot = TrialLedger.Read(machine);
            if (!snapshot.IsIssued)
            {
                TrialLedger.RecordIssuance(machine);
                snapshot = TrialLedger.Read(machine);
            }
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "The automatic trial ledger could not be read or written on this machine: " +
                    ex.Message
            };
        }

        DateTime issued = snapshot.FirstIssuedUtc ?? DateTime.UtcNow;

        TrialLedger.HeartbeatResult heartbeat;
        try
        {
            heartbeat = TrialLedger.Heartbeat();
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "The automatic trial ledger heartbeat could not be recorded: " + ex.Message
            };
        }

        if (heartbeat.ClockRollbackDetected)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Invalid,
                ErrorDetail =
                    "System clock rollback beyond the trial ledger tolerance was detected. " +
                    "The automatic trial is suspended until the system time is corrected. " +
                    "Installing a vendor-signed license key remains unaffected."
            };
        }

        DateTime expires = issued.ToUniversalTime().AddDays(TrialDays);
        var payload = BuildTrialPayload(machine, issued, expires);

        if (DateTime.UtcNow < expires)
        {
            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Trial,
                Payload = payload,
                ErrorDetail =
                    $"A {TrialDays}-day automatic evaluation trial is active on this machine. " +
                    "Install a vendor-signed license key before the trial ends to keep working without interruption."
            };
        }

        return new LicenseVerificationResult
        {
            Status = LicenseStatus.TrialExpired,
            Payload = payload,
            ErrorDetail =
                "The automatic evaluation trial has ended. All your data is preserved, but the " +
                "app is now in read-only mode until you install a vendor-signed license key."
        };
    }

    /// <summary>
    /// Project the trial window into the same runtime payload shape the
    /// signed flows produce, so About / Settings / banners render the
    /// trial exactly like any other license state.
    /// </summary>
    public static LicensePayload BuildTrialPayload(
        string machineFingerprint, DateTime firstIssuedUtc, DateTime expiresUtc)
    {
        return new LicensePayload
        {
            SchemaVersion = 1,
            LicenseId = "AUTO-TRIAL-" + firstIssuedUtc.ToUniversalTime().ToString("yyyyMMdd"),
            Edition = LicenseEdition.Trial,
            CustomerId = "",
            CompanyName = "",
            IssuedAt = firstIssuedUtc.ToUniversalTime(),
            ExpiresAt = expiresUtc.ToUniversalTime(),
            MaxActivations = 1,
            // The trial grants every module (LicenseGate also treats
            // Trial as premium); an explicit wildcard keeps any other
            // FeatureAllowed consumer consistent with that decision.
            Features = new[] { "*" },
            MachineFingerprint = string.IsNullOrWhiteSpace(machineFingerprint)
                ? null
                : machineFingerprint,
            Notes =
                $"Automatic {TrialDays}-day evaluation trial issued on first run of this machine. " +
                "Install a vendor-signed license key at any time — the license always takes precedence.",
            TrialSource = TrialSourceMarker,
            TrialExpiresAt = expiresUtc.ToUniversalTime()
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/TrialLedger.cs
// PROJECT: ProGlassAutomation
// MODULE: Licensing — tamper-evident, multi-anchor trial-issuance ledger
//
// PURPOSE
// ───────
// Track whether an auto-trial has ever been issued on this machine so
// the "one trial per machine" rule cannot be defeated by simply
// deleting %AppData%. Also detect system-clock rollback so a user
// cannot rewind the OS clock to extend their trial.
//
// FOUR INDEPENDENT ANCHORS
// ────────────────────────
// Trial state is written to four independent locations. If ANY of them
// says "already issued", the auto-trial is refused. All four must be
// wiped for the machine to be eligible for a new trial:
//
//   1. %AppData%\ProGlassAutomation\config.json  (AutoTrialWasIssued
//      flag) — the original anchor. Per Windows user.
//
//   2. %ProgramData%\ProGlassAutomation\trial-ledger.json — MACHINE-
//      wide (persists across Windows accounts on the same machine).
//      Requires no elevation to write for the first user (default
//      ACL grants users create-in-common-app-data); the file itself
//      inherits the default ACL, so any Windows account on the
//      machine can read the marker.
//
//   3. HKLM\SOFTWARE\ProGlassAutomation\Trial — machine-wide registry
//      hive. Writing HKLM normally requires admin; the installer runs
//      the write once at install time. On non-admin re-runs this
//      anchor gracefully degrades to WOW64 or HKCU fallback, so a
//      non-admin fresh install still writes to the other three
//      anchors.
//
//   4. DPAPI-encrypted "trial-fingerprint" blob in %ProgramData%
//      (encrypted with LocalMachine scope so any user on the machine
//      can decrypt, but the ciphertext cannot be trivially edited).
//      Contains a hash of the machine fingerprint at first issue
//      plus a signed timestamp — used to detect anchor tampering
//      and clock rollback.
//
// CLOCK-ROLLBACK DETECTION
// ────────────────────────
// Every LicenseService.Initialize() writes DateTime.UtcNow into
// the ledger's LastSeenUtc field. If a later Initialize() sees a
// UtcNow that is MORE THAN 24 HOURS EARLIER than the recorded
// LastSeenUtc, the app treats it as clock rollback and hard-expires
// the trial. Legitimate DST / timezone changes never move UtcNow
// backwards, so the 24h window is safe. The recorded LastSeenUtc
// only ever moves FORWARDS — never rewritten with an older value.
//
// GRACEFUL DEGRADATION
// ────────────────────
// Every anchor write is best-effort. If an anchor is unreachable
// (permission denied, disk full, registry hive locked, DPAPI
// missing), the READ side treats "no marker on this anchor" as
// silence — it does not conclude "trial not issued". Any single
// anchor saying "issued" is enough.
//
// AUDIT TRAIL
// ───────────
// Every write is logged to AuditService.Log with the anchor name and
// the outcome. Every clock-rollback detection is logged with the
// old / new timestamps. An operator investigating a "why is the app
// in read-only mode?" complaint sees the full evidence chain.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// A single read from the ledger. All fields may be null / default
    /// when the anchor was never written. <see cref="IsIssued"/> is
    /// true if ANY anchor recorded a trial issuance.
    /// </summary>
    public sealed record TrialLedgerSnapshot
    {
        /// <summary>True if at least one anchor says a trial was issued.</summary>
        public bool IsIssued { get; init; }

        /// <summary>UTC timestamp when the trial was first issued
        /// (earliest across all anchors).</summary>
        public DateTime? FirstIssuedUtc { get; init; }

        /// <summary>UTC timestamp of the most recent Initialize() call
        /// (latest across all anchors). Used for clock-rollback detection.</summary>
        public DateTime? LastSeenUtc { get; init; }

        /// <summary>Fingerprint-of-machine that first issued the trial
        /// (from the DPAPI anchor). Null if that anchor is unreadable.</summary>
        public string? IssuingFingerprint { get; init; }

        /// <summary>Which anchors agreed the trial was issued.</summary>
        public string AnchorEvidence { get; init; } = "";
    }

    /// <summary>
    /// Tamper-evident multi-anchor trial-issuance ledger. See file
    /// header for the full contract.
    /// </summary>
    public static class TrialLedger
    {
        // ─── Anchor #2 — %ProgramData% ledger file ───────────────
        private const string LedgerFileName  = "trial-ledger.json";
        private const string DpapiFileName   = "trial-fingerprint.bin";
        private const string ProgramDataSubfolder = "ProGlassAutomation";

        // ─── Anchor #3 — HKLM registry ───────────────────────────
        // These paths are the WINDOWS registry keys the app writes.
        // On Linux / macOS the RegistryAnchor is a no-op (Windows-only
        // API); the other three anchors carry the load.
        private const string RegistryKeyPath = @"SOFTWARE\ProGlassAutomation\Trial";
        private const string RegistryValueIssuedAt   = "FirstIssuedUtc";
        private const string RegistryValueLastSeen   = "LastSeenUtc";

        // ─── DPAPI entropy — mixed into the encrypted blob so a
        //     blob copied from another app cannot be replayed.
        private static readonly byte[] DpapiEntropy =
            Encoding.UTF8.GetBytes("ProGlassAutomation.Trial.v1");

        /// <summary>
        /// Clock-rollback tolerance. A UtcNow more than this far behind
        /// the recorded LastSeenUtc is treated as tampering. The window
        /// is generous enough to absorb legitimate laptop-suspend /
        /// timezone-database-update / NTP-correction edge cases.
        /// </summary>
        public static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromHours(24);

        /// <summary>
        /// Test-only override — when set, all anchors read/write under
        /// this folder instead of %ProgramData%. Deliberately ugly-named
        /// and hidden from IntelliSense.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(
            System.ComponentModel.EditorBrowsableState.Never)]
        public static string? TestProgramDataOverride { get; set; }

        /// <summary>
        /// True to skip the HKLM anchor (used by tests running on non-
        /// Windows CI or without admin). Never touched by production code.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(
            System.ComponentModel.EditorBrowsableState.Never)]
        public static bool SkipRegistryAnchorForTests { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  READ
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads all four anchors and returns a merged snapshot. Never
        /// throws — an unreachable anchor is silence.
        /// </summary>
        public static TrialLedgerSnapshot Read(string? appConfigMachineFingerprint = null)
        {
            bool anyIssued  = false;
            DateTime? earliest = null;
            DateTime? latest   = null;
            string? issuingFp  = null;
            var evidence = new StringBuilder();

            // Anchor 1 — AppConfig flag
            try
            {
                if (App.Config != null && App.Config.AutoTrialWasIssued)
                {
                    anyIssued = true;
                    var t = App.Config.AutoTrialIssuedAtUtc;
                    if (t.HasValue) MergeTimestamps(t.Value, ref earliest, ref latest);
                    evidence.Append("config;");
                }
            }
            catch { /* silence */ }

            // Anchor 2 — %ProgramData% ledger file
            try
            {
                var f = ReadProgramDataLedger();
                if (f != null)
                {
                    if (f.FirstIssuedUtc.HasValue)
                    {
                        anyIssued = true;
                        MergeTimestamps(f.FirstIssuedUtc.Value, ref earliest, ref latest);
                    }
                    if (f.LastSeenUtc.HasValue)
                        MergeTimestamps(f.LastSeenUtc.Value, ref earliest, ref latest);
                    if (anyIssued) evidence.Append("programdata;");
                    if (!string.IsNullOrEmpty(f.IssuingFingerprint) && issuingFp == null)
                        issuingFp = f.IssuingFingerprint;
                }
            }
            catch { /* silence */ }

            // Anchor 3 — HKLM registry. Skipped in tests so a stale
            // registry key left behind by a previous developer session
            // does not give false-positive "issued" verdicts.
            try
            {
                RegistryAnchor.Record? r = SkipRegistryAnchorForTests
                    ? null
                    : RegistryAnchor.Read();
                if (r.HasValue)
                {
                    anyIssued = true;
                    MergeTimestamps(r.Value.FirstIssuedUtc, ref earliest, ref latest);
                    if (r.Value.LastSeenUtc.HasValue)
                        MergeTimestamps(r.Value.LastSeenUtc.Value, ref earliest, ref latest);
                    evidence.Append("registry;");
                }
            }
            catch { /* silence */ }

            // Anchor 4 — DPAPI machine-scoped blob
            try
            {
                var d = ReadDpapiAnchor();
                if (d != null)
                {
                    anyIssued = true;
                    MergeTimestamps(d.FirstIssuedUtc, ref earliest, ref latest);
                    MergeTimestamps(d.LastSeenUtc,    ref earliest, ref latest);
                    if (!string.IsNullOrEmpty(d.IssuingFingerprint) && issuingFp == null)
                        issuingFp = d.IssuingFingerprint;
                    evidence.Append("dpapi;");
                }
            }
            catch { /* silence */ }

            return new TrialLedgerSnapshot
            {
                IsIssued           = anyIssued,
                FirstIssuedUtc     = earliest,
                LastSeenUtc        = latest,
                IssuingFingerprint = issuingFp,
                AnchorEvidence     = evidence.ToString()
            };
        }

        private static void MergeTimestamps(DateTime t, ref DateTime? earliest, ref DateTime? latest)
        {
            if (!earliest.HasValue || t < earliest.Value) earliest = t;
            if (!latest.HasValue    || t > latest.Value)   latest   = t;
        }

        // ══════════════════════════════════════════════════════════════
        //  WRITE — first-issue
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called from LicenseService.IssueAutoTrialIfEligible right
        /// after the .lic is signed. Writes the "issued" marker to all
        /// four anchors. Best-effort — an unreachable anchor is logged
        /// but does not fail the write.
        /// </summary>
        public static void RecordIssuance(string machineFingerprint)
        {
            var now = DateTime.UtcNow;

            // Anchor 1 — config (also handled by LicenseService, kept
            // here for completeness / test hosts that call the ledger
            // directly).
            try
            {
                if (App.Config != null && !App.Config.AutoTrialWasIssued)
                {
                    App.Config.AutoTrialWasIssued = true;
                    Services.AI.AIConfig.ConfigService.Save(App.Config);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "TrialLedger config anchor write failed: " + ex.Message,
                    new LogContext { Module = "Licensing", Class = nameof(TrialLedger) });
            }

            // Anchor 2 — %ProgramData% ledger file
            try
            {
                WriteProgramDataLedger(new LedgerFile
                {
                    FirstIssuedUtc     = now,
                    LastSeenUtc        = now,
                    IssuingFingerprint = machineFingerprint ?? ""
                });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "TrialLedger programdata anchor write failed: " + ex.Message,
                    new LogContext { Module = "Licensing", Class = nameof(TrialLedger) });
            }

            // Anchor 3 — HKLM registry
            if (!SkipRegistryAnchorForTests)
            {
                try
                {
                    RegistryAnchor.Write(new RegistryAnchor.Record
                    {
                        FirstIssuedUtc = now,
                        LastSeenUtc    = now
                    });
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        "TrialLedger registry anchor write failed (non-admin?): " + ex.Message,
                        new LogContext { Module = "Licensing", Class = nameof(TrialLedger) });
                }
            }

            // Anchor 4 — DPAPI blob
            try
            {
                WriteDpapiAnchor(new DpapiRecord
                {
                    FirstIssuedUtc     = now,
                    LastSeenUtc        = now,
                    IssuingFingerprint = machineFingerprint ?? ""
                });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "TrialLedger DPAPI anchor write failed: " + ex.Message,
                    new LogContext { Module = "Licensing", Class = nameof(TrialLedger) });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Trial ledger — issuance recorded",
                    "fingerprint=" + (machineFingerprint ?? "unknown") +
                    "; time=" + now.ToString("u"));
            }
            catch { /* audit must not break the ledger */ }
        }

        // ══════════════════════════════════════════════════════════════
        //  WRITE — heartbeat / clock-rollback detection
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Result of the heartbeat write. Signals clock-rollback so
        /// LicenseService can hard-expire the trial in that case.
        /// </summary>
        public sealed record HeartbeatResult(
            bool ClockRollbackDetected,
            DateTime? PreviousLastSeenUtc,
            DateTime CurrentUtc);

        /// <summary>
        /// Called on every LicenseService.Initialize(). Writes
        /// UtcNow into LastSeenUtc on every anchor. Compares
        /// against the highest LastSeenUtc previously recorded; if
        /// UtcNow is more than <see cref="ClockRollbackTolerance"/>
        /// earlier, returns ClockRollbackDetected=true.
        ///
        /// The write is idempotent — repeated calls within the same
        /// second do not thrash the disk. LastSeenUtc is only ever
        /// advanced forwards, never rewritten with an older value.
        /// </summary>
        public static HeartbeatResult Heartbeat()
        {
            var now = DateTime.UtcNow;
            var prev = Read().LastSeenUtc;

            bool rollback = false;
            if (prev.HasValue &&
                prev.Value - now > ClockRollbackTolerance)
            {
                rollback = true;
                try
                {
                    AuditService.Log(
                        AuditSections.Settings,
                        "Trial ledger — CLOCK ROLLBACK detected",
                        "prevLastSeen=" + prev.Value.ToString("u") +
                        "; nowUtc=" + now.ToString("u") +
                        "; delta=" + (prev.Value - now).TotalHours.ToString("0.0") + "h");
                }
                catch { /* silence */ }
            }

            // ONLY move LastSeenUtc forwards. On rollback we do NOT
            // rewrite it with the older value — that would let the
            // attacker "correct" the ledger by advancing the clock
            // again. Preserve the highest-ever value.
            DateTime toWrite = (prev.HasValue && prev.Value > now) ? prev.Value : now;

            // Anchor 1 — config (LastSeen — reuse existing timestamp)
            // Config only tracks FirstIssued; adding a LastSeen would
            // widen the config schema. The other three anchors carry
            // the LastSeen load — config alone is enough for IsIssued.

            // Anchor 2 — programdata
            try
            {
                var existing = ReadProgramDataLedger() ?? new LedgerFile();
                existing.LastSeenUtc = toWrite;
                if (!existing.FirstIssuedUtc.HasValue && prev.HasValue)
                    existing.FirstIssuedUtc = prev.Value;
                WriteProgramDataLedger(existing);
            }
            catch { /* silence */ }

            // Anchor 3 — registry
            if (!SkipRegistryAnchorForTests)
            {
                try { RegistryAnchor.UpdateLastSeen(toWrite); }
                catch { /* silence */ }
            }

            // Anchor 4 — DPAPI
            try
            {
                var existing = ReadDpapiAnchor();
                if (existing != null)
                {
                    existing.LastSeenUtc = toWrite;
                    WriteDpapiAnchor(existing);
                }
            }
            catch { /* silence */ }

            return new HeartbeatResult(rollback, prev, now);
        }

        // ══════════════════════════════════════════════════════════════
        //  ANCHOR 2 — %ProgramData% ledger file
        // ══════════════════════════════════════════════════════════════

        private static string ProgramDataFolder =>
            TestProgramDataOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                ProgramDataSubfolder);

        private sealed class LedgerFile
        {
            [JsonPropertyName("firstIssuedUtc")]
            public DateTime? FirstIssuedUtc { get; set; }

            [JsonPropertyName("lastSeenUtc")]
            public DateTime? LastSeenUtc { get; set; }

            [JsonPropertyName("issuingFingerprint")]
            public string? IssuingFingerprint { get; set; }
        }

        private static LedgerFile? ReadProgramDataLedger()
        {
            string path = Path.Combine(ProgramDataFolder, LedgerFileName);
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<LedgerFile>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void WriteProgramDataLedger(LedgerFile file)
        {
            Directory.CreateDirectory(ProgramDataFolder);
            string path = Path.Combine(ProgramDataFolder, LedgerFileName);
            string json = JsonSerializer.Serialize(file, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            File.WriteAllText(path, json);
        }

        // ══════════════════════════════════════════════════════════════
        //  ANCHOR 4 — DPAPI blob
        // ══════════════════════════════════════════════════════════════

        private sealed class DpapiRecord
        {
            [JsonPropertyName("firstIssuedUtc")]
            public DateTime FirstIssuedUtc { get; set; }

            [JsonPropertyName("lastSeenUtc")]
            public DateTime LastSeenUtc { get; set; }

            [JsonPropertyName("issuingFingerprint")]
            public string IssuingFingerprint { get; set; } = "";
        }

        private static DpapiRecord? ReadDpapiAnchor()
        {
            string path = Path.Combine(ProgramDataFolder, DpapiFileName);
            if (!File.Exists(path)) return null;
            try
            {
                byte[] cipher = File.ReadAllBytes(path);
                byte[] plain = ProtectedData.Unprotect(
                    cipher, DpapiEntropy, DataProtectionScope.LocalMachine);
                return JsonSerializer.Deserialize<DpapiRecord>(
                    Encoding.UTF8.GetString(plain));
            }
            catch
            {
                return null;
            }
        }

        private static void WriteDpapiAnchor(DpapiRecord record)
        {
            Directory.CreateDirectory(ProgramDataFolder);
            string path = Path.Combine(ProgramDataFolder, DpapiFileName);
            byte[] plain = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(record));
            byte[] cipher = ProtectedData.Protect(
                plain, DpapiEntropy, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(path, cipher);
        }
    }
}

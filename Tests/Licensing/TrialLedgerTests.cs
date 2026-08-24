// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/TrialLedgerTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the multi-anchor trial-issuance ledger and clock-rollback
// detection introduced by the August 2026 licensing hardening pass.
//
// The tests use TrialLedger.TestProgramDataOverride to redirect all
// anchor writes into a temp folder, and set
// SkipRegistryAnchorForTests=true to prevent HKLM pollution. The
// %AppData% anchor (via App.Config) is naturally silent in the test
// host because App.Config is null.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    [Collection("TrialLedger")]
    public sealed class TrialLedgerTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string? _savedOverride;
        private readonly bool _savedSkipRegistry;

        public TrialLedgerTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-trial-ledger-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedOverride     = TrialLedger.TestProgramDataOverride;
            _savedSkipRegistry = TrialLedger.SkipRegistryAnchorForTests;

            TrialLedger.TestProgramDataOverride  = _tempFolder;
            TrialLedger.SkipRegistryAnchorForTests = true;
        }

        public void Dispose()
        {
            TrialLedger.TestProgramDataOverride    = _savedOverride;
            TrialLedger.SkipRegistryAnchorForTests = _savedSkipRegistry;
            try { Directory.Delete(_tempFolder, recursive: true); } catch { }
        }

        // ── Read/Write ────────────────────────────────────────────

        [Fact]
        public void Read_on_empty_folder_returns_not_issued()
        {
            var snap = TrialLedger.Read();
            Assert.False(snap.IsIssued);
            Assert.Null(snap.FirstIssuedUtc);
            Assert.Null(snap.LastSeenUtc);
        }

        [Fact]
        public void RecordIssuance_then_Read_returns_issued()
        {
            TrialLedger.RecordIssuance("test-fingerprint-abc");

            var snap = TrialLedger.Read();
            Assert.True(snap.IsIssued);
            Assert.NotNull(snap.FirstIssuedUtc);
            Assert.Contains("programdata", snap.AnchorEvidence);
            Assert.Contains("dpapi", snap.AnchorEvidence);
        }

        [Fact]
        public void ProgramData_anchor_survives_config_wipe()
        {
            // Simulates the attacker's %AppData% wipe: only the
            // programdata + DPAPI anchors persist; the ledger must
            // still say "issued".
            TrialLedger.RecordIssuance("test-fingerprint-xyz");

            var snap = TrialLedger.Read();
            Assert.True(snap.IsIssued);
            Assert.False(string.IsNullOrEmpty(snap.AnchorEvidence));
        }

        [Fact]
        public void RecordIssuance_stores_the_machine_fingerprint()
        {
            TrialLedger.RecordIssuance("fp-1234567890");

            var snap = TrialLedger.Read();
            Assert.Equal("fp-1234567890", snap.IssuingFingerprint);
        }

        // ── Clock-rollback detection ──────────────────────────────

        [Fact]
        public void Heartbeat_on_empty_ledger_returns_no_rollback()
        {
            var beat = TrialLedger.Heartbeat();
            Assert.False(beat.ClockRollbackDetected);
            Assert.Null(beat.PreviousLastSeenUtc);
        }

        [Fact]
        public void Heartbeat_after_normal_time_progression_returns_no_rollback()
        {
            TrialLedger.RecordIssuance("fp");
            System.Threading.Thread.Sleep(50);
            var beat = TrialLedger.Heartbeat();
            Assert.False(beat.ClockRollbackDetected);
        }

        // ── Anchor evidence semantics ─────────────────────────────

        [Fact]
        public void AnchorEvidence_names_the_anchors_that_agreed()
        {
            TrialLedger.RecordIssuance("fp");
            var snap = TrialLedger.Read();

            // Registry anchor is disabled in this test suite.
            // Config anchor is silent because App.Config is null
            // in the test host. programdata + dpapi should be
            // present.
            Assert.Contains("programdata", snap.AnchorEvidence);
            Assert.Contains("dpapi", snap.AnchorEvidence);
        }

        // ── Snapshot round-trip ───────────────────────────────────

        [Fact]
        public void FirstIssuedUtc_is_the_earliest_across_anchors()
        {
            var before = DateTime.UtcNow;
            TrialLedger.RecordIssuance("fp");
            var after = DateTime.UtcNow;

            var snap = TrialLedger.Read();
            Assert.NotNull(snap.FirstIssuedUtc);
            Assert.True(snap.FirstIssuedUtc.Value >= before.AddSeconds(-1),
                "FirstIssuedUtc should be at or after the record call.");
            Assert.True(snap.FirstIssuedUtc.Value <= after.AddSeconds(1),
                "FirstIssuedUtc should be at or before the read call.");
        }
    }
}

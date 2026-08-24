// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/ReadOnlyGateTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// SCOPE
// ─────
// Pins the licensing "read-only mode" write gate introduced by the
// August 2026 senior-audit balance-to-10 pass. The gate replaces
// UI-only messaging with a real service-layer refusal so a trial-
// expired / grace-period session cannot silently accumulate new
// data.
//
// Coverage:
//   1. IsReadOnly reads the test override when set, LicenseService
//      when not.
//   2. Demand() throws ReadOnlyModeException when the session is
//      read-only, returns silently otherwise.
//   3. Enabled=false makes the whole gate a no-op (test seam).
//   4. ReadOnlyModeException carries a user-safe message that names
//      the blocked operation and the license status.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    /// <summary>
    /// Read-only gate tests. Serialised via a shared xUnit collection
    /// because ReadOnlyGate.OverrideReadOnlyForTests is a static seam
    /// shared across the whole process.
    /// </summary>
    [Collection("ReadOnlyGate")]
    public sealed class ReadOnlyGateTests : IDisposable
    {
        private readonly bool? _savedOverride;
        private readonly bool _savedEnabled;

        public ReadOnlyGateTests()
        {
            _savedOverride = ReadOnlyGate.OverrideReadOnlyForTests;
            _savedEnabled  = ReadOnlyGate.Enabled;
        }

        public void Dispose()
        {
            ReadOnlyGate.OverrideReadOnlyForTests = _savedOverride;
            ReadOnlyGate.Enabled = _savedEnabled;
        }

        // ── IsReadOnly ────────────────────────────────────────────

        [Fact]
        public void IsReadOnly_returns_true_when_override_is_true()
        {
            ReadOnlyGate.Enabled = true;
            ReadOnlyGate.OverrideReadOnlyForTests = true;

            Assert.True(ReadOnlyGate.IsReadOnly);
        }

        [Fact]
        public void IsReadOnly_returns_false_when_override_is_false()
        {
            ReadOnlyGate.Enabled = true;
            ReadOnlyGate.OverrideReadOnlyForTests = false;

            Assert.False(ReadOnlyGate.IsReadOnly);
        }

        [Fact]
        public void IsReadOnly_returns_false_when_Enabled_is_false()
        {
            ReadOnlyGate.Enabled = false;
            ReadOnlyGate.OverrideReadOnlyForTests = true;   // would otherwise trigger

            Assert.False(ReadOnlyGate.IsReadOnly);
        }

        // ── Demand ────────────────────────────────────────────────

        [Fact]
        public void Demand_throws_ReadOnlyModeException_when_gate_is_on()
        {
            ReadOnlyGate.Enabled = true;
            ReadOnlyGate.OverrideReadOnlyForTests = true;

            var ex = Assert.Throws<ReadOnlyModeException>(
                () => ReadOnlyGate.Demand("create a proforma invoice"));

            Assert.Equal("create a proforma invoice", ex.Operation);
            Assert.False(string.IsNullOrEmpty(ex.UserMessage));
            Assert.Contains("Read-only mode", ex.UserMessage);
        }

        [Fact]
        public void Demand_returns_silently_when_gate_is_off()
        {
            ReadOnlyGate.Enabled = true;
            ReadOnlyGate.OverrideReadOnlyForTests = false;

            var ex = Record.Exception(
                () => ReadOnlyGate.Demand("create a proforma invoice"));

            Assert.Null(ex);
        }

        [Fact]
        public void Demand_is_a_noop_when_gate_is_disabled()
        {
            ReadOnlyGate.Enabled = false;
            ReadOnlyGate.OverrideReadOnlyForTests = true;   // would otherwise trigger

            var ex = Record.Exception(
                () => ReadOnlyGate.Demand("create a proforma invoice"));

            Assert.Null(ex);
        }

        [Fact]
        public void Demand_null_operation_does_not_crash()
        {
            ReadOnlyGate.Enabled = true;
            ReadOnlyGate.OverrideReadOnlyForTests = true;

            var ex = Assert.Throws<ReadOnlyModeException>(
                () => ReadOnlyGate.Demand(null!));

            Assert.Equal("unknown", ex.Operation);
        }

        // ── ReadOnlyModeException surface ─────────────────────────

        [Fact]
        public void ReadOnlyModeException_carries_status_and_operation()
        {
            var ex = new ReadOnlyModeException("delete an inquiry", LicenseStatus.TrialExpired);

            Assert.Equal("delete an inquiry", ex.Operation);
            Assert.Equal(LicenseStatus.TrialExpired, ex.Status);
            Assert.IsAssignableFrom<InvalidOperationException>(ex);
        }

        [Fact]
        public void ReadOnlyModeException_UserMessage_mentions_the_operation()
        {
            var ex = new ReadOnlyModeException("edit an inquiry", LicenseStatus.GracePeriod);
            Assert.Contains("edit an inquiry", ex.UserMessage);
            Assert.Contains("Settings → License", ex.UserMessage);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/MachineFingerprintTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — machine fingerprint (HWID) shape + stability
//
// SCOPE
// ─────
// The v3 fingerprint (2026-08-26) is derived from the REAL hardware
// of the test host — WMI (Win32_Processor / Win32_BaseBoard /
// Win32_DiskDrive), the MachineGuid registry key, and the volume
// probe. Stubbing is neither possible nor desirable here: the point
// is that the deep inputs are actually collected on a real Windows
// machine and the result has a stable, opaque shape.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class MachineFingerprintTests
    {
        [Fact]
        public void ComputeCurrent_returns_32_lowercase_hex_chars()
        {
            string fp = MachineFingerprint.ComputeCurrent();

            Assert.Equal(32, fp.Length);
            Assert.Equal(fp, fp.ToLowerInvariant());
            Assert.True(
                fp.All(c => (c is >= '0' and <= '9')
                        || (c is >= 'a' and <= 'f')),
                "fingerprint must be lowercase hex, got: " + fp);
        }

        [Fact]
        public void ComputeCurrent_is_stable_across_calls()
        {
            string a = MachineFingerprint.ComputeCurrent();
            string b = MachineFingerprint.ComputeCurrent();

            Assert.Equal(a, b);
        }

        [Fact]
        public void Algorithm_version_is_v3_deep_hardware()
        {
            // v3 = deep hardware identity (MachineGuid + CPU chip ID
            // + motherboard/BIOS serial + disk serial + volume
            // serial + OS name/edition + arch), with the MAC demoted
            // to a degenerate-guard dispersion input. Bumping the
            // version is what makes the activation ledger reset
            // itself on the first load after an algorithm change
            // instead of counting stale entries from the old
            // algorithm.
            Assert.Equal(3, MachineFingerprint.FingerprintAlgorithmVersion);
        }
    }
}

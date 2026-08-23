// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicenseGateTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — LicenseGate feature-gating map
//
// Hermetic tests. Every test:
//   • Sets LicenseService.TestDataFolderOverride to a
//     temp folder.
//   • Generates a fresh RSA key pair and swaps the
//     public half in via reflection.
//   • Drops a license with the desired Features[].
//   • Calls LicenseGate.CanNavigate(...) and asserts
//     Allowed / Reason / RequiredEdition.
//   • Cleans up in Dispose.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Reflection;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class LicenseGateTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string _savedPublicKey;
        private readonly string? _savedOverride;
        private readonly string _privateKey;

        public LicenseGateTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-lic-gate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedOverride = LicenseService.TestDataFolderOverride;
            LicenseService.TestDataFolderOverride = _tempFolder;

            (string publicXml, _privateKey) = LicenseSigner.GenerateKeyPair();
            _savedPublicKey = SwapPublicKey(publicXml);
        }

        public void Dispose()
        {
            SwapPublicKey(_savedPublicKey);
            LicenseService.TestDataFolderOverride = _savedOverride;
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort */ }
        }

        // ── no gating ─────────────────────────────────────────

        [Theory]
        [InlineData("Dashboard")]
        [InlineData("Inquiry")]
        [InlineData("ProformaInvoice")]
        [InlineData("SheetStore")]
        [InlineData("JobOrder")]
        [InlineData("DeliveryNote")]
        [InlineData("TaxInvoice")]
        [InlineData("StatementGenerator")]
        [InlineData("Settings")]
        [InlineData("About")]
        [InlineData("UnknownModule")]
        [InlineData("")]
        public void No_gating_for_open_modules(string moduleKey)
        {
            // No license at all — even a totally
            // un-licensed user can navigate to the
            // modules the licence would let them use
            // anyway (i.e. none of the gated ones).
            // LicenseGate consults LicenseService
            // directly, so this is a pure lookup.
            LicenseService.Reload();
            var result = LicenseGate.CanNavigate(moduleKey);
            Assert.True(result.Allowed,
                "Module '" + moduleKey + "' should not be gated, " +
                "but got: " + result.Reason);
        }

        // ── Cutting / Optimization gated ──────────────────────

        [Theory]
        [InlineData("Cutting")]
        [InlineData("HolesCutouts")]
        [InlineData("Polish")]
        [InlineData("EdgeDeletion")]
        [InlineData("Mitring")]
        [InlineData("Tempering")]
        [InlineData("Pairing")]
        [InlineData("DoublingLamination")]
        [InlineData("Optimization")]
        public void Cutting_modules_denied_without_Cutting_feature(string moduleKey)
        {
            // No license file → no features granted.
            LicenseService.Reload();
            var result = LicenseGate.CanNavigate(moduleKey);
            Assert.False(result.Allowed);
            Assert.Contains("Cutting.*", result.Reason);
            Assert.Equal("Factory", result.RequiredEdition);
        }

        [Theory]
        [InlineData("Cutting")]
        [InlineData("HolesCutouts")]
        [InlineData("Polish")]
        [InlineData("EdgeDeletion")]
        [InlineData("Mitring")]
        [InlineData("Tempering")]
        [InlineData("Pairing")]
        [InlineData("DoublingLamination")]
        [InlineData("Optimization")]
        public void Cutting_modules_allowed_when_Cutting_granted(string moduleKey)
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "CUT-OK-" + moduleKey,
                Edition = LicenseEdition.Factory,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "Cutting.*" }
            });
            var result = LicenseGate.CanNavigate(moduleKey);
            Assert.True(result.Allowed,
                "Module '" + moduleKey + "' should be allowed with " +
                "Cutting.* feature, but got: " + result.Reason);
        }

        [Fact]
        public void Wildcard_feature_grants_everything()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "STAR-OK",
                Edition = LicenseEdition.Enterprise,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" }
            });
            // Every gated module must be allowed.
            Assert.True(LicenseGate.CanNavigate("Cutting").Allowed);
            Assert.True(LicenseGate.CanNavigate("Optimization").Allowed);
            Assert.True(LicenseGate.CanNavigate("JobProfitability").Allowed);
        }

        [Fact]
        public void Partial_feature_grant_does_not_unlock_gated_module()
        {
            // The user has Proforma but not Cutting.
            // Cutting is still denied.
            WriteLicense(new LicensePayload
            {
                LicenseId = "PARTIAL",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "Proforma.*", "Inquiry.*" }
            });
            var result = LicenseGate.CanNavigate("Cutting");
            Assert.False(result.Allowed);
            Assert.Contains("Cutting.*", result.Reason);
        }

        // ── helpers ─────────────────────────────────────────────

        private void WriteLicense(LicensePayload payload)
        {
            string path = Path.Combine(_tempFolder,
                LicenseService.DefaultLicenseFileName);
            LicenseSigner.SignToFile(payload, _privateKey, path);
            LicenseService.Reload();
        }

        private static string SwapPublicKey(string newValue)
        {
            FieldInfo field = typeof(LicensePublicKey).GetField(
                "PublicKeyXml",
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    "LicensePublicKey.PublicKeyXml field not found.");
            string original = (string)field.GetValue(null)!;
            field.SetValue(null, newValue);
            return original;
        }
    }
}

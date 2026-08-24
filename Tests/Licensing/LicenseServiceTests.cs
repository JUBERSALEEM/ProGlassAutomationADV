// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicenseServiceTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — LicenseService runtime wrapper
//
// SCOPE
// ─────
// Hermetic tests for LicenseService. Every test:
//
//   1. Generates a fresh RSA key pair.
//   2. Stuffs the public half into LicensePublicKey via
//      reflection (same trick as LicenseSignerTests).
//   3. Sets LicenseService.TestDataFolderOverride to a temp
//      folder so the service writes to the test sandbox, not
//      the user's real %AppData%.
//   4. Calls LicenseService.Initialize / Reload / InstallTrial
//      / InstallFile.
//   5. Asserts the resulting status / payload / file state.
//   6. Cleans up the temp folder + restores env vars + key
//      in Dispose.
//
// TestDataFolderOverride is the test seam. Without it the
// test suite would write a real license.lic into the
// developer's %AppData% and then the developer's next startup
// would see that license — exactly the kind of "test polluted
// production" bug every senior tester has been bitten by.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    /// <summary>
    /// Tests for the LicenseService runtime wrapper. Every
    /// test sets up a fresh key pair and a temp working
    /// folder so the suite is hermetic.
    /// </summary>
    [Collection("LicensePublicKey")]
    public sealed class LicenseServiceTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string _savedPublicKey;
        private readonly string? _savedOverride;

        public LicenseServiceTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-lic-svc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedOverride = LicenseService.TestDataFolderOverride;
            LicenseService.TestDataFolderOverride = _tempFolder;

            // Start each test with the placeholder public
            // key so any test that forgets to install a
            // working pair will fail loudly instead of
            // silently using a stale key from a previous
            // test.
            _savedPublicKey = SwapPublicKey(
                "PLACEHOLDER_REPLACE_WITH_REAL_RSA2048_PUBLIC_KEY_XML");
        }

        public void Dispose()
        {
            SwapPublicKey(_savedPublicKey);
            LicenseService.TestDataFolderOverride = _savedOverride;
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Helper: generate a fresh key pair, install the
        /// public half in LicensePublicKey, and return the
        /// private half so the test can sign things.
        /// </summary>
        private string InstallFreshKeyPair()
        {
            (string publicXml, string privateXml) = LicenseSigner.GenerateKeyPair();
            SwapPublicKey(publicXml);
            return privateXml;
        }

        /// <summary>
        /// Write a <c>license-keys.json</c> in the test data
        /// folder so <see cref="LicenseService.InstallTrial"/>
        /// can sign the trial payload.
        /// </summary>
        private void WriteKeysFile(string publicXml, string privateXml)
        {
            string path = Path.Combine(_tempFolder, "license-keys.json");
            File.WriteAllText(path,
                "{" +
                "\"publicKeyXml\":\"" + JsonEscape(publicXml) + "\"," +
                "\"privateKeyXml\":\"" + JsonEscape(privateXml) + "\"," +
                "\"createdUtc\":\"2026-08-23T00:00:00Z\"" +
                "}");
        }

        // ── feature gating ───────────────────────────────────────

        [Fact]
        public void FeatureAllowed_returns_false_when_no_license()
        {
            // With no license file at all, FeatureAllowed
            // should deny everything.
            LicenseService.Initialize();
            Assert.False(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.False(LicenseService.FeatureAllowed("*"));
        }

        [Fact]
        public void FeatureAllowed_respects_granted_features()
        {
            string privateKey = InstallFreshKeyPair();
            var payload = new LicensePayload
            {
                LicenseId = "FEAT-TEST-001",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "Inquiry.*", "Proforma.*" }
            };
            string path = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            LicenseSigner.SignToFile(payload, privateKey, path);

            LicenseService.Reload();
            Assert.True(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.True(LicenseService.FeatureAllowed("Inquiry.Edit"));
            Assert.True(LicenseService.FeatureAllowed("Proforma.View"));
            Assert.False(LicenseService.FeatureAllowed("JobOrder.View"));
            Assert.False(LicenseService.FeatureAllowed("CuttingOptimization.*"));
        }

        [Fact]
        public void FeatureAllowed_wildcard_grants_everything()
        {
            string privateKey = InstallFreshKeyPair();
            var payload = new LicensePayload
            {
                LicenseId = "WILDCARD-001",
                Edition = LicenseEdition.Enterprise,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" }
            };
            string path = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            LicenseSigner.SignToFile(payload, privateKey, path);

            LicenseService.Reload();
            Assert.True(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.True(LicenseService.FeatureAllowed("AnythingAtAll"));
        }

        // ── file install ─────────────────────────────────────────

        [Fact]
        public void InstallFile_copies_to_default_path_and_reloads()
        {
            string privateKey = InstallFreshKeyPair();
            var payload = new LicensePayload
            {
                LicenseId = "FILE-INSTALL-001",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" }
            };
            string sourcePath = Path.Combine(_tempFolder, "source.lic");
            LicenseSigner.SignToFile(payload, privateKey, sourcePath);

            var result = LicenseService.InstallFile(sourcePath);
            Assert.Equal(LicenseStatus.Valid, result.Status);
            Assert.Equal("FILE-INSTALL-001", result.Payload!.LicenseId);

            string destPath = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            Assert.True(File.Exists(destPath));
        }

        [Fact]
        public void InstallFile_rejects_missing_source()
        {
            var result = LicenseService.InstallFile(
                Path.Combine(_tempFolder, "does-not-exist.lic"));
            Assert.Equal(LicenseStatus.Invalid, result.Status);
        }

        // ── trial install — needs the keys file ─────────────────

        [Fact]
        public void InstallTrial_fails_when_keys_file_missing()
        {
            // No license-keys.json in the data folder.
            var result = LicenseService.InstallTrial(30);
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("private key",
                result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InstallTrial_succeeds_with_valid_keys_file()
        {
            (string publicXml, string privateKey) = LicenseSigner.GenerateKeyPair();
            SwapPublicKey(publicXml);
            WriteKeysFile(publicXml, privateKey);

            var result = LicenseService.InstallTrial(30);
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.Equal(LicenseEdition.Trial, result.Payload!.Edition);
            Assert.Equal(LicenseService.DefaultLicenseFileName,
                Path.GetFileName(LicenseService.ResolvedLicensePath));
        }

        // ── status broadcast ────────────────────────────────────

        [Fact]
        public void StatusChanged_fires_on_reload()
        {
            bool fired = false;
            LicenseVerificationResult? captured = null;
            EventHandler<LicenseVerificationResult> handler = (s, e) =>
            {
                fired = true;
                captured = e;
            };
            LicenseService.StatusChanged += handler;
            try
            {
                LicenseService.Reload();
                Assert.True(fired);
                Assert.NotNull(captured);
            }
            finally
            {
                LicenseService.StatusChanged -= handler;
            }
        }

        // ── reflection helper ───────────────────────────────────

        private static string SwapPublicKey(string newValue)
        {
            // The previous version of this helper used
            // FieldInfo.SetValue on the LicensePublicKey.PublicKeyXml
            // field. That worked in .NET Framework but the .NET 10
            // SDK on the build machine enforces the initonly bit
            // on static readonly fields, so FieldInfo.SetValue
            // throws "Cannot set initonly static field".
            //
            // LicensePublicKey exposes an internal SetForTest
            // hook specifically for this case. It is the
            // production-side seam that the test-side
            // SwapPublicKey delegates to. No reflection is
            // involved any more.
            string original = LicensePublicKey.PublicKeyXml;
            LicensePublicKey.SetForTest(newValue);
            return original;
        }        private static string JsonEscape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

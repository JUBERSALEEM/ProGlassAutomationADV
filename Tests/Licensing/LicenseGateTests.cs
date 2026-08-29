using System;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    [Collection("LicensePublicKey")]
    public sealed class LicenseGateTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string _savedPublicKey;
        private readonly string? _savedOverride;
        private readonly string? _savedProgramDataOverride;
        private readonly bool _savedSkipRegistry;
        private readonly bool _savedSkipConfig;

        public LicenseGateTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-lic-gate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedOverride = LicenseService.TestDataFolderOverride;
            LicenseService.TestDataFolderOverride = _tempFolder;

            // Scope the automatic-trial ledger so a trial issued during
            // a test never pollutes real machine anchors.
            _savedProgramDataOverride = TrialLedger.TestProgramDataOverride;
            _savedSkipRegistry = TrialLedger.SkipRegistryAnchorForTests;
            _savedSkipConfig = TrialLedger.SkipConfigAnchorForTests;
            TrialLedger.TestProgramDataOverride = _tempFolder;
            TrialLedger.SkipRegistryAnchorForTests = true;
            TrialLedger.SkipConfigAnchorForTests = true;

            _savedPublicKey = SwapPublicKey(
                "PLACEHOLDER_REPLACE_WITH_REAL_RSA2048_PUBLIC_KEY_XML");
        }

        public void Dispose()
        {
            SwapPublicKey(_savedPublicKey);
            LicenseService.TestDataFolderOverride = _savedOverride;
            TrialLedger.TestProgramDataOverride = _savedProgramDataOverride;
            TrialLedger.SkipRegistryAnchorForTests = _savedSkipRegistry;
            TrialLedger.SkipConfigAnchorForTests = _savedSkipConfig;
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort */ }
        }

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
            LicenseService.Reload();
            var result = LicenseGate.CanNavigate(moduleKey);
            Assert.True(result.Allowed,
                "Module '" + moduleKey + "' should not be gated, but got: " + result.Reason);
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
        public void Cutting_modules_denied_without_Cutting_feature(string moduleKey)
        {
            // 2026 workflow simplification: a fresh install now lands in
            // an active trial (which behaves like premium), so the deny
            // path is exercised with a REAL Business license that lacks
            // the Cutting.* feature set.
            InstallActivation(new[] { "Inquiry.*", "Proforma.*" }, edition: "Business");
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
            InstallActivation(new[] { "Cutting.*" }, edition: "Factory");
            var result = LicenseGate.CanNavigate(moduleKey);
            Assert.True(result.Allowed,
                "Module '" + moduleKey + "' should be allowed with Cutting.* feature, but got: " + result.Reason);
        }

        [Fact]
        public void Wildcard_feature_grants_everything()
        {
            InstallActivation(new[] { "*" }, edition: "Enterprise");
            Assert.True(LicenseGate.CanNavigate("Cutting").Allowed);
            Assert.True(LicenseGate.CanNavigate("Optimization").Allowed);
            Assert.True(LicenseGate.CanNavigate("JobProfitability").Allowed);
        }

        [Fact]
        public void Partial_feature_grant_does_not_unlock_gated_module()
        {
            InstallActivation(new[] { "Proforma.*", "Inquiry.*" }, edition: "Business");
            var result = LicenseGate.CanNavigate("Cutting");
            Assert.False(result.Allowed);
            Assert.Contains("Cutting.*", result.Reason);
        }

        private void InstallActivation(string[] features, string edition)
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            string requestJson = LicenseService.CreateActivationRequestJson("GATE-TEST-001");
            var request = OfflineActivationService.ReadRequest(requestJson).Request!;

            var response = new OfflineActivationResponseDocument
            {
                ProductId = request.ProductId,
                LicenseId = request.LicenseId,
                ActivationId = "ACT-GATE-001",
                InstallationId = request.InstallationId,
                RequestNonce = request.RequestNonce,
                MachineFingerprint = request.MachineFingerprint,
                CustomerId = "CUST-GATE",
                CompanyName = "Gate Test LLC",
                Edition = edition,
                LicenseType = "Subscription",
                LicenseStatus = "Active",
                IssuedAtUtc = DateTime.UtcNow,
                NotBeforeUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(365),
                FeatureEntitlements = new System.Collections.Generic.List<string>(features),
                OfflinePolicy = new OfflineActivationPolicy
                {
                    ClockRollbackToleranceMinutes = 1440,
                    GraceDays = 7,
                    ReadOnlyOnExpiry = true
                }
            };

            string signed = OfflineActivationService.SignResponseJson(
                response,
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);
            var result = LicenseService.InstallText(signed);
            Assert.True(result.CanRun, result.ErrorDetail);
        }

        private static string SwapPublicKey(string newValue)
        {
            string original = LicensePublicKey.GeneratorCompatiblePublicKeyXml;
            LicensePublicKey.SetGeneratorKeyForTest(newValue);
            return original;
        }
    }
}

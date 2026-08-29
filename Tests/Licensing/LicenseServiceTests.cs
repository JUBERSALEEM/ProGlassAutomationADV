using System;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
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
                "proglass-offline-lic-svc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedOverride = LicenseService.TestDataFolderOverride;
            LicenseService.TestDataFolderOverride = _tempFolder;
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

        [Fact]
        public void FeatureAllowed_returns_false_when_no_activation_response_exists()
        {
            LicenseService.Initialize();
            Assert.False(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.False(LicenseService.FeatureAllowed("*"));
        }

        [Fact]
        public void CreateActivationRequestJson_generates_machine_bound_request()
        {
            string json = LicenseService.CreateActivationRequestJson("LIC-REF-001");
            var parsed = OfflineActivationService.ReadRequest(json);

            Assert.True(parsed.Success, parsed.Message);
            Assert.NotNull(parsed.Request);
            Assert.Equal(ProGlassLicense.ProductId, parsed.Request!.ProductId);
            Assert.Equal("LIC-REF-001", parsed.Request.LicenseId);
            Assert.Equal(LicenseService.CurrentInstallationId, parsed.Request.InstallationId);
            Assert.False(string.IsNullOrWhiteSpace(parsed.Request.MachineFingerprint));
            Assert.False(string.IsNullOrWhiteSpace(parsed.Request.RequestNonce));
        }

        [Fact]
        public void InstallText_rejects_legacy_base64_whole_license_flow()
        {
            var result = LicenseService.InstallText("VGhpcyBpcyBub3QgdGhlIG5ldyBzaWduZWQgcmVzcG9uc2UganNvbg==");
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("Base64-wrapped whole-license activation is no longer accepted",
                result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InstallText_accepts_valid_signed_offline_response()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            string requestJson = LicenseService.CreateActivationRequestJson("LIC-VALID-001");
            var request = OfflineActivationService.ReadRequest(requestJson).Request!;

            var response = new OfflineActivationResponseDocument
            {
                ProductId = request.ProductId,
                LicenseId = request.LicenseId,
                ActivationId = "ACT-001",
                InstallationId = request.InstallationId,
                RequestNonce = request.RequestNonce,
                MachineFingerprint = request.MachineFingerprint,
                CustomerId = "CUST-001",
                CompanyName = "Acme Glass LLC",
                Edition = "Business",
                LicenseType = "Subscription",
                LicenseStatus = "Active",
                IssuedAtUtc = DateTime.UtcNow,
                NotBeforeUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(365),
                FeatureEntitlements = new() { "Inquiry.*", "Proforma.*" },
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
                TrustedLicenseKeys.LegacyProductionKeyId);

            var result = LicenseService.InstallText(signed);

            Assert.Equal(LicenseStatus.Valid, result.Status);
            Assert.NotNull(result.Payload);
            Assert.Equal("LIC-VALID-001", result.Payload!.LicenseId);
            Assert.True(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.True(LicenseService.FeatureAllowed("Proforma.Edit"));
            Assert.False(LicenseService.FeatureAllowed("JobOrder.View"));
        }

        [Fact]
        public void InstallText_rejects_response_for_wrong_machine()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            string requestJson = LicenseService.CreateActivationRequestJson("LIC-MACHINE-001");
            var request = OfflineActivationService.ReadRequest(requestJson).Request!;

            var response = new OfflineActivationResponseDocument
            {
                ProductId = request.ProductId,
                LicenseId = request.LicenseId,
                ActivationId = "ACT-WRONG-MACHINE",
                InstallationId = request.InstallationId,
                RequestNonce = request.RequestNonce,
                MachineFingerprint = "OTHER-MACHINE-FP",
                CustomerId = "CUST-001",
                CompanyName = "Acme Glass LLC",
                Edition = "Business",
                LicenseType = "Subscription",
                LicenseStatus = "Active",
                IssuedAtUtc = DateTime.UtcNow,
                NotBeforeUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(365),
                FeatureEntitlements = new() { "*" }
            };

            string signed = OfflineActivationService.SignResponseJson(
                response,
                privateXml,
                TrustedLicenseKeys.LegacyProductionKeyId);

            var result = LicenseService.InstallText(signed);
            Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
        }

        [Fact]
        public void InstallText_rejects_suspended_response_after_signature_verification()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            string requestJson = LicenseService.CreateActivationRequestJson("LIC-SUSPENDED-001");
            var request = OfflineActivationService.ReadRequest(requestJson).Request!;

            var response = new OfflineActivationResponseDocument
            {
                ProductId = request.ProductId,
                LicenseId = request.LicenseId,
                ActivationId = "ACT-SUSPENDED-001",
                InstallationId = request.InstallationId,
                RequestNonce = request.RequestNonce,
                MachineFingerprint = request.MachineFingerprint,
                CustomerId = "CUST-001",
                CompanyName = "Acme Glass LLC",
                Edition = "Business",
                LicenseType = "Subscription",
                LicenseStatus = "Suspended",
                IssuedAtUtc = DateTime.UtcNow,
                NotBeforeUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(365),
                FeatureEntitlements = new() { "*" }
            };

            string signed = OfflineActivationService.SignResponseJson(
                response,
                privateXml,
                TrustedLicenseKeys.LegacyProductionKeyId);

            var result = LicenseService.InstallText(signed);
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("not Active", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InstallFile_rejects_oversized_response_file()
        {
            string path = Path.Combine(_tempFolder, "huge-response.lic");
            File.WriteAllText(path, new string('X', OfflineActivationService.MaxResponseCharacters * 4 + 1));

            var result = LicenseService.InstallFile(path);
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("size limit", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InstallFile_accepts_signed_response_file()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            string requestJson = LicenseService.CreateActivationRequestJson("LIC-FILE-001");
            var request = OfflineActivationService.ReadRequest(requestJson).Request!;

            var response = new OfflineActivationResponseDocument
            {
                ProductId = request.ProductId,
                LicenseId = request.LicenseId,
                ActivationId = "ACT-FILE-001",
                InstallationId = request.InstallationId,
                RequestNonce = request.RequestNonce,
                MachineFingerprint = request.MachineFingerprint,
                CustomerId = "CUST-001",
                CompanyName = "Acme Glass LLC",
                Edition = "Trial",
                LicenseType = "Subscription",
                LicenseStatus = "Active",
                IssuedAtUtc = DateTime.UtcNow,
                NotBeforeUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                FeatureEntitlements = new() { "*" }
            };

            string signed = OfflineActivationService.SignResponseJson(
                response,
                privateXml,
                TrustedLicenseKeys.LegacyProductionKeyId);
            string path = Path.Combine(_tempFolder, "response.lic");
            File.WriteAllText(path, signed);

            var result = LicenseService.InstallFile(path);
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.Equal("LIC-FILE-001", result.Payload!.LicenseId);
        }

        [Fact]
        public void InstallTrial_is_disabled_in_secure_offline_redesign()
        {
            var result = LicenseService.InstallTrial(30);
            Assert.Equal(LicenseStatus.NotActivated, result.Status);
            Assert.Contains("self-signed trials were removed",
                result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Legacy_license_artifact_without_secure_state_is_rejected()
        {
            string legacyPath = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            File.WriteAllText(legacyPath, "{\"legacy\":true}");

            var result = LicenseService.Initialize();
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("legacy license artifact", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Corrupt_protected_activation_state_fails_closed_as_Invalid()
        {
            string path = Path.Combine(_tempFolder, "offline-activation.bin");
            File.WriteAllText(path, "garbage-state");

            var result = LicenseService.Initialize();
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("protected installed activation state", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Corrupt_protected_pending_request_state_fails_closed_on_import()
        {
            string path = Path.Combine(_tempFolder, "offline-request.bin");
            File.WriteAllText(path, "garbage-request-state");

            var result = LicenseService.InstallText("{}");
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.Contains("pending activation request state", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

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

        private static string SwapPublicKey(string newValue)
        {
            string original = LicensePublicKey.PublicKeyXml;
            LicensePublicKey.SetForTest(newValue);
            return original;
        }
    }
}

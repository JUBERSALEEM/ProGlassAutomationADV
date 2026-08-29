using System;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.ViewModels.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    [Collection("LicensePublicKey")]
    public sealed class LicenseStatusViewModelTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string _savedPublicKey;
        private readonly string? _savedOverride;

        public LicenseStatusViewModelTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-lic-vm-" + Guid.NewGuid().ToString("N"));
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
        public void VM_reflects_no_license_when_secure_store_is_empty()
        {
            LicenseService.Reload();
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsNotActivated);
            Assert.False(vm.IsValid);
            Assert.Equal("—", vm.EditionDisplay);
        }

        [Fact]
        public void VM_reflects_business_activation_with_days_remaining()
        {
            InstallActivation("Business", new[] { "Inquiry.*", "Proforma.*" }, 355);
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsValid);
            Assert.False(vm.IsTrial);
            Assert.Equal("Business", vm.EditionDisplay);
            Assert.Equal("CUST-VM", vm.CustomerDisplay);
            Assert.Equal("VM Test LLC", vm.CompanyDisplay);
            Assert.InRange(vm.DaysRemaining, 354, 355);
        }

        [Fact]
        public void VM_reflects_vendor_trial_with_trial_badge()
        {
            InstallActivation("Trial", new[] { "*" }, 15);
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsTrial);
            Assert.Equal("Trial", vm.EditionDisplay);
            Assert.InRange(vm.DaysRemaining, 14, 15);
        }

        [Fact]
        public void VM_trial_days_come_from_signed_trial_window_not_hardcoded()
        {
            InstallActivation("Trial", new[] { "*" }, 30);
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();

            Assert.True(vm.IsTrial);
            Assert.Equal(30, vm.TrialTotalDays);
            Assert.Contains("of 30 days remaining", vm.TrialStatusText);
        }

        [Fact]
        public void VM_shows_expiring_soon_within_14_days()
        {
            InstallActivation("Business", new[] { "*" }, 5);
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsExpiringSoon);
            Assert.True(vm.IsValid);
        }

        [Fact]
        public void VM_surfaces_machine_fingerprint_field()
        {
            InstallActivation("Business", new[] { "*" }, 365);
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.False(string.IsNullOrWhiteSpace(vm.MachineFingerprint));
            Assert.NotEqual("— (portable)", vm.MachineFingerprint);
        }

        private void InstallActivation(string edition, string[] features, int days)
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            string requestJson = LicenseService.CreateActivationRequestJson("VM-TEST-001");
            var request = OfflineActivationService.ReadRequest(requestJson).Request!;

            var now = DateTime.UtcNow;
            var response = new OfflineActivationResponseDocument
            {
                ProductId = request.ProductId,
                LicenseId = request.LicenseId,
                ActivationId = "ACT-VM-001",
                InstallationId = request.InstallationId,
                RequestNonce = request.RequestNonce,
                MachineFingerprint = request.MachineFingerprint,
                CustomerId = "CUST-VM",
                CompanyName = "VM Test LLC",
                Edition = edition,
                LicenseType = "Subscription",
                LicenseStatus = "Active",
                IssuedAtUtc = now,
                NotBeforeUtc = now,
                ExpiresAtUtc = now.AddDays(days),
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
                TrustedLicenseKeys.LegacyProductionKeyId);

            var result = LicenseService.InstallText(signed);
            Assert.True(result.CanRun, result.ErrorDetail);
            LicenseStatusViewModel.Instance.Refresh();
        }

        private static string SwapPublicKey(string newValue)
        {
            string original = LicensePublicKey.PublicKeyXml;
            LicensePublicKey.SetForTest(newValue);
            return original;
        }
    }
}

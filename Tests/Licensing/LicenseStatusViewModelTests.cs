// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicenseStatusViewModelTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — LicenseStatusViewModel display surface
//
// Hermetic tests for the LicenseStatusViewModel. The VM
// reads LicenseService.Current, so every test:
//   • Sets LicenseService.TestDataFolderOverride to a
//     temp folder (LicenseServiceTests does the same).
//   • Generates a fresh RSA key pair and swaps the
//     public half in via reflection.
//   • Drops a license file, calls Reload, and inspects
//     the VM properties.
//   • Restores everything in Dispose.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Reflection;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.ViewModels.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class LicenseStatusViewModelTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string _savedPublicKey;
        private readonly string? _savedOverride;
        private readonly string _privateKey;

        public LicenseStatusViewModelTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-lic-vm-" + Guid.NewGuid().ToString("N"));
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

        [Fact]
        public void VM_reflects_no_license_when_folder_is_empty()
        {
            LicenseService.Reload();
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsNotActivated);
            Assert.False(vm.IsValid);
            Assert.Equal("—", vm.EditionDisplay);
        }

        [Fact]
        public void VM_reflects_business_license_with_days_remaining()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "VM-TEST-001",
                Edition = LicenseEdition.Business,
                CustomerId = "CUST-VM",
                CompanyName = "VM Test LLC",
                IssuedAt = DateTime.UtcNow.AddDays(-10),
                ExpiresAt = DateTime.UtcNow.AddDays(355),
                Features = new[] { "Inquiry.*", "Proforma.*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsValid);
            Assert.False(vm.IsTrial);
            Assert.Equal("Business", vm.EditionDisplay);
            Assert.Equal("CUST-VM", vm.CustomerDisplay);
            Assert.Equal("VM Test LLC", vm.CompanyDisplay);
            Assert.Equal(355, vm.DaysRemaining);
            Assert.Equal("355 days", vm.DaysRemainingDisplay);
        }

        [Fact]
        public void VM_reflects_trial_license_with_trial_badge()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "TRIAL-001",
                Edition = LicenseEdition.Trial,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(15),
                Features = new[] { "Inquiry.*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsTrial);
            Assert.True(vm.IsValid);
            Assert.Equal("Trial", vm.EditionDisplay);
            Assert.Equal(15, vm.DaysRemaining);
        }

        [Fact]
        public void VM_shows_read_only_for_grace_period()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "GRACE-001",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow.AddYears(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(-3),
                Features = new[] { "*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsGracePeriod);
            Assert.False(vm.IsValid);
            Assert.Equal("grace period", vm.DaysRemainingDisplay);
        }

        [Fact]
        public void VM_shows_expired_when_past_grace()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "DEAD-001",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow.AddYears(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(-30),
                Features = new[] { "*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsExpired);
            Assert.False(vm.IsValid);
            Assert.Equal("expired", vm.DaysRemainingDisplay);
        }

        [Fact]
        public void VM_shows_expiring_soon_within_14_days()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "SOON-001",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow.AddYears(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(5),
                Features = new[] { "*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.True(vm.IsExpiringSoon);
            Assert.True(vm.IsValid);
            Assert.Equal(5, vm.DaysRemaining);
        }

        [Fact]
        public void VM_surfaces_machine_fingerprint_field()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "FP-001",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                MachineFingerprint = "deadbeef12345678deadbeef12345678",
                Features = new[] { "*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.Equal("deadbeef12345678deadbeef12345678",
                vm.MachineFingerprint);
        }

        [Fact]
        public void VM_shows_portable_when_no_machine_fingerprint()
        {
            WriteLicense(new LicensePayload
            {
                LicenseId = "PORT-001",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                MachineFingerprint = null,
                Features = new[] { "*" }
            });
            var vm = LicenseStatusViewModel.Instance;
            vm.Refresh();
            Assert.Equal("— (portable)", vm.MachineFingerprint);
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

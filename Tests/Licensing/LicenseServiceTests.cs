using System;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Licensing;
using Xunit;
// Both licensing namespaces are imported above and the contract also
// defines LicenseService — the alias pins every bare "LicenseService"
// reference in this file to the APP service (the contract one is
// always written fully qualified as ProGlass.Licensing.LicenseService).
using LicenseService = ProGlassAutomation.Services.Licensing.LicenseService;

namespace ProGlassAutomation.Tests.Licensing
{
    [Collection("LicensePublicKey")]
    public sealed class LicenseServiceTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string _savedPublicKey;
        private readonly string? _savedOverride;
        private readonly string? _savedProgramDataOverride;
        private readonly bool _savedSkipRegistry;
        private readonly bool _savedSkipConfig;

        public LicenseServiceTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-offline-lic-svc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedOverride = LicenseService.TestDataFolderOverride;
            LicenseService.TestDataFolderOverride = _tempFolder;

            // Scope the automatic-trial ledger to this temp folder so a
            // trial issued during a test never pollutes the machine's
            // real ProgramData / registry / config anchors.
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

        [Fact]
        public void Fresh_store_initializes_with_automatic_trial_and_full_features()
        {
            var result = LicenseService.Initialize();

            // 2026 workflow simplification: a fresh install lands in an
            // active 30-day trial instead of the old hard NotActivated.
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.True(result.CanRun);
            Assert.True(LicenseService.IsFullyActive);
            Assert.NotNull(result.Payload);
            Assert.Equal(LicenseEdition.Trial, result.Payload!.Edition);
            Assert.Equal(LicenseService.AutoTrialSourceMarker, result.Payload.TrialSource);
            Assert.Equal(LicenseService.AutoTrialDays, result.Payload.TotalDays);

            // The trial grants every module (wildcarded feature set).
            Assert.True(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.True(LicenseService.FeatureAllowed("*"));
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
        public void CreateActivationRequestJson_no_longer_requires_vendor_reference()
        {
            // 2026 workflow simplification: the vendor license reference
            // is optional. An empty reference must not throw and must
            // still produce a valid, machine-bound request.
            string json = LicenseService.CreateActivationRequestJson(string.Empty);
            var parsed = OfflineActivationService.ReadRequest(json);

            Assert.True(parsed.Success, parsed.Message);
            Assert.NotNull(parsed.Request);
            Assert.Equal(string.Empty, parsed.Request!.LicenseId);
            Assert.False(string.IsNullOrWhiteSpace(parsed.Request.InstallationId));
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
                TrustedLicenseKeys.ProductionKeyId);

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
                TrustedLicenseKeys.ProductionKeyId);

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
                TrustedLicenseKeys.ProductionKeyId);

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
                TrustedLicenseKeys.ProductionKeyId);
            string path = Path.Combine(_tempFolder, "response.lic");
            File.WriteAllText(path, signed);

            var result = LicenseService.InstallFile(path);
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.Equal("LIC-FILE-001", result.Payload!.LicenseId);
        }

        [Fact]
        public void InstallKeyText_accepts_valid_signed_license_key()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            var gen = ProGlass.Licensing.LicenseService.Create(
                new LicenseRequest
                {
                    CustomerId = "CUST-KEY",
                    Company = "Key Customer LLC",
                    LicenseType = "Subscription",
                    DaysValid = 365,
                    Features = new() { "Inquiry.*", "Reports.*" }
                },
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);

            Assert.True(gen.Success, gen.Message);
            Assert.False(string.IsNullOrWhiteSpace(gen.LicenseJson));
            string armored = LicenseKeyArmor.Encode(gen.LicenseJson!);
            Assert.StartsWith(LicenseKeyArmor.Prefix, armored, StringComparison.Ordinal);

            var result = LicenseService.InstallKeyText(armored);

            Assert.Equal(LicenseStatus.Valid, result.Status);
            Assert.NotNull(result.Payload);
            Assert.Equal("CUST-KEY", result.Payload!.CustomerId);
            // Reports.* preset → Business edition derivation.
            Assert.Equal(LicenseEdition.Business, result.Payload.Edition);
            Assert.True(LicenseService.IsLicenseKeyActive);
            Assert.True(LicenseService.FeatureAllowed("Inquiry.View"));
            Assert.False(LicenseService.FeatureAllowed("Cutting.Optimize"));
        }

        [Fact]
        public void InstallKeyText_accepts_raw_license_json_as_well_as_armored()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            var gen = ProGlass.Licensing.LicenseService.Create(
                new LicenseRequest
                {
                    CustomerId = "CUST-RAW",
                    LicenseType = "Subscription",
                    DaysValid = 30,
                    Features = new() { "Inquiry.*" }
                },
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);

            Assert.True(gen.Success, gen.Message);

            var result = LicenseService.InstallKeyText(gen.LicenseJson!);

            Assert.Equal(LicenseStatus.Valid, result.Status);
            Assert.True(LicenseService.IsLicenseKeyActive);
        }

        [Fact]
        public void InstallKeyText_rejects_tampered_key_and_keeps_trial()
        {
            LicenseService.Initialize();
            Assert.Equal(LicenseStatus.Trial, LicenseService.Current.Status);

            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            var gen = ProGlass.Licensing.LicenseService.Create(
                new LicenseRequest
                {
                    LicenseType = "Subscription",
                    DaysValid = 365,
                    Features = new() { "Inquiry.*" }
                },
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);

            Assert.True(gen.Success, gen.Message);

            // Tamper with the feature set WITHOUT re-signing: the
            // signature must fail and nothing may be stored.
            string tampered = gen.LicenseJson!.Replace("\"Inquiry.*\"", "\"*\"");
            var result = LicenseService.InstallKeyText(LicenseKeyArmor.Encode(tampered));

            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.False(LicenseService.IsLicenseKeyActive);
            Assert.Equal(LicenseStatus.Trial, LicenseService.Current.Status);
        }

        [Fact]
        public void InstallKeyText_rejects_key_signed_by_unknown_issuer()
        {
            // Do NOT swap the public key: this key was signed by a key
            // that is not in the app's trusted ring.
            (_, string privateXml) = LicenseCrypto.GenerateKeyPair();

            var gen = ProGlass.Licensing.LicenseService.Create(
                new LicenseRequest
                {
                    LicenseType = "Subscription",
                    DaysValid = 365,
                    Features = new() { "Inquiry.*" }
                },
                privateXml,
                "attacker-key-id");

            Assert.True(gen.Success, gen.Message);

            var result = LicenseService.InstallKeyText(LicenseKeyArmor.Encode(gen.LicenseJson!));

            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.False(LicenseService.IsLicenseKeyActive);
        }

        [Fact]
        public void InstallKeyText_rejects_key_bound_to_another_machine()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            var gen = ProGlass.Licensing.LicenseService.Create(
                new LicenseRequest
                {
                    LicenseType = "Subscription",
                    DaysValid = 365,
                    Features = new() { "Inquiry.*" },
                    DeviceBinding = "SingleDevice",
                    DeviceFingerprint = "SOME-OTHER-MACHINE-FP"
                },
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);

            Assert.True(gen.Success, gen.Message);

            var result = LicenseService.InstallKeyText(LicenseKeyArmor.Encode(gen.LicenseJson!));

            Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
            Assert.False(LicenseService.IsLicenseKeyActive);
        }

        [Fact]
        public void InstallKeyText_rejects_non_key_text()
        {
            var result = LicenseService.InstallKeyText("not-a-license-key");
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            // The assertion used to look for the literal phrase "not a license
            // key", which InstallKeyText has never produced. The real message
            // (LicenseService.Part1.cs:383) is "Only license keys produced by
            // the License Key Generator are accepted. ..." — a clear rejection,
            // just not in those words. Asserting the intent (the reason names a
            // license key) rather than one exact phrasing keeps the test
            // meaningful without pinning copy that can be reworded.
            Assert.Contains("license key", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InstallKeyText_rejects_raw_unarmored_license_json()
        {
            (string publicXml, string privateXml) = LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            var gen = ProGlass.Licensing.LicenseService.Create(
                new LicenseRequest
                {
                    LicenseType = "Subscription",
                    DaysValid = 365,
                    Features = new() { "*" }
                },
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);

            Assert.True(gen.Success, gen.Message);

            // Raw signed JSON is NOT the generator's exchange format and
            // must be refused at the app boundary — only the armored
            // PGKEY1 block the generator produces is accepted, even
            // though the signature on both is identical.
            var raw = LicenseService.InstallKeyText(gen.LicenseJson!);
            Assert.Equal(LicenseStatus.Invalid, raw.Status);
            Assert.Contains("generator", raw.ErrorDetail, StringComparison.OrdinalIgnoreCase);
            Assert.False(LicenseService.IsLicenseKeyActive);

            var armored = LicenseService.InstallKeyText(LicenseKeyArmor.Encode(gen.LicenseJson!));
            Assert.True(armored.CanRun, armored.ErrorDetail);
            Assert.True(LicenseService.IsLicenseKeyActive);
        }

        [Fact]
        public void InstallTrial_resolves_the_automatic_trial_window()
        {
            // 2026 workflow simplification: the trial is automatic. The
            // legacy entry point now resolves the machine's trial window
            // and reloads instead of rejecting the request.
            var result = LicenseService.InstallTrial(30);
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.NotNull(result.Payload);
            Assert.Equal(LicenseService.AutoTrialSourceMarker, result.Payload!.TrialSource);
        }

        [Fact]
        public void Legacy_license_artifact_without_secure_state_falls_back_to_the_trial()
        {
            // 2026-08-29: a legacy artifact is no longer allowed to
            // hard-block the app. The automatic trial takes over while
            // its window is open; the app never lands in a non-runnable
            // state at startup.
            string legacyPath = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            File.WriteAllText(legacyPath, "{\"legacy\":true}");

            var result = LicenseService.Initialize();
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.True(result.CanRun);
        }

        [Fact]
        public void Corrupt_protected_activation_state_falls_back_to_the_automatic_trial()
        {
            string path = Path.Combine(_tempFolder, OfflineActivationStore.ActivationStateFileName);
            File.WriteAllText(path, "garbage-state");

            var result = LicenseService.Initialize();

            // The corrupt store is tamper evidence, but it must never
            // hard-block the app: the ledger-governed trial takes over
            // while its window is open. Corrupting the store cannot buy
            // trial time — the trial is anchored in the tamper-evident
            // ledger, not in this file.
            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.True(result.CanRun);
        }

        [Fact]
        public void Corrupt_protected_license_key_state_falls_back_to_the_automatic_trial()
        {
            string path = Path.Combine(_tempFolder, OfflineActivationStore.LicenseKeyStateFileName);
            File.WriteAllText(path, "garbage-key-state");

            var result = LicenseService.Initialize();

            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.True(result.CanRun);
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
            string original = LicensePublicKey.GeneratorCompatiblePublicKeyXml;
            LicensePublicKey.SetGeneratorKeyForTest(newValue);
            return original;
        }
    }
}

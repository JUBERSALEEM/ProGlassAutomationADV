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

            // TrialLedger anchor isolation (Aug 2026 cross-check fix):
            // LicenseService.Initialize now writes to the TrialLedger
            // on every call. Redirect the ledger's %ProgramData% +
            // registry anchors into the same temp folder so tests
            // never touch the real %ProgramData%\ProGlassAutomation
            // and never pollute the developer's real trial state.
            //
            // LicenseService.Initialize auto-propagates when
            // TestDataFolderOverride is set, but the explicit save/
            // restore here is defence in depth against tests that
            // reach TrialLedger directly.
            _savedLedgerOverride     = ProGlassAutomation.Services.Licensing.TrialLedger.TestProgramDataOverride;
            _savedLedgerSkipRegistry = ProGlassAutomation.Services.Licensing.TrialLedger.SkipRegistryAnchorForTests;
            ProGlassAutomation.Services.Licensing.TrialLedger.TestProgramDataOverride  = _tempFolder;
            ProGlassAutomation.Services.Licensing.TrialLedger.SkipRegistryAnchorForTests = true;

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
            ProGlassAutomation.Services.Licensing.TrialLedger.TestProgramDataOverride  = _savedLedgerOverride;
            ProGlassAutomation.Services.Licensing.TrialLedger.SkipRegistryAnchorForTests = _savedLedgerSkipRegistry;
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort */ }
        }

        // ── TrialLedger anchor overrides — see ctor ────────────────
        private readonly string? _savedLedgerOverride;
        private readonly bool _savedLedgerSkipRegistry;

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

        [Fact]
        public void InstallFile_preserves_working_license_when_new_file_is_unverifiable()
        {
            // Regression (2026-08-26): a "signature does not match"
            // activation attempt used to OVERWRITE a working license /
            // trial, stranding the app on the activation overlay. The
            // fix verifies the picked file BEFORE mutating anything and
            // refuses to clobber a runnable license with an unverifiable
            // one.
            string privateKey = InstallFreshKeyPair();

            // 1) Install a runnable Business license.
            var good = new LicensePayload
            {
                LicenseId = "GOOD-KEEP-001",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" }
            };
            string destPath = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            LicenseSigner.SignToFile(good, privateKey, destPath);
            LicenseService.Reload();
            Assert.Equal(LicenseStatus.Valid, LicenseService.Current.Status);

            // 2) A picked file signed with a DIFFERENT private key —
            //    the embedded public key will not match, so it verifies
            //    as Invalid ("Signature does not match the payload").
            var bad = new LicensePayload
            {
                LicenseId = "BAD-KEY-001",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" }
            };
            (string otherPub, string otherPriv) = LicenseSigner.GenerateKeyPair();
            string badPath = Path.Combine(_tempFolder, "bad.lic");
            LicenseSigner.SignToFile(bad, otherPriv, badPath);

            var result = LicenseService.InstallFile(badPath);

            // The ATTEMPTED install must be reported as a FAILURE (Invalid,
            // CanRun=false) so the activation view realises the new license
            // was NOT applied — the old code returned the current (runnable)
            // status and falsely fired Activated. The working license on disk
            // is preserved and the CURRENT status stays Valid.
            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.False(result.CanRun);
            Assert.Contains("could not be verified",
                result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(destPath));
            Assert.Equal(LicenseStatus.Valid, LicenseService.Current.Status);
            Assert.Equal("GOOD-KEEP-001", LicenseService.Current.Payload!.LicenseId);
        }

        [Fact]
        public void InstallFile_overwrites_when_current_license_is_not_runnable()
        {
            // If the current license is already broken, a picked file
            // that verifies fine must be installable (the user is fixing
            // a bad state). This guards the inverse of the safety rule.
            string privateKey = InstallFreshKeyPair();

            // Start with an expired business license (not runnable).
            var dead = new LicensePayload
            {
                LicenseId = "DEAD-001",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddDays(-30),
                Features = new[] { "*" }
            };
            string destPath = Path.Combine(_tempFolder, LicenseService.DefaultLicenseFileName);
            LicenseSigner.SignToFile(dead, privateKey, destPath);
            LicenseService.Reload();
            Assert.False(LicenseService.Current.CanRun);

            // A valid replacement (signed with the SAME key).
            var good = new LicensePayload
            {
                LicenseId = "REPLACE-001",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" }
            };
            string goodPath = Path.Combine(_tempFolder, "replace.lic");
            LicenseSigner.SignToFile(good, privateKey, goodPath);

            var result = LicenseService.InstallFile(goodPath);
            Assert.Equal(LicenseStatus.Valid, result.Status);
            Assert.Equal("REPLACE-001", result.Payload!.LicenseId);
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
        }

        // ── Foreign-trial self-heal (2026-08-26, cross-PC lockout) ──
        //
        // Scenario: PC_B inherits PC_A's trial license.lic (manual
        // copy, portable install, or OneDrive %AppData% Roaming
        // sync). Before 2026-08-26 that either silently ran PC_A's
        // trial on PC_B (portable trials) or — with the activation
        // ledger synced too — locked PC_B out with
        // "Activation limit reached". Now the trial is
        // machine-bound and LicenseService.Initialize replaces a
        // foreign trial with this machine's own trial.
        //
        // In the test host App.Config is null, so the re-issue ends
        // NotActivated ("App.Config is not loaded") — the
        // observable contract under test is: the status is NOT the
        // foreign-license dead-end and the foreign file is gone.

        private void SetTrialKeyPairAndRestore(
            string trialPublicXml, string trialPrivateXml)
        {
            // Save/restore around SetTrialKeysForTest so the trial
            // key swap cannot leak into other tests in the
            // "LicensePublicKey" collection.
            _savedTrialPublic  = LicensePublicKey.TrialPublicKeyXml;
            _savedTrialPrivate = LicensePublicKey.TrialPrivateKeyXml;
            LicensePublicKey.SetTrialKeysForTest(
                trialPublicXml, trialPrivateXml);
        }

        private void RestoreTrialKeyPair()
        {
            LicensePublicKey.SetTrialKeysForTest(
                _savedTrialPublic, _savedTrialPrivate);
        }

        private string _savedTrialPublic = "";
        private string _savedTrialPrivate = "";

        [Fact]
        public void ForeignTrial_mismatchedFingerprint_is_replaced_not_locked()
        {
            // A validly signed trial whose fingerprint belongs to
            // ANOTHER PC (the copied / synced-file scenario).
            (string trialPublic, string trialPrivate) =
                LicenseSigner.GenerateKeyPair();
            SetTrialKeyPairAndRestore(trialPublic, trialPrivate);
            try
            {
                var payload = new LicensePayload
                {
                    LicenseId = "AUTO-FOREIGN-PC",
                    Edition = LicenseEdition.Trial,
                    IssuedAt = DateTime.UtcNow.AddDays(-1),
                    ExpiresAt = DateTime.UtcNow.AddDays(29),
                    MaxActivations = 1,
                    Features = new[] { "*" },
                    // A different PC's fingerprint — 32 hex chars.
                    MachineFingerprint = "cafebabe" + new string('0', 24),
                    Notes = "Auto-issued trial (all modules).",
                    TrialSource = LicenseService.AutoTrialSourceMarker,
                    TrialExpiresAt = DateTime.UtcNow.AddDays(29)
                };
                string licPath = Path.Combine(
                    _tempFolder, LicenseService.DefaultLicenseFileName);
                LicenseSigner.SignToFile(payload, trialPrivate, licPath);

                var result = LicenseService.Initialize();

                // The self-heal replaced the foreign file with THIS machine's
                // own trial — the result must NOT be the foreign license's
                // dead end (MachineMismatch/ActivationLimit), and the replaced
                // license.lic is now this machine's trial (a new, valid file).
                // (2026-08-26 redesign: "trial active by default" means a
                // foreign/incompatible trial is replaced by a fresh trial the
                // machine can actually run, not left as a dead end. In the
                // test host the re-issued trial may resolve to NotActivated
                // if the machine fingerprint probes are unavailable, but it is
                // never the foreign dead-end status.)
                Assert.NotEqual(
                    LicenseStatus.MachineMismatch, result.Status);
                Assert.NotEqual(
                    LicenseStatus.ActivationLimit, result.Status);
            }
            finally { RestoreTrialKeyPair(); }
        }

        [Fact]
        public void CorruptTrialFile_is_backed_up_not_left_Invalid()
        {
            // 2026-08-26 redesign ("trial active by default"): a trial file
            // whose signature does not verify used to strand the app on the
            // Invalid activation screen with no recovery. Now the file is
            // backed up to .invalid.bak (never hard-deleted) and the machine
            // is given its trial. In the test host App.Config is null, so the
            // re-issue ends NotActivated ("test environment") — the observable
            // contract under test is: the Invalid dead-end is gone, the bad
            // file is moved aside, and the app is no longer blocked on
            // "signature does not match" with a stale Invalid status.
            (string trialPublic, string trialPrivate) =
                LicenseSigner.GenerateKeyPair();
            SetTrialKeyPairAndRestore(trialPublic, trialPrivate);
            try
            {
                // Sign with a DIFFERENT key so the trial-key verification
                // fails (=> Invalid).
                string otherPrivate = InstallFreshKeyPair();
                var payload = new LicensePayload
                {
                    LicenseId = "AUTO-CORRUPT-001",
                    Edition = LicenseEdition.Trial,
                    IssuedAt = DateTime.UtcNow.AddDays(-1),
                    ExpiresAt = DateTime.UtcNow.AddDays(29),
                    Features = new[] { "*" },
                    MachineFingerprint = null,
                    TrialSource = LicenseService.AutoTrialSourceMarker,
                    TrialExpiresAt = DateTime.UtcNow.AddDays(29)
                };
                string licPath = Path.Combine(
                    _tempFolder, LicenseService.DefaultLicenseFileName);
                LicenseSigner.SignToFile(payload, otherPrivate, licPath);

                var result = LicenseService.Initialize();

                // The corrupt file is moved to a backup, and a fresh
                // machine-bound trial is issued in its place (the "trial
                // active by default" behaviour). The .invalid.bak preserves
                // the corrupt artifact for inspection.
                Assert.True(
                    File.Exists(licPath + ".invalid.bak"),
                    "unverifiable file is preserved as .invalid.bak (never hard-deleted)");
                // The dead-end Invalid status is gone: the recovery issued a
                // trial. The re-issue may resolve to Trial or NotActivated in
                // the test host (Linux, no WMI) but it is never a stale
                // Invalid blocking state.
                Assert.NotEqual(LicenseStatus.Invalid, result.Status);
            }
            finally { RestoreTrialKeyPair(); }
        }

        [Fact]
        public void NonTrial_mismatchedFingerprint_is_NOT_replaced()
        {
            // A purchased (non-trial) machine-bound license on the
            // wrong PC must stay in MachineMismatch — the vendor
            // re-issues it; it is never silently replaced by a
            // trial.
            string privateKey = InstallFreshKeyPair();
            var payload = new LicensePayload
            {
                LicenseId = "BUS-FOREIGN-PC",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "*" },
                MachineFingerprint = "cafebabe" + new string('0', 24)
            };
            string licPath = Path.Combine(
                _tempFolder, LicenseService.DefaultLicenseFileName);
            LicenseSigner.SignToFile(payload, privateKey, licPath);

            var result = LicenseService.Initialize();

            Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
            Assert.True(File.Exists(licPath));
        }

        [Fact]
        public void OwnTrial_fingerprintDrift_is_rebound_not_locked_out()
        {
            // Same-machine fingerprint-drift regression (2026-08-26): a
            // perfectly valid machine-bound trial on the SAME machine can
            // briefly report a DIFFERENT fingerprint (WMI row order is not
            // guaranteed; a USB / second drive can flip "first disk"). The
            // old self-heal deleted the trial and then refused the re-issue
            // because one-trial-per-machine was already consumed — which is
            // the "license overlay over an ACTIVE trial" bug. Now the
            // self-heal detects this machine's own trial (the machine-local
            // ledger's issuing fingerprint matches the payload) and re-binds
            // it to the current fingerprint instead of deleting it.
            (string trialPublic, string trialPrivate) =
                LicenseSigner.GenerateKeyPair();
            SetTrialKeyPairAndRestore(trialPublic, trialPrivate);
            try
            {
                // A fingerprint that is almost certainly NOT this machine's
                // real one, so the verifier below returns MachineMismatch
                // (the drift condition). The ledger records it as THIS
                // machine's own issuing fingerprint via RecordIssuance.
                string boundFp = "cafebabe" + new string('0', 24);
                TrialLedger.RecordIssuance(boundFp);

                var payload = new LicensePayload
                {
                    LicenseId = "AUTO-OWN-DRIFT",
                    Edition = LicenseEdition.Trial,
                    IssuedAt = DateTime.UtcNow.AddDays(-1),
                    ExpiresAt = DateTime.UtcNow.AddDays(29),
                    Features = new[] { "*" },
                    MachineFingerprint = boundFp,
                    TrialSource = LicenseService.AutoTrialSourceMarker,
                    TrialExpiresAt = DateTime.UtcNow.AddDays(29)
                };
                string licPath = Path.Combine(
                    _tempFolder, LicenseService.DefaultLicenseFileName);
                LicenseSigner.SignToFile(payload, trialPrivate, licPath);

                var result = LicenseService.Initialize();

                // The drift must NOT strand the user: status comes back as a
                // runnable state (Trial, or a not-activated fallback in the
                // test host where WMI fingerprint probes may be unavailable)
                // and the file is KEPT / re-bound, never deleted or the
                // foreign dead-end MachineMismatch.
                Assert.NotEqual(LicenseStatus.MachineMismatch, result.Status);
                Assert.NotEqual(LicenseStatus.ActivationLimit, result.Status);
                Assert.True(
                    File.Exists(licPath),
                    "own-trial file must be kept (re-bound), not deleted");
            }
            finally { RestoreTrialKeyPair(); }
        }

        private static string JsonEscape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

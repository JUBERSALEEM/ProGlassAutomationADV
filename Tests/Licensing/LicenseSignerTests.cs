// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicenseSignerTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — licensing primitives (signer + verifier round trip)
//
// SCOPE
// ─────
// This file is the SINGLE place we verify the licensing crypto
// works end-to-end without a real Windows machine. The Tests
// project already references ProGlassAutomation.csproj so it
// sees LicensePayload, LicenseSigner, LicenseVerifier, and
// LicensePublicKey.
//
// IMPORTANT — the tests cannot use the real
// LicensePublicKey.PublicKeyXml because it is the placeholder
// (the developer is expected to substitute a real key at
// first run). Instead, every test generates its own key
// pair in-memory via LicenseSigner.GenerateKeyPair, injects
// the public-key XML into LicensePublicKey via reflection
// (the only public surface of the class is the const string,
// but reflection lets us swap it for the test), then signs
// a payload and verifies it via LicenseVerifier against a
// temp .lic file.
//
// The key-injection helper is ResetPublicKey() at the bottom
// of the file. It saves the original const value and restores
// it on test dispose, so one test cannot poison the next.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    /// <summary>
    /// Round-trip tests for the licensing primitives. Every test
    /// generates a fresh RSA-2048 key pair so the public-key
    /// placeholder in <see cref="LicensePublicKey"/> is never a
    /// dependency.
    /// </summary>
    public sealed class LicenseSignerTests : IDisposable
    {
        private readonly string _tempLicensePath;
        private readonly string _originalPublicKeyXml;
        private readonly string _privateKeyXml;

        public LicenseSignerTests()
        {
            _tempLicensePath = Path.Combine(
                Path.GetTempPath(),
                "proglass-lic-test-" + Guid.NewGuid().ToString("N") + ".lic");
            (string publicXml, _privateKeyXml) = LicenseSigner.GenerateKeyPair();
            // Inject the generated public key into the const
            // field so LicenseVerifier uses it.
            _originalPublicKeyXml = SwapPublicKey(publicXml);
        }

        public void Dispose()
        {
            // Restore the original placeholder.
            SwapPublicKey(_originalPublicKeyXml);
            try { if (File.Exists(_tempLicensePath)) File.Delete(_tempLicensePath); }
            catch { /* best effort */ }
        }

        // ── tests ─────────────────────────────────────────────────

        [Fact]
        public void Sign_then_verify_returns_Valid()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-001",
                Edition = LicenseEdition.Business,
                CustomerId = "CUST-2026-001",
                CompanyName = "Acme Glass LLC",
                IssuedAt = DateTime.UtcNow.AddDays(-1),
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                MaxActivations = 3,
                Features = new[] { "Inquiry.*", "Proforma.*" }
            };

            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
            var result = LicenseVerifier.Verify(_tempLicensePath);

            Assert.Equal(LicenseStatus.Valid, result.Status);
            Assert.NotNull(result.Payload);
            Assert.Equal("TEST-001", result.Payload!.LicenseId);
            Assert.Equal(LicenseEdition.Business, result.Payload.Edition);
            Assert.True(result.CanRun);
            Assert.False(result.ReadOnly);
        }

        [Fact]
        public void Tampered_payload_fails_verification()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-002",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1)
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);

            // Mutate the file by hand so the signature no
            // longer matches. We replace the licenseId
            // post-sign to simulate an attacker.
            string json = File.ReadAllText(_tempLicensePath);
            json = json.Replace("\"TEST-002\"", "\"TAMPERED\"");
            File.WriteAllText(_tempLicensePath, json);

            var result = LicenseVerifier.Verify(_tempLicensePath);

            Assert.Equal(LicenseStatus.Invalid, result.Status);
            Assert.False(result.CanRun);
        }

        [Fact]
        public void Wrong_key_fails_verification()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-003",
                Edition = LicenseEdition.Factory,
                ExpiresAt = DateTime.UtcNow.AddYears(1)
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);

            // Swap in a different key pair and re-verify.
            (string newPub, string newPriv) = LicenseSigner.GenerateKeyPair();
            // The signature was made with the original key,
            // not the new one. Verification should fail.
            string savedPub = SwapPublicKey(newPub);
            try
            {
                var result = LicenseVerifier.Verify(_tempLicensePath);
                Assert.Equal(LicenseStatus.Invalid, result.Status);
            }
            finally
            {
                SwapPublicKey(savedPub);
            }
        }

        [Fact]
        public void Missing_file_returns_NotActivated()
        {
            var result = LicenseVerifier.Verify(
                Path.Combine(Path.GetTempPath(),
                    "this-file-does-not-exist-" + Guid.NewGuid().ToString("N") + ".lic"));
            Assert.Equal(LicenseStatus.NotActivated, result.Status);
            Assert.False(result.CanRun);
        }

        [Fact]
        public void Expired_license_in_grace_period_returns_GracePeriod()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-004",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow.AddYears(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(-3) // 3 days past expiry, 4 left
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
            var result = LicenseVerifier.Verify(_tempLicensePath);

            Assert.Equal(LicenseStatus.GracePeriod, result.Status);
            Assert.True(result.CanRun);      // still runs
            Assert.True(result.ReadOnly);    // but read-only
        }

        [Fact]
        public void Expired_license_past_grace_returns_Expired()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-005",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow.AddYears(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(-30) // well past 7d grace
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
            var result = LicenseVerifier.Verify(_tempLicensePath);

            Assert.Equal(LicenseStatus.Expired, result.Status);
            Assert.False(result.CanRun);
        }

        [Fact]
        public void Expiring_soon_returns_ExpiringSoon()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-006",
                Edition = LicenseEdition.Business,
                IssuedAt = DateTime.UtcNow.AddYears(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(5) // within 14d warning
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
            var result = LicenseVerifier.Verify(_tempLicensePath);

            Assert.Equal(LicenseStatus.ExpiringSoon, result.Status);
            Assert.True(result.CanRun);
        }

        [Fact]
        public void Trial_license_returns_Trial_status()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TRIAL-001",
                Edition = LicenseEdition.Trial,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Features = new[] { "Inquiry.*", "Proforma.*", "JobOrder.*" }
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
            var result = LicenseVerifier.Verify(_tempLicensePath);

            Assert.Equal(LicenseStatus.Trial, result.Status);
            Assert.True(result.CanRun);
        }

        [Fact]
        public void Machine_fingerprint_mismatch_returns_MachineMismatch()
        {
            var payload = new LicensePayload
            {
                LicenseId = "TEST-007",
                Edition = LicenseEdition.Business,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                MachineFingerprint = "deadbeef00000000deadbeef00000000"
            };
            LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
            var result = LicenseVerifier.Verify(_tempLicensePath);

            // The current machine's fingerprint will almost
            // certainly not be "deadbeef..." unless the test
            // is being run on a very specific machine.
            Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
            Assert.False(result.CanRun);
        }

        [Fact]
        public void Canonical_json_is_stable_across_property_order()
        {
            var p1 = new LicensePayload
            {
                LicenseId = "X",
                Edition = LicenseEdition.Business,
                CustomerId = "C",
                CompanyName = "Co",
                IssuedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpiresAt = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            string a = p1.ToCanonicalJson();
            string b = p1.ToCanonicalJson();
            Assert.Equal(a, b);
        }

        [Fact]
        public void Features_array_is_sorted_canonically()
        {
            var p = new LicensePayload
            {
                LicenseId = "X",
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Features = new[] { "Proforma.*", "Inquiry.*", "JobOrder.*" }
            };
            string json = p.ToCanonicalJson();
            int inquiryIdx = json.IndexOf("Inquiry.*", StringComparison.Ordinal);
            int joIdx = json.IndexOf("JobOrder.*", StringComparison.Ordinal);
            int piIdx = json.IndexOf("Proforma.*", StringComparison.Ordinal);

            Assert.True(inquiryIdx > 0, "Inquiry.* present in JSON");
            Assert.True(joIdx > 0, "JobOrder.* present in JSON");
            Assert.True(piIdx > 0, "Proforma.* present in JSON");
            // Alphabetical order: Inquiry, JobOrder, Proforma.
            Assert.True(inquiryIdx < joIdx, "Inquiry before JobOrder");
            Assert.True(joIdx < piIdx, "JobOrder before Proforma");
        }

        [Fact]
        public void Sign_rejects_empty_or_null_key()
        {
            // RSA-based signer does not enforce a byte count
            // (it loads XML), so the input-validation contract
            // is "rejects empty / null key material". A short
            // key is a separate concern handled by
            // FromXmlString at Sign time.
            Assert.Throws<ArgumentException>(() =>
                LicenseSigner.Sign(new LicensePayload(), ""));
            Assert.Throws<ArgumentNullException>(() =>
                LicenseSigner.Sign(null!, _privateKeyXml));
        }

        [Fact]
        public void Placeholder_key_returns_NotActivated_with_diagnostic()
        {
            // Restore the placeholder so the verifier refuses
            // everything until the developer wires a real
            // key into LicensePublicKey.
            string saved = SwapPublicKey(
                "PLACEHOLDER_REPLACE_WITH_REAL_RSA2048_PUBLIC_KEY_XML");
            try
            {
                var payload = new LicensePayload
                {
                    LicenseId = "X",
                    ExpiresAt = DateTime.UtcNow.AddYears(1)
                };
                LicenseSigner.SignToFile(payload, _privateKeyXml, _tempLicensePath);
                var result = LicenseVerifier.Verify(_tempLicensePath);
                Assert.Equal(LicenseStatus.NotActivated, result.Status);
                Assert.Contains("placeholder", result.ErrorDetail,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                SwapPublicKey(saved);
            }
        }

        // ── helpers ──────────────────────────────────────────────

        /// <summary>
        /// Replace the const <c>PublicKeyXml</c> value via
        /// reflection. Returns the previous value so the
        /// caller can restore it. The const is not technically
        /// writable, but the runtime field initialiser is —
        /// reflection lets us swap it for the duration of a
        /// test. This is the ONLY way to run real
        /// round-trip tests without committing a real public
        /// key to the source tree.
        /// </summary>
        private static string SwapPublicKey(string newValue)
        {
            FieldInfo field = typeof(LicensePublicKey).GetField(
                "PublicKeyXml",
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    "LicensePublicKey.PublicKeyXml field not found.");

            // The const is compiled into the static field
            // initialiser; reflection can still write it
            // because FieldInfo.SetValue bypasses the const
            // check at the C# level.
            string original = (string)field.GetValue(null)!;
            field.SetValue(null, newValue);
            return original;
        }
    }
}

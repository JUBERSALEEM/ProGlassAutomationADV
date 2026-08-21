// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/UpdateSignatureVerifierTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 10 — OTA Authenticode signature verification
//
// Covers the testable surface of UpdateSignatureVerifier:
//   • Verify fails cleanly for missing/unsigned files (no exceptions).
//   • HasCodeSigningEku accepts only Code Signing certificates.
//   • IsWithinValidityPeriod enforces NotBefore/NotAfter.
//   • MatchesPinnedPublisher enforces thumbprint + subject pins, and
//     accepts any publisher when no pins are configured.
//
// The WinVerifyTrust P/Invoke path itself is exercised at runtime on
// Windows (it requires a real Authenticode-signed binary), so it is
// deliberately not faked here.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ProGlassAutomation.Services.Updates;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class UpdateSignatureVerifierTests
    {
        // ── Verify — failure modes must never throw ─────────────────────

        [Fact]
        public void Verify_empty_path_fails_cleanly()
        {
            var result = UpdateSignatureVerifier.Verify("");
            Assert.False(result.IsValid);
            Assert.Contains("No installer path", result.Reason);
        }

        [Fact]
        public void Verify_missing_file_fails_cleanly()
        {
            var path = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");

            var result = UpdateSignatureVerifier.Verify(path);

            Assert.False(result.IsValid);
            Assert.Contains("does not exist", result.Reason);
        }

        [Fact]
        public void Verify_unsigned_file_fails_cleanly()
        {
            // "MZ" header but no Authenticode signature → must be refused.
            var path = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

            try
            {
                var result = UpdateSignatureVerifier.Verify(path);
                Assert.False(result.IsValid);
                Assert.NotEmpty(result.Reason);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ── Certificate predicate: Code Signing EKU ─────────────────────

        [Fact]
        public void HasCodeSigningEku_accepts_code_signing_certificate()
        {
            using var cert = CreateSelfSigned(
                "CN=ProGlass Signer", codeSigningEku: true);

            Assert.True(UpdateSignatureVerifier.HasCodeSigningEku(cert));
        }

        [Fact]
        public void HasCodeSigningEku_rejects_certificate_without_eku()
        {
            using var cert = CreateSelfSigned(
                "CN=ProGlass Signer", codeSigningEku: false);

            Assert.False(UpdateSignatureVerifier.HasCodeSigningEku(cert));
        }

        [Fact]
        public void HasCodeSigningEku_rejects_null()
        {
            Assert.False(UpdateSignatureVerifier.HasCodeSigningEku(null!));
        }

        // ── Certificate predicate: validity window ──────────────────────

        [Fact]
        public void IsWithinValidityPeriod_enforces_not_before_and_not_after()
        {
            var now = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
            using var cert = CreateSelfSigned(
                "CN=ProGlass Signer",
                codeSigningEku: true,
                notBefore: now.AddDays(-1),
                notAfter: now.AddDays(30));

            Assert.True(UpdateSignatureVerifier.IsWithinValidityPeriod(cert, now));
            Assert.False(UpdateSignatureVerifier.IsWithinValidityPeriod(cert, now.AddDays(-2)));
            Assert.False(UpdateSignatureVerifier.IsWithinValidityPeriod(cert, now.AddDays(31)));
        }

        // ── Publisher pinning ───────────────────────────────────────────

        [Fact]
        public void MatchesPinnedPublisher_accepts_any_when_no_pins()
        {
            Assert.True(UpdateSignatureVerifier.MatchesPinnedPublisher(
                "CN=Anyone", "AABBCCDD", null, null));
            Assert.True(UpdateSignatureVerifier.MatchesPinnedPublisher(
                "CN=Anyone", "AABBCCDD", Array.Empty<string>(), Array.Empty<string>()));
        }

        [Fact]
        public void MatchesPinnedPublisher_enforces_thumbprint_pin()
        {
            Assert.True(UpdateSignatureVerifier.MatchesPinnedPublisher(
                "CN=X", "AA BB CC DD", null, new[] { "aabbccdd" }));
            Assert.False(UpdateSignatureVerifier.MatchesPinnedPublisher(
                "CN=X", "AA BB CC DD", null, new[] { "deadbeef" }));
        }

        [Fact]
        public void MatchesPinnedPublisher_enforces_subject_pin()
        {
            Assert.True(UpdateSignatureVerifier.MatchesPinnedPublisher(
                "CN=Juber Saleem", "AA", new[] { "CN=Juber Saleem" }, null));
            Assert.False(UpdateSignatureVerifier.MatchesPinnedPublisher(
                "CN=Other", "AA", new[] { "CN=Juber Saleem" }, null));
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static X509Certificate2 CreateSelfSigned(
            string subject,
            bool codeSigningEku,
            DateTime? notBefore = null,
            DateTime? notAfter = null)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                subject, rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (codeSigningEku)
            {
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection
                        {
                            new(UpdateSignatureVerifier.CodeSigningEkuOid)
                        },
                        critical: false));
            }

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: false,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: false));

            return request.CreateSelfSigned(
                notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
                notAfter ?? DateTimeOffset.UtcNow.AddDays(30));
        }
    }
}

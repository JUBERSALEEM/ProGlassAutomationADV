// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/UpdateOriginSecurityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Covers audit finding C-1 (CRITICAL): the OTA updater downloaded the
// release asset from whatever URL appeared in the GitHub API JSON and
// then handed it to Process.Start. Nothing verified the origin, so a
// tampered API response, a hostile proxy or DNS interception could make
// the application execute an arbitrary binary as the logged-in user.
//
// UpdateService.IsAllowedDownloadUrl is the gate that now stands in
// front of both the download and the launch. These tests pin its
// behaviour, with particular attention to the host-spoofing patterns
// that a naive implementation gets wrong:
//
//   EndsWith(".github.com")  accepts  evil-github.com
//   Contains("github.com")   accepts  attacker.net/github.com/x
//   StartsWith("https://github.com") accepts github.com.attacker.net
//
// The implementation uses an exact host match specifically to defeat
// these. If someone later "simplifies" it to a Contains or EndsWith,
// these tests fail immediately.
//
// NOTE ON SCOPE: this is an ORIGIN check, not an authenticity check. It
// cannot detect a malicious file legitimately hosted on GitHub. Real
// authenticity requires Authenticode signature verification, which is
// what the Velopack path provides. That limitation is documented in
// Docs/SecurityAudit.md and in the method's own remarks.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Updates;
using System.Text.Json;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class UpdateOriginSecurityTests
    {
        // ── Legitimate release URLs must still work ───────────────────
        // A security control that blocks the real update flow is not a
        // control, it is an outage. These prove the gate is not simply
        // "return false".

        [Theory]
        [InlineData("https://github.com/JUBERSALEEM/ProGlassAutomationADV/releases/download/v2.0.1/Setup.exe")]
        [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1/2")]
        [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/9")]
        [InlineData("https://api.github.com/repos/JUBERSALEEM/ProGlassAutomationADV/releases/assets/1")]
        [InlineData("https://www.github.com/JUBERSALEEM/ProGlassAutomationADV/releases/download/v1/a.zip")]
        public void Accepts_genuine_github_release_urls(string url)
        {
            Assert.True(UpdateService.IsAllowedDownloadUrl(url));
        }

        /// <summary>Host comparison must be case-insensitive.</summary>
        [Theory]
        [InlineData("https://GITHUB.COM/o/r/releases/download/v1/Setup.exe")]
        [InlineData("https://GitHub.Com/o/r/releases/download/v1/Setup.exe")]
        public void Accepts_regardless_of_host_casing(string url)
        {
            Assert.True(UpdateService.IsAllowedDownloadUrl(url));
        }

        // ── Transport downgrade ───────────────────────────────────────

        /// <summary>
        /// An executable fetched over plaintext HTTP can be swapped in
        /// transit by anyone on the network path. Never allowed, even
        /// from the correct host.
        /// </summary>
        [Theory]
        [InlineData("http://github.com/o/r/releases/download/v1/Setup.exe")]
        [InlineData("http://objects.githubusercontent.com/a/b")]
        public void Rejects_plaintext_http(string url)
        {
            Assert.False(UpdateService.IsAllowedDownloadUrl(url));
        }

        // ── Host spoofing — the cases naive checks get wrong ──────────

        [Theory]
        [InlineData("https://evil-github.com/Setup.exe")]            // EndsWith trap
        [InlineData("https://notgithub.com/Setup.exe")]
        [InlineData("https://github.com.attacker.net/Setup.exe")]    // suffix spoof
        [InlineData("https://attacker.net/github.com/Setup.exe")]    // path spoof
        [InlineData("https://githubusercontent.com.evil.net/x")]
        [InlineData("https://raw.githubusercontent.com/o/r/Setup.exe")] // wrong GH host
        [InlineData("https://gist.githubusercontent.com/x/Setup.exe")]
        public void Rejects_spoofed_and_unexpected_hosts(string url)
        {
            Assert.False(UpdateService.IsAllowedDownloadUrl(url));
        }

        /// <summary>
        /// Credentials in the authority section are a classic way to make
        /// a URL *look* like it points at a trusted host:
        /// https://github.com@attacker.net/x  has host attacker.net.
        /// Uri parses this correctly; the test pins that we rely on the
        /// parsed Host and never on string inspection.
        /// </summary>
        [Fact]
        public void Rejects_userinfo_authority_spoof()
        {
            Assert.False(UpdateService.IsAllowedDownloadUrl(
                "https://github.com@attacker.net/Setup.exe"));
        }

        // ── Non-http schemes and malformed input ──────────────────────

        [Theory]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData(@"\\attacker\share\Setup.exe")]
        [InlineData("ftp://github.com/Setup.exe")]
        [InlineData("javascript:alert(1)")]
        public void Rejects_non_https_schemes(string url)
        {
            Assert.False(UpdateService.IsAllowedDownloadUrl(url));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a url")]
        [InlineData("//github.com/Setup.exe")]   // scheme-relative
        [InlineData("/releases/download/Setup.exe")]
        public void Rejects_empty_and_malformed(string? url)
        {
            Assert.False(UpdateService.IsAllowedDownloadUrl(url));
        }
    }

    /// <summary>
    /// Pins the update download completeness gate (U-2 style). A dropped
    /// connection must be detected as truncated and never reported as
    /// success. Exposed as the pure predicate UpdateService.IsDownloadComplete
    /// so it is testable without any network I/O.
    /// </summary>
    public class UpdateDownloadCompletenessTests
    {
        [Theory]
        [InlineData(1000L, 1000L, true)]   // exact match = complete
        [InlineData(1000L, 0L, false)]     // nothing copied
        [InlineData(1000L, 999L, false)]   // one byte short = truncated
        [InlineData(1000L, 1001L, false)]  // overshoot (server lied / chunk overlap)
        [InlineData(null, 0L, true)]       // no declared length = cannot verify
        [InlineData(null, 12345L, true)]
        [InlineData(0L, 0L, true)]         // zero-length payload declared and received
        public void IsDownloadComplete_reports_truncation(
            long? total, long copied, bool expected)
        {
            Assert.Equal(expected, UpdateService.IsDownloadComplete(total, copied));
        }

        [Fact]
        public void IsDownloadComplete_rejects_negative_copied()
        {
            Assert.False(UpdateService.IsDownloadComplete(1000, -1));
        }
    }

    public class UpdateReleaseAssetSelectionTests
    {
        [Fact]
        public void Prefers_tag_specific_single_file_app_over_velopack_packages()
        {
            using var document = JsonDocument.Parse(
                """{"assets":[{"name":"ProGlassAutomation-Setup.exe"},{"name":"ProGlassAutomation-win-full.nupkg"},{"name":"ProGlassAutomation-v2.0.12-win-x64.exe"}]}""");

            int index = UpdateService.FindPreferredReleaseAssetIndex(
                document.RootElement.GetProperty("assets"), "v2.0.12");

            Assert.Equal(2, index);
        }

        [Fact]
        public void Never_selects_license_generator_or_arbitrary_first_asset()
        {
            using var document = JsonDocument.Parse(
                """{"assets":[{"name":"ProGlassAutomation.LicenseGenerator.exe"},{"name":"ProGlassAutomation-win-full.nupkg"},{"name":"SHA256SUMS.txt"}]}""");

            int index = UpdateService.FindPreferredReleaseAssetIndex(
                document.RootElement.GetProperty("assets"), "v2.0.12");

            Assert.Equal(-1, index);
        }

        [Fact]
        public void Supports_existing_setup_asset_for_legacy_releases()
        {
            using var document = JsonDocument.Parse(
                """{"assets":[{"name":"ProGlassAutomation-win-full.nupkg"},{"name":"ProGlassAutomation-Setup.exe"}]}""");

            int index = UpdateService.FindPreferredReleaseAssetIndex(
                document.RootElement.GetProperty("assets"), "v2.0.12");

            Assert.Equal(1, index);
        }
    }

}

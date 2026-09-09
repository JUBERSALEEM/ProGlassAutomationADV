// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/SharingSettingsTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins per-document rule resolution: known keys return their rule
// (case-insensitive), unknown keys get a safe default (PDF) instead
// of null.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class SharingSettingsTests
    {
        [Theory]
        [InlineData("PI", "Pdf", "WHATSAPP", "CustomerAttention")]
        [InlineData("TI", "Pdf", "EMAIL", "CustomerAttention")]
        [InlineData("Report", "Excel", "EMAIL", "Customer")]
        [InlineData("pi", "Pdf", "WHATSAPP", "CustomerAttention")]
        public void RuleFor_returns_configured_rule(
            string key, string format, string provider, string recipient)
        {
            var settings = new SharingSettings();

            ShareDocumentRule rule = settings.RuleFor(key);

            Assert.Equal(format, rule.DefaultFormat);
            Assert.Equal(provider, rule.DefaultProviderId);
            Assert.Equal(recipient, rule.DefaultRecipientKind);
        }

        [Theory]
        [InlineData("SomethingNew")]
        [InlineData("")]
        [InlineData(null)]
        public void RuleFor_returns_safe_default_for_unknown_keys(string? key)
        {
            var settings = new SharingSettings();

            ShareDocumentRule rule = settings.RuleFor(key);

            Assert.NotNull(rule);
            Assert.Equal("Pdf", rule.DefaultFormat);
        }

        [Fact]
        public void RuleFor_never_returns_null_or_throws()
        {
            var settings = new SharingSettings
            {
                DocumentRules = new System.Collections.Generic.List<ShareDocumentRule>()
            };

            ShareDocumentRule rule = settings.RuleFor("PI");

            Assert.NotNull(rule);
            Assert.Equal("Pdf", rule.DefaultFormat);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/ShareTemplateServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the Sharing Center template engine: override precedence
// (doc+provider → doc → provider → GLOBAL → built-in doc → built-in
// global), variable substitution, attention fallback, and the rule
// that unknown {tokens} survive verbatim.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing;
using ProGlassAutomation.Services.Sharing.Templates;
using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class ShareTemplateServiceTests
    {
        private static ShareContext PiContext() => new()
        {
            DocumentTypeKey = "PI",
            DocumentTypeDisplay = "Proforma Invoice",
            DocumentNumber = "PI-2026-001",
            CustomerName = "Skyline Interiors LLC",
            AttentionName = "Mr. Ahmed",
            CompanyName = "ProGlass LLC",
            PreparedByName = "Juber",
            Amount = 12345.60m,
            Currency = "AED",
            DocumentDate = new System.DateTime(2026, 9, 1)
        };

        [Fact]
        public void Resolve_prefers_doc_plus_provider_override()
        {
            var overrides = new Dictionary<string, string>
            {
                ["GLOBAL"] = "global",
                ["WHATSAPP"] = "provider",
                ["PI"] = "doc",
                ["PI+WHATSAPP"] = "both"
            };

            Assert.Equal("both", ShareTemplateService.ResolveTemplateText(
                "PI", "WHATSAPP", overrides));
        }

        [Fact]
        public void Resolve_falls_back_doc_then_provider_then_global()
        {
            var overrides = new Dictionary<string, string>
            {
                ["GLOBAL"] = "global",
                ["WHATSAPP"] = "provider",
                ["PI"] = "doc"
            };

            Assert.Equal("doc", ShareTemplateService.ResolveTemplateText(
                "PI", "EMAIL", overrides));
            Assert.Equal("provider", ShareTemplateService.ResolveTemplateText(
                "JO", "WHATSAPP", overrides));
            Assert.Equal("global", ShareTemplateService.ResolveTemplateText(
                "JO", "EMAIL", overrides));
        }

        [Fact]
        public void Resolve_uses_builtin_doc_then_global_without_overrides()
        {
            string pi = ShareTemplateService.ResolveTemplateText(
                "PI", "WHATSAPP", null);

            Assert.Contains("Proforma Invoice", pi);
            Assert.Contains("{DocumentNumber}", pi);

            string report = ShareTemplateService.ResolveTemplateText(
                "Report", "EMAIL", null);

            Assert.Equal(ShareTemplateService.BuiltInGlobal, report);
        }

        [Fact]
        public void Render_substitutes_variables()
        {
            string text = ShareTemplateService.Render(
                "{CustomerAttentionName}|{DocumentType}|{DocumentNumber}|" +
                "{Currency} {Amount}|{Date}|{CompanyName}|{PreparedByName}",
                PiContext(),
                new ShareRecipient(
                    ShareRecipientKind.CustomerAttention, "Mr. Ahmed"),
                new ShareSenderInfo("Juber"));

            Assert.Equal(
                "Mr. Ahmed|Proforma Invoice|PI-2026-001|" +
                "AED 12,345.60|01-Sep-2026|ProGlass LLC|Juber",
                text);
        }

        [Fact]
        public void Render_keeps_unknown_tokens_verbatim()
        {
            string text = ShareTemplateService.Render(
                "Hello {TypoToken}, your {DocumentNumber} is ready.",
                PiContext(), null);

            Assert.Contains("{TypoToken}", text);
            Assert.Contains("PI-2026-001", text);
        }

        [Fact]
        public void Render_attention_falls_back_to_customer_name()
        {
            var context = PiContext();
            context.AttentionName = "";

            string text = ShareTemplateService.Render(
                "Dear {CustomerAttentionName},", context, null);

            Assert.Equal("Dear Skyline Interiors LLC,", text);
        }

        [Fact]
        public void Render_never_returns_null_or_throws()
        {
            Assert.Equal("", ShareTemplateService.Render(
                null, PiContext(), null));
            Assert.Equal("raw {", ShareTemplateService.Render(
                "raw {", PiContext(), null));
        }
    }
}

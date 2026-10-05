// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/PaymentTermsResolverTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins PaymentTermsResolver.ResolveDays (payment-terms label → due days).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.TaxInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class PaymentTermsResolverTests
    {
        [Theory]
        [InlineData("Immediate", 0)]
        [InlineData("Net 7", 7)]
        [InlineData("Net 15", 15)]
        [InlineData("Net 30", 30)]
        [InlineData("Net 45", 45)]
        [InlineData("Net 60", 60)]
        [InlineData("Net 90", 90)]
        public void Known_terms_map_to_due_days(string terms, int expected)
        {
            Assert.Equal(expected, PaymentTermsResolver.ResolveDays(terms));
        }

        [Theory]
        [InlineData("Custom")]
        [InlineData("")]
        [InlineData("Net 120")]
        [InlineData(null)]
        public void Unknown_or_custom_terms_default_to_30(string terms)
        {
            Assert.Equal(30, PaymentTermsResolver.ResolveDays(terms));
        }
    }
}

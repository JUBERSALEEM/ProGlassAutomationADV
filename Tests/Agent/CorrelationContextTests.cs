// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CorrelationContextTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 2 — CorrelationContext
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class CorrelationContextTests
    {
        [Fact]
        public void NewId_generates_CORR_prefix()
        {
            var id = CorrelationContext.NewId();
            Assert.StartsWith("CORR-", id);
            Assert.Equal(13, id.Length); // CORR- + 8 chars
        }

        [Fact]
        public void NewIdFor_includes_document_type()
        {
            var id = CorrelationContext.NewIdFor("ProformaInvoice", "PI-123");
            Assert.Contains("PROFORMAINVOICE", id.ToUpper());
            Assert.Contains("PI-123", id);
        }

        [Fact]
        public void BeginScope_sets_and_restores()
        {
            Assert.Null(CorrelationContext.CurrentId);

            var id1 = "CORR-TEST1";
            using (CorrelationContext.BeginScope(id1))
            {
                Assert.Equal(id1, CorrelationContext.CurrentId);

                var id2 = "CORR-TEST2";
                using (CorrelationContext.BeginScope(id2))
                {
                    Assert.Equal(id2, CorrelationContext.CurrentId);
                }

                // Restored to id1
                Assert.Equal(id1, CorrelationContext.CurrentId);
            }

            // Restored to null
            Assert.Null(CorrelationContext.CurrentId);
        }

        [Fact]
        public void BeginDocumentScope_sets_all_three()
        {
            var corr = CorrelationContext.NewIdFor("TaxInvoice", "INV-999");
            using var scope = CorrelationContext.BeginDocumentScope("TaxInvoice", "INV-999", corr);

            Assert.Equal(corr, CorrelationContext.CurrentId);
            Assert.Equal("INV-999", CorrelationContext.CurrentDocumentId);
            Assert.Equal("TaxInvoice", CorrelationContext.CurrentDocumentType);
        }

        [Fact]
        public void Nested_scopes_restore_correctly()
        {
            using var outer = CorrelationContext.BeginDocumentScope("ProformaInvoice", "PI-001", "CORR-OUTER");
            Assert.Equal("PI-001", CorrelationContext.CurrentDocumentId);

            using var inner = CorrelationContext.BeginDocumentScope("DeliveryNote", "DN-001", "CORR-INNER");
            Assert.Equal("DN-001", CorrelationContext.CurrentDocumentId);
            Assert.Equal("CORR-INNER", CorrelationContext.CurrentId);

            // inner disposed
            Assert.Equal("PI-001", CorrelationContext.CurrentDocumentId);
            Assert.Equal("CORR-OUTER", CorrelationContext.CurrentId);
        }
    }
}

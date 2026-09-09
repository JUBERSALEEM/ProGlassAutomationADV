// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/CreditNote/CreditNoteFactoryTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ERP hardening (Phase 4). A credit note is only ever created FROM the tax
// invoice it corrects. These tests pin that the factory copies the invoice's
// identity, party and (positive-magnitude) figures faithfully so the
// correction always ties back to a real fiscal document.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.CreditNote;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.CreditNote
{
    public class CreditNoteFactoryTests
    {
        private static SavedTaxInvoice SampleInvoice()
        {
            var inv = new SavedTaxInvoice
            {
                InvoiceNumber = "INV-2026-001",
                ProformaId = "PI-2026-001",
                JobOrderId = "JOB-2026-001",
                PurchaseOrderNumber = "PO-9",
                CustomerName = "Acme Glass",
                ProjectName = "Tower A",
                CompanyName = "ProGlass",
                CompanyTrn = "1000",
                Subtotal = 1000m,
                DiscountAmount = 100m,
                AfterDiscount = 900m,
                VatAmount = 45m,
                GrandTotal = 945m,
                TotalQty = 4,
                TotalSqm = 8m,
            };
            inv.Items.Add(new TaxInvoiceItem
            {
                InvoiceNumber = "INV-2026-001",
                ProformaId = "PI-2026-001",
                GlassRef = "G-1",
                Qty = 4,
                Sqm = 2m,
                TotalSqm = 8m,
                Rate = 125m,
                Value = 1000m,
            });
            return inv;
        }

        [Fact]
        public void Copies_identity_and_links_back_to_the_invoice()
        {
            var cn = CreditNoteFactory.FromTaxInvoice(SampleInvoice(), "Goods returned");

            Assert.Equal("INV-2026-001", cn.TaxInvoiceReference);
            Assert.Equal("PI-2026-001", cn.ProformaId);
            Assert.Equal("JOB-2026-001", cn.JobOrderId);
            Assert.Equal("Goods returned", cn.Reason);
            Assert.Equal("Draft", cn.Status);
            // Number is assigned by the repository on insert, not the factory.
            Assert.Equal("", cn.CreditNoteNumber);
        }

        [Fact]
        public void Copies_positive_magnitude_totals_and_lines()
        {
            var cn = CreditNoteFactory.FromTaxInvoice(SampleInvoice(), "Over-billed");

            Assert.Equal(1000m, cn.Subtotal);
            Assert.Equal(945m, cn.GrandTotal);
            Assert.True(cn.GrandTotal > 0, "totals are stored as positive magnitudes");
            Assert.Single(cn.Items);
            Assert.Equal("G-1", cn.Items[0].GlassRef);
            Assert.Equal(1000m, cn.Items[0].Value);
            Assert.Equal("INV-2026-001", cn.Items[0].TaxInvoiceReference);
        }

        [Fact]
        public void Defaults_issue_date_to_today_when_unspecified()
        {
            var cn = CreditNoteFactory.FromTaxInvoice(SampleInvoice(), "x");
            Assert.Equal(DateTime.Today, cn.IssueDate);
        }

        [Fact]
        public void Null_invoice_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => CreditNoteFactory.FromTaxInvoice(null!, "x"));
        }
    }
}

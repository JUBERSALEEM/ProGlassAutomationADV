// Pins TI invoiceable-qty rules. Source-level helpers — no SDK.

using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.TaxInvoice
{
    public sealed class TaxInvoiceEligibilityTests
    {
        [Theory]
        [InlineData(DeliveryNoteStatusValues.Dispatched, false, true)]
        [InlineData(DeliveryNoteStatusValues.Delivered, false, true)]
        [InlineData(DeliveryNoteStatusValues.Signed, false, true)]
        [InlineData(DeliveryNoteStatusValues.Received, false, true)]
        [InlineData(DeliveryNoteStatusValues.Draft, false, false)]
        [InlineData(DeliveryNoteStatusValues.Dispatching, false, false)]
        public void Confirmed_deliveries_are_invoiceable(
            string status, bool alreadyInvoiced, bool expected)
        {
            Assert.Equal(expected, TaxInvoiceLineService.CanInvoiceDeliveryNote(
                isMasterDn: false,
                status,
                alreadyInvoiced,
                existingTaxInvoiceId: alreadyInvoiced ? "INV-1" : null));
        }

        [Fact]
        public void Master_dn_is_never_invoiceable()
        {
            Assert.False(TaxInvoiceLineService.CanInvoiceDeliveryNote(
                isMasterDn: true,
                DeliveryNoteStatusValues.Dispatched,
                hasTaxInvoice: false,
                existingTaxInvoiceId: null));
        }

        [Fact]
        public void Already_invoiced_dn_is_blocked_on_create()
        {
            Assert.False(TaxInvoiceLineService.CanInvoiceDeliveryNote(
                isMasterDn: false,
                DeliveryNoteStatusValues.Dispatched,
                hasTaxInvoice: true,
                existingTaxInvoiceId: "INV-OLD"));
        }

        [Fact]
        public void Same_ti_may_re_save_its_own_dn()
        {
            Assert.True(TaxInvoiceLineService.CanInvoiceDeliveryNote(
                isMasterDn: false,
                DeliveryNoteStatusValues.Dispatched,
                hasTaxInvoice: true,
                existingTaxInvoiceId: "INV-1",
                thisInvoiceNumber: "INV-1"));
        }

        [Fact]
        public void Family_money_counts_overdue_as_already_invoiced()
        {
            var members = new[]
            {
                new SavedTaxInvoice { InvoiceNumber = "INV-A", Status = "Issued", GrandTotal = 30m },
                new SavedTaxInvoice { InvoiceNumber = "INV-B", Status = "Overdue", GrandTotal = 70m },
                new SavedTaxInvoice { InvoiceNumber = "INV-C", Status = "Draft", GrandTotal = 10m },
            };

            var (_, invoiced, balance) = TaxInvoiceLineService.CalculateFamily(
                members, proformaTotal: 100m);

            Assert.Equal(100m, invoiced);
            Assert.Equal(0m, balance);
        }
    }
}

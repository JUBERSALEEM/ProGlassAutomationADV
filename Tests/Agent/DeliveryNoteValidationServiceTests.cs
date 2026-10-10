// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DeliveryNoteValidationServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 6b + Phase 7 — DeliveryNoteValidationService
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DeliveryNoteValidationServiceTests
    {
        [Theory]
        [InlineData("Draft", "Dispatching", true)]
        [InlineData("Draft", "Delivered", false)]
        [InlineData("Dispatching", "Dispatched", true)]
        [InlineData("Dispatched", "Delivered", true)]
        [InlineData("Delivered", "Draft", false)]
        public void ValidateStatusTransition_checks_transition_table(string from, string to, bool shouldBeValid)
        {
            var result = DeliveryNoteValidationService.ValidateStatusTransition(from, to, "CORR-TEST");

            if (shouldBeValid)
                Assert.True(result.IsSuccess);
            else
                Assert.True(result.IsFailure);
        }

        [Fact]
        public void ValidateQuantities_fails_on_negative_delivered()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { GlassRef = "G-001", W1 = 1000, H1 = 1000, OrderedQty = 10, PreviouslyDeliveredQty = 0, DeliveredQty = -1, BalanceQty = 10 }),
            };

            var result = DeliveryNoteValidationService.ValidateQuantities(rows, "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("negative", result.Error.ToLower());
        }

        [Fact]
        public void ValidateQuantities_fails_when_exceeds_remaining()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { GlassRef = "G-001", W1 = 1000, H1 = 1000, OrderedQty = 10, PreviouslyDeliveredQty = 2, DeliveredQty = 9, BalanceQty = 0 }),
            };

            var result = DeliveryNoteValidationService.ValidateQuantities(rows, "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("exceeds", result.Error.ToLower());
        }

        [Fact]
        public void ValidateDispatch_fails_for_master_dn()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 5 }),
            };

            var result = DeliveryNoteValidationService.ValidateDispatch(rows, isMasterDn: true, "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("master", result.Error.ToLower());
        }

        [Fact]
        public void ValidateDispatch_fails_when_no_delivery()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 0 }),
            };

            var result = DeliveryNoteValidationService.ValidateDispatch(rows, isMasterDn: false, "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("no items", result.Error.ToLower());
        }

        [Fact]
        public void ValidateDispatch_succeeds_with_delivery()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 3 }),
            };

            var result = DeliveryNoteValidationService.ValidateDispatch(rows, isMasterDn: false, "CORR-TEST");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateTaxInvoiceConversion_fails_when_already_invoiced()
        {
            var entity = new SavedDeliveryNote
            {
                DeliveryNoteId = "DN-001",
                ProformaId = "PI-001",
                Status = "Dispatched",
                TotalDeliveredQty = 5,
                HasTaxInvoice = true,
                TaxInvoiceId = "INV-001",
                IsMasterDn = false
            };

            var result = DeliveryNoteValidationService.ValidateTaxInvoiceConversion(entity, null, "PI-001", "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("already invoiced", result.Error.ToLower());
        }

        [Fact]
        public void ValidateTaxInvoiceConversion_fails_on_invalid_status()
        {
            var entity = new SavedDeliveryNote
            {
                DeliveryNoteId = "DN-001",
                ProformaId = "PI-001",
                Status = "Draft",
                TotalDeliveredQty = 5,
                HasTaxInvoice = false,
                IsMasterDn = false
            };

            var result = DeliveryNoteValidationService.ValidateTaxInvoiceConversion(entity, null, "PI-001", "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("status", result.Error.ToLower());
        }

        [Fact]
        public void ValidateTaxInvoiceConversion_succeeds_on_valid()
        {
            var entity = new SavedDeliveryNote
            {
                DeliveryNoteId = "DN-001",
                ProformaId = "PI-001",
                Status = "Dispatched",
                TotalDeliveredQty = 5,
                HasTaxInvoice = false,
                IsMasterDn = false
            };

            var result = DeliveryNoteValidationService.ValidateTaxInvoiceConversion(entity, null, "PI-001", "CORR-TEST");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateSave_fails_for_master()
        {
            var entity = new SavedDeliveryNote { IsMasterDn = true };
            var rows = new[] { new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 1 }) };

            var result = DeliveryNoteValidationService.ValidateSave(entity, rows, "CORR-TEST");
            Assert.True(result.IsFailure);
        }
    }
}

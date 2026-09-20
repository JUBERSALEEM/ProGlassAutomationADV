// Pins remaining-qty validation. DnItemRowViewModel constructor does not
// clamp DeliveredQty, so the service must reject over-delivery itself.
// The previous BalanceQty+DeliveredQty guard never ran for BalanceQty >= 0.

using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.DeliveryNote
{
    public sealed class DeliveryNoteQuantityValidationTests
    {
        private static DnItemRowViewModel Row(
            int ordered, int prev, int delivered, int serial = 1) =>
            new(new DeliveryNoteItem
            {
                SpecGroupNumber = 1,
                SerialNumber = serial,
                GlassRef = "G-001",
                W1 = 1000,
                H1 = 1000,
                OrderedQty = ordered,
                PreviouslyDeliveredQty = prev,
                DeliveredQty = delivered,
                BalanceQty = 0
            });

        [Fact]
        public void Over_delivery_against_row_prev_is_rejected()
        {
            // Ordered 10, prev 2 → max 8. Constructor keeps DeliveredQty=9.
            var result = DeliveryNoteValidationService.ValidateQuantities(
                new[] { Row(10, 2, 9) }, "CORR-TEST");

            Assert.True(result.IsFailure);
            Assert.Contains("exceeds", result.Error.ToLowerInvariant());
        }

        [Fact]
        public void Exact_remaining_is_allowed()
        {
            var result = DeliveryNoteValidationService.ValidateQuantities(
                new[] { Row(10, 2, 8) }, "CORR-TEST");

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Negative_delivered_is_rejected()
        {
            var result = DeliveryNoteValidationService.ValidateQuantities(
                new[] { Row(10, 0, -1) }, "CORR-TEST");

            Assert.True(result.IsFailure);
            Assert.Contains("negative", result.Error.ToLowerInvariant());
        }

        [Fact]
        public void Live_confirmed_map_overrides_stale_row_prev()
        {
            // Row still thinks Prev=0 (draft opened before sibling dispatched).
            // Live map says 70 already confirmed → remaining 30, this DN 40 fails.
            var live = new Dictionary<string, int>
            {
                [DeliveryNoteWorkflow.LineKey(1, 1)] = 70
            };

            var result = DeliveryNoteValidationService.ValidateQuantities(
                new[] { Row(100, 0, 40) },
                "CORR-TEST",
                live);

            Assert.True(result.IsFailure);
            Assert.Contains("exceeds", result.Error.ToLowerInvariant());
        }

        [Fact]
        public void Live_confirmed_map_allows_true_remaining()
        {
            var live = new Dictionary<string, int>
            {
                [DeliveryNoteWorkflow.LineKey(1, 1)] = 70
            };

            var result = DeliveryNoteValidationService.ValidateQuantities(
                new[] { Row(100, 0, 30) },
                "CORR-TEST",
                live);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Live_map_missing_key_is_zero_not_stale_row_prev()
        {
            // Stale row Prev=90 would have capped this at 10. Live map has
            // no sibling qty for the line → remaining is the full order.
            var result = DeliveryNoteValidationService.ValidateQuantities(
                new[] { Row(100, 90, 40) },
                "CORR-TEST",
                new Dictionary<string, int>());

            Assert.True(result.IsSuccess);
        }
    }
}

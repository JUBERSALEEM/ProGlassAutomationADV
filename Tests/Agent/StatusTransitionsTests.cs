// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/StatusTransitionsTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the centralized workflow status-transition rules
// (Services/Workflow/StatusTransitions). Valid transitions must be allowed,
// invalid/impossible ones must be rejected, and re-saving the same status
// must be idempotent. Case-insensitive.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Workflow;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class StatusTransitionsTests
    {
        // ── Proforma Invoice ─────────────────────────────────────────────

        [Theory]
        [InlineData("Draft", "Sent")]
        [InlineData("Draft", "Approved")]
        [InlineData("Sent", "Approved")]
        [InlineData("Approved", "Rejected")]
        [InlineData("Rejected", "Draft")]   // reopening is an allowed edge
        public void Proforma_valid_transition(string from, string to)
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.ProformaInvoice, from, to);
            Assert.True(r.IsValid, r.Reason);
        }

        [Theory]
        [InlineData("Approved", "Expired")]
        [InlineData("", "Approved")]          // unknown source
        [InlineData("Sent", "NonExistent")]   // impossible target
        public void Proforma_invalid_transition(string from, string to)
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.ProformaInvoice, from, to);
            Assert.False(r.IsValid);
        }

        // ── Job Order ────────────────────────────────────────────────────

        [Theory]
        [InlineData("Draft", "Released")]
        [InlineData("Released", "InProduction")]
        [InlineData("InProduction", "Completed")]
        [InlineData("Completed", "Delivered")]
        [InlineData("Delivered", "Rejected")]
        public void JobOrder_valid_transition(string from, string to)
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, from, to);
            Assert.True(r.IsValid, r.Reason);
        }

        [Fact]
        public void JobOrder_draft_can_move_to_delivered()
        {
            // The app's workflow permits a direct Draft -> Delivered jump
            // (the transition table is COMPLETE for every transition the app
            // can legitimately reach), so this is valid — not a skip error.
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, "Draft", "Delivered");
            Assert.True(r.IsValid, r.Reason);
        }

        [Fact]
        public void JobOrder_rejects_non_joborder_status()
        {
            // "Received" is not a Job Order status -> impossible target.
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, "Draft", "Received");
            Assert.False(r.IsValid);
        }

        // ── Delivery Note ────────────────────────────────────────────────

        [Theory]
        [InlineData("Draft", "Dispatched")]
        [InlineData("Dispatched", "Delivered")]
        [InlineData("Delivered", "Signed")]
        [InlineData("Signed", "Received")]
        public void DeliveryNote_valid_transition(string from, string to)
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.DeliveryNote, from, to);
            Assert.True(r.IsValid, r.Reason);
        }

        [Fact]
        public void DeliveryNote_draft_can_move_to_received()
        {
            // As with Job Orders, a direct Draft -> Received jump is a
            // legitimate path the app can reach, so it is valid.
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.DeliveryNote, "Draft", "Received");
            Assert.True(r.IsValid, r.Reason);
        }

        [Fact]
        public void DeliveryNote_rejects_non_deliverynote_status()
        {
            // "Completed" is not a Delivery Note status -> impossible target.
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.DeliveryNote, "Draft", "Completed");
            Assert.False(r.IsValid);
        }

        // ── Tax Invoice ──────────────────────────────────────────────────

        [Theory]
        [InlineData("Draft", "Issued")]
        [InlineData("Issued", "Paid")]
        [InlineData("Issued", "PartiallyPaid")]
        [InlineData("PartiallyPaid", "Paid")]
        public void TaxInvoice_valid_transition(string from, string to)
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.TaxInvoice, from, to);
            Assert.True(r.IsValid, r.Reason);
        }

        // ── Status editor choices ────────────────────────────────────────

        [Fact]
        public void Status_choices_match_the_same_transition_table()
        {
            var choices = StatusTransitions.GetAllowedTargets(
                StatusTransitions.Kind.DeliveryNote, "Draft");

            Assert.Contains("Draft", choices);
            Assert.Contains("Dispatched", choices);
            Assert.Contains("Received", choices);
            Assert.DoesNotContain("Completed", choices);
        }

        [Fact]
        public void Unknown_legacy_status_is_preserved_as_the_only_choice()
        {
            var choices = StatusTransitions.GetAllowedTargets(
                StatusTransitions.Kind.JobOrder, "LegacyState");

            Assert.Equal(new[] { "LegacyState" }, choices);
        }

        // ── Idempotency ──────────────────────────────────────────────────

        [Fact]
        public void Same_status_is_idempotent()
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, "InProduction", "InProduction");
            Assert.True(r.IsValid);
        }

        [Fact]
        public void Empty_target_is_rejected()
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, "Draft", "   ");
            Assert.False(r.IsValid);
        }
    }
}

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
        public void JobOrder_cannot_skip_to_delivered_from_draft()
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, "Draft", "Delivered");
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
        public void DeliveryNote_cannot_receive_without_dispatch()
        {
            var r = StatusTransitions.Validate(
                StatusTransitions.Kind.DeliveryNote, "Draft", "Received");
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

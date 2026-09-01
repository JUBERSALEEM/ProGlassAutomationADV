// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Workflow/StatusServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Phase 4 (audit item 4.3): pins the IStatusService contract — the
// enforcement facade every user-initiated status change now flows through.
//
//   1. Valid transitions are recorded in the audit ring (newest first).
//   2. Illegal transitions are REJECTED and NEVER recorded.
//   3. The Transitioned event fires only for recorded transitions.
//   4. Case-insensitivity is preserved from the underlying table.
//   5. Explicit actor/note are captured; default actor is never blank.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Workflow;
using System;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Workflow
{
    public class StatusServiceTests
    {
        private static StatusService New() => new();

        [Fact]
        public void Valid_transition_is_recorded_and_returns_ok()
        {
            var svc = New();
            var r = svc.Transition(
                StatusTransitions.Kind.DeliveryNote,
                "Signed", "Received", actor: "tester");

            Assert.True(r.IsValid, r.Reason);
            var entry = Assert.Single(svc.Recent);
            Assert.Equal(StatusTransitions.Kind.DeliveryNote, entry.Kind);
            Assert.Equal("Signed", entry.From);
            Assert.Equal("Received", entry.To);
            Assert.Equal("tester", entry.Actor);
        }

        [Fact]
        public void Illegal_transition_is_rejected_and_never_recorded()
        {
            var svc = New();
            var r = svc.Transition(
                StatusTransitions.Kind.DeliveryNote,
                "Signed", "Draft");               // Signed -> Draft is illegal

            Assert.False(r.IsValid);
            Assert.NotEmpty(r.Reason ?? "");
            Assert.Empty(svc.Recent);
        }

        [Fact]
        public void Idempotent_rewrite_is_recorded_as_transition()
        {
            var svc = New();
            var r = svc.Transition(
                StatusTransitions.Kind.JobOrder,
                "InProduction", "InProduction");

            Assert.True(r.IsValid, r.Reason);
            Assert.Single(svc.Recent);
        }

        [Fact]
        public void Recent_is_newest_first()
        {
            var svc = New();
            svc.Transition(StatusTransitions.Kind.JobOrder, "Draft", "Released");
            svc.Transition(StatusTransitions.Kind.JobOrder, "Released", "InProduction");
            svc.Transition(StatusTransitions.Kind.JobOrder, "InProduction", "Completed");

            Assert.Equal(3, svc.Recent.Count);
            Assert.Equal("Completed", svc.Recent[0].To);
            Assert.Equal("InProduction", svc.Recent[1].To);
            Assert.Equal("Released", svc.Recent[2].To);
        }

        [Fact]
        public void Transitioned_event_fires_only_for_recorded_transitions()
        {
            var svc = New();
            int fired = 0;
            svc.Transitioned += _ => fired++;

            svc.Transition(StatusTransitions.Kind.TaxInvoice, "Draft", "Issued");
            svc.Transition(StatusTransitions.Kind.TaxInvoice, "Paid", "Void");  // illegal

            Assert.Equal(1, fired);
        }

        [Fact]
        public void Validation_is_case_insensitive()
        {
            var svc = New();
            Assert.True(svc.Validate(
                StatusTransitions.Kind.ProformaInvoice, "draft", "SENT").IsValid);
            Assert.True(svc.Validate(
                StatusTransitions.Kind.ProformaInvoice, "DRAFT", "sent").IsValid);
        }

        [Fact]
        public void Default_actor_is_never_blank()
        {
            var svc = New();
            svc.Transition(StatusTransitions.Kind.ProformaInvoice, "Draft", "Sent");
            var entry = Assert.Single(svc.Recent);
            Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
        }

        [Fact]
        public void Note_is_captured_when_provided()
        {
            var svc = New();
            svc.Transition(StatusTransitions.Kind.ProformaInvoice,
                "Sent", "Approved", actor: "manager", note: "LPO received");

            var entry = Assert.Single(svc.Recent);
            Assert.Equal("LPO received", entry.Note);
        }

        [Fact]
        public void Unknown_kind_is_rejected()
        {
            var svc = New();
            var r = svc.Transition((StatusTransitions.Kind)999,
                "Draft", "Sent");
            Assert.False(r.IsValid);
            Assert.Empty(svc.Recent);
        }
    }
}

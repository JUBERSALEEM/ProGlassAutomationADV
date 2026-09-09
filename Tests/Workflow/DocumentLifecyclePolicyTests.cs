// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Workflow/DocumentLifecyclePolicyTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ERP lifecycle hardening (Phase 1). Pins the delete/void/edit-lock policy —
// the single authority for "may this document be hard-deleted, or must it be
// voided, and may its body still be edited?".
//
//   1. Only childless PRE-POSTING drafts may be hard-deleted.
//   2. A posted document (issued / released / dispatched) is voided, never
//      deleted.
//   3. A document with LIVE children is Blocked (handle children first) —
//      never orphaned, never voided out from under live children.
//   4. Body-edit lock: posted bodies are frozen.
//   5. Case-insensitivity and null/blank safety (fiscal-safe default).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Workflow;
using Xunit;

using Kind = ProGlassAutomation.Services.Workflow.StatusTransitions.Kind;
using Decision = ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.DeleteDecision;

namespace ProGlassAutomation.Tests.Workflow
{
    public class DocumentLifecyclePolicyTests
    {
        // ── Hard-delete: only childless pre-posting drafts ─────────────────

        [Theory]
        [InlineData(Kind.ProformaInvoice, "Draft")]
        [InlineData(Kind.ProformaInvoice, "Sent")]
        [InlineData(Kind.ProformaInvoice, "Rejected")]
        [InlineData(Kind.ProformaInvoice, "Expired")]
        [InlineData(Kind.JobOrder, "Draft")]
        [InlineData(Kind.DeliveryNote, "Draft")]
        [InlineData(Kind.TaxInvoice, "Draft")]
        public void Childless_pre_posting_states_can_be_hard_deleted(Kind kind, string status)
        {
            Assert.True(DocumentLifecyclePolicy.CanHardDelete(kind, status, hasChildren: false));
        }

        [Theory]
        [InlineData(Kind.ProformaInvoice, "Approved")]   // converted to a JO
        [InlineData(Kind.ProformaInvoice, "Cancelled")]
        [InlineData(Kind.JobOrder, "Released")]
        [InlineData(Kind.JobOrder, "InProduction")]
        [InlineData(Kind.JobOrder, "Delivered")]
        [InlineData(Kind.DeliveryNote, "Dispatched")]
        [InlineData(Kind.DeliveryNote, "Signed")]
        [InlineData(Kind.TaxInvoice, "Issued")]
        [InlineData(Kind.TaxInvoice, "Paid")]
        [InlineData(Kind.TaxInvoice, "PartiallyPaid")]
        public void Posted_states_can_never_be_hard_deleted(Kind kind, string status)
        {
            Assert.False(DocumentLifecyclePolicy.CanHardDelete(kind, status, hasChildren: false));
        }

        [Fact]
        public void A_draft_with_children_cannot_be_hard_deleted()
        {
            Assert.False(DocumentLifecyclePolicy.CanHardDelete(
                Kind.ProformaInvoice, "Draft", hasChildren: true));
        }

        // ── Three-way delete decision ──────────────────────────────────────

        [Fact]
        public void Childless_draft_decides_HardDelete()
        {
            Assert.Equal(Decision.HardDelete,
                DocumentLifecyclePolicy.DecideDelete(Kind.TaxInvoice, "Draft", hasLiveChildren: false));
        }

        [Fact]
        public void Draft_with_live_children_decides_Blocked()
        {
            // A draft that somehow already has live children must not be
            // deleted OR voided — the children come first.
            Assert.Equal(Decision.Blocked,
                DocumentLifecyclePolicy.DecideDelete(Kind.ProformaInvoice, "Draft", hasLiveChildren: true));
        }

        [Fact]
        public void Posted_childless_decides_Void()
        {
            Assert.Equal(Decision.Void,
                DocumentLifecyclePolicy.DecideDelete(Kind.TaxInvoice, "Issued", hasLiveChildren: false));
        }

        [Fact]
        public void Posted_with_live_children_decides_Blocked()
        {
            Assert.Equal(Decision.Blocked,
                DocumentLifecyclePolicy.DecideDelete(Kind.ProformaInvoice, "Approved", hasLiveChildren: true));
        }

        // ── Body-edit lock ─────────────────────────────────────────────────

        [Theory]
        [InlineData(Kind.ProformaInvoice, "Draft", true)]
        [InlineData(Kind.ProformaInvoice, "Approved", false)]
        [InlineData(Kind.TaxInvoice, "Draft", true)]
        [InlineData(Kind.TaxInvoice, "Issued", false)]
        [InlineData(Kind.DeliveryNote, "Draft", true)]
        [InlineData(Kind.DeliveryNote, "Dispatched", false)]
        [InlineData(Kind.JobOrder, "InProduction", true)]
        [InlineData(Kind.JobOrder, "Delivered", false)]
        public void Body_edit_lock_matches_matrix(Kind kind, string status, bool editable)
        {
            Assert.Equal(editable, DocumentLifecyclePolicy.CanEditBody(kind, status));
        }

        // ── Safety: case-insensitive, null/blank protective ────────────────

        [Fact]
        public void Status_matching_is_case_insensitive()
        {
            Assert.True(DocumentLifecyclePolicy.CanHardDelete(Kind.TaxInvoice, "draft", false));
            Assert.True(DocumentLifecyclePolicy.CanEditBody(Kind.TaxInvoice, "DRAFT"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("SomethingUnknown")]
        public void Unknown_or_blank_status_is_protected(string? status)
        {
            // Fiscal-safe default: never hard-delete, never allow body edit.
            Assert.False(DocumentLifecyclePolicy.CanHardDelete(Kind.TaxInvoice, status, false));
            Assert.False(DocumentLifecyclePolicy.CanEditBody(Kind.TaxInvoice, status));
        }

        // ── Void target status per kind ────────────────────────────────────

        [Theory]
        [InlineData(Kind.ProformaInvoice, "Cancelled")]
        [InlineData(Kind.JobOrder, "Rejected")]
        [InlineData(Kind.DeliveryNote, "Cancelled")]
        [InlineData(Kind.TaxInvoice, "Cancelled")]
        public void Void_target_status_is_defined_per_kind(Kind kind, string expected)
        {
            Assert.Equal(expected, DocumentLifecyclePolicy.VoidTargetStatus(kind));
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Workflow/DocumentLifecyclePolicyTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ERP lifecycle hardening (Phase 1). Pins the delete/void/edit-lock policy —
// the single authority for "may this document be hard-deleted, or must it be
// voided, and may its body still be edited?".
//
//   1. Only childless PRE-POSTING drafts may be hard-deleted.
//   2. A posted document is never deleted; it is voided only when the
//      lifecycle transition table permits, otherwise retained unchanged.
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
        [InlineData(Kind.CreditNote, "Draft")]
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
        [InlineData(Kind.CreditNote, "Issued")]
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
                DocumentLifecyclePolicy.DecideDelete(Kind.TaxInvoice, "Draft", hasLinkedRecords: false));
        }

        [Fact]
        public void Draft_with_live_children_decides_Blocked()
        {
            // A draft that somehow already has live children must not be
            // deleted OR voided — the children come first.
            Assert.Equal(Decision.Blocked,
                DocumentLifecyclePolicy.DecideDelete(Kind.ProformaInvoice, "Draft", hasLinkedRecords: true));
        }

        [Fact]
        public void Posted_childless_decides_Void()
        {
            Assert.Equal(Decision.Void,
                DocumentLifecyclePolicy.DecideDelete(Kind.TaxInvoice, "Issued", hasLinkedRecords: false));
        }

        [Theory]
        [InlineData(Kind.TaxInvoice, "Paid")]
        [InlineData(Kind.CreditNote, "Applied")]
        [InlineData(Kind.DeliveryNote, "Received")]
        [InlineData(Kind.TaxInvoice, "LegacyUnknown")]
        [InlineData(Kind.TaxInvoice, "")]
        public void Terminal_or_unknown_states_are_retained_instead_of_forced_through_void(
            Kind kind, string status)
        {
            Assert.Equal(Decision.Retained,
                DocumentLifecyclePolicy.DecideDelete(kind, status, hasLinkedRecords: false));
        }

        [Fact]
        public void Posted_with_live_children_decides_Blocked()
        {
            Assert.Equal(Decision.Blocked,
                DocumentLifecyclePolicy.DecideDelete(Kind.ProformaInvoice, "Approved", hasLinkedRecords: true));
        }

        // ── Voided documents are retained ─────────────────────────────────

        [Theory]
        [InlineData(Kind.DeliveryNote, "Cancelled")]
        [InlineData(Kind.JobOrder, "Rejected")]
        [InlineData(Kind.ProformaInvoice, "Cancelled")]
        [InlineData(Kind.TaxInvoice, "Cancelled")]
        [InlineData(Kind.CreditNote, "cancelled")]
        public void Voided_terminal_childless_documents_decide_Retained(Kind kind, string status)
        {
            Assert.Equal(Decision.Retained,
                DocumentLifecyclePolicy.DecideDelete(kind, status, hasLinkedRecords: false));
        }

        [Fact]
        public void Voided_document_with_linked_records_stays_Blocked()
        {
            Assert.Equal(Decision.Blocked,
                DocumentLifecyclePolicy.DecideDelete(Kind.DeliveryNote, "Cancelled", hasLinkedRecords: true));
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
        [InlineData(Kind.CreditNote, "Draft", true)]
        [InlineData(Kind.CreditNote, "Issued", false)]
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
            // Fiscal-safe default: never hard-delete, edit, or force through void.
            Assert.False(DocumentLifecyclePolicy.CanHardDelete(Kind.TaxInvoice, status, false));
            Assert.False(DocumentLifecyclePolicy.CanEditBody(Kind.TaxInvoice, status));
            Assert.False(DocumentLifecyclePolicy.CanVoid(Kind.TaxInvoice, status));
        }

        // ── Void target status per kind ────────────────────────────────────

        [Theory]
        [InlineData(Kind.ProformaInvoice, "Cancelled")]
        [InlineData(Kind.JobOrder, "Rejected")]
        [InlineData(Kind.DeliveryNote, "Cancelled")]
        [InlineData(Kind.TaxInvoice, "Cancelled")]
        [InlineData(Kind.CreditNote, "Cancelled")]
        public void Void_target_status_is_defined_per_kind(Kind kind, string expected)
        {
            Assert.Equal(expected, DocumentLifecyclePolicy.VoidTargetStatus(kind));
        }

        [Theory]
        [InlineData(Kind.ProformaInvoice, "Draft", true)]
        [InlineData(Kind.JobOrder, "Delivered", true)]
        [InlineData(Kind.DeliveryNote, "Delivered", true)]
        [InlineData(Kind.DeliveryNote, "Received", false)]
        [InlineData(Kind.TaxInvoice, "Issued", true)]
        [InlineData(Kind.TaxInvoice, "Paid", false)]
        [InlineData(Kind.CreditNote, "Issued", true)]
        [InlineData(Kind.CreditNote, "Applied", false)]
        [InlineData(Kind.TaxInvoice, "LegacyUnknown", false)]
        public void CanVoid_matches_the_configured_status_transition(
            Kind kind, string status, bool allowed)
        {
            Assert.Equal(allowed, DocumentLifecyclePolicy.CanVoid(kind, status));
        }

        [Fact]
        public void New_records_cannot_carry_void_audit_metadata_without_the_repository_void_path()
        {
            Assert.False(DocumentLifecyclePolicy.HasVoidMetadata(null, null, null));
            Assert.True(DocumentLifecyclePolicy.HasVoidMetadata(
                System.DateTime.UtcNow, null, null));
            Assert.True(DocumentLifecyclePolicy.HasVoidMetadata(null, "operator", null));
            Assert.True(DocumentLifecyclePolicy.HasVoidMetadata(null, null, "reason"));
        }

        [Fact]
        public void Void_reason_is_trimmed_and_required()
        {
            Assert.Equal("Customer cancelled", DocumentLifecyclePolicy.NormalizeVoidReason(
                "  Customer cancelled  "));
            Assert.Throws<System.ArgumentException>(() =>
                DocumentLifecyclePolicy.NormalizeVoidReason("  "));
        }

        [Fact]
        public void Void_reason_is_rejected_when_too_long_for_audit_storage()
        {
            Assert.Throws<System.ArgumentException>(() =>
                DocumentLifecyclePolicy.NormalizeVoidReason(new string('x', 501)));
        }
    }
}

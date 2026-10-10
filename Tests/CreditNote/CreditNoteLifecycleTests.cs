// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/CreditNote/CreditNoteLifecycleTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ERP hardening (Phase 4). Pins the Credit Note into the shared workflow and
// lifecycle authorities so it obeys the same fiscal rules as a Tax Invoice:
//   • only a Draft may be physically deleted or edited;
//   • an Issued credit note is voided (Cancelled), never erased;
//   • deleting/voiding is Manager/Admin-only (not Accountant).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Workflow;
using Xunit;

namespace ProGlassAutomation.Tests.CreditNote
{
    public class CreditNoteLifecycleTests
    {
        [Fact]
        public void Only_a_draft_credit_note_is_hard_deletable()
        {
            Assert.True(DocumentLifecyclePolicy.CanHardDelete(
                StatusTransitions.Kind.CreditNote, "Draft", hasChildren: false));
            Assert.False(DocumentLifecyclePolicy.CanHardDelete(
                StatusTransitions.Kind.CreditNote, "Issued", hasChildren: false));
            Assert.False(DocumentLifecyclePolicy.CanHardDelete(
                StatusTransitions.Kind.CreditNote, "Applied", hasChildren: false));
        }

        [Fact]
        public void Only_a_draft_credit_note_body_is_editable()
        {
            Assert.True(DocumentLifecyclePolicy.CanEditBody(
                StatusTransitions.Kind.CreditNote, "Draft"));
            Assert.False(DocumentLifecyclePolicy.CanEditBody(
                StatusTransitions.Kind.CreditNote, "Issued"));
        }

        [Fact]
        public void Void_target_is_cancelled()
        {
            Assert.Equal("Cancelled",
                DocumentLifecyclePolicy.VoidTargetStatus(StatusTransitions.Kind.CreditNote));
        }

        [Fact]
        public void A_draft_credit_note_linked_to_its_source_invoice_is_not_hard_deletable()
        {
            // A source TI reference is mandatory for every valid CN, so the
            // repository's dependency check blocks physical deletion even in Draft.
            var decision = DocumentLifecyclePolicy.DecideDelete(
                StatusTransitions.Kind.CreditNote, "Draft", hasLinkedRecords: true);
            Assert.Equal(DocumentLifecyclePolicy.DeleteDecision.Blocked, decision);
        }

        [Fact]
        public void Childless_issued_credit_note_is_policy_eligible_for_void()
        {
            // The shared status policy selects void; the repository separately
            // blocks DeleteAsync while the mandatory source-TI link remains.
            var decision = DocumentLifecyclePolicy.DecideDelete(
                StatusTransitions.Kind.CreditNote, "Issued", hasLinkedRecords: false);
            Assert.Equal(DocumentLifecyclePolicy.DeleteDecision.Void, decision);
        }

        [Theory]
        [InlineData("Draft", "Issued", true)]
        [InlineData("Draft", "Cancelled", true)]
        [InlineData("Issued", "Applied", true)]
        [InlineData("Issued", "Cancelled", true)]
        [InlineData("Applied", "Draft", false)]   // terminal
        [InlineData("Cancelled", "Issued", false)] // terminal
        public void Transition_table_matches_fiscal_rules(string from, string to, bool allowed)
        {
            var result = StatusTransitions.Validate(
                StatusTransitions.Kind.CreditNote, from, to);
            Assert.Equal(allowed, result.IsValid);
        }

        [Theory]
        [InlineData(AppRoles.Administrator)]
        [InlineData(AppRoles.Manager)]
        public void Manager_and_admin_can_delete_credit_notes(string role)
        {
            Assert.True(RolePermissions.Has(role, AppPermissions.CreditNoteDelete));
        }

        [Fact]
        public void Accountant_can_edit_but_not_delete_credit_notes()
        {
            Assert.True(RolePermissions.Has(AppRoles.Accountant, AppPermissions.CreditNoteEdit));
            Assert.False(RolePermissions.Has(AppRoles.Accountant, AppPermissions.CreditNoteDelete));
        }
    }
}

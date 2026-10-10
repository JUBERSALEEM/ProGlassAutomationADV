// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Workflow/DocumentLifecyclePolicy.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — document delete/void/edit lifecycle policy (ERP hardening)
//
// THE PROBLEM THIS SOLVES
//   Every document delete in the app was a physical row delete, including
//   posted Tax Invoices — a fiscal/compliance failure (an issued invoice
//   must be retained, not erased; it is corrected by a credit note). There
//   was also no single authority for "may this document's body still be
//   edited?" — InvoiceLifecycleStateMachine.IsEditable existed but was
//   enforced nowhere.
//
//   This class is the SINGLE authority for three questions, matching the
//   §3 matrix in docs/ERP_LIFECYCLE_HARDENING_ANALYSIS_2026-09-09.md:
//
//     • CanHardDelete(kind, status, hasChildren)
//         → true  = a childless pre-posting DRAFT: safe to physically delete
//         → false = never physically delete; VOID only if the status table
//                     permits the configured void edge, otherwise retain
//
//     • CanEditBody(kind, status)
//         → true  = still a working draft: line items / totals editable
//         → false = posted: body frozen, only status transitions allowed
//                   (correct it via Cancel/Void or a Credit Note)
//
//     • VoidTargetStatus(kind)
//         → the terminal "soft-closed" status a Void writes for each kind
//           (Cancelled for PI/TI-cancel path, Rejected for JO, Cancelled
//           for DN). Kept here so repositories don't hard-code it.
//
// DESIGN RULES (mirrors StatusTransitions.cs):
//   • Pure, static, testable — no database, no WPF, never throws.
//   • Reuses StatusTransitions.Kind so there is ONE document-kind vocabulary.
//   • Case-insensitive status matching, tolerant of null/blank (unknown or
//     blank status is treated as NOT hard-deletable and NOT editable — the
//     safe default for a fiscal document is "protect it").
//   • The status sets are deliberately CONSERVATIVE: only genuinely
//     pre-posting states are hard-deletable / body-editable, so enforcing
//     this can never silently destroy or rewrite a committed document.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Workflow
{
    /// <summary>
    /// Single authority for delete-vs-void and body-edit-lock decisions per
    /// document type. See the class header and the ERP hardening analysis.
    /// </summary>
    public static class DocumentLifecyclePolicy
    {
        // ── Pre-posting statuses that MAY be physically deleted ────────────
        // Anything not in these sets is never physically deleted. It is
        // soft-closed only when the status transition table permits the void
        // edge; terminal/unknown states are retained unchanged.
        private static readonly Dictionary<StatusTransitions.Kind, HashSet<string>> HardDeletable =
            new()
            {
                // A PI in these states is only ever a quotation that never
                // became a real order — safe to delete. Approved (it converted
                // to a Job Order) and Cancelled are void-only.
                [StatusTransitions.Kind.ProformaInvoice] = CaseInsensitive(
                    "Draft", "Sent", "Rejected", "Expired"),

                // Once a Job Order is Released it is in production; only a raw
                // Draft may be deleted.
                [StatusTransitions.Kind.JobOrder] = CaseInsensitive(
                    "Draft"),

                // Once a Delivery Note is Dispatched the goods have left; only
                // a Draft may be deleted.
                [StatusTransitions.Kind.DeliveryNote] = CaseInsensitive(
                    "Draft"),

                // A Tax Invoice is a fiscal document the moment it is Issued —
                // only a Draft may ever be deleted. Everything else = void +
                // credit note.
                [StatusTransitions.Kind.TaxInvoice] = CaseInsensitive(
                    "Draft"),

                // A Credit Note is itself a fiscal correction document: once
                // Issued it must be retained (voided when its transition table
                // permits, never deleted). Only a Draft may be deleted.
                [StatusTransitions.Kind.CreditNote] = CaseInsensitive(
                    "Draft"),
            };

        // ── Statuses whose document BODY (lines / totals / party / dates)
        //    may still be edited. Status-only transitions (e.g. Issued→Paid)
        //    are always governed separately by StatusTransitions. ──────────
        private static readonly Dictionary<StatusTransitions.Kind, HashSet<string>> BodyEditable =
            new()
            {
                [StatusTransitions.Kind.ProformaInvoice] = CaseInsensitive(
                    "Draft", "Sent", "Rejected", "Expired"),

                // A Job Order's PI snapshot is immutable by design; its
                // production details stay editable until it is delivered or
                // rejected.
                [StatusTransitions.Kind.JobOrder] = CaseInsensitive(
                    "Draft", "Released", "InProduction", "Completed"),

                [StatusTransitions.Kind.DeliveryNote] = CaseInsensitive(
                    "Draft", "Dispatching"),

                // Only a Draft Tax Invoice body is editable. Once Issued the
                // figures are frozen.
                [StatusTransitions.Kind.TaxInvoice] = CaseInsensitive(
                    "Draft"),

                // Only a Draft Credit Note body is editable. Once Issued its
                // figures are frozen — it has legal effect on the ledger.
                [StatusTransitions.Kind.CreditNote] = CaseInsensitive(
                    "Draft"),
            };

        // ── The terminal status a Void writes, per kind ───────────────────
        private static readonly Dictionary<StatusTransitions.Kind, string> VoidStatus =
            new()
            {
                [StatusTransitions.Kind.ProformaInvoice] = "Cancelled",
                [StatusTransitions.Kind.JobOrder]        = "Rejected",
                [StatusTransitions.Kind.DeliveryNote]    = "Cancelled",
                [StatusTransitions.Kind.TaxInvoice]      = "Cancelled",
                [StatusTransitions.Kind.CreditNote]      = "Cancelled",
            };

        /// <summary>
        /// True only when the document is a childless, pre-posting draft that
        /// is safe to PHYSICALLY delete. Any posted state, or any document
        /// that still has dependents, returns false — the caller may VOID it
        /// only when the status transition table permits; otherwise it retains
        /// the row unchanged. Unknown/blank status is not deletable (safe).
        /// </summary>
        public static bool CanHardDelete(StatusTransitions.Kind kind, string? status, bool hasChildren)
        {
            if (hasChildren) return false;
            if (string.IsNullOrWhiteSpace(status)) return false;
            return HardDeletable.TryGetValue(kind, out var set)
                && set.Contains(status.Trim());
        }

        /// <summary>
        /// True when the document body (line items, totals, party, dates) may
        /// still be edited. False for posted documents — those are corrected
        /// via Cancel/Void or a Credit Note, not by rewriting the original.
        /// Unknown/blank status is treated as not-editable (safe).
        /// </summary>
        public static bool CanEditBody(StatusTransitions.Kind kind, string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return BodyEditable.TryGetValue(kind, out var set)
                && set.Contains(status.Trim());
        }

        /// <summary>
        /// The terminal "soft-closed" status a Void writes for a document
        /// kind (e.g. Cancelled for a PI, Rejected for a Job Order). Falls
        /// back to "Cancelled" for an unmapped kind.
        /// </summary>
        public static string VoidTargetStatus(StatusTransitions.Kind kind)
            => VoidStatus.TryGetValue(kind, out var s) ? s : "Cancelled";

        /// <summary>
        /// True when the document's current status may legally take the
        /// configured void edge in <see cref="StatusTransitions"/>. Unknown,
        /// blank, already-void, and terminal source states fail closed.
        /// Keeping this check in the shared policy prevents an explicit
        /// VoidAsync call from bypassing the ordinary status transition table.
        /// </summary>
        public static bool CanVoid(StatusTransitions.Kind kind, string? status)
        {
            if (string.IsNullOrWhiteSpace(status) || IsVoidTerminal(kind, status))
                return false;

            return StatusTransitions.Validate(
                kind, status, VoidTargetStatus(kind)).IsValid;
        }

        /// <summary>
        /// True when an entity carries any operator-void audit metadata. New
        /// records must not arrive pre-voided or with caller-forged audit data;
        /// repositories own that transition and its durable audit entry.
        /// </summary>
        public static bool HasVoidMetadata(
            DateTime? voidedAtUtc, string? voidedBy, string? voidReason)
            => voidedAtUtc.HasValue
                || !string.IsNullOrEmpty(voidedBy)
                || !string.IsNullOrEmpty(voidReason);

        /// <summary>
        /// Voids must carry an operator-supplied reason. Normalize whitespace
        /// once at the service boundary and reject values that cannot fit the
        /// persisted audit field instead of silently truncating legal context.
        /// </summary>
        public static string NormalizeVoidReason(string? reason)
        {
            string normalized = (reason ?? string.Empty).Trim();
            if (normalized.Length == 0)
                throw new ArgumentException("A void reason is required.", nameof(reason));
            if (normalized.Length > 500)
                throw new ArgumentException(
                    "A void reason cannot exceed 500 characters.", nameof(reason));
            return normalized;
        }

        /// <summary>
        /// The three-way decision a "delete" action must take, per the §3
        /// matrix. This is the single place the rule lives so every module
        /// behaves identically.
        /// </summary>
        public enum DeleteDecision
        {
            /// <summary>Childless pre-posting draft — physically delete it.</summary>
            HardDelete,

            /// <summary>Has dependents — block; the children must be handled
            /// first. Never orphan or void a parent out from under linked rows.</summary>
            Blocked,

            /// <summary>Already voided, terminal, unknown, or otherwise not
            /// safely deletable/voidable; retain without changing its state.</summary>
            Retained,

            /// <summary>Childless and the status table permits the void edge;
            /// soft-close so the number, snapshot and audit trail survive.</summary>
            Void,
        }

        /// <summary>
        /// True when the status is the terminal soft-closed (void) status for
        /// the kind (Cancelled / Rejected). Terminal records are retained and
        /// are never eligible for physical deletion.
        /// </summary>
        public static bool IsVoidTerminal(StatusTransitions.Kind kind, string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return string.Equals(
                status.Trim(), VoidTargetStatus(kind),
                StringComparison.OrdinalIgnoreCase);
        }

        public static DeleteDecision DecideDelete(
            StatusTransitions.Kind kind, string? status, bool hasLinkedRecords)
        {
            // A retained link is a hard-delete blocker regardless of whether
            // the linked document is live or voided.
            if (hasLinkedRecords)
                return DeleteDecision.Blocked;
            if (IsVoidTerminal(kind, status))
                return DeleteDecision.Retained;
            if (CanHardDelete(kind, status, hasChildren: false))
                return DeleteDecision.HardDelete;
            return CanVoid(kind, status)
                ? DeleteDecision.Void
                : DeleteDecision.Retained;
        }

        private static HashSet<string> CaseInsensitive(params string[] values)
            => new(values, StringComparer.OrdinalIgnoreCase);
    }
}

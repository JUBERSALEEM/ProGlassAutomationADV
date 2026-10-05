// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Workflow/StatementWorkflowEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine
//
// CHAT 22 v3 ADDITIONS:
//   • Step 3 — Per-DN line-item reconciliation logging
//     Shows Ordered/Prev Delivered/This DN/Balance/Invoiced per line
//     Flags over-invoicing at earliest possible pipeline stage
//     Uses cached PI + TI data (loaded once, reused per DN)
//   • Step 6 — Line-item reconciliation via OrderReconciliationService
//     Populates WorkflowResult.ReconciliationSummaries
//     Adds warnings for over-invoiced/under-invoiced items
//
// CHAT 18 PRESERVED:
//   BUG 1 FIX: Step 7 — loads TI attachments from DB after PDF regen.
//   BUG 2 FIX: Step 4 — partial delivery → MarkWarning (not Success).
//   BUG 3 FIX: Step 8 — tiAttCount included in sections total.
//   BUG 4 FIX: Step 3 — DN attachment file resolution with 3-strategy
//              fallback (stored path → rebuild from AppData → search
//              both subfolders). Full debug logging on every file check.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Reconciliation;
using ProGlassAutomation.Services.TaxInvoice.Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Workflow
{
    public delegate Task<UserWarningDecision> ResolveMissingInvoicesCallback(
        WorkflowResult partialResult);

    /// <summary>
    /// Summary of which required attachments are missing, shown to the user
    /// before a statement is finalized so they can attach the missing signed
    /// documents (or abort the statement).
    /// </summary>
    public sealed class MissingAttachmentSummary
    {
        /// <summary>True when the Proforma Invoice has no signed attachment in Step 8.</summary>
        public bool ProformaMissing { get; init; }

        /// <summary>Delivery-note ids that have no signed attachment on disk.</summary>
        public List<string> MissingDeliveryNoteIds { get; init; } = new();
    }

    public delegate Task<UserWarningDecision> ResolveMissingAttachmentsCallback(
        MissingAttachmentSummary summary);

    public partial class StatementWorkflowEngine
    {
    }
}
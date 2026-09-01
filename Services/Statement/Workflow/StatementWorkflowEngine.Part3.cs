// ═══════════════════════════════════════════════════════════════════════
// FILE: StatementWorkflowEngine.Part3.cs (Phase 3.8 split — member census verified)
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
        public partial class StatementWorkflowEngine
    {
            // ═════════════════════════════════════════════════════════════════
        //  STEP 8 — PREPARE MERGE
        //  BUG 3 FIX: tiAttCount included in sections total + summary
        // ═════════════════════════════════════════════════════════════════

        private async Task Step8_PrepareMerge(WorkflowResult result)
        {
            var step = Steps[7];
            step.MarkRunning("Preparing final PDF merge...");
            await Task.Delay(90);

            int sections = 1; // Statement summary always present

            // Signed PI
            bool hasPi = result.SignedProformaInvoiceBytes != null &&
                         result.SignedProformaInvoiceBytes.Length > 0;
            if (hasPi)
            {
                sections++;
                step.AddDetail("📎 Signed PI: included");
            }
            else
            {
                step.AddDetail("📎 Signed PI: not available");
            }

            // DN attachments
            int dnAttCount = result.SignedDeliveryNotes
                .Sum(d => d.AttachmentBytes.Count);
            sections += dnAttCount;
            step.AddDetail(
                $"📎 DN attachments: {dnAttCount} file(s)");

            // Invoice PDFs (valid + placeholders both count as sections)
            int validInv = result.TaxInvoicePdfs.Count(p => p.IsValid);
            int failedInv = result.TaxInvoicePdfs.Count(p => !p.IsValid);
            sections += validInv + failedInv;
            step.AddDetail(
                $"🧾 Invoice PDFs: {validInv} valid" +
                (failedInv > 0
                    ? $" · {failedInv} placeholder(s)"
                    : ""));

            // BUG 3 FIX: TI attachments per invoice
            int tiAttCount = result.TaxInvoicePdfs
                .Sum(p => p.AttachmentFileCount);
            sections += tiAttCount;
            step.AddDetail(
                $"📎 TI attachments: {tiAttCount} file(s) " +
                $"(across {result.TaxInvoicePdfs.Count} invoice(s))");

            await Task.Delay(90);

            step.AddDetail($"📄 Total sections: {sections}");
            step.AddDetail("");
            step.AddDetail(
                $"📊 Summary: Statement" +
                (hasPi ? " + Signed PI" : "") +
                $" + {dnAttCount} DN att(s)" +
                $" + {validInv} Invoice PDF(s)" +
                $" + {tiAttCount} TI att(s)" +
                $" = {sections} total");

            await Task.Delay(90);

            step.MarkSuccess(
                $"Ready to merge {sections} sections into final PDF ✓");
        }

        // ═════════════════════════════════════════════════════════════════
        //  FINALIZATION
        // ═════════════════════════════════════════════════════════════════

        private WorkflowResult FinalizeBlocked(WorkflowResult result)
        {
            MarkRemainingPendingAsSkipped();
            result.Outcome = WorkflowOutcome.Blocked;
            result.Summary = "Workflow blocked. Fix the issues above.";
            CollectAllWarnings(result);
            return result;
        }

        private WorkflowResult FinalizeCancelled(
            WorkflowResult result, string msg)
        {
            MarkRemainingPendingAsSkipped();
            result.Outcome = WorkflowOutcome.Cancelled;
            result.Summary = msg;
            CollectAllWarnings(result);
            return result;
        }

        private WorkflowResult FinalizeSuccess(WorkflowResult result)
        {
            CollectAllWarnings(result);

            if (result.AllWarnings.Count > 0)
            {
                result.Outcome = WorkflowOutcome.Warning;
                result.Summary =
                    $"Statement ready with " +
                    $"{result.AllWarnings.Count} warning(s).";
            }
            else
            {
                result.Outcome = WorkflowOutcome.Success;
                result.Summary = "Statement ready. All checks passed ✓";
            }

            return result;
        }

        private void MarkRemainingPendingAsSkipped()
        {
            foreach (var step in Steps)
                if (step.Status == StepStatus.Pending)
                    step.MarkSkipped();
        }

        private void CollectAllWarnings(WorkflowResult result)
        {
            result.AllWarnings.Clear();
            foreach (var step in Steps)
                foreach (var w in step.Warnings)
                    result.AllWarnings.Add(w);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a summary of missing signed attachments (PI + DNs), or null
        /// when everything required is present. Used by the missing-attachment
        /// gate in RunAsync so the user can attach documents or abort.
        /// </summary>
        private static MissingAttachmentSummary? BuildMissingAttachmentSummary(
            WorkflowResult result)
        {
            bool proformaMissing =
                result.SignedProformaInvoiceBytes == null ||
                result.SignedProformaInvoiceBytes.Length == 0;

            var missingDns = result.SignedDeliveryNotes
                .Where(d => d.AttachmentBytes.Count == 0)
                .Select(d => d.DeliveryNoteId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            if (!proformaMissing && missingDns.Count == 0)
                return null;

            return new MissingAttachmentSummary
            {
                ProformaMissing = proformaMissing,
                MissingDeliveryNoteIds = missingDns
            };
        }
    }
}

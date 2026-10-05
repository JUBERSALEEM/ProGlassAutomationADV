// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetDocumentChainTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §32 (DOCUMENT CHAIN QUERIES), §5-§8 (document awareness),
//                §4 (do not duplicate ERP logic), §21 (read-only first),
//                §29/§30 (confidence, no fabrication)
//
// ANSWERS: "What is the status of PI 1052?"
//          "How much of PI 1052 has been delivered?"
//          "Which job order / tax invoice belongs to PI 1052?"
//
// THE RELATIONSHIPS WERE READ OUT OF THE ENTITIES, NOT ASSUMED (§10, §32):
//     SavedJobOrder.SourceProformaId       -> PI
//     SavedDeliveryNote.ProformaId         -> PI
//     SavedDeliveryNote.JobOrderId         -> JO
//     SavedTaxInvoice.ProformaId           -> PI
//     TotalOrderedQty / TotalDeliveredQty / TotalBalanceQty -> progress
//
// TWO REPOSITORY FACTS THAT SHAPE THIS CODE — both verified, not guessed:
//
//   1. IJobOrderRepository does NOT expose GetBySourceProformaIdAsync.
//      That method exists on the concrete JobOrderRepository but is not on
//      the interface App.Jobs is typed as. Rather than construct a second
//      repository (which would bypass the app's own instance and violate
//      §4/§23), this filters GetAllAsync on SourceProformaId in memory.
//
//   2. IDeliveryNoteRepository.GetAllAsync EXCLUDES Master DNs by design.
//      Master DNs are what carry the running delivered/balance totals, so
//      asking GetAllAsync for progress would silently report nothing
//      outstanding. Progress therefore comes from GetMasterDnsAsync, and
//      the dispatched notes come from GetAllAsync.
//
// Everything goes through the App.* repositories the rest of the ERP uses.
// No DbContext, no raw SQL, no writes (§21, §23).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Walks PI → Job Order → Delivery Note → Tax Invoice.</summary>
    public sealed class GetDocumentChainTool : IAgentTool
    {
        /// <summary>Never dump an unbounded list into a chat reply.</summary>
        private const int MaxListed = 8;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_document_chain",
            Description =
                "Follow a proforma through its job orders, deliveries and tax invoices",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.ProformaView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string pi = (argument ?? "").Trim();
            if (AgentReferenceResolver.TryExtractDocumentId(pi, out var extracted))
                pi = extracted;

            if (pi.Length == 0)
            {
                return ToolResult.Fail(
                    "Tell me which proforma to follow, for example " +
                    "\"chain for PI-1052\".");
            }

            try
            {
                var sb = new StringBuilder();
                var docs = new List<string> { pi };

                // ── 1. Job orders raised from this PI ─────────────────────
                var jobOrders = new List<SavedJobOrder>();

                if (App.Jobs != null)
                {
                    var allJobs = await App.Jobs.GetAllAsync().ConfigureAwait(false)
                                  ?? new List<SavedJobOrder>();

                    jobOrders = allJobs
                        .Where(j => j != null && Same(j.SourceProformaId, pi))
                        .ToList();
                }

                // ── 2. Dispatched delivery notes (Master DNs excluded) ────
                var dns = new List<DeliveryNoteListItem>();
                var masters = new List<DeliveryNoteListItem>();

                if (App.DeliveryNotes != null)
                {
                    var allDns = await App.DeliveryNotes.GetAllAsync()
                        .ConfigureAwait(false) ?? new List<DeliveryNoteListItem>();

                    dns = allDns
                        .Where(d => d != null && Same(d.ProformaId, pi))
                        .ToList();

                    // Master DNs hold the running balance per job order.
                    var allMasters = await App.DeliveryNotes.GetMasterDnsAsync()
                        .ConfigureAwait(false) ?? new List<DeliveryNoteListItem>();

                    masters = allMasters
                        .Where(m => m != null && Same(m.ProformaId, pi))
                        .ToList();
                }

                // ── 3. Tax invoices raised against this PI ────────────────
                var tis = new List<TaxInvoiceListItem>();

                if (App.TaxInvoices != null)
                {
                    tis = await App.TaxInvoices.GetByProformaIdAsync(pi)
                        .ConfigureAwait(false) ?? new List<TaxInvoiceListItem>();
                }

                // ── Nothing at all: say so, do not invent a chain ─────────
                if (jobOrders.Count == 0 && dns.Count == 0 &&
                    masters.Count == 0 && tis.Count == 0)
                {
                    return ToolResult.Ok(
                        $"{pi} has no job order, delivery note or tax invoice " +
                        "linked to it. Either it has not been processed yet, or " +
                        "that reference is not in the system.");
                }

                sb.AppendLine($"Document chain for {pi}:");

                // ── Job orders ────────────────────────────────────────────
                if (jobOrders.Count == 0)
                {
                    sb.AppendLine("• Job order: none raised yet.");
                }
                else
                {
                    foreach (var jo in jobOrders.Take(MaxListed))
                    {
                        docs.Add(jo.JobOrderId);
                        sb.AppendLine(
                            $"• Job order {jo.JobOrderId} — {jo.Status}" +
                            (jo.DeliveryDate.HasValue
                                ? $", due {jo.DeliveryDate.Value:dd MMM yyyy}"
                                : ""));
                    }

                    if (jobOrders.Count > MaxListed)
                        sb.AppendLine($"  (+{jobOrders.Count - MaxListed} more)");
                }

                // ── Deliveries ────────────────────────────────────────────
                if (dns.Count == 0)
                {
                    sb.AppendLine("• Deliveries: none dispatched yet.");
                }
                else
                {
                    sb.AppendLine($"• Delivery notes: {dns.Count}");

                    foreach (var dn in dns.Take(MaxListed))
                    {
                        docs.Add(dn.DeliveryNoteId);
                        sb.AppendLine(
                            $"    {dn.DeliveryNoteId} — {dn.Status}, " +
                            $"{dn.DeliveryDate:dd MMM yyyy}");
                    }

                    if (dns.Count > MaxListed)
                        sb.AppendLine($"    (+{dns.Count - MaxListed} more)");
                }

                // ── Progress, from the Master DNs only ────────────────────
                int ordered = masters.Sum(m => m.TotalOrderedQty);
                int delivered = masters.Sum(m => m.TotalDeliveredQty);
                int balance = masters.Sum(m => m.TotalBalanceQty);

                bool progressKnown = masters.Count > 0 && ordered > 0;

                if (progressKnown)
                {
                    decimal pct = Math.Round(delivered / (decimal)ordered * 100m, 1);

                    sb.AppendLine(
                        $"• Progress: {delivered} of {ordered} delivered " +
                        $"({pct.ToString("0.#", CultureInfo.InvariantCulture)}%), " +
                        $"{balance} remaining.");
                }
                else
                {
                    // Absent quantities are NOT zero delivered. Saying "0%"
                    // here would be a fabricated figure (§30).
                    sb.AppendLine(
                        "• Progress: no master delivery note carries quantities " +
                        "for this proforma, so I cannot state how much is " +
                        "outstanding.");
                }

                // ── Tax invoices ──────────────────────────────────────────
                if (tis.Count == 0)
                {
                    sb.AppendLine("• Tax invoice: none raised yet.");
                }
                else
                {
                    foreach (var ti in tis.Take(MaxListed))
                    {
                        docs.Add(ti.InvoiceNumber);
                        sb.AppendLine(
                            $"• Tax invoice {ti.InvoiceNumber} — {ti.Status}, " +
                            $"AED {ti.GrandTotal:N2}");
                    }

                    if (tis.Count > MaxListed)
                        sb.AppendLine($"  (+{tis.Count - MaxListed} more)");

                    // §8: never fabricate payment status. Status is the
                    // document's own field, not a payment confirmation.
                    sb.AppendLine(
                        "  (That is the invoice's own status field — I am not " +
                        "reporting payment unless the ERP records it.)");
                }

                // ── Provenance (§28/§29) ──────────────────────────────────
                var provenance = progressKnown
                    ? AnswerProvenance.Calculated(
                        "App.Jobs + App.DeliveryNotes + App.TaxInvoices",
                        "delivered ÷ ordered × 100, from master delivery notes",
                        docs.ToArray())
                    : AnswerProvenance.Known(
                        "App.Jobs + App.DeliveryNotes + App.TaxInvoices",
                        docs.ToArray());

                string qualifier = provenance.UserQualifier();
                if (qualifier.Length > 0) sb.AppendLine().Append(qualifier);

                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GetDocumentChainTool] {ex.Message}");

                return ToolResult.Fail(
                    "I could not read the document chain, so I am not going to " +
                    "guess at it. Check that the databases are reachable.");
            }
        }

        private static bool Same(string? a, string b) =>
            string.Equals((a ?? "").Trim(), b, StringComparison.OrdinalIgnoreCase);
    }
}

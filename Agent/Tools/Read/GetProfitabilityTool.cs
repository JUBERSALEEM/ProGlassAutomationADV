// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetProfitabilityTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE N)
//
// READ-ONLY profit lookup over PI-confirmed proforma invoices.
//
// WHY THIS TOOL EXISTS
//   The agent could search invoices, jobs, deliveries, customers, stock
//   and activity, but could not answer the question the business actually
//   asks: "did we make money on this job?". That number already exists —
//   Step 9 computes it and the mapper persists it — so this tool reads it
//   rather than recalculating and risking a second, disagreeing answer.
//
// SCOPE (mirrors the Job Profitability module)
//   Only proforma invoices whose stored Status is "Approved" are
//   reportable — the exact value the Saved PI list persists when the
//   operator marks a PI as "PI Confirmed". Drafts ("PI created"), PI
//   sent, rejected, cancelled and expired PIs are never quoted as
//   profit.
//
// TRUTHFULNESS RULES OBSERVED HERE
//   • Net, profit and margin are READ from the persisted Step 9 P&L
//     snapshot — nothing is recomputed from the invoice header.
//   • An invoice confirmed before Step 9 existed has NO cost data.
//     Reporting it as 100% margin would be a lie, so it is reported as
//     "not costed".
//   • A repository failure says so, instead of returning "no results",
//     which would read as "you have no profitable jobs".
//   • Losses are stated as losses, not as negative profit buried in a
//     signed number.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Reports profit and margin for PI-confirmed proformas.</summary>
    public sealed class GetProfitabilityTool : IAgentTool
    {
        private const int MaxRows = 8;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_profitability",
            Description =
                "Profit, cost and margin for a PI-confirmed proforma invoice, " +
                "or the worst-performing confirmed jobs",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.ProformaView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                var repo = App.Invoices;
                if (repo == null)
                    return ReadToolHelpers.Unavailable("saved invoices");

                // Only stored "Approved" PIs are PI-confirmed — that is the
                // exact value the Saved PI list persists for the "PI
                // Confirmed" action. Filter at the indexed Status column
                // first; drafts, PI sent and cancelled rows never leave the
                // database, then re-check case-insensitively in memory.
                var invoices = await repo.GetByStatusAsync("Approved")
                    .ConfigureAwait(false);
                if (invoices == null)
                    return ReadToolHelpers.Unavailable("PI-confirmed invoices");

                var costed = new List<JobProfit>();
                int uncosted = 0;

                foreach (var inv in invoices ?? new List<SavedProformaInvoice>())
                {
                    if (inv == null) continue;

                    if (!string.Equals(
                            inv.Status, "Approved",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Same read path Job Profitability uses, so the agent and
                    // the module can never quote different numbers.
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                    var pl = snap?.ProfitLoss;

                    decimal cost = (pl?.TotalGlassCost ?? 0m)
                                 + (pl?.TotalProductionCost ?? 0m);

                    bool hasCosting = pl != null
                        && (pl.Rows.Count > 0 || cost > 0m || pl.NetExVat > 0m);

                    if (!hasCosting)
                    {
                        uncosted++;
                        continue;
                    }

                    // Mirror JobProfitabilityViewModel exactly: read Step 9's
                    // own persisted figures. Net, profit and margin are the
                    // snapshot's numbers — nothing is recomputed from the
                    // invoice header and no VAT divisor exists here.
                    costed.Add(new JobProfit
                    {
                        ProformaId = inv.ProformaId ?? "",
                        Customer = inv.CustomerName ?? "",
                        Project = inv.ProjectName ?? "",
                        Net = pl!.NetExVat,
                        Cost = cost,
                        Profit = pl.Profit,
                        MarginPercent = pl.MarginPercent
                    });
                }

                if (costed.Count == 0)
                {
                    return ToolResult.Ok(
                        uncosted > 0
                            ? $"None of the {uncosted} PI-confirmed invoice(s) have " +
                              "Step 9 costing yet, so I cannot report profit for " +
                              "any of them."
                            : "There are no PI-confirmed invoices to report on.");
                }

                // ── Specific invoice / customer / project ─────────────────
                if (term.Length > 0)
                {
                    var hits = costed
                        .Where(j => ReadToolHelpers.Hit(j.ProformaId, term)
                                 || ReadToolHelpers.Hit(j.Customer, term)
                                 || ReadToolHelpers.Hit(j.Project, term))
                        .OrderBy(j => j.MarginPercent)
                        .ToList();

                    if (hits.Count == 0)
                    {
                        return ToolResult.Ok(
                            $"No costed PI-confirmed invoice matches \"{term}\". " +
                            (uncosted > 0
                                ? $"{uncosted} PI-confirmed invoice(s) have no Step 9 costing yet."
                                : ""));
                    }

                    return ToolResult.Ok(Format(
                        $"Profitability for \"{term}\":",
                        hits, uncosted));
                }

                // ── No term: lead with what needs attention ───────────────
                var worst = costed
                    .OrderBy(j => j.MarginPercent)
                    .Take(MaxRows)
                    .ToList();

                int losses = costed.Count(j => j.Profit < 0m);

                var header = losses > 0
                    ? $"{losses} of {costed.Count} costed job(s) are running at a loss. " +
                      "Lowest margin first:"
                    : $"{costed.Count} costed job(s), lowest margin first:";

                return ToolResult.Ok(Format(header, worst, uncosted));
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GetProfitabilityTool] {ex.Message}");
                return ReadToolHelpers.Unavailable("profitability figures");
            }
        }

        private static string Format(string header, List<JobProfit> rows, int uncosted)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);

            foreach (var j in rows.Take(MaxRows))
            {
                string who = string.IsNullOrWhiteSpace(j.Customer)
                    ? j.Project
                    : j.Customer;

                string verdict = j.Profit < 0m
                    ? $"LOSS AED {Math.Abs(j.Profit):N2}"
                    : $"profit AED {j.Profit:N2}";

                sb.AppendLine(
                    $"• {j.ProformaId} — {who} · net AED {j.Net:N2} · " +
                    $"cost AED {j.Cost:N2} · {verdict} ({j.MarginPercent:0.#}%)");
            }

            if (rows.Count > MaxRows)
                sb.AppendLine($"({MaxRows} of {rows.Count} shown.)");

            if (uncosted > 0)
                sb.AppendLine(
                    $"{uncosted} further PI-confirmed invoice(s) have no Step 9 " +
                    "costing, so they are excluded rather than counted as pure profit.");

            return sb.ToString().TrimEnd();
        }

        private sealed class JobProfit
        {
            public string ProformaId { get; init; } = "";
            public string Customer { get; init; } = "";
            public string Project { get; init; } = "";
            public decimal Net { get; init; }
            public decimal Cost { get; init; }
            public decimal Profit { get; init; }
            public decimal MarginPercent { get; init; }
        }
    }
}

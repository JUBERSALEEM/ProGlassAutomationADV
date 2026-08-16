// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetProfitabilityTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE N)
//
// READ-ONLY profit lookup over saved proforma invoices.
//
// WHY THIS TOOL EXISTS
//   The agent could search invoices, jobs, deliveries, customers, stock
//   and activity, but could not answer the question the business actually
//   asks: "did we make money on this job?". That number already exists —
//   Step 9 computes it and the mapper persists it — so this tool reads it
//   rather than recalculating and risking a second, disagreeing answer.
//
// TRUTHFULNESS RULES OBSERVED HERE
//   • An invoice saved before Step 9 existed has NO cost data. Reporting
//     it as 100% margin would be a lie, so it is reported as "not costed".
//   • A repository failure says so, instead of returning "no results",
//     which would read as "you have no profitable jobs".
//   • Losses are stated as losses, not as negative profit buried in a
//     signed number.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
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
    /// <summary>Reports profit and margin for saved proforma invoices.</summary>
    public sealed class GetProfitabilityTool : IAgentTool
    {
        private const int MaxRows = 8;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_profitability",
            Description =
                "Profit, cost and margin for a saved proforma invoice, " +
                "or the worst-performing jobs",
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

                var invoices = await repo.GetAllAsync().ConfigureAwait(false);
                if (invoices == null)
                    return ReadToolHelpers.Unavailable("saved invoices");

                var costed = new List<JobProfit>();
                int uncosted = 0;

                foreach (var inv in invoices)
                {
                    if (inv == null) continue;

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

                    costed.Add(new JobProfit
                    {
                        ProformaId = inv.ProformaId ?? "",
                        Customer = inv.CustomerName ?? "",
                        Project = inv.ProjectName ?? "",
                        Net = pl!.NetExVat,
                        Cost = cost,
                        Profit = pl.Profit,
                        MarginPercent = pl.NetExVat > 0m
                            ? Math.Round(pl.Profit / pl.NetExVat * 100m, 1)
                            : 0m
                    });
                }

                if (costed.Count == 0)
                {
                    return ToolResult.Ok(
                        uncosted > 0
                            ? $"None of the {uncosted} saved invoice(s) have Step 9 " +
                              "costing yet, so I cannot report profit for any of them."
                            : "There are no saved invoices to report on.");
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
                            $"No costed invoice matches \"{term}\". " +
                            (uncosted > 0
                                ? $"{uncosted} saved invoice(s) have no Step 9 costing yet."
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
                Debug.WriteLine($"[GetProfitabilityTool] {ex.Message}");
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
                    $"{uncosted} further invoice(s) have no Step 9 costing, " +
                    "so they are excluded rather than counted as pure profit.");

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

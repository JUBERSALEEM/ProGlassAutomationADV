// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetSalesAnalyticsTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §33 (cross-module analysis), §12 (reuse profit logic,
//                never invent accounting), §34 (targeted queries, never
//                load the whole database into a model), §21 (read-only),
//                §29/§30 (confidence, no fabrication)
//
// ANSWERS: "How much did we sell this month?"
//          "What gross profit did we make this year?"
//          "Which glass types have the highest margin?"
//
// ─────────────────────────────────────────────────────────────────────
//  IT REUSES THE SHIPPED PROFIT RULE, IT DOES NOT RESTATE IT
// ─────────────────────────────────────────────────────────────────────
//  Net and profit are read exactly the way JobProfitabilityViewModel and
//  GetProfitabilityTool read them, including the rule that when the Step 9
//  snapshot has no usable net, BOTH net and profit fall back to the
//  invoice header — never one from each. Mixing bases is what previously
//  reported a profitable job as a loss.
//
//  Uncosted invoices are COUNTED AND REPORTED, never silently dropped:
//  a revenue total that quietly excludes half the jobs is worse than no
//  total at all.
//
// ─────────────────────────────────────────────────────────────────────
//  PERFORMANCE (§34)
// ─────────────────────────────────────────────────────────────────────
//  Aggregation happens locally over the invoice list and produces a
//  handful of numbers. Nothing here builds a large payload, and no ERP
//  data reaches a reasoning provider through this path.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Interfaces;
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
    /// <summary>Period revenue, profit and margin by glass type.</summary>
    public sealed class GetSalesAnalyticsTool : IAgentTool
    {
        private const int MaxGlassTypes = 6;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_sales_analytics",
            Description =
                "Revenue, cost, profit and margin for a period, and margin by glass type",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.ProformaView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.Invoices == null)
                    return ReadToolHelpers.Unavailable("saved invoices");

                var invoices = await App.Invoices.GetAllAsync().ConfigureAwait(false);
                if (invoices == null || invoices.Count == 0)
                    return ToolResult.Ok("There are no saved invoices yet.");

                var (from, to, label) = ResolvePeriod(term);

                decimal revenue = 0m, cost = 0m, profit = 0m;
                int counted = 0, uncosted = 0;

                // glass type → (cost, profit share) for the margin ranking
                var byType = new Dictionary<string, (decimal Cost, decimal Amount)>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var inv in invoices)
                {
                    if (inv == null) continue;

                    if (inv.IssueDate < from || inv.IssueDate > to) continue;

                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                    var pl = snap?.ProfitLoss;

                    decimal jobCost = (pl?.TotalGlassCost ?? 0m)
                                    + (pl?.TotalProductionCost ?? 0m);

                    bool costed = pl != null
                        && (pl.Rows.Count > 0 || jobCost > 0m || pl.NetExVat > 0m);

                    if (!costed) { uncosted++; continue; }

                    // Same base rule as Job Profitability — see header.
                    decimal snapshotNet = pl!.NetExVat;
                    decimal net;
                    decimal jobProfit;

                    if (snapshotNet > 0m)
                    {
                        net = snapshotNet;
                        jobProfit = pl.Profit;
                    }
                    else
                    {
                        net = Math.Round(inv.GrandTotalVatInclusive / 1.05m, 2);
                        jobProfit = Math.Round(net - jobCost, 2);
                    }

                    revenue += net;
                    cost += jobCost;
                    profit += jobProfit;
                    counted++;

                    // Per-glass-type cost, from the costed rows themselves.
                    foreach (var row in pl.Rows)
                    {
                        if (row == null) continue;
                        if (!string.Equals(row.Kind, "Glass",
                                StringComparison.OrdinalIgnoreCase)) continue;

                        string type = GlassTypeOf(row.SpecName, row.Description);
                        if (type.Length == 0) continue;

                        byType.TryGetValue(type, out var acc);
                        byType[type] = (acc.Cost + row.Amount, acc.Amount + row.Amount);
                    }
                }

                if (counted == 0)
                {
                    return ToolResult.Ok(
                        $"No costed invoices fall in {label}" +
                        (uncosted > 0
                            ? $". {uncosted} invoice(s) in range have no Profit & " +
                              "Loss data — open each one, visit Step 9 to " +
                              "recalculate, then save."
                            : "."));
                }

                var sb = new StringBuilder();
                sb.AppendLine($"{label}:");
                sb.AppendLine($"• Jobs counted: {counted}");
                sb.AppendLine($"• Revenue (net of VAT): AED {revenue:N2}");
                sb.AppendLine($"• Cost: AED {cost:N2}");
                sb.AppendLine($"• Profit: AED {profit:N2}");

                if (revenue > 0m)
                {
                    decimal margin = Math.Round(profit / revenue * 100m, 1);
                    sb.AppendLine($"• Margin: {margin:0.#}%");
                }
                else
                {
                    // Zero revenue with known cost is not a 0% margin.
                    sb.AppendLine(
                        "• Margin: not calculable — no revenue recorded against " +
                        "these jobs.");
                }

                // ── Glass cost concentration ──────────────────────────────
                if (byType.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Glass cost by type:");

                    foreach (var kv in byType.OrderByDescending(k => k.Value.Cost)
                                             .Take(MaxGlassTypes))
                    {
                        decimal share = cost > 0m
                            ? Math.Round(kv.Value.Cost / cost * 100m, 1)
                            : 0m;

                        sb.AppendLine(
                            $"• {kv.Key}: AED {kv.Value.Cost:N2} ({share:0.#}% of cost)");
                    }

                    if (byType.Count > MaxGlassTypes)
                        sb.AppendLine($"  (+{byType.Count - MaxGlassTypes} more types)");

                    // HONESTY: this is cost share, NOT margin per type.
                    // Revenue is recorded per invoice, not per pane, so
                    // profit cannot be attributed to a glass type without
                    // inventing an allocation rule (§12, §30).
                    sb.AppendLine();
                    sb.AppendLine(
                        "That is cost share per glass type. I cannot give margin " +
                        "PER TYPE: revenue is recorded per invoice, not per pane, " +
                        "so splitting profit across types would need an allocation " +
                        "rule the ERP does not define.");
                }

                if (uncosted > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(
                        $"Excluded: {uncosted} invoice(s) in this period have no " +
                        "Profit & Loss data, so they are NOT in the figures above.");
                }

                var provenance = AnswerProvenance.Calculated(
                    "ProformaInvoiceRepository + Step 9 snapshots",
                    "sum of per-job net and profit; margin = profit ÷ revenue × 100");

                string qualifier = provenance.UserQualifier();
                if (qualifier.Length > 0) sb.AppendLine().Append(qualifier);

                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GetSalesAnalyticsTool] {ex.Message}");
                return ReadToolHelpers.Unavailable("sales analytics");
            }
        }

        /// <summary>
        /// Maps free text to a date window. Defaults to the current month,
        /// which is the period people mean when they do not say one.
        /// </summary>
        private static (DateTime From, DateTime To, string Label) ResolvePeriod(
            string term)
        {
            var now = DateTime.Now;
            string t = term.ToLowerInvariant();

            if (t.Contains("last month"))
            {
                var first = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                return (first, first.AddMonths(1).AddTicks(-1),
                    $"Last month ({first:MMMM yyyy})");
            }

            if (t.Contains("this year") || t.Contains("year to date") ||
                t.Contains("ytd"))
            {
                var first = new DateTime(now.Year, 1, 1);
                return (first, now, $"{now.Year} to date");
            }

            if (t.Contains("last year"))
            {
                var first = new DateTime(now.Year - 1, 1, 1);
                return (first, first.AddYears(1).AddTicks(-1), $"{now.Year - 1}");
            }

            if (t.Contains("all time") || t.Contains("overall") ||
                t.Contains("total"))
            {
                return (DateTime.MinValue, DateTime.MaxValue, "All time");
            }

            var monthStart = new DateTime(now.Year, now.Month, 1);
            return (monthStart, now, $"This month ({now:MMMM yyyy})");
        }

        /// <summary>
        /// Extracts a coarse glass type ("6mm Clear") from the spec text.
        /// Returns empty when nothing recognisable is present — an
        /// unlabelled bucket is more misleading than no bucket.
        /// </summary>
        private static string GlassTypeOf(string? specName, string? description)
        {
            string source = !string.IsNullOrWhiteSpace(specName)
                ? specName!
                : (description ?? "");

            if (source.Length == 0) return "";

            var m = System.Text.RegularExpressions.Regex.Match(
                source,
                @"(\d+)\s*mm\s+([^+\d]+?)\s+(?:FT|Annealed|Heat\s+Strengthened|Heat\s+Soaked)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!m.Success) return "";

            return $"{m.Groups[1].Value}mm {m.Groups[2].Value.Trim()}";
        }
    }
}

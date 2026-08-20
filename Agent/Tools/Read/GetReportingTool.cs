// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetReportingTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// PURPOSE:
//   Connects the AI assistant to the SAME centralized reporting layer
//   every report ViewModel uses (ReportingDataService). AI answers real-
//   data questions via one service, not a separate fake AI database.
//
//   Handles:
//     "How many PI were confirmed this month?"
//     "Which confirmed PI have no Job Order?"
//     "Show pending deliveries for ABC company."
//     "Show all rows inside PI-1045."
//     "Find this exact DGU specification."
//     "How many pieces remain?"
//     "Which companies ordered this specification?"
//     "Which Tax Invoices are pending?"
//     "Show profitability for this project."
//     "Was a statement generated for this customer?"
//
// USES: ReportingDataService + repositories via App.* (no raw DbContext).
// Never fabricates. Reports missing data source when cost absent.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation;
using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    public sealed class GetReportingTool : IAgentTool
    {
        private readonly IReportingDataService _reporting;

        public GetReportingTool() : this(App.Reporting ?? new ReportingDataService()) { }
        public GetReportingTool(IReportingDataService reporting) => _reporting = reporting ?? App.Reporting ?? new ReportingDataService();

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_reporting",
            Description = "Query the central reporting layer: PI/JO/DN/TI counts, pending items, spec rows, delivery balances, profitability, and statements. Use for any ERP analytics question.",
            Risk = ToolRisk.Read,
            RequiredPermission = ProGlassAutomation.Services.Audit.AppPermissions.DashboardView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            var query = (argument ?? "").Trim();
            if (query.Length == 0)
                return ToolResult.Fail("Tell me what to report — for example: 'confirmed PI this month' or 'pending deliveries for Apex' or 'spec rows in PI-1045'.");

            try
            {
                var q = query.ToLowerInvariant();
                var sb = new StringBuilder();

                // ── 1. PI questions ──────────────────────────────────────
                if (q.Contains("pi ") || q.Contains("proforma") || q.Contains("quotation"))
                {
                    var filter = new ReportingFilter();
                    if (q.Contains("confirm")) filter.Status = "Approved";
                    else if (q.Contains("draft")) filter.Status = "Draft";
                    else if (q.Contains("reject")) filter.Status = "Rejected";

                    // Date scoping
                    if (q.Contains("this month")) { var n = DateTime.Now; filter.DateFrom = new DateTime(n.Year, n.Month, 1); filter.DateTo = new DateTime(n.Year, n.Month, DateTime.DaysInMonth(n.Year, n.Month)); }
                    else if (q.Contains("today")) { filter.DateFrom = DateTime.Today; filter.DateTo = DateTime.Today; }

                    var summary = await _reporting.GetPiSummaryAsync(filter);
                    sb.AppendLine($"PI summary{(filter.Status != null ? $" (status: {filter.Status})" : "")}:");
                    sb.AppendLine($"• Total: {summary.TotalPi} (AED {summary.TotalValue:N2})");
                    sb.AppendLine($"• Draft {summary.Draft} · Released {summary.Released} · Confirmed {summary.Confirmed} · Rejected {summary.Rejected}");
                    sb.AppendLine($"• Converted to JO: {summary.Converted} · Not converted: {summary.NotConverted}");
                    if (q.Contains("no job") || q.Contains("without job") || q.Contains("not conv"))
                    {
                        var rows = await _reporting.GetPiRowsAsync(new ReportingFilter { Status = "Approved", PageSize = 20 });
                        var pendingJo = rows.Items.Where(r => !r.HasJobOrder).Take(8).ToList();
                        if (pendingJo.Count > 0)
                        {
                            sb.AppendLine("• Confirmed PI without Job Order:");
                            foreach (var r in pendingJo) sb.AppendLine($"  {r.ProformaId} — {r.CustomerName} · AED {r.GrandTotal:N2}");
                        }
                        else sb.AppendLine("• All confirmed PI have a Job Order.");
                    }

                    // If PI id extracted
                    var piId = ExtractId(query, "PI-");
                    if (!string.IsNullOrEmpty(piId))
                    {
                        var detail = await _reporting.GetPiRowsDetailAsync(piId);
                        if (detail.Count > 0)
                        {
                            sb.AppendLine($"• Rows in {piId}: {detail.Count} spec rows, {detail.Sum(r => r.Qty)} pcs, {detail.Sum(r => r.TotalSqm):N3} SQM");
                            foreach (var r in detail.Take(6))
                                sb.AppendLine($"  {r.GlassRef} · {r.SpecName} · {r.W1}×{r.H1} × {r.Qty} — AED {r.Amount:N2}");
                            var rec = await _reporting.GetQuantityReconciliationAsync(piId);
                            sb.AppendLine($"• Delivered {rec.DeliveredQty}/{rec.OrderedQty} ({rec.DeliveryPct:0.#}%) · Remaining {rec.RemainingQty} · Invoiced {rec.InvoicedQty}");
                        }
                    }

                    return ToolResult.Ok(sb.ToString().TrimEnd());
                }

                // ── 2. Delivery / pending ────────────────────────────────
                if (q.Contains("deliver") || q.Contains("pending") && q.Contains("deli"))
                {
                    var customer = ExtractAfter(query, "for ");
                    var filter = new ReportingFilter { Customer = string.IsNullOrWhiteSpace(customer) ? null : customer.Trim() };
                    var summary = await _reporting.GetDeliverySummaryAsync(filter);
                    sb.AppendLine($"Delivery summary{(filter.Customer != null ? $" for {filter.Customer}" : "")}:");
                    sb.AppendLine($"• Rows: {summary.TotalRows} total · Not delivered {summary.NotDelivered} · Partial {summary.Partial} · Fully delivered {summary.FullyDelivered}");
                    sb.AppendLine($"• Ordered {summary.TotalOrderedQty} · Delivered {summary.TotalDeliveredQty} · Remaining {summary.TotalRemainingQty} ({summary.CompletionPct:0.#}%)");
                    if (summary.TotalRemainingQty > 0)
                    {
                        var rows = await _reporting.GetDeliveryRowsAsync(new ReportingFilter { Customer = filter.Customer, PageSize = 6 });
                        var pending = rows.Items.Where(r => r.BalanceQty > 0).Take(5).ToList();
                        if (pending.Count > 0)
                        {
                            sb.AppendLine("• Top pending deliveries:");
                            foreach (var r in pending) sb.AppendLine($"  {r.ProformaId} {r.Specification} (Sr {r.SerialNumber}) — {r.CustomerName} · {r.BalanceQty} pcs remaining");
                        }
                    }
                    return ToolResult.Ok(sb.ToString().TrimEnd());
                }

                // ── 3. Specification search ──────────────────────────────
                if (q.Contains("spec") || q.Contains("glass") || q.Contains("dgu") || q.Contains("sgu") || q.Contains("lam"))
                {
                    var filter = new ReportingFilter { SearchText = query, PageSize = 10 };
                    // Detect glass type tokens
                    if (q.Contains("dgu+lam") || q.Contains("dgu + lam")) filter.GlassType = "DGU+LAM";
                    else if (q.Contains("dgu")) filter.GlassType = "DGU";
                    else if (q.Contains("lam")) filter.GlassType = "LAM";
                    else if (q.Contains("sgu")) filter.GlassType = "SGU";

                    var rows = await _reporting.GetGlassSpecRowsAsync(filter);
                    if (rows.Items.Count == 0)
                        return ToolResult.Ok($"No glass rows matched '{query}' with current filter. Try specifying PI or glass reference.");

                    sb.AppendLine($"Glass rows matching '{query}': {rows.TotalCount} total, showing {rows.Items.Count}");
                    foreach (var r in rows.Items.Take(8))
                        sb.AppendLine($"• {r.ProformaId} Row {r.RowId} — {r.GlassRef} · {r.SpecName} · {r.W1}×{r.H1} × {r.Qty} — {r.CustomerName} ({r.DeliveryStatus})");
                    
                    var aggs = await _reporting.GetSpecAggregationsAsync(new ReportingFilter { SearchText = query, PageSize = 100 });
                    if (aggs.Count > 0)
                    {
                        var top = aggs.First();
                        sb.AppendLine($"• Top exact spec: {top.SpecName} — {top.TotalQty} pcs across {top.DistinctCustomers} customers");
                    }
                    return ToolResult.Ok(sb.ToString().TrimEnd());
                }

                // ── 4. Tax Invoice / pending invoice ─────────────────────
                if (q.Contains("tax") || q.Contains("invoice") && !q.Contains("proforma"))
                {
                    var filter = new ReportingFilter();
                    if (q.Contains("pending"))
                    {
                        var pending = await _reporting.GetPendingInvoicesAsync(filter);
                        sb.AppendLine($"Pending invoices (confirmed PI without TI): {pending.Count}");
                        foreach (var p in pending.Take(6)) sb.AppendLine($"• {p.ProformaId} — {p.CustomerName} · AED {p.GrandTotal:N2}");
                        return ToolResult.Ok(sb.ToString().TrimEnd());
                    }
                    var summary = await _reporting.GetTaxInvoiceSummaryAsync(filter);
                    sb.AppendLine($"Tax Invoice summary:");
                    sb.AppendLine($"• Total {summary.TotalInvoices} (AED {summary.TotalAmount:N2}) · Issued {summary.Issued} · Paid {summary.Paid} · Overdue {summary.Overdue} · Pending invoice {summary.PendingInvoice}");
                    if (q.Contains("overdue") && summary.Overdue > 0)
                    {
                        var rows = await _reporting.GetTaxInvoiceRowsAsync(new ReportingFilter { Status = "Overdue", PageSize = 6 });
                        foreach (var r in rows.Items) sb.AppendLine($"  {r.InvoiceNumber} — {r.CustomerName} · AED {r.GrandTotal:N2} due {r.DueDate:dd MMM yyyy}");
                    }
                    return ToolResult.Ok(sb.ToString().TrimEnd());
                }

                // ── 5. Profitability ─────────────────────────────────────
                if (q.Contains("profit") || q.Contains("margin") || q.Contains("p&l") || q.Contains("cost"))
                {
                    var project = ExtractAfter(query, "for ") ?? ExtractAfter(query, "project ");
                    var filter = new ReportingFilter { Project = string.IsNullOrWhiteSpace(project) ? null : project };
                    var summary = await _reporting.GetProfitabilitySummaryAsync(filter);
                    sb.AppendLine($"Profitability{(filter.Project != null ? $" for {filter.Project}" : "")}:");
                    sb.AppendLine($"• Revenue AED {summary.TotalRevenue:N2} · Cost AED {summary.TotalCost:N2} · Profit AED {summary.TotalProfit:N2} · Margin {summary.AvgMargin:0.0}%");
                    sb.AppendLine($"• With cost: {summary.InvoicesWithCost} · Missing cost: {summary.InvoicesWithoutCost}");
                    if (summary.InvoicesWithoutCost > 0) sb.AppendLine("  (Missing cost means Step 9 Profit & Loss was not saved for those PIs — open the PI and save Step 9 to populate.)");
                    var rows = await _reporting.GetProfitabilityRowsAsync(new ReportingFilter { Project = filter.Project, PageSize = 6 });
                    foreach (var r in rows.Items.Take(5)) sb.AppendLine($"  {r.Name} — Revenue AED {r.Revenue:N2} · Profit AED {r.Profit:N2} ({r.Margin:0.0}%){(r.HasCostData ? "" : " [no cost data]")}");
                    return ToolResult.Ok(sb.ToString().TrimEnd());
                }

                // ── 6. Statement ─────────────────────────────────────────
                if (q.Contains("statement"))
                {
                    var customer = ExtractAfter(query, "for ") ?? ExtractAfter(query, "customer ");
                    var filter = new ReportingFilter { Customer = string.IsNullOrWhiteSpace(customer) ? null : customer };
                    var rows = await _reporting.GetStatementRowsAsync(filter);
                    if (rows.Items.Count == 0)
                        return ToolResult.Ok($"No statements found{(filter.Customer != null ? $" for {filter.Customer}" : "")}. Generate one via Reports → Statement → New Statement.");
                    sb.AppendLine($"Statements{(filter.Customer != null ? $" for {filter.Customer}" : "")}: {rows.TotalCount} total");
                    foreach (var r in rows.Items.Take(6)) sb.AppendLine($"• {r.StatementNumber} — {r.CustomerName} · {r.InvoiceCount} invoice(s) · AED {r.TotalInvoiced:N2} invoiced · {r.CreatedAt:dd MMM yyyy}");
                    return ToolResult.Ok(sb.ToString().TrimEnd());
                }

                // ── 7. Generic sales reporting fallback ────────────────────
                var piSummary = await _reporting.GetPiSummaryAsync(new ReportingFilter());
                var joSummary = await _reporting.GetJoSummaryAsync(new ReportingFilter());
                var dnSummary = await _reporting.GetDeliverySummaryAsync(new ReportingFilter());
                var tiSummary = await _reporting.GetTaxInvoiceSummaryAsync(new ReportingFilter());
                sb.AppendLine("ERP overview (live):");
                sb.AppendLine($"• PI: {piSummary.TotalPi} total (Confirmed {piSummary.Confirmed}, Pending {piSummary.Pending}) · AED {piSummary.TotalValue:N2}");
                sb.AppendLine($"• JO: {joSummary.TotalJo} total · {joSummary.AwaitingJo} PI awaiting JO");
                sb.AppendLine($"• DN rows: {dnSummary.TotalRows} total · {dnSummary.TotalDeliveredQty}/{dnSummary.TotalOrderedQty} pcs delivered ({dnSummary.CompletionPct:0.#}%)");
                sb.AppendLine($"• TI: {tiSummary.TotalInvoices} · AED {tiSummary.TotalAmount:N2}");
                sb.AppendLine("Ask specifically: e.g. 'pending deliveries for Apex', 'rows in PI-1045', 'profit for Tower A', 'companies ordered DGU 6+12+6'.");
                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GetReportingTool] {ex.Message}");
                return ToolResult.Fail("I could not read reporting data, so I am not going to guess. Check that the databases are reachable.");
            }
        }

        private static string? ExtractId(string text, string prefix)
        {
            var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var sub = text.Substring(idx).Trim();
            var parts = sub.Split(new[] { ' ', ',', ';', '\n', '\r', '"' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.FirstOrDefault(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static string? ExtractAfter(string text, string marker)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var after = text.Substring(idx + marker.Length).Trim().TrimEnd('?', '.', '!');
            if (after.Length == 0) return null;
            // Take next 2-3 words as entity
            var words = after.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Take(3));
        }
    }
}

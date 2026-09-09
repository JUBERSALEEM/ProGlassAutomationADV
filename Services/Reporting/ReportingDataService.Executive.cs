// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Reporting/ReportingDataService.Executive.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — advanced reporting, slice 5
//
//  Assembles the cross-module executive summary from the EXISTING
//  per-module summaries, all under one shared filter.
//
//  WHY COMPOSE RATHER THAN QUERY
//  ---------------------------------------------------------------------
//  Every number here already has an owner: GetPiSummaryAsync,
//  GetJoSummaryAsync, GetDeliverySummaryAsync and so on. Re-querying them
//  with slightly different SQL would guarantee that the executive page
//  eventually disagrees with the module report it summarises — and an
//  executive page that contradicts the detail is worse than none, because
//  it gets believed.
//
//  Composing also means every future fix to a module summary flows here
//  automatically.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public sealed partial class ReportingDataService : IReportingDataService
    {
        /// <summary>
        /// A step converting below this rate is flagged as a bottleneck.
        /// Deliberately generous: a warning that fires on every row stops
        /// being a warning.
        /// </summary>
        private const decimal BottleneckThresholdPct = 60m;

        public async Task<ExecutiveSummary> GetExecutiveSummaryAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var result = new ExecutiveSummary
            {
                PeriodDescription = DescribePeriod(filter)
            };

            try
            {
                // Every stage under the SAME filter — that is what keeps the
                // executive page reconciled with the module reports.
                var pi = await GetPiSummaryAsync(filter, ct).ConfigureAwait(false);
                var jo = await GetJoSummaryAsync(filter, ct).ConfigureAwait(false);
                var dn = await GetDeliverySummaryAsync(filter, ct).ConfigureAwait(false);
                var ti = await GetTaxInvoiceSummaryAsync(filter, ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                BuildFunnel(result, pi, jo, dn, ti);

                result.QuotedValue = pi?.TotalValue ?? 0m;
                result.InvoicedValue = ti?.TotalAmount ?? 0m;
                // Balance due across tax invoices in the period. Previously
                // left at 0 (the executive page showed no receivables at all);
                // the TI summary already carries the figure the TaxInvoice
                // report shows, so the two now reconcile.
                result.OutstandingValue = ti?.Outstanding ?? 0m;
                result.UnconvertedQuotes = pi?.NotConverted ?? 0;
                result.UninvoicedConfirmed = ti?.PendingInvoice ?? 0;
                result.OverdueInvoices = ti?.Overdue ?? 0;
                result.DeliveryCompletionPct = dn?.CompletionPct ?? 0m;

                // Profitability and mix are best-effort enrichments: a
                // failure in either must leave the funnel intact, since the
                // funnel is the reason the page exists.
                await AddProfitabilityAsync(result, filter, ct).ConfigureAwait(false);
                await AddMixAsync(result, filter, ct).ConfigureAwait(false);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logging.AppLogService.Warning(
                    $"Executive summary failed: {ex.Message}",
                    new Logging.LogContext
                    {
                        Module = "Reporting",
                        Class = nameof(ReportingDataService),
                        Operation = "GetExecutiveSummary"
                    });

                return result;
            }
        }

        private static void BuildFunnel(
            ExecutiveSummary result,
            PiReportSummary? pi,
            JoReportSummary? jo,
            DeliveryReportSummary? dn,
            TaxInvoiceReportSummary? ti)
        {
            var stages = new List<FunnelStage>
            {
                new()
                {
                    Label = "Quoted (Proforma)",
                    Count = pi?.TotalPi ?? 0,
                    Value = pi?.TotalValue ?? 0m
                },
                new()
                {
                    Label = "In production (Job Order)",
                    Count = jo?.TotalJo ?? 0,
                    Value = jo?.TotalValue ?? 0m
                },
                new()
                {
                    // Rows fully delivered, not rows touched — a partially
                    // delivered row has not converted to cash yet.
                    Label = "Delivered",
                    Count = dn?.FullyDelivered ?? 0,
                    Value = 0m
                },
                new()
                {
                    Label = "Invoiced (Tax Invoice)",
                    Count = ti?.TotalInvoices ?? 0,
                    Value = ti?.TotalAmount ?? 0m
                },
                new()
                {
                    Label = "Paid",
                    Count = ti?.Paid ?? 0,
                    Value = 0m
                }
            };

            decimal first = stages[0].Count;

            for (int i = 0; i < stages.Count; i++)
            {
                var s = stages[i];

                s.SharePct = first <= 0m
                    ? 0m
                    : Math.Round(s.Count / first * 100m, 1);

                if (i == 0)
                {
                    s.StepConversionPct = 100m;
                    continue;
                }

                decimal prev = stages[i - 1].Count;
                s.StepConversionPct = prev <= 0m
                    ? 0m
                    : Math.Round(s.Count / prev * 100m, 1);

                // Only flag when the previous stage actually had volume;
                // 0 -> 0 is not a bottleneck, it is an empty period.
                s.IsBottleneck = prev > 0m
                                 && s.StepConversionPct < BottleneckThresholdPct;
            }

            result.Funnel = stages;
        }

        private async Task AddProfitabilityAsync(
            ExecutiveSummary result, ReportingFilter filter, CancellationToken ct)
        {
            try
            {
                var p = await GetProfitabilitySummaryAsync(filter, ct)
                    .ConfigureAwait(false);
                if (p == null) return;

                result.Revenue = p.TotalRevenue;
                result.Cost = p.TotalCost;

                // Surfaced, not buried: a margin computed from half the
                // invoices looks authoritative and misleads.
                result.InvoicesWithoutCost = p.InvoicesWithoutCost;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logging.AppLogService.Debug($"[Executive.Profitability] {ex.Message}");
            }
        }

        private async Task AddMixAsync(
            ExecutiveSummary result, ReportingFilter filter, CancellationToken ct)
        {
            try
            {
                var jo = await GetJoAnalyticsAsync(filter, ct).ConfigureAwait(false);
                if (jo != null)
                {
                    result.TopSpecifications = jo.TopSpecifications;
                    result.TopCustomers = jo.TopCustomers;
                    result.DominantSpecification = jo.DominantSpecification;
                    result.DominantColour = jo.DominantColour;
                }

                var dn = await GetDeliveryAnalyticsAsync(filter, ct).ConfigureAwait(false);
                if (dn != null)
                {
                    result.CriticalPendingQty = dn.CriticalPendingQty;
                    // OutstandingValue is set from the tax-invoice summary in
                    // the main method (delivery analytics carries no money).
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logging.AppLogService.Debug($"[Executive.Mix] {ex.Message}");
            }
        }

        /// <summary>
        /// Human-readable filter description. Printed on the report because
        /// a summary without its criteria stated is unauditable — the
        /// reader cannot tell what the numbers cover.
        /// </summary>
        private static string DescribePeriod(ReportingFilter f)
        {
            var parts = new List<string>();

            if (f.DateFrom.HasValue && f.DateTo.HasValue)
                parts.Add($"{f.DateFrom.Value:dd-MMM-yyyy} to {f.DateTo.Value:dd-MMM-yyyy}");
            else if (f.DateFrom.HasValue)
                parts.Add($"from {f.DateFrom.Value:dd-MMM-yyyy}");
            else if (f.DateTo.HasValue)
                parts.Add($"up to {f.DateTo.Value:dd-MMM-yyyy}");
            else
                parts.Add("All time");

            if (!string.IsNullOrWhiteSpace(f.Company)) parts.Add($"company: {f.Company}");
            if (!string.IsNullOrWhiteSpace(f.Customer)) parts.Add($"customer: {f.Customer}");
            if (!string.IsNullOrWhiteSpace(f.Salesman)) parts.Add($"salesman: {f.Salesman}");
            if (!string.IsNullOrWhiteSpace(f.Project)) parts.Add($"project: {f.Project}");
            if (!string.IsNullOrWhiteSpace(f.GlassType)) parts.Add($"glass: {f.GlassType}");

            return string.Join("  ·  ", parts);
        }
    }
}

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

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using ProGlassAutomation.Models.TaxInvoice.Common.TaxInvoiceStatus;
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
                var dn = await GetDeliverySummaryAsync(filter, ct).ConfigureAwait(false);
                var ti = await GetTaxInvoiceSummaryAsync(filter, ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                // Cohort milestone funnel — see method docs. (2026-09-10)
                await BuildCohortFunnelAsync(result, filter, pi, ti, ct)
                    .ConfigureAwait(false);

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

        // ═════════════════════════════════════════════════════════════════
        //  THE COHORT FUNNEL (2026-09-10 rewrite — the percentages were
        //  wrong before this)
        // ═════════════════════════════════════════════════════════════════
        //  The old funnel mixed UNITS per stage: quotes (documents) vs job
        //  orders (documents, but with an open-ended date filter that kept
        //  every undelivered JO ever) vs DELIVERY SPEC ROWS vs tax
        //  invoices. A quote has many spec rows, so "Delivered" routinely
        //  outnumbered "Quoted" and the step conversions exceeded 100%.
        //  A percentage between two different units is not an estimate of
        //  anything — it is noise.
        //
        //  The fix is a true COHORT MILESTONE funnel. Stage 1 is the set of
        //  quotes in the period; EVERY later stage counts how many of THOSE
        //  SAME QUOTES reached the milestone:
        //
        //      Quoted → became a job order → fully delivered → invoiced →
        //      paid
        //
        //  Every count is a distinct-ProformaId count, so units can never
        //  mix again and every rate is a real "share of quotes". The dates
        //  on downstream stages are intentionally ignored (ever-happened
        //  semantics): the question is what became of this period's quotes,
        //  not when each event happened. All filters flow through
        //  ApplyPiFilters / ApplyTiFilters so the funnel, the module
        //  reports and the KPI cards cannot disagree.
        // ═════════════════════════════════════════════════════════════════
        private async Task BuildCohortFunnelAsync(
            ExecutiveSummary result,
            ReportingFilter filter,
            PiReportSummary? pi,
            TaxInvoiceReportSummary? ti,
            CancellationToken ct)
        {
            int quoted = pi?.TotalPi ?? 0;

            // ── The cohort: quotes in the period (PI summary's own filter) ──
            var piIds = new List<string>();
            if (quoted > 0)
            {
                using var piDb = new ProformaInvoicesDbContext();
                piIds = await ApplyPiFilters(
                        Live(piDb.ProformaInvoices.AsNoTracking()).AsQueryable(), filter)
                    .Select(i => i.ProformaId)
                    .ToListAsync(ct).ConfigureAwait(false);
            }

            // ── Stage 2: cohort quotes that became job orders ───────────────
            int inProduction = 0;
            if (piIds.Count > 0)
            {
                using var joDb = new JobOrdersDbContext();
                inProduction = await Live(joDb.JobOrders.AsNoTracking())
                    .Where(j => j.SourceProformaId != null && piIds.Contains(j.SourceProformaId))
                    .Select(j => j.SourceProformaId)
                    .Distinct()
                    .CountAsync(ct).ConfigureAwait(false);
            }

            // ── Stage 3: cohort quotes FULLY delivered ──────────────────────
            // Row predicates mirror BuildDeliveryRowQuery (non-void parents,
            // no phantom rows, real quantities) so this stage can never
            // disagree with the Delivery report about what a completed row
            // is. A quote counts when EVERY one of its spec rows is
            // complete — a partially delivered quote has not converted to
            // cash yet.
            int delivered = 0;
            if (piIds.Count > 0)
            {
                using var dnDb = new DeliveryNotesDbContext();
                var rows = await dnDb.DeliveryNoteItems.AsNoTracking()
                    .Where(i => i.DeliveryNote != null
                                && !i.DeliveryNote.IsVoid
                                && !string.IsNullOrWhiteSpace(i.JobOrderId)
                                && i.DeliveryNote.ProformaId != null
                                && piIds.Contains(i.DeliveryNote.ProformaId)
                                && !(string.IsNullOrWhiteSpace(i.GlassRef) && i.W1 == 0 && i.H1 == 0)
                                && (i.OrderedQty > 0 || i.DeliveredQty > 0))
                    .GroupBy(i => new { i.DeliveryNote!.ProformaId, i.SpecGroupNumber, i.SerialNumber })
                    .Select(g => new FunnelRowAgg
                    {
                        ProformaId = g.Key.ProformaId ?? "",
                        TotalQty = g.Max(i => i.OrderedQty),
                        DeliveredQty = g.Sum(i => i.DeliveryNote!.IsMasterDn ? 0 : i.DeliveredQty)
                    })
                    .ToListAsync(ct).ConfigureAwait(false);

                delivered = rows
                    .GroupBy(r => r.ProformaId)
                    .Count(g => g.All(r => r.TotalQty > 0 && r.DeliveredQty >= r.TotalQty));
            }

            // ── Stages 4 + 5: cohort quotes invoiced / paid ─────────────────
            int invoiced = 0;
            int paid = 0;
            decimal invoicedValue = 0m;
            if (piIds.Count > 0)
            {
                using var tiDb = new TaxInvoiceDbContext();
                var pairs = await ApplyTiFilters(
                        Live(tiDb.TaxInvoices.AsNoTracking()).AsQueryable(), filter)
                    .Where(t => piIds.Contains(t.ProformaId))
                    .Select(t => new { t.ProformaId, t.Status, t.GrandTotal })
                    .ToListAsync(ct).ConfigureAwait(false);

                invoiced = pairs.Select(p => p.ProformaId).Distinct().Count();
                paid = pairs
                    .Where(p => TaxInvoiceStatusValues.IsPaid(p.Status))
                    .Select(p => p.ProformaId)
                    .Distinct()
                    .Count();
                invoicedValue = pairs.Sum(p => p.GrandTotal);
            }

            var stages = new List<FunnelStage>
            {
                new()
                {
                    Label = "Quoted (Proforma)",
                    Count = quoted,
                    Value = pi?.TotalValue ?? 0m
                },
                new()
                {
                    Label = "In production (Job Order)",
                    Count = inProduction,
                    Value = 0m
                },
                new()
                {
                    Label = "Delivered (full order)",
                    Count = delivered,
                    Value = 0m
                },
                new()
                {
                    Label = "Invoiced (Tax Invoice)",
                    Count = invoiced,
                    Value = invoicedValue
                },
                new()
                {
                    Label = "Paid",
                    Count = paid,
                    Value = 0m
                }
            };

            decimal first = stages[0].Count;

            for (int i = 0; i < stages.Count; i++)
            {
                var s = stages[i];
                s.FirstStageCount = (int)first;

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

        /// <summary>Minimal per-spec-row aggregate for the delivered stage.</summary>
        private sealed class FunnelRowAgg
        {
            public string ProformaId { get; set; } = "";
            public int TotalQty { get; set; }
            public int DeliveredQty { get; set; }
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

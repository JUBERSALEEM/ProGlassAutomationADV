// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Reporting/ReportingDataService.Analytics.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — advanced reporting, slice 1
//
//  Wires the pure aggregation in ReportingAnalyticsBuilder to the existing
//  filtered row queries.
//
//  ANALYTICS MUST COVER THE WHOLE FILTERED SET, NOT ONE PAGE.
//  ---------------------------------------------------------------------
//  GetJoRowsAsync / GetDeliveryRowsAsync are paged for the grid (default
//  100 rows). Feeding a page into a Pareto would produce "top
//  specifications on page 1", which is meaningless and — worse — looks
//  authoritative. These methods therefore request a large page explicitly.
//
//  A hard ceiling is applied rather than pulling everything unbounded: a
//  report that hangs the UI on a five-year range is a worse failure than
//  one that analyses the most recent slice and says so.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public sealed partial class ReportingDataService : IReportingDataService
    {
        /// <summary>
        /// Upper bound on rows pulled for analytics. Matches the DoS guard
        /// already used by the VAT report, so the two behave consistently.
        /// </summary>
        private const int AnalyticsMaxRows = 10000;

        public async Task<JoAnalyticsSummary> GetJoAnalyticsAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();

            try
            {
                // Clone so raising the page size cannot leak back into the
                // caller's filter and silently change their grid paging.
                var wide = CloneForAnalytics(filter);

                var jobOrders = await GetJoRowsAsync(wide, ct).ConfigureAwait(false);

                // Spec/colour mix needs per-item glass detail, which a
                // JoReportRow does not carry. The delivery row set is the
                // item-level view of the same filtered work, so it is the
                // correct source for that half of the analysis.
                var items = await GetDeliveryRowsAsync(wide, ct).ConfigureAwait(false);

                return ReportingAnalyticsBuilder.BuildJoAnalytics(
                    jobOrders?.Items, items?.Items);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Analytics is an enhancement to the report, never its
                // gatekeeper: a failure here must not blank the detail
                // table the user came for.
                Logging.AppLogService.Warning(
                    $"Job Order analytics failed: {ex.Message}",
                    new Logging.LogContext
                    {
                        Module = "Reporting",
                        Class = nameof(ReportingDataService),
                        Operation = "GetJoAnalytics"
                    });

                return new JoAnalyticsSummary();
            }
        }

        public async Task<DeliveryAnalyticsSummary> GetDeliveryAnalyticsAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();

            try
            {
                var wide = CloneForAnalytics(filter);
                var rows = await GetDeliveryRowsAsync(wide, ct).ConfigureAwait(false);

                return ReportingAnalyticsBuilder.BuildDeliveryAnalytics(
                    rows?.Items, DateTime.Now);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logging.AppLogService.Warning(
                    $"Delivery analytics failed: {ex.Message}",
                    new Logging.LogContext
                    {
                        Module = "Reporting",
                        Class = nameof(ReportingDataService),
                        Operation = "GetDeliveryAnalytics"
                    });

                return new DeliveryAnalyticsSummary();
            }
        }

        /// <summary>
        /// Copies a filter and widens its paging so analytics sees the full
        /// filtered set. Every other criterion is preserved verbatim —
        /// that is what keeps the analysis reconciled to the detail table.
        /// </summary>
        private static ReportingFilter CloneForAnalytics(ReportingFilter source)
        {
            return new ReportingFilter
            {
                DateFrom = source.DateFrom,
                DateTo = source.DateTo,
                Company = source.Company,
                Customer = source.Customer,
                Salesman = source.Salesman,
                Project = source.Project,
                ProformaId = source.ProformaId,
                JobOrderId = source.JobOrderId,
                DeliveryNoteId = source.DeliveryNoteId,
                TaxInvoiceNumber = source.TaxInvoiceNumber,
                Status = source.Status,
                GlassType = source.GlassType,
                Specification = source.Specification,
                Colour = source.Colour,
                Thickness = source.Thickness,
                GlassRef = source.GlassRef,
                SearchText = source.SearchText,

                Page = 1,
                PageSize = AnalyticsMaxRows
            };
        }
    }
}

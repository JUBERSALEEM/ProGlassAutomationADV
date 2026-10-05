// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/Core/IDashboardMetricsService.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.Core
//
// PURPOSE:
//   The dashboard service boundary the architecture was missing:
//
//     View → ViewModel → IDashboardMetricsService → DbContext → SQLite
//
//   A ViewModel asks for aggregates and receives small DTOs. It never
//   receives EF entities, so it cannot hold a table in memory or reach
//   a FullSnapshotJson blob.
//
// EVERY METHOD:
//   • takes a DashboardDateRange so the period is explicit
//   • takes a CancellationToken so navigating away cancels the work
//   • returns an aggregate DTO, never an entity
// ═══════════════════════════════════════════════════════════════════════

using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Dashboard.Core
{
    public interface IDashboardMetricsService
    {
        Task<SalesMetrics> GetSalesMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default);

        Task<ProductionMetrics> GetProductionMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default);

        Task<DeliveryMetrics> GetDeliveryMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default);

        Task<InventoryMetrics> GetInventoryMetricsAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Everything the Overview page needs. Runs the four queries
        /// above and composes them, so Overview makes one service call
        /// instead of four full-table loads.
        /// </summary>
        Task<ExecutiveMetrics> GetExecutiveMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default);

        /// <summary>
        /// Newest rows from proforma, job order and delivery note, for
        /// the activity feed. Bounded by <paramref name="limit"/> per
        /// source so the cost does not grow with table size.
        /// </summary>
        Task<RecentActivityRows> GetRecentRowsAsync(
            int limit = 20, CancellationToken ct = default);

        /// <summary>
        /// Statement dashboard aggregates plus the newest statements.
        ///
        /// <para>
        /// Added 2026-09-08 for Phase 3: the Statement dashboard was the only
        /// one of the four with no metrics coverage, so it loaded the whole
        /// table into its ViewModel and summed it there.
        /// </para>
        ///
        /// <para>
        /// The sums are computed in memory, not in SQL, and that is not an
        /// oversight — <c>SavedStatement.TotalInvoiced</c>,
        /// <c>TotalOutstanding</c> and <c>OverdueAmount</c> are mapped
        /// <c>[Column(TypeName = "TEXT")]</c>, so a server-side SUM would not
        /// be a decimal sum. Pushing this into SQL needs a migration to a
        /// numeric column first.
        /// </para>
        /// </summary>
        Task<StatementMetrics> GetStatementMetricsAsync(
            int recentLimit = 5, CancellationToken ct = default);
    }
}

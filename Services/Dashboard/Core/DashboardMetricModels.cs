// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/Core/DashboardMetricModels.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.Core
//
// PURPOSE:
//   Aggregate DTOs returned by the dashboard metric services.
//
//   These are the ONLY shapes that cross the service boundary. A
//   dashboard ViewModel receives a small, already-aggregated object and
//   never sees an EF entity, so it cannot accidentally hold a whole
//   table (or a FullSnapshotJson blob) alive in memory.
//
// WHY THIS EXISTS
//   Six of the seven dashboards called repository GetAllAsync() and
//   aggregated in memory. Opening the Overview page loaded four entire
//   tables — Invoices, Sheets, Jobs and Delivery Notes — including the
//   full invoice snapshot JSON on every proforma row, purely to produce
//   a handful of counts and sums.
//
//   Services/Dashboard/TaxInvoiceMetrics already demonstrated the right
//   pattern (AsNoTracking + Select projection). These models extend that
//   pattern to the remaining modules.
//
// DESIGN NOTES
//   • Plain POCOs. No INotifyPropertyChanged — the owning ViewModel is
//     responsible for reactivity, matching KpiCardModel's existing
//     convention.
//   • Defaults are all zero/empty so a service can return a valid empty
//     result without null checks at every call site.
//   • Nothing here formats for display. Formatting stays in the
//     ViewModel so culture and layout concerns do not leak into the
//     service layer.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Dashboard.Core
{
    /// <summary>
    /// A single value plus the equivalent figure for the preceding period
    /// of the same length, so a KPI can show "vs previous" without every
    /// widget inventing its own comparison window.
    /// </summary>
    public sealed class MetricDelta
    {
        public decimal Current { get; set; }
        public decimal Previous { get; set; }

        /// <summary>
        /// Percentage change against the previous period.
        ///
        /// Returns null when Previous is zero: a jump from nothing to
        /// something is not "+100%", it is undefined, and rendering an
        /// invented percentage there would be exactly the fabricated
        /// figure the brief forbids. Callers show "—" for null.
        /// </summary>
        public double? ChangePercent
        {
            get
            {
                if (Previous == 0m) return null;
                return (double)((Current - Previous) / Math.Abs(Previous)) * 100d;
            }
        }

        /// <summary>True when the current period is above the previous one.</summary>
        public bool IsUp => Current > Previous;

        /// <summary>
        /// True when there is no meaningful comparison to draw, either
        /// because the previous period had no activity or because both
        /// periods are empty.
        /// </summary>
        public bool HasComparison => Previous != 0m || Current != 0m;
    }

    // ═════════════════════════════════════════════════════════════════
    //  SALES
    // ═════════════════════════════════════════════════════════════════

    public sealed class SalesMetrics
    {
        public decimal TotalSalesValue { get; set; }
        public MetricDelta SalesThisPeriod { get; set; } = new();

        public int TotalInvoices { get; set; }
        public int ApprovedInvoices { get; set; }
        public int DraftInvoices { get; set; }

        /// <summary>
        /// Approved as a share of all invoices. Null when there are no
        /// invoices at all, so the UI shows "no data" rather than 0%.
        /// </summary>
        public double? ApprovalRatePercent { get; set; }

        public List<NamedValue> StatusBreakdown { get; set; } = new();
        public List<NamedValue> TopCustomers { get; set; } = new();
        public List<TimeBucket> MonthlyTrend { get; set; } = new();
    }

    // ═════════════════════════════════════════════════════════════════
    //  PRODUCTION
    // ═════════════════════════════════════════════════════════════════

    public sealed class ProductionMetrics
    {
        public int TotalJobs { get; set; }
        public int InProduction { get; set; }
        public int Completed { get; set; }
        public int Overdue { get; set; }
        public MetricDelta CompletedThisPeriod { get; set; } = new();

        public List<NamedValue> StatusBreakdown { get; set; } = new();
        public List<NamedValue> PriorityBreakdown { get; set; } = new();
        public List<TimeBucket> CompletionTrend { get; set; } = new();
    }

    // ═════════════════════════════════════════════════════════════════
    //  DELIVERY
    // ═════════════════════════════════════════════════════════════════

    public sealed class DeliveryMetrics
    {
        public int TotalNotes { get; set; }
        public int Preparing { get; set; }
        public int Dispatched { get; set; }

        /// <summary>Pieces (not notes) delivered today.</summary>
        public int PiecesDeliveredToday { get; set; }

        /// <summary>
        /// Lifetime pieces delivered, excluding drafts.
        ///
        /// Summed from TotalDeliveredQty on each note. Every note stores
        /// the quantity delivered BY THAT NOTE, so this sum is correct.
        /// TotalBalanceQty must NOT be summed the same way — see
        /// PendingPieces.
        /// </summary>
        public int PiecesDeliveredTotal { get; set; }

        /// <summary>
        /// Outstanding pieces across all jobs.
        ///
        /// Deliberately NOT a sum of TotalBalanceQty over notes. Each note
        /// holds a point-in-time snapshot of the remaining balance, so
        /// adding them double-counts the same outstanding pieces once per
        /// note. This is sourced from the delivery repository's
        /// per-job aggregation instead.
        /// </summary>
        public int PendingPieces { get; set; }

        public List<NamedValue> StatusBreakdown { get; set; } = new();
        public List<NamedValue> TypeBreakdown { get; set; } = new();
    }

    // ═════════════════════════════════════════════════════════════════
    //  INVENTORY
    // ═════════════════════════════════════════════════════════════════

    public sealed class InventoryMetrics
    {
        /// <summary>Value of remaining stock at selling price.</summary>
        public decimal StockValueAtSellPrice { get; set; }

        /// <summary>Value of remaining stock at purchase price.</summary>
        public decimal StockValueAtCost { get; set; }

        public int SheetTypes { get; set; }
        public int TotalSheetsAvailable { get; set; }
        public decimal TotalSqmAvailable { get; set; }

        /// <summary>Sheet types at or below the low-stock threshold.</summary>
        public int LowStockCount { get; set; }

        /// <summary>Sheet types with no stock left at all.</summary>
        public int OutOfStockCount { get; set; }

        public List<NamedValue> CategoryBreakdown { get; set; } = new();
        public List<LowStockItem> LowStockItems { get; set; } = new();
    }

    public sealed class LowStockItem
    {
        public string Description { get; set; } = string.Empty;
        public int Balance { get; set; }
        public bool IsOutOfStock { get; set; }
    }

    // ═════════════════════════════════════════════════════════════════
    //  EXECUTIVE ROLL-UP
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Everything the Overview page needs, in one object.
    ///
    /// Composed from the four services above so the Overview ViewModel
    /// makes a single call rather than four full-table loads.
    /// </summary>
    public sealed class ExecutiveMetrics
    {
        public SalesMetrics Sales { get; set; } = new();
        public ProductionMetrics Production { get; set; } = new();
        public DeliveryMetrics Delivery { get; set; } = new();
        public InventoryMetrics Inventory { get; set; } = new();
    }

    // ═════════════════════════════════════════════════════════════════
    //  SHARED SHAPES
    // ═════════════════════════════════════════════════════════════════

    /// <summary>A label with a numeric value — one slice, bar or row.</summary>
    public sealed class NamedValue
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int Count { get; set; }
    }

    // ═════════════════════════════════════════════════════════════════
    //  RECENT ROWS (activity feed)
    //
    //  The feed needs individual records, not aggregates, but it only
    //  ever displays the newest handful. These projections carry just
    //  the columns the feed renders -- no JSON snapshot column -- and
    //  the query is bounded with Take(), so a table with 50,000 rows
    //  costs the same as one with 50.
    // ═════════════════════════════════════════════════════════════════

    public sealed class RecentInvoiceRow
    {
        public string ProformaId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal GrandTotalVatInclusive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class RecentJobRow
    {
        public string JobOrderId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public decimal GrandTotalVatInclusive { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class RecentDeliveryRow
    {
        public string DeliveryNoteId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public int TotalDeliveredQty { get; set; }
    }

    /// <summary>The three recent-row sets the activity feed merges.</summary>
    public sealed class RecentActivityRows
    {
        public List<RecentInvoiceRow> Invoices { get; set; } = new();
        public List<RecentJobRow> Jobs { get; set; } = new();
        public List<RecentDeliveryRow> DeliveryNotes { get; set; } = new();
    }

    /// <summary>One point on a time series.</summary>
    public sealed class TimeBucket
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int Count { get; set; }
    }
}

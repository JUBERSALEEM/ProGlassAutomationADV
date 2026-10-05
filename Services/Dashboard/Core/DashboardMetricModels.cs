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

using ProGlassAutomation.Services.Statement.Repository;
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

        /// <summary>
        /// Newest jobs for the dashboard's activity feed, newest first.
        ///
        /// Added 2026-09-08 for Phase 3: the Job Order dashboard loaded every
        /// job into its ViewModel to render ten feed rows. Bounded here with
        /// Take() so a 50,000-job table costs the same as a 50-job one.
        /// </summary>
        public List<RecentJobRow> RecentJobs { get; set; } = new();
    }

    // ═════════════════════════════════════════════════════════════════
    //  DELIVERY
    // ═════════════════════════════════════════════════════════════════

    public sealed class DeliveryMetrics
    {
        public int TotalNotes { get; set; }
        public int Preparing { get; set; }
        public int Dispatched { get; set; }

        /// <summary>
        /// Notes currently in Dispatched status only — not Delivered /
        /// Signed / Received. Pipeline "in flight" uses this plus
        /// Preparing. <see cref="Dispatched"/> remains the lifetime
        /// left-the-building count the Delivery tiles already show.
        /// </summary>
        public int CurrentlyDispatched { get; set; }

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

        /// <summary>
        /// False when the per-job remaining query failed. Zero is then
        /// unknown, not "nothing pending".
        /// </summary>
        public bool PendingPiecesAvailable { get; set; } = true;

        /// <summary>
        /// COUNT of notes delivered today — not to be confused with
        /// <see cref="PiecesDeliveredToday"/>, which counts pieces.
        ///
        /// The Delivery Note dashboard's "Delivered Today" card has always
        /// counted notes. Migrating it onto this service without this field
        /// would have silently changed the card from a note count to a piece
        /// count — the same figure, two units, one label.
        /// </summary>
        public int NotesDeliveredToday { get; set; }

        /// <summary>
        /// Notes whose type is Rework or Return.
        ///
        /// Kept as a first-class count rather than derived from
        /// <see cref="TypeBreakdown"/>, because the breakdown groups on a
        /// trimmed, case-insensitive key with a "Standard" fallback while the
        /// dashboard matched the stored string exactly. Deriving one from the
        /// other would quietly change the number for any row with stray
        /// whitespace.
        /// </summary>
        public int ReworkReturned { get; set; }

        public List<NamedValue> StatusBreakdown { get; set; } = new();
        public List<NamedValue> TypeBreakdown { get; set; } = new();

        /// <summary>
        /// Newest delivery notes for the dashboard's activity feed, newest
        /// first. See <see cref="ProductionMetrics.RecentJobs"/>.
        /// </summary>
        public List<RecentDeliveryRow> RecentNotes { get; set; } = new();
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

        /// <summary>
        /// Number of distinct categories in store.
        ///
        /// Grouped the same way as <see cref="CategoryBreakdown"/> — trimmed
        /// and case-insensitive, blanks as "Uncategorised" — so this always
        /// equals CategoryBreakdown.Count. The dashboard previously counted
        /// raw Category strings, which treated "Clear" and "clear " as two
        /// categories.
        /// </summary>
        public int CategoryCount { get; set; }

        /// <summary>
        /// Stock grouped by category, carrying every measure a caller needs:
        /// sheet count, square metres, sell value and cost value.
        ///
        /// Retyped 2026-09-08 from <c>List&lt;NamedValue&gt;</c>. NamedValue
        /// holds one decimal and one int, so the Sheet Store dashboard — which
        /// charts square metres per category — could not be served by it
        /// without loading the table itself. No caller read this property
        /// before the retyping, so nothing breaks.
        /// </summary>
        public List<InventoryCategoryValue> CategoryBreakdown { get; set; } = new();

        public List<LowStockItem> LowStockItems { get; set; } = new();
    }

    /// <summary>
    /// One category's share of stock, in all four measures at once.
    /// </summary>
    public sealed class InventoryCategoryValue
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Sheets remaining (sum of Balance).</summary>
        public int Sheets { get; set; }

        /// <summary>Square metres remaining (sum of Balance × area).</summary>
        public decimal Sqm { get; set; }

        /// <summary>Remaining stock valued at sell price.</summary>
        public decimal SellValue { get; set; }

        /// <summary>Remaining stock valued at purchase price.</summary>
        public decimal CostValue { get; set; }
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

        // ── Added 2026-09-08 (Phase 3) ──────────────────────────────────
        // The Delivery Note dashboard feed renders a type icon, a driver
        // line, an ordered-quantity fallback and sorts by UpdatedAt. Carrying
        // these four columns means the feed no longer needs the full
        // DeliveryNoteListItem — and still never touches the snapshot blob.
        public string DeliveryType { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public int TotalOrderedQty { get; set; }
        public DateTime UpdatedAt { get; set; }
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

    // ═════════════════════════════════════════════════════════════════
    //  STATEMENT
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Statement dashboard aggregates. Statements are a running balance
    /// rather than a lifecycle, so there is no status breakdown here — the
    /// dashboard renders a balance mix from the three money figures.
    ///
    /// <para>
    /// <see cref="Recent"/> carries <c>StatementListItem</c> rather than a
    /// parallel DTO on purpose. That type is already a lightweight SQL
    /// projection with no JSON blob columns, and its display strings
    /// (<c>TotalInvoicedDisplay</c>, <c>SubTitle</c>, …) are computed from
    /// its own fields. Re-declaring them here would be exactly the
    /// duplicated-definition drift this codebase has spent 2026-09-08
    /// removing.
    /// </para>
    /// </summary>
    public sealed class StatementMetrics
    {
        public int TotalStatements { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }

        /// <summary>Newest first, bounded by the caller's recent limit.</summary>
        public List<StatementListItem> Recent { get; set; } = new();
    }
}

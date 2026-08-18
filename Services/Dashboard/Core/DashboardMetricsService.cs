// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/Core/DashboardMetricsService.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.Core
//
// PURPOSE:
//   Aggregate dashboard data without loading whole tables.
//
// THE PROBLEM THIS SOLVES
//   Six of seven dashboards called repository GetAllAsync() and
//   aggregated in memory. Opening Overview loaded FOUR entire tables.
//   ProformaInvoiceRepository.GetAllAsync materialises every column of
//   every row — including FullSnapshotJson, a complete invoice snapshot
//   blob — just to count rows and sum totals. Only 2 of 18 repositories
//   used AsNoTracking, so EF also built change-tracking entries for rows
//   that are read-only analytics input.
//
//   That is invisible on a developer database with 20 invoices and
//   becomes the dominant cost on a real one.
//
// THE PATTERN (copied from the existing TaxInvoiceMetricsService, which
// already did this correctly)
//   • AsNoTracking()      — no change tracking for read-only queries
//   • .Select(...)        — project ONLY the columns needed, so the JSON
//                           blob columns are never read off disk
//   • CancellationToken   — navigating away cancels in-flight work
//   • own DbContext per call — contexts are not thread-safe and these
//                           queries can run concurrently
//
// WHY PROJECT-THEN-AGGREGATE RATHER THAN COUNT IN SQL
//   Several figures depend on case-insensitive status comparison and on
//   [NotMapped] computed properties (GlassSheet.Balance is Stock - Used,
//   BalanceValue is SqmPerSheet * Balance * SellPricePerSqm). EF cannot
//   translate those, so the projection selects the small set of raw
//   columns and the arithmetic happens in memory over a narrow row.
//   The saving is in the columns read, not in the row count.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Dashboard.Core
{
    public sealed class DashboardMetricsService : IDashboardMetricsService
    {
        // Sheet types at or below this balance count as "low stock".
        // Matches the threshold the Overview dashboard already used.
        private const int LowStockThreshold = 5;

        private static bool Eq(string? a, string b) =>
            string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

        // ═════════════════════════════════════════════════════════════
        //  NARROW PROJECTIONS
        //  One class per table, holding only the columns the dashboard
        //  reads. Nothing here references a JSON snapshot column.
        // ═════════════════════════════════════════════════════════════

        private sealed class InvoiceRow
        {
            public string CustomerName { get; set; } = "";
            public string Status { get; set; } = "";
            public decimal Total { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }

            /// <summary>
            /// The timestamp the dashboard treats as the invoice's date.
            /// Mirrors the existing Overview rule: prefer UpdatedAt, fall
            /// back to CreatedAt when the row was never updated.
            /// </summary>
            public DateTime Effective =>
                UpdatedAt != default ? UpdatedAt : CreatedAt;
        }

        private sealed class JobRow
        {
            public string Status { get; set; } = "";
            public string Priority { get; set; } = "";
            public DateTime? DeliveryDate { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private sealed class DnRow
        {
            public string Status { get; set; } = "";
            public string DeliveryType { get; set; } = "";
            public DateTime DeliveryDate { get; set; }
            public int TotalDeliveredQty { get; set; }
        }

        private sealed class SheetRow
        {
            public string Category { get; set; } = "";
            public string Color { get; set; } = "";
            public int ThicknessMm { get; set; }
            public int WidthMm { get; set; }
            public int HeightMm { get; set; }
            public int Stock { get; set; }
            public int Used { get; set; }
            public decimal SellPricePerSqm { get; set; }
            public decimal PurchasePricePerSqm { get; set; }

            // Mirrors GlassSheet's [NotMapped] computed properties. Those
            // cannot be translated to SQL, so they are recomputed here
            // from the raw columns using the identical formulas.
            public int Balance => Stock - Used;
            public decimal SqmPerSheet =>
                ProGlassAutomation.Utilities.SqmCalculator.CalculateSheetArea(
                    WidthMm, HeightMm);
            public decimal BalanceValue => SqmPerSheet * Balance * SellPricePerSqm;
            public decimal BalanceCost => SqmPerSheet * Balance * PurchasePricePerSqm;
            public decimal SqmBalance => SqmPerSheet * Balance;
        }

        // ═════════════════════════════════════════════════════════════
        //  SALES
        // ═════════════════════════════════════════════════════════════

        public async Task<SalesMetrics> GetSalesMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default)
        {
            var m = new SalesMetrics();

            using var db = new ProformaInvoicesDbContext();

            var rows = await db.ProformaInvoices
                .AsNoTracking()
                .Select(i => new InvoiceRow
                {
                    CustomerName = i.CustomerName,
                    Status       = i.Status,
                    Total        = i.GrandTotalVatInclusive,
                    CreatedAt    = i.CreatedAt,
                    UpdatedAt    = i.UpdatedAt
                })
                .ToListAsync(ct);

            if (rows.Count == 0) return m;

            // Lifetime figures stay lifetime regardless of the selected
            // range: "total sales" answers a different question from
            // "sales this period", and conflating them was one reason
            // widgets disagreed with each other.
            m.TotalSalesValue = rows.Sum(r => r.Total);
            m.TotalInvoices   = rows.Count;
            m.ApprovedInvoices = rows.Count(r => Eq(r.Status, "Approved"));
            m.DraftInvoices    = rows.Count(r => Eq(r.Status, "Draft"));

            m.ApprovalRatePercent = m.TotalInvoices == 0
                ? null
                : (double)m.ApprovedInvoices / m.TotalInvoices * 100d;

            var inRange = rows.Where(r => range.Contains(r.Effective)).ToList();
            m.SalesThisPeriod.Current = inRange.Sum(r => r.Total);

            if (range.HasPrevious)
            {
                var prev = range.Previous();
                m.SalesThisPeriod.Previous =
                    rows.Where(r => prev.Contains(r.Effective)).Sum(r => r.Total);
            }

            m.StatusBreakdown = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Status) ? "Unknown" : r.Status.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => new NamedValue
                {
                    Name  = g.Key,
                    Count = g.Count(),
                    Value = g.Sum(x => x.Total)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            m.TopCustomers = (inRange.Count > 0 ? inRange : rows)
                .Where(r => !string.IsNullOrWhiteSpace(r.CustomerName))
                .GroupBy(r => r.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new NamedValue
                {
                    Name  = g.Key,
                    Count = g.Count(),
                    Value = g.Sum(x => x.Total)
                })
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToList();

            m.MonthlyTrend = BuildMonthlyTrend(
                rows.Select(r => (r.Effective, r.Total)), 6);

            return m;
        }

        // ═════════════════════════════════════════════════════════════
        //  PRODUCTION
        // ═════════════════════════════════════════════════════════════

        public async Task<ProductionMetrics> GetProductionMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default)
        {
            var m = new ProductionMetrics();

            using var db = new JobOrdersDbContext();

            var rows = await db.JobOrders
                .AsNoTracking()
                .Select(j => new JobRow
                {
                    Status       = j.Status,
                    Priority     = j.Priority,
                    DeliveryDate = j.DeliveryDate,
                    UpdatedAt    = j.UpdatedAt
                })
                .ToListAsync(ct);

            if (rows.Count == 0) return m;

            var today = DateTime.Today;

            m.TotalJobs     = rows.Count;
            m.InProduction  = rows.Count(r => Eq(r.Status, "In Production"));
            m.Completed     = rows.Count(r => Eq(r.Status, "Completed") ||
                                              Eq(r.Status, "Delivered"));

            // Overdue = past its delivery date and not finished. A job
            // already completed or delivered is never overdue even if the
            // date has passed.
            m.Overdue = rows.Count(r =>
                r.DeliveryDate.HasValue &&
                r.DeliveryDate.Value.Date < today &&
                !Eq(r.Status, "Completed") &&
                !Eq(r.Status, "Delivered"));

            bool IsDone(JobRow r) =>
                Eq(r.Status, "Completed") || Eq(r.Status, "Delivered");

            m.CompletedThisPeriod.Current =
                rows.Count(r => IsDone(r) && range.Contains(r.UpdatedAt));

            if (range.HasPrevious)
            {
                var prev = range.Previous();
                m.CompletedThisPeriod.Previous =
                    rows.Count(r => IsDone(r) && prev.Contains(r.UpdatedAt));
            }

            m.StatusBreakdown = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Status) ? "Unknown" : r.Status.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => new NamedValue { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            m.PriorityBreakdown = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => new NamedValue { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            m.CompletionTrend = BuildMonthlyCountTrend(
                rows.Where(IsDone).Select(r => r.UpdatedAt), 6);

            return m;
        }

        // ═════════════════════════════════════════════════════════════
        //  DELIVERY
        // ═════════════════════════════════════════════════════════════

        public async Task<DeliveryMetrics> GetDeliveryMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default)
        {
            var m = new DeliveryMetrics();

            using var db = new DeliveryNotesDbContext();

            var rows = await db.DeliveryNotes
                .AsNoTracking()
                .Select(d => new DnRow
                {
                    Status            = d.Status,
                    DeliveryType      = d.DeliveryType,
                    DeliveryDate      = d.DeliveryDate,
                    TotalDeliveredQty = d.TotalDeliveredQty
                })
                .ToListAsync(ct);

            // "Sent out" covers every state after dispatch. Counting only
            // "Dispatched" under-reports, because a note that has since
            // been Delivered/Signed/Received also left the building.
            bool IsSentOut(DnRow r) =>
                Eq(r.Status, "Dispatched") || Eq(r.Status, "Delivered") ||
                Eq(r.Status, "Signed")     || Eq(r.Status, "Received");

            if (rows.Count > 0)
            {
                var today = DateTime.Today;

                m.TotalNotes  = rows.Count;
                m.Preparing   = rows.Count(r => Eq(r.Status, "Draft"));
                m.Dispatched  = rows.Count(IsSentOut);

                m.PiecesDeliveredToday = rows
                    .Where(r => r.DeliveryDate.Date == today && IsSentOut(r))
                    .Sum(r => r.TotalDeliveredQty);

                // Safe to sum: TotalDeliveredQty is the quantity delivered
                // BY THIS NOTE, so the sum across notes is the true total.
                m.PiecesDeliveredTotal = rows
                    .Where(r => !Eq(r.Status, "Draft"))
                    .Sum(r => r.TotalDeliveredQty);

                m.StatusBreakdown = rows
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.Status) ? "Unknown" : r.Status.Trim(),
                             StringComparer.OrdinalIgnoreCase)
                    .Select(g => new NamedValue { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                m.TypeBreakdown = rows
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.DeliveryType) ? "Standard" : r.DeliveryType.Trim(),
                             StringComparer.OrdinalIgnoreCase)
                    .Select(g => new NamedValue { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();
            }

            // Outstanding pieces come from the repository's per-job
            // aggregation, NOT from summing TotalBalanceQty over notes.
            //
            // Each note stores a point-in-time snapshot of the remaining
            // balance for its job, so adding those columns counts the same
            // outstanding pieces once per note. The existing Overview code
            // carries this warning; encoding it here means every dashboard
            // gets the correct figure instead of each one rediscovering it.
            try
            {
                var pending = await new DeliveryNoteRepository().GetPendingQtyJobsAsync();
                m.PendingPieces = pending.Sum(p => p.TotalBalance);
            }
            catch
            {
                // A failure here must not lose the note metrics already
                // computed above. PendingPieces stays 0 and the caller
                // surfaces its own error state if it needs to.
                m.PendingPieces = 0;
            }

            return m;
        }

        // ═════════════════════════════════════════════════════════════
        //  INVENTORY
        //
        //  No date range: stock is a present-tense balance, not a
        //  period aggregate. Filtering it by "last month" would be
        //  meaningless.
        // ═════════════════════════════════════════════════════════════

        public async Task<InventoryMetrics> GetInventoryMetricsAsync(
            CancellationToken ct = default)
        {
            var m = new InventoryMetrics();

            using var db = new AppDbContext();

            var rows = await db.GlassSheets
                .AsNoTracking()
                .Select(s => new SheetRow
                {
                    Category            = s.Category,
                    Color               = s.Color,
                    ThicknessMm         = s.ThicknessMm,
                    WidthMm             = s.WidthMm,
                    HeightMm            = s.HeightMm,
                    Stock               = s.Stock,
                    Used                = s.Used,
                    SellPricePerSqm     = s.SellPricePerSqm,
                    PurchasePricePerSqm = s.PurchasePricePerSqm
                })
                .ToListAsync(ct);

            if (rows.Count == 0) return m;

            m.SheetTypes           = rows.Count;
            m.TotalSheetsAvailable = rows.Sum(r => r.Balance);
            m.TotalSqmAvailable    = rows.Sum(r => r.SqmBalance);
            m.StockValueAtSellPrice = rows.Sum(r => r.BalanceValue);
            m.StockValueAtCost      = rows.Sum(r => r.BalanceCost);

            m.OutOfStockCount = rows.Count(r => r.Balance <= 0);
            m.LowStockCount   = rows.Count(r => r.Balance <= LowStockThreshold);

            m.CategoryBreakdown = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Uncategorised" : r.Category.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => new NamedValue
                {
                    Name  = g.Key,
                    Count = g.Sum(x => x.Balance),
                    Value = g.Sum(x => x.BalanceValue)
                })
                .OrderByDescending(x => x.Value)
                .ToList();

            m.LowStockItems = rows
                .Where(r => r.Balance <= LowStockThreshold)
                .OrderBy(r => r.Balance)
                .Take(10)
                .Select(r => new LowStockItem
                {
                    Description  = $"{r.ThicknessMm}mm {r.Color}".Trim(),
                    Balance      = r.Balance,
                    IsOutOfStock = r.Balance <= 0
                })
                .ToList();

            return m;
        }

        // ═════════════════════════════════════════════════════════════
        //  EXECUTIVE ROLL-UP
        // ═════════════════════════════════════════════════════════════

        public async Task<ExecutiveMetrics> GetExecutiveMetricsAsync(
            DashboardDateRange range, CancellationToken ct = default)
        {
            // Sequential, not Task.WhenAll. Each call opens its own
            // DbContext against a DIFFERENT SQLite file, so parallelism
            // would be safe from EF's perspective — but SQLite under
            // concurrent connections is where intermittent "database is
            // locked" failures come from, and four fast projected queries
            // do not need the risk.
            var sales      = await GetSalesMetricsAsync(range, ct);
            var production = await GetProductionMetricsAsync(range, ct);
            var delivery   = await GetDeliveryMetricsAsync(range, ct);
            var inventory  = await GetInventoryMetricsAsync(ct);

            return new ExecutiveMetrics
            {
                Sales      = sales,
                Production = production,
                Delivery   = delivery,
                Inventory  = inventory
            };
        }

        // ═════════════════════════════════════════════════════════════
        //  RECENT ROWS (activity feed)
        // ═════════════════════════════════════════════════════════════

        public async Task<RecentActivityRows> GetRecentRowsAsync(
            int limit = 20, CancellationToken ct = default)
        {
            if (limit <= 0) limit = 20;

            var result = new RecentActivityRows();

            // DateTime.MinValue is what an unset UpdatedAt holds.
            var unsetDate = DateTime.MinValue;

            // Ordering happens in SQL and Take() bounds the result, so
            // only `limit` rows are read per table. The feed previously
            // loaded every row and threw nearly all of them away.

            using (var db = new ProformaInvoicesDbContext())
            {
                result.Invoices = await db.ProformaInvoices
                    .AsNoTracking()
                    // An explicit sentinel rather than `default`: EF has to
                    // translate this into SQL, and a captured local is
                    // parameterised cleanly where `default` may not be.
                    .OrderByDescending(i => i.UpdatedAt > unsetDate
                                                ? i.UpdatedAt : i.CreatedAt)
                    .Take(limit)
                    .Select(i => new RecentInvoiceRow
                    {
                        ProformaId             = i.ProformaId,
                        CustomerName           = i.CustomerName,
                        Status                 = i.Status,
                        GrandTotalVatInclusive = i.GrandTotalVatInclusive,
                        CreatedAt              = i.CreatedAt,
                        UpdatedAt              = i.UpdatedAt
                    })
                    .ToListAsync(ct);
            }

            using (var db = new JobOrdersDbContext())
            {
                result.Jobs = await db.JobOrders
                    .AsNoTracking()
                    .OrderByDescending(j => j.UpdatedAt)
                    .Take(limit)
                    .Select(j => new RecentJobRow
                    {
                        JobOrderId             = j.JobOrderId,
                        CustomerName           = j.CustomerName,
                        Status                 = j.Status,
                        Priority               = j.Priority,
                        GrandTotalVatInclusive = j.GrandTotalVatInclusive,
                        DeliveryDate           = j.DeliveryDate,
                        UpdatedAt              = j.UpdatedAt
                    })
                    .ToListAsync(ct);
            }

            using (var db = new DeliveryNotesDbContext())
            {
                result.DeliveryNotes = await db.DeliveryNotes
                    .AsNoTracking()
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(limit)
                    .Select(d => new RecentDeliveryRow
                    {
                        DeliveryNoteId    = d.DeliveryNoteId,
                        CustomerName      = d.CustomerName,
                        Status            = d.Status,
                        DeliveryDate      = d.DeliveryDate,
                        TotalDeliveredQty = d.TotalDeliveredQty
                    })
                    .ToListAsync(ct);
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════
        //  TREND HELPERS
        //
        //  Both emit a bucket for EVERY month in the window, including
        //  empty ones. Skipping empty months makes a chart silently lie
        //  about the shape of the trend.
        // ═════════════════════════════════════════════════════════════

        private static List<TimeBucket> BuildMonthlyTrend(
            IEnumerable<(DateTime When, decimal Amount)> source, int months)
        {
            var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                        .AddMonths(-(months - 1));

            var buckets = Enumerable.Range(0, months)
                .Select(i => first.AddMonths(i))
                .ToDictionary(d => d, _ => (Value: 0m, Count: 0));

            foreach (var (when, amount) in source)
            {
                var key = new DateTime(when.Year, when.Month, 1);
                if (!buckets.TryGetValue(key, out var cur)) continue;
                buckets[key] = (cur.Value + amount, cur.Count + 1);
            }

            return buckets
                .OrderBy(kv => kv.Key)
                .Select(kv => new TimeBucket
                {
                    PeriodStart = kv.Key,
                    Label       = kv.Key.ToString("MMM"),
                    Value       = kv.Value.Value,
                    Count       = kv.Value.Count
                })
                .ToList();
        }

        private static List<TimeBucket> BuildMonthlyCountTrend(
            IEnumerable<DateTime> source, int months) =>
            BuildMonthlyTrend(source.Select(d => (d, 1m)), months);
    }
}

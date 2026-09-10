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
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.Statement.Repository;
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
        // Matches the threshold the Overview dashboard already used — and now
        // the AI agent agrees, because both read GlassSheetLowStock. The value
        // is unchanged; only its home moved.
        private const int LowStockThreshold = ProGlassAutomation.Models.SheetStore.GlassSheetLowStock.Threshold;

        /// <summary>
        /// How many rows a dashboard activity feed shows. Bounded so the feed
        /// costs the same whether the table holds 50 rows or 50,000.
        /// </summary>
        private const int RecentFeedSize = 10;

        private static bool Eq(string? a, string b) =>
            string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

        // Job status comparisons live in JobOrderStatusValues (Models/JobOrder/
        // Common/JobOrderStatus/). They were local helpers here until
        // 2026-09-08, which is how "In Production" vs "InProduction" came to be
        // answered differently by this service, by ReportingDataService and by
        // the Job Order dashboard.
        //
        // ReportingDataService still carries its own both-spelling checks; it
        // is correct today, but it should be migrated to JobOrderStatusValues
        // so the rule has exactly one home. Tracked in Phase 3 of
        // docs/REMEDIATION_PLAN.md.

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

            // Added 2026-09-08 (Phase 3) for the Job Order dashboard's
            // activity feed. All scalar columns; no snapshot blob.
            public string JobOrderId { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public decimal GrandTotalVatInclusive { get; set; }
        }

        private sealed class DnRow
        {
            public string Status { get; set; } = "";
            public string DeliveryType { get; set; } = "";
            public DateTime DeliveryDate { get; set; }
            public int TotalDeliveredQty { get; set; }

            // Added 2026-09-08 (Phase 3) so the Delivery Note dashboard can
            // build its activity feed from this projection. Still no snapshot
            // blob column — these are all scalar columns.
            public string DeliveryNoteId { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string DriverName { get; set; } = "";
            public int TotalOrderedQty { get; set; }
            public DateTime UpdatedAt { get; set; }
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
                // Voided proformas are soft-deleted — every list screen
                // hides them, so the dashboard must not count them either.
                // (2026-09-10: sales band counted void documents.)
                .Where(i => !i.IsVoid)
                .Select(i => new InvoiceRow
                {
                    CustomerName = i.CustomerName,
                    Status       = i.Status,
                    Total        = i.GrandTotalVatInclusive,
                    CreatedAt    = i.CreatedAt,
                    UpdatedAt    = i.UpdatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);

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
                // Voided job orders are soft-deleted — same rule as above.
                .Where(j => !j.IsVoid)
                .Select(j => new JobRow
                {
                    Status                 = j.Status,
                    Priority               = j.Priority,
                    DeliveryDate           = j.DeliveryDate,
                    UpdatedAt              = j.UpdatedAt,
                    JobOrderId             = j.JobOrderId,
                    CustomerName           = j.CustomerName,
                    GrandTotalVatInclusive = j.GrandTotalVatInclusive
                })
                .ToListAsync(ct).ConfigureAwait(false);

            if (rows.Count == 0) return m;

            var today = DateTime.Today;

            m.TotalJobs     = rows.Count;

            // ── The legacy "InProduction" ↔ "In Production" split ────────
            //
            // JobOrderStatus is an enum whose member is `InProduction`, and
            // SavedJobOrder.Status is a string — so a job written through the
            // enum stores "InProduction" with no space. Older rows store
            // "In Production". BOTH forms are live in the database; this is
            // documented in ReportingDataService.DrillDown.cs ("Normalise the
            // legacy InProduction ↔ In Production split") and handled there
            // and in ReportingDataService.Jo.cs by matching both.
            //
            // This method previously matched only "In Production", so every
            // job written through the enum — i.e. every recent one — was
            // silently excluded. The Overview dashboard therefore
            // under-reported In Production while the Job Order dashboard,
            // which matches "InProduction", reported a different number.
            // Two dashboards, two answers to the same question.
            m.InProduction  = rows.Count(r => JobOrderStatusValues.IsInProduction(r.Status));
            m.Completed     = rows.Count(r => JobOrderStatusValues.IsFinished(r.Status));

            // Overdue = past its delivery date and not finished. A job
            // already completed or delivered is never overdue even if the
            // date has passed.
            m.Overdue = rows.Count(r =>
                r.DeliveryDate.HasValue &&
                r.DeliveryDate.Value.Date < today &&
                !JobOrderStatusValues.IsFinished(r.Status));

            bool IsDone(JobRow r) => JobOrderStatusValues.IsFinished(r.Status);

            m.CompletedThisPeriod.Current =
                rows.Count(r => IsDone(r) && range.Contains(r.UpdatedAt));

            if (range.HasPrevious)
            {
                var prev = range.Previous();
                m.CompletedThisPeriod.Previous =
                    rows.Count(r => IsDone(r) && prev.Contains(r.UpdatedAt));
            }

            // Canonical(), not a raw trim: without it a breakdown can
            // show "InProduction" and "In Production" as two separate states
            // and the status-mix chart splits one number into two bars.
            m.StatusBreakdown = rows
                .GroupBy(r => JobOrderStatusValues.Canonical(r.Status),
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

            // Ten newest jobs for the dashboard's activity feed, derived from
            // the set already in memory. The Job Order dashboard previously
            // loaded every job to render these ten rows.
            m.RecentJobs = rows
                .OrderByDescending(r => r.UpdatedAt)
                .Take(RecentFeedSize)
                .Select(r => new RecentJobRow
                {
                    JobOrderId             = r.JobOrderId,
                    CustomerName           = r.CustomerName,
                    Status                 = r.Status,
                    Priority               = r.Priority,
                    GrandTotalVatInclusive = r.GrandTotalVatInclusive,
                    DeliveryDate           = r.DeliveryDate,
                    UpdatedAt              = r.UpdatedAt
                })
                .ToList();

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

            // !IsMasterDn matches DeliveryNoteRepository.GetAllAsync and
            // every per-status query beside it (GetPreparedAsync,
            // GetDeliveredAsync). Those lists are what the user acts on, so
            // they are the ground truth for "how many delivery notes are
            // there". This method previously had no such filter, which meant
            // the Overview delivery tiles counted master notes that no list
            // screen ever shows — two answers to the same question.
            //
            // Covered by Tests/Architecture/MasterDnFilterParityTests.cs.
            var rows = await db.DeliveryNotes
                .AsNoTracking()
                .Where(d => !d.IsMasterDn)
                // Voided notes are soft-deleted and invisible on every DN
                // list — counting them inflated Preparing / Dispatched /
                // Total Delivered on the Overview. (2026-09-10)
                .Where(d => !d.IsVoid)
                .Select(d => new DnRow
                {
                    Status            = d.Status,
                    DeliveryType      = d.DeliveryType,
                    DeliveryDate      = d.DeliveryDate,
                    TotalDeliveredQty = d.TotalDeliveredQty,
                    DeliveryNoteId    = d.DeliveryNoteId,
                    CustomerName      = d.CustomerName,
                    DriverName        = d.DriverName,
                    TotalOrderedQty   = d.TotalOrderedQty,
                    UpdatedAt         = d.UpdatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);

            // "Sent out" covers every state after dispatch. Counting only
            // "Dispatched" under-reports, because a note that has since been
            // Delivered/Signed/Received also left the building.
            //
            // Delegates to DeliveryNoteStatusValues so the status vocabulary
            // has one home. This previously spelled the four states out here,
            // which is how Preparing came to be compared against "Draft"
            // alone while the mapper writes "Dispatching".
            bool IsSentOut(DnRow r) =>
                DeliveryNoteStatusValues.HasLeftTheBuilding(r.Status);

            if (rows.Count > 0)
            {
                var today = DateTime.Today;

                m.TotalNotes  = rows.Count;

                // IsPreparing matches BOTH "Draft" and "Dispatching" — the mapper
                // writes the latter, so counting only "Draft" left those
                // notes in neither bucket and Preparing + Dispatched no
                // longer equalled TotalNotes.
                m.Preparing   = rows.Count(r => DeliveryNoteStatusValues.IsPreparing(r.Status));
                m.Dispatched  = rows.Count(IsSentOut);

                m.PiecesDeliveredToday = rows
                    .Where(r => r.DeliveryDate.Date == today && IsSentOut(r))
                    .Sum(r => r.TotalDeliveredQty);

                // Same predicate, counted rather than summed. The Delivery
                // Note dashboard's "Delivered Today" card has always shown a
                // NOTE count; reusing the piece sum above would have changed
                // its unit while leaving its label alone.
                m.NotesDeliveredToday = rows
                    .Count(r => r.DeliveryDate.Date == today && IsSentOut(r));

                // Exact match on the stored type string, matching what the
                // dashboard did. TypeBreakdown below is trimmed and
                // case-insensitive with a "Standard" fallback, so deriving
                // this from the breakdown would change the figure for any row
                // carrying stray whitespace.
                // Routes through DeliveryTypeValues, the canonical home for
                // the four type strings. Fully qualified on purpose: that
                // namespace's last segment is also the DeliveryType enum's
                // name, so importing it would make `DeliveryType` ambiguous
                // for the rest of this file.
                m.ReworkReturned = rows.Count(r =>
                    r.DeliveryType == Models.DeliveryNote.Common.DeliveryType.DeliveryTypeValues.Rework ||
                    r.DeliveryType == Models.DeliveryNote.Common.DeliveryType.DeliveryTypeValues.Return);

                // PIECE METRICS COME FROM ITEM ROWS, not the denormalized
                // note header. TotalDeliveredQty is a header copy that only
                // some save paths refresh — a stale/mis-seeded header showed
                // an item's SR NO as "delivered pcs" on the Overview
                // (2026-09-10 report: one note, tile showed 34 = its row
                // serial). DeliveryNoteItems.DeliveredQty is what the
                // Delivery report reconciles from, so the dashboard now
                // reads the same truth. Master DNs contribute 0 by rule.
                // SENT-OUT predicate: constant OR-ed equality, deliberately
                // NOT a string[] closure with .Contains. Under a modern
                // Roslyn (VS 17.12+ / newer SDK), T[].Contains inside an
                // expression tree binds to MemoryExtensions.Contains with an
                // implicit ReadOnlySpan<string> conversion; EF Core 8's
                // parameter funcletizer then evaluates that conversion in
                // its interpreter and dies with "GenericArguments[1],
                // ReadOnlySpan`1[String] ... violates TRet" — the exact
                // Overview crash reported 2026-09-10. Constant == compares
                // carry no parameters and no spans, on every SDK.
                var pcsQuery = db.DeliveryNoteItems.AsNoTracking()
                    .Where(i => i.DeliveryNote != null
                                && !i.DeliveryNote.IsVoid
                                && !i.DeliveryNote.IsMasterDn
                                && (i.DeliveryNote.Status == DeliveryNoteStatusValues.Dispatched
                                    || i.DeliveryNote.Status == DeliveryNoteStatusValues.Delivered
                                    || i.DeliveryNote.Status == DeliveryNoteStatusValues.Signed
                                    || i.DeliveryNote.Status == DeliveryNoteStatusValues.Received));

                var todayStart = today;
                var todayEnd = today.AddDays(1);

                m.PiecesDeliveredTotal = await pcsQuery
                    .SumAsync(i => (int?)i.DeliveredQty, ct).ConfigureAwait(false) ?? 0;

                m.PiecesDeliveredToday = await pcsQuery
                    .Where(i => i.DeliveryNote!.DeliveryDate >= todayStart
                                && i.DeliveryNote!.DeliveryDate < todayEnd)
                    .SumAsync(i => (int?)i.DeliveredQty, ct).ConfigureAwait(false) ?? 0;

                // Canonical() collapses Draft/Dispatching, which
                // DeliveryNoteStatusMeta already renders as one label.
                m.StatusBreakdown = rows
                    .GroupBy(r => DeliveryNoteStatusValues.Canonical(r.Status),
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

                // Feed rows derived from the set already in memory rather
                // than a second query against the same SQLite file.
                m.RecentNotes = rows
                    .OrderByDescending(r => r.UpdatedAt)
                    .Take(RecentFeedSize)
                    .Select(r => new RecentDeliveryRow
                    {
                        DeliveryNoteId    = r.DeliveryNoteId,
                        CustomerName      = r.CustomerName,
                        Status            = r.Status,
                        DeliveryType      = r.DeliveryType,
                        DriverName        = r.DriverName,
                        DeliveryDate      = r.DeliveryDate,
                        TotalDeliveredQty = r.TotalDeliveredQty,
                        TotalOrderedQty   = r.TotalOrderedQty,
                        UpdatedAt         = r.UpdatedAt
                    })
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
                .ToListAsync(ct).ConfigureAwait(false);

            if (rows.Count == 0) return m;

            m.SheetTypes           = rows.Count;
            m.TotalSheetsAvailable = rows.Sum(r => r.Balance);
            m.TotalSqmAvailable    = rows.Sum(r => r.SqmBalance);
            m.StockValueAtSellPrice = rows.Sum(r => r.BalanceValue);
            m.StockValueAtCost      = rows.Sum(r => r.BalanceCost);

            m.OutOfStockCount = rows.Count(r => r.Balance <= 0);
            m.LowStockCount   = rows.Count(r => r.Balance <= LowStockThreshold);

            var byCategory = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Uncategorised" : r.Category.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => new InventoryCategoryValue
                {
                    Name      = g.Key,
                    Sheets    = g.Sum(x => x.Balance),
                    Sqm       = g.Sum(x => x.SqmBalance),
                    SellValue = g.Sum(x => x.BalanceValue),
                    CostValue = g.Sum(x => x.BalanceCost)
                })
                .OrderByDescending(x => x.SellValue)
                .ToList();

            m.CategoryBreakdown = byCategory;
            m.CategoryCount     = byCategory.Count;

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
                    .ToListAsync(ct).ConfigureAwait(false);
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
                    .ToListAsync(ct).ConfigureAwait(false);
            }

            using (var db = new DeliveryNotesDbContext())
            {
                // Master DNs excluded, and the filter sits BEFORE Take() so
                // master notes cannot consume slots in the feed and push real
                // ones off the end. This query had no filter at all until
                // 2026-09-08 — the same gap the delivery aggregates had,
                // found by Tests/Architecture/MasterDnFilterParityTests.cs.
                result.DeliveryNotes = await db.DeliveryNotes
                    .AsNoTracking()
                    .Where(d => !d.IsMasterDn && !d.IsVoid)
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(limit)
                    .Select(d => new RecentDeliveryRow
                    {
                        DeliveryNoteId    = d.DeliveryNoteId,
                        CustomerName      = d.CustomerName,
                        Status            = d.Status,
                        DeliveryDate      = d.DeliveryDate,
                        TotalDeliveredQty = d.TotalDeliveredQty,
                        DeliveryType      = d.DeliveryType,
                        DriverName        = d.DriverName,
                        TotalOrderedQty   = d.TotalOrderedQty,
                        UpdatedAt         = d.UpdatedAt
                    })
                    .ToListAsync(ct).ConfigureAwait(false);
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

        // ═════════════════════════════════════════════════════════════
        //  STATEMENT
        // ═════════════════════════════════════════════════════════════

        public async Task<StatementMetrics> GetStatementMetricsAsync(
            int recentLimit = 5, CancellationToken ct = default)
        {
            var m = new StatementMetrics();

            // StatementRepository.GetAllAsync is already a SQL-level
            // projection that skips the FullSnapshotJson / FilterJson blob
            // columns, so only the columns this dashboard reads are materialised.
            //
            // The three sums stay in memory deliberately. SavedStatement maps
            // TotalInvoiced / TotalOutstanding / OverdueAmount as
            // [Column(TypeName = "TEXT")], so a server-side SUM would not be a
            // decimal sum. Moving this into SQL needs a column-type migration
            // first — recorded in docs/REMEDIATION_PLAN.md rather than guessed
            // at here.
            var repo = new StatementRepository();
            var items = await repo.GetAllAsync(ct).ConfigureAwait(false);

            if (items == null || items.Count == 0) return m;

            m.TotalStatements  = items.Count;
            m.TotalInvoiced    = items.Sum(i => i.TotalInvoiced);
            m.TotalOutstanding = items.Sum(i => i.TotalOutstanding);
            m.OverdueAmount    = items.Sum(i => i.OverdueAmount);

            // GetAllAsync already orders by CreatedAt DESC, so Take(n) is the
            // newest n without a second sort.
            m.Recent = items.Take(Math.Max(0, recentLimit)).ToList();

            return m;
        }

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

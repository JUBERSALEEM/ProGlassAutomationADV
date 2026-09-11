// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — production.db repository (Phase 2, 2026-09-11)
//
// House pattern (JobOrderRepository / ProformaInvoiceRepository):
// lazy EnsureCreated + SchemaMigrationRunner on first use, then a fresh
// short-lived DbContext per operation.
//
// ReleaseAsync is IDEMPOTENT: work items are keyed by the deterministic
// UniqueKey the distribution engine builds, so re-releasing a job
// refreshes geometry/quantities without disturbing floor statuses.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    /// <summary>Outcome of a release/re-release.</summary>
    public sealed class ProductionReleaseResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int RemovedStalePending { get; set; }
        public int KeptOrphansInProgressOrDone { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public static class ProductionRepository
    {
        private static bool _initialized;
        private static readonly object InitLock = new();

        /// <summary>Creates production.db on first use (idempotent).</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;
                try
                {
                    using var db = new ProductionDbContext();
                    db.Database.EnsureCreated();

                    // Additive schema upgrades land in
                    // AppSchemaMigrations.Production as the module evolves.
                    SchemaMigrationRunner.Upgrade(
                        ProductionDbContext.DatabaseFilePath,
                        AppSchemaMigrations.Production,
                        "Production");

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    AppLogService.Error(
                        "Production database could not be initialized.",
                        ex,
                        new LogContext
                        {
                            Module = "Production",
                            Class = nameof(ProductionRepository),
                            Operation = nameof(EnsureInitialized)
                        });
                    throw;
                }
            }
        }

        // ── Release (idempotent upsert) ──────────────────────────────────

        /// <summary>
        /// Applies a distribution plan to production.db. Existing items keep
        /// their floor status; stale PENDING items that the refreshed plan
        /// no longer contains are removed; started/completed orphans are
        /// kept and reported.
        /// </summary>
        public static async Task<ProductionReleaseResult> ReleaseAsync(ProductionJobPlan plan)
        {
            EnsureInitialized();
            var result = new ProductionReleaseResult();

            if (plan == null || string.IsNullOrWhiteSpace(plan.JobRef))
            {
                result.Warnings.Add("Empty plan — nothing released.");
                return result;
            }

            using var db = new ProductionDbContext();

            // 1 ── Release header
            var release = await db.Releases
                .FirstOrDefaultAsync(r => r.JobRef == plan.JobRef)
                .ConfigureAwait(false);

            if (release == null)
            {
                release = new SavedProductionRelease
                {
                    JobRef = plan.JobRef,
                    FirstReleasedAt = DateTime.Now
                };
                db.Releases.Add(release);
            }

            release.SourceProformaId = plan.SourceProformaId;
            release.CustomerName = plan.CustomerName;
            release.ProjectName = plan.ProjectName;
            release.ItemCount = plan.ItemCount;
            release.WarningsJson = JsonSerializer.Serialize(plan.Warnings ?? new List<string>());
            release.LastReleasedAt = DateTime.Now;

            // 2 ── Work items: upsert on UniqueKey
            var existing = await db.WorkItems
                .Where(i => i.JobRef == plan.JobRef)
                .ToDictionaryAsync(i => i.UniqueKey)
                .ConfigureAwait(false);

            var freshKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in plan.Items)
            {
                freshKeys.Add(item.UniqueKey);

                if (existing.TryGetValue(item.UniqueKey, out var row))
                {
                    ApplyContent(row, item);   // status deliberately preserved
                    row.UpdatedAt = DateTime.Now;
                    result.Updated++;
                }
                else
                {
                    var newRow = new SavedProductionWorkItem();
                    ApplyContent(newRow, item);
                    newRow.Status = (int)WorkItemStatus.Pending;
                    newRow.CreatedAt = DateTime.Now;
                    newRow.UpdatedAt = DateTime.Now;
                    db.WorkItems.Add(newRow);
                    result.Inserted++;
                }
            }

            // 3 ── Stale cleanup: rows the refreshed plan no longer has
            foreach (var row in existing.Values)
            {
                if (freshKeys.Contains(row.UniqueKey)) continue;

                if (row.Status == (int)WorkItemStatus.Pending)
                {
                    db.WorkItems.Remove(row);
                    result.RemovedStalePending++;
                }
                else
                {
                    result.KeptOrphansInProgressOrDone++;
                    result.Warnings.Add(
                        $"Kept an item that is no longer in the plan " +
                        $"({row.GlassRef} · {ProductionDistributionEngine.StageLabel((ProductionStage)row.Stage)}) " +
                        "because floor work already started on it.");
                }
            }

            await db.SaveChangesAsync().ConfigureAwait(false);

            result.Warnings.AddRange(plan.Warnings);
            return result;
        }

        private static void ApplyContent(SavedProductionWorkItem row, ProductionWorkItem item)
        {
            row.UniqueKey = item.UniqueKey;
            row.JobRef = item.JobRef;
            row.SourceProformaId = item.SourceProformaId;
            row.CustomerName = item.CustomerName;
            row.ProjectName = item.ProjectName;
            row.Salesman = item.Salesman;
            row.SpecGroupNumber = item.SpecGroupNumber;
            row.SpecName = item.SpecName;
            row.BuildType = item.BuildType;
            row.SerialNumber = item.SerialNumber;
            row.GlassRef = item.GlassRef;
            row.Position = item.Position;
            row.WidthMm = item.WidthMm;
            row.HeightMm = item.HeightMm;
            row.Qty = item.Qty;
            row.UnitSqm = item.UnitSqm;
            row.TotalSqm = item.TotalSqm;
            row.TotalLm = item.TotalLm;
            row.Treatment = item.Treatment;
            row.Stage = (int)item.Stage;
            row.PredecessorStage = item.PredecessorStage;
            row.ChargeTemplateKey = item.ChargeTemplateKey;
            row.ChargeMetricQty = item.ChargeMetricQty;
            row.ChargeMetricUnit = item.ChargeMetricUnit;
            row.PolishSubTypeLabel = item.PolishSubTypeLabel;
            row.Notes = item.Notes;
        }

        // ── Stage queue queries ──────────────────────────────────────────

        /// <summary>Queue for one stage module, newest release first.</summary>
        public static async Task<List<SavedProductionWorkItem>> GetStageQueueAsync(
            ProductionStage stage,
            string? jobRefFilter = null,
            WorkItemStatus? statusFilter = null)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            IQueryable<SavedProductionWorkItem> query = db.WorkItems
                .AsNoTracking()
                .Where(i => i.Stage == (int)stage);

            if (!string.IsNullOrWhiteSpace(jobRefFilter))
                query = query.Where(i => i.JobRef == jobRefFilter);

            if (statusFilter.HasValue)
                query = query.Where(i => i.Status == (int)statusFilter.Value);

            return await query
                .OrderBy(i => i.JobRef)
                .ThenBy(i => i.SpecGroupNumber)
                .ThenBy(i => i.SerialNumber)
                .ThenBy(i => i.Position)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <summary>All job refs with at least one work item (for pickers).</summary>
        public static async Task<List<SavedProductionRelease>> GetReleasesAsync()
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Releases
                .AsNoTracking()
                .OrderByDescending(r => r.LastReleasedAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <summary>Stage-level counts for one job (dashboard chips).</summary>
        public static async Task<Dictionary<int, int>> GetStageCountsAsync(string jobRef)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();
            var rows = await db.WorkItems
                .AsNoTracking()
                .Where(i => i.JobRef == jobRef && i.Status != (int)WorkItemStatus.Completed)
                .GroupBy(i => i.Stage)
                .Select(g => new { Stage = g.Key, Count = g.Count() })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(r => r.Stage, r => r.Count);
        }

        // ── Stage list + detail projections (screens redesign 2026-09-11) ─

        /// <summary>
        /// One unit row for the module list: the Step-5-shaped envelope
        /// (Ref, W1/H1, W2/H2, Qty, SQM, LM) plus this stage's progress.
        /// </summary>
        public sealed class ProductionUnitProjection
        {
            public string JobRef { get; set; } = "";
            public string SourceProformaId { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public string Salesman { get; set; } = "";
            public int SpecGroupNumber { get; set; }
            public string SpecName { get; set; } = "";
            public string BuildType { get; set; } = "";
            public string GlassRef { get; set; } = "";

            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public int Qty { get; set; }

            public decimal UnitSqm { get; set; }
            public decimal TotalSqm { get; set; }
            public decimal TotalLm { get; set; }

            public int DoneCount { get; set; }
            public int TotalCount { get; set; }

            /// <summary>0 Pending · 1 InProgress · 2 Completed.</summary>
            public int AggregateStatus { get; set; }

            /// <summary>This stage's items for the unit (drives the detail panel).</summary>
            public List<SavedProductionWorkItem> Items { get; set; } = new();
        }

        /// <summary>Stage list = unit rows + charge batch rows.</summary>
        public sealed class ProductionStageListData
        {
            public List<ProductionUnitProjection> Units { get; set; } = new();
            public List<SavedProductionWorkItem> BatchItems { get; set; } = new();
        }

        /// <summary>
        /// Builds the module list view: unit projections (grouped by
        /// job+spec+serial) plus charge batch rows, filtered by job /
        /// aggregate status / free-text search.
        /// </summary>
        public static async Task<ProductionStageListData> GetStageListAsync(
            ProductionStage stage,
            string? jobRefFilter = null,
            WorkItemStatus? statusFilter = null,
            string? search = null)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            IQueryable<SavedProductionWorkItem> query = db.WorkItems
                .AsNoTracking()
                .Where(i => i.Stage == (int)stage);

            if (!string.IsNullOrWhiteSpace(jobRefFilter))
                query = query.Where(i => i.JobRef == jobRefFilter);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(i =>
                    (i.GlassRef != null && EF.Functions.Like(i.GlassRef, $"%{term}%"))
                    || (i.CustomerName != null && EF.Functions.Like(i.CustomerName, $"%{term}%"))
                    || (i.JobRef != null && EF.Functions.Like(i.JobRef, $"%{term}%"))
                    || (i.SpecName != null && EF.Functions.Like(i.SpecName, $"%{term}%"))
                    || (i.Salesman != null && EF.Functions.Like(i.Salesman, $"%{term}%")));
            }

            var items = await query
                .OrderBy(i => i.JobRef)
                .ThenBy(i => i.SpecGroupNumber)
                .ThenBy(i => i.SerialNumber)
                .ThenBy(i => i.Position)
                .ToListAsync()
                .ConfigureAwait(false);

            var data = new ProductionStageListData();

            var unitItems = items.Where(i => i.Position != "Batch").ToList();
            data.BatchItems = items.Where(i => i.Position == "Batch").ToList();

            foreach (var group in unitItems
                .GroupBy(i => new { i.JobRef, i.SpecGroupNumber, i.SerialNumber }))
            {
                var list = group.ToList();
                var first = list[0];

                var unit = new ProductionUnitProjection
                {
                    JobRef = first.JobRef,
                    SourceProformaId = first.SourceProformaId,
                    CustomerName = first.CustomerName,
                    ProjectName = first.ProjectName,
                    Salesman = first.Salesman,
                    SpecGroupNumber = first.SpecGroupNumber,
                    SpecName = first.SpecName,
                    BuildType = first.BuildType,
                    GlassRef = first.GlassRef,
                    Qty = list.Max(i => i.Qty),
                    Items = list
                };

                // Pane-1 envelope — Glass / Unit / Outer. In a pure LAM
                // build there is no "Outer"; the laminate's outer layer
                // ("Lam Outer") is the envelope instead. DGU+LAM always
                // has a real "Outer", so the fallback never fires there.
                var pane1 = list.FirstOrDefault(i =>
                    i.Position is "Glass" or "Unit" or "Outer"
                    && i.WidthMm > 0 && i.HeightMm > 0)
                    ?? list.FirstOrDefault(i =>
                    i.Position == "Lam Outer"
                    && i.WidthMm > 0 && i.HeightMm > 0);
                if (pane1 != null)
                {
                    unit.W1 = pane1.WidthMm;
                    unit.H1 = pane1.HeightMm;
                }

                // Pane-2 envelope — Inner / Middle / the laminate's inner
                // layer. (In DGU+LAM, "Lam Outer" belongs to pane 2's
                // laminate pair — never to the pane-1 slot.)
                var pane2 = list.FirstOrDefault(i =>
                    i.Position is "Inner" or "Lam Inner" or "Middle"
                    && i.WidthMm > 0 && i.HeightMm > 0);
                if (pane2 != null)
                {
                    unit.W2 = pane2.WidthMm;
                    unit.H2 = pane2.HeightMm;
                }

                // Areas recomputed from the envelope — exactly Step 5 maths.
                decimal a1 = unit.W1 > 0 && unit.H1 > 0
                    ? SqmCalculator.CalculatePanel(unit.W1, unit.H1) : 0m;
                decimal a2 = unit.W2 > 0 && unit.H2 > 0
                    ? SqmCalculator.CalculatePanel(unit.W2, unit.H2) : 0m;
                unit.UnitSqm = a1 + a2;
                unit.TotalSqm = Math.Round(unit.UnitSqm * unit.Qty, 3, MidpointRounding.AwayFromZero);

                // LM from the distinct pane items actually present in this
                // stage (each Position appears once per unit per stage).
                unit.TotalLm = list
                    .Where(i => i.TotalLm > 0)
                    .GroupBy(i => i.Position)
                    .Sum(g => g.Max(i => i.TotalLm));

                unit.DoneCount = list.Count(i => i.Status == (int)WorkItemStatus.Completed);
                unit.TotalCount = list.Count;
                unit.AggregateStatus =
                    unit.DoneCount == unit.TotalCount ? 2
                    : list.Any(i => i.Status == (int)WorkItemStatus.InProgress) ? 1
                    : 0;

                data.Units.Add(unit);
            }

            if (statusFilter.HasValue)
            {
                int wanted = (int)statusFilter.Value;
                data.Units = data.Units
                    .Where(u => u.AggregateStatus == wanted).ToList();
                data.BatchItems = data.BatchItems
                    .Where(b => b.Status == wanted).ToList();
            }

            return data;
        }

        // ── Connectivity: predecessor checks + dispatch readiness ────────

        /// <summary>
        /// How many of the same unit's predecessor-stage items are NOT yet
        /// completed. The floor rule: a pane must be cut before it is
        /// drilled/polished/mitred, and cut before it is tempered; a unit
        /// must be paired before assembly. Returns 0 when the item has no
        /// predecessor stage (Cutting, charge batches).
        ///
        /// Unit identity = JobRef + SpecGroupNumber + SerialNumber. Batch
        /// (charge-driven) items are spec-level, never part of a unit.
        /// </summary>
        public static async Task<int> GetPendingPredecessorCountAsync(SavedProductionWorkItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.PredecessorStage)) return 0;

            EnsureInitialized();
            using var db = new ProductionDbContext();

            int predecessorStage = StageFromLabel(item.PredecessorStage);
            if (predecessorStage == 0) return 0;

            return await db.WorkItems
                .AsNoTracking()
                .CountAsync(i =>
                    i.JobRef == item.JobRef
                    && i.SpecGroupNumber == item.SpecGroupNumber
                    && i.SerialNumber == item.SerialNumber
                    && i.Position != "Batch"
                    && i.Stage == predecessorStage
                    && i.Status != (int)WorkItemStatus.Completed)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Dispatch readiness across all released jobs (or one job):
        /// a unit is READY when every stage item in it is completed.
        /// Batch charge items are spec-level work, not unit stages, so they
        /// never hold a unit hostage.
        /// </summary>
        public static async Task<(int TotalUnits, int ReadyUnits)> GetUnitReadinessAsync(
            string? jobRef = null)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            var query = db.WorkItems
                .AsNoTracking()
                .Where(i => i.Position != "Batch");

            if (!string.IsNullOrWhiteSpace(jobRef))
                query = query.Where(i => i.JobRef == jobRef);

            var items = await query
                .Select(i => new { i.JobRef, i.SpecGroupNumber, i.SerialNumber, i.Status })
                .ToListAsync()
                .ConfigureAwait(false);

            int total = 0, ready = 0;
            foreach (var unit in items
                .GroupBy(i => new { i.JobRef, i.SpecGroupNumber, i.SerialNumber }))
            {
                total++;
                if (unit.All(i => i.Status == (int)WorkItemStatus.Completed))
                    ready++;
            }

            return (total, ready);
        }

        /// <summary>Label → stage value (0 when unknown).</summary>
        private static int StageFromLabel(string label) => label switch
        {
            "Cutting" => (int)ProductionStage.Cutting,
            "Holes & Cutouts" => (int)ProductionStage.HolesCutouts,
            "Polish" => (int)ProductionStage.Polish,
            "Edge Deletion" => (int)ProductionStage.EdgeDeletion,
            "Mitring" => (int)ProductionStage.Mitring,
            "Tempering" => (int)ProductionStage.Tempering,
            "Pairing" => (int)ProductionStage.Pairing,
            "Doubling / Lamination" => (int)ProductionStage.DoublingLamination,
            _ => 0
        };

        // ── Status updates ───────────────────────────────────────────────

        public static async Task<bool> SetStatusAsync(int itemId, WorkItemStatus status)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            var row = await db.WorkItems.FindAsync(itemId).ConfigureAwait(false);
            if (row == null) return false;

            row.Status = (int)status;
            row.UpdatedAt = DateTime.Now;
            row.CompletedAt = status == WorkItemStatus.Completed ? DateTime.Now : null;

            await db.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}

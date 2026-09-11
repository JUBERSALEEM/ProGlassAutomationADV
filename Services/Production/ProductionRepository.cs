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

        /// <summary>
        /// Factory-wide counts per stage (all jobs) — drives the pipeline
        /// chain bar. Counts honour the production-chain gate: "open" only
        /// counts work that is actually RELEASED into the stage (its
        /// upstream stages are complete), so the chain bar shows where the
        /// live work really sits.
        /// </summary>
        public sealed class ProductionChainSummaryRow
        {
            public int Stage { get; set; }
            /// <summary>Released AND not yet completed (actionable now).</summary>
            public int Open { get; set; }
            public int Completed { get; set; }
            public int Total { get; set; }
        }

        public static async Task<List<ProductionChainSummaryRow>> GetChainSummaryAsync()
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();
            var all = await db.WorkItems.AsNoTracking().ToListAsync().ConfigureAwait(false);

            var releasedKeys = ComputeReleasedKeys(all);
            int completed = (int)WorkItemStatus.Completed;

            return all
                .GroupBy(i => i.Stage)
                .Select(g => new ProductionChainSummaryRow
                {
                    Stage = g.Key,
                    Open = g.Count(i => releasedKeys.Contains(i.UniqueKey) && i.Status != completed),
                    Completed = g.Count(i => i.Status == completed),
                    Total = g.Count()
                })
                .ToList();
        }

        // ── Production-chain flow gate (screens redesign 2026-09-11) ────
        //
        // Work moves through the factory like a tool chain: every pane
        // enters at Cutting, and a unit's data only appears in the NEXT
        // module once its upstream stages are fully complete. A pane is
        // RELEASED into stage N when every non-charge item of its unit in
        // stages below N is Completed. Charge batches (Holes / Polish /
        // Mitring / Edge Deletion) are spec-level and release once the
        // spec's cutting is done.
        //
        // This gate is the single source of truth for "what is actionable
        // right now" — the stage lists, the chain bar and the waiting
        // counts all derive from it.

        /// <summary>
        /// Returns the UniqueKeys of every item that is RELEASED into its
        /// stage right now (upstream work complete). Pure over the item
        /// list — no DB round-trips.
        /// </summary>
        private static HashSet<string> ComputeReleasedKeys(List<SavedProductionWorkItem> all)
        {
            var released = new HashSet<string>(StringComparer.Ordinal);
            int cutting = (int)ProductionStage.Cutting;
            int completed = (int)WorkItemStatus.Completed;

            // Pane / unit items: released when the unit's own items in all
            // earlier stages are complete. Cutting (stage 1) is the entry
            // point — everything is released into it.
            foreach (var unit in all.Where(i => i.Position != "Batch")
                .GroupBy(i => (i.JobRef, i.SpecGroupNumber, i.SerialNumber)))
            {
                var items = unit.ToList();
                foreach (var item in items)
                {
                    bool ok = item.Stage == cutting
                        || items.Where(x => x.Stage < item.Stage)
                                .All(x => x.Status == completed);
                    if (ok) released.Add(item.UniqueKey);
                }
            }

            // Charge batches: spec-level work released once the spec's
            // cutting is fully done (a spec with no cutting items has
            // nothing to wait for and is released immediately).
            var cutDone = all
                .Where(i => i.Stage == cutting && i.Position != "Batch")
                .GroupBy(i => (i.JobRef, i.SpecGroupNumber))
                .ToDictionary(g => g.Key, g => g.All(x => x.Status == completed));

            foreach (var batch in all.Where(i => i.Position == "Batch"))
            {
                if (!cutDone.TryGetValue((batch.JobRef, batch.SpecGroupNumber), out bool pendingCuts)
                    || pendingCuts)
                {
                    released.Add(batch.UniqueKey);
                }
            }

            return released;
        }

        /// <summary>
        /// Earliest incomplete upstream stage for a not-yet-released unit —
        /// drives the "waiting on Cutting / Tempering…" label.
        /// </summary>
        private static string WaitingOnLabel(
            List<SavedProductionWorkItem> unitItems, int currentStage)
        {
            int completed = (int)WorkItemStatus.Completed;
            var blocker = unitItems
                .Where(i => i.Position != "Batch" && i.Stage < currentStage
                            && i.Status != completed)
                .OrderBy(i => i.Stage)
                .FirstOrDefault();

            return blocker == null
                ? "Waiting upstream"
                : $"Waiting on {ProductionDistributionEngine.StageLabel((ProductionStage)blocker.Stage)}";
        }

        // ── Stage list + detail projections ──────────────────────────────

        /// <summary>
        /// One unit row for the module list: the FULL Step-5 shape (Sr,
        /// Ref, W1/H1, W2/H2, Qty, SQM + LM split per pane — pricing
        /// deliberately excluded) plus this stage's progress and flow state.
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
            public int SerialNumber { get; set; }
            public string GlassRef { get; set; } = "";

            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public int Qty { get; set; }

            // Step-5 maths, split by pane (no Rate / Value — production
            // reads geometry, never pricing).
            public decimal UnitSqm1 { get; set; }
            public decimal TotalSqm1 { get; set; }
            public decimal Lm1 { get; set; }
            public decimal UnitSqm2 { get; set; }
            public decimal TotalSqm2 { get; set; }
            public decimal Lm2 { get; set; }

            public int DoneCount { get; set; }
            public int TotalCount { get; set; }

            /// <summary>0 Pending · 1 InProgress · 2 Completed.</summary>
            public int AggregateStatus { get; set; }

            /// <summary>
            /// Flow state: true = released into this stage (actionable);
            /// false = still waiting on an earlier stage. When the caller
            /// asks for waiting rows too, WaitingOn explains why.
            /// </summary>
            public bool FlowReleased { get; set; } = true;
            public string WaitingOn { get; set; } = "";

            /// <summary>This stage's items for the unit (drives the detail panel).</summary>
            public List<SavedProductionWorkItem> Items { get; set; } = new();
        }

        /// <summary>A charge batch row with its flow state.</summary>
        public sealed class ProductionBatchProjection
        {
            public SavedProductionWorkItem Item { get; set; } = null!;
            public bool FlowReleased { get; set; } = true;
            public string WaitingOn { get; set; } = "";
        }

        /// <summary>Stage list = unit rows + charge batch rows.</summary>
        public sealed class ProductionStageListData
        {
            public List<ProductionUnitProjection> Units { get; set; } = new();
            public List<ProductionBatchProjection> BatchItems { get; set; } = new();

            /// <summary>
            /// Work allocated to this stage but held back by an incomplete
            /// upstream stage — the pipeline backlog (tool-chain gating).
            /// </summary>
            public int WaitingUnits { get; set; }
            public int WaitingBatch { get; set; }
        }

        /// <summary>
        /// Builds the module list view. PRODUCTION-CHAIN RULE: by default a
        /// unit only appears once its upstream stages are complete
        /// (<paramref name="includeWaiting"/>=false) — completed data moves
        /// to the next module exactly like a tool chain. Pass
        /// includeWaiting=true to also see the rows still held upstream.
        /// </summary>
        public static async Task<ProductionStageListData> GetStageListAsync(
            ProductionStage stage,
            string? jobRefFilter = null,
            WorkItemStatus? statusFilter = null,
            string? search = null,
            bool includeWaiting = false)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            // Load EVERY stage for the job scope — the flow gate needs the
            // upstream statuses, not just this stage's own items.
            IQueryable<SavedProductionWorkItem> scope = db.WorkItems.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(jobRefFilter))
                scope = scope.Where(i => i.JobRef == jobRefFilter);

            var all = await scope
                .OrderBy(i => i.JobRef)
                .ThenBy(i => i.SpecGroupNumber)
                .ThenBy(i => i.SerialNumber)
                .ThenBy(i => i.Position)
                .ToListAsync()
                .ConfigureAwait(false);

            var releasedKeys = ComputeReleasedKeys(all);

            int stageInt = (int)stage;
            IEnumerable<SavedProductionWorkItem> stageItems =
                all.Where(i => i.Stage == stageInt);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                stageItems = stageItems.Where(i =>
                    Contains(i.GlassRef, term) || Contains(i.CustomerName, term)
                    || Contains(i.JobRef, term) || Contains(i.SpecName, term)
                    || Contains(i.Salesman, term));
            }

            var data = new ProductionStageListData();
            int completed = (int)WorkItemStatus.Completed;

            // Full item set per unit (all stages) — the Step-5 envelope and
            // the "waiting on" trace both need the whole unit, not just the
            // current stage's slice.
            var unitLookup = all
                .Where(i => i.Position != "Batch")
                .GroupBy(i => (i.JobRef, i.SpecGroupNumber, i.SerialNumber))
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── Unit rows (pane / assembly items) ────────────────────────
            var unitItems = stageItems.Where(i => i.Position != "Batch").ToList();

            foreach (var group in unitItems
                .GroupBy(i => (i.JobRef, i.SpecGroupNumber, i.SerialNumber)))
            {
                var list = group.OrderBy(i => i.Position).ToList();
                var first = list[0];
                unitLookup.TryGetValue(group.Key, out var fullUnit);
                fullUnit ??= list;

                bool released = list.All(i => releasedKeys.Contains(i.UniqueKey));
                if (!released)
                {
                    data.WaitingUnits++;
                    if (!includeWaiting) continue;
                }

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
                    SerialNumber = first.SerialNumber,
                    GlassRef = first.GlassRef,
                    Qty = fullUnit.Max(i => i.Qty),
                    Items = list,
                    FlowReleased = released,
                    WaitingOn = released ? "" : WaitingOnLabel(fullUnit, stageInt)
                };

                // Step-5 envelope from the unit's CUTTING panes — geometry
                // is identical in every stage, so cutting is the single
                // source of truth for W1/H1, W2/H2 and the pane LM.
                // Pane-1 = Glass / Outer / Unit; a pure LAM has no "Outer",
                // so its "Lam Outer" is the envelope instead (DGU+LAM always
                // has a real "Outer", so the fallback never fires there).
                var cut = fullUnit.Where(i => i.Stage == (int)ProductionStage.Cutting).ToList();
                var source = cut.Count > 0 ? cut : fullUnit;

                var pane1 = source.FirstOrDefault(i =>
                    i.Position is "Glass" or "Unit" or "Outer"
                    && i.WidthMm > 0 && i.HeightMm > 0)
                    ?? source.FirstOrDefault(i =>
                    i.Position == "Lam Outer"
                    && i.WidthMm > 0 && i.HeightMm > 0);

                // Pane-2 = Inner / Middle / the laminate's inner layer.
                // (In DGU+LAM, "Lam Outer" belongs to pane 2's laminate
                // pair — never to the pane-1 slot.)
                var pane2 = source.FirstOrDefault(i =>
                    i.Position is "Inner" or "Lam Inner" or "Middle"
                    && i.WidthMm > 0 && i.HeightMm > 0);

                if (pane1 != null)
                {
                    unit.W1 = pane1.WidthMm;
                    unit.H1 = pane1.HeightMm;
                    unit.UnitSqm1 = SqmCalculator.CalculatePanel(unit.W1, unit.H1);
                    unit.TotalSqm1 = Math.Round(unit.UnitSqm1 * unit.Qty, 3, MidpointRounding.AwayFromZero);
                    unit.Lm1 = pane1.TotalLm;
                }

                if (pane2 != null)
                {
                    unit.W2 = pane2.WidthMm;
                    unit.H2 = pane2.HeightMm;
                    unit.UnitSqm2 = SqmCalculator.CalculatePanel(unit.W2, unit.H2);
                    unit.TotalSqm2 = Math.Round(unit.UnitSqm2 * unit.Qty, 3, MidpointRounding.AwayFromZero);
                    unit.Lm2 = pane2.TotalLm;
                }

                unit.DoneCount = list.Count(i => i.Status == completed);
                unit.TotalCount = list.Count;
                unit.AggregateStatus =
                    unit.DoneCount == unit.TotalCount ? 2
                    : list.Any(i => i.Status == (int)WorkItemStatus.InProgress) ? 1
                    : 0;

                data.Units.Add(unit);
            }

            // ── Charge batch rows ────────────────────────────────────────
            foreach (var batch in stageItems.Where(i => i.Position == "Batch"))
            {
                bool released = releasedKeys.Contains(batch.UniqueKey);
                if (!released)
                {
                    data.WaitingBatch++;
                    if (!includeWaiting) continue;
                }
                data.BatchItems.Add(new ProductionBatchProjection
                {
                    Item = batch,
                    FlowReleased = released,
                    WaitingOn = released
                        ? ""
                        : $"Waiting on {ProductionDistributionEngine.StageLabel(ProductionStage.Cutting)}"
                });
            }

            if (statusFilter.HasValue)
            {
                int wanted = (int)statusFilter.Value;
                data.Units = data.Units
                    .Where(u => u.AggregateStatus == wanted).ToList();
                data.BatchItems = data.BatchItems
                    .Where(b => b.Item.Status == wanted).ToList();
            }

            return data;
        }

        private static bool Contains(string? value, string term) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

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

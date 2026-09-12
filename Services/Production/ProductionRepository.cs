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
using ProGlassAutomation.Services.Audit;
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

        /// <summary>
        /// Raised after any floor entry is saved (a work item's done qty
        /// changed). Stage screens subscribe to live-refresh their lists and
        /// detail pages the instant an operator marks pieces done — no
        /// manual refresh. Raised on the caller's (UI) thread after the
        /// database write; listeners marshal as needed.
        /// </summary>
        public static event Action? WorkItemsChanged;

        private static void RaiseWorkItemsChanged()
        {
            try { WorkItemsChanged?.Invoke(); }
            catch (Exception ex)
            {
                AppLogService.Debug("[ProductionRepository] WorkItemsChanged listener failed: " + ex.Message);
            }
        }

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

        /// <summary>
        /// Factory-wide PIECE counts per stage (all jobs) — drives the
        /// pipeline chain bar. Total = pieces arrived from upstream,
        /// Completed = pieces the floor finished here, Open = the balance
        /// still to work. The chain bar shows where the live pieces sit.
        /// </summary>
        public sealed class ProductionChainSummaryRow
        {
            public int Stage { get; set; }
            /// <summary>Arrived but not yet completed (actionable now).</summary>
            public int Open { get; set; }
            public int Completed { get; set; }
            public int Total { get; set; }
        }

        public static async Task<List<ProductionChainSummaryRow>> GetChainSummaryAsync()
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();
            var all = await db.WorkItems.AsNoTracking().ToListAsync().ConfigureAwait(false);

            // Piece counts per stage: Open = arrived-but-not-done pieces,
            // Completed = done pieces, Total = arrived pieces.
            var flows = ComputeUnitFlows(all);
            return flows
                .SelectMany(f => f.Stages.Select(kv => (kv.Key, kv.Value)))
                .GroupBy(t => t.Key, t => t.Value)
                .Select(g => new ProductionChainSummaryRow
                {
                    Stage = g.Key,
                    Total = g.Sum(x => x.Inflow),
                    Completed = g.Sum(x => x.Done),
                    Open = g.Sum(x => Math.Max(0, x.Inflow - x.Done))
                })
                .OrderBy(r => r.Stage)
                .ToList();
        }

        // ── Piece-flow engine (production toolchain, 2026-09-11) ────────
        //
        // Production flows PIECE BY PIECE. Every stage item tracks how
        // many pieces the floor has completed (QtyDone, 0..Qty); only the
        // completed quantity arrives in the next stage:
        //
        //   Cutting (all pieces) → Holes / Polish / Edge Deletion /
        //   Mitring (each receives the CUT quantity; a piece must finish
        //   every one of these before it may be tempered) → Tempering →
        //   Pairing → Doubling/Lamination.
        //
        // ComputeUnitFlows is the single source of truth: the module
        // lists, the chain bar, the KPIs and the clamped floor writes all
        // derive from it.

        /// <summary>One stage item with its flowed quantity.</summary>
        public sealed class ProductionFlowItem
        {
            public SavedProductionWorkItem Item { get; set; } = null!;

            /// <summary>Pieces that arrived at this stage (available to work).</summary>
            public int Inflow { get; set; }

            public int Done => Math.Min(Item.QtyDone, Inflow);
            public int Balance => Math.Max(0, Inflow - Item.QtyDone);
        }

        /// <summary>Full flow state of one unit (Job + Spec + Step 5 serial).</summary>
        public sealed class ProductionUnitFlow
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
            public int Qty { get; set; }

            // Step-5 envelope (from the cutting panes — identical geometry
            // in every stage, pricing excluded).
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public decimal UnitSqm1 { get; set; }
            public decimal TotalSqm1 { get; set; }
            public decimal Lm1 { get; set; }
            public decimal UnitSqm2 { get; set; }
            public decimal TotalSqm2 { get; set; }
            public decimal Lm2 { get; set; }

            public List<SavedProductionWorkItem> AllItems { get; set; } = new();

            /// <summary>Stage → (pieces arrived, pieces done).</summary>
            public Dictionary<int, (int Inflow, int Done)> Stages { get; } = new();

            /// <summary>Stage → per-item flows (drives the detail panel).</summary>
            public Dictionary<int, List<ProductionFlowItem>> StageItems { get; } = new();

            /// <summary>Pieces that finished every required pane stage.</summary>
            public int PaneReadyQty { get; set; }

            /// <summary>
            /// Pieces ready for dispatch RIGHT NOW — a quantity, not a
            /// flag. SGU: pane-chain-complete pieces dispatch directly
            /// (no pairing/doubling). DGU/LAM/DGU+LAM: pieces that
            /// finished doubling.
            /// </summary>
            public int DispatchReadyQty { get; set; }

            /// <summary>True when the whole quantity finished the last stage.</summary>
            public bool IsDispatchReady { get; set; }
        }

        /// <summary>Edge/drill work that must finish a piece before tempering.</summary>
        private static readonly int[] EdgeWorkStages =
        {
            (int)ProductionStage.HolesCutouts,
            (int)ProductionStage.Polish,
            (int)ProductionStage.EdgeDeletion,
            (int)ProductionStage.Mitring
        };

        /// <summary>
        /// Computes the piece-flow state of every unit. Pure over the item
        /// list — the gate for lists, chain bar, KPIs and floor writes.
        /// </summary>
        public static List<ProductionUnitFlow> ComputeUnitFlows(List<SavedProductionWorkItem> all)
        {
            var flows = new List<ProductionUnitFlow>();
            int cutting = (int)ProductionStage.Cutting;
            int tempering = (int)ProductionStage.Tempering;
            int pairing = (int)ProductionStage.Pairing;
            int doubling = (int)ProductionStage.DoublingLamination;

            foreach (var group in all
                .Where(i => i.Position != "Batch")
                .GroupBy(i => (i.JobRef, i.SpecGroupNumber, i.SerialNumber)))
            {
                var items = group.ToList();
                var first = items[0];

                var flow = new ProductionUnitFlow
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
                    Qty = items.Max(i => i.Qty),
                    AllItems = items
                };

                // ── Geometry from the cutting panes ──────────────────────
                var cut = items.Where(i => i.Stage == cutting).ToList();
                var src = cut.Count > 0 ? cut : items;
                var pane1 = src.FirstOrDefault(i =>
                        i.Position is "Glass" or "Unit" or "Outer"
                        && i.WidthMm > 0 && i.HeightMm > 0)
                    ?? src.FirstOrDefault(i =>
                        i.Position == "Lam Outer"
                        && i.WidthMm > 0 && i.HeightMm > 0);
                var pane2 = src.FirstOrDefault(i =>
                    i.Position is "Inner" or "Lam Inner" or "Middle"
                    && i.WidthMm > 0 && i.HeightMm > 0);

                if (pane1 != null)
                {
                    flow.W1 = pane1.WidthMm;
                    flow.H1 = pane1.HeightMm;
                    flow.UnitSqm1 = SqmCalculator.CalculatePanel(flow.W1, flow.H1);
                    flow.TotalSqm1 = Math.Round(flow.UnitSqm1 * flow.Qty, 3, MidpointRounding.AwayFromZero);
                    flow.Lm1 = pane1.TotalLm;
                }
                if (pane2 != null)
                {
                    flow.W2 = pane2.WidthMm;
                    flow.H2 = pane2.HeightMm;
                    flow.UnitSqm2 = SqmCalculator.CalculatePanel(flow.W2, flow.H2);
                    flow.TotalSqm2 = Math.Round(flow.UnitSqm2 * flow.Qty, 3, MidpointRounding.AwayFromZero);
                    flow.Lm2 = pane2.TotalLm;
                }

                void AddAgg(int stage, int inflow, int done)
                {
                    inflow = Math.Max(0, inflow);
                    done = Math.Clamp(done, 0, inflow);
                    flow.Stages[stage] = flow.Stages.TryGetValue(stage, out var agg)
                        ? (agg.Inflow + inflow, agg.Done + done)
                        : (inflow, done);
                }

                // ── Per-pane piece flow ──────────────────────────────────
                int paneReady = int.MaxValue;
                bool hasPane = false;

                foreach (var panes in items
                    .Where(i => i.Position is not ("Unit" or "Batch"))
                    .GroupBy(i => i.Position))
                {
                    hasPane = true;
                    var paneItems = panes.ToList();

                    var cutItem = paneItems.FirstOrDefault(i => i.Stage == cutting);
                    int cutDone = cutItem == null ? 0 : Math.Clamp(cutItem.QtyDone, 0, cutItem.Qty);
                    if (cutItem != null)
                    {
                        AddAgg(cutting, cutItem.Qty, cutDone);
                        if (!flow.StageItems.TryGetValue(cutting, out var cl))
                            flow.StageItems[cutting] = cl = new List<ProductionFlowItem>();
                        cl.Add(new ProductionFlowItem { Item = cutItem, Inflow = cutItem.Qty });
                    }

                    // Edge work after cutting — every edge stage receives
                    // the cut quantity; the MIN across them bounds the
                    // pieces allowed to move on (a piece needs all of it).
                    int edgeDoneMin = int.MaxValue;
                    foreach (int edgeStage in EdgeWorkStages)
                    {
                        var edgeItems = paneItems.Where(i => i.Stage == edgeStage).ToList();
                        if (edgeItems.Count == 0) continue;

                        int stageDone = Math.Min(edgeItems.Min(i => i.QtyDone), cutDone);
                        edgeDoneMin = Math.Min(edgeDoneMin, stageDone);
                        AddAgg(edgeStage, cutDone, stageDone);

                        if (!flow.StageItems.TryGetValue(edgeStage, out var el))
                            flow.StageItems[edgeStage] = el = new List<ProductionFlowItem>();
                        foreach (var ei in edgeItems)
                            el.Add(new ProductionFlowItem { Item = ei, Inflow = cutDone });
                    }

                    int readyForTemper = Math.Min(cutDone,
                        edgeDoneMin == int.MaxValue ? cutDone : edgeDoneMin);

                    var temperItem = paneItems.FirstOrDefault(i => i.Stage == tempering);
                    if (temperItem != null)
                    {
                        int temperDone = Math.Clamp(temperItem.QtyDone, 0, readyForTemper);
                        AddAgg(tempering, readyForTemper, temperDone);
                        if (!flow.StageItems.TryGetValue(tempering, out var tl))
                            flow.StageItems[tempering] = tl = new List<ProductionFlowItem>();
                        tl.Add(new ProductionFlowItem { Item = temperItem, Inflow = readyForTemper });
                        paneReady = Math.Min(paneReady, temperDone);
                    }
                    else
                    {
                        paneReady = Math.Min(paneReady, readyForTemper);
                    }
                }

                flow.PaneReadyQty = hasPane && paneReady != int.MaxValue
                    ? Math.Max(0, paneReady) : 0;

                // ── Unit-level assembly stages ───────────────────────────
                var pairItem = items.FirstOrDefault(i => i.Stage == pairing);
                if (pairItem != null)
                {
                    int pairDone = Math.Clamp(pairItem.QtyDone, 0, flow.PaneReadyQty);
                    AddAgg(pairing, flow.PaneReadyQty, pairDone);
                    if (!flow.StageItems.TryGetValue(pairing, out var pl))
                        flow.StageItems[pairing] = pl = new List<ProductionFlowItem>();
                    pl.Add(new ProductionFlowItem { Item = pairItem, Inflow = flow.PaneReadyQty });

                    var doubleItem = items.FirstOrDefault(i => i.Stage == doubling);
                    if (doubleItem != null)
                    {
                        int doubleDone = Math.Clamp(doubleItem.QtyDone, 0, pairDone);
                        AddAgg(doubling, pairDone, doubleDone);
                        if (!flow.StageItems.TryGetValue(doubling, out var dl))
                            flow.StageItems[doubling] = dl = new List<ProductionFlowItem>();
                        dl.Add(new ProductionFlowItem { Item = doubleItem, Inflow = pairDone });

                        // Multi-pane units dispatch what finished doubling.
                        flow.DispatchReadyQty = doubleDone;
                        flow.IsDispatchReady = flow.Qty > 0 && doubleDone >= flow.Qty;
                    }
                }
                else
                {
                    // SGU: the pane chain end IS dispatch — single glass
                    // needs no pairing and no doubling/lamination, so each
                    // finished piece is ready to deliver as it tempers.
                    flow.DispatchReadyQty = flow.PaneReadyQty;
                    flow.IsDispatchReady = flow.Qty > 0 && flow.PaneReadyQty >= flow.Qty;
                }

                flows.Add(flow);
            }

            return flows;
        }

        /// <summary>
        /// Earliest upstream stage that is still holding pieces back —
        /// drives the "waiting on Cutting / Tempering…" label.
        /// </summary>
        private static string WaitingOnLabel(ProductionUnitFlow flow, int currentStage)
        {
            var blocker = flow.Stages
                .Where(kv => kv.Key < currentStage && kv.Value.Done < kv.Value.Inflow)
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Key)
                .FirstOrDefault();

            return blocker == 0
                ? "Waiting upstream"
                : $"Waiting on {ProductionDistributionEngine.StageLabel((ProductionStage)blocker)}";
        }

        // ── Stage list + detail projections ──────────────────────────────

        /// <summary>
        /// One unit row for the module list: the FULL Step-5 shape (Sr,
        /// Ref, W1/H1, W2/H2, Qty, SQM + LM split per pane — pricing
        /// excluded) plus this stage's piece flow: In / Done / Balance.
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

            // Step-5 maths split by pane (no Rate / Value).
            public decimal UnitSqm1 { get; set; }
            public decimal TotalSqm1 { get; set; }
            public decimal Lm1 { get; set; }
            public decimal UnitSqm2 { get; set; }
            public decimal TotalSqm2 { get; set; }
            public decimal Lm2 { get; set; }

            // ── Piece flow for THIS stage ────────────────────────────────
            /// <summary>Pieces that arrived from upstream.</summary>
            public int Inflow { get; set; }
            /// <summary>Pieces the floor completed here.</summary>
            public int DoneQty { get; set; }
            public int BalanceQty { get; set; }

            /// <summary>Area/LM that actually arrived (KPI load).</summary>
            public decimal ArrivedArea { get; set; }
            public decimal ArrivedLm { get; set; }

            /// <summary>0 Pending · 1 InProgress · 2 Completed.</summary>
            public int AggregateStatus { get; set; }

            /// <summary>"" when pieces have arrived; else who holds them.</summary>
            public string WaitingOn { get; set; } = "";

            /// <summary>This stage's per-item flows (detail panel).</summary>
            public List<ProductionFlowItem> FlowItems { get; set; } = new();
        }

        /// <summary>Stage list = unit rows with piece flow.</summary>
        public sealed class ProductionStageListData
        {
            public List<ProductionUnitProjection> Units { get; set; } = new();

            /// <summary>
            /// Units allocated to this stage where no piece has arrived
            /// yet — the pipeline backlog (they flow in as upstream
            /// completes pieces).
            /// </summary>
            public int WaitingUnits { get; set; }
        }

        /// <summary>
        /// Builds the module list. PRODUCTION-CHAIN RULE: a unit appears
        /// with the quantity that has actually ARRIVED from upstream
        /// (In); Done/Balance track this stage's floor progress. Units
        /// with nothing arrived yet are counted as waiting (or shown when
        /// <paramref name="includeWaiting"/> is true).
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

            // Piece flow needs the whole job (all stages), not just this
            // stage's own items.
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

            var flows = ComputeUnitFlows(all);
            int stageInt = (int)stage;
            var data = new ProductionStageListData();

            foreach (var flow in flows)
            {
                if (!flow.StageItems.TryGetValue(stageInt, out var stageItems))
                    continue; // this unit has no work allocated to this stage

                var agg = flow.Stages.TryGetValue(stageInt, out var a) ? a : (Inflow: 0, Done: 0);
                bool arrived = agg.Inflow > 0;

                if (!arrived)
                {
                    data.WaitingUnits++;
                    if (!includeWaiting) continue;
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim();
                    string serial = flow.SerialNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (!(Contains(flow.GlassRef, term) || Contains(flow.CustomerName, term)
                        || Contains(flow.JobRef, term) || Contains(flow.SpecName, term)
                        || Contains(flow.Salesman, term) || Contains(flow.BuildType, term)
                        || serial.Contains(term)))
                        continue;
                }

                var unit = new ProductionUnitProjection
                {
                    JobRef = flow.JobRef,
                    SourceProformaId = flow.SourceProformaId,
                    CustomerName = flow.CustomerName,
                    ProjectName = flow.ProjectName,
                    Salesman = flow.Salesman,
                    SpecGroupNumber = flow.SpecGroupNumber,
                    SpecName = flow.SpecName,
                    BuildType = flow.BuildType,
                    SerialNumber = flow.SerialNumber,
                    GlassRef = flow.GlassRef,
                    Qty = flow.Qty,
                    W1 = flow.W1,
                    H1 = flow.H1,
                    W2 = flow.W2,
                    H2 = flow.H2,
                    UnitSqm1 = flow.UnitSqm1,
                    TotalSqm1 = flow.TotalSqm1,
                    Lm1 = flow.Lm1,
                    UnitSqm2 = flow.UnitSqm2,
                    TotalSqm2 = flow.TotalSqm2,
                    Lm2 = flow.Lm2,
                    Inflow = agg.Inflow,
                    DoneQty = agg.Done,
                    BalanceQty = Math.Max(0, agg.Inflow - agg.Done),
                    WaitingOn = arrived ? "" : WaitingOnLabel(flow, stageInt),
                    FlowItems = stageItems,
                    AggregateStatus = !arrived || agg.Done == 0 ? 0
                        : agg.Done >= agg.Inflow ? 2 : 1
                };

                // Load that physically arrived at this stage.
                foreach (var fi in stageItems)
                {
                    unit.ArrivedArea += fi.Item.UnitSqm * fi.Inflow;
                    if (fi.Item.Qty > 0)
                        unit.ArrivedLm += fi.Item.TotalLm * fi.Inflow / fi.Item.Qty;
                }
                unit.ArrivedArea = Math.Round(unit.ArrivedArea, 3, MidpointRounding.AwayFromZero);
                unit.ArrivedLm = Math.Round(unit.ArrivedLm, 3, MidpointRounding.AwayFromZero);

                data.Units.Add(unit);
            }

            if (statusFilter.HasValue)
            {
                int wanted = (int)statusFilter.Value;
                data.Units = data.Units.Where(u => u.AggregateStatus == wanted).ToList();
            }

            return data;
        }

        private static bool Contains(string? value, string term) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        // ── Connectivity: chain counts + dispatch readiness ──────────────

        /// <summary>
        /// Dispatch readiness across all released jobs (or one job).
        /// Readiness is a QUANTITY: SGU pieces dispatch straight off the
        /// pane chain; multi-pane units dispatch what finished doubling.
        /// </summary>
        public static async Task<(int TotalUnits, int ReadyUnits, int TotalPieces, int ReadyPieces)>
            GetUnitReadinessAsync(string? jobRef = null)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            IQueryable<SavedProductionWorkItem> scope = db.WorkItems.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(jobRef))
                scope = scope.Where(i => i.JobRef == jobRef);

            var all = await scope.ToListAsync().ConfigureAwait(false);
            var flows = ComputeUnitFlows(all);

            return (
                flows.Count,
                flows.Count(f => f.IsDispatchReady),
                flows.Sum(f => f.Qty),
                flows.Sum(f => f.DispatchReadyQty));
        }

        /// <summary>
        /// Per-unit dispatch-ready pieces for one job — used by the
        /// delivery-note gateway so a DN can only deliver what production
        /// actually finished. Key = SpecGroupNumber|SerialNumber.
        /// Returns null when the job has no production release.
        /// </summary>
        public static async Task<Dictionary<(int Spec, int Serial), (int Ready, int Qty, string GlassRef)>?>
            GetDispatchReadyByRowAsync(string jobRef)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            var all = await db.WorkItems.AsNoTracking()
                .Where(i => i.JobRef == jobRef)
                .ToListAsync().ConfigureAwait(false);

            if (all.Count == 0) return null;

            var flows = ComputeUnitFlows(all);
            return flows.ToDictionary(
                f => (f.SpecGroupNumber, f.SerialNumber),
                f => (f.DispatchReadyQty, f.Qty, f.GlassRef));
        }

        // ── Floor writes (piece flow) ────────────────────────────────────

        /// <summary>
        /// Records how many pieces the floor completed on one stage item.
        /// The value is CLAMPED to the pieces that actually arrived from
        /// upstream — a stage can never report more done than it received
        /// (that is the tool chain). Status follows the quantity:
        /// 0 → Pending · partial → InProgress · full inflow → Completed.
        /// Every save writes a piece-flow ENTRY (who + when) so the audit
        /// trail shows which operator moved pieces through the factory.
        /// </summary>
        public static async Task<(bool Ok, int AppliedQty, string Message)> SetDoneQtyAsync(
            int itemId, int requestedQty)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();

            var row = await db.WorkItems.FindAsync(itemId).ConfigureAwait(false);
            if (row == null) return (false, 0, "Work item not found.");

            // Flow context: how many pieces arrived at this item's stage.
            var all = await db.WorkItems.AsNoTracking().ToListAsync().ConfigureAwait(false);
            var flows = ComputeUnitFlows(all);

            int inflow = row.Qty; // fallback = the item's own quantity
            var flowItem = flows
                .SelectMany(f => f.StageItems.Values.SelectMany(list => list))
                .FirstOrDefault(fi => fi.Item.Id == itemId);
            if (flowItem != null) inflow = flowItem.Inflow;

            int applied = Math.Clamp(requestedQty, 0, Math.Max(0, inflow));

            row.QtyDone = applied;
            row.Status = applied == 0 ? (int)WorkItemStatus.Pending
                : applied >= inflow && inflow > 0 ? (int)WorkItemStatus.Completed
                : (int)WorkItemStatus.InProgress;
            row.UpdatedAt = DateTime.Now;
            row.CompletedAt = row.Status == (int)WorkItemStatus.Completed ? DateTime.Now : null;
            if (row.Status == (int)WorkItemStatus.Completed)
                row.CompletedBy = AuditService.CurrentUsername;

            // Piece-flow entry: who marked these pieces done, and when.
            db.Entries.Add(new SavedProductionEntry
            {
                WorkItemId = row.Id,
                JobRef = row.JobRef,
                Stage = row.Stage,
                SpecGroupNumber = row.SpecGroupNumber,
                SerialNumber = row.SerialNumber,
                Position = row.Position,
                SpecName = row.SpecName,
                GlassRef = row.GlassRef,
                Qty = applied,
                DoneBy = AuditService.CurrentUsername,
                DoneAt = DateTime.Now
            });

            await db.SaveChangesAsync().ConfigureAwait(false);

            // Live refresh: every stage screen re-reads its list + detail.
            RaiseWorkItemsChanged();

            string message = requestedQty > inflow
                ? $"Only {inflow.ToString(System.Globalization.CultureInfo.InvariantCulture)} piece(s) " +
                  "have arrived from the previous stage — saved that many."
                : "";

            return (true, applied, message);
        }

        /// <summary>
        /// The floor audit trail for one unit at one stage (Outer + Inner
        /// entries together), newest first: "Qty done · by operator · time".
        /// </summary>
        public static async Task<List<SavedProductionEntry>> GetUnitEntriesAsync(
            string jobRef, int specGroupNumber, int serialNumber, ProductionStage stage)
        {
            EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Entries.AsNoTracking()
                .Where(e => e.JobRef == jobRef
                    && e.SpecGroupNumber == specGroupNumber
                    && e.SerialNumber == serialNumber
                    && e.Stage == (int)stage)
                .OrderByDescending(e => e.DoneAt)
                .Take(100)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}

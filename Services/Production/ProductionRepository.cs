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

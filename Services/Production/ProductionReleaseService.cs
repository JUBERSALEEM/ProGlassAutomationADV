// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionReleaseService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — release orchestration (Phase 3, 2026-09-11)
//
// "Release to Production": the factory pulls work EXCLUSIVELY from Job
// Orders — a Job Order only ever exists for a CONFIRMED (Approved) PI.
// Draft, Sent, Rejected, Expired or Cancelled PIs can never reach the
// floor, and neither can a PI that was never converted. Only non-void
// Job Orders whose status is Released or In Production are releasable;
// Draft JOs (not yet handed to the factory) and Completed/Delivered JOs
// (floor work over) are excluded.
//
// Feeds the embedded (immutable) invoice snapshot through
// ProductionDistributionEngine and persists the plan via
// ProductionRepository. Idempotent — re-releasing the same job refreshes
// geometry without disturbing floor statuses.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    /// <summary>One releasable Job Order shown in the release picker.</summary>
    public sealed class ProductionReleaseSource
    {
        /// <summary>Always "JO" — production runs from confirmed Job Orders only.</summary>
        public string Kind => "JO";
        public string Reference { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string ProjectName { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime SortDate { get; init; }

        public string DisplayLabel =>
            $"{Reference} · {(string.IsNullOrWhiteSpace(CustomerName) ? "—" : CustomerName)}" +
            (string.IsNullOrWhiteSpace(Status) ? "" : $" ({Status})");
    }

    public static class ProductionReleaseService
    {
        /// <summary>
        /// Job Orders the factory can pull work from: non-void, status
        /// Released or In Production (the only states where floor work is
        /// legitimate). Never returns Draft/Completed/Delivered jobs.
        /// </summary>
        public static async Task<List<ProductionReleaseSource>> GetSourcesAsync()
        {
            var sources = new List<ProductionReleaseSource>();

            try
            {
                using var db = new JobOrdersDbContext();
                var jobs = await db.JobOrders
                    .AsNoTracking()
                    .Where(j => !j.IsVoid)
                    .OrderByDescending(j => j.UpdatedAt)
                    .Select(j => new
                    {
                        j.JobOrderId, j.CustomerName, j.ProjectName, j.Status, j.UpdatedAt
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                // Status comparison via JobOrderStatusValues — the house rule
                // (the InProduction/legacy "In Production" spelling split).
                sources.AddRange(jobs
                    .Where(j => JobOrderStatusValues.Is(j.Status, JobOrderStatusValues.Released)
                             || JobOrderStatusValues.IsInProduction(j.Status))
                    .Select(j => new ProductionReleaseSource
                    {
                        Reference = j.JobOrderId ?? "",
                        CustomerName = j.CustomerName ?? "",
                        ProjectName = j.ProjectName ?? "",
                        Status = JobOrderStatusValues.IsInProduction(j.Status)
                            ? JobOrderStatusValues.InProductionDisplay
                            : j.Status ?? "",
                        SortDate = j.UpdatedAt
                    }));
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Production release sources could not be listed.",
                    ex,
                    new LogContext
                    {
                        Module = "Production",
                        Class = nameof(ProductionReleaseService),
                        Operation = nameof(GetSourcesAsync)
                    });
            }

            return sources
                .OrderByDescending(s => s.SortDate)
                .ThenBy(s => s.Reference, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Distributes one Job Order into production.db and returns the
        /// outcome. Never throws — failures come back as Success=false.
        /// </summary>
        public static async Task<(bool Success, string Message, ProductionReleaseResult? Result)>
            ReleaseAsync(ProductionReleaseSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Reference))
                return (false, "Pick a Job Order to release.", null);

            try
            {
                var json = await LoadJobOrderJsonAsync(source).ConfigureAwait(false);
                var kind = ProductionSnapshotIntegrity.ReadJobOrderJson(
                    json, out var snapshot, out string snapMsg);

                if (kind == ProductionSnapshotKind.Corrupt)
                    return (false,
                        $"Cannot release {source.Reference}: {snapMsg}.", null);
                if (kind == ProductionSnapshotKind.Missing || snapshot == null)
                    return (false,
                        $"Could not read the invoice snapshot behind {source.Reference}.", null);
                if (kind == ProductionSnapshotKind.ValidEmpty)
                    return (false,
                        $"Nothing to distribute for {source.Reference} — {snapMsg} (empty is not corrupt).", null);

                var plan = ProductionDistributionEngine.Distribute(source.Reference, snapshot);
                if (plan.ItemCount == 0)
                {
                    string reason = plan.HasWarnings ? plan.Warnings[0] : "no spec rows found";
                    return (false, $"Nothing to distribute for {source.Reference} — {reason}.", null);
                }

                var result = await ProductionRepository.ReleaseAsync(plan).ConfigureAwait(false);

                AppLogService.Info(
                    $"Production release {source.Reference}: " +
                    $"{result.Inserted} new, {result.Updated} refreshed, " +
                    $"{result.RemovedStalePending} stale removed.",
                    new LogContext
                    {
                        Module = "Production",
                        Class = nameof(ProductionReleaseService),
                        Operation = nameof(ReleaseAsync)
                    });

                return (true,
                    $"{source.Reference} released: {result.Inserted} new work item(s), " +
                    $"{result.Updated} refreshed, {result.RemovedStalePending} stale pending removed.",
                    result);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    $"Production release failed for {source.Reference}.",
                    ex,
                    new LogContext
                    {
                        Module = "Production",
                        Class = nameof(ProductionReleaseService),
                        Operation = nameof(ReleaseAsync)
                    });
                return (false, $"Release failed: {ex.Message}", null);
            }
        }

        private static async Task<string?> LoadJobOrderJsonAsync(
            ProductionReleaseSource source)
        {
            using var db = new JobOrdersDbContext();
            var job = await db.JobOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.JobOrderId == source.Reference)
                .ConfigureAwait(false);

            return job?.FullSnapshotJson;
        }
    }
}

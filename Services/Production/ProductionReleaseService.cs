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
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.JobOrder.Mapper;
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

        /// <summary>
        /// Job-order name first, then the job number and customer. A picker
        /// that falls back to <see cref="object.ToString"/> must still show
        /// this — never the CLR type name.
        /// </summary>
        public string DisplayLabel
        {
            get
            {
                string name = UsableJobOrderName(ProjectName);
                string reference = (Reference ?? "").Trim();
                string customer = (CustomerName ?? "").Trim();
                var parts = new List<string>(3);
                if (name.Length > 0) parts.Add(name);
                if (reference.Length > 0
                    && !string.Equals(reference, name, StringComparison.OrdinalIgnoreCase))
                    parts.Add(reference);
                if (customer.Length > 0) parts.Add(customer);
                string core = parts.Count == 0 ? "Job order" : string.Join(" · ", parts);
                string status = (Status ?? "").Trim();
                return status.Length == 0 ? core : core + " (" + status + ")";
            }
        }

        /// <summary>Defence against the release picker showing a type name.</summary>
        public override string ToString() => DisplayLabel;

        /// <summary>
        /// A stored name that is blank or a CLR type name ("String",
        /// "System.String", "ProGlassAutomation.…") is not a job-order name.
        /// </summary>
        public static string UsableJobOrderName(string? value)
        {
            string name = (value ?? "").Trim();
            if (name.Length == 0) return "";
            if (name.Equals("String", StringComparison.OrdinalIgnoreCase)
                || name.Equals("System.String", StringComparison.OrdinalIgnoreCase))
                return "";
            if (name.StartsWith("ProGlassAutomation.", StringComparison.Ordinal)
                || name.StartsWith("System.", StringComparison.Ordinal))
                return "";
            return name;
        }
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

                var releasable = jobs
                    .Where(j => JobOrderStatusValues.Is(j.Status, JobOrderStatusValues.Released)
                             || JobOrderStatusValues.IsInProduction(j.Status))
                    .ToList();

                // Indexed ProjectName is the name. When that column is blank or
                // a type-name leak, read the embedded project (identifier, then
                // reference, then contract) — only for those rows.
                var needsName = new List<string>();
                foreach (var job in releasable)
                {
                    if (string.IsNullOrWhiteSpace(job.JobOrderId)) continue;
                    if (ProductionReleaseSource.UsableJobOrderName(job.ProjectName).Length == 0)
                        needsName.Add(job.JobOrderId);
                }
                var namesFromSnapshot = needsName.Count == 0
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : await ResolveNamesFromSnapshotsAsync(db, needsName).ConfigureAwait(false);

                // Status comparison via JobOrderStatusValues — the house rule
                // (the InProduction/legacy "In Production" spelling split).
                sources.AddRange(releasable.Select(j =>
                {
                    string stored = ProductionReleaseSource.UsableJobOrderName(j.ProjectName);
                    string project = stored.Length > 0
                        ? stored
                        : (namesFromSnapshot.TryGetValue(j.JobOrderId ?? "", out string? resolved)
                            ? resolved
                            : "");
                    return new ProductionReleaseSource
                    {
                        Reference = j.JobOrderId ?? "",
                        CustomerName = j.CustomerName ?? "",
                        ProjectName = project,
                        Status = JobOrderStatusValues.IsInProduction(j.Status)
                            ? JobOrderStatusValues.InProductionDisplay
                            : j.Status ?? "",
                        SortDate = j.UpdatedAt
                    };
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
                    $"{source.DisplayLabel} released: {result.Inserted} new work item(s), " +
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

        private static async Task<Dictionary<string, string>> ResolveNamesFromSnapshotsAsync(
            JobOrdersDbContext db, List<string> jobOrderIds)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rows = await db.JobOrders
                .AsNoTracking()
                .Where(j => jobOrderIds.Contains(j.JobOrderId))
                .Select(j => new { j.JobOrderId, j.FullSnapshotJson })
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.JobOrderId)) continue;
                var snap = JobOrderMapper.DeserializeSnapshot(new SavedJobOrder
                {
                    JobOrderId = row.JobOrderId,
                    FullSnapshotJson = row.FullSnapshotJson ?? ""
                });
                string name = ProductionReleaseSource.UsableJobOrderName(
                    JobOrderMapper.ResolveProjectName(snap?.EmbeddedInvoice?.Project));
                if (name.Length > 0) names[row.JobOrderId] = name;
            }

            return names;
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

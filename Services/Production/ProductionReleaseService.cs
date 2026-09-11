// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionReleaseService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — release orchestration (Phase 3, 2026-09-11)
//
// "Release to Production": lists what can be released (Released /
// In-Production Job Orders and Approved Proforma Invoices), feeds the
// embedded invoice snapshot through ProductionDistributionEngine, and
// persists the plan via ProductionRepository. Idempotent — re-releasing
// the same job refreshes geometry without disturbing floor statuses.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    /// <summary>One releasable source shown in the release picker.</summary>
    public sealed class ProductionReleaseSource
    {
        /// <summary>"JO" or "PI".</summary>
        public string Kind { get; init; } = "";
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
        private static readonly JsonSerializerOptions JsonOptions = new();

        /// <summary>
        /// Everything the factory can pull work from: live Job Orders first
        /// (the primary flow), then Approved PIs that have no Job Order yet
        /// (convenience path).
        /// </summary>
        public static async Task<List<ProductionReleaseSource>> GetSourcesAsync()
        {
            var sources = new List<ProductionReleaseSource>();

            try
            {
                using (var db = new JobOrdersDbContext())
                {
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

                    sources.AddRange(jobs.Select(j => new ProductionReleaseSource
                    {
                        Kind = "JO",
                        Reference = j.JobOrderId ?? "",
                        CustomerName = j.CustomerName ?? "",
                        ProjectName = j.ProjectName ?? "",
                        Status = j.Status ?? "",
                        SortDate = j.UpdatedAt
                    }));
                }

                using (var db = new ProformaInvoicesDbContext())
                {
                    var pis = await db.ProformaInvoices
                        .AsNoTracking()
                        .Where(p => !p.IsVoid && p.Status == "Approved")
                        .OrderByDescending(p => p.UpdatedAt)
                        .Select(p => new
                        {
                            p.ProformaId, p.CustomerName, p.ProjectName, p.Status, p.UpdatedAt
                        })
                        .ToListAsync()
                        .ConfigureAwait(false);

                    sources.AddRange(pis.Select(p => new ProductionReleaseSource
                    {
                        Kind = "PI",
                        Reference = p.ProformaId ?? "",
                        CustomerName = p.CustomerName ?? "",
                        ProjectName = p.ProjectName ?? "",
                        Status = p.Status ?? "",
                        SortDate = p.UpdatedAt
                    }));
                }
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
        /// Distributes one source into production.db and returns the outcome.
        /// Never throws — failures come back as a result with Success=false.
        /// </summary>
        public static async Task<(bool Success, string Message, ProductionReleaseResult? Result)>
            ReleaseAsync(ProductionReleaseSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Reference))
                return (false, "Pick a Job Order or Proforma Invoice to release.", null);

            try
            {
                var snapshot = await LoadSnapshotAsync(source).ConfigureAwait(false);
                if (snapshot == null)
                {
                    return (false,
                        $"Could not read the invoice snapshot behind {source.Reference}.", null);
                }

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

        private static async Task<ProformaInvoiceSnapshot?> LoadSnapshotAsync(
            ProductionReleaseSource source)
        {
            if (source.Kind == "JO")
            {
                using var db = new JobOrdersDbContext();
                var job = await db.JobOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.JobOrderId == source.Reference)
                    .ConfigureAwait(false);

                if (job == null || string.IsNullOrWhiteSpace(job.FullSnapshotJson)) return null;

                var joSnapshot = JsonSerializer.Deserialize<JobOrderSnapshot>(
                    job.FullSnapshotJson, JsonOptions);
                return joSnapshot?.EmbeddedInvoice;
            }

            using (var db = new ProformaInvoicesDbContext())
            {
                var pi = await db.ProformaInvoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProformaId == source.Reference)
                    .ConfigureAwait(false);

                if (pi == null || string.IsNullOrWhiteSpace(pi.FullSnapshotJson)) return null;

                return JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                    pi.FullSnapshotJson, JsonOptions);
            }
        }
    }
}

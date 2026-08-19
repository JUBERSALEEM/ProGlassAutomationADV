using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.JobOrder.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.ProformaInvoice;

namespace ProGlassAutomation.Services.JobOrder.Repository
{
    /// <summary>
    /// SQLite / EF Core implementation of IJobOrderRepository.
    /// Follows project convention: one DbContext per operation via `using`.
    /// </summary>
    public class JobOrderRepository : IJobOrderRepository
    {
        public async Task InitializeAsync()
        {
            using var db = new JobOrdersDbContext();
            await db.Database.EnsureCreatedAsync();

            // Additive schema upgrades for an existing joborders.db.
            SchemaMigrationRunner.Upgrade(
                JobOrdersDbContext.DatabaseFilePath,
                AppSchemaMigrations.JobOrders,
                "Job Order");
        }

        // ── CRUD ──────────────────────────────────────────────────────────

        public async Task<List<SavedJobOrder>> GetAllAsync()
        {
            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .AsNoTracking()
                .OrderByDescending(j => j.UpdatedAt)
                .ToListAsync();
        }

        public async Task<SavedJobOrder?> GetByIdAsync(int id)
        {
            using var db = new JobOrdersDbContext();
            return await db.JobOrders.FindAsync(id);
        }

        public async Task<SavedJobOrder?> GetByJobOrderIdAsync(string jobOrderId)
        {
            if (string.IsNullOrWhiteSpace(jobOrderId)) return null;

            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .FirstOrDefaultAsync(j => j.JobOrderId == jobOrderId);
        }

        public async Task<SavedJobOrder> InsertAsync(SavedJobOrder jobOrder)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderEdit, "create a job order");

            using var db = new JobOrdersDbContext();
            jobOrder.CreatedAt = DateTime.Now;
            jobOrder.UpdatedAt = DateTime.Now;
            db.JobOrders.Add(jobOrder);
            await db.SaveChangesAsync();
            DocumentLifecycle.Raise();
            return jobOrder;
        }

        public async Task UpdateAsync(SavedJobOrder jobOrder)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderEdit, "update a job order");

            using var db = new JobOrdersDbContext();
            jobOrder.UpdatedAt = DateTime.Now;
            db.JobOrders.Update(jobOrder);
            await db.SaveChangesAsync();
            DocumentLifecycle.Raise();
        }

        public async Task DeleteAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderDelete, "delete a job order");

            using var db = new JobOrdersDbContext();
            var job = await db.JobOrders.FindAsync(id);
            if (job == null) return;

            string? sourcePi = job.SourceProformaId;
            string? jobOrderId = job.JobOrderId;

            db.JobOrders.Remove(job);
            await db.SaveChangesAsync();
            DocumentLifecycle.Raise();

            await DeleteMasterDnQuietlyAsync(jobOrderId);
            await ProformaLifecycleSync.AfterDownstreamChangeAsync(sourcePi);
        }

        /// <summary>
        /// Master DNs are internal and excluded from GetByJobOrderIdAsync,
        /// so they never block JO delete. Remove the leftover tracker so
        /// the delivery-note database does not keep an orphan MDN.
        /// Failures are swallowed — the JO is already gone.
        /// </summary>
        private static async Task DeleteMasterDnQuietlyAsync(string? jobOrderId)
        {
            if (string.IsNullOrWhiteSpace(jobOrderId) || App.DeliveryNotes == null)
                return;

            try
            {
                var master = await App.DeliveryNotes
                    .GetMasterDnForJobOrderAsync(jobOrderId);
                if (master != null)
                    await App.DeliveryNotes.DeleteAsync(master.Id);
            }
            catch
            {
                // Best-effort cleanup. A leftover Master DN is hidden
                // from every public list and does not keep the PI open.
            }
        }

        // ── DUPLICATE DETECTION ───────────────────────────────────────────

        public async Task<bool> ExistsByJobOrderIdAsync(string jobOrderId)
        {
            if (string.IsNullOrWhiteSpace(jobOrderId)) return false;

            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .AnyAsync(j => j.JobOrderId == jobOrderId);
        }

        public async Task<string> SuggestNextIdAsync(string baseId)
        {
            if (string.IsNullOrWhiteSpace(baseId)) return baseId;

            // Parse trailing number, e.g. "JOB-2026-001" → prefix "JOB-2026-",
            // numeric "001" (width 3). Increment until an unused ID is found.
            int lastDash = baseId.LastIndexOf('-');
            if (lastDash < 0 || lastDash == baseId.Length - 1)
                return baseId + "-002";

            string prefix = baseId.Substring(0, lastDash + 1);
            string numericPart = baseId.Substring(lastDash + 1);

            if (!int.TryParse(numericPart, out int currentNum))
                return baseId + "-002";

            int width = numericPart.Length;

            using var db = new JobOrdersDbContext();

            int candidate = currentNum + 1;
            string candidateId;
            while (true)
            {
                candidateId = prefix + candidate.ToString().PadLeft(width, '0');
                bool exists = await db.JobOrders
                    .AnyAsync(j => j.JobOrderId == candidateId);
                if (!exists) return candidateId;
                candidate++;

                // safety net — very unlikely
                if (candidate > 99999) return prefix + Guid.NewGuid().ToString("N").Substring(0, 8);
            }
        }

        // ── ANALYTICS PROJECTION ─────────────────────────────────────────

        public async Task<List<JobOrderAnalyticsRow>> GetAnalyticsRowsAsync()
        {
            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .AsNoTracking()
                .Select(j => new JobOrderAnalyticsRow
                {
                    JobOrderId = j.JobOrderId,
                    Status = j.Status,
                    DeliveryDate = j.DeliveryDate
                })
                .ToListAsync();
        }

        // ── SEARCH (bounded, DB-side) ─────────────────────────────────────

        public async Task<List<JobOrderSearchRow>> SearchAsync(
            string term, int take)
        {
            using var db = new JobOrdersDbContext();

            string q = term?.Trim() ?? "";
            if (string.IsNullOrEmpty(q) || take <= 0)
                return new List<JobOrderSearchRow>();

            // Escape LIKE wildcards so DB-side search matches the previous
            // in-memory IndexOf(OrdinalIgnoreCase) semantics for literal terms.
            string like = q.Replace("%", "[%]").Replace("_", "[_]");
            string pattern = $"%{like}%";

            return await db.JobOrders
                .AsNoTracking()
                .Where(j =>
                    EF.Functions.Like(j.JobOrderId ?? "", pattern) ||
                    EF.Functions.Like(j.CustomerName ?? "", pattern) ||
                    EF.Functions.Like(j.ProjectName ?? "", pattern) ||
                    EF.Functions.Like(j.SourceProformaId ?? "", pattern))
                .OrderByDescending(j => j.UpdatedAt)
                .Take(take)
                .Select(j => new JobOrderSearchRow
                {
                    JobOrderId = j.JobOrderId,
                    CustomerName = j.CustomerName,
                    ProjectName = j.ProjectName,
                    SourceProformaId = j.SourceProformaId,
                    Status = j.Status
                })
                .ToListAsync();
        }

        // ── LOOKUP FROM PI ────────────────────────────────────────────────

        public async Task<List<SavedJobOrder>> GetBySourceProformaIdAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<SavedJobOrder>();

            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .Where(j => j.SourceProformaId == proformaId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }
    }
}
using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.JobOrder.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

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
        }

        // ── CRUD ──────────────────────────────────────────────────────────

        public async Task<List<SavedJobOrder>> GetAllAsync()
        {
            using var db = new JobOrdersDbContext();
            return await db.JobOrders
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
        }

        public async Task DeleteAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderDelete, "delete a job order");

            using var db = new JobOrdersDbContext();
            var job = await db.JobOrders.FindAsync(id);
            if (job != null)
            {
                db.JobOrders.Remove(job);
                await db.SaveChangesAsync();
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
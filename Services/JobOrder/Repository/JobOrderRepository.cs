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
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.Logging;

using ProGlassAutomation.Services.Licensing;         // ReadOnlyGate
using ProGlassAutomation.Services.Workflow;
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
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<SavedJobOrder?> GetByIdAsync(int id)
        {
            using var db = new JobOrdersDbContext();
            return await db.JobOrders.FindAsync(id).ConfigureAwait(false);
        }

        public async Task<List<JobOrderListRow>> GetListRowsAsync()
        {
            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .AsNoTracking()
                .OrderByDescending(j => j.UpdatedAt)
                .Select(j => new JobOrderListRow
                {
                    Id = j.Id,
                    JobOrderId = j.JobOrderId ?? "",
                    SourceProformaId = j.SourceProformaId ?? "",
                    CustomerName = j.CustomerName ?? "",
                    ProjectName = j.ProjectName ?? "",
                    GrandTotalVatInclusive = j.GrandTotalVatInclusive,
                    Status = j.Status ?? "",
                    Priority = j.Priority ?? "",
                    DeliveryDate = j.DeliveryDate,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt,
                    IsVoid = j.IsVoid,
                    RowVersion = j.RowVersion
                })
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<SavedJobOrder?> GetByJobOrderIdAsync(string jobOrderId)
        {
            if (string.IsNullOrWhiteSpace(jobOrderId)) return null;

            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .FirstOrDefaultAsync(j => j.JobOrderId == jobOrderId).ConfigureAwait(false);
        }

        public async Task<SavedJobOrder> InsertAsync(SavedJobOrder jobOrder)
        {
            ReadOnlyGate.Demand("create a job order");
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderEdit, "create a job order");

            if (jobOrder == null)
                throw new ArgumentNullException(nameof(jobOrder));
            if (jobOrder.IsVoid
                || DocumentLifecyclePolicy.HasVoidMetadata(
                    jobOrder.VoidedAtUtc, jobOrder.VoidedBy, jobOrder.VoidReason)
                || !DocumentLifecyclePolicy.CanEditBody(
                    StatusTransitions.Kind.JobOrder, jobOrder.Status))
                throw new InvalidOperationException(
                    "A new Job Order must be non-void and in an editable workflow status.");

            bool idReallocated = false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                await using var db = new JobOrdersDbContext();

                bool exists = await db.JobOrders
                    .AnyAsync(j => j.JobOrderId == jobOrder.JobOrderId)
                    .ConfigureAwait(false);
                if (exists)
                {
                    jobOrder.JobOrderId = await SuggestNextIdAsync(
                        jobOrder.JobOrderId).ConfigureAwait(false);
                    idReallocated = true;
                    continue;
                }

                if (idReallocated)
                    SyncSnapshotJobOrderId(jobOrder);

                jobOrder.CreatedAt = DateTime.Now;
                jobOrder.UpdatedAt = DateTime.Now;
                db.JobOrders.Add(jobOrder);
                try
                {
                    await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync())
                        .ConfigureAwait(false);
                }
                catch (DbUpdateException ex)
                    when (SqliteErrorClassifier.IsUnique(ex) && attempt < 4)
                {
                    db.Entry(jobOrder).State = EntityState.Detached;
                    jobOrder.JobOrderId = await SuggestNextIdAsync(
                        jobOrder.JobOrderId).ConfigureAwait(false);
                    jobOrder.Id = 0;
                    idReallocated = true;
                    continue;
                }

                DocumentLifecycle.Raise();
                return jobOrder;
            }

            throw new InvalidOperationException(
                $"Could not allocate a free JobOrderId after 5 retries " +
                $"starting from '{jobOrder.JobOrderId}'.");
        }

        private static void SyncSnapshotJobOrderId(SavedJobOrder jobOrder)
        {
            if (jobOrder == null || string.IsNullOrWhiteSpace(jobOrder.JobOrderId))
                return;
            try
            {
                var snap = JobOrderMapper.DeserializeSnapshot(jobOrder);
                if (snap == null) return;
                if (string.Equals(snap.JobOrderId, jobOrder.JobOrderId,
                        StringComparison.Ordinal))
                    return;
                snap.JobOrderId = jobOrder.JobOrderId;
                jobOrder.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(snap);
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    "Could not sync JO snapshot JobOrderId after reallocation: " +
                    ex.Message);
            }
        }

        /// <summary>
        /// Updates a job order using the version captured by the caller. EF's
        /// concurrency-token predicate closes the check-then-write race.
        /// </summary>
        public async Task UpdateAsync(SavedJobOrder jobOrder, int expectedRowVersion)
        {
            ReadOnlyGate.Demand("update a job order");
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderEdit, "update a job order");

            if (jobOrder == null)
                throw new ArgumentNullException(nameof(jobOrder));

            using var db = new JobOrdersDbContext();
            var stored = await db.JobOrders
                .FirstOrDefaultAsync(x => x.Id == jobOrder.Id).ConfigureAwait(false);
            if (stored == null)
                throw new InvalidOperationException("The Job Order no longer exists.");
            if (stored.IsVoid || !DocumentLifecyclePolicy.CanEditBody(
                    StatusTransitions.Kind.JobOrder, stored.Status))
                throw new InvalidOperationException(
                    $"Job Order {stored.JobOrderId} is voided or its lifecycle status locks the body.");
            if (!string.Equals(stored.Status, jobOrder.Status,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Job Order status changes must use the validated status-transition operation.");
            if (!string.Equals(stored.JobOrderId, jobOrder.JobOrderId,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.SourceProformaId, jobOrder.SourceProformaId,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Job Order number and source Proforma link are immutable.");

            var incomingSnapshot = JobOrderMapper.DeserializeSnapshot(jobOrder);
            var storedSnapshot = JobOrderMapper.DeserializeSnapshot(stored);
            if (incomingSnapshot == null || storedSnapshot == null
                || storedSnapshot.EmbeddedInvoice == null)
                throw new InvalidOperationException(
                    $"Job Order {stored.JobOrderId} has a missing or unreadable snapshot/source invoice; the update was not saved.");

            // The JSON repeats the indexed identity/status fields and embeds
            // the source PI. Keep those values canonical even for direct
            // repository callers that bypass the editor mapper.
            incomingSnapshot.JobOrderId = stored.JobOrderId;
            incomingSnapshot.SourceProformaId = stored.SourceProformaId;
            incomingSnapshot.EmbeddedInvoice = storedSnapshot.EmbeddedInvoice;
            incomingSnapshot.Production ??= storedSnapshot.Production ?? new();
            incomingSnapshot.Production.Status = stored.Status;
            incomingSnapshot.Production.Priority = jobOrder.Priority;
            incomingSnapshot.Production.DeliveryDate = jobOrder.DeliveryDate;

            if (stored.RowVersion != expectedRowVersion)
                throw new Services.Database.ConcurrencyConflictException(
                    "Job Order", stored.JobOrderId,
                    expectedRowVersion, stored.RowVersion);

            // Copy only mutable production fields. Immutable identity, source
            // invoice, fiscal totals, and void metadata are never overwritten
            // from a detached UI object.
            stored.Priority = jobOrder.Priority;
            stored.DeliveryDate = jobOrder.DeliveryDate;
            stored.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(incomingSnapshot);
            stored.RowVersion += 1;
            stored.UpdatedAt = DateTime.Now;

            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(db, stored.Id).ConfigureAwait(false);
                throw new Services.Database.ConcurrencyConflictException(
                    "Job Order", stored.JobOrderId, expectedRowVersion, actual);
            }
            jobOrder.Status = stored.Status;
            jobOrder.JobOrderId = stored.JobOrderId;
            jobOrder.SourceProformaId = stored.SourceProformaId;
            jobOrder.FullSnapshotJson = stored.FullSnapshotJson;
            jobOrder.RowVersion = stored.RowVersion;
            jobOrder.UpdatedAt = stored.UpdatedAt;
            DocumentLifecycle.Raise();
        }

        public async Task<SavedJobOrder?> UpdateStatusAsync(
            int id, string newStatus, int expectedRowVersion)
        {
            ReadOnlyGate.Demand("change a job order status");
            OperationGuard.Demand(
                AppPermissions.JobOrderEdit, "change a job order status");
            if (string.IsNullOrWhiteSpace(newStatus))
                throw new ArgumentException("A target status is required.", nameof(newStatus));

            using var db = new JobOrdersDbContext();
            var stored = await db.JobOrders
                .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
            if (stored == null) return null;
            if (stored.RowVersion != expectedRowVersion)
                throw new Services.Database.ConcurrencyConflictException(
                    "Job Order", stored.JobOrderId, expectedRowVersion, stored.RowVersion);
            if (stored.IsVoid)
                throw new InvalidOperationException(
                    $"Job Order {stored.JobOrderId} is voided and cannot change status.");

            string next = newStatus.Trim();
            var transition = StatusTransitions.Validate(
                StatusTransitions.Kind.JobOrder, stored.Status, next);
            if (!transition.IsValid)
                throw new InvalidOperationException(
                    transition.Reason ?? "Invalid Job Order status transition.");
            if (string.Equals(stored.Status, next, StringComparison.OrdinalIgnoreCase))
                return stored;

            string previous = stored.Status;
            var snapshot = JobOrderMapper.DeserializeSnapshot(stored)
                ?? throw new InvalidOperationException(
                    $"Job Order {stored.JobOrderId} has an invalid snapshot; status was not changed.");
            snapshot.Production ??= new Models.JobOrder.ProductionDetails.ProductionDetails();
            snapshot.Production.Status = next;
            snapshot.Production.LastEditedAt = DateTime.Now;
            snapshot.Production.LastEditedBy = AuditService.CurrentUsername;

            stored.Status = next;
            stored.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(snapshot);
            stored.UpdatedAt = DateTime.Now;
            stored.RowVersion += 1;
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(db, stored.Id).ConfigureAwait(false);
                throw new Services.Database.ConcurrencyConflictException(
                    "Job Order", stored.JobOrderId, expectedRowVersion, actual);
            }

            AuditService.Log(
                AuditSections.JobOrder, "Job Order status changed",
                $"{stored.JobOrderId}: {previous} → {next}",
                entityType: nameof(SavedJobOrder), entityId: stored.JobOrderId);
            DocumentLifecycle.Raise();
            return stored;
        }

        public async Task DeleteAsync(int id)
        {
            ReadOnlyGate.Demand("delete a job order");
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.JobOrderDelete, "delete a job order");

            using var db = new JobOrdersDbContext();
            var job = await db.JobOrders.FindAsync(id).ConfigureAwait(false);
            if (job == null) return;

            string? sourcePi = job.SourceProformaId;
            string? jobOrderId = job.JobOrderId;
            if (job.IsVoid)
                throw new InvalidOperationException(
                    $"Job Order {jobOrderId} is already voided and must be retained.");

            bool hasLinkedDeliveryNotes = false;
            if (!string.IsNullOrWhiteSpace(jobOrderId))
            {
                if (App.DeliveryNotes == null)
                    throw new InvalidOperationException(
                        "Cannot verify linked Delivery Notes; the delivery repository is unavailable.");
                hasLinkedDeliveryNotes = (await App.DeliveryNotes
                    .GetByJobOrderIdAsync(jobOrderId).ConfigureAwait(false)).Count > 0;
            }

            var decision = DocumentLifecyclePolicy.DecideDelete(
                StatusTransitions.Kind.JobOrder, job.Status,
                hasLinkedDeliveryNotes);
            if (decision == DocumentLifecyclePolicy.DeleteDecision.Blocked)
                throw new InvalidOperationException(
                    $"Job Order {jobOrderId} has linked Delivery Notes and cannot be deleted or voided until those records are handled.");
            if (decision == DocumentLifecyclePolicy.DeleteDecision.Retained)
                throw new InvalidOperationException(
                    $"Job Order {jobOrderId} cannot be safely deleted or voided from status '{job.Status}' and must be retained.");
            if (decision == DocumentLifecyclePolicy.DeleteDecision.Void)
            {
                await VoidAsync(id, "Delete requested; lifecycle policy requires retention.")
                    .ConfigureAwait(false);
                return;
            }

            db.JobOrders.Remove(job);
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            AuditService.Log(
                AuditSections.JobOrder, "Job Order deleted",
                $"Deleted draft Job Order {jobOrderId}"
                    + (string.IsNullOrWhiteSpace(sourcePi) ? "" : $" (source PI {sourcePi})"),
                entityType: nameof(SavedJobOrder), entityId: jobOrderId ?? "");

            await DeleteMasterDnQuietlyAsync(jobOrderId);

            // Run the lifecycle sync BEFORE raising the change signal so a
            // PI list refresh sees the final (cancelled) status the first
            // time — no stale "PI confirmed / PI created" frame.
            await ProformaLifecycleSync.AfterDownstreamChangeAsync(sourcePi);
            DocumentLifecycle.Raise();
        }

        /// <summary>
        /// ERP lifecycle hardening (Phase 1): soft-close (void) instead of a
        /// physical delete. Keeps the row (job-order number + immutable PI
        /// snapshot + audit trail survive); Status moves to the void target
        /// (Rejected). The master DN is intentionally left intact — the job
        /// order still exists, it is simply cancelled.
        /// </summary>
        public async Task VoidAsync(int id, string reason)
        {
            ReadOnlyGate.Demand("void a job order");
            OperationGuard.Demand(
                AppPermissions.JobOrderDelete, "void a job order");

            using var db = new JobOrdersDbContext();
            var job = await db.JobOrders.FindAsync(id).ConfigureAwait(false);
            if (job == null) return;
            if (job.IsVoid) return;   // idempotent
            if (!DocumentLifecyclePolicy.CanVoid(
                    StatusTransitions.Kind.JobOrder, job.Status))
                throw new InvalidOperationException(
                    $"Job Order {job.JobOrderId} cannot be voided from status '{job.Status}'; it must be retained.");

            string? sourcePi = job.SourceProformaId;
            if (!string.IsNullOrWhiteSpace(job.JobOrderId))
            {
                if (App.DeliveryNotes == null)
                    throw new InvalidOperationException(
                        "Cannot verify linked Delivery Notes; the delivery repository is unavailable.");
                if ((await App.DeliveryNotes.GetByJobOrderIdAsync(job.JobOrderId)
                        .ConfigureAwait(false)).Count > 0)
                    throw new InvalidOperationException(
                        $"Job Order {job.JobOrderId} cannot be voided while linked Delivery Notes remain.");
            }

            job.IsVoid = true;
            job.VoidedAtUtc = DateTime.UtcNow;
            job.VoidedBy = AuditService.CurrentUsername;
            job.VoidReason = DocumentLifecyclePolicy.NormalizeVoidReason(reason);
            job.Status = Services.Workflow.DocumentLifecyclePolicy.VoidTargetStatus(
                Services.Workflow.StatusTransitions.Kind.JobOrder);
            job.UpdatedAt = DateTime.Now;
            job.RowVersion += 1;

            var snapshot = JobOrderMapper.DeserializeSnapshot(job);
            if (snapshot?.Production != null)
            {
                snapshot.Production.Status = job.Status;
                snapshot.Production.LastEditedAt = job.UpdatedAt;
                snapshot.Production.LastEditedBy = AuditService.CurrentUsername;
                job.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(snapshot);
            }
            else
            {
                AppLogService.Warning(
                    $"Job Order {job.JobOrderId} was voided but its snapshot status could not be refreshed.");
            }

            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            AuditService.Log(
                AuditSections.JobOrder, "Job Order voided",
                $"Voided Job Order {job.JobOrderId}",
                entityType: nameof(SavedJobOrder), entityId: job.JobOrderId,
                reason: job.VoidReason);

            // Recover the parent PI's derived status just like a delete would.
            await ProformaLifecycleSync.AfterDownstreamChangeAsync(sourcePi);
            DocumentLifecycle.Raise();
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
            catch (System.Exception ex)
            {
                // Best-effort cleanup. A leftover Master DN is hidden
                // from every public list and does not keep the PI open.
                ProGlassAutomation.Services.Logging.AppLogService.Warning("[JobOrderRepository] Master-DN quiet cleanup failed: " + ex.Message);
            }
        }

        // ── DUPLICATE DETECTION ───────────────────────────────────────────

        public async Task<bool> ExistsByJobOrderIdAsync(string jobOrderId)
        {
            if (string.IsNullOrWhiteSpace(jobOrderId)) return false;

            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .AnyAsync(j => j.JobOrderId == jobOrderId).ConfigureAwait(false);
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
                    .AnyAsync(j => j.JobOrderId == candidateId).ConfigureAwait(false);
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
                .ToListAsync().ConfigureAwait(false);
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
                .ToListAsync().ConfigureAwait(false);
        }

        private static async Task<int> GetCurrentRowVersionAsync(
            JobOrdersDbContext db, int id)
        {
            return await db.JobOrders.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => (int?)x.RowVersion)
                .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
        }

        // ── LOOKUP FROM PI ────────────────────────────────────────────────

        public async Task<List<SavedJobOrder>> GetBySourceProformaIdAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<SavedJobOrder>();

            using var db = new JobOrdersDbContext();
            return await db.JobOrders
                .Where(j => j.SourceProformaId == proformaId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync().ConfigureAwait(false);
        }
    }
}
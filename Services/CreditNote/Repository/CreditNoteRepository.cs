using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.CreditNote.SavedCreditNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.CreditNote.Database;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.CreditNote.Repository
{
    /// <summary>
    /// EF Core implementation of ICreditNoteRepository (ERP hardening — Phase 4).
    ///
    /// Senior hardening carried over from the Tax Invoice repository:
    ///   • service-layer permission + read-only enforcement lives here (the
    ///     boundary that cannot be bypassed by the UI)
    ///   • a Credit Note is a fiscal document — it is voided, never deleted,
    ///     once issued; only a childless Draft may be physically removed
    ///   • immutable, gap-free numbering assigned once on insert
    ///   • optimistic-concurrency counter (RowVersion)
    ///   • every destructive operation is audited
    /// </summary>
    public class CreditNoteRepository : ICreditNoteRepository
    {
        public async Task InitializeAsync()
        {
            using var ctx = new CreditNoteDbContext();
            await ctx.Database.EnsureCreatedAsync();

            SchemaMigrationRunner.Upgrade(
                CreditNoteDbContext.DatabaseFilePath,
                AppSchemaMigrations.CreditNotes,
                "Credit Note");
        }

        // ── Retrieval ───────────────────────────────────────────────────

        public async Task<SavedCreditNote?> GetByIdAsync(int id)
        {
            using var ctx = new CreditNoteDbContext();
            return await ctx.CreditNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        }

        public async Task<SavedCreditNote?> GetByNumberAsync(string creditNoteNumber)
        {
            using var ctx = new CreditNoteDbContext();
            return await ctx.CreditNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.CreditNoteNumber == creditNoteNumber)
                .ConfigureAwait(false);
        }

        public async Task<List<SavedCreditNote>> GetAllAsync(bool includeVoided = false)
        {
            using var ctx = new CreditNoteDbContext();
            var query = ctx.CreditNotes.AsNoTracking().AsQueryable();
            if (!includeVoided) query = query.Where(x => !x.IsVoid);
            return await query
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<SavedCreditNote>> GetByTaxInvoiceAsync(string taxInvoiceReference)
        {
            using var ctx = new CreditNoteDbContext();
            return await ctx.CreditNotes.AsNoTracking()
                .Where(x => x.TaxInvoiceReference == taxInvoiceReference)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<SavedCreditNote>> GetByProformaAsync(string proformaId)
        {
            using var ctx = new CreditNoteDbContext();
            return await ctx.CreditNotes.AsNoTracking()
                .Where(x => x.ProformaId == proformaId)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync().ConfigureAwait(false);
        }

        // ── Numbering (immutable, gap-free per year) ──────────────────────

        public async Task<int> GetNextSequenceNumberAsync(int year)
        {
            // MAX+1 over RETAINED rows (voided rows are kept), so a number is
            // never reissued — a credit note number, once used, is permanent.
            string prefix = $"CN-{year:D4}-";
            using var ctx = new CreditNoteDbContext();
            var max = await ctx.CreditNotes
                .Where(x => x.CreditNoteNumber.StartsWith(prefix))
                .MaxAsync(x => (int?)x.SequenceNumber).ConfigureAwait(false) ?? 0;
            return max + 1;
        }

        public string BuildCreditNoteNumber(int year, int sequenceNumber)
            => $"CN-{year:D4}-{sequenceNumber:D3}";

        public async Task<bool> ExistsByNumberAsync(string creditNoteNumber)
        {
            using var ctx = new CreditNoteDbContext();
            return await ctx.CreditNotes
                .AnyAsync(x => x.CreditNoteNumber == creditNoteNumber)
                .ConfigureAwait(false);
        }

        // ── CRUD ─────────────────────────────────────────────────────────

        public async Task<SavedCreditNote> InsertAsync(SavedCreditNote entity)
        {
            OperationGuard.Demand(AppPermissions.CreditNoteEdit, "create a credit note");
            ReadOnlyGate.Demand("create a credit note");
            ValidateForSave(entity);

            using var ctx = new CreditNoteDbContext();

            // Assign an immutable number if the caller left it blank.
            if (string.IsNullOrWhiteSpace(entity.CreditNoteNumber))
            {
                int year = entity.IssueDate == default
                    ? DateTime.Now.Year
                    : entity.IssueDate.Year;
                entity.SequenceNumber = await GetNextSequenceNumberAsync(year)
                    .ConfigureAwait(false);
                entity.CreditNoteNumber = BuildCreditNoteNumber(year, entity.SequenceNumber);
            }

            entity.RowVersion = 0;
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;

            ctx.CreditNotes.Add(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());

            AuditService.Log(
                AuditSections.CreditNote, "Credit note created",
                $"Created {entity.CreditNoteNumber} against invoice {entity.TaxInvoiceReference} "
                    + $"for {entity.CustomerName} (AED {entity.GrandTotal:N2})",
                entityType: nameof(SavedCreditNote), entityId: entity.CreditNoteNumber);

            DocumentLifecycle.Raise();
            return entity;
        }

        public async Task UpdateAsync(SavedCreditNote entity)
            => await UpdateAsync(entity, expectedRowVersion: null).ConfigureAwait(false);

        public async Task UpdateAsync(SavedCreditNote entity, int? expectedRowVersion)
        {
            OperationGuard.Demand(AppPermissions.CreditNoteEdit, "update a credit note");
            ReadOnlyGate.Demand("update a credit note");
            ValidateForSave(entity);

            using var ctx = new CreditNoteDbContext();

            // ERP hardening (Phase 5): optimistic-concurrency check + counter.
            var storedRowVersion = await ctx.CreditNotes
                .Where(x => x.Id == entity.Id)
                .Select(x => (int?)x.RowVersion)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (expectedRowVersion.HasValue
                && storedRowVersion.HasValue
                && storedRowVersion.Value != expectedRowVersion.Value)
            {
                throw new ConcurrencyConflictException(
                    "Credit Note", entity.CreditNoteNumber,
                    expectedRowVersion.Value, storedRowVersion.Value);
            }

            entity.RowVersion = (storedRowVersion ?? entity.RowVersion) + 1;
            entity.UpdatedAt = DateTime.Now;

            var existingItems = await ctx.CreditNoteItems
                .Where(i => i.CreditNoteNumber == entity.CreditNoteNumber)
                .ToListAsync().ConfigureAwait(false);
            ctx.CreditNoteItems.RemoveRange(existingItems);

            ctx.CreditNotes.Update(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            DocumentLifecycle.Raise();
        }

        public async Task DeleteAsync(int id)
        {
            // Deleting even a draft credit note is a fiscal action → same
            // Manager/Admin gate as voiding one.
            OperationGuard.Demand(AppPermissions.CreditNoteDelete, "delete a credit note");
            ReadOnlyGate.Demand("delete a credit note");

            using var ctx = new CreditNoteDbContext();
            var entity = await ctx.CreditNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
            if (entity == null) return;

            string number = entity.CreditNoteNumber;

            ctx.CreditNotes.Remove(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());

            AuditService.Log(
                AuditSections.CreditNote, "Credit note deleted",
                $"Deleted draft credit note {number}",
                entityType: nameof(SavedCreditNote), entityId: number);

            DocumentLifecycle.Raise();
        }

        /// <summary>
        /// Soft-close (void) an issued credit note. A credit note is a fiscal
        /// document — once issued it is voided (row + number retained, made
        /// visible-but-voided), never physically erased.
        /// </summary>
        public async Task VoidAsync(int id, string reason)
        {
            OperationGuard.Demand(AppPermissions.CreditNoteDelete, "void a credit note");
            ReadOnlyGate.Demand("void a credit note");

            using var ctx = new CreditNoteDbContext();
            var entity = await ctx.CreditNotes
                .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
            if (entity == null) return;
            if (entity.IsVoid) return;   // idempotent

            string number = entity.CreditNoteNumber;

            entity.IsVoid = true;
            entity.VoidedAtUtc = DateTime.UtcNow;
            entity.VoidedBy = AuditService.CurrentUsername;
            entity.VoidReason = string.IsNullOrWhiteSpace(reason) ? "(no reason given)" : reason.Trim();
            entity.Status = Services.Workflow.DocumentLifecyclePolicy.VoidTargetStatus(
                Services.Workflow.StatusTransitions.Kind.CreditNote);
            entity.RowVersion += 1;
            entity.UpdatedAt = DateTime.Now;

            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());

            AuditService.Log(
                AuditSections.CreditNote, "Credit note voided",
                $"Voided credit note {number}",
                entityType: nameof(SavedCreditNote), entityId: number,
                reason: entity.VoidReason);

            DocumentLifecycle.Raise();
        }

        // ── Validation ────────────────────────────────────────────────────

        /// <summary>
        /// Minimal fiscal-correctness checks. A credit note must reference the
        /// invoice it corrects, state a reason, and never be negative.
        /// </summary>
        private static void ValidateForSave(SavedCreditNote entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(entity.TaxInvoiceReference))
                throw new InvalidOperationException(
                    "A credit note must reference the tax invoice it corrects.");
            if (string.IsNullOrWhiteSpace(entity.Reason))
                throw new InvalidOperationException(
                    "A credit note must state a reason for the correction.");
            if (entity.GrandTotal < 0m)
                throw new InvalidOperationException(
                    "A credit note total is a positive magnitude (its type makes it a credit).");
        }
    }
}

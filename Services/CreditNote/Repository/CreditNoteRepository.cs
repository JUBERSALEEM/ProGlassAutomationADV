using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.CreditNote.SavedCreditNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.CreditNote.Database;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Workflow;
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
    ///     once issued. Physical deletion is only policy-eligible for an
    ///     unlinked Draft, but valid Credit Notes require a source TI reference,
    ///     so repository deletion retains/blocks every valid linked record.
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
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.IsVoid
                || DocumentLifecyclePolicy.HasVoidMetadata(
                    entity.VoidedAtUtc, entity.VoidedBy, entity.VoidReason)
                || !DocumentLifecyclePolicy.CanEditBody(
                    StatusTransitions.Kind.CreditNote, entity.Status))
                throw new InvalidOperationException(
                    "Only a non-void Credit Note in Draft status can be created or edited.");
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

            // The factory intentionally leaves the child FK blank until the
            // repository assigns the immutable document number. Stamp and
            // canonicalize every normalized line before EF persists the graph.
            foreach (var item in entity.Items ?? new List<CreditNoteItem>())
            {
                item.Id = 0;
                item.CreditNoteNumber = entity.CreditNoteNumber;
                item.TaxInvoiceReference = entity.TaxInvoiceReference;
                item.ProformaId = entity.ProformaId;
                item.CreditNote = null;
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

        /// <summary>Updates an existing credit note using the caller's loaded RowVersion.</summary>
        public async Task UpdateAsync(
            SavedCreditNote entity,
            int expectedRowVersion)
        {
            OperationGuard.Demand(AppPermissions.CreditNoteEdit, "update a credit note");
            ReadOnlyGate.Demand("update a credit note");
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            using var ctx = new CreditNoteDbContext();

            var stored = await ctx.CreditNotes
                .FirstOrDefaultAsync(x => x.Id == entity.Id).ConfigureAwait(false);
            if (stored == null)
                throw new ConcurrencyConflictException(
                    "Credit Note", entity.CreditNoteNumber,
                    expectedRowVersion, actualRowVersion: -1);
            if (stored.IsVoid || !DocumentLifecyclePolicy.CanEditBody(
                    StatusTransitions.Kind.CreditNote, stored.Status))
                throw new InvalidOperationException(
                    $"Credit Note {stored.CreditNoteNumber} is issued, voided, or terminal; its body cannot be edited.");
            if (!string.Equals(stored.Status, entity.Status,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Credit Note status changes are not allowed through body update.");
            if (entity.IsVoid != stored.IsVoid
                || entity.VoidedAtUtc != stored.VoidedAtUtc
                || !string.Equals(entity.VoidedBy, stored.VoidedBy, StringComparison.Ordinal)
                || !string.Equals(entity.VoidReason, stored.VoidReason, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Credit Note void metadata can only be changed by VoidAsync.");
            if (!string.Equals(stored.CreditNoteNumber, entity.CreditNoteNumber,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.TaxInvoiceReference, entity.TaxInvoiceReference,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.ProformaId, entity.ProformaId,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.JobOrderId, entity.JobOrderId,
                    StringComparison.OrdinalIgnoreCase)
                || stored.SequenceNumber != entity.SequenceNumber)
                throw new InvalidOperationException(
                    "The Credit Note number, source invoice/family/job links, and sequence are immutable.");
            ValidateForSave(entity);

            // Check the loaded version before replacing the body; EF's
            // concurrency-token predicate protects the write after this read.
            int storedRowVersion = stored.RowVersion;
            if (storedRowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Credit Note", stored.CreditNoteNumber,
                    expectedRowVersion, storedRowVersion);

            entity.Id = stored.Id;
            entity.CreditNoteNumber = stored.CreditNoteNumber;
            entity.TaxInvoiceReference = stored.TaxInvoiceReference;
            entity.ProformaId = stored.ProformaId;
            entity.JobOrderId = stored.JobOrderId;
            entity.SequenceNumber = stored.SequenceNumber;
            entity.Status = stored.Status;
            entity.IsVoid = stored.IsVoid;
            entity.VoidedAtUtc = stored.VoidedAtUtc;
            entity.VoidedBy = stored.VoidedBy;
            entity.VoidReason = stored.VoidReason;
            entity.CreatedAt = stored.CreatedAt;
            entity.RowVersion = storedRowVersion + 1;
            entity.UpdatedAt = DateTime.Now;

            var existingItems = await ctx.CreditNoteItems
                .Where(i => i.CreditNoteNumber == stored.CreditNoteNumber)
                .ToListAsync().ConfigureAwait(false);
            ctx.CreditNoteItems.RemoveRange(existingItems);

            var incomingItems = entity.Items ?? new List<CreditNoteItem>();
            foreach (var item in incomingItems)
            {
                item.Id = 0;
                item.CreditNoteNumber = stored.CreditNoteNumber;
                item.TaxInvoiceReference = stored.TaxInvoiceReference;
                item.ProformaId = stored.ProformaId;
                item.CreditNote = null;
            }

            // Copy scalar body fields onto the tracked row, then replace only
            // normalized draft lines. Detached IsVoid/void-audit values are
            // never attached to EF's update graph.
            ctx.Entry(stored).CurrentValues.SetValues(entity);
            ctx.CreditNoteItems.AddRange(incomingItems);
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(ctx, stored.Id)
                    .ConfigureAwait(false);
                throw new ConcurrencyConflictException(
                    "Credit Note", stored.CreditNoteNumber,
                    expectedRowVersion, actual);
            }
            entity.RowVersion = stored.RowVersion;
            entity.UpdatedAt = stored.UpdatedAt;
            DocumentLifecycle.Raise();
        }

        private static async Task<int> GetCurrentRowVersionAsync(
            CreditNoteDbContext context, int id)
        {
            return await context.CreditNotes.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => (int?)x.RowVersion)
                .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
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
            if (entity.IsVoid)
                throw new InvalidOperationException(
                    $"Credit Note {entity.CreditNoteNumber} is voided and must be retained.");

            bool linkedToTaxInvoice = !string.IsNullOrWhiteSpace(entity.TaxInvoiceReference);
            var decision = DocumentLifecyclePolicy.DecideDelete(
                StatusTransitions.Kind.CreditNote, entity.Status, linkedToTaxInvoice);
            if (decision == DocumentLifecyclePolicy.DeleteDecision.Blocked)
            {
                if (!DocumentLifecyclePolicy.CanVoid(
                        StatusTransitions.Kind.CreditNote, entity.Status))
                    throw new InvalidOperationException(
                        $"Credit Note {entity.CreditNoteNumber} is linked to Tax Invoice {entity.TaxInvoiceReference} and cannot be voided from status '{entity.Status}'; retain it unchanged.");
                throw new InvalidOperationException(
                    $"Credit Note {entity.CreditNoteNumber} is linked to Tax Invoice {entity.TaxInvoiceReference} and cannot be hard-deleted. Use the explicit void operation to retain the correction trail.");
            }
            if (decision == DocumentLifecyclePolicy.DeleteDecision.Retained)
                throw new InvalidOperationException(
                    $"Credit Note {entity.CreditNoteNumber} cannot be safely deleted or voided from status '{entity.Status}' and must be retained.");
            if (decision == DocumentLifecyclePolicy.DeleteDecision.Void)
            {
                await VoidAsync(id, "Delete requested; lifecycle policy requires retention.")
                    .ConfigureAwait(false);
                return;
            }

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
            if (!DocumentLifecyclePolicy.CanVoid(
                    StatusTransitions.Kind.CreditNote, entity.Status))
                throw new InvalidOperationException(
                    $"Credit Note {entity.CreditNoteNumber} cannot be voided from status '{entity.Status}'; it must be retained.");

            string number = entity.CreditNoteNumber;

            entity.IsVoid = true;
            entity.VoidedAtUtc = DateTime.UtcNow;
            entity.VoidedBy = AuditService.CurrentUsername;
            entity.VoidReason = DocumentLifecyclePolicy.NormalizeVoidReason(reason);
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

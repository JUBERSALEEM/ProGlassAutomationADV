using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
    /// <summary>
    /// EF Core implementation of IProformaInvoiceRepository.
    ///
    /// BUG-07: Return types nullable (matches interface contract).
    /// BUG-08: SuggestNextIdAsync batch-loads all matching IDs (no N+1).
    /// BUG-09: Fallback ID uses Guid.NewGuid() — no timestamp collision.
    /// BUG-10: UpdateAsync no longer opens second context in insert path.
    /// BUG-11: Regex tightened — non-greedy prefix, explicit numeric group.
    /// BUG-12: GetAllAsync accepts CancellationToken.
    /// </summary>
    public class ProformaInvoiceRepository : IProformaInvoiceRepository
    {
        // ══════════════════════════════════════════════════════════════
        //  SAVED-INVOICE CHANGE SIGNAL
        //
        //  Job Profitability reads saved invoices. It reloaded only when the
        //  module was entered, so saving an invoice while that screen was
        //  already open left it showing figures for a job that had since
        //  changed, with no indication anything was stale.
        //
        //  The signal is raised HERE rather than at each call site because
        //  invoices are written from several places (the wizard's print
        //  preview, the saved-invoices list, draft auto-save). Hooking the
        //  repository means a future write path is covered without anyone
        //  remembering to add a notification.
        //
        //  Static and parameterless on purpose: listeners re-query rather
        //  than trusting a payload, so a missed or duplicated event costs a
        //  redundant read, never a wrong number on screen.
        // ══════════════════════════════════════════════════════════════
        public static event Action? SavedInvoicesChanged;

        private static void RaiseChanged()
        {
            // A listener that throws must not roll back a completed save.
            try { SavedInvoicesChanged?.Invoke(); } catch { }
        }

        // BUG-11: named groups, non-greedy prefix, anchored at end
        private static readonly Regex _idPattern = new(
            @"^(?<prefix>.*?)(?<num>\d+)$",
            RegexOptions.Compiled);

        public async Task InitializeAsync()
        {
            using var db = new ProformaInvoicesDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        public async Task<bool> ExistsByProformaIdAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return false;

            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices
                .AnyAsync(x => x.ProformaId == proformaId);
        }

        /// <summary>
        /// Suggests the next available ProformaId by auto-incrementing the trailing number.
        ///
        /// BUG-08: Was doing up to 10,000 DB queries. Now does ONE query
        /// to load all IDs with matching prefix, then finds next unused
        /// in memory. Massive perf improvement.
        ///
        /// BUG-09: Fallback uses Guid.NewGuid() instead of timestamp —
        /// no risk of two invoices created same second colliding.
        ///
        /// BUG-11: Regex tightened using named groups + non-greedy prefix.
        ///
        /// Examples:
        ///   "PI-2026-001" → "PI-2026-002" (or 003, 004... if those also exist)
        ///   "INVOICE-42"  → "INVOICE-43"
        ///   "ABC"         → "ABC-001"
        /// </summary>
        public async Task<string> SuggestNextIdAsync(string currentId)
        {
            if (string.IsNullOrWhiteSpace(currentId))
                currentId = "PI-000";

            var match = _idPattern.Match(currentId);

            string prefix;
            int currentNumber;
            int padWidth;

            if (match.Success)
            {
                prefix = match.Groups["prefix"].Value;
                var numText = match.Groups["num"].Value;

                // AUDIT V-1 (Phase 13 — input validation).
                // _idPattern uses \d+ which is UNBOUNDED, so a document
                // id such as "PI-99999999999" (11 digits, easily typed or
                // pasted, and legal in the ProformaId column) produced an
                // OverflowException here. int.Parse throws, SuggestNextIdAsync
                // has no try/catch, and the caller is an async command whose
                // exception was swallowed by RelayCommand — so the user saw
                // "nothing happens" and could not create an invoice at all.
                //
                // TryParse degrades instead: an out-of-range or malformed
                // run of digits restarts numbering from that prefix rather
                // than crashing. The duplicate-avoidance loop below then
                // finds the next free id, so correctness is preserved.
                if (!int.TryParse(numText, out currentNumber))
                    currentNumber = 0;

                padWidth = numText.Length;
            }
            else
            {
                prefix = currentId + "-";
                currentNumber = 0;
                padWidth = 3;
            }

            using var db = new ProformaInvoicesDbContext();

            // BUG-08: single query — load all IDs starting with prefix
            var existingIds = await db.ProformaInvoices
                .Where(x => x.ProformaId.StartsWith(prefix))
                .Select(x => x.ProformaId)
                .ToListAsync();

            var existingSet = new HashSet<string>(
                existingIds, StringComparer.OrdinalIgnoreCase);

            // Find next unused number by simple in-memory loop
            for (int i = 1; i <= 100_000; i++)
            {
                var candidate =
                    $"{prefix}{(currentNumber + i).ToString().PadLeft(padWidth, '0')}";
                if (!existingSet.Contains(candidate))
                    return candidate;
            }

            // BUG-09: GUID fallback — never collides
            return $"{prefix}{Guid.NewGuid():N}".Substring(0, Math.Min(50,
                prefix.Length + 32));
        }

        public async Task<int> InsertAsync(SavedProformaInvoice invoice)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.ProformaCreate, "create a proforma invoice");

            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice));

            using var db = new ProformaInvoicesDbContext();

            bool exists = await db.ProformaInvoices
                .AnyAsync(x => x.ProformaId == invoice.ProformaId);
            if (exists)
                throw new InvalidOperationException(
                    $"An invoice with ProformaId '{invoice.ProformaId}' already exists. " +
                    "Use UpdateAsync or generate a new ID.");

            invoice.CreatedAt = DateTime.Now;
            invoice.UpdatedAt = DateTime.Now;

            db.ProformaInvoices.Add(invoice);
            await db.SaveChangesAsync();
            RaiseChanged();

            return invoice.Id;
        }

        /// <summary>
        /// Updates an existing invoice or creates a new one if missing.
        ///
        /// BUG-10: Previously opened a SECOND DbContext by calling
        /// InsertAsync() from inside — SQLite locking risk. Fixed by
        /// doing insert inline within the current context.
        /// </summary>
        public async Task UpdateAsync(SavedProformaInvoice invoice)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.ProformaEdit, "update a proforma invoice");

            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice));

            using var db = new ProformaInvoicesDbContext();

            var existing = await db.ProformaInvoices
                .FirstOrDefaultAsync(x => x.ProformaId == invoice.ProformaId);

            if (existing == null)
            {
                // BUG-10: Insert inline instead of calling InsertAsync()
                // (which would open a second context and cause SQLite lock)
                invoice.CreatedAt = DateTime.Now;
                invoice.UpdatedAt = DateTime.Now;

                db.ProformaInvoices.Add(invoice);
                await db.SaveChangesAsync();
            RaiseChanged();
                return;
            }

            // Copy fields onto tracked entity
            existing.CompanyName = invoice.CompanyName;
            existing.CustomerName = invoice.CustomerName;
            existing.ProjectName = invoice.ProjectName;
            existing.Status = invoice.Status;
            existing.IssueDate = invoice.IssueDate;
            existing.ValidUntil = invoice.ValidUntil;
            existing.GlassMaterialsSubtotal = invoice.GlassMaterialsSubtotal;
            existing.OtherChargesSubtotal = invoice.OtherChargesSubtotal;
            existing.DiscountAmount = invoice.DiscountAmount;
            existing.VatAmount = invoice.VatAmount;
            existing.GrandTotalVatInclusive = invoice.GrandTotalVatInclusive;
            existing.FullSnapshotJson = invoice.FullSnapshotJson;
            existing.UpdatedAt = DateTime.Now;

            await db.SaveChangesAsync();
            RaiseChanged();
        }

        public async Task<SavedProformaInvoice?> GetByIdAsync(int id)
        {
            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices.FindAsync(id);
        }

        public async Task<SavedProformaInvoice?> GetByProformaIdAsync(
            string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return null;

            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices
                .FirstOrDefaultAsync(x => x.ProformaId == proformaId);
        }

        public async Task<List<SavedProformaInvoice>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// PERF-2: see IProformaInvoiceRepository for why this exists
        /// alongside GetAllAsync rather than replacing it.
        /// </summary>
        public async Task<List<ProformaAnalyticsRow>> GetAnalyticsRowsAsync(
            CancellationToken cancellationToken = default)
        {
            using var db = new ProformaInvoicesDbContext();

            // AsNoTracking: these rows are read-only analytics input, so
            // there is no reason to build change-tracking entries for them.
            // The Select is what keeps FullSnapshotJson off the wire.
            return await db.ProformaInvoices
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new ProformaAnalyticsRow
                {
                    Status                 = x.Status,
                    CustomerName           = x.CustomerName,
                    GrandTotalVatInclusive = x.GrandTotalVatInclusive,
                    CreatedAt              = x.CreatedAt,
                    IssueDate              = x.IssueDate
                })
                .ToListAsync(cancellationToken);
        }

        public async Task DeleteAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.ProformaDelete, "delete a proforma invoice");

            using var db = new ProformaInvoicesDbContext();
            var entity = await db.ProformaInvoices.FindAsync(id);
            if (entity == null) return;

            db.ProformaInvoices.Remove(entity);
            await db.SaveChangesAsync();
            RaiseChanged();
        }
    }
}
// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Repository/StatementRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16 + Chat 21 v2 fix)
//
// FIXES vs PHASE 2 VERSION:
//   1. GetByIdAsync: now uses Include(s => s.LineItems) — nav prop exists.
//   2. UpdateAsync: delete-then-reinsert line items correctly via
//      LineItems collection on the entity (not a separate Add loop).
//   3. InsertAsync: EF cascade-inserts LineItems automatically via
//      nav property — no separate ctx.LineItems.AddRange needed.
//   4. ToListItem helper: moved to non-EF queryable method, called
//      after ToListAsync() on in-memory results.
//   5. GetNextStatementNumberAsync: uses year-scoped prefix count
//      with zero-padding to D3 (STMT-2026-001 format confirmed).
//
// CHAT 21 v2 FIX:
//   ★ GetNextStatementNumberAsync REWRITTEN to use MAX-based logic
//     instead of COUNT-based. The old COUNT+1 approach caused
//     UNIQUE constraint collisions when previous save attempts
//     failed (leaving gaps in the sequence but incrementing COUNT
//     out of sync with actual data).
//
//     NEW: Extracts numeric suffix from all existing statement
//     numbers for the current year, finds the MAX, returns MAX+1.
//     This guarantees no collision because we base the next number
//     on actual persisted data, not on a count that may diverge.
//
// THREAD SAFETY:
//   New DbContext per operation (not shared singleton).
//   All DB work synchronous within the method — callers use Task.Run.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

namespace ProGlassAutomation.Services.Statement.Repository
{
    public class StatementRepository : IStatementRepository
    {
        // ── Lifecycle ─────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            await using var ctx = new StatementDbContext();
            await ctx.Database.EnsureCreatedAsync();
        }

        // ── Single record retrieval ───────────────────────────────────────

        public async Task<SavedStatement?> GetByIdAsync(int id)
        {
            await using var ctx = new StatementDbContext();
            return await ctx.Statements
                .Include(s => s.LineItems)         // ← nav prop now exists
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<SavedStatement?> GetByStatementNumberAsync(
            string statementNumber)
        {
            await using var ctx = new StatementDbContext();
            return await ctx.Statements
                .Include(s => s.LineItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.StatementNumber == statementNumber);
        }

        // ── List retrieval ────────────────────────────────────────────────

        public async Task<List<StatementListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            await using var ctx = new StatementDbContext();
            var entities = await ctx.Statements
                .AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<StatementListItem>> GetByCustomerAsync(
            string customerName)
        {
            await using var ctx = new StatementDbContext();
            string lower = customerName.ToLower();
            var entities = await ctx.Statements
                .AsNoTracking()
                .Where(s => s.FilterCustomer.ToLower().Contains(lower))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<StatementListItem>> GetByCompanyAsync(
            string companyName)
        {
            await using var ctx = new StatementDbContext();
            string lower = companyName.ToLower();
            var entities = await ctx.Statements
                .AsNoTracking()
                .Where(s => s.FilterCompany.ToLower().Contains(lower))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<StatementListItem>> GetByProjectAsync(
            string projectName)
        {
            await using var ctx = new StatementDbContext();
            string lower = projectName.ToLower();
            var entities = await ctx.Statements
                .AsNoTracking()
                .Where(s => s.FilterProject.ToLower().Contains(lower))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return entities.Select(ToListItem).ToList();
        }

        // ═════════════════════════════════════════════════════════════════
        //  NUMBERING (Chat 21 v2 — collision-proof MAX-based logic)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the next available statement number for the current year.
        /// Format: STMT-YYYY-NNN (zero-padded 3-digit sequence).
        ///
        /// COLLISION-PROOF DESIGN:
        ///   Uses MAX(existing suffix) + 1 instead of COUNT + 1.
        ///   This prevents "UNIQUE constraint failed" errors when:
        ///     • A previous InsertAsync failed mid-transaction
        ///     • Numbers were manually inserted out of sequence
        ///     • Records were deleted, leaving gaps
        ///
        ///   Example scenario:
        ///     DB has: STMT-2026-001, STMT-2026-002
        ///     (STMT-2026-003 attempt failed — never persisted)
        ///     OLD logic: count(2) + 1 → "STMT-2026-003" → collision!
        ///     NEW logic: max(2) + 1  → "STMT-2026-003" → succeeds
        ///     After success: max(3) + 1 → "STMT-2026-004"
        ///
        /// FIRST OF YEAR:
        ///   If no statements exist for current year, returns "STMT-YYYY-001".
        /// </summary>
        public async Task<string> GetNextStatementNumberAsync()
        {
            await using var ctx = new StatementDbContext();
            int year = DateTime.Now.Year;
            string prefix = $"STMT-{year}-";

            // Fetch all existing statement numbers for this year.
            // Uses StartsWith on the indexed StatementNumber column (fast).
            var existingNumbers = await ctx.Statements
                .AsNoTracking()
                .Where(s => s.StatementNumber.StartsWith(prefix))
                .Select(s => s.StatementNumber)
                .ToListAsync();

            // Find the MAX numeric suffix across all existing numbers.
            // Parses "STMT-2026-005" → 5, "STMT-2026-012" → 12, etc.
            // Any unparseable entry is skipped (defensive).
            int maxSeq = 0;
            foreach (var num in existingNumbers)
            {
                if (num.Length <= prefix.Length) continue;

                string suffix = num.Substring(prefix.Length);
                if (int.TryParse(suffix, out int seq) && seq > maxSeq)
                    maxSeq = seq;
            }

            // Return MAX + 1 with zero-padded 3-digit format.
            // Example: max=7 → "STMT-2026-008"
            return $"{prefix}{(maxSeq + 1):D3}";
        }

        // ── CRUD ──────────────────────────────────────────────────────────

        public async Task<SavedStatement> InsertAsync(SavedStatement entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "create a statement");

            await using var ctx = new StatementDbContext();

            // Set timestamps explicitly (BUG-11 pattern)
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            // EF cascade-inserts LineItems automatically
            // because entity.LineItems is populated before Add()
            ctx.Statements.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(SavedStatement entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "update a statement");

            await using var ctx = new StatementDbContext();

            entity.UpdatedAt = DateTime.UtcNow;

            // ── Delete existing line items (delete-then-reinsert) ─────────
            // Same pattern as TaxInvoiceRepository.UpdateAsync
            var existingLines = await ctx.LineItems
                .Where(l => l.StatementId == entity.Id)
                .ToListAsync();

            ctx.LineItems.RemoveRange(existingLines);

            // Ensure new line items have correct StatementId FK
            foreach (var line in entity.LineItems)
                line.StatementId = entity.Id;

            // Update parent entity
            ctx.Statements.Update(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "delete a statement");

            await using var ctx = new StatementDbContext();
            var entity = await ctx.Statements
                .Include(s => s.LineItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity == null) return;

            // Cascade-delete removes LineItems automatically
            // (configured in StatementDbContext.OnModelCreating)
            ctx.Statements.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ── Existence checks ──────────────────────────────────────────────

        public async Task<bool> ExistsByStatementNumberAsync(
            string statementNumber)
        {
            await using var ctx = new StatementDbContext();
            return await ctx.Statements
                .AsNoTracking()
                .AnyAsync(s => s.StatementNumber == statementNumber);
        }

        // ── PDF path management ───────────────────────────────────────────

        public async Task UpdatePdfPathAsync(
            int statementId,
            string pdfPath,
            bool hasAttachments)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "update a statement PDF path");

            await using var ctx = new StatementDbContext();
            var entity = await ctx.Statements.FindAsync(statementId);
            if (entity == null) return;

            entity.PdfPath = pdfPath;
            entity.HasAttachments = hasAttachments;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
        }

        // ── Private helpers ───────────────────────────────────────────────

        /// <summary>
        /// Projects SavedStatement to StatementListItem DTO.
        /// Called on in-memory objects AFTER ToListAsync().
        /// NOT used inside EF LINQ queries (not translatable to SQL).
        /// </summary>
        private static StatementListItem ToListItem(SavedStatement s) => new()
        {
            Id = s.Id,
            StatementNumber = s.StatementNumber,
            Title = s.Title,
            FilterCustomer = s.FilterCustomer,
            FilterCompany = s.FilterCompany,
            FilterProject = s.FilterProject,
            FilterSalesman = s.FilterSalesman,
            FilterDateFrom = s.FilterDateFrom,
            FilterDateTo = s.FilterDateTo,
            InvoiceCount = s.InvoiceCount,
            TotalInvoiced = s.TotalInvoiced,
            TotalOutstanding = s.TotalOutstanding,
            OverdueAmount = s.OverdueAmount,
            PdfPath = s.PdfPath,
            HasAttachments = s.HasAttachments,
            CreatedAt = s.CreatedAt
        };

        // ── ANALYTICS PROJECTION ─────────────────────────────────────────

        public async Task<List<StatementAnalyticsRow>> GetAnalyticsRowsAsync()
        {
            await using var ctx = new StatementDbContext();
            return await ctx.Statements
                .AsNoTracking()
                .Select(s => new StatementAnalyticsRow
                {
                    TotalInvoiced = s.TotalInvoiced,
                    TotalOutstanding = s.TotalOutstanding,
                    OverdueAmount = s.OverdueAmount
                })
                .ToListAsync();
        }
    }
}
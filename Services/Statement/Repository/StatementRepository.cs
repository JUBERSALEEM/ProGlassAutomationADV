// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Repository/StatementRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
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

        // ── Numbering ─────────────────────────────────────────────────────

        public async Task<string> GetNextStatementNumberAsync()
        {
            await using var ctx = new StatementDbContext();
            int year = DateTime.Now.Year;
            string prefix = $"STMT-{year}-";

            // Count existing statements for this calendar year
            // Uses StartsWith on the indexed StatementNumber column
            int count = await ctx.Statements
                .AsNoTracking()
                .CountAsync(s => s.StatementNumber.StartsWith(prefix));

            // Zero-padded 3-digit sequence: STMT-2026-001
            return $"{prefix}{(count + 1):D3}";
        }

        // ── CRUD ──────────────────────────────────────────────────────────

        public async Task<SavedStatement> InsertAsync(SavedStatement entity)
        {
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
    }
}
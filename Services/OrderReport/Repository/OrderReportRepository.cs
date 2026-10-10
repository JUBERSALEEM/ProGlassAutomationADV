// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/OrderReport/Repository/OrderReportRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — repository (2026-09-06)
//
// Mirrors Services/Statement/Repository/StatementRepository.cs: fresh
// DbContext per operation, MAX-based numbering, explicit timestamps,
// OperationGuard at the service boundary (StatementGenerate permission,
// same as the Statement Generator).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.OrderReport.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.OrderReport.Repository
{
    public class OrderReportRepository : IOrderReportRepository
    {
        public async Task InitializeAsync()
        {
            await using var ctx = new OrderReportDbContext();
            await ctx.Database.EnsureCreatedAsync();
            bool upgraded = SchemaMigrationRunner.Upgrade(
                OrderReportDbContext.DatabaseFilePath,
                AppSchemaMigrations.OrderReports,
                "Order Report");
            if (!upgraded)
                throw new InvalidOperationException(
                    "The Order Report database could not be upgraded safely. " +
                    "Review the migration log and the pre-migration backup before retrying.");
        }

        public async Task<SavedOrderReport?> GetByIdAsync(int id)
        {
            await using var ctx = new OrderReportDbContext();
            return await ctx.OrderReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
        }

        public async Task<List<OrderReportListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            await using var ctx = new OrderReportDbContext();
            return await ctx.OrderReports
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new OrderReportListItem
                {
                    Id = r.Id,
                    RowVersion = r.RowVersion,
                    ReportNumber = r.ReportNumber,
                    Title = r.Title,
                    FilterCompany = r.FilterCompany,
                    FilterCustomer = r.FilterCustomer,
                    FilterProject = r.FilterProject,
                    FilterSalesman = r.FilterSalesman,
                    FilterDateFrom = r.FilterDateFrom,
                    FilterDateTo = r.FilterDateTo,
                    PiCount = r.PiCount,
                    TotalOrderedQty = r.TotalOrderedQty,
                    TotalDeliveredQty = r.TotalDeliveredQty,
                    TotalInvoicedQty = r.TotalInvoicedQty,
                    MissingDocuments = r.MissingDocuments,
                    TotalInvoicedValue = r.TotalInvoicedValue,
                    PdfPath = r.PdfPath,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GetNextReportNumberAsync()
        {
            await using var ctx = new OrderReportDbContext();
            int year = DateTime.Now.Year;
            string prefix = $"REP-{year}-";

            var existing = await ctx.OrderReports
                .AsNoTracking()
                .Where(r => r.ReportNumber.StartsWith(prefix))
                .Select(r => r.ReportNumber)
                .ToListAsync().ConfigureAwait(false);

            int maxSeq = 0;
            foreach (var num in existing)
            {
                if (num.Length <= prefix.Length) continue;
                if (int.TryParse(num.Substring(prefix.Length), out int seq) && seq > maxSeq)
                    maxSeq = seq;
            }

            return $"{prefix}{(maxSeq + 1):D3}";
        }

        public async Task<SavedOrderReport> InsertAsync(SavedOrderReport entity)
        {
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "create an order report");

            await using var ctx = new OrderReportDbContext();

            entity.RowVersion = 0;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            ctx.OrderReports.Add(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            return entity;
        }

        public async Task UpdateAsync(
            SavedOrderReport entity, int expectedRowVersion)
        {
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "update an order report");
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            await using var ctx = new OrderReportDbContext();
            var stored = await ctx.OrderReports
                .FirstOrDefaultAsync(r => r.Id == entity.Id).ConfigureAwait(false);
            if (stored == null)
                throw new ConcurrencyConflictException(
                    "Order Report", entity.ReportNumber,
                    expectedRowVersion, actualRowVersion: -1);
            if (stored.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Order Report", stored.ReportNumber,
                    expectedRowVersion, stored.RowVersion);

            entity.Id = stored.Id;
            entity.RowVersion = expectedRowVersion + 1;
            entity.UpdatedAt = DateTime.UtcNow;
            ctx.Entry(stored).CurrentValues.SetValues(entity);
            stored.RowVersion = expectedRowVersion + 1;
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync())
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(ctx, stored.Id)
                    .ConfigureAwait(false);
                throw new ConcurrencyConflictException(
                    "Order Report", stored.ReportNumber,
                    expectedRowVersion, actual);
            }

            entity.RowVersion = stored.RowVersion;
            entity.UpdatedAt = stored.UpdatedAt;
        }

        public async Task DeleteAsync(int id, int expectedRowVersion)
        {
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "delete an order report");

            await using var ctx = new OrderReportDbContext();
            var entity = await ctx.OrderReports
                .FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
            if (entity == null)
                throw new ConcurrencyConflictException(
                    "Order Report", id.ToString(),
                    expectedRowVersion, actualRowVersion: -1);
            if (entity.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Order Report", entity.ReportNumber,
                    expectedRowVersion, entity.RowVersion);

            ctx.OrderReports.Remove(entity);
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync())
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(ctx, id)
                    .ConfigureAwait(false);
                throw new ConcurrencyConflictException(
                    "Order Report", entity.ReportNumber,
                    expectedRowVersion, actual);
            }
        }

        public async Task<int> UpdatePdfPathAsync(
            int reportId, string pdfPath, int expectedRowVersion)
        {
            await using var ctx = new OrderReportDbContext();
            var entity = await ctx.OrderReports
                .FirstOrDefaultAsync(r => r.Id == reportId).ConfigureAwait(false);
            if (entity == null)
                throw new ConcurrencyConflictException(
                    "Order Report", reportId.ToString(),
                    expectedRowVersion, actualRowVersion: -1);
            if (entity.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Order Report", entity.ReportNumber,
                    expectedRowVersion, entity.RowVersion);

            entity.PdfPath = pdfPath;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.RowVersion++;
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync())
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(ctx, reportId)
                    .ConfigureAwait(false);
                throw new ConcurrencyConflictException(
                    "Order Report", entity.ReportNumber,
                    expectedRowVersion, actual);
            }

            return entity.RowVersion;
        }

        private static async Task<int> GetCurrentRowVersionAsync(
            OrderReportDbContext context, int id)
        {
            return await context.OrderReports.AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => (int?)r.RowVersion)
                .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
        }
    }
}

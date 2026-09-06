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

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            ctx.OrderReports.Add(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            return entity;
        }

        public async Task UpdateAsync(SavedOrderReport entity)
        {
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "update an order report");

            await using var ctx = new OrderReportDbContext();
            entity.UpdatedAt = DateTime.UtcNow;
            ctx.OrderReports.Update(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
        }

        public async Task DeleteAsync(int id)
        {
            OperationGuard.Demand(
                AppPermissions.StatementGenerate, "delete an order report");

            await using var ctx = new OrderReportDbContext();
            var entity = await ctx.OrderReports
                .FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
            if (entity == null) return;

            ctx.OrderReports.Remove(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
        }

        public async Task UpdatePdfPathAsync(int reportId, string pdfPath)
        {
            await using var ctx = new OrderReportDbContext();
            var entity = await ctx.OrderReports.FindAsync(reportId).ConfigureAwait(false);
            if (entity == null) return;

            entity.PdfPath = pdfPath;
            entity.UpdatedAt = DateTime.UtcNow;
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
        }
    }
}

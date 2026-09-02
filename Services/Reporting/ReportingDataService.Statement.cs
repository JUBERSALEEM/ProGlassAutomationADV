// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.Statement.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the Statement
// report family members; the class declaration + shared helpers live in
// ReportingDataService.cs.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Database;
using ProGlassAutomation.Services.TaxInvoice.Database;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public sealed partial class ReportingDataService : IReportingDataService
    {
        // ════════════════════════════════════════════════════════════════
        //  STATEMENT REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<StatementReportSummary> GetStatementSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();
            var query = db.Statements.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(s => EF.Functions.Like(s.FilterCustomer, $"%{filter.Customer}%") || EF.Functions.Like(s.StatementNumber, $"%{filter.Customer}%"));
            if (filter.DateFrom.HasValue)
                query = query.Where(s => s.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(s => s.CreatedAt <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.StatementNumber, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCustomer, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCompany, $"%{term}%"));
            }

            var summary = new StatementReportSummary();
            summary.TotalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (summary.TotalCount == 0) return summary;

            var stAmounts = await query.Select(s => new { s.TotalInvoiced, s.TotalOutstanding, s.OverdueAmount }).ToListAsync(ct).ConfigureAwait(false);
            summary.TotalInvoiced = stAmounts.Sum(x => x.TotalInvoiced);
            summary.TotalOutstanding = stAmounts.Sum(x => x.TotalOutstanding);
            summary.TotalOverdue = stAmounts.Sum(x => x.OverdueAmount);

            return summary;
        }

        public async Task<PagedResult<StatementReportRow>> GetStatementRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();
            var query = db.Statements.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(s => EF.Functions.Like(s.FilterCustomer, $"%{filter.Customer}%") || EF.Functions.Like(s.StatementNumber, $"%{filter.Customer}%"));
            if (filter.DateFrom.HasValue)
                query = query.Where(s => s.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(s => s.CreatedAt <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.StatementNumber, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCustomer, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCompany, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (totalCount == 0)
                return new PagedResult<StatementReportRow> { Items = new List<StatementReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var page = await query.OrderByDescending(s => s.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(s => new StatementReportRow
                {
                    Id = s.Id,
                    StatementNumber = s.StatementNumber,
                    CustomerName = s.FilterCustomer,
                    CompanyName = s.FilterCompany,
                    ProjectName = s.FilterProject,
                    InvoiceCount = s.LineItems != null ? s.LineItems.Count : 0,
                    TotalInvoiced = s.TotalInvoiced,
                    TotalOutstanding = s.TotalOutstanding,
                    OverdueAmount = s.OverdueAmount,
                    CreatedAt = s.CreatedAt,
                    FilterJson = s.FilterJson
                })
                .ToListAsync(ct).ConfigureAwait(false);

            return new PagedResult<StatementReportRow> { Items = page, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  STATEMENT FINANCIAL LEDGER (Phase 8)
        //  One row per statement line item: a real Tax Invoice inside a
        //  saved statement, rendered as Date | Doc # | Type | Description |
        //  Debit | Credit | Balance. All figures from stored data.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the SQL-side statement-line query: StatementLineItems
        /// joined to their parent Statement for company/customer context.
        /// Filters: company, customer, date range (by invoice issue date),
        /// free-text search across doc numbers / PI / description.
        /// </summary>
        private static IQueryable<StatementLineProjection> BuildStatementLineQuery(
            StatementDbContext db, ReportingFilter filter)
        {
            IQueryable<StatementLineItem> items = db.LineItems
                .AsNoTracking()
                .Where(i => i.Statement != null);

            if (!string.IsNullOrWhiteSpace(filter.Company))
                items = items.Where(i => EF.Functions.Like(i.Statement!.FilterCompany, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                items = items.Where(i => EF.Functions.Like(i.Statement!.FilterCustomer, $"%{filter.Customer}%"));
            if (filter.DateFrom.HasValue)
                items = items.Where(i => i.IssueDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                items = items.Where(i => i.IssueDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                items = items.Where(i =>
                    EF.Functions.Like(i.InvoiceNumber, $"%{term}%") ||
                    EF.Functions.Like(i.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(i.DeliveryNoteRef, $"%{term}%") ||
                    EF.Functions.Like(i.Statement!.StatementNumber, $"%{term}%"));
            }

            return items.Select(i => new StatementLineProjection
            {
                Id = i.Id,
                StatementNumber = i.Statement!.StatementNumber,
                CustomerName = i.Statement!.FilterCustomer,
                CompanyName = i.Statement!.FilterCompany,
                IssueDate = i.IssueDate,
                InvoiceNumber = i.InvoiceNumber,
                ProformaId = i.ProformaId,
                DocumentType = i.InvoiceType,
                Description = i.DeliveryNoteRef,
                Status = i.Status,
                Debit = i.GrandTotal,
                Credit = i.PaidAmount
            });
        }

        /// <summary>SQL projection shape for statement financial rows.</summary>
        private sealed class StatementLineProjection
        {
            public int Id { get; set; }
            public string StatementNumber { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public DateTime IssueDate { get; set; }
            public string InvoiceNumber { get; set; } = "";
            public string ProformaId { get; set; } = "";
            public string DocumentType { get; set; } = "";
            public string Description { get; set; } = "";
            public string Status { get; set; } = "";
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        public async Task<StatementLineSummary> GetStatementLineSummaryAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var summary = new StatementLineSummary();

            using var db = new StatementDbContext();
            var query = BuildStatementLineQuery(db, filter);

            summary.TotalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (summary.TotalCount == 0) return summary;

            // SQLite cannot aggregate decimal server-side (no native decimal
            // type), so materialize the Debit/Credit values and sum in memory.
            var amounts = await query.Select(x => new { x.Debit, x.Credit }).ToListAsync(ct).ConfigureAwait(false);
            summary.TotalDebit = amounts.Sum(x => x.Debit);
            summary.TotalCredit = amounts.Sum(x => x.Credit);
            summary.ClosingBalance = summary.TotalDebit - summary.TotalCredit;

            // Opening balance = outstanding (Debit − Credit) of documents
            // dated BEFORE the period start, same company/customer filters.
            // With no date-from the tracked window starts at the first
            // document, so opening is 0 (honest — we never fabricate it).
            if (filter.DateFrom.HasValue)
            {
                var openingFilter = new ReportingFilter
                {
                    Company = filter.Company,
                    Customer = filter.Customer,
                    DateFrom = null,
                    DateTo = filter.DateFrom.Value.AddDays(-1)
                };
                var openingQuery = BuildStatementLineQuery(db, openingFilter);
                var openingAmounts = await openingQuery
                    .Select(x => new { x.Debit, x.Credit })
                    .ToListAsync(ct).ConfigureAwait(false);
                summary.OpeningBalance = Math.Max(0,
                    openingAmounts.Sum(x => x.Debit - x.Credit));
            }

            return summary;
        }

        public async Task<PagedResult<StatementLineRow>> GetStatementLineRowsAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();

            var query = BuildStatementLineQuery(db, filter);
            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (totalCount == 0)
                return new PagedResult<StatementLineRow> { Items = new List<StatementLineRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var page = await query
                .OrderBy(x => x.IssueDate)
                .ThenBy(x => x.InvoiceNumber)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .ToListAsync(ct).ConfigureAwait(false);

            var rows = page.Select(x => new StatementLineRow
            {
                Id = x.Id,
                StatementNumber = x.StatementNumber,
                CustomerName = x.CustomerName,
                CompanyName = x.CompanyName,
                IssueDate = x.IssueDate,
                InvoiceNumber = x.InvoiceNumber,
                ProformaId = x.ProformaId,
                DocumentType = string.IsNullOrWhiteSpace(x.DocumentType) ? "Tax Invoice" : x.DocumentType,
                Description = x.Description,
                Status = x.Status,
                Debit = x.Debit,
                Credit = x.Credit,
                Balance = x.Debit - x.Credit
            }).ToList();

            return new PagedResult<StatementLineRow>
            {
                Items = rows,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
    }
}

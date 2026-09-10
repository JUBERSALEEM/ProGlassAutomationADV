// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.TaxInvoice.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the TaxInvoice
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
        //  TAX INVOICE REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<TaxInvoiceReportSummary> GetTaxInvoiceSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new TaxInvoiceDbContext();
            var query = Live(db.TaxInvoices.AsNoTracking()).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(t => t.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.TaxInvoiceNumber))
                query = query.Where(t => t.InvoiceNumber == filter.TaxInvoiceNumber);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(t => EF.Functions.Like(t.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(t => EF.Functions.Like(t.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var st = filter.Status.Trim();
                if (Eq(st, "Partially Paid") || Eq(st, "PartiallyPaid"))
                    query = query.Where(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid");
                else
                    query = query.Where(t => t.Status == st);
            }
            if (filter.DateFrom.HasValue)
                query = query.Where(t => t.IssueDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(t => t.IssueDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(t =>
                    EF.Functions.Like(t.InvoiceNumber, $"%{term}%") ||
                    EF.Functions.Like(t.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(t.CustomerName, $"%{term}%"));
            }

            var summary = new TaxInvoiceReportSummary();
            summary.TotalInvoices = await query.CountAsync(ct).ConfigureAwait(false);
            if (summary.TotalInvoices == 0) return summary;

            var tiAmounts = await query.Select(t => new { t.GrandTotal, t.VatAmount, t.Status, t.DueDate }).ToListAsync(ct).ConfigureAwait(false);
            summary.TotalAmount = tiAmounts.Sum(x => x.GrandTotal);
            summary.TotalVat = tiAmounts.Sum(x => x.VatAmount);

            summary.Issued = tiAmounts.Count(t => t.Status == "Issued");
            summary.Paid = tiAmounts.Count(t => t.Status == "Paid");
            summary.PartiallyPaid = tiAmounts.Count(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid");
            summary.Overdue = tiAmounts.Count(t => t.Status == "Overdue" || (t.Status == "Issued" && t.DueDate < DateTime.Today));
            summary.Draft = tiAmounts.Count(t => t.Status == "Draft");
            summary.Outstanding = tiAmounts.Where(t => t.Status == "Issued" || t.Status == "Overdue" || t.Status == "PartiallyPaid" || t.Status == "Partially Paid").Sum(x => x.GrandTotal);

            return summary;
        }

        public async Task<PagedResult<TaxInvoiceReportRow>> GetTaxInvoiceRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new TaxInvoiceDbContext();
            var query = Live(db.TaxInvoices.AsNoTracking()).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(t => t.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.TaxInvoiceNumber))
                query = query.Where(t => t.InvoiceNumber == filter.TaxInvoiceNumber);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(t => EF.Functions.Like(t.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(t => EF.Functions.Like(t.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var st = filter.Status.Trim();
                if (Eq(st, "Partially Paid") || Eq(st, "PartiallyPaid"))
                    query = query.Where(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid");
                else
                    query = query.Where(t => t.Status == st);
            }
            if (filter.DateFrom.HasValue)
                query = query.Where(t => t.IssueDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(t => t.IssueDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(t =>
                    EF.Functions.Like(t.InvoiceNumber, $"%{term}%") ||
                    EF.Functions.Like(t.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(t.CustomerName, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (totalCount == 0)
                return new PagedResult<TaxInvoiceReportRow> { Items = new List<TaxInvoiceReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            bool tiDesc = filter.SortDescending;
            IOrderedQueryable<SavedTaxInvoice> tiOrdered = (filter.SortField) switch
            {
                "Amount"   => tiDesc ? query.OrderByDescending(t => t.GrandTotal) : query.OrderBy(t => t.GrandTotal),
                "Customer" => tiDesc ? query.OrderByDescending(t => t.CustomerName) : query.OrderBy(t => t.CustomerName),
                "Company"  => tiDesc ? query.OrderByDescending(t => t.CompanyName) : query.OrderBy(t => t.CompanyName),
                "Status"   => tiDesc ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "Invoice"  => tiDesc ? query.OrderByDescending(t => t.InvoiceNumber) : query.OrderBy(t => t.InvoiceNumber),
                "DueDate"  => tiDesc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
                _          => tiDesc ? query.OrderByDescending(t => t.IssueDate) : query.OrderBy(t => t.IssueDate),
            };

            var page = await tiOrdered
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(t => new TaxInvoiceReportRow
                {
                    Id = t.Id,
                    InvoiceNumber = t.InvoiceNumber,
                    ProformaId = t.ProformaId,
                    JobOrderId = t.JobOrderId,
                    DeliveryNoteReference = t.DeliveryNoteReference,
                    CustomerName = t.CustomerName,
                    ProjectName = t.ProjectName,
                    CompanyName = t.CompanyName,
                    Status = t.Status,
                    InvoiceType = t.InvoiceType,
                    IssueDate = t.IssueDate,
                    DueDate = t.DueDate,
                    GrandTotal = t.GrandTotal,
                    VatAmount = t.VatAmount,
                    TotalQty = t.TotalQty,
                    TotalSqm = t.TotalSqm,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);

            // Enrich the visible page (≤100 rows) with Salesman + Prepared By
            // from the linked PI snapshot JSON — never the full dataset.
            await EnrichTiPageAsync(page, ct);

            return new PagedResult<TaxInvoiceReportRow> { Items = page, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        /// <summary>
        /// Page-only enrichment of TI rows with Salesman and Prepared By from
        /// the source PI snapshot. Reads at most one PI snapshot per distinct
        /// ProformaId on the visible page.
        /// </summary>
        private static async Task EnrichTiPageAsync(
            List<TaxInvoiceReportRow> rows, CancellationToken ct)
        {
            var piIds = rows
                .Select(r => r.ProformaId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            if (piIds.Count == 0) return;

            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                var snapshots = await Live(piDb.ProformaInvoices.AsNoTracking())
                    .Where(p => piIds.Contains(p.ProformaId))
                    .Select(p => new { p.ProformaId, p.FullSnapshotJson })
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var s in snapshots)
                {
                    var snap = TryDeserializePiSnapshot(s.FullSnapshotJson);
                    if (snap == null) continue;

                    string salesman = snap.Client?.SalesRepresentative ?? "";
                    string preparedBy = snap.PreparedBy?.PreparedByName ?? "";

                    foreach (var r in rows)
                    {
                        if (!string.Equals(r.ProformaId, s.ProformaId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (string.IsNullOrWhiteSpace(r.Salesman))
                            r.Salesman = salesman;
                        if (string.IsNullOrWhiteSpace(r.PreparedBy))
                            r.PreparedBy = preparedBy;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"TI report enrichment failed: {ex.Message}");
            }
        }

        public async Task<List<TaxInvoiceReportRow>> GetPendingInvoicesAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var result = new List<TaxInvoiceReportRow>();
            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                using var tiDb = new TaxInvoiceDbContext();

                var confirmedPis = await Live(piDb.ProformaInvoices.AsNoTracking())
                    .Where(p => p.Status == "Approved" || p.Status == "Confirmed" || p.Status == "Issued" || p.Status == "Sent")
                    .Select(p => new { p.ProformaId, p.CustomerName, p.ProjectName, p.CompanyName, p.GrandTotalVatInclusive, p.IssueDate })
                    .ToListAsync(ct).ConfigureAwait(false);

                var invoicedPis = (await Live(tiDb.TaxInvoices.AsNoTracking()).Select(t => t.ProformaId).ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var pi in confirmedPis.Where(p => !invoicedPis.Contains(p.ProformaId)))
                {
                    if (filter.HasCustomer && !ContainsCI(pi.CustomerName, filter.Customer)) continue;
                    if (filter.HasCompany && !ContainsCI(pi.CompanyName, filter.Company)) continue;
                    if (filter.HasProject && !ContainsCI(pi.ProjectName, filter.Project)) continue;
                    result.Add(new TaxInvoiceReportRow
                    {
                        ProformaId = pi.ProformaId,
                        CustomerName = pi.CustomerName,
                        ProjectName = pi.ProjectName,
                        CompanyName = pi.CompanyName,
                        Status = "Pending Invoice",
                        GrandTotal = pi.GrandTotalVatInclusive,
                        IssueDate = pi.IssueDate
                    });
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Pending invoices query warning: {ex.Message}"); }
            return result;
        }
    }
}

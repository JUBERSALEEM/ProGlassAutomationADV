// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.Vat.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the Vat
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
        public async Task<VatSubmissionSummary> GetVatSubmissionSummaryAsync(
            ReportingFilter filter, 
            CancellationToken ct = default)
        {
            var rows = await GetAllVatSubmissionRowsAsync(filter, ct);
            
            return new VatSubmissionSummary
            {
                TotalInvoices = rows.Count,
                TotalSubtotal = rows.Sum(r => r.SubtotalBeforeVat),
                TotalVatCollected = rows.Sum(r => r.VatAmount),
                TotalGrandTotal = rows.Sum(r => r.GrandTotalVatInclusive),
                
                BySalesman = rows
                    .GroupBy(r => r.Salesman)
                    .Select(g => new SalesmanVatSummary
                    {
                        Salesman = g.Key,
                        InvoiceCount = g.Count(),
                        TotalVat = g.Sum(r => r.VatAmount),
                        TotalRevenue = g.Sum(r => r.GrandTotalVatInclusive)
                    })
                    .OrderByDescending(s => s.TotalVat)
                    .ToList(),
                    
                ByCompany = rows
                    .GroupBy(r => r.CompanyName)
                    .Select(g => new CompanyVatSummary
                    {
                        CompanyName = g.Key,
                        InvoiceCount = g.Count(),
                        TotalVat = g.Sum(r => r.VatAmount),
                        TotalRevenue = g.Sum(r => r.GrandTotalVatInclusive)
                    })
                    .OrderByDescending(c => c.TotalVat)
                    .ToList(),
                    
                ByProject = rows
                    .GroupBy(r => new { r.ProjectName, r.CompanyName })
                    .Select(g => new ProjectVatSummary
                    {
                        ProjectName = g.Key.ProjectName,
                        CompanyName = g.Key.CompanyName,
                        InvoiceCount = g.Count(),
                        TotalVat = g.Sum(r => r.VatAmount),
                        TotalRevenue = g.Sum(r => r.GrandTotalVatInclusive)
                    })
                    .OrderByDescending(p => p.TotalVat)
                    .ToList()
            };
        }

        public async Task<PagedResult<VatSubmissionRow>> GetVatSubmissionRowsAsync(
            ReportingFilter filter, 
            CancellationToken ct = default)
        {
            var allRows = await GetAllVatSubmissionRowsAsync(filter, ct);
            
            return new PagedResult<VatSubmissionRow>
            {
                Items = allRows
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList(),
                TotalCount = allRows.Count,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<List<VatSubmissionRow>> GetAllVatSubmissionRowsAsync(
            ReportingFilter filter, 
            CancellationToken ct = default)
        {
            try
            {
                // SECURITY: Validate date range (max 5 years to prevent DoS)
                if (filter.DateFrom.HasValue && filter.DateTo.HasValue)
                {
                    var dateRange = filter.DateTo.Value - filter.DateFrom.Value;
                    if (dateRange.TotalDays > 1825) // 5 years
                    {
                        throw new ArgumentException(
                            "Date range cannot exceed 5 years. Please use more restrictive filters to prevent performance issues.");
                    }
                }

                using var db = new ProformaInvoicesDbContext();
                var query = db.ProformaInvoices.AsNoTracking().AsQueryable();
                
                // VAT compliance — exclude non-finalized invoices
                // Draft = never issued, Rejected/Cancelled/Void = voided
                // Include: Approved, Confirmed, Issued, Sent, Released (all have VAT charged)
                var vatExcludedStatuses = new[] { "Draft", "Rejected", "Cancelled", "Void" };
                query = query.Where(i => i.Status != null && !vatExcludedStatuses.Contains(i.Status));
                
                // Apply filters
                if (filter.DateFrom.HasValue)
                    query = query.Where(i => i.IssueDate >= filter.DateFrom.Value);
                if (filter.DateTo.HasValue)
                    query = query.Where(i => i.IssueDate <= filter.DateTo.Value);
                if (!string.IsNullOrWhiteSpace(filter.Company))
                    query = query.Where(i => Eq(i.CompanyName, filter.Company));
                if (!string.IsNullOrWhiteSpace(filter.Customer))
                    query = query.Where(i => Eq(i.CustomerName, filter.Customer));
                if (!string.IsNullOrWhiteSpace(filter.Project))
                    query = query.Where(i => Eq(i.ProjectName, filter.Project));
                if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                    query = query.Where(i => ContainsCI(i.ProformaId, filter.ProformaId));
                
                ct.ThrowIfCancellationRequested();
                
                // SECURITY: DoS protection - limit maximum records
                const int MAX_RECORDS = 10000;
                var invoiceList = await query.Take(MAX_RECORDS + 1).ToListAsync(ct);
                
                if (invoiceList.Count > MAX_RECORDS)
                {
                    Services.Logging.AppLogService.Warning(
                        $"VAT Submission: Dataset truncated at {MAX_RECORDS} records. " +
                        "Consider using more restrictive filters.");
                    invoiceList = invoiceList.Take(MAX_RECORDS).ToList();
                }
                
                var result = new List<VatSubmissionRow>();
                var validationWarnings = new List<string>();
                var invalidCount = 0;
                
                // SEQUENTIAL PROCESSING: Avoids thread pool starvation and UI freezing
                foreach (var invoice in invoiceList)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    // CRITICAL: Data Validation using VatCalculator
                    var validation = VatCalculator.ValidateInvoice(
                        invoice.GrandTotalVatInclusive, 
                        invoice.VatAmount, 
                        invoice.IssueDate);
                    
                    if (!validation.IsValid)
                    {
                        invalidCount++;
                        continue; // Skip invalid data
                    }
                    
                    if (validation.HasWarnings)
                    {
                        validationWarnings.AddRange(
                            validation.Warnings.Select(w => $"PI {invoice.ProformaId}: {w}"));
                    }
                    
                    // Extract SalesRepresentative from snapshot (synchronous - fast enough)
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(invoice);
                    var salesman = snap?.Client?.SalesRepresentative ?? "Unassigned";
                    
                    // Apply salesman filter if specified
                    if (!string.IsNullOrWhiteSpace(filter.Salesman) && !Eq(salesman, filter.Salesman))
                        continue;
                    
                    // CRITICAL: Use VatCalculator for accurate calculations
                    var subtotal = VatCalculator.CalculateSubtotal(
                        invoice.GrandTotalVatInclusive, 
                        invoice.VatAmount);
                    
                    result.Add(new VatSubmissionRow
                    {
                        Id = invoice.Id,
                        ProformaId = invoice.ProformaId ?? "",
                        CompanyName = invoice.CompanyName ?? "",
                        CustomerName = invoice.CustomerName ?? "",
                        ProjectName = invoice.ProjectName ?? "",
                        Salesman = salesman,
                        IssueDate = invoice.IssueDate,
                        Status = invoice.Status ?? "",
                        SubtotalBeforeVat = subtotal,
                        VatAmount = VatCalculator.Round(invoice.VatAmount),
                        GrandTotalVatInclusive = VatCalculator.Round(invoice.GrandTotalVatInclusive)
                    });
                }
                
                // Log validation summary
                if (invalidCount > 0)
                {
                    Services.Logging.AppLogService.Warning(
                        $"VAT Submission: {invalidCount} invoices skipped due to validation errors");
                }
                
                if (validationWarnings.Any())
                {
                    Services.Logging.AppLogService.Warning(
                        $"VAT Submission: {validationWarnings.Count} validation warnings:\n" +
                        string.Join("\n", validationWarnings.Take(10)));
                }
                
                // AUDIT: Log access
                Services.Logging.AppLogService.Info(
                    $"VAT Submission: Retrieved {result.Count} invoices " +
                    $"(filtered from {invoiceList.Count}, {invalidCount} invalid)");
                
                return result
                    .OrderByDescending(r => r.IssueDate)
                    .ThenBy(r => r.ProformaId)
                    .ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw; // Re-throw validation errors
            }
            catch (Exception ex)
            {
                // GRACEFUL DEGRADATION: Return empty results instead of crashing
                // This handles cases where the database doesn't exist yet,
                // the table is missing, or the connection fails.
                Services.Logging.AppLogService.Warning(
                    $"VAT Submission: Data load failed gracefully — {ex.Message}. " +
                    "Returning empty results. The database may not be initialized yet.");
                return new List<VatSubmissionRow>();
            }
        }
    }
}

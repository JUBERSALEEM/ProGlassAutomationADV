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
                // ERR-4F211D ROOT CAUSE.
                //
                // This ran on EVERY load, before any user filter, which is why
                // the warning appeared repeatedly at startup with no
                // interaction. It was the ONLY string[].Contains() in a query
                // anywhere in the codebase.
                //
                // EF must hoist `vatExcludedStatuses` into a query parameter.
                // On .NET 8 the expression interpreter binds the array's
                // Contains to a ReadOnlySpan<string> overload, producing:
                //
                //   An exception was thrown while attempting to evaluate a
                //   LINQ query parameter expression ... INNER: ArgumentException:
                //   GenericArguments[1], 'System.ReadOnlySpan`1[System.String]',
                //   ... violates the constraint of type 'TRet'
                //
                // The broad catch below swallowed it and returned an empty
                // list, so the VAT report showed ZERO ROWS every time.
                //
                // Fix: express the exclusion as explicit, individually
                // translatable comparisons. No collection is captured, so
                // there is no parameter for the interpreter to mis-bind.
                // Semantics are identical.
                query = query.Where(i =>
                    i.Status != null
                    && i.Status != "Draft"
                    && i.Status != "Rejected"
                    && i.Status != "Cancelled"
                    && i.Status != "Void");
                
                // Apply filters
                if (filter.DateFrom.HasValue)
                    query = query.Where(i => i.IssueDate >= filter.DateFrom.Value);
                if (filter.DateTo.HasValue)
                    query = query.Where(i => i.IssueDate <= filter.DateTo.Value);
                // EF Core cannot translate the local helpers Eq()/ContainsCI()
                // -- they are plain C# methods using StringComparison, which
                // has no SQL equivalent. Passing them to Where() made EF throw
                // "An exception was thrown while attempting to evaluate a LINQ
                // query parameter expression", which the catch below swallowed,
                // so the VAT report silently returned ZERO rows on every load.
                //
                // Use translatable constructs instead: EF.Functions.Like is
                // case-insensitive for ASCII in SQLite, and Trim()/Contains()
                // both map to SQL. Semantics are preserved.
                // ERR-4F211D FOLLOW-UP — the LIKE PATTERN MUST BE A PLAIN LOCAL.
                //
                // The previous version built the pattern inside the lambda
                // ("%" + pid + "%") and called .Trim() on the COLUMN. Both are
                // parameter expressions EF must evaluate on the client, and on
                // .NET 8 the expression interpreter binds string.Concat to a
                // ReadOnlySpan<char> overload, which blows up with:
                //
                //   ArgumentException: GenericArguments[1],
                //   'System.ReadOnlySpan`1[System.String]' ... violates the
                //   constraint of type 'TRet'
                //
                // The broad catch below turned that into "load failed
                // gracefully" and the VAT report showed ZERO ROWS on every
                // load, repeatedly.
                //
                // Fix: precompute the complete pattern into a local BEFORE the
                // query, and never call a method on the column. This is the
                // same shape JobOrderRepository/ProformaInvoiceRepository
                // already use successfully.
                if (!string.IsNullOrWhiteSpace(filter.Company))
                {
                    var companyPattern = filter.Company.Trim();
                    query = query.Where(i => i.CompanyName != null
                        && EF.Functions.Like(i.CompanyName, companyPattern));
                }
                if (!string.IsNullOrWhiteSpace(filter.Customer))
                {
                    var customerPattern = filter.Customer.Trim();
                    query = query.Where(i => i.CustomerName != null
                        && EF.Functions.Like(i.CustomerName, customerPattern));
                }
                if (!string.IsNullOrWhiteSpace(filter.Project))
                {
                    var projectPattern = filter.Project.Trim();
                    query = query.Where(i => i.ProjectName != null
                        && EF.Functions.Like(i.ProjectName, projectPattern));
                }
                if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                {
                    var pidPattern = "%" + filter.ProformaId.Trim() + "%";
                    query = query.Where(i => i.ProformaId != null
                        && EF.Functions.Like(i.ProformaId, pidPattern));
                }
                
                ct.ThrowIfCancellationRequested();
                
                // SECURITY: DoS protection - limit maximum records
                const int MAX_RECORDS = 10000;
                var invoiceList = await query.Take(MAX_RECORDS + 1).ToListAsync(ct).ConfigureAwait(false);
                
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
                // Log the INNER exception too. EF wraps the real cause (e.g.
                // an untranslatable LINQ expression) inside a generic outer
                // message, and logging only ex.Message hid the actual bug
                // here for a long time -- the report just showed no data.
                Services.Logging.AppLogService.Warning(
                    $"VAT Submission: Data load failed gracefully — {ex.Message}. " +
                    (ex.InnerException != null
                        ? $"INNER: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}. "
                        : string.Empty) +
                    "Returning empty results. The database may not be initialized yet.");
                return new List<VatSubmissionRow>();
            }
        }
    }
}

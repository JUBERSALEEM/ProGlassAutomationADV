// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.GlassSpecs.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the GlassSpecs
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
        //  GLASS / SPEC REPORT — Row-Level Intelligence
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<GlassSpecReportRow>> GetGlassSpecRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var allRows = new List<GlassSpecReportRow>();

            using var piDb = new ProformaInvoicesDbContext();
            var query = Live(piDb.ProformaInvoices.AsNoTracking()).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(p => p.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(p => EF.Functions.Like(p.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(p => EF.Functions.Like(p.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(p => p.Status == filter.Status);
            if (filter.DateFrom.HasValue)
                query = query.Where(p => p.IssueDate >= filter.DateFrom.Value || (p.IssueDate == default && p.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(p => p.IssueDate <= DayEnd(filter.DateTo) || (p.IssueDate == default && p.CreatedAt <= DayEnd(filter.DateTo)));

            // Load PI records (limit snapshot stream if unbounded)
            var piList = await query.OrderByDescending(p => p.CreatedAt)
                .Take(string.IsNullOrWhiteSpace(filter.ProformaId) ? 1000 : 1)
                .Select(p => new
                {
                    p.ProformaId, p.CompanyName, p.CustomerName, p.ProjectName, p.Status, p.IssueDate, p.CreatedAt, p.FullSnapshotJson
                }).ToListAsync(ct).ConfigureAwait(false);

            foreach (var pi in piList)
            {
                var snap = TryDeserializePiSnapshot(pi.FullSnapshotJson);
                if (snap == null) continue;
                var salesman = snap.Client?.SalesRepresentative ?? "";
                var rows = ExtractGlassRowsFromPiSnapshot(snap, pi.ProformaId, pi.CompanyName, pi.CustomerName, salesman, pi.IssueDate == default ? pi.CreatedAt : pi.IssueDate, pi.Status);

                foreach (var r in rows)
                {
                    if (!string.IsNullOrWhiteSpace(filter.GlassType) && !Eq(r.GlassType, filter.GlassType!)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.GlassRef) && !ContainsCI(r.GlassRef, filter.GlassRef)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Specification) && !ContainsCI(r.SpecName, filter.Specification)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Colour) && !(ContainsCI(r.OuterColor, filter.Colour) || ContainsCI(r.InnerColor, filter.Colour))) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Thickness) && !ContainsCI(r.SpecName, filter.Thickness)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Project) && !ContainsCI(r.ProjectName, filter.Project)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Salesman) && !ContainsCI(r.Salesman, filter.Salesman)) continue;
                    if (filter.HasSearch)
                    {
                        var hit = ContainsCI(r.GlassRef, filter.SearchText) ||
                                  ContainsCI(r.SpecName, filter.SearchText) ||
                                  ContainsCI(r.ProformaId, filter.SearchText) ||
                                  ContainsCI(r.CustomerName, filter.SearchText);
                        if (!hit) continue;
                    }
                    allRows.Add(r);
                }
            }

            bool gsDesc = filter.SortDescending;
            IOrderedEnumerable<GlassSpecReportRow> gsOrdered = (filter.SortField) switch
            {
                "Amount"   => gsDesc ? allRows.OrderByDescending(r => r.Amount) : allRows.OrderBy(r => r.Amount),
                "Customer" => gsDesc ? allRows.OrderByDescending(r => r.CustomerName) : allRows.OrderBy(r => r.CustomerName),
                "Company"  => gsDesc ? allRows.OrderByDescending(r => r.CompanyName) : allRows.OrderBy(r => r.CompanyName),
                "Qty"      => gsDesc ? allRows.OrderByDescending(r => r.Qty) : allRows.OrderBy(r => r.Qty),
                "Area"     => gsDesc ? allRows.OrderByDescending(r => r.TotalSqm) : allRows.OrderBy(r => r.TotalSqm),
                "Proforma" => gsDesc ? allRows.OrderByDescending(r => r.ProformaId) : allRows.OrderBy(r => r.ProformaId),
                _          => gsDesc ? allRows.OrderByDescending(r => r.IssueDate) : allRows.OrderBy(r => r.IssueDate),
            };
            var sorted = gsOrdered.ThenBy(r => r.ProformaId).ThenBy(r => r.GroupNumber).ThenBy(r => r.SerialNumber).ToList();
            var total = sorted.Count;
            var page = sorted.Skip(filter.Skip).Take(filter.Take).ToList();

            return new PagedResult<GlassSpecReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<List<SpecAggregation>> GetSpecAggregationsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            var page = await GetGlassSpecRowsAsync(new ReportingFilter
            {
                DateFrom = filter.DateFrom, DateTo = filter.DateTo,
                Company = filter.Company, Customer = filter.Customer, Salesman = filter.Salesman,
                Project = filter.Project, GlassType = filter.GlassType, Specification = filter.Specification,
                Colour = filter.Colour, Thickness = filter.Thickness, GlassRef = filter.GlassRef,
                SearchText = filter.SearchText, ProformaId = filter.ProformaId,
                // Status must ride along: the KPI strip aggregates the same
                // scope the grid shows — a status-filtered grid with
                // unfiltered KPIs reads as a wrong total. (2026-09-10)
                Status = filter.Status,
                Page = 1, PageSize = 5000
            }, ct);

            return SpecificationIntelligenceService.Instance.AggregateSpecificationRows(page.Items);
        }

        public async Task<RowTrace?> GetRowTraceAsync(string rowId, CancellationToken ct = default)
        {
            return await RowTraceabilityService.Instance.TraceRowLifecycleAsync(rowId, ct);
        }
    }
}

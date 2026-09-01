// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.MasterData.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the MasterData
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
        //  COMPANY / CUSTOMER / SALESMAN / PROJECT
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<CompanyReportRow>> GetCompanyRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.CompanyName, p.ProformaId, p.Status, p.GrandTotalVatInclusive })
                .ToListAsync(ct);

            var groups = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CompanyName) ? "Unknown" : p.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CompanyReportRow
                {
                    CompanyName = g.Key,
                    PiCount = g.Count(),
                    PiValue = g.Sum(x => x.GrandTotalVatInclusive)
                }).ToList();

            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.CompanyName, filter.SearchText)).ToList();
            if (filter.HasCompany)
                groups = groups.Where(g => ContainsCI(g.CompanyName, filter.Company)).ToList();

            var piByCompany = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CompanyName) ? "Unknown" : p.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ProformaId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joAll = await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct);
                var joSet = joAll.Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (piByCompany.TryGetValue(g.CompanyName, out var pis))
                        g.JobOrderCount = pis.Count(pi => joSet.Contains(pi));
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Company JO count warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiAll = await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal, t.Status }).ToListAsync(ct);
                var tiByPi = tiAll.GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (!piByCompany.TryGetValue(g.CompanyName, out var pis)) continue;
                    int tiCount = 0; decimal invoiced = 0;
                    foreach (var pi in pis)
                    {
                        if (tiByPi.TryGetValue(pi, out var list))
                        {
                            tiCount += list.Count;
                            invoiced += list.Sum(x => x.GrandTotal);
                        }
                    }
                    g.TaxInvoiceCount = tiCount;
                    g.InvoicedValue = invoiced;
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Company TI count warning: {ex.Message}"); }

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<CompanyReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<CustomerReportRow>> GetCustomerRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.CustomerName, p.ProformaId, p.GrandTotalVatInclusive, p.FullSnapshotJson })
                .ToListAsync(ct);

            var groups = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CustomerName) ? "Unknown" : p.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    string salesman = "";
                    try
                    {
                        var first = g.FirstOrDefault();
                        if (first != null)
                        {
                            var snap = TryDeserializePiSnapshot(first.FullSnapshotJson);
                            salesman = snap?.Client?.SalesRepresentative ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        // Phase 2: Never swallow — log with correlation context
                        AppLogService.Warning(
                            $"Could not extract salesman for customer '{g.Key}': {ex.Message}",
                            new LogContext
                            {
                                Module = "Reporting",
                                Class = nameof(ReportingDataService),
                                Method = nameof(GetCustomerRowsAsync),
                                Operation = "EnrichSalesman",
                                Extra = $"Customer={g.Key}"
                            });
                    }
                    return new CustomerReportRow
                    {
                        CustomerName = g.Key,
                        Salesman = salesman,
                        PiCount = g.Count(),
                        PiValue = g.Sum(x => x.GrandTotalVatInclusive)
                    };
                }).ToList();

            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.CustomerName, filter.SearchText) || ContainsCI(g.Salesman, filter.SearchText)).ToList();
            if (filter.HasCustomer)
                groups = groups.Where(g => ContainsCI(g.CustomerName, filter.Customer)).ToList();
            if (filter.HasSalesman)
                groups = groups.Where(g => ContainsCI(g.Salesman, filter.Salesman)).ToList();

            var piByCustomer = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CustomerName) ? "Unknown" : p.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ProformaId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joPis = (await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                    if (piByCustomer.TryGetValue(g.CustomerName, out var pis))
                        g.JoCount = pis.Count(pi => joPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Customer JO count warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiAll = await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal }).ToListAsync(ct);
                var tiByPi = tiAll.GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(x => x.GrandTotal), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (!piByCustomer.TryGetValue(g.CustomerName, out var pis)) continue;
                    int tiCount = 0; decimal invoiced = 0;
                    foreach (var pi in pis)
                        if (tiByPi.TryGetValue(pi, out var sum)) { tiCount++; invoiced += sum; }
                    g.TiCount = tiCount;
                    g.InvoicedValue = invoiced;
                    g.Outstanding = Math.Max(0, g.PiValue - invoiced);
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Customer TI count warning: {ex.Message}"); }

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<CustomerReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<SalesmanReportRow>> GetSalesmanRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.ProformaId, p.Status, p.GrandTotalVatInclusive, p.CustomerName, p.ProjectName, p.FullSnapshotJson })
                .ToListAsync(ct);

            var piWithSalesman = new List<(string ProformaId, string Salesman, string Status, decimal Total, string Customer, string Project)>();
            foreach (var p in piAll)
            {
                var snap = TryDeserializePiSnapshot(p.FullSnapshotJson);
                var sm = (snap?.Client?.SalesRepresentative ?? "").Trim();
                if (string.IsNullOrWhiteSpace(sm)) sm = "Unassigned";
                piWithSalesman.Add((p.ProformaId, sm, p.Status, p.GrandTotalVatInclusive, p.CustomerName, p.ProjectName));
            }

            var groups = piWithSalesman.GroupBy(x => x.Salesman, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var confirmed = g.Where(x => Eq(x.Status, "Approved") || Eq(x.Status, "Confirmed") || Eq(x.Status, "Issued")).ToList();
                    return new SalesmanReportRow
                    {
                        SalesmanName = g.Key,
                        PiCount = g.Count(),
                        ConfirmedPiCount = confirmed.Count,
                        ConfirmedValue = confirmed.Sum(x => x.Total),
                        CustomerCount = g.Select(x => x.Customer).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        ProjectCount = g.Select(x => x.Project).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        Revenue = confirmed.Sum(x => x.Total)
                    };
                }).ToList();

            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.SalesmanName, filter.SearchText)).ToList();
            if (filter.HasSalesman)
                groups = groups.Where(g => ContainsCI(g.SalesmanName, filter.Salesman)).ToList();

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.Revenue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<SalesmanReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<ProjectReportRow>> GetProjectRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.ProformaId, p.ProjectName, p.CustomerName, p.CompanyName, p.GrandTotalVatInclusive, p.FullSnapshotJson })
                .ToListAsync(ct);

            var projectRows = new Dictionary<string, ProjectReportRow>(StringComparer.OrdinalIgnoreCase);
            var piByProject = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in piAll)
            {
                var snap = TryDeserializePiSnapshot(p.FullSnapshotJson);
                var projName = (!string.IsNullOrWhiteSpace(p.ProjectName) ? p.ProjectName : snap?.Project?.ProjectIdentifierName ?? snap?.Project?.ProjectReference ?? "").Trim();
                if (string.IsNullOrWhiteSpace(projName)) projName = "(No Project)";
                var salesman = snap?.Client?.SalesRepresentative ?? "";
                var contractNo = snap?.Project?.ProjectContractNo ?? "";

                if (!projectRows.TryGetValue(projName, out var row))
                {
                    row = new ProjectReportRow
                    {
                        ProjectName = projName,
                        ProjectContractNo = contractNo,
                        ProjectReference = snap?.Project?.ProjectReference ?? "",
                        CustomerName = p.CustomerName,
                        CompanyName = p.CompanyName,
                        Salesman = salesman
                    };
                    projectRows[projName] = row;
                    piByProject[projName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                row.PiCount++;
                row.PiValue += p.GrandTotalVatInclusive;
                piByProject[projName].Add(p.ProformaId);
            }

            var list = projectRows.Values.ToList();

            if (filter.HasSearch)
                list = list.Where(r => ContainsCI(r.ProjectName, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText)).ToList();
            if (filter.HasProject)
                list = list.Where(r => ContainsCI(r.ProjectName, filter.Project)).ToList();
            if (filter.HasCustomer)
                list = list.Where(r => ContainsCI(r.CustomerName, filter.Customer)).ToList();

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiByPi = (await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal }).ToListAsync(ct))
                    .GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.GrandTotal), StringComparer.OrdinalIgnoreCase);
                foreach (var r in list)
                    if (piByProject.TryGetValue(r.ProjectName, out var pis))
                    {
                        int c = 0; decimal sum = 0;
                        foreach (var pi in pis) if (tiByPi.TryGetValue(pi, out var v)) { c++; sum += v; }
                        r.TiCount = c;
                        r.InvoicedValue = sum;
                    }
            }
            catch (Exception ex) { AppLogService.Warning($"Project TI calculation warning: {ex.Message}"); }

            var total = list.Count;
            var page = list.OrderByDescending(r => r.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<ProjectReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }
    }
}

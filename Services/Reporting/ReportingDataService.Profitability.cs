// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.Profitability.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the Profitability
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
        //  PROFITABILITY (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            var rows = await GetProfitabilityRowsAsync(new ReportingFilter
            {
                DateFrom = filter.DateFrom, DateTo = filter.DateTo,
                Company = filter.Company, Customer = filter.Customer, Project = filter.Project,
                Salesman = filter.Salesman, Page = 1, PageSize = 1000
            }, ct);

            var summary = new ProfitabilitySummary
            {
                TotalRevenue = rows.Items.Sum(r => r.Revenue),
                TotalCost = rows.Items.Sum(r => r.Cost),
                TotalProfit = rows.Items.Sum(r => r.Profit),
                InvoicesWithCost = rows.Items.Count(r => r.HasCostData),
                InvoicesWithoutCost = rows.Items.Count(r => !r.HasCostData)
            };
            summary.AvgMargin = summary.TotalRevenue == 0 ? 0 : Math.Round(summary.TotalProfit / summary.TotalRevenue * 100, 1);
            summary.MissingCostSources = rows.Items.Where(r => !r.HasCostData).Select(r => r.Name).Distinct().Take(10).ToList();
            return summary;
        }

        public async Task<PagedResult<ProfitabilityRow>> GetProfitabilityRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var rows = new List<ProfitabilityRow>();

            using var piDb = new ProformaInvoicesDbContext();
            var query = piDb.ProformaInvoices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(p => EF.Functions.Like(p.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(p => EF.Functions.Like(p.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(p => p.ProformaId == filter.ProformaId);
            if (filter.DateFrom.HasValue)
                query = query.Where(p => p.IssueDate >= filter.DateFrom.Value || (p.IssueDate == default && p.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(p => p.IssueDate <= filter.DateTo.Value || (p.IssueDate == default && p.CreatedAt <= filter.DateTo.Value));

            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (totalCount == 0)
                return new PagedResult<ProfitabilityRow> { Items = rows, TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var pis = await query.OrderByDescending(p => p.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(p => new { p.ProformaId, p.CompanyName, p.CustomerName, p.ProjectName, p.GrandTotalVatInclusive, p.Status, p.IssueDate, p.FullSnapshotJson })
                .ToListAsync(ct).ConfigureAwait(false);

            foreach (var pi in pis)
            {
                var snap = TryDeserializePiSnapshot(pi.FullSnapshotJson);
                var pl = snap?.ProfitLoss;
                bool hasCost = pl != null && (pl.TotalGlassCost > 0 || pl.TotalProductionCost > 0);
                decimal revenue = pl != null && pl.NetExVat > 0 ? pl.NetExVat : VatCalculator.Round(pi.GrandTotalVatInclusive / VatCalculator.VAT_MULTIPLIER);
                decimal cost = hasCost ? pl!.TotalGlassCost + pl.TotalProductionCost : 0;
                decimal profit = hasCost ? revenue - cost : 0;
                decimal margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 1);

                int qty = 0; decimal sqm = 0;
                if (snap?.SpecGroups != null)
                    foreach (var g in snap.SpecGroups)
                        if (g?.ItemRows != null)
                            foreach (var r in g.ItemRows)
                            {
                                qty += r.Qty;
                                sqm += r.TotalSqm;
                            }

                rows.Add(new ProfitabilityRow
                {
                    Key = pi.ProformaId,
                    Name = pi.ProformaId,
                    Category = "PI",
                    Revenue = revenue,
                    Cost = cost,
                    GlassCost = pl?.TotalGlassCost ?? 0,
                    ProductionCost = pl?.TotalProductionCost ?? 0,
                    Profit = profit,
                    Margin = margin,
                    Qty = qty,
                    Sqm = Math.Round(sqm, 3),
                    DocumentCount = 1,
                    HasCostData = hasCost,
                    CostSource = hasCost ? (pl!.CostAtSellingPrice ? "Selling price" : "Purchase price") + $" (wastage {pl.WastagePercent}%)" : "Missing — no Step 9 calculation saved"
                });
            }

            return new PagedResult<ProfitabilityRow> { Items = rows, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }
    }
}

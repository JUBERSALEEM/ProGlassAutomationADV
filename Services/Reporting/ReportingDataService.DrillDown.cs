// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.DrillDown.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the DrillDown
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
        //  DRILL-DOWN HELPERS
        // ════════════════════════════════════════════════════════════════

        public async Task<List<GlassSpecReportRow>> GetPiRowsDetailAsync(string proformaId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<GlassSpecReportRow>();
            var res = await GetGlassSpecRowsAsync(new ReportingFilter { ProformaId = proformaId.Trim(), Page = 1, PageSize = 500 }, ct);
            return res.Items;
        }

        public async Task<QuantityReconciliation> GetQuantityReconciliationAsync(string proformaId, CancellationToken ct = default)
        {
            return await RowTraceabilityService.Instance.CalculateReconciliationAsync(proformaId, "", 0, 0, ct);
        }

        public async Task<Dictionary<string, object>> GetDocumentChainAsync(string proformaId, CancellationToken ct = default)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(proformaId)) return dict;
            var pi = proformaId.Trim();

            var piRows = await GetPiRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 5 }, ct);
            dict["Proforma"] = piRows.Items.FirstOrDefault() ?? new PiReportRow { ProformaId = pi };

            var joRows = await GetJoRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 10 }, ct);
            dict["JobOrders"] = joRows.Items;
            dict["HasJobOrder"] = joRows.Items.Count > 0;

            var dnRows = await GetDeliveryRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 20 }, ct);
            dict["DeliveryNotes"] = dnRows.Items;

            var tiRows = await GetTaxInvoiceRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 20 }, ct);
            dict["TaxInvoices"] = tiRows.Items;

            var glassRows = await GetPiRowsDetailAsync(pi, ct);
            dict["GlassRows"] = glassRows;
            dict["TotalQty"] = glassRows.Sum(r => r.Qty);
            dict["TotalSqm"] = Math.Round(glassRows.Sum(r => r.TotalSqm), 3);

            dict["Reconciliation"] = await GetQuantityReconciliationAsync(pi, ct);

            var profit = await GetProfitabilityRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 5 }, ct);
            // Intentional: a PI with no profitability rows stores a
            // null slot in the chain dictionary (consumers already
            // treat missing rows as "not computed").
            dict["Profitability"] = profit.Items.FirstOrDefault()!;

            return dict;
        }

        // ── Filter lookups ──────────────────────────────────────────────

        public async Task<List<string>> GetCompanyOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.CompanyName).Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetCustomerOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.CustomerName).Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetSalesmanOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new MasterDataDbContext();
                var list = await db.Salesmen.AsNoTracking().Select(s => s.Name).ToListAsync(ct).ConfigureAwait(false);
                var clientReps = await db.Clients.AsNoTracking().Select(c => c.SalesRepresentative).Distinct().ToListAsync(ct).ConfigureAwait(false);
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list.Where(x => !string.IsNullOrWhiteSpace(x))) set.Add(s.Trim());
                foreach (var s in clientReps.Where(x => !string.IsNullOrWhiteSpace(x))) set.Add(s.Trim());
                return set.OrderBy(s => s).ToList();
            }
            catch (Exception ex) { AppLogService.Warning($"Salesman options masterdata warning: {ex.Message}"); }
            return new List<string>();
        }

        public async Task<List<string>> GetProjectOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.ProjectName).Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetGlassTypeOptionsAsync(CancellationToken ct = default)
        {
            return new List<string> { "SGU", "DGU", "LAM", "DGU+LAM" };
        }

        // ── Delivery report filter lookups (data-driven) ──────────────

        public async Task<List<string>> GetDeliveryPiOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && !string.IsNullOrWhiteSpace(d.ProformaId))
                    .Select(d => d.ProformaId)
                    .Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliverySpecificationOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNoteItems.AsNoTracking()
                    .Where(i => !string.IsNullOrWhiteSpace(i.SpecGroupName))
                    .Select(i => i.SpecGroupName)
                    .Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliveryGlassRefOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNoteItems.AsNoTracking()
                    .Where(i => !string.IsNullOrWhiteSpace(i.GlassRef))
                    .Select(i => i.GlassRef)
                    .Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliveryProjectOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && !string.IsNullOrWhiteSpace(d.ProjectName))
                    .Select(d => d.ProjectName)
                    .Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliverySalesmanOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var joDb = new JobOrdersDbContext();
                var snapshots = await joDb.JobOrders.AsNoTracking()
                    .Select(j => new { j.FullSnapshotJson })
                    .ToListAsync(ct).ConfigureAwait(false);

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in snapshots)
                {
                    var snap = JobOrderMapper.DeserializeSnapshot(
                        new SavedJobOrder { FullSnapshotJson = s.FullSnapshotJson });
                    var rep = (snap?.EmbeddedInvoice?.Client?.SalesRepresentative ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(rep)) set.Add(rep);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch (Exception ex) { AppLogService.Warning($"Delivery salesman options warning: {ex.Message}"); }
            return new List<string>();
        }

        public async Task<List<string>> GetJoSourcePiOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new JobOrdersDbContext();
                var list = await db.JobOrders.AsNoTracking()
                    .Where(j => !string.IsNullOrWhiteSpace(j.SourceProformaId))
                    .Select(j => j.SourceProformaId)
                    .Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetTiProformaOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new TaxInvoiceDbContext();
                var list = await db.TaxInvoices.AsNoTracking()
                    .Where(t => !string.IsNullOrWhiteSpace(t.ProformaId))
                    .Select(t => t.ProformaId)
                    .Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetPiStatusOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.Status).Distinct().ToListAsync(ct).ConfigureAwait(false);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetJoStatusOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new JobOrdersDbContext();
                var list = await db.JobOrders.AsNoTracking().Select(j => j.Status).Distinct().ToListAsync(ct).ConfigureAwait(false);
                // Normalise the legacy "InProduction" ↔ "In Production" split
                // to a single human label so the dropdown reflects real data.
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var v = s!.Trim();
                    set.Add(Eq(v, "InProduction") ? "In Production" : v);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetTiStatusOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new TaxInvoiceDbContext();
                var list = await db.TaxInvoices.AsNoTracking().Select(t => t.Status).Distinct().ToListAsync(ct).ConfigureAwait(false);
                // Normalise "PartiallyPaid" ↔ "Partially Paid" to one label.
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var v = s!.Trim();
                    set.Add(Eq(v, "PartiallyPaid") ? "Partially Paid" : v);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        // ── VAT Submission Report ─────────────────────────────────────
    }
}

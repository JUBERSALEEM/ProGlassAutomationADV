// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/RowTraceabilityService.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — End-to-End Row Traceability & Reconciliation Engine
//
// PURPOSE:
//   Answers the core manufacturing & accounting questions:
//     • "Where did this exact glass item go across the factory?"
//     • "How many were ordered, cut, delivered, invoiced, and settled?"
//     • "What is the exact financial and fulfillment reconciliation?"
//
// RELATIONAL AUDIT CHAIN (Verified from actual SQLite databases):
//   Proforma Invoice  (proformainvoices.db)
//   ↓
//   Job Order         (joborders.db)
//   ↓
//   Delivery Note(s)  (deliverynotes.db -> DeliveryNoteItems)
//   ↓
//   Tax Invoice(s)    (taxinvoices.db   -> TaxInvoiceItems)
//   ↓
//   Account Statement (statements.db)
//   ↓
//   Profitability P&L (embedded line cost calculations)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>
    /// Contract for end-to-end specification row lifecycle tracing and quantity reconciliation.
    /// </summary>
    public interface IRowTraceabilityService
    {
        Task<RowTrace?> TraceRowLifecycleAsync(string rowId, CancellationToken ct = default);
        Task<QuantityReconciliation> CalculateReconciliationAsync(string proformaId, string glassRef = "", int w1 = 0, int h1 = 0, CancellationToken ct = default);
    }

    /// <summary>
    /// Implementation of the High-Performance Relational Traceability Engine.
    /// </summary>
    public sealed class RowTraceabilityService : IRowTraceabilityService
    {
        public static readonly RowTraceabilityService Instance = new();

        private static bool Eq(string? a, string? b) =>
            string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reconstructs the complete unbroken audit trail for a single glass specification row.
        /// </summary>
        public async Task<RowTrace?> TraceRowLifecycleAsync(string rowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rowId)) return null;

            // 1. Parse composite RowId: e.g. "PI-2026-0842-G1-S1" -> ProformaId = "PI-2026-0842"
            string piId = rowId.Trim();
            var gIdx = rowId.IndexOf("-G", StringComparison.OrdinalIgnoreCase);
            if (gIdx > 0) piId = rowId.Substring(0, gIdx);

            // 2. Query target Proforma Invoice
            SavedProformaInvoice? piEntity;
            using (var piDb = new ProformaInvoicesDbContext())
            {
                piEntity = await piDb.ProformaInvoices.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProformaId == piId, ct)
                    .ConfigureAwait(false);
            }
            if (piEntity == null) return null;

            var piSnap = TryDeserializePiSnapshot(piEntity.FullSnapshotJson);
            if (piSnap == null) return null;

            var rows = SpecificationIntelligenceService.Instance.ExtractGlassRowsFromPi(
                piSnap, piEntity.ProformaId, piEntity.CompanyName, piEntity.CustomerName,
                piSnap.Client?.SalesRepresentative ?? "", piEntity.IssueDate, piEntity.Status);

            var targetRow = rows.FirstOrDefault(r => Eq(r.RowId, rowId)) ?? rows.FirstOrDefault();
            if (targetRow == null) return null;

            var trace = new RowTrace { SourceRow = targetRow };

            // ── Step 1: Proforma Invoice ──────────────────────────────────────
            trace.Steps.Add(new TraceStep
            {
                Stage = "Proforma Invoice",
                DocumentNumber = piEntity.ProformaId,
                Status = piEntity.Status,
                Qty = targetRow.Qty,
                Value = targetRow.Amount,
                Date = piEntity.IssueDate,
                Notes = $"Quoted for {piEntity.CustomerName} ({piEntity.ProjectName})"
            });

            // ── Step 2: Job Order ─────────────────────────────────────────────
            List<SavedJobOrder> jobOrders;
            using (var joDb = new JobOrdersDbContext())
            {
                jobOrders = await joDb.JobOrders.AsNoTracking()
                    .Where(j => j.SourceProformaId == piId)
                    .OrderBy(j => j.CreatedAt)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }

            if (jobOrders.Count > 0)
            {
                foreach (var jo in jobOrders)
                {
                    trace.Steps.Add(new TraceStep
                    {
                        Stage = "Job Order",
                        DocumentNumber = jo.JobOrderId,
                        Status = jo.Status,
                        Qty = targetRow.Qty,
                        Value = jo.GrandTotalVatInclusive,
                        Date = jo.DeliveryDate ?? jo.CreatedAt,
                        Notes = $"Production status: {jo.Status} · Priority: {jo.Priority}"
                    });
                    targetRow.JobOrderId = jo.JobOrderId;
                }
            }
            else
            {
                trace.Steps.Add(new TraceStep
                {
                    Stage = "Job Order",
                    DocumentNumber = "—",
                    Status = "Awaiting Conversion",
                    Qty = 0,
                    Notes = "Proforma has not yet been converted to a factory Job Order"
                });
            }

            // ── Step 3: Delivery Notes ────────────────────────────────────────
            int totalDelivered = 0;
            int totalRework = 0;
            int totalReturned = 0;

            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var dnItems = await dnDb.DeliveryNoteItems.AsNoTracking()
                    .Include(i => i.DeliveryNote)
                    .Where(i => i.DeliveryNote != null && i.DeliveryNote.ProformaId == piId && !i.DeliveryNote.IsMasterDn)
                    .OrderBy(i => i.DeliveryNote!.DeliveryDate)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                // Match specific line item by GlassRef + Dimensions
                var matchingDnItems = dnItems.Where(i =>
                    Eq(i.GlassRef, targetRow.GlassRef) &&
                    i.W1 == targetRow.W1 && i.H1 == targetRow.H1).ToList();

                if (matchingDnItems.Count > 0)
                {
                    foreach (var item in matchingDnItems)
                    {
                        var dn = item.DeliveryNote!;
                        totalDelivered += item.DeliveredQty;
                        totalRework += item.ReworkQty;
                        totalReturned += item.ReturnedQty;

                        trace.Steps.Add(new TraceStep
                        {
                            Stage = "Delivery",
                            DocumentNumber = dn.DeliveryNoteId,
                            Status = dn.Status,
                            Qty = item.DeliveredQty,
                            Date = dn.DeliveryDate,
                            Notes = $"Driver: {dn.DriverName} · Vehicle: {dn.VehicleNumber} · Type: {dn.DeliveryType}"
                        });
                        targetRow.DeliveryNoteId = dn.DeliveryNoteId;
                    }
                }
                else
                {
                    trace.Steps.Add(new TraceStep
                    {
                        Stage = "Delivery",
                        DocumentNumber = "—",
                        Status = "Pending Dispatch",
                        Qty = 0,
                        Notes = "No delivery note has been dispatched for this item"
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Trace DN query error: {ex.Message}");
            }

            // ── Step 4: Tax Invoices ──────────────────────────────────────────
            int totalInvoiced = 0;
            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiItems = await tiDb.TaxInvoiceItems.AsNoTracking()
                    .Include(t => t.TaxInvoice)
                    .Where(t => t.ProformaId == piId)
                    .OrderBy(t => t.TaxInvoice != null ? t.TaxInvoice.IssueDate : DateTime.MinValue)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var matchingTiItems = tiItems.Where(t =>
                    Eq(t.GlassRef, targetRow.GlassRef) &&
                    t.W1 == targetRow.W1 && t.H1 == targetRow.H1).ToList();

                if (matchingTiItems.Count > 0)
                {
                    foreach (var item in matchingTiItems)
                    {
                        var ti = item.TaxInvoice;
                        totalInvoiced += item.Qty;

                        trace.Steps.Add(new TraceStep
                        {
                            Stage = "Tax Invoice",
                            DocumentNumber = item.InvoiceNumber,
                            Status = ti?.Status ?? "Issued",
                            Qty = item.Qty,
                            Value = item.Value,
                            Date = ti?.IssueDate ?? DateTime.Today,
                            Notes = $"Billed amount: AED {item.Value:N2} · Status: {ti?.Status ?? "Issued"}"
                        });
                        targetRow.TaxInvoiceNumber = item.InvoiceNumber;
                    }
                }
                else
                {
                    trace.Steps.Add(new TraceStep
                    {
                        Stage = "Tax Invoice",
                        DocumentNumber = "—",
                        Status = "Pending Invoice",
                        Qty = 0,
                        Notes = "Item not yet billed on a Tax Invoice"
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Trace TI query error: {ex.Message}");
            }

            // ── Step 5: Profitability ─────────────────────────────────────────
            var pl = piSnap.ProfitLoss;
            if (pl != null && (pl.TotalGlassCost > 0 || pl.TotalProductionCost > 0))
            {
                decimal rev = pl.NetExVat > 0 ? pl.NetExVat : Math.Round(piEntity.GrandTotalVatInclusive / 1.05m, 2);
                decimal cost = pl.TotalGlassCost + pl.TotalProductionCost;
                decimal profit = rev - cost;
                decimal margin = rev == 0 ? 0 : Math.Round(profit / rev * 100, 1);

                trace.Steps.Add(new TraceStep
                {
                    Stage = "Profitability",
                    DocumentNumber = $"P&L ({piEntity.ProformaId})",
                    Status = profit >= 0 ? "Profitable" : "Loss",
                    Value = profit,
                    Notes = $"Margin: {margin}% · Net: AED {rev:N0} · Total Cost: AED {cost:N0} (Glass: AED {pl.TotalGlassCost:N0}, Prod: AED {pl.TotalProductionCost:N0})"
                });
            }

            // ── Live Reconciliation State Machine ─────────────────────────────
            targetRow.DeliveredQty = totalDelivered;
            targetRow.BalanceQty = Math.Max(0, targetRow.Qty - totalDelivered);
            targetRow.InvoicedQty = totalInvoiced;

            targetRow.DeliveryStatus = totalDelivered == 0 ? "Pending" : totalDelivered < targetRow.Qty ? "Partial" : "Complete";
            targetRow.InvoiceStatus = totalInvoiced == 0 ? "Pending" : totalInvoiced < targetRow.Qty ? "Partial" : "Complete";

            trace.Reconciliation = new QuantityReconciliation
            {
                OrderedQty = targetRow.Qty,
                JobOrderQty = jobOrders.Count > 0 ? targetRow.Qty : 0,
                DeliveredQty = totalDelivered,
                RemainingQty = targetRow.BalanceQty,
                InvoicedQty = totalInvoiced,
                DeliveryState = targetRow.DeliveryStatus == "Complete" ? "FullyDelivered" : targetRow.DeliveryStatus == "Partial" ? "PartiallyDelivered" : "NotDelivered",
                InvoiceState = targetRow.InvoiceStatus == "Complete" ? "FullyInvoiced" : targetRow.InvoiceStatus == "Partial" ? "PartiallyInvoiced" : "Pending"
            };

            return trace;
        }

        /// <summary>
        /// Fast reconciliation calculation for document or line item.
        /// </summary>
        public async Task<QuantityReconciliation> CalculateReconciliationAsync(
            string proformaId,
            string glassRef = "",
            int w1 = 0,
            int h1 = 0,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new QuantityReconciliation();

            var recon = new QuantityReconciliation();
            try
            {
                // 1. PI Ordered Qty
                using (var piDb = new ProformaInvoicesDbContext())
                {
                    var snapJson = await piDb.ProformaInvoices.AsNoTracking()
                        .Where(p => p.ProformaId == proformaId)
                        .Select(p => p.FullSnapshotJson)
                        .FirstOrDefaultAsync(ct)
                        .ConfigureAwait(false);

                    var snap = TryDeserializePiSnapshot(snapJson);
                    if (snap != null)
                    {
                        var rows = SpecificationIntelligenceService.Instance.ExtractGlassRowsFromPi(snap, proformaId, "", "", "", DateTime.Now, "");
                        if (!string.IsNullOrWhiteSpace(glassRef) && w1 > 0 && h1 > 0)
                            recon.OrderedQty = rows.Where(r => Eq(r.GlassRef, glassRef) && r.W1 == w1 && r.H1 == h1).Sum(r => r.Qty);
                        else
                            recon.OrderedQty = rows.Sum(r => r.Qty);
                    }
                }

                // 2. JO Committed Qty
                recon.JobOrderQty = recon.OrderedQty;

                // 3. Delivered Qty via DN Master or items
                using (var dnDb = new DeliveryNotesDbContext())
                {
                    var delivered = await dnDb.DeliveryNotes.AsNoTracking()
                        .Where(d => d.ProformaId == proformaId && d.IsMasterDn)
                        .Select(d => d.TotalDeliveredQty)
                        .FirstOrDefaultAsync(ct)
                        .ConfigureAwait(false);
                    recon.DeliveredQty = delivered;
                }

                // 4. Invoiced Qty via TI
                using (var tiDb = new TaxInvoiceDbContext())
                {
                    var tiQtys = await tiDb.TaxInvoices.AsNoTracking()
                        .Where(t => t.ProformaId == proformaId)
                        .Select(t => t.TotalQty)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);
                    recon.InvoicedQty = tiQtys.Sum();
                }

                recon.RemainingQty = Math.Max(0, recon.OrderedQty - recon.DeliveredQty);
                recon.DeliveryState = recon.DeliveredQty == 0 ? "NotDelivered" : recon.DeliveredQty < recon.OrderedQty ? "PartiallyDelivered" : "FullyDelivered";
                recon.InvoiceState = recon.InvoicedQty == 0 ? "Pending" : recon.InvoicedQty < recon.OrderedQty ? "PartiallyInvoiced" : "FullyInvoiced";
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"CalculateReconciliationAsync error: {ex.Message}");
            }

            return recon;
        }

        private static ProformaInvoiceSnapshot? TryDeserializePiSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch { return null; }
        }
    }
}

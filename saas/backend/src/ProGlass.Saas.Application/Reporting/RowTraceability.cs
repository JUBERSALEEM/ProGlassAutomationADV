// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 21:
//   Services/Reporting/RowTraceabilityService.cs
//   Services/Reporting/ReportingModels.cs (RowTrace/TraceStep/
//   QuantityReconciliation)
// Answers: "Where did this exact glass item go across the factory?" and
// "How many were ordered, cut, delivered, invoiced, and settled?" —
// the relational audit chain PI → JO → DN → TI → P&L.
//
// Documented differences (mapping §1.9):
//  • Row identity: the WPF matches delivery/invoice items to the quoted
//    row by GlassRef + W1 + H1 (glass ref is a free-text snapshot field).
//    The SaaS carries the (SpecGroupNumber, SerialNumber) composite key
//    on every delivery and invoice line — the robust identity the WPF
//    reaches for when the text match fails — so the trace matches on it.
//  • The quoted row set comes from the normalized spec_group_items (the
//    WPF re-extracts glass rows from the PI snapshot JSON).
//  • The P&L step reads the persisted Step 9 KPI columns.
//  • Verbatim quirk kept: the WPF's delivery query excludes master DNs
//    but NOT voided ones — preserved and documented (the reconciliation
//    endpoint DOES exclude void; the trace does not, exactly like the
//    WPF).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;

namespace ProGlass.Saas.Application.Reporting;

/// <summary>The quoted glass row the trace starts from.</summary>
public sealed record TraceSourceRowDto(
    string RowId,
    string ProformaNo,
    int SpecGroupNumber,
    int SerialNumber,
    string Specification,
    string GlassType,
    string GlassRef,
    int W1, int H1, int W2, int H2,
    int Qty,
    decimal Rate,
    decimal Amount);

/// <summary>One stage of the audit chain.</summary>
public sealed record TraceStepDto(
    string Stage,
    string DocumentNumber,
    string Status,
    int Qty,
    decimal Value,
    DateTime? Date,
    string Notes);

/// <summary>The live quantity reconciliation (WPF QuantityReconciliation).</summary>
public sealed record QuantityReconciliationDto(
    int OrderedQty,
    int JobOrderQty,
    int DeliveredQty,
    int RemainingQty,
    int InvoicedQty,
    string DeliveryState,
    string InvoiceState);

public sealed record RowTraceDto(
    TraceSourceRowDto SourceRow,
    List<TraceStepDto> Steps,
    QuantityReconciliationDto Reconciliation);

public sealed class RowTraceabilityService(IReportingStore store)
{
    /// <summary>
    /// Reconstructs the complete unbroken audit trail for one glass
    /// specification row. The row id is the WPF composite
    /// "PI-2026-0842-G1-S1" (proforma number + group + serial); a bare
    /// proforma number traces the FIRST row, exactly like the WPF's
    /// fallback.
    /// </summary>
    public async Task<RowTraceDto?> TraceRowLifecycleAsync(
        Guid tenantId, string rowId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rowId)) return null;

        // 1. Parse the composite row id → proforma number.
        string piNo = rowId.Trim();
        int groupNo = 0, serial = 0;
        var gIdx = rowId.IndexOf("-G", StringComparison.OrdinalIgnoreCase);
        if (gIdx > 0)
        {
            piNo = rowId.Substring(0, gIdx);
            var rest = rowId.Substring(gIdx + 2);
            var sIdx = rest.IndexOf("-S", StringComparison.OrdinalIgnoreCase);
            if (sIdx > 0
                && int.TryParse(rest.Substring(0, sIdx), out var g)
                && int.TryParse(rest.Substring(sIdx + 2), out var s))
            {
                groupNo = g;
                serial = s;
            }
        }

        var pi = await store.FindTraceProformaAsync(tenantId, piNo, ct).ConfigureAwait(false);
        if (pi is null) return null;

        var rows = await store.ListTraceRowsAsync(tenantId, pi.Id, ct).ConfigureAwait(false);
        if (rows.Count == 0) return null;
        rows = rows.Select(r => r with
        {
            RowId = $"{pi.ProformaNo}-G{r.SpecGroupNumber}-S{r.SerialNumber}",
            ProformaNo = pi.ProformaNo
        }).ToList();

        // Exact row when the composite key resolves; the first row
        // otherwise (the WPF's Rows.FirstOrDefault fallback).
        var targetRow = rows.FirstOrDefault(r => r.SpecGroupNumber == groupNo
                                                 && r.SerialNumber == serial)
                        ?? rows[0];

        var trace = new RowTraceDto(targetRow, new List<TraceStepDto>(),
            new QuantityReconciliationDto(0, 0, 0, 0, 0, "NotDelivered", "Pending"));

        // ── Step 1: Proforma Invoice ──────────────────────────────────
        trace.Steps.Add(new TraceStepDto(
            "Proforma Invoice",
            pi.ProformaNo,
            pi.Status,
            targetRow.Qty,
            targetRow.Amount,
            pi.IssueDate?.ToDateTime(TimeOnly.MinValue),
            $"Quoted for {pi.CustomerName} ({pi.ProjectName})"));

        // ── Step 2: Job Order ─────────────────────────────────────────
        var jobOrders = await store.ListJobOrdersForProformaAsync(tenantId, pi.Id, ct)
            .ConfigureAwait(false);

        if (jobOrders.Count > 0)
        {
            foreach (var jo in jobOrders)
            {
                trace.Steps.Add(new TraceStepDto(
                    "Job Order",
                    jo.JobNo,
                    jo.Status,
                    targetRow.Qty,
                    jo.GrandTotalVatInclusive,
                    (jo.DeliveryDate ?? DateOnly.FromDateTime(jo.CreatedAt)).ToDateTime(TimeOnly.MinValue),
                    $"Production status: {jo.Status} · Priority: {jo.Priority}"));
            }
        }
        else
        {
            trace.Steps.Add(new TraceStepDto(
                "Job Order", "—", "Awaiting Conversion", 0, 0m, null,
                "Proforma has not yet been converted to a factory Job Order"));
        }

        // ── Step 3: Delivery Notes ────────────────────────────────────
        int totalDelivered = 0, totalRework = 0, totalReturned = 0;

        var dnItems = await store.ListTraceDeliveriesAsync(tenantId, pi.Id, ct)
            .ConfigureAwait(false);
        var matchingDnItems = dnItems
            .Where(i => i.SpecGroupNumber == targetRow.SpecGroupNumber
                        && i.SerialNumber == targetRow.SerialNumber)
            .ToList();

        if (matchingDnItems.Count > 0)
        {
            foreach (var item in matchingDnItems)
            {
                totalDelivered += item.DeliveredQty;
                totalRework += item.ReworkQty;
                totalReturned += item.ReturnedQty;

                trace.Steps.Add(new TraceStepDto(
                    "Delivery",
                    item.DeliveryNo,
                    item.Status,
                    item.DeliveredQty,
                    0m,
                    item.DeliveryDate?.ToDateTime(TimeOnly.MinValue),
                    $"Driver: {item.Driver} · Vehicle: {item.Vehicle} · Type: {item.DeliveryType}"));
            }
        }
        else
        {
            trace.Steps.Add(new TraceStepDto(
                "Delivery", "—", "Pending Dispatch", 0, 0m, null,
                "No delivery note has been dispatched for this item"));
        }

        // ── Step 4: Tax Invoices ──────────────────────────────────────
        int totalInvoiced = 0;

        var tiItems = await store.ListTraceInvoiceItemsAsync(tenantId, pi.Id, ct)
            .ConfigureAwait(false);
        var matchingTiItems = tiItems
            .Where(t => t.SpecGroupNumber == targetRow.SpecGroupNumber
                        && t.SerialNumber == targetRow.SerialNumber)
            .ToList();

        if (matchingTiItems.Count > 0)
        {
            foreach (var item in matchingTiItems)
            {
                totalInvoiced += item.Qty;

                trace.Steps.Add(new TraceStepDto(
                    "Tax Invoice",
                    item.InvoiceNo,
                    item.Status,
                    item.Qty,
                    item.Value,
                    item.IssueDate?.ToDateTime(TimeOnly.MinValue),
                    $"Billed amount: AED {item.Value:N2} · Status: {item.Status}"));
            }
        }
        else
        {
            trace.Steps.Add(new TraceStepDto(
                "Tax Invoice", "—", "Pending Invoice", 0, 0m, null,
                "Item not yet billed on a Tax Invoice"));
        }

        // ── Step 5: Profitability ─────────────────────────────────────
        if (pi.PnlCalculatedAt != null
            && (pi.PnlTotalGlassCost > 0 || pi.PnlTotalProductionCost > 0))
        {
            decimal rev = pi.PnlNetExVat > 0
                ? pi.PnlNetExVat
                : MoneyRounding.Round(pi.GrandTotalVatInclusive / 1.05m);
            decimal cost = pi.PnlTotalGlassCost + pi.PnlTotalProductionCost;
            decimal profit = rev - cost;
            decimal margin = rev == 0 ? 0 : Math.Round(profit / rev * 100, 1);

            trace.Steps.Add(new TraceStepDto(
                "Profitability",
                $"P&L ({pi.ProformaNo})",
                profit >= 0 ? "Profitable" : "Loss",
                0,
                profit,
                pi.PnlCalculatedAt,
                $"Margin: {margin}% · Net: AED {rev:N0} · Total Cost: AED {cost:N0} " +
                $"(Glass: AED {pi.PnlTotalGlassCost:N0}, Prod: AED {pi.PnlTotalProductionCost:N0})"));
        }

        // ── Live reconciliation state ─────────────────────────────────
        int balance = Math.Max(0, targetRow.Qty - totalDelivered);
        string deliveryStatus = totalDelivered == 0 ? "Pending"
            : totalDelivered < targetRow.Qty ? "Partial"
            : "Complete";
        string invoiceStatus = totalInvoiced == 0 ? "Pending"
            : totalInvoiced < targetRow.Qty ? "Partial"
            : "Complete";

        trace = trace with
        {
            Reconciliation = new QuantityReconciliationDto(
                targetRow.Qty,
                jobOrders.Count > 0 ? targetRow.Qty : 0,
                totalDelivered,
                balance,
                totalInvoiced,
                deliveryStatus == "Complete" ? "FullyDelivered"
                    : deliveryStatus == "Partial" ? "PartiallyDelivered"
                    : "NotDelivered",
                invoiceStatus == "Complete" ? "FullyInvoiced"
                    : invoiceStatus == "Partial" ? "PartiallyInvoiced"
                    : "Pending")
        };

        return trace;
    }

    /// <summary>
    /// Fast reconciliation for a document or line item (the WPF's
    /// CalculateReconciliationAsync): ordered from the quoted rows
    /// (optionally narrowed to one line), delivered from the PI's
    /// non-master delivery-note lines, invoiced from the PI's tax
    /// invoice lines.
    /// </summary>
    public async Task<QuantityReconciliationDto> CalculateReconciliationAsync(
        Guid tenantId, string proformaNo,
        int? groupNumber = null, int? serialNumber = null,
        CancellationToken ct = default)
    {
        var recon = new QuantityReconciliationDto(0, 0, 0, 0, 0, "NotDelivered", "Pending");
        if (string.IsNullOrWhiteSpace(proformaNo)) return recon;

        var pi = await store.FindTraceProformaAsync(tenantId, proformaNo, ct).ConfigureAwait(false);
        if (pi is null) return recon;

        var rows = await store.ListTraceRowsAsync(tenantId, pi.Id, ct).ConfigureAwait(false);
        if (groupNumber.HasValue && serialNumber.HasValue)
            recon = recon with
            {
                OrderedQty = rows
                    .Where(r => r.SpecGroupNumber == groupNumber && r.SerialNumber == serialNumber)
                    .Sum(r => r.Qty)
            };
        else
            recon = recon with { OrderedQty = rows.Sum(r => r.Qty) };

        // JO committed qty (the WPF sets it to the ordered qty).
        var jobCount = await store.CountJobOrdersForProformaAsync(tenantId, pi.Id, ct)
            .ConfigureAwait(false);
        recon = recon with { JobOrderQty = jobCount > 0 ? recon.OrderedQty : 0 };

        var dnItems = await store.ListTraceDeliveriesAsync(tenantId, pi.Id, ct).ConfigureAwait(false);
        IEnumerable<TraceDeliveryItem> matched = dnItems;
        if (groupNumber.HasValue && serialNumber.HasValue)
            matched = dnItems.Where(i => i.SpecGroupNumber == groupNumber
                                         && i.SerialNumber == serialNumber);
        int delivered = matched.Sum(i => i.DeliveredQty);
        recon = recon with { DeliveredQty = delivered };

        var tiItems = await store.ListTraceInvoiceItemsAsync(tenantId, pi.Id, ct).ConfigureAwait(false);
        IEnumerable<TraceInvoiceItem> matchedTi = tiItems;
        if (groupNumber.HasValue && serialNumber.HasValue)
            matchedTi = tiItems.Where(t => t.SpecGroupNumber == groupNumber
                                           && t.SerialNumber == serialNumber);
        int invoiced = matchedTi.Sum(t => t.Qty);
        recon = recon with { InvoicedQty = invoiced };

        recon = recon with
        {
            RemainingQty = Math.Max(0, recon.OrderedQty - recon.DeliveredQty),
            DeliveryState = delivered == 0 ? "NotDelivered"
                : delivered < recon.OrderedQty ? "PartiallyDelivered"
                : "FullyDelivered",
            InvoiceState = invoiced == 0 ? "Pending"
                : invoiced < recon.OrderedQty ? "PartiallyInvoiced"
                : "FullyInvoiced"
        };

        return recon;
    }
}

/// <summary>One PI-linked delivery line (trace projection).</summary>
public sealed record TraceDeliveryItem(
    string DeliveryNo, string Status, DateOnly? DeliveryDate,
    string Driver, string Vehicle, string DeliveryType,
    int SpecGroupNumber, int SerialNumber,
    int DeliveredQty, int ReworkQty, int ReturnedQty);

/// <summary>One PI-linked tax-invoice line (trace projection).</summary>
public sealed record TraceInvoiceItem(
    string InvoiceNo, string Status, DateOnly? IssueDate,
    int SpecGroupNumber, int SerialNumber, int Qty, decimal Value);

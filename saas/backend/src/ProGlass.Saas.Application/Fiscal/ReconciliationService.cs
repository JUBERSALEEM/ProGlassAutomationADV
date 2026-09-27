using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Application.Fiscal;

// ═══════════════════════════════════════════════════════════════════════
// Phase 19 — Order fulfillment reconciliation (WPF
// Models/Statement/Reconciliation/OrderReconciliationService.cs +
// ReconciliationModels.cs, ported).
//
// Per line item — the composite key "specGroupNumber|serialNumber" — three
// quantities are cross-referenced:
//   OrderedQty   PI spec_group_items.qty            (the authoritative order)
//   DeliveredQty Σ delivery_note_items.delivered_qty over the PI's job
//                orders' dispatch DNs (excludes Draft, Dummy and master
//                DNs — the WPF's "confirmed" filter)
//   InvoicedQty  Σ tax_invoice_items.qty over the PI's family invoices
//                (excludes Draft, Cancelled, Void)
// BalanceQty = MAX(0, ordered − delivered);
// InvoiceVsDeliveryDiff = invoiced − delivered (POSITIVE = over-invoiced —
// ★ the critical bug scenario the WPF service was built to catch).
//
// Status rules (WPF DetermineStatus, priority order, first match wins):
//   delivered == 0 && invoiced == 0 → NotYetStarted
//   invoiced > delivered            → OverInvoiced  (★ THE BUG)
//   delivered > invoiced            → UnderInvoiced (revenue leakage)
//   delivered > ordered             → OverDelivered (rare)
//   delivered < ordered             → PartialDelivery
//   otherwise                       → FullyMatched
// ═══════════════════════════════════════════════════════════════════════

public enum ReconciliationStatus
{
    FullyMatched,
    PartialDelivery,
    OverInvoiced,
    UnderInvoiced,
    NotYetStarted,
    OverDelivered
}

public sealed record ReconciliationRowDto(
    int SpecGroupNumber,
    int SerialNumber,
    string SpecGroupName,
    string GlassRef,
    int W1,
    int H1,
    int W2,
    int H2,
    decimal UnitSqm,
    decimal TotalSqm,
    int OrderedQty,
    int DeliveredQty,
    int BalanceQty,
    int InvoicedQty,
    int InvoiceVsDeliveryDiff,
    ReconciliationStatus Status);

public sealed record ReconciliationSummaryDto(
    string ProformaNo,
    string JobOrderIds,
    bool HasProformaData,
    bool HasDeliveryTracking,
    IReadOnlyList<ReconciliationRowDto> Rows,
    int TotalOrderedQty,
    int TotalDeliveredQty,
    int TotalBalanceQty,
    int TotalInvoicedQty,
    int TotalInvoiceVsDeliveryDiff,
    decimal TotalSqm,
    bool HasDiscrepancies,
    bool HasOverInvoicing,
    bool HasUnderInvoicing,
    bool HasPendingDelivery,
    bool AllMatched,
    int MatchedCount,
    int OverInvoicedCount,
    int UnderInvoicedCount,
    int PartialDeliveryCount,
    int NotStartedCount,
    ReconciliationStatus OverallStatus);

/// <summary>Line identity for the store's PI query.</summary>
public sealed record ProformaLine(
    int SpecGroupNumber,
    string SpecGroupName,
    string GlassType,
    int SerialNumber,
    string GlassRef,
    int W1,
    int H1,
    int W2,
    int H2,
    int Qty);

public sealed class ReconciliationService(IFiscalStore store)
{
    public async Task<FiscalResult<ReconciliationSummaryDto>> BuildForProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        var pi = await store.FindProformaAsync(tenantId, proformaId, ct);
        if (pi is null)
        {
            return FiscalResult<ReconciliationSummaryDto>.Fail(
                FiscalErrorCode.NotFound,
                $"Proforma invoice {proformaId} was not found.");
        }

        var lines = await store.ListProformaLinesAsync(tenantId, proformaId, ct);
        var jobNos = await store.ListJobOrderNosForProformaAsync(
            tenantId, proformaId, ct);

        var delivered = new Dictionary<(int Group, int Serial), int>();
        if (jobNos.Count > 0)
        {
            delivered = await store.SumDeliveredByLineAsync(
                tenantId,
                jobNos.Select(j => j.JobOrderId).ToList(), ct);
        }

        var invoiced = await store.SumInvoicedByLineAsync(
            tenantId, proformaId, ct);

        var rows = new List<ReconciliationRowDto>();
        foreach (var line in lines)
        {
            var key = (line.SpecGroupNumber, line.SerialNumber);
            var orderedQty = line.Qty;
            var deliveredQty = delivered.GetValueOrDefault(key);
            var invoicedQty = invoiced.GetValueOrDefault(key);
            rows.Add(new ReconciliationRowDto(
                line.SpecGroupNumber, line.SerialNumber,
                line.SpecGroupName, line.GlassRef,
                line.W1, line.H1, line.W2, line.H2,
                SqmCalculator.CalculatePanel(line.W1, line.H1),
                SqmCalculator.CalculateTotal(line.W1, line.H1, orderedQty),
                orderedQty, deliveredQty,
                Math.Max(0, orderedQty - deliveredQty),
                invoicedQty,
                invoicedQty - deliveredQty,
                DetermineStatus(orderedQty, deliveredQty, invoicedQty)));
        }

        // Rollups (WPF OrderReconciliationSummary computed properties).
        var hasOverInvoicing = rows.Any(r =>
            r.Status == ReconciliationStatus.OverInvoiced);
        var hasUnderInvoicing = rows.Any(r =>
            r.Status == ReconciliationStatus.UnderInvoiced);
        var hasPendingDelivery = rows.Any(r => r.BalanceQty > 0);
        var allMatched = rows.Count > 0 && rows.All(r =>
            r.Status == ReconciliationStatus.FullyMatched);

        ReconciliationStatus overall;
        if (lines.Count == 0)
        {
            overall = ReconciliationStatus.NotYetStarted;
        }
        else if (hasOverInvoicing)
        {
            overall = ReconciliationStatus.OverInvoiced;
        }
        else if (hasUnderInvoicing)
        {
            overall = ReconciliationStatus.UnderInvoiced;
        }
        else if (hasPendingDelivery)
        {
            overall = ReconciliationStatus.PartialDelivery;
        }
        else if (allMatched)
        {
            overall = ReconciliationStatus.FullyMatched;
        }
        else
        {
            overall = ReconciliationStatus.NotYetStarted;
        }

        return FiscalResult<ReconciliationSummaryDto>.Ok(
            new ReconciliationSummaryDto(
                pi.ProformaNo,
                string.Join(",", jobNos.Select(j => j.JobNo)),
                HasProformaData: lines.Count > 0,
                HasDeliveryTracking: jobNos.Count > 0,
                rows,
                rows.Sum(r => r.OrderedQty),
                rows.Sum(r => r.DeliveredQty),
                rows.Sum(r => r.BalanceQty),
                rows.Sum(r => r.InvoicedQty),
                rows.Sum(r => r.InvoiceVsDeliveryDiff),
                rows.Sum(r => r.TotalSqm),
                rows.Any(r => r.Status != ReconciliationStatus.FullyMatched),
                hasOverInvoicing,
                hasUnderInvoicing,
                hasPendingDelivery,
                allMatched,
                rows.Count(r => r.Status == ReconciliationStatus.FullyMatched),
                rows.Count(r => r.Status == ReconciliationStatus.OverInvoiced),
                rows.Count(r => r.Status == ReconciliationStatus.UnderInvoiced),
                rows.Count(r => r.Status == ReconciliationStatus.PartialDelivery),
                rows.Count(r => r.Status == ReconciliationStatus.NotYetStarted),
                overall));
    }

    /// <summary>WPF DetermineStatus — verbatim (priority order).</summary>
    internal static ReconciliationStatus DetermineStatus(
        int ordered, int delivered, int invoiced)
    {
        if (delivered == 0 && invoiced == 0)
            return ReconciliationStatus.NotYetStarted;
        if (invoiced > delivered)
            return ReconciliationStatus.OverInvoiced;
        if (delivered > invoiced)
            return ReconciliationStatus.UnderInvoiced;
        if (delivered > ordered)
            return ReconciliationStatus.OverDelivered;
        if (delivered < ordered)
            return ReconciliationStatus.PartialDelivery;
        return ReconciliationStatus.FullyMatched;
    }
}

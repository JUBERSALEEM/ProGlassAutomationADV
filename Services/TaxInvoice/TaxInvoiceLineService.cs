// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/TaxInvoice/TaxInvoiceLineService.cs
// PROJECT: ProGlassAutomation
// MODULE: Tax Invoice — Line Service (Phase 6: God VM Split)
// VERSION: Phase 6 — 2026-08-21 — Extracted from TaxInvoiceDetailViewModel 2131 LOC
//
// PURPOSE:
//   TaxInvoiceDetailViewModel was 2131 LOC — similar God VM as DN Detail.
//   Extracts:
//     • Line item calculations (qty, sqm, VAT, totals)
//     • Family math (Ordered, Invoiced, Balance)
//     • Numbering (INV-xxx, sequence letters)
//     • Validation
//
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.TaxInvoice
{
    public sealed class TaxInvoiceLineService
    {
        public static (int TotalQty, decimal TotalSqm) CalculateTotals(IEnumerable<TaxInvoiceItem> items)
        {
            int qty = items.Sum(i => i.Qty);
            decimal sqm = items.Sum(i => i.TotalSqm);
            return (qty, Math.Round(sqm, 3));
        }

        public static (decimal Subtotal, decimal Vat, decimal GrandTotal) CalculateFinancials(
            IEnumerable<TaxInvoiceItem> items,
            decimal discountAmount,
            decimal vatRate = 0.05m)
        {
            decimal subtotal = items.Sum(i => i.Value);
            decimal afterDiscount = Math.Max(0, subtotal - discountAmount);
            decimal vat = Math.Round(afterDiscount * vatRate, 2);
            decimal grandTotal = afterDiscount + vat;
            return (subtotal, vat, grandTotal);
        }

        public static (int InvoiceCount, decimal TotalInvoiced, decimal Balance) CalculateFamily(
            IEnumerable<SavedTaxInvoice> members,
            decimal proformaTotal,
            string? currentInvoiceNumber = null)
        {
            var issuedStatuses = new[] { "Issued", "Paid", "PartiallyPaid", "Partially Paid" };
            var issued = members.Where(m =>
                issuedStatuses.Contains(m.Status, StringComparer.OrdinalIgnoreCase) &&
                !string.Equals(m.InvoiceNumber, currentInvoiceNumber, StringComparison.OrdinalIgnoreCase)).ToList();

            decimal totalInvoiced = issued.Sum(m => m.GrandTotal);
            decimal balance = Math.Max(0, proformaTotal - totalInvoiced);
            return (members.Count(), totalInvoiced, balance);
        }

        public static string BuildInvoiceNumber(string proformaId, string sequenceLetter = "")
        {
            string core = proformaId.StartsWith("PI-", StringComparison.OrdinalIgnoreCase) ? proformaId.Substring(3) : proformaId;
            string baseNumber = $"INV-{core}";
            return string.IsNullOrWhiteSpace(sequenceLetter) ? baseNumber : $"{baseNumber}-{sequenceLetter.ToUpperInvariant().Trim()}";
        }

        public static Result ValidateInvoice(SavedTaxInvoice invoice, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();

            if (invoice == null)
                return Result.Fail("Invoice is null", corrId);

            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                return Result.Fail("Invoice number is required", corrId);

            if (string.IsNullOrWhiteSpace(invoice.ProformaId))
                return Result.Fail("Proforma reference is required for tax invoice", corrId);

            if (invoice.Items == null || invoice.Items.Count == 0)
                return Result.Fail("Invoice must have at least one line item", corrId);

            if (invoice.Items.Any(i => i.Qty <= 0))
                return Result.Fail("All line items must have qty > 0", corrId);

            if (invoice.GrandTotal <= 0)
                return Result.Fail("Grand total must be > 0", corrId);

            return Result.Ok(corrId);
        }

        public static Result ValidateStatusTransition(string currentStatus, string newStatus, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            var transition = Services.Workflow.StatusTransitions.Validate(
                Services.Workflow.StatusTransitions.Kind.TaxInvoice,
                currentStatus, newStatus);

            if (!transition.IsValid)
            {
                AppLogService.Warning(
                    $"TI status transition invalid: {currentStatus} → {newStatus}: {transition.Reason}",
                    new LogContext { Module = "TaxInvoice", Operation = "ValidateStatus", CorrelationId = corrId });
                return Result.Fail(transition.Reason ?? $"Cannot change status from {currentStatus} to {newStatus}", corrId);
            }

            return Result.Ok(corrId);
        }
    }
}

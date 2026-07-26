using ProGlassAutomation.Models.TaxInvoice;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.TaxInvoice.CalculationService
{
    public class InvoiceCalculationService
    {
        /// <summary>
        /// Calculates the line amount for a specific glass panel based on its dimensions, invoiced quantity, and rate.
        /// Uses SqmCalculator.CalculatePanel for accurate glass industry 0.5 m² minimum rule.
        /// </summary>
        public static decimal CalculateLineAmount(int w1, int h1, int invoicedQty, decimal rate)
        {
            if (w1 <= 0 || h1 <= 0 || invoicedQty <= 0)
                return 0.0m;

            decimal sqm = SqmCalculator.CalculatePanel(w1, h1);
            return Math.Round(sqm * invoicedQty * rate, 2);
        }

        /// <summary>
        /// Performs full VAT and discount calculations for the invoice summary.
        /// UAE Rules: Discount is applied before VAT.
        /// </summary>
        public static TaxInvoiceSummary RecalculateTotals(
            IEnumerable<TaxInvoiceItem> items,
            decimal discountValue,
            string currency,
            decimal exchangeRate)
        {
            decimal subtotal = 0.0m;
            foreach (var item in items)
            {
                item.Amount = CalculateLineAmount(item.W1, item.H1, item.InvoicedQty, item.Rate);
                subtotal += item.Amount;
            }

            decimal taxable = Math.Max(0.0m, subtotal - discountValue);
            decimal vat = TaxCalculationService.CalculateVat(taxable);
            decimal grandTotal = taxable + vat;

            return new TaxInvoiceSummary
            {
                Subtotal = subtotal,
                Discount = discountValue,
                TaxableAmount = taxable,
                VatAmount = vat,
                GrandTotal = grandTotal,
                Currency = currency,
                ExchangeRate = exchangeRate,
                VatPercent = 5.0m
            };
        }
    }
}
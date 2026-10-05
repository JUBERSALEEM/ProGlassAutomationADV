using ProGlassAutomation.Models.CreditNote.SavedCreditNote;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.CreditNote
{
    /// <summary>
    /// Builds a Credit Note from the Tax Invoice it corrects (ERP hardening —
    /// Phase 4). This is the ONLY correct way to create a credit note: the
    /// figures and party details are copied from the source invoice so the
    /// correction always ties back to a real fiscal document.
    ///
    /// Pure and side-effect free (no DB, no WPF) so it is unit-testable. The
    /// caller assigns the number (via the repository) and persists the result.
    /// Amounts are copied as POSITIVE magnitudes — the document TYPE is what
    /// makes them reduce the ledger.
    /// </summary>
    public static class CreditNoteFactory
    {
        /// <summary>
        /// Creates a full-value credit note that reverses the entire invoice.
        /// A partial credit is produced by editing the returned draft's lines
        /// and totals before saving.
        /// </summary>
        /// <param name="invoice">The tax invoice being corrected.</param>
        /// <param name="reason">Why the credit is raised (required at save).</param>
        /// <param name="issueDate">Credit-note issue date (defaults to today).</param>
        public static SavedCreditNote FromTaxInvoice(
            SavedTaxInvoice invoice, string reason, DateTime? issueDate = null)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            var cn = new SavedCreditNote
            {
                // Number + sequence are assigned by the repository on insert.
                CreditNoteNumber = "",
                TaxInvoiceReference = invoice.InvoiceNumber,
                ProformaId = invoice.ProformaId,
                JobOrderId = invoice.JobOrderId,
                PurchaseOrderNumber = invoice.PurchaseOrderNumber,
                Reason = reason ?? "",
                Status = "Draft",

                CustomerName = invoice.CustomerName,
                ProjectName = invoice.ProjectName,
                CompanyName = invoice.CompanyName,
                CompanyTrn = invoice.CompanyTrn,

                IssueDate = issueDate ?? DateTime.Today,

                // Positive magnitudes, copied 1:1 from the invoice.
                Subtotal = invoice.Subtotal,
                DiscountAmount = invoice.DiscountAmount,
                AfterDiscount = invoice.AfterDiscount,
                VatAmount = invoice.VatAmount,
                GrandTotal = invoice.GrandTotal,
                TotalQty = invoice.TotalQty,
                TotalSqm = invoice.TotalSqm,

                Items = new List<CreditNoteItem>(),
            };

            if (invoice.Items != null)
            {
                foreach (var it in invoice.Items)
                {
                    cn.Items.Add(new CreditNoteItem
                    {
                        // CreditNoteNumber is stamped by the repository once the
                        // number is known (mirrors the item FK on insert).
                        TaxInvoiceReference = invoice.InvoiceNumber,
                        ProformaId = it.ProformaId,
                        SpecGroupNumber = it.SpecGroupNumber,
                        SpecGroupName = it.SpecGroupName,
                        GlassType = it.GlassType,
                        SerialNumber = it.SerialNumber,
                        GlassRef = it.GlassRef,
                        W1 = it.W1,
                        H1 = it.H1,
                        W2 = it.W2,
                        H2 = it.H2,
                        Qty = it.Qty,
                        Sqm = it.Sqm,
                        TotalSqm = it.TotalSqm,
                        Rate = it.Rate,
                        Value = it.Value,
                        Notes = it.Notes,
                    });
                }
            }

            return cn;
        }
    }
}

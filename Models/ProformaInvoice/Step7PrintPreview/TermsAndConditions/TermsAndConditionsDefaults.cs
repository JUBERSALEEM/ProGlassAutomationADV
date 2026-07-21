using System.Collections.Generic;

namespace ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions
{
    /// <summary>
    /// Factory for the default UAE glass industry Terms & Conditions clauses.
    /// User can edit, remove, or add custom clauses per invoice.
    /// </summary>
    public static class TermsAndConditionsDefaults
    {
        public static List<TermClause> BuildDefaults()
        {
            return new List<TermClause>
            {
                new TermClause
                {
                    Type = TermClauseType.PaymentTerms,
                    Order = 1,
                    Title = "Payment Terms",
                    Body = "Payment is required as per the following schedule:",
                    SubLines = new System.Collections.ObjectModel.ObservableCollection<string>
                    {
                        "50% advance upon PO confirmation",
                        "50% balance before dispatch from factory",
                        "All payments in AED via bank transfer or cheque"
                    }
                },
                new TermClause
                {
                    Type = TermClauseType.Delivery,
                    Order = 2,
                    Title = "Delivery",
                    Body = "Delivery within 3-4 weeks from PO confirmation and receipt of advance payment. Delivery timeline subject to material availability and factory workload."
                },
                new TermClause
                {
                    Type = TermClauseType.Warranty,
                    Order = 3,
                    Title = "Warranty",
                    Body = "12 months against manufacturing defects only. Glass breakage, mishandling, or damage after installation is not covered under warranty."
                },
                new TermClause
                {
                    Type = TermClauseType.Validity,
                    Order = 4,
                    Title = "Validity",
                    ValidityDays = 7,
                    Body = "This quotation is valid for 7 days from issue date."
                },
                new TermClause
                {
                    Type = TermClauseType.Taxes,
                    Order = 5,
                    Title = "Taxes",
                    Body = "Prices are inclusive of 5% UAE VAT unless otherwise specified. TRN registration details as per invoice header."
                },
                new TermClause
                {
                    Type = TermClauseType.Installation,
                    Order = 6,
                    Title = "Installation",
                    Body = "Installation is NOT included in this quotation unless specified separately. Site work, scaffolding, and permits are the client's responsibility."
                }
            };
        }
    }
}
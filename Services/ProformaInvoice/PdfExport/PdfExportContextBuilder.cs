using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.PdfExport
{
    /// <summary>
    /// Builds a PdfExportContext from live wizard ViewModels.
    /// Separates WPF concerns from PDF generation.
    /// </summary>
    public static class PdfExportContextBuilder
    {
        public static PdfExportContext Build(
            CompanySetupViewModel company,
            ProjectSpecsViewModel project,
            ClientProfileViewModel client,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            var ctx = new PdfExportContext
            {
                // Step 1
                CompanyName = company.CompanyName ?? "",
                CompanyTrn = company.CompanyTrn ?? "",
                CompanyLocation = company.CompanyLocation ?? "",
                ProformaId = company.ProformaId ?? "",
                IssueDate = company.IssueDate.ToString("yyyy-MM-dd"),
                ValidUntil = company.ValidUntil.ToString("yyyy-MM-dd"),
                ProformaStatus = company.ProformaStatus ?? "Draft",

                // Step 2
                ProjectName = project.ProjectIdentifierName ?? "",
                ProjectContractNo = project.ProjectContractNo ?? "",
                SiteDelivery = project.SiteDeliveryLocation ?? "",
                LpoNumber = project.LpoNumber ?? "",

                // Step 3
                CustomerName = client.CustomerName ?? "",
                CustomerTrn = client.CustomerTrn ?? "",
                AttentionName = client.AttentionExecutiveName ?? "",
                ContactPhone = client.ClientContactPhone ?? "",
                BillingAddress = client.BillingAddress ?? "",
                CustomerReference = client.CustomerReferenceCode ?? "",
                SalesRepresentative = client.SalesRepresentative ?? "",

                // Financial Summary
                GlassMaterialsTotal = summary.GlassMaterialsSubtotal,
                OtherChargesTotal = summary.OtherChargesSubtotal,
                NetSubtotal = summary.NetSubtotal,
                DiscountAmount = summary.DiscountAmount,
                IsVatApplicable = summary.IsVatApplicable,
                VatPercent = summary.VatPercent,
                VatAmount = summary.VatAmount,
                GrandTotal = summary.GrandTotalVatInclusive,

                // Footer
                FooterDisclaimer = printable.FooterDisclaimer ?? "",
                TermsText = printable.TermsText ?? "",

                // Prepared By
                PreparedByName = printable.PreparedByName ?? "",
                PreparedByDept = printable.PreparedByDept ?? "",
                PreparedByStatus = printable.PreparedByStatus ?? ""
            };

            // Step 5: Spec Groups
            foreach (var group in dataGrid.SpecGroups)
            {
                var pdfGroup = new PdfSpecGroup
                {
                    GroupNumber = group.GroupNumber,
                    GroupName = group.GroupName ?? "",
                    TotalQty = group.FooterTotalQty,
                    TotalSqm = group.FooterTotalSqm,
                    TotalValue = group.FooterTotalValue
                };

                foreach (var row in group.ItemRows)
                {
                    pdfGroup.Rows.Add(new PdfGlassRow
                    {
                        Serial = row.SerialNumber,
                        GlassRef = row.GlassRef ?? "",
                        SizeW1H1 = $"{row.W1}×{row.H1}",
                        SizeW2H2 = $"{row.W2}×{row.H2}",
                        Qty = row.Qty,
                        Area = row.TotalSqm,
                        Rate = row.Rate,
                        Total = row.Value
                    });
                }

                ctx.SpecGroups.Add(pdfGroup);
            }

            // Step 6: Other Charges (only active)
            foreach (var charge in otherCharges.ChargeRows.Where(c => c.IsActive))
            {
                ctx.OtherCharges.Add(new PdfChargeLine
                {
                    Name = charge.ChargeName ?? "",
                    QtyLabel = $"Qty: {charge.MetricQty:0.##} {charge.Unit}",
                    RateLabel = $"Rate: AED {charge.Rate:N2}",
                    Amount = charge.ResultAmount
                });
            }

            // Wastages
            foreach (var w in otherCharges.WastagesSection.WastageRows.Where(w => w.IsActive))
            {
                ctx.OtherCharges.Add(new PdfChargeLine
                {
                    Name = $"Wastage — {w.ColorName}",
                    QtyLabel = $"Qty: {w.TotalSqm:0.###} sqm",
                    RateLabel = $"Rate: AED {w.Rate:N2}",
                    Amount = w.ResultAmount
                });
            }

            // Custom Charges
            foreach (var c in otherCharges.CustomChargesSection.AddedCharges.Where(c => c.IsActive))
            {
                ctx.OtherCharges.Add(new PdfChargeLine
                {
                    Name = c.ChargeName ?? "",
                    QtyLabel = $"Qty: {c.Qty:0.##} {c.Unit}",
                    RateLabel = $"Rate: AED {c.Rate:N2}",
                    Amount = c.ResultAmount
                });
            }

            // Terms & Conditions (active only, ordered)
            foreach (var clause in terms.GetActivePrintableClauses())
            {
                var pdfClause = new PdfTermClause
                {
                    Order = clause.Order,
                    Title = clause.Title ?? "",
                    Body = clause.Body ?? ""
                };

                if (clause.HasSubLines && clause.SubLines != null)
                {
                    foreach (var line in clause.SubLines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            pdfClause.SubLines.Add(line);
                    }
                }

                ctx.TermClauses.Add(pdfClause);
            }

            return ctx;
        }
    }
}
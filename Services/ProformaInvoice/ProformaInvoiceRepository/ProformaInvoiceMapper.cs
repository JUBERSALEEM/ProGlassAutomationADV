using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using System.Linq;
using System.Text.Json;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
    /// <summary>
    /// Converts between live wizard ViewModels and the JSON-serializable
    /// ProformaInvoiceSnapshot / SavedProformaInvoice entity.
    ///
    /// Phase 7.15 focuses on the SAVE direction (VMs → Snapshot → Entity).
    /// The RESTORE direction (Snapshot → VMs) will be extended in Phase 7.17
    /// when we add the Load Saved Invoice UI.
    /// </summary>
    public static class ProformaInvoiceMapper
    {
        // ══════════════════════════════════════════════════════════════════
        //  SAVE PATH: VMs → Snapshot → Entity
        // ══════════════════════════════════════════════════════════════════

        public static SavedProformaInvoice BuildEntityFromWizard(
            CompanySetupViewModel company,
            ProjectSpecsViewModel project,
            ClientProfileViewModel client,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            var snapshot = BuildSnapshot(company, project, client, dataGrid, otherCharges, summary, terms, printable);

            string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            var entity = new SavedProformaInvoice
            {
                ProformaId = company.ProformaId ?? "",
                CompanyName = company.CompanyName ?? "",
                CustomerName = client.CustomerName ?? "",
                ProjectName = project.ProjectIdentifierName ?? "",
                Status = company.ProformaStatus ?? "Draft",
                IssueDate = company.IssueDate,
                ValidUntil = company.ValidUntil,

                GlassMaterialsSubtotal = summary.GlassMaterialsSubtotal,
                OtherChargesSubtotal = summary.OtherChargesSubtotal,
                DiscountAmount = summary.DiscountAmount,
                VatAmount = summary.VatAmount,
                GrandTotalVatInclusive = summary.GrandTotalVatInclusive,

                FullSnapshotJson = json
            };

            return entity;
        }

        private static ProformaInvoiceSnapshot BuildSnapshot(
            CompanySetupViewModel company,
            ProjectSpecsViewModel project,
            ClientProfileViewModel client,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            var snap = new ProformaInvoiceSnapshot
            {
                // Step 1
                Company = new CompanySnapshot
                {
                    CompanyName = company.CompanyName ?? "",
                    CompanyTrn = company.CompanyTrn ?? "",
                    CompanyLocation = company.CompanyLocation ?? "",
                    ProformaId = company.ProformaId ?? "",
                    IssueDate = company.IssueDate,
                    ValidUntil = company.ValidUntil,
                    ProformaStatus = company.ProformaStatus ?? "Draft"
                },

                // Step 2
                Project = new ProjectSnapshot
                {
                    ProjectIdentifierName = project.ProjectIdentifierName ?? "",
                    ProjectContractNo = project.ProjectContractNo ?? "",
                    SiteDeliveryLocation = project.SiteDeliveryLocation ?? "",
                    LpoNumber = project.LpoNumber ?? ""
                },

                // Step 3
                Client = new ClientSnapshot
                {
                    CustomerName = client.CustomerName ?? "",
                    CustomerTrn = client.CustomerTrn ?? "",
                    CustomerReferenceCode = client.CustomerReferenceCode ?? "",
                    SalesRepresentative = client.SalesRepresentative ?? "",
                    DocumentPreparedBy = client.DocumentPreparedBy ?? "",
                    BillingAddress = client.BillingAddress ?? "",
                    AttentionExecutiveName = client.AttentionExecutiveName ?? "",
                    ClientContactPhone = client.ClientContactPhone ?? ""
                },

                // Phase 7.12
                Summary = new InvoiceSummarySnapshot
                {
                    DiscountType = summary.SelectedDiscountType.ToString(),
                    DiscountValue = summary.DiscountValue,
                    IsVatApplicable = summary.IsVatApplicable,
                    VatPercent = summary.VatPercent
                },

                // Phase 7.13
                PreparedBy = new PreparedBySnapshot
                {
                    PreparedByName = printable.PreparedByName ?? "",
                    PreparedByDept = printable.PreparedByDept ?? "",
                    PreparedByStatus = printable.PreparedByStatus ?? "",
                    TermsText = printable.TermsText ?? "",
                    FooterDisclaimer = printable.FooterDisclaimer ?? ""
                }
            };

            // Step 5 — spec groups + rows
            // Note: OuterGlassColor / InnerGlassColor are not yet on SpecGroup
            // — they will be added in a future phase when color-based reporting is needed.
            foreach (var group in dataGrid.SpecGroups)
            {
                var g = new SpecGroupSnapshot
                {
                    GroupNumber = group.GroupNumber,
                    GroupName = group.GroupName ?? "",
                    SelectedGlassType = group.SelectedGlassType ?? "",
                    SelectedSpecName = group.SelectedSpecName ?? "",
                    OuterGlassColor = "",  // reserved for future use
                    InnerGlassColor = "",  // reserved for future use
                    BaseRate = group.BaseRate,
                    SurchargePercent = group.SurchargePercent
                };

                foreach (var row in group.ItemRows)
                {
                    g.ItemRows.Add(new GlassItemRowSnapshot
                    {
                        SerialNumber = row.SerialNumber,
                        GlassRef = row.GlassRef ?? "",
                        W1 = row.W1,
                        H1 = row.H1,
                        W2 = row.W2,
                        H2 = row.H2,
                        Qty = row.Qty,
                        Rate = row.Rate,
                        PerimeterFactor = row.PerimeterFactor
                    });
                }

                snap.SpecGroups.Add(g);
            }

            // Step 6 — predefined charges
            foreach (var charge in otherCharges.ChargeRows)
            {
                snap.OtherCharges.PredefinedCharges.Add(new PredefinedChargeSnapshot
                {
                    TemplateKey = charge.TemplateKey ?? "",
                    IsActive = charge.IsActive,
                    SelectedSourceKey = charge.SelectedSource?.Key ?? "",
                    Rate = charge.Rate,
                    PolishSubType = charge.SelectedPolishSubType?.Type.ToString() ?? "",
                    CustomPolishDescription = charge.CustomPolishDescription ?? "",
                    CustomPolishQty = charge.CustomPolishQty
                });
            }

            // Step 6 — wastages
            foreach (var wastage in otherCharges.WastagesSection.WastageRows)
            {
                var w = new WastageRowSnapshot
                {
                    IsActive = wastage.IsActive,
                    ColorName = wastage.ColorName ?? "",
                    Rate = wastage.Rate
                };
                foreach (var src in wastage.AvailableSources.Where(s => s.IsChecked))
                    w.CheckedSourceKeys.Add($"{src.SpecNumber}-{src.SideLabel}");

                snap.OtherCharges.Wastages.Add(w);
            }

            // Step 6 — custom charges
            foreach (var custom in otherCharges.CustomChargesSection.AddedCharges)
            {
                snap.OtherCharges.CustomCharges.Add(new CustomChargeSnapshot
                {
                    IsActive = custom.IsActive,
                    ChargeName = custom.ChargeName ?? "",
                    Qty = custom.Qty,
                    Unit = custom.Unit.ToString(),
                    Rate = custom.Rate
                });
            }

            // Phase 7.14 — Terms & Conditions clauses
            foreach (var clause in terms.Clauses)
            {
                var c = new TermClauseSnapshot
                {
                    Type = clause.Type.ToString(),
                    Order = clause.Order,
                    IsActive = clause.IsActive,
                    Title = clause.Title ?? "",
                    Body = clause.Body ?? "",
                    ValidityDays = clause.ValidityDays
                };
                foreach (var line in clause.SubLines)
                    c.SubLines.Add(line);

                snap.TermClauses.Add(c);
            }

            return snap;
        }

        // ══════════════════════════════════════════════════════════════════
        //  RESTORE PATH: Snapshot → VMs (Phase 7.17 will fully implement)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deserializes the JSON snapshot from a saved entity. Phase 7.17
        /// will consume this to rehydrate wizard ViewModels on Load.
        /// </summary>
        public static ProformaInvoiceSnapshot DeserializeSnapshot(SavedProformaInvoice entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.FullSnapshotJson))
                return new ProformaInvoiceSnapshot();

            try
            {
                return JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(entity.FullSnapshotJson)
                       ?? new ProformaInvoiceSnapshot();
            }
            catch
            {
                return new ProformaInvoiceSnapshot();
            }
        }
    }
}
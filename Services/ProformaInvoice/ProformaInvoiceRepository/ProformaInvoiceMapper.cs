// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.cs
// ═══════════════════════════════════════════════════════════════════════
// CHAT 15 — PROJECT REFERENCE FIELD ADDED
//   3 precise changes:
//     1. BuildSnapshot: ProjectSnapshot gains ProjectReference
//     2. RestoreWizardFromSnapshot: project.ProjectReference restored
//     3. SafeAutoSaveMasterDataAsync: projectReference passed to auto-save
//   All BUG-15/16/17/18/20 + PHASE 14 + step swap fixes preserved.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.CustomCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ClientProfile;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
    /// <summary>
    /// Converts between live wizard ViewModels and the JSON-serializable
    /// ProformaInvoiceSnapshot / SavedProformaInvoice entity.
    ///
    /// BUG-15 [CRITICAL]: Status reflects PreparedByStatus override.
    /// BUG-16: Wastages restore — no UI command called from mapper.
    /// BUG-17: Spec name mismatch check removed.
    /// BUG-18: DeserializeSnapshot catches only JsonException.
    /// BUG-20: Shared static JsonSerializerOptions.
    /// PHASE 14: Auto-save hook — fire-and-forget to masterdata.db.
    /// CHAT 15:  Step swap (Step2=Client, Step3=Project).
    ///           ProjectReference field added.
    /// </summary>
    public static class ProformaInvoiceMapper
    {
        // BUG-20: shared static — allocated once, reused everywhere
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        // ══════════════════════════════════════════════════════════════
        //  SAVE PATH: VMs → Snapshot → Entity
        // ══════════════════════════════════════════════════════════════

        public static SavedProformaInvoice BuildEntityFromWizard(
            CompanySetupViewModel company,
            ClientProfileViewModel client,
            ProjectSpecsViewModel project,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            var snapshot = BuildSnapshot(
                company, client, project,
                dataGrid, otherCharges,
                summary, terms, printable);

            string json = JsonSerializer.Serialize(snapshot, _jsonOptions);

            // BUG-15: Effective status — printable override takes precedence
            string effectiveStatus =
                !string.IsNullOrWhiteSpace(printable?.PreparedByStatus)
                    ? printable.PreparedByStatus
                    : (company.ProformaStatus ?? "Draft");

            var entity = new SavedProformaInvoice
            {
                ProformaId = company.ProformaId ?? "",
                CompanyName = company.CompanyName ?? "",
                CustomerName = client.CustomerName ?? "",
                ProjectName = project.ProjectIdentifierName ?? "",
                Status = effectiveStatus,
                IssueDate = company.IssueDate,
                ValidUntil = company.ValidUntil,

                GlassMaterialsSubtotal = summary.GlassMaterialsSubtotal,
                OtherChargesSubtotal = summary.OtherChargesSubtotal,
                DiscountAmount = summary.DiscountAmount,
                VatAmount = summary.VatAmount,
                GrandTotalVatInclusive = summary.GrandTotalVatInclusive,

                FullSnapshotJson = json
            };

            // PHASE 14 — Master Data auto-save (fire-and-forget)
            if (App.MasterData != null)
            {
                _ = SafeAutoSaveMasterDataAsync(company, client, project);
            }

            return entity;
        }

        /// <summary>
        /// Phase 14: Fire-and-forget master data auto-save.
        /// CHAT 15: projectReference now passed to AutoSaveFromInvoiceAsync.
        /// All exceptions swallowed — must never block invoice save.
        /// </summary>
        private static async Task SafeAutoSaveMasterDataAsync(
            CompanySetupViewModel company,
            ClientProfileViewModel client,
            ProjectSpecsViewModel project)
        {
            try
            {
                await App.MasterData.AutoSaveFromInvoiceAsync(
                    companyName: company.CompanyName ?? "",
                    companyTrn: company.CompanyTrn ?? "",
                    companyLocation: company.CompanyLocation ?? "",

                    customerName: client.CustomerName ?? "",
                    customerTrn: client.CustomerTrn ?? "",
                    customerReferenceCode: client.CustomerReferenceCode ?? "",
                    salesRepresentative: client.SalesRepresentative ?? "",
                    documentPreparedBy: client.DocumentPreparedBy ?? "",
                    billingAddress: client.BillingAddress ?? "",
                    attentionExecutiveName: client.AttentionExecutiveName ?? "",
                    clientContactPhone: client.ClientContactPhone ?? "",

                    projectIdentifierName: project.ProjectIdentifierName ?? "",
                    projectContractNo: project.ProjectContractNo ?? "",
                    projectReference: project.ProjectReference ?? "",   // CHAT 15: NEW
                    siteDeliveryLocation: project.SiteDeliveryLocation ?? "",
                    lpoNumber: project.LpoNumber ?? "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"[ProformaInvoiceMapper] Master data auto-save failed: {ex.Message}");
            }
        }

        private static ProformaInvoiceSnapshot BuildSnapshot(
            CompanySetupViewModel company,
            ClientProfileViewModel client,
            ProjectSpecsViewModel project,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            var snap = new ProformaInvoiceSnapshot
            {
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

                Project = new ProjectSnapshot
                {
                    ProjectIdentifierName = project.ProjectIdentifierName ?? "",
                    ProjectContractNo = project.ProjectContractNo ?? "",
                    ProjectReference = project.ProjectReference ?? "",  // CHAT 15: NEW
                    SiteDeliveryLocation = project.SiteDeliveryLocation ?? "",
                    LpoNumber = project.LpoNumber ?? ""
                },

                Summary = new InvoiceSummarySnapshot
                {
                    DiscountType = summary.SelectedDiscountType.ToString(),
                    DiscountValue = summary.DiscountValue,
                    IsVatApplicable = summary.IsVatApplicable,
                    VatPercent = summary.VatPercent
                },

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
            foreach (var group in dataGrid.SpecGroups)
            {
                var g = new SpecGroupSnapshot
                {
                    GroupNumber = group.GroupNumber,
                    GroupName = group.GroupName ?? "",
                    SelectedGlassType = group.SelectedGlassType ?? "",
                    SelectedSpecName = group.SelectedSpecName ?? "",
                    OuterGlassColor = "",
                    InnerGlassColor = "",
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

            // Phase 7.14 — Terms & Conditions
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

        // ══════════════════════════════════════════════════════════════
        //  RESTORE PATH: Entity → Snapshot → VMs
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// BUG-18: catches only JsonException.
        /// </summary>
        public static ProformaInvoiceSnapshot DeserializeSnapshot(
            SavedProformaInvoice entity)
        {
            if (entity == null
                || string.IsNullOrWhiteSpace(entity.FullSnapshotJson))
                return new ProformaInvoiceSnapshot();

            try
            {
                return JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                    entity.FullSnapshotJson, _jsonOptions)
                    ?? new ProformaInvoiceSnapshot();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine(
                    $"[ProformaInvoiceMapper] Snapshot deserialize failed " +
                    $"for PI '{entity.ProformaId}': {ex.Message}");
                return new ProformaInvoiceSnapshot();
            }
        }

        public static void RestoreWizardFromSnapshot(
            ProformaInvoiceSnapshot snap,
            CompanySetupViewModel company,
            ClientProfileViewModel client,
            ProjectSpecsViewModel project,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            if (snap == null) return;

            // ── Step 1: Company ────────────────────────────────────────
            if (snap.Company != null)
            {
                company.CompanyName = snap.Company.CompanyName ?? "";
                company.CompanyTrn = snap.Company.CompanyTrn ?? "";
                company.CompanyLocation = snap.Company.CompanyLocation ?? "";
                company.ProformaId = snap.Company.ProformaId ?? "";
                company.IssueDate = snap.Company.IssueDate;
                company.ValidUntil = snap.Company.ValidUntil;
                company.ProformaStatus = snap.Company.ProformaStatus ?? "Draft";
            }

            // ── Step 2: Client ─────────────────────────────────────────
            if (snap.Client != null)
            {
                client.CustomerName = snap.Client.CustomerName ?? "";
                client.CustomerTrn = snap.Client.CustomerTrn ?? "";
                client.CustomerReferenceCode = snap.Client.CustomerReferenceCode ?? "";
                client.SalesRepresentative = snap.Client.SalesRepresentative ?? "";
                client.DocumentPreparedBy = snap.Client.DocumentPreparedBy ?? "";
                client.BillingAddress = snap.Client.BillingAddress ?? "";
                client.AttentionExecutiveName = snap.Client.AttentionExecutiveName ?? "";
                client.ClientContactPhone = snap.Client.ClientContactPhone ?? "";
            }

            // ── Step 3: Project ────────────────────────────────────────
            if (snap.Project != null)
            {
                project.ProjectIdentifierName = snap.Project.ProjectIdentifierName ?? "";
                project.ProjectContractNo = snap.Project.ProjectContractNo ?? "";
                project.ProjectReference = snap.Project.ProjectReference ?? "";  // CHAT 15: NEW
                project.SiteDeliveryLocation = snap.Project.SiteDeliveryLocation ?? "";
                project.LpoNumber = snap.Project.LpoNumber ?? "";
            }

            // ── Step 5: DataGrid ───────────────────────────────────────
            RestoreDataGrid(snap, dataGrid);

            // ── Step 6: Other Charges ──────────────────────────────────
            RestoreOtherCharges(snap, otherCharges);

            // ── Financial Summary ──────────────────────────────────────
            if (snap.Summary != null)
            {
                if (System.Enum.TryParse<
                    ProGlassAutomation.Models.ProformaInvoice
                        .Step7PrintPreview.InvoiceMainSummary.DiscountType>(
                        snap.Summary.DiscountType, out var dt))
                {
                    summary.SelectedDiscountType = dt;
                }
                summary.DiscountValue = snap.Summary.DiscountValue;
                summary.IsVatApplicable = snap.Summary.IsVatApplicable;
                summary.VatPercent = snap.Summary.VatPercent;
            }

            // ── Terms & Conditions ─────────────────────────────────────
            RestoreTerms(snap, terms);

            // ── Prepared By ────────────────────────────────────────────
            if (snap.PreparedBy != null)
            {
                printable.PreparedByName = snap.PreparedBy.PreparedByName ?? "";
                printable.PreparedByDept = snap.PreparedBy.PreparedByDept ?? "";
                printable.PreparedByStatus = snap.PreparedBy.PreparedByStatus ?? "";
                printable.TermsText = snap.PreparedBy.TermsText ?? "";
                printable.FooterDisclaimer = snap.PreparedBy.FooterDisclaimer ?? "";
            }
        }

        // ── Restore helpers ────────────────────────────────────────────

        private static void RestoreDataGrid(
            ProformaInvoiceSnapshot snap, DataGridViewModel dataGrid)
        {
            if (snap.SpecGroups == null) return;

            dataGrid.SpecGroups.Clear();

            foreach (var gSnap in snap.SpecGroups)
            {
                var group = new SpecGroup
                {
                    GroupNumber = gSnap.GroupNumber,
                    GroupName = gSnap.GroupName ?? "",
                    SelectedGlassType = gSnap.SelectedGlassType ?? "",
                    SelectedSpecName = gSnap.SelectedSpecName ?? "",
                    BaseRate = gSnap.BaseRate,
                    SurchargePercent = gSnap.SurchargePercent
                };

                if (gSnap.ItemRows != null && gSnap.ItemRows.Count > 0)
                {
                    foreach (var rSnap in gSnap.ItemRows)
                    {
                        var row = new GlassItemRow();
                        row.SetAllSilent(
                            rSnap.GlassRef ?? "",
                            rSnap.W1, rSnap.H1, rSnap.W2, rSnap.H2,
                            rSnap.Qty,
                            rSnap.PerimeterFactor,
                            rSnap.Rate);
                        row.SerialNumber = rSnap.SerialNumber;
                        group.ItemRows.Add(row);
                    }
                }

                dataGrid.SpecGroups.Add(group);
            }
        }

        private static void RestoreOtherCharges(
            ProformaInvoiceSnapshot snap, OtherChargesViewModel otherCharges)
        {
            if (snap.OtherCharges == null) return;

            if (snap.OtherCharges.PredefinedCharges != null)
            {
                foreach (var cSnap in snap.OtherCharges.PredefinedCharges)
                {
                    var row = otherCharges.ChargeRows.FirstOrDefault(
                        r => r.TemplateKey == cSnap.TemplateKey);
                    if (row == null) continue;

                    row.IsActive = cSnap.IsActive;
                    row.Rate = cSnap.Rate;

                    if (!row.IsPolishRow
                        && !string.IsNullOrWhiteSpace(cSnap.SelectedSourceKey))
                    {
                        var src = row.AvailableSources.FirstOrDefault(
                            s => s.Key == cSnap.SelectedSourceKey);
                        if (src != null) row.SelectedSource = src;
                    }

                    if (row.IsPolishRow
                        && !string.IsNullOrWhiteSpace(cSnap.PolishSubType))
                    {
                        if (System.Enum.TryParse<PolishSubType>(
                            cSnap.PolishSubType, out var pst))
                        {
                            var meta = row.AvailablePolishSubTypes
                                .FirstOrDefault(p => p.Type == pst);
                            if (meta != null) row.SelectedPolishSubType = meta;
                        }
                        row.CustomPolishDescription = cSnap.CustomPolishDescription ?? "";
                        row.CustomPolishQty = cSnap.CustomPolishQty;
                    }
                }
            }

            // BUG-16: Wastages restore — no UI command, create directly
            otherCharges.WastagesSection.WastageRows.Clear();
            if (snap.OtherCharges.Wastages != null)
            {
                foreach (var wSnap in snap.OtherCharges.Wastages)
                {
                    var newRow = new WastageRow
                    {
                        IsActive = wSnap.IsActive,
                        ColorName = wSnap.ColorName ?? "",
                        Rate = wSnap.Rate
                    };

                    otherCharges.WastagesSection.PopulateSourcesForRestoredRow(newRow);

                    if (wSnap.CheckedSourceKeys != null)
                    {
                        foreach (var src in newRow.AvailableSources)
                        {
                            string key = $"{src.SpecNumber}-{src.SideLabel}";
                            src.IsChecked = wSnap.CheckedSourceKeys.Contains(key);
                        }
                    }

                    otherCharges.WastagesSection.WastageRows.Add(newRow);
                }
            }

            otherCharges.CustomChargesSection.AddedCharges.Clear();
            if (snap.OtherCharges.CustomCharges != null)
            {
                foreach (var cSnap in snap.OtherCharges.CustomCharges)
                {
                    var custom = new CustomChargeRow
                    {
                        IsActive = cSnap.IsActive,
                        ChargeName = cSnap.ChargeName ?? "",
                        Qty = cSnap.Qty,
                        Rate = cSnap.Rate
                    };
                    if (System.Enum.TryParse<OtherChargeUnit>(cSnap.Unit, out var unit))
                        custom.Unit = unit;

                    otherCharges.CustomChargesSection.AddedCharges.Add(custom);
                }
            }
        }

        private static void RestoreTerms(
            ProformaInvoiceSnapshot snap, TermsAndConditionsViewModel terms)
        {
            if (snap.TermClauses == null) return;

            terms.Clauses.Clear();
            foreach (var cSnap in snap.TermClauses)
            {
                var clause = new TermClause
                {
                    Order = cSnap.Order,
                    IsActive = cSnap.IsActive,
                    Title = cSnap.Title ?? "",
                    Body = cSnap.Body ?? "",
                    ValidityDays = cSnap.ValidityDays
                };
                if (System.Enum.TryParse<TermClauseType>(cSnap.Type, out var t))
                    clause.Type = t;

                if (cSnap.SubLines != null)
                    foreach (var line in cSnap.SubLines)
                        clause.SubLines.Add(line);

                terms.Clauses.Add(clause);
            }
        }
    }
}
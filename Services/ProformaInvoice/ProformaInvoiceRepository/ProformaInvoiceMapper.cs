using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.CustomCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile;
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
    /// Supports both SAVE (VMs → Snapshot → Entity) and RESTORE
    /// (Entity → Snapshot → VMs) paths.
    ///
    /// BUG-15 [CRITICAL]: Entity.Status now reflects PreparedByStatus
    ///         override when the user changed it in the printable preview.
    /// BUG-16: Wastages restore no longer calls UI command from mapper.
    /// BUG-17: Spec name mismatch check removed — FilteredSpecs is now
    ///         an ICollectionView populated asynchronously.
    /// BUG-18: DeserializeSnapshot catches only JsonException.
    /// BUG-20: Shared static JsonSerializerOptions.
    ///
    /// PHASE 14: Auto-save hook — BuildEntityFromWizard now fires
    ///           SafeAutoSaveMasterDataAsync to upsert company/client/
    ///           project/salesman into masterdata.db. Fire-and-forget —
    ///           never blocks invoice save.
    /// </summary>
    public static class ProformaInvoiceMapper
    {
        // BUG-20: shared static — allocated once, reused everywhere
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

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
            var snapshot = BuildSnapshot(
                company, project, client,
                dataGrid, otherCharges,
                summary, terms, printable);

            string json = JsonSerializer.Serialize(snapshot, _jsonOptions);

            // BUG-15 [CRITICAL]: Status column now reflects the
            // effective status — printable override takes precedence
            // over the company setup status. This ensures the list
            // view shows the same status that appears on the PDF.
            string effectiveStatus = !string.IsNullOrWhiteSpace(printable?.PreparedByStatus)
                ? printable.PreparedByStatus
                : (company.ProformaStatus ?? "Draft");

            var entity = new SavedProformaInvoice
            {
                ProformaId = company.ProformaId ?? "",
                CompanyName = company.CompanyName ?? "",
                CustomerName = client.CustomerName ?? "",
                ProjectName = project.ProjectIdentifierName ?? "",
                Status = effectiveStatus,   // ← BUG-15 fix
                IssueDate = company.IssueDate,
                ValidUntil = company.ValidUntil,

                GlassMaterialsSubtotal = summary.GlassMaterialsSubtotal,
                OtherChargesSubtotal = summary.OtherChargesSubtotal,
                DiscountAmount = summary.DiscountAmount,
                VatAmount = summary.VatAmount,
                GrandTotalVatInclusive = summary.GrandTotalVatInclusive,

                FullSnapshotJson = json
            };

            // ══════════════════════════════════════════════════════════════
            //  PHASE 14 — Master Data auto-save
            //
            //  Extracts company, client, project, and salesman from the
            //  wizard VMs and upserts them into masterdata.db.
            //
            //  Fire-and-forget — failures are logged but never block
            //  invoice save. Master data is a CONVENIENCE feature
            //  (card auto-fill); invoice save is the CRITICAL path.
            // ══════════════════════════════════════════════════════════════
            if (App.MasterData != null)
            {
                _ = SafeAutoSaveMasterDataAsync(company, project, client);
            }

            return entity;
        }

        /// <summary>
        /// Phase 14 — Fire-and-forget master data auto-save.
        /// Private helper so BuildEntityFromWizard stays clean.
        /// All exceptions swallowed — master data must never block invoice save.
        /// </summary>
        private static async Task SafeAutoSaveMasterDataAsync(
            CompanySetupViewModel company,
            ProjectSpecsViewModel project,
            ClientProfileViewModel client)
        {
            try
            {
                await App.MasterData.AutoSaveFromInvoiceAsync(
                    // Company fields
                    companyName: company.CompanyName ?? "",
                    companyTrn: company.CompanyTrn ?? "",
                    companyLocation: company.CompanyLocation ?? "",

                    // Client fields
                    customerName: client.CustomerName ?? "",
                    customerTrn: client.CustomerTrn ?? "",
                    customerReferenceCode: client.CustomerReferenceCode ?? "",
                    salesRepresentative: client.SalesRepresentative ?? "",
                    documentPreparedBy: client.DocumentPreparedBy ?? "",
                    billingAddress: client.BillingAddress ?? "",
                    attentionExecutiveName: client.AttentionExecutiveName ?? "",
                    clientContactPhone: client.ClientContactPhone ?? "",

                    // Project fields
                    projectIdentifierName: project.ProjectIdentifierName ?? "",
                    projectContractNo: project.ProjectContractNo ?? "",
                    siteDeliveryLocation: project.SiteDeliveryLocation ?? "",
                    lpoNumber: project.LpoNumber ?? "");
            }
            catch (Exception ex)
            {
                // Master data auto-save must NEVER block invoice save.
                // Log and move on. User sees fewer auto-fill cards next
                // time but their invoice is safe.
                Debug.WriteLine(
                    $"[ProformaInvoiceMapper] Master data auto-save failed: {ex.Message}");
            }
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

                Project = new ProjectSnapshot
                {
                    ProjectIdentifierName = project.ProjectIdentifierName ?? "",
                    ProjectContractNo = project.ProjectContractNo ?? "",
                    SiteDeliveryLocation = project.SiteDeliveryLocation ?? "",
                    LpoNumber = project.LpoNumber ?? ""
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

        // ══════════════════════════════════════════════════════════════════
        //  RESTORE PATH: Entity → Snapshot → VMs (Phase 7.16)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deserializes the JSON snapshot from a saved entity.
        /// BUG-18: catches only JsonException — other exceptions bubble
        /// up so real bugs can be caught. Malformed JSON returns empty
        /// snapshot with diagnostic log.
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
            ProjectSpecsViewModel project,
            ClientProfileViewModel client,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel summary,
            TermsAndConditionsViewModel terms,
            PrintableInvoiceViewModel printable)
        {
            if (snap == null) return;

            // ── Step 1: Company ─────────────────────────────────────────
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

            // ── Step 2: Project ─────────────────────────────────────────
            if (snap.Project != null)
            {
                project.ProjectIdentifierName = snap.Project.ProjectIdentifierName ?? "";
                project.ProjectContractNo = snap.Project.ProjectContractNo ?? "";
                project.SiteDeliveryLocation = snap.Project.SiteDeliveryLocation ?? "";
                project.LpoNumber = snap.Project.LpoNumber ?? "";
            }

            // ── Step 3: Client ──────────────────────────────────────────
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

            // ── Step 5: DataGrid spec groups ───────────────────────────
            RestoreDataGrid(snap, dataGrid);

            // ── Step 6: Other Charges ───────────────────────────────────
            RestoreOtherCharges(snap, otherCharges);

            // ── Phase 7.12: Financial Summary settings ─────────────────
            if (snap.Summary != null)
            {
                if (System.Enum.TryParse<
                    ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary.DiscountType>(
                        snap.Summary.DiscountType, out var dt))
                {
                    summary.SelectedDiscountType = dt;
                }
                summary.DiscountValue = snap.Summary.DiscountValue;
                summary.IsVatApplicable = snap.Summary.IsVatApplicable;
                summary.VatPercent = snap.Summary.VatPercent;
            }

            // ── Phase 7.14: Terms & Conditions ──────────────────────────
            RestoreTerms(snap, terms);

            // ── Phase 7.13: Prepared By ─────────────────────────────────
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

            // Predefined charges — match by TemplateKey
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

            // Custom charges — clear and rebuild
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
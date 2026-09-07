// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.InternalImport.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.InternalImport members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using ProGlassAutomation.Views.TaxInvoice.TaxInvoiceDetail;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    public partial class TaxInvoiceDetailViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════════
        //  INTERNAL IMPORT (shared logic — unchanged from Chat 14)
        // ══════════════════════════════════════════════════════════════════

        private async Task ImportInternalAsync(
            string proformaId,
            Dictionary<(int SpecGroup, int Serial), int>? deliveryNoteQtyMap,
            string dnReferenceLabel,
            string importLabel)
        {
            IsNewInvoice = true;
            _editingId = 0;
            _editingInvoiceNumber = "";

            ResetFields();
            ProformaId = proformaId;
            DeliveryNoteReference = dnReferenceLabel;

            if (_taxInvoices != null)
            {
                var summary = await _taxInvoices
                    .GetFamilySummaryAsync(proformaId);
                PreviouslyInvoicedTotal = summary.TotalInvoiced;
            }

            if (_invoices == null)
            {
                StatusMessage = "";
                return;
            }

            var piEntity = await _invoices.GetByProformaIdAsync(proformaId);

            if (piEntity == null)
            {
                StatusMessage = $"⚠️ PI '{proformaId}' not found.";
                return;
            }

            var piSnapshot = Services.ProformaInvoice
                .ProformaInvoiceRepository.ProformaInvoiceMapper
                .DeserializeSnapshot(piEntity);

            if (piSnapshot == null)
            {
                StatusMessage = "⚠️ PI snapshot could not be read.";
                return;
            }

            ProformaTotal = piEntity.GrandTotalVatInclusive;

            if (piSnapshot.Company != null)
            {
                CompanyName = piSnapshot.Company.CompanyName;
                CompanyTrn = piSnapshot.Company.CompanyTrn;
                CompanyLocation = piSnapshot.Company.CompanyLocation;
                CompanyEmail = piSnapshot.Company.CompanyEmail;
                if (string.IsNullOrWhiteSpace(CompanyAddress))
                    CompanyAddress = piSnapshot.Company.CompanyLocation;
                BankName = piSnapshot.Company.BankName;
                BankAccountNumber = piSnapshot.Company.AccountNumber;

                // v3 — branding: the Tax Invoice letterhead mirrors the PI.
                LogoPath = piSnapshot.Company.LogoPath ?? "";
                LogoLayout = piSnapshot.Company.LogoLayout;
                LogoWidthMm = piSnapshot.Company.LogoWidthMm > 0
                    ? piSnapshot.Company.LogoWidthMm : 22;
                LogoHeightMm = piSnapshot.Company.LogoHeightMm > 0
                    ? piSnapshot.Company.LogoHeightMm : 26;
            }

            if (piSnapshot.Client != null)
            {
                CustomerName = piSnapshot.Client.CustomerName;
                CustomerTrn = piSnapshot.Client.CustomerTrn;
                CustomerAddress = piSnapshot.Client.BillingAddress;
                CustomerPhone = piSnapshot.Client.ClientContactPhone;
                ContactPerson = piSnapshot.Client.AttentionExecutiveName;
            }

            // CHAT 20: All 5 PI project fields → 4 TI project properties, 1:1.
            // No data loss. No field collapsing. LPO maps to PurchaseOrderNumber
            // (LPO = PO in UAE glass industry — same concept, different terminology).
            if (piSnapshot.Project != null)
            {
                ProjectIdentifierName = piSnapshot.Project.ProjectIdentifierName;
                ProjectContractNo = piSnapshot.Project.ProjectContractNo;
                ProjectReference = piSnapshot.Project.ProjectReference;
                SiteDeliveryLocation = piSnapshot.Project.SiteDeliveryLocation;
                PurchaseOrderNumber = piSnapshot.Project.LpoNumber;
            }

            if (piSnapshot.PreparedBy != null)
            {
                PreparedByName = piSnapshot.PreparedBy.PreparedByName;
                PreparedByDepartment = piSnapshot.PreparedBy.PreparedByDept;
            }

            if (piSnapshot.Summary != null
                && string.Equals(piSnapshot.Summary.DiscountType,
                    "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                DiscountPercent = piSnapshot.Summary.DiscountValue;
            }

            SpecGroups.Clear();

            if (piSnapshot.SpecGroups != null)
            {
                foreach (var piGroup in piSnapshot.SpecGroups)
                {
                    if (piGroup == null || piGroup.ItemRows == null) continue;

                    var eligibleRows = new List<(GlassRowData row, int qty)>();

                    foreach (var piRow in piGroup.ItemRows)
                    {
                        if (piRow == null) continue;

                        int invoiceQty;
                        if (deliveryNoteQtyMap == null)
                        {
                            invoiceQty = piRow.Qty;
                        }
                        else
                        {
                            var key = (piGroup.GroupNumber, piRow.SerialNumber);
                            if (!deliveryNoteQtyMap.TryGetValue(key, out invoiceQty))
                                continue;
                            if (invoiceQty <= 0) continue;
                        }

                        eligibleRows.Add((
                            new GlassRowData
                            {
                                SerialNumber = piRow.SerialNumber,
                                GlassRef = piRow.GlassRef ?? "",
                                W1 = piRow.W1,
                                H1 = piRow.H1,
                                W2 = piRow.W2,
                                H2 = piRow.H2,
                                Rate = piRow.Rate > 0
                                    ? piRow.Rate : piGroup.BaseRate
                            },
                            invoiceQty));
                    }

                    if (eligibleRows.Count == 0) continue;

                    var invGroup = new InvSpecGroupViewModel(
                        piGroup.GroupNumber, proformaId)
                    {
                        GroupName = !string.IsNullOrWhiteSpace(piGroup.SelectedSpecName)
                            ? piGroup.SelectedSpecName
                            : (piGroup.GroupName ?? ""),
                        GlassType = piGroup.SelectedGlassType ?? "",
                        SpecName = piGroup.SelectedSpecName ?? "",
                        BaseRate = piGroup.BaseRate,
                        OnRowChanged = RecalculateFinancials
                    };

                    foreach (var (row, qty) in eligibleRows)
                    {
                        invGroup.Rows.Add(new InvRowViewModel(invGroup)
                        {
                            SerialNumber = row.SerialNumber,
                            GlassRef = row.GlassRef,
                            W1 = row.W1,
                            H1 = row.H1,
                            W2 = row.W2,
                            H2 = row.H2,
                            Qty = qty,
                            Rate = row.Rate,
                            Notes = ""
                        });
                    }

                    invGroup.RecalculateGroup();
                    SpecGroups.Add(invGroup);
                }
            }

            RecalculateFinancials();

            if (piSnapshot.Summary != null
                && string.Equals(piSnapshot.Summary.DiscountType,
                    "FixedAmount", StringComparison.OrdinalIgnoreCase)
                && Subtotal > 0m)
            {
                DiscountPercent = Math.Round(
                    (piSnapshot.Summary.DiscountValue / Subtotal) * 100m, 2);
                RecalculateFinancials();
            }

            int totalRows = SpecGroups.Sum(g => g.Rows.Count);
            int totalQty = SpecGroups.Sum(g => g.Rows.Sum(r => r.Qty));

            StatusMessage = totalRows > 0
                ? $"✅ Imported {importLabel}: {SpecGroups.Count} spec group(s), " +
                  $"{totalRows} row(s), {totalQty} pcs."
                : $"⚠️ Nothing to invoice — no delivered items found.";
        }

        private sealed class GlassRowData
        {
            public int SerialNumber { get; set; }
            public string GlassRef { get; set; } = "";
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public decimal Rate { get; set; }
        }

    }
}

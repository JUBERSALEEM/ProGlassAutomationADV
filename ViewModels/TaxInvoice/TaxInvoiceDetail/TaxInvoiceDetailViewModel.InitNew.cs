// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.InitNew.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.InitNew members.
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
        //  INIT NEW — 3 ENTRY POINTS (from Chat 14)
        // ══════════════════════════════════════════════════════════════════

        public async Task InitNewAsync(string proformaId)
        {
            await ImportInternalAsync(
                proformaId: proformaId,
                deliveryNoteQtyMap: null,
                dnReferenceLabel: "",
                importLabel: "full PI");
        }

        public async Task InitFromDeliveryNoteAsync(string deliveryNoteId)
        {
            if (_deliveryNotes == null
                || string.IsNullOrWhiteSpace(deliveryNoteId))
            {
                StatusMessage = "Delivery Note database not initialized.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading DN '{deliveryNoteId}'…";

                var dnEntity = await _deliveryNotes
                    .GetByDeliveryNoteIdAsync(deliveryNoteId);

                if (dnEntity == null)
                {
                    StatusMessage = $"⚠️ DN '{deliveryNoteId}' not found.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(dnEntity.ProformaId))
                {
                    StatusMessage =
                        $"⚠️ DN '{deliveryNoteId}' has no linked PI.";
                    return;
                }

                var qtyMap = dnEntity.Items
                    .Where(i => i.DeliveredQty > 0)
                    .ToDictionary(
                        i => (i.SpecGroupNumber, i.SerialNumber),
                        i => i.DeliveredQty);

                if (qtyMap.Count == 0)
                {
                    StatusMessage =
                        $"⚠️ DN '{deliveryNoteId}' has no delivered items.";
                    return;
                }

                await ImportInternalAsync(
                    proformaId: dnEntity.ProformaId,
                    deliveryNoteQtyMap: qtyMap,
                    dnReferenceLabel: deliveryNoteId,
                    importLabel: $"DN '{deliveryNoteId}'");

                JobOrderId = dnEntity.JobOrderId;

                // Chat 16: load family sidebar for new invoice too
                await LoadFamilyAsync(dnEntity.ProformaId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Init from DN failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedToImportDNData"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapImportFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task InitFromDeliveryNoteFamilyAsync(string proformaId)
        {
            if (_deliveryNotes == null
                || string.IsNullOrWhiteSpace(proformaId))
            {
                StatusMessage = "Delivery Note database not initialized.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading all deliveries for '{proformaId}'…";

                // Phase 2: scoped query, not a whole-table load. This loaded
                // every delivery note in the database to collect the sibling
                // IDs for one proforma. GetByProformaIdAsync applies the same
                // !IsMasterDn exclusion and the same case-insensitive
                // ProformaId match, in SQL.
                var siblingDns = await _deliveryNotes.GetByProformaIdAsync(proformaId);
                var siblingDnIds = siblingDns
                    .Select(d => d.DeliveryNoteId)
                    .ToList();

                if (siblingDnIds.Count == 0)
                {
                    StatusMessage =
                        $"⚠️ No delivery notes for '{proformaId}'.";
                    return;
                }

                var aggregatedQty = new Dictionary<(int, int), int>();
                var refList = new List<string>();

                foreach (var dnId in siblingDnIds)
                {
                    var dn = await _deliveryNotes
                        .GetByDeliveryNoteIdAsync(dnId);
                    if (dn == null) continue;

                    bool hasAnyDelivered = false;
                    foreach (var item in dn.Items.Where(i => i.DeliveredQty > 0))
                    {
                        var key = (item.SpecGroupNumber, item.SerialNumber);
                        if (aggregatedQty.ContainsKey(key))
                            aggregatedQty[key] += item.DeliveredQty;
                        else
                            aggregatedQty[key] = item.DeliveredQty;
                        hasAnyDelivered = true;
                    }

                    if (hasAnyDelivered) refList.Add(dnId);
                }

                if (aggregatedQty.Count == 0)
                {
                    StatusMessage =
                        $"⚠️ No delivered items across any DN.";
                    return;
                }

                string dnRefs = string.Join(",", refList);

                await ImportInternalAsync(
                    proformaId: proformaId,
                    deliveryNoteQtyMap: aggregatedQty,
                    dnReferenceLabel: dnRefs,
                    importLabel: $"Master ({refList.Count} DN{(refList.Count > 1 ? "s" : "")})");

                var firstJo = allDns.FirstOrDefault(d =>
                    string.Equals(d.ProformaId, proformaId,
                        StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(d.JobOrderId));
                if (firstJo != null)
                    JobOrderId = firstJo.JobOrderId;

                // Chat 16: load family sidebar
                await LoadFamilyAsync(proformaId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Master import failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapImportFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

    }
}

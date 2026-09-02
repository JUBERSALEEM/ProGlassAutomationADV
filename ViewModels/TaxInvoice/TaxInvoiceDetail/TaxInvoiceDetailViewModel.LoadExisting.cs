// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.LoadExisting.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.LoadExisting members.
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
        //  LOAD EXISTING (enhanced to load family)
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadAsync(int id)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "";

                if (_taxInvoices == null)
                {
                    StatusMessage = "Database not initialized.";
                    return;
                }

                var entity = await _taxInvoices.GetByIdAsync(id);
                if (entity == null)
                {
                    StatusMessage = $"Invoice #{id} not found.";
                    return;
                }

                var snapshot = TaxInvoiceMapper.DeserializeSnapshot(entity);
                if (snapshot == null)
                {
                    StatusMessage = "Invoice snapshot could not be read.";
                    return;
                }

                _editingId = id;
                _editingInvoiceNumber = entity.InvoiceNumber;
                IsNewInvoice = false;

                RestoreFromSnapshot(entity, snapshot);

                // Chat 16: load family sidebar
                await LoadFamilyAsync(entity.ProformaId);

                CaptureCleanState();   // v2 §28: freshly loaded = clean
                StatusMessage = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// v2 §24: shows the version history for the invoice being edited
        /// and applies a chosen restore into the editor (pending save).
        /// </summary>
        private async Task ShowVersionsAsync()
        {
            if (string.IsNullOrWhiteSpace(_editingInvoiceNumber))
            {
                StatusMessage = "Save the invoice first to build its history.";
                return;
            }

            var versions = await ProGlassAutomation.Services.Audit
                .VersionHistoryService
                .GetVersionsAsync("TI", _editingInvoiceNumber);

            var restoreId = Views.ProformaInvoice.SavedInvoicesList
                .VersionsDialog.Show(
                    System.Windows.Application.Current?.MainWindow,
                    _editingInvoiceNumber, versions);

            if (restoreId == null) return;

            bool ok = await RestoreVersionAsync(restoreId.Value);
            StatusMessage = ok
                ? "Version loaded — save to keep it."
                : "This version could not be applied.";
        }

    }
}

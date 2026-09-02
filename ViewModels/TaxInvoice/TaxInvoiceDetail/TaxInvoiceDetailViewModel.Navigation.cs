// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Navigation.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Navigation members.
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
        //  NAVIGATE TO SIBLING INVOICE (Chat 16 — family sidebar click)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Switch to a sibling invoice within the same family.
        /// PERFORMANCE: skips family reload (family didn't change).
        /// Only reloads invoice content + attachments.
        /// </summary>
        private async Task NavigateToFamilyMemberAsync(
            TaxInvoiceRowViewModel target)
        {
            if (_taxInvoices == null) return;
            if (_isNavigatingFamily) return;
            if (target.Id == _editingId) return;

            try
            {
                _isNavigatingFamily = true;
                IsLoading = true;

                var result = await Task.Run(async () =>
                {
                    var ent = await _taxInvoices.GetByIdAsync(target.Id);
                    if (ent == null) return ((SavedTaxInvoice?)null, (TaxInvoiceSnapshot?)null);
                    var snap = TaxInvoiceMapper.DeserializeSnapshot(ent);
                    return (ent, snap);
                });

                if (result.Item1 == null || result.Item2 == null) return;

                var entity = result.Item1;
                var snapshot = result.Item2;

                _editingId = target.Id;
                _editingInvoiceNumber = entity.InvoiceNumber;
                IsNewInvoice = false;

                RestoreFromSnapshot(entity, snapshot);

                // Update family highlights WITHOUT reloading family list
                foreach (var m in FamilyMembers)
                {
                    m.IsCurrentInvoice = string.Equals(
                        m.InvoiceNumber, _editingInvoiceNumber,
                        StringComparison.OrdinalIgnoreCase);
                    m.IsSelected = m.IsCurrentInvoice;
                }

                _selectedFamilyMember = FamilyMembers.FirstOrDefault(
                    m => m.IsCurrentInvoice);
                OnPropertyChanged(nameof(SelectedFamilyMember));
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
                OnPropertyChanged(nameof(IsSelectedFamilyCurrent));

                StatusMessage = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Switch failed: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
                _isNavigatingFamily = false;
            }
        }

    }
}

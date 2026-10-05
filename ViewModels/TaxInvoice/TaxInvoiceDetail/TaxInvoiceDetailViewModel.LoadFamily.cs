// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.LoadFamily.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.LoadFamily members.
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
        //  LOAD FAMILY (Chat 16 — sidebar contents)
        // ══════════════════════════════════════════════════════════════════

        private async Task LoadFamilyAsync(string proformaId)
        {
            if (_taxInvoices == null ||
                string.IsNullOrWhiteSpace(proformaId))
                return;

            try
            {
                // Fetch the family
                var family = await _taxInvoices.GetFamilyAsync(proformaId);
                if (family == null || family.Members == null)
                {
                    FamilyMembers = new ObservableCollection<TaxInvoiceRowViewModel>();
                    return;
                }

                // Build family member VMs ordered by sequence
                var memberVms = family.Members
                    .OrderBy(m => m.SequenceNumber)
                    .Select(m =>
                    {
                        var vm = new TaxInvoiceRowViewModel(m);
                        vm.IsCurrentInvoice = string.Equals(
                            m.InvoiceNumber, _editingInvoiceNumber,
                            StringComparison.OrdinalIgnoreCase);
                        return vm;
                    })
                    .ToList();

                // Load attachment counts per member
                var invoiceNumbers = memberVms
                    .Select(m => m.InvoiceNumber).ToList();
                var counts = await _taxInvoices
                    .GetAttachmentCountsAsync(invoiceNumbers);

                foreach (var vm in memberVms)
                {
                    vm.AttachmentCount = counts.TryGetValue(
                        vm.InvoiceNumber, out var c) ? c : 0;
                }

                FamilyMembers = new ObservableCollection<TaxInvoiceRowViewModel>(
                    memberVms);

                // Update family financial totals
                FamilyProformaTotal = family.ProformaTotal > 0m
                    ? family.ProformaTotal
                    : family.Members.Sum(m => m.GrandTotal);
                FamilyInvoicedTotal = family.InvoicedTotal;
                FamilyBalanceTotal = family.BalanceTotal;

                // Auto-select the currently loaded invoice
                var current = memberVms.FirstOrDefault(m =>
                    string.Equals(m.InvoiceNumber, _editingInvoiceNumber,
                        StringComparison.OrdinalIgnoreCase));

                _selectedFamilyMember = current;
                if (current != null) current.IsSelected = true;

                OnPropertyChanged(nameof(SelectedFamilyMember));
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
                OnPropertyChanged(nameof(IsSelectedFamilyCurrent));
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[TaxInvoiceDetailVM] Load family failed: {ExceptionMessages.Deep(ex)}");
            }
        }

    }
}

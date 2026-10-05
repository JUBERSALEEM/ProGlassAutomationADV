// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.FamilySidebar.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.FamilySidebar members.
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
        //  FAMILY SIDEBAR (Chat 16)
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<TaxInvoiceRowViewModel> _familyMembers = new();
        public ObservableCollection<TaxInvoiceRowViewModel> FamilyMembers
        {
            get => _familyMembers;
            private set
            {
                _familyMembers = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FamilyMemberCount));
                OnPropertyChanged(nameof(HasMultipleFamilyMembers));
                OnPropertyChanged(nameof(CanIssueAllDrafts));
            }
        }

        public int FamilyMemberCount => _familyMembers?.Count ?? 0;

        public bool HasMultipleFamilyMembers => FamilyMemberCount > 1;

        /// <summary>
        /// True when this PI family has at least one Draft invoice that
        /// Issue All can send. Hidden in the UI when false.
        /// </summary>
        public bool CanIssueAllDrafts =>
            !_isSaving && FamilyMembers != null && FamilyMembers.Any(m => m.IsDraft);

        private TaxInvoiceRowViewModel? _selectedFamilyMember;
        public TaxInvoiceRowViewModel? SelectedFamilyMember
        {
            get => _selectedFamilyMember;
            set
            {
                if (_selectedFamilyMember == value) return;
                if (_selectedFamilyMember != null)
                    _selectedFamilyMember.IsSelected = false;
                _selectedFamilyMember = value;
                if (_selectedFamilyMember != null)
                    _selectedFamilyMember.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
                OnPropertyChanged(nameof(IsSelectedFamilyCurrent));
            }
        }

        /// <summary>
        /// True when selected family member matches the currently loaded invoice.
        /// Used to hide the "Viewing different invoice" warning banner.
        /// </summary>
        public bool IsSelectedFamilyCurrent =>
            _selectedFamilyMember == null ||
            string.Equals(_selectedFamilyMember.InvoiceNumber,
                _editingInvoiceNumber, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Attachments header text — shows selected invoice number and its
        /// attachment count. Updates when family member is clicked.
        /// </summary>
        public string SelectedFamilyHeaderText =>
            _selectedFamilyMember != null
                ? $"ATTACHMENTS ({_selectedFamilyMember.AttachmentCount}) · " +
                  $"{_selectedFamilyMember.InvoiceNumber}"
                : $"ATTACHMENTS ({Attachments?.Count ?? 0})";

        // ── Family financial totals ──────────────────────────────────────

        private decimal _familyProformaTotal;
        public decimal FamilyProformaTotal
        {
            get => _familyProformaTotal;
            private set
            {
                _familyProformaTotal = value; OnPropertyChanged();
                OnPropertyChanged(nameof(FamilyProformaTotalDisplay));
            }
        }

        private decimal _familyInvoicedTotal;
        public decimal FamilyInvoicedTotal
        {
            get => _familyInvoicedTotal;
            private set
            {
                _familyInvoicedTotal = value; OnPropertyChanged();
                OnPropertyChanged(nameof(FamilyInvoicedTotalDisplay));
            }
        }

        private decimal _familyBalanceTotal;
        public decimal FamilyBalanceTotal
        {
            get => _familyBalanceTotal;
            private set
            {
                _familyBalanceTotal = value; OnPropertyChanged();
                OnPropertyChanged(nameof(FamilyBalanceTotalDisplay));
            }
        }

        public string FamilyProformaTotalDisplay => $"AED {_familyProformaTotal:N2}";
        public string FamilyInvoicedTotalDisplay => $"AED {_familyInvoicedTotal:N2}";
        public string FamilyBalanceTotalDisplay => $"AED {_familyBalanceTotal:N2}";

    }
}

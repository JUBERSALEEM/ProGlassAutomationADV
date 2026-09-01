// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.SpecFinancials.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.SpecFinancials members.
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
        //  SPEC GROUPS
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<InvSpecGroupViewModel> _specGroups = new();
        public ObservableCollection<InvSpecGroupViewModel> SpecGroups
        {
            get => _specGroups;
            set { _specGroups = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  FINANCIALS
        // ══════════════════════════════════════════════════════════════════

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                _discountPercent = Math.Max(0m, Math.Min(100m, value));
                OnPropertyChanged();
                RecalculateFinancials();
            }
        }

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            private set
            {
                _subtotal = value; OnPropertyChanged();
                OnPropertyChanged(nameof(SubtotalDisplay));
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set
            {
                _discountAmount = value; OnPropertyChanged();
                OnPropertyChanged(nameof(DiscountAmountDisplay));
            }
        }

        private decimal _afterDiscount;
        public decimal AfterDiscount
        {
            get => _afterDiscount;
            private set
            {
                _afterDiscount = value; OnPropertyChanged();
                OnPropertyChanged(nameof(AfterDiscountDisplay));
            }
        }

        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            private set
            {
                _vatAmount = value; OnPropertyChanged();
                OnPropertyChanged(nameof(VatDisplay));
            }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            private set
            {
                _grandTotal = value; OnPropertyChanged();
                OnPropertyChanged(nameof(GrandTotalDisplay));
                OnPropertyChanged(nameof(GrandTotalInWords));
            }
        }

        public string SubtotalDisplay => $"AED {_subtotal:N2}";
        public string DiscountAmountDisplay => $"AED {_discountAmount:N2}";
        public string AfterDiscountDisplay => $"AED {_afterDiscount:N2}";
        public string VatDisplay => $"AED {_vatAmount:N2}";
        public string GrandTotalDisplay => $"AED {_grandTotal:N2}";
        public string GrandTotalInWords => TaxInvoiceMapper.AmountToWords(_grandTotal);

        private decimal _previouslyInvoicedTotal;
        public decimal PreviouslyInvoicedTotal
        {
            get => _previouslyInvoicedTotal;
            private set
            {
                _previouslyInvoicedTotal = value; OnPropertyChanged();
                OnPropertyChanged(nameof(PreviouslyInvoicedDisplay));
            }
        }

        private decimal _proformaTotal;
        public decimal ProformaTotal
        {
            get => _proformaTotal;
            private set { _proformaTotal = value; OnPropertyChanged(); }
        }

        public string PreviouslyInvoicedDisplay => $"AED {_previouslyInvoicedTotal:N2}";

    }
}

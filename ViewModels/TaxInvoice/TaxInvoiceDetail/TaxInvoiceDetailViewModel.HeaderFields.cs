// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.HeaderFields.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.HeaderFields members.
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
        //  HEADER FIELDS
        // ══════════════════════════════════════════════════════════════════

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            set { _proformaId = value; OnPropertyChanged(); }
        }

        private string _deliveryNoteReference = "";
        public string DeliveryNoteReference
        {
            get => _deliveryNoteReference;
            set { _deliveryNoteReference = value; OnPropertyChanged(); }
        }

        private string _jobOrderId = "";
        public string JobOrderId
        {
            get => _jobOrderId;
            set { _jobOrderId = value; OnPropertyChanged(); }
        }

        private string _purchaseOrderNumber = "";
        public string PurchaseOrderNumber
        {
            get => _purchaseOrderNumber;
            set { _purchaseOrderNumber = value; OnPropertyChanged(); }
        }

        private string _status = "Draft";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBodyEditable));
                OnPropertyChanged(nameof(IsBodyLocked));
                OnPropertyChanged(nameof(BodyLockBannerVisibility));
            }
        }

        /// <summary>
        /// ERP lifecycle hardening (Phase 2): true while the invoice body
        /// (line items, totals, party, dates) may still be edited. Once the
        /// invoice is posted (Issued and beyond) the body freezes — it is
        /// corrected via Cancel/Void or a Credit Note, never by rewriting the
        /// original. Status-only transitions (Issued→Paid) remain available.
        /// A brand-new, unsaved invoice is always editable.
        /// </summary>
        public bool IsBodyEditable =>
            IsNewInvoice ||
            ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.CanEditBody(
                ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.TaxInvoice,
                _status);

        /// <summary>Inverse of <see cref="IsBodyEditable"/>, for XAML.</summary>
        public bool IsBodyLocked => !IsBodyEditable;

        /// <summary>Shows the "posted — body locked" banner only when locked.</summary>
        public System.Windows.Visibility BodyLockBannerVisibility =>
            IsBodyLocked
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        private DateTime _issueDate = DateTime.Today;
        public DateTime IssueDate
        {
            get => _issueDate;
            set
            {
                _issueDate = value;
                OnPropertyChanged();
                DueDate = value.AddDays(PaymentTermsDays);
            }
        }

        private DateTime _dueDate = DateTime.Today.AddDays(30);
        public DateTime DueDate
        {
            get => _dueDate;
            set { _dueDate = value; OnPropertyChanged(); }
        }

        private string _paymentTerms = "Net 30";
        public string PaymentTerms
        {
            get => _paymentTerms;
            set
            {
                _paymentTerms = value;
                OnPropertyChanged();
                PaymentTermsDays = PaymentTermsResolver.ResolveDays(value);
                DueDate = IssueDate.AddDays(PaymentTermsDays);
            }
        }

        private int _paymentTermsDays = 30;
        public int PaymentTermsDays
        {
            get => _paymentTermsDays;
            set { _paymentTermsDays = value; OnPropertyChanged(); }
        }

        public List<string> PaymentTermsOptions { get; } = new()
        {
            "Immediate", "Net 7", "Net 15", "Net 30",
            "Net 45", "Net 60", "Net 90", "Custom"
        };

        public List<string> StatusOptions { get; } = new()
        {
            "Draft", "Issued", "Paid", "PartiallyPaid",
            "Overdue", "Cancelled", "Void"
        };

    }
}

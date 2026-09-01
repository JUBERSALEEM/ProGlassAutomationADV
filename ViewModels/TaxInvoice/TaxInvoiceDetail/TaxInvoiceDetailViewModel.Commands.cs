// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Commands.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Commands members.
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
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════════

        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand IssueCommand { get; }
        public ICommand IssueAllCommand { get; }
        public ICommand MarkPaidCommand { get; }

        /// <summary>v2 §24: shows this invoice's version history.</summary>
        public ICommand ShowVersionsCommand { get; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }
        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }
        public ICommand RecalculateCommand { get; }
        public ICommand ExportPdfCommand { get; }
        // ═══ DETAIL TAB SWITCHER (Overview / Attachments / Family) ═══
        private string _detailTab = "Overview";
        public string DetailTab
        {
            get => _detailTab;
            set
            {
                if (_detailTab == value) return;
                _detailTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverviewTab));
                OnPropertyChanged(nameof(IsAttachmentsTab));
                OnPropertyChanged(nameof(IsFamilyTab));
            }
        }
        public bool IsOverviewTab => DetailTab == "Overview";
        public bool IsAttachmentsTab => DetailTab == "Attachments";
        public bool IsFamilyTab => DetailTab == "Family";

        public ICommand SwitchDetailTabCommand { get; }

        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Chat 16: click a family sidebar entry to switch to that invoice.
        /// Reloads invoice + attachments in place.
        /// </summary>
        public ICommand SelectFamilyMemberCommand { get; }

    }
}

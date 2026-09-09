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

        public ICommand SaveCommand { get; private set; }
        public ICommand BackCommand { get; private set; }
        public ICommand IssueCommand { get; private set; }
        public ICommand IssueAllCommand { get; private set; }
        public ICommand MarkPaidCommand { get; private set; }

        /// <summary>v2 §24: shows this invoice's version history.</summary>
        public ICommand ShowVersionsCommand { get; private set; }
        public ICommand AddAttachmentCommand { get; private set; }
        public ICommand RemoveAttachmentCommand { get; private set; }
        public ICommand AddSpecGroupCommand { get; private set; }
        public ICommand RemoveSpecGroupCommand { get; private set; }
        public ICommand RecalculateCommand { get; private set; }
        public ICommand ExportPdfCommand { get; private set; }
        public ICommand ShareCommand { get; private set; } = null!;
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

        public ICommand SwitchDetailTabCommand { get; private set; }

        public ICommand DeleteCommand { get; private set; }

        /// <summary>
        /// Chat 16: click a family sidebar entry to switch to that invoice.
        /// Reloads invoice + attachments in place.
        /// </summary>
        public ICommand SelectFamilyMemberCommand { get; private set; }

        /// <summary>
        /// ERP lifecycle hardening (Phase 1): soft-close (void) an ISSUED tax
        /// invoice instead of deleting it. A fiscal document is retained, not
        /// erased — the number, figures and audit trail survive, and linked
        /// DNs are freed for re-invoicing. Captures a mandatory reason.
        /// </summary>
        private async Task VoidInvoiceAsync()
        {
            if (IsNewInvoice || _taxInvoices == null) return;

            // ERP hardening (Phase 3, G-6): voiding a fiscal document is a
            // Manager/Admin-only capability (an Accountant may issue but not
            // erase). The repository re-checks at the unbypassable boundary.
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.TaxInvoiceDelete,
                "void a tax invoice"))
                return;

            var proceed = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Void.MsgConfirmVoidX"), _editingInvoiceNumber),
                LocalizationService.Instance.T("Void.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;

            var dlg = _dialogs.Create<ProGlassAutomation.Views.Shared.VoidReasonDialog>(_editingInvoiceNumber);
            if (dlg.ShowDialog() != true) return;
            string reason = dlg.ResultReason;

            try
            {
                await _taxInvoices.VoidAsync(_editingId, reason);

                AuditService.Log(
                    AuditSections.TaxInvoice, "Tax invoice voided",
                    _editingInvoiceNumber, "TI", _editingInvoiceNumber, reason);

                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Void.MsgVoidedX"), _editingInvoiceNumber),
                    LocalizationService.Instance.T("Void.CapVoided"),
                    MessageBoxButton.OK, MessageBoxImage.Information);

                OnRequestBackToList?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Void.MsgVoidFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Void.CapVoidFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}

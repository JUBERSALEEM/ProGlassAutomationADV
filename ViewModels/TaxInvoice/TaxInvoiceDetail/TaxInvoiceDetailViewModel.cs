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
    /// <summary>
    /// ViewModel for Tax Invoice detail / creation.
    ///
    /// FAMILY-AWARE (Chat 16):
    ///   Detail view now shows all family members (partial invoices A/B/C)
    ///   in a right sidebar. User clicks a member → loads that invoice
    ///   + its attachments (like DN History sidebar).
    ///
    /// NEW PROPERTIES:
    ///   FamilyMembers        — all invoices sharing this ProformaId
    ///   SelectedFamilyMember — currently loaded invoice in family
    ///   FamilyProformaTotal  — PI grand total
    ///   FamilyInvoicedTotal  — sum of issued invoices in family
    ///   FamilyBalanceTotal   — remaining unbilled amount
    ///
    /// NEW COMMANDS:
    ///   SelectFamilyMemberCommand — click family entry to load that invoice
    /// </summary>
    public partial class TaxInvoiceDetailViewModel : ViewModelBase
    {
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly IDialogService _dialogs = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;
        private readonly ITaxInvoiceRepository _taxInvoices = null!;

        public TaxInvoiceDetailViewModel(IDeliveryNoteRepository? deliveryNotes, IDialogService? dialogs, IProformaInvoiceRepository? invoices, ITaxInvoiceRepository? taxInvoices)
        {
            _deliveryNotes = deliveryNotes!;
            _dialogs = dialogs!;
            _invoices = invoices!;
            _taxInvoices = taxInvoices!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        public Action? OnRequestBackToList { get; set; }

        // ── Navigation guard ─────────────────────────────────────────────

        private bool _isNavigatingFamily;

        // ── Mode ─────────────────────────────────────────────────────────

        private bool _isNewInvoice = true;
        public bool IsNewInvoice
        {
            get => _isNewInvoice;
            private set
            {
                _isNewInvoice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageTitle));
            }
        }

        private int _editingId;
        private string _editingInvoiceNumber = "";

        public string PageTitle => IsNewInvoice
            ? "New Tax Invoice"
            : $"Edit Invoice — {_editingInvoiceNumber}";

        /// <summary>
        /// Invoice number currently being edited, or empty for a new one.
        /// Read-only accessor added so the agent context layer can name the
        /// open document without reaching into private state. Exposes only
        /// the identifier that PageTitle already shows on screen.
        /// </summary>
        public string EditingInvoiceNumber => _editingInvoiceNumber ?? "";

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public TaxInvoiceDetailViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!,
            App.ServiceProvider?.GetService<ITaxInvoiceRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            SaveCommand = new RelayCommand(
                async _ => await SaveAsync(),
                _ => !_isSaving && !_isLoading);

            BackCommand = new RelayCommand(
                _ => OnRequestBackToList?.Invoke());

            // v2 §24: version history dialog (shared control).
            ShowVersionsCommand = new RelayCommand(
                async _ => await ShowVersionsAsync());

            IssueCommand = new RelayCommand(
                async _ => await ChangeStatusAndSaveAsync("Issued"),
                _ => _status == "Draft" && !_isSaving);

            IssueAllCommand = new RelayCommand(
                async _ => await IssueSelectedDraftsAsync(),
                _ => CanIssueAllDrafts);

            MarkPaidCommand = new RelayCommand(
                async _ => await ChangeStatusAndSaveAsync("Paid"),
                _ => (_status == "Issued" || _status == "PartiallyPaid")
                     && !_isSaving);

            AddSpecGroupCommand = new RelayCommand(_ => AddSpecGroup());

            RemoveSpecGroupCommand = new RelayCommand(param =>
            {
                if (param is InvSpecGroupViewModel g) RemoveSpecGroup(g);
            });

            AddAttachmentCommand = new RelayCommand(
                async _ => await AddAttachmentAsync());

            RemoveAttachmentCommand = new RelayCommand(async param =>
            {
                if (param is InvAttachmentViewModel a)
                    await RemoveAttachmentAsync(a);
            });

            RecalculateCommand = new RelayCommand(_ => RecalculateFinancials());

            ExportPdfCommand = new RelayCommand(
                async _ => await ExportPdfAsync(),
                _ => !IsNewInvoice && !IsSaving);

            SwitchDetailTabCommand = new RelayCommand(p =>
                DetailTab = p as string ?? "Overview");

            DeleteCommand = new RelayCommand(async _ =>
            {
                if (IsNewInvoice) return;
                var result = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.DeleteInvoiceXThisCannot"), _editingInvoiceNumber),
                    LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    await _taxInvoices!.DeleteAsync(_editingId);
                    OnRequestBackToList?.Invoke();
                }
                catch (Exception ex)
                {
                    _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.DeleteFailedX"), ExceptionMessages.Deep(ex)),
                        LocalizationService.Instance.T("Msg.CapDeleteFailed"), MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            },
            _ => !IsNewInvoice && !_isSaving);

            // Chat 16: family sidebar navigation
            SelectFamilyMemberCommand = new RelayCommand(async param =>
            {
                if (param is TaxInvoiceRowViewModel member)
                    await NavigateToFamilyMemberAsync(member);
            });

            // Continuous SR NO across ALL spec groups (same as Step 5): any
            // row/group add or remove triggers ONE global renumber pass so a
            // second spec group continues after the first (Sr 11, not Sr 1).
            SpecGroups.CollectionChanged += (_, e) =>
            {
                if (e.OldItems != null)
                    foreach (InvSpecGroupViewModel g in e.OldItems)
                        g.Rows.CollectionChanged -= OnGroupRowsChanged;
                if (e.NewItems != null)
                    foreach (InvSpecGroupViewModel g in e.NewItems)
                        g.Rows.CollectionChanged += OnGroupRowsChanged;

                RenumberAllSerialNumbers();
            };
        }

        /// <summary>
        /// Assigns CONTINUOUS serial numbers (SR NO) across ALL spec groups,
        /// matching the Step 5 cut-list. Serial numbers flow through to the
        /// saved Tax Invoice and its PDF/export.
        /// </summary>
        public void RenumberAllSerialNumbers()
        {
            int serial = 0;
            foreach (var group in SpecGroups)
                foreach (var row in group.Rows)
                    row.SerialNumber = ++serial;
        }

        private void OnGroupRowsChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => RenumberAllSerialNumbers();


    }
}
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
    public class TaxInvoiceDetailViewModel : ViewModelBase
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
            set { _status = value; OnPropertyChanged(); }
        }

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

        // ══════════════════════════════════════════════════════════════════
        //  COMPANY / CUSTOMER / PROJECT / BANK / PREPARED BY
        // ══════════════════════════════════════════════════════════════════

        private string _companyName = "";
        public string CompanyName
        { get => _companyName; set { _companyName = value; OnPropertyChanged(); } }

        private string _companyTrn = "";
        public string CompanyTrn
        {
            get => _companyTrn;
            set
            {
                _companyTrn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTrnValid));
            }
        }

        public bool IsTrnValid =>
            Regex.IsMatch(_companyTrn ?? "", @"^\d{15}$");

        private string _companyAddress = "";
        public string CompanyAddress
        { get => _companyAddress; set { _companyAddress = value; OnPropertyChanged(); } }

        private string _companyLocation = "";
        public string CompanyLocation
        { get => _companyLocation; set { _companyLocation = value; OnPropertyChanged(); } }

        private string _companyPhone = "";
        public string CompanyPhone
        { get => _companyPhone; set { _companyPhone = value; OnPropertyChanged(); } }

        private string _companyEmail = "";
        public string CompanyEmail
        { get => _companyEmail; set { _companyEmail = value; OnPropertyChanged(); } }

        private string _customerName = "";
        public string CustomerName
        { get => _customerName; set { _customerName = value; OnPropertyChanged(); } }

        private string _customerTrn = "";
        public string CustomerTrn
        { get => _customerTrn; set { _customerTrn = value; OnPropertyChanged(); } }

        private string _customerAddress = "";
        public string CustomerAddress
        { get => _customerAddress; set { _customerAddress = value; OnPropertyChanged(); } }

        private string _customerPhone = "";
        public string CustomerPhone
        { get => _customerPhone; set { _customerPhone = value; OnPropertyChanged(); } }

        private string _customerEmail = "";
        public string CustomerEmail
        { get => _customerEmail; set { _customerEmail = value; OnPropertyChanged(); } }

        private string _contactPerson = "";
        public string ContactPerson
        { get => _contactPerson; set { _contactPerson = value; OnPropertyChanged(); } }

        // ── Project (CHAT 20: aligned with Proforma Invoice naming) ──────
        //
        // Previously: ProjectName, ProjectLocation, ProjectReference (3 fields)
        // Now:        ProjectIdentifierName, ProjectContractNo, ProjectReference,
        //             SiteDeliveryLocation (4 fields)
        //
        // Matches PI ProjectSnapshot exactly — no data loss on PI → TI import.

        private string _projectIdentifierName = "";
        /// <summary>
        /// Short project designation (e.g. "IMAD HABIB").
        /// Matches PI ProjectIdentifierName.
        /// </summary>
        public string ProjectIdentifierName
        { get => _projectIdentifierName; set { _projectIdentifierName = value; OnPropertyChanged(); } }

        private string _projectContractNo = "";
        /// <summary>
        /// Project number with name (e.g. "428 IMAD HABIB").
        /// Matches PI ProjectContractNo.
        /// </summary>
        public string ProjectContractNo
        { get => _projectContractNo; set { _projectContractNo = value; OnPropertyChanged(); } }

        private string _projectReference = "";
        /// <summary>
        /// Full descriptive reference (e.g. "HOUSE FOR IMAD HABIB").
        /// Matches PI ProjectReference.
        /// </summary>
        public string ProjectReference
        { get => _projectReference; set { _projectReference = value; OnPropertyChanged(); } }

        private string _siteDeliveryLocation = "";
        /// <summary>
        /// Site or delivery address (e.g. "AJMAN").
        /// Matches PI SiteDeliveryLocation.
        /// </summary>
        public string SiteDeliveryLocation
        { get => _siteDeliveryLocation; set { _siteDeliveryLocation = value; OnPropertyChanged(); } }

        private string _bankName = "";
        public string BankName
        { get => _bankName; set { _bankName = value; OnPropertyChanged(); } }

        private string _bankAccountNumber = "";
        public string BankAccountNumber
        { get => _bankAccountNumber; set { _bankAccountNumber = value; OnPropertyChanged(); } }

        private string _iban = "";
        public string Iban
        { get => _iban; set { _iban = value; OnPropertyChanged(); } }

        private string _swiftCode = "";
        public string SwiftCode
        { get => _swiftCode; set { _swiftCode = value; OnPropertyChanged(); } }

        private string _paymentNotes = "";
        public string PaymentNotes
        { get => _paymentNotes; set { _paymentNotes = value; OnPropertyChanged(); } }

        private string _preparedByName = "";
        public string PreparedByName
        { get => _preparedByName; set { _preparedByName = value; OnPropertyChanged(); } }

        private string _preparedByTitle = "";
        public string PreparedByTitle
        { get => _preparedByTitle; set { _preparedByTitle = value; OnPropertyChanged(); } }

        private string _preparedByDepartment = "";
        public string PreparedByDepartment
        { get => _preparedByDepartment; set { _preparedByDepartment = value; OnPropertyChanged(); } }

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

        // ══════════════════════════════════════════════════════════════════
        //  ATTACHMENTS
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<InvAttachmentViewModel> _attachments = new();
        public ObservableCollection<InvAttachmentViewModel> Attachments
        {
            get => _attachments;
            set
            {
                _attachments = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                _isSaving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanIssueAllDrafts));
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);

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

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public TaxInvoiceDetailViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!,
            App.ServiceProvider?.GetService<ITaxInvoiceRepository>()!){
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
                    _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.DeleteFailedX"), ex.Message),
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
                StatusMessage = $"Switch failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                _isNavigatingFamily = false;
            }
        }

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
                StatusMessage = $"Load failed: {ex.Message}";
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

        // ══════════════════════════════════════════════════════════════
        //  v2 §28 — UNSAVED-CHANGES DETECTION
        //
        //  TI has dozens of editable fields; instead of wiring every
        //  setter, we serialize the wizard state into a signature after
        //  each load/save and compare on demand. One mechanism covers
        //  every field — nothing can be missed.
        // ══════════════════════════════════════════════════════════════

        private string _cleanStateSignature;

        private string BuildStateSignature()
        {
            try
            {
                return System.Text.Json.JsonSerializer
                    .Serialize(BuildWizardState());
            }
            catch
            {
                // Never block on a signature failure — treat as changed.
                return Guid.NewGuid().ToString();
            }
        }

        /// <summary>Call after a successful load/save.</summary>
        private void CaptureCleanState() =>
            _cleanStateSignature = BuildStateSignature();

        /// <summary>True when the editor differs from the last load/save.</summary>
        public bool IsDirtyNow()
        {
            if (IsNewInvoice) return true;
            if (_cleanStateSignature == null) return false;
            return _cleanStateSignature != BuildStateSignature();
        }

        /// <summary>v2 §28: save entry used by the unsaved-changes guard.</summary>
        public async Task<bool> TrySaveAsync()
        {
            await SaveAsync();
            return !IsDirtyNow();
        }

        /// <summary>
        /// v2 §24: loads an old version's snapshot into the editor.
        /// The database row is untouched until the user saves — saving
        /// appends a NEW version (history is never erased).
        /// </summary>
        public async Task<bool> RestoreVersionAsync(int versionId)
        {
            try
            {
                var version = await ProGlassAutomation.Services.Audit
                    .VersionHistoryService.GetByIdAsync(versionId);

                if (version == null ||
                    string.IsNullOrEmpty(version.SnapshotJson) ||
                    _taxInvoices == null)
                    return false;

                var entity = await _taxInvoices
                    .GetByInvoiceNumberAsync(version.EntityId);
                if (entity == null) return false;

                // Swap in the old snapshot and re-hydrate the UI.
                entity.FullSnapshotJson = version.SnapshotJson;
                var snapshot = TaxInvoiceMapper.DeserializeSnapshot(entity);
                if (snapshot == null) return false;

                _editingId = entity.Id;
                _editingInvoiceNumber = entity.InvoiceNumber;
                IsNewInvoice = false;

                RestoreFromSnapshot(entity, snapshot);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.TaxInvoice,
                    "Version restored",
                    $"{entity.InvoiceNumber} → version {version.VersionNo} " +
                    "(pending save)",
                    "TI", entity.InvoiceNumber);

                return true;
            }
            catch
            {
                return false;
            }
        }
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
                    $"[TaxInvoiceDetailVM] Load family failed: {ex.Message}");
            }
        }

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
                StatusMessage = $"Init from DN failed: {ex.Message}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedToImportDNData"), ex.Message),
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

                var allDns = await _deliveryNotes.GetAllAsync();
                var siblingDnIds = allDns
                    .Where(d => string.Equals(d.ProformaId, proformaId,
                        StringComparison.OrdinalIgnoreCase))
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
                StatusMessage = $"Master import failed: {ex.Message}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapImportFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  INTERNAL IMPORT (shared logic — unchanged from Chat 14)
        // ══════════════════════════════════════════════════════════════════

        private async Task ImportInternalAsync(
            string proformaId,
            Dictionary<(int SpecGroup, int Serial), int>? deliveryNoteQtyMap,
            string dnReferenceLabel,
            string importLabel)
        {
            IsNewInvoice = true;
            _editingId = 0;
            _editingInvoiceNumber = "";

            ResetFields();
            ProformaId = proformaId;
            DeliveryNoteReference = dnReferenceLabel;

            if (_taxInvoices != null)
            {
                var summary = await _taxInvoices
                    .GetFamilySummaryAsync(proformaId);
                PreviouslyInvoicedTotal = summary.TotalInvoiced;
            }

            if (_invoices == null)
            {
                StatusMessage = "";
                return;
            }

            var piEntity = await _invoices.GetByProformaIdAsync(proformaId);

            if (piEntity == null)
            {
                StatusMessage = $"⚠️ PI '{proformaId}' not found.";
                return;
            }

            var piSnapshot = Services.ProformaInvoice
                .ProformaInvoiceRepository.ProformaInvoiceMapper
                .DeserializeSnapshot(piEntity);

            if (piSnapshot == null)
            {
                StatusMessage = "⚠️ PI snapshot could not be read.";
                return;
            }

            ProformaTotal = piEntity.GrandTotalVatInclusive;

            if (piSnapshot.Company != null)
            {
                CompanyName = piSnapshot.Company.CompanyName;
                CompanyTrn = piSnapshot.Company.CompanyTrn;
                CompanyLocation = piSnapshot.Company.CompanyLocation;
                CompanyEmail = piSnapshot.Company.CompanyEmail;
                if (string.IsNullOrWhiteSpace(CompanyAddress))
                    CompanyAddress = piSnapshot.Company.CompanyLocation;
                BankName = piSnapshot.Company.BankName;
                BankAccountNumber = piSnapshot.Company.AccountNumber;
            }

            if (piSnapshot.Client != null)
            {
                CustomerName = piSnapshot.Client.CustomerName;
                CustomerTrn = piSnapshot.Client.CustomerTrn;
                CustomerAddress = piSnapshot.Client.BillingAddress;
                CustomerPhone = piSnapshot.Client.ClientContactPhone;
                ContactPerson = piSnapshot.Client.AttentionExecutiveName;
            }

            // CHAT 20: All 5 PI project fields → 4 TI project properties, 1:1.
            // No data loss. No field collapsing. LPO maps to PurchaseOrderNumber
            // (LPO = PO in UAE glass industry — same concept, different terminology).
            if (piSnapshot.Project != null)
            {
                ProjectIdentifierName = piSnapshot.Project.ProjectIdentifierName;
                ProjectContractNo = piSnapshot.Project.ProjectContractNo;
                ProjectReference = piSnapshot.Project.ProjectReference;
                SiteDeliveryLocation = piSnapshot.Project.SiteDeliveryLocation;
                PurchaseOrderNumber = piSnapshot.Project.LpoNumber;
            }

            if (piSnapshot.PreparedBy != null)
            {
                PreparedByName = piSnapshot.PreparedBy.PreparedByName;
                PreparedByDepartment = piSnapshot.PreparedBy.PreparedByDept;
            }

            if (piSnapshot.Summary != null
                && string.Equals(piSnapshot.Summary.DiscountType,
                    "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                DiscountPercent = piSnapshot.Summary.DiscountValue;
            }

            SpecGroups.Clear();

            if (piSnapshot.SpecGroups != null)
            {
                foreach (var piGroup in piSnapshot.SpecGroups)
                {
                    if (piGroup == null || piGroup.ItemRows == null) continue;

                    var eligibleRows = new List<(GlassRowData row, int qty)>();

                    foreach (var piRow in piGroup.ItemRows)
                    {
                        if (piRow == null) continue;

                        int invoiceQty;
                        if (deliveryNoteQtyMap == null)
                        {
                            invoiceQty = piRow.Qty;
                        }
                        else
                        {
                            var key = (piGroup.GroupNumber, piRow.SerialNumber);
                            if (!deliveryNoteQtyMap.TryGetValue(key, out invoiceQty))
                                continue;
                            if (invoiceQty <= 0) continue;
                        }

                        eligibleRows.Add((
                            new GlassRowData
                            {
                                SerialNumber = piRow.SerialNumber,
                                GlassRef = piRow.GlassRef ?? "",
                                W1 = piRow.W1,
                                H1 = piRow.H1,
                                W2 = piRow.W2,
                                H2 = piRow.H2,
                                Rate = piRow.Rate > 0
                                    ? piRow.Rate : piGroup.BaseRate
                            },
                            invoiceQty));
                    }

                    if (eligibleRows.Count == 0) continue;

                    var invGroup = new InvSpecGroupViewModel(
                        piGroup.GroupNumber, proformaId)
                    {
                        GroupName = !string.IsNullOrWhiteSpace(piGroup.SelectedSpecName)
                            ? piGroup.SelectedSpecName
                            : (piGroup.GroupName ?? ""),
                        GlassType = piGroup.SelectedGlassType ?? "",
                        SpecName = piGroup.SelectedSpecName ?? "",
                        BaseRate = piGroup.BaseRate,
                        OnRowChanged = RecalculateFinancials
                    };

                    foreach (var (row, qty) in eligibleRows)
                    {
                        invGroup.Rows.Add(new InvRowViewModel(invGroup)
                        {
                            SerialNumber = row.SerialNumber,
                            GlassRef = row.GlassRef,
                            W1 = row.W1,
                            H1 = row.H1,
                            W2 = row.W2,
                            H2 = row.H2,
                            Qty = qty,
                            Rate = row.Rate,
                            Notes = ""
                        });
                    }

                    invGroup.RecalculateGroup();
                    SpecGroups.Add(invGroup);
                }
            }

            RecalculateFinancials();

            if (piSnapshot.Summary != null
                && string.Equals(piSnapshot.Summary.DiscountType,
                    "FixedAmount", StringComparison.OrdinalIgnoreCase)
                && Subtotal > 0m)
            {
                DiscountPercent = Math.Round(
                    (piSnapshot.Summary.DiscountValue / Subtotal) * 100m, 2);
                RecalculateFinancials();
            }

            int totalRows = SpecGroups.Sum(g => g.Rows.Count);
            int totalQty = SpecGroups.Sum(g => g.Rows.Sum(r => r.Qty));

            StatusMessage = totalRows > 0
                ? $"✅ Imported {importLabel}: {SpecGroups.Count} spec group(s), " +
                  $"{totalRows} row(s), {totalQty} pcs."
                : $"⚠️ Nothing to invoice — no delivered items found.";
        }

        private sealed class GlassRowData
        {
            public int SerialNumber { get; set; }
            public string GlassRef { get; set; } = "";
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public decimal Rate { get; set; }
        }

        // ══════════════════════════════════════════════════════════════════
        //  SAVE (enhanced — reload family after save)
        // ══════════════════════════════════════════════════════════════════

        public async Task SaveAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.TaxInvoiceEdit, "save a tax invoice"))
                return;

            if (!ValidateBeforeSave()) return;

            // X-6 / D-4: this save spans TWO databases — it writes
            // taxinvoices.db and then updates deliverynotes.db. No
            // transaction can cover both, so being killed between them
            // leaves the books inconsistent. Registering the operation
            // makes the window's Closing handler warn the user instead of
            // terminating mid-sequence.
            using var _critical = ProGlassAutomation.Services.Safety
                .CriticalOperationTracker.Begin("Saving tax invoice");

            try
            {
                IsSaving = true;
                StatusMessage = "Saving…";

                if (_taxInvoices == null)
                {
                    StatusMessage = "Database not initialized.";
                    return;
                }

                var state = BuildWizardState();
                string savedInvoiceNumber;
                string savedSnapshotJson = "";   // v2 §24 version capture

                if (IsNewInvoice)
                {
                    int seq = await _taxInvoices
                        .GetNextSequenceNumberAsync(ProformaId);
                    bool hasExisting = await _taxInvoices
                        .HasAnyInvoiceAsync(ProformaId);
                    string letter = hasExisting
                        ? await _taxInvoices
                            .GetNextSequenceLetterAsync(ProformaId)
                        : "";
                    string invoiceNum = await _taxInvoices
                        .BuildInvoiceNumberAsync(ProformaId, letter);
                    bool exists = await _taxInvoices
                        .ExistsByInvoiceNumberAsync(invoiceNum);

                    if (exists)
                    {
                        StatusMessage = $"Invoice '{invoiceNum}' already exists.";
                        return;
                    }

                    var entity = TaxInvoiceMapper.BuildEntity(
                        state, invoiceNum, seq, letter);
                    savedSnapshotJson = entity.FullSnapshotJson;
                    var saved = await _taxInvoices.InsertAsync(entity);
                    _editingId = saved.Id;
                    _editingInvoiceNumber = saved.InvoiceNumber;
                    savedInvoiceNumber = saved.InvoiceNumber;
                    IsNewInvoice = false;
                    StatusMessage = $"✅ Saved as {saved.InvoiceNumber}";
                }
                else
                {
                    var entity = await _taxInvoices.GetByIdAsync(_editingId);
                    if (entity == null)
                    {
                        StatusMessage = "Invoice not found — cannot update.";
                        return;
                    }

                    var updated = TaxInvoiceMapper.BuildEntity(
                        state, entity.InvoiceNumber,
                        entity.SequenceNumber, entity.SequenceLetter);
                    updated.Id = entity.Id;
                    updated.CreatedAt = entity.CreatedAt;
                    savedSnapshotJson = updated.FullSnapshotJson;

                    await _taxInvoices.UpdateAsync(updated);
                    savedInvoiceNumber = entity.InvoiceNumber;
                    StatusMessage = $"✅ Updated {entity.InvoiceNumber}";
                }

                // ── CHAT 19 INVOICED: mark DNs as invoiced after TI saved ────────
                //
                // DeliveryNoteReference stores DN IDs in two formats:
                //   Single DN : "DN-2026-001-01"
                //   Multiple  : "DN-2026-001-01,DN-2026-001-02,DN-2026-001-03"
                //
                // We parse all IDs and mark each one as invoiced.
                // MarkMultipleAsInvoicedAsync handles both single and multiple.
                // It is idempotent — safe to call even if already marked.
                // Only marks DNs that exist in the DB — missing IDs silently skipped.
                //
                // This fires regardless of Insert or Update so that:
                //   a) Newly saved TI immediately marks DNs
                //   b) Re-saving existing TI keeps DNs marked (idempotent)

                // Single audit point — both insert and update reach here.
                AuditService.Log(
                    AuditSections.TaxInvoice, "Tax invoice saved",
                    $"{savedInvoiceNumber} — status {Status}",
                    "TI", savedInvoiceNumber);

                CaptureCleanState();   // v2 §28: saved = clean

                // v2 §24: append a restorable version of every save.
                if (!string.IsNullOrEmpty(savedSnapshotJson))
                {
                    _ = ProGlassAutomation.Services.Audit
                        .VersionHistoryService.AddVersionAsync(
                            "TI", savedInvoiceNumber,
                            $"Status {Status}",
                            savedSnapshotJson);
                }

                await MarkDnsAsInvoicedAsync(savedInvoiceNumber);

                // Chat 16: reload family sidebar after save
                await LoadFamilyAsync(ProformaId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedToSaveX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// CHAT 19 INVOICED — marks all referenced DNs as invoiced.
        /// Called after every successful TI save (insert or update).
        ///
        /// Parses DeliveryNoteReference field which may contain:
        ///   "DN-2026-001-01"                          ← single DN
        ///   "DN-2026-001-01,DN-2026-001-02"           ← multiple DNs (comma)
        ///
        /// Uses MarkMultipleAsInvoicedAsync which is:
        ///   • Idempotent — safe to call multiple times
        ///   • Batch — one DB call for all IDs
        ///   • Null-safe — empty/blank IDs filtered before DB call
        ///
        /// Non-fatal: if DN marking fails, TI is already saved.
        /// Error is logged to debug output only — no user-facing error.
        /// </summary>
        private async Task MarkDnsAsInvoicedAsync(string taxInvoiceId)
        {
            if (_deliveryNotes == null) return;
            if (string.IsNullOrWhiteSpace(DeliveryNoteReference)) return;
            if (string.IsNullOrWhiteSpace(taxInvoiceId)) return;

            try
            {
                // Phase 11/12: reuse the shared DN-reference parser (single
                // implementation, also used by the reconciliation sweep).
                var dnIds = ProGlassAutomation.Services.TaxInvoice.Repository
                    .TaxInvoiceDnReconciler.ParseDnRefs(DeliveryNoteReference)
                    .ToList();

                if (dnIds.Count == 0) return;

                // Mark all referenced DNs as invoiced in one DB call
                await _deliveryNotes.MarkMultipleAsInvoicedAsync(
                    dnIds, taxInvoiceId);

                AppLogService.Debug(
                    $"[TaxInvoiceDetailVM] Marked {dnIds.Count} DN(s) as " +
                    $"invoiced with TI '{taxInvoiceId}': " +
                    string.Join(", ", dnIds));
            }
            catch (Exception ex)
            {
                // ══════════════════════════════════════════════════════
                //  AUDIT D-4 / R-1 — this is the cross-database seam.
                //
                //  The tax invoice is already committed to taxinvoices.db.
                //  This second write targets deliverynotes.db, a DIFFERENT
                //  database file, so no transaction can span the two. If
                //  it fails, the books disagree: an invoice exists while
                //  its delivery notes still look un-invoiced, so the same
                //  DNs can be invoiced again.
                //
                //  It was reported ONLY through Debug.WriteLine, which is
                //  [Conditional("DEBUG")] and therefore absent from the
                //  Release build — the inconsistency was completely
                //  invisible in production. It now reaches the log file
                //  and the audit trail so it can be found and corrected.
                //
                //  Still deliberately non-fatal: failing the whole save
                //  here would be worse, because the invoice IS saved and
                //  telling the user otherwise would be a lie. Re-saving
                //  the invoice re-runs this and repairs the link, since
                //  MarkMultipleAsInvoicedAsync is idempotent.
                // ══════════════════════════════════════════════════════
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    "Tax invoice saved but its delivery notes could not be " +
                    "marked as invoiced. Re-save the invoice to repair the " +
                    $"link. TI='{taxInvoiceId}', DNs='{DeliveryNoteReference}'.",
                    ex,
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "TaxInvoice",
                        Class = nameof(TaxInvoiceDetailViewModel),
                        Operation = "MarkDnsAsInvoiced"
                    });

                try
                {
                    AuditService.Log(
                        AuditSections.TaxInvoice,
                        "DN invoiced-link FAILED",
                        $"TI {taxInvoiceId} saved, but delivery notes " +
                        $"'{DeliveryNoteReference}' were not marked invoiced. " +
                        "Re-save to repair.");
                }
                catch (System.Exception auditEx)
                {
                    // Audit failure must not mask the original problem.
                    ProGlassAutomation.Services.Logging.AppLogService.Warning("[TaxInvoiceDetailViewModel] Audit write failed after TI save: " + auditEx.Message);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PDF EXPORT (unchanged from Chat 14)
        // ══════════════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (_taxInvoices == null || _editingId == 0) return;

            try
            {
                IsSaving = true;
                StatusMessage = "Generating PDF…";

                var entity = await _taxInvoices.GetByIdAsync(_editingId);
                if (entity == null)
                {
                    StatusMessage = "Invoice not found.";
                    return;
                }

                var attachments = await _taxInvoices
                    .GetAttachmentsAsync(entity.InvoiceNumber);

                bool includeAttachments = false;
                if (attachments != null && attachments.Count > 0)
                {
                    var choice = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.InvoiceHasXAttachmentS"), attachments.Count),
                        LocalizationService.Instance.T("Msg.CapIncludeAttachments"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    includeAttachments = choice == MessageBoxResult.Yes;
                }

                string? savedPath = await Services.TaxInvoice.Pdf
                    .TaxInvoicePdfExporter.SaveToFileAsync(
                        entity,
                        includeAttachments ? attachments : null);

                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    StatusMessage = "PDF export cancelled.";
                    return;
                }

                StatusMessage = $"✅ PDF saved: {System.IO.Path.GetFileName(savedPath)}";

                var open = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.PDFSavedXOpenNow"), savedPath),
                    LocalizationService.Instance.T("Msg.CapPDFReady"), MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (open == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(savedPath)
                        {
                            UseShellExecute = true
                        });
                }
            }
            catch (ReadOnlyModeException ex)
            {
                StatusMessage = ex.UserMessage;
                _dialogs.Show(ex.UserMessage,
                    LocalizationService.Instance.T("Msg.CapReadOnlyMode"), MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF export failed: {ex.Message}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapPDFExportFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  SPEC GROUP MANAGEMENT
        // ══════════════════════════════════════════════════════════════════

        private void AddSpecGroup()
        {
            int next = SpecGroups.Count > 0
                ? SpecGroups.Max(g => g.GroupNumber) + 1 : 1;

            var group = new InvSpecGroupViewModel(next, ProformaId)
            {
                OnRowChanged = RecalculateFinancials
            };

            SpecGroups.Add(group);
            RecalculateFinancials();
        }

        private void RemoveSpecGroup(InvSpecGroupViewModel group)
        {
            SpecGroups.Remove(group);
            RecalculateFinancials();
        }

        // ══════════════════════════════════════════════════════════════════
        //  FINANCIAL RECALCULATION
        // ══════════════════════════════════════════════════════════════════

        public void RecalculateFinancials()
        {
            decimal subtotal = 0m;
            foreach (var group in SpecGroups)
            {
                group.RecalculateGroup();
                subtotal += group.GroupTotalValue;
            }

            // Phase 11/12: shared financial-total rule (UAE 5% VAT).
            var totals = ProGlassAutomation.Services.TaxInvoice
                .InvoiceTotalsCalculator.Compute(subtotal, _discountPercent);

            Subtotal = Math.Round(subtotal, 2);
            DiscountAmount = totals.DiscountAmount;
            AfterDiscount = totals.AfterDiscount;
            VatAmount = totals.VatAmount;
            GrandTotal = totals.GrandTotal;
        }

        // ══════════════════════════════════════════════════════════════════
        //  ATTACHMENT MANAGEMENT
        // ══════════════════════════════════════════════════════════════════

        private async Task AddAttachmentAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Attachment",
                Filter = "PDF Files (*.pdf)|*.pdf|" +
                         "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|" +
                         "All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                string path = dialog.FileName;
                string fileName = System.IO.Path.GetFileName(path);
                byte[] bytes = await System.IO.File.ReadAllBytesAsync(path);

                if (bytes.Length > 10 * 1024 * 1024)
                {
                    _dialogs.Show(LocalizationService.Instance.T("Msg.FileIsLargerThan10"),
                        LocalizationService.Instance.T("Msg.CapFileTooLarge"), MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                string mime = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                string lower = fileName.ToLowerInvariant();
                string attachType = lower.Contains("pi") || lower.Contains("proforma")
                    ? "SignedPI"
                    : lower.Contains("dn") || lower.Contains("delivery")
                        ? "SignedDN"
                        : "Other";

                var vm = new InvAttachmentViewModel
                {
                    AttachmentType = attachType,
                    FileName = fileName,
                    MimeType = mime,
                    FileBytes = bytes,
                    FileSize = bytes.Length,
                    UploadedAt = DateTime.Now,
                    Description = ""
                };

                Attachments.Add(vm);

                if (!IsNewInvoice && _taxInvoices != null
                    && !string.IsNullOrWhiteSpace(_editingInvoiceNumber))
                {
                    var attachEntity = new TaxInvoiceAttachment
                    {
                        InvoiceNumber = _editingInvoiceNumber,
                        AttachmentType = vm.AttachmentType,
                        FileName = vm.FileName,
                        MimeType = vm.MimeType,
                        FileBytes = vm.FileBytes,
                        FileSize = vm.FileSize,
                        Description = vm.Description
                    };

                    var saved = await _taxInvoices.AddAttachmentAsync(attachEntity);
                    vm.Id = saved.Id;

                    // Update count directly — no DB round-trip needed
                    if (_selectedFamilyMember != null)
                        _selectedFamilyMember.AttachmentCount += 1;
                    OnPropertyChanged(nameof(SelectedFamilyHeaderText));
                }
            }
            catch (Exception ex)
            {
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapAttachmentError"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task RemoveAttachmentAsync(InvAttachmentViewModel vm)
        {
            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.RemoveX"), vm.FileName),
                LocalizationService.Instance.T("Msg.CapConfirmRemove"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (vm.Id > 0 && _taxInvoices != null)
                    await _taxInvoices.DeleteAttachmentAsync(vm.Id);

                Attachments.Remove(vm);

                // Update count directly — no DB round-trip needed
                if (_selectedFamilyMember != null && _selectedFamilyMember.AttachmentCount > 0)
                    _selectedFamilyMember.AttachmentCount -= 1;
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
            }
            catch (ReadOnlyModeException ex)
            {
                _dialogs.Show(ex.UserMessage,
                    LocalizationService.Instance.T("Msg.CapReadOnlyMode"), MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapRemoveFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════

        private bool ValidateBeforeSave()
        {
            if (string.IsNullOrWhiteSpace(ProformaId))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.ProformaInvoiceReferenceIsRequired"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.CustomerNameIsRequired"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!IsTrnValid)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.CompanyTRNMustBeExactly"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (DueDate < IssueDate)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.DueDateCannotBeEarlier"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (SpecGroups.Count == 0 || SpecGroups.All(g => g.Rows.Count == 0))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.LeastOneGlassItemRow"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (SpecGroups.SelectMany(g => g.Rows).Any(r => r.Qty <= 0))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.InvoiceRowsMustHaveQty"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (GrandTotal <= 0m)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.GrandTotalMustBeGreater"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private InvWizardState BuildWizardState() => new()
        {
            ProformaId = ProformaId,
            DeliveryNoteReference = DeliveryNoteReference,
            JobOrderId = JobOrderId,
            PurchaseOrderNumber = PurchaseOrderNumber,
            Status = Status,
            IssueDate = IssueDate,
            PaymentTerms = PaymentTerms,
            PaymentTermsDays = PaymentTermsDays,
            CompanyName = CompanyName,
            CompanyTrn = CompanyTrn,
            CompanyAddress = CompanyAddress,
            CompanyLocation = CompanyLocation,
            CompanyPhone = CompanyPhone,
            CompanyEmail = CompanyEmail,
            LogoBase64 = "",
            CustomerName = CustomerName,
            CustomerTrn = CustomerTrn,
            CustomerAddress = CustomerAddress,
            CustomerPhone = CustomerPhone,
            CustomerEmail = CustomerEmail,
            ContactPerson = ContactPerson,
            // CHAT 20: 4-field project section aligned with PI.
            ProjectIdentifierName = ProjectIdentifierName,
            ProjectContractNo = ProjectContractNo,
            ProjectReference = ProjectReference,
            SiteDeliveryLocation = SiteDeliveryLocation,
            DiscountPercent = DiscountPercent,
            ProformaTotal = ProformaTotal,
            PreviouslyInvoicedTotal = PreviouslyInvoicedTotal,
            BankName = BankName,
            BankAccountNumber = BankAccountNumber,
            Iban = Iban,
            SwiftCode = SwiftCode,
            PaymentNotes = PaymentNotes,
            PreparedByName = PreparedByName,
            PreparedByTitle = PreparedByTitle,
            PreparedByDepartment = PreparedByDepartment,
            PreparedBySignatureBase64 = "",
            SpecGroups = SpecGroups.Select(g => new InvSpecGroupState
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                GlassType = g.GlassType,
                SpecName = g.SpecName,
                ProformaId = ProformaId,
                BaseRate = g.BaseRate,
                Rows = g.Rows.Select(r => new InvRowState
                {
                    SerialNumber = r.SerialNumber,
                    GlassRef = r.GlassRef,
                    W1 = r.W1,
                    H1 = r.H1,
                    W2 = r.W2,
                    H2 = r.H2,
                    Qty = r.Qty,
                    Rate = r.Rate,
                    Notes = r.Notes
                }).ToList()
            }).ToList(),
            AttachmentSnapshots = Attachments.Select(a => new InvAttachmentSnapshot
            {
                AttachmentType = a.AttachmentType,
                FileName = a.FileName,
                FileBase64 = a.FileBytes != null
                    ? Convert.ToBase64String(a.FileBytes) : "",
                FileSize = a.FileSize,
                Description = a.Description,
                UploadedAt = a.UploadedAt
            }).ToList()
        };

        private void RestoreFromSnapshot(
    SavedTaxInvoice entity, TaxInvoiceSnapshot snap)
        {
            _proformaId = snap.Header.ProformaId;
            _deliveryNoteReference = snap.Header.DeliveryNoteReference;
            _jobOrderId = snap.Header.JobOrderId;
            _purchaseOrderNumber = snap.Header.PurchaseOrderNumber;
            _status = snap.Header.Status;
            _issueDate = snap.Header.IssueDate == default
                ? DateTime.Today : snap.Header.IssueDate;
            _dueDate = snap.Header.DueDate == default
                ? _issueDate.AddDays(30) : snap.Header.DueDate;

            _companyName = snap.Company.CompanyName;
            _companyTrn = snap.Company.CompanyTrn;
            _companyAddress = snap.Company.CompanyAddress;
            _companyLocation = snap.Company.CompanyLocation;
            _companyPhone = snap.Company.CompanyPhone;
            _companyEmail = snap.Company.CompanyEmail;

            _customerName = snap.Customer.CustomerName;
            _customerTrn = snap.Customer.CustomerTrn;
            _customerAddress = snap.Customer.CustomerAddress;
            _customerPhone = snap.Customer.CustomerPhone;
            _customerEmail = snap.Customer.CustomerEmail;
            _contactPerson = snap.Customer.ContactPerson;

            // CHAT 20: Restore new fields with legacy migration.
            // New saved invoices have ProjectIdentifierName populated.
            // Old saved invoices have only ProjectName (legacy field).
            // If new field is empty, fall back to legacy value — no data loss.
#pragma warning disable CS0612 // Suppress [Obsolete] warnings for migration reads
            _projectIdentifierName = !string.IsNullOrWhiteSpace(snap.Project.ProjectIdentifierName)
                ? snap.Project.ProjectIdentifierName
                : snap.Project.ProjectName;     // ← legacy fallback

            _projectContractNo = snap.Project.ProjectContractNo;
            _projectReference = snap.Project.ProjectReference;

            _siteDeliveryLocation = !string.IsNullOrWhiteSpace(snap.Project.SiteDeliveryLocation)
                ? snap.Project.SiteDeliveryLocation
                : snap.Project.ProjectLocation;  // ← legacy fallback
#pragma warning restore CS0612

            _bankName = snap.Bank.BankName;
            _bankAccountNumber = snap.Bank.BankAccountNumber;
            _iban = snap.Bank.Iban;
            _swiftCode = snap.Bank.SwiftCode;
            _paymentTerms = snap.Bank.PaymentTerms;
            _paymentTermsDays = snap.Bank.PaymentTermsDays;
            _paymentNotes = snap.Bank.PaymentNotes;

            _preparedByName = snap.PreparedBy.Name;
            _preparedByTitle = snap.PreparedBy.Title;
            _preparedByDepartment = snap.PreparedBy.Department;

            _discountPercent = snap.Financial.DiscountPercent;
            _previouslyInvoicedTotal = snap.Financial.PreviouslyInvoicedTotal;

            _specGroups.Clear();
            foreach (var g in snap.SpecGroups)
            {
                var groupVm = new InvSpecGroupViewModel(g.GroupNumber, _proformaId)
                {
                    GroupName = g.GroupName,
                    GlassType = g.GlassType,
                    SpecName = g.SpecName,
                    BaseRate = g.BaseRate,
                    OnRowChanged = RecalculateFinancials
                };

                foreach (var r in g.Rows)
                {
                    groupVm.Rows.Add(new InvRowViewModel(groupVm)
                    {
                        SerialNumber = r.SerialNumber,
                        GlassRef = r.GlassRef,
                        W1 = r.W1,
                        H1 = r.H1,
                        W2 = r.W2,
                        H2 = r.H2,
                        Qty = r.Qty,
                        Rate = r.Rate,
                        Notes = r.Notes
                    });
                }

                groupVm.RecalculateGroup();
                _specGroups.Add(groupVm);
            }

            _attachments.Clear();
            foreach (var a in entity.Attachments)
            {
                _attachments.Add(new InvAttachmentViewModel
                {
                    Id = a.Id,
                    AttachmentType = a.AttachmentType,
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    FileBytes = a.FileBytes,
                    FileSize = a.FileSize,
                    Description = a.Description,
                    UploadedAt = a.UploadedAt
                });
            }

            decimal subtotal = 0m;
            foreach (var group in _specGroups)
                subtotal += group.GroupTotalValue;

            decimal discountAmt = Math.Round(subtotal * (_discountPercent / 100m), 2);
            decimal afterDiscount = subtotal - discountAmt;
            decimal vatAmount = Math.Round(afterDiscount * 0.05m, 2);

            _subtotal = Math.Round(subtotal, 2);
            _discountAmount = discountAmt;
            _afterDiscount = afterDiscount;
            _vatAmount = vatAmount;
            _grandTotal = afterDiscount + vatAmount;

            OnPropertyChanged(string.Empty);
        }

        private void ResetFields()
        {
            ProformaId = ""; DeliveryNoteReference = "";
            JobOrderId = ""; PurchaseOrderNumber = "";
            Status = "Draft";
            IssueDate = DateTime.Today;
            PaymentTerms = "Net 30"; PaymentTermsDays = 30;
            DueDate = DateTime.Today.AddDays(30);
            CompanyName = ""; CompanyTrn = ""; CompanyAddress = "";
            CompanyLocation = ""; CompanyPhone = ""; CompanyEmail = "";
            CustomerName = ""; CustomerTrn = ""; CustomerAddress = "";
            CustomerPhone = ""; CustomerEmail = ""; ContactPerson = "";
            // CHAT 20: 4-field project reset
            ProjectIdentifierName = ""; ProjectContractNo = "";
            ProjectReference = ""; SiteDeliveryLocation = "";
            BankName = ""; BankAccountNumber = ""; Iban = "";
            SwiftCode = ""; PaymentNotes = "";
            PreparedByName = ""; PreparedByTitle = ""; PreparedByDepartment = "";
            _discountPercent = 0m;
            OnPropertyChanged(nameof(DiscountPercent));
            SpecGroups.Clear();
            Attachments.Clear();
            FamilyMembers = new ObservableCollection<TaxInvoiceRowViewModel>();
            FamilyProformaTotal = 0m;
            FamilyInvoicedTotal = 0m;
            FamilyBalanceTotal = 0m;
            RecalculateFinancials();
        }

        /// <summary>
        /// Opens the Issue All popup for every Draft in this PI family.
        /// Sibling drafts go through UpdateStatusAsync (snapshot header
        /// patched in the repository). The open invoice, if selected,
        /// uses ChangeStatusAndSaveAsync so TRN / line validation still runs.
        /// </summary>
        private async Task IssueSelectedDraftsAsync()
        {
            var drafts = FamilyMembers.Where(m => m.IsDraft).ToList();
            if (drafts.Count == 0) return;

            var selected = IssueDraftsDialog.Show(
                Application.Current?.MainWindow, drafts);
            if (selected == null || selected.Count == 0) return;

            if (_taxInvoices == null)
            {
                StatusMessage = "Database not initialized.";
                return;
            }

            try
            {
                IsSaving = true;
                var others = selected
                    .Where(m => !string.Equals(
                        m.InvoiceNumber, _editingInvoiceNumber,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                bool includeCurrent = selected.Any(m => string.Equals(
                    m.InvoiceNumber, _editingInvoiceNumber,
                    StringComparison.OrdinalIgnoreCase));

                foreach (var member in others)
                {
                    await _taxInvoices.UpdateStatusAsync(
                        member.InvoiceNumber, "Issued");
                    AuditService.Log(
                        AuditSections.TaxInvoice, "Tax invoice issued",
                        $"{member.InvoiceNumber} — Issue all",
                        "TI", member.InvoiceNumber);
                }

                int issued = others.Count;
                IsSaving = false;

                if (includeCurrent && _status == "Draft")
                {
                    await ChangeStatusAndSaveAsync("Issued");
                    if (_status == "Issued")
                    {
                        issued++;
                        StatusMessage = $"Issued {issued} tax invoice(s).";
                    }
                    else if (others.Count > 0)
                    {
                        StatusMessage =
                            $"Issued {others.Count} sibling invoice(s). " +
                            $"This invoice was not issued. {StatusMessage}";
                    }
                }
                else
                {
                    await LoadFamilyAsync(ProformaId);
                    if (issued > 0)
                        StatusMessage = $"Issued {issued} tax invoice(s).";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Issue all failed: {ex.Message}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.CouldNotIssueInvoicesX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapIssueAllFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task ChangeStatusAndSaveAsync(string newStatus)
        {
            // Phase 3: enforce centralized status-transition rules at the
            // business boundary.
            var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                .Validate(
                    ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.TaxInvoice,
                    _status, newStatus);
            if (!transition.IsValid)
            {
                StatusMessage = transition.Reason ?? "Invalid status change.";
                return;
            }

            Status = newStatus;
            await SaveAsync();
        }

    }
}
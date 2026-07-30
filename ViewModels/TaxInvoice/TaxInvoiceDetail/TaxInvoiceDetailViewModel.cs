using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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
        public Action? OnRequestBackToList { get; set; }

        // ── Navigation guard ─────────────────────────────────────────────

        private bool _isNavigatingFamily;
        private bool _suppressNotifications;

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
                PaymentTermsDays = ResolveTermsDays(value);
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
            }
        }

        public int FamilyMemberCount => _familyMembers?.Count ?? 0;

        public bool HasMultipleFamilyMembers => FamilyMemberCount > 1;

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
            private set { _isSaving = value; OnPropertyChanged(); }
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
        public ICommand MarkPaidCommand { get; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }
        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }
        public ICommand RecalculateCommand { get; }
        public ICommand ExportPdfCommand { get; }
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
        {
            SaveCommand = new RelayCommand(
                async _ => await SaveAsync(),
                _ => !_isSaving && !_isLoading);

            BackCommand = new RelayCommand(
                _ => OnRequestBackToList?.Invoke());

            IssueCommand = new RelayCommand(
                async _ => await ChangeStatusAndSaveAsync("Issued"),
                _ => _status == "Draft" && !_isSaving);

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

            DeleteCommand = new RelayCommand(async _ =>
            {
                if (IsNewInvoice) return;
                var result = MessageBox.Show(
                    $"Delete invoice '{_editingInvoiceNumber}'?\n\n" +
                    "This cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    await App.TaxInvoices!.DeleteAsync(_editingId);
                    OnRequestBackToList?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Delete failed:\n\n{ex.Message}",
                        "Delete Failed", MessageBoxButton.OK,
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
        }

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
            if (App.TaxInvoices == null) return;
            if (_isNavigatingFamily) return;
            if (target.Id == _editingId) return;

            try
            {
                _isNavigatingFamily = true;
                IsLoading = true;

                var result = await Task.Run(async () =>
                {
                    var ent = await App.TaxInvoices.GetByIdAsync(target.Id);
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

                if (App.TaxInvoices == null)
                {
                    StatusMessage = "Database not initialized.";
                    return;
                }

                var entity = await App.TaxInvoices.GetByIdAsync(id);
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

        // ══════════════════════════════════════════════════════════════════
        //  LOAD FAMILY (Chat 16 — sidebar contents)
        // ══════════════════════════════════════════════════════════════════

        private async Task LoadFamilyAsync(string proformaId)
        {
            if (App.TaxInvoices == null ||
                string.IsNullOrWhiteSpace(proformaId))
                return;

            try
            {
                // Fetch the family
                var family = await App.TaxInvoices.GetFamilyAsync(proformaId);
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
                var counts = await App.TaxInvoices
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
                System.Diagnostics.Debug.WriteLine(
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
            if (App.DeliveryNotes == null
                || string.IsNullOrWhiteSpace(deliveryNoteId))
            {
                StatusMessage = "Delivery Note database not initialized.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading DN '{deliveryNoteId}'…";

                var dnEntity = await App.DeliveryNotes
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
                MessageBox.Show($"Failed to import DN data:\n\n{ex.Message}",
                    "Import Failed", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task InitFromDeliveryNoteFamilyAsync(string proformaId)
        {
            if (App.DeliveryNotes == null
                || string.IsNullOrWhiteSpace(proformaId))
            {
                StatusMessage = "Delivery Note database not initialized.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading all deliveries for '{proformaId}'…";

                var allDns = await App.DeliveryNotes.GetAllAsync();
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
                    var dn = await App.DeliveryNotes
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

                // Chat 16: load family sidebar
                await LoadFamilyAsync(proformaId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Master import failed: {ex.Message}";
                MessageBox.Show($"Failed:\n\n{ex.Message}",
                    "Import Failed", MessageBoxButton.OK,
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

            if (App.TaxInvoices != null)
            {
                var summary = await App.TaxInvoices
                    .GetFamilySummaryAsync(proformaId);
                PreviouslyInvoicedTotal = summary.TotalInvoiced;
            }

            if (App.Invoices == null)
            {
                StatusMessage = "";
                return;
            }

            var piEntity = await App.Invoices.GetByProformaIdAsync(proformaId);

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
            if (!ValidateBeforeSave()) return;

            try
            {
                IsSaving = true;
                StatusMessage = "Saving…";

                if (App.TaxInvoices == null)
                {
                    StatusMessage = "Database not initialized.";
                    return;
                }

                var state = BuildWizardState();
                string savedInvoiceNumber;

                if (IsNewInvoice)
                {
                    int seq = await App.TaxInvoices
                        .GetNextSequenceNumberAsync(ProformaId);
                    bool hasExisting = await App.TaxInvoices
                        .HasAnyInvoiceAsync(ProformaId);
                    string letter = hasExisting
                        ? await App.TaxInvoices
                            .GetNextSequenceLetterAsync(ProformaId)
                        : "";
                    string invoiceNum = await App.TaxInvoices
                        .BuildInvoiceNumberAsync(ProformaId, letter);
                    bool exists = await App.TaxInvoices
                        .ExistsByInvoiceNumberAsync(invoiceNum);

                    if (exists)
                    {
                        StatusMessage = $"Invoice '{invoiceNum}' already exists.";
                        return;
                    }

                    var entity = TaxInvoiceMapper.BuildEntity(
                        state, invoiceNum, seq, letter);
                    var saved = await App.TaxInvoices.InsertAsync(entity);
                    _editingId = saved.Id;
                    _editingInvoiceNumber = saved.InvoiceNumber;
                    savedInvoiceNumber = saved.InvoiceNumber;
                    IsNewInvoice = false;
                    StatusMessage = $"✅ Saved as {saved.InvoiceNumber}";
                }
                else
                {
                    var entity = await App.TaxInvoices.GetByIdAsync(_editingId);
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

                    await App.TaxInvoices.UpdateAsync(updated);
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

                await MarkDnsAsInvoicedAsync(savedInvoiceNumber);

                // Chat 16: reload family sidebar after save
                await LoadFamilyAsync(ProformaId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                MessageBox.Show($"Failed to save:\n\n{ex.Message}",
                    "Save Failed", MessageBoxButton.OK,
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
            if (App.DeliveryNotes == null) return;
            if (string.IsNullOrWhiteSpace(DeliveryNoteReference)) return;
            if (string.IsNullOrWhiteSpace(taxInvoiceId)) return;

            try
            {
                // Parse comma-separated DN IDs from DeliveryNoteReference
                // Trim whitespace from each ID — handles "DN-001, DN-002" too
                var dnIds = DeliveryNoteReference
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();

                if (dnIds.Count == 0) return;

                // Mark all referenced DNs as invoiced in one DB call
                await App.DeliveryNotes.MarkMultipleAsInvoicedAsync(
                    dnIds, taxInvoiceId);

                System.Diagnostics.Debug.WriteLine(
                    $"[TaxInvoiceDetailVM] Marked {dnIds.Count} DN(s) as " +
                    $"invoiced with TI '{taxInvoiceId}': " +
                    string.Join(", ", dnIds));
            }
            catch (Exception ex)
            {
                // Non-fatal — TI is already saved successfully
                // DN invoiced state will be corrected on next save
                System.Diagnostics.Debug.WriteLine(
                    $"[TaxInvoiceDetailVM] MarkDnsAsInvoicedAsync failed " +
                    $"(non-fatal): {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PDF EXPORT (unchanged from Chat 14)
        // ══════════════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (App.TaxInvoices == null || _editingId == 0) return;

            try
            {
                IsSaving = true;
                StatusMessage = "Generating PDF…";

                var entity = await App.TaxInvoices.GetByIdAsync(_editingId);
                if (entity == null)
                {
                    StatusMessage = "Invoice not found.";
                    return;
                }

                var attachments = await App.TaxInvoices
                    .GetAttachmentsAsync(entity.InvoiceNumber);

                bool includeAttachments = false;
                if (attachments != null && attachments.Count > 0)
                {
                    var choice = MessageBox.Show(
                        $"This invoice has {attachments.Count} attachment(s).\n\n" +
                        "Include them in the PDF?",
                        "Include Attachments?",
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

                var open = MessageBox.Show(
                    $"PDF saved:\n\n{savedPath}\n\nOpen now?",
                    "PDF Ready", MessageBoxButton.YesNo,
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
            catch (Exception ex)
            {
                StatusMessage = $"PDF export failed: {ex.Message}";
                MessageBox.Show($"Failed:\n\n{ex.Message}",
                    "PDF Export Failed", MessageBoxButton.OK,
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

            decimal discountAmt = Math.Round(subtotal * (_discountPercent / 100m), 2);
            decimal afterDiscount = subtotal - discountAmt;
            decimal vatAmount = Math.Round(afterDiscount * 0.05m, 2);
            decimal grandTotal = afterDiscount + vatAmount;

            Subtotal = Math.Round(subtotal, 2);
            DiscountAmount = discountAmt;
            AfterDiscount = afterDiscount;
            VatAmount = vatAmount;
            GrandTotal = grandTotal;
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
                    MessageBox.Show("File is larger than 10 MB.",
                        "File Too Large", MessageBoxButton.OK,
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

                if (!IsNewInvoice && App.TaxInvoices != null
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

                    var saved = await App.TaxInvoices.AddAttachmentAsync(attachEntity);
                    vm.Id = saved.Id;

                    // Update count directly — no DB round-trip needed
                    if (_selectedFamilyMember != null)
                        _selectedFamilyMember.AttachmentCount += 1;
                    OnPropertyChanged(nameof(SelectedFamilyHeaderText));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed:\n\n{ex.Message}",
                    "Attachment Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task RemoveAttachmentAsync(InvAttachmentViewModel vm)
        {
            var result = MessageBox.Show(
                $"Remove '{vm.FileName}'?",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (vm.Id > 0 && App.TaxInvoices != null)
                    await App.TaxInvoices.DeleteAttachmentAsync(vm.Id);

                Attachments.Remove(vm);

                // Update count directly — no DB round-trip needed
                if (_selectedFamilyMember != null && _selectedFamilyMember.AttachmentCount > 0)
                    _selectedFamilyMember.AttachmentCount -= 1;
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed:\n\n{ex.Message}",
                    "Remove Failed", MessageBoxButton.OK,
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
                MessageBox.Show("Proforma Invoice reference is required.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                MessageBox.Show("Customer name is required.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (SpecGroups.Count == 0 || SpecGroups.All(g => g.Rows.Count == 0))
            {
                MessageBox.Show("At least one glass item row is required.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            _suppressNotifications = true;

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

            _suppressNotifications = false;

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

        private async Task ChangeStatusAndSaveAsync(string newStatus)
        {
            Status = newStatus;
            await SaveAsync();
        }

        private static int ResolveTermsDays(string terms) => terms switch
        {
            "Immediate" => 0,
            "Net 7" => 7,
            "Net 15" => 15,
            "Net 30" => 30,
            "Net 45" => 45,
            "Net 60" => 60,
            "Net 90" => 90,
            _ => 30
        };
    }
}
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
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
    /// LOAD PATHS:
    ///   LoadAsync(id)                     — load existing invoice from DB
    ///   InitNewAsync(piId)                — blank invoice for PI (full import, rare)
    ///   InitFromDeliveryNoteAsync(dnId)   — invoice ONLY that DN's delivered qty
    ///   InitFromDeliveryNoteFamilyAsync(piId) — Master Invoice: sum ALL sibling
    ///                                            DNs' delivered qty for this PI
    ///
    /// PARTIAL INVOICING BUSINESS RULE:
    ///   Never invoice the full PI unless zero deliveries exist.
    ///   Always base Qty on actual DeliveredQty from DN items.
    ///   Skip rows with 0 delivered qty (not billable).
    ///   Rate comes from PI (pricing), Qty comes from DN (delivered).
    ///
    /// PHASE 6 (PDF): ExportPdfAsync() uses TaxInvoicePdfExporter.
    /// </summary>
    public class TaxInvoiceDetailViewModel : ViewModelBase
    {
        public Action? OnRequestBackToList { get; set; }

        // ── Mode ──────────────────────────────────────────────────────────

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

        public System.Collections.Generic.List<string> PaymentTermsOptions { get; } = new()
        {
            "Immediate", "Net 7", "Net 15", "Net 30",
            "Net 45", "Net 60", "Net 90", "Custom"
        };

        public System.Collections.Generic.List<string> StatusOptions { get; } = new()
        {
            "Draft", "Issued", "Paid", "PartiallyPaid",
            "Overdue", "Cancelled", "Void"
        };

        // ══════════════════════════════════════════════════════════════════
        //  COMPANY
        // ══════════════════════════════════════════════════════════════════

        private string _companyName = "";
        public string CompanyName
        {
            get => _companyName;
            set { _companyName = value; OnPropertyChanged(); }
        }

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
        {
            get => _companyAddress;
            set { _companyAddress = value; OnPropertyChanged(); }
        }

        private string _companyLocation = "";
        public string CompanyLocation
        {
            get => _companyLocation;
            set { _companyLocation = value; OnPropertyChanged(); }
        }

        private string _companyPhone = "";
        public string CompanyPhone
        {
            get => _companyPhone;
            set { _companyPhone = value; OnPropertyChanged(); }
        }

        private string _companyEmail = "";
        public string CompanyEmail
        {
            get => _companyEmail;
            set { _companyEmail = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  CUSTOMER
        // ══════════════════════════════════════════════════════════════════

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            set { _customerName = value; OnPropertyChanged(); }
        }

        private string _customerTrn = "";
        public string CustomerTrn
        {
            get => _customerTrn;
            set { _customerTrn = value; OnPropertyChanged(); }
        }

        private string _customerAddress = "";
        public string CustomerAddress
        {
            get => _customerAddress;
            set { _customerAddress = value; OnPropertyChanged(); }
        }

        private string _customerPhone = "";
        public string CustomerPhone
        {
            get => _customerPhone;
            set { _customerPhone = value; OnPropertyChanged(); }
        }

        private string _customerEmail = "";
        public string CustomerEmail
        {
            get => _customerEmail;
            set { _customerEmail = value; OnPropertyChanged(); }
        }

        private string _contactPerson = "";
        public string ContactPerson
        {
            get => _contactPerson;
            set { _contactPerson = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PROJECT
        // ══════════════════════════════════════════════════════════════════

        private string _projectName = "";
        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }

        private string _projectLocation = "";
        public string ProjectLocation
        {
            get => _projectLocation;
            set { _projectLocation = value; OnPropertyChanged(); }
        }

        private string _projectReference = "";
        public string ProjectReference
        {
            get => _projectReference;
            set { _projectReference = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  BANK
        // ══════════════════════════════════════════════════════════════════

        private string _bankName = "";
        public string BankName
        {
            get => _bankName;
            set { _bankName = value; OnPropertyChanged(); }
        }

        private string _bankAccountNumber = "";
        public string BankAccountNumber
        {
            get => _bankAccountNumber;
            set { _bankAccountNumber = value; OnPropertyChanged(); }
        }

        private string _iban = "";
        public string Iban
        {
            get => _iban;
            set { _iban = value; OnPropertyChanged(); }
        }

        private string _swiftCode = "";
        public string SwiftCode
        {
            get => _swiftCode;
            set { _swiftCode = value; OnPropertyChanged(); }
        }

        private string _paymentNotes = "";
        public string PaymentNotes
        {
            get => _paymentNotes;
            set { _paymentNotes = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PREPARED BY
        // ══════════════════════════════════════════════════════════════════

        private string _preparedByName = "";
        public string PreparedByName
        {
            get => _preparedByName;
            set { _preparedByName = value; OnPropertyChanged(); }
        }

        private string _preparedByTitle = "";
        public string PreparedByTitle
        {
            get => _preparedByTitle;
            set { _preparedByTitle = value; OnPropertyChanged(); }
        }

        private string _preparedByDepartment = "";
        public string PreparedByDepartment
        {
            get => _preparedByDepartment;
            set { _preparedByDepartment = value; OnPropertyChanged(); }
        }

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
                _subtotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubtotalDisplay));
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set
            {
                _discountAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiscountAmountDisplay));
            }
        }

        private decimal _afterDiscount;
        public decimal AfterDiscount
        {
            get => _afterDiscount;
            private set
            {
                _afterDiscount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AfterDiscountDisplay));
            }
        }

        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            private set
            {
                _vatAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VatDisplay));
            }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            private set
            {
                _grandTotal = value;
                OnPropertyChanged();
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
                _previouslyInvoicedTotal = value;
                OnPropertyChanged();
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
        //  ATTACHMENTS
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<InvAttachmentViewModel> _attachments = new();
        public ObservableCollection<InvAttachmentViewModel> Attachments
        {
            get => _attachments;
            set { _attachments = value; OnPropertyChanged(); }
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

        public bool HasStatusMessage =>
            !string.IsNullOrWhiteSpace(_statusMessage);

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

            RecalculateCommand = new RelayCommand(
                _ => RecalculateFinancials());

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
                    MessageBox.Show(
                        $"Delete failed:\n\n{ex.Message}",
                        "Delete Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            },
            _ => !IsNewInvoice && !_isSaving);
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD EXISTING
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
        //  INIT NEW — 3 ENTRY POINTS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// PI-only path (no DN context). Rarely used — creates invoice with
        /// full PI qty. Only appropriate when no DN exists yet.
        /// Called by: PI picker dialog when creating invoice directly from PI.
        /// </summary>
        public async Task InitNewAsync(string proformaId)
        {
            await ImportInternalAsync(
                proformaId: proformaId,
                deliveryNoteQtyMap: null,
                dnReferenceLabel: "",
                importLabel: "full PI");
        }

        /// <summary>
        /// Convert a SINGLE DN to Tax Invoice.
        /// Only imports rows where DeliveredQty > 0 in THIS specific DN.
        /// Qty column = this DN's DeliveredQty (not ordered qty from PI).
        /// Called by: "Convert to Tax Invoice" button on DN Detail history row.
        /// </summary>
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

                // Fetch this specific DN
                var dnEntity = await App.DeliveryNotes
                    .GetByDeliveryNoteIdAsync(deliveryNoteId);

                if (dnEntity == null)
                {
                    StatusMessage = $"⚠️ Delivery Note '{deliveryNoteId}' not found.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(dnEntity.ProformaId))
                {
                    StatusMessage =
                        $"⚠️ DN '{deliveryNoteId}' has no linked Proforma Invoice.";
                    return;
                }

                // Build qty map: (SpecGroupNumber, SerialNumber) → DeliveredQty
                // Only include rows with DeliveredQty > 0
                var qtyMap = dnEntity.Items
                    .Where(i => i.DeliveredQty > 0)
                    .ToDictionary(
                        i => (i.SpecGroupNumber, i.SerialNumber),
                        i => i.DeliveredQty);

                if (qtyMap.Count == 0)
                {
                    StatusMessage =
                        $"⚠️ DN '{deliveryNoteId}' has no delivered items " +
                        "(all qty = 0). Cannot create invoice.";
                    return;
                }

                await ImportInternalAsync(
                    proformaId: dnEntity.ProformaId,
                    deliveryNoteQtyMap: qtyMap,
                    dnReferenceLabel: deliveryNoteId,
                    importLabel: $"DN '{deliveryNoteId}'");

                // Set JO ID from DN for cross-module tracking
                JobOrderId = dnEntity.JobOrderId;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Init from DN failed: {ex.Message}";
                MessageBox.Show(
                    $"Failed to import DN data:\n\n{ex.Message}",
                    "Import Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// MASTER INVOICE: aggregate ALL delivered qty across ALL sibling DNs
        /// for this ProformaId. Only imports rows with total delivered > 0.
        /// Called by: "Master Invoice" button on DN List JO group row.
        /// </summary>
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

                // Fetch all DNs for this PI
                var allDns = await App.DeliveryNotes.GetAllAsync();
                var siblingDnIds = allDns
                    .Where(d => string.Equals(d.ProformaId, proformaId,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.DeliveryNoteId)
                    .ToList();

                if (siblingDnIds.Count == 0)
                {
                    StatusMessage =
                        $"⚠️ No delivery notes found for '{proformaId}'.";
                    return;
                }

                // Aggregate delivered qty across all sibling DNs
                // Key: (SpecGroupNumber, SerialNumber)
                // Value: SUM of DeliveredQty across all DNs
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

                    if (hasAnyDelivered)
                        refList.Add(dnId);
                }

                if (aggregatedQty.Count == 0)
                {
                    StatusMessage =
                        $"⚠️ No delivered items across any DN for '{proformaId}'.";
                    return;
                }

                string dnRefs = string.Join(",", refList);

                await ImportInternalAsync(
                    proformaId: proformaId,
                    deliveryNoteQtyMap: aggregatedQty,
                    dnReferenceLabel: dnRefs,
                    importLabel: $"Master ({refList.Count} DN{(refList.Count > 1 ? "s" : "")})");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Master import failed: {ex.Message}";
                MessageBox.Show(
                    $"Failed to build Master Invoice:\n\n{ex.Message}",
                    "Import Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  INTERNAL IMPORT — shared logic
        //  If deliveryNoteQtyMap is null → import full PI qty
        //  Otherwise → import ONLY rows in map, using map's qty
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

            // ── Tax Invoice family summary ────────────────────────────────
            if (App.TaxInvoices != null)
            {
                var summary = await App.TaxInvoices
                    .GetFamilySummaryAsync(proformaId);
                PreviouslyInvoicedTotal = summary.TotalInvoiced;
            }

            // ── Load PI entity ────────────────────────────────────────────
            if (App.Invoices == null)
            {
                StatusMessage = "";
                return;
            }

            var piEntity = await App.Invoices.GetByProformaIdAsync(proformaId);

            if (piEntity == null)
            {
                StatusMessage =
                    $"⚠️ Proforma Invoice '{proformaId}' not found.";
                return;
            }

            var piSnapshot = Services.ProformaInvoice
                .ProformaInvoiceRepository.ProformaInvoiceMapper
                .DeserializeSnapshot(piEntity);

            if (piSnapshot == null)
            {
                StatusMessage = $"⚠️ PI snapshot could not be read.";
                return;
            }

            ProformaTotal = piEntity.GrandTotalVatInclusive;

            // ── Copy Company data ────────────────────────────────────────
            if (piSnapshot.Company != null)
            {
                CompanyName = piSnapshot.Company.CompanyName;
                CompanyTrn = piSnapshot.Company.CompanyTrn;
                CompanyLocation = piSnapshot.Company.CompanyLocation;
            }

            // ── Copy Customer data ───────────────────────────────────────
            if (piSnapshot.Client != null)
            {
                CustomerName = piSnapshot.Client.CustomerName;
                CustomerTrn = piSnapshot.Client.CustomerTrn;
                CustomerAddress = piSnapshot.Client.BillingAddress;
                CustomerPhone = piSnapshot.Client.ClientContactPhone;
                ContactPerson = piSnapshot.Client.AttentionExecutiveName;
            }

            // ── Copy Project data ────────────────────────────────────────
            if (piSnapshot.Project != null)
            {
                ProjectName = piSnapshot.Project.ProjectIdentifierName;
                ProjectLocation = piSnapshot.Project.SiteDeliveryLocation;
                ProjectReference =
                    !string.IsNullOrWhiteSpace(piSnapshot.Project.ProjectReference)
                        ? piSnapshot.Project.ProjectReference
                        : piSnapshot.Project.ProjectContractNo;
                PurchaseOrderNumber = piSnapshot.Project.LpoNumber;
            }

            // ── Copy Prepared By data ────────────────────────────────────
            if (piSnapshot.PreparedBy != null)
            {
                PreparedByName = piSnapshot.PreparedBy.PreparedByName;
                PreparedByDepartment = piSnapshot.PreparedBy.PreparedByDept;
            }

            // ── Copy discount from PI ────────────────────────────────────
            if (piSnapshot.Summary != null
                && string.Equals(piSnapshot.Summary.DiscountType,
                    "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                DiscountPercent = piSnapshot.Summary.DiscountValue;
            }

            // ── Build Spec Groups + Rows ─────────────────────────────────
            SpecGroups.Clear();

            if (piSnapshot.SpecGroups != null)
            {
                foreach (var piGroup in piSnapshot.SpecGroups)
                {
                    if (piGroup == null || piGroup.ItemRows == null) continue;

                    // Collect ONLY rows we should invoice
                    var eligibleRows = new List<(GlassRowData row, int qty)>();

                    foreach (var piRow in piGroup.ItemRows)
                    {
                        if (piRow == null) continue;

                        int invoiceQty;

                        if (deliveryNoteQtyMap == null)
                        {
                            // Full PI import — use PI qty as-is
                            invoiceQty = piRow.Qty;
                        }
                        else
                        {
                            // DN-scoped import — only if this row was delivered
                            var key = (piGroup.GroupNumber, piRow.SerialNumber);
                            if (!deliveryNoteQtyMap.TryGetValue(key, out invoiceQty))
                                continue; // 0 delivered → skip row entirely

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
                                    ? piRow.Rate
                                    : piGroup.BaseRate
                            },
                            invoiceQty));
                    }

                    // Skip empty groups
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
                        var invRow = new InvRowViewModel(invGroup)
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
                        };

                        invGroup.Rows.Add(invRow);
                    }

                    invGroup.RecalculateGroup();
                    SpecGroups.Add(invGroup);
                }
            }

            // ── Recalculate financials ────────────────────────────────────
            RecalculateFinancials();

            // ── Fixed-amount discount → convert to % ─────────────────────
            if (piSnapshot.Summary != null
                && string.Equals(piSnapshot.Summary.DiscountType,
                    "FixedAmount", StringComparison.OrdinalIgnoreCase)
                && Subtotal > 0m)
            {
                DiscountPercent = Math.Round(
                    (piSnapshot.Summary.DiscountValue / Subtotal) * 100m, 2);
                RecalculateFinancials();
            }

            // ── Success message ──────────────────────────────────────────
            int totalRows = SpecGroups.Sum(g => g.Rows.Count);
            int totalQty = SpecGroups.Sum(g => g.Rows.Sum(r => r.Qty));

            StatusMessage = totalRows > 0
                ? $"✅ Imported {importLabel}: {SpecGroups.Count} spec group(s), " +
                  $"{totalRows} row(s), {totalQty} pcs."
                : $"⚠️ Nothing to invoice — no delivered items found.";
        }

        /// <summary>Private DTO for eligible row collection.</summary>
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
        //  SAVE
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
                        state,
                        entity.InvoiceNumber,
                        entity.SequenceNumber,
                        entity.SequenceLetter);

                    updated.Id = entity.Id;
                    updated.CreatedAt = entity.CreatedAt;

                    await App.TaxInvoices.UpdateAsync(updated);
                    StatusMessage = $"✅ Updated {entity.InvoiceNumber}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                MessageBox.Show(
                    $"Failed to save:\n\n{ex.Message}",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PDF EXPORT — Phase 6
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
                    StatusMessage = "Invoice not found — cannot export.";
                    return;
                }

                var attachments = await App.TaxInvoices
                    .GetAttachmentsAsync(entity.InvoiceNumber);

                bool includeAttachments = false;
                if (attachments != null && attachments.Count > 0)
                {
                    var choice = MessageBox.Show(
                        $"This invoice has {attachments.Count} attachment(s).\n\n" +
                        "Include them in the PDF?\n\n" +
                        "  Yes → Invoice + all attachments (single PDF)\n" +
                        "  No  → Invoice only",
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
                    $"PDF saved successfully:\n\n{savedPath}\n\n" +
                    "Open now?",
                    "PDF Ready",
                    MessageBoxButton.YesNo,
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
                MessageBox.Show(
                    $"Failed to generate PDF:\n\n{ex.Message}",
                    "PDF Export Failed",
                    MessageBoxButton.OK,
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
                ? SpecGroups.Max(g => g.GroupNumber) + 1
                : 1;

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
                    MessageBox.Show(
                        "File is larger than 10 MB.\n\nPlease compress before attaching.",
                        "File Too Large",
                        MessageBoxButton.OK,
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

                if (!IsNewInvoice
                    && App.TaxInvoices != null
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to add attachment:\n\n{ex.Message}",
                    "Attachment Error",
                    MessageBoxButton.OK,
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to remove:\n\n{ex.Message}",
                    "Remove Failed",
                    MessageBoxButton.OK,
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
            ProjectName = ProjectName,
            ProjectLocation = ProjectLocation,
            ProjectReference = ProjectReference,
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
            ProformaId = snap.Header.ProformaId;
            DeliveryNoteReference = snap.Header.DeliveryNoteReference;
            JobOrderId = snap.Header.JobOrderId;
            PurchaseOrderNumber = snap.Header.PurchaseOrderNumber;
            Status = snap.Header.Status;
            IssueDate = snap.Header.IssueDate == default
                ? DateTime.Today : snap.Header.IssueDate;
            DueDate = snap.Header.DueDate == default
                ? IssueDate.AddDays(30) : snap.Header.DueDate;

            CompanyName = snap.Company.CompanyName;
            CompanyTrn = snap.Company.CompanyTrn;
            CompanyAddress = snap.Company.CompanyAddress;
            CompanyLocation = snap.Company.CompanyLocation;
            CompanyPhone = snap.Company.CompanyPhone;
            CompanyEmail = snap.Company.CompanyEmail;

            CustomerName = snap.Customer.CustomerName;
            CustomerTrn = snap.Customer.CustomerTrn;
            CustomerAddress = snap.Customer.CustomerAddress;
            CustomerPhone = snap.Customer.CustomerPhone;
            CustomerEmail = snap.Customer.CustomerEmail;
            ContactPerson = snap.Customer.ContactPerson;

            ProjectName = snap.Project.ProjectName;
            ProjectLocation = snap.Project.ProjectLocation;
            ProjectReference = snap.Project.ProjectReference;

            BankName = snap.Bank.BankName;
            BankAccountNumber = snap.Bank.BankAccountNumber;
            Iban = snap.Bank.Iban;
            SwiftCode = snap.Bank.SwiftCode;
            PaymentTerms = snap.Bank.PaymentTerms;
            PaymentTermsDays = snap.Bank.PaymentTermsDays;
            PaymentNotes = snap.Bank.PaymentNotes;

            PreparedByName = snap.PreparedBy.Name;
            PreparedByTitle = snap.PreparedBy.Title;
            PreparedByDepartment = snap.PreparedBy.Department;

            _discountPercent = snap.Financial.DiscountPercent;
            OnPropertyChanged(nameof(DiscountPercent));
            PreviouslyInvoicedTotal = snap.Financial.PreviouslyInvoicedTotal;

            SpecGroups.Clear();
            foreach (var g in snap.SpecGroups)
            {
                var groupVm = new InvSpecGroupViewModel(g.GroupNumber, ProformaId)
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
                SpecGroups.Add(groupVm);
            }

            Attachments.Clear();
            foreach (var a in entity.Attachments)
            {
                Attachments.Add(new InvAttachmentViewModel
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

            RecalculateFinancials();
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
            ProjectName = ""; ProjectLocation = ""; ProjectReference = "";
            BankName = ""; BankAccountNumber = ""; Iban = "";
            SwiftCode = ""; PaymentNotes = "";
            PreparedByName = ""; PreparedByTitle = ""; PreparedByDepartment = "";
            _discountPercent = 0m;
            OnPropertyChanged(nameof(DiscountPercent));
            SpecGroups.Clear();
            Attachments.Clear();
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
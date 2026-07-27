using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.CalculationService;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Services.TaxInvoice.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice
{
    public class TaxInvoiceItemRowViewModel : ViewModelBase
    {
        public event Action? InvoicedQtyChanged;

        public int SpecGroupNumber { get; }
        public string SpecGroupName { get; }
        public int SerialNumber { get; }
        public string GlassRef { get; }
        public int W1 { get; }
        public int H1 { get; }
        public int W2 { get; }
        public int H2 { get; }

        public int DeliveredQty { get; }
        public int PreviousInvoicedQty { get; }
        public int RemainingQty => Math.Max(0, DeliveredQty - PreviousInvoicedQty);

        private int _invoicedQty;
        public int InvoicedQty
        {
            get => _invoicedQty;
            set
            {
                if (_invoicedQty == value) return;
                // Bound invoiced quantity between 0 and remaining delivered quantity
                _invoicedQty = Math.Max(0, Math.Min(value, RemainingQty));
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineAmount));
                InvoicedQtyChanged?.Invoke();
            }
        }

        private decimal _rateAed;
        public decimal RateAed
        {
            get => _rateAed;
            set
            {
                if (_rateAed == value) return;
                _rateAed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineAmount));
            }
        }

        private decimal _exchangeRate = 1.0m;
        public decimal ExchangeRate
        {
            get => _exchangeRate;
            set
            {
                if (_exchangeRate == value) return;
                _exchangeRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineAmount));
                OnPropertyChanged(nameof(DisplayRate));
            }
        }

        public decimal DisplayRate => Math.Round(_rateAed / _exchangeRate, 2);

        public decimal Sqm => SqmCalculator.CalculatePanel(W1, H1);

        public decimal LineAmount
        {
            get
            {
                decimal rateInCurrency = _rateAed / _exchangeRate;
                return Math.Round(Sqm * _invoicedQty * rateInCurrency, 2);
            }
        }

        public string DimensionDisplay =>
            W2 > 0 || H2 > 0
                ? $"{W1}×{H1}  /  {W2}×{H2}"
                : $"{W1}×{H1}";

        public string Notes { get; set; } = "";

        public TaxInvoiceItemRowViewModel(DeliveryNoteItem item, decimal rateAed)
        {
            SpecGroupNumber = item.SpecGroupNumber;
            SpecGroupName = item.SpecGroupName;
            SerialNumber = item.SerialNumber;
            GlassRef = item.GlassRef;
            W1 = item.W1;
            H1 = item.H1;
            W2 = item.W2;
            H2 = item.H2;
            DeliveredQty = item.DeliveredQty;
            PreviousInvoicedQty = item.InvoicedQty; // This tracks how much of this item has already been invoiced
            _rateAed = rateAed;
            _invoicedQty = RemainingQty; // default to remaining
        }
    }

    public class TaxInvoiceCreateViewModel : ViewModelBase
    {
        private readonly TaxInvoiceViewModel _parent;

        private SavedDeliveryNote? _deliveryNote;
        public SavedDeliveryNote? DeliveryNote
        {
            get => _deliveryNote;
            private set
            {
                if (_deliveryNote == value) return;
                _deliveryNote = value;
                OnPropertyChanged();
            }
        }

        // ── Invoice fields ───────────────────────────────────────────────

        private string _invoiceNumber = "";
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set { if (_invoiceNumber == value) return; _invoiceNumber = value; OnPropertyChanged(); }
        }

        private DateTime _invoiceDate = DateTime.Today;
        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set { if (_invoiceDate == value) return; _invoiceDate = value; OnPropertyChanged(); }
        }

        private string _deliveryNoteId = "";
        public string DeliveryNoteId
        {
            get => _deliveryNoteId;
            set { if (_deliveryNoteId == value) return; _deliveryNoteId = value; OnPropertyChanged(); }
        }

        private string _jobOrderId = "";
        public string JobOrderId
        {
            get => _jobOrderId;
            set { if (_jobOrderId == value) return; _jobOrderId = value; OnPropertyChanged(); }
        }

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            set { if (_proformaId == value) return; _proformaId = value; OnPropertyChanged(); }
        }

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            set { if (_customerName == value) return; _customerName = value; OnPropertyChanged(); }
        }

        private string _projectName = "";
        public string ProjectName
        {
            get => _projectName;
            set { if (_projectName == value) return; _projectName = value; OnPropertyChanged(); }
        }

        private string _salesman = "";
        public string Salesman
        {
            get => _salesman;
            set { if (_salesman == value) return; _salesman = value; OnPropertyChanged(); }
        }

        private string _customerTrn = "";
        public string CustomerTrn
        {
            get => _customerTrn;
            set { if (_customerTrn == value) return; _customerTrn = value; OnPropertyChanged(); }
        }

        private string _companyTrn = "";
        public string CompanyTrn
        {
            get => _companyTrn;
            set { if (_companyTrn == value) return; _companyTrn = value; OnPropertyChanged(); }
        }

        // ── Multi-currency ───────────────────────────────────────────────

        public ObservableCollection<CurrencyInfo> Currencies { get; }

        private CurrencyInfo _selectedCurrency;
        public CurrencyInfo SelectedCurrency
        {
            get => _selectedCurrency;
            set
            {
                if (_selectedCurrency == value) return;
                _selectedCurrency = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrencySymbol));
                OnPropertyChanged(nameof(CurrencyCode));

                // Propagate exchange rate to items and recalculate
                foreach (var item in ItemRows)
                {
                    item.ExchangeRate = _selectedCurrency.ExchangeRateToAed;
                }

                RecalculateTotals();
            }
        }

        public string CurrencySymbol => _selectedCurrency?.Symbol ?? "AED";
        public string CurrencyCode => _selectedCurrency?.Code ?? "AED";

        // ── Totals ───────────────────────────────────────────────────────

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set { if (_subtotal == value) return; _subtotal = value; OnPropertyChanged(); }
        }

        private decimal _discountValue;
        public decimal DiscountValue
        {
            get => _discountValue;
            set
            {
                if (_discountValue == value) return;
                _discountValue = Math.Max(0.0m, value);
                OnPropertyChanged();
                RecalculateTotals();
            }
        }

        private decimal _taxableAmount;
        public decimal TaxableAmount
        {
            get => _taxableAmount;
            set { if (_taxableAmount == value) return; _taxableAmount = value; OnPropertyChanged(); }
        }

        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            set { if (_vatAmount == value) return; _vatAmount = value; OnPropertyChanged(); }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set { if (_grandTotal == value) return; _grandTotal = value; OnPropertyChanged(); }
        }

        private string _paymentStatus = "Unpaid";
        public string PaymentStatus
        {
            get => _paymentStatus;
            set { if (_paymentStatus == value) return; _paymentStatus = value; OnPropertyChanged(); }
        }

        public List<string> PaymentStatusOptions { get; } = new() { "Unpaid", "Paid", "Partially Paid" };

        private ObservableCollection<TaxInvoiceItemRowViewModel> _itemRows = new();
        public ObservableCollection<TaxInvoiceItemRowViewModel> ItemRows
        {
            get => _itemRows;
            set { if (_itemRows == value) return; _itemRows = value; OnPropertyChanged(); }
        }

        private bool _isReadOnly = false;
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set { if (_isReadOnly == value) return; _isReadOnly = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditable)); }
        }

        public bool IsEditable => !_isReadOnly;

        // ── Commands ──────────────────────────────────────────────────────

        public new ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportExcelCommand { get; }

        public TaxInvoiceCreateViewModel(TaxInvoiceViewModel parent)
        {
            _parent = parent;

            Currencies = new ObservableCollection<CurrencyInfo>(CurrencyService.GetSupportedCurrencies());
            _selectedCurrency = Currencies.FirstOrDefault(c => c.Code == "AED") ?? new CurrencyInfo();

            SaveCommand = new RelayCommand(async _ => await SaveInvoiceAsync(), _ => IsEditable);
            CancelCommand = new RelayCommand(_ => _parent.ReturnToList());
            ExportPdfCommand = new RelayCommand(async _ => await ExportPdfAsync(), _ => IsReadOnly);
            ExportExcelCommand = new RelayCommand(async _ => await ExportExcelAsync(), _ => IsReadOnly);
        }

        public void InitializeFromDeliveryNote(SavedDeliveryNote dn, bool isFullConversion)
        {
            IsReadOnly = false;
            DeliveryNote = dn;

            DeliveryNoteId = dn.DeliveryNoteId;
            JobOrderId = dn.JobOrderId;
            ProformaId = dn.ProformaId;
            CustomerName = dn.CustomerName;
            ProjectName = dn.ProjectName;

            InvoiceDate = DateTime.Today;
            DiscountValue = 0.0m;
            PaymentStatus = "Unpaid";

            SelectedCurrency = Currencies.FirstOrDefault(c => c.Code == "AED") ?? new CurrencyInfo();

            // Run async job in background
            _ = InitializeCreateFlowAsync(dn, isFullConversion);
        }

        public void LoadExistingInvoice(Models.TaxInvoice.TaxInvoice invoice)
        {
            IsReadOnly = true;
            InvoiceNumber = invoice.InvoiceNumber;
            InvoiceDate = invoice.InvoiceDate;
            DeliveryNoteId = invoice.DeliveryNoteId;
            JobOrderId = invoice.JobOrderId;
            ProformaId = invoice.ProformaId;
            CustomerName = invoice.CustomerName;
            ProjectName = invoice.ProjectName;
            Salesman = invoice.Salesman;
            CustomerTrn = invoice.CustomerTrn;
            CompanyTrn = invoice.CompanyTrn;
            PaymentStatus = invoice.PaymentStatus;

            SelectedCurrency = Currencies.FirstOrDefault(c => c.Code == invoice.Currency) ?? new CurrencyInfo();
            DiscountValue = invoice.Discount;

            // Load snapshots
            var snap = TaxInvoiceMapper.DeserializeSnapshot(invoice);
            var rows = new List<TaxInvoiceItemRowViewModel>();
            foreach (var item in snap.Items)
            {
                var row = new TaxInvoiceItemRowViewModel(new DeliveryNoteItem
                {
                    SpecGroupNumber = item.SpecGroupNumber,
                    SpecGroupName = item.SpecGroupName,
                    SerialNumber = item.SerialNumber,
                    GlassRef = item.GlassRef,
                    W1 = item.W1,
                    H1 = item.H1,
                    W2 = item.W2,
                    H2 = item.H2,
                    DeliveredQty = item.DeliveredQty,
                    InvoicedQty = 0 // Will overwrite
                }, item.Rate * SelectedCurrency.ExchangeRateToAed); // Map back to AED rate

                row.InvoicedQty = item.InvoicedQty;
                row.ExchangeRate = SelectedCurrency.ExchangeRateToAed;
                rows.Add(row);
            }

            ItemRows = new ObservableCollection<TaxInvoiceItemRowViewModel>(rows);
            RecalculateTotals();
        }

        private async Task InitializeCreateFlowAsync(SavedDeliveryNote dn, bool isFullConversion)
        {
            try
            {
                // Retrieve Parent Job Order to match item rates
                decimal defaultRate = 150m;
                var jo = await App.Jobs.GetByJobOrderIdAsync(dn.JobOrderId);
                var joSnapshot = jo != null ? JobOrderMapper.DeserializeSnapshot(jo) : null;

                // Prepopulate customer TRN from PI/JO details if available
                if (joSnapshot?.EmbeddedInvoice?.Client != null)
                {
                    CustomerTrn = joSnapshot.EmbeddedInvoice.Client.CustomerTrn;
                    Salesman = joSnapshot.EmbeddedInvoice.Client.SalesRepresentative;
                }

                // Prepopulate Company TRN
                if (joSnapshot?.EmbeddedInvoice?.Company != null)
                {
                    CompanyTrn = joSnapshot.EmbeddedInvoice.Company.CompanyTrn;
                }

                // Generate Sequential Invoice Number
                if (App.TaxInvoices != null)
                {
                    int nextSeq = await App.TaxInvoices.GetNextSequenceAsync();
                    InvoiceNumber = await App.TaxInvoices.BuildInvoiceNumberAsync(nextSeq);
                }

                var rows = new List<TaxInvoiceItemRowViewModel>();
                foreach (var item in dn.Items)
                {
                    decimal rateAed = defaultRate;
                    if (joSnapshot?.EmbeddedInvoice?.SpecGroups != null)
                    {
                        var groupSpec = joSnapshot.EmbeddedInvoice.SpecGroups.FirstOrDefault(g => g.GroupNumber == item.SpecGroupNumber);
                        if (groupSpec != null)
                        {
                            var rowSpec = groupSpec.ItemRows.FirstOrDefault(r => r.SerialNumber == item.SerialNumber);
                            rateAed = rowSpec != null ? rowSpec.Rate : groupSpec.BaseRate;
                        }
                    }

                    var row = new TaxInvoiceItemRowViewModel(item, rateAed);
                    row.ExchangeRate = SelectedCurrency.ExchangeRateToAed;

                    // If partial conversion, default invoiced quantity to 0 so the user can choose what to invoice.
                    // If full conversion, default to the entire remaining delivered quantity.
                    if (!isFullConversion)
                    {
                        row.InvoicedQty = 0;
                    }

                    row.InvoicedQtyChanged += RecalculateTotals;
                    rows.Add(row);
                }

                ItemRows = new ObservableCollection<TaxInvoiceItemRowViewModel>(rows);
                RecalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Tax Invoice: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecalculateTotals()
        {
            decimal subtotal = 0.0m;
            foreach (var r in ItemRows)
            {
                subtotal += r.LineAmount;
            }

            Subtotal = subtotal;
            TaxableAmount = Math.Max(0.0m, subtotal - _discountValue);
            VatAmount = TaxCalculationService.CalculateVat(_taxableAmount);
            GrandTotal = _taxableAmount + _vatAmount;
        }

        private async Task SaveInvoiceAsync()
        {
            if (string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                MessageBox.Show("Please enter a valid Invoice Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ItemRows.Count == 0 || ItemRows.All(r => r.InvoicedQty == 0))
            {
                MessageBox.Show("At least one item must have an invoiced quantity greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (App.TaxInvoices == null || App.DeliveryNotes == null) return;

                // ── Step 1: Duplicate check ──────────────────────────────────
                bool exists = await App.TaxInvoices.ExistsByInvoiceNumberAsync(InvoiceNumber);
                if (exists)
                {
                    MessageBox.Show($"Invoice Number '{InvoiceNumber}' already exists.\nPlease generate a different number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ── Step 2: Build snapshot and entity ────────────────────────
                var snapshot = new TaxInvoiceSnapshot
                {
                    InvoiceNumber = InvoiceNumber,
                    InvoiceDate = InvoiceDate,
                    DeliveryNoteId = DeliveryNoteId,
                    JobOrderId = JobOrderId,
                    ProformaId = ProformaId,
                    CustomerName = CustomerName,
                    ProjectName = ProjectName,
                    Salesman = Salesman,
                    CustomerTrn = CustomerTrn,
                    CompanyTrn = CompanyTrn,
                    Subtotal = Subtotal,
                    Discount = DiscountValue,
                    TaxableAmount = TaxableAmount,
                    VatAmount = VatAmount,
                    GrandTotal = GrandTotal,
                    Currency = SelectedCurrency.Code,
                    ExchangeRate = SelectedCurrency.ExchangeRateToAed,
                    PaymentStatus = PaymentStatus
                };

                foreach (var row in ItemRows)
                {
                    snapshot.Items.Add(new TaxInvoiceItemSnapshot
                    {
                        SpecGroupNumber = row.SpecGroupNumber,
                        SpecGroupName = row.SpecGroupName,
                        SerialNumber = row.SerialNumber,
                        GlassRef = row.GlassRef,
                        W1 = row.W1,
                        H1 = row.H1,
                        W2 = row.W2,
                        H2 = row.H2,
                        DeliveredQty = row.DeliveredQty,
                        InvoicedQty = row.InvoicedQty,
                        Rate = row.RateAed, // Save rates in base currency (AED)
                        Amount = row.LineAmount,
                        Notes = row.Notes
                    });
                }

                var entity = TaxInvoiceMapper.FromSnapshot(snapshot);

                // Add default history row
                entity.History.Add(new TaxInvoiceHistory
                {
                    TaxInvoiceId = InvoiceNumber,
                    Status = PaymentStatus,
                    EventDate = DateTime.Now,
                    Notes = $"Invoice {InvoiceNumber} generated successfully."
                });

                // Insert into DB
                await App.TaxInvoices.InsertAsync(entity);

                // ── Step 3: Update Delivery Note items and HasTaxInvoice flag ──
                if (_deliveryNote != null)
                {
                    var freshDn = await App.DeliveryNotes.GetByIdAsync(_deliveryNote.Id);
                    if (freshDn != null)
                    {
                        foreach (var row in ItemRows)
                        {
                            var freshItem = freshDn.Items.FirstOrDefault(i => i.SpecGroupNumber == row.SpecGroupNumber && i.SerialNumber == row.SerialNumber);
                            if (freshItem != null)
                            {
                                freshItem.InvoicedQty += row.InvoicedQty;
                            }
                        }

                        // Check if DN is now fully invoiced
                        bool allInvoiced = freshDn.Items.All(i => i.InvoicedQty >= i.DeliveredQty);
                        if (allInvoiced)
                        {
                            freshDn.HasTaxInvoice = true;
                        }
                        freshDn.TaxInvoiceId = InvoiceNumber;

                        await App.DeliveryNotes.UpdateAsync(freshDn);
                    }
                }

                MessageBox.Show($"Tax Invoice '{InvoiceNumber}' saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                _parent.ReturnToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save Tax Invoice:\n\n{ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportPdfAsync()
        {
            if (App.TaxInvoices == null) return;
            var invoice = await App.TaxInvoices.GetByInvoiceNumberAsync(InvoiceNumber);
            if (invoice == null) return;

            var layout = new TaxInvoicePdfService(invoice);
            var engine = new Services.Export.Pdf.PdfExportEngine();
            var request = new Services.Export.Core.ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };
            var result = await engine.ExportAsync(layout, request);
            if (!result.Success && !result.Cancelled)
            {
                MessageBox.Show($"Export PDF failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportExcelAsync()
        {
            if (App.TaxInvoices == null) return;
            var invoice = await App.TaxInvoices.GetByInvoiceNumberAsync(InvoiceNumber);
            if (invoice == null) return;

            var layout = new TaxInvoiceExcelService(invoice);
            var engine = new Services.Export.Excel.ExcelExportEngine();
            var request = new Services.Export.Core.ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };
            var result = await engine.ExportAsync(layout, request);
            if (!result.Success && !result.Cancelled)
            {
                MessageBox.Show($"Export Excel failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
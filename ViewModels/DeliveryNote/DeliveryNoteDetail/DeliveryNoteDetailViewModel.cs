using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    /// <summary>
    /// ViewModel for the Delivery Note detail view.
    ///
    /// Two-column layout:
    ///   Left  — read-only JO snapshot context (customer, project, spec summary)
    ///   Right — editable DN fields (driver, vehicle, receiver, status, notes)
    ///           + delivery items grid (ordered/prev/this DN/balance per row)
    ///           + delivery history timeline (all DNs for this JO)
    ///           + attachments section (Phase 10.6)
    ///
    /// LoadAsync(id) fetches entity + deserializes snapshot.
    /// SaveAsync() updates entity + refreshes snapshot JSON.
    /// OnRequestBackToList callback wired by shell VM.
    /// </summary>
    public class DeliveryNoteDetailViewModel : ViewModelBase
    {
        // ── callbacks ──────────────────────────────────────────────────────
        public Action? OnRequestBackToList { get; set; }

        // ── loaded entity + snapshot ──────────────────────────────────────
        private SavedDeliveryNote? _entity;
        private DeliveryNoteSnapshot? _snapshot;

        // ── loading state ─────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set { if (_isSaving == value) return; _isSaving = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            private set { if (_errorMessage == value) return; _errorMessage = value; OnPropertyChanged(); }
        }

        // ── DN header fields (read display) ──────────────────────────────

        private string _deliveryNoteId = "";
        public string DeliveryNoteId
        {
            get => _deliveryNoteId;
            private set { if (_deliveryNoteId == value) return; _deliveryNoteId = value; OnPropertyChanged(); }
        }

        private string _jobOrderId = "";
        public string JobOrderId
        {
            get => _jobOrderId;
            private set { if (_jobOrderId == value) return; _jobOrderId = value; OnPropertyChanged(); }
        }

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            private set { if (_proformaId == value) return; _proformaId = value; OnPropertyChanged(); }
        }

        private string _deliveryType = "Original";
        public string DeliveryType
        {
            get => _deliveryType;
            private set { if (_deliveryType == value) return; _deliveryType = value; OnPropertyChanged(); }
        }

        private string _deliveryDateDisplay = "";
        public string DeliveryDateDisplay
        {
            get => _deliveryDateDisplay;
            private set { if (_deliveryDateDisplay == value) return; _deliveryDateDisplay = value; OnPropertyChanged(); }
        }

        private string _deliveryTime = "";
        public string DeliveryTime
        {
            get => _deliveryTime;
            private set { if (_deliveryTime == value) return; _deliveryTime = value; OnPropertyChanged(); }
        }

        private int _sequenceNumber;
        public int SequenceNumber
        {
            get => _sequenceNumber;
            private set { if (_sequenceNumber == value) return; _sequenceNumber = value; OnPropertyChanged(); }
        }

        // ── JO context (read-only left panel) ────────────────────────────

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            private set { if (_customerName == value) return; _customerName = value; OnPropertyChanged(); }
        }

        private string _projectName = "";
        public string ProjectName
        {
            get => _projectName;
            private set { if (_projectName == value) return; _projectName = value; OnPropertyChanged(); }
        }

        private string _companyName = "";
        public string CompanyName
        {
            get => _companyName;
            private set { if (_companyName == value) return; _companyName = value; OnPropertyChanged(); }
        }

        private string _companyTrn = "";
        public string CompanyTrn
        {
            get => _companyTrn;
            private set { if (_companyTrn == value) return; _companyTrn = value; OnPropertyChanged(); }
        }

        // ── Editable fields (right panel) ────────────────────────────────

        private string _status = "Draft";
        public string Status
        {
            get => _status;
            set { if (_status == value) return; _status = value; OnPropertyChanged(); }
        }

        private bool _isFoc;
        public bool IsFoc
        {
            get => _isFoc;
            set { if (_isFoc == value) return; _isFoc = value; OnPropertyChanged(); }
        }

        private string _driverName = "";
        public string DriverName
        {
            get => _driverName;
            set { if (_driverName == value) return; _driverName = value; OnPropertyChanged(); }
        }

        private string _vehicleNumber = "";
        public string VehicleNumber
        {
            get => _vehicleNumber;
            set { if (_vehicleNumber == value) return; _vehicleNumber = value; OnPropertyChanged(); }
        }

        private string _receiverName = "";
        public string ReceiverName
        {
            get => _receiverName;
            set { if (_receiverName == value) return; _receiverName = value; OnPropertyChanged(); }
        }

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set { if (_notes == value) return; _notes = value; OnPropertyChanged(); }
        }

        // ── Status options ────────────────────────────────────────────────

        public System.Collections.Generic.List<string> StatusOptions { get; } = new()
        {
            "Draft", "Dispatched", "Delivered", "Signed", "Received"
        };

        // ── Delivery items grid ───────────────────────────────────────────

        private ObservableCollection<DnItemRowViewModel> _itemRows = new();
        public ObservableCollection<DnItemRowViewModel> ItemRows
        {
            get => _itemRows;
            private set { if (_itemRows == value) return; _itemRows = value; OnPropertyChanged(); }
        }

        // ── Qty summary ───────────────────────────────────────────────────

        private int _totalOrderedQty;
        public int TotalOrderedQty
        {
            get => _totalOrderedQty;
            private set { if (_totalOrderedQty == value) return; _totalOrderedQty = value; OnPropertyChanged(); }
        }

        private int _totalDeliveredQty;
        public int TotalDeliveredQty
        {
            get => _totalDeliveredQty;
            private set { if (_totalDeliveredQty == value) return; _totalDeliveredQty = value; OnPropertyChanged(); }
        }

        private int _totalBalanceQty;
        public int TotalBalanceQty
        {
            get => _totalBalanceQty;
            private set { if (_totalBalanceQty == value) return; _totalBalanceQty = value; OnPropertyChanged(); }
        }

        // ── Delivery history (all DNs for same JO) ───────────────────────

        private ObservableCollection<DnHistoryEntryViewModel> _historyEntries = new();
        public ObservableCollection<DnHistoryEntryViewModel> HistoryEntries
        {
            get => _historyEntries;
            private set { if (_historyEntries == value) return; _historyEntries = value; OnPropertyChanged(); }
        }

        private int _historyTotalOrdered;
        public int HistoryTotalOrdered
        {
            get => _historyTotalOrdered;
            private set { if (_historyTotalOrdered == value) return; _historyTotalOrdered = value; OnPropertyChanged(); }
        }

        private int _historyTotalDelivered;
        public int HistoryTotalDelivered
        {
            get => _historyTotalDelivered;
            private set { if (_historyTotalDelivered == value) return; _historyTotalDelivered = value; OnPropertyChanged(); }
        }

        private int _historyTotalBalance;
        public int HistoryTotalBalance
        {
            get => _historyTotalBalance;
            private set { if (_historyTotalBalance == value) return; _historyTotalBalance = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ConfirmDispatchCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteDetailViewModel()
        {
            SaveCommand = new RelayCommand(
                async _ => await SaveAsync(),
                _ => !IsSaving && !IsLoading);

            BackCommand = new RelayCommand(
                _ => OnRequestBackToList?.Invoke());

            ConfirmDispatchCommand = new RelayCommand(
                async _ => await ConfirmDispatchAsync(),
                _ => Status == "Draft" && !IsSaving);
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        public async Task LoadAsync(int deliveryNoteId)
        {
            MessageBox.Show($"LoadAsync called with id={deliveryNoteId}");

            if (App.DeliveryNotes == null)
            {
                MessageBox.Show("App.DeliveryNotes is NULL");
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = "";

                _entity = await App.DeliveryNotes.GetByIdAsync(deliveryNoteId);
                if (_entity == null)
                {
                    HasError = true;
                    ErrorMessage = "Delivery note not found.";
                    return;
                }

                _snapshot = DeliveryNoteMapper.DeserializeSnapshot(_entity);

                // ── Populate header display fields ────────────────────────
                DeliveryNoteId = _entity.DeliveryNoteId;
                JobOrderId = _entity.JobOrderId;
                ProformaId = _entity.ProformaId;
                DeliveryType = _entity.DeliveryType;
                SequenceNumber = _entity.SequenceNumber;
                DeliveryDateDisplay = _entity.DeliveryDate.ToString("dd MMM yyyy");
                DeliveryTime = _entity.DeliveryTime;

                // ── JO context from snapshot ──────────────────────────────
                var joRef = _snapshot?.JobOrderRef;
                CustomerName = joRef?.CustomerName ?? _entity.CustomerName;
                ProjectName = joRef?.ProjectName ?? _entity.ProjectName;
                CompanyName = joRef?.CompanyName ?? "";
                CompanyTrn = joRef?.CompanyTrn ?? "";

                // ── Editable fields ───────────────────────────────────────
                Status = _entity.Status;
                IsFoc = _entity.IsFoc;
                DriverName = _entity.DriverName;
                VehicleNumber = _entity.VehicleNumber;
                ReceiverName = _entity.ReceiverName;
                Notes = _entity.Notes;

                // ── Items grid ────────────────────────────────────────────
                var rows = _entity.Items
                    .OrderBy(i => i.SpecGroupNumber)
                    .ThenBy(i => i.SerialNumber)
                    .Select(i => new DnItemRowViewModel(i))
                    .ToList();

                ItemRows = new ObservableCollection<DnItemRowViewModel>(rows);

                // Wire DeliveredQty change → recalculate totals
                foreach (var row in ItemRows)
                    row.DeliveredQtyChanged += OnItemDeliveredQtyChanged;

                RecalculateTotals();

                // ── Delivery history ──────────────────────────────────────
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"LOAD FAILED:\n\n{ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}",
                    "Load Error");
                HasError = true;
                ErrorMessage = $"Failed to load delivery note: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadHistoryAsync()
        {
            if (App.DeliveryNotes == null || string.IsNullOrWhiteSpace(JobOrderId))
                return;

            var history = await App.DeliveryNotes
                .GetDeliveryHistoryAsync(JobOrderId);

            HistoryTotalOrdered = history.TotalOrdered;
            HistoryTotalDelivered = history.TotalDelivered;
            HistoryTotalBalance = history.TotalBalance;

            var entries = history.Entries
                .Select(e => new DnHistoryEntryViewModel(e))
                .ToList();

            HistoryEntries = new ObservableCollection<DnHistoryEntryViewModel>(entries);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAVE
        // ═════════════════════════════════════════════════════════════════

        public async Task SaveAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

            try
            {
                IsSaving = true;

                // Update entity from editable fields
                _entity.Status = Status;
                _entity.IsFoc = IsFoc;
                _entity.DriverName = DriverName;
                _entity.VehicleNumber = VehicleNumber;
                _entity.ReceiverName = ReceiverName;
                _entity.Notes = Notes;

                // Update items from VM rows
                foreach (var row in ItemRows)
                {
                    var item = _entity.Items.FirstOrDefault(i =>
                        i.SpecGroupNumber == row.SpecGroupNumber &&
                        i.SerialNumber == row.SerialNumber);

                    if (item == null) continue;

                    item.DeliveredQty = row.DeliveredQty;
                    item.BalanceQty = item.OrderedQty
                                        - item.PreviouslyDeliveredQty
                                        - item.DeliveredQty;
                    item.Notes = row.Notes;
                }

                // Update qty summary on entity
                _entity.TotalDeliveredQty = _entity.Items.Sum(i => i.DeliveredQty);
                _entity.TotalBalanceQty = _entity.Items.Sum(i => i.BalanceQty);

                // Refresh snapshot JSON
                var joRef = _snapshot?.JobOrderRef;
                DeliveryNoteMapper.RefreshSnapshot(_entity, joRef);

                await App.DeliveryNotes.UpdateAsync(_entity);

                MessageBox.Show(
                    $"Delivery note '{DeliveryNoteId}' saved successfully.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload history after save
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
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

        // ═════════════════════════════════════════════════════════════════
        //  CONFIRM DISPATCH
        //  Saves + changes status to Dispatched + auto-creates Dummy DN
        //  if balance qty remains.
        // ═════════════════════════════════════════════════════════════════

        private async Task ConfirmDispatchAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

            // Validate at least 1 item has DeliveredQty > 0
            bool hasDelivery = ItemRows.Any(r => r.DeliveredQty > 0);
            if (!hasDelivery)
            {
                MessageBox.Show(
                    "Cannot dispatch — no items have a delivered quantity.\n\n" +
                    "Enter the quantity delivered for at least one item.",
                    "Dispatch Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Confirm dispatch of '{DeliveryNoteId}'?\n\n" +
                "Status will change to Dispatched.\n" +
                "If balance qty remains, a Dummy DN will be auto-created.",
                "Confirm Dispatch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsSaving = true;

                // Set status to dispatched and save
                Status = "Dispatched";
                await SaveAsync();

                // Check if balance remains → create Dummy DN
                int totalBalance = _entity.Items.Sum(i => i.BalanceQty);
                if (totalBalance > 0)
                {
                    int nextSeq = await App.DeliveryNotes
                        .GetNextSequenceAsync(_entity.JobOrderId);
                    string newDnId = await App.DeliveryNotes
                        .BuildDeliveryNoteIdAsync(_entity.JobOrderId, nextSeq);

                    var dummyDn = DeliveryNoteMapper.FromBalance(
                        _entity, newDnId, nextSeq);

                    await App.DeliveryNotes.InsertAsync(dummyDn);

                    MessageBox.Show(
                        $"Dispatched successfully!\n\n" +
                        $"Balance of {totalBalance} pcs detected.\n" +
                        $"Dummy DN '{newDnId}' created in Prepared tab.",
                        "Dispatched",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Dispatched successfully! All items fully delivered.",
                        "Dispatched",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                OnRequestBackToList?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Dispatch failed:\n\n{ex.Message}",
                    "Dispatch Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────

        private void OnItemDeliveredQtyChanged()
            => RecalculateTotals();

        private void RecalculateTotals()
        {
            TotalOrderedQty = ItemRows.Sum(r => r.OrderedQty);
            TotalDeliveredQty = ItemRows.Sum(r => r.DeliveredQty);
            TotalBalanceQty = ItemRows.Sum(r => r.BalanceQty);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  DN ITEM ROW VIEWMODEL — one editable row in the items grid
    // ═════════════════════════════════════════════════════════════════════

    public class DnItemRowViewModel : ViewModelBase
    {
        public event Action? DeliveredQtyChanged;

        public int SpecGroupNumber { get; }
        public string SpecGroupName { get; }
        public int SerialNumber { get; }
        public string GlassRef { get; }
        public int W1 { get; }
        public int H1 { get; }
        public int W2 { get; }
        public int H2 { get; }
        public int OrderedQty { get; }
        public int PreviouslyDeliveredQty { get; }

        private int _deliveredQty;
        public int DeliveredQty
        {
            get => _deliveredQty;
            set
            {
                if (_deliveredQty == value) return;
                // Clamp: 0 ≤ DeliveredQty ≤ (Ordered - Previously delivered)
                int max = Math.Max(0, OrderedQty - PreviouslyDeliveredQty);
                _deliveredQty = Math.Max(0, Math.Min(value, max));
                OnPropertyChanged();
                OnPropertyChanged(nameof(BalanceQty));
                DeliveredQtyChanged?.Invoke();
            }
        }

        public int BalanceQty =>
            Math.Max(0, OrderedQty - PreviouslyDeliveredQty - DeliveredQty);

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set { if (_notes == value) return; _notes = value; OnPropertyChanged(); }
        }

        // ── Display helpers ───────────────────────────────────────────────
        public string DimensionDisplay =>
            W2 > 0 || H2 > 0
                ? $"{W1}×{H1}  /  {W2}×{H2}"
                : $"{W1}×{H1}";

        public bool HasBalance => BalanceQty > 0;

        public DnItemRowViewModel(DeliveryNoteItem item)
        {
            SpecGroupNumber = item.SpecGroupNumber;
            SpecGroupName = item.SpecGroupName;
            SerialNumber = item.SerialNumber;
            GlassRef = item.GlassRef;
            W1 = item.W1;
            H1 = item.H1;
            W2 = item.W2;
            H2 = item.H2;
            OrderedQty = item.OrderedQty;
            PreviouslyDeliveredQty = item.PreviouslyDeliveredQty;
            _deliveredQty = item.DeliveredQty;
            _notes = item.Notes;
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  DN HISTORY ENTRY VIEWMODEL — one row in history timeline
    // ═════════════════════════════════════════════════════════════════════

    public class DnHistoryEntryViewModel
    {
        public string DeliveryNoteId { get; }
        public string DeliveryType { get; }
        public string Status { get; }
        public int DeliveredQty { get; }
        public int BalanceQty { get; }
        public string DeliveryDate { get; }
        public string DriverName { get; }
        public string TypeIcon { get; }
        public string StatusBrushKey { get; }
        public bool IsCurrentDn { get; }

        public DnHistoryEntryViewModel(DeliveryHistoryEntry e)
        {
            DeliveryNoteId = e.DeliveryNoteId;
            DeliveryType = e.DeliveryType;
            Status = e.Status;
            DeliveredQty = e.DeliveredQty;
            BalanceQty = e.BalanceQty;
            DeliveryDate = e.DeliveryDate.ToString("dd MMM yyyy");
            DriverName = e.DriverName;

            TypeIcon = e.DeliveryType switch
            {
                "Rework" => "🔧",
                "Return" => "↩️",
                "Dummy" => "📌",
                _ => "📦"
            };

            var statusInfo = Models.DeliveryNote.Common.DeliveryNoteStatus
                .DeliveryNoteStatusMeta.Get(e.Status);
            StatusBrushKey = statusInfo.BrushKey;
        }
    }
}
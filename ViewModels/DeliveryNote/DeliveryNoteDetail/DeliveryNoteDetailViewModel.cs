using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.DeliveryNote.Export;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    /// <summary>
    /// ViewModel for the Delivery Note detail view.
    ///
    /// CHAT 19 CHANGES:
    ///   • IsMasterDn — drives read-only mode
    ///   • DeliveryProgressPercent — progress bar
    ///   • IsFullyDelivered — green badge
    ///   • FillRemainingCommand — SELECT ALL REMAINING
    ///   • ResetQtyCommand — RESET QTY (sets all to 0)
    ///   • CreateNewDnFromMasterCommand — fires OnRequestCreateDnFromMaster
    ///   • OnRequestCreateDnFromMaster — callback to shell VM
    ///   • Master DN hides rows where BalanceQty = 0
    ///   • ConfirmDispatchAsync calls UpdateMasterDnAsync
    /// </summary>
    public class DeliveryNoteDetailViewModel : ViewModelBase
    {
        // ── callbacks ──────────────────────────────────────────────────────
        public Action? OnRequestBackToList { get; set; }
        public Func<string, Task>? OnRequestOpenTaxInvoiceForDeliveryNote { get; set; }

        /// <summary>
        /// CHAT 19 — fires when user clicks "Create New DN from Balance"
        /// on a Master DN detail view.
        /// </summary>
        public Action<SavedDeliveryNote>? OnRequestCreateDnFromMaster { get; set; }

        // ── loaded entity + snapshot ──────────────────────────────────────
        private SavedDeliveryNote? _entity;
        private DeliveryNoteSnapshot? _snapshot;
        private readonly AttachmentService _attachmentService = new();
        private bool _isNavigatingHistory;

        // ── CHAT 19 — Master DN state ─────────────────────────────────────

        private bool _isMasterDn;
        public bool IsMasterDn
        {
            get => _isMasterDn;
            private set
            {
                if (_isMasterDn == value) return;
                _isMasterDn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotMasterDn));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsNotMasterDn => !_isMasterDn;

        // ── loading state ─────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (_isSaving == value) return;
                _isSaving = value;
                OnPropertyChanged();
            }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set
            {
                if (_hasError == value) return;
                _hasError = value;
                OnPropertyChanged();
            }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage == value) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        // ── DN header fields ─────────────────────────────────────────────

        private string _deliveryNoteId = "";
        public string DeliveryNoteId
        {
            get => _deliveryNoteId;
            private set
            {
                if (_deliveryNoteId == value) return;
                _deliveryNoteId = value;
                OnPropertyChanged();
            }
        }

        private string _jobOrderId = "";
        public string JobOrderId
        {
            get => _jobOrderId;
            private set
            {
                if (_jobOrderId == value) return;
                _jobOrderId = value;
                OnPropertyChanged();
            }
        }

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            private set
            {
                if (_proformaId == value) return;
                _proformaId = value;
                OnPropertyChanged();
            }
        }

        private string _deliveryType = "Original";
        public string DeliveryType
        {
            get => _deliveryType;
            private set
            {
                if (_deliveryType == value) return;
                _deliveryType = value;
                OnPropertyChanged();
            }
        }

        private string _deliveryDateDisplay = "";
        public string DeliveryDateDisplay
        {
            get => _deliveryDateDisplay;
            private set
            {
                if (_deliveryDateDisplay == value) return;
                _deliveryDateDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _deliveryTime = "";
        public string DeliveryTime
        {
            get => _deliveryTime;
            private set
            {
                if (_deliveryTime == value) return;
                _deliveryTime = value;
                OnPropertyChanged();
            }
        }

        private int _sequenceNumber;
        public int SequenceNumber
        {
            get => _sequenceNumber;
            private set
            {
                if (_sequenceNumber == value) return;
                _sequenceNumber = value;
                OnPropertyChanged();
            }
        }

        // ── JO context ───────────────────────────────────────────────────

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            private set
            {
                if (_customerName == value) return;
                _customerName = value;
                OnPropertyChanged();
            }
        }

        private string _projectName = "";
        public string ProjectName
        {
            get => _projectName;
            private set
            {
                if (_projectName == value) return;
                _projectName = value;
                OnPropertyChanged();
            }
        }

        private string _companyName = "";
        public string CompanyName
        {
            get => _companyName;
            private set
            {
                if (_companyName == value) return;
                _companyName = value;
                OnPropertyChanged();
            }
        }

        private string _companyTrn = "";
        public string CompanyTrn
        {
            get => _companyTrn;
            private set
            {
                if (_companyTrn == value) return;
                _companyTrn = value;
                OnPropertyChanged();
            }
        }

        // ── Editable fields ──────────────────────────────────────────────

        private string _status = "Draft";
        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isFoc;
        public bool IsFoc
        {
            get => _isFoc;
            set
            {
                if (_isFoc == value) return;
                _isFoc = value;
                OnPropertyChanged();
            }
        }

        private string _driverName = "";
        public string DriverName
        {
            get => _driverName;
            set
            {
                if (_driverName == value) return;
                _driverName = value;
                OnPropertyChanged();
            }
        }

        private string _vehicleNumber = "";
        public string VehicleNumber
        {
            get => _vehicleNumber;
            set
            {
                if (_vehicleNumber == value) return;
                _vehicleNumber = value;
                OnPropertyChanged();
            }
        }

        private string _receiverName = "";
        public string ReceiverName
        {
            get => _receiverName;
            set
            {
                if (_receiverName == value) return;
                _receiverName = value;
                OnPropertyChanged();
            }
        }

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set
            {
                if (_notes == value) return;
                _notes = value;
                OnPropertyChanged();
            }
        }

        public System.Collections.Generic.List<string> StatusOptions { get; } = new()
        {
            "Draft", "Dispatched", "Delivered", "Signed", "Received"
        };

        // ── Delivery items grid ───────────────────────────────────────────

        private ObservableCollection<DnItemRowViewModel> _itemRows = new();
        public ObservableCollection<DnItemRowViewModel> ItemRows
        {
            get => _itemRows;
            private set
            {
                if (_itemRows == value) return;
                _itemRows = value;
                OnPropertyChanged();
                RebuildGroupedView();
            }
        }

        private ICollectionView? _groupedItemsView;
        public ICollectionView? GroupedItemsView
        {
            get => _groupedItemsView;
            private set
            {
                if (_groupedItemsView == value) return;
                _groupedItemsView = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// CHAT 19: On Master DN — filter hides rows where BalanceQty = 0
        /// (fully delivered items not shown — only pending balance items).
        /// On real DN — all rows shown (user needs to enter qty).
        /// </summary>
        private void RebuildGroupedView()
        {
            var view = CollectionViewSource.GetDefaultView(_itemRows);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(
                new PropertyGroupDescription(
                    nameof(DnItemRowViewModel.SpecGroupName)));

            // CHAT 19: Master DN hides fully delivered rows
            if (_isMasterDn)
            {
                view.Filter = obj =>
                    obj is DnItemRowViewModel row && row.BalanceQty > 0;
            }
            else
            {
                view.Filter = null;
            }

            GroupedItemsView = view;
        }

        // ── Qty summary ───────────────────────────────────────────────────

        private int _totalOrderedQty;
        public int TotalOrderedQty
        {
            get => _totalOrderedQty;
            private set
            {
                if (_totalOrderedQty == value) return;
                _totalOrderedQty = value;
                OnPropertyChanged();
            }
        }

        private int _totalPreviouslyDeliveredQty;
        public int TotalPreviouslyDeliveredQty
        {
            get => _totalPreviouslyDeliveredQty;
            private set
            {
                if (_totalPreviouslyDeliveredQty == value) return;
                _totalPreviouslyDeliveredQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryProgressPercent));
            }
        }

        private int _totalDeliveredQty;
        public int TotalDeliveredQty
        {
            get => _totalDeliveredQty;
            private set
            {
                if (_totalDeliveredQty == value) return;
                _totalDeliveredQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryProgressPercent));
            }
        }

        private int _totalBalanceQty;
        public int TotalBalanceQty
        {
            get => _totalBalanceQty;
            private set
            {
                if (_totalBalanceQty == value) return;
                _totalBalanceQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryProgressPercent));
                OnPropertyChanged(nameof(IsFullyDelivered));
            }
        }

        public double DeliveryProgressPercent
        {
            get
            {
                if (TotalOrderedQty == 0) return 0;
                int delivered = TotalPreviouslyDeliveredQty + TotalDeliveredQty;
                return Math.Min(100.0,
                    (double)delivered / TotalOrderedQty * 100.0);
            }
        }

        public bool IsFullyDelivered =>
            TotalOrderedQty > 0 && TotalBalanceQty == 0;

        // ── Delivery history ──────────────────────────────────────────────

        private ObservableCollection<DnHistoryEntryViewModel> _historyEntries = new();
        public ObservableCollection<DnHistoryEntryViewModel> HistoryEntries
        {
            get => _historyEntries;
            private set
            {
                if (_historyEntries == value) return;
                _historyEntries = value;
                OnPropertyChanged();
            }
        }

        private int _historyTotalOrdered;
        public int HistoryTotalOrdered
        {
            get => _historyTotalOrdered;
            private set
            {
                if (_historyTotalOrdered == value) return;
                _historyTotalOrdered = value;
                OnPropertyChanged();
            }
        }

        private int _historyTotalDelivered;
        public int HistoryTotalDelivered
        {
            get => _historyTotalDelivered;
            private set
            {
                if (_historyTotalDelivered == value) return;
                _historyTotalDelivered = value;
                OnPropertyChanged();
            }
        }

        private int _historyTotalBalance;
        public int HistoryTotalBalance
        {
            get => _historyTotalBalance;
            private set
            {
                if (_historyTotalBalance == value) return;
                _historyTotalBalance = value;
                OnPropertyChanged();
            }
        }

        // ── Selected history entry ────────────────────────────────────────

        private DnHistoryEntryViewModel? _selectedHistoryEntry;
        public DnHistoryEntryViewModel? SelectedHistoryEntry
        {
            get => _selectedHistoryEntry;
            set
            {
                if (_selectedHistoryEntry == value) return;
                if (_selectedHistoryEntry != null)
                    _selectedHistoryEntry.IsSelected = false;
                _selectedHistoryEntry = value;
                if (_selectedHistoryEntry != null)
                    _selectedHistoryEntry.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDnHeaderText));
                OnPropertyChanged(nameof(IsSelectedDnCurrent));
            }
        }

        public bool IsSelectedDnCurrent =>
            _selectedHistoryEntry == null ||
            string.Equals(
                _selectedHistoryEntry.DeliveryNoteId,
                _deliveryNoteId,
                StringComparison.OrdinalIgnoreCase);

        public string SelectedDnHeaderText =>
            _selectedHistoryEntry != null
                ? $"ATTACHMENTS ({_selectedHistoryEntry.AttachmentCount})"
                : $"ATTACHMENTS ({AttachmentsCount})";

        public ICommand SelectHistoryEntryCommand { get; }

        // ── Attachments ───────────────────────────────────────────────────

        private ObservableCollection<DnAttachmentRowViewModel> _attachments = new();
        public ObservableCollection<DnAttachmentRowViewModel> Attachments
        {
            get => _attachments;
            private set
            {
                if (_attachments == value) return;
                _attachments = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AttachmentsCount));
                OnPropertyChanged(nameof(HasNoAttachments));
                OnPropertyChanged(nameof(HasAttachments));
                OnPropertyChanged(nameof(SelectedDnHeaderText));
            }
        }

        public int AttachmentsCount => Attachments?.Count ?? 0;
        public bool HasNoAttachments => AttachmentsCount == 0;
        public bool HasAttachments => AttachmentsCount > 0;

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand MakeDnCommand { get; }
        public ICommand DeleteDnCommand { get; }
        public ICommand UploadImageCommand { get; }
        public ICommand UploadPdfCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ConvertToTaxInvoiceCommand { get; }

        /// <summary>
        /// CHAT 19 — SELECT ALL REMAINING.
        /// Sets DeliveredQty = BalanceQty for every item where BalanceQty > 0.
        /// REMINDER: fills ALL remaining pcs.
        /// Hidden on Master DN.
        /// </summary>
        public ICommand FillRemainingCommand { get; }

        /// <summary>
        /// CHAT 19 — RESET QTY.
        /// Sets DeliveredQty = 0 for ALL items.
        /// Companion to FillRemainingCommand — lets user start over.
        /// Hidden on Master DN.
        /// </summary>
        public ICommand ResetQtyCommand { get; }

        /// <summary>
        /// CHAT 19 — shown only on Master DN detail view.
        /// Fires OnRequestCreateDnFromMaster → shell VM creates Draft DN.
        /// </summary>
        public ICommand CreateNewDnFromMasterCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteDetailViewModel()
        {
            SaveCommand = new RelayCommand(
                async _ => await SaveAsync(),
                _ => !IsSaving && !IsLoading && !IsMasterDn);

            BackCommand = new RelayCommand(
                _ => OnRequestBackToList?.Invoke());

            MakeDnCommand = new RelayCommand(
                async _ => await ConfirmDispatchAsync(),
                _ => Status == "Draft" && !IsSaving && !IsMasterDn);

            DeleteDnCommand = new RelayCommand(
                async _ => await DeleteDnAsync(),
                _ => _entity != null && !IsSaving && !IsMasterDn);

            UploadImageCommand = new RelayCommand(
                async _ => await UploadImageAsync(),
                _ => _entity != null && !IsSaving && !IsMasterDn);

            UploadPdfCommand = new RelayCommand(
                async _ => await UploadPdfAsync(),
                _ => _entity != null && !IsSaving && !IsMasterDn);

            OpenAttachmentCommand = new RelayCommand(param =>
            {
                if (param is DnAttachmentRowViewModel row)
                    OpenAttachment(row);
            });

            DeleteAttachmentCommand = new RelayCommand(
                async param =>
                {
                    if (param is DnAttachmentRowViewModel row)
                        await DeleteAttachmentAsync(row);
                },
                canExecute: param =>
                    param is DnAttachmentRowViewModel && !IsMasterDn);

            SelectHistoryEntryCommand = new RelayCommand(async param =>
            {
                if (param is DnHistoryEntryViewModel entry)
                    await NavigateToDnAsync(entry.DeliveryNoteId);
            });

            ExportPdfCommand = new RelayCommand(
                async _ => await ExportPdfAsync(),
                _ => _entity != null && !IsSaving && !IsLoading);

            ExportExcelCommand = new RelayCommand(
                async _ => await ExportExcelAsync(),
                _ => _entity != null && !IsSaving && !IsLoading);

            ExportCsvCommand = new RelayCommand(
                async _ => await ExportCsvAsync(),
                _ => _entity != null && !IsSaving && !IsLoading);

            ConvertToTaxInvoiceCommand = new RelayCommand(
                execute: param => ConvertToTaxInvoiceAsync(
                    param as DnHistoryEntryViewModel),
                canExecute: param => CanConvertToTaxInvoice(
                    param as DnHistoryEntryViewModel));

            // ═══════════════════════════════════════════════════════════
            //  CHAT 19 — FillRemainingCommand
            //  SELECT ALL REMAINING: fills balance qty into DeliveredQty
            //  REMINDER: balance pcs = ALL remaining pcs for that item
            // ═══════════════════════════════════════════════════════════
            FillRemainingCommand = new RelayCommand(
                _ => FillAllRemaining(),
                _ => !IsMasterDn &&
                     !IsSaving &&
                     _itemRows.Any(r => r.BalanceQty > 0));

            // ═══════════════════════════════════════════════════════════
            //  CHAT 19 — ResetQtyCommand
            //  RESET QTY: sets DeliveredQty = 0 for ALL items
            //  Companion to FillRemainingCommand — lets user start over
            // ═══════════════════════════════════════════════════════════
            ResetQtyCommand = new RelayCommand(
                _ => ResetAllQty(),
                _ => !IsMasterDn &&
                     !IsSaving &&
                     _itemRows.Any(r => r.DeliveredQty > 0));

            // CHAT 19 — Create new real DN from Master DN balance
            CreateNewDnFromMasterCommand = new RelayCommand(
                _ =>
                {
                    if (_entity != null)
                        OnRequestCreateDnFromMaster?.Invoke(_entity);
                },
                _ => IsMasterDn &&
                     !IsSaving &&
                     _entity != null &&
                     _itemRows.Any(r => r.BalanceQty > 0));
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 19 — FILL ALL REMAINING
        //  REMINDER: balance pcs = ALL remaining pcs for that item.
        //  SR.1 balance=5 → fills 5. SR.2 balance=0 → skipped.
        // ═════════════════════════════════════════════════════════════════

        private void FillAllRemaining()
        {
            foreach (var row in _itemRows)
            {
                if (row.BalanceQty > 0)
                    row.DeliveredQty = row.BalanceQty;
            }
            RecalculateTotals();
            CommandManager.InvalidateRequerySuggested();
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 19 — RESET ALL QTY
        //  Sets DeliveredQty = 0 for ALL items.
        //  User can start entering quantities fresh.
        // ═════════════════════════════════════════════════════════════════

        private void ResetAllQty()
        {
            foreach (var row in _itemRows)
                row.DeliveredQty = 0;

            RecalculateTotals();
            CommandManager.InvalidateRequerySuggested();
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATE TO SIBLING DN
        // ═════════════════════════════════════════════════════════════════

        private async Task NavigateToDnAsync(string targetDeliveryNoteId)
        {
            if (App.DeliveryNotes == null) return;
            if (_isNavigatingHistory) return;

            if (string.Equals(targetDeliveryNoteId, _deliveryNoteId,
                StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                _isNavigatingHistory = true;

                var target = await App.DeliveryNotes
                    .GetByDeliveryNoteIdAsync(targetDeliveryNoteId);

                if (target == null)
                {
                    MessageBox.Show(
                        $"Delivery note '{targetDeliveryNoteId}' not found.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await LoadAsync(target.Id);
            }
            finally
            {
                _isNavigatingHistory = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        public async Task LoadAsync(int deliveryNoteId)
        {
            if (App.DeliveryNotes == null)
            {
                HasError = true;
                ErrorMessage = "Delivery Notes database not initialized.";
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

                DeliveryNoteId = _entity.DeliveryNoteId;
                JobOrderId = _entity.JobOrderId;
                ProformaId = _entity.ProformaId;
                DeliveryType = _entity.DeliveryType;
                SequenceNumber = _entity.SequenceNumber;
                DeliveryDateDisplay =
                    _entity.DeliveryDate.ToString("dd MMM yyyy");
                DeliveryTime = _entity.DeliveryTime;

                // CHAT 19: set Master DN flag BEFORE RebuildGroupedView
                IsMasterDn = _entity.IsMasterDn;

                var joRef = _snapshot?.JobOrderRef;
                CustomerName = joRef?.CustomerName ?? _entity.CustomerName;
                ProjectName = joRef?.ProjectName ?? _entity.ProjectName;
                CompanyName = joRef?.CompanyName ?? "";
                CompanyTrn = joRef?.CompanyTrn ?? "";

                Status = _entity.Status;
                IsFoc = _entity.IsFoc;
                DriverName = _entity.DriverName;
                VehicleNumber = _entity.VehicleNumber;
                ReceiverName = _entity.ReceiverName;
                Notes = _entity.Notes;

                // Heal PREV only for real DNs
                if (!_entity.IsMasterDn)
                    await HealItemPrevQtyAsync(_entity);

                // BUG-42: unsubscribe from old rows
                foreach (var oldRow in _itemRows)
                    oldRow.DeliveredQtyChanged -= OnItemDeliveredQtyChanged;

                var rows = _entity.Items
                    .OrderBy(i => i.SpecGroupNumber)
                    .ThenBy(i => i.SerialNumber)
                    .Select(i => new DnItemRowViewModel(i))
                    .ToList();

                ItemRows = new ObservableCollection<DnItemRowViewModel>(rows);
                // RebuildGroupedView called by ItemRows setter
                // Filter applied based on IsMasterDn flag already set above

                foreach (var row in ItemRows)
                    row.DeliveredQtyChanged += OnItemDeliveredQtyChanged;

                RecalculateTotals();
                await LoadHistoryAsync();

                if (!_entity.IsMasterDn)
                {
                    var currentInHistory = HistoryEntries
                        .FirstOrDefault(h => string.Equals(
                            h.DeliveryNoteId, DeliveryNoteId,
                            StringComparison.OrdinalIgnoreCase));

                    _selectedHistoryEntry = currentInHistory;
                    if (currentInHistory != null)
                        currentInHistory.IsSelected = true;

                    OnPropertyChanged(nameof(SelectedHistoryEntry));
                    OnPropertyChanged(nameof(IsSelectedDnCurrent));
                    OnPropertyChanged(nameof(SelectedDnHeaderText));
                }

                await LoadAttachmentsDirectlyAsync(DeliveryNoteId);

                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load delivery note: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task HealItemPrevQtyAsync(SavedDeliveryNote entity)
        {
            if (App.DeliveryNotes == null) return;

            var prevMap = await App.DeliveryNotes
                .GetPreviouslyDeliveredMapAsync(
                    entity.JobOrderId,
                    entity.DeliveryNoteId);

            foreach (var item in entity.Items)
            {
                var key = $"{item.SpecGroupNumber}|{item.SerialNumber}";
                item.PreviouslyDeliveredQty =
                    prevMap.TryGetValue(key, out var prev) ? prev : 0;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD HISTORY
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadHistoryAsync()
        {
            if (App.DeliveryNotes == null ||
                string.IsNullOrWhiteSpace(JobOrderId)) return;

            var history = await App.DeliveryNotes
                .GetDeliveryHistoryAsync(JobOrderId);

            HistoryTotalOrdered = history.TotalOrdered;
            HistoryTotalDelivered = history.TotalDelivered;
            HistoryTotalBalance = history.TotalBalance;

            var siblingIds = history.Entries
                .Select(e => e.DeliveryNoteId).ToList();
            var counts = await App.DeliveryNotes
                .GetAttachmentCountsAsync(siblingIds);

            var entries = history.Entries.Select(e =>
            {
                var vm = new DnHistoryEntryViewModel(e);
                vm.AttachmentCount = counts.TryGetValue(
                    e.DeliveryNoteId, out var c) ? c : 0;
                vm.IsCurrentDn = string.Equals(
                    e.DeliveryNoteId, DeliveryNoteId,
                    StringComparison.OrdinalIgnoreCase);
                if (vm.IsCurrentDn) vm.IsSelected = true;
                return vm;
            })
            .OrderByDescending(vm => vm.SequenceNumber)
            .ToList();

            HistoryEntries =
                new ObservableCollection<DnHistoryEntryViewModel>(entries);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAVE (disabled for Master DN)
        // ═════════════════════════════════════════════════════════════════

        public async Task SaveAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.DeliveryNoteEdit, "save a delivery note"))
                return;

            if (_entity == null || App.DeliveryNotes == null) return;
            if (_entity.IsMasterDn) return;

            bool hasAnyDelivery = ItemRows.Any(r => r.DeliveredQty > 0);
            if (!hasAnyDelivery)
            {
                var warn = MessageBox.Show(
                    "No items have a delivered quantity — all quantities are 0.\n\n" +
                    "Are you sure you want to save with zero deliveries?\n\n" +
                    "  Yes → Save anyway\n  No → Go back and enter quantities",
                    "Zero Delivered Qty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warn != MessageBoxResult.Yes) return;
            }

            try
            {
                IsSaving = true;

                _entity.Status = Status;
                _entity.IsFoc = IsFoc;
                _entity.DriverName = DriverName;
                _entity.VehicleNumber = VehicleNumber;
                _entity.ReceiverName = ReceiverName;
                _entity.Notes = Notes;

                foreach (var row in ItemRows)
                {
                    var item = _entity.Items.FirstOrDefault(i =>
                        i.SpecGroupNumber == row.SpecGroupNumber &&
                        i.SerialNumber == row.SerialNumber);
                    if (item == null) continue;

                    item.DeliveredQty = row.DeliveredQty;
                    item.BalanceQty = Math.Max(0,
                        item.OrderedQty
                        - item.PreviouslyDeliveredQty
                        - item.DeliveredQty);
                    item.Notes = row.Notes;
                }

                _entity.TotalOrderedQty = _entity.Items.Sum(i => i.OrderedQty);
                _entity.TotalDeliveredQty =
                    _entity.Items.Sum(i => i.DeliveredQty);
                _entity.TotalBalanceQty =
                    _entity.Items.Sum(i => i.BalanceQty);
                _entity.TotalPreviouslyDeliveredQty =
                    _entity.Items.Sum(i => i.PreviouslyDeliveredQty);

                var joRef = _snapshot?.JobOrderRef;
                DeliveryNoteMapper.RefreshSnapshot(_entity, joRef);

                await App.DeliveryNotes.UpdateAsync(_entity);
                await LoadAsync(_entity.Id);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Delivery note saved",
                    $"{DeliveryNoteId} — status {Status}",
                    "DN", DeliveryNoteId);

                MessageBox.Show(
                    $"Delivery note '{DeliveryNoteId}' saved successfully.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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
        //  MAKE DN — CHAT 19 FIXED
        //  Calls UpdateMasterDnAsync instead of creating new Dummy DN
        // ═════════════════════════════════════════════════════════════════

        private async Task ConfirmDispatchAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.DeliveryNoteEdit, "dispatch a delivery note"))
                return;

            if (_entity == null || App.DeliveryNotes == null) return;
            if (_entity.IsMasterDn) return;

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
                "The Master DN will be updated with new balance quantities.",
                "Confirm Dispatch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsSaving = true;
                Status = "Dispatched";

                _entity.Status = Status;
                _entity.IsFoc = IsFoc;
                _entity.DriverName = DriverName;
                _entity.VehicleNumber = VehicleNumber;
                _entity.ReceiverName = ReceiverName;
                _entity.Notes = Notes;

                foreach (var row in ItemRows)
                {
                    var item = _entity.Items.FirstOrDefault(i =>
                        i.SpecGroupNumber == row.SpecGroupNumber &&
                        i.SerialNumber == row.SerialNumber);
                    if (item == null) continue;

                    item.DeliveredQty = row.DeliveredQty;
                    item.BalanceQty = Math.Max(0,
                        item.OrderedQty
                        - item.PreviouslyDeliveredQty
                        - item.DeliveredQty);
                    item.Notes = row.Notes;
                }

                _entity.TotalOrderedQty = _entity.Items.Sum(i => i.OrderedQty);
                _entity.TotalDeliveredQty =
                    _entity.Items.Sum(i => i.DeliveredQty);
                _entity.TotalBalanceQty =
                    _entity.Items.Sum(i => i.BalanceQty);
                _entity.TotalPreviouslyDeliveredQty =
                    _entity.Items.Sum(i => i.PreviouslyDeliveredQty);

                var joRef = _snapshot?.JobOrderRef;
                DeliveryNoteMapper.RefreshSnapshot(_entity, joRef);
                await App.DeliveryNotes.UpdateAsync(_entity);

                // CHAT 19: update Master DN — no new Dummy DN
                await App.DeliveryNotes.UpdateMasterDnAsync(
                    _entity.JobOrderId,
                    _entity.DeliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Delivery dispatched",
                    $"{_entity.DeliveryNoteId} dispatched",
                    "DN", _entity.DeliveryNoteId);

                int totalBalance = _entity.Items.Sum(i => i.BalanceQty);

                // ── FULLY DELIVERED ───────────────────────────────────────────
                if (totalBalance <= 0)
                {
                    MessageBox.Show(
                        "✅  Dispatched successfully!\n\n" +
                        "All items fully delivered.\n" +
                        "Master DN updated to complete.",
                        "Dispatched",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    OnRequestBackToList?.Invoke();
                    return;
                }

                // ── BALANCE REMAINING — offer to create next DN ───────────────
                int deliveredThisDn = _entity.Items.Sum(i => i.DeliveredQty);
                int balanceItemCount = _entity.Items.Count(i => i.BalanceQty > 0);

                var nextDn = MessageBox.Show(
                    $"✅  Dispatched successfully!\n\n" +
                    $"This delivery     :  {deliveredThisDn} pcs\n" +
                    $"Balance remaining :  {totalBalance} pcs " +
                    $"({balanceItemCount} items)\n\n" +
                    "Would you like to create the next delivery note now?\n\n" +
                    "  Yes  →  Create next DN pre-filled with balance\n" +
                    "           (use SELECT ALL REMAINING to fill instantly)\n" +
                    "  No   →  Go back to list",
                    "Dispatched — Balance Remaining",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (nextDn == MessageBoxResult.Yes)
                {
                    // Load fresh Master DN with updated balances
                    var masterDn = await App.DeliveryNotes
                        .GetMasterDnForJobOrderAsync(_entity.JobOrderId);

                    if (masterDn != null &&
                        masterDn.Items.Any(i => i.BalanceQty > 0))
                    {
                        // Fire callback — shell VM creates next DN from balance
                        OnRequestCreateDnFromMaster?.Invoke(masterDn);
                    }
                    else
                    {
                        // Fallback — master not found, go to list
                        MessageBox.Show(
                            "Could not load Master DN.\n\n" +
                            "Use 'Create DN' from the list to create next delivery.",
                            "Master DN Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        OnRequestBackToList?.Invoke();
                    }
                }
                else
                {
                    OnRequestBackToList?.Invoke();
                }
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

        // ═════════════════════════════════════════════════════════════════
        //  DELETE DN (guarded for Master DN)
        // ═════════════════════════════════════════════════════════════════

        private async Task DeleteDnAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;
            if (_entity.IsMasterDn) return;

            var result = MessageBox.Show(
                $"Delete delivery note '{DeliveryNoteId}'?\n\n" +
                $"Customer : {CustomerName}\n" +
                $"Project  : {ProjectName}\n" +
                $"Status   : {Status}\n\n" +
                "This will permanently delete the DN and all attached files.\n" +
                "This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsSaving = true;

                var freshAttachments = await App.DeliveryNotes
                    .GetAttachmentsAsync(_entity.DeliveryNoteId);

                foreach (var att in freshAttachments)
                {
                    try { _attachmentService.DeleteFile(att.FilePath); }
                    catch { /* file may already be gone */ }
                }

                await App.DeliveryNotes.DeleteAsync(_entity.Id);

                MessageBox.Show(
                    $"Delivery note '{DeliveryNoteId}' deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                OnRequestBackToList?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete:\n\n{ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAX INVOICE
        // ═════════════════════════════════════════════════════════════════

        private bool CanConvertToTaxInvoice(DnHistoryEntryViewModel? entry)
        {
            if (_entity == null || IsSaving || IsMasterDn) return false;
            if (string.IsNullOrWhiteSpace(ProformaId)) return false;

            // CHAT 19 INVOICED: already invoiced DNs cannot be re-invoiced
            // Check the specific history entry's invoiced state
            if (entry != null && entry.HasTaxInvoice) return false;

            // If no specific entry, check the currently loaded DN
            if (entry == null && _entity.HasTaxInvoice) return false;

            var targetStatus = entry?.Status ?? Status;
            return targetStatus == "Dispatched"
                || targetStatus == "Delivered"
                || targetStatus == "Signed"
                || targetStatus == "Received";
        }

        private async void ConvertToTaxInvoiceAsync(
            DnHistoryEntryViewModel? entry)
        {
            if (string.IsNullOrWhiteSpace(ProformaId))
            {
                MessageBox.Show(
                    "This delivery note has no linked Proforma Invoice.",
                    "No PI Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForDeliveryNote == null)
            {
                MessageBox.Show(
                    "Tax Invoice navigation is not wired up.",
                    "Not Wired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var targetDnId = entry?.DeliveryNoteId ?? DeliveryNoteId;
            var targetStatus = entry?.Status ?? Status;
            var targetQty = entry?.DeliveredQty ?? TotalDeliveredQty;

            if (string.IsNullOrWhiteSpace(targetDnId))
            {
                MessageBox.Show(
                    "Cannot determine which DN to invoice.",
                    "No DN Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (targetQty <= 0)
            {
                MessageBox.Show(
                    $"DN '{targetDnId}' has 0 delivered qty.",
                    "Nothing to Invoice",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Create Tax Invoice for this specific delivery?\n\n" +
                $"DN        : {targetDnId}\n" +
                $"Status    : {targetStatus}\n" +
                $"Delivered : {targetQty} pcs (THIS DN only)\n" +
                $"PI        : {ProformaId}\n" +
                $"Customer  : {CustomerName}",
                "Convert Single DN to Tax Invoice",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.OK) return;

            try
            {
                await OnRequestOpenTaxInvoiceForDeliveryNote(targetDnId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open Tax Invoice:\n\n{ex.Message}",
                    "Navigation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORT
        // ═════════════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;
            var layout = new DeliveryNotePdfLayout(entity, snapshot);
            var engine = new PdfExportEngine();
            ShowExportResult(
                await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    }), "PDF");
        }

        private async Task ExportExcelAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;
            var layout = new DeliveryNoteExcelLayout(entity, snapshot);
            var engine = new ExcelExportEngine();
            ShowExportResult(
                await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    }), "Excel");
        }

        private async Task ExportCsvAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;
            var layout = new DeliveryNoteCsvLayout(entity, snapshot);
            var engine = new CsvExportEngine();
            ShowExportResult(
                await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    }), "CSV");
        }

        private bool EnsureLoaded(
            out SavedDeliveryNote entity,
            out DeliveryNoteSnapshot snapshot)
        {
            if (_entity == null)
            {
                entity = null!;
                snapshot = null!;
                MessageBox.Show(
                    "Load a delivery note before exporting.",
                    "Nothing to export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            entity = _entity;
            snapshot = _snapshot ?? new DeliveryNoteSnapshot
            {
                Header = new DnHeaderSnapshot
                {
                    DeliveryNoteId = _entity.DeliveryNoteId,
                    JobOrderId = _entity.JobOrderId,
                    Status = _entity.Status,
                    DeliveryType = _entity.DeliveryType,
                    DeliveryDate = _entity.DeliveryDate,
                    DeliveryTime = _entity.DeliveryTime
                },
                JobOrderRef = new DnJobOrderRefSnapshot
                {
                    CustomerName = _entity.CustomerName,
                    ProjectName = _entity.ProjectName
                }
            };
            return true;
        }

        private static void ShowExportResult(
            ExportResult result, string formatLabel)
        {
            if (result.Success || result.Cancelled) return;
            MessageBox.Show(
                $"{formatLabel} export failed:\n\n{result.ErrorMessage}",
                "Export Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // ═════════════════════════════════════════════════════════════════
        //  ATTACHMENTS
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadAttachmentsDirectlyAsync(string deliveryNoteId)
        {
            if (App.DeliveryNotes == null ||
                string.IsNullOrWhiteSpace(deliveryNoteId)) return;

            var list = await App.DeliveryNotes
                .GetAttachmentsAsync(deliveryNoteId);
            var rows = list
                .Select(a => new DnAttachmentRowViewModel(a)).ToList();
            Attachments =
                new ObservableCollection<DnAttachmentRowViewModel>(rows);

            var historyRow = HistoryEntries.FirstOrDefault(h =>
                string.Equals(h.DeliveryNoteId, deliveryNoteId,
                    StringComparison.OrdinalIgnoreCase));
            if (historyRow != null)
                historyRow.AttachmentCount = rows.Count;

            OnPropertyChanged(nameof(SelectedDnHeaderText));
        }

        private async Task UploadImageAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select image to attach",
                Filter = AttachmentLimits.ImageDialogFilter,
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (!_attachmentService.IsValidImageExtension(dlg.FileName))
                {
                    MessageBox.Show(
                        "Unsupported image format.\n\n" +
                        "Allowed: JPG, PNG, BMP, GIF, WEBP.",
                        "Invalid File",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!_attachmentService.IsUnderSizeLimit(dlg.FileName))
                {
                    MessageBox.Show(
                        $"File is too large.\n\n" +
                        $"Maximum: {AttachmentLimits.MaxFileSizeDisplay}",
                        "File Too Large",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var origName = Path.GetFileName(dlg.FileName);
                var renameDlg = new RenameAttachmentDialog(origName)
                {
                    Owner = Application.Current.MainWindow
                };
                if (renameDlg.ShowDialog() != true) return;

                IsSaving = true;
                var entity = await _attachmentService
                    .CopyImageAsync(
                        dlg.FileName, _deliveryNoteId,
                        renameDlg.ResultFileName);
                await App.DeliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Attachment added",
                    $"Image '{entity.FileName}' → {_deliveryNoteId}",
                    "DN", _deliveryNoteId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to upload image:\n\n{ex.Message}",
                    "Upload Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsSaving = false; }
        }

        private async Task UploadPdfAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select PDF to attach",
                Filter = AttachmentLimits.PdfDialogFilter,
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (!_attachmentService.IsValidPdfExtension(dlg.FileName))
                {
                    MessageBox.Show(
                        "Only PDF files are allowed in this slot.",
                        "Invalid File",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!_attachmentService.IsUnderSizeLimit(dlg.FileName))
                {
                    MessageBox.Show(
                        $"File is too large.\n\n" +
                        $"Maximum: {AttachmentLimits.MaxFileSizeDisplay}",
                        "File Too Large",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var origName = Path.GetFileName(dlg.FileName);
                var renameDlg = new RenameAttachmentDialog(origName)
                {
                    Owner = Application.Current.MainWindow
                };
                if (renameDlg.ShowDialog() != true) return;

                IsSaving = true;
                var entity = await _attachmentService
                    .CopyPdfAsync(
                        dlg.FileName, _deliveryNoteId,
                        renameDlg.ResultFileName);
                await App.DeliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Attachment added",
                    $"PDF '{entity.FileName}' → {_deliveryNoteId}",
                    "DN", _deliveryNoteId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to upload PDF:\n\n{ex.Message}",
                    "Upload Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsSaving = false; }
        }

        private void OpenAttachment(DnAttachmentRowViewModel row)
        {
            try { _attachmentService.OpenInDefaultApp(row.FilePath); }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open file:\n\n{ex.Message}",
                    "Open Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task DeleteAttachmentAsync(DnAttachmentRowViewModel row)
        {
            if (App.DeliveryNotes == null) return;

            var result = MessageBox.Show(
                $"Delete attachment '{row.FileName}'?\n\n" +
                "This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await App.DeliveryNotes.DeleteAttachmentAsync(row.Id);
                _attachmentService.DeleteFile(row.FilePath);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Attachment deleted",
                    $"'{row.FileName}' removed from {_deliveryNoteId}",
                    "DN", _deliveryNoteId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete attachment:\n\n{ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────

        private void OnItemDeliveredQtyChanged() => RecalculateTotals();

        private void RecalculateTotals()
        {
            TotalOrderedQty = ItemRows.Sum(r => r.OrderedQty);
            TotalPreviouslyDeliveredQty =
                ItemRows.Sum(r => r.PreviouslyDeliveredQty);
            TotalDeliveredQty = ItemRows.Sum(r => r.DeliveredQty);
            TotalBalanceQty = ItemRows.Sum(r => r.BalanceQty);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  DN ITEM ROW VIEWMODEL
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
                int max = Math.Max(0, OrderedQty - PreviouslyDeliveredQty);
                _deliveredQty = Math.Max(0, Math.Min(value, max));
                OnPropertyChanged();
                OnPropertyChanged(nameof(BalanceQty));
                OnPropertyChanged(nameof(HasBalance));
                DeliveredQtyChanged?.Invoke();
            }
        }

        public int BalanceQty =>
            Math.Max(0, OrderedQty - PreviouslyDeliveredQty - DeliveredQty);

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set
            {
                if (_notes == value) return;
                _notes = value;
                OnPropertyChanged();
            }
        }

        public string DimensionDisplay =>
            W2 > 0 || H2 > 0
                ? $"{W1}×{H1}  /  {W2}×{H2}"
                : $"{W1}×{H1}";

        public bool HasBalance => BalanceQty > 0;
        public decimal Sqm => SqmCalculator.CalculatePanel(W1, H1);
        public decimal TotalSqm =>
            SqmCalculator.CalculateTotal(W1, H1, OrderedQty);

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
    //  DN HISTORY ENTRY VIEWMODEL
    //
    //  CHAT 19 INVOICED:
    //    HasTaxInvoice    — true when this DN has been invoiced
    //    TaxInvoiceId     — Tax Invoice reference e.g. "TI-2026-001"
    //    IsInvoiced       — convenience alias for HasTaxInvoice
    //    InvoicedBadgeText — "✅ TI-2026-001" or "" for XAML binding
    //    CanInvoice       — false when already invoiced or status not eligible
    // ═════════════════════════════════════════════════════════════════════

    public class DnHistoryEntryViewModel : ViewModelBase
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
        public int OrderedQty { get; }
        public int CumulativePrevQty { get; }
        public int CumulativeBalanceQty { get; }
        public int SequenceNumber { get; }

        // ── CHAT 19 INVOICED ─────────────────────────────────────────────

        /// <summary>
        /// True when this DN has been converted to a Tax Invoice.
        /// Populated from DeliveryHistoryEntry.HasTaxInvoice which is
        /// set by DeliveryNoteRepository.GetDeliveryHistoryAsync().
        /// Drives the invoiced badge in the history sidebar row.
        /// </summary>
        public bool HasTaxInvoice { get; }

        /// <summary>
        /// The Tax Invoice reference linked to this DN.
        /// e.g. "TI-2026-001" — empty when not yet invoiced.
        /// </summary>
        public string TaxInvoiceId { get; }

        /// <summary>Convenience alias — use in XAML where IsInvoiced reads naturally.</summary>
        public bool IsInvoiced => HasTaxInvoice;

        /// <summary>
        /// Badge text for history sidebar row:
        ///   "✅ TI-2026-001" — when invoiced
        ///   ""               — when not yet invoiced
        /// Bind to TextBlock.Text + Visibility.
        /// </summary>
        public string InvoicedBadgeText =>
            HasTaxInvoice && !string.IsNullOrWhiteSpace(TaxInvoiceId)
                ? $"✅ {TaxInvoiceId}"
                : "";

        /// <summary>
        /// True when this DN can be invoiced:
        ///   • Not already invoiced (HasTaxInvoice = false)
        ///   • Status is Dispatched/Delivered/Signed/Received
        /// Used to control "🧾 Tax Invoice" button enabled state in sidebar.
        /// </summary>
        public bool CanInvoice =>
            !HasTaxInvoice && (
                string.Equals(Status, "Dispatched",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Received",
                    StringComparison.OrdinalIgnoreCase));

        // ── Standard properties ───────────────────────────────────────────

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _isCurrentDn;
        public bool IsCurrentDn
        {
            get => _isCurrentDn;
            set
            {
                if (_isCurrentDn == value) return;
                _isCurrentDn = value;
                OnPropertyChanged();
            }
        }

        private int _attachmentCount;
        public int AttachmentCount
        {
            get => _attachmentCount;
            set
            {
                if (_attachmentCount == value) return;
                _attachmentCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachments));
            }
        }

        public bool HasAttachments => _attachmentCount > 0;
        public bool HasBalance => BalanceQty > 0;

        public DnHistoryEntryViewModel(DeliveryHistoryEntry e)
        {
            DeliveryNoteId = e.DeliveryNoteId;
            DeliveryType = e.DeliveryType;
            Status = e.Status;
            DeliveredQty = e.DeliveredQty;
            BalanceQty = e.BalanceQty;
            DeliveryDate = e.DeliveryDate.ToString("dd MMM yyyy");
            DriverName = e.DriverName;
            OrderedQty = e.OrderedQty;
            CumulativePrevQty = e.CumulativePrevQty;
            CumulativeBalanceQty = e.CumulativeBalanceQty;
            SequenceNumber = e.SequenceNumber;

            // CHAT 19 INVOICED: map invoice state from history entry
            // Set by DeliveryNoteRepository.GetDeliveryHistoryAsync()
            // which reads HasTaxInvoice + TaxInvoiceId from DN record
            HasTaxInvoice = e.HasTaxInvoice;
            TaxInvoiceId = e.TaxInvoiceId ?? "";

            TypeIcon = e.DeliveryType switch
            {
                "Rework" => "🔧",
                "Return" => "↩️",
                _ => "📦"
            };

            var statusInfo = Models.DeliveryNote.Common.DeliveryNoteStatus
                .DeliveryNoteStatusMeta.Get(e.Status);
            StatusBrushKey = statusInfo.BrushKey;
        }
    }
}
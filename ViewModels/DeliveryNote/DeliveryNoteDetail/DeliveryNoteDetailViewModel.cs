using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.DeliveryNote.Export;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
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
    /// Phase 10.6 : Attachments managed independently.
    /// Phase 10.6a: Rename dialog on upload.
    /// Phase 10.6b: Clickable history rows drive attachments panel.
    /// Phase 10.7 : Self-healing PREV, GroupedItemsView, MakeDnCommand.
    /// Phase 10.7a: DeleteDnCommand, zero-qty save warning, status refresh.
    ///              Clicking a history row now NAVIGATES to that DN
    ///              (full page reload) — so Status, Save, Make DN, Delete
    ///              always operate on the currently-viewed DN.
    ///              No more confusion between "open DN" and "selected DN".
    /// Phase 10.8 : Export commands — PDF, Excel, CSV via shared engine.
    /// Phase 10.8a: TotalPreviouslyDeliveredQty exposed for XAML binding.
    ///              Prev chip added to items grid header.
    ///              PDF totals strip now shows all 5 values.
    /// </summary>
    public class DeliveryNoteDetailViewModel : ViewModelBase
    {
        // ── callbacks ──────────────────────────────────────────────────────
        public Action? OnRequestBackToList { get; set; }

        // ── loaded entity + snapshot ──────────────────────────────────────
        private SavedDeliveryNote? _entity;
        private DeliveryNoteSnapshot? _snapshot;

        private readonly AttachmentService _attachmentService = new();

        // Phase 10.7a — guard against re-entrancy during history navigation
        private bool _isNavigatingHistory;

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

        // ── DN header fields ─────────────────────────────────────────────

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

        // ── JO context ───────────────────────────────────────────────────

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

        // ── Editable fields ──────────────────────────────────────────────

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

        // ── Grouped items view ────────────────────────────────────────────

        private ICollectionView? _groupedItemsView;
        public ICollectionView? GroupedItemsView
        {
            get => _groupedItemsView;
            private set { if (_groupedItemsView == value) return; _groupedItemsView = value; OnPropertyChanged(); }
        }

        private void RebuildGroupedView()
        {
            var view = CollectionViewSource.GetDefaultView(_itemRows);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(
                new PropertyGroupDescription(
                    nameof(DnItemRowViewModel.SpecGroupName)));
            GroupedItemsView = view;
        }

        // ── Qty summary ───────────────────────────────────────────────────

        private int _totalOrderedQty;
        public int TotalOrderedQty
        {
            get => _totalOrderedQty;
            private set { if (_totalOrderedQty == value) return; _totalOrderedQty = value; OnPropertyChanged(); }
        }

        // Phase 10.8a — exposed for XAML binding (Prev chip in items header)
        private int _totalPreviouslyDeliveredQty;
        public int TotalPreviouslyDeliveredQty
        {
            get => _totalPreviouslyDeliveredQty;
            private set { if (_totalPreviouslyDeliveredQty == value) return; _totalPreviouslyDeliveredQty = value; OnPropertyChanged(); }
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

        // ── Delivery history ──────────────────────────────────────────────

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
            string.Equals(_selectedHistoryEntry.DeliveryNoteId, _deliveryNoteId,
                          StringComparison.OrdinalIgnoreCase);

        public string SelectedDnHeaderText =>
            $"ATTACHMENTS ({AttachmentsCount})";

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

        // Phase 10.8 — export commands
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }

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

            MakeDnCommand = new RelayCommand(
                async _ => await ConfirmDispatchAsync(),
                _ => Status == "Draft" && !IsSaving);

            DeleteDnCommand = new RelayCommand(
                async _ => await DeleteDnAsync(),
                _ => _entity != null && !IsSaving);

            UploadImageCommand = new RelayCommand(
                async _ => await UploadImageAsync(),
                _ => _entity != null && !IsSaving);

            UploadPdfCommand = new RelayCommand(
                async _ => await UploadPdfAsync(),
                _ => _entity != null && !IsSaving);

            OpenAttachmentCommand = new RelayCommand(param =>
            {
                if (param is DnAttachmentRowViewModel row)
                    OpenAttachment(row);
            });

            DeleteAttachmentCommand = new RelayCommand(async param =>
            {
                if (param is DnAttachmentRowViewModel row)
                    await DeleteAttachmentAsync(row);
            });

            SelectHistoryEntryCommand = new RelayCommand(async param =>
            {
                if (param is DnHistoryEntryViewModel entry)
                    await NavigateToDnAsync(entry.DeliveryNoteId);
            });

            // Phase 10.8 — export
            ExportPdfCommand = new RelayCommand(
                async _ => await ExportPdfAsync(),
                _ => _entity != null && !IsSaving && !IsLoading);

            ExportExcelCommand = new RelayCommand(
                async _ => await ExportExcelAsync(),
                _ => _entity != null && !IsSaving && !IsLoading);

            ExportCsvCommand = new RelayCommand(
                async _ => await ExportCsvAsync(),
                _ => _entity != null && !IsSaving && !IsLoading);
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
                DeliveryDateDisplay = _entity.DeliveryDate.ToString("dd MMM yyyy");
                DeliveryTime = _entity.DeliveryTime;

                var joRef = _snapshot?.JobOrderRef;
                CustomerName = joRef?.CustomerName ?? _entity.CustomerName;
                ProjectName = joRef?.ProjectName ?? _entity.ProjectName;
                CompanyName = joRef?.CompanyName ?? "";
                CompanyTrn = joRef?.CompanyTrn ?? "";

                // Reset ALL editable fields from entity
                Status = _entity.Status;
                IsFoc = _entity.IsFoc;
                DriverName = _entity.DriverName;
                VehicleNumber = _entity.VehicleNumber;
                ReceiverName = _entity.ReceiverName;
                Notes = _entity.Notes;

                await HealItemPrevQtyAsync(_entity);

                var rows = _entity.Items
                    .OrderBy(i => i.SpecGroupNumber)
                    .ThenBy(i => i.SerialNumber)
                    .Select(i => new DnItemRowViewModel(i))
                    .ToList();

                ItemRows = new ObservableCollection<DnItemRowViewModel>(rows);

                foreach (var row in ItemRows)
                    row.DeliveredQtyChanged += OnItemDeliveredQtyChanged;

                RecalculateTotals();

                await LoadHistoryAsync();

                var currentInHistory = HistoryEntries
                    .FirstOrDefault(h => string.Equals(
                        h.DeliveryNoteId, DeliveryNoteId,
                        StringComparison.OrdinalIgnoreCase));

                _selectedHistoryEntry = currentInHistory;
                if (currentInHistory != null) currentInHistory.IsSelected = true;
                OnPropertyChanged(nameof(SelectedHistoryEntry));
                OnPropertyChanged(nameof(IsSelectedDnCurrent));

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
            }).ToList();

            HistoryEntries =
                new ObservableCollection<DnHistoryEntryViewModel>(entries);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAVE
        // ═════════════════════════════════════════════════════════════════

        public async Task SaveAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

            bool hasAnyDelivery = ItemRows.Any(r => r.DeliveredQty > 0);
            if (!hasAnyDelivery)
            {
                var warn = MessageBox.Show(
                    "No items have a delivered quantity — all quantities are 0.\n\n" +
                    "Are you sure you want to save with zero deliveries?\n\n" +
                    "  Yes → Save anyway (keeps Draft)\n" +
                    "  No  → Go back and enter quantities",
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
                    item.BalanceQty = item.OrderedQty
                                        - item.PreviouslyDeliveredQty
                                        - item.DeliveredQty;
                    item.Notes = row.Notes;
                }

                _entity.TotalDeliveredQty = _entity.Items.Sum(i => i.DeliveredQty);
                _entity.TotalBalanceQty = _entity.Items.Sum(i => i.BalanceQty);

                var joRef = _snapshot?.JobOrderRef;
                DeliveryNoteMapper.RefreshSnapshot(_entity, joRef);

                await App.DeliveryNotes.UpdateAsync(_entity);

                await LoadAsync(_entity.Id);

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
        //  MAKE DN (dispatch)
        // ═════════════════════════════════════════════════════════════════

        private async Task ConfirmDispatchAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

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

                Status = "Dispatched";
                await SaveAsync();

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

        // ═════════════════════════════════════════════════════════════════
        //  DELETE DN
        // ═════════════════════════════════════════════════════════════════

        private async Task DeleteDnAsync()
        {
            if (_entity == null || App.DeliveryNotes == null) return;

            var result = MessageBox.Show(
                $"Delete delivery note '{DeliveryNoteId}'?\n\n" +
                $"Customer : {CustomerName}\n" +
                $"Project  : {ProjectName}\n" +
                $"Status   : {Status}\n\n" +
                "This will permanently delete the DN, all its items\n" +
                "and all attached files. This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsSaving = true;

                if (_entity.Attachments != null)
                {
                    foreach (var att in _entity.Attachments)
                    {
                        try { _attachmentService.DeleteFile(att.FilePath); }
                        catch { /* file may already be gone */ }
                    }
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
        //  PHASE 10.8 — EXPORT
        // ═════════════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (!EnsureLoaded()) return;

            var layout = new DeliveryNotePdfLayout(_entity!, _snapshot!);
            var engine = new PdfExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "PDF");
        }

        private async Task ExportExcelAsync()
        {
            if (!EnsureLoaded()) return;

            var layout = new DeliveryNoteExcelLayout(_entity!, _snapshot!);
            var engine = new ExcelExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "Excel");
        }

        private async Task ExportCsvAsync()
        {
            if (!EnsureLoaded()) return;

            var layout = new DeliveryNoteCsvLayout(_entity!, _snapshot!);
            var engine = new CsvExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "CSV");
        }

        private bool EnsureLoaded()
        {
            if (_entity != null && _snapshot != null) return true;

            MessageBox.Show(
                "Load a delivery note before exporting.",
                "Nothing to export",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        private static void ShowExportResult(ExportResult result, string formatLabel)
        {
            if (result.Success) return;
            if (result.Cancelled) return;

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
            var rows = list.Select(a => new DnAttachmentRowViewModel(a)).ToList();
            Attachments = new ObservableCollection<DnAttachmentRowViewModel>(rows);

            var historyRow = HistoryEntries.FirstOrDefault(h => string.Equals(
                h.DeliveryNoteId, deliveryNoteId,
                StringComparison.OrdinalIgnoreCase));
            if (historyRow != null)
                historyRow.AttachmentCount = rows.Count;
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
                        "Unsupported image format.\n\nAllowed: JPG, PNG, BMP, GIF, WEBP.",
                        "Invalid File",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!_attachmentService.IsUnderSizeLimit(dlg.FileName))
                {
                    MessageBox.Show(
                        $"File is too large.\n\nMaximum size: {AttachmentLimits.MaxFileSizeDisplay}",
                        "File Too Large",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var origName = Path.GetFileName(dlg.FileName);
                var renameDlg = new RenameAttachmentDialog(origName)
                { Owner = Application.Current.MainWindow };
                if (renameDlg.ShowDialog() != true) return;
                var finalName = renameDlg.ResultFileName;

                IsSaving = true;

                var entity = await _attachmentService
                    .CopyImageAsync(dlg.FileName, _deliveryNoteId, finalName);

                await App.DeliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);
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
                        $"File is too large.\n\nMaximum size: {AttachmentLimits.MaxFileSizeDisplay}",
                        "File Too Large",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var origName = Path.GetFileName(dlg.FileName);
                var renameDlg = new RenameAttachmentDialog(origName)
                { Owner = Application.Current.MainWindow };
                if (renameDlg.ShowDialog() != true) return;
                var finalName = renameDlg.ResultFileName;

                IsSaving = true;

                var entity = await _attachmentService
                    .CopyPdfAsync(dlg.FileName, _deliveryNoteId, finalName);

                await App.DeliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);
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
                "This will remove the file from disk and cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await App.DeliveryNotes.DeleteAttachmentAsync(row.Id);
                _attachmentService.DeleteFile(row.FilePath);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);
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

        // Phase 10.8a — added TotalPreviouslyDeliveredQty
        private void RecalculateTotals()
        {
            TotalOrderedQty = ItemRows.Sum(r => r.OrderedQty);
            TotalPreviouslyDeliveredQty = ItemRows.Sum(r => r.PreviouslyDeliveredQty);
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

        public string DimensionDisplay =>
            W2 > 0 || H2 > 0 ? $"{W1}×{H1}  /  {W2}×{H2}" : $"{W1}×{H1}";

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
    //  DN HISTORY ENTRY VIEWMODEL
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

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        private bool _isCurrentDn;
        public bool IsCurrentDn
        {
            get => _isCurrentDn;
            set { if (_isCurrentDn == value) return; _isCurrentDn = value; OnPropertyChanged(); }
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
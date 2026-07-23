using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
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
    /// Two-column layout:
    ///   Left  — read-only JO snapshot context (customer, project, spec summary)
    ///          + delivery history timeline (clickable sibling DN list)
    ///   Right — editable DN fields (driver, vehicle, receiver, status, notes)
    ///          + delivery items grid (ordered/prev/this DN/balance per row)
    ///          + attachments section (per selected history DN)
    ///
    /// Phase 10.6 : Attachments managed independently — upload/delete
    ///              commits immediately, does NOT depend on Save button.
    /// Phase 10.6a: Rename dialog appears on upload — user can choose a
    ///              custom filename before the file is saved to disk.
    /// Phase 10.6b: Delivery history rows are clickable and drive the
    ///              attachments panel on the right. Selected sibling DN's
    ///              attachments are shown/managed. Current DN highlighted.
    ///              Each history row shows its own attachment count badge.
    /// Phase 10.7 : Self-healing PREV column — GetPreviouslyDeliveredMapAsync
    ///              called on load AND after save to overwrite stale DB
    ///              snapshots with fresh confirmed-sibling totals (Gotcha #41).
    ///              Save now re-fetches entity + re-heals + rebuilds rows
    ///              so both panels stay in sync (Gotcha #42).
    ///              Renamed ConfirmDispatchCommand → MakeDnCommand.
    ///              GroupedItemsView groups items by SpecGroupName
    ///              for display in the items grid (Improvement 3).
    /// </summary>
    public class DeliveryNoteDetailViewModel : ViewModelBase
    {
        // ── callbacks ──────────────────────────────────────────────────────
        public Action? OnRequestBackToList { get; set; }

        // ── loaded entity + snapshot ──────────────────────────────────────
        private SavedDeliveryNote? _entity;
        private DeliveryNoteSnapshot? _snapshot;

        // Phase 10.6 — Attachments file-I/O service
        private readonly AttachmentService _attachmentService = new();

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
            private set
            {
                if (_itemRows == value) return;
                _itemRows = value;
                OnPropertyChanged();
                // Phase 10.7 (Improvement 3) — rebuild grouped view
                // every time the rows collection is replaced
                RebuildGroupedView();
            }
        }

        // ── Phase 10.7 Improvement 3 — Grouped items view ────────────────

        private ICollectionView? _groupedItemsView;
        /// <summary>
        /// ICollectionView over ItemRows, grouped by SpecGroupName.
        /// Bound in XAML instead of ItemRows so the grid shows
        /// a spec-group header row before each group of items.
        /// Rebuilt automatically whenever ItemRows is replaced.
        /// </summary>
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
        /// Recreates the grouped view from the current _itemRows collection.
        /// Called from the ItemRows setter so it's always in sync.
        /// </summary>
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

        // ══════════════════════════════════════════════════════════════════
        //  Phase 10.6b — SELECTED SIBLING DN (drives Attachments panel)
        // ══════════════════════════════════════════════════════════════════

        private DnHistoryEntryViewModel? _selectedHistoryEntry;
        public DnHistoryEntryViewModel? SelectedHistoryEntry
        {
            get => _selectedHistoryEntry;
            set
            {
                if (_selectedHistoryEntry == value) return;

                // Deselect previous
                if (_selectedHistoryEntry != null)
                    _selectedHistoryEntry.IsSelected = false;

                _selectedHistoryEntry = value;

                if (_selectedHistoryEntry != null)
                    _selectedHistoryEntry.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDnHeaderText));
                OnPropertyChanged(nameof(IsSelectedDnCurrent));

                // Reload attachments for the newly-selected DN
                _ = LoadAttachmentsForSelectedAsync();
            }
        }

        /// <summary>
        /// True when the selected history DN is the current DN (default state).
        /// Used by the view to show/hide "you are viewing another DN's attachments" hint.
        /// </summary>
        public bool IsSelectedDnCurrent =>
            _selectedHistoryEntry != null &&
            string.Equals(_selectedHistoryEntry.DeliveryNoteId, _deliveryNoteId,
                          StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Dynamic attachments panel header:
        ///   "ATTACHMENTS FOR DN-2026-337-01 (3)"
        /// or "ATTACHMENTS (3)" when a DN isn't selected (initial state).
        /// </summary>
        public string SelectedDnHeaderText
        {
            get
            {
                if (_selectedHistoryEntry == null)
                    return $"ATTACHMENTS ({AttachmentsCount})";
                return $"ATTACHMENTS FOR {_selectedHistoryEntry.DeliveryNoteId} ({AttachmentsCount})";
            }
        }

        // Click a history row → select it (drives attachments panel)
        public ICommand SelectHistoryEntryCommand { get; }

        // ── Attachments (Phase 10.6 + 10.6b) ─────────────────────────────

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

        // Phase 10.7: renamed from ConfirmDispatchCommand → MakeDnCommand
        public ICommand MakeDnCommand { get; }

        public ICommand UploadImageCommand { get; }
        public ICommand UploadPdfCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }

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

            // Phase 10.7: renamed from ConfirmDispatchCommand → MakeDnCommand
            MakeDnCommand = new RelayCommand(
                async _ => await ConfirmDispatchAsync(),
                _ => Status == "Draft" && !IsSaving);

            UploadImageCommand = new RelayCommand(
                async _ => await UploadImageAsync(),
                _ => _selectedHistoryEntry != null && !IsSaving);

            UploadPdfCommand = new RelayCommand(
                async _ => await UploadPdfAsync(),
                _ => _selectedHistoryEntry != null && !IsSaving);

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

            // Phase 10.6b — click a history DN row → select it
            SelectHistoryEntryCommand = new RelayCommand(param =>
            {
                if (param is DnHistoryEntryViewModel entry)
                    SelectedHistoryEntry = entry;
            });
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

                Status = _entity.Status;
                IsFoc = _entity.IsFoc;
                DriverName = _entity.DriverName;
                VehicleNumber = _entity.VehicleNumber;
                ReceiverName = _entity.ReceiverName;
                Notes = _entity.Notes;

                // ── Phase 10.7 — Self-heal PREV column (Gotcha #41) ─────
                // Fetch fresh totals from confirmed siblings BEFORE building
                // row VMs, so stale DB snapshots are never shown to the user.
                await HealItemPrevQtyAsync(_entity);

                var rows = _entity.Items
                    .OrderBy(i => i.SpecGroupNumber)
                    .ThenBy(i => i.SerialNumber)
                    .Select(i => new DnItemRowViewModel(i))
                    .ToList();

                // Setter triggers RebuildGroupedView() automatically
                ItemRows = new ObservableCollection<DnItemRowViewModel>(rows);

                foreach (var row in ItemRows)
                    row.DeliveredQtyChanged += OnItemDeliveredQtyChanged;

                RecalculateTotals();

                // ── Load history (populates HistoryEntries) ─────────────
                await LoadHistoryAsync();

                // ── Phase 10.6b — auto-select CURRENT DN in the history
                //    list so attachments panel loads for THIS DN by default
                var currentInHistory = HistoryEntries
                    .FirstOrDefault(h => string.Equals(
                        h.DeliveryNoteId, DeliveryNoteId,
                        StringComparison.OrdinalIgnoreCase));

                if (currentInHistory != null)
                {
                    // Setter triggers LoadAttachmentsForSelectedAsync()
                    SelectedHistoryEntry = currentInHistory;
                }
                else
                {
                    // Fallback (shouldn't happen, but handle gracefully):
                    // load attachments directly for current DN
                    await LoadAttachmentsDirectlyAsync(DeliveryNoteId);
                }
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

        /// <summary>
        /// Phase 10.7 — Self-healing PREV column (Gotcha #41).
        /// Overwrites each item's PreviouslyDeliveredQty with a fresh
        /// total from confirmed sibling DNs (excluding this DN itself).
        /// Operates directly on the entity's Items collection in memory —
        /// does NOT write back to the DB.
        /// </summary>
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
            if (App.DeliveryNotes == null || string.IsNullOrWhiteSpace(JobOrderId))
                return;

            var history = await App.DeliveryNotes
                .GetDeliveryHistoryAsync(JobOrderId);

            HistoryTotalOrdered = history.TotalOrdered;
            HistoryTotalDelivered = history.TotalDelivered;
            HistoryTotalBalance = history.TotalBalance;

            // Phase 10.6b — batch-fetch attachment counts per sibling DN
            var siblingIds = history.Entries.Select(e => e.DeliveryNoteId).ToList();
            var counts = await App.DeliveryNotes
                .GetAttachmentCountsAsync(siblingIds);

            var entries = history.Entries.Select(e =>
            {
                var vm = new DnHistoryEntryViewModel(e);
                vm.AttachmentCount = counts.TryGetValue(e.DeliveryNoteId, out var c) ? c : 0;
                vm.IsCurrentDn = string.Equals(
                    e.DeliveryNoteId, DeliveryNoteId,
                    StringComparison.OrdinalIgnoreCase);
                return vm;
            }).ToList();

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

                // ── Phase 10.7 — Re-fetch + re-heal + rebuild (Gotcha #42) ──
                var refreshed = await App.DeliveryNotes.GetByIdAsync(_entity.Id);

                if (refreshed != null)
                {
                    _entity = refreshed;

                    await HealItemPrevQtyAsync(_entity);

                    foreach (var old in ItemRows)
                        old.DeliveredQtyChanged -= OnItemDeliveredQtyChanged;

                    var rows = _entity.Items
                        .OrderBy(i => i.SpecGroupNumber)
                        .ThenBy(i => i.SerialNumber)
                        .Select(i => new DnItemRowViewModel(i))
                        .ToList();

                    // Setter triggers RebuildGroupedView() automatically
                    ItemRows = new ObservableCollection<DnItemRowViewModel>(rows);

                    foreach (var row in ItemRows)
                        row.DeliveredQtyChanged += OnItemDeliveredQtyChanged;

                    RecalculateTotals();
                }

                MessageBox.Show(
                    $"Delivery note '{DeliveryNoteId}' saved successfully.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadHistoryAsync();

                // Re-select current DN in history so attachments panel stays correct
                var currentInHistory = HistoryEntries
                    .FirstOrDefault(h => string.Equals(
                        h.DeliveryNoteId, DeliveryNoteId,
                        StringComparison.OrdinalIgnoreCase));
                if (currentInHistory != null)
                    SelectedHistoryEntry = currentInHistory;
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
        //  MAKE DN (was: Confirm Dispatch) — Phase 10.7 rename
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
        //  ATTACHMENTS (Phase 10.6 + Phase 10.6b per-selected-DN loading)
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadAttachmentsForSelectedAsync()
        {
            if (_selectedHistoryEntry == null)
            {
                Attachments = new ObservableCollection<DnAttachmentRowViewModel>();
                return;
            }

            await LoadAttachmentsDirectlyAsync(_selectedHistoryEntry.DeliveryNoteId);
        }

        private async Task LoadAttachmentsDirectlyAsync(string deliveryNoteId)
        {
            if (App.DeliveryNotes == null || string.IsNullOrWhiteSpace(deliveryNoteId))
                return;

            var list = await App.DeliveryNotes
                .GetAttachmentsAsync(deliveryNoteId);

            var rows = list.Select(a => new DnAttachmentRowViewModel(a)).ToList();
            Attachments = new ObservableCollection<DnAttachmentRowViewModel>(rows);

            // Also refresh the count badge on the corresponding history row
            var historyRow = HistoryEntries.FirstOrDefault(h => string.Equals(
                h.DeliveryNoteId, deliveryNoteId, StringComparison.OrdinalIgnoreCase));
            if (historyRow != null)
                historyRow.AttachmentCount = rows.Count;
        }

        private async Task UploadImageAsync()
        {
            if (_selectedHistoryEntry == null || App.DeliveryNotes == null) return;

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
                        $"File is too large.\n\nMaximum size: {AttachmentLimits.MaxFileSizeDisplay}",
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
                var finalName = renameDlg.ResultFileName;

                IsSaving = true;

                var targetDnId = _selectedHistoryEntry.DeliveryNoteId;
                var entity = await _attachmentService
                    .CopyImageAsync(dlg.FileName, targetDnId, finalName);

                await App.DeliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsForSelectedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to upload image:\n\n{ex.Message}",
                    "Upload Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task UploadPdfAsync()
        {
            if (_selectedHistoryEntry == null || App.DeliveryNotes == null) return;

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
                {
                    Owner = Application.Current.MainWindow
                };
                if (renameDlg.ShowDialog() != true) return;
                var finalName = renameDlg.ResultFileName;

                IsSaving = true;

                var targetDnId = _selectedHistoryEntry.DeliveryNoteId;
                var entity = await _attachmentService
                    .CopyPdfAsync(dlg.FileName, targetDnId, finalName);

                await App.DeliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsForSelectedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to upload PDF:\n\n{ex.Message}",
                    "Upload Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void OpenAttachment(DnAttachmentRowViewModel row)
        {
            try
            {
                _attachmentService.OpenInDefaultApp(row.FilePath);
            }
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
                await LoadAttachmentsForSelectedAsync();
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

        // Phase 10.7: mutable so HealItemPrevQtyAsync can overwrite
        // stale DB snapshot before row VM is constructed. The entity's
        // item is mutated in memory; the row VM reads it fresh each time.
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
    //  DN HISTORY ENTRY VIEWMODEL — one clickable sibling DN row
    //  Phase 10.6b: added IsSelected + IsCurrentDn + AttachmentCount
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

        // Phase 10.6b — reactive flags
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
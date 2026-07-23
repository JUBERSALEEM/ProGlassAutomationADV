using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
    /// <summary>
    /// ViewModel for the Delivery Note list landing page.
    ///
    /// 6 tabs: All | Prepared | Delivered | Pending Qty | Rework | Returned
    /// 5 hero cards: Total | Pending Jobs | Rework | Returned | Delivered Today
    ///
    /// Phase 10.5c:
    ///   - Added Returned tab (DeliveryType = Return)
    ///   - Added 5th hero card for Returned DNs (red tint + ↩️)
    ///   - Single-click row → SelectedRow set → top 5 hero cards
    ///     swap to show selected DN's stats:
    ///       Card 1: Ordered Qty
    ///       Card 2: Delivered Qty
    ///       Card 3: Balance Qty
    ///       Card 4: Driver name
    ///       Card 5: Sequence number ("Delivery #N")
    ///   - Selection clears on tab switch, filter change, refresh
    ///   - Tab counts always reflect full dataset (not affected by selection)
    /// </summary>
    public class DeliveryNoteListViewModel : ViewModelBase
    {
        private int _loadingFlag = 0;

        public Action<int>? OnDeliveryNoteOpenRequested { get; set; }

        // ══════════════════════════════════════════════════════════════════
        //  SELECTION (Phase 10.5c)
        // ══════════════════════════════════════════════════════════════════

        private DeliveryNoteRowViewModel? _selectedRow;
        public DeliveryNoteRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;

                if (_selectedRow != null) _selectedRow.IsSelected = false;
                _selectedRow = value;
                if (_selectedRow != null) _selectedRow.IsSelected = true;

                OnPropertyChanged();

                // Refresh all hero card computed props
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HeroCard1Value));
                OnPropertyChanged(nameof(HeroCard1Label));
                OnPropertyChanged(nameof(HeroCard2Value));
                OnPropertyChanged(nameof(HeroCard2Label));
                OnPropertyChanged(nameof(HeroCard3Value));
                OnPropertyChanged(nameof(HeroCard3Label));
                OnPropertyChanged(nameof(HeroCard4Value));
                OnPropertyChanged(nameof(HeroCard4Label));
                OnPropertyChanged(nameof(HeroCard5Value));
                OnPropertyChanged(nameof(HeroCard5Label));
                OnPropertyChanged(nameof(HeroCard5Icon));
                OnPropertyChanged(nameof(SelectedDnMetaLine));
            }
        }

        public bool HasSelection => _selectedRow != null;

        // ── Hero card 1 — Total DNs / Selected Ordered Qty ────────────────
        public string HeroCard1Value =>
            _selectedRow != null
                ? _selectedRow.TotalOrderedQty.ToString()
                : TotalCount.ToString();

        public string HeroCard1Label =>
            _selectedRow != null ? "Ordered Qty (this DN)" : "Total Delivery Notes";

        // ── Hero card 2 — Pending Jobs / Selected Delivered Qty ───────────
        public string HeroCard2Value =>
            _selectedRow != null
                ? _selectedRow.TotalDeliveredQty.ToString()
                : PendingJobsCount.ToString();

        public string HeroCard2Label =>
            _selectedRow != null ? "Delivered Qty (this DN)" : "Jobs with Pending Qty";

        // ── Hero card 3 — Rework DNs / Selected Balance Qty ───────────────
        public string HeroCard3Value =>
            _selectedRow != null
                ? _selectedRow.TotalBalanceQty.ToString()
                : ReworkCount.ToString();

        public string HeroCard3Label =>
            _selectedRow != null ? "Balance Qty (this DN)" : "Rework DNs";

        // ── Hero card 4 — Returned DNs / Selected Driver (Phase 10.5c) ────
        //    When idle: shows returned count
        //    When selected: shows driver name
        public string HeroCard4Value =>
            _selectedRow != null
                ? (string.IsNullOrWhiteSpace(_selectedRow.DriverName)
                    ? "—"
                    : _selectedRow.DriverName)
                : ReturnedCount.ToString();

        public string HeroCard4Label =>
            _selectedRow != null ? "Driver" : "Returned DNs";

        // ── Hero card 5 — Delivered Today / Selected Sequence ─────────────
        //    When idle: shows Delivered Today count with ✅
        //    When selected: shows "Delivery #N" with 🔢
        public string HeroCard5Value =>
            _selectedRow != null
                ? $"Delivery #{_selectedRow.SequenceNumber}"
                : DeliveredTodayCount.ToString();

        public string HeroCard5Label =>
            _selectedRow != null ? "Sequence Number" : "Delivered Today";

        public string HeroCard5Icon =>
            _selectedRow != null ? "🔢" : "✅";

        // ── Selected DN meta line (shown under hero cards when selected) ──
        public string SelectedDnMetaLine =>
            _selectedRow == null
                ? ""
                : $"{_selectedRow.DeliveryNoteId}  ·  🚚 {NullIfBlank(_selectedRow.VehicleNumber, "—")}  ·  📅 {_selectedRow.DeliveryDateDisplay}  ·  🕐 {NullIfBlank(_selectedRow.DeliveryTime, "—")}";

        private static string NullIfBlank(string? s, string fallback)
            => string.IsNullOrWhiteSpace(s) ? fallback : s!;

        // ── Selection commands ────────────────────────────────────────────

        public ICommand SelectRowCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        // ══════════════════════════════════════════════════════════════════
        //  TAB INDEX  (0=All, 1=Prepared, 2=Delivered, 3=Pending, 4=Rework, 5=Returned)
        // ══════════════════════════════════════════════════════════════════

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex == value) return;
                _selectedTabIndex = value;
                OnPropertyChanged();

                SelectedRow = null;   // clear selection on tab switch
                _ = LoadTabAsync(value);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAB COLLECTIONS
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<DeliveryNoteRowViewModel> _allItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> AllItems
        {
            get => _allItems;
            private set { if (_allItems == value) return; _allItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _preparedItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> PreparedItems
        {
            get => _preparedItems;
            private set { if (_preparedItems == value) return; _preparedItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _deliveredItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> DeliveredItems
        {
            get => _deliveredItems;
            private set { if (_deliveredItems == value) return; _deliveredItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<PendingQtyRowViewModel> _pendingItems = new();
        public ObservableCollection<PendingQtyRowViewModel> PendingItems
        {
            get => _pendingItems;
            private set { if (_pendingItems == value) return; _pendingItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _reworkItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> ReworkItems
        {
            get => _reworkItems;
            private set { if (_reworkItems == value) return; _reworkItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _returnedItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> ReturnedItems
        {
            get => _returnedItems;
            private set { if (_returnedItems == value) return; _returnedItems = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  KPI  (dataset-wide — used when no row selected)
        // ══════════════════════════════════════════════════════════════════

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount == value) return;
                _totalCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard1Value));
            }
        }

        private int _pendingJobsCount;
        public int PendingJobsCount
        {
            get => _pendingJobsCount;
            private set
            {
                if (_pendingJobsCount == value) return;
                _pendingJobsCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard2Value));
            }
        }

        private int _reworkCount;
        public int ReworkCount
        {
            get => _reworkCount;
            private set
            {
                if (_reworkCount == value) return;
                _reworkCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard3Value));
            }
        }

        private int _returnedCount;
        public int ReturnedCount
        {
            get => _returnedCount;
            private set
            {
                if (_returnedCount == value) return;
                _returnedCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard4Value));
            }
        }

        private int _deliveredTodayCount;
        public int DeliveredTodayCount
        {
            get => _deliveredTodayCount;
            private set
            {
                if (_deliveredTodayCount == value) return;
                _deliveredTodayCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard5Value));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOADING / ERROR STATE
        // ══════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
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

        // ══════════════════════════════════════════════════════════════════
        //  FILTERS
        // ══════════════════════════════════════════════════════════════════

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                SelectedRow = null;
                ApplyAllTabFilter();
            }
        }

        private string _selectedStatusFilter = "All Statuses";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                SelectedRow = null;
                ApplyAllTabFilter();
            }
        }

        private string _selectedTypeFilter = "All Types";
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (_selectedTypeFilter == value) return;
                _selectedTypeFilter = value;
                OnPropertyChanged();
                SelectedRow = null;
                ApplyAllTabFilter();
            }
        }

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Draft", "Dispatched", "Delivered", "Signed", "Received"
        };

        public List<string> TypeFilterOptions { get; } = new()
        {
            "All Types", "Original", "Dummy", "Rework", "Return"
        };

        private List<DeliveryNoteRowViewModel> _rawAll = new();

        // ══════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════════

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ChangeStatusCommand { get; }

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public DeliveryNoteListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ =>
            {
                SelectedRow = null;
                await LoadAsync();
            });

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            OpenCommand = new RelayCommand(param =>
            {
                if (param is DeliveryNoteRowViewModel row)
                    OnDeliveryNoteOpenRequested?.Invoke(row.Id);
            });

            SelectRowCommand = new RelayCommand(param =>
            {
                if (param is DeliveryNoteRowViewModel row)
                {
                    // Toggle: click same row → deselect
                    SelectedRow = (SelectedRow == row) ? null : row;
                }
            });

            ClearSelectionCommand = new RelayCommand(_ => SelectedRow = null);

            DeleteCommand = new RelayCommand(async param =>
            {
                if (param is DeliveryNoteRowViewModel row)
                    await DeleteAsync(row);
            });

            ChangeStatusCommand = new RelayCommand(async param =>
            {
                if (param is (DeliveryNoteRowViewModel row, string newStatus))
                    await ChangeStatusAsync(row, newStatus);
            });

            _ = LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD (all tabs at once)
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = "";

                if (App.DeliveryNotes == null)
                {
                    HasError = true;
                    ErrorMessage = "Delivery Notes database not initialized.";
                    return;
                }

                var allTask = App.DeliveryNotes.GetAllAsync();
                var prepTask = App.DeliveryNotes.GetPreparedAsync();
                var delTask = App.DeliveryNotes.GetDeliveredAsync();
                var pendingTask = App.DeliveryNotes.GetPendingQtyJobsAsync();
                var reworkTask = App.DeliveryNotes.GetReworkAsync();
                var returnedTask = App.DeliveryNotes.GetReturnedAsync();

                await Task.WhenAll(
                    allTask, prepTask, delTask,
                    pendingTask, reworkTask, returnedTask);

                var allItems = allTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var prepItems = prepTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var delItems = delTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var pendingItems = pendingTask.Result.Select(x => new PendingQtyRowViewModel(x)).ToList();
                var reworkItems = reworkTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var returnedItems = returnedTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                _rawAll = allItems;

                AllItems = new ObservableCollection<DeliveryNoteRowViewModel>(allItems);
                PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(prepItems);
                DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(delItems);
                PendingItems = new ObservableCollection<PendingQtyRowViewModel>(pendingItems);
                ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(reworkItems);
                ReturnedItems = new ObservableCollection<DeliveryNoteRowViewModel>(returnedItems);

                TotalCount = allItems.Count;
                PendingJobsCount = pendingItems.Count;
                ReworkCount = reworkItems.Count;
                ReturnedCount = returnedItems.Count;
                DeliveredTodayCount = delItems.Count(x =>
                    x.DeliveryDate.Date == DateTime.Today);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load delivery notes: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        private async Task LoadTabAsync(int tabIndex)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                switch (tabIndex)
                {
                    case 1:
                        var prep = await App.DeliveryNotes.GetPreparedAsync();
                        PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            prep.Select(x => new DeliveryNoteRowViewModel(x)));
                        break;

                    case 2:
                        var del = await App.DeliveryNotes.GetDeliveredAsync();
                        DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            del.Select(x => new DeliveryNoteRowViewModel(x)));
                        break;

                    case 3:
                        var pending = await App.DeliveryNotes.GetPendingQtyJobsAsync();
                        PendingItems = new ObservableCollection<PendingQtyRowViewModel>(
                            pending.Select(x => new PendingQtyRowViewModel(x)));
                        PendingJobsCount = PendingItems.Count;
                        break;

                    case 4:
                        var rework = await App.DeliveryNotes.GetReworkAsync();
                        ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            rework.Select(x => new DeliveryNoteRowViewModel(x)));
                        ReworkCount = ReworkItems.Count;
                        break;

                    case 5:
                        var returned = await App.DeliveryNotes.GetReturnedAsync();
                        ReturnedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            returned.Select(x => new DeliveryNoteRowViewModel(x)));
                        ReturnedCount = ReturnedItems.Count;
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to reload tab: {ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  FILTER
        // ══════════════════════════════════════════════════════════════════

        private void ApplyAllTabFilter()
        {
            IEnumerable<DeliveryNoteRowViewModel> query = _rawAll;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.DeliveryNoteId ?? "").ToLowerInvariant().Contains(q) ||
                    (x.JobOrderId ?? "").ToLowerInvariant().Contains(q) ||
                    (x.CustomerName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.ProjectName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.DriverName ?? "").ToLowerInvariant().Contains(q));
            }

            if (_selectedStatusFilter != "All Statuses")
                query = query.Where(x => string.Equals(
                    x.Status, _selectedStatusFilter,
                    StringComparison.OrdinalIgnoreCase));

            if (_selectedTypeFilter != "All Types")
                query = query.Where(x => string.Equals(
                    x.DeliveryType, _selectedTypeFilter,
                    StringComparison.OrdinalIgnoreCase));

            AllItems = new ObservableCollection<DeliveryNoteRowViewModel>(query);
        }

        private void ClearFilters()
        {
            _searchText = "";
            _selectedStatusFilter = "All Statuses";
            _selectedTypeFilter = "All Types";
            SelectedRow = null;

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(SelectedTypeFilter));

            ApplyAllTabFilter();
        }

        // ══════════════════════════════════════════════════════════════════
        //  MUTATIONS
        // ══════════════════════════════════════════════════════════════════

        private async Task DeleteAsync(DeliveryNoteRowViewModel row)
        {
            var result = MessageBox.Show(
                $"Delete delivery note '{row.DeliveryNoteId}'?\n\n" +
                $"Customer: {row.CustomerName}\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (SelectedRow == row) SelectedRow = null;
                await App.DeliveryNotes!.DeleteAsync(row.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete:\n\n{ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(
            DeliveryNoteRowViewModel row, string newStatus)
        {
            try
            {
                var entity = await App.DeliveryNotes!.GetByIdAsync(row.Id);
                if (entity == null) return;

                entity.Status = newStatus;
                entity.UpdatedAt = DateTime.Now;
                await App.DeliveryNotes.UpdateAsync(entity);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change status:\n\n{ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ROW VIEWMODEL — extended with IsSelected for highlight
    // ═════════════════════════════════════════════════════════════════════

    public class DeliveryNoteRowViewModel : ViewModelBase
    {
        public int Id { get; }
        public string DeliveryNoteId { get; }
        public string JobOrderId { get; }
        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string Status { get; }
        public string DeliveryType { get; }
        public bool IsFoc { get; }
        public string DriverName { get; }
        public string VehicleNumber { get; }
        public int TotalOrderedQty { get; }
        public int TotalDeliveredQty { get; }
        public int TotalBalanceQty { get; }
        public int SequenceNumber { get; }
        public DateTime DeliveryDate { get; }
        public string DeliveryTime { get; }
        public DateTime CreatedAt { get; }

        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string TypeLabel { get; }
        public string TypeBrushKey { get; }
        public string DeliveryDateDisplay { get; }
        public string QtySummary { get; }
        public bool HasBalance { get; }
        public bool IsDummy { get; }

        // ── Selection highlight (Phase 10.5c) ─────────────────────────────
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public DeliveryNoteRowViewModel(DeliveryNoteListItem e)
        {
            Id = e.Id;
            DeliveryNoteId = e.DeliveryNoteId;
            JobOrderId = e.JobOrderId;
            ProformaId = e.ProformaId;
            CustomerName = string.IsNullOrWhiteSpace(e.CustomerName) ? "—" : e.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(e.ProjectName) ? "—" : e.ProjectName;
            Status = e.Status;
            DeliveryType = e.DeliveryType;
            IsFoc = e.IsFoc;
            DriverName = e.DriverName;
            VehicleNumber = e.VehicleNumber;
            TotalOrderedQty = e.TotalOrderedQty;
            TotalDeliveredQty = e.TotalDeliveredQty;
            TotalBalanceQty = e.TotalBalanceQty;
            SequenceNumber = e.SequenceNumber;
            DeliveryDate = e.DeliveryDate;
            DeliveryTime = e.DeliveryTime;
            CreatedAt = e.CreatedAt;

            var statusInfo = Models.DeliveryNote.Common.DeliveryNoteStatus
                .DeliveryNoteStatusMeta.Get(Status);
            StatusLabel = $"{statusInfo.Icon} {statusInfo.DisplayName}";
            StatusBrushKey = statusInfo.BrushKey;

            var typeInfo = Models.DeliveryNote.Common.DeliveryType
                .DeliveryTypeMeta.Get(DeliveryType);
            TypeLabel = $"{typeInfo.Icon} {typeInfo.DisplayName}";
            TypeBrushKey = typeInfo.BrushKey;

            DeliveryDateDisplay = DeliveryDate.ToString("dd MMM yyyy");
            QtySummary = $"{TotalDeliveredQty} delivered  •  {TotalBalanceQty} balance";
            HasBalance = TotalBalanceQty > 0;
            IsDummy = string.Equals(DeliveryType, "Dummy",
                                  StringComparison.OrdinalIgnoreCase);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PENDING QTY ROW
    // ═════════════════════════════════════════════════════════════════════

    public class PendingQtyRowViewModel
    {
        public string JobOrderId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public int TotalOrdered { get; }
        public int TotalDelivered { get; }
        public int TotalBalance { get; }
        public string LastDnId { get; }
        public string ProgressDisplay { get; }
        public double ProgressPercent { get; }

        public PendingQtyRowViewModel(PendingQtySummary s)
        {
            JobOrderId = s.JobOrderId;
            CustomerName = string.IsNullOrWhiteSpace(s.CustomerName) ? "—" : s.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(s.ProjectName) ? "—" : s.ProjectName;
            TotalOrdered = s.TotalOrdered;
            TotalDelivered = s.TotalDelivered;
            TotalBalance = s.TotalBalance;
            LastDnId = s.LastDnId;
            ProgressDisplay = $"{TotalDelivered} / {TotalOrdered} pcs";
            ProgressPercent = TotalOrdered > 0
                ? Math.Round((double)TotalDelivered / TotalOrdered * 100, 1)
                : 0;
        }
    }
}
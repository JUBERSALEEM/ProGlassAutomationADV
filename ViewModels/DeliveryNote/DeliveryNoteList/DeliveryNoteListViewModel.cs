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
    /// 5 tabs:
    ///   All         — every DN newest first
    ///   Prepared    — Draft DNs (includes Dummy DNs)
    ///   Delivered   — Delivered | Signed | Received
    ///   Pending Qty — JOs with balance qty > 0
    ///   Rework      — DeliveryType = Rework
    ///
    /// KPI cards: Total DNs | Pending Jobs | Rework Count | Delivered Today
    ///
    /// Right-click menu (code-behind): Open | Delete | Change Status |
    ///   Export PDF | Export Excel
    /// </summary>
    public class DeliveryNoteListViewModel : ViewModelBase
    {
        // ── reentrancy guard ──────────────────────────────────────────────
        private int _loadingFlag = 0;

        // ── callback wired by shell VM ────────────────────────────────────
        public Action<int>? OnDeliveryNoteOpenRequested { get; set; }

        // ── tab index ─────────────────────────────────────────────────────

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex == value) return;
                _selectedTabIndex = value;
                OnPropertyChanged();
                _ = LoadTabAsync(value);
            }
        }

        // ── ALL tab data ──────────────────────────────────────────────────

        private ObservableCollection<DeliveryNoteRowViewModel> _allItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> AllItems
        {
            get => _allItems;
            private set { if (_allItems == value) return; _allItems = value; OnPropertyChanged(); }
        }

        // ── PREPARED tab ──────────────────────────────────────────────────

        private ObservableCollection<DeliveryNoteRowViewModel> _preparedItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> PreparedItems
        {
            get => _preparedItems;
            private set { if (_preparedItems == value) return; _preparedItems = value; OnPropertyChanged(); }
        }

        // ── DELIVERED tab ─────────────────────────────────────────────────

        private ObservableCollection<DeliveryNoteRowViewModel> _deliveredItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> DeliveredItems
        {
            get => _deliveredItems;
            private set { if (_deliveredItems == value) return; _deliveredItems = value; OnPropertyChanged(); }
        }

        // ── PENDING QTY tab ───────────────────────────────────────────────

        private ObservableCollection<PendingQtyRowViewModel> _pendingItems = new();
        public ObservableCollection<PendingQtyRowViewModel> PendingItems
        {
            get => _pendingItems;
            private set { if (_pendingItems == value) return; _pendingItems = value; OnPropertyChanged(); }
        }

        // ── REWORK tab ────────────────────────────────────────────────────

        private ObservableCollection<DeliveryNoteRowViewModel> _reworkItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> ReworkItems
        {
            get => _reworkItems;
            private set { if (_reworkItems == value) return; _reworkItems = value; OnPropertyChanged(); }
        }

        // ── KPI cards ─────────────────────────────────────────────────────

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); }
        }

        private int _pendingJobsCount;
        public int PendingJobsCount
        {
            get => _pendingJobsCount;
            private set { if (_pendingJobsCount == value) return; _pendingJobsCount = value; OnPropertyChanged(); }
        }

        private int _reworkCount;
        public int ReworkCount
        {
            get => _reworkCount;
            private set { if (_reworkCount == value) return; _reworkCount = value; OnPropertyChanged(); }
        }

        private int _deliveredTodayCount;
        public int DeliveredTodayCount
        {
            get => _deliveredTodayCount;
            private set { if (_deliveredTodayCount == value) return; _deliveredTodayCount = value; OnPropertyChanged(); }
        }

        // ── loading + error ───────────────────────────────────────────────

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

        // ── search + filter (All tab) ─────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
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

        // ── raw data (all DNs for filter) ─────────────────────────────────
        private List<DeliveryNoteRowViewModel> _rawAll = new();

        // ── commands ──────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ChangeStatusCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            OpenCommand = new RelayCommand(param =>
            {
                if (param is DeliveryNoteRowViewModel row)
                    OnDeliveryNoteOpenRequested?.Invoke(row.Id);
            });

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

        // ═════════════════════════════════════════════════════════════════
        //  LOAD — full reload (called on init + refresh + return from detail)
        // ═════════════════════════════════════════════════════════════════

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

                // Load all tabs in parallel for fast initial render
                var allTask = App.DeliveryNotes.GetAllAsync();
                var prepTask = App.DeliveryNotes.GetPreparedAsync();
                var delTask = App.DeliveryNotes.GetDeliveredAsync();
                var pendingTask = App.DeliveryNotes.GetPendingQtyJobsAsync();
                var reworkTask = App.DeliveryNotes.GetReworkAsync();

                await Task.WhenAll(allTask, prepTask, delTask, pendingTask, reworkTask);

                var allItems = allTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var prepItems = prepTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var delItems = delTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var pendingItems = pendingTask.Result.Select(x => new PendingQtyRowViewModel(x)).ToList();
                var reworkItems = reworkTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                _rawAll = allItems;

                AllItems = new ObservableCollection<DeliveryNoteRowViewModel>(allItems);
                PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(prepItems);
                DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(delItems);
                PendingItems = new ObservableCollection<PendingQtyRowViewModel>(pendingItems);
                ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(reworkItems);

                // KPI cards
                TotalCount = allItems.Count;
                PendingJobsCount = pendingItems.Count;
                ReworkCount = reworkItems.Count;
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

        // Tab switch reload — only reloads the tab that was switched to
        private async Task LoadTabAsync(int tabIndex)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                switch (tabIndex)
                {
                    case 1: // Prepared
                        var prep = await App.DeliveryNotes.GetPreparedAsync();
                        PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            prep.Select(x => new DeliveryNoteRowViewModel(x)));
                        break;

                    case 2: // Delivered
                        var del = await App.DeliveryNotes.GetDeliveredAsync();
                        DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            del.Select(x => new DeliveryNoteRowViewModel(x)));
                        break;

                    case 3: // Pending Qty
                        var pending = await App.DeliveryNotes.GetPendingQtyJobsAsync();
                        PendingItems = new ObservableCollection<PendingQtyRowViewModel>(
                            pending.Select(x => new PendingQtyRowViewModel(x)));
                        PendingJobsCount = PendingItems.Count;
                        break;

                    case 4: // Rework
                        var rework = await App.DeliveryNotes.GetReworkAsync();
                        ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            rework.Select(x => new DeliveryNoteRowViewModel(x)));
                        ReworkCount = ReworkItems.Count;
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

        // ═════════════════════════════════════════════════════════════════
        //  FILTER (All tab only)
        // ═════════════════════════════════════════════════════════════════

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

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(SelectedTypeFilter));

            ApplyAllTabFilter();
        }

        // ═════════════════════════════════════════════════════════════════
        //  MUTATIONS
        // ═════════════════════════════════════════════════════════════════

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
                await App.DeliveryNotes.DeleteAsync(row.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete delivery note:\n\n{ex.Message}",
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
                var entity = await App.DeliveryNotes.GetByIdAsync(row.Id);
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
    //  ROW VIEWMODEL — display wrapper for DeliveryNoteListItem
    // ═════════════════════════════════════════════════════════════════════

    public class DeliveryNoteRowViewModel
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

        // ── Derived display ───────────────────────────────────────────────
        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string TypeLabel { get; }
        public string TypeBrushKey { get; }
        public string DeliveryDateDisplay { get; }
        public string QtySummary { get; }
        public bool HasBalance { get; }
        public bool IsDummy { get; }
        public string FocBadge { get; }

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

            // Status meta
            var statusInfo = Models.DeliveryNote.Common.DeliveryNoteStatus
                .DeliveryNoteStatusMeta.Get(Status);
            StatusLabel = $"{statusInfo.Icon} {statusInfo.DisplayName}";
            StatusBrushKey = statusInfo.BrushKey;

            // Type meta
            var typeInfo = Models.DeliveryNote.Common.DeliveryType
                .DeliveryTypeMeta.Get(DeliveryType);
            TypeLabel = $"{typeInfo.Icon} {typeInfo.DisplayName}";
            TypeBrushKey = typeInfo.BrushKey;

            DeliveryDateDisplay = DeliveryDate.ToString("dd MMM yyyy");
            QtySummary = $"{TotalDeliveredQty} delivered  •  {TotalBalanceQty} balance";
            HasBalance = TotalBalanceQty > 0;
            IsDummy = string.Equals(DeliveryType, "Dummy",
                                  StringComparison.OrdinalIgnoreCase);
            FocBadge = IsFoc ? "FOC" : "";
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PENDING QTY ROW — display wrapper for PendingQtySummary
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
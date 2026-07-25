using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList.JobOrderPicker;
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
    /// 5 hero cards: dynamic — dataset stats or selected DN stats.
    ///
    /// BUG-33: LoadTabAsync now refreshes _sqmCache on every tab switch.
    /// BUG-35: Filter now applies to Tabs 1,2,4,5 not just Tab 0.
    /// BUG-36: GenerateMasterInvoiceCommand canExecute re-evaluates correctly.
    /// BUG-37: Using directive added — no more fragile full namespace path.
    /// BUG-38: Row ViewModels split to DeliveryNoteListRowViewModels.cs.
    /// BUG-39: Tab count badges use dedicated grouped count properties.
    /// </summary>
    public class DeliveryNoteListViewModel : ViewModelBase
    {
        private int _loadingFlag = 0;

        public Action<int>? OnDeliveryNoteOpenRequested { get; set; }
        public Action<SavedJobOrder>? OnCreateDnFromJobOrder { get; set; }

        // ══════════════════════════════════════════════════════════════════
        //  SELECTION
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

        public string HeroCard1Value =>
            _selectedRow != null
                ? _selectedRow.TotalOrderedQty.ToString()
                : TotalCount.ToString();

        public string HeroCard1Label =>
            _selectedRow != null
                ? "Ordered Qty (this DN)"
                : "Total Delivery Notes";

        public string HeroCard2Value =>
            _selectedRow != null
                ? _selectedRow.TotalDeliveredQty.ToString()
                : PendingJobsCount.ToString();

        public string HeroCard2Label =>
            _selectedRow != null
                ? "Delivered Qty (this DN)"
                : "Jobs with Pending Qty";

        public string HeroCard3Value =>
            _selectedRow != null
                ? _selectedRow.TotalBalanceQty.ToString()
                : ReworkCount.ToString();

        public string HeroCard3Label =>
            _selectedRow != null
                ? "Balance Qty (this DN)"
                : "Rework DNs";

        public string HeroCard4Value =>
            _selectedRow != null
                ? (string.IsNullOrWhiteSpace(_selectedRow.DriverName)
                    ? "—" : _selectedRow.DriverName)
                : ReturnedCount.ToString();

        public string HeroCard4Label =>
            _selectedRow != null ? "Driver" : "Returned DNs";

        public string HeroCard5Value =>
            _selectedRow != null
                ? $"Delivery #{_selectedRow.SequenceNumber}"
                : DeliveredTodayCount.ToString();

        public string HeroCard5Label =>
            _selectedRow != null ? "Sequence Number" : "Delivered Today";

        public string HeroCard5Icon =>
            _selectedRow != null ? "🔢" : "✅";

        public string SelectedDnMetaLine =>
            _selectedRow == null ? "" :
            $"{_selectedRow.DeliveryNoteId}  ·  " +
            $"🚚 {NullIfBlank(_selectedRow.VehicleNumber, "—")}  ·  " +
            $"📅 {_selectedRow.DeliveryDateDisplay}  ·  " +
            $"🕐 {NullIfBlank(_selectedRow.DeliveryTime, "—")}";

        private static string NullIfBlank(string? s, string fallback)
            => string.IsNullOrWhiteSpace(s) ? fallback : s!;

        public ICommand SelectRowCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        // ══════════════════════════════════════════════════════════════════
        //  TAB INDEX
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
                SelectedRow = null;
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
            private set
            {
                if (_allItems == value) return;
                _allItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AllItemsGrouped));
                OnPropertyChanged(nameof(AllGroupedCount));
            }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _preparedItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> PreparedItems
        {
            get => _preparedItems;
            private set
            {
                if (_preparedItems == value) return;
                _preparedItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreparedItemsGrouped));
                OnPropertyChanged(nameof(PreparedGroupedCount));
            }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _deliveredItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> DeliveredItems
        {
            get => _deliveredItems;
            private set
            {
                if (_deliveredItems == value) return;
                _deliveredItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveredItemsGrouped));
                OnPropertyChanged(nameof(DeliveredGroupedCount));
            }
        }

        private ObservableCollection<PendingQtyRowViewModel> _pendingItems = new();
        public ObservableCollection<PendingQtyRowViewModel> PendingItems
        {
            get => _pendingItems;
            private set
            {
                if (_pendingItems == value) return;
                _pendingItems = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _reworkItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> ReworkItems
        {
            get => _reworkItems;
            private set
            {
                if (_reworkItems == value) return;
                _reworkItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReworkItemsGrouped));
                OnPropertyChanged(nameof(ReworkGroupedCount));
            }
        }

        private ObservableCollection<DeliveryNoteRowViewModel> _returnedItems = new();
        public ObservableCollection<DeliveryNoteRowViewModel> ReturnedItems
        {
            get => _returnedItems;
            private set
            {
                if (_returnedItems == value) return;
                _returnedItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReturnedItemsGrouped));
                OnPropertyChanged(nameof(ReturnedGroupedCount));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PER-JO GROUPED COLLECTIONS + SQM CACHE
        // ══════════════════════════════════════════════════════════════════

        private Dictionary<string, JobOrderSqmSummary> _sqmCache = new();

        public IReadOnlyList<JobOrderGroupRowViewModel> AllItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_allItems, _sqmCache);

        public IReadOnlyList<JobOrderGroupRowViewModel> PreparedItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_preparedItems, _sqmCache);

        public IReadOnlyList<JobOrderGroupRowViewModel> DeliveredItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_deliveredItems, _sqmCache);

        public IReadOnlyList<JobOrderGroupRowViewModel> ReworkItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_reworkItems, _sqmCache);

        public IReadOnlyList<JobOrderGroupRowViewModel> ReturnedItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_returnedItems, _sqmCache);

        // BUG-39: dedicated count properties for tab badges
        public int AllGroupedCount => AllItemsGrouped.Count;
        public int PreparedGroupedCount => PreparedItemsGrouped.Count;
        public int DeliveredGroupedCount => DeliveredItemsGrouped.Count;
        public int ReworkGroupedCount => ReworkItemsGrouped.Count;
        public int ReturnedGroupedCount => ReturnedItemsGrouped.Count;

        // ══════════════════════════════════════════════════════════════════
        //  KPI
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
            private set
            {
                if (_isLoading == value) return;
                _isLoading = value;
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
                ApplyFilters();
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
                ApplyFilters();
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
                ApplyFilters();
            }
        }

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Draft", "Dispatched",
            "Delivered", "Signed", "Received"
        };

        public List<string> TypeFilterOptions { get; } = new()
        {
            "All Types", "Original", "Dummy", "Rework", "Return"
        };

        // Raw unfiltered lists for each tab
        private List<DeliveryNoteRowViewModel> _rawAll = new();
        private List<DeliveryNoteRowViewModel> _rawPrepared = new();
        private List<DeliveryNoteRowViewModel> _rawDelivered = new();
        private List<DeliveryNoteRowViewModel> _rawRework = new();
        private List<DeliveryNoteRowViewModel> _rawReturned = new();

        // ══════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════════

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand CreateDnCommand { get; }
        public ICommand GenerateInvoiceCommand { get; }
        public ICommand OpenGroupCommand { get; }
        public ICommand GenerateMasterInvoiceCommand { get; }
        public ICommand SelectRowCommand2 { get; }

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
                    SelectedRow = (SelectedRow == row) ? null : row;
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

            CreateDnCommand = new RelayCommand(
                async _ => await OpenJobOrderPickerAsync());

            GenerateInvoiceCommand = new RelayCommand(param =>
            {
                if (param is DeliveryNoteRowViewModel row)
                {
                    MessageBox.Show(
                        $"Tax Invoice generation for '{row.DeliveryNoteId}'\n\n" +
                        "This feature is coming in Phase 19 — Tax Invoice module.",
                        "Coming in Phase 19",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            });

            OpenGroupCommand = new RelayCommand(param =>
            {
                if (param is JobOrderGroupRowViewModel group)
                    OnDeliveryNoteOpenRequested?.Invoke(group.LatestDnDbId);
            });

            // BUG-36: canExecute re-evaluates via CommandManager
            GenerateMasterInvoiceCommand = new RelayCommand(
                execute: param =>
                {
                    if (param is JobOrderGroupRowViewModel group)
                    {
                        MessageBox.Show(
                            $"Master Tax Invoice for '{group.JobOrderId}'\n\n" +
                            $"Splits         : {group.DnCount} DNs\n" +
                            $"Total ordered  : {group.TotalOrdered} pcs\n" +
                            $"Total delivered: {group.TotalDelivered} pcs\n" +
                            $"Balance        : {group.TotalBalance} pcs\n\n" +
                            $"Ordered SQM    : {group.TotalOrderedSqm:0.000}\n" +
                            $"Delivered SQM  : {group.TotalDeliveredSqm:0.000}\n" +
                            $"Balance SQM    : {group.TotalBalanceSqm:0.000}\n\n" +
                            "Tax Invoice module coming in Phase 19.",
                            "Master Tax Invoice — Coming in Phase 19",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                },
                canExecute: param =>
                    param is JobOrderGroupRowViewModel g && g.AllDelivered);

            _ = LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  JO PICKER
        // ══════════════════════════════════════════════════════════════════

        private async Task OpenJobOrderPickerAsync()
        {
            if (App.Jobs == null)
            {
                MessageBox.Show(
                    "Job Orders database not initialized.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            List<SavedJobOrder> jobOrders;
            try
            {
                jobOrders = await App.Jobs.GetAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load job orders:\n\n{ex.Message}",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (jobOrders.Count == 0)
            {
                MessageBox.Show(
                    "No job orders found.\n\n" +
                    "Create a Job Order from a Proforma Invoice first.",
                    "No Job Orders",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new JobOrderPickerDialog(jobOrders)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true) return;
            if (dialog.SelectedJobOrder == null) return;

            OnCreateDnFromJobOrder?.Invoke(dialog.SelectedJobOrder);
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD
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

                var allItems = allTask.Result
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var prepItems = prepTask.Result
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var delItems = delTask.Result
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var pendingItems = pendingTask.Result
                    .Select(x => new PendingQtyRowViewModel(x)).ToList();
                var reworkItems = reworkTask.Result
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var returnedItems = returnedTask.Result
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                // Store raw lists for filtering
                _rawAll = allItems;
                _rawPrepared = prepItems;
                _rawDelivered = delItems;
                _rawRework = reworkItems;
                _rawReturned = returnedItems;

                // BUG-33: batch-load SQM cache once per full load
                var allJobOrderIds = allItems
                    .Select(x => x.JobOrderId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _sqmCache = await App.DeliveryNotes
                    .GetSqmSummariesByJobOrderAsync(allJobOrderIds);

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

                // BUG-36: trigger canExecute re-evaluation after load
                CommandManager.InvalidateRequerySuggested();
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

        // BUG-33: LoadTabAsync now refreshes _sqmCache on every tab switch
        private async Task LoadTabAsync(int tabIndex)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                switch (tabIndex)
                {
                    case 1:
                        var prep = await App.DeliveryNotes.GetPreparedAsync();
                        _rawPrepared = prep
                            .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                        await RefreshSqmCacheAsync(_rawPrepared);
                        PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            _rawPrepared);
                        break;

                    case 2:
                        var del = await App.DeliveryNotes.GetDeliveredAsync();
                        _rawDelivered = del
                            .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                        await RefreshSqmCacheAsync(_rawDelivered);
                        DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            _rawDelivered);
                        break;

                    case 3:
                        var pending = await App.DeliveryNotes.GetPendingQtyJobsAsync();
                        PendingItems = new ObservableCollection<PendingQtyRowViewModel>(
                            pending.Select(x => new PendingQtyRowViewModel(x)));
                        PendingJobsCount = PendingItems.Count;
                        break;

                    case 4:
                        var rework = await App.DeliveryNotes.GetReworkAsync();
                        _rawRework = rework
                            .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                        await RefreshSqmCacheAsync(_rawRework);
                        ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            _rawRework);
                        ReworkCount = ReworkItems.Count;
                        break;

                    case 5:
                        var returned = await App.DeliveryNotes.GetReturnedAsync();
                        _rawReturned = returned
                            .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                        await RefreshSqmCacheAsync(_rawReturned);
                        ReturnedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                            _rawReturned);
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

        // BUG-33: refresh SQM cache for a set of rows
        private async Task RefreshSqmCacheAsync(
            List<DeliveryNoteRowViewModel> rows)
        {
            if (App.DeliveryNotes == null) return;

            var joIds = rows
                .Select(x => x.JobOrderId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (joIds.Count == 0) return;

            var fresh = await App.DeliveryNotes
                .GetSqmSummariesByJobOrderAsync(joIds);

            foreach (var kvp in fresh)
                _sqmCache[kvp.Key] = kvp.Value;
        }

        // ══════════════════════════════════════════════════════════════════
        //  FILTER — BUG-35: now applies to all tabs not just Tab 0
        // ══════════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            AllItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawAll));
            PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawPrepared));
            DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawDelivered));
            ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawRework));
            ReturnedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawReturned));
        }

        private IEnumerable<DeliveryNoteRowViewModel> Filter(
            IEnumerable<DeliveryNoteRowViewModel> source)
        {
            IEnumerable<DeliveryNoteRowViewModel> query = source;

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

            return query;
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

            ApplyFilters();
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
}
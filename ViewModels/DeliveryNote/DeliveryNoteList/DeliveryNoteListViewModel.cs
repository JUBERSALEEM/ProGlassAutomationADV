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
    /// Phase 10.5c : Selection, 6 tabs, 5 hero cards.
    /// Phase 10.7a : CreateDnCommand (JO picker).
    /// Phase 10.7a : GenerateInvoiceCommand placeholder — visible per row
    ///               when Status ∈ {Delivered, Signed, Received}.
    ///               Actual wiring in Phase 19 (Tax Invoice module).
    /// Ship 8a     : Per-JO grouped collections + JobOrderGroupRowViewModel
    ///               + OpenGroupCommand + GenerateMasterInvoiceCommand.
    /// Ship 8c-1   : BUGFIX — TotalOrdered was SUM (wrong). Now MAX
    ///               because OrderedQty is REPLICATED across splits.
    ///               Balance = Ordered - Delivered (derived).
    /// Ship 8c-2   : SQM aggregation added. Repository batch-computes
    ///               SqmCache once per LoadAsync. JobOrderGroupRowViewModel
    ///               gains TotalOrderedSqm / TotalDeliveredSqm / TotalBalanceSqm.
    ///               Uses SqmCalculator (0.5 m² min per panel).
    /// </summary>
    public class DeliveryNoteListViewModel : ViewModelBase
    {
        private int _loadingFlag = 0;

        public Action<int>? OnDeliveryNoteOpenRequested { get; set; }

        // Phase 10.7a — callback to shell VM to create DN from picked JO
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
            _selectedRow != null ? "Ordered Qty (this DN)" : "Total Delivery Notes";

        public string HeroCard2Value =>
            _selectedRow != null
                ? _selectedRow.TotalDeliveredQty.ToString()
                : PendingJobsCount.ToString();

        public string HeroCard2Label =>
            _selectedRow != null ? "Delivered Qty (this DN)" : "Jobs with Pending Qty";

        public string HeroCard3Value =>
            _selectedRow != null
                ? _selectedRow.TotalBalanceQty.ToString()
                : ReworkCount.ToString();

        public string HeroCard3Label =>
            _selectedRow != null ? "Balance Qty (this DN)" : "Rework DNs";

        public string HeroCard4Value =>
            _selectedRow != null
                ? (string.IsNullOrWhiteSpace(_selectedRow.DriverName)
                    ? "—"
                    : _selectedRow.DriverName)
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
            _selectedRow == null
                ? ""
                : $"{_selectedRow.DeliveryNoteId}  ·  🚚 {NullIfBlank(_selectedRow.VehicleNumber, "—")}  ·  📅 {_selectedRow.DeliveryDateDisplay}  ·  🕐 {NullIfBlank(_selectedRow.DeliveryTime, "—")}";

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
        //  TAB COLLECTIONS (per-DN)
        //
        //  Setters ALSO raise PropertyChanged for the corresponding
        //  *Grouped property so XAML can bind to either shape.
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
            }
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
            private set
            {
                if (_reworkItems == value) return;
                _reworkItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReworkItemsGrouped));
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
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PER-JO GROUPED COLLECTIONS (Ship 8a + 8c-2 SQM cache)
        //
        //  All 5 grouped views pass _sqmCache so JobOrderGroupRowViewModel
        //  can populate TotalOrderedSqm / TotalDeliveredSqm / TotalBalanceSqm.
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

        public ICommand CreateDnCommand { get; }

        public ICommand GenerateInvoiceCommand { get; }

        public ICommand OpenGroupCommand { get; }
        public ICommand GenerateMasterInvoiceCommand { get; }

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
                        "This feature is coming in Phase 19 — Tax Invoice module.\n\n" +
                        "The button is placed here now so the flow is ready\n" +
                        "when Phase 19 wires up the actual generation logic.",
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
                            "Tax Invoice module is coming in Phase 19.\n\n" +
                            "When ready, this button will:\n" +
                            "  • Consolidate ALL split DNs into one invoice\n" +
                            "  • Copy customer, project, all items, quantities\n" +
                            "  • Auto-calculate UAE 5% VAT on grand total\n" +
                            "  • Link back to every split DN (TaxInvoiceId)\n" +
                            "  • Mark the whole JO as fully invoiced\n\n" +
                            "For per-shipment (partial) invoices, open a DN\n" +
                            "and use the Tax Invoice button in its history row.",
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

                var allItems = allTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var prepItems = prepTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var delItems = delTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var pendingItems = pendingTask.Result.Select(x => new PendingQtyRowViewModel(x)).ToList();
                var reworkItems = reworkTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var returnedItems = returnedTask.Result.Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                _rawAll = allItems;

                // ══════════════════════════════════════════════════════════
                //  Ship 8c-2 — batch-load SQM aggregates per JO
                //  One repo call. Populates _sqmCache which JobOrderGroupRow-
                //  ViewModel reads in its constructor via GroupFrom().
                // ══════════════════════════════════════════════════════════
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
    //  ROW VIEWMODEL — per-DN (unchanged from Ship 8c-1)
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

        public bool CanGenerateInvoice { get; }

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

            CanGenerateInvoice =
                string.Equals(Status, "Delivered", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Signed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Received", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PENDING QTY ROW (unchanged)
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

    // ═════════════════════════════════════════════════════════════════════
    //  JOB ORDER GROUP ROW — Ship 8a + 8c-1 + 8c-2
    //
    //  Aggregates a family of split DNs into a single per-JO summary row.
    //
    //  Ship 8c-1 (qty math fix):
    //    OrderedQty is REPLICATED across all splits → take MAX (or First).
    //    DeliveredQty is per-split partial → SUM.
    //    Balance = Ordered - Delivered (derived, clamped ≥ 0).
    //
    //  Ship 8c-2 (SQM aggregation):
    //    SQM sourced from repository batch cache (JobOrderSqmSummary).
    //    Same MAX/SUM math applied at panel level in the repository.
    //    Uses SqmCalculator (0.5 m² min per panel).
    //    Graceful fallback to 0 if JO not in cache.
    // ═════════════════════════════════════════════════════════════════════

    public class JobOrderGroupRowViewModel
    {
        // Shared JO context
        public string JobOrderId { get; }
        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }

        // Family stats — qty
        public int DnCount { get; }
        public int TotalOrdered { get; }
        public int TotalDelivered { get; }
        public int TotalBalance { get; }

        // Family stats — SQM (Ship 8c-2)
        public decimal TotalOrderedSqm { get; }
        public decimal TotalDeliveredSqm { get; }
        public decimal TotalBalanceSqm { get; }

        // Display strings for XAML binding
        public string QtySummaryDisplay { get; }
        public string SqmSummaryDisplay { get; }

        public bool AllDelivered { get; }
        public string StatusLabel { get; }
        public double ProgressPercent { get; }
        public string ProgressDisplay { get; }

        // Latest split
        public string LatestDnId { get; }
        public int LatestDnDbId { get; }
        public DateTime LatestDeliveryDate { get; }
        public string LatestDeliveryDateDisplay { get; }
        public string LatestDriverName { get; }
        public int LatestSequenceNumber { get; }

        public bool HasFoc { get; }
        public bool HasRework { get; }
        public bool HasReturn { get; }

        // ──────────────────────────────────────────────────────────────
        // Factory: group per-DN collection into per-JO summaries.
        //
        // sqmCache: keyed by JobOrderId, sourced from repository batch
        //           query (GetSqmSummariesByJobOrderAsync). If a JO is
        //           missing from cache, SQM shows as 0 (graceful fallback).
        // ──────────────────────────────────────────────────────────────
        public static IReadOnlyList<JobOrderGroupRowViewModel> GroupFrom(
            IEnumerable<DeliveryNoteRowViewModel>? source,
            Dictionary<string, JobOrderSqmSummary>? sqmCache = null)
        {
            if (source == null) return Array.Empty<JobOrderGroupRowViewModel>();

            var cache = sqmCache ?? new Dictionary<string, JobOrderSqmSummary>();

            return source
                .Where(x => !string.IsNullOrWhiteSpace(x.JobOrderId))
                .GroupBy(x => x.JobOrderId, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    cache.TryGetValue(g.Key, out var sqm);
                    return new JobOrderGroupRowViewModel(g.Key, g.ToList(), sqm);
                })
                .OrderByDescending(g => g.LatestDeliveryDate)
                .ThenByDescending(g => g.LatestSequenceNumber)
                .ToList();
        }

        private JobOrderGroupRowViewModel(
            string jobOrderId,
            List<DeliveryNoteRowViewModel> family,
            JobOrderSqmSummary? sqm)
        {
            JobOrderId = jobOrderId;
            DnCount = family.Count;

            // Latest split by sequence number (fallback: delivery date)
            var latest = family
                .OrderByDescending(x => x.SequenceNumber)
                .ThenByDescending(x => x.DeliveryDate)
                .First();

            LatestDnId = latest.DeliveryNoteId;
            LatestDnDbId = latest.Id;
            LatestDeliveryDate = latest.DeliveryDate;
            LatestDeliveryDateDisplay = latest.DeliveryDateDisplay;
            LatestDriverName = string.IsNullOrWhiteSpace(latest.DriverName)
                ? "—" : latest.DriverName;
            LatestSequenceNumber = latest.SequenceNumber;

            ProformaId = latest.ProformaId;
            CustomerName = latest.CustomerName;
            ProjectName = latest.ProjectName;

            // ══════════════════════════════════════════════════════════════
            //  Correct qty math (Ship 8c-1)
            // ══════════════════════════════════════════════════════════════
            TotalOrdered = family.Max(x => x.TotalOrderedQty);
            TotalDelivered = family.Sum(x => x.TotalDeliveredQty);
            TotalBalance = Math.Max(0, TotalOrdered - TotalDelivered);

            // ══════════════════════════════════════════════════════════════
            //  SQM aggregation (Ship 8c-2)
            //  Sourced from repository cache — computed server-side with
            //  correct per-item MAX/SUM logic + 0.5 m² minimum per panel.
            //  If cache miss, graceful fallback to 0.
            // ══════════════════════════════════════════════════════════════
            TotalOrderedSqm = sqm?.OrderedSqm ?? 0m;
            TotalDeliveredSqm = sqm?.DeliveredSqm ?? 0m;
            TotalBalanceSqm = sqm?.BalanceSqm ?? 0m;

            // Display strings for XAML (used by JO row template)
            QtySummaryDisplay =
                $"{TotalOrdered} ordered  ·  {TotalDelivered} delivered  ·  {TotalBalance} balance";
            SqmSummaryDisplay =
                $"{TotalOrderedSqm:0.000} SQM  ·  {TotalDeliveredSqm:0.000} del  ·  {TotalBalanceSqm:0.000} bal";

            // Family status
            AllDelivered = family.All(x =>
                string.Equals(x.Status, "Delivered", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Signed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Received", StringComparison.OrdinalIgnoreCase));
            StatusLabel = AllDelivered ? "Completed" : "In Progress";

            ProgressPercent = TotalOrdered > 0
                ? Math.Round((double)TotalDelivered / TotalOrdered * 100, 1)
                : 0;
            ProgressDisplay = $"{TotalDelivered} / {TotalOrdered} pcs";

            HasFoc = family.Any(x => x.IsFoc);
            HasRework = family.Any(x =>
                string.Equals(x.DeliveryType, "Rework", StringComparison.OrdinalIgnoreCase));
            HasReturn = family.Any(x =>
                string.Equals(x.DeliveryType, "Return", StringComparison.OrdinalIgnoreCase));
        }
    }
}
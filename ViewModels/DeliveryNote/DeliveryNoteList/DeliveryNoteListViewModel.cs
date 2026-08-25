using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Database;
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
using ProGlassAutomation.Services.Audit;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
    /// <summary>
    /// ViewModel for the Delivery Note list landing page.
    ///
    /// 6 tabs: All | Prepared | Delivered | Pending Qty | Rework | Returned
    ///
    /// CHAT 19 CHANGES:
    ///   • Master DN Tab 6 REMOVED — Master DN is internal only
    ///   • MasterDnItems / MasterDnCount removed
    ///   • OnOpenMasterDnRequested removed
    ///   • OpenMasterDnCommand removed
    ///   • GetMasterDnsAsync no longer called in LoadAsync
    ///   • TypeFilterOptions keeps "Dummy" removed (Master DN internal)
    ///   • DeleteAsync guards against Master DN deletion (safety)
    /// </summary>
    public class DeliveryNoteListViewModel : ViewModelBase, System.IDisposable
    {
        private int _loadingFlag = 0;
        private int _reloadQueued = 0;
        private bool _disposed;

        public Action<int>? OnDeliveryNoteOpenRequested { get; set; }
        public Action<SavedJobOrder>? OnCreateDnFromJobOrder { get; set; }
        public Func<string, Task>? OnRequestOpenTaxInvoiceForProforma { get; set; }
        public Action<string>? OnRequestOpenMaster { get; set; }

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
        //  TAB COLLECTIONS — 6 tabs only (no Master DN tab)
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
                if (_selectedRow == null)
                    OnPropertyChanged(nameof(HeroCard1Value));
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
                if (_selectedRow == null)
                    OnPropertyChanged(nameof(HeroCard2Value));
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
                if (_selectedRow == null)
                    OnPropertyChanged(nameof(HeroCard3Value));
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
                if (_selectedRow == null)
                    OnPropertyChanged(nameof(HeroCard4Value));
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
                if (_selectedRow == null)
                    OnPropertyChanged(nameof(HeroCard5Value));
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

        // Delivery statuses only — no tax-invoice state ("Invoiced" removed).
        // "Delivered" was also added (it was missing from the list).
        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Dispatching", "Dispatched", "Delivered", "Signed", "Received"
        };

        // CHAT 19: Dummy removed — Master DN is internal, not in any list
        public List<string> TypeFilterOptions { get; } = new()
        {
            "All Types", "Original", "Rework", "Return"
        };

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
        public ICommand OpenMasterCommand { get; }
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

            GenerateInvoiceCommand = new RelayCommand(
                execute: async param =>
                {
                    if (param is not DeliveryNoteRowViewModel row) return;
                    await FireTaxInvoiceRequestAsync(
                        row.ProformaId, row.DeliveryNoteId);
                },
                canExecute: param =>
                    param is DeliveryNoteRowViewModel r
                    && r.CanGenerateInvoice
                    && !string.IsNullOrWhiteSpace(r.ProformaId));

            OpenGroupCommand = new RelayCommand(param =>
            {
                if (param is JobOrderGroupRowViewModel group)
                    OnDeliveryNoteOpenRequested?.Invoke(group.LatestDnDbId);
            });

            OpenMasterCommand = new RelayCommand(param =>
            {
                string? joId = param switch
                {
                    JobOrderGroupRowViewModel group => group.JobOrderId,
                    string id => id,
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(joId))
                    OnRequestOpenMaster?.Invoke(joId);
            });

            GenerateMasterInvoiceCommand = new RelayCommand(
                execute: async param =>
                {
                    if (param is not JobOrderGroupRowViewModel group) return;
                    await FireTaxInvoiceRequestAsync(
                        group.ProformaId, group.JobOrderId);
                },
                canExecute: param =>
                    param is JobOrderGroupRowViewModel g
                    && g.AnyDelivered
                    && !string.IsNullOrWhiteSpace(g.ProformaId));

            DocumentLifecycle.Subscribe(LoadAsync);

            _ = LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAX INVOICE REQUEST
        // ══════════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  AUDIT R-2 (Phase 11 — command re-entry / duplicate documents)
        //
        //  GenerateInvoiceCommand and GenerateMasterInvoiceCommand are
        //  async. Their CanExecute depends on row state that does not
        //  change until the async work finishes, and WPF re-queries
        //  CanExecute only on the CommandManager cycle. A double-click —
        //  ordinary behaviour for many users — therefore fired the tax
        //  invoice request TWICE before the first pass completed.
        //
        //  The database saves this app from silent corruption: every
        //  document table has a unique index (TaxInvoices.InvoiceNumber
        //  IsUnique), so the second write fails rather than duplicating.
        //  But the user gets an unexplained error dialog on a legitimate
        //  action, which looks like a broken app.
        //
        //  This flag makes the second click a no-op instead. It is a
        //  plain bool, not a lock: WPF commands execute on the UI thread,
        //  so there is no race to protect against, and a lock here would
        //  imply a concurrency model this code does not have.
        // ══════════════════════════════════════════════════════════════
        private bool _invoiceRequestInFlight;

        private async Task FireTaxInvoiceRequestAsync(
            string? proformaId, string sourceRef)
        {
            if (_invoiceRequestInFlight) return;
            _invoiceRequestInFlight = true;
            try
            {
                await FireTaxInvoiceRequestCoreAsync(proformaId, sourceRef);
            }
            finally
            {
                // Always clear, including on an exception path, or the
                // button would stay dead for the rest of the session.
                _invoiceRequestInFlight = false;
            }
        }

        private async Task FireTaxInvoiceRequestCoreAsync(
            string? proformaId, string sourceRef)
        {
            if (string.IsNullOrWhiteSpace(proformaId))
            {
                MessageBox.Show(
                    "Cannot create Tax Invoice — no PI reference.\n\n" +
                    $"Source: {sourceRef}",
                    "No PI Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForProforma == null)
            {
                MessageBox.Show(
                    "Tax Invoice navigation is not wired up.\n\n" +
                    "Restart the app.",
                    "Not Wired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"Create Tax Invoice?\n\n" +
                $"Source : {sourceRef}\n" +
                $"PI     : {proformaId}",
                "Create Tax Invoice",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.OK) return;

            try
            {
                await OnRequestOpenTaxInvoiceForProforma(proformaId);
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
            try { jobOrders = await App.Jobs.GetAllAsync(); }
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

            List<ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteListItem> dns = new();
            try
            {
                if (App.DeliveryNotes != null)
                    dns = await App.DeliveryNotes.GetAllAsync();
            }
            catch { /* picker still searches JO fields */ }

            var dialog = new JobOrderPickerDialog(jobOrders, dns)
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
            {
                Interlocked.Exchange(ref _reloadQueued, 1);
                return;
            }

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

                // 6 tabs only — no Master DN tab
                var allTask = App.DeliveryNotes.GetAllAsync();
                var prepTask = App.DeliveryNotes.GetPreparedAsync();
                var delTask = App.DeliveryNotes.GetDeliveredAsync();
                var pendingTask = App.DeliveryNotes.GetPendingQtyJobsAsync();
                var reworkTask = App.DeliveryNotes.GetReworkAsync();
                var returnedTask = App.DeliveryNotes.GetReturnedAsync();

                await Task.WhenAll(
                    allTask, prepTask, delTask,
                    pendingTask, reworkTask, returnedTask);

                // Phase 3: No .Result — use await (tasks already completed, but await is deadlock-safe)
                var allItems = (await allTask)
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var prepItems = (await prepTask)
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var delItems = (await delTask)
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var pendingItems = (await pendingTask)
                    .Select(x => new PendingQtyRowViewModel(x)).ToList();
                var reworkItems = (await reworkTask)
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();
                var returnedItems = (await returnedTask)
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                _rawAll = allItems;
                _rawPrepared = prepItems;
                _rawDelivered = delItems;
                _rawRework = reworkItems;
                _rawReturned = returnedItems;

                var allJobOrderIds = allItems
                    .Select(x => x.JobOrderId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _sqmCache = await App.DeliveryNotes
                    .GetSqmSummariesByJobOrderAsync(allJobOrderIds);

                AllItems =
                    new ObservableCollection<DeliveryNoteRowViewModel>(allItems);
                PreparedItems =
                    new ObservableCollection<DeliveryNoteRowViewModel>(prepItems);
                DeliveredItems =
                    new ObservableCollection<DeliveryNoteRowViewModel>(delItems);
                PendingItems =
                    new ObservableCollection<PendingQtyRowViewModel>(pendingItems);
                ReworkItems =
                    new ObservableCollection<DeliveryNoteRowViewModel>(reworkItems);
                ReturnedItems =
                    new ObservableCollection<DeliveryNoteRowViewModel>(returnedItems);

                // Count real delivery notes only — auto-generated Dummy DNs
                // (balance placeholders) are NOT delivery notes, so they must
                // not inflate the "Total Delivery Notes" figure.
                TotalCount = allItems.Count(x => !x.IsDummy);
                PendingJobsCount = pendingItems.Count;
                ReworkCount = reworkItems.Count;
                ReturnedCount = returnedItems.Count;
                DeliveredTodayCount = delItems.Count(x =>
                    x.DeliveryDate.Date == DateTime.Today);

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
                if (Interlocked.CompareExchange(ref _reloadQueued, 0, 1) == 1)
                    _ = LoadAsync();
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
                        _rawPrepared = prep
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawPrepared);
                        PreparedItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawPrepared);
                        break;

                    case 2:
                        var del = await App.DeliveryNotes.GetDeliveredAsync();
                        _rawDelivered = del
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawDelivered);
                        DeliveredItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawDelivered);
                        break;

                    case 3:
                        var pending =
                            await App.DeliveryNotes.GetPendingQtyJobsAsync();
                        PendingItems =
                            new ObservableCollection<PendingQtyRowViewModel>(
                                pending.Select(x => new PendingQtyRowViewModel(x)));
                        PendingJobsCount = PendingItems.Count;
                        break;

                    case 4:
                        var rework = await App.DeliveryNotes.GetReworkAsync();
                        _rawRework = rework
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawRework);
                        ReworkItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawRework);
                        ReworkCount = ReworkItems.Count;
                        break;

                    case 5:
                        var returned = await App.DeliveryNotes.GetReturnedAsync();
                        _rawReturned = returned
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawReturned);
                        ReturnedItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawReturned);
                        ReturnedCount = ReturnedItems.Count;
                        break;

                        // No case 6 — Master DN tab removed
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
        //  FILTER
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
                    x.DisplayStatus, _selectedStatusFilter,
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
            // A-1 (UI layer): tell the user BEFORE the confirm
            // dialog. The repository guard is the real boundary.
            if (!PermissionService.Require(
                    AppPermissions.DeliveryNoteEdit, "delete a delivery note"))
                return;

            // Safety guard — Master DNs must never be deleted from list
            if (row.IsMasterDn)
            {
                MessageBox.Show(
                    "Master DNs cannot be deleted manually.\n\n" +
                    "They are managed automatically by the system.",
                    "Cannot Delete Master DN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Phase 14: orphan protection — refuse to delete a DN that has
            // been invoiced.
            var guard = await ProGlassAutomation.Services.Database
                .DependencyGuard.CheckAsync(
                    ProGlassAutomation.Services.Database.DependencyGuard
                        .Kind.DeliveryNote,
                    row.DeliveryNoteId);
            if (guard.HasDependents)
            {
                MessageBox.Show(guard.Message, "Cannot Delete",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

                // Phase 3: enforce centralized status-transition rules at the
                // business boundary.
                var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                    .Validate(
                        ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.DeliveryNote,
                        entity.Status, newStatus);
                if (!transition.IsValid)
                {
                    MessageBox.Show(transition.Reason ?? "Invalid status change.",
                        "Status Change Not Allowed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

        /// <summary>
        /// Detaches the static DocumentLifecycle subscriber so this VM is not
        /// held alive (and not re-run) for the whole session after it is no
        /// longer in use. Without this it is a memory leak.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ProGlassAutomation.Services.Database.DocumentLifecycle.Unsubscribe(LoadAsync); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteListViewModel.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.DeliveryNote;
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
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
        public partial class DeliveryNoteListViewModel : ViewModelBase, System.IDisposable
    {
            private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly IDatabaseServices _database = null!;
        private readonly IDialogService _dialogs = null!;
        private readonly IJobOrderRepository _jobs = null!;

        public DeliveryNoteListViewModel(IDeliveryNoteRepository? deliveryNotes, IDialogService? dialogs, IJobOrderRepository? jobs, IDatabaseServices? database)
        {
            _deliveryNotes = deliveryNotes!;
            _dialogs = dialogs!;
            _jobs = jobs!;
            _database = database!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

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

        public ICommand SelectRowCommand { get; private set; }
        public ICommand ClearSelectionCommand { get; private set; }

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
        private DocumentProgressSnapshot _progress = DocumentProgressSnapshot.Empty;

        public IReadOnlyList<JobOrderGroupRowViewModel> AllItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_allItems, _sqmCache, _progress);

        public IReadOnlyList<JobOrderGroupRowViewModel> PreparedItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_preparedItems, _sqmCache, _progress);

        public IReadOnlyList<JobOrderGroupRowViewModel> DeliveredItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_deliveredItems, _sqmCache, _progress);

        public IReadOnlyList<JobOrderGroupRowViewModel> ReworkItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_reworkItems, _sqmCache, _progress);

        public IReadOnlyList<JobOrderGroupRowViewModel> ReturnedItemsGrouped
            => JobOrderGroupRowViewModel.GroupFrom(_returnedItems, _sqmCache, _progress);

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

        public ICommand RefreshCommand { get; private set; }
        public ICommand ClearFiltersCommand { get; private set; }
        public ICommand OpenCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand ChangeStatusCommand { get; private set; }
        public ICommand CreateDnCommand { get; private set; }
        public ICommand GenerateInvoiceCommand { get; private set; }
        public ICommand OpenGroupCommand { get; private set; }
        public ICommand OpenMasterCommand { get; private set; }
        public ICommand GenerateMasterInvoiceCommand { get; private set; }
        public ICommand CreateDnForJobOrderCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public DeliveryNoteListViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IJobOrderRepository>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
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
                {
                    // Open Master DN (tracking view) instead of latest real DN
                    // Master DN shows complete delivery picture for this job order
                    OnRequestOpenMaster?.Invoke(group.JobOrderId);
                }
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

            // Create DN directly from a job order row (no picker popup)
            CreateDnForJobOrderCommand = new RelayCommand(
                execute: async param =>
                {
                    string? jobOrderId = param as string;
                    if (string.IsNullOrWhiteSpace(jobOrderId) || _jobs == null) return;

                    try
                    {
                        // Phase 2: GetByJobOrderIdAsync already existed and was
                        // not being used. This loaded every job order in the
                        // database to find one by its id.
                        var job = await _jobs.GetByJobOrderIdAsync(jobOrderId);
                        if (job != null)
                            OnCreateDnFromJobOrder?.Invoke(job);
                    }
                    catch (Exception ex)
                    {
                        _dialogs.Show(
                            string.Format(LocalizationService.Instance.T("Msg.FailedToLoadJobOrder"), ExceptionMessages.Deep(ex)),
                            LocalizationService.Instance.T("Msg.CapError"), System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                },
                canExecute: param => param is string s && !string.IsNullOrWhiteSpace(s));

            _database.SubscribeDocumentsChanged(LoadAsync);

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
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.CannotCreateTaxInvoiceNo"), sourceRef),
                    LocalizationService.Instance.T("Msg.CapPIReference"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForProforma == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.TaxInvoiceNavigationIsNot2"),
                    LocalizationService.Instance.T("Msg.CapWired"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var confirm = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.CreateTaxInvoiceSourceX"), sourceRef, proformaId),
                LocalizationService.Instance.T("TaxInvoice.CreateTAXInvoice"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.OK) return;

            try
            {
                await OnRequestOpenTaxInvoiceForProforma(proformaId);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToOpenTaxInvoice"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapNavigationFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  JO PICKER
        // ══════════════════════════════════════════════════════════════════

        private async Task OpenJobOrderPickerAsync()
        {
            if (_jobs == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.JobOrdersDatabaseNotInitialized"),
                    LocalizationService.Instance.T("Msg.CapError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            List<SavedJobOrder> allJobOrders;
            try { allJobOrders = await _jobs.GetAllAsync(); }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToLoadJobOrders"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapLoadFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (allJobOrders.Count == 0)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.JobOrdersFoundCreateA"),
                    LocalizationService.Instance.T("Msg.CapJobOrders"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var jobOrdersWithBalance = new List<SavedJobOrder>();

            foreach (var jo in allJobOrders)
            {
                try
                {
                    if (_deliveryNotes == null)
                    {
                        jobOrdersWithBalance.Add(jo);
                        continue;
                    }

                    var masterDn = await _deliveryNotes
                        .GetMasterDnForJobOrderAsync(jo.JobOrderId);

                    if (masterDn == null)
                    {
                        jobOrdersWithBalance.Add(jo);
                        continue;
                    }

                    bool hasRemaining = masterDn.TotalBalanceQty > 0 ||
                        (masterDn.Items?.Any(i => i.ComputedBalanceQty > 0) == true);
                    if (hasRemaining)
                    {
                        jobOrdersWithBalance.Add(jo);
                        continue;
                    }

                    var siblings = await _deliveryNotes
                        .GetByJobOrderIdAsync(jo.JobOrderId);
                    if (siblings.Any(d =>
                            DeliveryNoteWorkflow.IsOpenForDispatch(d.Status)))
                    {
                        jobOrdersWithBalance.Add(jo);
                    }
                }
                catch
                {
                    jobOrdersWithBalance.Add(jo);
                }
            }

            if (jobOrdersWithBalance.Count == 0)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.JobOrdersHaveBeenFully"),
                    LocalizationService.Instance.T("Msg.CapDelivered"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            List<ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteListItem> dns = new();
            try
            {
                if (_deliveryNotes != null)
                    dns = await _deliveryNotes.GetAllAsync();
            }
            catch { /* picker still searches JO fields */ }

            var dialog = _dialogs.Create<JobOrderPickerDialog>(jobOrdersWithBalance, dns);

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

                if (_deliveryNotes == null)
                {
                    HasError = true;
                    ErrorMessage = "Delivery Notes database not initialized.";
                    return;
                }

                // 6 tabs only — no Master DN tab
                // Phase 2: this used to fire SIX parallel full-table loads,
                // five of which are subsets of the first. Prepared and
                // Delivered are now derived from the one GetAllAsync result —
                // same !IsMasterDn exclusion, same projection, so the rows are
                // identical — which removes two round trips over the whole
                // table.
                //
                // Rework / Returned are deliberately still fetched separately:
                // unlike GetAllAsync they do NOT exclude Master DNs, so
                // deriving them here would silently change those two tabs.
                var allTask = _deliveryNotes.GetAllAsync();
                var pendingTask = _deliveryNotes.GetPendingQtyJobsAsync();
                var reworkTask = _deliveryNotes.GetReworkAsync();
                var returnedTask = _deliveryNotes.GetReturnedAsync();

                await Task.WhenAll(
                    allTask, pendingTask, reworkTask, returnedTask);

                // Phase 3: No .Result — use await (tasks already completed, but await is deadlock-safe)
                var allRaw = await allTask;

                var allItems = allRaw
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                // GetPreparedAsync filters Status == "Draft" || "Dispatching",
                // which is exactly DeliveryNoteStatusValues.IsPreparing.
                // One nuance: SQL `=` on SQLite is case-sensitive while
                // IsPreparing is not, so the derived version is marginally
                // broader. Statuses are written from the canonical vocabulary
                // in PascalCase, so the sets are identical in practice; being
                // broader here is the safe direction for a filter tab.
                // Ordering matches GetAllAsync (CreatedAt DESC), so no re-sort.
                var prepItems = allRaw
                    .Where(x => DeliveryNoteStatusValues.IsPreparing(x.Status))
                    .Select(x => new DeliveryNoteRowViewModel(x)).ToList();

                // GetDeliveredAsync filters Delivered | Signed | Received and
                // orders by DeliveryDate DESC — not CreatedAt — so the sort is
                // reproduced explicitly. Omitting it would have reordered the
                // Delivered tab.
                var delItems = allRaw
                    .Where(x => DeliveryNoteStatusValues.IsDelivered(x.Status)
                             || DeliveryNoteStatusValues.IsSigned(x.Status)
                             || DeliveryNoteStatusValues.IsReceived(x.Status))
                    .OrderByDescending(x => x.DeliveryDate)
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

                _sqmCache = await _deliveryNotes
                    .GetSqmSummariesByJobOrderAsync(allJobOrderIds);

                _progress = await DocumentProgressSnapshot.LoadAsync(
                    allJobOrderIds.Select(id => (id, "")));

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

                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load delivery notes: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
                if (Interlocked.CompareExchange(ref _reloadQueued, 0, 1) == 1)
                    _ = LoadAsync();
            }
        }
    }
}

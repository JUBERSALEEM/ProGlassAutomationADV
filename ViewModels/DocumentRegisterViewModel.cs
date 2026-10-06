using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.Workflow;
using ProGlassAutomation.Utilities;

namespace ProGlassAutomation.ViewModels
{
    /// <summary>
    /// Joined, read-model register for PI → JO → DN. The source repositories
    /// remain authoritative; in-cell status/priority edits call the existing
    /// module list workflows rather than writing register rows directly.
    /// </summary>
    public sealed class DocumentRegisterViewModel : ViewModelBase, System.IDisposable
    {
        private readonly IProformaInvoiceRepository? _invoices;
        private readonly IJobOrderRepository? _jobs;
        private readonly IDeliveryNoteRepository? _deliveryNotes;
        private readonly IDatabaseServices? _database;

        private List<ProformaRegisterRow> _allProformas = new();
        private List<JobOrderRegisterRow> _allJobOrders = new();
        private List<DeliveryNoteRegisterRow> _allDeliveryNotes = new();
        private int _loadingFlag;
        private int _reloadQueued;
        private bool _disposed;

        public DocumentRegisterViewModel()
            : this(
                App.ServiceProvider?.GetService<IProformaInvoiceRepository>(),
                App.ServiceProvider?.GetService<IJobOrderRepository>(),
                App.ServiceProvider?.GetService<IDeliveryNoteRepository>(),
                App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public DocumentRegisterViewModel(
            IProformaInvoiceRepository? invoices,
            IJobOrderRepository? jobs,
            IDeliveryNoteRepository? deliveryNotes,
            IDatabaseServices? database)
        {
            _invoices = invoices;
            _jobs = jobs;
            _deliveryNotes = deliveryNotes;
            _database = database;

            RefreshCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadAsync, "Document Register Refresh"));
            NewProformaCommand = new RelayCommand(_ => OnNewProformaRequested?.Invoke());
            NewJobOrderCommand = new RelayCommand(_ => OnNewJobOrderRequested?.Invoke());
            NewDeliveryNoteCommand = new RelayCommand(_ => OnNewDeliveryNoteRequested?.Invoke());
            OpenProformaCommand = new RelayCommand(parameter =>
            {
                if (parameter is ProformaRegisterRow row && OnOpenProformaRequested is { } open)
                    AsyncUtility.SafeFireAndForget(() => open(row.Id), "Document Register Open PI");
            });
            OpenJobOrderCommand = new RelayCommand(parameter =>
            {
                if (parameter is JobOrderRegisterRow row && OnOpenJobOrderRequested is { } open)
                    AsyncUtility.SafeFireAndForget(() => open(row.Id), "Document Register Open JO");
            });
            OpenDeliveryNoteCommand = new RelayCommand(parameter =>
            {
                if (parameter is DeliveryNoteRegisterRow row && OnOpenDeliveryNoteRequested is { } open)
                    AsyncUtility.SafeFireAndForget(() => open(row.Id), "Document Register Open DN");
            });

            _database?.SubscribeDocumentsChanged(LoadAsync);
        }

        public ObservableCollection<ProformaRegisterRow> ProformaRows { get; } = new();
        public ObservableCollection<JobOrderRegisterRow> JobOrderRows { get; } = new();
        public ObservableCollection<DeliveryNoteRegisterRow> DeliveryNoteRows { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand NewProformaCommand { get; }
        public ICommand NewJobOrderCommand { get; }
        public ICommand NewDeliveryNoteCommand { get; }
        public ICommand OpenProformaCommand { get; }
        public ICommand OpenJobOrderCommand { get; }
        public ICommand OpenDeliveryNoteCommand { get; }

        public Action? OnNewProformaRequested { get; set; }
        public Action? OnNewJobOrderRequested { get; set; }
        public Action? OnNewDeliveryNoteRequested { get; set; }
        public System.Func<int, Task>? OnOpenProformaRequested { get; set; }
        public System.Func<int, Task>? OnOpenJobOrderRequested { get; set; }
        public System.Func<int, Task>? OnOpenDeliveryNoteRequested { get; set; }
        public System.Func<int, string, Task>? OnChangeProformaStatusRequested { get; set; }
        public System.Func<int, string, Task>? OnChangeJobOrderStatusRequested { get; set; }
        public System.Func<int, string, Task>? OnChangeJobOrderPriorityRequested { get; set; }
        public System.Func<int, string, Task>? OnChangeDeliveryNoteStatusRequested { get; set; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private bool _showVoided;
        public bool ShowVoided
        {
            get => _showVoided;
            set
            {
                if (_showVoided == value) return;
                _showVoided = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRefresh));
            }
        }

        public bool CanRefresh => !IsLoading;

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

        private string _errorMessage = string.Empty;
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

        private string _lastRefreshedText = "Not refreshed yet";
        public string LastRefreshedText
        {
            get => _lastRefreshedText;
            private set
            {
                if (_lastRefreshedText == value) return;
                _lastRefreshedText = value;
                OnPropertyChanged();
            }
        }

        private int _visibleProformaCount;
        public int VisibleProformaCount
        {
            get => _visibleProformaCount;
            private set { if (_visibleProformaCount == value) return; _visibleProformaCount = value; OnPropertyChanged(); }
        }

        private int _visibleUnconvertedCount;
        public int VisibleUnconvertedCount
        {
            get => _visibleUnconvertedCount;
            private set { if (_visibleUnconvertedCount == value) return; _visibleUnconvertedCount = value; OnPropertyChanged(); }
        }

        private int _visibleJobOrderCount;
        public int VisibleJobOrderCount
        {
            get => _visibleJobOrderCount;
            private set { if (_visibleJobOrderCount == value) return; _visibleJobOrderCount = value; OnPropertyChanged(); }
        }

        private int _visibleDeliveryNoteCount;
        public int VisibleDeliveryNoteCount
        {
            get => _visibleDeliveryNoteCount;
            private set { if (_visibleDeliveryNoteCount == value) return; _visibleDeliveryNoteCount = value; OnPropertyChanged(); }
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
            {
                Interlocked.Exchange(ref _reloadQueued, 1);
                return;
            }

            try
            {
                do
                {
                    Interlocked.Exchange(ref _reloadQueued, 0);
                    await LoadCoreAsync();
                }
                while (Interlocked.CompareExchange(ref _reloadQueued, 0, 1) == 1);
            }
            finally
            {
                Interlocked.Exchange(ref _loadingFlag, 0);
                if (Interlocked.CompareExchange(ref _reloadQueued, 0, 1) == 1)
                    AsyncUtility.SafeFireAndForget(LoadAsync, "Document Register queued refresh");
            }
        }

        private async Task LoadCoreAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            try
            {
                if (_invoices == null || _jobs == null || _deliveryNotes == null)
                    throw new System.InvalidOperationException("Document Register repositories are not available.");

                // All three calls return narrow list projections; no full document
                // snapshot JSON is materialized by this register.
                var proformaTask = _invoices.GetListRowsAsync();
                var jobOrderTask = _jobs.GetListRowsAsync();
                var deliveryNoteTask = _deliveryNotes.GetAllAsync();
                await Task.WhenAll(proformaTask, jobOrderTask, deliveryNoteTask);

                var proformas = await proformaTask;
                var jobOrders = await jobOrderTask;
                var deliveryNotes = await deliveryNoteTask;

                var jobsByProforma = GroupByIdentifier(jobOrders, row => row.SourceProformaId);
                var deliveryNotesByJobOrder = GroupByIdentifier(deliveryNotes, row => row.JobOrderId);
                var deliveryNotesByProforma = GroupByIdentifier(deliveryNotes, row => row.ProformaId);

                _allProformas = proformas.Select(proforma =>
                {
                    var key = NormalizeKey(proforma.ProformaId);
                    var linkedJobs = GetGroup(jobsByProforma, key)
                        .OrderByDescending(job => job.CreatedAt)
                        .ToList();
                    var linkedNotes = FindLinkedDeliveryNotes(
                        key, linkedJobs, deliveryNotesByJobOrder, deliveryNotesByProforma);
                    return new ProformaRegisterRow(
                        proforma,
                        linkedJobs.Select(job => job.JobOrderId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray(),
                        linkedNotes.Select(note => note.DeliveryNoteId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray(),
                        ChangeProformaStatusAsync);
                }).ToList();

                _allJobOrders = jobOrders.Select(job => new JobOrderRegisterRow(
                    job,
                    GetGroup(deliveryNotesByJobOrder, NormalizeKey(job.JobOrderId))
                        .OrderByDescending(note => note.DeliveryDate)
                        .Select(note => note.DeliveryNoteId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .ToArray(),
                    ChangeJobOrderStatusAsync,
                    ChangeJobOrderPriorityAsync)).ToList();

                _allDeliveryNotes = deliveryNotes
                    .Where(note => !note.IsMasterDn)
                    .Select(note => new DeliveryNoteRegisterRow(note, ChangeDeliveryNoteStatusAsync))
                    .ToList();

                LastRefreshedText = "Updated " + DateTime.Now.ToString("dd MMM yyyy, h:mm tt", CultureInfo.CurrentCulture);
                ApplyFilters();
            }
            catch (System.Exception ex)
            {
                HasError = true;
                ErrorMessage = "Could not load the document register: " + ExceptionMessages.Deep(ex);
                AppLogService.Error("Document Register load failed", ex,
                    new LogContext { Module = "Document Register", Operation = "Load" });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ChangeProformaStatusAsync(int id, string status)
        {
            try
            {
                if (OnChangeProformaStatusRequested != null)
                    await OnChangeProformaStatusRequested(id, status);
            }
            finally { await LoadAsync(); }
        }

        private async Task ChangeJobOrderStatusAsync(int id, string status)
        {
            try
            {
                if (OnChangeJobOrderStatusRequested != null)
                    await OnChangeJobOrderStatusRequested(id, status);
            }
            finally { await LoadAsync(); }
        }

        private async Task ChangeJobOrderPriorityAsync(int id, string priority)
        {
            try
            {
                if (OnChangeJobOrderPriorityRequested != null)
                    await OnChangeJobOrderPriorityRequested(id, priority);
            }
            finally { await LoadAsync(); }
        }

        private async Task ChangeDeliveryNoteStatusAsync(int id, string status)
        {
            try
            {
                if (OnChangeDeliveryNoteStatusRequested != null)
                    await OnChangeDeliveryNoteStatusRequested(id, status);
            }
            finally { await LoadAsync(); }
        }

        private void ApplyFilters()
        {
            var piRows = _allProformas
                .Where(row => ShowVoided || !row.IsVoid)
                .Where(row => row.MatchesSearch(SearchText))
                .ToList();
            var jobRows = _allJobOrders
                .Where(row => ShowVoided || !row.IsVoid)
                .Where(row => row.MatchesSearch(SearchText))
                .ToList();
            var dnRows = _allDeliveryNotes
                .Where(row => ShowVoided || !row.IsVoid)
                .Where(row => row.MatchesSearch(SearchText))
                .ToList();

            ReplaceRows(ProformaRows, piRows);
            ReplaceRows(JobOrderRows, jobRows);
            ReplaceRows(DeliveryNoteRows, dnRows);

            VisibleProformaCount = piRows.Count;
            VisibleUnconvertedCount = piRows.Count(row => row.JobOrderCount == 0);
            VisibleJobOrderCount = jobRows.Count;
            VisibleDeliveryNoteCount = dnRows.Count;
        }

        private static void ReplaceRows<T>(ObservableCollection<T> target, IEnumerable<T> rows)
        {
            target.Clear();
            foreach (var row in rows)
                target.Add(row);
        }

        private static Dictionary<string, List<T>> GroupByIdentifier<T>(
            IEnumerable<T> rows,
            System.Func<T, string?> identifier)
        {
            var result = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var key = NormalizeKey(identifier(row));
                if (key.Length == 0) continue;
                if (!result.TryGetValue(key, out var bucket))
                {
                    bucket = new List<T>();
                    result.Add(key, bucket);
                }
                bucket.Add(row);
            }
            return result;
        }

        private static List<T> GetGroup<T>(Dictionary<string, List<T>> groups, string key) =>
            groups.TryGetValue(key, out var rows) ? rows : new List<T>();

        private static List<DeliveryNoteListItem> FindLinkedDeliveryNotes(
            string proformaKey,
            IEnumerable<JobOrderListRow> linkedJobs,
            Dictionary<string, List<DeliveryNoteListItem>> notesByJobOrder,
            Dictionary<string, List<DeliveryNoteListItem>> notesByProforma)
        {
            var result = new Dictionary<int, DeliveryNoteListItem>();
            foreach (var note in GetGroup(notesByProforma, proformaKey))
                result[note.Id] = note;

            foreach (var job in linkedJobs)
            {
                foreach (var note in GetGroup(notesByJobOrder, NormalizeKey(job.JobOrderId)))
                    result[note.Id] = note;
            }

            return result.Values
                .OrderByDescending(note => note.DeliveryDate)
                .ToList();
        }

        private static string NormalizeKey(string? value) => value?.Trim() ?? string.Empty;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _database?.UnsubscribeDocumentsChanged(LoadAsync); }
            catch (System.Exception ex)
            {
                AppLogService.Debug("[DocumentRegisterViewModel] Dispose detach failed: " + ExceptionMessages.Deep(ex));
            }
            System.GC.SuppressFinalize(this);
        }
    }

    public sealed class ProformaRegisterRow : ViewModelBase
    {
        private readonly System.Func<int, string, Task>? _onStatusChanged;
        private string _selectedStatus;
        private bool _isStatusUpdating;

        public ProformaRegisterRow(
            ProformaListRow source,
            IEnumerable<string> jobOrderIds,
            IEnumerable<string> deliveryNoteIds,
            System.Func<int, string, Task>? onStatusChanged)
        {
            Id = source.Id;
            ProformaId = source.ProformaId ?? string.Empty;
            CompanyName = source.CompanyName ?? string.Empty;
            CustomerName = source.CustomerName ?? string.Empty;
            ProjectName = source.ProjectName ?? string.Empty;
            Status = source.Status ?? string.Empty;
            IssueDate = source.IssueDate;
            GrandTotalVatInclusive = source.GrandTotalVatInclusive;
            IsVoid = source.IsVoid;
            JobOrderIds = string.Join(", ", jobOrderIds.Distinct(StringComparer.OrdinalIgnoreCase));
            DeliveryNoteIds = string.Join(", ", deliveryNoteIds.Distinct(StringComparer.OrdinalIgnoreCase));
            JobOrderCount = jobOrderIds.Count(id => !string.IsNullOrWhiteSpace(id));
            DeliveryNoteCount = deliveryNoteIds.Count(id => !string.IsNullOrWhiteSpace(id));
            StatusOptions = StatusTransitions.GetAllowedTargets(StatusTransitions.Kind.ProformaInvoice, Status);
            _onStatusChanged = onStatusChanged;
            _selectedStatus = StatusOptions.FirstOrDefault(option =>
                string.Equals(option, Status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Status.Trim();
        }

        public int Id { get; }
        public string ProformaId { get; }
        public string CompanyName { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string Status { get; }
        public System.DateTime IssueDate { get; }
        public decimal GrandTotalVatInclusive { get; }
        public string JobOrderIds { get; }
        public string DeliveryNoteIds { get; }
        public int JobOrderCount { get; }
        public int DeliveryNoteCount { get; }
        public bool IsVoid { get; }
        public IReadOnlyList<string> StatusOptions { get; }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus == value) return;
                _selectedStatus = value ?? string.Empty;
                OnPropertyChanged();
                RequestStatusChange(_selectedStatus);
            }
        }

        public bool IsStatusUpdating
        {
            get => _isStatusUpdating;
            private set
            {
                if (_isStatusUpdating == value) return;
                _isStatusUpdating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanChangeStatus));
            }
        }

        public bool CanChangeStatus => !IsVoid && !IsStatusUpdating && StatusOptions.Count > 1;

        public bool MatchesSearch(string? term) =>
            Contains(ProformaId, term) || Contains(CompanyName, term) || Contains(CustomerName, term)
            || Contains(ProjectName, term) || Contains(Status, term) || Contains(JobOrderIds, term)
            || Contains(DeliveryNoteIds, term);

        private void RequestStatusChange(string status)
        {
            if (IsVoid || _onStatusChanged == null || string.IsNullOrWhiteSpace(status)) return;
            IsStatusUpdating = true;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                try { await _onStatusChanged(Id, status).ConfigureAwait(true); }
                finally { IsStatusUpdating = false; }
            }, $"Document Register PI status {ProformaId}");
        }

        private static bool Contains(string value, string? term) =>
            string.IsNullOrWhiteSpace(term) || value.Contains(term.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public sealed class JobOrderRegisterRow : ViewModelBase
    {
        private readonly System.Func<int, string, Task>? _onStatusChanged;
        private readonly System.Func<int, string, Task>? _onPriorityChanged;
        private string _selectedStatus;
        private string _selectedPriority;
        private bool _isStatusUpdating;
        private bool _isPriorityUpdating;

        public JobOrderRegisterRow(
            JobOrderListRow source,
            IEnumerable<string> deliveryNoteIds,
            System.Func<int, string, Task>? onStatusChanged,
            System.Func<int, string, Task>? onPriorityChanged)
        {
            Id = source.Id;
            JobOrderId = source.JobOrderId ?? string.Empty;
            SourceProformaId = source.SourceProformaId ?? string.Empty;
            CustomerName = source.CustomerName ?? string.Empty;
            ProjectName = source.ProjectName ?? string.Empty;
            Status = source.Status ?? string.Empty;
            Priority = string.IsNullOrWhiteSpace(source.Priority) ? "Medium" : source.Priority;
            DeliveryDate = source.DeliveryDate;
            GrandTotalVatInclusive = source.GrandTotalVatInclusive;
            IsVoid = source.IsVoid;
            DeliveryNoteIds = string.Join(", ", deliveryNoteIds.Distinct(StringComparer.OrdinalIgnoreCase));
            DeliveryNoteCount = deliveryNoteIds.Count(id => !string.IsNullOrWhiteSpace(id));
            StatusOptions = StatusTransitions.GetAllowedTargets(StatusTransitions.Kind.JobOrder, Status);
            var priorityOptions = new List<string> { "High", "Medium", "Low" };
            if (!string.IsNullOrWhiteSpace(Priority)
                && !priorityOptions.Contains(Priority, StringComparer.OrdinalIgnoreCase))
                priorityOptions.Insert(0, Priority);
            PriorityOptions = priorityOptions;
            _onStatusChanged = onStatusChanged;
            _onPriorityChanged = onPriorityChanged;
            _selectedStatus = StatusOptions.FirstOrDefault(option =>
                string.Equals(option, Status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Status.Trim();
            _selectedPriority = PriorityOptions.FirstOrDefault(option =>
                string.Equals(option, Priority, StringComparison.OrdinalIgnoreCase)) ?? Priority;
        }

        public int Id { get; }
        public string JobOrderId { get; }
        public string SourceProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string Status { get; }
        public string Priority { get; }
        public System.DateTime? DeliveryDate { get; }
        public decimal GrandTotalVatInclusive { get; }
        public string DeliveryNoteIds { get; }
        public int DeliveryNoteCount { get; }
        public bool IsVoid { get; }
        public IReadOnlyList<string> StatusOptions { get; }
        public IReadOnlyList<string> PriorityOptions { get; }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus == value) return;
                _selectedStatus = value ?? string.Empty;
                OnPropertyChanged();
                RequestStatusChange(_selectedStatus);
            }
        }

        public string SelectedPriority
        {
            get => _selectedPriority;
            set
            {
                if (_selectedPriority == value) return;
                _selectedPriority = value ?? string.Empty;
                OnPropertyChanged();
                RequestPriorityChange(_selectedPriority);
            }
        }

        public bool IsStatusUpdating
        {
            get => _isStatusUpdating;
            private set
            {
                if (_isStatusUpdating == value) return;
                _isStatusUpdating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanChangeStatus));
            }
        }

        public bool IsPriorityUpdating
        {
            get => _isPriorityUpdating;
            private set
            {
                if (_isPriorityUpdating == value) return;
                _isPriorityUpdating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanChangePriority));
            }
        }

        public bool CanChangeStatus => !IsVoid && !IsStatusUpdating && StatusOptions.Count > 1;
        public bool CanChangePriority => !IsVoid && !IsPriorityUpdating && PriorityOptions.Count > 1;

        public bool MatchesSearch(string? term) =>
            Contains(JobOrderId, term) || Contains(SourceProformaId, term) || Contains(CustomerName, term)
            || Contains(ProjectName, term) || Contains(Status, term) || Contains(Priority, term)
            || Contains(DeliveryNoteIds, term);

        private void RequestStatusChange(string status)
        {
            if (IsVoid || _onStatusChanged == null || string.IsNullOrWhiteSpace(status)) return;
            IsStatusUpdating = true;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                try { await _onStatusChanged(Id, status).ConfigureAwait(true); }
                finally { IsStatusUpdating = false; }
            }, $"Document Register JO status {JobOrderId}");
        }

        private void RequestPriorityChange(string priority)
        {
            if (IsVoid || _onPriorityChanged == null || string.IsNullOrWhiteSpace(priority)) return;
            IsPriorityUpdating = true;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                try { await _onPriorityChanged(Id, priority).ConfigureAwait(true); }
                finally { IsPriorityUpdating = false; }
            }, $"Document Register JO priority {JobOrderId}");
        }

        private static bool Contains(string value, string? term) =>
            string.IsNullOrWhiteSpace(term) || value.Contains(term.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public sealed class DeliveryNoteRegisterRow : ViewModelBase
    {
        private readonly System.Func<int, string, Task>? _onStatusChanged;
        private string _selectedStatus;
        private bool _isStatusUpdating;

        public DeliveryNoteRegisterRow(
            DeliveryNoteListItem source,
            System.Func<int, string, Task>? onStatusChanged)
        {
            Id = source.Id;
            DeliveryNoteId = source.DeliveryNoteId ?? string.Empty;
            JobOrderId = source.JobOrderId ?? string.Empty;
            ProformaId = source.ProformaId ?? string.Empty;
            CustomerName = source.CustomerName ?? string.Empty;
            ProjectName = source.ProjectName ?? string.Empty;
            Status = source.Status ?? string.Empty;
            DeliveryType = source.DeliveryType ?? string.Empty;
            DeliveryDate = source.DeliveryDate;
            TotalDeliveredQty = source.TotalDeliveredQty;
            TotalBalanceQty = source.TotalBalanceQty;
            TaxInvoiceId = source.TaxInvoiceId ?? string.Empty;
            HasTaxInvoice = source.HasTaxInvoice;
            IsVoid = source.IsVoid;
            StatusOptions = StatusTransitions.GetAllowedTargets(StatusTransitions.Kind.DeliveryNote, Status);
            _onStatusChanged = onStatusChanged;
            _selectedStatus = StatusOptions.FirstOrDefault(option =>
                string.Equals(option, Status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Status.Trim();
        }

        public int Id { get; }
        public string DeliveryNoteId { get; }
        public string JobOrderId { get; }
        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string Status { get; }
        public string DeliveryType { get; }
        public System.DateTime DeliveryDate { get; }
        public int TotalDeliveredQty { get; }
        public int TotalBalanceQty { get; }
        public string TaxInvoiceId { get; }
        public bool HasTaxInvoice { get; }
        public bool IsVoid { get; }
        public IReadOnlyList<string> StatusOptions { get; }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus == value) return;
                _selectedStatus = value ?? string.Empty;
                OnPropertyChanged();
                RequestStatusChange(_selectedStatus);
            }
        }

        public bool IsStatusUpdating
        {
            get => _isStatusUpdating;
            private set
            {
                if (_isStatusUpdating == value) return;
                _isStatusUpdating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanChangeStatus));
            }
        }

        public bool CanChangeStatus => !IsVoid && !IsStatusUpdating && StatusOptions.Count > 1;

        public bool MatchesSearch(string? term) =>
            Contains(DeliveryNoteId, term) || Contains(JobOrderId, term) || Contains(ProformaId, term)
            || Contains(CustomerName, term) || Contains(ProjectName, term) || Contains(Status, term)
            || Contains(DeliveryType, term) || Contains(TaxInvoiceId, term);

        private void RequestStatusChange(string status)
        {
            if (IsVoid || _onStatusChanged == null || string.IsNullOrWhiteSpace(status)) return;
            IsStatusUpdating = true;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                try { await _onStatusChanged(Id, status).ConfigureAwait(true); }
                finally { IsStatusUpdating = false; }
            }, $"Document Register DN status {DeliveryNoteId}");
        }

        private static bool Contains(string value, string? term) =>
            string.IsNullOrWhiteSpace(term) || value.Contains(term.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}

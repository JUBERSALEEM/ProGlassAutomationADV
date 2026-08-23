using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Inquiry
{
    /// <summary>
    /// Shell ViewModel for the approved Inquiry list. Persistence is added
    /// separately; an empty collection is truthful and never demo/fake data.
    /// </summary>
    public sealed class InquiryListViewModel : ViewModelBase
    {
        private readonly IInquiryRepository _repository;
        private readonly IInquiryWorkflowService _workflow;
        private readonly InquiryImportExportService _io;
        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _debounceCts;
        public ObservableCollection<InquiryListRow> Inquiries { get; } = new();
        public ObservableCollection<string> DateRanges { get; } = new() { "Today", "Yesterday", "Last 7 Days", "Last 30 Days", "Custom" };
        public ObservableCollection<string> Statuses { get; } = new() { "All statuses", "Received", "In progress", "Sent", "Hold", "Confirmed", "Converted", "Cancelled" };
        public ObservableCollection<string> Salesmen { get; } = new() { "All salesmen" };
        public ObservableCollection<string> PreparedByUsers { get; } = new() { "All prepared by" };
        public ObservableCollection<string> Priorities { get; } = new() { "All priorities", "Urgent", "High", "Normal", "Low" };
        public ObservableCollection<string> SortOptions { get; } = new() { "Updated newest", "Received newest", "Received oldest", "Inquiry number" };

        private InquiryListRow? _selectedInquiry;
        public InquiryListRow? SelectedInquiry
        {
            get => _selectedInquiry;
            set
            {
                if (ReferenceEquals(_selectedInquiry, value)) return;
                _selectedInquiry = value;
                IsDetailsDrawerOpen = value != null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectionText));
                if (value != null)
                    AsyncUtility.SafeFireAndForget(() => LoadSelectedDetailAsync(value), "Inquiry drawer detail");
            }
        }

        private bool _isMoreFiltersOpen;
        public bool IsMoreFiltersOpen { get => _isMoreFiltersOpen; set { _isMoreFiltersOpen = value; OnPropertyChanged(); } }
        private bool _isDetailsDrawerOpen;
        public bool IsDetailsDrawerOpen { get => _isDetailsDrawerOpen; private set { _isDetailsDrawerOpen = value; OnPropertyChanged(); } }
        public bool HasSelection => SelectedInquiry != null;
        public bool HasInquiries => Inquiries.Count > 0;
        public bool HasNoInquiries => !HasInquiries;
        public string SelectionText => SelectedInquiry == null ? "" : "1 selected";
        private string _pageStatus = "No inquiries found";
        public string PageStatus { get => _pageStatus; private set { _pageStatus = value; OnPropertyChanged(); } }
        private int _totalCount, _receivedCount, _sentCount, _confirmedCount, _holdCount, _cancelledCount;
        public int TotalCount { get => _totalCount; private set { _totalCount = value; OnPropertyChanged(); } }
        public int ReceivedCount { get => _receivedCount; private set { _receivedCount = value; OnPropertyChanged(); } }
        public int SentCount { get => _sentCount; private set { _sentCount = value; OnPropertyChanged(); } }
        public int ConfirmedCount { get => _confirmedCount; private set { _confirmedCount = value; OnPropertyChanged(); } }
        public int HoldCount { get => _holdCount; private set { _holdCount = value; OnPropertyChanged(); } }
        public int CancelledCount { get => _cancelledCount; private set { _cancelledCount = value; OnPropertyChanged(); } }

        private string _searchText = "";
        public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); DebounceReload(); } }
        private string _selectedDateRange = "Today";
        public string SelectedDateRange { get => _selectedDateRange; set { if (_selectedDateRange == value) return; _selectedDateRange = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedStatus = "All statuses";
        public string SelectedStatus { get => _selectedStatus; set { if (_selectedStatus == value) return; _selectedStatus = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedSalesman = "All salesmen";
        public string SelectedSalesman { get => _selectedSalesman; set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedPreparedBy = "All prepared by";
        public string SelectedPreparedBy { get => _selectedPreparedBy; set { if (_selectedPreparedBy == value) return; _selectedPreparedBy = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedPriority = "All priorities";
        public string SelectedPriority { get => _selectedPriority; set { if (_selectedPriority == value) return; _selectedPriority = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedSort = "Updated newest";
        public string SelectedSort { get => _selectedSort; set { if (_selectedSort == value) return; _selectedSort = value; OnPropertyChanged(); ResetAndReload(); } }
        private bool _hasAttachmentsOnly;
        public bool HasAttachmentsOnly { get => _hasAttachmentsOnly; set { if (_hasAttachmentsOnly == value) return; _hasAttachmentsOnly = value; OnPropertyChanged(); ResetAndReload(); } }

        public Func<InquiryRecord?, Task<bool>>? EditRequested { get; set; }
        public Func<string?>? ExportPathRequested { get; set; }
        public Func<string?>? ImportPathRequested { get; set; }
        public Func<int, bool>? ConfirmImportRequested { get; set; }
        public ICommand NewInquiryCommand { get; }
        public ICommand FilterByStatisticCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ToggleMoreFiltersCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand OpenWorkspaceCommand { get; }
        public ICommand ConvertToPiCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand BulkChangeOwnerCommand { get; }
        public ICommand BulkChangeStatusCommand { get; }
        public ICommand ExportSelectedCommand { get; }

        public InquiryListViewModel() : this(App.Inquiries ?? new InquiryRepository(),
            new InquiryWorkflowService(App.Inquiries ?? new InquiryRepository()), new InquiryImportExportService()) { }

        public InquiryListViewModel(IInquiryRepository repository,
            IInquiryWorkflowService workflow, InquiryImportExportService io)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _io = io ?? throw new ArgumentNullException(nameof(io));
            NewInquiryCommand = new RelayCommand(async _ => await EditAsync(null));
            FilterByStatisticCommand = new RelayCommand(p =>
            {
                SelectedStatus = p as string ?? "All statuses";
            });
            ExportCommand = new RelayCommand(async _ => await ExportAsync());
            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            ResetFiltersCommand = new RelayCommand(_ => { ResetFilters(); ResetAndReload(); });
            RefreshCommand = new RelayCommand(_ => Reload());
            ToggleMoreFiltersCommand = new RelayCommand(_ => IsMoreFiltersOpen = !IsMoreFiltersOpen);
            PreviousPageCommand = new RelayCommand(_ => { if (CanGoPrevious) { CurrentPage--; Reload(); } }, _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => { if (CanGoNext) { CurrentPage++; Reload(); } }, _ => CanGoNext);
            CloseDetailsCommand = new RelayCommand(_ => IsDetailsDrawerOpen = false);
            OpenWorkspaceCommand = new RelayCommand(async _ =>
            {
                if (SelectedInquiry != null)
                {
                    var detail = await _repository.GetDetailAsync(SelectedInquiry.Id);
                    await EditAsync(detail);
                }
            }, _ => SelectedInquiry != null);
            ConvertToPiCommand = new RelayCommand(_ => SetStatus("Inquiry conversion is not implemented yet."));
            ShowHistoryCommand = new RelayCommand(_ => SetStatus("Inquiry history is shown in the detail panel."));
            OpenAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachmentPreview file)
                {
                    var result = ProcessStartGuard.TryOpenFile(file.StoragePath, CorrelationContext.NewId());
                    if (!result.IsSuccess) SetStatus(result.Error ?? "Could not open attachment.");
                }
            });
            BulkChangeOwnerCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            BulkChangeStatusCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            ExportSelectedCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            AsyncUtility.SafeFireAndForget(LoadFilterSourcesAsync, "Inquiry filter sources");
            Reload();
        }

        private async Task ExportAsync()
        {
            if (!Services.Audit.PermissionService.Require(Services.Audit.AppPermissions.ProformaExport, "export inquiries")) return;
            string? path = ExportPathRequested?.Invoke(); if (string.IsNullOrWhiteSpace(path)) return;
            var rows = Inquiries.Select(x => new InquiryListProjection
            {
                Id = x.Id, InquiryNumber = x.InquiryNumber, InquiryReference = x.InquiryReference,
                CustomerName = x.CustomerName, CustomerLocation = x.CustomerLocation,
                ProjectName = x.Project, ProjectDescription = x.Subject, SalesmanName = x.Salesman,
                PreparedByName = x.PreparedBy, ReceivedAt = x.ReceivedAt, UpdatedAt = x.UpdatedAt,
                Status = x.Status, AttachmentCount = x.AttachmentCount
            });
            await _io.ExportCsvAsync(path, rows); SetStatus("Inquiry page exported.");
        }

        private async Task ImportAsync()
        {
            string? path = ImportPathRequested?.Invoke(); if (string.IsNullOrWhiteSpace(path)) return;
            var rows = await _io.ParseCsvAsync(path);
            if (rows.Count == 0) { SetStatus("Import contains no valid customer/project rows."); return; }
            if (ConfirmImportRequested?.Invoke(rows.Count) != true) return;
            int created = 0;
            foreach (var row in rows) { await _workflow.CreateAsync(row); created++; }
            SetStatus($"Imported {created} inquiries."); ResetAndReload();
        }

        private async Task EditAsync(InquiryRecord? record)
        {
            if (EditRequested == null) { SetStatus("Inquiry editor is unavailable."); return; }
            if (await EditRequested(record)) ResetAndReload();
        }

        private async Task LoadFilterSourcesAsync()
        {
            try
            {
                if (App.MasterData != null)
                    foreach (var x in await App.MasterData.GetAllSalesmenAsync())
                        if (!Salesmen.Contains(x.Name)) Salesmen.Add(x.Name);
                foreach (var user in await Services.Audit.AuditService.GetUsersAsync())
                {
                    string name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
                    if (!PreparedByUsers.Contains(name)) PreparedByUsers.Add(name);
                }
            }
            catch (Exception ex) { SetStatus("Filter sources unavailable: " + ex.Message); }
        }

        private void DebounceReload()
        {
            _debounceCts?.Cancel(); _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                await Task.Delay(300, token);
                token.ThrowIfCancellationRequested();
                ResetAndReload();
            }, "Inquiry search debounce");
        }

        private void ResetAndReload()
        {
            CurrentPage = 1;
            Reload();
        }

        private void Reload()
        {
            _loadCts?.Cancel(); _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            AsyncUtility.SafeFireAndForget(() => LoadAsync(_loadCts.Token), "Inquiry list load");
        }

        private async Task LoadAsync(CancellationToken ct)
        {
            SetStatus("Loading inquiries…");
            var range = DateRange(SelectedDateRange, DateTimeOffset.Now);
            var query = new InquiryQuery
            {
                Search = SearchText, Status = SelectedStatus,
                SalesmanName = SelectedSalesman, PreparedByName = SelectedPreparedBy,
                From = range.From, ToExclusive = range.ToExclusive,
                HasAttachmentsOnly = HasAttachmentsOnly, Priority = SelectedPriority,
                Sort = SelectedSort, Page = CurrentPage, PageSize = 50
            };
            var page = await _repository.QueryAsync(query, ct);
            var stats = await _repository.GetStatisticsAsync(query, ct);
            ct.ThrowIfCancellationRequested();
            Inquiries.Clear();
            foreach (var x in page.Items)
            {
                Inquiries.Add(new InquiryListRow
                {
                    Id = x.Id, InquiryNumber = x.InquiryNumber,
                    InquiryReference = x.InquiryReference, ReceivedAt = x.ReceivedAt,
                    UpdatedAt = x.UpdatedAt, CustomerName = x.CustomerName,
                    CustomerLocation = x.CustomerLocation, Project = x.ProjectName,
                    Subject = x.ProjectDescription, Salesman = x.SalesmanName,
                    PreparedBy = x.PreparedByName, PreparedByRole = x.PreparedByRole,
                    InquiryValue = x.InquiryValue, Priority = x.Priority, AttachmentCount = x.AttachmentCount,
                    AttachmentBadgeText = x.AttachmentBadgeText, Status = x.Status, ProformaId = x.ProformaNumber,
                    DownstreamStatus = string.IsNullOrWhiteSpace(x.JobOrderNumber)
                        ? "Not released" : x.JobOrderNumber,
                    UpdatedBy = x.UpdatedBy
                });
            }
            OnPropertyChanged(nameof(HasInquiries)); OnPropertyChanged(nameof(HasNoInquiries));
            TotalCount = stats.All; ReceivedCount = stats.Received;
            SentCount = stats.Sent; ConfirmedCount = stats.Confirmed;
            HoldCount = stats.Hold; CancelledCount = stats.Cancelled;
            TotalPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);
            PageStatus = page.TotalCount == 0 ? "No inquiries found"
                : $"Showing {(page.Page - 1) * page.PageSize + 1}–{Math.Min(page.TotalCount, page.Page * page.PageSize)} of {page.TotalCount}";
            SetStatus(PageStatus);
        }

        private async Task LoadSelectedDetailAsync(InquiryListRow row)
        {
            var attachments = await _repository.GetAttachmentsAsync(row.Id);
            var communications = await _repository.GetCommunicationsAsync(row.Id);
            if (!ReferenceEquals(SelectedInquiry, row)) return;
            row.Attachments.Clear();
            foreach (var x in attachments)
                row.Attachments.Add(new InquiryAttachmentPreview
                {
                    Id = x.Id, FileName = x.FileName, Extension = x.Extension,
                    FileSize = x.FileSize, StoragePath = x.StoragePath,
                    ProcessingStatus = x.AttachmentType
                });
            row.RecentActivity.Clear();
            foreach (var x in communications)
                row.RecentActivity.Add($"{x.OccurredAt:dd MMM yyyy · hh:mm tt}  {x.Title}" +
                    (string.IsNullOrWhiteSpace(x.Description) ? "" : " — " + x.Description));
            OnPropertyChanged(nameof(SelectedInquiry));
        }

        private static (DateTimeOffset? From, DateTimeOffset? ToExclusive) DateRange(string value, DateTimeOffset now)
        {
            var today = new DateTimeOffset(now.Date, now.Offset);
            return value switch
            {
                "Today" => (today, today.AddDays(1)),
                "Yesterday" => (today.AddDays(-1), today),
                "Last 7 Days" => (today.AddDays(-6), today.AddDays(1)),
                "Last 30 Days" => (today.AddDays(-29), today.AddDays(1)),
                _ => (null, null)
            };
        }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; private set { _currentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoPrevious)); OnPropertyChanged(nameof(CanGoNext)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
        private int _totalPages;
        public int TotalPages { get => _totalPages; private set { _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        private string _statusText = "Inquiry list ready.";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        private void SetStatus(string value) => StatusText = value;
        private void ResetFilters()
        {
            _searchText = ""; _selectedDateRange = "Today"; _selectedStatus = "All statuses";
            _selectedSalesman = "All salesmen"; _selectedPreparedBy = "All prepared by";
            _selectedPriority = "All priorities"; _selectedSort = "Updated newest";
            _hasAttachmentsOnly = false;
            OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(SelectedDateRange));
            OnPropertyChanged(nameof(SelectedStatus)); OnPropertyChanged(nameof(SelectedSalesman));
            OnPropertyChanged(nameof(SelectedPreparedBy)); OnPropertyChanged(nameof(SelectedPriority));
            OnPropertyChanged(nameof(SelectedSort)); OnPropertyChanged(nameof(HasAttachmentsOnly));
        }
    }

    public sealed class InquiryListRow
    {
        public int Id { get; set; }
        public bool IsSelected { get; set; }
        public string InquiryNumber { get; set; } = "";
        public string InquiryReference { get; set; } = "";
        public DateTimeOffset ReceivedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerLocation { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Project { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string PreparedBy { get; set; } = "";
        public string PreparedByRole { get; set; } = "";
        public string Priority { get; set; } = "Normal";
        public decimal InquiryValue { get; set; }
        public int AttachmentCount { get; set; }
        public string AttachmentBadgeText { get; set; } = "";
        public string Status { get; set; } = "Received";
        public string ProformaId { get; set; } = "";
        public string DownstreamStatus { get; set; } = "Not started";
        public string UpdatedBy { get; set; } = "";
        public string ContactSummary { get; set; } = "";
        public string NotesPreview { get; set; } = "";
        public string DocumentChainText { get; set; } = "Inquiry → PI not created";
        public ObservableCollection<InquiryAttachmentPreview> Attachments { get; } = new();
        public ObservableCollection<string> RecentActivity { get; } = new();
    }

    public sealed class InquiryAttachmentPreview
    {
        public long Id { get; set; }
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = "";
        public long FileSize { get; set; }
        public string StoragePath { get; set; } = "";
        public string ProcessingStatus { get; set; } = "Document";
        public string FileSizeDisplay => FileSize < 1024 * 1024
            ? $"{FileSize / 1024d:N0} KB" : $"{FileSize / (1024d * 1024d):N1} MB";
    }
}

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Inquiry
{
    /// <summary>
    /// Inquiry register — 2026 SaaS rebuild.
    ///
    /// Read path: segmented status tabs → database-side paged projection.
    /// Write path: editor dialog / drawer quick actions → workflow service
    /// (guarded transitions, audit, version snapshots).
    ///
    /// Design notes (fixes from the senior analysis):
    /// - date filter defaults to "All time" (S1: saved inquiries vanished);
    /// - cancellations return quietly (S7: log spam);
    /// - selection survives a reload (A7: drawer slammed shut after save);
    /// - no placeholder commands — every visible control does real work.
    /// </summary>
    public sealed class InquiryListViewModel : ViewModelBase
    {
        private readonly IInquiryRepository _repository;
        private readonly IInquiryWorkflowService _workflow;
        private readonly InquiryAttachmentService _attachments;
        private readonly InquiryImportExportService _io;
        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _debounceCts;
        private int _reloadGeneration;

        public ObservableCollection<InquiryListRow> Inquiries { get; } = new();
        public ObservableCollection<InquiryStatusTab> StatusTabs { get; } = new();
        public ObservableCollection<string> DateRanges { get; } =
            new() { "All time", "Today", "Yesterday", "Last 7 Days", "Last 30 Days" };
        public ObservableCollection<string> Salesmen { get; } = new() { "All salesmen" };
        public ObservableCollection<string> PreparedByUsers { get; } = new() { "All prepared by" };
        public ObservableCollection<string> Priorities { get; } = new() { "All priorities", "Urgent", "High", "Normal", "Low" };
        public ObservableCollection<string> SortOptions { get; } =
            new() { "Updated newest", "Updated oldest", "Received newest", "Received oldest", "Inquiry number", "Value highest" };

        // ── Selection & drawer ─────────────────────────────────────────

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
                OnPropertyChanged(nameof(CanConvertSelected));
                if (value != null)
                    AsyncUtility.SafeFireAndForget(() => LoadSelectedDetailAsync(value), "Inquiry drawer detail");
            }
        }

        private bool _isDetailsDrawerOpen;
        public bool IsDetailsDrawerOpen { get => _isDetailsDrawerOpen; private set { _isDetailsDrawerOpen = value; OnPropertyChanged(); } }
        public bool HasSelection => SelectedInquiry != null;
        public bool CanConvertSelected => SelectedInquiry is { CanConvert: true };

        // ── KPI strip ──────────────────────────────────────────────────

        private decimal _pipelineValue;
        public decimal PipelineValue { get => _pipelineValue; private set { _pipelineValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(PipelineValueText)); } }
        public string PipelineValueText => PipelineValue == 0 ? "—" : $"AED {PipelineValue:N0}";
        private int _openCount;
        public int OpenCount { get => _openCount; private set { _openCount = value; OnPropertyChanged(); } }
        private int _awaitingCount;
        public int AwaitingCount { get => _awaitingCount; private set { _awaitingCount = value; OnPropertyChanged(); } }
        private int _confirmedCount;
        public int ConfirmedCount { get => _confirmedCount; private set { _confirmedCount = value; OnPropertyChanged(); } }

        private string _pageStatus = "Loading…";
        public string PageStatus { get => _pageStatus; private set { _pageStatus = value; OnPropertyChanged(); } }
        public bool HasInquiries => Inquiries.Count > 0;

        // ── Filters ────────────────────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActiveFilters)); DebounceReload(); }
        }
        // S1 fix: "All time" default — a register must not hide rows by accident.
        private string _selectedDateRange = "All time";
        public string SelectedDateRange { get => _selectedDateRange; set { if (_selectedDateRange == value) return; _selectedDateRange = value; OnPropertyChanged(); ResetAndReload(); } }
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
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchText) || SelectedDateRange != "All time"
            || SelectedSalesman != "All salesmen" || SelectedPreparedBy != "All prepared by"
            || SelectedPriority != "All priorities" || HasAttachmentsOnly
            || SelectedStatusKey.Length > 0;

        /// <summary>"" = All statuses; otherwise a controlled status value.</summary>
        public string SelectedStatusKey
        {
            get => StatusTabs.FirstOrDefault(x => x.IsSelected)?.Key ?? "";
            private set
            {
                foreach (var tab in StatusTabs)
                    tab.IsSelected = tab.Key == value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
            }
        }

        // ── UI callbacks (kept UI-free for testability) ────────────────

        public Func<InquiryRecord?, Task<bool>>? EditRequested { get; set; }
        public Func<InquiryRecord, Task>? CreateProformaRequested { get; set; }
        public Func<string?>? ExportPathRequested { get; set; }
        public Func<string?>? ImportPathRequested { get; set; }
        public Func<int, bool>? ConfirmImportRequested { get; set; }
        public Func<InquiryAttachment, bool>? ConfirmDeleteAttachmentRequested { get; set; }
        public Action<InquiryAttachment>? PreviewAttachmentRequested { get; set; }

        // ── Commands ───────────────────────────────────────────────────

        public ICommand NewInquiryCommand { get; }
        public ICommand EditInquiryCommand { get; }
        public ICommand SelectStatusTabCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand OpenWorkspaceCommand { get; }
        public ICommand ConvertToPiCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand PreviewAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }

        public InquiryListViewModel() : this(
            App.Inquiries ?? new InquiryRepository(),
            new InquiryWorkflowService(App.Inquiries ?? new InquiryRepository()),
            new InquiryAttachmentService(App.Inquiries ?? new InquiryRepository()),
            new InquiryImportExportService()) { }

        public InquiryListViewModel(IInquiryRepository repository,
            IInquiryWorkflowService workflow, InquiryAttachmentService attachments,
            InquiryImportExportService io)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _io = io ?? throw new ArgumentNullException(nameof(io));

            StatusTabs.Add(new InquiryStatusTab { Key = "", Label = "All", Count = 0, IsSelected = true });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Received, Label = "Received" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Prepared, Label = "Prepared" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Sent, Label = "Sent" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Confirmed, Label = "Confirmed" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Hold, Label = "Hold" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Cancelled, Label = "Cancelled" });

            NewInquiryCommand = new RelayCommand(async _ => await EditAsync(null));
            EditInquiryCommand = new RelayCommand(async p =>
            {
                var row = p as InquiryListRow ?? SelectedInquiry;
                if (row != null) await EditRowAsync(row);
            }, _ => SelectedInquiry != null);
            SelectStatusTabCommand = new RelayCommand(p =>
            {
                SelectedStatusKey = p as string ?? "";
                ResetAndReload();
            });
            ExportCommand = new RelayCommand(async _ => await ExportAsync());
            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
            RefreshCommand = new RelayCommand(_ => Reload());
            PreviousPageCommand = new RelayCommand(_ => { if (CanGoPrevious) { CurrentPage--; Reload(); } }, _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => { if (CanGoNext) { CurrentPage++; Reload(); } }, _ => CanGoNext);
            CloseDetailsCommand = new RelayCommand(_ => IsDetailsDrawerOpen = false);
            OpenWorkspaceCommand = new RelayCommand(async _ => { if (SelectedInquiry != null) await EditRowAsync(SelectedInquiry); }, _ => SelectedInquiry != null);
            ConvertToPiCommand = new RelayCommand(async _ => await ConvertToPiAsync(), _ => CanConvertSelected);
            ChangeStatusCommand = new RelayCommand(async p => await ChangeSelectedStatusAsync(p as string), p => SelectedInquiry != null && p is string);
            OpenAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachmentItem item)
                {
                    var result = _attachments.Open(item.Attachment);
                    if (!result.IsSuccess) SetStatus(result.Error);
                }
            });
            PreviewAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachmentItem item) PreviewAttachmentRequested?.Invoke(item.Attachment);
            });
            DeleteAttachmentCommand = new RelayCommand(async p =>
            {
                if (p is InquiryAttachmentItem item && SelectedInquiry is { } row)
                {
                    if (ConfirmDeleteAttachmentRequested?.Invoke(item.Attachment) != true) return;
                    try
                    {
                        bool removed = await _attachments.DeleteAsync(item.Attachment);
                        if (removed)
                        {
                            row.Attachments.Remove(item);
                            row.AttachmentCount = Math.Max(0, row.AttachmentCount - 1);
                            SetStatus($"Deleted '{item.FileName}'.");
                            AsyncUtility.SafeFireAndForget(() => LoadSelectedDetailAsync(row), "Inquiry drawer refresh");
                        }
                    }
                    catch (Exception ex) { SetStatus("Delete failed: " + ExceptionMessages.Deep(ex)); }
                }
            });

            AsyncUtility.SafeFireAndForget(LoadFilterSourcesAsync, "Inquiry filter sources");
            Reload();
        }

        // ── Loading ────────────────────────────────────────────────────

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
            catch (Exception ex) { SetStatus("Filter sources unavailable: " + ExceptionMessages.Deep(ex)); }
        }

        private void DebounceReload()
        {
            _debounceCts?.Cancel(); _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    token.ThrowIfCancellationRequested();
                    ResetAndReload();
                }
                catch (OperationCanceledException) { /* superseded keystroke */ }
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
            int generation = ++_reloadGeneration;
            AsyncUtility.SafeFireAndForget(() => LoadAsync(_loadCts.Token, generation), "Inquiry list load");
        }

        private async Task LoadAsync(CancellationToken ct, int generation)
        {
            IsLoading = true; ErrorText = ""; SetStatus("Loading inquiries…");
            try
            {
                var range = DateRange(SelectedDateRange, DateTime.Now);
                var query = new InquiryQuery
                {
                    Search = SearchText, Status = SelectedStatusKey.Length == 0 ? "All statuses" : SelectedStatusKey,
                    SalesmanName = SelectedSalesman, PreparedByName = SelectedPreparedBy,
                    From = range.From, ToExclusive = range.ToExclusive,
                    HasAttachmentsOnly = HasAttachmentsOnly, Priority = SelectedPriority,
                    Sort = SelectedSort, Page = CurrentPage, PageSize = 50
                };
                var page = await _repository.QueryAsync(query, ct);
                var stats = await _repository.GetStatisticsAsync(query, ct);
                ct.ThrowIfCancellationRequested();
                // A7 fix: keep the drawer open across reloads when the row is
                // still on (or returned to) the current page.
                int keepId = SelectedInquiry?.Id ?? 0;
                InquiryListRow? restored = null;
                Inquiries.Clear();
                foreach (var x in page.Items)
                {
                    var row = InquiryListRow.FromProjection(x);
                    Inquiries.Add(row);
                    if (row.Id == keepId) restored = row;
                }
                OnPropertyChanged(nameof(HasInquiries));
                ApplyStatistics(stats);
                TotalPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);
                PageStatus = page.TotalCount == 0
                    ? "No inquiries match the current filters"
                    : $"Showing {(page.Page - 1) * page.PageSize + 1}–{Math.Min(page.TotalCount, page.Page * page.PageSize)} of {page.TotalCount}";
                SetStatus(PageStatus);
                // Clearing the collection already nulled the selection; restore
                // it (and the drawer) when the row is still on this page.
                if (restored != null && generation == _reloadGeneration)
                    SelectedInquiry = restored;
            }
            catch (OperationCanceledException)
            {
                // S7 fix: superseded load — quiet, not an error.
            }
            catch (Exception ex)
            {
                ErrorText = "Could not load inquiries: " + ExceptionMessages.Deep(ex);
                SetStatus(ErrorText);
            }
            finally { if (generation == _reloadGeneration) IsLoading = false; }
        }

        private void ApplyStatistics(InquiryStatistics stats)
        {
            PipelineValue = stats.OpenPipelineValue;
            OpenCount = stats.Received + stats.Prepared;
            AwaitingCount = stats.Sent;
            ConfirmedCount = stats.Confirmed;
            foreach (var tab in StatusTabs)
            {
                tab.Count = tab.Key switch
                {
                    "" => stats.All,
                    InquiryStatuses.Received => stats.Received,
                    InquiryStatuses.Prepared => stats.Prepared,
                    InquiryStatuses.Sent => stats.Sent,
                    InquiryStatuses.Confirmed => stats.Confirmed,
                    InquiryStatuses.Hold => stats.Hold,
                    InquiryStatuses.Cancelled => stats.Cancelled,
                    _ => 0
                };
            }
        }

        private async Task LoadSelectedDetailAsync(InquiryListRow row)
        {
            try
            {
                var attachments = await _attachments.GetAsync(row.Id);
                var communications = await _repository.GetCommunicationsAsync(row.Id);
                var detail = await _repository.GetDetailAsync(row.Id);
                if (!ReferenceEquals(SelectedInquiry, row)) return;
                row.Attachments.Clear();
                foreach (var x in attachments)
                    row.Attachments.Add(InquiryAttachmentItem.FromModel(x));
                row.RecentActivity.Clear();
                foreach (var x in communications)
                    row.RecentActivity.Add(new InquiryActivityItem
                    {
                        Title = x.Title,
                        Description = x.Description,
                        Actor = x.ActorName,
                        OccurredAt = x.OccurredAt
                    });
                if (detail != null)
                {
                    row.Notes = detail.Notes;
                    row.NotesPreview = string.IsNullOrWhiteSpace(detail.Notes)
                        ? "" : detail.Notes.Trim().Replace("\r", "");
                }
                row.IsDetailLoaded = true;
                OnPropertyChanged(nameof(SelectedInquiry));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetStatus("Detail unavailable: " + ExceptionMessages.Deep(ex)); }
        }

        // ── Actions ────────────────────────────────────────────────────

        private async Task EditRowAsync(InquiryListRow row)
        {
            var detail = await _repository.GetDetailAsync(row.Id);
            await EditAsync(detail);
        }

        private async Task EditAsync(InquiryRecord? record)
        {
            if (EditRequested == null) { SetStatus("Inquiry editor is unavailable."); return; }
            if (await EditRequested(record)) ResetAndReload();
        }

        private async Task ConvertToPiAsync()
        {
            if (SelectedInquiry == null || CreateProformaRequested == null) return;
            var detail = await _repository.GetDetailAsync(SelectedInquiry.Id);
            if (detail == null) return;
            if (detail.ProformaInvoiceId.HasValue || !string.IsNullOrWhiteSpace(detail.ProformaNumber))
            {
                SetStatus($"PI {detail.ProformaNumber} already exists for this inquiry.");
                return;
            }
            await CreateProformaRequested(detail);
        }

        private async Task ChangeSelectedStatusAsync(string? next)
        {
            var row = SelectedInquiry;
            if (row == null || string.IsNullOrWhiteSpace(next)) return;
            try
            {
                if (!InquiryWorkflowService.CanTransition(row.Status, next))
                {
                    SetStatus($"An inquiry cannot move from {row.Status} to {next}.");
                    return;
                }
                bool changed = await _workflow.ChangeStatusAsync(row.Id, row.Status, next);
                if (changed)
                {
                    SetStatus($"Inquiry {row.InquiryNumber} is now {InquiryStatuses.Normalize(next)}.");
                    Reload();
                }
            }
            catch (Exception ex) { SetStatus(ExceptionMessages.Deep(ex)); }
        }

        private async Task ExportAsync()
        {
            if (!Services.Audit.PermissionService.Require(Services.Audit.AppPermissions.ProformaExport, "export inquiries")) return;
            string? path = ExportPathRequested?.Invoke();
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                // Export every row matching the current filter (paged fetch, bounded),
                // not just the visible page.
                var range = DateRange(SelectedDateRange, DateTime.Now);
                InquiryQuery Build(int page) => new()
                {
                    Search = SearchText, Status = SelectedStatusKey.Length == 0 ? "All statuses" : SelectedStatusKey,
                    SalesmanName = SelectedSalesman, PreparedByName = SelectedPreparedBy,
                    From = range.From, ToExclusive = range.ToExclusive,
                    HasAttachmentsOnly = HasAttachmentsOnly, Priority = SelectedPriority,
                    Sort = SelectedSort, Page = page, PageSize = 200
                };
                var rows = new System.Collections.Generic.List<InquiryListProjection>();
                var result = await _repository.QueryAsync(Build(1));
                rows.AddRange(result.Items);
                while (result.HasNext && rows.Count < 5000)
                {
                    result = await _repository.QueryAsync(Build(result.Page + 1));
                    rows.AddRange(result.Items);
                }
                await _io.ExportCsvAsync(path, rows);
                SetStatus($"Exported {rows.Count} inquiries to {System.IO.Path.GetFileName(path)}.");
            }
            catch (Exception ex) { SetStatus("Export failed: " + ExceptionMessages.Deep(ex)); }
        }

        private async Task ImportAsync()
        {
            string? path = ImportPathRequested?.Invoke();
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var parse = await _io.ParseCsvAsync(path);
                if (parse.Rows.Count == 0)
                {
                    SetStatus(parse.SkippedInvalidRows > 0
                        ? $"Import contains no valid rows ({parse.SkippedInvalidRows} rows lacked customer or project)."
                        : "Import contains no rows.");
                    return;
                }
                if (ConfirmImportRequested?.Invoke(parse.Rows.Count) != true) return;
                int created = 0, failed = 0;
                foreach (var row in parse.Rows)
                {
                    try { await _workflow.CreateAsync(row); created++; }
                    catch (Services.Audit.PermissionDeniedException) { throw; }
                    catch (Exception) { failed++; }
                }
                string suffix = parse.SkippedInvalidRows > 0 ? $", {parse.SkippedInvalidRows} invalid skipped" : "";
                string dupes = parse.DuplicateNumbersRegenerated > 0
                    ? $", {parse.DuplicateNumbersRegenerated} duplicate numbers regenerated" : "";
                SetStatus($"Imported {created} inquiries{suffix}{dupes}{(failed > 0 ? $", {failed} failed" : "")}.");
                ResetAndReload();
            }
            catch (Services.Audit.PermissionDeniedException ex) { SetStatus(ex.Message); }
            catch (Exception ex) { SetStatus("Import failed: " + ExceptionMessages.Deep(ex)); }
        }

        private static (DateTime? From, DateTime? ToExclusive) DateRange(string value, DateTime now)
        {
            var today = now.Date;
            return value switch
            {
                "Today" => (today, today.AddDays(1)),
                "Yesterday" => (today.AddDays(-1), today),
                "Last 7 Days" => (today.AddDays(-6), today.AddDays(1)),
                "Last 30 Days" => (today.AddDays(-29), today.AddDays(1)),
                _ => (null, null) // "All time" — the honest default
            };
        }

        // ── Pagination & state ─────────────────────────────────────────

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                _currentPage = value; OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoPrevious)); OnPropertyChanged(nameof(CanGoNext));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value; OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        private bool _isLoading = true;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); } }
        private string _errorText = "";
        public string ErrorText { get => _errorText; private set { _errorText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
        private string _statusText = "Loading…";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        private void SetStatus(string value) => StatusText = value;

        private void ResetFilters()
        {
            _searchText = ""; _selectedDateRange = "All time";
            _selectedSalesman = "All salesmen"; _selectedPreparedBy = "All prepared by";
            _selectedPriority = "All priorities"; _selectedSort = "Updated newest";
            _hasAttachmentsOnly = false;
            foreach (var tab in StatusTabs) tab.IsSelected = tab.Key.Length == 0;
            OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(SelectedDateRange));
            OnPropertyChanged(nameof(SelectedSalesman)); OnPropertyChanged(nameof(SelectedPreparedBy));
            OnPropertyChanged(nameof(SelectedPriority)); OnPropertyChanged(nameof(SelectedSort));
            OnPropertyChanged(nameof(HasAttachmentsOnly)); OnPropertyChanged(nameof(HasActiveFilters));
            ResetAndReload();
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Row models
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Segmented status tab with a live count.</summary>
    public sealed class InquiryStatusTab : ViewModelBase
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
        private int _count;
        public int Count { get => _count; set { _count = value; OnPropertyChanged(); } }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }

    /// <summary>One register row. Display text is precomputed for cheap binding.</summary>
    public sealed class InquiryListRow
    {
        public int Id { get; private set; }
        public string InquiryNumber { get; private set; } = "";
        public string InquiryReference { get; private set; } = "";
        public string CustomerName { get; private set; } = "";
        public string CustomerLocation { get; private set; } = "";
        public string Project { get; private set; } = "";
        public string Subject { get; private set; } = "";
        public string Salesman { get; private set; } = "";
        public string PreparedBy { get; private set; } = "";
        public string PreparedByRole { get; private set; } = "";
        public string Status { get; internal set; } = InquiryStatuses.Received;
        public string Priority { get; private set; } = "Normal";
        public decimal InquiryValue { get; private set; }
        public string ValueText => InquiryValue == 0 ? "—" : InquiryValue.ToString("N0", CultureInfo.CurrentCulture);
        public DateTime ReceivedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public string ReceivedDateText => ReceivedAt.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        public string ReceivedTimeText => ReceivedAt.ToString("hh:mm tt", CultureInfo.CurrentCulture);
        public string UpdatedText => UpdatedAt.ToString("dd MMM yyyy · hh:mm tt", CultureInfo.CurrentCulture);
        public string UpdatedBy { get; private set; } = "";
        public string UpdatedByDisplay => string.IsNullOrWhiteSpace(UpdatedBy) ? "" : $"by {UpdatedBy}";
        public int AttachmentCount { get; internal set; }
        public string AttachmentBadgeText { get; private set; } = "";
        public bool HasAttachments => AttachmentCount > 0;
        public string AttachmentChipText => AttachmentCount == 0 ? "None" : $"{AttachmentCount} file{(AttachmentCount == 1 ? "" : "s")}";
        public string ProformaNumber { get; private set; } = "";
        public string JobOrderNumber { get; private set; } = "";
        public bool IsConverted => !string.IsNullOrWhiteSpace(ProformaNumber);
        public bool CanConvert => Status == InquiryStatuses.Confirmed && !IsConverted;
        // Workflow affordances for the drawer (guarded by the same state
        // machine the workflow service enforces server-side).
        public bool CanPrepareWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Prepared);
        public bool CanSendWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Sent);
        public bool CanConfirmWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Confirmed);
        public bool CanHoldWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Hold);
        public bool CanCancelWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Cancelled);
        public string ChainText => IsConverted
            ? "PI " + ProformaNumber + (string.IsNullOrWhiteSpace(JobOrderNumber) ? " · JO not released" : " · JO " + JobOrderNumber)
            : "Not converted to PI yet";
        public string Notes { get; internal set; } = "";
        public string NotesPreview { get; internal set; } = "";
        public bool IsDetailLoaded { get; internal set; }
        public string ContactSummary => string.IsNullOrWhiteSpace(CustomerLocation) ? "" : CustomerLocation;
        /// <summary>Avatar initials for the row card (first letters of the first two words).</summary>
        public string CustomerInitials
        {
            get
            {
                var parts = CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "·";
                return parts.Length == 1
                    ? parts[0][..1].ToUpperInvariant()
                    : (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
            }
        }
        public ObservableCollection<InquiryAttachmentItem> Attachments { get; } = new();
        public ObservableCollection<InquiryActivityItem> RecentActivity { get; } = new();

        public static InquiryListRow FromProjection(InquiryListProjection x) => new()
        {
            Id = x.Id, InquiryNumber = x.InquiryNumber, InquiryReference = x.InquiryReference,
            CustomerName = x.CustomerName, CustomerLocation = x.CustomerLocation,
            Project = x.ProjectName, Subject = x.ProjectDescription,
            Salesman = x.SalesmanName, PreparedBy = x.PreparedByName, PreparedByRole = x.PreparedByRole,
            Status = InquiryStatuses.Normalize(x.Status), Priority = x.Priority,
            InquiryValue = x.InquiryValue, ReceivedAt = x.ReceivedAt, UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy, AttachmentCount = x.AttachmentCount,
            AttachmentBadgeText = x.AttachmentBadgeText, ProformaNumber = x.ProformaNumber,
            JobOrderNumber = x.JobOrderNumber
        };
    }

    /// <summary>Attachment chip in the details drawer.</summary>
    public sealed class InquiryAttachmentItem
    {
        public long Id { get; private set; }
        public string FileName { get; private set; } = "";
        public string Extension { get; private set; } = "";
        public string TypeLabel { get; private set; } = "";
        public long FileSize { get; private set; }
        public string FileSizeDisplay => FileSize < 1024 * 1024
            ? $"{FileSize / 1024d:N0} KB" : $"{FileSize / (1024d * 1024d):N1} MB";
        public string UploadedBy { get; private set; } = "";
        public DateTime UploadedAt { get; private set; }
        public string UploadedText => UploadedAt.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        public InquiryAttachment Attachment { get; private set; } = new();

        public static InquiryAttachmentItem FromModel(InquiryAttachment x) => new()
        {
            Id = x.Id, FileName = x.FileName, Extension = x.Extension,
            TypeLabel = (x.Extension ?? "").TrimStart('.').ToUpperInvariant(),
            FileSize = x.FileSize, UploadedBy = x.UploadedBy, UploadedAt = x.UploadedAt,
            Attachment = x
        };
    }

    /// <summary>Timeline entry for the drawer activity feed.</summary>
    public sealed class InquiryActivityItem
    {
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string Actor { get; init; } = "";
        public DateTime OccurredAt { get; init; }
        public string OccurredText => OccurredAt.ToString("dd MMM yyyy · hh:mm tt", CultureInfo.CurrentCulture);
    }
}

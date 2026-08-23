using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Inquiry;
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
        private CancellationTokenSource? _loadCts;
        public ObservableCollection<InquiryListRow> Inquiries { get; } = new();
        public ObservableCollection<string> DateRanges { get; } = new() { "Today", "Yesterday", "Last 7 Days", "Last 30 Days", "Custom" };
        public ObservableCollection<string> Statuses { get; } = new() { "All statuses", "Received", "In progress", "Sent", "Hold", "Confirmed", "Converted", "Cancelled" };
        public ObservableCollection<string> Salesmen { get; } = new() { "All salesmen" };
        public ObservableCollection<string> Priorities { get; } = new() { "All priorities", "Urgent", "High", "Normal", "Low" };

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
            }
        }

        private bool _isDetailsDrawerOpen;
        public bool IsDetailsDrawerOpen { get => _isDetailsDrawerOpen; private set { _isDetailsDrawerOpen = value; OnPropertyChanged(); } }
        public bool HasSelection => SelectedInquiry != null;
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

        public string SearchText { get; set; } = "";
        public string SelectedDateRange { get; set; } = "Today";
        public string SelectedStatus { get; set; } = "All statuses";
        public string SelectedSalesman { get; set; } = "All salesmen";
        public string SelectedPriority { get; set; } = "All priorities";
        public bool HasAttachmentsOnly { get; set; }

        public ICommand NewInquiryCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand OpenWorkspaceCommand { get; }
        public ICommand ConvertToPiCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand BulkChangeOwnerCommand { get; }
        public ICommand BulkChangeStatusCommand { get; }
        public ICommand ExportSelectedCommand { get; }

        public InquiryListViewModel() : this(App.Inquiries ?? new InquiryRepository()) { }

        public InquiryListViewModel(IInquiryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            NewInquiryCommand = new RelayCommand(_ => SetStatus("Inquiry editor is the next implementation phase."));
            ExportCommand = new RelayCommand(_ => SetStatus("Export is the next implementation phase."));
            ResetFiltersCommand = new RelayCommand(_ => { ResetFilters(); Reload(); });
            RefreshCommand = new RelayCommand(_ => Reload());
            CloseDetailsCommand = new RelayCommand(_ => IsDetailsDrawerOpen = false);
            OpenWorkspaceCommand = new RelayCommand(_ => SetStatus("Select an Inquiry to open its workspace."));
            ConvertToPiCommand = new RelayCommand(_ => SetStatus("Inquiry conversion is not implemented yet."));
            ShowHistoryCommand = new RelayCommand(_ => SetStatus("Inquiry history is not implemented yet."));
            BulkChangeOwnerCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            BulkChangeStatusCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            ExportSelectedCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
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
            var query = new InquiryQuery
            {
                Search = SearchText, Status = SelectedStatus,
                HasAttachmentsOnly = HasAttachmentsOnly, Page = 1, PageSize = 50
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
                    Priority = x.Priority, AttachmentCount = x.AttachmentCount,
                    Status = x.Status, ProformaId = x.ProformaNumber,
                    DownstreamStatus = string.IsNullOrWhiteSpace(x.JobOrderNumber)
                        ? "Not released" : x.JobOrderNumber,
                    UpdatedBy = x.UpdatedBy
                });
            }
            TotalCount = stats.All; ReceivedCount = stats.Received;
            SentCount = stats.Sent; ConfirmedCount = stats.Confirmed;
            HoldCount = stats.Hold; CancelledCount = stats.Cancelled;
            PageStatus = page.TotalCount == 0 ? "No inquiries found"
                : $"Showing {(page.Page - 1) * page.PageSize + 1}–{Math.Min(page.TotalCount, page.Page * page.PageSize)} of {page.TotalCount}";
            SetStatus(PageStatus);
        }

        private string _statusText = "Inquiry list ready.";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        private void SetStatus(string value) => StatusText = value;
        private void ResetFilters()
        {
            SearchText = ""; SelectedDateRange = "Today"; SelectedStatus = "All statuses";
            SelectedSalesman = "All salesmen"; SelectedPriority = "All priorities";
            HasAttachmentsOnly = false;
            OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(SelectedDateRange));
            OnPropertyChanged(nameof(SelectedStatus)); OnPropertyChanged(nameof(SelectedSalesman));
            OnPropertyChanged(nameof(SelectedPriority)); OnPropertyChanged(nameof(HasAttachmentsOnly));
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
        public int AttachmentCount { get; set; }
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
        public string FileName { get; set; } = "";
        public string ProcessingStatus { get; set; } = "Not processed";
    }
}

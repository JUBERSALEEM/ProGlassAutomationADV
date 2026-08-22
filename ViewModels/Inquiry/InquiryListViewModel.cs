using ProGlassAutomation.Commands;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Inquiry
{
    /// <summary>
    /// Shell ViewModel for the approved Inquiry list. Persistence is added
    /// separately; an empty collection is truthful and never demo/fake data.
    /// </summary>
    public sealed class InquiryListViewModel : ViewModelBase
    {
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
        public string PageStatus => Inquiries.Count == 0 ? "No inquiries yet" : $"Showing 1–{Inquiries.Count} of {Inquiries.Count}";
        public int TotalCount => Inquiries.Count;
        public int OpenCount => 0;
        public int ConfirmedCount => 0;
        public int ConvertedCount => 0;
        public int CancelledCount => 0;

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

        public InquiryListViewModel()
        {
            NewInquiryCommand = new RelayCommand(_ => SetStatus("Inquiry details workflow is not implemented yet."));
            ExportCommand = new RelayCommand(_ => SetStatus("Nothing to export."));
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
            RefreshCommand = new RelayCommand(_ => SetStatus("Inquiry list refreshed."));
            CloseDetailsCommand = new RelayCommand(_ => IsDetailsDrawerOpen = false);
            OpenWorkspaceCommand = new RelayCommand(_ => SetStatus("Select an Inquiry to open its workspace."));
            ConvertToPiCommand = new RelayCommand(_ => SetStatus("Inquiry conversion is not implemented yet."));
            ShowHistoryCommand = new RelayCommand(_ => SetStatus("Inquiry history is not implemented yet."));
            BulkChangeOwnerCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            BulkChangeStatusCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
            ExportSelectedCommand = new RelayCommand(_ => SetStatus("No Inquiry records selected."));
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
        public bool IsSelected { get; set; }
        public string InquiryNumber { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
        public string CustomerName { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Project { get; set; } = "";
        public string Salesman { get; set; } = "";
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

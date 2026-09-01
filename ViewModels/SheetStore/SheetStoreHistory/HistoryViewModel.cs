using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly ISheetHistoryRepository _repo = new SheetHistoryRepository();
        private bool _isLoading;

        public ObservableCollection<SheetHistory> Entries { get; } = new();

        // ============ FILTERS ============

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        private string _filterAction = "All";
        public string FilterAction
        {
            get => _filterAction;
            set
            {
                if (_filterAction == value) return;
                _filterAction = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (_fromDate == value) return;
                _fromDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDateFilter));
                _ = LoadAsync();
            }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (_toDate == value) return;
                _toDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDateFilter));
                _ = LoadAsync();
            }
        }

        public bool HasDateFilter => FromDate.HasValue || ToDate.HasValue;

        // ============ STATS ============

        public int TotalEntries => Entries.Count;
        public bool HasEntries => Entries.Count > 0;
        public bool IsEmpty => Entries.Count == 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // ============ COMMANDS ============

        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ClearDatesCommand { get; }

        public Action? CloseAction { get; set; }

        public HistoryViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            CloseCommand = new RelayCommand(_ => CloseAction?.Invoke());
            ClearDatesCommand = new RelayCommand(_ =>
            {
                _fromDate = null;
                _toDate = null;
                OnPropertyChanged(nameof(FromDate));
                OnPropertyChanged(nameof(ToDate));
                OnPropertyChanged(nameof(HasDateFilter));
                _ = LoadAsync();
            });

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                IsBusy = true;

                var all = await _repo.GetAllAsync();

                // Filter by action
                if (FilterAction != "All" && !string.IsNullOrEmpty(FilterAction))
                    all = all.Where(h => h.Action == FilterAction).ToList();

                // Filter by date range
                if (FromDate.HasValue)
                    all = all.Where(h => h.Timestamp.Date >= FromDate.Value.Date).ToList();

                if (ToDate.HasValue)
                    all = all.Where(h => h.Timestamp.Date <= ToDate.Value.Date).ToList();

                // Filter by search
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var q = SearchText.Trim().ToLower();
                    all = all.Where(h =>
                        (h.SheetDescription ?? "").ToLower().Contains(q) ||
                        (h.Details ?? "").ToLower().Contains(q) ||
                        (h.UserName ?? "").ToLower().Contains(q)
                    ).ToList();
                }

                Entries.Clear();
                foreach (var e in all)
                    Entries.Add(e);

                OnPropertyChanged(nameof(TotalEntries));
                OnPropertyChanged(nameof(HasEntries));
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                App.Dialogs.Show(
                    "Failed to load history:\n\n" + ex.Message,
                    LocalizationService.Instance.T("Msg.CapError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                IsBusy = false;
            }
        }
    }
}
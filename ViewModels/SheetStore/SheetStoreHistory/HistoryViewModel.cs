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

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly ISheetHistoryRepository _repo = new SheetHistoryRepository();

        // Reentrancy guard — prevents multiple concurrent loads
        private bool _isLoading;

        public ObservableCollection<SheetHistory> Entries { get; } = new();

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

        public int TotalEntries => Entries.Count;
        public bool HasEntries => Entries.Count > 0;
        public bool IsEmpty => Entries.Count == 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }

        public Action? CloseAction { get; set; }

        public HistoryViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            CloseCommand = new RelayCommand(_ => CloseAction?.Invoke());

            // Initial load
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            // Prevent concurrent loads (fixes DB lock + crash on refresh)
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                IsBusy = true;

                var all = await _repo.GetAllAsync();

                // Filter by action
                if (FilterAction != "All" && !string.IsNullOrEmpty(FilterAction))
                    all = all.Where(h => h.Action == FilterAction).ToList();

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

                // Update collection on UI thread
                Entries.Clear();
                foreach (var e in all)
                    Entries.Add(e);

                OnPropertyChanged(nameof(TotalEntries));
                OnPropertyChanged(nameof(HasEntries));
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load history:\n\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                IsBusy = false;
            }
        }
    }
}
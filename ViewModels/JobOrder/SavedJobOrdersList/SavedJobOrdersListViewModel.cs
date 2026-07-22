using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
    /// <summary>
    /// ViewModel for the Job Order list landing page.
    /// Loads all saved job orders from App.Jobs and provides search,
    /// filter, sort, and delete operations.
    ///
    /// Callback OnJobOrderOpenRequested is fired when user clicks OPEN
    /// on a row — the shell VM (JobOrderViewModel) handles the actual
    /// view switch.
    /// </summary>
    public class SavedJobOrdersListViewModel : ViewModelBase
    {
        // ── reentrancy guard ──────────────────────────────────────────────
        private int _loadingFlag = 0;

        // ── callback wired by shell VM ────────────────────────────────────
        /// <summary>
        /// Called when user clicks OPEN on a job order row.
        /// Parameter = SavedJobOrder.Id (int).
        /// Wired by JobOrderViewModel constructor.
        /// </summary>
        public Action<int>? OnJobOrderOpenRequested { get; set; }

        // ── data: full list + filtered display list ───────────────────────

        private List<JobOrderRowViewModel> _allJobs = new();

        private ObservableCollection<JobOrderRowViewModel> _displayedJobs = new();
        public ObservableCollection<JobOrderRowViewModel> DisplayedJobs
        {
            get => _displayedJobs;
            private set
            {
                if (_displayedJobs == value) return;
                _displayedJobs = value;
                OnPropertyChanged();
            }
        }

        // ── KPI aggregates ────────────────────────────────────────────────

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); }
        }

        private int _inProductionCount;
        public int InProductionCount
        {
            get => _inProductionCount;
            private set { if (_inProductionCount == value) return; _inProductionCount = value; OnPropertyChanged(); }
        }

        private int _completedCount;
        public int CompletedCount
        {
            get => _completedCount;
            private set { if (_completedCount == value) return; _completedCount = value; OnPropertyChanged(); }
        }

        private int _overdueCount;
        public int OverdueCount
        {
            get => _overdueCount;
            private set { if (_overdueCount == value) return; _overdueCount = value; OnPropertyChanged(); }
        }

        // ── loading + error state ─────────────────────────────────────────

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

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            private set { if (_errorMessage == value) return; _errorMessage = value; OnPropertyChanged(); }
        }

        // ── filter + search + sort ────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Draft", "Released", "In Production", "Completed", "Delivered"
        };

        private string _selectedStatusFilter = "All Statuses";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> PriorityFilterOptions { get; } = new()
        {
            "All Priorities", "High", "Medium", "Low"
        };

        private string _selectedPriorityFilter = "All Priorities";
        public string SelectedPriorityFilter
        {
            get => _selectedPriorityFilter;
            set
            {
                if (_selectedPriorityFilter == value) return;
                _selectedPriorityFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> SortOptions { get; } = new()
        {
            "Newest first",
            "Oldest first",
            "Delivery date (soonest)",
            "Delivery date (latest)",
            "Priority (high first)",
            "Priority (low first)",
            "Grand total (highest)",
            "Grand total (lowest)"
        };

        private string _selectedSort = "Newest first";
        public string SelectedSort
        {
            get => _selectedSort;
            set
            {
                if (_selectedSort == value) return;
                _selectedSort = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // ── commands ──────────────────────────────────────────────────────

        public ICommand OpenJobOrderCommand { get; }
        public ICommand DeleteJobOrderCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand ChangePriorityCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public SavedJobOrdersListViewModel()
        {
            OpenJobOrderCommand = new RelayCommand(param =>
            {
                if (param is JobOrderRowViewModel row)
                    OnJobOrderOpenRequested?.Invoke(row.Id);
            });

            DeleteJobOrderCommand = new RelayCommand(async param =>
            {
                if (param is JobOrderRowViewModel row)
                    await DeleteJobOrderAsync(row);
            });

            ChangeStatusCommand = new RelayCommand(async param =>
            {
                // param = Tuple<JobOrderRowViewModel, string newStatus>
                if (param is (JobOrderRowViewModel row, string newStatus))
                    await ChangeStatusAsync(row, newStatus);
            });

            ChangePriorityCommand = new RelayCommand(async param =>
            {
                if (param is (JobOrderRowViewModel row, string newPriority))
                    await ChangePriorityAsync(row, newPriority);
            });

            RefreshCommand = new RelayCommand(async _ => await LoadJobOrdersAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            // Load on construction
            _ = LoadJobOrdersAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        public async Task LoadJobOrdersAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                if (App.Jobs == null)
                {
                    HasError = true;
                    ErrorMessage = "Job Order database not initialised.";
                    return;
                }

                var entities = await App.Jobs.GetAllAsync();

                _allJobs = entities
                    .Select(e => new JobOrderRowViewModel(e))
                    .ToList();

                RecalculateKpis();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load job orders: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  KPI + FILTER
        // ═════════════════════════════════════════════════════════════════

        private void RecalculateKpis()
        {
            TotalCount = _allJobs.Count;
            InProductionCount = _allJobs.Count(j => Eq(j.StatusRaw, "InProduction"));
            CompletedCount = _allJobs.Count(j => Eq(j.StatusRaw, "Completed"));

            var today = DateTime.Today;
            OverdueCount = _allJobs.Count(j =>
                j.DeliveryDate.HasValue &&
                j.DeliveryDate.Value.Date < today &&
                !Eq(j.StatusRaw, "Completed") &&
                !Eq(j.StatusRaw, "Delivered"));
        }

        private void ApplyFilters()
        {
            IEnumerable<JobOrderRowViewModel> query = _allJobs;

            // Search
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText.Trim().ToLowerInvariant();
                query = query.Where(j =>
                    (j.JobOrderId?.ToLowerInvariant().Contains(q) ?? false) ||
                    (j.CustomerName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (j.ProjectName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (j.SourceProformaId?.ToLowerInvariant().Contains(q) ?? false));
            }

            // Status filter
            if (_selectedStatusFilter != "All Statuses")
            {
                string statusKey = _selectedStatusFilter.Replace(" ", "");
                query = query.Where(j => Eq(j.StatusRaw, statusKey));
            }

            // Priority filter
            if (_selectedPriorityFilter != "All Priorities")
            {
                query = query.Where(j => Eq(j.PriorityRaw, _selectedPriorityFilter));
            }

            // Sort
            query = _selectedSort switch
            {
                "Newest first" => query.OrderByDescending(j => j.UpdatedAt),
                "Oldest first" => query.OrderBy(j => j.UpdatedAt),
                "Delivery date (soonest)" => query.OrderBy(j => j.DeliveryDate ?? DateTime.MaxValue),
                "Delivery date (latest)" => query.OrderByDescending(j => j.DeliveryDate ?? DateTime.MinValue),
                "Priority (high first)" => query.OrderByDescending(j => PriorityRank(j.PriorityRaw)),
                "Priority (low first)" => query.OrderBy(j => PriorityRank(j.PriorityRaw)),
                "Grand total (highest)" => query.OrderByDescending(j => j.GrandTotal),
                "Grand total (lowest)" => query.OrderBy(j => j.GrandTotal),
                _ => query.OrderByDescending(j => j.UpdatedAt)
            };

            DisplayedJobs = new ObservableCollection<JobOrderRowViewModel>(query);
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            _selectedStatusFilter = "All Statuses";
            _selectedPriorityFilter = "All Priorities";
            _selectedSort = "Newest first";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(SelectedPriorityFilter));
            OnPropertyChanged(nameof(SelectedSort));

            ApplyFilters();
        }

        // ═════════════════════════════════════════════════════════════════
        //  MUTATIONS
        // ═════════════════════════════════════════════════════════════════

        private async Task DeleteJobOrderAsync(JobOrderRowViewModel row)
        {
            var result = MessageBox.Show(
                $"Delete job order '{row.JobOrderId}'?\n\n" +
                $"Customer: {row.CustomerName}\n" +
                $"This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await App.Jobs.DeleteAsync(row.Id);
                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete job order:\n\n{ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(JobOrderRowViewModel row, string newStatus)
        {
            try
            {
                var entity = await App.Jobs.GetByIdAsync(row.Id);
                if (entity == null) return;

                entity.Status = newStatus;
                entity.UpdatedAt = DateTime.Now;
                await App.Jobs.UpdateAsync(entity);

                await LoadJobOrdersAsync();
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

        private async Task ChangePriorityAsync(JobOrderRowViewModel row, string newPriority)
        {
            try
            {
                var entity = await App.Jobs.GetByIdAsync(row.Id);
                if (entity == null) return;

                entity.Priority = newPriority;
                entity.UpdatedAt = DateTime.Now;
                await App.Jobs.UpdateAsync(entity);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change priority:\n\n{ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int PriorityRank(string? priority) =>
            priority?.ToLowerInvariant() switch
            {
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 0
            };
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ROW VIEWMODEL — Display wrapper for SavedJobOrder entity
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Display-friendly row for the job order list.
    /// Wraps SavedJobOrder entity with pre-formatted display strings
    /// and resolved brush keys for XAML binding.
    /// </summary>
    public class JobOrderRowViewModel
    {
        public int Id { get; }
        public string JobOrderId { get; }
        public string SourceProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public decimal GrandTotal { get; }
        public string StatusRaw { get; }
        public string PriorityRaw { get; }
        public DateTime? DeliveryDate { get; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; }

        // ── computed display fields ───────────────────────────────────────

        public string GrandTotalDisplay { get; }
        public string DeliveryDisplay { get; }
        public string CreatedDisplay { get; }

        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string StatusTintKey { get; }

        public string PriorityLabel { get; }
        public string PriorityBrushKey { get; }
        public string PriorityPillStyleKey { get; }

        public bool IsOverdue { get; }
        public string OverdueDisplay { get; }

        public JobOrderRowViewModel(SavedJobOrder entity)
        {
            Id = entity.Id;
            JobOrderId = entity.JobOrderId ?? string.Empty;
            SourceProformaId = entity.SourceProformaId ?? string.Empty;
            CustomerName = string.IsNullOrWhiteSpace(entity.CustomerName)
                                   ? "—" : entity.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(entity.ProjectName)
                                   ? "—" : entity.ProjectName;
            GrandTotal = entity.GrandTotalVatInclusive;
            StatusRaw = entity.Status ?? "Draft";
            PriorityRaw = entity.Priority ?? "Medium";
            DeliveryDate = entity.DeliveryDate;
            CreatedAt = entity.CreatedAt;
            UpdatedAt = entity.UpdatedAt;

            GrandTotalDisplay = $"AED {GrandTotal:N0}";
            CreatedDisplay = CreatedAt.ToString("dd MMM yyyy");
            DeliveryDisplay = DeliveryDate.HasValue
                                    ? DeliveryDate.Value.ToString("dd MMM yyyy")
                                    : "No date set";

            // Status meta
            if (Enum.TryParse<JobOrderStatus>(StatusRaw, ignoreCase: true, out var statusEnum))
            {
                StatusLabel = JobOrderStatusMeta.GetLabel(statusEnum);
                StatusBrushKey = JobOrderStatusMeta.GetBrushKey(statusEnum);
                StatusTintKey = JobOrderStatusMeta.GetTintKey(statusEnum);
            }
            else
            {
                StatusLabel = StatusRaw;
                StatusBrushKey = "NeutralBrush";
                StatusTintKey = "TintGrayBrush";
            }

            // Priority meta
            if (Enum.TryParse<JobPriority>(PriorityRaw, ignoreCase: true, out var priorityEnum))
            {
                PriorityLabel = JobPriorityMeta.GetLabel(priorityEnum);
                PriorityBrushKey = JobPriorityMeta.GetBrushKey(priorityEnum);
                PriorityPillStyleKey = JobPriorityMeta.GetPillStyleKey(priorityEnum);
            }
            else
            {
                PriorityLabel = PriorityRaw;
                PriorityBrushKey = "NeutralBrush";
                PriorityPillStyleKey = "PriorityPillLowStyle";
            }

            // Overdue check
            var today = DateTime.Today;
            IsOverdue = DeliveryDate.HasValue
                        && DeliveryDate.Value.Date < today
                        && !string.Equals(StatusRaw, "Completed", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(StatusRaw, "Delivered", StringComparison.OrdinalIgnoreCase);

            if (IsOverdue)
            {
                int daysLate = (today - DeliveryDate!.Value.Date).Days;
                OverdueDisplay = daysLate == 1
                                     ? "Overdue by 1 day"
                                     : $"Overdue by {daysLate} days";
            }
            else
            {
                OverdueDisplay = string.Empty;
            }
        }
    }
}
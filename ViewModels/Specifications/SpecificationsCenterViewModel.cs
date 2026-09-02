// ═══════════════════════════════════════════════════════════════════════
//  FILE: ViewModels/Specifications/SpecificationsCenterViewModel.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — Specifications Center (slice 3b)
//
//  WHAT THIS IS
//  ---------------------------------------------------------------------
//  A read-oriented LEDGER of every saved glass specification, laid out
//  like Sheet Store: KPI cards on top, filters, a list, and a detail
//  panel that opens when a row is clicked.
//
//  WHAT THIS IS NOT
//  It does not build or price specifications — Step 4 remains the only
//  place that happens. There is no "Add Specification" dialog, because a
//  spec is a DERIVED artefact: it only means something alongside the
//  pricing inputs that produced it. Offering a hand-entry form here would
//  let someone create a spec that Step 4 could never reproduce.
//
//  The Sheet Store relationship is READ-ONLY and exists purely to answer
//  "which sheet size was this quoted against". Nothing here writes to
//  Sheet Store, and nothing here re-prices a spec.
//
//  The only mutation offered is DELETE, for pruning mistakes out of the
//  catalogue, and it is permission-gated in the repository.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Specifications
{
    public class SpecificationsCenterViewModel : ViewModelBase
    {
        private readonly IDialogService? _dialogs;
        private readonly ISpecificationsRepository? _specs;

        // Full unfiltered set; Specifications is the filtered view of it.
        private List<SavedSpecification> _all = new();

        public SpecificationsCenterViewModel(
            IDialogService? dialogs,
            ISpecificationsRepository? specs)
        {
            _dialogs = dialogs;
            _specs = specs;

            InitializeCore();
        }

        // Parameterless ctor chains, matching the ERR-633EEB v2 pattern:
        // the body lives in InitializeCore() and is never duplicated.
        public SpecificationsCenterViewModel()
            : this(App.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService,
                   App.Specs)
        {
        }

        private void InitializeCore()
        {
            RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            SelectSpecCommand = new RelayCommand(p => _ = SelectSpecAsync(p as SavedSpecification));
            CloseDetailCommand = new RelayCommand(_ => SelectedSpec = null);
            DeleteSpecCommand = new RelayCommand(p => _ = DeleteSpecAsync(p as SavedSpecification));

            // Live catalogue sync: Step 4 saving a spec updates this list
            // with no manual refresh, same channel Step 5's dropdown uses.
            SpecCatalogNotifier.Changed += OnCatalogChanged;

            _ = LoadAsync();
        }

        private void OnCatalogChanged(object? sender, EventArgs e)
        {
            // May arrive on a background thread (async repository writes).
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                _ = LoadAsync();
            else
                dispatcher.BeginInvoke(new Action(() => _ = LoadAsync()));
        }

        // ── Collections ──────────────────────────────────────────────────

        public ObservableCollection<SavedSpecification> Specifications { get; } = new();
        public ObservableCollection<SpecificationUsage> UsageHistory { get; } = new();

        public ObservableCollection<string> GlassTypeOptions { get; } = new();
        public ObservableCollection<string> ColorOptions { get; } = new();

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Most used",
            "Recently used",
            "Newest first",
            "Name (A–Z)",
            "Price (high → low)"
        };

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ClearFiltersCommand { get; private set; } = null!;
        public ICommand SelectSpecCommand { get; private set; } = null!;
        public ICommand CloseDetailCommand { get; private set; } = null!;
        public ICommand DeleteSpecCommand { get; private set; } = null!;

        // ── Filters ──────────────────────────────────────────────────────

        private const string AllFilter = "All";

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value ?? ""; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _glassTypeFilter = AllFilter;
        public string GlassTypeFilter
        {
            get => _glassTypeFilter;
            set { if (_glassTypeFilter == value) return; _glassTypeFilter = value ?? AllFilter; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _colorFilter = AllFilter;
        public string ColorFilter
        {
            get => _colorFilter;
            set { if (_colorFilter == value) return; _colorFilter = value ?? AllFilter; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _sortBy = "Most used";
        public string SortBy
        {
            get => _sortBy;
            set { if (_sortBy == value) return; _sortBy = value ?? "Most used"; OnPropertyChanged(); ApplyFilters(); }
        }

        // ── Selection / detail panel ─────────────────────────────────────

        private SavedSpecification? _selectedSpec;
        public SavedSpecification? SelectedSpec
        {
            get => _selectedSpec;
            set
            {
                _selectedSpec = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailOpen));
                OnPropertyChanged(nameof(DetailColumnWidth));
            }
        }

        public bool IsDetailOpen => _selectedSpec != null;

        /// <summary>Slide-in width for the detail pane; 0 collapses it.</summary>
        public GridLength DetailColumnWidth =>
            _selectedSpec != null ? new GridLength(420) : new GridLength(0);

        // ── Status ───────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);

        public bool IsEmpty => Specifications.Count == 0 && !IsLoading;

        // ── KPI cards ────────────────────────────────────────────────────
        // Computed from the FILTERED list so the numbers always describe
        // what is actually on screen — a KPI that disagrees with the list
        // below it is worse than no KPI.

        public int TotalSpecs => Specifications.Count;

        public int TotalUses => Specifications.Sum(s => s.UsageCount);

        public decimal AveragePricePerSqm =>
            Specifications.Count == 0
                ? 0m
                : Math.Round(Specifications.Average(s => s.PricePerSqm), 2);

        public int LinkedSheetCount => Specifications.Count(s => s.HasSheetMatch);

        public string MostUsedSpecName =>
            Specifications.Count == 0
                ? "—"
                : Specifications.OrderByDescending(s => s.UsageCount).First().SpecName;

        private void RaiseKpis()
        {
            OnPropertyChanged(nameof(TotalSpecs));
            OnPropertyChanged(nameof(TotalUses));
            OnPropertyChanged(nameof(AveragePricePerSqm));
            OnPropertyChanged(nameof(LinkedSheetCount));
            OnPropertyChanged(nameof(MostUsedSpecName));
            OnPropertyChanged(nameof(IsEmpty));
        }

        // ── Load / filter ────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            if (_specs == null)
            {
                StatusMessage = "Specifications repository unavailable.";
                return;
            }

            try
            {
                IsLoading = true;

                var rows = await _specs.GetAllAsync();
                _all = rows ?? new List<SavedSpecification>();

                RebuildFilterOptions();
                ApplyFilters();

                StatusMessage = "";
            }
            catch (Exception ex)
            {
                // Never throws into the shell: an unreadable catalogue shows
                // an empty, explained list rather than taking the app down.
                AppLogService.Warning(
                    $"Specifications Center load failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "Load" });
                StatusMessage = "Could not load specifications.";
                _all = new List<SavedSpecification>();
                ApplyFilters();
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private void RebuildFilterOptions()
        {
            string keptType = _glassTypeFilter;
            string keptColor = _colorFilter;

            GlassTypeOptions.Clear();
            GlassTypeOptions.Add(AllFilter);
            foreach (var t in _all.Select(s => s.GlassType)
                                  .Where(t => !string.IsNullOrWhiteSpace(t))
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .OrderBy(t => t))
                GlassTypeOptions.Add(t);

            ColorOptions.Clear();
            ColorOptions.Add(AllFilter);
            foreach (var c in _all.Select(s => s.Color)
                                  .Where(c => !string.IsNullOrWhiteSpace(c))
                                  .Select(c => c!.Trim())
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .OrderBy(c => c))
                ColorOptions.Add(c);

            // Preserve the active filter if it still exists, so a background
            // refresh does not silently reset what the user is looking at.
            _glassTypeFilter = GlassTypeOptions.Contains(keptType) ? keptType : AllFilter;
            _colorFilter = ColorOptions.Contains(keptColor) ? keptColor : AllFilter;
            OnPropertyChanged(nameof(GlassTypeFilter));
            OnPropertyChanged(nameof(ColorFilter));
        }

        private void ApplyFilters()
        {
            IEnumerable<SavedSpecification> q = _all;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string term = _searchText.Trim();
                q = q.Where(s =>
                    (s.SpecName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (s.Color ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (s.ProformaId ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(_glassTypeFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
                q = q.Where(s => string.Equals(s.GlassType, _glassTypeFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.Equals(_colorFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
                q = q.Where(s => string.Equals((s.Color ?? "").Trim(), _colorFilter, StringComparison.OrdinalIgnoreCase));

            q = _sortBy switch
            {
                "Recently used"      => q.OrderByDescending(s => s.LastUsedAt),
                "Newest first"       => q.OrderByDescending(s => s.CreatedAt),
                "Name (A–Z)"         => q.OrderBy(s => s.SpecName),
                "Price (high → low)" => q.OrderByDescending(s => s.PricePerSqm),
                _                    => q.OrderByDescending(s => s.UsageCount)
                                          .ThenByDescending(s => s.LastUsedAt)
            };

            Specifications.Clear();
            foreach (var s in q) Specifications.Add(s);

            RaiseKpis();
        }

        private void ClearFilters()
        {
            _searchText = "";
            _glassTypeFilter = AllFilter;
            _colorFilter = AllFilter;
            _sortBy = "Most used";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(GlassTypeFilter));
            OnPropertyChanged(nameof(ColorFilter));
            OnPropertyChanged(nameof(SortBy));

            ApplyFilters();
        }

        // ── Row click → detail + usage history ───────────────────────────

        private async Task SelectSpecAsync(SavedSpecification? spec)
        {
            SelectedSpec = spec;
            UsageHistory.Clear();

            if (spec == null || _specs == null) return;

            try
            {
                var history = await _specs.GetUsageHistoryAsync(spec.Id);
                foreach (var u in history) UsageHistory.Add(u);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecificationsCenter.History] {ex.Message}");
            }
            finally
            {
                OnPropertyChanged(nameof(UsageCountDisplay));
            }
        }

        public string UsageCountDisplay =>
            UsageHistory.Count == 1 ? "1 use" : $"{UsageHistory.Count} uses";

        // ── Delete ───────────────────────────────────────────────────────

        private async Task DeleteSpecAsync(SavedSpecification? spec)
        {
            if (spec == null || _specs == null) return;

            bool confirmed = _dialogs?.Confirm(
                $"Delete the specification \"{spec.SpecName}\"?\n\n"
                + "Existing invoices keep the price they were quoted at. "
                + "This only removes it from the catalogue and the Step 5 "
                + "dropdown.",
                "Delete specification") ?? false;

            if (!confirmed) return;

            try
            {
                await _specs.DeleteAsync(spec.Id);
                SelectedSpec = null;
                StatusMessage = $"Deleted \"{spec.SpecName}\".";
                // Repository raises SpecCatalogNotifier.Changed, which
                // reloads this list — no manual refresh needed.
            }
            catch (Services.Audit.PermissionDeniedException denied)
            {
                _dialogs?.Warning(denied.UserMessage, "Permission denied");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specification delete failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "Delete" });
                _dialogs?.Error("Could not delete that specification.", "Specifications");
            }
        }
    }
}

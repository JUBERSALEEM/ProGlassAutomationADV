// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementList/StatementListViewModel.cs
// PROJECT: ProGlassAutomation
// CHAT 17 — ALL BUGS FIXED
//
// FIXES APPLIED:
//   L1 — Added IsListVisible property
//   L2 — Added OnPropertyChanged(nameof(HasSearchText)) in SearchText setter
//   L3 — IsListVisible notified in IsLoading setter, ApplySearch, LoadAsync
//   L4 — EditStatementCommand canExecute checks CommandParameter first
//   L6 — ApplySearch fires OnPropertyChanged(nameof(IsListVisible))
//   L9 — StatusMessage set on Dispatcher after load
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProGlassAutomation.Utilities;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
namespace ProGlassAutomation.ViewModels.Reports.StatementList
{
    public class StatementListViewModel : ViewModelBase
    {
        private readonly IStatementRepository _repo;

        // ── Collections ───────────────────────────────────────────────────
        public BulkObservableCollection<StatementListRowViewModel> Rows { get; }
            = new();

        // ── Search ────────────────────────────────────────────────────────
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();

                // FIX L2: notify HasSearchText so clear button
                // visibility updates immediately on every keystroke
                OnPropertyChanged(nameof(HasSearchText));

                ApplySearch();
            }
        }

        // FIX L2: HasSearchText is a computed bool backed by _searchText.
        // Now correctly notified via SearchText setter.
        public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

        // ── State ─────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();

                // FIX L3: notify all computed state properties
                // whenever loading state changes
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }

        /// <summary>
        /// Prevents empty state flashing before first DB query runs.
        /// Set to true once LoadAsync completes (success or error).
        /// </summary>
        private bool _isLoaded;

        /// <summary>
        /// True only when: load has completed AND not loading
        /// AND zero rows in current filtered view.
        /// Never true during a loading operation.
        /// </summary>
        public bool IsEmpty => _isLoaded && !_isLoading && Rows.Count == 0;

        /// <summary>
        /// FIX L1: New property required by redesigned StatementListView.xaml.
        /// True when: load has completed AND not loading AND rows exist.
        /// Drives visibility of both the column header row and the ListView.
        /// Replaces the old InverseBoolToVisibilityConverter on IsEmpty
        /// which could not guard against the IsLoading race condition.
        /// </summary>
        public bool IsListVisible => _isLoaded && !_isLoading && Rows.Count > 0;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private StatementListRowViewModel? _selectedRow;
        public StatementListRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                _selectedRow = value;
                OnPropertyChanged();
            }
        }

        // ── KPI summary ───────────────────────────────────────────────────
        private int _totalStatements;
        private decimal _totalInvoiced;
        private decimal _totalOutstanding;
        private decimal _totalOverdue;

        public int TotalStatements => _totalStatements;
        public string TotalInvoicedDisplay
            => $"AED {_totalInvoiced:N2}";
        public string TotalOutstandingDisplay
            => $"AED {_totalOutstanding:N2}";
        public string TotalOverdueDisplay
            => _totalOverdue > 0 ? $"AED {_totalOverdue:N2}" : "None";
        public bool HasOverdue => _totalOverdue > 0;

        // ── Callbacks (set by SalesReportViewModel) ───────────────────────
        public Action? OnRequestNewStatement { get; set; }
        public Action<StatementListItem>? OnRequestEditStatement { get; set; }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand NewStatementCommand { get; }
        public ICommand EditStatementCommand { get; }
        public ICommand OpenPdfCommand { get; }
        public ICommand DeleteStatementCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        // ── All rows cache (for search filtering) ─────────────────────────
        private List<StatementListRowViewModel> _allRows = new();

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════
                private readonly IDialogService _dialogs = null!;
        private readonly IDatabaseServices _database = null!;

public StatementListViewModel(IDialogService? dialogs,
            IStatementRepository repo,
            IDatabaseServices? database)
        {
            _dialogs = dialogs!;
            _repo = repo;
            _database = database!;

            // LIVE DATA: refresh whenever any document changes (statement
            // created/deleted, or PI/JO/DN/TI writes) — same cross-module
            // signal the module list pages subscribe to. The wizard
            // (StatementGeneratorViewModel) deliberately does NOT subscribe:
            // reloading mid-wizard would wipe the user's filter state.
            _database.SubscribeDocumentsChanged(LoadAsync);

            NewStatementCommand = new RelayCommand(
                _ => OnRequestNewStatement?.Invoke());

            // FIX L4: EditStatementCommand resolves row from CommandParameter
            // first, then falls back to SelectedRow — consistent with
            // OpenPdfCommand and DeleteStatementCommand pattern.
            EditStatementCommand = new RelayCommand(
                p =>
                {
                    var row = p as StatementListRowViewModel ?? SelectedRow;
                    if (row != null)
                        OnRequestEditStatement?.Invoke(row.Source);
                },
                p => (p as StatementListRowViewModel ?? SelectedRow) != null);

            OpenPdfCommand = new RelayCommand(
                p => OpenPdf(p as StatementListRowViewModel ?? SelectedRow),
                p => (p as StatementListRowViewModel
                      ?? SelectedRow)?.HasPdf == true);

            DeleteStatementCommand = new RelayCommand(
                async p => await DeleteAsync(
                    p as StatementListRowViewModel ?? SelectedRow),
                p => (p as StatementListRowViewModel
                      ?? SelectedRow) != null);

            RefreshCommand = new RelayCommand(
                async _ => await LoadAsync());

            ClearSearchCommand = new RelayCommand(
                _ => SearchText = "",
                _ => HasSearchText);
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════
        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading statements…";

            try
            {
                var items = await Task.Run(
                    () => _repo.GetAllAsync());

                var rows = items
                    .Select(i => new StatementListRowViewModel(i))
                    .ToList();

                // FIX L9: all UI state mutations go through Dispatcher
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _allRows = rows;
                    _isLoaded = true;

                    ApplySearch();
                    UpdateKpis(rows);

                    // Status message set inside Dispatcher — safe UI thread write
                    StatusMessage = rows.Count == 0
                        ? "No statements found. Click '+ New Statement' to begin."
                        : $"{rows.Count} statement(s) loaded.";
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isLoaded = true;

                    // FIX L3: notify state after error so empty state renders
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsListVisible));

                    StatusMessage = $"Error loading statements: {ExceptionMessages.Deep(ex)}";
                });
            }
            finally
            {
                // IsLoading setter fires IsEmpty + IsListVisible notifications
                IsLoading = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SEARCH
        // ═════════════════════════════════════════════════════════════════
        private void ApplySearch()
        {
            var filtered = string.IsNullOrWhiteSpace(_searchText)
                ? _allRows
                : _allRows.Where(r =>
                    r.StatementNumber.Contains(
                        _searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    r.Title.Contains(
                        _searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    r.FilterCustomer.Contains(
                        _searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    r.FilterProject.Contains(
                        _searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    r.FilterCompany.Contains(
                        _searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    r.FilterSalesman.Contains(
                        _searchText,
                        StringComparison.OrdinalIgnoreCase))
                  .ToList();

            Rows.ClearSilent();
            Rows.AddRangeSilent(filtered);
            Rows.RaiseReset();

            // FIX L3 + L6: notify all computed visibility properties
            // after Rows collection is updated
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsListVisible));
        }

        // ═════════════════════════════════════════════════════════════════
        //  KPI UPDATE
        // ═════════════════════════════════════════════════════════════════
        private void UpdateKpis(List<StatementListRowViewModel> rows)
        {
            _totalStatements = rows.Count;
            _totalInvoiced = rows.Sum(r => r.Source.TotalInvoiced);
            _totalOutstanding = rows.Sum(r => r.Source.TotalOutstanding);
            _totalOverdue = rows.Sum(r => r.Source.OverdueAmount);

            // Bulk notify — fires PropertyChanged for every property
            OnPropertyChanged(string.Empty);
        }

        // ═════════════════════════════════════════════════════════════════
        //  OPEN PDF
        // ═════════════════════════════════════════════════════════════════
        private static void OpenPdf(StatementListRowViewModel? row)
        {
            if (row == null || !row.HasPdf) return;
            StatementPdfExporter.OpenPdf(row.PdfPath);
        }

        // ═════════════════════════════════════════════════════════════════
        //  DELETE
        // ═════════════════════════════════════════════════════════════════
        private async Task DeleteAsync(StatementListRowViewModel? row)
        {
            // A-1 (UI layer): tell the user BEFORE the confirm
            // dialog. The repository guard is the real boundary.
            if (!PermissionService.Require(
                    AppPermissions.StatementGenerate, "delete a statement"))
                return;

            if (row == null) return;

            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DeleteStatementXThisWill"), row.StatementNumber),
                LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatementPdfExporter.DeletePdfs(row.StatementNumber);
                await Task.Run(() => _repo.DeleteAsync(row.Id));

                _allRows.Remove(row);
                ApplySearch();           // re-filters + notifies IsEmpty/IsListVisible
                UpdateKpis(_allRows);    // recalculates KPI cards

                StatusMessage = $"Statement {row.StatementNumber} deleted.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ExceptionMessages.Deep(ex)}";
            }
        }
    }
}
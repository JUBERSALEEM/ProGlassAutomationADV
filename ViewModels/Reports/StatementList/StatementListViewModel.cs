// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementList/StatementListViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIX vs PREVIOUS VERSION:
//   IsEmpty logic fixed:
//     OLD: IsEmpty = Rows.Count == 0
//          Problem: IsLoading=true AND IsEmpty=true simultaneously
//          caused both loading overlay AND empty state to show.
//     NEW: IsEmpty = !IsLoading && Rows.Count == 0 && _isLoaded
//          Empty state only shows AFTER load completes with zero results.
//   Added _isLoaded backing flag to track first load completion.
//   All other code UNCHANGED.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.StatementList
{
    public class StatementListViewModel : ViewModelBase
    {
        private readonly IStatementRepository _repo;

        // ── Collections ───────────────────────────────────────────────────
        public ObservableCollection<StatementListRowViewModel> Rows { get; }
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
                ApplySearch();
            }
        }

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
                // Recompute IsEmpty whenever loading state changes
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        /// <summary>
        /// True only after first load completes.
        /// Prevents empty state flashing before first DB query runs.
        /// </summary>
        private bool _isLoaded;

        /// <summary>
        /// True only when: load has completed + not currently loading
        /// + zero rows in current filtered view.
        /// Never true during a loading operation.
        /// </summary>
        public bool IsEmpty => _isLoaded && !_isLoading && Rows.Count == 0;

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
        public string TotalInvoicedDisplay => $"AED {_totalInvoiced:N2}";
        public string TotalOutstandingDisplay => $"AED {_totalOutstanding:N2}";
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
        public StatementListViewModel(IStatementRepository repo)
        {
            _repo = repo;

            NewStatementCommand = new RelayCommand(_ =>
                OnRequestNewStatement?.Invoke());

            EditStatementCommand = new RelayCommand(
                _ => OnRequestEditStatement?.Invoke(SelectedRow!.Source),
                _ => SelectedRow != null);

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

            // FIX: ClearSearchCommand canExecute uses HasSearchText
            // (bool property) instead of comparing SearchText directly.
            // The XAML button visibility is handled by HasSearchText binding
            // not by BoolToVisibilityConverter on a string.
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

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _allRows = rows;
                    _isLoaded = true;
                    ApplySearch();
                    UpdateKpis(rows);
                });

                StatusMessage = rows.Count == 0
                    ? "No statements found. Click '+ New Statement' to begin."
                    : $"{rows.Count} statement(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading statements: {ex.Message}";
                _isLoaded = true; // allow empty state to show on error
            }
            finally
            {
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

            Rows.Clear();
            foreach (var r in filtered)
                Rows.Add(r);

            // Notify IsEmpty after Rows is updated
            OnPropertyChanged(nameof(IsEmpty));
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
            if (row == null) return;

            var result = MessageBox.Show(
                $"Delete statement {row.StatementNumber}?\n\n" +
                "This will also delete the PDF file if it exists.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatementPdfExporter.DeletePdfs(row.StatementNumber);
                await Task.Run(() => _repo.DeleteAsync(row.Id));

                _allRows.Remove(row);
                ApplySearch();
                UpdateKpis(_allRows);

                StatusMessage =
                    $"Statement {row.StatementNumber} deleted.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }
    }
}
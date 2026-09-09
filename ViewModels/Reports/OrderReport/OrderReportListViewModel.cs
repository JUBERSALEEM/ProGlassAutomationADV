// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/OrderReport/OrderReportListViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — saved reports list
//
// Mirrors ViewModels/Reports/StatementList/StatementListViewModel.cs:
// search + KPI cards + open/regenerate/delete, with live refresh on the
// cross-module DocumentLifecycle signal.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.OrderReport.Pdf;
using ProGlassAutomation.Services.OrderReport.Repository;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.OrderReport
{
    public class OrderReportListViewModel : ViewModelBase
    {
        private readonly IOrderReportRepository _repo;
        private readonly IDialogService _dialogs;
        private readonly IDatabaseServices _database;

        public BulkObservableCollection<OrderReportListRowViewModel> Rows { get; } = new();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchText));
                ApplySearch();
            }
        }
        public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }

        private bool _isLoaded;
        public bool IsEmpty => _isLoaded && !_isLoading && Rows.Count == 0;
        public bool IsListVisible => _isLoaded && !_isLoading && Rows.Count > 0;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
        }

        private OrderReportListRowViewModel? _selectedRow;
        public OrderReportListRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set { if (_selectedRow == value) return; _selectedRow = value; OnPropertyChanged(); }
        }

        // ── KPI summary ───────────────────────────────────────────────────
        private int _totalReports;
        private decimal _totalInvoicedValue;
        private int _totalMissingDocuments;

        public string TotalReportsDisplay => _totalReports.ToString();
        public string TotalInvoicedValueDisplay => $"AED {_totalInvoicedValue:N2}";
        public string TotalMissingDocumentsDisplay =>
            _totalMissingDocuments > 0 ? _totalMissingDocuments.ToString() : "None";
        public bool HasMissingDocuments => _totalMissingDocuments > 0;

        // ── Callbacks (set by module VM) ──────────────────────────────────
        public Action? OnRequestNewReport { get; set; }
        public Action<OrderReportListItem>? OnRequestOpenReport { get; set; }

        public ICommand NewReportCommand { get; }
        public ICommand OpenReportCommand { get; }
        public ICommand OpenPdfCommand { get; }
        public ICommand ShareReportCommand { get; }
        public ICommand DeleteReportCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        private List<OrderReportListRowViewModel> _allRows = new();

        public OrderReportListViewModel(
            IDialogService? dialogs,
            IOrderReportRepository repo,
            IDatabaseServices? database)
        {
            _dialogs = dialogs!;
            _repo = repo;
            _database = database!;

            _database.SubscribeDocumentsChanged(LoadAsync);

            NewReportCommand = new RelayCommand(_ => OnRequestNewReport?.Invoke());

            OpenReportCommand = new RelayCommand(
                p =>
                {
                    var row = p as OrderReportListRowViewModel ?? SelectedRow;
                    if (row != null) OnRequestOpenReport?.Invoke(row.Source);
                },
                p => (p as OrderReportListRowViewModel ?? SelectedRow) != null);

            OpenPdfCommand = new RelayCommand(
                p => OpenPdf(p as OrderReportListRowViewModel ?? SelectedRow),
                p => (p as OrderReportListRowViewModel ?? SelectedRow)?.HasPdf == true);

            ShareReportCommand = new RelayCommand(
                p => ShareReport(p as OrderReportListRowViewModel ?? SelectedRow),
                p => (p as OrderReportListRowViewModel ?? SelectedRow)?.HasPdf == true);

            DeleteReportCommand = new RelayCommand(
                async p => await DeleteAsync(p as OrderReportListRowViewModel ?? SelectedRow),
                p => (p as OrderReportListRowViewModel ?? SelectedRow) != null);

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearSearchCommand = new RelayCommand(_ => SearchText = "", _ => HasSearchText);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading order reports…";

            try
            {
                var items = await Task.Run(() => _repo.GetAllAsync());
                var rows = items.Select(i => new OrderReportListRowViewModel(i)).ToList();

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _allRows = rows;
                    _isLoaded = true;
                    ApplySearch();
                    UpdateKpis(rows);
                    StatusMessage = rows.Count == 0
                        ? "No reports yet. Click '+ New Report' to generate one."
                        : $"{rows.Count} report(s) loaded.";
                }));
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isLoaded = true;
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsListVisible));
                    StatusMessage = $"Error loading reports: {ExceptionMessages.Deep(ex)}";
                }));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplySearch()
        {
            var filtered = string.IsNullOrWhiteSpace(_searchText)
                ? _allRows
                : _allRows.Where(r =>
                    r.ReportNumber.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    r.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    r.FilterCustomer.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    r.FilterProject.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    r.FilterCompany.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                  .ToList();

            Rows.ClearSilent();
            Rows.AddRangeSilent(filtered);
            Rows.RaiseReset();

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsListVisible));
        }

        private void UpdateKpis(List<OrderReportListRowViewModel> rows)
        {
            _totalReports = rows.Count;
            _totalInvoicedValue = rows.Sum(r => r.Source.TotalInvoicedValue);
            _totalMissingDocuments = rows.Sum(r => r.Source.MissingDocuments);
            OnPropertyChanged(string.Empty);
        }

        private static void OpenPdf(OrderReportListRowViewModel? row)
        {
            if (row == null || !row.HasPdf) return;
            OrderReportPdfExporter.OpenPdf(row.PdfPath);
        }

        // ── Sharing Center (Pattern B: the report PDF already exists) ──
        private void ShareReport(OrderReportListRowViewModel? row)
        {
            if (row == null || !row.HasPdf) return;

            var item = row.Source;
            var context = new ProGlassAutomation.Services.Sharing.ShareContext
            {
                DocumentTypeKey = "OrderReport",
                DocumentNumber = item?.ReportNumber ?? "",
                Title = string.IsNullOrWhiteSpace(item?.Title)
                    ? $"Order Report {item?.ReportNumber}"
                    : item.Title.Trim(),
                CustomerName = item?.FilterCustomer ?? "",
                CompanyName = item?.FilterCompany ?? "",
                SalesmanName = item?.FilterSalesman ?? "",
                SourceModule = "OrderReport"
            };

            if (ProGlassAutomation.Services.Sharing.ShareLauncher
                    .AttachFile(context, row.PdfPath, _dialogs))
                ProGlassAutomation.Services.Sharing.ShareLauncher
                    .OpenComposer(context, _dialogs);
        }

        private async Task DeleteAsync(OrderReportListRowViewModel? row)
        {
            if (!PermissionService.Require(AppPermissions.StatementGenerate, "delete an order report"))
                return;

            if (row == null) return;

            var result = _dialogs.Show(
                $"Delete report {row.ReportNumber}? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                OrderReportPdfExporter.DeletePdfs(row.ReportNumber, row.PdfPath);
                await Task.Run(() => _repo.DeleteAsync(row.Id));

                _allRows.Remove(row);
                ApplySearch();
                UpdateKpis(_allRows);
                StatusMessage = $"Report {row.ReportNumber} deleted.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ExceptionMessages.Deep(ex)}";
            }
        }
    }
}

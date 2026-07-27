using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.Reports.Repository;
using ProGlassAutomation.Services.Reports.Export;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.Sales
{
    public class SalesReportViewModel : ViewModelBase
    {
        private readonly ISalesReportRepository _repository;
        private List<SalesReport> _allRecords = new();

        // ── Sub ViewModels ────────────────────────────────────────────────

        public SalesFilterViewModel FilterVM { get; }
        public SalesDashboardViewModel DashboardVM { get; }

        // ── Data lists ───────────────────────────────────────────────────

        public ObservableCollection<SalesReport> Invoices { get; } = new();

        // ── Trace Workflow Chain ─────────────────────────────────────────

        private string _searchInvoiceNumber = "";
        public string SearchInvoiceNumber
        {
            get => _searchInvoiceNumber;
            set { if (_searchInvoiceNumber == value) return; _searchInvoiceNumber = value; OnPropertyChanged(); }
        }

        private string _traceProformaId = "—";
        public string TraceProformaId
        {
            get => _traceProformaId;
            set { _traceProformaId = value; OnPropertyChanged(); }
        }

        private string _traceJobOrderId = "—";
        public string TraceJobOrderId
        {
            get => _traceJobOrderId;
            set { _traceJobOrderId = value; OnPropertyChanged(); }
        }

        private string _traceDeliveryNoteId = "—";
        public string TraceDeliveryNoteId
        {
            get => _traceDeliveryNoteId;
            set { _traceDeliveryNoteId = value; OnPropertyChanged(); }
        }

        private string _traceTaxInvoiceId = "—";
        public string TraceTaxInvoiceId
        {
            get => _traceTaxInvoiceId;
            set { _traceTaxInvoiceId = value; OnPropertyChanged(); }
        }

        private bool _isChainFound = false;
        public bool IsChainFound
        {
            get => _isChainFound;
            set { _isChainFound = value; OnPropertyChanged(); }
        }

        // ── State flags ──────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand TraceWorkflowCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public SalesReportViewModel()
        {
            _repository = new SalesReportRepository();

            FilterVM = new SalesFilterViewModel(_repository);
            DashboardVM = new SalesDashboardViewModel();

            // Bind filter change to automatic grid update
            FilterVM.FilterChanged += ApplyFilters;

            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            TraceWorkflowCommand = new RelayCommand(async _ => await TraceWorkflowAsync());

            ExportPdfCommand = new RelayCommand(async _ => await ExportPdfAsync(), _ => Invoices.Count > 0);
            ExportExcelCommand = new RelayCommand(async _ => await ExportExcelAsync(), _ => Invoices.Count > 0);
            ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync(), _ => Invoices.Count > 0);

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                _allRecords = await _repository.GetSalesReportAsync();

                // Load filters list from newly retrieved database records
                await FilterVM.LoadFilterOptionsAsync();

                // Initial unfiltered draw
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Sales Reports data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            var filter = FilterVM.ToFilterModel();

            // Re-query repository with active filters
            var queryTask = _repository.GetSalesReportAsync(filter);

            // Execute synchronously since this triggers from property changes
            queryTask.ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return;

                var filteredList = t.Result;

                // Dispatch to UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Invoices.Clear();
                    foreach (var r in filteredList)
                    {
                        Invoices.Add(r);
                    }

                    // Update child KPI metrics card + charts
                    DashboardVM.UpdateDashboard(filteredList);
                });
            });
        }

        private async Task TraceWorkflowAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchInvoiceNumber))
            {
                MessageBox.Show("Please enter a valid Tax Invoice Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var chain = await _repository.GetDocumentChainAsync(SearchInvoiceNumber.Trim());
                if (chain == null)
                {
                    IsChainFound = false;
                    TraceProformaId = "—";
                    TraceJobOrderId = "—";
                    TraceDeliveryNoteId = "—";
                    TraceTaxInvoiceId = "—";
                    MessageBox.Show($"No document chain found for invoice '{SearchInvoiceNumber}'.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                TraceProformaId = string.IsNullOrWhiteSpace(chain.Value.ProformaId) ? "—" : chain.Value.ProformaId;
                TraceJobOrderId = string.IsNullOrWhiteSpace(chain.Value.JobOrderId) ? "—" : chain.Value.JobOrderId;
                TraceDeliveryNoteId = string.IsNullOrWhiteSpace(chain.Value.DeliveryNoteId) ? "—" : chain.Value.DeliveryNoteId;
                TraceTaxInvoiceId = chain.Value.InvoiceNumber;
                IsChainFound = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Trace operation failed:\n\n{ex.Message}", "Trace Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportPdfAsync()
        {
            var activeInvoices = Invoices.ToList();
            var activeSummary = DashboardVM.Summary;

            var layout = new SalesReportPdfService(activeInvoices, activeSummary, FilterVM.ToFilterModel());
            var engine = new Services.Export.Pdf.PdfExportEngine();
            var request = new Services.Export.Core.ExportRequest { PromptForLocation = true, OpenAfterSave = true };

            var result = await engine.ExportAsync(layout, request);
            if (!result.Success && !result.Cancelled)
            {
                MessageBox.Show($"Export PDF failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportExcelAsync()
        {
            var activeInvoices = Invoices.ToList();
            var activeSummary = DashboardVM.Summary;

            var layout = new SalesReportExcelService(activeInvoices, activeSummary, FilterVM.ToFilterModel());
            var engine = new Services.Export.Excel.ExcelExportEngine();
            var request = new Services.Export.Core.ExportRequest { PromptForLocation = true, OpenAfterSave = true };

            var result = await engine.ExportAsync(layout, request);
            if (!result.Success && !result.Cancelled)
            {
                MessageBox.Show($"Export Excel failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportCsvAsync()
        {
            var activeInvoices = Invoices.ToList();

            var layout = new SalesReportCsvService(activeInvoices, FilterVM.ToFilterModel());
            var engine = new Services.Export.Csv.CsvExportEngine();
            var request = new Services.Export.Core.ExportRequest { PromptForLocation = true, OpenAfterSave = true };

            var result = await engine.ExportAsync(layout, request);
            if (!result.Success && !result.Cancelled)
            {
                MessageBox.Show($"Export CSV failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.DeliveryReport
{
    /// <summary>
    /// ROW-LEVEL Delivery Note report — SINGLE unified grid. One row per
    /// exact PI specification row (PI → Specification → Sr No) with Total /
    /// Delivered / Balance quantities reconciled per row from the
    /// DeliveryNoteItems table. The related Delivery Notes are revealed
    /// INLINE (expander) inside each row — no split list + side panel.
    ///
    /// Filters are DATA-DRIVEN: PI number, Specification, Glass Ref,
    /// Salesman, Project, Customer and delivery Status are all populated
    /// from the distinct values actually stored (never hardcoded).
    /// </summary>
    public class DeliveryReportViewModel : ReportBaseViewModel
    {
        /// <summary>Sentinel shown at the top of every filter dropdown.</summary>
        public const string AllFilter = "All";

        public DeliveryReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();

            SelectedStatus = AllStatusesOption;
            SetStatusOptions(new[] { "Not delivered", "Partially delivered", "Fully delivered" });

            ClearFilterCommand = new RelayCommand(_ =>
            {
                PiFilter = AllFilter;
                SpecificationFilter = AllFilter;
                GlassRefFilter = AllFilter;
                SalesmanFilter = AllFilter;
                ProjectFilter = AllFilter;
                CustomerFilter = AllFilter;
                SelectedStatus = AllStatusesOption;
                ClearFilters(); // resets dates/search and triggers a reload
            });

            _ = LoadDeliveryFilterOptionsAsync();
            _ = LoadAsync();
        }

        // ── Filters (data-driven dropdowns; "All" = no filter) ──────────

        private string _piFilter = AllFilter;
        public string PiFilter
        {
            get => _piFilter;
            set { if (_piFilter == value) return; _piFilter = value ?? AllFilter; OnPropertyChanged(); }
        }

        private string _specificationFilter = AllFilter;
        public string SpecificationFilter
        {
            get => _specificationFilter;
            set { if (_specificationFilter == value) return; _specificationFilter = value ?? AllFilter; OnPropertyChanged(); }
        }

        private string _glassRefFilter = AllFilter;
        public string GlassRefFilter
        {
            get => _glassRefFilter;
            set { if (_glassRefFilter == value) return; _glassRefFilter = value ?? AllFilter; OnPropertyChanged(); }
        }

        private string _salesmanFilter = AllFilter;
        public string SalesmanFilter
        {
            get => _salesmanFilter;
            set { if (_salesmanFilter == value) return; _salesmanFilter = value ?? AllFilter; OnPropertyChanged(); }
        }

        private string _projectFilter = AllFilter;
        public string ProjectFilter
        {
            get => _projectFilter;
            set { if (_projectFilter == value) return; _projectFilter = value ?? AllFilter; OnPropertyChanged(); }
        }

        private string _customerFilter = AllFilter;
        public string CustomerFilter
        {
            get => _customerFilter;
            set { if (_customerFilter == value) return; _customerFilter = value ?? AllFilter; OnPropertyChanged(); }
        }

        // ── Option lists (distinct stored values, "All" first) ──────────

        private List<string> _piOptions = new() { AllFilter };
        public List<string> PiOptions { get => _piOptions; private set { _piOptions = value; OnPropertyChanged(); } }

        private List<string> _specificationOptions = new() { AllFilter };
        public List<string> SpecificationOptions { get => _specificationOptions; private set { _specificationOptions = value; OnPropertyChanged(); } }

        private List<string> _glassRefOptions = new() { AllFilter };
        public List<string> GlassRefOptions { get => _glassRefOptions; private set { _glassRefOptions = value; OnPropertyChanged(); } }

        private List<string> _salesmanOptions = new() { AllFilter };
        public List<string> DeliverySalesmanOptions { get => _salesmanOptions; private set { _salesmanOptions = value; OnPropertyChanged(); } }

        private List<string> _projectOptions = new() { AllFilter };
        public List<string> DeliveryProjectOptions { get => _projectOptions; private set { _projectOptions = value; OnPropertyChanged(); } }

        private List<string> _customerOptions = new() { AllFilter };
        public List<string> DeliveryCustomerOptions { get => _customerOptions; private set { _customerOptions = value; OnPropertyChanged(); } }

        /// <summary>Prepend the "All" sentinel to a distinct-value list.</summary>
        private static List<string> WithAll(IEnumerable<string> distinct)
        {
            var list = new List<string> { AllFilter };
            list.AddRange((distinct ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)));
            return list;
        }

        private async Task LoadDeliveryFilterOptionsAsync()
        {
            try
            {
                PiOptions = WithAll(await _service.GetDeliveryPiOptionsAsync());
                SpecificationOptions = WithAll(await _service.GetDeliverySpecificationOptionsAsync());
                GlassRefOptions = WithAll(await _service.GetDeliveryGlassRefOptionsAsync());
                DeliverySalesmanOptions = WithAll(await _service.GetDeliverySalesmanOptionsAsync());
                DeliveryProjectOptions = WithAll(await _service.GetDeliveryProjectOptionsAsync());
                DeliveryCustomerOptions = WithAll(await _service.GetCustomerOptionsAsync());
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Warning($"Delivery filter options warning: {ex.Message}");
            }
        }

        /// <summary>Maps the "All" sentinel back to no filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            return new ReportingFilter
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                ProformaId = NotAll(PiFilter),
                Specification = NotAll(SpecificationFilter),
                GlassRef = NotAll(GlassRefFilter),
                Salesman = NotAll(SalesmanFilter),
                Project = NotAll(ProjectFilter),
                Customer = NotAll(CustomerFilter),
                Status = NotAll(SelectedStatus),
                Page = Page,
                PageSize = PageSize
            };
        }

        private static string? NotAll(string value) =>
            string.IsNullOrWhiteSpace(value) || string.Equals(value, AllFilter, StringComparison.OrdinalIgnoreCase)
                ? null : value.Trim();

        // ── Summary ──────────────────────────────────────────────────────

        private DeliveryReportSummary _summary = new();
        public DeliveryReportSummary Summary
        {
            get => _summary;
            set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); OnPropertyChanged(nameof(CompletionText)); }
        }

        public string CompletionText =>
            $"{Summary.CompletionPct:0.0}% delivered ({Summary.TotalDeliveredQty:N0}/{Summary.TotalOrderedQty:N0} pcs)";

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("ROWS", Summary.TotalRows.ToString("N0"), CompletionText, "AccentBrush"),
            new KpiItem("NOT DELIVERED", Summary.NotDelivered.ToString("N0"), "No delivery yet", "NeutralBrush"),
            new KpiItem("PARTIAL", Summary.Partial.ToString("N0"), "In progress", "WarningBrush"),
            new KpiItem("FULLY DELIVERED", Summary.FullyDelivered.ToString("N0"), "Balance = 0", "SuccessBrush"),
        };

        private ObservableCollection<DeliveryReportRow> _rows = new();
        public ObservableCollection<DeliveryReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        /// <summary>
        /// Lazy-loads the related Delivery Notes for one row and fills the
        /// inline ObservableCollection in place (so the expanded list
        /// refreshes without re-creating the row).
        /// </summary>
        public async Task LoadRowDeliveriesAsync(DeliveryReportRow row)
        {
            if (row == null || row.HasLoadedDeliveries) return;

            try
            {
                var deliveries = await _service.GetRowDeliveriesAsync(
                    row.JobOrderId, row.SpecGroupNumber, row.SerialNumber);

                row.Deliveries.Clear();
                foreach (var d in deliveries ?? new List<DeliveryNoteDelivery>())
                    row.Deliveries.Add(d);
                row.HasLoadedDeliveries = true;
            }
            catch (Exception ex)
            {
                row.Deliveries.Clear();
                row.HasLoadedDeliveries = true;
                Services.Logging.AppLogService.Warning($"Could not load delivery history: {ex.Message}");
            }
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetDeliverySummaryAsync(filter, token);
                var paged = await _service.GetDeliveryRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<DeliveryReportRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            { HasError = true; ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Delivery_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}

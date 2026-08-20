using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.DeliveryReport
{
    /// <summary>
    /// ROW-LEVEL Delivery Note report. One row per exact PI specification row
    /// (PI → Specification → Sr No) with Total / Delivered / Balance
    /// quantities reconciled per row from the DeliveryNoteItems table.
    /// Selecting a row loads its related Delivery Notes into the right-side
    /// detail panel.
    /// </summary>
    public class DeliveryReportViewModel : ReportBaseViewModel
    {
        public DeliveryReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();

            // Row-level status filter defaults to "All Statuses" and clearing
            // the filters restores it (the base resets SelectedStatus to "").
            SelectedStatus = "All Statuses";
            ClearFilterCommand = new RelayCommand(_ =>
            {
                ClearFilters();
                SelectedStatus = "All Statuses";
            });

            CloseDetailCommand = new RelayCommand(_ => SelectedRow = null);

            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        public ICommand CloseDetailCommand { get; private set; } = null!;

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

        // ── Row selection → right-side detail panel ────────────────────
        private DeliveryReportRow? _selectedRow;
        public DeliveryReportRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (ReferenceEquals(_selectedRow, value)) return;
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedRow));
                _ = LoadRowDeliveriesAsync();
            }
        }
        public bool HasSelectedRow => _selectedRow != null;

        // ── Row-level status filter options ────────────────────────────
        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses",
            "Not delivered",
            "Partially delivered",
            "Fully delivered"
        };

        /// <summary>Maps the UI's "All Statuses" option to a null filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, "All Statuses", StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            return filter;
        }

        private async Task LoadRowDeliveriesAsync()
        {
            var row = SelectedRow;
            if (row == null) return;

            try
            {
                var deliveries = await _service.GetRowDeliveriesAsync(
                    row.JobOrderId, row.SpecGroupNumber, row.SerialNumber);
                row.Deliveries = deliveries ?? new List<DeliveryNoteDelivery>();
                OnPropertyChanged(nameof(SelectedRow));
                OnPropertyChanged(nameof(HasSelectedRow));
            }
            catch (System.Exception ex)
            {
                row.Deliveries = new List<DeliveryNoteDelivery>();
                Services.Logging.AppLogService.Warning(
                    $"Could not load delivery history: {ex.Message}");
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
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            { HasError = true; ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Delivery_Report_{System.DateTime.Now:yyyyMMdd}" };
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

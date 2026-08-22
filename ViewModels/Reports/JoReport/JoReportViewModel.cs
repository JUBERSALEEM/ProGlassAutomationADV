using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.JoReport
{
    public class JoReportViewModel : ReportBaseViewModel
    {
        public JoReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            SelectedStatus = AllStatusesOption;
            _ = LoadFilterOptionsAsync();
            _ = LoadStatusOptionsAsync();
            _ = LoadAsync();
        }

        /// <summary>Populates the status dropdown from DISTINCT statuses
        /// actually stored (never a hardcoded list that drifts).</summary>
        private async Task LoadStatusOptionsAsync()
        {
            try { SetStatusOptions(await _service.GetJoStatusOptionsAsync()); }
            catch (System.Exception ex) { Services.Logging.AppLogService.Warning($"JO status options warning: {ex.Message}"); }
        }

        /// <summary>Maps the "All" sentinel back to no status filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            return filter;
        }

        private JoReportSummary _summary = new();
        public JoReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TOTAL JO", Summary.TotalJo.ToString("N0"), $"Value AED {Summary.TotalValue:N0}", "AccentBrush"),
            new KpiItem("AWAITING JO", Summary.AwaitingJo.ToString("N0"), "PI not yet converted", "WarningBrush"),
            new KpiItem("IN PRODUCTION", GetStatusCount("In Production", "InProduction").ToString("N0"), "Active jobs", "InfoBrush"),
            new KpiItem("COMPLETED", GetStatusCount("Completed").ToString("N0"), "Finished", "SuccessBrush"),
        };

        private int GetStatusCount(params string[] statusKeys)
        {
            int sum = 0;
            foreach (var key in statusKeys)
            {
                if (Summary.ByStatus.TryGetValue(key, out var count))
                    sum += count;
            }
            return sum;
        }

        private ObservableCollection<JoReportRow> _rows = new();
        public ObservableCollection<JoReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        private JoReportRow? _selectedRow;
        public JoReportRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedRow));
                OnPropertyChanged(nameof(SidebarSubtitle));
            }
        }
        public bool HasSelectedRow => SelectedRow != null;
        public string SidebarSubtitle => SelectedRow?.JobOrderId ?? "";

        /// <summary>Raised with the selected JO's DB id when the user clicks
        /// "Open Job Order". Wired by the hub to MainViewModel.</summary>
        public Action<int>? OpenDocumentRequested { get; set; }
        public ICommand CloseDetailCommand { get; private set; } = null!;
        public ICommand OpenDocumentCommand { get; private set; } = null!;

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ =>
            {
                ClearFilters();
                SelectedStatus = AllStatusesOption;
            });
            CloseDetailCommand = new RelayCommand(_ => SelectedRow = null);
            OpenDocumentCommand = new RelayCommand(_ =>
            {
                if (SelectedRow != null)
                    OpenDocumentRequested?.Invoke(SelectedRow.Id);
            }, _ => SelectedRow != null);
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetJoSummaryAsync(filter, token);
                var paged = await _service.GetJoRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<JoReportRow>(paged.Items);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"JO_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Job Order Report", ReportingExportService.JoHeaders, ReportingExportService.JoMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Job Order Report", ReportingExportService.JoHeaders, ReportingExportService.JoMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Job Order Report", ReportingExportService.JoHeaders, ReportingExportService.JoMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}

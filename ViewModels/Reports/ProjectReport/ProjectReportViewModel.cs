using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.ProjectReport
{
    public class ProjectReportViewModel : ReportBaseViewModel
    {
        public ProjectReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadAsync();
        }

        private ObservableCollection<ProjectReportRow> _rows = new();
        public ObservableCollection<ProjectReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        private ProjectReportRow? _selected;
        public ProjectReportRow? Selected { get => _selected; set { _selected = value; OnPropertyChanged(); if (value != null) _ = LoadProjectDetailAsync(value.ProjectName); } }

        private ObservableCollection<PiReportRow> _piRows = new();
        public ObservableCollection<PiReportRow> PiRows { get => _piRows; set { _piRows = value; OnPropertyChanged(); } }

        private async Task LoadProjectDetailAsync(string project)
        {
            try
            {
                var pi = await _service.GetPiRowsAsync(new ReportingFilter { Project = project, PageSize = 20 });
                PiRows = new ObservableCollection<PiReportRow>(pi.Items);
            }
            catch { }
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var paged = await _service.GetProjectRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Rows = new ObservableCollection<ProjectReportRow>(paged.Items);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Project_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Project Report", ReportingExportService.ProjectHeaders, ReportingExportService.ProjectMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Project Report", ReportingExportService.ProjectHeaders, ReportingExportService.ProjectMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Project Report", ReportingExportService.ProjectHeaders, ReportingExportService.ProjectMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}

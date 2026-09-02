using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Reports.JoReport
{
    public class JoReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;

        public JoReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public JoReportViewModel(IDialogService? dialogs, IReportingDataService? service = null, IDatabaseServices? database = null) : base(service ?? new ReportingDataService(), database)
        {
            _dialogs = dialogs!;
            InitCommands();
            SelectedStatus = AllStatusesOption;
            SourcePiFilter = AllFilter;
            _ = LoadFilterOptionsAsync();
            _ = LoadStatusOptionsAsync();
            _ = LoadSourcePiOptionsAsync();
            _ = LoadAsync();
        }

        /// <summary>Sentinel shown at the top of the source-PI dropdown.</summary>
        public const string AllFilter = "All";

        private string _sourcePiFilter = AllFilter;
        public string SourcePiFilter
        {
            get => _sourcePiFilter;
            set { if (_sourcePiFilter == value) return; _sourcePiFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private List<string> _sourcePiOptions = new() { AllFilter };
        public List<string> SourcePiOptions { get => _sourcePiOptions; private set { _sourcePiOptions = value; OnPropertyChanged(); } }

        private async Task LoadSourcePiOptionsAsync()
        {
            try
            {
                var list = new List<string> { AllFilter };
                list.AddRange(await _service.GetJoSourcePiOptionsAsync());
                SourcePiOptions = list;
            }
            catch (System.Exception ex) { Services.Logging.AppLogService.Warning($"JO source-PI options warning: {ExceptionMessages.Deep(ex)}"); }
        }

        /// <summary>Populates the status dropdown from DISTINCT statuses
        /// actually stored (never a hardcoded list that drifts).</summary>
        private async Task LoadStatusOptionsAsync()
        {
            try { SetStatusOptions(await _service.GetJoStatusOptionsAsync()); }
            catch (System.Exception ex) { Services.Logging.AppLogService.Warning($"JO status options warning: {ExceptionMessages.Deep(ex)}"); }
        }

        /// <summary>Maps the "All" sentinel back to no filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            filter.ProformaId = string.IsNullOrWhiteSpace(SourcePiFilter) || string.Equals(SourcePiFilter, AllFilter, StringComparison.OrdinalIgnoreCase)
                ? null : SourcePiFilter.Trim();
            return filter;
        }

        private JoReportSummary _summary = new();
        public JoReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        // ── MIX ANALYTICS (slice 2) ──────────────────────────────────────
        // Answers "which specifications and colours are most used".
        // Computed from the same filtered set as Rows, so the Pareto tables
        // always reconcile to the grid beneath them.
        private JoAnalyticsSummary _analytics = new();
        public JoAnalyticsSummary Analytics
        {
            get => _analytics;
            set
            {
                _analytics = value ?? new JoAnalyticsSummary();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnalytics));
            }
        }

        /// <summary>Drives visibility: no rows means no meaningful mix.</summary>
        public bool HasAnalytics => _analytics is { HasData: true };

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

        /// <summary>Raised with the JO's DB id when the user clicks
        /// "Open Job Order". Wired by the hub to MainViewModel.</summary>
        public Action<int>? OpenDocumentRequested { get; set; }
        public ICommand OpenDocumentCommand { get; private set; } = null!;

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ =>
            {
                SourcePiFilter = AllFilter;
                ClearFilters();
                SelectedStatus = AllStatusesOption;
            });
            OpenDocumentCommand = new RelayCommand(p =>
            {
                if (p is JoReportRow r)
                    OpenDocumentRequested?.Invoke(r.Id);
            });
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

                // Analytics is an ENHANCEMENT, never a gatekeeper: it is
                // loaded after the grid is populated and its failure is
                // swallowed by the service, so a mix-analysis problem can
                // never blank the detail table the user came for.
                Analytics = await _service.GetJoAnalyticsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            { HasError = true; ErrorMessage = ExceptionMessages.Deep(ex); }
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
                if (!res.Success && !res.Cancelled) _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}

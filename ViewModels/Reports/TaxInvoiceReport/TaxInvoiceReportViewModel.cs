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
namespace ProGlassAutomation.ViewModels.Reports.TaxInvoiceReport
{
    public class TaxInvoiceReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;

        public TaxInvoiceReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public TaxInvoiceReportViewModel(IDialogService? dialogs, IReportingDataService? service = null, IDatabaseServices? database = null) : base(service ?? new ReportingDataService(), database)
        {
            _dialogs = dialogs!;
            InitCommands();
            SetSortOptions(new[] { "Date", "Amount", "Customer", "Company", "Status", "Invoice", "DueDate" }, "Date");
            SelectedStatus = AllStatusesOption;
            PiFilter = AllFilter;
            _ = LoadFilterOptionsAsync();
            _ = LoadStatusOptionsAsync();
            _ = LoadPiOptionsAsync();
            _ = LoadAsync();
        }

        /// <summary>Sentinel shown at the top of the PI dropdown.</summary>
        public const string AllFilter = "All";

        private string _piFilter = AllFilter;
        public string PiFilter
        {
            get => _piFilter;
            set { if (_piFilter == value) return; _piFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private List<string> _piOptions = new() { AllFilter };
        public List<string> PiOptions { get => _piOptions; private set { _piOptions = value; OnPropertyChanged(); } }

        private async Task LoadPiOptionsAsync()
        {
            try
            {
                var list = new List<string> { AllFilter };
                list.AddRange(await _service.GetTiProformaOptionsAsync());
                PiOptions = list;
            }
            catch (System.Exception ex) { Services.Logging.AppLogService.Warning($"TI PI options warning: {ExceptionMessages.Deep(ex)}"); }
        }

        /// <summary>Populates the status dropdown from DISTINCT statuses
        /// actually stored (never a hardcoded list that drifts).</summary>
        private async Task LoadStatusOptionsAsync()
        {
            try { SetStatusOptions(await _service.GetTiStatusOptionsAsync()); }
            catch (System.Exception ex) { Services.Logging.AppLogService.Warning($"TI status options warning: {ExceptionMessages.Deep(ex)}"); }
        }

        /// <summary>Maps the "All" sentinel back to no filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            filter.ProformaId = string.IsNullOrWhiteSpace(PiFilter) || string.Equals(PiFilter, AllFilter, StringComparison.OrdinalIgnoreCase)
                ? null : PiFilter.Trim();
            return filter;
        }

        private TaxInvoiceReportSummary _summary = new();
        public TaxInvoiceReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TAX INVOICES", Summary.TotalInvoices.ToString("N0"), $"AED {Summary.TotalAmount:N0}", "AccentBrush"),
            new KpiItem("OUTSTANDING", $"AED {Summary.Outstanding:N0}", $"{Summary.Issued} issued", "WarningBrush"),
            new KpiItem("OVERDUE", Summary.Overdue.ToString("N0"), "Needs follow-up", "DangerBrush"),
            new KpiItem("PENDING INVOICE", Summary.PendingInvoice.ToString("N0"), "Confirmed PI without TI", "InfoBrush"),
        };

        private ObservableCollection<TaxInvoiceReportRow> _rows = new();
        public ObservableCollection<TaxInvoiceReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        /// <summary>Raised with the TI's DB id when the user clicks
        /// "Open Tax Invoice". Wired by the hub to MainViewModel.</summary>
        public Action<int>? OpenDocumentRequested { get; set; }
        public ICommand OpenDocumentCommand { get; private set; } = null!;

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ =>
            {
                PiFilter = AllFilter;
                ClearFilters();
                SelectedStatus = AllStatusesOption;
            });
            OpenDocumentCommand = new RelayCommand(p =>
            {
                if (p is TaxInvoiceReportRow r)
                    OpenDocumentRequested?.Invoke(r.Id);
            });
        }

        private ObservableCollection<TaxInvoiceReportRow> _pendingRows = new();
        public ObservableCollection<TaxInvoiceReportRow> PendingRows { get => _pendingRows; set { _pendingRows = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPending)); } }
        public bool HasPending => PendingRows.Count > 0;

        public override async Task LoadAsync()
        {
            CancelPrevious();
            int gen = _loadGen; // generation of THIS load — see ReportBaseViewModel
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetTaxInvoiceSummaryAsync(filter, token);
                var paged = await _service.GetTaxInvoiceRowsAsync(filter, token);
                var pending = await _service.GetPendingInvoicesAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<TaxInvoiceReportRow>(paged.Items);
                PendingRows = new ObservableCollection<TaxInvoiceReportRow>(pending);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            { HasError = true; ErrorMessage = ExceptionMessages.Deep(ex); }
            finally { if (gen == _loadGen) IsLoading = false; }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"TaxInvoice_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled) _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        // ── Sharing Center — the SAME core above, staged to temp ──
        protected override string ShareReportTitle => "Tax Invoice Report";

        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (Rows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"TaxInvoice_Report_{System.DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            if (format == ExportFormat.Csv) return await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Tax Invoice Report", ReportingExportService.TiHeaders, ReportingExportService.TiMap, req);
            else if (format == ExportFormat.Excel) return await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Tax Invoice Report", ReportingExportService.TiHeaders, ReportingExportService.TiMapExcel, req);
            else return await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Tax Invoice Report", ReportingExportService.TiHeaders, ReportingExportService.TiMap, req);
        }
    }
}

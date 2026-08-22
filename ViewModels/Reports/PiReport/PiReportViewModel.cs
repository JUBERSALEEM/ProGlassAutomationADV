// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/PiReport/PiReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — PI Report
//
// SHOWS:
//   Total PI, Draft, Released, Confirmed, Rejected, Pending,
//   Converted to JO, Not converted, Value, Company, Customer,
//   Salesman, Project, PI date, actual specification rows
//   Drill-down: PI → rows → JO → Delivery → TI → Profitability
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.PiReport
{
    public class PiReportViewModel : ReportBaseViewModel
    {
        public PiReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
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
            try { SetStatusOptions(await _service.GetPiStatusOptionsAsync()); }
            catch (System.Exception ex) { AppLogService.Warning($"PI status options warning: {ex.Message}"); }
        }

        /// <summary>Maps the "All" sentinel back to no status filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            return filter;
        }

        // ── Summary KPIs ──────────────────────────────────────────────────
        private PiReportSummary _summary = new();
        public PiReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TOTAL PI", Summary.TotalPi.ToString("N0"), $"AED {Summary.TotalValue:N0}", "AccentBrush"),
            new KpiItem("CONFIRMED", Summary.Confirmed.ToString("N0"), $"{Summary.Confirmed} approved/issued", "SuccessBrush"),
            new KpiItem("PENDING", Summary.Pending.ToString("N0"), $"{Summary.Draft} draft", "WarningBrush"),
            new KpiItem("CONVERTED", Summary.Converted.ToString("N0"), $"{Summary.NotConverted} not yet", "InfoBrush"),
        };

        // ── Rows ──────────────────────────────────────────────────────────
        private ObservableCollection<PiReportRow> _rows = new();
        public ObservableCollection<PiReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        private PiReportRow? _selectedRow;
        public PiReportRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedRow));
                OnPropertyChanged(nameof(SidebarSubtitle));
                if (value != null) _ = LoadDetailAsync(value.ProformaId);
            }
        }
        public bool HasSelectedRow => SelectedRow != null;
        public string SidebarSubtitle => SelectedRow?.ProformaId ?? "";

        /// <summary>Raised with the selected PI's DB id when the user clicks
        /// "Open Proforma Invoice". Wired by the hub to MainViewModel.</summary>
        public Action<int>? OpenDocumentRequested { get; set; }
        public ICommand CloseDetailCommand { get; private set; } = null!;
        public ICommand OpenDocumentCommand { get; private set; } = null!;

        // ── Detail ────────────────────────────────────────────────────────
        private ObservableCollection<GlassSpecReportRow> _detailRows = new();
        public ObservableCollection<GlassSpecReportRow> DetailRows { get => _detailRows; set { _detailRows = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDetail)); } }
        public bool HasDetail => DetailRows.Count > 0;

        private QuantityReconciliation? _reconciliation;
        public QuantityReconciliation? Reconciliation { get => _reconciliation; set { _reconciliation = value; OnPropertyChanged(); } }

        public ICommand DrillDownCommand { get; private set; } = null!;
        public ICommand OpenDetailCommand { get; private set; } = null!;

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ =>
            {
                ClearFilters();
                SelectedStatus = AllStatusesOption;
            });
            DrillDownCommand = new RelayCommand(p =>
            {
                if (p is PiReportRow r) SelectedRow = r;
            });
            OpenDetailCommand = new RelayCommand(async p =>
            {
                if (p is string piId) await LoadDetailAsync(piId);
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
                var summary = await _service.GetPiSummaryAsync(filter, token);
                if (token.IsCancellationRequested) return;
                var paged = await _service.GetPiRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = summary;
                Rows = new ObservableCollection<PiReportRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            {
                HasError = true; ErrorMessage = ex.Message;
                AppLogService.Warning($"PiReport LoadAsync error: {ex.Message}");
            }
            finally { IsLoading = false; }
        }

        private async Task LoadDetailAsync(string proformaId)
        {
            try
            {
                var rows = await _service.GetPiRowsDetailAsync(proformaId);
                DetailRows = new ObservableCollection<GlassSpecReportRow>(rows);
                Reconciliation = await _service.GetQuantityReconciliationAsync(proformaId);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"PI_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv)
                    res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Proforma Invoice Report", ReportingExportService.PiHeaders, ReportingExportService.PiMap, req);
                else if (format == ExportFormat.Excel)
                    res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Proforma Invoice Report", ReportingExportService.PiHeaders, ReportingExportService.PiMapExcel, req);
                else
                    res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Proforma Invoice Report", ReportingExportService.PiHeaders, ReportingExportService.PiMap, req);
                if (!res.Success && !res.Cancelled)
                    System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }

    public class KpiItem
    {
        public string Title { get; }
        public string Value { get; }
        public string Subtitle { get; }
        public string BrushKey { get; }
        public KpiItem(string title, string value, string subtitle, string brushKey)
        { Title = title; Value = value; Subtitle = subtitle; BrushKey = brushKey; }
    }
}

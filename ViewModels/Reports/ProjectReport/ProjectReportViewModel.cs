// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/ProjectReport/ProjectReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Project Register & Progress
//
// SHOWS (per project, one row):
//   • IDENTITY from documents + SavedProject master data: project name,
//     contract #, reference, LPO, site/delivery location, and the CUSTOMER
//     and COMPANY the project belongs to (the "customer's project" tie-in).
//   • ANALYTICS from ReportingDataService.GetProjectRowsAsync: PI / JO / TI
//     counts, PI value, invoiced value, OUTSTANDING.
//
// DATA SOURCING ("both, merged"):
//   Project analytics are invoice-derived (grouped by ProjectName). The
//   SavedProject roster is left-joined for the master-data fields and to
//   append projects saved but not yet invoiced. Join key is the trimmed,
//   case-insensitive project identifier name.
//
//   No new query written; export reuses ReportingExportService.ProjectHeaders/Map.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.ProjectReport
{
    /// <summary>One project row: identity + master-data detail merged with
    /// live analytics. Plain DTO, hydrated once per load.</summary>
    public sealed class ProjectRow
    {
        // ── Identity ─────────────────────────────────────────────────
        public string ProjectName { get; set; } = "";
        public string ProjectContractNo { get; set; } = "";
        public string ProjectReference { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Salesman { get; set; } = "";

        // ── Master-data detail (SavedProject) ────────────────────────
        public string SiteDeliveryLocation { get; set; } = "";
        public string LpoNumber { get; set; } = "";
        public bool IsSavedProject { get; set; }

        // ── Analytics ────────────────────────────────────────────────
        public int PiCount { get; set; }
        public int JoCount { get; set; }
        public int TiCount { get; set; }
        public decimal PiValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal Outstanding { get; set; }

        // ── Presentation helpers ─────────────────────────────────────
        public bool HasOutstanding => Outstanding > 0m;
        public string Initials => ReportInitials.From(ProjectName);

        public ProjectReportRow ToReportRow() => new()
        {
            ProjectName = ProjectName,
            ProjectContractNo = ProjectContractNo,
            ProjectReference = ProjectReference,
            CustomerName = CustomerName,
            CompanyName = CompanyName,
            Salesman = Salesman,
            PiCount = PiCount,
            JoCount = JoCount,
            TiCount = TiCount,
            PiValue = PiValue,
            InvoicedValue = InvoicedValue,
            Outstanding = Outstanding
        };
    }

    public class ProjectReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IMasterDataRepository? _masterData;

        private List<ProjectRow> _allRows = new();

        public ProjectReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public ProjectReportViewModel(
            IDialogService? dialogs,
            IReportingDataService? service = null,
            IDatabaseServices? database = null,
            IMasterDataRepository? masterData = null)
            : base(service ?? new ReportingDataService(), database)
        {
            _dialogs = dialogs!;
            _masterData = masterData ?? App.ServiceProvider?.GetService<IMasterDataRepository>();
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        private ObservableCollection<ProjectRow> _rows = new();
        public ObservableCollection<ProjectRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Summary KPIs ──────────────────────────────────────────────
        private int _totalProjects;
        public int TotalProjects { get => _totalProjects; private set { _totalProjects = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _savedProjectCount;
        public int SavedProjectCount { get => _savedProjectCount; private set { _savedProjectCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalOutstanding;
        public decimal TotalOutstanding { get => _totalOutstanding; private set { _totalOutstanding = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalPiValue;
        public decimal TotalPiValue { get => _totalPiValue; private set { _totalPiValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalInvoiced;
        public decimal TotalInvoiced { get => _totalInvoiced; private set { _totalInvoiced = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("PROJECTS", TotalProjects.ToString("N0"), $"{SavedProjectCount} on file", "AccentBrush"),
            new KpiItem("OUTSTANDING", $"AED {TotalOutstanding:N0}", "balance due across projects", "WarningBrush"),
            new KpiItem("PI VALUE", $"AED {TotalPiValue:N0}", "quoted across projects", "InfoBrush"),
            new KpiItem("INVOICED", $"AED {TotalInvoiced:N0}", CollectionRateDisplay + " collected", "SuccessBrush"),
        };

        private string CollectionRateDisplay =>
            _totalPiValue <= 0m ? "—" : $"{(_totalInvoiced / _totalPiValue) * 100m:0.#}%";

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                filter.Page = 1;
                filter.PageSize = int.MaxValue;
                var paged = await _service.GetProjectRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;

                var merged = await BuildMergedRowsAsync(paged.Items);
                if (token.IsCancellationRequested) return;

                merged = ApplyLocalFilter(merged);
                merged = merged
                    .OrderByDescending(r => r.Outstanding)
                    .ThenByDescending(r => r.PiValue)
                    .ThenBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _allRows = merged;
                RecomputeSummary(merged);

                TotalCount = merged.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(merged.Count / (double)PageSize));
                if (Page > TotalPages) Page = TotalPages;

                Rows = new ObservableCollection<ProjectRow>(
                    merged.Skip((Math.Max(1, Page) - 1) * PageSize).Take(PageSize));
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"ProjectReport LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally { IsLoading = false; }
        }

        private async Task<List<ProjectRow>> BuildMergedRowsAsync(IEnumerable<ProjectReportRow> analytics)
        {
            var byName = new Dictionary<string, ProjectRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in analytics)
            {
                var key = (a.ProjectName ?? "").Trim();
                if (key.Length == 0) key = "(No Project)";
                byName[key] = new ProjectRow
                {
                    ProjectName = a.ProjectName,
                    ProjectContractNo = a.ProjectContractNo,
                    ProjectReference = a.ProjectReference,
                    CustomerName = a.CustomerName,
                    CompanyName = a.CompanyName,
                    Salesman = a.Salesman,
                    PiCount = a.PiCount,
                    JoCount = a.JoCount,
                    TiCount = a.TiCount,
                    PiValue = a.PiValue,
                    InvoicedValue = a.InvoicedValue,
                    Outstanding = a.Outstanding
                };
            }

            if (_masterData != null)
            {
                try
                {
                    var projects = await _masterData.GetAllProjectsAsync();
                    foreach (var p in projects ?? new List<SavedProject>())
                    {
                        var key = (p.ProjectIdentifierName ?? "").Trim();
                        if (key.Length == 0) continue;

                        if (!byName.TryGetValue(key, out var row))
                        {
                            row = new ProjectRow { ProjectName = p.ProjectIdentifierName };
                            byName[key] = row;
                        }
                        row.IsSavedProject = true;
                        if (string.IsNullOrWhiteSpace(row.ProjectContractNo)) row.ProjectContractNo = p.ProjectContractNo ?? "";
                        if (string.IsNullOrWhiteSpace(row.ProjectReference)) row.ProjectReference = p.ProjectReference ?? "";
                        if (string.IsNullOrWhiteSpace(row.CustomerName)) row.CustomerName = p.CustomerName ?? "";
                        row.SiteDeliveryLocation = p.SiteDeliveryLocation ?? "";
                        row.LpoNumber = p.LpoNumber ?? "";
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Project roster merge warning: {ExceptionMessages.Deep(ex)}"); }
            }

            return byName.Values.ToList();
        }

        private List<ProjectRow> ApplyLocalFilter(List<ProjectRow> rows)
        {
            if (!HasSearchText) return rows;
            var q = SearchText.Trim();
            bool Match(string? s) => !string.IsNullOrEmpty(s) &&
                s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            return rows.Where(r =>
                Match(r.ProjectName) || Match(r.ProjectContractNo) || Match(r.ProjectReference) ||
                Match(r.CustomerName) || Match(r.CompanyName) || Match(r.Salesman) ||
                Match(r.LpoNumber) || Match(r.SiteDeliveryLocation)).ToList();
        }

        private void RecomputeSummary(List<ProjectRow> rows)
        {
            TotalProjects = rows.Count;
            SavedProjectCount = rows.Count(r => r.IsSavedProject);
            TotalOutstanding = rows.Sum(r => r.Outstanding);
            TotalPiValue = rows.Sum(r => r.PiValue);
            TotalInvoiced = rows.Sum(r => r.InvoicedValue);
        }

        // ── Export / Share ────────────────────────────────────────────
        protected override async Task ExportAsync(ExportFormat format)
        {
            if (_allRows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Project_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        protected override string ShareReportTitle => "Project Report";
        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (_allRows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Project_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            var reportRows = _allRows.Select(r => r.ToReportRow()).ToList();
            if (format == ExportFormat.Csv)
                return await ReportingExportService.ExportCsvAsync(reportRows, "Project Report", ReportingExportService.ProjectHeaders, ReportingExportService.ProjectMap, req);
            else if (format == ExportFormat.Excel)
                return await ReportingExportService.ExportExcelAsync(reportRows, "Project Report", ReportingExportService.ProjectHeaders, r => ReportingExportService.ProjectMap(r).Cast<object>().ToArray(), req);
            else
                return await ReportingExportService.ExportPdfAsync(reportRows, "Project Report", ReportingExportService.ProjectHeaders, ReportingExportService.ProjectMap, req);
        }
    }
}

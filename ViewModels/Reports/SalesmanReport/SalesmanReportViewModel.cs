// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/SalesmanReport/SalesmanReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Salesman Performance
//
// SHOWS (per salesman, one row):
//   PI count, confirmed count/value, customers served, projects,
//   confirmed revenue — from ReportingDataService.GetSalesmanRowsAsync,
//   which reads each PI's salesman from the invoice snapshot.
//
// DATA SOURCING:
//   Analytics are invoice-derived. The saved-salesman roster
//   (SavedSalesman in masterdata.db) is appended so a salesman on the books
//   with no confirmed invoices yet still appears (zero figures). Join key is
//   the trimmed, case-insensitive salesman name.
//
//   No new query was written — GetSalesmanRowsAsync already computes every
//   figure; export reuses ReportingExportService.SalesmanHeaders/Map.
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

namespace ProGlassAutomation.ViewModels.Reports.SalesmanReport
{
    /// <summary>One salesman row: performance analytics, optionally flagged as
    /// present in the saved-salesman roster. Plain DTO, hydrated per load.</summary>
    public sealed class SalesmanRow
    {
        public string SalesmanName { get; set; } = "";
        public string Department { get; set; } = "";
        public bool IsSavedSalesman { get; set; }

        public int PiCount { get; set; }
        public int ConfirmedPiCount { get; set; }
        public decimal ConfirmedValue { get; set; }
        public int CustomerCount { get; set; }
        public int ProjectCount { get; set; }
        public decimal Revenue { get; set; }

        public double ConversionRate =>
            PiCount <= 0 ? 0d : (double)ConfirmedPiCount / PiCount * 100d;
        public string ConversionDisplay =>
            PiCount <= 0 ? "—" : $"{ConversionRate:0.#}%";

        public string Initials
        {
            get
            {
                var n = (SalesmanName ?? "").Trim();
                if (n.Length == 0) return "—";
                var parts = n.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                    return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
                return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
            }
        }

        public SalesmanReportRow ToReportRow() => new()
        {
            SalesmanName = SalesmanName,
            PiCount = PiCount,
            ConfirmedPiCount = ConfirmedPiCount,
            ConfirmedValue = ConfirmedValue,
            CustomerCount = CustomerCount,
            ProjectCount = ProjectCount,
            Revenue = Revenue
        };
    }

    public class SalesmanReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IMasterDataRepository? _masterData;

        private List<SalesmanRow> _allRows = new();

        public SalesmanReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public SalesmanReportViewModel(
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

        private ObservableCollection<SalesmanRow> _rows = new();
        public ObservableCollection<SalesmanRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Summary KPIs ──────────────────────────────────────────────
        private int _totalSalesmen;
        public int TotalSalesmen { get => _totalSalesmen; private set { _totalSalesmen = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalRevenue;
        public decimal TotalRevenue { get => _totalRevenue; private set { _totalRevenue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _totalConfirmedPi;
        public int TotalConfirmedPi { get => _totalConfirmedPi; private set { _totalConfirmedPi = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _totalPi;
        public int TotalPi { get => _totalPi; private set { _totalPi = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private string _topPerformer = "—";
        public string TopPerformer { get => _topPerformer; private set { _topPerformer = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _topPerformerRevenue;

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("SALESMEN", TotalSalesmen.ToString("N0"), $"{TotalConfirmedPi} confirmed of {TotalPi} PIs", "AccentBrush"),
            new KpiItem("CONFIRMED REVENUE", $"AED {TotalRevenue:N0}", "approved / issued quotations", "SuccessBrush"),
            new KpiItem("CONVERSION", ConversionDisplay, "confirmed ÷ total PIs", "InfoBrush"),
            new KpiItem("TOP PERFORMER", TopPerformer, $"AED {_topPerformerRevenue:N0} revenue", "WarningBrush"),
        };

        private string ConversionDisplay =>
            _totalPi <= 0 ? "—" : $"{(double)_totalConfirmedPi / _totalPi * 100d:0.#}%";

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
                var paged = await _service.GetSalesmanRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;

                var merged = await BuildMergedRowsAsync(paged.Items);
                if (token.IsCancellationRequested) return;

                merged = ApplyLocalFilter(merged);
                merged = merged
                    .OrderByDescending(r => r.Revenue)
                    .ThenByDescending(r => r.PiCount)
                    .ThenBy(r => r.SalesmanName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _allRows = merged;
                RecomputeSummary(merged);

                TotalCount = merged.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(merged.Count / (double)PageSize));
                if (Page > TotalPages) Page = TotalPages;

                Rows = new ObservableCollection<SalesmanRow>(
                    merged.Skip((Math.Max(1, Page) - 1) * PageSize).Take(PageSize));
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"SalesmanReport LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally { IsLoading = false; }
        }

        private async Task<List<SalesmanRow>> BuildMergedRowsAsync(IEnumerable<SalesmanReportRow> analytics)
        {
            var byName = new Dictionary<string, SalesmanRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in analytics)
            {
                var key = (a.SalesmanName ?? "").Trim();
                if (key.Length == 0) key = "Unassigned";
                byName[key] = new SalesmanRow
                {
                    SalesmanName = a.SalesmanName,
                    PiCount = a.PiCount,
                    ConfirmedPiCount = a.ConfirmedPiCount,
                    ConfirmedValue = a.ConfirmedValue,
                    CustomerCount = a.CustomerCount,
                    ProjectCount = a.ProjectCount,
                    Revenue = a.Revenue
                };
            }

            if (_masterData != null)
            {
                try
                {
                    var salesmen = await _masterData.GetAllSalesmenAsync();
                    foreach (var s in salesmen ?? new List<SavedSalesman>())
                    {
                        var key = (s.Name ?? "").Trim();
                        if (key.Length == 0) continue;

                        if (!byName.TryGetValue(key, out var row))
                        {
                            row = new SalesmanRow { SalesmanName = s.Name };
                            byName[key] = row;
                        }
                        row.IsSavedSalesman = true;
                        row.Department = s.Department ?? "";
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Salesman roster merge warning: {ExceptionMessages.Deep(ex)}"); }
            }

            return byName.Values.ToList();
        }

        private List<SalesmanRow> ApplyLocalFilter(List<SalesmanRow> rows)
        {
            if (!HasSearchText) return rows;
            var q = SearchText.Trim();
            bool Match(string? s) => !string.IsNullOrEmpty(s) &&
                s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            return rows.Where(r => Match(r.SalesmanName) || Match(r.Department)).ToList();
        }

        private void RecomputeSummary(List<SalesmanRow> rows)
        {
            TotalSalesmen = rows.Count;
            TotalRevenue = rows.Sum(r => r.Revenue);
            TotalConfirmedPi = rows.Sum(r => r.ConfirmedPiCount);
            TotalPi = rows.Sum(r => r.PiCount);
            var top = rows.OrderByDescending(r => r.Revenue).FirstOrDefault();
            TopPerformer = top != null && !string.IsNullOrWhiteSpace(top.SalesmanName) ? top.SalesmanName : "—";
            _topPerformerRevenue = top?.Revenue ?? 0m;
        }

        // ── Export / Share ────────────────────────────────────────────
        protected override async Task ExportAsync(ExportFormat format)
        {
            if (_allRows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Salesman_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        protected override string ShareReportTitle => "Salesman Report";
        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (_allRows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Salesman_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            var reportRows = _allRows.Select(r => r.ToReportRow()).ToList();
            if (format == ExportFormat.Csv)
                return await ReportingExportService.ExportCsvAsync(reportRows, "Salesman Report", ReportingExportService.SalesmanHeaders, ReportingExportService.SalesmanMap, req);
            else if (format == ExportFormat.Excel)
                return await ReportingExportService.ExportExcelAsync(reportRows, "Salesman Report", ReportingExportService.SalesmanHeaders, r => ReportingExportService.SalesmanMap(r).Cast<object>().ToArray(), req);
            else
                return await ReportingExportService.ExportPdfAsync(reportRows, "Salesman Report", ReportingExportService.SalesmanHeaders, ReportingExportService.SalesmanMap, req);
        }
    }
}

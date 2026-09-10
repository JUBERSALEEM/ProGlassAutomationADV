// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/CompanyReport/CompanyReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Company Book & Receivables
//
// SHOWS (per issuing company, one row):
//   • ROSTER detail from masterdata.db (SavedCompany, the same records
//     Company Center manages and PI Step 1 selects): TRN, representative,
//     mobile, e-mail, bank, location, logo/default flag.
//   • ANALYTICS derived from live documents (ReportingDataService):
//     PI / JO / TI counts, PI value, invoiced value, OUTSTANDING.
//
// DATA SOURCING (same "both, merged" decision as the Customer report):
//   GetCompanyRowsAsync groups the ProformaInvoices DB by CompanyName, so a
//   company in the roster with no invoices would never surface. This VM
//   LEFT-JOINS the SavedCompany roster onto the analytics rows and appends
//   roster companies that have no invoices yet (zero figures) so the book is
//   complete. Join key is the trimmed, case-insensitive company name.
//
//   No new query written — the service already computes every figure;
//   export reuses ReportingExportService.CompanyHeaders/Map.
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

namespace ProGlassAutomation.ViewModels.Reports.CompanyReport
{
    /// <summary>One company row: master-data roster detail merged with live
    /// document analytics. Plain DTO, hydrated once per load.</summary>
    public sealed class CompanyRow
    {
        // ── Identity / roster (SavedCompany) ─────────────────────────
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CompanyLocation { get; set; } = "";
        public string Representative { get; set; } = "";
        public string RepresentativeMobile { get; set; } = "";
        public string CompanyEmail { get; set; } = "";
        public string BankName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public bool IsSavedCompany { get; set; }
        public bool IsDefault { get; set; }

        // ── Analytics (ReportingDataService) ─────────────────────────
        public int PiCount { get; set; }
        public int JobOrderCount { get; set; }
        public int TaxInvoiceCount { get; set; }
        public decimal PiValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal Outstanding { get; set; }

        // ── Presentation helpers ─────────────────────────────────────
        public bool HasOutstanding => Outstanding > 0m;
        public string Initials => ReportInitials.From(CompanyName);

        // Row-local UI state for the inline details expander. Plain auto-property
        // is sufficient: the Expander two-way binds to it and rows are recreated
        // on every reload, so no change notification is required for correctness.
        public bool IsExpanded { get; set; }

        public CompanyReportRow ToReportRow() => new()
        {
            CompanyName = CompanyName,
            CompanyTrn = CompanyTrn,
            PiCount = PiCount,
            JobOrderCount = JobOrderCount,
            TaxInvoiceCount = TaxInvoiceCount,
            PiValue = PiValue,
            InvoicedValue = InvoicedValue,
            Outstanding = Outstanding
        };
    }

    public class CompanyReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IMasterDataRepository? _masterData;

        private List<CompanyRow> _allRows = new();

        public CompanyReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public CompanyReportViewModel(
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

        private ObservableCollection<CompanyRow> _rows = new();
        public ObservableCollection<CompanyRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Summary KPIs ──────────────────────────────────────────────
        private int _totalCompanies;
        public int TotalCompanies { get => _totalCompanies; private set { _totalCompanies = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _savedCompanyCount;
        public int SavedCompanyCount { get => _savedCompanyCount; private set { _savedCompanyCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalOutstanding;
        public decimal TotalOutstanding { get => _totalOutstanding; private set { _totalOutstanding = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalInvoiced;
        public decimal TotalInvoiced { get => _totalInvoiced; private set { _totalInvoiced = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalPiValue;
        public decimal TotalPiValue { get => _totalPiValue; private set { _totalPiValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("COMPANIES", TotalCompanies.ToString("N0"), $"{SavedCompanyCount} on file", "AccentBrush"),
            new KpiItem("OUTSTANDING", $"AED {TotalOutstanding:N0}", "balance due across companies", "WarningBrush"),
            new KpiItem("INVOICED", $"AED {TotalInvoiced:N0}", $"of AED {TotalPiValue:N0} quoted", "SuccessBrush"),
            new KpiItem("COLLECTION", CollectionRateDisplay, "invoiced ÷ quoted value", "InfoBrush"),
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
                var paged = await _service.GetCompanyRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;

                var merged = await BuildMergedRowsAsync(paged.Items);
                if (token.IsCancellationRequested) return;

                merged = ApplyLocalFilter(merged);
                merged = merged
                    .OrderByDescending(r => r.Outstanding)
                    .ThenByDescending(r => r.PiValue)
                    .ThenBy(r => r.CompanyName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _allRows = merged;
                RecomputeSummary(merged);

                TotalCount = merged.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(merged.Count / (double)PageSize));
                if (Page > TotalPages) Page = TotalPages;

                Rows = new ObservableCollection<CompanyRow>(
                    merged.Skip((Math.Max(1, Page) - 1) * PageSize).Take(PageSize));
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"CompanyReport LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally { IsLoading = false; }
        }

        private async Task<List<CompanyRow>> BuildMergedRowsAsync(IEnumerable<CompanyReportRow> analytics)
        {
            var byName = new Dictionary<string, CompanyRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in analytics)
            {
                var key = (a.CompanyName ?? "").Trim();
                if (key.Length == 0) key = "Unknown";
                byName[key] = new CompanyRow
                {
                    CompanyName = a.CompanyName,
                    CompanyTrn = a.CompanyTrn,
                    PiCount = a.PiCount,
                    JobOrderCount = a.JobOrderCount,
                    TaxInvoiceCount = a.TaxInvoiceCount,
                    PiValue = a.PiValue,
                    InvoicedValue = a.InvoicedValue,
                    Outstanding = a.Outstanding
                };
            }

            if (_masterData != null)
            {
                try
                {
                    var companies = await _masterData.GetAllCompaniesAsync();
                    foreach (var c in companies ?? new List<SavedCompany>())
                    {
                        var key = (c.CompanyName ?? "").Trim();
                        if (key.Length == 0) continue;

                        if (!byName.TryGetValue(key, out var row))
                        {
                            row = new CompanyRow { CompanyName = c.CompanyName };
                            byName[key] = row;
                        }
                        row.IsSavedCompany = true;
                        row.IsDefault = c.IsDefault;
                        if (string.IsNullOrWhiteSpace(row.CompanyTrn)) row.CompanyTrn = c.CompanyTrn ?? "";
                        row.CompanyLocation = c.CompanyLocation ?? "";
                        row.Representative = c.Representative ?? "";
                        row.RepresentativeMobile = c.RepresentativeMobile ?? "";
                        row.CompanyEmail = c.CompanyEmail ?? "";
                        row.BankName = c.BankName ?? "";
                        row.AccountNumber = c.AccountNumber ?? "";
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Company roster merge warning: {ExceptionMessages.Deep(ex)}"); }
            }

            return byName.Values.ToList();
        }

        private List<CompanyRow> ApplyLocalFilter(List<CompanyRow> rows)
        {
            if (!HasSearchText) return rows;
            var q = SearchText.Trim();
            bool Match(string? s) => !string.IsNullOrEmpty(s) &&
                s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            return rows.Where(r =>
                Match(r.CompanyName) || Match(r.CompanyTrn) || Match(r.Representative) ||
                Match(r.CompanyEmail) || Match(r.BankName) || Match(r.CompanyLocation)).ToList();
        }

        private void RecomputeSummary(List<CompanyRow> rows)
        {
            TotalCompanies = rows.Count;
            SavedCompanyCount = rows.Count(r => r.IsSavedCompany);
            TotalOutstanding = rows.Sum(r => r.Outstanding);
            TotalInvoiced = rows.Sum(r => r.InvoicedValue);
            TotalPiValue = rows.Sum(r => r.PiValue);
        }

        // ── Export / Share ────────────────────────────────────────────
        protected override async Task ExportAsync(ExportFormat format)
        {
            if (_allRows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Company_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        protected override string ShareReportTitle => "Company Report";
        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (_allRows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Company_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            var reportRows = _allRows.Select(r => r.ToReportRow()).ToList();
            if (format == ExportFormat.Csv)
                return await ReportingExportService.ExportCsvAsync(reportRows, "Company Report", ReportingExportService.CompanyHeaders, ReportingExportService.CompanyMap, req);
            else if (format == ExportFormat.Excel)
                return await ReportingExportService.ExportExcelAsync(reportRows, "Company Report", ReportingExportService.CompanyHeaders, r => ReportingExportService.CompanyMap(r).Cast<object>().ToArray(), req);
            else
                return await ReportingExportService.ExportPdfAsync(reportRows, "Company Report", ReportingExportService.CompanyHeaders, ReportingExportService.CompanyMap, req);
        }
    }
}

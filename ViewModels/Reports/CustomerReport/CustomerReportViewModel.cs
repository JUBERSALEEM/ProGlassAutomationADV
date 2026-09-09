// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/CustomerReport/CustomerReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Customer Directory & Analytics
//
// SHOWS (per customer, one row):
//   • ROSTER detail from masterdata.db (SavedClient): TRN, attention
//     executive, contact phone, billing address, reference code, salesman.
//   • ANALYTICS derived from live documents (ReportingDataService):
//     PI / JO / TI counts, PI value, invoiced value, OUTSTANDING, qty, sqm.
//   • PROJECTS the customer is linked to (count + primary), from SavedProject.
//
// DATA SOURCING (the senior decision — "both, merged"):
//   The analytics engine groups the ProformaInvoices DB by customer name,
//   so a saved client with NO invoices would never surface. This report
//   therefore LEFT-JOINS the master-data roster onto the analytics rows and
//   ALSO appends saved clients that have no invoices yet (zero figures) so
//   the page is a complete customer directory, not just an invoice rollup.
//   The join key is the trimmed, case-insensitive customer name.
//
//   No new query was written: GetCustomerRowsAsync already computes every
//   figure (Outstanding = max(0, PI value − invoiced)); this VM only joins
//   and presents. Export reuses ReportingExportService.CustomerHeaders/Map.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Commands;
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

namespace ProGlassAutomation.ViewModels.Reports.CustomerReport
{
    /// <summary>
    /// One customer row: master-data roster detail merged with live
    /// document analytics. Plain DTO — hydrated once per load, so no change
    /// notification is required.
    /// </summary>
    public sealed class CustomerRow
    {
        // ── Identity / roster (SavedClient) ──────────────────────────
        public string CustomerName { get; set; } = "";
        public string CustomerTrn { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string AttentionExecutiveName { get; set; } = "";
        public string ClientContactPhone { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public string CustomerReferenceCode { get; set; } = "";

        /// <summary>True when this customer exists in the saved-client roster
        /// (as opposed to a name that appears only on invoices).</summary>
        public bool IsSavedClient { get; set; }

        // ── Projects (SavedProject, grouped by customer) ─────────────
        public int ProjectCount { get; set; }
        public string PrimaryProject { get; set; } = "";

        // ── Analytics (ReportingDataService) ─────────────────────────
        public int PiCount { get; set; }
        public int JoCount { get; set; }
        public int TiCount { get; set; }
        public decimal PiValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal Outstanding { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }

        // ── Presentation helpers ─────────────────────────────────────
        public bool HasOutstanding => Outstanding > 0m;
        public string Initials
        {
            get
            {
                var n = (CustomerName ?? "").Trim();
                if (n.Length == 0) return "—";
                var parts = n.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                    return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
                return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
            }
        }

        /// <summary>Projects the merged row back onto the export contract so
        /// the CSV/Excel/PDF exporters can reuse CustomerHeaders/CustomerMap.</summary>
        public CustomerReportRow ToReportRow() => new()
        {
            CustomerName = CustomerName,
            CustomerTrn = CustomerTrn,
            Salesman = Salesman,
            PiCount = PiCount,
            JoCount = JoCount,
            TiCount = TiCount,
            PiValue = PiValue,
            InvoicedValue = InvoicedValue,
            Outstanding = Outstanding,
            TotalQty = TotalQty,
            TotalSqm = TotalSqm
        };
    }

    public class CustomerReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IMasterDataRepository? _masterData;

        // Full merged set (before in-memory paging). LoadAsync rebuilds it;
        // the visible Rows collection is a page-sized slice of this list.
        private List<CustomerRow> _allRows = new();

        public CustomerReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public CustomerReportViewModel(
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

        // ── Rows (current page) ───────────────────────────────────────
        private ObservableCollection<CustomerRow> _rows = new();
        public ObservableCollection<CustomerRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Summary KPIs ──────────────────────────────────────────────
        private int _totalCustomers;
        public int TotalCustomers { get => _totalCustomers; private set { _totalCustomers = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _savedClientCount;
        public int SavedClientCount { get => _savedClientCount; private set { _savedClientCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _withOutstandingCount;
        public int WithOutstandingCount { get => _withOutstandingCount; private set { _withOutstandingCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalOutstanding;
        public decimal TotalOutstanding { get => _totalOutstanding; private set { _totalOutstanding = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalInvoiced;
        public decimal TotalInvoiced { get => _totalInvoiced; private set { _totalInvoiced = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalPiValue;
        public decimal TotalPiValue { get => _totalPiValue; private set { _totalPiValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("CUSTOMERS", TotalCustomers.ToString("N0"), $"{SavedClientCount} saved · {TotalCustomers - SavedClientCount} invoice-only", "AccentBrush"),
            new KpiItem("OUTSTANDING", $"AED {TotalOutstanding:N0}", $"{WithOutstandingCount} with balance due", "WarningBrush"),
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
                // Pull ALL analytics rows in one shot (master-data volumes are
                // modest); this VM does its own filtering, merge and paging so
                // saved clients without invoices can still be listed.
                var filter = BuildFilter();
                filter.Page = 1;
                filter.PageSize = int.MaxValue;
                var paged = await _service.GetCustomerRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;

                var merged = await BuildMergedRowsAsync(paged.Items);
                if (token.IsCancellationRequested) return;

                merged = ApplyLocalFilter(merged);
                merged = merged
                    .OrderByDescending(r => r.Outstanding)
                    .ThenByDescending(r => r.PiValue)
                    .ThenBy(r => r.CustomerName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _allRows = merged;
                RecomputeSummary(merged);

                TotalCount = merged.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(merged.Count / (double)PageSize));
                if (Page > TotalPages) Page = TotalPages;

                Rows = new ObservableCollection<CustomerRow>(
                    merged.Skip((Math.Max(1, Page) - 1) * PageSize).Take(PageSize));
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"CustomerReport LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally { IsLoading = false; }
        }

        /// <summary>
        /// Left-joins the master-data roster (SavedClient + SavedProject) onto
        /// the invoice-derived analytics rows, then appends saved clients that
        /// have no invoices so the directory is complete.
        /// </summary>
        private async Task<List<CustomerRow>> BuildMergedRowsAsync(IEnumerable<CustomerReportRow> analytics)
        {
            var byName = new Dictionary<string, CustomerRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in analytics)
            {
                var key = (a.CustomerName ?? "").Trim();
                if (key.Length == 0) key = "Unknown";
                byName[key] = new CustomerRow
                {
                    CustomerName = a.CustomerName,
                    CustomerTrn = a.CustomerTrn,
                    Salesman = a.Salesman,
                    PiCount = a.PiCount,
                    JoCount = a.JoCount,
                    TiCount = a.TiCount,
                    PiValue = a.PiValue,
                    InvoicedValue = a.InvoicedValue,
                    Outstanding = a.Outstanding,
                    TotalQty = a.TotalQty,
                    TotalSqm = a.TotalSqm
                };
            }

            if (_masterData != null)
            {
                // Roster detail — attention, TRN, contact, billing, salesman.
                try
                {
                    var clients = await _masterData.GetAllClientsAsync();
                    foreach (var c in clients ?? new List<SavedClient>())
                    {
                        var key = (c.CustomerName ?? "").Trim();
                        if (key.Length == 0) continue;

                        if (!byName.TryGetValue(key, out var row))
                        {
                            // Saved client with no invoices yet — still list it.
                            row = new CustomerRow { CustomerName = c.CustomerName };
                            byName[key] = row;
                        }

                        row.IsSavedClient = true;
                        if (string.IsNullOrWhiteSpace(row.CustomerTrn)) row.CustomerTrn = c.CustomerTrn ?? "";
                        if (string.IsNullOrWhiteSpace(row.Salesman)) row.Salesman = c.SalesRepresentative ?? "";
                        row.AttentionExecutiveName = c.AttentionExecutiveName ?? "";
                        row.ClientContactPhone = c.ClientContactPhone ?? "";
                        row.BillingAddress = c.BillingAddress ?? "";
                        row.CustomerReferenceCode = c.CustomerReferenceCode ?? "";
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Customer roster merge warning: {ExceptionMessages.Deep(ex)}"); }

                // Projects — count + primary name per customer.
                try
                {
                    var projects = await _masterData.GetAllProjectsAsync();
                    foreach (var g in (projects ?? new List<SavedProject>())
                        .Where(p => !string.IsNullOrWhiteSpace(p.CustomerName))
                        .GroupBy(p => p.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        if (!byName.TryGetValue(g.Key, out var row)) continue;
                        row.ProjectCount = g.Count();
                        row.PrimaryProject = g
                            .OrderByDescending(p => p.UsageCount)
                            .Select(p => p.ProjectIdentifierName)
                            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Customer project merge warning: {ExceptionMessages.Deep(ex)}"); }
            }

            return byName.Values.ToList();
        }

        /// <summary>
        /// Applies the free-text search across roster + analytics fields. The
        /// dropdown/date filters already narrowed the analytics query; this is
        /// the extra pass over the merged (roster-inclusive) set.
        /// </summary>
        private List<CustomerRow> ApplyLocalFilter(List<CustomerRow> rows)
        {
            if (!HasSearchText) return rows;
            var q = SearchText.Trim();
            bool Match(string? s) => !string.IsNullOrEmpty(s) &&
                s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            return rows.Where(r =>
                Match(r.CustomerName) || Match(r.Salesman) || Match(r.CustomerTrn) ||
                Match(r.AttentionExecutiveName) || Match(r.ClientContactPhone) ||
                Match(r.CustomerReferenceCode) || Match(r.PrimaryProject)).ToList();
        }

        private void RecomputeSummary(List<CustomerRow> rows)
        {
            TotalCustomers = rows.Count;
            SavedClientCount = rows.Count(r => r.IsSavedClient);
            WithOutstandingCount = rows.Count(r => r.Outstanding > 0m);
            TotalOutstanding = rows.Sum(r => r.Outstanding);
            TotalInvoiced = rows.Sum(r => r.InvoicedValue);
            TotalPiValue = rows.Sum(r => r.PiValue);
        }

        // ── Export / Share (reuse the shared Customer contract) ───────
        protected override async Task ExportAsync(ExportFormat format)
        {
            if (_allRows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Customer_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        protected override string ShareReportTitle => "Customer Report";
        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (_allRows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Customer_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            var reportRows = _allRows.Select(r => r.ToReportRow()).ToList();
            if (format == ExportFormat.Csv)
                return await ReportingExportService.ExportCsvAsync(reportRows, "Customer Report", ReportingExportService.CustomerHeaders, ReportingExportService.CustomerMap, req);
            else if (format == ExportFormat.Excel)
                return await ReportingExportService.ExportExcelAsync(reportRows, "Customer Report", ReportingExportService.CustomerHeaders, r => ReportingExportService.CustomerMap(r).Cast<object>().ToArray(), req);
            else
                return await ReportingExportService.ExportPdfAsync(reportRows, "Customer Report", ReportingExportService.CustomerHeaders, ReportingExportService.CustomerMap, req);
        }
    }
}

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Reports.DeliveryReport
{
    /// <summary>
    /// ROW-LEVEL Delivery Note report — SINGLE unified grid. One row per
    /// exact PI specification row (PI → Specification → Sr No) with Total /
    /// Delivered / Balance quantities reconciled per row from the
    /// DeliveryNoteItems table. The related Delivery Notes are revealed
    /// INLINE (expander) inside each row — no split list + side panel.
    ///
    /// Filters are DATA-DRIVEN: PI number, Specification, Glass Ref,
    /// Salesman, Project, Customer and delivery Status are all populated
    /// from the distinct values actually stored (never hardcoded).
    /// </summary>
    public class DeliveryReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;

        public DeliveryReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        /// <summary>Sentinel shown at the top of every filter dropdown.</summary>
        public const string AllFilter = "All";

        public DeliveryReportViewModel(IDialogService? dialogs, IReportingDataService? service = null, IDatabaseServices? database = null) : base(service ?? new ReportingDataService(), database)
        {
            _dialogs = dialogs!;
            InitCommands();
            SetSortOptions(new[] { "Proforma", "Customer", "Project", "Ordered", "Status", "Balance" }, "Proforma", defaultDescending: false);

            SelectedStatus = AllStatusesOption;
            SetStatusOptions(new[] { "Not delivered", "Partially delivered", "Fully delivered" });

            ClearFilterCommand = new RelayCommand(_ =>
            {
                PiFilter = AllFilter;
                SpecificationFilter = AllFilter;
                GlassRefFilter = AllFilter;
                GlassTypeFilter = AllFilter;
                SalesmanFilter = AllFilter;
                ProjectFilter = AllFilter;
                CustomerFilter = AllFilter;
                SelectedStatus = AllStatusesOption;
                ClearFilters(); // resets dates/search and triggers a reload
            });

            CloseDetailCommand = new RelayCommand(_ => SelectedRow = null);
            SelectRowCommand = new RelayCommand(p =>
            {
                if (p is DeliveryReportRow row)
                    SelectedRow = ReferenceEquals(SelectedRow, row) ? null : row;
            });

            // Open the Job Order that owns the selected delivery row — same
            // "open the original document" affordance the PI / JO / TI reports
            // already provide. The row carries the JO reference (string); the
            // hub resolves it to a DB id and navigates.
            OpenJobOrderCommand = new RelayCommand(p =>
            {
                if (p is DeliveryReportRow r && !string.IsNullOrWhiteSpace(r.JobOrderId))
                    OpenJobOrderRequested?.Invoke(r.JobOrderId);
            });

            _ = LoadDeliveryFilterOptionsAsync();
            _ = LoadAsync();
        }

        /// <summary>Raised with the row's JobOrderId when the user clicks
        /// "Open Job Order". Wired by the hub to MainViewModel.</summary>
        public Action<string>? OpenJobOrderRequested { get; set; }
        public ICommand OpenJobOrderCommand { get; private set; } = null!;
        public ICommand SelectRowCommand { get; private set; } = null!;

        // ── Filters (data-driven dropdowns; "All" = no filter) ──────────

        private string _piFilter = AllFilter;
        public string PiFilter
        {
            get => _piFilter;
            set { if (_piFilter == value) return; _piFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _specificationFilter = AllFilter;
        public string SpecificationFilter
        {
            get => _specificationFilter;
            set { if (_specificationFilter == value) return; _specificationFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _glassRefFilter = AllFilter;
        public string GlassRefFilter
        {
            get => _glassRefFilter;
            set { if (_glassRefFilter == value) return; _glassRefFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _salesmanFilter = AllFilter;
        public string SalesmanFilter
        {
            get => _salesmanFilter;
            set { if (_salesmanFilter == value) return; _salesmanFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _projectFilter = AllFilter;
        public string ProjectFilter
        {
            get => _projectFilter;
            set { if (_projectFilter == value) return; _projectFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _customerFilter = AllFilter;
        public string CustomerFilter
        {
            get => _customerFilter;
            set { if (_customerFilter == value) return; _customerFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _glassTypeFilter = AllFilter;
        public string GlassTypeFilter
        {
            get => _glassTypeFilter;
            set { if (_glassTypeFilter == value) return; _glassTypeFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        // ── Option lists (distinct stored values, "All" first) ──────────

        private List<string> _piOptions = new() { AllFilter };
        public List<string> PiOptions { get => _piOptions; private set { _piOptions = value; OnPropertyChanged(); } }

        private List<string> _specificationOptions = new() { AllFilter };
        public List<string> SpecificationOptions { get => _specificationOptions; private set { _specificationOptions = value; OnPropertyChanged(); } }

        private List<string> _glassRefOptions = new() { AllFilter };
        public List<string> GlassRefOptions { get => _glassRefOptions; private set { _glassRefOptions = value; OnPropertyChanged(); } }

        private List<string> _salesmanOptions = new() { AllFilter };
        public List<string> DeliverySalesmanOptions { get => _salesmanOptions; private set { _salesmanOptions = value; OnPropertyChanged(); } }

        private List<string> _projectOptions = new() { AllFilter };
        public List<string> DeliveryProjectOptions { get => _projectOptions; private set { _projectOptions = value; OnPropertyChanged(); } }

        private List<string> _customerOptions = new() { AllFilter };
        public List<string> DeliveryCustomerOptions { get => _customerOptions; private set { _customerOptions = value; OnPropertyChanged(); } }

        private List<string> _glassTypeOptions = new() { AllFilter };
        public List<string> GlassTypeOptions { get => _glassTypeOptions; private set { _glassTypeOptions = value; OnPropertyChanged(); } }

        /// <summary>Prepend the "All" sentinel to a distinct-value list.</summary>
        private static List<string> WithAll(IEnumerable<string> distinct)
        {
            var list = new List<string> { AllFilter };
            list.AddRange((distinct ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)));
            return list;
        }

        private async Task LoadDeliveryFilterOptionsAsync()
        {
            try
            {
                PiOptions = WithAll(await _service.GetDeliveryPiOptionsAsync());
                SpecificationOptions = WithAll(await _service.GetDeliverySpecificationOptionsAsync());
                GlassRefOptions = WithAll(await _service.GetDeliveryGlassRefOptionsAsync());
                DeliverySalesmanOptions = WithAll(await _service.GetDeliverySalesmanOptionsAsync());
                DeliveryProjectOptions = WithAll(await _service.GetDeliveryProjectOptionsAsync());
                DeliveryCustomerOptions = WithAll(await _service.GetCustomerOptionsAsync());
                GlassTypeOptions = WithAll(await _service.GetGlassTypeOptionsAsync());
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Warning($"Delivery filter options warning: {ExceptionMessages.Deep(ex)}");
            }
        }

        /// <summary>Maps the "All" sentinel back to no filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            return new ReportingFilter
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                ProformaId = NotAll(PiFilter),
                Specification = NotAll(SpecificationFilter),
                GlassRef = NotAll(GlassRefFilter),
                GlassType = NotAll(GlassTypeFilter),
                Salesman = NotAll(SalesmanFilter),
                Project = NotAll(ProjectFilter),
                Customer = NotAll(CustomerFilter),
                Status = NotAll(SelectedStatus),
                Page = Page,
                PageSize = PageSize
            };
        }

        private static string? NotAll(string value) =>
            string.IsNullOrWhiteSpace(value) || string.Equals(value, AllFilter, StringComparison.OrdinalIgnoreCase)
                ? null : value.Trim();

        // ── Summary ──────────────────────────────────────────────────────

        private DeliveryReportSummary _summary = new();
        public DeliveryReportSummary Summary
        {
            get => _summary;
            set
            {
                _summary = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SummaryCards));
                OnPropertyChanged(nameof(CompletionText));
                OnPropertyChanged(nameof(OrderedText));
                OnPropertyChanged(nameof(DeliveredText));
                OnPropertyChanged(nameof(BalanceText));
            }
        }

        // ── AGEING ANALYTICS (slice 2) ───────────────────────────────────
        // DeliveryReportSummary already answers "how many gone / how many
        // pending". This answers how OLD the pending work is and who it is
        // for -- the part that turns a count into a call list.
        private DeliveryAnalyticsSummary _analytics = new();
        public DeliveryAnalyticsSummary Analytics
        {
            get => _analytics;
            set
            {
                _analytics = value ?? new DeliveryAnalyticsSummary();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnalytics));
                OnPropertyChanged(nameof(CriticalPendingText));
                OnPropertyChanged(nameof(HasCriticalPending));
            }
        }

        public bool HasAnalytics => _analytics is { HasData: true };

        /// <summary>
        /// The single number a dispatch manager should see first.
        /// </summary>
        public string CriticalPendingText =>
            $"{_analytics.CriticalPendingQty:N0} pcs over 30 days";

        public bool HasCriticalPending => _analytics.CriticalPendingQty > 0;

        public string CompletionText =>
            $"{Summary.CompletionPct:0.0}% delivered ({Summary.TotalDeliveredQty:N0}/{Summary.TotalOrderedQty:N0} pcs)";

        public string OrderedText => $"{Summary.TotalOrderedQty:N0} pieces ordered";

        public string DeliveredText =>
            $"{Summary.TotalDeliveredQty:N0} pieces · {Summary.CompletionPct:0.0}%";

        public string BalanceText => $"{Summary.TotalRemainingQty:N0} pieces remaining";

        /// <summary>
        /// Quantity-based KPI cards — the top panel reflects TOTAL / DELIVERED
        /// / BALANCE pieces (and completion %) for the CURRENT filter set, not
        /// a count of rows. Every value is recomputed by GetDeliverySummaryAsync
        /// from the filtered DeliveryNoteItems, so it updates with the filters.
        /// </summary>
        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TOTAL QTY", Summary.TotalOrderedQty.ToString("N0"), OrderedText, "AccentBrush"),
            new KpiItem("DELIVERED", Summary.TotalDeliveredQty.ToString("N0"), DeliveredText, "SuccessBrush"),
            new KpiItem("BALANCE", Summary.TotalRemainingQty.ToString("N0"), BalanceText, "WarningBrush"),
            new KpiItem("COMPLETION", $"{Summary.CompletionPct:0.0}%", CompletionText, "InfoBrush"),
        };

        private ObservableCollection<DeliveryReportRow> _rows = new();
        public ObservableCollection<DeliveryReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Row selection → right slide-in panel (Sheet Store pattern) ───
        private DeliveryReportRow? _selectedRow;
        public DeliveryReportRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (ReferenceEquals(_selectedRow, value)) return;

                if (_selectedRow != null) _selectedRow.IsSelected = false;
                _selectedRow = value;
                if (_selectedRow != null) _selectedRow.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedRow));
                OnPropertyChanged(nameof(SidebarSubtitle));
                if (value != null)
                    _ = LoadRowDeliveriesAsync(value);
            }
        }
        public bool HasSelectedRow => SelectedRow != null;
        public string SidebarSubtitle => SelectedRow == null
            ? ""
            : string.IsNullOrWhiteSpace(SelectedRow.GlassRef)
                ? $"Sr {SelectedRow.SerialNumber}"
                : $"{SelectedRow.GlassRef} · Sr {SelectedRow.SerialNumber}";

        public ICommand CloseDetailCommand { get; private set; } = null!;

        /// <summary>
        /// Lazy-loads the related Delivery Notes for one row and fills the
        /// ObservableCollection in place (so the side panel refreshes without
        /// re-creating the row).
        /// </summary>
        public async Task LoadRowDeliveriesAsync(DeliveryReportRow row)
        {
            if (row == null || row.HasLoadedDeliveries) return;

            try
            {
                var deliveries = await _service.GetRowDeliveriesAsync(
                    row.JobOrderId, row.SpecGroupNumber, row.SerialNumber);

                row.Deliveries.Clear();
                foreach (var d in deliveries ?? new List<DeliveryNoteDelivery>())
                    row.Deliveries.Add(d);
                row.HasLoadedDeliveries = true;
            }
            catch (Exception ex)
            {
                row.Deliveries.Clear();
                row.HasLoadedDeliveries = true;
                Services.Logging.AppLogService.Warning($"Could not load delivery history: {ExceptionMessages.Deep(ex)}");
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

                // Enhancement, not a gatekeeper — see JoReportViewModel.
                Analytics = await _service.GetDeliveryAnalyticsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            { HasError = true; ErrorMessage = ExceptionMessages.Deep(ex); }
            finally { IsLoading = false; }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Delivery_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled) _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        // ── Sharing Center — the SAME core above, staged to temp ──
        protected override string ShareReportTitle => "Delivery Report";

        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (Rows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Delivery_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            if (format == ExportFormat.Csv) return await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMap, req);
            else if (format == ExportFormat.Excel) return await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMapExcel, req);
            else return await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMap, req);
        }
    }
}

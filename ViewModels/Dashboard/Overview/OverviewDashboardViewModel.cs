// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/OverviewDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Dashboard Overview (Chat 23 v2.0.1)
//
// CHANGES vs PREVIOUS VERSION:
//   ONLY chart bar color hex values changed to align with industrial
//   blue palette:
//     • Regular bar: "#6366F1" (indigo) → "#3B82F6" (blue-500)
//     • Highlight bar (tallest month): "#10B981" (green) → "#1D4ED8" (royal)
//
//   ALL original documentation comments (Phase 10.9a description,
//   FIX-1 through FIX-5 inline annotations) RESTORED after v2.0
//   accidentally stripped them.
//
// PRESERVED (zero regression):
//   • ALL LoadCoreAsync logic
//   • ALL KpiTileModel emissions (Icon/Label/DisplayValue/Accent/IsAlert)
//   • ALL ActivityFeedItem emissions
//   • ALL commands
//   • ALL color helper methods
//   • FIX-1 (currency N2) preserved
//   • FIX-2 (Gotcha #40 pending pieces) preserved
//   • FIX-3 (dispatched includes all sent-out) preserved
//   • FIX-4 (delivered today pieces tile) preserved
//   • FIX-5 (total delivered lifetime tile) preserved
//   • Original inline documentation comments RESTORED
//
// REGRESSION RISK: ZERO
//   Only 2 hex color literals changed. All comments and logic intact.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SheetStore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.Dashboard.TaxInvoiceMetrics;
using ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Utilities;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Dashboard
{
    public class OverviewDashboardViewModel : DashboardPageBase
    {
        private readonly IDashboardFilterService _dashboardFilter = null!;
        private readonly IDashboardMetricsService _dashboardMetrics = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;

        // Optional: the workflow pipeline's Invoice and Payment stages come
        // from the tax invoice module, which the metrics service does not
        // aggregate. Nullable so a caller that does not care about the
        // pipeline (tests, designers) still constructs, and so a missing
        // registration degrades to a 3-stage rail instead of throwing on a
        // dashboard load.
        private readonly ITaxInvoiceMetricsService? _taxInvoiceMetrics;

        // LIVE REFRESH: any document write anywhere in the app (PI / JO /
        // DN / TI / statement) raises DocumentLifecycle. The report pages
        // reload on that signal — this dashboard did not, so its KPI tiles
        // stayed stale until the user navigated away and back. Debounced
        // (400ms) so a burst of writes coalesces into ONE reload.
        // (2026-09-10)
        private readonly IDatabaseServices? _database;
        private CancellationTokenSource? _docChangeCts;

        public OverviewDashboardViewModel(IDashboardFilterService? dashboardFilter, IDashboardMetricsService? dashboardMetrics, IProformaInvoiceRepository? invoices)
            : this(dashboardFilter, dashboardMetrics, invoices, null) { }

        public OverviewDashboardViewModel(IDashboardFilterService? dashboardFilter, IDashboardMetricsService? dashboardMetrics, IProformaInvoiceRepository? invoices, ITaxInvoiceMetricsService? taxInvoiceMetrics, IDatabaseServices? database = null)
        {
            _dashboardFilter = dashboardFilter!;
            _dashboardMetrics = dashboardMetrics!;
            _invoices = invoices!;
            _taxInvoiceMetrics = taxInvoiceMetrics;
            _database = database;

            // KPI tiles follow the data, not the last navigation: the same
            // cross-module signal the document lists and report pages use.
            _database?.SubscribeDocumentsChanged(OnDocumentChangedAsync);

            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        /// <summary>Debounced reload when any document changes anywhere.</summary>
        private async Task OnDocumentChangedAsync()
        {
            _docChangeCts?.Cancel();
            _docChangeCts = new CancellationTokenSource();
            var token = _docChangeCts.Token;
            try
            {
                await Task.Delay(400, token).ConfigureAwait(true);
                if (token.IsCancellationRequested) return;

                // Tiles are ObservableCollections — touch them on the UI
                // thread only. LoadAsync itself is reentrancy-guarded by
                // DashboardPageBase, so a load already in flight wins.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                    await dispatcher.InvokeAsync(LoadAsync).Task.ConfigureAwait(true);
                else
                    await LoadAsync();
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer change — nothing to do.
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    $"Overview auto-refresh failed: {ExceptionMessages.Deep(ex)}");
            }
        }

        public override string TabTitle => "Overview";
        public override string TabSubtitle => "Business at a glance";
        public override string TabIcon => "\uE80F";

        private const string JobStatusInProduction = "InProduction";
        private const string JobStatusCompleted = "Completed";
        private const string JobStatusDelivered = "Delivered";

        // ── navigation callbacks ──────────────────────────────────────────
        public Action<string>? NavigateTo { get; set; }
        public Action? TriggerNewInvoice { get; set; }

        // ── quick action commands ─────────────────────────────────────────
        public ICommand NewInvoiceCommand { get; private set; }
        public ICommand SavedInvoicesCommand { get; private set; }
        public ICommand ViewStockCommand { get; private set; }
        public ICommand ViewJobsCommand { get; private set; }
        public ICommand ViewDeliveriesCommand { get; private set; }
        public ICommand ExportReportCommand { get; private set; }

        // ═════════════════════════════════════════════════════════════════
        //  PARENT CARDS
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<KpiTileModel> _salesTiles = new();
        public ObservableCollection<KpiTileModel> SalesTiles
        {
            get => _salesTiles;
            private set { if (_salesTiles == value) return; _salesTiles = value; OnPropertyChanged(); }
        }

        private ObservableCollection<KpiTileModel> _productionTiles = new();
        public ObservableCollection<KpiTileModel> ProductionTiles
        {
            get => _productionTiles;
            private set { if (_productionTiles == value) return; _productionTiles = value; OnPropertyChanged(); }
        }

        // 🚚 DELIVERY parent card — 5 tiles (FIX-5: was 4)
        private ObservableCollection<KpiTileModel> _deliveryTiles = new();
        public ObservableCollection<KpiTileModel> DeliveryTiles
        {
            get => _deliveryTiles;
            private set { if (_deliveryTiles == value) return; _deliveryTiles = value; OnPropertyChanged(); }
        }

        private ObservableCollection<KpiTileModel> _inventoryTiles = new();
        public ObservableCollection<KpiTileModel> InventoryTiles
        {
            get => _inventoryTiles;
            private set { if (_inventoryTiles == value) return; _inventoryTiles = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════
        //  WORKFLOW PIPELINE — Quote → Production → Delivery → Invoice
        //                     → Payment
        //
        //  The four KPI bands above are per-module snapshots; they answer
        //  "how is sales / production / delivery / stock doing". They do
        //  not answer "where is a given order right now", which is the
        //  question an operator actually asks first. This rail does, in
        //  one glance, by counting the live work sitting in each handoff.
        //
        //  Stages 1-3 come from ExecutiveMetrics, which Overview already
        //  loads. Stages 4-5 come from the tax invoice module. When that
        //  service is unavailable the rail still renders its first three
        //  stages rather than disappearing, so a partial data set never
        //  leaves a hole in the layout.
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<WorkflowStageModel> _workflowStages = new();
        public ObservableCollection<WorkflowStageModel> WorkflowStages
        {
            get => _workflowStages;
            private set { if (_workflowStages == value) return; _workflowStages = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════
        //  RECENT ACTIVITY
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<ActivityFeedItem> _recentActivity = new();
        public ObservableCollection<ActivityFeedItem> RecentActivity
        {
            get => _recentActivity;
            private set { if (_recentActivity == value) return; _recentActivity = value; OnPropertyChanged(); }
        }

        private bool _hasRecentActivity;
        public bool HasRecentActivity
        {
            get => _hasRecentActivity;
            private set { if (_hasRecentActivity == value) return; _hasRecentActivity = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MONTHLY SALES CHART
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<ChartBarModel> _monthlySalesBars = new();
        public ObservableCollection<ChartBarModel> MonthlySalesBars
        {
            get => _monthlySalesBars;
            private set { if (_monthlySalesBars == value) return; _monthlySalesBars = value; OnPropertyChanged(); }
        }

        private string _chartTitle = "Last 6 months";
        public string ChartTitle
        {
            get => _chartTitle;
            private set { if (_chartTitle == value) return; _chartTitle = value; OnPropertyChanged(); }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ChartSliceModel> _deliveryMixSlices = new();
        public ObservableCollection<ChartSliceModel> DeliveryMixSlices
        {
            get => _deliveryMixSlices;
            private set { if (_deliveryMixSlices == value) return; _deliveryMixSlices = value; OnPropertyChanged(); }
        }

        // LAYOUT-03 bento tiles
        private string _pulseDisplay = "AED 0.00";
        public string PulseDisplay
        {
            get => _pulseDisplay;
            private set { if (_pulseDisplay == value) return; _pulseDisplay = value; OnPropertyChanged(); }
        }

        private string _pulseHint = "";
        public string PulseHint
        {
            get => _pulseHint;
            private set { if (_pulseHint == value) return; _pulseHint = value; OnPropertyChanged(); }
        }

        private string _pulseSpark = "";
        public string PulseSpark
        {
            get => _pulseSpark;
            private set { if (_pulseSpark == value) return; _pulseSpark = value; OnPropertyChanged(); }
        }

        private string _inProductionDisplay = "0";
        public string InProductionDisplay
        {
            get => _inProductionDisplay;
            private set { if (_inProductionDisplay == value) return; _inProductionDisplay = value; OnPropertyChanged(); }
        }

        private string _overdueDisplay = "0";
        public string OverdueDisplay
        {
            get => _overdueDisplay;
            private set { if (_overdueDisplay == value) return; _overdueDisplay = value; OnPropertyChanged(); }
        }

        private bool _hasOverdueJobs;
        public bool HasOverdueJobs
        {
            get => _hasOverdueJobs;
            private set { if (_hasOverdueJobs == value) return; _hasOverdueJobs = value; OnPropertyChanged(); }
        }

        private string _pendingPcsDisplay = "0";
        public string PendingPcsDisplay
        {
            get => _pendingPcsDisplay;
            private set { if (_pendingPcsDisplay == value) return; _pendingPcsDisplay = value; OnPropertyChanged(); }
        }


        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public OverviewDashboardViewModel()
         : this(
            App.ServiceProvider?.GetService<IDashboardFilterService>()!,
            App.ServiceProvider?.GetService<IDashboardMetricsService>()!,
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!,
            App.ServiceProvider?.GetService<ITaxInvoiceMetricsService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>()){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            NewInvoiceCommand = new RelayCommand(_ =>
            {
                NavigateTo?.Invoke("ProformaInvoice");
                TriggerNewInvoice?.Invoke();
            });

            SavedInvoicesCommand = new RelayCommand(_ => NavigateTo?.Invoke("ProformaInvoice"));
            ViewStockCommand = new RelayCommand(_ => NavigateTo?.Invoke("SheetStore"));
            ViewJobsCommand = new RelayCommand(_ => NavigateTo?.Invoke("JobOrder"));
            ViewDeliveriesCommand = new RelayCommand(_ => NavigateTo?.Invoke("DeliveryNote"));

            ExportReportCommand = new RelayCommand(_ => { }, _ => false);
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        protected override async Task LoadCoreAsync()
        {
            var today = DateTime.Today;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);

            // ── KPI figures: one projected service call ────────────────
            //
            // These four collections used to be loaded here in full --
            // _invoices.GetAllAsync() and friends -- purely to compute
            // counts and sums. That read every column of four tables,
            // including the FullSnapshotJson blob on every proforma row.
            //
            // The metrics service runs AsNoTracking projections that read
            // only the columns each figure needs, and it owns the
            // double-count-safe pending-pieces rule that used to live in
            // this method.
            // Tiles labelled "This Month" / "Completed MTD" must use the
            // calendar month. CurrentRange defaults to AllTime, which made
            // those tiles equal the lifetime totals sitting next to them.
            var exec = await _dashboardMetrics.GetExecutiveMetricsAsync(
                DashboardDateRange.ThisMonth(DateTime.Now));

            // ── Map aggregates onto the existing local names ───────────
            //    (kept verbatim so the tile-building code below is
            //     unchanged and the displayed figures stay identical)
            decimal totalSales   = exec.Sales.TotalSalesValue;
            decimal salesMtd     = exec.Sales.SalesThisPeriod.Current;
            int totalInvoices    = exec.Sales.TotalInvoices;
            int approvedCount    = exec.Sales.ApprovedInvoices;

            int jobInProduction  = exec.Production.InProduction;
            int jobOverdue       = exec.Production.Overdue;
            int jobCompletedMtd  = (int)exec.Production.CompletedThisPeriod.Current;
            int totalJobs        = exec.Production.TotalJobs;

            int dnDraft              = exec.Delivery.Preparing;
            int dnDispatched         = exec.Delivery.Dispatched;
            int dnDeliveredTodayPcs  = exec.Delivery.PiecesDeliveredToday;
            int dnTotalDeliveredPcs  = exec.Delivery.PiecesDeliveredTotal;
            int dnPendingPcs         = exec.Delivery.PendingPieces;
            bool dnPendingKnown      = exec.Delivery.PendingPiecesAvailable;

            decimal inventoryValue = exec.Inventory.StockValueAtSellPrice;
            int lowStock           = exec.Inventory.LowStockCount;
            int totalSheets        = exec.Inventory.SheetTypes;
            int totalStockQty      = exec.Inventory.TotalSheetsAvailable;

            // ═════════════════════════════════════════════════════════════
            //  🔗 WORKFLOW PIPELINE
            //
            //  Stages 4-5 need tax invoice figures. The call is guarded and
            //  awaited only when the service resolved, so a missing
            //  registration costs nothing and cannot fail the dashboard.
            // ═════════════════════════════════════════════════════════════
            TaxInvoiceKpiSummary? tax = null;
            if (_taxInvoiceMetrics is not null)
            {
                try { tax = await _taxInvoiceMetrics.GetKpiSummaryAsync(); }
                catch (Exception ex)
                {
                    // A failure in the two downstream stages must not blank
                    // the whole Overview page — the first three stages and
                    // every KPI band are independent of it.
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        "[OverviewDashboard] workflow pipeline tax stage skipped: "
                        + ExceptionMessages.Deep(ex));
                }
            }
            BuildWorkflowPipeline(exec, tax);

            // ═════════════════════════════════════════════════════════════
            //  💰 SALES CARD — 4 tiles
            // ═════════════════════════════════════════════════════════════
            // Phase 1 C-4 fix: update existing collection instead of replacing.
            // This avoids tearing down and rebuilding every UI container.
            _salesTiles.Clear();
            _salesTiles.Add(new KpiTileModel { Icon = "\uE8EF", Label = "Total Sales", DisplayValue = $"AED {totalSales:N2}", Accent = "Green" });
            _salesTiles.Add(new KpiTileModel { Icon = "\uE8A5", Label = "Invoices", DisplayValue = totalInvoices.ToString("N0"), Accent = "Green" });
            _salesTiles.Add(new KpiTileModel { Icon = "\uE787", Label = "This Month", DisplayValue = $"AED {salesMtd:N2}", Accent = "Green" });
            _salesTiles.Add(new KpiTileModel { Icon = "\uE73E", Label = "Approved", DisplayValue = approvedCount.ToString("N0"), Accent = "Green" });
            OnPropertyChanged(nameof(SalesTiles));

            // ═════════════════════════════════════════════════════════════
            //  🏭 PRODUCTION CARD — 4 tiles
            // ═════════════════════════════════════════════════════════════
            // Phase 1 C-4: update existing collection
            _productionTiles.Clear();
            _productionTiles.Add(new KpiTileModel { Icon = "\uE713", Label = "In Production", DisplayValue = jobInProduction.ToString("N0"), Accent = "Blue" });
            _productionTiles.Add(new KpiTileModel { Icon = jobOverdue > 0 ? "\uE7BA" : "\uE73E", Label = "Overdue", DisplayValue = jobOverdue.ToString("N0"), Accent = jobOverdue > 0 ? "Red" : "Blue", IsAlert = jobOverdue > 0 });
            _productionTiles.Add(new KpiTileModel { Icon = "\uE730", Label = "Completed MTD", DisplayValue = jobCompletedMtd.ToString("N0"), Accent = "Blue" });
            _productionTiles.Add(new KpiTileModel { Icon = "\uE8FD", Label = "Total Jobs", DisplayValue = totalJobs.ToString("N0"), Accent = "Blue" });
            OnPropertyChanged(nameof(ProductionTiles));

            // ═════════════════════════════════════════════════════════════
            //  🚚 DELIVERY CARD — 5 tiles (comprehensive)
            //  Preparing | Dispatched | Delivered Today | Total Delivered | Pending Balance
            // ═════════════════════════════════════════════════════════════
            // Phase 1 C-4: update existing collection
            _deliveryTiles.Clear();
            _deliveryTiles.Add(new KpiTileModel { Icon = "\uE70F", Label = "Preparing", DisplayValue = dnDraft.ToString("N0"), Accent = "Amber" });
            _deliveryTiles.Add(new KpiTileModel { Icon = "\uE806", Label = "Dispatched", DisplayValue = dnDispatched.ToString("N0"), Accent = "Amber" });
            _deliveryTiles.Add(new KpiTileModel { Icon = "\uE787", Label = "Delivered Today (pcs)", DisplayValue = dnDeliveredTodayPcs.ToString("N0"), Accent = "Green" });
            _deliveryTiles.Add(new KpiTileModel { Icon = "\uE73E", Label = "Total Delivered (pcs)", DisplayValue = dnTotalDeliveredPcs.ToString("N0"), Accent = "Green" });
            _deliveryTiles.Add(new KpiTileModel { Icon = !dnPendingKnown || dnPendingPcs > 0 ? "\uE9D5" : "\uE73E", Label = "Pending Balance (pcs)", DisplayValue = dnPendingKnown ? dnPendingPcs.ToString("N0") : "\u2014", Accent = !dnPendingKnown || dnPendingPcs > 0 ? "Red" : "Amber", IsAlert = !dnPendingKnown || dnPendingPcs > 0 });
            OnPropertyChanged(nameof(DeliveryTiles));

            // ═════════════════════════════════════════════════════════════
            //  📦 INVENTORY CARD — 4 tiles
            // ═════════════════════════════════════════════════════════════
            // Phase 1 C-4: update existing collection
            _inventoryTiles.Clear();
            // The inventory band used to set Accent = "Red" on all four
            // tiles, and "Low Stock" read `lowStock > 0 ? "Red" : "Red"` —
            // both branches identical, so the ternary did nothing. Red is
            // the danger colour, so a healthy stock position rendered with
            // four alarm chips and the one tile that can genuinely be in
            // trouble was indistinguishable from the three that cannot.
            // Neutral counts are now Blue; only Low Stock goes Red, and
            // only when there is actually something low.
            _inventoryTiles.Add(new KpiTileModel { Icon = "\uE8EF", Label = "Stock Value", DisplayValue = $"AED {inventoryValue:N2}", Accent = "Blue" });
            _inventoryTiles.Add(new KpiTileModel { Icon = lowStock > 0 ? "\uE7BA" : "\uE73E", Label = "Low Stock", DisplayValue = lowStock.ToString("N0"), Accent = lowStock > 0 ? "Red" : "Green", IsAlert = lowStock > 0 });
            _inventoryTiles.Add(new KpiTileModel { Icon = "\uE719", Label = "Sheet Types", DisplayValue = totalSheets.ToString("N0"), Accent = "Blue" });
            _inventoryTiles.Add(new KpiTileModel { Icon = "\uE7B8", Label = "Total Sheets", DisplayValue = totalStockQty.ToString("N0"), Accent = "Blue" });
            OnPropertyChanged(nameof(InventoryTiles));

            // ═════════════════════════════════════════════════════════════
            //  📈 CHART — Monthly sales, last 6 months
            //  Chat 23 v2.0.1: bar colors updated to industrial blue palette.
            //    Regular:   "#6366F1" (indigo) → "#3B82F6" (blue-500)
            //    Highlight: "#10B981" (green)  → "#1D4ED8" (royal blue)
            // ═════════════════════════════════════════════════════════════
            var now = DateTime.Now;
            var months = Enumerable.Range(0, 6)
                .Select(i => now.AddMonths(-5 + i))
                .ToList();

            // The six-month totals come from the metrics service, which
            // already built them with the same "prefer UpdatedAt, fall
            // back to CreatedAt" rule this loop used. Recomputing them
            // here would mean a second full scan of the invoice table.
            var rawBars = exec.Sales.MonthlyTrend.Select(t => new ChartBarModel
            {
                Label = t.Label,
                RawValue = t.Value,
                ValueDisplay = t.Value > 0 ? $"AED {t.Value:N2}" : "—",
                BarColor = "#3B82F6"
            }).ToList();

            decimal maxVal = rawBars.Any() ? rawBars.Max(b => b.RawValue) : 0m;
            bool anyData = maxVal > 0;

            foreach (var bar in rawBars)
            {
                bar.NormalizedHeight = anyData
                    ? (double)(bar.RawValue / maxVal)
                    : 0.04;
                bar.IsHighlight = anyData && bar.RawValue == maxVal;
                // v2.0.1: highlight = royal blue (was #10B981 green)
                bar.BarColor = bar.IsHighlight ? "#1D4ED8" : "#3B82F6";
            }

            // Phase 1 C-4: update existing collection instead of replacing
            _monthlySalesBars.Clear();
            foreach (var bar in rawBars) _monthlySalesBars.Add(bar);
            OnPropertyChanged(nameof(MonthlySalesBars));
            HasChartData = anyData;
            ChartTitle = $"{months.First():MMM yyyy} → {months.Last():MMM yyyy}";
            PulseDisplay = $"AED {totalSales:N2}";
            PulseHint = $"This month  AED {salesMtd:N2}";
            PulseSpark = string.Join(",", rawBars.Select(b =>
                b.RawValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            InProductionDisplay = jobInProduction.ToString("N0");
            OverdueDisplay = jobOverdue.ToString("N0");
            HasOverdueJobs = jobOverdue > 0;
            PendingPcsDisplay = dnPendingKnown ? dnPendingPcs.ToString("N0") : "\u2014";

            // Phase 1 C-4: update existing collection
            _deliveryMixSlices.Clear();
            _deliveryMixSlices.Add(new ChartSliceModel { Label = "Preparing", RawValue = dnDraft, ValueDisplay = $"{dnDraft:N0} notes", Accent = "Amber" });
            _deliveryMixSlices.Add(new ChartSliceModel { Label = "Dispatched", RawValue = dnDispatched, ValueDisplay = $"{dnDispatched:N0} notes", Accent = "Blue" });
            _deliveryMixSlices.Add(new ChartSliceModel { Label = "Delivered pcs", RawValue = dnTotalDeliveredPcs, ValueDisplay = $"{dnTotalDeliveredPcs:N0} pcs", Accent = "Green" });
            _deliveryMixSlices.Add(new ChartSliceModel { Label = "Pending pcs", RawValue = dnPendingKnown ? dnPendingPcs : 0, ValueDisplay = dnPendingKnown ? $"{dnPendingPcs:N0} pcs" : "\u2014", Accent = dnPendingPcs > 0 ? "Red" : "Amber" });
            OnPropertyChanged(nameof(DeliveryMixSlices));

            // ═════════════════════════════════════════════════════════════
            //  💬 RECENT ACTIVITY — merge all module events
            // ═════════════════════════════════════════════════════════════
            var feedItems = new List<ActivityFeedItem>();

            // The feed keeps only the newest 20 entries overall, so each
            // source needs at most its own newest 20. These used to be
            // full-table loads whose rows were then discarded.
            const int FeedFetch = 20;

            var recent   = await _dashboardMetrics.GetRecentRowsAsync(FeedFetch);
            var invoices = recent.Invoices;
            var jobs     = recent.Jobs;
            var allDns   = recent.DeliveryNotes;

            // Invoice activity
            var invoiceActivity = invoices.Select(i =>
            {
                var date = i.UpdatedAt != default ? i.UpdatedAt : i.CreatedAt;
                var isUpdated = i.UpdatedAt != default && i.UpdatedAt != i.CreatedAt;
                return new ActivityFeedItem
                {
                    Icon = "\uE8A5",
                    Title = $"{(string.IsNullOrWhiteSpace(i.ProformaId) ? "Invoice" : i.ProformaId)}" +
                                   $"{(string.IsNullOrWhiteSpace(i.CustomerName) ? "" : $"  ·  {i.CustomerName}")}",
                    SubTitle = $"Proforma  ·  {(isUpdated ? "Updated" : "Created")} {date:dd MMM}",
                    ValueDisplay = $"AED {i.GrandTotalVatInclusive:N2}",
                    StatusLabel = i.Status ?? "Draft",
                    StatusColor = ResolveStatusColor(i.Status),
                    TintColor = ResolveStatusTint(i.Status),
                    ModuleTag = "ProformaInvoice",
                    SortDate = date
                };
            });
            feedItems.AddRange(invoiceActivity);

            // Sheet store history
            try
            {
                using var db = new AppDbContext();
                var historyRows = await db.SheetHistories
                    .OrderByDescending(h => h.Timestamp)
                    .Take(20)
                    .ToListAsync();

                var sheetActivity = historyRows.Select(h => new ActivityFeedItem
                {
                    Icon = "\uE7B8",
                    Title = string.IsNullOrWhiteSpace(h.SheetDescription)
                                       ? "Sheet Store" : h.SheetDescription,
                    SubTitle = $"Sheet Store  ·  {h.Action}  ·  {h.Timestamp:dd MMM HH:mm}",
                    ValueDisplay = h.QuantityChanged > 0
                                       ? $"{h.QuantityChanged} sheet{(h.QuantityChanged == 1 ? "" : "s")}"
                                       : h.ValueChanged.HasValue
                                           ? $"AED {h.ValueChanged:N2}" : "—",
                    StatusLabel = h.Action,
                    StatusColor = ResolveActionColor(h.Action),
                    TintColor = ResolveActionTint(h.Action),
                    ModuleTag = "SheetStore",
                    SortDate = h.Timestamp
                });

                feedItems.AddRange(sheetActivity);
            }
            catch { /* sheet history unavailable */ }

            // Job Order activity
            var jobActivity = jobs.Select(j =>
            {
                bool isOverdue = j.DeliveryDate.HasValue
                              && j.DeliveryDate.Value.Date < today
                              && !ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus.JobOrderStatusValues.IsFinished(j.Status);

                string statusLabel = isOverdue ? "OVERDUE" : (j.Status ?? "Draft").ToUpperInvariant();
                string priorityIcon = j.Priority switch
                {
                    "High" => "🔴",
                    "Medium" => "🟡",
                    "Low" => "🟢",
                    _ => "🏭"
                };

                return new ActivityFeedItem
                {
                    Icon = priorityIcon,
                    Title = $"{j.JobOrderId}" +
                                   $"{(string.IsNullOrWhiteSpace(j.CustomerName) ? "" : $"  ·  {j.CustomerName}")}",
                    SubTitle = $"Job Order  ·  " +
                                   (j.DeliveryDate.HasValue
                                       ? $"Due {j.DeliveryDate.Value:dd MMM}"
                                       : "No due date"),
                    ValueDisplay = j.GrandTotalVatInclusive > 0
                                       ? $"AED {j.GrandTotalVatInclusive:N2}" : "—",
                    StatusLabel = statusLabel,
                    StatusColor = ResolveJobStatusColor(j.Status, isOverdue),
                    TintColor = ResolveJobPriorityTint(j.Priority),
                    ModuleTag = "JobOrder",
                    SortDate = j.UpdatedAt
                };
            });
            feedItems.AddRange(jobActivity);

            // Delivery Note activity
            var dnActivity = allDns
                .Where(d => !Eq(d.Status, "Draft"))
                .Select(d =>
                {
                    string icon = d.Status switch
                    {
                        "Dispatched" => "🚚",
                        "Delivered" => "✅",
                        "Signed" => "✍️",
                        "Received" => "📬",
                        _ => "📦"
                    };

                    string valueLine = d.TotalDeliveredQty > 0
                        ? $"{d.TotalDeliveredQty} pcs"
                        : "—";

                    return new ActivityFeedItem
                    {
                        Icon = icon,
                        Title = $"{d.DeliveryNoteId}" +
                                       $"{(string.IsNullOrWhiteSpace(d.CustomerName) ? "" : $"  ·  {d.CustomerName}")}",
                        SubTitle = $"Delivery  ·  {d.Status}  ·  {d.DeliveryDate:dd MMM}",
                        ValueDisplay = valueLine,
                        StatusLabel = d.Status.ToUpperInvariant(),
                        StatusColor = ResolveDnStatusColor(d.Status),
                        TintColor = ResolveDnStatusTint(d.Status),
                        ModuleTag = "DeliveryNote",
                        SortDate = d.DeliveryDate
                    };
                });
            feedItems.AddRange(dnActivity);

            var merged = feedItems
                .OrderByDescending(f => f.SortDate)
                .Take(20)
                .ToList();

            // Phase 1 C-4: update existing collection
            _recentActivity.Clear();
            foreach (var item in merged) _recentActivity.Add(item);
            OnPropertyChanged(nameof(RecentActivity));
            HasRecentActivity = merged.Count > 0;
        }

        // ═════════════════════════════════════════════════════════════════
        //  WORKFLOW PIPELINE BUILD
        //
        //  Each stage counts the live work sitting in that handoff, not a
        //  lifetime total — a rail that showed "14,203 invoices ever"
        //  would tell an operator nothing about what to chase today.
        //
        //  A stage whose backing module is unavailable renders an em dash
        //  rather than a zero. A zero asserts "there is no work here",
        //  which is a different claim from "we could not read it", and on
        //  a money document that difference matters.
        // ═════════════════════════════════════════════════════════════════
        private void BuildWorkflowPipeline(ExecutiveMetrics exec, TaxInvoiceKpiSummary? tax)
        {
            _workflowStages.Clear();

            // ── 1 · QUOTE ────────────────────────────────────────────────
            _workflowStages.Add(new WorkflowStageModel
            {
                Glyph = "\uE8A5",
                Name = "Quote",
                ValueDisplay = exec.Sales.TotalInvoices.ToString("N0"),
                Foot = $"{exec.Sales.ApprovedInvoices:N0} approved · "
                     + $"{exec.Sales.DraftInvoices:N0} draft",
                AccentKey = "InfoBrush",
                ModuleKey = "ProformaInvoice",
                IsFirst = true,
            });

            // ── 2 · PRODUCTION ───────────────────────────────────────────
            // Overdue jobs are the stage most worth interrupting someone
            // for, so this is the stage that goes alert.
            _workflowStages.Add(new WorkflowStageModel
            {
                Glyph = exec.Production.Overdue > 0 ? "\uE7BA" : "\uE713",
                Name = "Production",
                ValueDisplay = exec.Production.InProduction.ToString("N0"),
                Foot = exec.Production.Overdue > 0
                    ? $"{exec.Production.Overdue:N0} overdue"
                    : "on schedule",
                AccentKey = exec.Production.Overdue > 0 ? "DangerBrush" : "AccentBrush",
                IsActive = exec.Production.Overdue > 0,
                ModuleKey = "JobOrder",
            });

            // ── 3 · DELIVERY ─────────────────────────────────────────────
            // Live handoff = still preparing + currently Dispatched.
            // Do not add HasLeftTheBuilding (Delivered/Signed/Received) —
            // that is a lifetime sent-out count, not work in this stage.
            var dnInFlight = exec.Delivery.Preparing + exec.Delivery.CurrentlyDispatched;
            _workflowStages.Add(new WorkflowStageModel
            {
                Glyph = "\uE806",
                Name = "Delivery",
                ValueDisplay = dnInFlight.ToString("N0"),
                Foot = !exec.Delivery.PendingPiecesAvailable
                    ? "pending qty unavailable"
                    : exec.Delivery.PendingPieces > 0
                        ? $"{exec.Delivery.PendingPieces:N0} pcs pending"
                        : "fully dispatched",
                AccentKey = !exec.Delivery.PendingPiecesAvailable || exec.Delivery.PendingPieces > 0
                    ? "WarningBrush" : "SuccessBrush",
                IsActive = !exec.Delivery.PendingPiecesAvailable || exec.Delivery.PendingPieces > 0,
                ModuleKey = "DeliveryNote",
            });

            // ── 4 · INVOICE ── and ── 5 · PAYMENT ────────────────────────
            if (tax is null)
            {
                foreach (var name in new[] { "Invoice", "Payment" })
                {
                    _workflowStages.Add(new WorkflowStageModel
                    {
                        Glyph = "\uE9CE",
                        Name = name,
                        ValueDisplay = "\u2014",
                        Foot = "tax invoice data unavailable",
                        AccentKey = "MutedForegroundBrush",
                        ModuleKey = name == "Invoice" ? "TaxInvoice" : "Statement",
                    });
                }
            }
            else
            {
                _workflowStages.Add(new WorkflowStageModel
                {
                    Glyph = tax.OverdueCount > 0 ? "\uE7BA" : "\uE8EF",
                    Name = "Invoice",
                    ValueDisplay = tax.IssuedCount.ToString("N0"),
                    Foot = tax.OverdueCount > 0
                        ? $"{tax.OverdueCount:N0} past terms"
                        : $"{tax.PendingCount:N0} awaiting payment",
                    AccentKey = tax.OverdueCount > 0 ? "DangerBrush" : "AccentBrush",
                    IsActive = tax.OverdueCount > 0,
                    ModuleKey = "TaxInvoice",
                });

                _workflowStages.Add(new WorkflowStageModel
                {
                    Glyph = "\uE8C7",
                    Name = "Payment",
                    ValueDisplay = tax.PaidThisMonthCount.ToString("N0"),
                    Foot = tax.OverdueAmount > 0
                        ? $"AED {tax.OverdueAmount:N2} overdue"
                        : "nothing overdue",
                    AccentKey = tax.OverdueAmount > 0 ? "WarningBrush" : "SuccessBrush",
                    IsActive = tax.OverdueAmount > 0,
                    ModuleKey = "Statement",
                });
            }

            OnPropertyChanged(nameof(WorkflowStages));
        }

        // ═════════════════════════════════════════════════════════════════
        //  THEME SWAP
        // ═════════════════════════════════════════════════════════════════

        protected override void DisposeCore()
        {
            _docChangeCts?.Cancel();
            _docChangeCts?.Dispose();
            _docChangeCts = null;
            _database?.UnsubscribeDocumentsChanged(OnDocumentChangedAsync);
        }

        protected override void OnThemeChanged()
        {
            SalesTiles = new ObservableCollection<KpiTileModel>(_salesTiles);
            ProductionTiles = new ObservableCollection<KpiTileModel>(_productionTiles);
            DeliveryTiles = new ObservableCollection<KpiTileModel>(_deliveryTiles);
            InventoryTiles = new ObservableCollection<KpiTileModel>(_inventoryTiles);
            RecentActivity = new ObservableCollection<ActivityFeedItem>(_recentActivity);
            DeliveryMixSlices = new ObservableCollection<ChartSliceModel>(_deliveryMixSlices);
            // The pipeline colours its stage chips through BrushKeyConverter,
            // which resolves once at bind time — so the rail has to be
            // re-materialised on a theme swap or its chips keep the old
            // theme's brushes, same reason the KPI bands above are rebuilt.
            WorkflowStages = new ObservableCollection<WorkflowStageModel>(_workflowStages);
        }

        // ═════════════════════════════════════════════════════════════════
        //  COLOR HELPERS
        // ═════════════════════════════════════════════════════════════════

        private static string ResolveStatusColor(string? status) =>
            status?.ToLowerInvariant() switch
            {
                "draft" => "NeutralBrush",
                "sent" => "InfoBrush",
                "approved" => "SuccessBrush",
                "rejected" => "DangerBrush",
                _ => "NeutralBrush"
            };

        private static string ResolveStatusTint(string? status) =>
            status?.ToLowerInvariant() switch
            {
                "draft" => "TintGrayBrush",
                "sent" => "TintBlueBrush",
                "approved" => "TintGreenBrush",
                "rejected" => "TintRedBrush",
                _ => "TintGrayBrush"
            };

        private static string ResolveActionColor(string? action) =>
            action?.ToLowerInvariant() switch
            {
                "added" => "SuccessBrush",
                "edited" => "WarningBrush",
                "deleted" => "DangerBrush",
                "bought" => "InfoBrush",
                "used" => "AccentBrush",
                _ => "NeutralBrush"
            };

        private static string ResolveActionTint(string? action) =>
            action?.ToLowerInvariant() switch
            {
                "added" => "TintGreenBrush",
                "edited" => "TintAmberBrush",
                "deleted" => "TintRedBrush",
                "bought" => "TintBlueBrush",
                "used" => "TintPurpleBrush",
                _ => "TintGrayBrush"
            };

        private static string ResolveJobStatusColor(string? status, bool isOverdue)
        {
            if (isOverdue) return "DangerBrush";
            return status switch
            {
                "Draft" => "NeutralBrush",
                "Released" => "InfoBrush",
                "InProduction" => "WarningBrush",
                "Completed" => "SuccessBrush",
                "Delivered" => "AccentBrush",
                _ => "NeutralBrush"
            };
        }

        private static string ResolveJobPriorityTint(string? priority) =>
            priority switch
            {
                "High" => "TintRedBrush",
                "Medium" => "TintAmberBrush",
                "Low" => "TintGreenBrush",
                _ => "TintIndigoBrush"
            };

        private static string ResolveDnStatusColor(string? status) =>
            status switch
            {
                "Dispatched" => "AccentBrush",
                "Delivered" => "SuccessBrush",
                "Signed" => "SuccessBrush",
                "Received" => "InfoBrush",
                _ => "NeutralBrush"
            };

        private static string ResolveDnStatusTint(string? status) =>
            status switch
            {
                "Dispatched" => "TintIndigoBrush",
                "Delivered" => "TintGreenBrush",
                "Signed" => "TintGreenBrush",
                "Received" => "TintBlueBrush",
                _ => "TintGrayBrush"
            };

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

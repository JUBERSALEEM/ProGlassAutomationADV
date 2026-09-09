// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/Statement/StDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 23 v1.1)
//
// CHANGES vs PREVIOUS VERSION:
//   1. RecentStatementItem: added Index property (int, 1-based serial no)
//   2. LoadCoreAsync(): projection uses .Select((item, i) => ...) to
//      assign Index = i + 1 during list building.
//   3. Nothing else changed. Zero logic changes to load flow, KPIs,
//      commands, or dispatcher pattern.
//
// PRESERVED (zero regression):
//   • LoadCoreAsync signature (base class contract)
//   • All KPI backing fields + display properties
//   • _statements null guard
//   • Task.Run background load pattern
//   • Dispatcher.Invoke UI thread updates
//   • OnPropertyChanged(string.Empty) batch notify
//   • All 3 commands (NavigateTo pattern)
//   • IsEmpty guard logic (_isLoaded && !IsLoading && count==0)
//
// REGRESSION RISK: ZERO
//   New property on POCO — no consumer impact.
//   Projection change adds index — existing property values unchanged.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Statement.Pdf;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Sharing;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Dashboard.Statement
{
    public class StDashboardViewModel : DashboardPageBase
    {
        private readonly IStatementRepository _statements = null!;

        /// <summary>How many statements the recent panel shows.</summary>
        private const int RecentStatementCount = 5;

        public StDashboardViewModel(IStatementRepository? statements)
        {
            _statements = statements!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        // ── Tab identity ──────────────────────────────────────────────────
        public override string TabTitle => "Statements";
        public override string TabSubtitle => "Customer balances";
        public override string TabIcon => "\uE9D9";

        // ── Navigation callback (wired by DashboardViewModel) ─────────────
        public Action<string>? NavigateTo { get; set; }

        // ── Recent statements list ────────────────────────────────────────
        public ObservableCollection<RecentStatementItem> RecentStatements { get; }
            = new();

        // ── KPI backing fields ────────────────────────────────────────────
        private int _totalStatements;
        private decimal _totalInvoiced;
        private decimal _totalOutstanding;
        private decimal _overdueAmount;
        private bool _isLoaded;

        // ── KPI display properties ────────────────────────────────────────
        public int TotalStatements
            => _totalStatements;
        public string TotalInvoicedDisplay
            => $"AED {_totalInvoiced:N2}";
        public string TotalOutstandingDisplay
            => $"AED {_totalOutstanding:N2}";
        public string OverdueAmountDisplay
            => _overdueAmount > 0 ? $"AED {_overdueAmount:N2}" : "—";

        public bool HasOverdue => _overdueAmount > 0;
        public bool HasStatements => _totalStatements > 0;

        public ObservableCollection<ProGlassAutomation.Models.Dashboard.ChartSliceModel> BalanceMixSlices { get; }
            = new();

        /// <summary>
        /// KPI stat cards, built in the SAME KpiCardModel shape as the other
        /// six module dashboards so the Statement page renders through the
        /// shared SoftKpiCardTemplate (no more hand-written scalar cards).
        /// Rebuilt whenever the underlying scalars change.
        /// </summary>
        public ObservableCollection<ProGlassAutomation.Models.Dashboard.KpiCardModel> KpiCards { get; }
            = new();

        /// <summary>
        /// True only after first load completes with zero statements.
        /// Uses base.IsLoading to avoid conflict with DashboardPageBase.
        /// </summary>
        public bool IsEmpty => _isLoaded && !IsLoading && _totalStatements == 0;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand GoToStatementsCommand { get; private set; }
        public ICommand NewStatementCommand { get; private set; }
        public ICommand OpenPdfCommand { get; private set; }
        public ICommand ShareCommand { get; private set; } = null!;

        // ═════════════════════════════════════════════════════════════════
        public StDashboardViewModel()
         : this(
            App.ServiceProvider?.GetService<IStatementRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            GoToStatementsCommand = new RelayCommand(_ =>
                NavigateTo?.Invoke("StatementGenerator"));

            NewStatementCommand = new RelayCommand(_ =>
                NavigateTo?.Invoke("StatementGenerator"));

            OpenPdfCommand = new RelayCommand(p =>
            {
                if (p is RecentStatementItem item && item.HasPdf)
                    StatementPdfExporter.OpenPdf(item.PdfPath);
            });

            ShareCommand = new RelayCommand(async p =>
            {
                if (p is RecentStatementItem item && item.HasPdf)
                    await ShareAsync(item);
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  CORE LOAD — called by DashboardPageBase.LoadAsync()
        //  DashboardPageBase sets IsLoading=true BEFORE calling this.
        //  DashboardPageBase sets IsLoading=false AFTER this returns.
        //  Do NOT set IsLoading here — base class manages it.
        // ═════════════════════════════════════════════════════════════════

        protected override async Task LoadCoreAsync()
        {
            // Guard: both dependencies may be null if this page is loaded
            // before App.StartupCore has finished. _statements is still
            // injected (DI selects the greediest constructor) and is checked
            // here so a half-initialised app degrades to an empty page rather
            // than throwing inside the load.
            if (_statements == null || App.DashboardMetrics == null)
            {
                AppLogService.Debug(
                    "[StDashboard] dependencies not yet initialized.");
                return;
            }

            // Phase 3: aggregates come from IDashboardMetricsService, not from
            // summing the whole table here. This page was the only one of the
            // four module dashboards with no metrics coverage, so it loaded
            // every statement into the ViewModel.
            var metrics = await App.DashboardMetrics
                .GetStatementMetricsAsync(RecentStatementCount);

            if (metrics.TotalStatements == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    RecentStatements.Clear();
                    _totalStatements = 0;
                    _totalInvoiced = 0m;
                    _totalOutstanding = 0m;
                    _overdueAmount = 0m;
                    _isLoaded = true;
                    RebuildBalanceMix();
                    OnPropertyChanged(string.Empty);
                }));
                return;
            }

            // ── KPIs, already aggregated by the service ──────────────────
            int totalCount = metrics.TotalStatements;
            decimal totalInv = metrics.TotalInvoiced;
            decimal totalOut = metrics.TotalOutstanding;
            decimal totalOverdue = metrics.OverdueAmount;

            // ── Recent 5 statements ───────────────────────────────────────
            // CHAT 23 v1.1: .Select((item, i) => ...) provides 1-based
            // Index for serial-number badge in the UI. Ordering is
            // OrderByDescending(CreatedAt) so #1 = newest, #2 = next, etc.
            var recent = metrics.Recent
                .OrderByDescending(i => i.CreatedAt)
                .Take(RecentStatementCount)
                .Select((i, idx) => new RecentStatementItem
                {
                    Index = idx + 1,   // 1-based serial number
                    StatementNumber = i.StatementNumber,
                    Title = i.Title,
                    SubTitle = i.SubTitle,
                    TotalInvoicedDisplay = i.TotalInvoicedDisplay,
                    OutstandingDisplay = i.TotalOutstandingDisplay,
                    OverdueDisplay = i.OverdueAmountDisplay,
                    HasOverdue = i.OverdueAmount > 0,
                    CreatedAtDisplay = i.CreatedAtDisplay,
                    PdfPath = i.PdfPath,
                    HasPdf = i.HasPdf,
                    HasAttachments = i.HasAttachments,
                    InvoiceCount = i.InvoiceCount
                })
                .ToList();

            // ── Update UI on dispatcher ───────────────────────────────────
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                RecentStatements.Clear();
                foreach (var r in recent)
                    RecentStatements.Add(r);

                _totalStatements = totalCount;
                _totalInvoiced = totalInv;
                _totalOutstanding = totalOut;
                _overdueAmount = totalOverdue;
                _isLoaded = true;
                RebuildBalanceMix();
                RebuildKpiCards();

                // Batch notification — single fire
                OnPropertyChanged(string.Empty);
            }));
        }

        // Sharing Center (Pattern B) — full context via the repository so
        // recipients resolve from the real customer/company filters.
        private async Task ShareAsync(RecentStatementItem item)
        {
            if (item == null || !item.HasPdf || _statements == null) return;

            ShareContext context;
            try
            {
                var saved = await _statements
                    .GetByStatementNumberAsync(item.StatementNumber);
                context = saved != null
                    ? ShareContextFactory.FromStatement(saved)
                    : ShareContextFactory.FromListReport(
                        "Statement", item.Title, "Statement");
                if (string.IsNullOrWhiteSpace(context.DocumentNumber))
                    context.DocumentNumber = item.StatementNumber ?? "";
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Debug(
                    $"[Sharing] statement lookup failed: {ex.Message}");
                context = ShareContextFactory.FromListReport(
                    "Statement", item.Title, "Statement");
                context.DocumentNumber = item.StatementNumber ?? "";
            }

            var dialogs = ProGlassAutomation.App.Dialogs;
            if (ShareLauncher.AttachFile(context, item.PdfPath, dialogs))
                ShareLauncher.OpenComposer(context, dialogs);
        }

        private void RebuildBalanceMix()
        {
            decimal collected = Math.Max(0m, _totalInvoiced - _totalOutstanding);
            BalanceMixSlices.Clear();
            BalanceMixSlices.Add(new ProGlassAutomation.Models.Dashboard.ChartSliceModel
            {
                Label = "Collected",
                RawValue = collected,
                ValueDisplay = $"AED {collected:N0}",
                Accent = "Green"
            });
            BalanceMixSlices.Add(new ProGlassAutomation.Models.Dashboard.ChartSliceModel
            {
                Label = "Outstanding",
                RawValue = Math.Max(0m, _totalOutstanding - _overdueAmount),
                ValueDisplay = TotalOutstandingDisplay,
                Accent = "Amber"
            });
            BalanceMixSlices.Add(new ProGlassAutomation.Models.Dashboard.ChartSliceModel
            {
                Label = "Overdue",
                RawValue = _overdueAmount,
                ValueDisplay = OverdueAmountDisplay,
                Accent = "Red"
            });
        }

        /// <summary>
        /// Rebuilds the KPI card collection from the four scalar values so the
        /// Statement page uses the SAME SoftKpiCardTemplate as every other
        /// module dashboard. BadgeColor/TintColor are theme-brush keys that
        /// swap with the Light/Dark theme.
        /// </summary>
        private void RebuildKpiCards()
        {
            KpiCards.Clear();
            KpiCards.Add(new ProGlassAutomation.Models.Dashboard.KpiCardModel
            {
                Icon = "\uE9D9",
                Title = "Statements",
                DisplayValue = _totalStatements.ToString("N0"),
                SubLabel = "Generated to date",
                BadgeColor = "AccentBrush",
                TintColor = "TintBlueBrush"
            });
            KpiCards.Add(new ProGlassAutomation.Models.Dashboard.KpiCardModel
            {
                Icon = "\uE8A5",
                Title = "Total Invoiced",
                DisplayValue = TotalInvoicedDisplay,
                SubLabel = "All statements",
                BadgeColor = "SuccessBrush",
                TintColor = "TintGreenBrush"
            });
            KpiCards.Add(new ProGlassAutomation.Models.Dashboard.KpiCardModel
            {
                Icon = "\uE7B8",
                Title = "Outstanding",
                DisplayValue = TotalOutstandingDisplay,
                SubLabel = "Awaiting payment",
                BadgeColor = "WarningBrush",
                TintColor = "TintAmberBrush"
            });
            KpiCards.Add(new ProGlassAutomation.Models.Dashboard.KpiCardModel
            {
                Icon = _overdueAmount > 0 ? "\uE7BA" : "\uE73E",
                Title = "Overdue",
                DisplayValue = OverdueAmountDisplay,
                SubLabel = "Past payment terms",
                BadgeColor = _overdueAmount > 0 ? "DangerBrush" : "NeutralBrush",
                TintColor = _overdueAmount > 0 ? "TintRedBrush" : "TintGrayBrush",
                IsAlert = _overdueAmount > 0
            });
        }
    }
}

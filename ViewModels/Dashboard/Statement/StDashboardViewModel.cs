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
//   • App.Statements null guard
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

namespace ProGlassAutomation.ViewModels.Dashboard.Statement
{
    public class StDashboardViewModel : DashboardPageBase
    {
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
        /// True only after first load completes with zero statements.
        /// Uses base.IsLoading to avoid conflict with DashboardPageBase.
        /// </summary>
        public bool IsEmpty => _isLoaded && !IsLoading && _totalStatements == 0;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand GoToStatementsCommand { get; }
        public ICommand NewStatementCommand { get; }
        public ICommand OpenPdfCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public StDashboardViewModel()
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
        }

        // ═════════════════════════════════════════════════════════════════
        //  CORE LOAD — called by DashboardPageBase.LoadAsync()
        //  DashboardPageBase sets IsLoading=true BEFORE calling this.
        //  DashboardPageBase sets IsLoading=false AFTER this returns.
        //  Do NOT set IsLoading here — base class manages it.
        // ═════════════════════════════════════════════════════════════════

        protected override async Task LoadCoreAsync()
        {
            // Guard: App.Statements may be null if called before startup
            if (App.Statements == null)
            {
                AppLogService.Debug(
                    "[StDashboard] App.Statements not yet initialized.");
                return;
            }

            // Load on background thread
            var items = await Task.Run(
                () => App.Statements.GetAllAsync());

            if (items == null || items.Count == 0)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RecentStatements.Clear();
                    _totalStatements = 0;
                    _totalInvoiced = 0m;
                    _totalOutstanding = 0m;
                    _overdueAmount = 0m;
                    _isLoaded = true;
                    RebuildBalanceMix();
                    OnPropertyChanged(string.Empty);
                });
                return;
            }

            // ── Compute KPIs ──────────────────────────────────────────────
            int totalCount = items.Count;
            decimal totalInv = items.Sum(i => i.TotalInvoiced);
            decimal totalOut = items.Sum(i => i.TotalOutstanding);
            decimal totalOverdue = items.Sum(i => i.OverdueAmount);

            // ── Recent 5 statements ───────────────────────────────────────
            // CHAT 23 v1.1: .Select((item, i) => ...) provides 1-based
            // Index for serial-number badge in the UI. Ordering is
            // OrderByDescending(CreatedAt) so #1 = newest, #2 = next, etc.
            var recent = items
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
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
            Application.Current?.Dispatcher.Invoke(() =>
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

                // Batch notification — single fire
                OnPropertyChanged(string.Empty);
            });
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
    }
}

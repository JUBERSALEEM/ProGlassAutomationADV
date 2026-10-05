// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/DashboardViewModel.cs
// PROJECT: ProGlassAutomation
//
// CHAT 16: Added StDashboardViewModel as the 7th dashboard tab.
//          Added NavigateTo callback wiring for the Statement tab.
//          All other code UNCHANGED.
//
// PHASE 28: Added OptimizationDashboardViewModel and ProductionDashboardViewModel
//          as the 8th and 9th tabs. Construction, the Pages list and the
//          DataTemplate map in DashboardView.xaml are the only three places a
//          dashboard page is registered; nothing else needed to change.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.Dashboard.DeliveryNote.DnDashboard;
using ProGlassAutomation.ViewModels.Dashboard.JobOrder.JoDashboard;
using ProGlassAutomation.ViewModels.Dashboard.Statement;
using ProGlassAutomation.ViewModels.Dashboard.TaxInvoice.TiDashboard;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Shell ViewModel for the multi-page Dashboard.
    /// Owns the tab list and the currently active sub-page.
    /// Lazy-loads each sub-page on first tab selection via ActivePage setter.
    /// NavigateTo and TriggerNewInvoice are wired by MainViewModel after
    /// construction and passed down to OverviewVM.
    ///
    /// Phase 10.9:  Added DnDashboardVM (Deliveries tab).
    /// Chat 15:     Added TiDashboardVM (Tax Invoices tab).
    /// Chat 16:     Added StDashboardVM (Statements tab).
    /// </summary>
    public class DashboardViewModel : ViewModelBase, IDisposable
    {
        // Phase 4: Named handler for static event — detachable
        private EventHandler? _currentUserChangedHandler;
        private bool _disposed;

        // ── tab pages ─────────────────────────────────────────────────────
        public List<DashboardPageBase> Pages { get; }

        private DashboardPageBase _activePage;
        public DashboardPageBase ActivePage
        {
            get => _activePage;
            set
            {
                if (_activePage == value) return;
                _activePage = value;
                OnPropertyChanged();
                // Phase 2 H-5 fix: the constructor already loads the initial
                // page; this setter only needs to load on subsequent TAB
                // CHANGES. Guard against a redundant first load.
                if (!_isInitialLoad)
                    _ = _activePage.LoadAsync();
            }
        }

        private bool _isInitialLoad = true;

        // ── sub-page VMs ──────────────────────────────────────────────────
        public OverviewDashboardViewModel OverviewVM { get; }
        public SheetStoreDashboardViewModel SheetStoreVM { get; }
        public PiDashboardViewModel ProformaVM { get; }
        public JoDashboardViewModel JobOrderVM { get; }
        public DnDashboardViewModel DeliveryNoteVM { get; }
        public TiDashboardViewModel TaxInvoiceVM { get; }  // Chat 15
        public StDashboardViewModel StatementVM { get; }  // Chat 16 NEW

        // Phase 28 — Optimization and Production pages. Both live in this
        // namespace (not a sub-namespace) exactly like SheetStoreDashboardViewModel,
        // so no extra using is needed here and the XAML DataTemplate can reach
        // them through the existing vm: prefix.
        public OptimizationDashboardViewModel OptimizationVM { get; }
        public ProductionDashboardViewModel ProductionVM { get; }

        // ── commands ──────────────────────────────────────────────────────
        public ICommand SelectTabCommand { get; }

        // ── v2 welcome banner (bound in DashboardView.xaml) ──────────────

        public string CurrentUserDisplay =>
            Services.Audit.AuditService.CurrentUser?.DisplayName
            ?? Services.Audit.AuditService.CurrentUsername;

        public string WelcomeLine
        {
            get
            {
                int hour = DateTime.Now.Hour;
                string part = hour < 12 ? "Good morning"
                    : hour < 17 ? "Good afternoon"
                    : "Good evening";
                return $"{part}, {CurrentUserDisplay}";
            }
        }

        public string WelcomeDateLine =>
            DateTime.Now.ToString("dddd, dd MMMM yyyy") +
            "  •  Here's your glass production at a glance.";

        // ── navigation callbacks — set by MainViewModel after construction ─

        /// <summary>
        /// Wired by MainViewModel: target => Navigate(target).
        /// Passed down to OverviewVM, TiDashboardVM, AND StDashboardVM
        /// so quick action buttons on all tabs can navigate to modules.
        /// </summary>
        public Action<string>? NavigateTo
        {
            set
            {
                OverviewVM.NavigateTo = value;
                TaxInvoiceVM.NavigateTo = value;  // Chat 15
                StatementVM.NavigateTo = value;  // Chat 16 NEW
            }
        }

        /// <summary>
        /// Wired by MainViewModel: () => ProformaInvoiceVM.TriggerNewInvoice().
        /// Passed down to OverviewVM for the "+ New Invoice" quick action.
        /// </summary>
        public Action? TriggerNewInvoice
        {
            set => OverviewVM.TriggerNewInvoice = value;
        }

        // ═════════════════════════════════════════════════════════════════
        public DashboardViewModel()
        {
            OverviewVM = new OverviewDashboardViewModel();
            SheetStoreVM = new SheetStoreDashboardViewModel();
            ProformaVM = new PiDashboardViewModel();
            JobOrderVM = new JoDashboardViewModel();
            DeliveryNoteVM = new DnDashboardViewModel();
            TaxInvoiceVM = new TiDashboardViewModel();   // Chat 15
            StatementVM = new StDashboardViewModel();   // Chat 16 NEW
            OptimizationVM = new OptimizationDashboardViewModel();   // Phase 28
            ProductionVM = new ProductionDashboardViewModel();       // Phase 28

            Pages = new List<DashboardPageBase>
            {
                OverviewVM,
                SheetStoreVM,
                ProformaVM,
                JobOrderVM,
                DeliveryNoteVM,
                TaxInvoiceVM,                              // Chat 15
                StatementVM,                               // Chat 16 NEW
                OptimizationVM,                            // Phase 28
                ProductionVM                               // Phase 28
            };

            SelectTabCommand = new RelayCommand(p =>
            {
                if (p is DashboardPageBase page)
                    ActivePage = page;
            });

            // Default tab — set backing field directly to avoid double load.
            // Phase 2 H-5: the ActivePage setter skips the first load (flag),
            // so this explicit load is the ONLY initial load that runs.
            _activePage = OverviewVM;
            _isInitialLoad = false;
            _ = OverviewVM.LoadAsync();

            // Phase 4: Named handler — detachable, no lambda leak
            _currentUserChangedHandler = (_, __) =>
            {
                OnPropertyChanged(nameof(CurrentUserDisplay));
                OnPropertyChanged(nameof(WelcomeLine));
            };
            Services.Audit.AuditService.CurrentUserChanged += _currentUserChangedHandler;
        }

        public void OnViewLoaded()
        {
            _ = ActivePage.LoadAsync();
        }

        // Phase 4: Event lifetime — detach static event
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Services.Audit.AuditService.CurrentUserChanged -= _currentUserChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DashboardViewModel] Dispose detach failed: " + ExceptionMessages.Deep(ex)); }
            GC.SuppressFinalize(this);
        }
    }
}
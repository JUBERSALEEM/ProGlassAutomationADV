// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/DashboardViewModel.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.ViewModels.Dashboard
//
// CHANGES vs PREVIOUS VERSION:
//   Chat 15 — Added TiDashboardViewModel as the 6th dashboard tab.
//   Added NavigateTo callback wiring for the Tax Invoice tab.
//   All other code UNCHANGED.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.Dashboard.DeliveryNote.DnDashboard;
using ProGlassAutomation.ViewModels.Dashboard.JobOrder.JoDashboard;
using ProGlassAutomation.ViewModels.Dashboard.TaxInvoice.TiDashboard;
using System;
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
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
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
                _ = _activePage.LoadAsync();
            }
        }

        // ── sub-page VMs ──────────────────────────────────────────────────
        public OverviewDashboardViewModel OverviewVM { get; }
        public SheetStoreDashboardViewModel SheetStoreVM { get; }
        public PiDashboardViewModel ProformaVM { get; }
        public JoDashboardViewModel JobOrderVM { get; }
        public DnDashboardViewModel DeliveryNoteVM { get; }
        public TiDashboardViewModel TaxInvoiceVM { get; }   // ← Chat 15 NEW

        // ── commands ──────────────────────────────────────────────────────
        public ICommand SelectTabCommand { get; }

        // ── navigation callbacks — set by MainViewModel after construction ─

        /// <summary>
        /// Wired by MainViewModel: target => Navigate(target).
        /// Passed down to OverviewVM AND TiDashboardVM so quick action
        /// buttons on both tabs can navigate to other modules.
        /// </summary>
        public Action<string>? NavigateTo
        {
            set
            {
                OverviewVM.NavigateTo = value;
                TaxInvoiceVM.NavigateTo = value;   // ← Chat 15: wire TI tab too
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
            TaxInvoiceVM = new TiDashboardViewModel();       // ← Chat 15 NEW

            Pages = new List<DashboardPageBase>
            {
                OverviewVM,
                SheetStoreVM,
                ProformaVM,
                JobOrderVM,
                DeliveryNoteVM,
                TaxInvoiceVM                                   // ← Chat 15 NEW
            };

            SelectTabCommand = new RelayCommand(p =>
            {
                if (p is DashboardPageBase page)
                    ActivePage = page;
            });

            // Default tab — set backing field directly to avoid double load
            _activePage = OverviewVM;
            _ = OverviewVM.LoadAsync();
        }

        /// <summary>
        /// Called by DashboardView.xaml.cs OnLoaded.
        /// Refreshes the active page every time the user navigates to Dashboard.
        /// </summary>
        public void OnViewLoaded()
        {
            _ = ActivePage.LoadAsync();
        }
    }
}
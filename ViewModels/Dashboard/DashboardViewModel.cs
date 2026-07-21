using ProGlassAutomation.Commands;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Shell ViewModel for the multi-page Dashboard.
    /// Owns the tab list and the currently active sub-page.
    /// Lazy-loads each sub-page on first tab selection via ActivePage setter.
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
                // trigger LoadAsync on the newly selected page
                _ = _activePage.LoadAsync();
            }
        }

        // ── sub-page VMs ──────────────────────────────────────────────────
        public OverviewDashboardViewModel OverviewVM { get; }
        public SheetStoreDashboardViewModel SheetStoreVM { get; }
        public PiDashboardViewModel ProformaVM { get; }

        // ── commands ──────────────────────────────────────────────────────
        public ICommand SelectTabCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public DashboardViewModel()
        {
            OverviewVM = new OverviewDashboardViewModel();
            SheetStoreVM = new SheetStoreDashboardViewModel();
            ProformaVM = new PiDashboardViewModel();

            Pages = new List<DashboardPageBase>
            {
                OverviewVM,
                SheetStoreVM,
                ProformaVM
            };

            SelectTabCommand = new RelayCommand(p =>
            {
                if (p is DashboardPageBase page)
                    ActivePage = page;
            });

            // default tab — set backing field directly to avoid double load
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
// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/MainViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Main Shell (Chat 23 v3.1)
// VERSION: Chat 23 v3.1 — Extracted settings into SettingsViewModel
//
// CHANGES vs v3.0:
//   ✓ ADDED SettingsVM (new SettingsViewModel instance)
//   ✓ ADDED ShowSettingsCommand — navigates to Settings module
//     (matches ShowAboutCommand pattern)
//   ✓ ADDED "Settings" case in Navigate() switch
//   ✓ REMOVED IsSettingsOpen property (no longer a drawer)
//   ✓ REMOVED ToggleSettingsCommand (replaced by ShowSettingsCommand)
//   ✓ REMOVED IsDarkTheme property (moved to SettingsViewModel)
//   ✓ REMOVED IsAiEnabled property (moved to SettingsViewModel)
//   ✓ REMOVED AiApiKey property (moved to SettingsViewModel)
//   ✓ REMOVED AiTestStatus property (moved to SettingsViewModel)
//   ✓ REMOVED TestAiConnectionCommand (moved to SettingsViewModel)
//   ✓ REMOVED ToggleThemeCommand (was unused; theme toggled via
//     SettingsViewModel.IsDarkTheme setter directly)
//
// PRESERVED (zero regression):
//   • All 17 module VM properties
//   • CurrentViewModel property
//   • IsMenuOpen property
//   • ToggleMenuCommand
//   • NavigateCommand
//   • ShowAboutCommand
//   • Dashboard callbacks (NavigateTo, TriggerNewInvoice)
//   • Phase 10.5 JO→DN navigation
//   • Chat 14 dual-path DN→TaxInvoice navigation
//   • AboutVM instance
//   • AiChatVM instance
//   • Default landing on DashboardVM
//
// REGRESSION RISK: LOW
//   Settings extraction is clean — nothing outside MainViewModel
//   referenced the removed properties (only the right drawer XAML,
//   which is also removed in this delivery).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.AI;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
using ProGlassAutomation.ViewModels.Settings;
using ProGlassAutomation.ViewModels.SheetStore;
using ProGlassAutomation.ViewModels.TaxInvoice;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels
{
    /// <summary>
    /// Root view-model for the main shell window.
    /// Owns every module VM, manages the left-drawer menu state,
    /// and coordinates cross-module navigation callbacks.
    ///
    /// v3.1: Theme + AI settings extracted into <see cref="SettingsViewModel"/>.
    /// This VM no longer owns any settings state — it just navigates to
    /// the Settings module when the gear button is clicked.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        private bool _isMenuOpen;

        // ── modules ───────────────────────────────────────────────────────
        public DashboardViewModel DashboardVM { get; }
        public ProformaInvoiceViewModel ProformaInvoiceVM { get; }
        public AboutViewModel AboutVM { get; } = new AboutViewModel();

        // v3.1: NEW Settings module
        public SettingsViewModel SettingsVM { get; } = new SettingsViewModel();

        // ── AI chat ───────────────────────────────────────────────────────
        public AiChatViewModel AiChatVM { get; }

        // ── placeholder modules ───────────────────────────────────────────
        public SheetStoreViewModel SheetStoreVM { get; } = new SheetStoreViewModel();
        public JobOrderViewModel JobOrderVM { get; } = new JobOrderViewModel();
        public OptimizationViewModel OptimizationVM { get; } = new OptimizationViewModel();
        public CuttingViewModel CuttingVM { get; } = new CuttingViewModel();
        public HolesCutoutsViewModel HolesCutoutsVM { get; } = new HolesCutoutsViewModel();
        public PolishViewModel PolishVM { get; } = new PolishViewModel();
        public EdgeDeletionViewModel EdgeDeletionVM { get; } = new EdgeDeletionViewModel();
        public MitringViewModel MitringVM { get; } = new MitringViewModel();
        public TemperingViewModel TemperingVM { get; } = new TemperingViewModel();
        public PairingViewModel PairingVM { get; } = new PairingViewModel();
        public DoublingLaminationViewModel DoublingLaminationVM { get; } = new DoublingLaminationViewModel();
        public DeliveryNoteViewModel DeliveryNoteVM { get; } = new DeliveryNoteViewModel();
        public TaxInvoiceViewModel TaxInvoiceVM { get; } = new TaxInvoiceViewModel();
        public SalesReportViewModel SalesReportVM { get; } = new SalesReportViewModel();

        // ── current view ──────────────────────────────────────────────────
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { if (_currentViewModel == value) return; _currentViewModel = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// True while the left navigation drawer is visible.
        /// The dim overlay behind the drawer is bound to this property.
        /// </summary>
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set { if (_isMenuOpen == value) return; _isMenuOpen = value; OnPropertyChanged(); }
        }

        // ── commands ──────────────────────────────────────────────────────

        /// <summary>Toggles the left navigation drawer.</summary>
        public ICommand ToggleMenuCommand { get; }

        /// <summary>Navigates to a module by string key.</summary>
        public ICommand NavigateCommand { get; }

        /// <summary>Navigates to the About page.</summary>
        public ICommand ShowAboutCommand { get; }

        /// <summary>v3.1: Navigates to the Settings page (was toggle drawer).</summary>
        public ICommand ShowSettingsCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public MainViewModel()
        {
            // ── construct modules ─────────────────────────────────────────
            DashboardVM = new DashboardViewModel();
            ProformaInvoiceVM = new ProformaInvoiceViewModel();
            AiChatVM = new AiChatViewModel(App.Ai);

            // ── wire dashboard navigation callbacks ───────────────────────
            DashboardVM.NavigateTo = target =>
            {
                Navigate(target);
            };

            DashboardVM.TriggerNewInvoice = () =>
            {
                ProformaInvoiceVM.TriggerNewInvoice();
            };

            // ── Phase 10.5: Wire JO → DN navigation ───────────────────────
            JobOrderVM.OnRequestOpenDeliveryNote = async dnId =>
            {
                Navigate("DeliveryNote");
                await DeliveryNoteVM.OpenDeliveryNoteAsync(dnId);
            };

            // ══════════════════════════════════════════════════════════════
            //  CHAT 14 — DN → Tax Invoice navigation (TWO distinct paths)
            // ══════════════════════════════════════════════════════════════

            // Path 1: DN List "Master Invoice" on JO group row
            DeliveryNoteVM.OnRequestOpenTaxInvoiceForMasterFromProforma =
                async proformaId =>
                {
                    Navigate("TaxInvoice");
                    await TaxInvoiceVM.CreateFromProformaAsync(proformaId);
                };

            // Path 2: DN Detail "Convert to Tax Invoice" on history entry
            DeliveryNoteVM.OnRequestOpenTaxInvoiceForDeliveryNote =
                async deliveryNoteId =>
                {
                    Navigate("TaxInvoice");
                    await TaxInvoiceVM.CreateFromDeliveryNoteAsync(deliveryNoteId);
                };

            // ── commands ──────────────────────────────────────────────────
            ToggleMenuCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = !IsMenuOpen;
            });

            NavigateCommand = new RelayCommand(Navigate);

            ShowAboutCommand = new RelayCommand(_ =>
            {
                CurrentViewModel = AboutVM;
                IsMenuOpen = false;
            });

            // v3.1: Settings now navigates like About (was drawer toggle)
            ShowSettingsCommand = new RelayCommand(_ =>
            {
                CurrentViewModel = SettingsVM;
                IsMenuOpen = false;
            });

            // default landing page
            CurrentViewModel = DashboardVM;
        }

        // ── navigation ────────────────────────────────────────────────────
        private void Navigate(object parameter)
        {
            switch (parameter?.ToString())
            {
                case "Dashboard": CurrentViewModel = DashboardVM; break;
                case "SheetStore": CurrentViewModel = SheetStoreVM; break;
                case "ProformaInvoice": CurrentViewModel = ProformaInvoiceVM; break;
                case "JobOrder": CurrentViewModel = JobOrderVM; break;
                case "Optimization": CurrentViewModel = OptimizationVM; break;
                case "Cutting": CurrentViewModel = CuttingVM; break;
                case "HolesCutouts": CurrentViewModel = HolesCutoutsVM; break;
                case "Polish": CurrentViewModel = PolishVM; break;
                case "EdgeDeletion": CurrentViewModel = EdgeDeletionVM; break;
                case "Mitring": CurrentViewModel = MitringVM; break;
                case "Tempering": CurrentViewModel = TemperingVM; break;
                case "Pairing": CurrentViewModel = PairingVM; break;
                case "DoublingLamination": CurrentViewModel = DoublingLaminationVM; break;
                case "DeliveryNote": CurrentViewModel = DeliveryNoteVM; break;
                case "TaxInvoice": CurrentViewModel = TaxInvoiceVM; break;
                case "SalesReport": CurrentViewModel = SalesReportVM; break;
                case "About": CurrentViewModel = AboutVM; break;

                // v3.1: NEW Settings navigation
                case "Settings": CurrentViewModel = SettingsVM; break;
            }
            IsMenuOpen = false;
        }
    }
}
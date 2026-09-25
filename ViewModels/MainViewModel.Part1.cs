// ═══════════════════════════════════════════════════════════════════════
// FILE: MainViewModel.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.CompanyCenter;
using ProGlassAutomation.ViewModels.UserCenter;
using ProGlassAutomation.ViewModels.AI;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.Dashboard.DeliveryNote.DnDashboard;
using ProGlassAutomation.ViewModels.Dashboard.JobOrder.JoDashboard;
using ProGlassAutomation.ViewModels.Dashboard.Statement;
using ProGlassAutomation.ViewModels.Dashboard.TaxInvoice.TiDashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Inquiry;
using ProGlassAutomation.ViewModels.JobProfitability;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
using ProGlassAutomation.ViewModels.Reports.OrderReport;
using ProGlassAutomation.ViewModels.Reports.VatSubmission;
using ProGlassAutomation.ViewModels.Settings;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.ViewModels.Diagnostics;
using ProGlassAutomation.ViewModels.SheetStore;
using ProGlassAutomation.ViewModels.SystemCenter;
using ProGlassAutomation.ViewModels.TaxInvoice;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Dialogs;

namespace ProGlassAutomation.ViewModels
{
        public partial class MainViewModel : ViewModelBase, IDisposable
    {
            private readonly AppConfig _config = null!;
        private readonly IAiService _ai = null!;
        private readonly IConnectivityService _connectivity = null!;
        private readonly ISpeechToTextService _speechToText = null!;
        private readonly ITextToSpeechService _textToSpeech = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;
        private readonly IJobOrderRepository _jobs = null!;
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly ITaxInvoiceRepository _taxInvoices = null!;
        private readonly IMasterDataRepository _masterData = null!;
        private readonly IDialogService _dialogs = null!;

        private ViewModelBase _currentViewModel;

        // Phase 4: Named handlers for static events — detachable
        private EventHandler<bool>? _connectivityHandler;
        private EventHandler? _currentUserChangedHandler;
        private bool _disposed;

        // ── DI — injected services (Phase 5) ──────────────────────────
        private readonly IReportingDataService? _reportingService;
        private readonly IDashboardMetricsService? _dashboardMetrics;
        private readonly IDashboardFilterService? _dashboardFilter;
        private readonly IServiceProvider? _serviceProvider;

        // ── modules — now resolved via DI (Phase 5) ───────────────────
        public DashboardViewModel DashboardVM { get; }
        public InquiryListViewModel InquiryVM { get; }
        public ProformaInvoiceViewModel ProformaInvoiceVM { get; }
        private AboutViewModel? _lazyAboutVM;
        public AboutViewModel AboutVM => _lazyAboutVM ??= ResolveOrNew<AboutViewModel>();
        public SettingsViewModel SettingsVM { get; }
        private SystemCenterViewModel? _lazySystemCenterVM;
        public SystemCenterViewModel SystemCenterVM => _lazySystemCenterVM ??= ResolveOrNew<SystemCenterViewModel>();
        private DiagnosticsViewModel? _lazyDiagnosticsVM;
        public DiagnosticsViewModel DiagnosticsVM => _lazyDiagnosticsVM ??= ResolveOrNew<DiagnosticsViewModel>();
        public GlobalSearchViewModel GlobalSearchVM { get; }
        public AiChatViewModel AiChatVM { get; }
        public ProGlassAutomation.Agent.UI.AgentChatViewModel AgentChatVM { get; }

        private SheetStoreViewModel? _lazySheetStoreVM;
        private bool _sheetStoreLedgerHooked;
        public SheetStoreViewModel SheetStoreVM => _lazySheetStoreVM ??= ResolveOrNew<SheetStoreViewModel>();

        /// <summary>Specifications Center — catalogue of saved specs.</summary>
        private Specifications.SpecificationsCenterViewModel? _lazySpecificationsVM;
        public Specifications.SpecificationsCenterViewModel SpecificationsVM => _lazySpecificationsVM ??= ResolveOrNew<Specifications.SpecificationsCenterViewModel>();
        public JobOrderViewModel JobOrderVM { get; }
        private OptimizationViewModel? _lazyOptimizationVM;
        public OptimizationViewModel OptimizationVM => _lazyOptimizationVM ??= ResolveOrNew<OptimizationViewModel>();
        private CuttingViewModel? _lazyCuttingVM;
        public CuttingViewModel CuttingVM => _lazyCuttingVM ??= WireProductionCrossNav(ResolveOrNew<CuttingViewModel>());
        private CuttingWorkCenterViewModel? _lazyCuttingWorkCenterVM;
        public CuttingWorkCenterViewModel CuttingWorkCenterVM => _lazyCuttingWorkCenterVM ??= ResolveOrNew<CuttingWorkCenterViewModel>();
        private ProcessWorkCenterViewModel? _lazyProcessWorkCenterVM;
        public ProcessWorkCenterViewModel ProcessWorkCenterVM => _lazyProcessWorkCenterVM ??= ResolveOrNew<ProcessWorkCenterViewModel>();
        private ProductionControlCenterViewModel? _lazyProductionControlCenterVM;
        public ProductionControlCenterViewModel ProductionControlCenterVM => _lazyProductionControlCenterVM ??= ResolveOrNew<ProductionControlCenterViewModel>();
        private HolesCutoutsViewModel? _lazyHolesCutoutsVM;
        public HolesCutoutsViewModel HolesCutoutsVM => _lazyHolesCutoutsVM ??= WireProductionCrossNav(ResolveOrNew<HolesCutoutsViewModel>());
        private PolishViewModel? _lazyPolishVM;
        public PolishViewModel PolishVM => _lazyPolishVM ??= WireProductionCrossNav(ResolveOrNew<PolishViewModel>());
        private EdgeDeletionViewModel? _lazyEdgeDeletionVM;
        public EdgeDeletionViewModel EdgeDeletionVM => _lazyEdgeDeletionVM ??= WireProductionCrossNav(ResolveOrNew<EdgeDeletionViewModel>());
        private MitringViewModel? _lazyMitringVM;
        public MitringViewModel MitringVM => _lazyMitringVM ??= WireProductionCrossNav(ResolveOrNew<MitringViewModel>());
        private TemperingViewModel? _lazyTemperingVM;
        public TemperingViewModel TemperingVM => _lazyTemperingVM ??= WireProductionCrossNav(ResolveOrNew<TemperingViewModel>());
        private PairingViewModel? _lazyPairingVM;
        public PairingViewModel PairingVM => _lazyPairingVM ??= WireProductionCrossNav(ResolveOrNew<PairingViewModel>());
        private DoublingLaminationViewModel? _lazyDoublingLaminationVM;
        public DoublingLaminationViewModel DoublingLaminationVM => _lazyDoublingLaminationVM ??= WireProductionCrossNav(ResolveOrNew<DoublingLaminationViewModel>());

        /// <summary>
        /// Screens redesign (2026-09-11): every production stage module's
        /// detail panel gets OPEN JOB ORDER / OPEN PROFORMA INVOICE
        /// cross-navigation — same house pattern as Sales Report and
        /// Statement drill-downs. Wired lazily, on first VM creation, so
        /// the lazy-init design is preserved.
        /// </summary>
        private T WireProductionCrossNav<T>(T vm) where T : ProductionStageViewModelBase
        {
            // Pipeline chain bar: any stage chip opens its module so the
            // floor can follow work through the chain.
            vm.OnOpenStage = key => Navigate(key);

            vm.OnOpenJobOrderByNumber = async jobNo =>
            {
                if (string.IsNullOrWhiteSpace(jobNo) || _jobs == null) return;
                var jo = await _jobs.GetByJobOrderIdAsync(jobNo);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
            };
            vm.OnOpenProformaByNumber = async piNo =>
            {
                if (string.IsNullOrWhiteSpace(piNo) || _invoices == null) return;
                var pi = await _invoices.GetByProformaIdAsync(piNo);
                if (pi == null) return;
                Navigate("ProformaInvoice");
                await ProformaInvoiceVM.OpenSavedInvoiceAsync(pi.Id);
            };

            // Dispatch from production: create (or reopen) the Dispatching
            // delivery note for the Job Order — remaining balance only,
            // capped to the pieces production has finished — then open it.
            vm.OnCreateDeliveryNote = async jobNo =>
            {
                if (string.IsNullOrWhiteSpace(jobNo) || _jobs == null)
                    return "Pick a unit first.";

                var jo = await _jobs.GetByJobOrderIdAsync(jobNo);
                if (jo == null)
                    return $"Job Order '{jobNo}' was not found.";

                var result = await ProGlassAutomation.Services.DeliveryNote
                    .DeliveryNoteCreateService
                    .CreateOrOpenDispatchingAsync(jo);

                switch (result.Outcome)
                {
                    case ProGlassAutomation.Services.DeliveryNote.DnCreateResult.Kind.OpenExisting:
                    case ProGlassAutomation.Services.DeliveryNote.DnCreateResult.Kind.Created:
                        Navigate("DeliveryNote");
                        // auto-select the remaining-balance pieces so the
                        // note is ready to confirm (2026-09-14).
                        await DeliveryNoteVM.OpenDeliveryNoteAsync(result.DeliveryNoteDbId, autoSelectBalance: true);
                        return "";

                    case ProGlassAutomation.Services.DeliveryNote.DnCreateResult.Kind.FullyDelivered:
                        return result.Message;

                    default:
                        return string.IsNullOrWhiteSpace(result.Message)
                            ? "Failed to create delivery note."
                            : result.Message;
                }
            };
            return vm;
        }

        public DeliveryNoteViewModel DeliveryNoteVM { get; }
        public TaxInvoiceViewModel TaxInvoiceVM { get; }
        public SalesReportViewModel SalesReportVM { get; }
        public StatementModuleViewModel StatementModuleVM { get; }
        private OrderReportModuleViewModel? _lazyOrderReportModuleVM;
        public OrderReportModuleViewModel OrderReportModuleVM =>
            _lazyOrderReportModuleVM ??= ResolveOrNew<OrderReportModuleViewModel>();
        private JobProfitabilityViewModel? _lazyJobProfitabilityVM;
        public JobProfitabilityViewModel JobProfitabilityVM => _lazyJobProfitabilityVM ??= ResolveOrNew<JobProfitabilityViewModel>();
        private CompanyCenterViewModel? _lazyCompanyCenterVM;
        public CompanyCenterViewModel CompanyCenterVM => _lazyCompanyCenterVM ??= ResolveOrNew<CompanyCenterViewModel>();
        private UserCenterViewModel? _lazyUserCenterVM;
        public UserCenterViewModel UserCenterVM => _lazyUserCenterVM ??= ResolveOrNew<UserCenterViewModel>();
        private VatSubmissionViewModel? _lazyVatSubmissionVM;
        public VatSubmissionViewModel VatSubmissionVM => _lazyVatSubmissionVM ??= ResolveOrNew<VatSubmissionViewModel>();

        // ── popups ────────────────────────────────────────────────────
        private bool _isAgentChatOpen;
        public bool IsAgentChatOpen
        {
            get => _isAgentChatOpen;
            set
            {
                if (_isAgentChatOpen == value) return;
                _isAgentChatOpen = value;
                OnPropertyChanged();
                // SENIOR FIX 2026-09-19: when Agent opens, close AI and other popups
                if (value)
                {
                    try { if (AiChatVM != null) AiChatVM.IsOpen = false; } catch { }
                    IsSettingsOpen = false;
                    IsNotificationsOpen = false;
                    IsScreenshotOpen = false;
                    IsUserSwitchOpen = false;
                    IsScreenRecordOpen = false;
                    IsStorageOpen = false;
                    IsShellMoreOpen = false;
                }
            }
        }

        private bool _isUserSwitchOpen;
        public bool IsUserSwitchOpen
        {
            get => _isUserSwitchOpen;
            set { if (_isUserSwitchOpen == value) return; _isUserSwitchOpen = value; OnPropertyChanged(); }
        }

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { if (_isSettingsOpen == value) return; _isSettingsOpen = value; OnPropertyChanged(); }
        }

        private bool _isNotificationsOpen;
        public bool IsNotificationsOpen
        {
            get => _isNotificationsOpen;
            set { if (_isNotificationsOpen == value) return; _isNotificationsOpen = value; OnPropertyChanged(); }
        }

        private bool _isScreenshotOpen;
        public bool IsScreenshotOpen
        {
            get => _isScreenshotOpen;
            set { if (_isScreenshotOpen == value) return; _isScreenshotOpen = value; OnPropertyChanged(); }
        }

        private bool _isScreenRecordOpen;
        public bool IsScreenRecordOpen
        {
            get => _isScreenRecordOpen;
            set { if (_isScreenRecordOpen == value) return; _isScreenRecordOpen = value; OnPropertyChanged(); }
        }

        // More holds Screenshot, Record, About and Experience so the
        // professional bar can stay one row. Opening it closes the other
        // shell popups; closing it does not reopen them.
        private bool _isShellMoreOpen;
        public bool IsShellMoreOpen
        {
            get => _isShellMoreOpen;
            set
            {
                if (_isShellMoreOpen == value) return;
                _isShellMoreOpen = value;
                OnPropertyChanged();
                if (!value) return;
                IsSettingsOpen = false;
                IsNotificationsOpen = false;
                IsScreenshotOpen = false;
                IsUserSwitchOpen = false;
                IsScreenRecordOpen = false;
                IsStorageOpen = false;
                IsAgentChatOpen = false;
                try { if (AiChatVM != null) AiChatVM.IsOpen = false; } catch { }
            }
        }

        public ScreenRecordPanelViewModel ScreenRecordVM => ScreenRecordPanelViewModel.Shared;

        public ICommand ToggleSettingsCommand { get; private set; }
        public ICommand ToggleNotificationsCommand { get; private set; }
        public ICommand ToggleAgentChatCommand { get; private set; }

        // ── current view ──────────────────────────────────────────────
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel == value) return;
                _currentViewModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentModuleTitle));
                OnPropertyChanged(nameof(CurrentModuleHeaderTitle));
                OnPropertyChanged(nameof(CurrentModuleSubText));
                UpdateAiScreenContext();
                // GB-P14: republish the experience-scoped content hosts
                // (ProfessionalContent/PersonalContent) so module
                // navigation follows the active experience. No-op unless
                // the Personal UI switch is in play.
                UpdateExperienceContent();
            }
        }

        public string CurrentModuleTitle
        {
            get
            {
                var raw = _currentViewModel?.GetType().Name.Replace("ViewModel", "") ?? "";
                var pretty = PrettyModuleName(raw);
                return string.IsNullOrWhiteSpace(pretty) ? "MODULE" : pretty.ToUpperInvariant();
            }
        }

        public string CurrentModuleHeaderTitle
        {
            get
            {
                if (_currentViewModel is DashboardViewModel db)
                {
                    if (db.ActivePage is OverviewDashboardViewModel) return "Plant Operations Overview";
                    if (db.ActivePage is SheetStoreDashboardViewModel) return "Sheet Store Warehouse";
                    if (db.ActivePage is PiDashboardViewModel) return "Proforma Invoice Ledger";
                    if (db.ActivePage is JoDashboardViewModel) return "Job Order Production";
                    if (db.ActivePage is DnDashboardViewModel) return "Delivery Note Logistics";
                    if (db.ActivePage is TiDashboardViewModel) return "Tax Invoice Billing";
                    if (db.ActivePage is StDashboardViewModel) return "Account Statements";
                    return "Plant Operations Overview";
                }
                if (_currentViewModel is ProformaInvoiceViewModel) return "Proforma Invoice Operations";
                if (_currentViewModel is InquiryListViewModel) return "Inquiry Management";
                if (_currentViewModel is JobOrderViewModel) return "Job Order Operations";
                if (_currentViewModel is DeliveryNoteViewModel) return "Delivery Note Logistics";
                if (_currentViewModel is TaxInvoiceViewModel) return "Tax Invoice Accounting";
                if (_currentViewModel is SheetStoreViewModel) return "Sheet Store Warehouse";
                if (_currentViewModel is ProGlassAutomation.ViewModels.Specifications.SpecificationsCenterViewModel) return "Glass Specifications";
                if (_currentViewModel is OptimizationViewModel) return "Glass Cutting Optimization";
                if (_currentViewModel is ProductionControlCenterViewModel || _currentViewModel is ProductionStageViewModelBase) return "Factory Floor Control";
                if (_currentViewModel is SalesReportViewModel) return "Executive Reports & Analytics";
                if (_currentViewModel is StatementModuleViewModel) return "Customer Statement Module";
                if (_currentViewModel is SettingsViewModel) return "System Configuration";
                if (_currentViewModel is AboutViewModel) return "About ProGlass Automation";
                if (_currentViewModel is SystemCenterViewModel) return "System Diagnostic Center";
                if (_currentViewModel is CompanyCenterViewModel) return "Company Profile & Identity";
                if (_currentViewModel is UserCenterViewModel) return "User Management Center";

                var raw = _currentViewModel?.GetType().Name.Replace("ViewModel", "") ?? "";
                var pretty = PrettyModuleName(raw);
                return string.IsNullOrWhiteSpace(pretty) ? "Plant Operations Overview" : pretty;
            }
        }

        public string CurrentModuleSubText
        {
            get
            {
                if (_currentViewModel is DashboardViewModel db)
                {
                    if (db.ActivePage is OverviewDashboardViewModel) return "Real-time production metrics, live dispatch schedules & glass inventory yield";
                    if (db.ActivePage is SheetStoreDashboardViewModel) return "Stock levels, inventory valuation & glass pack movements";
                    if (db.ActivePage is PiDashboardViewModel) return "Commercial quotations, conversion pipeline & pricing schedules";
                    if (db.ActivePage is JoDashboardViewModel) return "Live shop floor routing, machine work centers & tempering queue";
                    if (db.ActivePage is DnDashboardViewModel) return "Gate passes, site deliveries & driver dispatch schedules";
                    if (db.ActivePage is TiDashboardViewModel) return "FTA VAT 5% compliant invoices, collections & revenue ledger";
                    if (db.ActivePage is StDashboardViewModel) return "Client balances, ageing analysis & payment reconciliation";
                    return "Real-time production metrics, live dispatch schedules & glass inventory yield";
                }
                if (_currentViewModel is ProformaInvoiceViewModel) return "Commercial glass cutting, tempering, laminating & billing schedules";
                if (_currentViewModel is InquiryListViewModel) return "Customer inquiries, architectural specifications & quotation intake";
                if (_currentViewModel is JobOrderViewModel) return "Shop floor routing, cutting schedules & work center queue";
                if (_currentViewModel is DeliveryNoteViewModel) return "Fleet dispatch, site deliveries & customer proof of receipt";
                if (_currentViewModel is TaxInvoiceViewModel) return "FTA VAT 5% compliant invoicing, settlements & ledger";
                if (_currentViewModel is SheetStoreViewModel) return "Raw glass inventory, stock replenishment & yield analysis";
                if (_currentViewModel is ProGlassAutomation.ViewModels.Specifications.SpecificationsCenterViewModel) return "Technical glass definitions, tints & processing rules";
                if (_currentViewModel is OptimizationViewModel) return "2D Nesting algorithms, scrap minimization & sheet yield";
                if (_currentViewModel is ProductionControlCenterViewModel || _currentViewModel is ProductionStageViewModelBase) return "Work center scheduling, tempering furnaces & autoclave lines";
                if (_currentViewModel is SalesReportViewModel) return "Executive summaries, order book analysis & financial statements";
                if (_currentViewModel is StatementModuleViewModel) return "Client ledger statements, balance summaries & credit audits";
                if (_currentViewModel is SettingsViewModel) return "Personalization, localization, storage & preferences";
                if (_currentViewModel is AboutViewModel) return "Platform telemetry, version status & system diagnostics";
                if (_currentViewModel is SystemCenterViewModel) return "System logs, database health & engine diagnostics";
                if (_currentViewModel is CompanyCenterViewModel) return "Corporate details, tax registration & branch information";
                if (_currentViewModel is UserCenterViewModel) return "Role-based access control, operator profiles & credentials";

                return "Integrated Enterprise Glass Processing & Automation Platform";
            }
        }

        // ── active navigation key ──────────────────────────────────────
        // Matches each sidebar button's CommandParameter, so the sidebar can
        // highlight the currently open module (active indicator).
        // Concept 30 PORCELAIN FLOW: "Proforma Invoice" is the primary selected row.
        private string _currentModuleKey = "ProformaInvoice";
        public string CurrentModuleKey
        {
            get => _currentModuleKey;
            private set
            {
                if (_currentModuleKey == value) return;
                _currentModuleKey = value;
                OnPropertyChanged();
            }
        }


        private void UpdateAiScreenContext()
        {
            try
            {
                var raw = _currentViewModel?.GetType().Name.Replace("ViewModel", "") ?? "";
                string detail = "";
                if (_currentViewModel is ProGlassAutomation.Agent.Context.IAgentContextAware aware)
                {
                    // Per-VM best-effort probe. A third-party VM that
                    // throws on AgentContextDetail is non-fatal — the AI
                    // context just gets an empty detail — but we log at
                    // Debug so the developer can find the offender
                    // (audit S-5: 1 real candidate fixed in this slice).
                    try { detail = aware.AgentContextDetail ?? ""; }
                    catch (Exception probeEx)
                    {
                        AppLogService.Debug(
                            $"[Main] AgentContextDetail probe failed for " +
                            $"{_currentViewModel?.GetType().Name}: {probeEx.Message}",
                            new LogContext { Module = "Main", Operation = "UpdateAiScreenContext" });
                        detail = "";
                    }
                }
                ProGlassAutomation.Services.AI.AIContext.AiScreenContext.Set(PrettyModuleName(raw), detail);
                ProGlassAutomation.Agent.Context.AgentScreenContext.CurrentViewModelProvider = () => _currentViewModel;
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] AI screen-context update failed: " + ExceptionMessages.Deep(ex)); }
        }

        private static string PrettyModuleName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new System.Text.StringBuilder(raw.Length + 4);
            foreach (var ch in raw)
            {
                if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString();
        }

        private bool _isMenuOpen = true;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set { if (_isMenuOpen == value) return; _isMenuOpen = value; OnPropertyChanged(); }
        }

        private bool _isOnline = true;
        public bool IsOnline
        {
            get => _isOnline;
            private set
            {
                if (_isOnline == value) return;
                _isOnline = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectivityLabel));
            }
        }

        public string ConnectivityLabel => IsOnline ? "Online" : "Offline";

        private string _currentUserDisplayName = "";
        public string CurrentUserDisplayName
        {
            get => _currentUserDisplayName;
            private set
            {
                if (_currentUserDisplayName == value) return;
                _currentUserDisplayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentUserInitials));
            }
        }

        private string _currentUserRole = "";
        public string CurrentUserRole
        {
            get => _currentUserRole;
            private set { if (_currentUserRole == value) return; _currentUserRole = value; OnPropertyChanged(); }
        }

        public string CurrentUserInitials
        {
            get
            {
                var name = _currentUserDisplayName?.Trim() ?? "";
                if (name.Length == 0) return "?";
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return parts.Length switch
                {
                    0 => "?",
                    1 => parts[0].Substring(0, 1).ToUpperInvariant(),
                    _ => (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant()
                };
            }
        }

        private string _storageDisplay = "";
        public string StorageDisplay
        {
            get => _storageDisplay;
            private set { if (_storageDisplay == value) return; _storageDisplay = value; OnPropertyChanged(); }
        }

        private string _storageSubtitle = "";
        public string StorageSubtitle
        {
            get => _storageSubtitle;
            private set { if (_storageSubtitle == value) return; _storageSubtitle = value; OnPropertyChanged(); }
        }

        private string _currentDateText = "";
        public string CurrentDateText
        {
            get => _currentDateText;
            private set { if (_currentDateText == value) return; _currentDateText = value; OnPropertyChanged(); }
        }

        private string _currentTimeText = "";
        public string CurrentTimeText
        {
            get => _currentTimeText;
            private set { if (_currentTimeText == value) return; _currentTimeText = value; OnPropertyChanged(); }
        }

        private System.Windows.Threading.DispatcherTimer? _clockTimer;

        // Phase 2 H-3 fix: Only update time when seconds change, date only
        // on day change. Previously every 1-second tick fired TWO property
        // changes (date + time) regardless of whether the values actually
        // changed, causing unnecessary binding re-evaluation in the top bar.
        private string _lastDateStr = "";

        private void StartClock()
        {
            UpdateClock();
            _clockTimer = AsyncUtility.CreateDispatcherTimerWithHandler(
                TimeSpan.FromSeconds(1),
                (_, __) => UpdateClock(),
                "MainViewModel Clock");
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            // Exact format for Concept 30 Porcelain Flow: "dd MMM yyyy / HH:mm"
            var timeStr = now.ToString("HH:mm");
            if (timeStr != _currentTimeText)
                CurrentTimeText = timeStr;

            var dateStr = now.ToString("dd MMM yyyy");
            if (dateStr != _lastDateStr)
            {
                _lastDateStr = dateStr;
                CurrentDateText = dateStr;
            }
        }

        public void RefreshCurrentUserPublic() => RefreshCurrentUser();

        public void OpenSettingsPage(string page)
        {
            CurrentModuleKey = "Settings";
            CurrentViewModel = SettingsVM;

            // ERR-BEFC44 / ERR-435233: this line had TWO unguarded
            // dereferences. SettingsVM comes from ResolveOrNew<T>(), which
            // returns null! if both DI and Activator fail; and
            // SwitchSettingsPageCommand is only non-null once InitializeCore()
            // has run. Navigation must never crash the shell, so fall back to
            // setting the page directly if the command is unavailable.
            var vm = SettingsVM;
            if (vm != null)
            {
                var target = string.IsNullOrWhiteSpace(page) ? "Appearance" : page;
                var cmd = vm.SwitchSettingsPageCommand;
                if (cmd != null && cmd.CanExecute(target))
                    cmd.Execute(target);
                else
                    vm.GoToPage(target);        // same effect as the command
            }
            else
            {
                Services.Logging.AppLogService.Error(
                    "OpenSettingsPage: SettingsVM is null — Settings cannot open. "
                    + "SettingsViewModel failed to construct via both DI and Activator.");
            }

            IsSettingsOpen = false;
            IsMenuOpen = false;
        }

        private void RefreshCurrentUser()
        {
            var user = Services.Audit.AuditService.CurrentUser;
            CurrentUserDisplayName = string.IsNullOrWhiteSpace(user?.DisplayName) ? Services.Audit.AuditService.CurrentUsername : user!.DisplayName!;
            CurrentUserRole = PrettyRole(user?.Role);
        }

        private static string PrettyRole(string? role) => role switch
        {
            "Admin" => "Administrator",
            "Manager" => "Manager",
            "Operator" => "Operator",
            "Store" => "Store",
            _ => string.IsNullOrWhiteSpace(role) ? "User" : role
        };

        private System.Collections.ObjectModel.ObservableCollection<ProGlassAutomation.Services.Storage.StorageBreakdownItem>? _storageItems;
        /// <summary>Per-category storage rows for the storage popup.</summary>
        public System.Collections.ObjectModel.ObservableCollection<ProGlassAutomation.Services.Storage.StorageBreakdownItem> StorageItems =>
            _storageItems ??= new System.Collections.ObjectModel.ObservableCollection<ProGlassAutomation.Services.Storage.StorageBreakdownItem>();

        private bool _isStorageOpen;
        /// <summary>Storage details popup (click-outside guarded, like the top-bar popups).</summary>
        public bool IsStorageOpen
        {
            get => _isStorageOpen;
            set { if (_isStorageOpen == value) return; _isStorageOpen = value; OnPropertyChanged(); }
        }

        private bool _isStorageRefreshing;
        public bool IsStorageRefreshing
        {
            get => _isStorageRefreshing;
            private set { if (_isStorageRefreshing == value) return; _isStorageRefreshing = value; OnPropertyChanged(); }
        }

        private string _storageTotalDisplay = "";
        public string StorageTotalDisplay
        {
            get => _storageTotalDisplay;
            private set { if (_storageTotalDisplay == value) return; _storageTotalDisplay = value; OnPropertyChanged(); }
        }

        private string _storageCountDisplay = "";
        public string StorageCountDisplay
        {
            get => _storageCountDisplay;
            private set { if (_storageCountDisplay == value) return; _storageCountDisplay = value; OnPropertyChanged(); }
        }

        public ICommand? StorageCardCommand { get; private set; }
        public ICommand? RefreshStorageCommand { get; private set; }

        /// <summary>
        /// Toggles the storage popup and refreshes the breakdown whenever
        /// it opens (async — the folder walk runs on a background thread).
        /// </summary>
        private void ToggleStoragePopup()
        {
            IsStorageOpen = !IsStorageOpen;
            if (IsStorageOpen)
                RefreshStorage();
        }

        /// <summary>
        /// Rebuilds the storage breakdown on a background thread and
        /// publishes results on the UI thread. Never blocks the UI —
        /// the previous implementation walked the whole app-data folder
        /// synchronously on the UI thread (a startup freeze on machines
        /// with large databases/backups).
        /// </summary>
        public void RefreshStorage()
        {
            if (IsStorageRefreshing) return;
            IsStorageRefreshing = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var items = ProGlassAutomation.Services.Storage.StorageBreakdownService.ComputeBreakdown();
                    long total = 0;
                    foreach (var i in items) total += i.Bytes;

                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher == null) return;

                    // Phase 1 C-1 fix: non-blocking dispatch for storage refresh
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        StorageItems.Clear();
                        var loc = Services.Localization.LocalizationService.Instance;
                        foreach (var item in items)
                        {
                            item.DisplayName = loc.T(item.DisplayNameKey);
                            StorageItems.Add(item);
                        }

                        StorageDisplay = ProGlassAutomation.Services.Storage.StorageBreakdownService.FormatSize(total);
                        StorageTotalDisplay = StorageDisplay;
                        StorageCountDisplay = items.Count.ToString();
                        StorageSubtitle = Services.Localization.LocalizationService.Instance.T("Storage.StoredLocally",
                            Services.Localization.LocalizationService.Instance.CurrentLanguage.Code)
                            .Replace("{0}", StorageDisplay);
                        IsStorageRefreshing = false;
                    }));
                }
                catch (System.Exception ex)
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        "[MainViewModel] Storage breakdown failed: " + ExceptionMessages.Deep(ex));
                    DispatchToUi(() =>
                    {
                        IsStorageRefreshing = false;
                        StorageDisplay = "—";
                        StorageSubtitle = Services.Localization.LocalizationService.Instance
                            .T("Storage.Unavailable");
                        StorageTotalDisplay = "—";
                    });
                }
            });
        }

        // Phase 1 C-1 fix: non-blocking dispatch — was blocking Invoke
        private static void DispatchToUi(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            if (dispatcher.CheckAccess()) action();
            else dispatcher.BeginInvoke(action);
        }

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            private set { if (_isDarkTheme == value) return; _isDarkTheme = value; OnPropertyChanged(); }
        }

        public ICommand ToggleThemeCommand { get; private set; }
        public ICommand TogglePopupCommand { get; private set; }
        public ICommand LogoClickCommand { get; private set; }
        public ICommand CloseAllPopupsCommand { get; private set; }
        public ICommand OpenSettingsPageCommand { get; private set; }
        public ICommand ToggleMenuCommand { get; }
        public ICommand NewProformaInvoiceCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ShowUserSwitchCommand { get; }

        /// <summary>
        /// Opens Settings and selects the License page. Bound to the
        /// "Activate license" button on the read-only banner so a
        /// single click takes the user from anywhere in the app to
        /// the activation surface — no manual nav-rail hunting.
        /// Assigned in the constructor alongside the other Show*
        /// commands, so it is guaranteed non-null for the lifetime
        /// of any bound view.
        /// </summary>
        public ICommand OpenLicenseSettingsCommand { get; }

        /// <summary>
        /// Signs the current operator out, clears the "Skip login until
        /// update" DPAPI credentials, and asks the shell to restart the
        /// splash / login flow. Bound to a menu affordance in the shell
        /// so a shop-floor terminal can be handed off cleanly at shift
        /// change. See <see cref="AuthSessionService.SignOutAsync"/> for
        /// the exact cleanup sequence.
        /// </summary>
        public ICommand SignOutCommand { get; }

        /// <summary>
        /// Revokes the auto-sign-in credentials for this device without
        /// signing out the current session. Bound to a "Forget this
        /// device" affordance in Settings so a user who armed the pill
        /// by accident can revoke it without ending their work.
        /// </summary>
        public ICommand ForgetThisDeviceCommand { get; }

        // ── Parameterless ctor for XAML backward compat — uses App.ServiceProvider fallback ──
        public MainViewModel() : this(App.ServiceProvider ?? Services.DependencyInjection.ServiceRegistration.BuildProGlassProvider())
        {
        }

        // ── DI ctor — Phase 5: Full DI, no new ViewModel() ──────────────────
        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _config = serviceProvider.GetService<AppConfig>() ?? App.Config;
            _ai = serviceProvider.GetService<IAiService>() ?? App.Ai;
            _connectivity = serviceProvider.GetService<IConnectivityService>() ?? App.Connectivity;
            _speechToText = serviceProvider.GetService<ISpeechToTextService>() ?? App.SpeechToText;
            _textToSpeech = serviceProvider.GetService<ITextToSpeechService>() ?? App.TextToSpeech;
            _invoices = serviceProvider.GetService<IProformaInvoiceRepository>() ?? App.Invoices;
            _jobs = serviceProvider.GetService<IJobOrderRepository>() ?? App.Jobs;
            _deliveryNotes = serviceProvider.GetService<IDeliveryNoteRepository>() ?? App.DeliveryNotes;
            _taxInvoices = serviceProvider.GetService<ITaxInvoiceRepository>() ?? App.TaxInvoices;
            _masterData = serviceProvider.GetService<IMasterDataRepository>() ?? App.MasterData;
            _dialogs = serviceProvider.GetService<IDialogService>() ?? App.Dialogs;

            // Try resolve reporting services via DI, fallback to App statics
            try { _reportingService = serviceProvider.GetService<IReportingDataService>(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] DI resolve failed for IReportingDataService; using App statics: " + ExceptionMessages.Deep(ex)); }
            try { _dashboardMetrics = serviceProvider.GetService<IDashboardMetricsService>(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] DI resolve failed for IDashboardMetricsService; using App statics: " + ExceptionMessages.Deep(ex)); }
            try { _dashboardFilter = serviceProvider.GetService<IDashboardFilterService>(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] DI resolve failed for IDashboardFilterService; using App statics: " + ExceptionMessages.Deep(ex)); }

            // Resolve all module VMs via DI — Phase 5: no `new ViewModel()` 
            //
            // LAZY vs EAGER (Phase 4, 2026-09-02)
            // Constructing all 28 module view models here cost the user a
            // slow, unresponsive startup: most of these constructors kick
            // off database loads, so the shell could not paint until every
            // module had queried. Modules are now built ON FIRST USE via
            // `_lazyX ??= ResolveOrNew<X>()` properties.
            //
            // The ones still resolved eagerly below are the ones this
            // constructor WIRES UP afterwards (cross-module callbacks such
            // as InquiryVM.OpenProformaForRowRequested). Touching their
            // property here would instantiate them anyway, so declaring
            // them lazy would be a lie that only added indirection.
            // Verified per-property by counting `X.` member access in this
            // file rather than assumed.
            // Each GetService falls back to `new` if not registered (migration safety)
            DashboardVM = ResolveOrNew<DashboardViewModel>();
            DashboardVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DashboardVM.ActivePage))
                {
                    OnPropertyChanged(nameof(CurrentModuleHeaderTitle));
                    OnPropertyChanged(nameof(CurrentModuleSubText));
                }
            };
            InquiryVM = ResolveOrNew<InquiryListViewModel>();
            ProformaInvoiceVM = ResolveOrNew<ProformaInvoiceViewModel>();
            SettingsVM = ResolveOrNew<SettingsViewModel>();
            JobOrderVM = ResolveOrNew<JobOrderViewModel>();
            DeliveryNoteVM = ResolveOrNew<DeliveryNoteViewModel>();
            TaxInvoiceVM = ResolveOrNew<TaxInvoiceViewModel>();
            SalesReportVM = ResolveOrNew<SalesReportViewModel>();
            StatementModuleVM = ResolveOrNew<StatementModuleViewModel>();
            // Report Generator is lazy (first navigation resolves it) — no
            // cross-module callbacks need wiring at shell construction.

            // AI chat — may need voice services from App
            try
            {
                AiChatVM = serviceProvider.GetService<ViewModels.AI.AiChatViewModel>()
                    ?? new ViewModels.AI.AiChatViewModel(_config, _connectivity, _ai, _speechToText, _textToSpeech);
            }
            catch
            {
                AiChatVM = new ViewModels.AI.AiChatViewModel(_config, _connectivity, _ai, _speechToText, _textToSpeech);
            }

            try
            {
                AgentChatVM = serviceProvider.GetService<ProGlassAutomation.Agent.UI.AgentChatViewModel>()
                    ?? new ProGlassAutomation.Agent.UI.AgentChatViewModel();
            }
            catch
            {
                AgentChatVM = new ProGlassAutomation.Agent.UI.AgentChatViewModel();
            }

            // SENIOR FIX 2026-09-19: Agent chat toggle now closes all
            // other popups and the ProGlass AI chat to prevent overlap
            // (ZIndex 200 vs 210). Previously both chats could stay open
            // simultaneously, and opening Agent left Notifications/Settings
            // popups underneath. Only top-panel buttons may open AI surfaces.
            ToggleAgentChatCommand = new RelayCommand(_ =>
            {
                bool willOpen = !IsAgentChatOpen;
                if (willOpen)
                {
                    // Close everything else before opening Agent
                    IsSettingsOpen = false;
                    IsNotificationsOpen = false;
                    IsScreenshotOpen = false;
                    IsUserSwitchOpen = false;
                    IsScreenRecordOpen = false;
                    IsStorageOpen = false;
                    IsShellMoreOpen = false;
                    try { if (AiChatVM != null) AiChatVM.IsOpen = false; } catch { }
                }
                IsAgentChatOpen = willOpen;
            });

            // SENIOR FIX 2026-09-19: enforce closed-by-default for AI chats.
            // IsOpen defaults false in VM, but we harden here against any
            // future init that might flip it, and wire mutual exclusivity:
            // when ProGlass AI opens, Agent closes, and vice versa, plus
            // all other popups close to prevent overlap.
            try { if (AiChatVM != null) AiChatVM.IsOpen = false; } catch { }
            IsAgentChatOpen = false;

            try
            {
                if (AiChatVM != null)
                {
                    AiChatVM.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(AiChatVM.IsOpen) && AiChatVM.IsOpen)
                        {
                            // ProGlass AI opened — close Agent + other popups
                            IsAgentChatOpen = false;
                            IsSettingsOpen = false;
                            IsNotificationsOpen = false;
                            IsScreenshotOpen = false;
                            IsUserSwitchOpen = false;
                            IsScreenRecordOpen = false;
                            IsStorageOpen = false;
                            IsShellMoreOpen = false;
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[MainViewModel] AiChatVM PropertyChanged wiring failed: " + ProGlassAutomation.Utilities.ExceptionMessages.Deep(ex));
            }

            IsDarkTheme = ProGlassAutomation.Services.ThemeManager.CurrentTheme == ProGlassAutomation.Services.AppTheme.Dark;
            SettingsVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDarkTheme))
                    IsDarkTheme = SettingsVM.IsDarkTheme;
            };
            ToggleThemeCommand = new RelayCommand(_ => SettingsVM.IsDarkTheme = !SettingsVM.IsDarkTheme);
            TogglePopupCommand = new RelayCommand(p => TogglePopup(p as string));
            StorageCardCommand = new RelayCommand(_ => ToggleStoragePopup());
            RefreshStorageCommand = new RelayCommand(_ => RefreshStorage());
            LogoClickCommand = new RelayCommand(_ => { CloseAllPopups(); NavigateFromAgent("About"); });
            CloseAllPopupsCommand = new RelayCommand(_ => { IsMenuOpen = false; CloseAllPopups(); });
            OpenSettingsPageCommand = new RelayCommand(p => OpenSettingsPage(p as string ?? ""));

            GlobalSearchVM = new GlobalSearchViewModel(_deliveryNotes, _invoices, _jobs, _masterData, _taxInvoices, target => Navigate(target));

            DashboardVM.NavigateTo = target => Navigate(target);
            DashboardVM.TriggerNewInvoice = () => ProformaInvoiceVM.TriggerNewInvoice();
            InquiryVM.CreateProformaRequested = async inquiry =>
            {
                // The user picked (or created) an inquiry. Clear the
                // PI module's "awaiting inquiry" banner — the host
                // navigates back to the PI module below, and we want
                // the wizard to land cleanly without the stale
                // "Waiting for inquiry pick" indicator.
                ProformaInvoiceVM.CompleteInquirySelectionForNewPi();
                await ProformaInvoiceVM.StartFromInquiryAsync(inquiry);
                Navigate("ProformaInvoice");
            };

            // 3-way prompt for "new PI" entry points. Every caller
            // (sidebar +, dashboard, in-list button) goes through
            // ProformaInvoiceVM.TriggerNewInvoice / NewInvoiceCommand,
            // which call this callback. The MessageBox lives here so
            // the VM stays UI-free; the VM only sees the choice.
            // Uses fully-qualified System.Threading.Tasks.Task so the
            // build is robust against missing `using` directives in
            // any IDE/SDK combination — the property type is
            // Func<Task<NewProformaStartChoice>> and the lambda body
            // returns the same.
            ProformaInvoiceVM.AskStartFromInquiryRequested = () =>
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null || !dispatcher.CheckAccess())
                {
                    // We must show a MessageBox on the UI thread.
                    return dispatcher?.InvokeAsync(() => PromptNewPiStart())
                        .Task ?? System.Threading.Tasks.Task.FromResult(
                            ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Skip);
                }
                return System.Threading.Tasks.Task.FromResult(PromptNewPiStart());
            };

            // Explicit "new PI from inquiry" flow. We set the
            // "awaiting" flag on the inquiry module so the user
            // sees a banner telling them why they were sent here
            // and what to do next (pick a Confirmed inquiry or
            // create one). Then we navigate; the inquiry module
            // clears the flag when the user picks a row
            // (CreateProformaRequested fires) or cancels.
            ProformaInvoiceVM.NavigateToInquiryForNewPiRequested = () =>
            {
                InquiryVM.IsAwaitingNewPiSelection = true;
                Navigate("Inquiry");
            };

            // 2026-09-10: Step 4 "Open Sheet Store — Add Sheet" button.
            // License gate runs inside Navigate, same as every rail item.
            // Step 4's button says "Open Sheet Store — Add Sheet", so it has to
            // arrive with the add-sheet workspace open. Navigating alone lands on
            // the module's list — the operator clicks, the page changes, and
            // nothing they asked for appears. Navigate first (it applies the
            // license gate and creates the VM), then open the workspace.
            ProformaInvoiceVM.Step4VM.NavigateToSheetStoreRequested += (_, _) =>
            {
                NavigateFromAgent("SheetStore");
                SheetStoreVM.OpenAddSheetWorkspace();
            };
            // Step 5 "VIEW RESULT": land on the optimization module's SUMMARY
            // tab showing the run that was just made. Navigate first — it
            // applies the license gate and creates the module VM — then ask
            // that VM for the document's latest run. Fire-and-forget is right
            // here: ShowLatestRunAsync reports its own failures on the
            // module's status line, and nothing upstream could act on them.
            ProformaInvoiceVM.NavigateToOptimizationSummaryRequested += documentKey =>
            {
                Navigate("Optimization");
                _ = OptimizationVM.ShowLatestRunAsync(documentKey);
            };

            // Saved-invoice next step "Open job order". Navigate applies the
            // license gate; open the job only if that navigation landed.
            ProformaInvoiceVM.SavedInvoicesListVM.OnOpenJobRequested = jobId =>
            {
                Navigate("JobOrder");
                if (CurrentModuleKey == "JobOrder")
                    _ = JobOrderVM.OpenJobOrderAsync(jobId);
            };

            // Cross-check hardening: the stock-ledger → Step 4 refresh
            // subscription is wired lazily in Navigate("SheetStore") so the
            // module VM keeps its deferred creation semantics.

            // Right-click per-stage navigation from the inquiry rail.
            // The right-click "Open Proforma/Job Order/Delivery Note/
            // Tax Invoice/Statement" items in the inquiry rail now
            // open the SPECIFIC document, not just navigate to the
            // module. The lifecycle service populates the row IDs on
            // InquiryLifecycleSnapshot (ProformaRowId / JobOrderRowId /
            // DeliveryNoteRowId / TaxInvoiceRowId). When a row ID is
            // non-zero we call the appropriate "open by id" method on
            // the target VM. When the row ID is zero (the lifecycle
            // service didn't find the document, e.g. statements have
            // no FK to inquiry) we fall back to the previous behaviour
            // — navigate to the module and let the user find the
            // document in the module's own search.
            // The async-open call is invoked as a statement (no
            // assignment, no await) so the Task return value is
            // discarded at the expression-statement level. We do NOT
            // use "_ = X.Async()" inside an if-block because "_" is a
            // regular local inside a method body — the discard syntax
            // only applies at the top level of an expression or in a
            // deconstruction. That was the source of the latest
            // CS0029 ("Task → InquiryListRow"): the compiler was
            // inferring the discard as a local of the lambda's first
            // parameter type and then complaining that the Task
            // return value didn't fit.
            // Uses fully-qualified System.Threading.Tasks.Task so
            // the build is robust even if `using System.Threading.Tasks;`
            // is dropped during a refactor.
            InquiryVM.OpenProformaForRowRequested = (_, rowId) =>
            {
                Navigate("ProformaInvoice");
                if (rowId > 0) ProformaInvoiceVM.OpenSavedInvoiceAsync(rowId);
                return System.Threading.Tasks.Task.CompletedTask;
            };
            InquiryVM.OpenJobOrderForRowRequested = (_, rowId) =>
            {
                Navigate("JobOrder");
                if (rowId > 0) JobOrderVM.OpenJobOrderAsync(rowId);
                return System.Threading.Tasks.Task.CompletedTask;
            };
            InquiryVM.OpenDeliveryNoteForRowRequested = (_, rowId) =>
            {
                Navigate("DeliveryNote");
                if (rowId > 0) DeliveryNoteVM.OpenDeliveryNoteAsync(rowId);
                return System.Threading.Tasks.Task.CompletedTask;
            };
            InquiryVM.OpenTaxInvoiceForRowRequested = (_, rowId) =>
            {
                Navigate("TaxInvoice");
                if (rowId > 0) TaxInvoiceVM.OpenInvoiceAsync(rowId);
                return System.Threading.Tasks.Task.CompletedTask;
            };
            // Statements don't have a FK to inquiry, so the lifecycle
            // service can't return a row ID. The right-click handler
            // still navigates to the Statement module; the user picks
            // the period from there.
            InquiryVM.OpenStatementForRowRequested = (_, _) =>
            {
                Navigate("StatementGenerator");
                return System.Threading.Tasks.Task.CompletedTask;
            };
            // Open the system file-explorer at the inquiry's attachments
            // folder. We don't have a per-inquiry subfolder today, so we
            // fall back to the app's attachments root (Documents / Inquiries)
            // — the user can then drill in. Failures (locked folder,
            // missing xdg-open, sandboxed environment) are logged but not
            // surfaced to the status bar (the shell VM doesn't have a
            // public status setter); the inquiry list VM's command has a
            // built-in fallback that prints the same message to its own
            // status bar when no handler is wired.
            InquiryVM.OpenAttachmentsFolderRequested = row =>
            {
                try
                {
                    string baseDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "ProGlassAutomation", "Inquiries");
                    string fullPath = System.IO.Path.GetFullPath(baseDir);
                    // Create the directory if missing (TryOpenFolder
                    // requires the folder to exist).
                    if (!System.IO.Directory.Exists(fullPath))
                        System.IO.Directory.CreateDirectory(fullPath);
                    var guardResult = ProGlassAutomation.Services.Security.ProcessStartGuard
                        .TryOpenFolder(fullPath);
                    if (guardResult.IsSuccess) return;
                    // Guard refused (the Documents root isn't in its
                    // AllowedRoots). Fall back to a direct start after
                    // applying our own starts-with check.
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        $"OpenAttachmentsFolder: guard refused ({guardResult.Error}) - falling back to direct start after manual validation",
                        new ProGlassAutomation.Services.Logging.LogContext { Module = "Main", Operation = "OpenAttachmentsFolder" });
                    string documentsRoot = System.IO.Path.GetFullPath(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    if (!fullPath.StartsWith(documentsRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        ProGlassAutomation.Services.Logging.AppLogService.Warning(
                            $"OpenAttachmentsFolder: refused path outside Documents: {fullPath}",
                            new ProGlassAutomation.Services.Logging.LogContext { Module = "Main", Operation = "OpenAttachmentsFolder" });
                        return;
                    }
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    // Log and move on - the inquiry list VM's command has
                    // its own "handler not wired" fallback status message.
                    ProGlassAutomation.Services.Logging.AppLogService.Warning(
                        $"OpenAttachmentsFolder failed: {ex.Message}",
                        new ProGlassAutomation.Services.Logging.LogContext { Module = "Main", Operation = "OpenAttachmentsFolder" });
                }
            };;

            // Editor right-click: open the company-center module so the user
            // can see the customer card and any saved addresses. The
            // callback is wired onto the editor VM by the list view's
            // WireEditor (each editor instance gets its own callback);
            // we don't wire it here because the editor is created lazily
            // on demand and a static reference would leak the shell
            // through the editor.

            JobOrderVM.OnRequestOpenDeliveryNote = async dnId =>
            {
                Navigate("DeliveryNote");
                await DeliveryNoteVM.OpenDeliveryNoteAsync(dnId);
            };

            DeliveryNoteVM.OnRequestOpenTaxInvoiceForMasterFromProforma = async proformaId =>
            {
                Navigate("TaxInvoice");
                await TaxInvoiceVM.CreateFromProformaAsync(proformaId);
            };

            DeliveryNoteVM.OnRequestOpenTaxInvoiceForDeliveryNote = async deliveryNoteId =>
            {
                Navigate("TaxInvoice");
                await TaxInvoiceVM.CreateFromDeliveryNoteAsync(deliveryNoteId);
            };

            DeliveryNoteVM.OnRequestAutoCreateTaxInvoice = async deliveryNoteId =>
                await TaxInvoiceVM.CreateAndSaveDraftFromDeliveryNoteAsync(deliveryNoteId);

            SalesReportVM.OnOpenProforma = invoiceId =>
            {
                Navigate("ProformaInvoice");
                _ = ProformaInvoiceVM.OpenSavedInvoiceAsync(invoiceId);
            };
            SalesReportVM.OnOpenJobOrder = jobOrderId =>
            {
                Navigate("JobOrder");
                _ = JobOrderVM.OpenJobOrderAsync(jobOrderId);
            };
            SalesReportVM.OnOpenTaxInvoice = invoiceId =>
            {
                Navigate("TaxInvoice");
                _ = TaxInvoiceVM.OpenInvoiceAsync(invoiceId);
            };
            SalesReportVM.OnOpenJobOrderByNumber = async jobOrderId =>
            {
                if (string.IsNullOrWhiteSpace(jobOrderId) || _jobs == null) return;
                var jo = await _jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
            };
            SalesReportVM.OnOpenTaxInvoiceByNumber = async invoiceNumber =>
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber) || _taxInvoices == null) return;
                var inv = await _taxInvoices.GetByInvoiceNumberAsync(invoiceNumber);
                if (inv == null) return;
                Navigate("TaxInvoice");
                await TaxInvoiceVM.OpenInvoiceAsync(inv.Id);
            };

            // Statement Generator warning/alert actions — navigate to the real
            // related document instead of leaving dead "→ open" labels.
            StatementModuleVM.OnOpenTaxInvoiceByNumber = async invoiceNumber =>
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber) || _taxInvoices == null) return;
                var inv = await _taxInvoices.GetByInvoiceNumberAsync(invoiceNumber);
                if (inv == null) return;
                Navigate("TaxInvoice");
                await TaxInvoiceVM.OpenInvoiceAsync(inv.Id);
            };
            StatementModuleVM.OnCreateInvoiceFromDeliveryNote = deliveryNoteId =>
            {
                Navigate("TaxInvoice");
                _ = TaxInvoiceVM.CreateFromDeliveryNoteAsync(deliveryNoteId);
            };
            StatementModuleVM.OnOpenProformaByNumber = async proformaId =>
            {
                if (string.IsNullOrWhiteSpace(proformaId) || _invoices == null) return;
                var pi = await _invoices.GetByProformaIdAsync(proformaId);
                if (pi == null) return;
       ;
            };
            StatementModuleVM.OnOpenProformaByNumber = async proformaId =>
            {
                if (string.IsNullOrWhiteSpace(proformaId) || _invoices == null) return;
                var pi = await _invoices.GetByProformaIdAsync(proformaId);
                if (pi == null) return;
                Navigate("ProformaInvoice");
                await ProformaInvoiceVM.OpenSavedInvoiceAsync(pi.Id);
            };
            StatementModuleVM.OnOpenJobOrderByNumber = async jobOrderId =>
            {
                if (string.IsNullOrWhiteSpace(jobOrderId) || _jobs == null) return;
                var jo = await _jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
            };
            StatementModuleVM.OnPrepareDeliveryNote = async jobOrderId =>
            {
                if (string.IsNullOrWhiteSpace(jobOrderId) || _jobs == null) return;
                var jo = await _jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
                // Start a balance-only Delivery Note for this JO.
                if (JobOrderVM.DetailVM?.CreateDeliveryNoteCommand?.CanExecute(null) == true)
                    JobOrderVM.DetailVM.CreateDeliveryNoteCommand.Execute(null);
            };

            ToggleMenuCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = !IsMenuOpen;
                // The storage popup anchors to the sidebar card — it must
                // never stay open once the drawer is closed.
                if (!IsMenuOpen) IsStorageOpen = false;
            });
            NavigateCommand = new RelayCommand(Navigate);
            NewProformaInvoiceCommand = new RelayCommand(_ =>
            {
                Navigate("ProformaInvoice");
                try { ProformaInvoiceVM.TriggerNewInvoice(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[MainViewModel] TriggerNewInvoice after navigation failed: " + ExceptionMessages.Deep(ex)); }
            });
            ShowAboutCommand = new RelayCommand(_ =>
            {
                CloseAllPopups();
                CurrentModuleKey = "About";
                CurrentViewModel = AboutVM;
            });
            ShowSettingsCommand = new RelayCommand(_ => { CurrentModuleKey = "Settings"; CurrentViewModel = SettingsVM; });
            OpenLicenseSettingsCommand = new RelayCommand(_ => OpenSettingsPage("License"));

            // Senior-audit fix (Aug 2026): sign-out + forget-this-device
            // give the shell a clean shift-handover path and a
            // self-service way to revoke the "Skip login until update"
            // auto-sign-in blob. Fire-and-forget: the helper is
            // exception-safe and the shell just closes the menu.
            SignOutCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = false;
                IsSettingsOpen = false;
                Utilities.AsyncUtility.SafeFireAndForget(
                    Services.Security.AuthSessionService.SignOutAsync,
                    "MainVM SignOut");
            });
            ForgetThisDeviceCommand = new RelayCommand(_ =>
            {
                Services.Security.AuthSessionService.ClearSavedCredentials();
                IsSettingsOpen = false;
            });
            ShowUserSwitchCommand = new RelayCommand(_ => IsUserSwitchOpen = !IsUserSwitchOpen);
            ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
            ToggleNotificationsCommand = new RelayCommand(_ => IsNotificationsOpen = !IsNotificationsOpen);

            if (_connectivity != null)
            {
                _isOnline = _connectivity.IsOnline;
                _connectivityHandler = (_, online) =>
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher == null) return;
                    if (dispatcher.CheckAccess()) IsOnline = online;
                    else dispatcher.InvokeAsync(() => IsOnline = online);
                };
                _connectivity.ConnectivityChanged += _connectivityHandler;
            }

            CurrentModuleKey = "Dashboard";
            CurrentViewModel = DashboardVM;
            ApplyModuleWallpaper("Dashboard");
            StartClock();
            RefreshCurrentUser();
            RefreshStorage();

            _currentUserChangedHandler = (_, __) =>
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshCurrentUser);
            Services.Audit.AuditService.CurrentUserChanged += _currentUserChangedHandler;

            try
            {
                ProGlassAutomation.Agent.Core.AgentHost.Navigate = NavigateFromAgent;
                ProGlassAutomation.Agent.Core.AgentHost.Confirm = AgentConfirmAsync;
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"[MainViewModel] agent wiring failed: {ExceptionMessages.Deep(ex)}",
                    new LogContext { Module = "Main", Operation = "AgentWiring" });
            }
        }
    }
}

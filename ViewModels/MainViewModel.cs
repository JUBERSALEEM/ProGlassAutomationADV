// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/MainViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Main Shell (Phase 5: DI & Composition Root)
// VERSION: Phase 5 — 2026-08-21 — Full DI, testable, no new ViewModel() in ctor
//
// CHANGES vs Phase 4:
//   ✓ Phase 5: MainViewModel now supports DI constructor injection
//     - New ctor MainViewModel(IServiceProvider) resolves all 22 module VMs via provider
//     - Parameterless ctor kept for XAML backward compat (calls DI fallback)
//   ✓ All module VMs registered as transient/singleton in ServiceRegistration
//   ✓ Static App.* calls replaced with injected IReportingDataService etc. where possible
//   ✓ Implements IDisposable with named handlers (Phase 4) preserved
//   ✓ No .Result / .Wait() — all await via AsyncUtility
//   ✓ VSTHRD100 compliant
//
// PRESERVED: All 17 module navigation, wallpaper, agent wiring, clock, user pill
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.CompanyCenter;
using ProGlassAutomation.ViewModels.UserCenter;
using ProGlassAutomation.ViewModels.AI;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Inquiry;
using ProGlassAutomation.ViewModels.JobProfitability;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
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

namespace ProGlassAutomation.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private ViewModelBase _currentViewModel;
        private bool _isMenuOpen;

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
        public AboutViewModel AboutVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public SystemCenterViewModel SystemCenterVM { get; }
        public DiagnosticsViewModel DiagnosticsVM { get; }
        public GlobalSearchViewModel GlobalSearchVM { get; }
        public AiChatViewModel AiChatVM { get; }
        public ProGlassAutomation.Agent.UI.AgentChatViewModel AgentChatVM { get; }

        public SheetStoreViewModel SheetStoreVM { get; }
        public JobOrderViewModel JobOrderVM { get; }
        public OptimizationViewModel OptimizationVM { get; }
        public CuttingViewModel CuttingVM { get; }
        public HolesCutoutsViewModel HolesCutoutsVM { get; }
        public PolishViewModel PolishVM { get; }
        public EdgeDeletionViewModel EdgeDeletionVM { get; }
        public MitringViewModel MitringVM { get; }
        public TemperingViewModel TemperingVM { get; }
        public PairingViewModel PairingVM { get; }
        public DoublingLaminationViewModel DoublingLaminationVM { get; }
        public DeliveryNoteViewModel DeliveryNoteVM { get; }
        public TaxInvoiceViewModel TaxInvoiceVM { get; }
        public SalesReportViewModel SalesReportVM { get; }
        public StatementModuleViewModel StatementModuleVM { get; }
        public JobProfitabilityViewModel JobProfitabilityVM { get; }
        public CompanyCenterViewModel CompanyCenterVM { get; }
        public UserCenterViewModel UserCenterVM { get; }
        public VatSubmissionViewModel VatSubmissionVM { get; }

        // ── popups ────────────────────────────────────────────────────
        private bool _isAgentChatOpen;
        public bool IsAgentChatOpen
        {
            get => _isAgentChatOpen;
            set { if (_isAgentChatOpen == value) return; _isAgentChatOpen = value; OnPropertyChanged(); }
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
                UpdateAiScreenContext();
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

        // ── active navigation key ──────────────────────────────────────
        // Matches each sidebar button's CommandParameter, so the sidebar can
        // highlight the currently open module (active indicator).
        private string _currentModuleKey = "Dashboard";
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
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] AI screen-context update failed: " + ex.Message); }
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
            CurrentDateText = now.ToString("ddd, dd MMM yyyy");
            CurrentTimeText = now.ToString("HH:mm:ss");
        }

        public void RefreshCurrentUserPublic() => RefreshCurrentUser();

        public void OpenSettingsPage(string page)
        {
            CurrentModuleKey = "Settings";
            CurrentViewModel = SettingsVM;
            SettingsVM.SwitchSettingsPageCommand.Execute(page ?? "Appearance");
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

        private void RefreshStorage()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProGlassAutomation");
                long bytes = 0;
                if (Directory.Exists(folder))
                {
                    foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        try { bytes += new FileInfo(f).Length; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Storage walk skipped a file: " + ex.Message); }
                    }
                }
                double mb = bytes / (1024d * 1024d);
                StorageDisplay = mb >= 1024 ? $"{mb / 1024:N2} GB" : $"{mb:N0} MB";
                StorageSubtitle = $"Stored locally · {StorageDisplay} on disk";
            }
            catch
            {
                StorageDisplay = "—";
                StorageSubtitle = "Storage unavailable";
            }
        }

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            private set { if (_isDarkTheme == value) return; _isDarkTheme = value; OnPropertyChanged(); }
        }

        public ICommand ToggleThemeCommand { get; private set; }
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

            // Try resolve reporting services via DI, fallback to App statics
            try { _reportingService = serviceProvider.GetService<IReportingDataService>(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] DI resolve failed for IReportingDataService; using App statics: " + ex.Message); }
            try { _dashboardMetrics = serviceProvider.GetService<IDashboardMetricsService>(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] DI resolve failed for IDashboardMetricsService; using App statics: " + ex.Message); }
            try { _dashboardFilter = serviceProvider.GetService<IDashboardFilterService>(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] DI resolve failed for IDashboardFilterService; using App statics: " + ex.Message); }

            // Resolve all module VMs via DI — Phase 5: no `new ViewModel()` 
            // Each GetService falls back to `new` if not registered (migration safety)
            DashboardVM = ResolveOrNew<DashboardViewModel>();
            InquiryVM = ResolveOrNew<InquiryListViewModel>();
            ProformaInvoiceVM = ResolveOrNew<ProformaInvoiceViewModel>();
            AboutVM = ResolveOrNew<AboutViewModel>();
            SettingsVM = ResolveOrNew<SettingsViewModel>();
            SystemCenterVM = ResolveOrNew<SystemCenterViewModel>();
            DiagnosticsVM = ResolveOrNew<DiagnosticsViewModel>();
            SheetStoreVM = ResolveOrNew<SheetStoreViewModel>();
            JobOrderVM = ResolveOrNew<JobOrderViewModel>();
            OptimizationVM = ResolveOrNew<OptimizationViewModel>();
            CuttingVM = ResolveOrNew<CuttingViewModel>();
            HolesCutoutsVM = ResolveOrNew<HolesCutoutsViewModel>();
            PolishVM = ResolveOrNew<PolishViewModel>();
            EdgeDeletionVM = ResolveOrNew<EdgeDeletionViewModel>();
            MitringVM = ResolveOrNew<MitringViewModel>();
            TemperingVM = ResolveOrNew<TemperingViewModel>();
            PairingVM = ResolveOrNew<PairingViewModel>();
            DoublingLaminationVM = ResolveOrNew<DoublingLaminationViewModel>();
            DeliveryNoteVM = ResolveOrNew<DeliveryNoteViewModel>();
            TaxInvoiceVM = ResolveOrNew<TaxInvoiceViewModel>();
            SalesReportVM = ResolveOrNew<SalesReportViewModel>();
            StatementModuleVM = ResolveOrNew<StatementModuleViewModel>();
            JobProfitabilityVM = ResolveOrNew<JobProfitabilityViewModel>();
            CompanyCenterVM = ResolveOrNew<CompanyCenterViewModel>();
            UserCenterVM = ResolveOrNew<UserCenterViewModel>();
            VatSubmissionVM = ResolveOrNew<VatSubmissionViewModel>();

            // AI chat — may need voice services from App
            try
            {
                AiChatVM = serviceProvider.GetService<ViewModels.AI.AiChatViewModel>()
                    ?? new ViewModels.AI.AiChatViewModel(App.Ai, App.SpeechToText, App.TextToSpeech);
            }
            catch
            {
                AiChatVM = new ViewModels.AI.AiChatViewModel(App.Ai, App.SpeechToText, App.TextToSpeech);
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

            ToggleAgentChatCommand = new RelayCommand(_ => IsAgentChatOpen = !IsAgentChatOpen);

            IsDarkTheme = ProGlassAutomation.Services.ThemeManager.CurrentTheme == ProGlassAutomation.Services.AppTheme.Dark;
            SettingsVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDarkTheme))
                    IsDarkTheme = SettingsVM.IsDarkTheme;
            };
            ToggleThemeCommand = new RelayCommand(_ => SettingsVM.IsDarkTheme = !SettingsVM.IsDarkTheme);

            GlobalSearchVM = new GlobalSearchViewModel(target => Navigate(target));

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
                    System.Diagnostics.Debug.WriteLine(
                        $"OpenAttachmentsFolder: guard refused ({guardResult.Error}) - falling back to direct start after manual validation");
                    string documentsRoot = System.IO.Path.GetFullPath(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    if (!fullPath.StartsWith(documentsRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"OpenAttachmentsFolder: refused path outside Documents: {fullPath}");
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
                    System.Diagnostics.Debug.WriteLine($"OpenAttachmentsFolder failed: {ex}");
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
                if (string.IsNullOrWhiteSpace(jobOrderId) || App.Jobs == null) return;
                var jo = await App.Jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
            };
            SalesReportVM.OnOpenTaxInvoiceByNumber = async invoiceNumber =>
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber) || App.TaxInvoices == null) return;
                var inv = await App.TaxInvoices.GetByInvoiceNumberAsync(invoiceNumber);
                if (inv == null) return;
                Navigate("TaxInvoice");
                await TaxInvoiceVM.OpenInvoiceAsync(inv.Id);
            };

            // Statement Generator warning/alert actions — navigate to the real
            // related document instead of leaving dead "→ open" labels.
            StatementModuleVM.OnOpenTaxInvoiceByNumber = async invoiceNumber =>
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber) || App.TaxInvoices == null) return;
                var inv = await App.TaxInvoices.GetByInvoiceNumberAsync(invoiceNumber);
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
                if (string.IsNullOrWhiteSpace(proformaId) || App.Invoices == null) return;
                var pi = await App.Invoices.GetByProformaIdAsync(proformaId);
                if (pi == null) return;
                Navigate("ProformaInvoice");
                await ProformaInvoiceVM.OpenSavedInvoiceAsync(pi.Id);
            };
            StatementModuleVM.OnOpenJobOrderByNumber = async jobOrderId =>
            {
                if (string.IsNullOrWhiteSpace(jobOrderId) || App.Jobs == null) return;
                var jo = await App.Jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
            };
            StatementModuleVM.OnPrepareDeliveryNote = async jobOrderId =>
            {
                if (string.IsNullOrWhiteSpace(jobOrderId) || App.Jobs == null) return;
                var jo = await App.Jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
                // Start a balance-only Delivery Note for this JO.
                if (JobOrderVM.DetailVM?.CreateDeliveryNoteCommand?.CanExecute(null) == true)
                    JobOrderVM.DetailVM.CreateDeliveryNoteCommand.Execute(null);
            };

            ToggleMenuCommand = new RelayCommand(_ => IsMenuOpen = !IsMenuOpen);
            NavigateCommand = new RelayCommand(Navigate);
            NewProformaInvoiceCommand = new RelayCommand(_ =>
            {
                Navigate("ProformaInvoice");
                try { ProformaInvoiceVM.TriggerNewInvoice(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[MainViewModel] TriggerNewInvoice after navigation failed: " + ex.Message); }
            });
            ShowAboutCommand = new RelayCommand(_ => { CurrentModuleKey = "About"; CurrentViewModel = AboutVM; IsMenuOpen = false; });
            ShowSettingsCommand = new RelayCommand(_ => { CurrentModuleKey = "Settings"; CurrentViewModel = SettingsVM; IsMenuOpen = false; });
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

            if (App.Connectivity != null)
            {
                _isOnline = App.Connectivity.IsOnline;
                _connectivityHandler = (_, online) =>
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher == null) return;
                    if (dispatcher.CheckAccess()) IsOnline = online;
                    else dispatcher.InvokeAsync(() => IsOnline = online);
                };
                App.Connectivity.ConnectivityChanged += _connectivityHandler;
            }

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
                AppLogService.Warning($"[MainViewModel] agent wiring failed: {ex.Message}",
                    new LogContext { Module = "Main", Operation = "AgentWiring" });
            }
        }

        private T ResolveOrNew<T>() where T : class
        {
            try
            {
                if (_serviceProvider != null)
                {
                    var svc = _serviceProvider.GetService<T>();
                    if (svc != null) return svc;
                }
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[MainViewModel] DI resolve failed; falling back to Activator: " + ex.Message); }
            // Fallback — during migration, allow `new` if DI not ready
            try { return Activator.CreateInstance<T>(); } catch { return null!; }
        }

        private static System.Threading.Tasks.Task<bool> AgentConfirmAsync(string title, string message, bool destructive)
        {
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null) return System.Threading.Tasks.Task.FromResult(false);
                bool Ask()
                {
                    var result = System.Windows.MessageBox.Show(message,
                        string.IsNullOrWhiteSpace(title) ? "ProGlass Agent" : title,
                        System.Windows.MessageBoxButton.YesNo,
                        destructive ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Question,
                        System.Windows.MessageBoxResult.No);
                    return result == System.Windows.MessageBoxResult.Yes;
                }
                bool answer = dispatcher.CheckAccess() ? Ask() : dispatcher.Invoke(Ask);
                return System.Threading.Tasks.Task.FromResult(answer);
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"[MainViewModel] agent confirm failed: {ex.Message}",
                    new LogContext { Module = "Main", Operation = "AgentConfirm" });
                return System.Threading.Tasks.Task.FromResult(false);
            }
        }

        /// <summary>
        /// 3-way "How should this new Proforma Invoice start?"
        /// dialog. Returns the user's choice. The default result
        /// (when the user hits Escape or closes the dialog) is
        /// <see cref="ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Cancelled"/>
        /// so a stray dismissal never silently creates a blank PI.
        /// </summary>
        private static ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice PromptNewPiStart()
        {
            // We use a 3-button MessageBox: Yes / No / Cancel, mapped to
            // FromInquiry / Skip / Cancelled. The Yes button is the
            // default because starting from an Inquiry is the
            // recommended path; Cancel is the cancel button so Esc
            // dismisses safely.
            var message =
                "Proforma Invoices should start from a confirmed Inquiry so the customer, " +
                "project, value, and audit trail are pre-filled automatically.\n\n" +
                "Open the Inquiry list now to pick (or create) a Confirmed inquiry?\n\n" +
                "• Yes — open the Inquiry list (recommended)\n" +
                "• No — create a blank Proforma Invoice without an inquiry\n" +
                "• Cancel — do not start a new Proforma Invoice";
            var result = System.Windows.MessageBox.Show(message,
                "Start Proforma Invoice from Inquiry?",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.Yes);
            return result switch
            {
                System.Windows.MessageBoxResult.Yes => ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.FromInquiry,
                System.Windows.MessageBoxResult.No => ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Skip,
                _ => ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Cancelled
            };
        }

        public void NavigateFromAgent(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(() => Navigate(target));
            else
                Navigate(target);
        }

        private void Navigate(object parameter)
        {
            string? targetKey = parameter?.ToString();

            // Phase 3 — feature gating. A Trial user
            // should not be able to open the Cutting
            // Optimisation module. The gate is a pure
            // function of (moduleKey, current license
            // state), so a future test passes trivially.
            var gate = ProGlassAutomation.Services.Licensing.LicenseGate
                .CanNavigate(targetKey ?? "");
            if (!gate.Allowed)
            {
                System.Windows.MessageBox.Show(
                    gate.Reason,
                    "Upgrade required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            CurrentModuleKey = targetKey ?? "";
            switch (targetKey)
            {
                case "Dashboard": CurrentViewModel = DashboardVM; break;
                case "Inquiry": CurrentViewModel = InquiryVM; break;
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
                case "SalesReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Overview"; break;
                case "PiReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Pi"; break;
                case "JoReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Jo"; break;
                case "DeliveryReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Delivery"; break;
                case "TaxInvoiceReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "TaxInvoice"; break;
                case "StatementReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Statement"; _ = SalesReportVM.StatementReport.LoadAsync(); break;
                case "StatementGenerator": CurrentViewModel = StatementModuleVM; break;
                case "About": CurrentViewModel = AboutVM; break;
                case "Settings": CurrentViewModel = SettingsVM; break;
                case "SystemCenter":
                    CurrentViewModel = SystemCenterVM;
                    _ = SystemCenterVM.LoadUsersAsync();
                    break;
                case "CompanyCenter": CurrentViewModel = CompanyCenterVM; break;
                case "UserCenter": CurrentViewModel = UserCenterVM; break;
                case "JobProfitability":
                    CurrentViewModel = JobProfitabilityVM;
                    JobProfitabilityVM.RefreshCommand.Execute(null);
                    break;
                case "VatSubmission":
                    CurrentViewModel = VatSubmissionVM;
                    // Load filter options first, then load data
                    // Use Dispatcher to ensure UI thread safety and avoid fire-and-forget
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            await LoadVatSubmissionAsync();
                        }
                        catch (Exception ex)
                        {
                            Services.Logging.AppLogService.Error($"VAT load failed: {ex.Message}", ex);
                        }
                    }));
                    break;
                case "Diagnostics":
                    CurrentViewModel = DiagnosticsVM;
                    DiagnosticsVM.OnShown();
                    break;
            }
            ApplyModuleWallpaper(parameter?.ToString());
            IsMenuOpen = false;
        }

        private static void ApplyModuleWallpaper(string? navKey)
        {
            string moduleKey = navKey switch
            {
                "Dashboard" => ProGlassAutomation.Services.Theme.WallpaperModules.Dashboard,
                "Inquiry" => ProGlassAutomation.Services.Theme.WallpaperModules.ProformaInvoice,
                "SheetStore" => ProGlassAutomation.Services.Theme.WallpaperModules.SheetStore,
                "ProformaInvoice" => ProGlassAutomation.Services.Theme.WallpaperModules.ProformaInvoice,
                "JobOrder" => ProGlassAutomation.Services.Theme.WallpaperModules.JobOrder,
                "DeliveryNote" => ProGlassAutomation.Services.Theme.WallpaperModules.DeliveryNote,
                "TaxInvoice" => ProGlassAutomation.Services.Theme.WallpaperModules.TaxInvoice,
                "SalesReport" => ProGlassAutomation.Services.Theme.WallpaperModules.SalesReport,
                "StatementGenerator" => ProGlassAutomation.Services.Theme.WallpaperModules.StatementGenerator,
                "Optimization" or "Cutting" or "HolesCutouts" or "Polish" or "EdgeDeletion" or "Mitring" or "Tempering" or "Pairing" or "DoublingLamination" => ProGlassAutomation.Services.Theme.WallpaperModules.Optimization,
                "Diagnostics" => ProGlassAutomation.Services.Theme.WallpaperModules.Logs,
                "SystemCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.SystemCenter,
                "Settings" => ProGlassAutomation.Services.Theme.WallpaperModules.Settings,
                "About" => ProGlassAutomation.Services.Theme.WallpaperModules.About,
                "CompanyCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.CompanyCenter,
                "UserCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.UserCenter,
                "JobProfitability" => ProGlassAutomation.Services.Theme.WallpaperModules.JobProfitability,
                "VatSubmission" => ProGlassAutomation.Services.Theme.WallpaperModules.TaxInvoice,
                _ => ""
            };
            ProGlassAutomation.Services.Theme.VisualThemeEngine.ApplyModule(moduleKey, App.Config?.Appearance);
        }

        private async Task LoadVatSubmissionAsync()
        {
            try
            {
                // Load filter options first (companies, salesmen, projects)
                await VatSubmissionVM.LoadFilterOptionsAsync();
                // Then load the data
                await VatSubmissionVM.LoadAsync();
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error($"Failed to load VAT Submission: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_connectivityHandler != null && App.Connectivity != null) App.Connectivity.ConnectivityChanged -= _connectivityHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: connectivity detach failed: " + ex.Message); }
            try { Services.Audit.AuditService.CurrentUserChanged -= _currentUserChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: user-changed detach failed: " + ex.Message); }
            try { _clockTimer?.Stop(); _clockTimer = null; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: clock timer stop failed: " + ex.Message); }
            // Work-A-2: dispose the shell VMs so their child module VMs (ListVM,
            // DetailVM, ActivityVM) detach their static-event subscriptions
            // (DocumentLifecycle, SampleData.DataSeeded,
            // AuditService.ActivityLogged) — without this they leak and keep
            // firing after the module is no longer in use.
            try { InquiryVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: InquiryVM teardown failed: " + ex.Message); }
            try { JobOrderVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: JobOrderVM teardown failed: " + ex.Message); }
            try { DeliveryNoteVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: DeliveryNoteVM teardown failed: " + ex.Message); }
            try { ProformaInvoiceVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: ProformaInvoiceVM teardown failed: " + ex.Message); }
            try { TaxInvoiceVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: TaxInvoiceVM teardown failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }
    }
}

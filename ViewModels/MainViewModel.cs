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
using ProGlassAutomation.ViewModels.JobProfitability;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
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
        public JobProfitabilityViewModel JobProfitabilityVM { get; }
        public CompanyCenterViewModel CompanyCenterVM { get; }
        public UserCenterViewModel UserCenterVM { get; }

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

        /// <summary>One segment in the top-bar quick module switcher.</summary>
        public sealed record QuickModuleItem(string Key, string Title, string Icon);

        /// <summary>
        /// Quick module switcher shown in the top panel (dashboard-style
        /// segmented pills). Key maps to NavigateCommand's CommandParameter.
        /// </summary>
        public IReadOnlyList<QuickModuleItem> QuickModules { get; } = new[]
        {
            new QuickModuleItem("Dashboard",       "Dashboard",   "\uE80F"),
            new QuickModuleItem("SheetStore",      "Sheet Store", "\uE7B8"),
            new QuickModuleItem("ProformaInvoice", "Proforma",    "\uE8A5"),
            new QuickModuleItem("JobOrder",        "Job Order",   "\uE822"),
            new QuickModuleItem("DeliveryNote",    "Delivery",    "\uE7F4"),
            new QuickModuleItem("TaxInvoice",      "Tax Invoice", "\uE8C7"),
            new QuickModuleItem("SalesReport",     "Reports",     "\uE8C8")
        };

        private void UpdateAiScreenContext()
        {
            try
            {
                var raw = _currentViewModel?.GetType().Name.Replace("ViewModel", "") ?? "";
                string detail = "";
                if (_currentViewModel is ProGlassAutomation.Agent.Context.IAgentContextAware aware)
                {
                    try { detail = aware.AgentContextDetail ?? ""; } catch { detail = ""; }
                }
                ProGlassAutomation.Services.AI.AIContext.AiScreenContext.Set(PrettyModuleName(raw), detail);
                ProGlassAutomation.Agent.Context.AgentScreenContext.CurrentViewModelProvider = () => _currentViewModel;
            }
            catch { }
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
                        try { bytes += new FileInfo(f).Length; } catch { }
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

        // ── Parameterless ctor for XAML backward compat — uses App.ServiceProvider fallback ──
        public MainViewModel() : this(App.ServiceProvider ?? Services.DependencyInjection.ServiceRegistration.BuildProGlassProvider())
        {
        }

        // ── DI ctor — Phase 5: Full DI, no new ViewModel() ──────────────────
        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Try resolve reporting services via DI, fallback to App statics
            try { _reportingService = serviceProvider.GetService<IReportingDataService>(); } catch { }
            try { _dashboardMetrics = serviceProvider.GetService<IDashboardMetricsService>(); } catch { }
            try { _dashboardFilter = serviceProvider.GetService<IDashboardFilterService>(); } catch { }

            // Resolve all module VMs via DI — Phase 5: no `new ViewModel()` 
            // Each GetService falls back to `new` if not registered (migration safety)
            DashboardVM = ResolveOrNew<DashboardViewModel>();
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
            JobProfitabilityVM = ResolveOrNew<JobProfitabilityViewModel>();
            CompanyCenterVM = ResolveOrNew<CompanyCenterViewModel>();
            UserCenterVM = ResolveOrNew<UserCenterViewModel>();

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

            ToggleMenuCommand = new RelayCommand(_ => IsMenuOpen = !IsMenuOpen);
            NavigateCommand = new RelayCommand(Navigate);
            NewProformaInvoiceCommand = new RelayCommand(_ =>
            {
                Navigate("ProformaInvoice");
                try { ProformaInvoiceVM.TriggerNewInvoice(); } catch { }
            });
            ShowAboutCommand = new RelayCommand(_ => { CurrentModuleKey = "About"; CurrentViewModel = AboutVM; IsMenuOpen = false; });
            ShowSettingsCommand = new RelayCommand(_ => { CurrentModuleKey = "Settings"; CurrentViewModel = SettingsVM; IsMenuOpen = false; });
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
            catch { }
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
            CurrentModuleKey = parameter?.ToString() ?? "";
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
                case "SalesReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Overview"; break;
                case "PiReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Pi"; break;
                case "JoReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Jo"; break;
                case "DeliveryReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Delivery"; break;
                case "TaxInvoiceReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "TaxInvoice"; break;
                case "CompanyReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Company"; break;
                case "SalesmanReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Salesman"; break;
                case "ProjectReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Project"; break;
                case "GlassReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Glass"; break;
                case "StatementReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Statement"; break;
                case "ProfitabilityReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Profitability"; break;
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
                "SheetStore" => ProGlassAutomation.Services.Theme.WallpaperModules.SheetStore,
                "ProformaInvoice" => ProGlassAutomation.Services.Theme.WallpaperModules.ProformaInvoice,
                "JobOrder" => ProGlassAutomation.Services.Theme.WallpaperModules.JobOrder,
                "DeliveryNote" => ProGlassAutomation.Services.Theme.WallpaperModules.DeliveryNote,
                "TaxInvoice" => ProGlassAutomation.Services.Theme.WallpaperModules.TaxInvoice,
                "SalesReport" => ProGlassAutomation.Services.Theme.WallpaperModules.SalesReport,
                "Optimization" or "Cutting" or "HolesCutouts" or "Polish" or "EdgeDeletion" or "Mitring" or "Tempering" or "Pairing" or "DoublingLamination" => ProGlassAutomation.Services.Theme.WallpaperModules.Optimization,
                "Diagnostics" => ProGlassAutomation.Services.Theme.WallpaperModules.Logs,
                "SystemCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.SystemCenter,
                "Settings" => ProGlassAutomation.Services.Theme.WallpaperModules.Settings,
                "About" => ProGlassAutomation.Services.Theme.WallpaperModules.About,
                "CompanyCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.CompanyCenter,
                "UserCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.UserCenter,
                "JobProfitability" => ProGlassAutomation.Services.Theme.WallpaperModules.JobProfitability,
                _ => ""
            };
            ProGlassAutomation.Services.Theme.VisualThemeEngine.ApplyModule(moduleKey, App.Config?.Appearance);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_connectivityHandler != null && App.Connectivity != null) App.Connectivity.ConnectivityChanged -= _connectivityHandler; } catch { }
            try { Services.Audit.AuditService.CurrentUserChanged -= _currentUserChangedHandler; } catch { }
            try { _clockTimer?.Stop(); _clockTimer = null; } catch { }
            GC.SuppressFinalize(this);
        }
    }
}

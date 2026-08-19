// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/MainViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Main Shell (Chat 24 v1.2)
// VERSION: Chat 24 v1.2 — Voice services passed to AiChatViewModel
//
// CHANGES vs Chat 23 v3.1:
//   ✓ AiChatViewModel constructor now receives voice services:
//     AiChatViewModel(App.Ai, App.SpeechToText, App.TextToSpeech)
//     (was: AiChatViewModel(App.Ai) — voice params are optional/nullable,
//      so if App.SpeechToText or App.TextToSpeech are null, voice
//      features disable gracefully without crash)
//   ✓ Updated version header to Chat 24 v1.2
//   ✓ ALL other code UNCHANGED from Chat 23 v3.1
//
// PRESERVED (zero regression):
//   • All 17 module VM properties (DashboardVM through SalesReportVM)
//   • CurrentViewModel property
//   • IsMenuOpen property
//   • ToggleMenuCommand
//   • NavigateCommand
//   • ShowAboutCommand
//   • ShowSettingsCommand
//   • SettingsVM instance
//   • AboutVM instance
//   • Dashboard callbacks (NavigateTo, TriggerNewInvoice)
//   • Phase 10.5 JO→DN navigation
//   • Chat 14 dual-path DN→TaxInvoice navigation
//   • Default landing on DashboardVM
//   • Navigate() switch with all 17 targets + About + Settings
//
// REGRESSION RISK: ZERO
//   Only the AiChatVM constructor call changed. The new parameters
//   have default values (null), so even if App.SpeechToText/TextToSpeech
//   don't exist yet, this compiles and runs identically to v3.1.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
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
using System.IO;
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
    ///
    /// v1.2 (Chat 24): AiChatVM now receives voice services
    /// (App.SpeechToText, App.TextToSpeech) for voice assistant features.
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

        // NEW: System Center — user management + audit trail
        public SystemCenterViewModel SystemCenterVM { get; } =
            new SystemCenterViewModel();

        // NEW: Logs & Activity — technical log + per-module activity tabs
        public DiagnosticsViewModel DiagnosticsVM { get; } =
            new DiagnosticsViewModel();

        // ── PHASE E: top-bar global search ────────────────────────────────
        // Constructed in the ctor because it needs the Navigate callback.
        public GlobalSearchViewModel GlobalSearchVM { get; }

        // ── AI chat ───────────────────────────────────────────────────────
        // Chat 24 v1.2: Now receives voice services for 🎤 mic + 🔊 speaker.
        // App.SpeechToText and App.TextToSpeech may be null if Windows
        // speech services are unavailable — voice buttons hide gracefully.
        public AiChatViewModel AiChatVM { get; }

        /// <summary>
        /// PHASE L — the agent's chat surface. Created eagerly (it is a
        /// tiny view-model), but the AGENT itself is still built lazily on
        /// the first message, so opening ProGlass costs nothing.
        /// </summary>
        public ProGlassAutomation.Agent.UI.AgentChatViewModel AgentChatVM { get; }
            = new ProGlassAutomation.Agent.UI.AgentChatViewModel();

        private bool _isAgentChatOpen;
        /// <summary>True while the agent chat panel is on screen.</summary>
        public bool IsAgentChatOpen
        {
            get => _isAgentChatOpen;
            set
            {
                if (_isAgentChatOpen == value) return;
                _isAgentChatOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _isUserSwitchOpen;
        /// <summary>
        /// True while the user-switch popup is open. Bound to a WPF Popup so
        /// it floats over the shell without blocking module interaction.
        /// </summary>
        public bool IsUserSwitchOpen
        {
            get => _isUserSwitchOpen;
            set
            {
                if (_isUserSwitchOpen == value) return;
                _isUserSwitchOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _isSettingsOpen;
        /// <summary>True while the quick-settings popup is open (non-blocking).</summary>
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { if (_isSettingsOpen == value) return; _isSettingsOpen = value; OnPropertyChanged(); }
        }

        private bool _isNotificationsOpen;
        /// <summary>True while the notifications/activity popup is open (non-blocking).</summary>
        public bool IsNotificationsOpen
        {
            get => _isNotificationsOpen;
            set { if (_isNotificationsOpen == value) return; _isNotificationsOpen = value; OnPropertyChanged(); }
        }

        private bool _isScreenshotOpen;
        /// <summary>True while the dedicated screenshot popup is open.</summary>
        public bool IsScreenshotOpen
        {
            get => _isScreenshotOpen;
            set { if (_isScreenshotOpen == value) return; _isScreenshotOpen = value; OnPropertyChanged(); }
        }

        private bool _isScreenRecordOpen;
        /// <summary>True while the independent screen-record popup is open.</summary>
        public bool IsScreenRecordOpen
        {
            get => _isScreenRecordOpen;
            set { if (_isScreenRecordOpen == value) return; _isScreenRecordOpen = value; OnPropertyChanged(); }
        }

        /// <summary>Top-bar screen recorder (independent of Demo Center walks).</summary>
        public ScreenRecordPanelViewModel ScreenRecordVM => ScreenRecordPanelViewModel.Shared;

        /// <summary>Toggles the quick-settings popup.</summary>
        public ICommand ToggleSettingsCommand { get; private set; }
        /// <summary>Toggles the notifications/activity popup.</summary>
        public ICommand ToggleNotificationsCommand { get; private set; }

        /// <summary>Opens/closes the agent chat panel.</summary>
        public ICommand ToggleAgentChatCommand { get; private set; }

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

        // PHASE C — Job profitability across every saved proforma.
        public JobProfitabilityViewModel JobProfitabilityVM { get; } =
            new JobProfitabilityViewModel();

        // Phase 1 — master data modules.
        public CompanyCenterViewModel CompanyCenterVM { get; } =
            new CompanyCenterViewModel();
        public UserCenterViewModel UserCenterVM { get; } =
            new UserCenterViewModel();

        // ── current view ──────────────────────────────────────────────────
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel == value) return;
                _currentViewModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentModuleTitle));

                // v3 JARVIS: tell the AI assistant which screen the
                // user is looking at (global screen awareness).
                UpdateAiScreenContext();
            }
        }

        /// <summary>
        /// Human-readable name of the active module, shown in the top-bar
        /// pill. Derived from the current ViewModel's type name using the
        /// same PrettyModuleName conversion the AI screen context uses, so
        /// the two can never disagree.
        ///
        /// The pill previously displayed the literal word "MODULE", which
        /// told the user nothing.
        /// </summary>
        public string CurrentModuleTitle
        {
            get
            {
                var raw = _currentViewModel?.GetType().Name
                    .Replace("ViewModel", "") ?? "";

                var pretty = PrettyModuleName(raw);
                return string.IsNullOrWhiteSpace(pretty)
                    ? "MODULE"
                    : pretty.ToUpperInvariant();
            }
        }

        /// <summary>
        /// Publishes the active module to AiScreenContext so the AI can
        /// answer "help me with this screen" without being told where
        /// the user is. Additive — nothing else consumes this.
        /// </summary>
        private void UpdateAiScreenContext()
        {
            try
            {
                var raw = _currentViewModel?.GetType().Name
                    .Replace("ViewModel", "") ?? "";

                // A module may OPT IN to publishing which record is open by
                // implementing IAgentContextAware. Modules that do not simply
                // report no detail - nothing is scraped or guessed, and no
                // other view-model needed editing to make this work.
                //
                // The detail is a short identifier only ("PI-2026-014"). ERP
                // values still require an audited read tool with a permission
                // check; they must never leak through a context label.
                string detail = "";

                if (_currentViewModel is ProGlassAutomation.Agent.Context
                        .IAgentContextAware aware)
                {
                    try { detail = aware.AgentContextDetail ?? ""; }
                    catch { detail = ""; }   // a broken module must not break nav
                }

                ProGlassAutomation.Services.AI.AIContext.AiScreenContext
                    .Set(PrettyModuleName(raw), detail);

                // Let the agent re-read the LIVE view-model when it is asked,
                // rather than relying on this snapshot. Selecting a different
                // record without navigating would otherwise leave the stored
                // detail stale, and a confidently wrong document reference is
                // worse than none.
                ProGlassAutomation.Agent.Context.AgentScreenContext
                    .CurrentViewModelProvider = () => _currentViewModel;
            }
            catch { /* screen awareness must never break navigation */ }
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

        /// <summary>
        /// True while the left navigation drawer is visible.
        /// The dim overlay behind the drawer is bound to this property.
        /// </summary>
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set { if (_isMenuOpen == value) return; _isMenuOpen = value; OnPropertyChanged(); }
        }

        // ── connectivity indicator (top bar pill) ─────────────────────

        private bool _isOnline = true;

        /// <summary>Live internet state — drives the top-bar pill.</summary>
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

        // ── signed-in user (top-bar pill) ──────────────────────────────
        // Mirrors the mockup's user pill, but bound to the REAL signed-in
        // identity (AuditService.CurrentUser) instead of a hardcoded name.

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
        /// <summary>Role of the signed-in user ("Administrator", "Manager", "Operator").</summary>
        public string CurrentUserRole
        {
            get => _currentUserRole;
            private set { if (_currentUserRole == value) return; _currentUserRole = value; OnPropertyChanged(); }
        }

        /// <summary>Two-letter initials derived from the display name.</summary>
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
                    _ => (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1))
                        .ToUpperInvariant()
                };
            }
        }

        // ── storage footer (sidebar) ───────────────────────────────────
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

        // ── live clock (top bar) — auto-updating date & time ─────────
        private string _currentDateText = "";
        public string CurrentDateText
        {
            get => _currentDateText;
            private set
            {
                if (_currentDateText == value) return;
                _currentDateText = value;
                OnPropertyChanged();
            }
        }

        private string _currentTimeText = "";
        public string CurrentTimeText
        {
            get => _currentTimeText;
            private set
            {
                if (_currentTimeText == value) return;
                _currentTimeText = value;
                OnPropertyChanged();
            }
        }

        private System.Windows.Threading.DispatcherTimer? _clockTimer;

        /// <summary>Starts the top-bar clock; ticks every second.</summary>
        private void StartClock()
        {
            UpdateClock();
            _clockTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, __) => UpdateClock();
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentDateText = now.ToString("ddd, dd MMM yyyy");
            CurrentTimeText = now.ToString("HH:mm:ss");
        }

        /// <summary>Public refresh of the top-bar user pill after a switch.</summary>
        public void RefreshCurrentUserPublic() => RefreshCurrentUser();

        /// <summary>
        /// Opens the Settings module on a specific section (Appearance / AI /
        /// Agent / Voice / Data / Updates / About). Used by the quick-settings
        /// popup so a row navigates to that section without the popup blocking.
        /// </summary>
        public void OpenSettingsPage(string page)
        {
            CurrentViewModel = SettingsVM;
            // Route through the public command (the SettingsPage setter is
            // private and validates the key).
            SettingsVM.SwitchSettingsPageCommand.Execute(page ?? "Appearance");
            IsSettingsOpen = false;
            IsMenuOpen = false;
        }

        private void RefreshCurrentUser()
        {
            var user = Services.Audit.AuditService.CurrentUser;
            CurrentUserDisplayName =
                string.IsNullOrWhiteSpace(user?.DisplayName)
                    ? Services.Audit.AuditService.CurrentUsername
                    : user!.DisplayName!;
            CurrentUserRole = PrettyRole(user?.Role);
        }

        /// <summary>Normalises the stored role into a friendly label.</summary>
        private static string PrettyRole(string? role) => role switch
        {
            "Admin" => "Administrator",
            "Manager" => "Manager",
            "Operator" => "Operator",
            "Store" => "Store",
            _ => string.IsNullOrWhiteSpace(role) ? "User" : role
        };

        /// <summary>
        /// Computes the app's data-folder size for the sidebar storage
        /// footer. Never throws — the footer is cosmetic and must not break
        /// the shell if the folder is missing or locked.
        /// </summary>
        private void RefreshStorage()
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation");

                long bytes = 0;
                if (Directory.Exists(folder))
                {
                    foreach (var f in Directory.EnumerateFiles(
                        folder, "*", SearchOption.AllDirectories))
                    {
                        try { bytes += new FileInfo(f).Length; }
                        catch { /* skip locked/in-use files */ }
                    }
                }

                double mb = bytes / (1024d * 1024d);
                StorageDisplay = mb >= 1024
                    ? $"{mb / 1024:N2} GB"
                    : $"{mb:N0} MB";
                StorageSubtitle =
                    $"Stored locally · {StorageDisplay} on disk";
            }
            catch
            {
                StorageDisplay = "—";
                StorageSubtitle = "Storage unavailable";
            }
        }

        // ── theme toggle (top bar, before the AI agent) ──────────────────

        private bool _isDarkTheme;
        /// <summary>True when Dark theme is active (mirrors Settings).</summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            private set
            {
                if (_isDarkTheme == value) return;
                _isDarkTheme = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Toggles Light/Dark theme from the top-bar switch. Delegates to the
        /// Settings ViewModel so the visual-theme rebuild + audit trail are
        /// exactly the same path as the Appearance screen.
        /// </summary>
        public ICommand ToggleThemeCommand { get; private set; }

        // ── commands ──────────────────────────────────────────────────────

        /// <summary>Toggles the left navigation drawer.</summary>
        public ICommand ToggleMenuCommand { get; }

        /// <summary>
        /// Opens a fresh Proforma Invoice in the wizard. Wired to the
        /// sidebar's "New Invoice" sub-item (real action, not a placeholder).
        /// </summary>
        public ICommand NewProformaInvoiceCommand { get; }

        /// <summary>Navigates to a module by string key.</summary>
        public ICommand NavigateCommand { get; }

        /// <summary>Navigates to the About page.</summary>
        public ICommand ShowAboutCommand { get; }

        /// <summary>v3.1: Navigates to the Settings page (was toggle drawer).</summary>
        public ICommand ShowSettingsCommand { get; }

        /// <summary>
        /// Toggles the secure user-switch popup. Lists the active users and
        /// switches the signed-in identity only after the selected user's
        /// password is verified (reuses AuditService.SwitchUserAsync, so a
        /// user whose password was set by someone else cannot be entered
        /// without that password). Non-blocking Popup.
        /// </summary>
        public ICommand ShowUserSwitchCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public MainViewModel()
        {
            // ── construct modules ─────────────────────────────────────────
            DashboardVM = new DashboardViewModel();
            ProformaInvoiceVM = new ProformaInvoiceViewModel();

            // Chat 24 v1.2: Pass voice services to AI chat ViewModel.
            // App.SpeechToText / App.TextToSpeech are initialized in
            // App.xaml.cs step 10b. They may be null if Windows speech
            // recognition or synthesis is not available on this machine
            // (missing language pack, no microphone, etc.).
            // AiChatViewModel handles null gracefully — voice buttons
            // simply hide when services are unavailable.
            AiChatVM = new AiChatViewModel(App.Ai, App.SpeechToText, App.TextToSpeech);

            // PHASE L: agent chat toggle.
            ToggleAgentChatCommand = new RelayCommand(
                _ => IsAgentChatOpen = !IsAgentChatOpen);

            // ── theme toggle (top bar) ────────────────────────────────────
            IsDarkTheme =
                ProGlassAutomation.Services.ThemeManager.CurrentTheme
                == ProGlassAutomation.Services.AppTheme.Dark;
            SettingsVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDarkTheme))
                    IsDarkTheme = SettingsVM.IsDarkTheme;
            };
            ToggleThemeCommand = new RelayCommand(_ =>
            {
                // Reuses the full Settings path (visual theme rebuild + audit).
                SettingsVM.IsDarkTheme = !SettingsVM.IsDarkTheme;
            });

            // PHASE E: global search navigates through the SAME Navigate()
            // switch the sidebar uses, so wallpaper + AI screen context stay
            // correct no matter how the user reached a module.
            GlobalSearchVM = new GlobalSearchViewModel(target => Navigate(target));

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
            //   → aggregates delivered qty across ALL sibling DNs
            //   → creates Master Invoice covering all delivered pieces
            DeliveryNoteVM.OnRequestOpenTaxInvoiceForMasterFromProforma =
                async proformaId =>
                {
                    Navigate("TaxInvoice");
                    await TaxInvoiceVM.CreateFromProformaAsync(proformaId);
                };

            // Path 2: DN Detail "Convert to Tax Invoice" on history entry
            //   → uses ONLY that specific DN's delivered qty
            //   → creates Partial Invoice with just that DN's rows
            DeliveryNoteVM.OnRequestOpenTaxInvoiceForDeliveryNote =
                async deliveryNoteId =>
                {
                    Navigate("TaxInvoice");
                    await TaxInvoiceVM.CreateFromDeliveryNoteAsync(deliveryNoteId);
                };

            DeliveryNoteVM.OnRequestAutoCreateTaxInvoice =
                async deliveryNoteId =>
                    await TaxInvoiceVM.CreateAndSaveDraftFromDeliveryNoteAsync(
                        deliveryNoteId);

            // ── commands ──────────────────────────────────────────────────
            ToggleMenuCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = !IsMenuOpen;
            });

            NavigateCommand = new RelayCommand(Navigate);

            NewProformaInvoiceCommand = new RelayCommand(_ =>
            {
                Navigate("ProformaInvoice");
                try { ProformaInvoiceVM.TriggerNewInvoice(); }
                catch { /* never let the shell fail on a new-invoice shortcut */ }
            });

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

            // Toggles the user-switch popup (non-blocking). The popup's
            // Opened event refreshes the user list and the pill refresh is
            // handled after it closes, so switching reflects immediately.
            ShowUserSwitchCommand = new RelayCommand(_ =>
                IsUserSwitchOpen = !IsUserSwitchOpen);

            // Top-bar icon buttons open non-blocking popups (consistent with
            // the user card and agent): single click opens, click again closes,
            // and clicking a module dismisses them. They never navigate away
            // to a full module, so they don't interfere with module content.
            ToggleSettingsCommand = new RelayCommand(_ =>
                IsSettingsOpen = !IsSettingsOpen);
            ToggleNotificationsCommand = new RelayCommand(_ =>
                IsNotificationsOpen = !IsNotificationsOpen);

            // ── connectivity pill: follow the shared ConnectivityService ──
            if (App.Connectivity != null)

            {
                _isOnline = App.Connectivity.IsOnline;
                App.Connectivity.ConnectivityChanged += (_, online) =>
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher == null) return;

                    if (dispatcher.CheckAccess()) IsOnline = online;
                    else dispatcher.InvokeAsync(() => IsOnline = online);
                };
            }

            // default landing page
            CurrentViewModel = DashboardVM;

            // v2 §9: the landing module gets its wallpaper layer too
            // (constructor bypasses Navigate()).
            ApplyModuleWallpaper("Dashboard");

            // Live clock on the top bar.
            StartClock();

            // Signed-in user pill + storage footer.
            RefreshCurrentUser();
            RefreshStorage();
            Services.Audit.AuditService.CurrentUserChanged += (_, __) =>
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshCurrentUser);

            // ── PHASE K: hand the agent its two shell capabilities ────────
            //
            // Both are assigned BEFORE AgentHost.Ensure() ever runs (the
            // agent is built lazily when Settings > AI & Agent is opened),
            // so the built instance captures them.
            //
            // Until now both were null: the navigate tool was registered but
            // inert, and every Write/Destructive tool would have been denied
            // for want of a confirmation prompt. Nothing else about the
            // agent's posture changes — navigation still goes through the
            // same switch the sidebar uses, and the prompt is a real modal
            // the user must accept.
            try
            {
                ProGlassAutomation.Agent.Core.AgentHost.Navigate = NavigateFromAgent;
                ProGlassAutomation.Agent.Core.AgentHost.Confirm = AgentConfirmAsync;
            }
            catch (Exception ex)
            {
                // The shell must start even if the agent assembly misbehaves.
                System.Diagnostics.Debug.WriteLine(
                    $"[MainViewModel] agent wiring failed: {ex.Message}");
            }
        }

        /// <summary>
        /// PHASE K — the confirmation gate for agent actions that write or
        /// destroy. Returns true only on an explicit user Yes.
        ///
        /// Fail-closed by construction: any exception, or no UI thread,
        /// returns false, so an action can never proceed unconfirmed.
        /// Destructive prompts carry the "IRREVERSIBLE" wording the agent
        /// supplies and default to No.
        /// </summary>
        private static System.Threading.Tasks.Task<bool> AgentConfirmAsync(
            string title, string message, bool destructive)
        {
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return System.Threading.Tasks.Task.FromResult(false);

                bool Ask()
                {
                    var result = System.Windows.MessageBox.Show(
                        message,
                        string.IsNullOrWhiteSpace(title) ? "ProGlass Agent" : title,
                        System.Windows.MessageBoxButton.YesNo,
                        destructive
                            ? System.Windows.MessageBoxImage.Warning
                            : System.Windows.MessageBoxImage.Question,
                        System.Windows.MessageBoxResult.No);

                    return result == System.Windows.MessageBoxResult.Yes;
                }

                bool answer = dispatcher.CheckAccess() ? Ask() : dispatcher.Invoke(Ask);
                return System.Threading.Tasks.Task.FromResult(answer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainViewModel] agent confirm failed: {ex.Message}");
                return System.Threading.Tasks.Task.FromResult(false);
            }
        }

        // ── navigation ────────────────────────────────────────────────────
        /// <summary>
        /// PHASE K — public navigation entry point for the ProGlass Agent.
        ///
        /// The agent's NavigateTool holds an Action&lt;string&gt; and has no
        /// reference to the shell. Wiring it here routes agent navigation
        /// through the SAME switch the sidebar uses, so the agent can never
        /// reach a screen the user could not reach themselves, and any
        /// module added to the switch is automatically available to both.
        ///
        /// Marshals to the UI thread: the agent runs its pipeline on a
        /// background task, and CurrentViewModel drives WPF bindings.
        /// </summary>
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
                // Central Reporting — module-based (Master Prompt)
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

                // v3.1: NEW Settings navigation
                case "Settings": CurrentViewModel = SettingsVM; break;

                // NEW: System Center (Users + Activity Log)
                case "SystemCenter":
                    CurrentViewModel = SystemCenterVM;
                    SystemCenterVM.LoadUsersAsync();
                    break;

                // Phase 1: Company Center + User Center modules.
                case "CompanyCenter":
                    CurrentViewModel = CompanyCenterVM;
                    break;
                case "UserCenter":
                    CurrentViewModel = UserCenterVM;
                    break;

                // PHASE C: Job Profitability — reloads on entry so the
                // figures always reflect the latest saved invoices.
                case "JobProfitability":
                    CurrentViewModel = JobProfitabilityVM;
                    JobProfitabilityVM.RefreshCommand.Execute(null);
                    break;

                // NEW: Logs & Activity (technical log + per-module feeds)
                case "Diagnostics":
                    CurrentViewModel = DiagnosticsVM;
                    DiagnosticsVM.OnShown();
                    break;
            }

            // v2 §9: each module can carry its own wallpaper.
            ApplyModuleWallpaper(parameter?.ToString());

            IsMenuOpen = false;
        }

        /// <summary>
        /// Resolves the wallpaper module key for a navigation target and
        /// lets the visual theme engine layer it. Cheap when the module
        /// has no wallpaper configured.
        /// </summary>
        private static void ApplyModuleWallpaper(string? navKey)
        {
            // Static constants — referenced directly (a static class
            // cannot be assigned to a variable).
            string moduleKey = navKey switch
            {
                "Dashboard" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.Dashboard,
                "SheetStore" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.SheetStore,
                "ProformaInvoice" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.ProformaInvoice,
                "JobOrder" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.JobOrder,
                "DeliveryNote" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.DeliveryNote,
                "TaxInvoice" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.TaxInvoice,
                "SalesReport" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.SalesReport,
                "Optimization" or "Cutting" or "HolesCutouts" or "Polish"
                    or "EdgeDeletion" or "Mitring" or "Tempering"
                    or "Pairing" or "DoublingLamination" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.Optimization,
                "Diagnostics" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.Logs,
                "SystemCenter" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.SystemCenter,
                "Settings" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.Settings,
                "About" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.About,
                "CompanyCenter" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.CompanyCenter,
                "UserCenter" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.UserCenter,
                "JobProfitability" =>
                    ProGlassAutomation.Services.Theme.WallpaperModules.JobProfitability,
                _ => ""
            };

            ProGlassAutomation.Services.Theme.VisualThemeEngine.ApplyModule(
                moduleKey, App.Config?.Appearance);
        }
    }
}
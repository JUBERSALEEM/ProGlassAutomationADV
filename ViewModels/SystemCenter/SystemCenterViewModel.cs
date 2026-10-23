// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/System/SystemCenterViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: System Center — Users + Activity Log (audit system UI)
//
// Two tabs:
//   • Users        — create users, activate/deactivate, switch the
//                    active session user (password verified).
//   • Activity Log — the full audit trail across ALL sections with a
//                    section filter; live-updates as actions happen.
//
// All persistence goes through Services/Audit/AuditService.cs.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Network;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.SystemCenter
{
    public class SystemCenterViewModel : ViewModelBase, IDisposable
    {
        private readonly IDialogService _dialogs = null!;
        private readonly UserPresenceService _presence = null!;

        public SystemCenterViewModel(IDialogService? dialogs, UserPresenceService? presence)
        {
            _dialogs = dialogs!;
            _presence = presence!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        // Phase 4: Named handlers for static events — detachable
        private EventHandler? _currentUserChangedHandler;
        private bool _disposed;

        public const string TabUsers = "Users";
        public const string TabActivity = "Activity";

        private const int MaxActivityRows = 300;

        // PERF: tab switches must be instant. A reload only happens when
        // the data is stale/empty; the live subscriptions already keep
        // both lists current between reloads.
        private static readonly TimeSpan ReloadStale =
            TimeSpan.FromSeconds(30);
        private DateTime _lastActivityLoadUtc = DateTime.MinValue;
        private DateTime _lastUsersLoadUtc = DateTime.MinValue;

        public SystemCenterViewModel()
         : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<UserPresenceService>()?? App.Presence){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            // ── Tab state ─────────────────────────────────────────────
            SwitchTabCommand = new RelayCommand(param =>
            {
                ActiveTab = param as string ?? TabUsers;
                if (ActiveTab == TabActivity)
                {
                    if (ActivityRows.Count == 0 ||
                        DateTime.UtcNow - _lastActivityLoadUtc > ReloadStale)
                        LoadActivityAsync();
                }
                else
                {
                    if (Users.Count == 0 ||
                        DateTime.UtcNow - _lastUsersLoadUtc > ReloadStale)
                        LoadUsersAsync();
                }
            });

            // ── Users commands ────────────────────────────────────────
            CreateUserCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(CreateUserAsync, "SystemCenter CreateUser"));
            ToggleActiveCommand = new RelayCommand(param =>
                AsyncUtility.SafeFireAndForget(() => ToggleActiveAsync(param as UserRowViewModel), "SystemCenter ToggleActive"));
            RefreshUsersCommand = new RelayCommand(_ => LoadUsersAsync());

            SwitchUserCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(SwitchUserAsync, "SystemCenter SwitchUser"));

            // v2: full role catalogue (permissions resolve per role)
            RoleOptions = new List<string>(
                ProGlassAutomation.Services.Audit.AppRoles.All);
            SelectedRole = ProGlassAutomation.Services.Audit.AppRoles.Viewer;

            // ── Activity commands ─────────────────────────────────────
            RefreshActivityCommand = new RelayCommand(_ => LoadActivityAsync());
            SetScopeCommand = new RelayCommand(
                param => SelectedScope = param as string ?? ScopeAll);

            // Phase 4: Named handlers — detachable, no lambda leak
            AuditService.ActivityLogged += OnActivityLogged;
            _currentUserChangedHandler = (_, __) =>
            {
                RefreshCurrentFlags();
                OnPropertyChanged(nameof(CurrentUserDisplay));
            };
            AuditService.CurrentUserChanged += _currentUserChangedHandler;

            // Same-network presence (UDP heartbeats from colleagues).
            if (_presence != null)
            {
                _presence.PeersChanged += (_, __) => RefreshPeers();
                RefreshPeers();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  NETWORK PRESENCE (same LAN)
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<Services.Network.NetworkPeer>
            OnlinePeers { get; } = new();

        private bool _hasPeers;
        public bool HasPeers
        {
            get => _hasPeers;
            private set { _hasPeers = value; OnPropertyChanged(); }
        }

        private void RefreshPeers()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            void Update()
            {
                OnlinePeers.Clear();
                foreach (var peer in _presence?.GetPeers() ?? new())
                    OnlinePeers.Add(peer);
                HasPeers = OnlinePeers.Count > 0;
            }

            if (dispatcher == null || dispatcher.CheckAccess()) Update();
            else dispatcher.InvokeAsync(Update);
        }

        // ══════════════════════════════════════════════════════════════
        //  TABS
        // ══════════════════════════════════════════════════════════════

        private string _activeTab = TabUsers;
        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab == value) return;
                _activeTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsUsersTab));
                OnPropertyChanged(nameof(IsActivityTab));
            }
        }

        public bool IsUsersTab => ActiveTab == TabUsers;
        public bool IsActivityTab => ActiveTab == TabActivity;

        public ICommand SwitchTabCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  USERS TAB
        // ══════════════════════════════════════════════════════════════

        private ObservableCollection<UserRowViewModel> _users = new();
        public ObservableCollection<UserRowViewModel> Users
        {
            get => _users;
            private set { _users = value; OnPropertyChanged(); }
        }

        public string CurrentUserDisplay =>
            AuditService.CurrentUser?.DisplayName ?? "(no active user)";

        // ── Create form ───────────────────────────────────────────────

        public List<string> RoleOptions { get; private set; }

        private string _selectedRole = "Operator";
        public string SelectedRole
        {
            get => _selectedRole;
            set { _selectedRole = value; OnPropertyChanged(); }
        }

        private string _newUsername = "";
        public string NewUsername
        {
            get => _newUsername;
            set { _newUsername = value; OnPropertyChanged(); }
        }

        private string _newDisplayName = "";
        public string NewDisplayName
        {
            get => _newDisplayName;
            set { _newDisplayName = value; OnPropertyChanged(); }
        }

        private string _newPassword = "";
        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        private string _userFormMessage = "";
        public string UserFormMessage
        {
            get => _userFormMessage;
            private set { _userFormMessage = value; OnPropertyChanged(); }
        }

        public ICommand CreateUserCommand { get; private set; }
        public ICommand ToggleActiveCommand { get; private set; }
        public ICommand RefreshUsersCommand { get; private set; }

        /// <summary>Loads the user table from audit.db.</summary>
        public async Task LoadUsersAsync()
        {
            var users = await AuditService.GetUsersAsync();

            // PERF: build the rows, then swap the collection ONCE —
            // one CollectionChanged event instead of one per row.
            var rows = users.Select(u => new UserRowViewModel
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedDisplay = u.CreatedAtUtc.ToLocalTime()
                    .ToString("dd MMM yyyy"),
                IsCurrent = u.Username == AuditService.CurrentUsername
            }).ToList();

            UiDispatch.Run(() =>
            {
                Users = new ObservableCollection<UserRowViewModel>(rows);
                _lastUsersLoadUtc = DateTime.UtcNow;
            });
        }

        private async Task CreateUserAsync()
        {
            var (ok, error) = await AuditService.CreateUserAsync(
                NewUsername, NewDisplayName, SelectedRole, NewPassword);

            if (ok)
            {
                UserFormMessage = $"✓ User '{NewUsername.Trim()}' created.";
                NewUsername = "";
                NewDisplayName = "";
                NewPassword = "";
                LoadUsersAsync();
            }
            else
            {
                UserFormMessage = $"⚠ {error}";
            }
        }

        private async Task ToggleActiveAsync(UserRowViewModel? row)
        {
            if (row == null) return;

            var (ok, error) = await AuditService.SetUserActiveAsync(
                row.Id, !row.IsActive);

            if (!ok)
            {
                _dialogs.Show(
                    error,
                    LocalizationService.Instance.T("SystemCenter.Users"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            LoadUsersAsync();
        }

        private void RefreshCurrentFlags()
        {
            foreach (var row in Users)
                row.IsCurrent = row.Username == AuditService.CurrentUsername;
        }

        // ── Switch active user ────────────────────────────────────────

        private string _switchUsername = "";
        public string SwitchUsername
        {
            get => _switchUsername;
            set { _switchUsername = value; OnPropertyChanged(); }
        }

        private string _switchPassword = "";
        public string SwitchPassword
        {
            get => _switchPassword;
            set { _switchPassword = value; OnPropertyChanged(); }
        }

        private string _switchMessage = "";
        public string SwitchMessage
        {
            get => _switchMessage;
            private set { _switchMessage = value; OnPropertyChanged(); }
        }

        public ICommand SwitchUserCommand { get; private set; }

        private async Task SwitchUserAsync()
        {
            var (ok, error) = await AuditService.SwitchUserAsync(
                SwitchUsername, SwitchPassword);

            SwitchMessage = ok
                ? $"✓ Now working as {AuditService.CurrentUser?.DisplayName}."
                : $"⚠ {error}";

            if (ok)
            {
                SwitchPassword = "";
                LoadUsersAsync();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ACTIVITY TAB — ACTION-FOCUSED (redesigned 2026-09-10)
        //
        //  The DB keeps recording EVERYTHING (audit integrity is never
        //  reduced). Only the SURFACED view is narrowed:
        //    • Scope chips  — Transactions / Inventory / Users & Security
        //                     / System, or All for the compliance view.
        //    • Important ON — hides low-signal rows (viewed / opened /
        //                     started / session / heartbeat …). Security
        //                     sections always pass.
        //    • Search       — free text over action, details, user.
        //  We fetch a larger window (1500) and filter client-side, so a
        //  narrow scope still shows real actions instead of 300 recent
        //  noise rows.
        // ══════════════════════════════════════════════════════════════

        private const int FetchActivityRows = 1500;

        /// <summary>Section sets behind the scope chips (AuditSections values).</summary>
        private static readonly Dictionary<string, HashSet<string>> ScopeMap =
            new()
            {
                ["Transactions"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    AuditSections.Proforma, AuditSections.JobOrder,
                    AuditSections.DeliveryNote, AuditSections.TaxInvoice,
                    AuditSections.CreditNote, AuditSections.Statement,
                    AuditSections.Inquiry
                },
                ["Inventory"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    AuditSections.SheetStore, AuditSections.Specifications,
                    AuditSections.MasterData
                },
                ["UsersSecurity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    AuditSections.Users, AuditSections.Settings,
                    AuditSections.Sharing, AuditSections.Updates
                },
                ["System"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    AuditSections.App, AuditSections.Dashboard,
                    AuditSections.Reports, AuditSections.Ai
                }
            };

        /// <summary>
        /// Security-relevant sections are ALWAYS important — they never get
        /// hidden by the noise-verb filter.
        /// </summary>
        private static bool IsSecuritySection(string section) =>
            section == AuditSections.Users ||
            section == AuditSections.Settings ||
            section == AuditSections.Sharing;

        /// <summary>
        /// Low-signal verbs. Conservative deny-list on purpose: when in
        /// doubt, a row stays visible.
        /// </summary>
        private static readonly string[] NoiseVerbs =
            { "view", "opened", "open ", "session", "heartbeat",
              "presence", "refresh", "hover", "navigat", "startup" };

        private static bool IsNoiseAction(ActivityLogEntry e)
        {
            if (IsSecuritySection(e.Section)) return false;
            var action = e.Action ?? string.Empty;
            foreach (var verb in NoiseVerbs)
            {
                if (action.ToLowerInvariant().Contains(verb)) return true;
            }
            return false;
        }

        private List<ActivityLogEntry> _allRecentEntries = new();

        private ObservableCollection<Shared.ActivityRowViewModel>
            _activityRows = new();
        public ObservableCollection<Shared.ActivityRowViewModel> ActivityRows
        {
            get => _activityRows;
            private set { _activityRows = value; OnPropertyChanged(); }
        }

        // ── Scope chips ───────────────────────────────────────────────

        public const string ScopeAll = "All";
        private string _selectedScope = ScopeAll;
        /// <summary>One of: All / Transactions / Inventory / UsersSecurity / System.</summary>
        public string SelectedScope
        {
            get => _selectedScope;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? ScopeAll : value;
                if (_selectedScope == next) return;
                _selectedScope = next;
                OnPropertyChanged();
                RaiseScopeFlags();
                ApplyActivityFilters();
            }
        }

        public bool IsScopeAll => _selectedScope == ScopeAll;
        public bool IsScopeTransactions => _selectedScope == "Transactions";
        public bool IsScopeInventory => _selectedScope == "Inventory";
        public bool IsScopeUsersSecurity => _selectedScope == "UsersSecurity";
        public bool IsScopeSystem => _selectedScope == "System";

        private void RaiseScopeFlags()
        {
            OnPropertyChanged(nameof(IsScopeAll));
            OnPropertyChanged(nameof(IsScopeTransactions));
            OnPropertyChanged(nameof(IsScopeInventory));
            OnPropertyChanged(nameof(IsScopeUsersSecurity));
            OnPropertyChanged(nameof(IsScopeSystem));
        }

        public ICommand SetScopeCommand { get; private set; }

        // ── Important-only + search ───────────────────────────────────

        private bool _importantOnly = true;
        /// <summary>Default ON: show actions, not every heartbeat.</summary>
        public bool ImportantOnly
        {
            get => _importantOnly;
            set
            {
                if (_importantOnly == value) return;
                _importantOnly = value;
                OnPropertyChanged();
                ApplyActivityFilters();
            }
        }

        private string _activitySearch = "";
        public string ActivitySearch
        {
            get => _activitySearch;
            set
            {
                var next = value ?? "";
                if (_activitySearch == next) return;
                _activitySearch = next;
                OnPropertyChanged();
                ApplyActivityFilters();
            }
        }

        private int _activityTotal;
        /// <summary>Rows in the current fetch window before filtering.</summary>
        public int ActivityTotal
        {
            get => _activityTotal;
            private set { _activityTotal = value; OnPropertyChanged(); }
        }

        private string _activityCountDisplay = "";
        public string ActivityCountDisplay
        {
            get => _activityCountDisplay;
            private set { _activityCountDisplay = value; OnPropertyChanged(); }
        }

        private bool _activityEmpty = true;
        public bool ActivityEmpty
        {
            get => _activityEmpty;
            private set { _activityEmpty = value; OnPropertyChanged(); }
        }

        public ICommand RefreshActivityCommand { get; private set; }

        /// <summary>
        /// Loads a wide audit window and applies the action-focused filters.
        /// </summary>
        public async Task LoadActivityAsync()
        {
            var entries = await AuditService.GetRecentAsync(
                FetchActivityRows, section: null);

            _allRecentEntries = entries ?? new List<ActivityLogEntry>();
            ActivityTotal = _allRecentEntries.Count;

            UiDispatch.Run(ApplyActivityFilters);
        }

        /// <summary>
        /// Single client-side pipeline: scope → importance → search → cap.
        /// Runs on whatever thread observed the change; the collection swap
        /// lands on the UI thread.
        /// </summary>
        private void ApplyActivityFilters()
        {
            IEnumerable<ActivityLogEntry> q = _allRecentEntries;

            if (SelectedScope != ScopeAll &&
                ScopeMap.TryGetValue(SelectedScope, out var sections))
            {
                q = q.Where(e => sections.Contains(e.Section ?? ""));
            }

            if (ImportantOnly)
                q = q.Where(e => !IsNoiseAction(e));

            var term = (ActivitySearch ?? "").Trim();
            if (term.Length > 0)
            {
                q = q.Where(e =>
                    (e.Action ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (e.Details ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (e.Username ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var rows = q.Take(MaxActivityRows).Select(ToRow).ToList();
            int shown = rows.Count;

            UiDispatch.Run(() =>
            {
                ActivityRows =
                    new ObservableCollection<Shared.ActivityRowViewModel>(rows);
                _lastActivityLoadUtc = DateTime.UtcNow;
                ActivityEmpty = shown == 0;
                ActivityCountDisplay = string.Format(
                    LocalizationService.Instance.T(
                        "SystemCenter.ActionsShownFormat"),
                    shown, ActivityTotal);
            });
        }

        private void OnActivityLogged(object? sender, ActivityEventArgs e)
        {
            // Keep the wide window fresh; the pipeline decides visibility.
            _allRecentEntries.Insert(0, e.Entry);
            if (_allRecentEntries.Count > FetchActivityRows)
                _allRecentEntries.RemoveAt(_allRecentEntries.Count - 1);
            ActivityTotal = _allRecentEntries.Count;

            ApplyActivityFilters();
        }

        private static Shared.ActivityRowViewModel ToRow(ActivityLogEntry e)
        {
            return new Shared.ActivityRowViewModel
            {
                TimeDisplay = e.TimestampUtc.ToLocalTime()
                    .ToString("dd MMM yyyy  HH:mm:ss"),
                Username = e.Username,
                Action = e.Action,
                Details = e.Details,
                Section = e.Section
            };
        }

        // Phase 4: Event lifetime — detach static events
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { AuditService.ActivityLogged -= OnActivityLogged; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SystemCenterViewModel] Dispose: ActivityLogged detach failed: " + ExceptionMessages.Deep(ex)); }
            try { AuditService.CurrentUserChanged -= _currentUserChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SystemCenterViewModel] Dispose: user-changed detach failed: " + ExceptionMessages.Deep(ex)); }
            GC.SuppressFinalize(this);
        }
    }
}

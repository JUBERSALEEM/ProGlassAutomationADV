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
using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.System
{
    /// <summary>One user row on the Users tab.</summary>
    public class UserRowViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public string CreatedDisplay { get; set; } = "";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isCurrent;
        /// <summary>True when this user is the active session user.</summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set { _isCurrent = value; OnPropertyChanged(); }
        }

        public string RoleBadge => Role == "Admin" ? "🛡 Admin"
            : Role == "Manager" ? "⭐ Manager" : "👷 Operator";
    }

    /// <summary>
    /// System Center — user management + full audit trail.
    /// </summary>
    public class SystemCenterViewModel : ViewModelBase
    {
        public const string TabUsers = "Users";
        public const string TabActivity = "Activity";

        private const int MaxActivityRows = 300;

        public SystemCenterViewModel()
        {
            // ── Tab state ─────────────────────────────────────────────
            SwitchTabCommand = new RelayCommand(param =>
            {
                ActiveTab = param as string ?? TabUsers;
                if (ActiveTab == TabActivity) LoadActivityAsync();
                else LoadUsersAsync();
            });

            // ── Users commands ────────────────────────────────────────
            CreateUserCommand = new RelayCommand(_ => CreateUser());
            ToggleActiveCommand = new RelayCommand(param =>
                ToggleActive(param as UserRowViewModel));
            RefreshUsersCommand = new RelayCommand(_ => LoadUsersAsync());

            SwitchUserCommand = new RelayCommand(_ => SwitchUser());

            RoleOptions = new List<string> { "Operator", "Manager", "Admin" };
            SelectedRole = "Operator";

            // ── Activity commands ─────────────────────────────────────
            RefreshActivityCommand = new RelayCommand(_ => LoadActivityAsync());

            // Live updates — service raises on the UI thread.
            AuditService.ActivityLogged += OnActivityLogged;
            AuditService.CurrentUserChanged += (_, __) =>
            {
                RefreshCurrentFlags();
                OnPropertyChanged(nameof(CurrentUserDisplay));
            };
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

        public ICommand SwitchTabCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  USERS TAB
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<UserRowViewModel> Users { get; } = new();

        public string CurrentUserDisplay =>
            AuditService.CurrentUser?.DisplayName ?? "(no active user)";

        // ── Create form ───────────────────────────────────────────────

        public List<string> RoleOptions { get; }

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

        public ICommand CreateUserCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand RefreshUsersCommand { get; }

        /// <summary>Loads the user table from audit.db.</summary>
        public async void LoadUsersAsync()
        {
            var users = await AuditService.GetUsersAsync();

            Users.Clear();
            foreach (var u in users)
            {
                Users.Add(new UserRowViewModel
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedDisplay = u.CreatedAtUtc.ToLocalTime()
                        .ToString("dd MMM yyyy"),
                    IsCurrent = u.Username == AuditService.CurrentUsername
                });
            }
        }

        private async void CreateUser()
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

        private async void ToggleActive(UserRowViewModel? row)
        {
            if (row == null) return;

            bool ok = await AuditService.SetUserActiveAsync(
                row.Id, !row.IsActive);

            if (!ok)
            {
                MessageBox.Show(
                    "Cannot change this user.\n\n" +
                    "The last active admin must stay active.",
                    "Users",
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

        public ICommand SwitchUserCommand { get; }

        private async void SwitchUser()
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
        //  ACTIVITY TAB
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<Shared.ActivityRowViewModel>
            ActivityRows { get; } = new();

        private List<string> _sectionFilters = new();
        /// <summary>"All sections" + every section that has activity.</summary>
        public List<string> SectionFilters
        {
            get => _sectionFilters;
            private set { _sectionFilters = value; OnPropertyChanged(); }
        }

        private string _selectedSectionFilter = "All sections";
        public string SelectedSectionFilter
        {
            get => _selectedSectionFilter;
            set
            {
                _selectedSectionFilter = value;
                OnPropertyChanged();
                LoadActivityAsync();
            }
        }

        private bool _activityEmpty = true;
        public bool ActivityEmpty
        {
            get => _activityEmpty;
            private set { _activityEmpty = value; OnPropertyChanged(); }
        }

        public ICommand RefreshActivityCommand { get; }

        /// <summary>Loads the audit trail (filtered by selected section).</summary>
        public async void LoadActivityAsync()
        {
            string? section = SelectedSectionFilter == "All sections"
                ? null
                : SelectedSectionFilter;

            var entries = await AuditService.GetRecentAsync(
                MaxActivityRows, section);

            ActivityRows.Clear();
            foreach (var e in entries)
                ActivityRows.Add(ToRow(e));

            ActivityEmpty = ActivityRows.Count == 0;

            // Refresh the section filter list (cheap, keeps it honest).
            var sections = await AuditService.GetSectionsWithActivityAsync();
            var filters = new List<string> { "All sections" };
            filters.AddRange(sections);
            SectionFilters = filters;

            if (!filters.Contains(SelectedSectionFilter))
                SelectedSectionFilter = "All sections";
        }

        private void OnActivityLogged(object? sender, ActivityEventArgs e)
        {
            // Respect the active filter.
            if (SelectedSectionFilter != "All sections" &&
                e.Entry.Section != SelectedSectionFilter)
                return;

            ActivityRows.Insert(0, ToRow(e.Entry));

            while (ActivityRows.Count > MaxActivityRows)
                ActivityRows.RemoveAt(ActivityRows.Count - 1);

            ActivityEmpty = ActivityRows.Count == 0;
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
    }
}

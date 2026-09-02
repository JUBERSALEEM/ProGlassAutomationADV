// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/UserCenter/UserCenterViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: User Center (Phase 1 — master data foundation)
//
// Dedicated module for the people who use the app:
//   • Create users (Admin / Manager / Operator) with hashed passwords
//   • Activate / deactivate
//   • Switch the active session user (verified by password) — the
//     active user's DisplayName auto-fills "Document Prepared By" in
//     Proforma Invoice Step 2 and signs every audit entry
//
// Built entirely on AuditService (audit.db) — no duplicated storage.
// Passwords are passed from the view's PasswordBoxes straight into
// AuditService and never held in VM properties.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.Shared;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.UserCenter
{
    /// <summary>One user card on the User Center page.</summary>
    public sealed class UserCard : ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsCurrent { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    public sealed class UserCenterViewModel : ViewModelBase, IDisposable
    {
        // Phase 4: Named handler for static event — detachable
        private EventHandler? _currentUserChangedHandler;
        private bool _disposed;

        private readonly ObservableCollection<UserCard> _users = new();
        public ObservableCollection<UserCard> Users => _users;

        public string[] RoleOptions { get; } =
        {
            "Operator", "Manager", "Admin"
        };

        // ── editor state ─────────────────────────────────────────────
        private string _username = "";
        private string _displayName = "";
        private string _role = "Operator";
        private string _status = "";
        private int _selectedUserId;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        public string SelectedRole
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }
        public string Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }
        public string CurrentUserName =>
            AuditService.CurrentUser?.DisplayName ?? "Administrator";

        private string _reasonNote = "";
        /// <summary>
        /// PHASE D — optional WHY note recorded with the "User created"
        /// audit entry ("new estimator joined the Dubai branch").
        /// Cleared after a successful create.
        /// </summary>
        public string ReasonNote
        {
            get => _reasonNote;
            set { _reasonNote = value ?? ""; OnPropertyChanged(); }
        }

        // ── A-3: default-credential warning ──────────────────────────
        private bool _isDefaultPasswordInUse;
        /// <summary>
        /// True while the seeded admin account still uses its default
        /// password. Drives a visible banner — this build has no sign-in
        /// screen, so warning is the honest option; blocking startup would
        /// lock the user out of their own offline data.
        /// </summary>
        public bool IsDefaultPasswordInUse
        {
            get => _isDefaultPasswordInUse;
            private set
            {
                _isDefaultPasswordInUse = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SelectUserCommand { get; }
        public ICommand ToggleActiveCommand { get; private set; }

        public UserCenterViewModel()
        {
            // Phase 4: Named handler — detachable, no lambda leak
            _currentUserChangedHandler = (_, __) =>
            {
                OnPropertyChanged(nameof(CurrentUserName));
                _ = LoadAsync();
            };
            AuditService.CurrentUserChanged += _currentUserChangedHandler;

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());

            SelectUserCommand = new RelayCommand(p =>
            {
                _selectedUserId = p is UserCard c ? c.Id : 0;
                foreach (var u in _users)
                    u.IsSelected = u.Id == _selectedUserId;
            });

            ToggleActiveCommand = new RelayCommand(async p =>
            {
                if (p is not UserCard card) return;
                if (card.IsCurrent)
                {
                    Status = "You cannot deactivate the user you are signed in as.";
                    return;
                }

                bool ok = await AuditService
                    .SetUserActiveAsync(card.Id, !card.IsActive);
                Status = ok
                    ? (card.IsActive ? "Deactivated: " : "Activated: ") + card.DisplayName
                    : "Could not change activation (last active Admin is protected).";
                await LoadAsync();
            });

            _ = LoadAsync();
        }

        public async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var current = AuditService.CurrentUser;
                var cards = (await AuditService.GetUsersAsync())
                    .OrderByDescending(x => x.IsActive)
                    .ThenBy(x => x.DisplayName)
                    .Select(u => new UserCard
                    {
                        Id = u.Id,
                        Username = u.Username,
                        DisplayName = u.DisplayName,
                        Role = u.Role,
                        IsActive = u.IsActive,
                        IsCurrent = current != null && current.Id == u.Id,
                        IsSelected = u.Id == _selectedUserId
                    })
                    .ToList();

                bool defaultPw = await AuditService.IsUsingDefaultPasswordAsync();

                UiDispatch.Run(() =>
                {
                    _users.Clear();
                    foreach (var card in cards)
                        _users.Add(card);
                    IsDefaultPasswordInUse = defaultPw;
                });
            }
            catch (Exception ex)
            {
                UiDispatch.Run(() => Status = "Load failed: " + ExceptionMessages.Deep(ex));
            }
        }

        /// <summary>
        /// Called by the view's code-behind with the raw password from
        /// its PasswordBox (passwords never live in VM properties).
        /// </summary>
        public async System.Threading.Tasks.Task CreateUserAsync(
            string password, string confirm)
        {
            if (string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(DisplayName))
            {
                Status = "Username and display name are required.";
                return;
            }
            if (password.Length <
                ProGlassAutomation.Services.Security.PasswordHasher.MinLength)
            {
                Status =
                    $"Password must be at least " +
                    $"{ProGlassAutomation.Services.Security.PasswordHasher.MinLength} characters.";
                return;
            }
            if (password != confirm)
            {
                Status = "Passwords do not match.";
                return;
            }

            var (ok, error) = await AuditService.CreateUserAsync(
                Username.Trim(), DisplayName.Trim(), SelectedRole, password,
                ReasonNote.Trim());

            Status = ok ? "User created ✔ " + DisplayName : error;
            if (ok)
            {
                Username = "";
                DisplayName = "";
                ReasonNote = "";
                await LoadAsync();
            }
        }

        /// <summary>
        /// Switch the active session user (password verified by
        /// AuditService). Called from the view with the raw password.
        /// </summary>
        public async System.Threading.Tasks.Task SwitchToSelectedAsync(
            string password)
        {
            var card = _users.FirstOrDefault(u => u.Id == _selectedUserId);
            if (card == null)
            {
                Status = "Select a user card first.";
                return;
            }

            var (ok, error) = await AuditService
                .SwitchUserAsync(card.Username, password);

            Status = ok
                ? "Signed in as " + card.DisplayName +
                  " — new invoices will show this name as preparer."
                : error;
        }

        /// <summary>
        /// A-3: rotate the selected user's password. Called from the
        /// view's code-behind with raw PasswordBox values, matching the
        /// existing convention that passwords never live in VM properties.
        /// </summary>
        public async System.Threading.Tasks.Task ChangePasswordAsync(
            string currentPassword, string newPassword, string confirm)
        {
            var card = _users.FirstOrDefault(u => u.Id == _selectedUserId);
            if (card == null)
            {
                Status = "Select a user card first.";
                return;
            }

            if (newPassword.Length <
                ProGlassAutomation.Services.Security.PasswordHasher.MinLength)
            {
                Status =
                    $"New password must be at least " +
                    $"{ProGlassAutomation.Services.Security.PasswordHasher.MinLength} characters.";
                return;
            }

            if (newPassword != confirm)
            {
                Status = "New passwords do not match.";
                return;
            }

            var (ok, error) = await AuditService.ChangePasswordAsync(
                card.Username, currentPassword, newPassword,
                ReasonNote.Trim());

            Status = ok
                ? "Password changed for " + card.DisplayName + " ✔"
                : error;

            if (ok)
            {
                ReasonNote = "";
                await LoadAsync();
            }
        }

        // Phase 4: Event lifetime — detach static event
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { AuditService.CurrentUserChanged -= _currentUserChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[UserCenterViewModel] Dispose: user-changed detach failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }
    }
}

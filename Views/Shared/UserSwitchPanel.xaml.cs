// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/UserSwitchPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — secure user switch (popup panel)
//
// A password-gated user switcher hosted in a WPF Popup so it is
// NON-BLOCKING: it floats over the shell without taking focus from (or
// blocking interaction with) the module content behind it. The popup is
// opened from the top-bar user card and dismissed by clicking elsewhere
// (or a second click).
//
// SECURITY MODEL
//   Every switch goes through AuditService.SwitchUserAsync, which verifies
//   the password against the stored hash (PBKDF2, with legacy SHA-256
//   migration). There is deliberately NO password-less path here: a user
//   whose password was set by someone else cannot be entered without that
//   password. The audit trail records each switch with the acting user.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Password-gated user switcher shown in a Popup (non-blocking).
    /// Reuses AuditService.SwitchUserAsync — the single auth path.
    /// </summary>
    public partial class UserSwitchPanel : UserControl
    {
        private readonly List<AppUser> _users = new();

        /// <summary>The user currently selected for switch (by Id).</summary>
        private AppUser? _selected;

        public UserSwitchPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        /// <summary>Reloads the active-user list when the panel opens.</summary>
        public async void RefreshAsync()
        {
            _users.Clear();
            _users.AddRange((await AuditService.GetUsersAsync())
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username));

            RenderList();
            // Reset selection so re-opening starts fresh.
            _selected = null;
            PasswordBox.Clear();
            ErrorText.Visibility = Visibility.Collapsed;
            SwitchButton.IsEnabled = false;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
            => await RefreshCoreAsync();

        private async Task RefreshCoreAsync()
        {
            _users.Clear();
            _users.AddRange((await AuditService.GetUsersAsync())
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username));
            RenderList();
        }

        private void RenderList()
        {
            UserList.Children.Clear();
            string current = AuditService.CurrentUsername;

            foreach (var user in _users)
                UserList.Children.Add(BuildCard(user, current));
        }

        private Border BuildCard(AppUser user, string currentUsername)
        {
            bool isCurrent = string.Equals(
                user.Username, currentUsername, StringComparison.OrdinalIgnoreCase);

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(isCurrent ? 1.5 : 1),
                Cursor = Cursors.Hand,
                Tag = user.Id,
                SnapsToDevicePixels = true
            };
            card.SetResourceReference(BackgroundProperty,
                isCurrent ? "TintIndigoBrush" : "ModuleBackgroundBrush");
            card.SetResourceReference(BorderBrushProperty,
                isCurrent ? "AccentBrush" : "OutlineBrush");

            card.MouseEnter += (_, _) =>
            {
                if (!isCurrent)
                    card.SetResourceReference(BackgroundProperty, "MenuHoverBrush");
            };
            card.MouseLeave += (_, _) =>
            {
                if (!isCurrent)
                    card.SetResourceReference(BackgroundProperty, "ModuleBackgroundBrush");
            };
            card.MouseLeftButtonUp += (_, _) => Select(user);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = GridLength.Auto });

            var left = new StackPanel();
            var nameLine = new TextBlock
            {
                Text = user.DisplayName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
            nameLine.SetResourceReference(TextBlock.ForegroundProperty,
                "PrimaryForegroundBrush");

            var meta = new TextBlock
            {
                Text = $"{PrettyRole(user.Role)}  ·  {user.Username}",
                FontSize = 10
            };
            meta.SetResourceReference(TextBlock.ForegroundProperty,
                "SecondaryForegroundBrush");

            left.Children.Add(nameLine);
            left.Children.Add(meta);
            Grid.SetColumn(left, 0);

            var right = new TextBlock
            {
                Text = isCurrent ? "\uE73E" : "\uE76C",   // checkmark / chevron
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            right.SetResourceReference(TextBlock.ForegroundProperty,
                isCurrent ? "SuccessBrush" : "MutedForegroundBrush");
            Grid.SetColumn(right, 1);

            grid.Children.Add(left);
            grid.Children.Add(right);
            card.Child = grid;
            return card;
        }

        private void Select(AppUser user)
        {
            _selected = user;
            bool isCurrent = string.Equals(
                user.Username, AuditService.CurrentUsername,
                StringComparison.OrdinalIgnoreCase);

            PasswordLabel.Text = isCurrent
                ? $"You are already signed in as {user.DisplayName}"
                : $"Enter the password for {user.DisplayName}";
            PasswordBox.Clear();
            ErrorText.Visibility = Visibility.Collapsed;
            SwitchButton.IsEnabled = !isCurrent;
            PasswordBox.IsEnabled = !isCurrent;

            if (!isCurrent) PasswordBox.Focus();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = TrySwitchAsync();
        }

        private async void OnSwitchClick(object sender, RoutedEventArgs e)
            => await TrySwitchAsync();

        private async Task TrySwitchAsync()
        {
            if (_selected == null) return;

            string password = PasswordBox.Password ?? "";
            if (password.Length == 0)
            {
                ShowError("Enter a password to switch users.");
                return;
            }

            SwitchButton.IsEnabled = false;
            SwitchButton.Content = "SWITCHING…";

            try
            {
                var (ok, error) = await AuditService.SwitchUserAsync(
                    _selected.Username, password);

                if (ok)
                {
                    PasswordBox.Clear();
                    // Let the shell refresh the user pill.
                    // The popup stays open; caller dismisses on demand.
                    return;
                }

                ShowError("Incorrect password. You cannot sign in as " +
                          $"{_selected.DisplayName} without their password.");
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
            catch (Exception ex)
            {
                ShowError("Switch failed. See the log for details.");
                Services.Logging.AppLogService.Error(
                    "User switch failed unexpectedly.", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Security",
                        Class = nameof(UserSwitchPanel),
                        Operation = "SwitchUser"
                    });
            }
            finally
            {
                SwitchButton.IsEnabled = _selected != null
                    && !string.Equals(_selected.Username,
                        AuditService.CurrentUsername,
                        StringComparison.OrdinalIgnoreCase);
                SwitchButton.Content = "SWITCH";
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private static string PrettyRole(string? role) => role switch
        {
            "Admin" => "Administrator",
            "Manager" => "Manager",
            "Operator" => "Operator",
            "Store" => "Store",
            _ => string.IsNullOrWhiteSpace(role) ? "User" : role
        };
    }
}

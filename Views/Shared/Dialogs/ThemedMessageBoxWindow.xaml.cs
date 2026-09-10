// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/Dialogs/ThemedMessageBoxWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Unified dialog platform — themed replacement for MessageBox
//
// Behavioural twin of System.Windows.MessageBox: same MessageBoxButton /
// MessageBoxImage / MessageBoxResult vocabulary, same default-button and
// Esc semantics, rendered with the shared ThemedDialogWindow chrome.
// Constructed and driven only through the ThemedMessageBox facade.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Shared.Dialogs
{
    public partial class ThemedMessageBoxWindow : ThemedDialogWindow
    {
        private MessageBoxButton _buttons;
        private MessageBoxResult _result;

        private ThemedMessageBoxWindow()
        {
            InitializeComponent();
            MessageText.Text = string.Empty;
        }

        /// <summary>Configures the box; returns the window ready to ShowDialog().</summary>
        internal static ThemedMessageBoxWindow Create(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage icon,
            MessageBoxResult defaultResult)
        {
            var (glyph, brushKey) = MapIcon(icon);

            var window = new ThemedMessageBoxWindow
            {
                Title = caption,
                DialogIcon = glyph
            };
            window.Message = message;
            window.Configure(buttons, icon, defaultResult);

            if (!string.IsNullOrEmpty(brushKey))
                window.SetResourceReference(DialogIconBrushProperty, brushKey);

            return window;
        }

        internal string Message
        {
            get => MessageText.Text;
            set => MessageText.Text = value ?? string.Empty;
        }

        /// <summary>The result of ShowDialog(); use GetResult() after it closes.</summary>
        internal MessageBoxResult GetResult()
            => _result == MessageBoxResult.None
                ? ResultWhenClosedWithoutButton
                : _result;

        /// <summary>MessageBox semantics for closing without a button (Esc / ✕).</summary>
        private MessageBoxResult ResultWhenClosedWithoutButton => _buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.None
        };

        // ─────────────────────────────────────────────────────────────────
        //  BUTTON CONSTRUCTION
        // ─────────────────────────────────────────────────────────────────

        private void Configure(
            MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            _buttons = buttons;

            // Destructive confirmations (delete / void / discard flows come
            // in as Warning/Error prompts) get the danger treatment so the
            // default button reads as consequential.
            bool destructive = icon is MessageBoxImage.Warning or MessageBoxImage.Error
                                     or MessageBoxImage.Hand or MessageBoxImage.Stop;

            var host = new StackPanel { Orientation = Orientation.Horizontal };

            void AddButton(MessageBoxResult result, int index, int count)
            {
                bool isPrimary = result is MessageBoxResult.OK or MessageBoxResult.Yes;
                bool isDefault = defaultResult == MessageBoxResult.None
                    ? isPrimary
                    : defaultResult == result;

                string key = KeyFor(result);

                var btn = new Button
                {
                    Content = TryLocalize(key, out var label) ? label : key,
                    Style = (Style)FindResource(
                        isPrimary && destructive ? "DangerButton"
                        : isPrimary ? "PrimaryPillButton"
                        : "OutlinedPillButton"),
                    IsDefault = isDefault,
                    IsCancel = result == MessageBoxResult.Cancel
                               || (buttons == MessageBoxButton.YesNo
                                   && result == MessageBoxResult.No)
                };

                if (TryLocalize(key + ".Glyph", out var glyph))
                    btn.Tag = glyph; // pill templates render Tag as the MDL2 icon

                if (index < count - 1)
                    btn.Margin = new Thickness(0, 0, 10, 0);

                btn.Click += (_, __) =>
                {
                    _result = result;
                    Close();
                };

                host.Children.Add(btn);
            }

            // Order matches the native box: [Yes] [No] [Cancel] / [OK] [Cancel].
            (MessageBoxResult Result, int Index)[] plan = buttons switch
            {
                MessageBoxButton.OK => new[] { (MessageBoxResult.OK, 0) },
                MessageBoxButton.OKCancel => new[] { (MessageBoxResult.OK, 0), (MessageBoxResult.Cancel, 1) },
                MessageBoxButton.YesNo => new[] { (MessageBoxResult.Yes, 0), (MessageBoxResult.No, 1) },
                MessageBoxButton.YesNoCancel => new[]
                {
                    (MessageBoxResult.Yes, 0), (MessageBoxResult.No, 1), (MessageBoxResult.Cancel, 2)
                },
                _ => new[] { (MessageBoxResult.OK, 0) }
            };

            for (int i = 0; i < plan.Length; i++)
                AddButton(plan[i].Result, i, plan.Length);

            FooterContent = host;
        }

        // ─────────────────────────────────────────────────────────────────
        //  MAPPING HELPERS
        // ─────────────────────────────────────────────────────────────────

        private static string KeyFor(MessageBoxResult result) => result switch
        {
            MessageBoxResult.OK => "Action.OK",
            MessageBoxResult.Yes => "Action.Yes",
            MessageBoxResult.No => "Action.No",
            _ => "Action.Cancel"
        };

        private static bool TryLocalize(string key, out string value)
        {
            value = Services.Localization.LocalizationService.Instance.T(key);
            // LocalizationService returns the key itself when missing.
            return !string.IsNullOrEmpty(value)
                   && !string.Equals(value, key, StringComparison.Ordinal);
        }

        // WPF's MessageBoxImage members alias the same underlying values
        // (Information == Asterisk = 64, Warning == Exclamation = 48,
        // Error == Hand == Stop = 16), so each VALUE gets exactly ONE arm —
        // listing alias names as extra arms is unreachable code (CS8510).
        // Note: 'Help' does not exist on System.Windows.MessageBoxImage
        // (that is Windows.Forms.MessageBoxIcon) — CS0117 if added.
        private static (string Glyph, string? BrushKey) MapIcon(MessageBoxImage icon) => icon switch
        {
            MessageBoxImage.Information => ("\uE946", "InfoBrush"),   // 64 (Asterisk)
            MessageBoxImage.Warning => ("\uE7BA", "WarningBrush"),    // 48 (Exclamation)
            MessageBoxImage.Error => ("\uE783", "DangerBrush"),       // 16 (Hand, Stop)
            MessageBoxImage.Question => ("\uE897", null),             // 32
            _ => (string.Empty, null)                                 // None + fallback
        };
    }

    public static class ThemedMessageBox
    {
        public static MessageBoxResult Show(
            string message,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
            => Show(null, message, caption, button, icon, defaultResult);

        public static MessageBoxResult Show(
            Window? owner,
            string message,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
            => OnUi(() =>
            {
                owner ??= Application.Current?.MainWindow;
                if (owner == null || !owner.IsLoaded)
                    owner = null;

                try
                {
                    var window = ThemedMessageBoxWindow.Create(
                        message, caption, button, icon, defaultResult);

                    if (owner != null)
                        window.Owner = owner;

                    window.ShowDialog();
                    return window.GetResult();
                }
                catch
                {
                    // Never swallow a prompt: degrade to the native box.
                    return MessageBox.Show(owner, message, caption, button, icon);
                }
            });

        private static T OnUi<T>(Func<T> work)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return work();
            return dispatcher.Invoke(work);
        }
    }
}

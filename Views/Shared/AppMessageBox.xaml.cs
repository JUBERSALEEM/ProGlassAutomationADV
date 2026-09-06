// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/AppMessageBox.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: One professional dialog shell for the whole app
//
// Code-behind keeps the MessageBox.Show semantics EXACTLY (buttons,
// icons, default result, DialogResult mapping). Contract and invariants
// live in the XAML file header; pins in
// Tests/Agent/AppMessageBoxSourceTests.cs.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Themed replacement for <see cref="MessageBox"/>. Same inputs,
    /// same <see cref="MessageBoxResult"/> outputs.
    /// </summary>
    public partial class AppMessageBox : Window
    {
        // ── Bound surface (view-side, set before show) ────────────────
        public string Caption { get; private set; } = string.Empty;
        public string BodyText { get; private set; } = string.Empty;
        public string IconGlyph { get; private set; } = "\uE946";       // info circle
        public Brush IconBrush { get; private set; } = Brushes.White;
        public Brush IconTint { get; private set; } = Brushes.Transparent;

        private MessageBoxButton _buttons;
        private MessageBoxResult _defaultResult;

        /// <summary>
        /// ERR-36432A: this constructor did not exist.
        ///
        /// Without it the compiler-generated InitializeComponent() never
        /// ran, so NONE of the x:Name fields were ever assigned —
        /// OkButton, YesButton, NoButton and CancelButton were all null.
        /// The visual tree was never loaded at all, which is why the
        /// crash was a bare NullReferenceException on the very first
        /// line of ConfigureButtons() rather than anything descriptive.
        ///
        /// A parameterless WPF Window compiles fine without a
        /// constructor — the default one is generated — but the default
        /// constructor does NOT call InitializeComponent(). That call
        /// only appears in a hand-written ctor, so a partial Window
        /// class with no ctor silently produces an empty window. Nothing
        /// warns about it, and the class is only reachable through the
        /// static ShowBox() factory, so it looked deliberate.
        ///
        /// Every AppMessageBox.Show in the app was therefore dead:
        /// any code path that tried to show a message box crashed
        /// instead, which meant error reporting itself was the thing
        /// that failed. Bugs hid behind it.
        /// </summary>
        public AppMessageBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Themed equivalent of MessageBox.Show(owner, message, caption,
        /// buttons, icon, defaultResult). Falls back to the caller's
        /// responsibility when the themed shell cannot open (owner-gone
        /// etc.) — DialogService owns that fallback decision.
        /// </summary>
        public static MessageBoxResult ShowBox(
            Window? owner,
            string message,
            string? caption,
            MessageBoxButton buttons,
            MessageBoxImage icon,
            MessageBoxResult defaultResult)
        {
            var box = new AppMessageBox
            {
                _buttons = buttons,
                _defaultResult = defaultResult,
                Caption = string.IsNullOrWhiteSpace(caption)
                    ? (System.Windows.Application.Current?.MainWindow?.Title ?? "")
                    : caption,
                BodyText = message ?? string.Empty,
            };

            if (owner != null && owner.IsVisible)
                box.Owner = owner;
            else
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            box.ConfigureIcon(icon);
            box.ConfigureButtons();

            box.ShowDialog();
            return box.DialogTag;   // set by the button handlers
        }

        public MessageBoxResult DialogTag { get; private set; } = MessageBoxResult.None;

        private void ConfigureIcon(MessageBoxImage icon)
        {
            // Severity chip: ONE tinted square + glyph (never a full
            // colored background — the 90s-error-window look). Brushes
            // resolve from the ACTIVE theme; hex fallbacks cover the
            // edge where a theme merge has not finished yet.
            static Brush Resolve(string key, string hex)
                => System.Windows.Application.Current?.TryFindResource(key) is Brush b
                    ? b
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

            switch (icon)
            {
                case MessageBoxImage.Error:
                    IconGlyph = "\uEA39";
                    IconBrush = Resolve("DangerBrush", "#F87171");
                    IconTint  = Resolve("TintRedBrush", "#3F1D1D");
                    break;
                case MessageBoxImage.Warning:
                    IconGlyph = "\uE7BA";
                    IconBrush = Resolve("WarningBrush", "#FBBF24");
                    IconTint  = Resolve("TintAmberBrush", "#3B2A10");
                    break;
                case MessageBoxImage.Question:
                    IconGlyph = "\uE897";
                    IconBrush = Resolve("AccentBrush", "#60A5FA");
                    IconTint  = Resolve("TintBlueBrush", "#13283F");
                    break;
                default:      // Information + None
                    IconGlyph = "\uE946";
                    IconBrush = Resolve("InfoBrush", "#38BDF8");
                    IconTint  = Resolve("TintBlueBrush", "#13283F");
                    break;
            }

            // Tint is the chip's background; never allow a near-transparent
            // tint (design invariant: the chip must always read as a chip).
            if (IconTint is SolidColorBrush s && s.Color.A < 0x18)
                IconTint = new SolidColorBrush(Color.FromArgb(0x33, 0x60, 0xA5, 0xFA));
        }

        private void ConfigureButtons()
        {
            OkButton.Visibility = Visibility.Collapsed;
            YesButton.Visibility = Visibility.Collapsed;
            NoButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;

            switch (_buttons)
            {
                case MessageBoxButton.OK:
                    OkButton.Visibility = Visibility.Visible;
                    if (_defaultResult != MessageBoxResult.None) OkButton.Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    OkButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    // OS MessageBox: Yes/No has no Cancel-on-Esc (Esc = No
                    // would silently deny); IsCancel=False on No kept.
                    break;
                case MessageBoxButton.YesNoCancel:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
            }

            // Default-result honour: focus the button matching the default
            // (OS MessageBox presses it on Enter).
            if (_defaultResult == MessageBoxResult.No && NoButton.Visibility == Visibility.Visible)
                NoButton.Focus();
            else if (_defaultResult == MessageBoxResult.Cancel && CancelButton.Visibility == Visibility.Visible)
                CancelButton.Focus();
        }

        // ── Result wiring (DialogResult so ShowDialog returns) ──────
        private void OnOk(object sender, RoutedEventArgs e)
        { DialogTag = MessageBoxResult.OK; DialogResult = true; }

        private void OnYes(object sender, RoutedEventArgs e)
        { DialogTag = MessageBoxResult.Yes; DialogResult = true; }

        private void OnNo(object sender, RoutedEventArgs e)
        { DialogTag = MessageBoxResult.No; DialogResult = false; }

        private void OnCancel(object sender, RoutedEventArgs e)
        { DialogTag = MessageBoxResult.Cancel; DialogResult = false; }

        // OS parity: on an OK-only box Esc closes with OK (the Cancel
        // button variant already handles Esc via IsCancel).
        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            if (CancelButton.Visibility == Visibility.Visible) return;   // IsCancel handles it
            DialogTag = _buttons == MessageBoxButton.YesNo
                          || _buttons == MessageBoxButton.YesNoCancel
                ? MessageBoxResult.No           // yes/no boxes: Esc ≈ deny
                : MessageBoxResult.OK;
            DialogResult = DialogTag == MessageBoxResult.OK;
            e.Handled = true;
        }
    }
}

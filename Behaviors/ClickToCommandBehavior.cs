// ═══════════════════════════════════════════════════════════════════════
// FILE: Behaviors/ClickToCommandBehavior.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 2.5 — MVVM leakage sweep. Lets a non-button element (logo, dim
// overlay, row, …) invoke an ICommand on MouseLeftButtonDown without
// code-behind. Optional <c>ExcludeAncestorTypes</c> skips the command when
// the click originated on/inside one of the named element types (e.g.
// "TextBox" — an editable cell must receive the click itself).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Behaviors
{
    /// <summary>
    /// Attached behavior: <c>ClickToCommandBehavior.Command</c> (optionally
    /// with <c>CommandParameter</c>) executes the command when the element
    /// receives MouseLeftButtonDown. Replaces view-only click code-behind.
    /// </summary>
    public static class ClickToCommandBehavior
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command", typeof(ICommand), typeof(ClickToCommandBehavior),
                new PropertyMetadata(null, OnCommandChanged));

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached(
                "CommandParameter", typeof(object), typeof(ClickToCommandBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Comma-separated element type names (simple names, e.g. "TextBox").
        /// When the click's original source is — or is inside — an element of
        /// one of these types, the command is NOT executed.
        /// </summary>
        public static readonly DependencyProperty ExcludeAncestorTypesProperty =
            DependencyProperty.RegisterAttached(
                "ExcludeAncestorTypes", typeof(string), typeof(ClickToCommandBehavior),
                new PropertyMetadata(null));

        public static void SetCommand(DependencyObject d, ICommand? value) => d.SetValue(CommandProperty, value);
        public static ICommand? GetCommand(DependencyObject d) => (ICommand?)d.GetValue(CommandProperty);

        public static void SetCommandParameter(DependencyObject d, object? value) => d.SetValue(CommandParameterProperty, value);
        public static object? GetCommandParameter(DependencyObject d) => d.GetValue(CommandParameterProperty);

        public static void SetExcludeAncestorTypes(DependencyObject d, string? value) => d.SetValue(ExcludeAncestorTypesProperty, value);
        public static string? GetExcludeAncestorTypes(DependencyObject d) => (string?)d.GetValue(ExcludeAncestorTypesProperty);

        // ══════════════════════════════════════════════════════════════
        //  DOUBLE-CLICK SUPPORT (2026-09-06)
        //
        //  THE RACE THIS EXISTS TO PREVENT
        //
        //  This behaviour fires on MouseLeftButtonDown. WPF raises that
        //  event TWICE during a double-click, before MouseDoubleClick is
        //  ever delivered. So a row that both (a) toggles selection on
        //  single click and (b) opens a detail page on double click would
        //  run: select -> DESELECT -> open page for a now-null selection.
        //
        //  When DoubleClickCommand is set, the single-click command is
        //  DEFERRED by the system double-click interval. If a second click
        //  lands inside that window the single-click command is cancelled
        //  and the double-click command runs instead.
        //
        //  STRICTLY OPT-IN. With no DoubleClickCommand the dispatch path
        //  is byte-for-byte the old one — no timer, no deferral, no
        //  latency. All 13 existing call sites are unaffected.
        // ══════════════════════════════════════════════════════════════

        public static readonly DependencyProperty DoubleClickCommandProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickCommand", typeof(ICommand), typeof(ClickToCommandBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DoubleClickCommandParameterProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickCommandParameter", typeof(object), typeof(ClickToCommandBehavior),
                new PropertyMetadata(null));

        public static void SetDoubleClickCommand(DependencyObject d, ICommand? value) => d.SetValue(DoubleClickCommandProperty, value);
        public static ICommand? GetDoubleClickCommand(DependencyObject d) => (ICommand?)d.GetValue(DoubleClickCommandProperty);

        public static void SetDoubleClickCommandParameter(DependencyObject d, object? value) => d.SetValue(DoubleClickCommandParameterProperty, value);
        public static object? GetDoubleClickCommandParameter(DependencyObject d) => d.GetValue(DoubleClickCommandParameterProperty);

        /// <summary>
        /// Pending single-click timer, per element. Keyed weakly so a
        /// recycled virtualised row cannot leak its timer — this behaviour
        /// is applied inside VirtualizingStackPanel item templates, where
        /// containers are reused aggressively.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            UIElement, System.Windows.Threading.DispatcherTimer> _pending = new();

        private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            element.RemoveHandler(UIElement.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnMouseLeftButtonDown));

            if (e.NewValue is ICommand)
            {
                element.AddHandler(UIElement.MouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(OnMouseLeftButtonDown), true);
            }
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement element) return;

            var excludes = GetExcludeAncestorTypes(element);
            if (!string.IsNullOrWhiteSpace(excludes) &&
                IsInsideExcludedType(e.OriginalSource as DependencyObject, excludes))
                return;

            var command = GetCommand(element);
            var parameter = GetCommandParameter(element);
            var doubleCommand = GetDoubleClickCommand(element);

            // ── Fast path: no double-click wired, behave exactly as before.
            if (doubleCommand == null)
            {
                if (command != null && command.CanExecute(parameter))
                    command.Execute(parameter);
                return;
            }

            // ── Second click inside the interval: cancel the pending
            //    single-click and run the double-click command instead.
            if (e.ClickCount >= 2 || _pending.TryGetValue(element, out _))
            {
                CancelPending(element);

                var dblParam = GetDoubleClickCommandParameter(element) ?? parameter;
                if (doubleCommand.CanExecute(dblParam))
                    doubleCommand.Execute(dblParam);
                return;
            }

            // ── First click: defer. If no second click arrives within the
            //    system double-click interval, the single-click command
            //    runs. Using the OS setting (not a hard-coded delay) keeps
            //    the app consistent with the user's own mouse settings and
            //    with accessibility configurations.
            if (command == null) return;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(DoubleClickIntervalMs)
            };

            timer.Tick += (_, _) =>
            {
                CancelPending(element);
                if (command.CanExecute(parameter))
                    command.Execute(parameter);
            };

            _pending.AddOrUpdate(element, timer);
            timer.Start();
        }

        /// <summary>
        /// The user's own double-click speed, straight from Win32.
        ///
        /// NOT hard-coded: the interval is a user/accessibility setting,
        /// and a fixed 300ms makes the app feel broken for anyone who has
        /// tuned it. SystemInformation.DoubleClickTime would be the
        /// idiomatic source, but that lives in System.Windows.Forms and
        /// this project sets UseWPF only — referencing it would not
        /// compile. GetDoubleClickTime is the same value from the same
        /// place, without the framework reference.
        ///
        /// Read once: it is a global mouse setting, and re-reading it per
        /// click would mean a P/Invoke on every row click.
        /// </summary>
        private static readonly int DoubleClickIntervalMs = ReadDoubleClickTime();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private static int ReadDoubleClickTime()
        {
            try
            {
                var ms = (int)GetDoubleClickTime();
                // Win32 documents 0 as "use the default"; clamp defensively
                // so a hostile or unset value cannot produce a zero-interval
                // timer that fires in a tight loop.
                return ms > 0 ? ms : 500;
            }
            catch
            {
                return 500;   // never let a P/Invoke failure break clicking
            }
        }

        private static void CancelPending(UIElement element)
        {
            if (_pending.TryGetValue(element, out var timer))
            {
                timer.Stop();
                _pending.Remove(element);
            }
        }

        private static bool IsInsideExcludedType(DependencyObject? source, string excludes)
        {
            var names = excludes.Split(',');
            while (source != null)
            {
                var typeName = source.GetType().Name;
                foreach (var n in names)
                {
                    if (string.Equals(n.Trim(), typeName, System.StringComparison.Ordinal))
                        return true;
                }
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
    }
}

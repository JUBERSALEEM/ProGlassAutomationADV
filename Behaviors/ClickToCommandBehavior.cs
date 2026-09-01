// ═══════════════════════════════════════════════════════════════════════
// FILE: Behaviors/ClickToCommandBehavior.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 2.5 — MVVM leakage sweep. Lets a non-button element (logo, dim
// overlay, …) invoke an ICommand on MouseLeftButtonDown without code-behind.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Input;

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

        public static void SetCommand(DependencyObject d, ICommand? value) => d.SetValue(CommandProperty, value);
        public static ICommand? GetCommand(DependencyObject d) => (ICommand?)d.GetValue(CommandProperty);

        public static void SetCommandParameter(DependencyObject d, object? value) => d.SetValue(CommandParameterProperty, value);
        public static object? GetCommandParameter(DependencyObject d) => d.GetValue(CommandParameterProperty);

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

            var command = GetCommand(element);
            var parameter = GetCommandParameter(element);
            if (command != null && command.CanExecute(parameter))
                command.Execute(parameter);
        }
    }
}

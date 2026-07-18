using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Behaviors
{
    /// <summary>
    /// Global attached behaviors for TextBox controls.
    /// Provides UX enhancements that can be enabled per-TextBox or globally via styles.
    /// </summary>
    public static class TextBoxBehaviors
    {
        // ==================== SelectAllOnFocus ====================
        //
        // When enabled, clicking or tabbing into a TextBox auto-selects all its
        // text so the user can immediately overwrite without manually deleting.
        //
        // Usage in XAML:
        //   <TextBox behaviors:TextBoxBehaviors.SelectAllOnFocus="True" ... />
        //
        // Or apply globally in a style:
        //   <Style TargetType="TextBox">
        //     <Setter Property="behaviors:TextBoxBehaviors.SelectAllOnFocus" Value="True"/>
        //   </Style>

        public static readonly DependencyProperty SelectAllOnFocusProperty =
            DependencyProperty.RegisterAttached(
                "SelectAllOnFocus",
                typeof(bool),
                typeof(TextBoxBehaviors),
                new UIPropertyMetadata(false, OnSelectAllOnFocusChanged));

        public static bool GetSelectAllOnFocus(DependencyObject obj)
            => (bool)obj.GetValue(SelectAllOnFocusProperty);

        public static void SetSelectAllOnFocus(DependencyObject obj, bool value)
            => obj.SetValue(SelectAllOnFocusProperty, value);

        private static void OnSelectAllOnFocusChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            // Detach first to prevent double-hooking
            textBox.GotKeyboardFocus -= TextBox_GotKeyboardFocus;
            textBox.PreviewMouseLeftButtonDown -= TextBox_PreviewMouseLeftButtonDown;
            textBox.GotFocus -= TextBox_GotFocus;

            if ((bool)e.NewValue)
            {
                textBox.GotKeyboardFocus += TextBox_GotKeyboardFocus;
                textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
                textBox.GotFocus += TextBox_GotFocus;
            }
        }

        private static void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.SelectAll();
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.SelectAll();
        }

        /// <summary>
        /// If TextBox doesn't have keyboard focus yet, intercept the mouse click,
        /// give it focus (which triggers SelectAll via GotKeyboardFocus),
        /// then swallow the click so caret doesn't reposition.
        /// </summary>
        private static void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                tb.Focus();
            }
        }
    }
}
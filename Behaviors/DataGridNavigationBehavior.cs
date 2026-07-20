using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Behaviors
{
    /// <summary>
    /// Excel-like keyboard navigation for data grid rows.
    /// 
    /// Tab        → Next cell (right), wraps to next row
    /// Shift+Tab  → Previous cell (left), wraps to previous row
    /// Right Arrow→ Next cell (right)
    /// Left Arrow → Previous cell (left)
    /// Enter      → Same anchor column in next row (where user first clicked)
    /// Down Arrow → Same column position in next row
    /// Up Arrow   → Same column position in previous row
    /// 
    /// Anchor column logic:
    ///   When user clicks a cell, that column becomes the "anchor".
    ///   Pressing Enter always returns to that anchor column in the next row.
    ///   The anchor resets when user clicks a cell in a different row.
    /// 
    /// All movements auto-select text in target cell for immediate editing.
    /// 
    /// Usage in XAML:
    ///   xmlns:behaviors="clr-namespace:ProGlassAutomation.Behaviors"
    ///   
    ///   <Border behaviors:DataGridNavigationBehavior.EnableExcelNav="True">
    ///       ... your ItemsControl with TextBoxes ...
    ///   </Border>
    /// </summary>
    public static class DataGridNavigationBehavior
    {
        // Tracks which column the user first clicked/focused in the current row
        private static int _anchorColumn = -1;
        private static int _anchorRow = -1;

        public static readonly DependencyProperty EnableExcelNavProperty =
            DependencyProperty.RegisterAttached(
                "EnableExcelNav",
                typeof(bool),
                typeof(DataGridNavigationBehavior),
                new UIPropertyMetadata(false, OnEnableChanged));

        public static bool GetEnableExcelNav(DependencyObject obj)
            => (bool)obj.GetValue(EnableExcelNavProperty);

        public static void SetEnableExcelNav(DependencyObject obj, bool value)
            => obj.SetValue(EnableExcelNavProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement el) return;

            // Detach first to prevent double-hooking
            el.PreviewKeyDown -= OnKey;
            el.GotFocus -= OnGotFocus;

            if ((bool)e.NewValue)
            {
                el.PreviewKeyDown += OnKey;
                el.GotFocus += OnGotFocus;
            }
        }

        /// <summary>
        /// When user clicks a cell directly (mouse click), set that as the anchor column.
        /// Only resets anchor when the row changes.
        /// </summary>
        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TextBox tb) return;
            if (sender is not FrameworkElement container) return;

            var allBoxes = FindAllTextBoxes(container);
            int idx = allBoxes.IndexOf(tb);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(tb, container);
            if (colsPerRow <= 0) colsPerRow = 7;

            int currentRow = idx / colsPerRow;
            int currentCol = idx % colsPerRow;

            // If user clicked into a different row, or anchor not set yet, update anchor
            if (_anchorRow != currentRow || _anchorColumn < 0)
            {
                _anchorColumn = currentCol;
                _anchorRow = currentRow;
            }
        }

        private static void OnKey(object sender, KeyEventArgs e)
        {
            // Only handle when a TextBox has focus
            if (e.OriginalSource is not TextBox current) return;
            if (sender is not FrameworkElement container) return;

            // Get ALL TextBoxes in the entire table container (flat list, row by row)
            var allBoxes = FindAllTextBoxes(container);
            int idx = allBoxes.IndexOf(current);
            if (idx < 0) return;

            // Count how many editable TextBoxes per row
            // (GlassRef, W1, H1, W2, H2, Qty, Rate = 7 per row)
            int colsPerRow = GetColumnsPerRow(current, container);
            if (colsPerRow <= 0) colsPerRow = 7;

            int currentCol = idx % colsPerRow;
            int currentRow = idx / colsPerRow;

            switch (e.Key)
            {
                // ===== TAB: Move right (or left with Shift) =====
                case Key.Tab:
                    e.Handled = true;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        // Move left, wrap to previous row end
                        int prevIdx = idx - 1;
                        if (prevIdx >= 0)
                            FocusAndSelect(allBoxes[prevIdx]);
                    }
                    else
                    {
                        // Move right, wrap to next row start
                        int nextIdx = idx + 1;
                        if (nextIdx < allBoxes.Count)
                            FocusAndSelect(allBoxes[nextIdx]);
                    }
                    break;

                // ===== ENTER: Anchor column in next row =====
                case Key.Enter:
                    e.Handled = true;
                    // Go to the column where user first clicked, in the next row
                    int anchorCol = _anchorColumn >= 0 ? _anchorColumn : currentCol;
                    int enterIdx = (currentRow + 1) * colsPerRow + anchorCol;
                    if (enterIdx < allBoxes.Count)
                    {
                        _anchorRow = currentRow + 1;
                        FocusAndSelect(allBoxes[enterIdx]);
                    }
                    break;

                // ===== RIGHT ARROW: Next cell (right) =====
                case Key.Right:
                    e.Handled = true;
                    int rightIdx = idx + 1;
                    if (rightIdx < allBoxes.Count)
                        FocusAndSelect(allBoxes[rightIdx]);
                    break;

                // ===== LEFT ARROW: Previous cell (left) =====
                case Key.Left:
                    e.Handled = true;
                    int leftIdx = idx - 1;
                    if (leftIdx >= 0)
                        FocusAndSelect(allBoxes[leftIdx]);
                    break;

                // ===== DOWN ARROW: Same column, next row =====
                case Key.Down:
                    e.Handled = true;
                    int downIdx = idx + colsPerRow;
                    if (downIdx < allBoxes.Count)
                        FocusAndSelect(allBoxes[downIdx]);
                    break;

                // ===== UP ARROW: Same column, previous row =====
                case Key.Up:
                    e.Handled = true;
                    int upIdx = idx - colsPerRow;
                    if (upIdx >= 0)
                        FocusAndSelect(allBoxes[upIdx]);
                    break;
            }
        }

        /// <summary>
        /// Focus the target TextBox and select all its text for immediate editing.
        /// </summary>
        private static void FocusAndSelect(TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }

        /// <summary>
        /// Counts how many TextBoxes are in the current row.
        /// Used to calculate columns-per-row for Up/Down/Enter navigation.
        /// </summary>
        private static int GetColumnsPerRow(TextBox current, FrameworkElement container)
        {
            var rowBorder = FindRowBorder(current);
            if (rowBorder == null) return 7;

            var rowBoxes = FindAllTextBoxes(rowBorder);
            return rowBoxes.Count > 0 ? rowBoxes.Count : 7;
        }

        /// <summary>
        /// Walks up the visual tree to find the row's Border container.
        /// Our rows use Border with bottom BorderThickness > 0 and Padding > 0.
        /// </summary>
        private static Border FindRowBorder(DependencyObject element)
        {
            var current = VisualTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is Border b && b.BorderThickness.Bottom > 0 && b.Padding.Top > 0)
                    return b;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// Recursively finds all TextBox elements within a visual tree parent.
        /// Returns them in document order (left to right, top to bottom).
        /// </summary>
        private static List<TextBox> FindAllTextBoxes(DependencyObject parent)
        {
            var result = new List<TextBox>();
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBox tb)
                    result.Add(tb);
                else
                    result.AddRange(FindAllTextBoxes(child));
            }
            return result;
        }
    }
}
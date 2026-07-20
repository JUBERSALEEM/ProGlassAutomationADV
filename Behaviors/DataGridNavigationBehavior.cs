using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Behaviors
{
    /// <summary>
    /// Excel-like keyboard navigation and smart paste with auto-row expansion.
    /// </summary>
    public static class DataGridNavigationBehavior
    {
        private static int _anchorColumn = -1;
        private static int _anchorRow = -1;
        private static FrameworkElement _container;

        public static readonly DependencyProperty EnableExcelNavProperty =
            DependencyProperty.RegisterAttached(
                "EnableExcelNav",
                typeof(bool),
                typeof(DataGridNavigationBehavior),
                new UIPropertyMetadata(false, OnEnableChanged));

        public static bool GetEnableExcelNav(DependencyObject obj) => (bool)obj.GetValue(EnableExcelNavProperty);
        public static void SetEnableExcelNav(DependencyObject obj, bool value) => obj.SetValue(EnableExcelNavProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement el) return;

            el.PreviewKeyDown -= OnKey;
            el.GotFocus -= OnGotFocus;
            el.RemoveHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnDataObjectPasting));

            if ((bool)e.NewValue)
            {
                _container = el;
                el.PreviewKeyDown += OnKey;
                el.GotFocus += OnGotFocus;
                el.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnDataObjectPasting));
            }
        }

        private static void OnDataObjectPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (Keyboard.FocusedElement is not TextBox currentBox) return;
            if (_container == null) return;

            var clipText = e.DataObject.GetData(typeof(string)) as string;
            if (string.IsNullOrWhiteSpace(clipText)) return;

            bool hasTab = clipText.Contains('\t');
            bool hasSpace = clipText.Trim().Contains(' ');
            bool hasNewline = clipText.Contains('\n') || clipText.Contains('\r');

            if (!hasTab && !hasSpace && !hasNewline) return;

            e.CancelCommand();

            var allBoxes = FindAllTextBoxes(_container);
            int idx = allBoxes.IndexOf(currentBox);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(currentBox, _container);
            if (colsPerRow <= 0) colsPerRow = 7;

            SmartPaste(currentBox, idx, allBoxes, colsPerRow, clipText);
            e.Handled = true;
        }

        private static void SmartPaste(TextBox current, int startIdx, List<TextBox> allBoxes, int colsPerRow, string clipText)
        {
            var lines = clipText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

            int currentRowIndex = startIdx / colsPerRow;
            int rowStartIdx = currentRowIndex * colsPerRow;

            foreach (var line in lines)
            {
                // 1. Ensure row exists. If not, trigger AddRowCommand on the SpecGroup ViewModel
                if (rowStartIdx >= allBoxes.Count)
                {
                    if (TryAddRow(current))
                    {
                        // Refresh boxes and layout to find the newly created TextBoxes
                        _container.UpdateLayout();
                        allBoxes = FindAllTextBoxes(_container);
                    }
                    else break; // Could not add row, stop pasting
                }

                // 2. Parse the line
                var values = line.Split('\t');
                if (values.Length <= 1)
                    values = line.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

                // 3. Paste values into the row
                for (int i = 0; i < values.Length; i++)
                {
                    int targetIdx = rowStartIdx + i;
                    if (targetIdx >= allBoxes.Count || i >= colsPerRow) break;

                    var targetBox = allBoxes[targetIdx];
                    var trimmed = values[i].Trim();

                    if (trimmed == "-" || trimmed == "–" || trimmed == "N/A" || trimmed == "n/a" || trimmed == "")
                        trimmed = "0";

                    targetBox.Text = trimmed;

                    var binding = targetBox.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource();
                }

                // Move to next row start
                rowStartIdx += colsPerRow;
            }

            // Return focus to start
            if (startIdx < allBoxes.Count) FocusAndSelect(allBoxes[startIdx]);
        }

        /// <summary>
        /// Finds the SpecGroup ViewModel and executes its AddRowCommand.
        /// </summary>
        private static bool TryAddRow(DependencyObject element)
        {
            var itemsControl = FindParentOfType<ItemsControl>(element);
            dynamic vm = itemsControl?.DataContext;
            if (vm != null)
            {
                try
                {
                    if (vm.AddRowCommand != null && vm.AddRowCommand.CanExecute(null))
                    {
                        vm.AddRowCommand.Execute(null);
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TextBox tb) return;
            if (sender is not FrameworkElement container) return;

            var allBoxes = FindAllTextBoxes(container);
            int idx = allBoxes.IndexOf(tb);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(tb, container);
            int currentRow = idx / (colsPerRow > 0 ? colsPerRow : 7);
            int currentCol = idx % (colsPerRow > 0 ? colsPerRow : 7);

            if (_anchorRow != currentRow || _anchorColumn < 0)
            {
                _anchorColumn = currentCol;
                _anchorRow = currentRow;
            }
        }

        private static void OnKey(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is not TextBox current) return;
            if (sender is not FrameworkElement container) return;
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) return;

            var allBoxes = FindAllTextBoxes(container);
            int idx = allBoxes.IndexOf(current);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(current, container);
            if (colsPerRow <= 0) colsPerRow = 7;

            int currentCol = idx % colsPerRow;
            int currentRow = idx / colsPerRow;

            switch (e.Key)
            {
                case Key.Tab:
                    e.Handled = true;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        int prevIdx = idx - 1;
                        if (prevIdx >= 0) FocusAndSelect(allBoxes[prevIdx]);
                    }
                    else
                    {
                        int nextIdx = idx + 1;
                        if (nextIdx < allBoxes.Count) FocusAndSelect(allBoxes[nextIdx]);
                    }
                    break;

                case Key.Enter:
                    e.Handled = true;
                    int anchorCol = _anchorColumn >= 0 ? _anchorColumn : currentCol;
                    int enterIdx = (currentRow + 1) * colsPerRow + anchorCol;
                    if (enterIdx < allBoxes.Count)
                    {
                        _anchorRow = currentRow + 1;
                        FocusAndSelect(allBoxes[enterIdx]);
                    }
                    break;

                case Key.Right:
                    e.Handled = true;
                    if (idx + 1 < allBoxes.Count) FocusAndSelect(allBoxes[idx + 1]);
                    break;

                case Key.Left:
                    e.Handled = true;
                    if (idx - 1 >= 0) FocusAndSelect(allBoxes[idx - 1]);
                    break;

                case Key.Down:
                    e.Handled = true;
                    if (idx + colsPerRow < allBoxes.Count) FocusAndSelect(allBoxes[idx + colsPerRow]);
                    break;

                case Key.Up:
                    e.Handled = true;
                    if (idx - colsPerRow >= 0) FocusAndSelect(allBoxes[idx - colsPerRow]);
                    break;
            }
        }

        private static void FocusAndSelect(TextBox tb) { tb.Focus(); tb.SelectAll(); }

        private static int GetColumnsPerRow(TextBox current, FrameworkElement container)
        {
            var rowBorder = FindRowBorder(current);
            if (rowBorder == null) return 7;
            var rowBoxes = FindAllTextBoxes(rowBorder);
            return rowBoxes.Count > 0 ? rowBoxes.Count : 7;
        }

        private static Border FindRowBorder(DependencyObject element)
        {
            var current = VisualTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is Border b && b.BorderThickness.Bottom > 0 && b.Padding.Top > 0) return b;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static List<TextBox> FindAllTextBoxes(DependencyObject parent)
        {
            var result = new List<TextBox>();
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBox tb) result.Add(tb);
                else result.AddRange(FindAllTextBoxes(child));
            }
            return result;
        }

        private static T FindParentOfType<T>(DependencyObject element) where T : DependencyObject
        {
            var current = VisualTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is T found) return found;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
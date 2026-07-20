using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Behaviors
{
    public static class DataGridNavigationBehavior
    {
        private static int _anchorColumn = -1;
        private static int _anchorRow = -1;

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
                el.PreviewKeyDown += OnKey;
                el.GotFocus += OnGotFocus;
                el.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnDataObjectPasting));
            }
        }

        private static void OnDataObjectPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (Keyboard.FocusedElement is not TextBox focusedBox) return;

            var clipText = e.DataObject.GetData(typeof(string)) as string;
            if (string.IsNullOrWhiteSpace(clipText)) return;

            bool hasTab = clipText.Contains('\t');
            bool hasNewline = clipText.Contains('\n') || clipText.Contains('\r');
            if (!hasTab && !hasNewline) return;

            // Cancel default TextBox paste
            e.CancelCommand();
            e.Handled = true;

            // Determine which data row the focused TextBox belongs to
            object startRowDataContext = FindRowDataContext(focusedBox);

            // Route to ViewModel — find IExcelPasteable in the visual tree
            if (sender is DependencyObject senderDep)
            {
                var pasteable = FindViewModelInTree(senderDep);
                if (pasteable != null)
                {
                    pasteable.PasteFromText(clipText, startRowDataContext);
                    return;
                }
            }

            System.Windows.MessageBox.Show(
                "Paste not supported here. Use the context menu 'Paste from Excel' button.",
                "Paste",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Walks up the visual tree from the focused element to find the ListViewItem's
        /// DataContext, which represents the data row where paste originated.
        /// </summary>
        private static object FindRowDataContext(DependencyObject element)
        {
            var current = VisualTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is ListViewItem lvi)
                    return lvi.DataContext;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static IExcelPasteable FindViewModelInTree(DependencyObject start)
        {
            var current = start;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is IExcelPasteable pasteable)
                    return pasteable;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TextBox tb) return;
            if (sender is not FrameworkElement container) return;

            var allBoxes = FindAllTextBoxes(container);
            int idx = allBoxes.IndexOf(tb);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(tb);
            int cols = colsPerRow > 0 ? colsPerRow : 7;
            int currentRow = idx / cols;
            int currentCol = idx % cols;

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

            int colsPerRow = GetColumnsPerRow(current);
            if (colsPerRow <= 0) colsPerRow = 7;

            int currentCol = idx % colsPerRow;
            int currentRow = idx / colsPerRow;

            switch (e.Key)
            {
                case Key.Tab:
                    e.Handled = true;
                    int tabTarget = Keyboard.Modifiers == ModifierKeys.Shift ? idx - 1 : idx + 1;
                    if (tabTarget >= 0 && tabTarget < allBoxes.Count)
                        FocusAndSelect(allBoxes[tabTarget]);
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

        private static int GetColumnsPerRow(TextBox current)
        {
            var rowContainer = FindRowContainer(current);
            if (rowContainer == null) return 7;
            var boxes = FindAllTextBoxes(rowContainer);
            return boxes.Count > 0 ? boxes.Count : 7;
        }

        private static DependencyObject FindRowContainer(DependencyObject element)
        {
            var current = VisualTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is ListViewItem) return current;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static List<TextBox> FindAllTextBoxes(DependencyObject parent)
        {
            var result = new List<TextBox>();
            if (parent == null) return result;
            CollectTextBoxes(parent, result);
            return result;
        }

        private static void CollectTextBoxes(DependencyObject parent, List<TextBox> result)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBox tb)
                    result.Add(tb);
                else
                    CollectTextBoxes(child, result);
            }
        }
    }

    public interface IExcelPasteable
    {
        /// <summary>
        /// Paste clipboard text starting at the given row (or at end if null / row not found / row is empty logic decides).
        /// </summary>
        void PasteFromText(string clipboardText, object startRowContext = null);

        void AddRows(int count);
    }
}
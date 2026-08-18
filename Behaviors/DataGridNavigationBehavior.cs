// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Behaviors/DataGridNavigationBehavior.cs                                 ║
// ║  ProGlass Automation                                                     ║
// ║                                                                          ║
// ║  CHAT 20 FINAL — Multi-row selection + persistent Find/Replace.         ║
// ║                                                                          ║
// ║  NEW IN THIS VERSION:                                                    ║
// ║    SEL-01 : Excel-style multi-row selection                              ║
// ║              • Click row (outside TextBox) → select single row           ║
// ║              • Shift+Click → range select from anchor                    ║
// ║              • Ctrl+Click → toggle row in/out of selection               ║
// ║              • Escape → clear selection                                  ║
// ║              • Ctrl+A → select all rows in group                         ║
// ║    SEL-02 : Ctrl+D across multiple selected rows                        ║
// ║              • 0 or 1 selected → single-column fill from row above      ║
// ║              • 2+ selected → source=first, targets=others,              ║
// ║                fill writable columns (GlassRef,W1,H1,W2,H2,Qty)         ║
// ║    FIND-01: Persistent Find dialog — stays open for Find Next          ║
// ║              • Callback pattern — dialog fires event on click           ║
// ║              • Close button or Escape closes dialog                     ║
// ║              • Wrap-around search                                       ║
// ║    FIND-02: Persistent Find/Replace dialog                             ║
// ║              • Replace All fires callback, dialog stays open           ║
// ║              • Multiple replace operations possible                    ║
// ║                                                                          ║
// ║  INTERFACE ADDITIONS (must be implemented in SpecGroup):                ║
// ║    void ClearRowSelection()                                             ║
// ║    void SelectAllRows()                                                 ║
// ║    void SetRowSelection(object, bool)                                   ║
// ║    void SelectRowRange(object, object)                                  ║
// ║    List<object> GetSelectedRows()                                       ║
// ║    void FillDownAcrossRows(object, List<object>)                        ║
// ║                                                                          ║
// ║  PRESERVED FROM PREVIOUS:                                               ║
// ║    ✅ Tab/Shift+Tab, Enter, Arrow navigation                            ║
// ║    ✅ Ctrl+V multi-cell paste routing                                   ║
// ║    ✅ Ctrl+Home/End, Ctrl+Delete                                        ║
// ║    ✅ Per-ListView anchor properties                                    ║
// ║    ✅ TextBox cache with StatusChanged invalidation                     ║
// ║    ✅ AutoAddRowAndFocus on Down/Enter at last row                      ║
// ║    ✅ Row boundary rules for Left/Right                                 ║
// ║    ✅ Virtualization-safe scroll                                        ║
// ╚══════════════════════════════════════════════════════════════════════════╝
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProGlassAutomation.Behaviors
{
    public static class DataGridNavigationBehavior
    {
        // ══════════════════════════════════════════════════════════════════
        // ENABLE ATTACHED PROPERTY
        // ══════════════════════════════════════════════════════════════════

        public static readonly DependencyProperty EnableExcelNavProperty =
            DependencyProperty.RegisterAttached(
                "EnableExcelNav",
                typeof(bool),
                typeof(DataGridNavigationBehavior),
                new UIPropertyMetadata(false, OnEnableChanged));

        public static bool GetEnableExcelNav(DependencyObject obj) =>
            (bool)obj.GetValue(EnableExcelNavProperty);

        public static void SetEnableExcelNav(DependencyObject obj, bool value) =>
            obj.SetValue(EnableExcelNavProperty, value);

        private static void OnEnableChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement el) return;

            // Always detach first — prevents double-hook on style reload
            el.PreviewKeyDown -= OnKey;
            el.GotFocus -= OnGotFocus;
            el.PreviewMouseLeftButtonDown -= OnRowMouseDown;
            el.RemoveHandler(
                DataObject.PastingEvent,
                new DataObjectPastingEventHandler(OnDataObjectPasting));

            if ((bool)e.NewValue)
            {
                el.PreviewKeyDown += OnKey;
                el.GotFocus += OnGotFocus;
                // Row selection click handler — bubbles from ListViewItem
                el.PreviewMouseLeftButtonDown += OnRowMouseDown;
                el.AddHandler(
                    DataObject.PastingEvent,
                    new DataObjectPastingEventHandler(OnDataObjectPasting));

                if (el is ListView lv)
                {
                    lv.Loaded -= OnListViewLoaded;
                    lv.Loaded += OnListViewLoaded;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PER-LISTVIEW ATTACHED PROPERTIES
        // ══════════════════════════════════════════════════════════════════

        private static readonly DependencyProperty AnchorColumnProperty =
            DependencyProperty.RegisterAttached(
                "AnchorColumn", typeof(int),
                typeof(DataGridNavigationBehavior),
                new PropertyMetadata(-1));

        private static readonly DependencyProperty AnchorRowProperty =
            DependencyProperty.RegisterAttached(
                "AnchorRow", typeof(int),
                typeof(DataGridNavigationBehavior),
                new PropertyMetadata(-1));

        private static readonly DependencyProperty CachedTextBoxesProperty =
            DependencyProperty.RegisterAttached(
                "CachedTextBoxes", typeof(List<TextBox>),
                typeof(DataGridNavigationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Selection anchor row for Shift+Click range selection.
        /// Stores the row DataContext of the last Ctrl-clicked or single-clicked row.
        /// Cleared when selection is cleared.
        /// </summary>
        private static readonly DependencyProperty SelectionAnchorProperty =
            DependencyProperty.RegisterAttached(
                "SelectionAnchor", typeof(object),
                typeof(DataGridNavigationBehavior),
                new PropertyMetadata(null));

        private static int GetAnchorColumn(DependencyObject o) =>
            (int)o.GetValue(AnchorColumnProperty);
        private static void SetAnchorColumn(DependencyObject o, int v) =>
            o.SetValue(AnchorColumnProperty, v);

        private static int GetAnchorRow(DependencyObject o) =>
            (int)o.GetValue(AnchorRowProperty);
        private static void SetAnchorRow(DependencyObject o, int v) =>
            o.SetValue(AnchorRowProperty, v);

        private static List<TextBox> GetCachedTextBoxes(DependencyObject o) =>
            (List<TextBox>)o.GetValue(CachedTextBoxesProperty);
        private static void SetCachedTextBoxes(DependencyObject o, List<TextBox> v) =>
            o.SetValue(CachedTextBoxesProperty, v);

        private static object GetSelectionAnchor(DependencyObject o) =>
            o.GetValue(SelectionAnchorProperty);
        private static void SetSelectionAnchor(DependencyObject o, object v) =>
            o.SetValue(SelectionAnchorProperty, v);

        // ══════════════════════════════════════════════════════════════════
        // CACHE MANAGEMENT
        // ══════════════════════════════════════════════════════════════════

        private static void OnListViewLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ListView lv) return;

            lv.ItemContainerGenerator.StatusChanged += (s, _) =>
            {
                if (lv.ItemContainerGenerator.Status ==
                    GeneratorStatus.ContainersGenerated)
                {
                    InvalidateTextBoxCache(lv);
                }
            };
        }

        private static void InvalidateTextBoxCache(DependencyObject listView) =>
            SetCachedTextBoxes(listView, null);

        private static List<TextBox> GetOrBuildTextBoxes(FrameworkElement container)
        {
            var cached = GetCachedTextBoxes(container);
            if (cached != null) return cached;

            var fresh = FindAllTextBoxes(container);
            SetCachedTextBoxes(container, fresh);
            return fresh;
        }

        // ══════════════════════════════════════════════════════════════════
        // ROW MOUSE HANDLER — SELECTION LOGIC
        //
        // Called on PreviewMouseLeftButtonDown from the ListView.
        // Determines if the click is on a TextBox (edit mode) or on a
        // row's non-editable area (selection mode).
        //
        // Selection rules:
        //   Plain click on row body → select single row (clear others)
        //   Shift+Click            → extend range from anchor to this row
        //   Ctrl+Click             → toggle this row's selection
        //   Click on TextBox       → edit mode, clear selection
        // ══════════════════════════════════════════════════════════════════

        private static void OnRowMouseDown(object sender, MouseButtonEventArgs e)
        {
            // If click originated on a TextBox — enter edit mode.
            // Do NOT clear selection — user may click into a selected row
            // to enable keyboard shortcuts (like Ctrl+D) that require focus.
            if (e.OriginalSource is DependencyObject src &&
                FindAncestor<TextBox>(src) != null)
            {
                // Preserve selection — TextBox will get focus naturally,
                // enabling Ctrl+D and other shortcuts on the current selection.
                return;
            }

            // Find the ListViewItem clicked
            var lvi = FindAncestor<ListViewItem>(
                e.OriginalSource as DependencyObject);

            if (lvi == null)
            {
                // Click on empty area — clear selection
                if (sender is DependencyObject senderDep2)
                {
                    var vm = FindViewModelInTree(senderDep2);
                    vm?.ClearRowSelection();
                }
                return;
            }

            var rowContext = lvi.DataContext;
            if (rowContext == null) return;

            if (sender is not DependencyObject dep) return;

            var pasteable = FindViewModelInTree(dep);
            if (pasteable == null) return;

            var container = sender as FrameworkElement;
            if (container == null) return;

            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control)
                          == ModifierKeys.Control;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift)
                           == ModifierKeys.Shift;

            if (isShift)
            {
                // Shift+Click — extend range from anchor
                var anchor = GetSelectionAnchor(container);
                if (anchor == null)
                {
                    // No anchor yet — treat as single click
                    pasteable.ClearRowSelection();
                    pasteable.SetRowSelection(rowContext, true);
                    SetSelectionAnchor(container, rowContext);
                }
                else
                {
                    pasteable.SelectRowRange(anchor, rowContext);
                }
                // Do NOT set e.Handled — let TextBox click through if any
            }
            else if (isCtrl)
            {
                // Ctrl+Click — toggle this row's selection
                var selected = pasteable.GetSelectedRows();
                bool wasSelected = selected.Contains(rowContext);
                pasteable.SetRowSelection(rowContext, !wasSelected);
                SetSelectionAnchor(container, rowContext);
            }
            else
            {
                // Plain click — select only this row
                pasteable.ClearRowSelection();
                pasteable.SetRowSelection(rowContext, true);
                SetSelectionAnchor(container, rowContext);
            }
            // Note: Not handling e.Handled=true allows TextBox click-to-focus
            // to still work when user clicks on cell content area.
        }

        // ══════════════════════════════════════════════════════════════════
        // PASTE HANDLER
        // ══════════════════════════════════════════════════════════════════

        private static void OnDataObjectPasting(
            object sender, DataObjectPastingEventArgs e)
        {
            if (Keyboard.FocusedElement is not TextBox focusedBox)
            {
                e.CancelCommand();
                e.Handled = true;

                if (sender is DependencyObject senderDep)
                {
                    var vm = FindViewModelInTree(senderDep);
                    vm?.PasteFromText("", null, -1);
                }
                return;
            }

            var clipText = e.DataObject.GetData(typeof(string)) as string;
            if (string.IsNullOrWhiteSpace(clipText)) return;

            bool isMultiCell = clipText.Contains('\t') ||
                               clipText.Contains('\n') ||
                               clipText.Contains('\r');
            if (!isMultiCell) return;

            e.CancelCommand();
            e.Handled = true;

            object startRowContext = FindRowDataContext(focusedBox);
            int startColumnIndex = FindColumnIndex(focusedBox);

            if (sender is not DependencyObject dep) return;

            var pasteable = FindViewModelInTree(dep);
            if (pasteable != null)
            {
                pasteable.PasteFromText(clipText, startRowContext, startColumnIndex);
                return;
            }

            MessageBox.Show(
                "Paste not supported here. Click a cell inside the grid first.",
                "Paste",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════════
        // FOCUS HANDLER
        // ══════════════════════════════════════════════════════════════════

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TextBox tb) return;
            if (sender is not FrameworkElement container) return;

            var allBoxes = GetOrBuildTextBoxes(container);
            int idx = allBoxes.IndexOf(tb);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(tb);
            int currentRow = idx / colsPerRow;
            int currentCol = idx % colsPerRow;

            if (GetAnchorRow(container) != currentRow ||
                GetAnchorColumn(container) < 0)
            {
                SetAnchorColumn(container, currentCol);
                SetAnchorRow(container, currentRow);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // KEY HANDLER
        // ══════════════════════════════════════════════════════════════════

        private static void OnKey(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is not TextBox current) return;
            if (sender is not FrameworkElement container) return;

            // ── Ctrl+V — DataObject.PastingEvent handles it ──
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
                return;

            // ── Escape — clear selection ──
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                var pasteable = FindViewModelInTree(container);
                pasteable?.ClearRowSelection();
                SetSelectionAnchor(container, null);
                e.Handled = true;
                return;
            }

            // ── Ctrl+key shortcuts ──
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.F:
                        e.Handled = true;
                        HandleFind(container, current);
                        return;

                    case Key.H:
                        e.Handled = true;
                        HandleFindReplace(container);
                        return;

                    case Key.D:
                        e.Handled = true;
                        HandleFillDown(container, current);
                        return;

                    case Key.Delete:
                        e.Handled = true;
                        HandleDeleteRow(container, current);
                        return;

                    case Key.Home:
                        e.Handled = true;
                        HandleCtrlHome(container, current);
                        return;

                    case Key.End:
                        e.Handled = true;
                        HandleCtrlEnd(container, current);
                        return;

                    case Key.A:
                        e.Handled = true;
                        HandleCtrlSelectAll(container);
                        return;
                }
                return;
            }

            // ── Non-modified navigation keys ──
            var allBoxes = GetOrBuildTextBoxes(container);
            int idx = allBoxes.IndexOf(current);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(current);
            int currentRow = idx / colsPerRow;
            int currentCol = idx % colsPerRow;

            switch (e.Key)
            {
                case Key.Tab:
                    {
                        e.Handled = true;
                        int tabTarget = Keyboard.Modifiers == ModifierKeys.Shift
                            ? idx - 1 : idx + 1;

                        if (tabTarget >= 0 && tabTarget < allBoxes.Count)
                        {
                            FocusAndSelect(allBoxes[tabTarget]);
                        }
                        else
                        {
                            int targetRow = tabTarget / colsPerRow;
                            int targetCol = tabTarget % colsPerRow;
                            ScrollRowIntoViewAndFocus(container, targetRow, targetCol);
                        }
                        break;
                    }

                case Key.Enter:
                    {
                        e.Handled = true;
                        int anchorCol = GetAnchorColumn(container);
                        if (anchorCol < 0) anchorCol = currentCol;

                        int nextRow = currentRow + 1;
                        int enterIdx = nextRow * colsPerRow + anchorCol;

                        SetAnchorRow(container, nextRow);

                        if (enterIdx < allBoxes.Count)
                        {
                            FocusAndSelect(allBoxes[enterIdx]);
                        }
                        else
                        {
                            var itemsCtrl = FindItemsControl(container);
                            if (itemsCtrl != null && nextRow < itemsCtrl.Items.Count)
                                ScrollRowIntoViewAndFocus(container, nextRow, anchorCol);
                            else
                                AutoAddRowAndFocus(container, anchorCol);
                        }
                        break;
                    }

                case Key.Right:
                    {
                        // Caret first when the cell is being edited.
                        // Jump cell when all text is selected (Excel) or
                        // the caret is already at the end.
                        if (!ShouldLeaveCell(current, goingRight: true))
                            return;

                        e.Handled = true;
                        int rightTarget = idx + 1;
                        if (rightTarget < allBoxes.Count &&
                            rightTarget / colsPerRow == currentRow)
                        {
                            FocusAndSelect(allBoxes[rightTarget]);
                        }
                        break;
                    }

                case Key.Left:
                    {
                        if (!ShouldLeaveCell(current, goingRight: false))
                            return;

                        e.Handled = true;
                        int leftTarget = idx - 1;
                        if (leftTarget >= 0 &&
                            leftTarget / colsPerRow == currentRow)
                        {
                            FocusAndSelect(allBoxes[leftTarget]);
                        }
                        break;
                    }

                case Key.Down:
                    {
                        e.Handled = true;
                        int downIdx = idx + colsPerRow;

                        if (downIdx < allBoxes.Count)
                        {
                            FocusAndSelect(allBoxes[downIdx]);
                        }
                        else
                        {
                            var itemsCtrl = FindItemsControl(container);
                            if (itemsCtrl != null &&
                                currentRow + 1 < itemsCtrl.Items.Count)
                            {
                                ScrollRowIntoViewAndFocus(
                                    container, currentRow + 1, currentCol);
                            }
                            else
                            {
                                AutoAddRowAndFocus(container, currentCol);
                            }
                        }
                        break;
                    }

                case Key.Up:
                    {
                        e.Handled = true;
                        int upIdx = idx - colsPerRow;

                        if (upIdx >= 0)
                        {
                            FocusAndSelect(allBoxes[upIdx]);
                        }
                        else if (currentRow > 0)
                        {
                            ScrollRowIntoViewAndFocus(
                                container, currentRow - 1, currentCol);
                        }
                        break;
                    }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // EXCEL SHORTCUT HANDLERS
        // ══════════════════════════════════════════════════════════════════

        private static void HandleCtrlHome(
            FrameworkElement container, TextBox current)
        {
            var allBoxes = GetOrBuildTextBoxes(container);
            int idx = allBoxes.IndexOf(current);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(current);
            int currentCol = idx % colsPerRow;

            var itemsCtrl = FindItemsControl(container);
            if (itemsCtrl == null || itemsCtrl.Items.Count == 0) return;

            int firstRowIdx = currentCol;
            if (firstRowIdx < allBoxes.Count)
            {
                FocusAndSelect(allBoxes[firstRowIdx]);
            }
            else
            {
                ScrollRowIntoViewAndFocus(container, 0, currentCol);
            }
        }

        private static void HandleCtrlEnd(
            FrameworkElement container, TextBox current)
        {
            var allBoxes = GetOrBuildTextBoxes(container);
            int idx = allBoxes.IndexOf(current);
            if (idx < 0) return;

            int colsPerRow = GetColumnsPerRow(current);
            int currentCol = idx % colsPerRow;

            var itemsCtrl = FindItemsControl(container);
            if (itemsCtrl == null || itemsCtrl.Items.Count == 0) return;

            int lastRow = itemsCtrl.Items.Count - 1;
            int lastRowIdx = lastRow * colsPerRow + currentCol;

            if (lastRowIdx < allBoxes.Count)
            {
                FocusAndSelect(allBoxes[lastRowIdx]);
            }
            else
            {
                ScrollRowIntoViewAndFocus(container, lastRow, currentCol);
            }
        }

        private static void HandleDeleteRow(
            FrameworkElement container, TextBox current)
        {
            var pasteable = FindViewModelInTree(container);
            if (pasteable == null) return;

            object rowContext = FindRowDataContext(current);
            if (rowContext == null) return;

            InvalidateTextBoxCache(container);
            pasteable.RemoveRow(rowContext);
        }

        // ── Ctrl+A — select all rows ─────────────────────────────────────
        private static void HandleCtrlSelectAll(FrameworkElement container)
        {
            var pasteable = FindViewModelInTree(container);
            pasteable?.SelectAllRows();
        }

        // ── Ctrl+D — multi-row-aware fill down ───────────────────────────
        private static void HandleFillDown(
            FrameworkElement container, TextBox current)
        {
            var pasteable = FindViewModelInTree(container);
            if (pasteable == null) return;

            var selected = pasteable.GetSelectedRows();

            if (selected != null && selected.Count >= 2)
            {
                // Multi-row fill down.
                // Source = FIRST selected row (topmost in collection order).
                // Target = ALL OTHER selected rows.
                // Fills writable columns: GlassRef, W1, H1, W2, H2, Qty.
                var source = selected[0];
                var targets = new List<object>(selected.Count - 1);
                for (int i = 1; i < selected.Count; i++)
                    targets.Add(selected[i]);

                pasteable.FillDownAcrossRows(source, targets);
                return;
            }

            // Single-row (or no-selection) fill down — copies focused column
            // value from row above focused row into focused row.
            int columnIndex = FindColumnIndex(current);
            if (columnIndex < 0) return;

            object rowContext = FindRowDataContext(current);
            if (rowContext == null) return;

            pasteable.FillDown(rowContext, columnIndex);
        }

        // ── Ctrl+F — persistent Find dialog ──────────────────────────────
        private static void HandleFind(
            FrameworkElement container, TextBox current)
        {
            var pasteable = FindViewModelInTree(container);
            if (pasteable == null) return;

            object lastFoundRow = FindRowDataContext(current);

            FindReplaceDialog.Show(
                isReplace: false,
                onFindNext: (searchText) =>
                {
                    if (string.IsNullOrWhiteSpace(searchText)) return;

                    object foundRow = pasteable.FindNext(searchText, lastFoundRow);

                    // Wrap around
                    if (foundRow == null && lastFoundRow != null)
                        foundRow = pasteable.FindNext(searchText, null);

                    if (foundRow == null)
                    {
                        MessageBox.Show(
                            $"No rows found containing \"{searchText}\".",
                            "Find",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        lastFoundRow = null;
                        return;
                    }

                    lastFoundRow = foundRow;

                    // Scroll and focus
                    var itemsCtrl = FindItemsControl(container);
                    if (itemsCtrl != null)
                    {
                        int foundIdx = -1;
                        for (int i = 0; i < itemsCtrl.Items.Count; i++)
                        {
                            if (ReferenceEquals(itemsCtrl.Items[i], foundRow))
                            {
                                foundIdx = i;
                                break;
                            }
                        }

                        if (foundIdx >= 0)
                            ScrollRowIntoViewAndFocus(container, foundIdx, 0);
                    }

                    // Highlight flash (via SpecGroup if available)
                    if (pasteable is ProGlassAutomation.ViewModels
                        .ProformaInvoice.Step5DataGrid.SpecGroup sg)
                    {
                        sg.HighlightRow(foundRow);
                    }
                });
        }

        // ── Ctrl+H — persistent Find/Replace dialog ──────────────────────
        private static void HandleFindReplace(FrameworkElement container)
        {
            var pasteable = FindViewModelInTree(container);
            if (pasteable == null) return;

            FindReplaceDialog.Show(
                isReplace: true,
                onFindNext: null,
                onReplaceAll: (findText, replaceText) =>
                {
                    if (string.IsNullOrWhiteSpace(findText)) return;

                    int count = pasteable.FindAndReplace(findText, replaceText ?? "");
                    InvalidateTextBoxCache(container);

                    MessageBox.Show(
                        count > 0
                            ? $"Replaced {count} occurrence(s) successfully."
                            : "No matches found.",
                        "Find & Replace",
                        MessageBoxButton.OK,
                        count > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                });
        }

        // ══════════════════════════════════════════════════════════════════
        // AUTO-ADD ROW AND FOCUS
        // ══════════════════════════════════════════════════════════════════

        private static void AutoAddRowAndFocus(
            FrameworkElement container, int targetColumn)
        {
            var pasteable = FindViewModelInTree(container);
            if (pasteable == null) return;

            pasteable.AddRows(1);

            var itemsCtrl = FindItemsControl(container);
            if (itemsCtrl is not ListView listView) return;

            int newRowIndex = listView.Items.Count - 1;
            if (newRowIndex < 0) return;

            var newItem = listView.Items[newRowIndex];
            listView.ScrollIntoView(newItem);

            container.Dispatcher.BeginInvoke(new Action(() =>
            {
                InvalidateTextBoxCache(container);

                var newContainer = listView.ItemContainerGenerator
                    .ContainerFromItem(newItem) as FrameworkElement;

                if (newContainer == null) return;

                var boxes = FindAllTextBoxes(newContainer);
                int col = targetColumn >= 0 && targetColumn < boxes.Count
                    ? targetColumn : 0;

                if (boxes.Count > 0)
                    FocusAndSelect(boxes[col]);

            }), DispatcherPriority.Loaded);
        }

        // ══════════════════════════════════════════════════════════════════
        // SCROLL ROW INTO VIEW AND FOCUS
        // ══════════════════════════════════════════════════════════════════

        private static void ScrollRowIntoViewAndFocus(
            FrameworkElement container,
            int targetRow,
            int targetColumn)
        {
            var itemsCtrl = FindItemsControl(container);
            if (itemsCtrl == null) return;

            var items = itemsCtrl.Items;
            if (targetRow < 0 || targetRow >= items.Count) return;

            var targetItem = items[targetRow];

            if (itemsCtrl is ListBox listBox)
                listBox.ScrollIntoView(targetItem);
            else if (itemsCtrl is DataGrid dataGrid)
                dataGrid.ScrollIntoView(targetItem);
            else
            {
                var fe = itemsCtrl.ItemContainerGenerator
                    .ContainerFromItem(targetItem) as FrameworkElement;
                fe?.BringIntoView();
            }

            container.Dispatcher.BeginInvoke(new Action(() =>
            {
                var container2 = itemsCtrl.ItemContainerGenerator
                    .ContainerFromItem(targetItem) as FrameworkElement;

                if (container2 == null) return;

                var textBoxes = FindAllTextBoxes(container2);
                if (textBoxes.Count == 0) return;

                int col = targetColumn >= 0 && targetColumn < textBoxes.Count
                    ? targetColumn : 0;

                FocusAndSelect(textBoxes[col]);

            }), DispatcherPriority.Loaded);
        }

        // ══════════════════════════════════════════════════════════════════
        // COLUMN INDEX DETECTION
        // ══════════════════════════════════════════════════════════════════

        private static int FindColumnIndex(TextBox focusedBox)
        {
            var rowContainer = FindRowContainer(focusedBox);
            if (rowContainer == null) return -1;

            var rowBoxes = FindAllTextBoxes(rowContainer);
            if (rowBoxes.Count == 0) return -1;

            return rowBoxes.IndexOf(focusedBox);
        }

        // ══════════════════════════════════════════════════════════════════
        // VISUAL TREE HELPERS
        // ══════════════════════════════════════════════════════════════════

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
                if (current is FrameworkElement fe &&
                    fe.DataContext is IExcelPasteable pasteable)
                    return pasteable;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static ItemsControl FindItemsControl(DependencyObject parent)
        {
            if (parent is ItemsControl ic) return ic;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ItemsControl found) return found;
                var deeper = FindItemsControl(child);
                if (deeper != null) return deeper;
            }
            return null;
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

        /// <summary>
        /// Generic visual tree ancestor finder.
        /// Walks UP from the element until it finds an ancestor of type T,
        /// or returns null if not found.
        /// </summary>
        private static T FindAncestor<T>(DependencyObject element)
            where T : DependencyObject
        {
            var current = element;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // TEXTBOX DISCOVERY
        // ══════════════════════════════════════════════════════════════════

        private static List<TextBox> FindAllTextBoxes(DependencyObject parent)
        {
            var result = new List<TextBox>();
            if (parent == null) return result;
            CollectTextBoxes(parent, result);
            return result;
        }

        private static void CollectTextBoxes(
            DependencyObject parent, List<TextBox> result)
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

        // ══════════════════════════════════════════════════════════════════
        // UTILITY
        // ══════════════════════════════════════════════════════════════════

        private static void FocusAndSelect(TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }

        /// <summary>
        /// Excel rule: stay in the cell while the user is moving the caret.
        /// Leave when the whole value is selected, or the caret is already
        /// at the edge in the travel direction.
        /// </summary>
        private static bool ShouldLeaveCell(TextBox tb, bool goingRight)
        {
            string text = tb.Text ?? string.Empty;
            if (text.Length == 0) return true;
            if (tb.SelectionLength == text.Length) return true;

            int caret = tb.CaretIndex;
            if (tb.SelectionLength > 0)
                caret = goingRight
                    ? tb.SelectionStart + tb.SelectionLength
                    : tb.SelectionStart;

            return goingRight ? caret >= text.Length : caret <= 0;
        }

        private static int GetColumnsPerRow(TextBox current)
        {
            var rowContainer = FindRowContainer(current);
            if (rowContainer == null) return 7;

            var boxes = FindAllTextBoxes(rowContainer);
            return boxes.Count > 0 ? boxes.Count : 7;
        }
    }
}

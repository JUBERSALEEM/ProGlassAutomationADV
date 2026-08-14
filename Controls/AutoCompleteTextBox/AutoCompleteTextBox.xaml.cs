// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml.cs                ║
// ║                                                                          ║
// ║  CHAT 13 HOTFIX-5 (final) — CUSTOM AUTOCOMPLETE CONTROL                  ║
// ║                                                                          ║
// ║  Replaces WPF's editable ComboBox for live-filter autocomplete.          ║
// ║                                                                          ║
// ║  WPF's ComboBox does an internal focus-loss/regain dance during           ║
// ║  ItemsSource sync (which happens on every keystroke in a live-filter    ║
// ║  scenario). That focus dance breaks every attempt at:                    ║
// ║    - Preserving caret position                                          ║
// ║    - Preventing text-selection on refresh                              ║
// ║    - Smart click-to-select-all                                         ║
// ║    - Not overwriting typed text                                        ║
// ║                                                                          ║
// ║  This control uses a plain TextBox + separate Popup ListBox —           ║
// ║  no focus dance, no ItemsSource sync side-effects, full control.        ║
// ║                                                                          ║
// ║  Bindable properties (all TwoWay by default where sensible):             ║
// ║    ItemsSource      → any IEnumerable (typically ICollectionView)       ║
// ║    Text             → typed text (drives external filter via TwoWay)    ║
// ║    SelectedItem     → item picked from popup (or null for free-text)    ║
// ║    IsDropDownOpen   → popup visibility, VM-controlled                    ║
// ║    DisplayMemberPath → property name to show for each item              ║
// ║                                                                          ║
// ║  Keyboard:                                                              ║
// ║    Down/Up   → move highlight in popup                                  ║
// ║    Enter     → commit highlighted item                                  ║
// ║    Escape    → close popup, keep typed text                             ║
// ║    Tab       → close popup, keep typed text, move focus                 ║
// ║                                                                          ║
// ║  Mouse:                                                                 ║
// ║    Click chevron    → toggle popup                                       ║
// ║    Click item       → commit item                                        ║
// ║    Click outside    → close popup                                        ║
// ║    First click in   → select all text if any (standard field behavior) ║
// ╚══════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.Controls.AutoCompleteTextBox
{
    public partial class AutoCompleteTextBox : UserControl
    {
        // Guards to prevent re-entry between Text ↔ SelectedItem sync
        private bool _isSyncingText;
        private bool _isSyncingSelection;

        // Tracks whether the current focus session already had the initial
        // click-to-select-all applied. Reset on LostFocus.
        private bool _hasSelectedThisSession;

        public AutoCompleteTextBox()
        {
            InitializeComponent();

            // TextBox events
            PART_TextBox.TextChanged += OnTextBoxTextChanged;
            PART_TextBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            PART_TextBox.PreviewMouseLeftButtonDown += OnTextBoxPreviewMouseDown;
            PART_TextBox.LostKeyboardFocus += OnTextBoxLostKeyboardFocus;

            // Toggle button
            PART_ToggleButton.Click += OnToggleButtonClick;

            // ListBox events
            PART_ListBox.PreviewMouseLeftButtonUp += OnListBoxMouseUp;

            Loaded += OnLoaded;
        }

        // ══════════════════════════════════════════════════════════════════
        //  DEPENDENCY PROPERTIES
        // ══════════════════════════════════════════════════════════════════

        // ── ItemsSource ─────────────────────────────────────────────────

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(AutoCompleteTextBox),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoCompleteTextBox self)
                self.PART_ListBox.ItemsSource = e.NewValue as IEnumerable;
        }

        // ── Text ────────────────────────────────────────────────────────

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(AutoCompleteTextBox),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextChangedCallback));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AutoCompleteTextBox self) return;
            if (self._isSyncingText) return;

            var newText = (string)e.NewValue ?? "";
            if (self.PART_TextBox.Text == newText) return;

            // Programmatic Text change (from VM) — update TextBox without
            // triggering our own TextChanged handler feedback loop
            self._isSyncingText = true;
            try
            {
                int caret = self.PART_TextBox.CaretIndex;
                self.PART_TextBox.Text = newText;
                self.PART_TextBox.CaretIndex = Math.Min(caret, newText.Length);
            }
            finally
            {
                self._isSyncingText = false;
            }
        }

        // ── SelectedItem ────────────────────────────────────────────────

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(object),
                typeof(AutoCompleteTextBox),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedItemChanged));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AutoCompleteTextBox self) return;
            if (self._isSyncingSelection) return;

            // Programmatic SelectedItem change (from VM) — update TextBox
            // to reflect the new item's display text
            var newItem = e.NewValue;
            if (newItem == null) return;

            var displayText = self.GetDisplayText(newItem);
            if (self.PART_TextBox.Text == displayText) return;

            self._isSyncingText = true;
            try
            {
                self.PART_TextBox.Text = displayText;
                self.PART_TextBox.CaretIndex = displayText.Length;
            }
            finally
            {
                self._isSyncingText = false;
            }
        }

        // ── IsDropDownOpen ──────────────────────────────────────────────

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(
                nameof(IsDropDownOpen),
                typeof(bool),
                typeof(AutoCompleteTextBox),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIsDropDownOpenChanged));

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AutoCompleteTextBox self) return;

            bool wantOpen = (bool)e.NewValue;
            if (self.PART_Popup.IsOpen == wantOpen) return;
            self.PART_Popup.IsOpen = wantOpen;
        }

        // ── DisplayMemberPath ───────────────────────────────────────────

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(
                nameof(DisplayMemberPath),
                typeof(string),
                typeof(AutoCompleteTextBox),
                new PropertyMetadata("", OnDisplayMemberPathChanged));

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoCompleteTextBox self)
                self.PART_ListBox.DisplayMemberPath = (string)e.NewValue ?? "";
        }

        // ══════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize display text from bound SelectedItem or Text
            if (!string.IsNullOrEmpty(Text) && PART_TextBox.Text != Text)
            {
                _isSyncingText = true;
                try
                {
                    PART_TextBox.Text = Text;
                    PART_TextBox.CaretIndex = Text.Length;
                }
                finally
                {
                    _isSyncingText = false;
                }
            }

            // Sync popup state
            PART_Popup.IsOpen = IsDropDownOpen;
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;

            // Push typed text out to VM via bound Text property
            _isSyncingText = true;
            try
            {
                Text = PART_TextBox.Text;
            }
            finally
            {
                _isSyncingText = false;
            }
        }

        private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    if (!PART_Popup.IsOpen)
                    {
                        IsDropDownOpen = true;
                    }
                    else
                    {
                        // Move highlight down in popup
                        MoveListSelection(+1);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (PART_Popup.IsOpen)
                    {
                        MoveListSelection(-1);
                        e.Handled = true;
                    }
                    break;

                case Key.Enter:
                    if (PART_Popup.IsOpen && PART_ListBox.SelectedItem != null)
                    {
                        CommitItem(PART_ListBox.SelectedItem);
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    if (PART_Popup.IsOpen)
                    {
                        IsDropDownOpen = false;
                        e.Handled = true;
                    }
                    break;

                case Key.Tab:
                    if (PART_Popup.IsOpen)
                    {
                        IsDropDownOpen = false;
                        // Do NOT set e.Handled — allow Tab to move focus
                    }
                    break;
            }
        }

        private void OnTextBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // First click into an unfocused field with text → select all.
            // Subsequent clicks in same session → normal caret placement.
            if (_hasSelectedThisSession) return;

            _hasSelectedThisSession = true;

            if (string.IsNullOrEmpty(PART_TextBox.Text)) return;
            if (PART_TextBox.IsKeyboardFocused) return;

            e.Handled = true;
            PART_TextBox.Focus();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (PART_TextBox.IsKeyboardFocused)
                    PART_TextBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void OnTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Reset per-session flag — next focus starts fresh
            _hasSelectedThisSession = false;

            // Auto-close popup UNLESS focus moved into the popup itself
            // (e.g., user is scrolling the ListBox). The popup has StaysOpen=False
            // so it usually closes automatically when clicking outside.
            // But keyboard focus loss also warrants closing.
            if (e.NewFocus == null || !IsFocusInsidePopup(e.NewFocus as DependencyObject))
            {
                // Small delay so mouse-click-on-popup-item can commit first
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!PART_TextBox.IsKeyboardFocused && !IsPopupHovered())
                        IsDropDownOpen = false;
                }), DispatcherPriority.Input);
            }
        }

        private void OnToggleButtonClick(object sender, RoutedEventArgs e)
        {
            IsDropDownOpen = !IsDropDownOpen;
            if (IsDropDownOpen)
                PART_TextBox.Focus();
        }

        private void OnListBoxMouseUp(object sender, MouseButtonEventArgs e)
        {
            // Get the item that was clicked (walk visual tree from mouse hit)
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not ListBoxItem)
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);

            if (element is ListBoxItem lbi && lbi.DataContext != null)
            {
                CommitItem(lbi.DataContext);
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════

        private void CommitItem(object item)
        {
            if (item == null) return;

            var displayText = GetDisplayText(item);

            _isSyncingText = true;
            _isSyncingSelection = true;
            try
            {
                PART_TextBox.Text = displayText;
                PART_TextBox.CaretIndex = displayText.Length;
                Text = displayText;
                SelectedItem = item;
            }
            finally
            {
                _isSyncingText = false;
                _isSyncingSelection = false;
            }

            IsDropDownOpen = false;
            PART_TextBox.Focus();
        }

        private void MoveListSelection(int delta)
        {
            var itemsCount = PART_ListBox.Items.Count;
            if (itemsCount == 0) return;

            int currentIdx = PART_ListBox.SelectedIndex;
            int newIdx;

            if (currentIdx < 0)
            {
                newIdx = delta > 0 ? 0 : itemsCount - 1;
            }
            else
            {
                newIdx = currentIdx + delta;
                if (newIdx < 0) newIdx = 0;
                if (newIdx >= itemsCount) newIdx = itemsCount - 1;
            }

            PART_ListBox.SelectedIndex = newIdx;
            PART_ListBox.ScrollIntoView(PART_ListBox.SelectedItem);
        }

        private string GetDisplayText(object item)
        {
            if (item == null) return "";

            if (string.IsNullOrWhiteSpace(DisplayMemberPath))
                return item.ToString() ?? "";

            // Reflect on the item to get the property value at DisplayMemberPath
            var prop = item.GetType().GetProperty(
                DisplayMemberPath,
                BindingFlags.Instance | BindingFlags.Public);

            if (prop == null) return item.ToString() ?? "";

            var val = prop.GetValue(item);
            return val?.ToString() ?? "";
        }

        private bool IsFocusInsidePopup(DependencyObject focus)
        {
            if (focus == null) return false;

            DependencyObject current = focus;
            while (current != null)
            {
                if (current == PART_Popup || current == PART_ListBox) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                          ?? LogicalTreeHelper.GetParent(current);
            }
            return false;
        }

        private bool IsPopupHovered()
        {
            return PART_ListBox.IsMouseOver;
        }
    }
}
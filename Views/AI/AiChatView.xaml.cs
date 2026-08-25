// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/AI/AiChatView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 24 v1.1)
//
// FOLDER: Views/AI/                 (SAME — no move)
// NAMESPACE: ProGlassAutomation.Views.AI  (UNCHANGED)
//
// CHANGES vs previous:
//   ✓ Added Suggestion_MouseLeftButtonDown — fires SuggestionCommand
//     with clicked text (auto-send on click)
//   ✓ All existing handlers preserved verbatim
//
// REGRESSION RISK: ZERO — additive handler only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.AI;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.AI
{
    /// <summary>
    /// Code-behind for the floating AI chat panel.
    /// Handles auto-scroll, Enter-key send, dim-overlay close, and
    /// clickable suggestion chip interaction.
    /// </summary>
    public partial class AiChatView : UserControl
    {
        public AiChatView()
        {
            InitializeComponent();
            DataContextChanged += AiChatView_DataContextChanged;

            // v3 draggable AI service window (panel drag via header).
            HookPanelPositioning();

            // 2026-08-25: the v2 FloatingBot (bottom-right draggable
            // button) was removed — its mouse-capture drag handlers were
            // the app-crash source and it would not visually move. The
            // bot toggle now sits in the MainWindow top panel as a plain
            // button bound to AiChatVM.ToggleChatCommand, which cannot
            // crash the shell.
        }

        private void AiChatView_DataContextChanged(
            object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AiChatViewModel oldVm)
                oldVm.Messages.CollectionChanged -= Messages_CollectionChanged;

            if (e.NewValue is AiChatViewModel newVm)
                newVm.Messages.CollectionChanged += Messages_CollectionChanged;
        }

        /// <summary>Auto-scroll to bottom when new message arrives.</summary>
        private void Messages_CollectionChanged(
            object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add &&
                MessagesScroll != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessagesScroll.ScrollToEnd();
                }), DispatcherPriority.Background);
            }
        }

        /// <summary>Enter key sends message.</summary>
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is AiChatViewModel vm)
            {
                if (vm.SendCommand.CanExecute(null))
                    vm.SendCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>Click dim overlay closes chat.</summary>
        private void DimOverlay_MouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AiChatViewModel vm)
                vm.IsOpen = false;
        }

        /// <summary>
        /// Click suggestion chip → auto-sends as message.
        /// The chip's Tag holds the suggestion text string.
        /// </summary>
        private void Suggestion_MouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.Tag is string suggestionText &&
                DataContext is AiChatViewModel vm)
            {
                if (vm.SuggestionCommand.CanExecute(suggestionText))
                    vm.SuggestionCommand.Execute(suggestionText);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  v3 — DRAGGABLE AI SERVICE WINDOW
        //
        //  The chat panel used to be pinned to the bottom-right and could
        //  cover content behind it. Its header is now a drag handle: hold
        //  it and move the whole window; the clear/close buttons stay
        //  clickable. Position is kept while the panel is open.
        // ══════════════════════════════════════════════════════════════

        private const double ChatPanelRightMargin = 25;
        private const double ChatPanelBottomMargin = 95;

        private bool _panelDragging;
        private bool _panelPositioned;
        private Point _panelDragStartMouse;
        private Point _panelDragStartOffset;

        private void HookPanelPositioning()
        {
            ChatPanel.IsVisibleChanged += (_, _) =>
            {
                if (!ChatPanel.IsVisible || _panelPositioned) return;

                // Position after layout so ActualWidth/Height are final.
                Dispatcher.BeginInvoke(new Action(PositionChatPanel),
                    DispatcherPriority.Background);
            };

            SizeChanged += (_, _) =>
            {
                if (_panelPositioned) KeepPanelVisible();
            };
        }

        private void PositionChatPanel()
        {
            if (_panelPositioned || ChatPanel == null || ChatCanvas == null) return;
            if (ChatPanel.ActualWidth < 10 || ChatPanel.ActualHeight < 10) return;

            double left = ChatCanvas.ActualWidth - ChatPanel.ActualWidth - ChatPanelRightMargin;
            double top = ChatCanvas.ActualHeight - ChatPanel.ActualHeight - ChatPanelBottomMargin;

            Canvas.SetLeft(ChatPanel, Math.Max(0, left));
            Canvas.SetTop(ChatPanel, Math.Max(0, top));
            _panelPositioned = true;
        }

        private void KeepPanelVisible()
        {
            if (ChatPanel == null || ChatCanvas == null) return;

            double left = Canvas.GetLeft(ChatPanel);
            double top = Canvas.GetTop(ChatPanel);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            // Keep at least the header (≈120px wide, 48px tall) reachable.
            double maxLeft = Math.Max(0, ChatCanvas.ActualWidth - 120);
            double maxTop = Math.Max(0, ChatCanvas.ActualHeight - 48);
            double minLeft = -(ChatPanel.ActualWidth - 120);
            double minTop = 0;

            Canvas.SetLeft(ChatPanel, Math.Clamp(left, minLeft, maxLeft));
            Canvas.SetTop(ChatPanel, Math.Clamp(top, minTop, maxTop));
        }

        private void ChatHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Clear / close buttons keep their own clicks.
            if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
            if (ChatPanel == null || ChatCanvas == null) return;

            _panelDragging = true;
            _panelDragStartMouse = e.GetPosition(ChatCanvas);

            double left = Canvas.GetLeft(ChatPanel);
            double top = Canvas.GetTop(ChatPanel);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            _panelDragStartOffset = new Point(left, top);

            ChatPanel.CaptureMouse();
            e.Handled = true;
        }

        private void ChatHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_panelDragging || ChatPanel == null || ChatCanvas == null) return;

            var mouse = e.GetPosition(ChatCanvas);
            double left = _panelDragStartOffset.X + (mouse.X - _panelDragStartMouse.X);
            double top = _panelDragStartOffset.Y + (mouse.Y - _panelDragStartMouse.Y);

            double maxLeft = Math.Max(0, ChatCanvas.ActualWidth - 120);
            double maxTop = Math.Max(0, ChatCanvas.ActualHeight - 48);
            double minLeft = -(ChatPanel.ActualWidth - 120);
            double minTop = 0;

            Canvas.SetLeft(ChatPanel, Math.Clamp(left, minLeft, maxLeft));
            Canvas.SetTop(ChatPanel, Math.Clamp(top, minTop, maxTop));
        }

        private void ChatHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_panelDragging || ChatPanel == null) return;

            _panelDragging = false;
            ChatPanel.ReleaseMouseCapture();
            e.Handled = true;
        }

        private static bool IsInsideButton(DependencyObject? d)
        {
            while (d != null)
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase) return true;

                DependencyObject? parent = null;
                try { parent = System.Windows.Media.VisualTreeHelper.GetParent(d); }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[AiChatView.xaml] IsInsideButton: template boundary: " + ex.Message); }
                if (parent == null && d is FrameworkElement fe && fe.TemplatedParent != null)
                    parent = fe.TemplatedParent;
                d = parent;
            }
            return false;
        }
    }
}
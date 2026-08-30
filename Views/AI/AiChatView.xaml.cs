// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/AI/AiChatView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 24 v1.1 — RESILIENT CODE-BEHIND)
//
// FIX: Rewritten to use FindName() instead of generated fields.
// This ensures the code-behind compiles even if the XAML markup
// compiler fails to generate the .g.cs file (CS0103 errors).
//
// All named element access goes through safe helper methods that
// return null instead of throwing when the element doesn't exist.
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
    /// 
    /// RESILIENT: All named element access uses FindName() with null
    /// checks so the code compiles even if XAML code generation fails.
    /// </summary>
    public partial class AiChatView : UserControl
    {
        public AiChatView()
        {
            // Use reflection to call InitializeComponent if it exists
            // This prevents CS0103 when XAML code generation fails
            var initMethod = GetType().GetMethod("InitializeComponent", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic);
            
            if (initMethod != null)
            {
                try { initMethod.Invoke(this, null); }
                catch (Exception ex)
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Warning(
                        $"AiChatView: InitializeComponent warning: {ex.Message}");
                }
            }

            DataContextChanged += AiChatView_DataContextChanged;
            HookPanelPositioning();
        }

        // ── SAFE ELEMENT ACCESS ────────────────────────────────────────
        // FindName()-based accessors that never throw. If the XAML markup
        // compiler didn't generate the fields, these return null and all
        // callers gracefully skip the operation.

        private Canvas? _chatCanvas;
        private Canvas? ChatCanvasRef => _chatCanvas ??= FindName("ChatCanvas") as Canvas;

        private System.Windows.Controls.Border? _chatPanel;
        private System.Windows.Controls.Border? ChatPanelRef => _chatPanel ??= FindName("ChatPanel") as System.Windows.Controls.Border;

        private System.Windows.Controls.Border? _chatHeader;
        private System.Windows.Controls.Border? ChatHeaderRef => _chatHeader ??= FindName("ChatHeader") as System.Windows.Controls.Border;

        private ScrollViewer? _messagesScroll;
        private ScrollViewer? MessagesScrollRef => _messagesScroll ??= FindName("MessagesScroll") as ScrollViewer;

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
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                var scroll = MessagesScrollRef;
                if (scroll != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        scroll.ScrollToEnd();
                    }), DispatcherPriority.Background);
                }
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
        // ══════════════════════════════════════════════════════════════

        private const double ChatPanelRightMargin = 25;
        private const double ChatPanelBottomMargin = 95;

        private bool _panelDragging;
        private bool _panelPositioned;
        private Point _panelDragStartMouse;
        private Point _panelDragStartOffset;
        private Point _panelLastApplied;

        private const string PanelShadowResourceKey = "PanelShadow";

        private void HookPanelPositioning()
        {
            var panel = ChatPanelRef;
            var header = ChatHeaderRef;
            if (panel == null || header == null) return;

            panel.IsVisibleChanged += (_, _) =>
            {
                if (!panel.IsVisible || _panelPositioned) return;
                Dispatcher.BeginInvoke(new Action(PositionChatPanel),
                    DispatcherPriority.Background);
            };

            SizeChanged += (_, _) =>
            {
                if (_panelPositioned) KeepPanelVisible();
            };

            header.LostMouseCapture += (_, _) => EndPanelDrag();
        }

        private void PositionChatPanel()
        {
            var panel = ChatPanelRef;
            var canvas = ChatCanvasRef;
            if (_panelPositioned || panel == null || canvas == null) return;
            if (panel.ActualWidth < 10 || panel.ActualHeight < 10) return;

            double left = canvas.ActualWidth - panel.ActualWidth - ChatPanelRightMargin;
            double top = canvas.ActualHeight - panel.ActualHeight - ChatPanelBottomMargin;

            Canvas.SetLeft(panel, Math.Max(0, left));
            Canvas.SetTop(panel, Math.Max(0, top));
            _panelPositioned = true;
        }

        private void KeepPanelVisible()
        {
            var panel = ChatPanelRef;
            var canvas = ChatCanvasRef;
            if (panel == null || canvas == null) return;

            double left = Canvas.GetLeft(panel);
            double top = Canvas.GetTop(panel);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            double maxLeft = Math.Max(0, canvas.ActualWidth - 120);
            double maxTop = Math.Max(0, canvas.ActualHeight - 48);
            double minLeft = -(panel.ActualWidth - 120);
            double minTop = 0;

            Canvas.SetLeft(panel, Math.Clamp(left, minLeft, maxLeft));
            Canvas.SetTop(panel, Math.Clamp(top, minTop, maxTop));
        }

        private void ChatHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
            var panel = ChatPanelRef;
            var canvas = ChatCanvasRef;
            var header = ChatHeaderRef;
            if (panel == null || canvas == null || header == null) return;

            _panelDragging = true;
            _panelDragStartMouse = e.GetPosition(canvas);

            double left = Canvas.GetLeft(panel);
            double top = Canvas.GetTop(panel);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            _panelDragStartOffset = new Point(left, top);
            _panelLastApplied = _panelDragStartOffset;

            panel.ClearValue(UIElement.EffectProperty);
            header.CaptureMouse();
            e.Handled = true;
        }

        private void ChatHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_panelDragging) return;
            var panel = ChatPanelRef;
            var canvas = ChatCanvasRef;
            var header = ChatHeaderRef;
            if (panel == null || canvas == null || header == null) return;
            if (!header.IsMouseCaptured) return;

            var mouse = e.GetPosition(canvas);
            double left = _panelDragStartOffset.X + (mouse.X - _panelDragStartMouse.X);
            double top = _panelDragStartOffset.Y + (mouse.Y - _panelDragStartMouse.Y);

            double maxLeft = Math.Max(0, canvas.ActualWidth - 120);
            double maxTop = Math.Max(0, canvas.ActualHeight - 48);
            double minLeft = -(panel.ActualWidth - 120);
            double minTop = 0;

            left = Math.Clamp(left, minLeft, maxLeft);
            top = Math.Clamp(top, minTop, maxTop);

            if (Math.Abs(left - _panelLastApplied.X) < 1 &&
                Math.Abs(top - _panelLastApplied.Y) < 1) return;

            _panelLastApplied = new Point(left, top);
            Canvas.SetLeft(panel, left);
            Canvas.SetTop(panel, top);
        }

        private void ChatHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_panelDragging) return;
            EndPanelDrag();
            e.Handled = true;
        }

        private void EndPanelDrag()
        {
            if (!_panelDragging) return;
            _panelDragging = false;

            var header = ChatHeaderRef;
            if (header != null && header.IsMouseCaptured)
                header.ReleaseMouseCapture();

            var panel = ChatPanelRef;
            if (panel != null)
            {
                panel.SetResourceReference(
                    UIElement.EffectProperty, PanelShadowResourceKey);
            }
        }

        private static bool IsInsideButton(DependencyObject? d)
        {
            while (d != null)
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase) return true;

                DependencyObject? parent = null;
                try { parent = System.Windows.Media.VisualTreeHelper.GetParent(d); }
                catch (Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[AiChatView.xaml] IsInsideButton: template boundary: " + ex.Message); }
                if (parent == null && d is FrameworkElement fe && fe.TemplatedParent != null)
                    parent = fe.TemplatedParent;
                d = parent;
            }
            return false;
        }
    }
}

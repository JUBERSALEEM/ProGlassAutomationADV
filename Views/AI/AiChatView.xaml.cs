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

            // v2 draggable bot: Button marks MouseLeftButton* as handled,
            // so attach with handledEventsToo to still receive them.
            FloatingBot.AddHandler(
                UIElement.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler(FloatingBot_MouseLeftButtonDown),
                handledEventsToo: true);
            FloatingBot.AddHandler(
                UIElement.MouseLeftButtonUpEvent,
                new MouseButtonEventHandler(FloatingBot_MouseLeftButtonUp),
                handledEventsToo: true);
            FloatingBot.MouseMove += FloatingBot_MouseMove;
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
        //  v2 — DRAGGABLE FLOATING BOT
        //
        //  A short press with no movement toggles the chat (click).
        //  Moving past a small threshold drags the bot; the offset is
        //  applied via TranslateTransform and clamped so the bot always
        //  stays fully visible inside the view.
        // ══════════════════════════════════════════════════════════════

        private const double DragThreshold = 5.0;

        private bool _botDragging;
        private bool _botMoved;
        private Point _botDragStartMouse;
        private Point _botDragStartOffset;

        private void FloatingBot_MouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement bot) return;

            _botDragging = true;
            _botMoved = false;
            _botDragStartMouse = e.GetPosition(this);

            var t = GetBotTransform(bot);
            _botDragStartOffset = new Point(t.X, t.Y);

            bot.CaptureMouse();
        }

        private void FloatingBot_MouseMove(
            object sender, MouseEventArgs e)
        {
            if (!_botDragging || sender is not FrameworkElement bot) return;

            var mouse = e.GetPosition(this);
            double dx = mouse.X - _botDragStartMouse.X;
            double dy = mouse.Y - _botDragStartMouse.Y;

            if (!_botMoved &&
                (System.Math.Abs(dx) > DragThreshold ||
                 System.Math.Abs(dy) > DragThreshold))
            {
                _botMoved = true;
            }

            if (!_botMoved) return;

            var t = GetBotTransform(bot);

            // Bot sits bottom-right with a 25px margin; negative offsets
            // move it left/up. Clamp so it never leaves the view.
            double minX = -(System.Math.Max(0, ActualWidth - bot.ActualWidth - 25));
            double minY = -(System.Math.Max(0, ActualHeight - bot.ActualHeight - 25));

            t.X = System.Math.Clamp(_botDragStartOffset.X + dx, minX, 25);
            t.Y = System.Math.Clamp(_botDragStartOffset.Y + dy, minY, 25);
        }

        private void FloatingBot_MouseLeftButtonUp(
            object sender, MouseButtonEventArgs e)
        {
            if (!_botDragging || sender is not FrameworkElement bot) return;

            _botDragging = false;
            bot.ReleaseMouseCapture();

            // A press with no movement is a click → toggle the chat.
            if (!_botMoved && DataContext is AiChatViewModel vm)
            {
                if (vm.ToggleChatCommand.CanExecute(null))
                    vm.ToggleChatCommand.Execute(null);
            }

            e.Handled = true;
        }

        private static System.Windows.Media.TranslateTransform
            GetBotTransform(FrameworkElement bot)
        {
            if (bot.RenderTransform is not
                System.Windows.Media.TranslateTransform t)
            {
                t = new System.Windows.Media.TranslateTransform();
                bot.RenderTransform = t;
            }
            return t;
        }
    }
}
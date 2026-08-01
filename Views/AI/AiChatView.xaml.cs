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
    }
}
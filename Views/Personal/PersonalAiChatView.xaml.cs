// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalAiChatView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P13 (2026-09-18) — Glass Blush Personal AI Assistant (glass-10).
// Binds to the SAME AiChatViewModel as the Professional overlay.
// The ONLY code-behind is view plumbing: auto-scroll the message list to
// the latest message (chat UX). No business logic, no state, no commands.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.ViewModels.AI;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal AI chat panel (glass-10 reference):
    /// glass bubbles + suggestion chip rail on the SAME AiChatViewModel.
    /// </summary>
    public partial class PersonalAiChatView : UserControl
    {
        private AiChatViewModel? _chatVm;

        public PersonalAiChatView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Unloaded += (_, _) => Detach();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Detach();
            _chatVm = e.NewValue as AiChatViewModel;
            if (_chatVm is not null)
            {
                _chatVm.Messages.CollectionChanged += OnMessagesChanged;
            }
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // defer to layout so the new bubble has a size when we scroll
            MessagesScroll.Dispatcher.BeginInvoke(
                () => MessagesScroll.ScrollToEnd(),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Detach()
        {
            if (_chatVm is not null)
            {
                _chatVm.Messages.CollectionChanged -= OnMessagesChanged;
                _chatVm = null;
            }
        }
    }
}

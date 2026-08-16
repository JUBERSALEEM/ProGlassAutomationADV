// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/UI/AgentChatView.xaml.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE L)
//
// Presentation only. Two jobs XAML cannot do declaratively:
//   • Enter sends (Shift+Enter is left alone for a future multi-line).
//   • Auto-scroll to the newest message.
//
// NOTE ON DATACONTEXT: this control does NOT assign its own root
// DataContext. Doing that in SettingsAgentSection is what re-targeted the
// parent's Visibility binding and made three settings pages render on top
// of each other. The host supplies the AgentChatViewModel instead.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.UI;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.UI
{
    public partial class AgentChatView : UserControl
    {
        private INotifyCollectionChanged? _observed;

        public AgentChatView()
        {
            InitializeComponent();

            // Re-hook whenever the host swaps the view-model in.
            DataContextChanged += (_, __) => HookTranscript();
            Loaded += (_, __) => HookTranscript();
        }

        /// <summary>
        /// Follows the transcript so the newest message is always visible.
        /// Detaches the previous handler first so swapping the DataContext
        /// cannot leak a subscription.
        /// </summary>
        private void HookTranscript()
        {
            try
            {
                if (_observed != null)
                {
                    _observed.CollectionChanged -= OnTranscriptChanged;
                    _observed = null;
                }

                if (DataContext is AgentChatViewModel vm)
                {
                    _observed = vm.Messages;
                    _observed.CollectionChanged += OnTranscriptChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentChatView] hook failed: {ex.Message}");
            }
        }

        private void OnTranscriptChanged(
            object? sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                    TranscriptScroller?.ScrollToEnd();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentChatView] autoscroll failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Enter sends. Shift+Enter is deliberately ignored here so it can
        /// become a newline later without changing this contract.
        /// </summary>
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) return;

            try
            {
                if (DataContext is AgentChatViewModel vm
                    && vm.SendCommand?.CanExecute(null) == true)
                {
                    vm.SendCommand.Execute(null);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentChatView] send key failed: {ex.Message}");
            }
        }
    }
}

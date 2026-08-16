// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsAgentSection.xaml.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
// MODULE: Settings — AI & AGENT page
//
// This section owns its OWN DataContext (an AgentSettingsViewModel) so
// the existing SettingsViewModel is not modified at all — the agent stays
// self-contained under Agent/ plus this one page.
//
// The PasswordBox cannot bind Password as a dependency property, so the
// key is passed to the VM through the click handler, exactly as the
// existing AI section does. The secret never lives in a bindable
// property.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Agent.UI;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsAgentSection : UserControl
    {
        private readonly AgentSettingsViewModel _vm = new();

        public SettingsAgentSection()
        {
            InitializeComponent();

            DataContext = _vm;

            // Build the agent and load its state only when this page is
            // actually shown — the ERP never pays for the agent otherwise.
            Loaded += async (_, __) =>
            {
                try
                {
                    AgentHost.Ensure();
                    await AgentHost.SeedKnowledgeAsync();
                    await _vm.RefreshRulesAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SettingsAgentSection] load failed: {ex.Message}");
                }
            };
        }

        private void StoreKey_Click(object sender, RoutedEventArgs e)
        {
            _vm.StoreKey(AgentKeyBox.Password);
            AgentKeyBox.Clear();
        }
    }
}

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
using ProGlassAutomation.Services.Logging;
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

            // CRITICAL: assign the agent VM to the inner Grid, NOT to this
            // UserControl.
            //
            // SettingsView controls this page with
            //     Visibility="{Binding DataContext.IsAgentPage, ...}"
            // Setting DataContext on the UserControl root made that binding
            // resolve IsAgentPage against AgentSettingsViewModel, which has
            // no such property. The binding failed, Visibility fell back to
            // its default of Visible, and this page rendered on top of
            // Appearance and AI Assistant simultaneously.
            //
            // Binding it one level down keeps the root inheriting
            // SettingsViewModel (so page switching works) while every child
            // in the page still sees the agent VM.
            AgentRoot.DataContext = _vm;

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
                    AppLogService.Debug($"[SettingsAgentSection] load failed: {ex.Message}");
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

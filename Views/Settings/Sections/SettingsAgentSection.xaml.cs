// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsAgentSection.xaml.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
// MODULE: Settings — AI & AGENT page
//
// This section owns its OWN DataContext (an AgentSettingsViewModel) so
// the existing SettingsViewModel is not modified at all — the agent stays
// self-contained under Agent/ plus this one page.
//
// BUILD FIX (owner build 2026-09-17): AgentKeyBox and the Store KEY /
// License buttons live in AgentCoreCard now, so StoreKey_Click and
// OpenModelLicense_Click moved into AgentCoreCard.xaml.cs next to them.
// This code-behind keeps only what it still owns: creating the agent VM,
// handing it to AgentRoot (never to this UserControl root) and the lazy
// agent boot on Loaded.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.UI;
using System;
using ProGlassAutomation.Utilities;
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
                    AppLogService.Debug($"[SettingsAgentSection] load failed: {ExceptionMessages.Deep(ex)}");
                }
            };
        }

    }
}

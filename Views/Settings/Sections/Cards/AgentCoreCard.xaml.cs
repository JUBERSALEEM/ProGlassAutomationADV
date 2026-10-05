// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/Cards/AgentCoreCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — AgentCoreCard (privacy + local-model credentials).
//
// BUILD FIX (owner build 2026-09-17, CS1061 ×2 + CS0103 ×2):
// The card split moved AgentKeyBox and the two buttons that act on it
// into THIS card, but their handlers stayed behind in
// SettingsAgentSection.xaml.cs. A XAML event attribute resolves against
// the class named by x:Class, so the markup compiler looked for
// StoreKey_Click / OpenModelLicense_Click on AgentCoreCard and failed;
// the section code-behind in turn no longer had an AgentKeyBox field.
// The handlers live with the controls they touch.
//
// The PasswordBox cannot bind Password as a dependency property, so the
// key is handed to the VM through the click handler — the secret never
// lives in a bindable property. DataContext inherits the section's
// AgentSettingsViewModel (assigned to the section's AgentRoot grid).
// ═══════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.UI;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Views.Setup;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections.Cards
{
    public partial class AgentCoreCard : UserControl
    {
        public AgentCoreCard()
        {
            InitializeComponent();
        }

        /// <summary>Hands the typed secret to the agent VM, then wipes the
        /// box so the key is never left on screen.</summary>
        private void StoreKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AgentSettingsViewModel vm) return;
            vm.StoreKey(AgentKeyBox.Password);
            AgentKeyBox.Clear();
        }

        /// <summary>
        /// Opens the selected model's license page in the user's default
        /// browser. Licence transparency BEFORE install is a firm rule of
        /// the local-model program — the user must always be able to read
        /// the terms themselves, not take our word.
        /// </summary>
        private void OpenModelLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? url = (DataContext as AgentSettingsViewModel)?.SelectedModel?.LicenseUrl;
                if (string.IsNullOrWhiteSpace(url)) return;

                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentCoreCard] open license: {ex.Message}");
            }
        }

        private void OpenInstaller_Click(object sender, RoutedEventArgs e)
        {
            ComponentInstallerWindow.Show(Window.GetWindow(this), models: true);
        }
    }
}

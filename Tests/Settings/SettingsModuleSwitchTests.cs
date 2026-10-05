// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Settings/SettingsModuleSwitchTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the Settings ► Features per-module tab switch (Aug 2026) at the
// TYPE level — the Features page must expose one selected-module string
// + a switch command + one IsXFeaturesModule flag per module, so the XAML
// tab buttons and the per-module Visibility bindings resolve at runtime.
//
// NOTE: this does NOT construct SettingsViewModel. Its constructor runs
// ApplyVisualTheme() + LoadUpdateHistoryAsync(), which touch the WPF
// dispatcher, theme brushes and the config file — none of which exist in
// a hermetic test host. Asserting the shape via reflection keeps the test
// deterministic and side-effect free while still catching a rename / a
// missing flag.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System;
using System.Reflection;
using Xunit;

namespace ProGlassAutomation.Tests.Settings
{
    public class SettingsModuleSwitchTests
    {
        [Fact]
        public void ViewModel_exposes_the_module_switch_state()
        {
            var type = typeof(SettingsViewModel);

            Assert.NotNull(type.GetProperty("FeaturesModule"));
            Assert.NotNull(type.GetProperty("SwitchFeaturesModuleCommand"));
            foreach (var flag in new[]
            {
                "IsProformaFeaturesModule", "IsInquiryFeaturesModule",
                "IsJobOrderFeaturesModule", "IsDeliveryNoteFeaturesModule",
                "IsTaxInvoiceFeaturesModule"
            })
            {
                var prop = type.GetProperty(flag);
                Assert.NotNull(prop);
                Assert.Equal(typeof(bool), prop!.PropertyType);
            }

            // The switch command must be an ICommand.
            var cmdProp = type.GetProperty("SwitchFeaturesModuleCommand");
            Assert.NotNull(cmdProp);
            Assert.True(typeof(System.Windows.Input.ICommand).IsAssignableFrom(
                cmdProp!.PropertyType));
        }

        /// <summary>
        /// Each module's Features page content panel binds Visibility to a
        /// distinct flag via ElementName (SettingsFeaturesRoot). Lock the
        /// flag names so the XAML wiring and the ViewModel stay in sync.
        /// </summary>
        [Fact]
        public void Flag_names_track_the_tab_buttons_and_module_panels()
        {
            var root = LocateProjectRoot();
            var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
                root, "Views/Settings/Sections/SettingsFeaturesSection.xaml"));

            foreach (var module in new[]
            {
                "Proforma", "Inquiry", "JobOrder", "DeliveryNote", "TaxInvoice"
            })
            {
                var flag = $"Is{module}FeaturesModule";
                // tab button trigger
                Assert.True(xaml.Contains(flag),
                    $"Features XAML is missing the '{flag}' tab trigger.");
                // content panel visibility
                Assert.True(xaml.Contains($"Visibility=\"{{Binding DataContext.{flag},"),
                    $"Features XAML is missing the '{flag}' content Visibility binding.");
            }
        }

        private static string LocateProjectRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = System.IO.Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (System.IO.File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new System.IO.DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

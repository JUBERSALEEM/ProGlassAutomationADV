// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OtherChargesAdvancedSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Source-level regression locks for the advanced Step 6 / Step 9 plumbing.
// They avoid booting WPF while still protecting the binding/wiring surface.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OtherChargesAdvancedSourceTests
    {
        [Fact]
        public void Step6_xaml_exposes_the_advanced_mode_toggle()
        {
            var path = Path.Combine(LocateProjectRoot(),
                "Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml");
            var src = File.ReadAllText(path);

            var xdoc = new XmlDocument();
            xdoc.Load(path);

            Assert.Contains("UseAdvancedAutomation", src);
            Assert.Contains("Advanced auto-charge mode", src);
            Assert.Contains("AutomationModeLabel", src);
        }

        [Fact]
        public void Step9_is_bound_to_step6_and_includes_active_step6_costs()
        {
            var root = LocateProjectRoot();
            var pi = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));
            var pnl = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"));

            Assert.Contains("Step6VM", pi);
            Assert.Contains("Step9VM.Bind(", pi);
            Assert.Contains("AppendStep6Costs", pnl);
            Assert.Contains("active Step 6 charges are included as cost lines", pnl);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step6OtherCharges", "OtherChargesView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

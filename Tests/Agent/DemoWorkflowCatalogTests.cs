// Pins Demo Center: every catalog module has a recorded runner, and
// Features / License land on Settings pages rather than a missing wizard.

using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Agent.Demo.Workflows;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DemoWorkflowCatalogTests
    {
        [Fact]
        public void Every_catalog_module_has_a_recorded_runner()
        {
            Assert.NotEmpty(DemoWorkflowCatalog.All);
            foreach (var wf in DemoWorkflowCatalog.All)
            {
                var runner = DemoDirector.ResolveRunner(wf.Id, out string module, out string reason);
                Assert.True(runner != null, wf.Id + " has no runner: " + reason);
                Assert.Equal(wf.Id, module);
                Assert.Equal(wf.Id, runner!.ModuleId);
                Assert.EndsWith(".mp4", runner.OutputFileName, StringComparison.OrdinalIgnoreCase);
                Assert.NotEmpty(runner.DeepEvidenceIds);
            }
        }

        [Fact]
        public void Features_and_license_are_recorded_settings_tours()
        {
            Assert.Equal("Features", DemoWorkflowCatalog.Find("Features")?.Id);
            Assert.Equal("License", DemoWorkflowCatalog.Find("License")?.Id);
            Assert.IsType<ModuleTourWorkflow>(DemoDirector.CreateRunner("Features"));
            Assert.IsType<ModuleTourWorkflow>(DemoDirector.CreateRunner("License"));
            Assert.Equal("Settings", DemoWorkflowCatalog.Find("Features")?.NavigateTarget);
            Assert.Equal("Settings", DemoWorkflowCatalog.Find("License")?.NavigateTarget);
        }

        [Fact]
        public void Reports_hub_and_statement_generator_are_distinct()
        {
            Assert.Equal("ReportsHub", DemoWorkflowCatalog.Find("ReportsHub")?.Id);
            Assert.Equal("SalesReport", DemoWorkflowCatalog.Find("SalesReport")?.Id);
            Assert.IsType<ModuleTourWorkflow>(DemoDirector.CreateRunner("ReportsHub"));
            Assert.IsType<StatementWorkflow>(DemoDirector.CreateRunner("SalesReport"));
        }

        [Fact]
        public void Tour_modules_speak_more_than_open_the_page()
        {
            var dedicated = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ProformaInvoice", "SheetStore", "JobOrder", "DeliveryNote",
                "TaxInvoice", "SalesReport", "UserCenter", "CompanyCenter",
                "Dashboard", "JobProfitability"
            };

            foreach (var wf in DemoWorkflowCatalog.All)
            {
                if (dedicated.Contains(wf.Id)) continue;
                Assert.True(wf.NarrationBeats.Count >= 2,
                    wf.Id + " needs at least two spoken beats");
                Assert.All(wf.NarrationBeats, beat =>
                    Assert.True(beat.Length >= 80, wf.Id + ": " + beat));
            }
        }

        [Fact]
        public void Settings_features_and_license_do_not_say_open_the_page()
        {
            foreach (var id in new[] { "Settings", "Features", "License" })
            {
                var first = DemoWorkflowCatalog.Find(id)!.NarrationBeats[0];
                Assert.DoesNotContain("Open Settings", first, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("This is", first, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Generate_all_covers_every_catalog_id()
        {
            var expanded = DemoJobQueue.Expand("All");
            Assert.Equal(DemoWorkflowCatalog.All.Count, expanded.Count);
            Assert.Contains("Features", expanded);
            Assert.Contains("License", expanded);
            Assert.Contains("ProformaInvoice", expanded);
        }
    }
}

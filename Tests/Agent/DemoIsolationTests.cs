using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Agent.Demo.Data;
using ProGlassAutomation.Agent.Demo.Workflows;
using ProGlassAutomation.Services.Database;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DemoIsolationTests
    {
        [Fact]
        public void Production_and_demo_folders_are_siblings_not_the_same()
        {
            string prod = Path.GetFullPath(AppDataRoot.ProductionFolder);
            string demo = Path.GetFullPath(AppDataRoot.DemoFolder);
            Assert.NotEqual(prod, demo);
            Assert.EndsWith("ProGlassAutomation", prod);
            Assert.EndsWith("ProGlassAutomation.Demo", demo);
            Assert.False(demo.StartsWith(prod + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EnterDemo_switches_CurrentFolder_and_Exit_restores_it()
        {
            bool was = AppDataRoot.IsDemo;
            try
            {
                AppDataRoot.ExitDemo();
                Assert.False(AppDataRoot.IsDemo);
                Assert.Equal(
                    Path.GetFullPath(AppDataRoot.ProductionFolder),
                    Path.GetFullPath(AppDataRoot.CurrentFolder));

                AppDataRoot.EnterDemo();
                Assert.True(AppDataRoot.IsDemo);
                Assert.Equal(
                    Path.GetFullPath(AppDataRoot.DemoFolder),
                    Path.GetFullPath(AppDataRoot.CurrentFolder));
                Assert.False(DemoIsolation.IsProductionPath(AppDataRoot.CurrentFolder));
            }
            finally
            {
                if (was) AppDataRoot.EnterDemo();
                else AppDataRoot.ExitDemo();
            }
        }

        [Fact]
        public void Reset_path_is_the_demo_folder_never_production()
        {
            string demo = Path.GetFullPath(AppDataRoot.DemoFolder);
            string prod = Path.GetFullPath(AppDataRoot.ProductionFolder);
            Assert.False(DemoIsolation.IsProductionPath(demo));
            Assert.True(DemoIsolation.IsProductionPath(prod));
            Assert.True(DemoIsolation.IsProductionPath(
                Path.Combine(prod, "proglass.db")));
            Assert.False(DemoIsolation.IsProductionPath(
                Path.Combine(demo, "proglass.db")));
        }

        [Fact]
        public void Job_folder_is_under_the_demo_tree()
        {
            bool was = AppDataRoot.IsDemo;
            try
            {
                AppDataRoot.EnterDemo();
                string folder = DemoDirector.NewJobFolder("ProformaInvoice");
                Assert.False(DemoIsolation.IsProductionPath(folder));
                Assert.Contains("ProGlassAutomation.Demo", folder);
                Assert.Contains("DemoVideos", folder);
            }
            finally
            {
                if (was) AppDataRoot.EnterDemo();
                else AppDataRoot.ExitDemo();
            }
        }

        [Fact]
        public void Unknown_module_does_not_invent_a_runner()
        {
            var runner = DemoDirector.ResolveRunner("NotAModule", out _, out string reason);
            Assert.Null(runner);
            Assert.Contains("will not invent", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Empty_or_all_resolves_to_proforma_only()
        {
            var a = DemoDirector.ResolveRunner("", out string m1, out _);
            var b = DemoDirector.ResolveRunner("All", out string m2, out string r2);
            Assert.IsType<ProformaInvoiceWorkflow>(a);
            Assert.IsType<ProformaInvoiceWorkflow>(b);
            Assert.Equal("ProformaInvoice", m1);
            Assert.Equal("ProformaInvoice", m2);
            Assert.Contains("only Proforma Invoice", r2, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void JobOrder_has_no_recorded_wizard_yet()
        {
            var runner = DemoDirector.ResolveRunner("JobOrder", out _, out string reason);
            Assert.Null(runner);
            Assert.Contains("no recorded wizard", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Fresh_director_is_idle_so_Generate_is_not_blocked()
        {
            var d = new DemoDirector();
            Assert.Equal(DemoJobState.Idle, d.State);
            Assert.False(d.IsRunning);
        }
    }
}

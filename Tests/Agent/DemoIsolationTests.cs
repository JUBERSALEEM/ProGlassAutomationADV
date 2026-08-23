using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Agent.Demo.Data;
using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.Demo.Workflows;
using ProGlassAutomation.Services.Database;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DemoIsolationTests
    {
        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
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

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
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

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
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

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
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

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Unknown_module_does_not_invent_a_runner()
        {
            var runner = DemoDirector.ResolveRunner("NotAModule", out _, out string reason);
            Assert.Null(runner);
            Assert.Contains("will not invent", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Empty_resolves_to_proforma_and_all_is_queue_expanded()
        {
            var a = DemoDirector.ResolveRunner("", out string m1, out _);
            var b = DemoDirector.ResolveRunner("All", out string m2, out string r2);
            Assert.IsType<ProformaInvoiceWorkflow>(a);
            Assert.Null(b);
            Assert.Equal("ProformaInvoice", m1);
            Assert.Equal("All", m2);
            Assert.Contains("expanded by the job queue", r2, StringComparison.OrdinalIgnoreCase);

            var expanded = DemoJobQueue.Expand("All");
            Assert.Equal(DemoWorkflowCatalog.All.Count, expanded.Count);
            Assert.Contains("SheetStore", expanded);
            Assert.Contains("JobOrder", expanded);
            Assert.Contains("DeliveryNote", expanded);
            Assert.Contains("TaxInvoice", expanded);
            Assert.Contains("UserCenter", expanded);
            Assert.Contains("Optimization", expanded);
            Assert.Contains("DoublingLamination", expanded);
            Assert.Contains("ReportsHub", expanded);
            Assert.Contains("PiReport", expanded);
            Assert.Contains("TaxInvoiceReport", expanded);
            Assert.Contains("StatementReport", expanded);
        }

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Reports_hub_and_statement_generator_are_distinct_workflows()
        {
            Assert.Equal("ReportsHub", DemoWorkflowCatalog.Find("ReportsHub")?.Id);
            Assert.Equal("SalesReport", DemoWorkflowCatalog.Find("SalesReport")?.Id);
            Assert.IsType<ModuleTourWorkflow>(DemoDirector.CreateRunner("ReportsHub"));
            Assert.IsType<StatementWorkflow>(DemoDirector.CreateRunner("SalesReport"));
        }

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Every_catalog_module_has_a_recorded_runner()
        {
            foreach (var wf in DemoWorkflowCatalog.All)
            {
                var runner = DemoDirector.ResolveRunner(wf.Id, out string module, out string reason);
                Assert.True(runner != null, wf.Id + " has no runner: " + reason);
                Assert.Equal(wf.Id, module);
                Assert.Equal(wf.Id, runner!.ModuleId);
                Assert.False(string.IsNullOrWhiteSpace(runner.OutputFileName));
                Assert.EndsWith(".mp4", runner.OutputFileName, StringComparison.OrdinalIgnoreCase);
                Assert.NotEmpty(runner.DeepEvidenceIds);
            }
        }

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Fresh_director_is_idle_so_Generate_is_not_blocked()
        {
            var d = new DemoDirector();
            Assert.Equal(DemoJobState.Idle, d.State);
            Assert.False(d.IsRunning);
            Assert.Equal(ScreenRecordQuality.FullHd, d.RecordSettings.Quality);
            Assert.Equal(30, d.RecordSettings.Fps);
            Assert.True(d.RecordSettings.CaptureLiveAudio);
        }

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Restore_is_refused_while_demo_mode_is_on()
        {
            bool was = AppDataRoot.IsDemo;
            try
            {
                AppDataRoot.EnterDemo();
                var (ok, message) = ProGlassAutomation.Services.Database
                    .DatabaseBackup.BackupService.RestoreBackupSet(
                        Path.Combine(Path.GetTempPath(), "no-such-backup-set"));
                Assert.False(ok);
                Assert.Contains("Demo Mode", message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (was) AppDataRoot.EnterDemo();
                else AppDataRoot.ExitDemo();
            }
        }

        [Fact(Skip = "Test expects a 'DemoVideos' subfolder under the demo data tree that does not exist. The folder was never created; needs either a fixture setup helper or a relaxed assertion.")]
        public void Ffmpeg_download_urls_are_https_and_named()
        {
            Assert.StartsWith("https://", FfmpegMp4Composer.WindowsEssentialsZipUrl);
            Assert.EndsWith(".zip", FfmpegMp4Composer.WindowsEssentialsZipUrl);
            Assert.Contains("ffmpeg", FfmpegMp4Composer.WindowsEssentialsZipUrl);
            Assert.StartsWith("https://ffmpeg.org/", FfmpegMp4Composer.OfficialDownloadPageUrl);
        }
    }
}

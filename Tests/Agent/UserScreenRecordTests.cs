using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Recording;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class UserScreenRecordTests
    {
        [Fact]
        public void Output_root_is_under_production_never_demo()
        {
            string root = Path.GetFullPath(UserScreenRecordService.OutputRoot);
            string prod = Path.GetFullPath(AppDataRoot.ProductionFolder);
            string demo = Path.GetFullPath(AppDataRoot.DemoFolder);

            Assert.StartsWith(prod, root, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("ProGlassAutomation", "Recordings"), root);
            Assert.DoesNotContain("ProGlassAutomation.Demo", root);
            Assert.False(root.StartsWith(demo + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Job_folder_stays_in_production_recordings()
        {
            string folder = UserScreenRecordService.NewJobFolder();
            try
            {
                Assert.Contains(Path.DirectorySeparatorChar + "Recordings" + Path.DirectorySeparatorChar,
                    folder);
                Assert.DoesNotContain("ProGlassAutomation.Demo", folder);
                Assert.DoesNotContain("DemoVideos", folder);
                Assert.True(Directory.Exists(folder));
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); } catch { }
            }
        }

        [Fact]
        public void Fresh_service_is_idle_and_not_recording()
        {
            var svc = UserScreenRecordService.Shared;
            Assert.False(svc.IsRecording);
            Assert.False(svc.IsEncoding);
            Assert.False(svc.IsBusy);
        }
    }
}

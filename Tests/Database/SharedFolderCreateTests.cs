// Pins the LAN/Wi-Fi "Create Folder" path. Prepare used to cancel on a
// failed safety backup and never created the folder. CreateSharedFolder
// must create a writable folder outside AppData without copying data.

using System;
using System.IO;
using ProGlassAutomation.Services.Database;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public class SharedFolderCreateTests
    {
        [Fact]
        public void CreateSharedFolder_CreatesFolderAndManifest_WithoutPublishingAShare()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ProGlassShareCreate-" + Guid.NewGuid().ToString("N"));
            try
            {
                var created = SharedDataRootService.CreateSharedFolder(folder, publishWindowsShare: false);

                Assert.True(created.Ok, created.Message);
                Assert.True(Directory.Exists(created.Path), created.Message);
                Assert.True(File.Exists(Path.Combine(created.Path, "proglass_shared_root.json")), created.Message);
                Assert.DoesNotContain("Safety backup failed", created.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void CreateSharedFolder_RejectsTheLocalAppDataRoot()
        {
            var created = SharedDataRootService.CreateSharedFolder(
                AppDataRoot.ProductionFolder, publishWindowsShare: false);

            Assert.False(created.Ok);
            Assert.Contains("AppData", created.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SuggestShareableFolder_IsNotTheLocalAppDataRoot()
        {
            string suggested = SharedDataRootService.SuggestShareableFolder();
            Assert.False(string.IsNullOrWhiteSpace(suggested));
            Assert.NotEqual(
                Path.GetFullPath(AppDataRoot.ProductionFolder),
                Path.GetFullPath(suggested),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}

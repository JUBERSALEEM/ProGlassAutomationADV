// Pins the LAN/Wi-Fi host-folder behavior. A folder create must be
// separated from dataset preparation, return the host UNC path after SMB
// publication, and never copy or merge data as a side effect.

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

        [Fact]
        public void BuildHostSharePath_UsesTheSelectedHostName()
        {
            Assert.Equal(@"\\PRIMARY-PC\ProGlassData",
                SharedDataRootService.BuildHostSharePath("PRIMARY-PC"));
        }

        [Fact]
        public void ServerPlaceholder_IsRecognizedAndProbeExplainsHostSetup()
        {
            Assert.True(SharedDataRootService.IsServerPlaceholderPath(
                @"\\SERVER\ProGlassData"));
            Assert.False(SharedDataRootService.IsServerPlaceholderPath(
                @"\\SERVER-01\ProGlassData"));

            var probe = SharedDataRootService.Probe(@"\\SERVER\ProGlassData");

            Assert.False(probe.Ok);
            Assert.Contains("not a real computer name", probe.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Create Share on This PC", probe.Message,
                StringComparison.Ordinal);
        }
    }
}

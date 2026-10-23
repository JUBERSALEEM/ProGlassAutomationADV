using ProGlassAutomation.Services.Database;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public sealed class SharedFolderProbeTests
    {
        [Fact]
        public void Probe_tests_an_existing_folder_without_changing_its_data()
        {
            string folder = Path.Combine(
                Path.GetTempPath(), "ProGlassSharedFolderProbe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string existingData = Path.Combine(folder, "company.db");
            File.WriteAllText(existingData, "existing company dataset");

            try
            {
                var result = SharedDataRootService.Probe(folder);

                Assert.True(result.Ok, result.Message);
                Assert.Equal(1, result.DatabaseFileCount);
                Assert.Equal("existing company dataset", File.ReadAllText(existingData));
                Assert.Single(Directory.GetFiles(folder));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void Probe_does_not_create_a_missing_folder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(), "ProGlassMissingSharedFolder-" + Guid.NewGuid().ToString("N"));

            var result = SharedDataRootService.Probe(folder);

            Assert.False(result.Ok);
            Assert.False(Directory.Exists(folder));
        }

        [Fact]
        public void Probe_rejects_local_AppData_as_a_shared_root()
        {
            var result = SharedDataRootService.Probe(AppDataRoot.ProductionFolder);

            Assert.False(result.Ok);
            Assert.Contains("local ProGlass AppData", result.Message,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

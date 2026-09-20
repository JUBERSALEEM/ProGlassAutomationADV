// Pins JO identity after forensic audit. Source-level — no SDK / SQLite.
// Does not touch PI.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.JobOrder
{
    public sealed class JobOrderIdentityHardeningTests
    {
        [Fact]
        public void DeriveJobOrderId_replaces_PI_prefix()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "JobOrder", "Mapper", "JobOrderMapper.cs"));

            Assert.Contains(
                "return \"JOB-\" + proformaId.Substring(3);", src);
            Assert.Contains("return \"JOB-\" + proformaId;", src);
        }

        [Fact]
        public void Manual_create_only_bumps_JobOrderId_when_derived_id_is_taken()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "JobOrder", "SavedJobOrdersList",
                "SavedJobOrdersListViewModel.Part1.cs"));

            Assert.Contains(
                "if (await _jobs.ExistsByJobOrderIdAsync(snapshot.JobOrderId))",
                src);
            Assert.Contains("SuggestNextIdAsync(snapshot.JobOrderId)", src);
        }

        [Fact]
        public void InsertAsync_retries_unique_JobOrderId_and_syncs_snapshot()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "JobOrder", "Repository", "JobOrderRepository.cs"));

            Assert.Contains("for (int attempt = 0; attempt < 5; attempt++)", src);
            Assert.Contains("SqliteErrorClassifier.IsUnique(ex)", src);
            Assert.Contains("SyncSnapshotJobOrderId(jobOrder)", src);
            Assert.Contains("snap.JobOrderId = jobOrder.JobOrderId;", src);
        }

        [Fact]
        public void Existing_databases_get_unique_JobOrderId_index_via_migration_1003()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Database", "SchemaMigrations",
                "AppSchemaMigrations.cs"));

            Assert.Contains("new(1003, \"Job Order: unique JobOrderId index\"", src);
            Assert.Contains(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_JobOrder_JobOrderId_Unique ON JobOrders(JobOrderId);",
                src);
        }

        [Fact]
        public void List_kpi_uses_JobOrderStatusValues_for_InProduction_split()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "JobOrder", "SavedJobOrdersList",
                "SavedJobOrdersListViewModel.Part1.cs"));

            Assert.Contains("JobOrderStatusValues.IsInProduction(j.StatusRaw)", src);
            Assert.Contains("JobOrderStatusValues.IsFinished(j.StatusRaw)", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "JobOrder", "JobOrderViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

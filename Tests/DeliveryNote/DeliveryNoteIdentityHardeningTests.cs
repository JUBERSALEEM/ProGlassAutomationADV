// Pins DN identity after forensic audit. Source-level — no SDK / SQLite.
// Does not touch PI or JO.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.DeliveryNote
{
    public sealed class DeliveryNoteIdentityHardeningTests
    {
        [Fact]
        public void InsertAsync_retries_unique_DeliveryNoteId_and_syncs_snapshot()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "DeliveryNote", "Repository",
                "DeliveryNoteRepository.Part1.cs"));

            Assert.Contains("for (int attempt = 0; attempt < 5; attempt++)", src);
            Assert.Contains("SqliteErrorClassifier.IsUnique(ex)", src);
            Assert.Contains("ReallocateDeliveryNoteId(entity)", src);
            Assert.Contains("SyncSnapshotDeliveryNoteId(entity)", src);
            Assert.Contains("item.DeliveryNoteId = next", src);
        }

        [Fact]
        public void Master_insert_does_not_reallocate_MDN_ids()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "DeliveryNote", "Repository",
                "DeliveryNoteRepository.Part1.cs"));

            Assert.Contains("if (entity.IsMasterDn)", src);
            Assert.Contains("Master / Dummy tracker ids", src);
        }

        [Fact]
        public void Existing_databases_get_unique_DeliveryNoteId_index_via_migration_1003()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Database", "SchemaMigrations",
                "AppSchemaMigrations.cs"));

            Assert.Contains(
                "new(1003, \"Delivery Note: unique DeliveryNoteId index\"", src);
            Assert.Contains(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_DeliveryNotes_DeliveryNoteId ON DeliveryNotes(DeliveryNoteId);",
                src);
        }

        [Fact]
        public void Dispatch_rechecks_live_confirmed_remaining()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "DeliveryNote", "DeliveryNoteDetail",
                "DeliveryNoteDetailViewModel.Part2.cs"));

            Assert.Contains("GetPreviouslyDeliveredMapAsync", src);
            Assert.Contains("confirmedPreviouslyDelivered: livePrev", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "DeliveryNote", "DeliveryNoteValidationService.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

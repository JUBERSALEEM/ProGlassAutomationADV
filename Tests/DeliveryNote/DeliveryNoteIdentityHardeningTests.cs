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
        public void InsertAsync_serializes_sequence_allocation_and_propagates_the_final_identity()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "DeliveryNote", "Repository",
                "DeliveryNoteRepository.Part1.cs"));

            Assert.Contains("const int maxAttempts = 5", src);
            Assert.Contains("attempt <= maxAttempts", src);
            Assert.Contains("SqliteErrorClassifier.IsUnique(ex)", src);
            Assert.Contains("BeginTransactionAsync(System.Data.IsolationLevel.Serializable)", src);
            Assert.Contains("entity.DeliveryNoteId = BuildDeliveryNoteId(entity.JobOrderId, sequence)", src);
            Assert.Contains("PrepareAllocatedIdentity(entity)", src);
            Assert.Contains("item.DeliveryNoteId = entity.DeliveryNoteId", src);
            Assert.DoesNotContain("ReallocateDeliveryNoteId(entity)", src);
        }

        [Fact]
        public void Master_insert_does_not_reallocate_MDN_ids()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "DeliveryNote", "Repository",
                "DeliveryNoteRepository.Part1.cs"));

            Assert.Contains("if (entity.IsMasterDn)", src);
            Assert.Contains("Master Delivery Notes must be created through CreateMasterDnAsync", src);
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

        [Fact]
        public void Delete_and_void_refresh_master_remaining()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "DeliveryNote", "Repository",
                "DeliveryNoteRepository.Part2.cs"));

            Assert.Contains("Master remaining refresh after delete skipped", src);
            Assert.Contains("Master remaining refresh after void skipped", src);
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

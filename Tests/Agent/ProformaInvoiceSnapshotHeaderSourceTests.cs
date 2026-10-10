using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProformaInvoiceSnapshotHeaderSourceTests
    {
        [Fact]
        public void Every_PI_write_path_canonicalizes_snapshot_header_before_save()
        {
            string source = ReadRepoFile(
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs");

            AssertWritePathContainsBeforeSave(
                source,
                "public async Task<int> InsertAsync(SavedProformaInvoice invoice)",
                "public async Task UpdateAsync(",
                "ProformaInvoiceMapper.SynchronizeSnapshotHeader(invoice)",
                "snapshot is missing a readable header; it cannot be created safely");
            AssertWritePathContainsBeforeSave(
                source,
                "public async Task UpdateAsync(\n            SavedProformaInvoice invoice,\n            int expectedRowVersion)",
                "public async Task<SavedProformaInvoice?> GetByIdAsync",
                "ProformaInvoiceMapper.SynchronizeSnapshotHeader(existing)",
                "snapshot is missing a readable header; the update was not saved");
            AssertWritePathContainsBeforeSave(
                source,
                "public async Task VoidAsync(\n            int id, int expectedRowVersion, string reason)",
                "public async Task<bool> MarkCancelledAsync(",
                "ProformaInvoiceMapper.SynchronizeSnapshotHeader(entity)",
                "snapshot is missing a readable header; the void was not saved");
            AssertWritePathContainsBeforeSave(
                source,
                "public async Task<bool> MarkCancelledAsync(\n            string proformaId, int expectedRowVersion)",
                "private static async Task ClearAttachmentsQuietlyAsync",
                "ProformaInvoiceMapper.SynchronizeSnapshotHeader(entity)",
                "snapshot is missing a readable header; the lifecycle status was not saved");
        }

        [Fact]
        public void PI_mutation_contracts_require_a_non_nullable_expected_version()
        {
            string contract = ReadRepoFile(
                "Services/ProformaInvoice/ProformaInvoiceRepository/IProformaInvoiceRepository.cs");
            string source = ReadRepoFile(
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs");

            Assert.Contains("Task UpdateAsync(SavedProformaInvoice invoice, int expectedRowVersion);", contract);
            Assert.DoesNotContain("Task UpdateAsync(SavedProformaInvoice invoice);", contract);
            Assert.DoesNotContain("int? expectedRowVersion", contract + source);
            Assert.Contains("int expectedRowVersion,", source);
            Assert.Contains("Task DeleteAsync(int id, int expectedRowVersion);", contract);
            Assert.Contains("Task VoidAsync(int id, int expectedRowVersion, string reason);", contract);
            Assert.Contains("Task<bool> MarkCancelledAsync(string proformaId, int expectedRowVersion);", contract);
            Assert.Contains("await UpdateAsync(invoice!, expectedRowVersion);", source);
        }

        [Fact]
        public void Insert_does_not_repair_id_through_the_empty_snapshot_fallback()
        {
            string source = ReadRepoFile(
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs");
            int start = source.IndexOf(
                "public async Task<int> InsertAsync(SavedProformaInvoice invoice)",
                StringComparison.Ordinal);
            int end = source.IndexOf(
                "public async Task UpdateAsync(",
                start, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);

            string insert = source.Substring(start, end - start);
            Assert.DoesNotContain("SyncSnapshotProformaId", insert);
            Assert.DoesNotContain("DeserializeSnapshot", insert);
        }

        private static void AssertWritePathContainsBeforeSave(
            string source,
            string startMarker,
            string endMarker,
            string requiredCall,
            string failureMessage)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start + startMarker.Length,
                StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start,
                $"Could not isolate PI write path between '{startMarker}' and '{endMarker}'.");

            string method = source.Substring(start, end - start);
            int canonicalize = method.IndexOf(requiredCall, StringComparison.Ordinal);
            int save = method.IndexOf("SaveWithRetryAsync", StringComparison.Ordinal);
            Assert.True(canonicalize >= 0 && save > canonicalize,
                $"{startMarker} must canonicalize its snapshot header before saving.");
            Assert.Contains(failureMessage, method);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var project = Path.Combine(directory.FullName, "ProGlassAutomation.csproj");
                if (File.Exists(project))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + AppContext.BaseDirectory);
        }
    }
}

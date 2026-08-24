// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Inquiry/InquiryRepositoryCreateResilienceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Inquiry — regression test for CreateAsync UNIQUE-collision handling
//
// Before the Aug 2026 SQLite hardening, InquiryRepository.CreateAsync
// reused ONE DbContext across every retry attempt. The retry pattern
//   try { SaveChangesAsync } catch UNIQUE { detach; regen number; loop }
// worked in the happy path but fell apart the moment the change tracker
// held any tracked child (an Attachment auto-added by EF's fixup, a
// second Add before the retry, etc.). Users reported the symptom as
// "SQLite error inside Inquiry save" — because EF surfaced a second
// exception ("attempted to update or insert entity with the same key")
// that looked like an unrelated SQLite failure.
//
// This test locks in the new behaviour: on a UNIQUE collision the
// repository transparently regenerates a fresh InquiryNumber and the
// caller receives a saved record with a NEW number, on the same
// database, without the caller ever seeing the exception.
//
// Hermetic:
//   • Points AppDataRoot at Path.GetTempPath() via TestFolderOverride.
//   • Skips OperationGuard / audit / license concerns by using the
//     repository directly (not the workflow service).
//   • Cleans up the temp folder in Dispose.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Inquiry;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public class InquiryRepositoryCreateResilienceTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string? _savedOverride;

        public InquiryRepositoryCreateResilienceTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "pga_inquiry_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            _savedOverride = AppDataRoot.TestFolderOverride;
            AppDataRoot.TestFolderOverride = _tempFolder;
        }

        public void Dispose()
        {
            AppDataRoot.TestFolderOverride = _savedOverride;
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort */ }
        }

        [Fact]
        public async Task CreateAsync_regenerates_number_when_client_supplied_number_collides()
        {
            var repo = new InquiryRepository(new StubLifecycleService());
            await repo.InitializeAsync().ConfigureAwait(false);

            // First record — accept the auto-generated number.
            var first = await repo.CreateAsync(new InquiryRecord
            {
                CustomerName = "Alpha LLC",
                ProjectName = "Alpha tower",
                SalesmanName = "Rep A",
            }).ConfigureAwait(false);
            Assert.False(string.IsNullOrWhiteSpace(first.InquiryNumber));

            // Second record — force the SAME number the first got. The
            // repository must NOT throw; it must regenerate the number
            // and land on a fresh unique value.
            var second = await repo.CreateAsync(new InquiryRecord
            {
                InquiryNumber = first.InquiryNumber, // deliberate collision
                CustomerName = "Beta LLC",
                ProjectName = "Beta plaza",
                SalesmanName = "Rep B",
            }).ConfigureAwait(false);

            Assert.False(string.IsNullOrWhiteSpace(second.InquiryNumber));
            Assert.NotEqual(first.InquiryNumber, second.InquiryNumber);
            Assert.NotEqual(0, second.Id);
        }

        [Fact]
        public async Task CreateAsync_sets_default_timestamps_when_caller_leaves_them_blank()
        {
            var repo = new InquiryRepository(new StubLifecycleService());
            await repo.InitializeAsync().ConfigureAwait(false);

            var saved = await repo.CreateAsync(new InquiryRecord
            {
                CustomerName = "Gamma LLC",
                ProjectName = "Gamma podium",
                SalesmanName = "Rep G",
            }).ConfigureAwait(false);

            Assert.NotEqual(default, saved.CreatedAt);
            Assert.NotEqual(default, saved.UpdatedAt);
            Assert.NotEqual(default, saved.ReceivedAt);
            // Sanity — CreatedAt should equal UpdatedAt for a brand new row.
            Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
        }

        [Fact]
        public async Task CreateAsync_normalises_status_before_persisting()
        {
            var repo = new InquiryRepository(new StubLifecycleService());
            await repo.InitializeAsync().ConfigureAwait(false);

            // Legacy status "On Hold" should be normalised to InquiryStatuses.Hold.
            var saved = await repo.CreateAsync(new InquiryRecord
            {
                CustomerName = "Delta LLC",
                ProjectName = "Delta site",
                SalesmanName = "Rep D",
                Status = "On Hold",
            }).ConfigureAwait(false);

            Assert.Equal(InquiryStatuses.Hold, saved.Status);
        }

        [Fact]
        public async Task Concurrent_creates_do_not_produce_duplicate_numbers()
        {
            // Live regression for the SQLITE_BUSY / UNIQUE collision that
            // used to happen when two threads called CreateAsync at the
            // same instant. With WAL + busy_timeout + the retry loop,
            // both must succeed and land on different numbers.
            var repo = new InquiryRepository(new StubLifecycleService());
            await repo.InitializeAsync().ConfigureAwait(false);

            const int n = 8;
            var tasks = new Task<InquiryRecord>[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(() => repo.CreateAsync(new InquiryRecord
                {
                    CustomerName = "Concurrent " + idx,
                    ProjectName = "Project " + idx,
                    SalesmanName = "Rep " + idx,
                }));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);

            var numbers = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var t in tasks) Assert.True(numbers.Add(t.Result.InquiryNumber),
                "Duplicate inquiry number produced under concurrent load: " + t.Result.InquiryNumber);
            Assert.Equal(n, numbers.Count);
        }

        // ── Test doubles ─────────────────────────────────────────────────

        /// <summary>Does nothing — lifecycle hydration touches sibling DBs
        /// that don't exist in the isolated test folder. Invalidate is
        /// still called by the repository, which is fine.</summary>
        private sealed class StubLifecycleService : IInquiryLifecycleService
        {
            public Task<System.Collections.Generic.IReadOnlyDictionary<int, InquiryLifecycleSnapshot>>
                GetLifecycleAsync(
                    System.Collections.Generic.IReadOnlyCollection<int> inquiryIds,
                    CancellationToken ct = default)
            {
                var empty = new System.Collections.Generic.Dictionary<int, InquiryLifecycleSnapshot>();
                foreach (var id in inquiryIds) empty[id] = InquiryLifecycleSnapshot.Empty(id);
                return Task.FromResult(
                    (System.Collections.Generic.IReadOnlyDictionary<int, InquiryLifecycleSnapshot>)empty);
            }

            public void Invalidate(int inquiryId) { /* no cache */ }
            public void InvalidateAll() { /* no cache */ }
        }
    }
}

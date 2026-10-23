// Hermetic SQLite tests for Order Report optimistic concurrency.
// No WPF or Windows behavior is simulated.

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.OrderReport.Database;
using ProGlassAutomation.Services.OrderReport.Repository;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    [Collection("HermeticDb")]
    public sealed class OrderReportConcurrencyTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string? _savedFolderOverride;
        private readonly bool _savedGuardEnabled;
        private readonly bool? _savedReadOnlyOverride;

        public OrderReportConcurrencyTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "pga_order_report_concurrency_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedFolderOverride = AppDataRoot.TestFolderOverride;
            AppDataRoot.TestFolderOverride = _tempFolder;
            _savedGuardEnabled = OperationGuard.Enabled;
            OperationGuard.Enabled = false;
            _savedReadOnlyOverride = ReadOnlyGate.OverrideReadOnlyForTests;
            ReadOnlyGate.OverrideReadOnlyForTests = false;
        }

        public void Dispose()
        {
            AppDataRoot.TestFolderOverride = _savedFolderOverride;
            OperationGuard.Enabled = _savedGuardEnabled;
            ReadOnlyGate.OverrideReadOnlyForTests = _savedReadOnlyOverride;
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }

        [Fact]
        public async Task Pdf_path_write_versions_the_report_and_rejects_stale_edits_and_deletes()
        {
            var repo = new OrderReportRepository();
            await repo.InitializeAsync();
            var created = await repo.InsertAsync(NewReport("REP-TEST-001"));
            var stale = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(stale);
            Assert.Equal(0, stale!.RowVersion);

            int currentVersion = await repo.UpdatePdfPathAsync(
                created.Id, "reports/REP-TEST-001.pdf", expectedRowVersion: 0);
            Assert.Equal(1, currentVersion);

            await Assert.ThrowsAsync<ConcurrencyConflictException>(
                () => repo.UpdateAsync(stale, stale.RowVersion));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(
                () => repo.UpdatePdfPathAsync(
                    created.Id, "reports/stale.pdf", expectedRowVersion: stale.RowVersion));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(
                () => repo.DeleteAsync(created.Id, stale.RowVersion));

            var current = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(current);
            Assert.Equal(1, current!.RowVersion);
            Assert.Equal("reports/REP-TEST-001.pdf", current.PdfPath);

            var list = await repo.GetAllAsync();
            Assert.Equal(1, Assert.Single(list).RowVersion);
        }

        private static SavedOrderReport NewReport(string number) => new()
        {
            ReportNumber = number,
            Title = "Concurrency test report",
            FullSnapshotJson = "{}",
            FilterJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

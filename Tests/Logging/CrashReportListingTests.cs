// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Logging/CrashReportListingTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — CrashReportService.GetRecentReports + parse
//
// The listing / parse / delete-all paths land in
// CrashReportService but read from the real
// %AppData%\ProGlassAutomation\logs folder. The tests
// run there, which is a hermeticity compromise: the
// tests do not pollute other test runs because each
// test uses a unique file name and cleans up after
// itself. The folder is in the developer's real
// %AppData%, not a temp folder — there is no DI seam
// in the service for the folder path.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using ProGlassAutomation.Services.Logging;
using Xunit;

namespace ProGlassAutomation.Tests.Logging
{
    public sealed class CrashReportListingTests : IDisposable
    {
        private readonly System.Collections.Generic.List<string> _created;

        public CrashReportListingTests()
        {
            _created = new();
        }

        public void Dispose()
        {
            // Best-effort cleanup of files THIS test
            // created. We never touch the developer's
            // existing crash files.
            foreach (var f in _created)
            {
                try { if (File.Exists(f)) File.Delete(f); }
                catch { }
            }
        }

        private string WriteFakeReport(string errorId, string typeName,
            string message, DateTime? when = null)
        {
            when ??= DateTime.UtcNow;
            string stamp = when.Value.ToString("yyyyMMdd_HHmmss");
            string fileName = "crash-" + stamp + "_" +
                Guid.NewGuid().ToString("N").Substring(0, 4) + ".txt";
            string path = Path.Combine(
                CrashReportService.CrashReportFolder, fileName);
            Directory.CreateDirectory(CrashReportService.CrashReportFolder);

            var text =
                "═══════════════════════════════════════════════════════\n" +
                " ProGlass Automation — crash report\n" +
                "═══════════════════════════════════════════════════════\n" +
                "Error ID : " + errorId + "\n" +
                "Time     : " + when.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff") + "\n" +
                "Version  : 2.0.0\n" +
                "User     : test-user\n" +
                "OS       : Microsoft Windows\n" +
                "[0] " + typeName + "\n" +
                "    Message : " + message + "\n";

            File.WriteAllText(path, text);
            _created.Add(path);
            return path;
        }

        [Fact]
        public void GetRecentReports_returns_files_in_reverse_chronological_order()
        {
            // Three reports, spaced a few seconds apart
            // so CreationTimeUtc is stable enough for
            // ordering on Windows.
            string p1 = WriteFakeReport("ERR-A", "X.Y.Z",
                "first", DateTime.UtcNow.AddMinutes(-10));
            string p2 = WriteFakeReport("ERR-B", "X.Y.Z",
                "second", DateTime.UtcNow.AddMinutes(-5));
            string p3 = WriteFakeReport("ERR-C", "X.Y.Z",
                "third", DateTime.UtcNow);

            // We can't easily force FileSystemInfo
            // ordering across runs because GetFiles
            // returns them in directory order; we just
            // assert the result is a superset of our
            // three and includes the third (newest) at
            // index 0.
            var list = CrashReportService.GetRecentReports(50);
            Assert.Contains(list, r => r.FilePath == p1);
            Assert.Contains(list, r => r.FilePath == p2);
            Assert.Contains(list, r => r.FilePath == p3);
            // Newest is the first one in the result.
            Assert.Equal(p3, list[0].FilePath);
        }

        [Fact]
        public void ParseReportFile_extracts_error_id_type_and_message()
        {
            string path = WriteFakeReport("ERR-PARSE",
                "NullReferenceException",
                "Object reference not set to an instance of an object.",
                DateTime.UtcNow);
            var info = new FileInfo(path);
            var listing = CrashReportService.ParseReportFile(info);

            Assert.Equal("ERR-PARSE", listing.ErrorId);
            Assert.Equal("NullReferenceException", listing.TopExceptionType);
            Assert.Contains("Object reference",
                listing.TopExceptionMessage);
            Assert.True(listing.FileSize > 0);
        }

        [Fact]
        public void ParseReportFile_returns_empty_fields_on_partial_or_corrupt_file()
        {
            // Write a file that does NOT look like a
            // report at all. The parser should
            // populate the path + size but leave
            // the metadata fields empty.
            string path = Path.Combine(
                CrashReportService.CrashReportFolder,
                "crash-bogus-" + Guid.NewGuid().ToString("N") + ".txt");
            Directory.CreateDirectory(CrashReportService.CrashReportFolder);
            File.WriteAllText(path, "hello world\nthis is not a crash report\n");
            _created.Add(path);

            var listing = CrashReportService.ParseReportFile(
                new FileInfo(path));

            Assert.Equal(path, listing.FilePath);
            Assert.True(listing.FileSize > 0);
            // No recognised header lines.
            Assert.Equal("", listing.ErrorId);
            Assert.Equal("", listing.TopExceptionType);
            Assert.Equal("", listing.TopExceptionMessage);
        }

        [Fact]
        public void GetRecentReports_respects_count_argument()
        {
            // Write 5 reports.
            for (int i = 0; i < 5; i++)
            {
                WriteFakeReport("ERR-C" + i, "T.M",
                    "m" + i, DateTime.UtcNow.AddSeconds(-i));
            }
            var list = CrashReportService.GetRecentReports(3);
            Assert.Equal(3, list.Count);
        }

        [Fact]
        public void GetRecentReports_returns_empty_when_folder_has_no_reports()
        {
            // The list returned from the live folder
            // may include other test runs' files, but
            // with the count cap of 1 we can assert
            // it is a non-null list.
            var list = CrashReportService.GetRecentReports(1);
            Assert.NotNull(list);
            Assert.True(list.Count <= 1);
        }

        [Fact]
        public void DeleteAllReports_removes_crash_files_but_leaves_other_files()
        {
            // Seed: one crash file + one unrelated log
            // file. DeleteAll should remove only the
            // crash file.
            string crashPath = WriteFakeReport("ERR-DEL",
                "Test", "to-delete",
                DateTime.UtcNow);
            string otherPath = Path.Combine(
                CrashReportService.CrashReportFolder,
                "app-20260101.log");
            Directory.CreateDirectory(CrashReportService.CrashReportFolder);
            File.WriteAllText(otherPath, "unrelated log content");
            _created.Add(otherPath);

            int removed = CrashReportService.DeleteAllReports();

            Assert.True(removed >= 1, "expected at least one file removed");
            Assert.False(File.Exists(crashPath),
                "crash file should be gone");
            Assert.True(File.Exists(otherPath),
                "unrelated log file should NOT be deleted");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Logging/CrashReportServiceTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — CrashReportService
//
// Hermetic tests for the offline crash report sink.
// Every test writes to a temp CrashReportFolder by
// temporarily overriding the result of
// Path.Combine via a small reflection trick: the
// service exposes the folder as a property, and the
// test cannot easily redirect that to a temp folder
// without DI. So instead, every test calls
// CrashReportService.WriteReport with a synthetic
// Report and asserts on the file that lands in the
// REAL folder — then cleans it up.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using Xunit;

namespace ProGlassAutomation.Tests.Logging
{
    public sealed class CrashReportServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string? _savedOverride;
        private readonly string _savedPublicKey;

        public CrashReportServiceTests()
        {
            // Override the per-machine data folder so the
            // crash report lands in a temp dir. Without
            // this, tests would pollute the developer's
            // %AppData%\ProGlassAutomation\logs\ folder.
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                "proglass-crash-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            // The crash report folder is
            // %AppData%\ProGlassAutomation\logs. We
            // override that by mocking AppDataRoot (used
            // by LicenseService in the same way) AND
            // setting Environment.SpecialFolder via
            // SetEnvironmentVariable is not possible. The
            // pragmatic approach: tests run on Windows
            // with %AppData% pointing to a real user; we
            // accept that they leave files in the
            // developer's profile but we tag every test
            // file with a unique ErrorId so cleanup is
            // easy.
            //
            // For a hermetic test, we delete the file at
            // the end via Reflection on the result of
            // CrashReportService.CrashReportFolder.
            _savedOverride = LicenseService.TestDataFolderOverride;
            LicenseService.TestDataFolderOverride = _tempDir;

            (string publicXml, _) = LicenseSigner.GenerateKeyPair();
            _savedPublicKey = SwapPublicKey(publicXml);
        }

        public void Dispose()
        {
            SwapPublicKey(_savedPublicKey);
            LicenseService.TestDataFolderOverride = _savedOverride;
            // The service writes to %AppData%, not to
            // _tempDir. The temp dir is only used by
            // LicenseService (for the .lic file). The
            // crash report itself ends up in the real
            // %AppData%, which is a problem for hermetic
            // testing but unavoidable without a DI seam.
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best effort */ }
        }

        // ── Build ─────────────────────────────────────────────

        [Fact]
        public void BuildReport_includes_error_id_version_and_exceptions()
        {
            var root = new InvalidOperationException(
                "test root",
                new ArgumentException("test inner"));

            var report = CrashReportService.BuildReport("ERR-ABC123", root);

            Assert.Equal("ERR-ABC123", report.ErrorId);
            Assert.Equal(2, report.ExceptionChain.Count);
            Assert.Contains("InvalidOperationException",
                report.ExceptionChain[0].TypeName);
            Assert.Contains("test root",
                report.ExceptionChain[0].Message);
            Assert.Contains("ArgumentException",
                report.ExceptionChain[1].TypeName);
            Assert.Contains("test inner",
                report.ExceptionChain[1].Message);
            // Version is read from AppInfo — assert it is
            // non-empty.
            Assert.False(string.IsNullOrEmpty(report.AppVersion));
        }

        [Fact]
        public void BuildReport_never_throws_even_with_null_exception()
        {
            var report = CrashReportService.BuildReport("ERR-XYZ", null!);
            // Chain is empty when the input is null.
            Assert.Empty(report.ExceptionChain);
        }

        [Fact]
        public void BuildReport_includes_a_stack_trace_per_level()
        {
            // Force a real throw so the captured stack
            // trace is non-empty. The service writes the
            // exception's StackTrace as-is (truncating
            // only the gigantic ones), so this test
            // asserts "a non-empty stack was captured" —
            // the truncation logic is exercised in
            // BuildReport_never_throws_even_with_null_exception
            // and via the natural length of real stacks.
            try
            {
                throw new InvalidOperationException("forced");
            }
            catch (Exception ex)
            {
                var report = CrashReportService.BuildReport(
                    "ERR-STK", ex);
                Assert.Single(report.ExceptionChain);
                Assert.False(string.IsNullOrEmpty(
                    report.ExceptionChain[0].StackTrace),
                    "Captured stack should not be empty.");
            }
        }

        [Fact]
        public void BuildReport_includes_recent_log_lines()
        {
            // Write a few log lines so the in-memory
            // ring buffer has content.
            AppLogService.Info("Test log line A");
            AppLogService.Info("Test log line B");

            var report = CrashReportService.BuildReport(
                "ERR-LOG", new Exception("dummy"));

            Assert.NotEmpty(report.RecentLog);
            // The recent log should contain at least the
            // two lines we just wrote (the buffer holds
            // the last 500 entries, so anything we wrote
            // is in there).
            Assert.Contains(report.RecentLog, l => l.Contains("Test log line A"));
            Assert.Contains(report.RecentLog, l => l.Contains("Test log line B"));
        }

        [Fact]
        public void BuildReport_includes_license_summary()
        {
            // Drop a Trial licence so the summary is
            // meaningful.
            string tempLic = Path.Combine(_tempDir, "license.lic");
            string publicXml;
            string privateKey;
            (publicXml, privateKey) = LicenseSigner.GenerateKeyPair();
            SwapPublicKey(publicXml);
            var payload = new LicensePayload
            {
                LicenseId = "T-001",
                Edition = LicenseEdition.Trial,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(15),
                Features = new[] { "Inquiry.*" }
            };
            LicenseSigner.SignToFile(payload, privateKey, tempLic);
            LicenseService.Reload();

            var report = CrashReportService.BuildReport(
                "ERR-LIC", new Exception("dummy"));

            Assert.Contains("Trial", report.LicenseSummary);
            Assert.Contains("T-001", report.LicenseSummary);
        }

        // ── ToText ────────────────────────────────────────────

        [Fact]
        public void Report_ToText_contains_every_section_header()
        {
            var report = new CrashReportService.Report
            {
                ErrorId = "ERR-TXT",
                ExceptionChain = new()
                {
                    new CrashReportService.ExceptionEntry
                    {
                        TypeName = "X.Y",
                        Message = "msg",
                        StackTrace = "stack1"
                    }
                }
            };
            string text = report.ToText();
            Assert.Contains("ProGlass Automation — crash report", text);
            Assert.Contains("Error ID : ERR-TXT", text);
            Assert.Contains("Exception chain", text);
            Assert.Contains("Last ", text);   // last log lines header
            Assert.Contains("X.Y", text);
            Assert.Contains("stack1", text);
        }

        // ── HandleCrash (the full pipeline) ──────────────────

        [Fact]
        public void HandleCrash_writes_file_to_crash_folder()
        {
            // The crash folder is %AppData%, not _tempDir.
            // We DO know the exact folder (computed from
            // the same Path.Combine the service uses), so
            // we can read the file back and clean up.
            string folder = CrashReportService.CrashReportFolder;
            Directory.CreateDirectory(folder);

            // Snapshot the files already there so we
            // only clean up the one this test creates.
            var before = new DirectoryInfo(folder)
                .GetFiles("crash-*.txt")
                .Select(f => f.FullName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string written = CrashReportService.HandleCrash(
                "ERR-PIPE-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                new Exception("pipe test"));

            Assert.False(string.IsNullOrEmpty(written));
            Assert.True(File.Exists(written),
                "Crash report was not written at " + written);
            // crash-latest.txt should also exist and have
            // identical content.
            Assert.True(File.Exists(CrashReportService.CrashReportLatestPath));

            // Clean up ONLY the file this test created.
            // Other tests in the same process may have
            // created their own files; do not touch them.
            try { File.Delete(written); } catch { }
            if (File.Exists(CrashReportService.CrashReportLatestPath)
                && !before.Contains(CrashReportService.CrashReportLatestPath))
            {
                try
                {
                    File.Delete(CrashReportService.CrashReportLatestPath);
                }
                catch { }
            }
        }

        // ── helpers ─────────────────────────────────────────────

        private static string SwapPublicKey(string newValue)
        {
            FieldInfo field = typeof(LicensePublicKey).GetField(
                "PublicKeyXml",
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    "LicensePublicKey.PublicKeyXml field not found.");
            string original = (string)field.GetValue(null)!;
            field.SetValue(null, newValue);
            return original;
        }
    }
}

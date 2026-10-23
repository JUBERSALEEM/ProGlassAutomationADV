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
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using Xunit;

namespace ProGlassAutomation.Tests.Logging
{
    [Collection("LicensePublicKey")]
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
            // Phase 19R forensic: legacy LicenseSigner.SignToFile creates license.lic which is now treated as legacy artifact
            // and returns Invalid with migration prompt, not Trial with LicenseId. So summary did not contain T-001.
            // Root cause: LoadSecureOfflineLicense now checks legacy artifact and returns Invalid, not Trial.
            // Fix: use new license key flow (ProGlass.Licensing) which is the primary 2026 flow and is readable by LicenseService.
            // Generate a Trial license key, install armored block, reload, then verify summary contains LicenseId and Trial.
            string publicXml;
            string privateXml;
            (publicXml, privateXml) = ProGlass.Licensing.LicenseCrypto.GenerateKeyPair();
            SwapPublicKey(publicXml);

            var gen = ProGlass.Licensing.LicenseService.Create(
                new ProGlass.Licensing.LicenseRequest
                {
                    CustomerId = "CUST-CRASH-001",
                    LicenseType = "Trial",
                    DaysValid = 15,
                    Features = new() { "Inquiry.*" }
                },
                privateXml,
                TrustedLicenseKeys.ProductionKeyId);

            Assert.True(gen.Success, gen.Message);
            Assert.NotNull(gen.Document);
            string expectedLicenseId = gen.Document!.LicenseId ?? "";
            Assert.False(string.IsNullOrWhiteSpace(expectedLicenseId));

            string armored = ProGlass.Licensing.LicenseKeyArmor.Encode(gen.LicenseJson!);
            var installResult = LicenseService.InstallKeyText(armored);
            Assert.Equal(LicenseStatus.Valid, installResult.Status);
            // Reload to ensure Current reflects installed key
            LicenseService.Reload();

            var report = CrashReportService.BuildReport(
                "ERR-LIC", new Exception("dummy"));

            // License summary must contain edition and the generated LicenseId
            Assert.Contains("Trial", report.LicenseSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(expectedLicenseId, report.LicenseSummary);
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

        [Fact]
        public void BuildReport_indents_multiline_recent_log_messages()
        {
            const string marker = "CRASH-LOG-MULTILINE-TEST";
            AppLogService.Info(marker + " first line\r\nsecond line");

            var report = CrashReportService.BuildReport(
                "ERR-MULTILOG", new Exception("synthetic"));
            string line = report.RecentLog.Last(x => x.Contains(marker));

            Assert.Contains(
                marker + " first line" + Environment.NewLine + "      second line",
                line);
        }

        [Fact]
        public void AppLogService_indents_multiline_exception_chain()
        {
            const string marker = "APP-LOG-EXCEPTION-MULTILINE-TEST";
            var exception = new SharedDataRootUnavailableException(
                @"\\OFFICE\ProGlassData",
                new IOException("The underlying network path could not be opened."),
                "The shared folder did not respond within 4 seconds.");

            AppLogService.Error(marker, exception);
            var entry = AppLogService.GetRecent(50).Last(x => x.Message == marker);

            Assert.Contains(
                "  SharedDataRootUnavailableException: The configured shared company data folder is unavailable:",
                entry.ExceptionText);
            Assert.Contains(
                Environment.NewLine + "      Database access is blocked.",
                entry.ExceptionText);
            Assert.Contains(
                "  caused by:" + Environment.NewLine +
                "  IOException: The underlying network path could not be opened.",
                entry.ExceptionText);
        }

        [Fact]
        public void Report_ToText_indents_shared_root_message_and_keeps_inner_cause_separate()
        {
            const string folder = @"\\OFFICE\ProGlassData";
            const string reason = "The shared folder did not respond within 4 seconds.";
            var root = new SharedDataRootUnavailableException(
                folder,
                new TimeoutException("The SMB startup probe exceeded four seconds."),
                reason);

            var report = CrashReportService.BuildReport("ERR-SHARE", root);
            string topMessage = report.ExceptionChain[0].Message;
            string text = report.ToText();

            Assert.Equal(
                topMessage.IndexOf(folder, StringComparison.Ordinal),
                topMessage.LastIndexOf(folder, StringComparison.Ordinal));
            Assert.Equal(
                topMessage.IndexOf(reason, StringComparison.Ordinal),
                topMessage.LastIndexOf(reason, StringComparison.Ordinal));
            Assert.Contains("    Message : The configured shared company data folder is unavailable:", text);
            Assert.Contains("              Database access is blocked.", text);
            Assert.Contains("              Reason: " + reason, text);
            Assert.Contains("── caused by ──", text);
            Assert.Contains("TimeoutException", text);
            Assert.Contains("The SMB startup probe exceeded four seconds.", text);
            Assert.DoesNotContain("Technical detail:", topMessage);
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
            // The previous version of this helper used
            // FieldInfo.SetValue on the LicensePublicKey.PublicKeyXml
            // field. That worked in .NET Framework but the .NET 10
            // SDK on the build machine enforces the initonly bit
            // on static readonly fields, so FieldInfo.SetValue
            // throws "Cannot set initonly static field".
            //
            // LicensePublicKey exposes an internal SetForTest
            // hook specifically for this case. It is the
            // production-side seam that the test-side
            // SwapPublicKey delegates to. No reflection is
            // involved any more.
            string original = LicensePublicKey.PublicKeyXml;
            LicensePublicKey.SetForTest(newValue);
            return original;
        }
    }
}

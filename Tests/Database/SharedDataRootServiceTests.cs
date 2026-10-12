// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/SharedDataRootServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Database — Company LAN/Wi-Fi shared data root (senior audit — 2026-09-24)
//
// Hermetic tests for the shared data root fixes:
//   1. TryNormalizeSharedRoot rejects the local AppData roots (a "shared"
//      folder pointing at the local database is a data-loss mistake).
//   2. Probe verifies a real folder and rejects the local AppData folder.
//   3. LooksNetworkBased recognises UNC paths (and never a plain local
//      drive).
//   4. ApplyToConfig REFUSES to persist a half-configured shared mode —
//      enabled=true with an empty or local path must not be saved. Runtime
//      offline fallback is visible, but an invalid saved target is still
//      rejected rather than being accepted as a working shared configuration.
//   5. SharedDataRootUnavailableException carries the operator-facing
//      message with the folder path and the inner exception.
//
// No test touches %AppData% or opens a real database file.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Database;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    [Collection("HermeticDb")]
    public sealed class SharedDataRootServiceTests : IDisposable
    {
        private readonly string _tempRoot;

        public SharedDataRootServiceTests()
        {
            _tempRoot = Path.Combine(
                Path.GetTempPath(),
                "proglass_shared_root_tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        private string MakeFolder(string name)
        {
            var path = Path.Combine(_tempRoot, name);
            Directory.CreateDirectory(path);
            return path;
        }

        // ── TryNormalizeSharedRoot ───────────────────────────────────

        [Fact]
        public void Normalize_rejects_the_local_production_folder()
        {
            Assert.False(AppDataRoot.TryNormalizeSharedRoot(
                AppDataRoot.ProductionFolder, out var normalized));
            Assert.Equal("", normalized);
        }

        [Fact]
        public void Normalize_rejects_the_local_demo_folder()
        {
            Assert.False(AppDataRoot.TryNormalizeSharedRoot(
                AppDataRoot.DemoFolder, out var normalized));
            Assert.Equal("", normalized);
        }

        [Fact]
        public void Normalize_rejects_blank_and_null()
        {
            Assert.False(AppDataRoot.TryNormalizeSharedRoot("", out _));
            Assert.False(AppDataRoot.TryNormalizeSharedRoot("   ", out _));
            Assert.False(AppDataRoot.TryNormalizeSharedRoot(null, out _));
        }

        [Fact]
        public void Normalize_accepts_a_distinct_folder_and_trims_trailing_separators()
        {
            var folder = MakeFolder("share").TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            Assert.True(AppDataRoot.TryNormalizeSharedRoot(folder, out var normalized));
            Assert.False(normalized.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal));
        }

        [Fact]
        public void Normalize_preserves_a_volume_root_separator()
        {
            if (!OperatingSystem.IsWindows()) return;

            Assert.True(AppDataRoot.TryNormalizeSharedRoot(@"Z:\", out var normalized));
            Assert.Equal(@"Z:\", normalized);
        }

        // ── LooksNetworkBased ────────────────────────────────────────

        [Fact]
        public void LooksNetworkBased_detects_unc_paths()
        {
            Assert.True(SharedDataRootService.LooksNetworkBased(@"\\OFFICE-PC\ProGlassData"));
            Assert.True(SharedDataRootService.LooksNetworkBased(@"\\192.168.1.50\share"));
        }

        [Fact]
        public void LooksNetworkBased_rejects_plain_local_paths()
        {
            Assert.False(SharedDataRootService.LooksNetworkBased(_tempRoot));
            Assert.False(SharedDataRootService.LooksNetworkBased("C:\\"));
            Assert.False(SharedDataRootService.LooksNetworkBased(""));
        }

        // ── Probe ────────────────────────────────────────────────────

        [Fact]
        public void Probe_accepts_a_real_writable_folder()
        {
            var result = SharedDataRootService.Probe(MakeFolder("probe-ok"));

            Assert.True(result.Ok);
            Assert.Contains("✓", result.Message);
            Assert.False(result.LooksLikeNetworkPath);
        }

        [Fact]
        public void Probe_reports_no_databases_in_a_fresh_folder()
        {
            var result = SharedDataRootService.Probe(MakeFolder("probe-empty"));

            Assert.True(result.Ok);
            Assert.Contains("No database files found yet", result.Message);
        }

        [Fact]
        public void Probe_rejects_the_local_AppData_production_folder()
        {
            var result = SharedDataRootService.Probe(AppDataRoot.ProductionFolder);

            Assert.False(result.Ok);
            Assert.Contains("different from the local ProGlass AppData folder", result.Message);
        }

        [Fact]
        public void Probe_rejects_a_blank_path()
        {
            var result = SharedDataRootService.Probe("");

            Assert.False(result.Ok);
        }

        [Fact]
        public void StartupProbe_verifies_existing_folder_without_creating_a_missing_one()
        {
            var missing = Path.Combine(_tempRoot, "not-created-by-startup");

            Assert.False(AppDataRoot.TryVerifySharedFolder(
                missing, out var failure, out var cause));
            Assert.False(Directory.Exists(missing));
            Assert.False(string.IsNullOrWhiteSpace(failure));
            Assert.NotNull(cause);
        }

        [Fact]
        public void StartupProbe_requires_read_write_and_delete_access_to_existing_folder()
        {
            var folder = MakeFolder("startup-probe");

            Assert.True(AppDataRoot.TryVerifySharedFolder(
                folder, out var failure, out var cause), failure);
            Assert.Null(cause);
            Assert.Empty(Directory.GetFiles(folder));
        }

        // ── ApplyToConfig — the half-configured-state ratchet ────────

        [Fact]
        public void Apply_refuses_to_enable_with_an_empty_path()
        {
            var config = new AppConfig();

            var (ok, message) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: "", copyLocalData: true);

            Assert.False(ok);
            Assert.Contains("valid shared folder path", message, StringComparison.OrdinalIgnoreCase);
            Assert.False(config.DataConnectivity!.UseSharedDataRoot);
        }

        [Fact]
        public void Apply_refuses_to_enable_with_the_local_AppData_folder()
        {
            var config = new AppConfig();

            var (ok, _) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: AppDataRoot.ProductionFolder, copyLocalData: true);

            Assert.False(ok);
            Assert.False(config.DataConnectivity!.UseSharedDataRoot);
        }

        [Fact]
        public void Apply_persists_the_path_when_enabling()
        {
            var config = new AppConfig();
            config.DataConnectivity!.SharedRootOfflineFallbackNeedsReview = true;
            config.DataConnectivity.SharedRootLocalModeNoticePending = true;
            config.DataConnectivity.SharedRootReconnectNoticeShown = true;
            var folder = MakeFolder("apply-on");

            var (ok, _) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: folder, copyLocalData: true);

            Assert.True(ok);
            Assert.True(config.DataConnectivity.UseSharedDataRoot);
            Assert.False(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
            Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);
            Assert.False(config.DataConnectivity.SharedRootReconnectNoticeShown);
            Assert.Equal(folder, config.DataConnectivity.SharedDataRootPath);
            Assert.False(string.IsNullOrEmpty(config.DataConnectivity.WorkstationName));
        }

        [Fact]
        public void Apply_clears_the_path_when_disabling()
        {
            var config = new AppConfig();
            config.DataConnectivity!.SharedRootOfflineFallbackNeedsReview = true;
            config.DataConnectivity.SharedRootLocalModeNoticePending = true;
            config.DataConnectivity.SharedRootReconnectNoticeShown = true;
            var folder = MakeFolder("apply-off");

            var on = SharedDataRootService.ApplyToConfig(config, true, folder, true);
            Assert.True(on.Ok);
            Assert.False(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
            Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);
            Assert.False(config.DataConnectivity.SharedRootReconnectNoticeShown);

            var off = SharedDataRootService.ApplyToConfig(config, false, folder, true);
            Assert.True(off.Ok);
            Assert.False(config.DataConnectivity!.UseSharedDataRoot);
            Assert.False(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
            Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);
            Assert.False(config.DataConnectivity.SharedRootReconnectNoticeShown);
            Assert.Equal("", config.DataConnectivity.SharedDataRootPath);
        }

        // ── Startup uses visible local mode if shared root is unavailable ──

        [Fact]
        public void Enabled_shared_mode_with_invalid_path_selects_local_and_marks_review()
        {
            var previousOverride = AppDataRoot.TestFolderOverride;
            try
            {
                AppDataRoot.TestFolderOverride = null;
                var config = new AppConfig();
                config.DataConnectivity!.UseSharedDataRoot = true;
                config.DataConnectivity.SharedDataRootPath = "";
                AppDataRoot.ConfigureFrom(config);

                Assert.True(AppDataRoot.SharedRootFallbackActive);
                Assert.False(AppDataRoot.IsSharedDataRootEnabled);
                Assert.Equal(AppDataRoot.ProductionFolder, AppDataRoot.CurrentFolder);
                Assert.True(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
                Assert.True(config.DataConnectivity.SharedRootLocalModeNoticePending);
                Assert.Contains("local database", AppDataRoot.SharedRootFallbackReason,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains("will not be shared", AppDataRoot.SharedRootFallbackReason,
                    StringComparison.OrdinalIgnoreCase);

                // After the one-time notice is acknowledged, repeated offline
                // launches must remain visible in Settings/logs without
                // forcing the same modal on every launch.
                config.DataConnectivity.SharedRootLocalModeNoticePending = false;
                AppDataRoot.ConfigureFrom(config);
                Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);

                // Exercise the local-root ensure path without touching the
                // test runner user's real %AppData% database directory.
                var localOverride = Path.Combine(_tempRoot, "offline-local");
                AppDataRoot.TestFolderOverride = localOverride;
                AppDataRoot.EnsureCurrentFolder();
                Assert.True(Directory.Exists(localOverride));
            }
            finally
            {
                AppDataRoot.TestFolderOverride = previousOverride;
                AppDataRoot.ConfigureFrom(new AppConfig());
            }
        }

        [Fact]
        public void Recovered_share_stays_local_until_offline_data_is_reviewed()
        {
            var previousOverride = AppDataRoot.TestFolderOverride;
            try
            {
                AppDataRoot.TestFolderOverride = null;
                var config = new AppConfig();
                config.DataConnectivity!.UseSharedDataRoot = true;
                config.DataConnectivity.SharedDataRootPath = MakeFolder("recovered-share");
                config.DataConnectivity.SharedRootOfflineFallbackNeedsReview = true;

                AppDataRoot.ConfigureFrom(config);

                Assert.True(AppDataRoot.SharedRootFallbackActive);
                Assert.True(AppDataRoot.SharedRootReconnectAvailable);
                Assert.True(config.DataConnectivity.SharedRootLocalModeNoticePending);
                Assert.True(config.DataConnectivity.SharedRootReconnectNoticeShown);
                Assert.False(AppDataRoot.IsSharedDataRootEnabled);
                Assert.Equal(AppDataRoot.ProductionFolder, AppDataRoot.CurrentFolder);
                Assert.Contains("reachable again", AppDataRoot.SharedRootFallbackReason,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains("not merged automatically", AppDataRoot.SharedRootFallbackReason,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                AppDataRoot.TestFolderOverride = previousOverride;
                AppDataRoot.ConfigureFrom(new AppConfig());
            }
        }

        [Fact]
        public void Reachable_share_is_selected_when_no_offline_review_is_pending()
        {
            var previousOverride = AppDataRoot.TestFolderOverride;
            try
            {
                AppDataRoot.TestFolderOverride = null;
                var folder = MakeFolder("reachable-share");
                var config = new AppConfig();
                config.DataConnectivity!.UseSharedDataRoot = true;
                config.DataConnectivity.SharedDataRootPath = folder;

                AppDataRoot.ConfigureFrom(config);

                Assert.True(AppDataRoot.IsSharedDataRootEnabled);
                Assert.False(AppDataRoot.SharedRootFallbackActive);
                Assert.False(AppDataRoot.SharedRootReconnectAvailable);
                Assert.Equal(folder, AppDataRoot.CurrentFolder);
            }
            finally
            {
                AppDataRoot.TestFolderOverride = previousOverride;
                AppDataRoot.ConfigureFrom(new AppConfig());
            }
        }

        // ── SharedDataRootUnavailableException ───────────────────────

        [Fact]
        public void UnavailableException_carries_the_operator_message()
        {
            var inner = new IOException("The network path was not found.");
            var ex = new SharedDataRootUnavailableException(@"\\OFFICE\ProGlassData", inner);

            Assert.Equal(@"\\OFFICE\ProGlassData", ex.SharedFolder);
            Assert.Contains(@"\\OFFICE\ProGlassData", ex.Message);
            Assert.Contains("same network", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void UnavailableException_does_not_duplicate_path_or_failure_reason()
        {
            const string folder = @"\\OFFICE\ProGlassData";
            const string reason = "The shared folder did not respond within 4 seconds.";
            var inner = new TimeoutException("The startup probe exceeded four seconds.");
            var ex = new SharedDataRootUnavailableException(folder, inner, reason);

            Assert.Contains(folder, ex.Message);
            Assert.Contains(reason, ex.Message);
            Assert.Equal(reason, ex.FailureReason);
            Assert.Equal(
                ex.Message.IndexOf(folder, StringComparison.Ordinal),
                ex.Message.LastIndexOf(folder, StringComparison.Ordinal));
            Assert.Equal(
                ex.Message.IndexOf(reason, StringComparison.Ordinal),
                ex.Message.LastIndexOf(reason, StringComparison.Ordinal));
            Assert.DoesNotContain("Technical detail:", ex.Message);
            Assert.DoesNotContain(inner.Message, ex.Message);
            Assert.Same(inner, ex.InnerException);
        }
    }
}

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
            Assert.False(normalized.EndsWith(
                Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal));
        }

        [Fact]
        public void Normalize_preserves_a_volume_root_separator()
        {
            if (!OperatingSystem.IsWindows()) return;

            Assert.True(AppDataRoot.TryNormalizeSharedRoot(@"Z:\", out var normalized));
            Assert.Equal(@"Z:\", normalized);
        }

        [Fact]
        public void LooksNetworkBased_detects_unc_paths()
        {
            Assert.True(SharedDataRootService.LooksNetworkBased(
                @"\\OFFICE-PC\ProGlassData"));
            Assert.True(SharedDataRootService.LooksNetworkBased(
                @"\\192.168.1.50\ProGlassData"));
        }

        [Fact]
        public void LooksNetworkBased_rejects_plain_local_paths()
        {
            Assert.False(SharedDataRootService.LooksNetworkBased(_tempRoot));
            Assert.False(SharedDataRootService.LooksNetworkBased("C:\\"));
            Assert.False(SharedDataRootService.LooksNetworkBased(""));
        }

        [Fact]
        public void Probe_accepts_a_writable_folder_but_does_not_treat_it_as_a_network_path()
        {
            var folder = MakeFolder("probe-ok");
            var result = SharedDataRootService.Probe(folder);

            Assert.True(result.Ok, result.Message);
            Assert.Contains("✓", result.Message);
            Assert.False(result.LooksLikeNetworkPath);
            Assert.Contains("local path", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetFiles(folder));
        }

        [Fact]
        public void Probe_counts_existing_database_files_without_modifying_them()
        {
            var folder = MakeFolder("probe-existing-data");
            string database = Path.Combine(folder, "Customers.db");
            File.WriteAllText(database, "existing dataset marker");

            var result = SharedDataRootService.Probe(folder);

            Assert.True(result.Ok, result.Message);
            Assert.Equal(1, result.DatabaseFileCount);
            Assert.Contains("1 top-level ProGlass database file", result.Message);
            Assert.Equal("existing dataset marker", File.ReadAllText(database));
            Assert.Single(Directory.GetFiles(folder));
        }

        [Fact]
        public void Probe_flags_an_empty_folder_for_operator_review()
        {
            var result = SharedDataRootService.Probe(MakeFolder("probe-empty"));

            Assert.True(result.Ok, result.Message);
            Assert.Equal(0, result.DatabaseFileCount);
            Assert.Contains("No top-level ProGlass database files were found", result.Message);
        }

        [Fact]
        public void Probe_does_not_create_a_missing_folder()
        {
            var missing = Path.Combine(_tempRoot, "must-not-be-created");

            var result = SharedDataRootService.Probe(missing);

            Assert.False(result.Ok);
            Assert.False(Directory.Exists(missing));
        }

        [Fact]
        public void Probe_rejects_the_local_AppData_production_folder()
        {
            var result = SharedDataRootService.Probe(AppDataRoot.ProductionFolder);

            Assert.False(result.Ok);
            Assert.Contains("other than the local ProGlass AppData folder", result.Message);
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

        [Fact]
        public void Apply_refuses_to_enable_with_an_empty_path()
        {
            var config = new AppConfig();

            var (ok, message) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: "");

            Assert.False(ok);
            Assert.Contains("valid shared folder path", message, StringComparison.OrdinalIgnoreCase);
            Assert.False(config.DataConnectivity!.UseSharedDataRoot);
        }

        [Fact]
        public void Apply_refuses_to_enable_with_a_local_path()
        {
            var config = new AppConfig();

            var (ok, message) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: MakeFolder("apply-local"));

            Assert.False(ok);
            Assert.Contains("path is local", message, StringComparison.OrdinalIgnoreCase);
            Assert.False(config.DataConnectivity!.UseSharedDataRoot);
        }

        [Fact]
        public void Apply_persists_a_network_path_and_clears_review_flags()
        {
            if (!OperatingSystem.IsWindows()) return;

            var config = new AppConfig();
            config.DataConnectivity!.SharedRootOfflineFallbackNeedsReview = true;
            config.DataConnectivity.SharedRootLocalModeNoticePending = true;
            config.DataConnectivity.SharedRootReconnectNoticeShown = true;
            const string path = @"\\OFFICE-PC\ProGlassData";
            Assert.True(AppDataRoot.TryNormalizeSharedRoot(path, out var normalizedPath));

            var (ok, message) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: path);

            Assert.True(ok, message);
            Assert.True(config.DataConnectivity.UseSharedDataRoot);
            Assert.False(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
            Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);
            Assert.False(config.DataConnectivity.SharedRootReconnectNoticeShown);
            Assert.Equal(normalizedPath, config.DataConnectivity.SharedDataRootPath);
            Assert.False(string.IsNullOrEmpty(config.DataConnectivity.WorkstationName));
            Assert.Contains("No data was copied or merged", message);
        }

        [Fact]
        public void Apply_clears_the_shared_path_when_disabling()
        {
            var config = new AppConfig();
            config.DataConnectivity!.SharedRootOfflineFallbackNeedsReview = true;
            config.DataConnectivity.SharedRootLocalModeNoticePending = true;
            config.DataConnectivity.SharedRootReconnectNoticeShown = true;

            var (ok, message) = SharedDataRootService.ApplyToConfig(
                config, enabled: false, path: "");

            Assert.True(ok, message);
            Assert.False(config.DataConnectivity.UseSharedDataRoot);
            Assert.False(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
            Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);
            Assert.False(config.DataConnectivity.SharedRootReconnectNoticeShown);
            Assert.Equal("", config.DataConnectivity.SharedDataRootPath);
        }

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

                config.DataConnectivity.SharedRootLocalModeNoticePending = false;
                AppDataRoot.ConfigureFrom(config);
                Assert.False(config.DataConnectivity.SharedRootLocalModeNoticePending);

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
        public void Saved_local_path_is_not_accepted_as_a_shared_root_at_startup()
        {
            var previousOverride = AppDataRoot.TestFolderOverride;
            try
            {
                AppDataRoot.TestFolderOverride = null;
                var config = new AppConfig();
                config.DataConnectivity!.UseSharedDataRoot = true;
                config.DataConnectivity.SharedDataRootPath = MakeFolder("not-a-network-share");

                AppDataRoot.ConfigureFrom(config);

                Assert.True(AppDataRoot.SharedRootFallbackActive);
                Assert.False(AppDataRoot.IsSharedDataRootEnabled);
                Assert.True(config.DataConnectivity.SharedRootOfflineFallbackNeedsReview);
                Assert.Contains("not a network location", AppDataRoot.SharedRootFallbackReason,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("not a network location", AppDataRoot.CurrentFolder,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                AppDataRoot.TestFolderOverride = previousOverride;
                AppDataRoot.ConfigureFrom(new AppConfig());
            }
        }

        [Fact]
        public void UnavailableException_carries_the_operator_message()
        {
            var inner = new IOException("The network path was not found.");
            var ex = new SharedDataRootUnavailableException(
                @"\\OFFICE\ProGlassData", inner);

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

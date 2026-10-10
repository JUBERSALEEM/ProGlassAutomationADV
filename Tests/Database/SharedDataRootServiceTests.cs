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
//      enabled=true with an empty or local path must not be saved, because
//      the next startup would silently fall back to local AppData while
//      Settings still claimed "shared" (the split-brain bug this audit
//      exists to keep dead).
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
            var folder = MakeFolder("apply-on");

            var (ok, _) = SharedDataRootService.ApplyToConfig(
                config, enabled: true, path: folder, copyLocalData: true);

            Assert.True(ok);
            Assert.True(config.DataConnectivity!.UseSharedDataRoot);
            Assert.Equal(folder, config.DataConnectivity.SharedDataRootPath);
            Assert.False(string.IsNullOrEmpty(config.DataConnectivity.WorkstationName));
        }

        [Fact]
        public void Apply_clears_the_path_when_disabling()
        {
            var config = new AppConfig();
            var folder = MakeFolder("apply-off");

            var on = SharedDataRootService.ApplyToConfig(config, true, folder, true);
            Assert.True(on.Ok);

            var off = SharedDataRootService.ApplyToConfig(config, false, folder, true);
            Assert.True(off.Ok);
            Assert.False(config.DataConnectivity!.UseSharedDataRoot);
            Assert.Equal("", config.DataConnectivity.SharedDataRootPath);
        }

        // ── Startup remains fail-closed ──────────────────────────────

        [Fact]
        public void Enabled_shared_mode_with_invalid_path_never_selects_local_AppData()
        {
            try
            {
                var config = new AppConfig();
                config.DataConnectivity!.UseSharedDataRoot = true;
                config.DataConnectivity.SharedDataRootPath = "";
                AppDataRoot.ConfigureFrom(config);

                Assert.True(AppDataRoot.SharedRootFallbackActive);
                Assert.NotEqual(AppDataRoot.ProductionFolder, AppDataRoot.CurrentFolder);
                Assert.Contains("will not open local AppData", AppDataRoot.SharedRootFallbackReason,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Throws<SharedDataRootUnavailableException>(
                    AppDataRoot.EnsureCurrentFolder);
            }
            finally
            {
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
    }
}

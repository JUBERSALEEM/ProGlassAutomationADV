// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/LocalModelCatalogTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Local LLM program (2026-09-04)
//
// Pure catalog/safety invariants — NO NETWORK, NO FILES. These pin the
// promises the installer makes to the user (HTTPS-only URLs, GGUF files,
// permissive licenses, unique ids, sane sizes).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.LocalAi;
using System;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class LocalModelCatalogTests
    {
        [Fact]
        public void Catalog_is_nonempty_and_ids_are_unique()
        {
            Assert.NotEmpty(LocalModelCatalog.All);
            Assert.Equal(
                LocalModelCatalog.All.Length,
                LocalModelCatalog.All
                    .Select(e => e.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());
        }

        [Fact]
        public void Every_download_url_is_https_and_gguf()
        {
            foreach (var e in LocalModelCatalog.All)
            {
                Assert.StartsWith("https://", e.DownloadUrl, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith(".gguf", e.DownloadUrl, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("huggingface.co", e.DownloadUrl);
            }
        }

        [Fact]
        public void Every_license_is_permissive_and_linked()
        {
            foreach (var e in LocalModelCatalog.All)
            {
                Assert.True(
                    string.Equals(e.LicenseName, "Apache-2.0", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.LicenseName, "MIT", StringComparison.OrdinalIgnoreCase),
                    $"Catalog policy allows Apache-2.0 / MIT only; {e.Id} declares {e.LicenseName}");
                Assert.StartsWith("https://", e.LicenseUrl, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Sizes_and_rams_are_sane()
        {
            foreach (var e in LocalModelCatalog.All)
            {
                Assert.True(e.SizeMb > 0, $"{e.Id} declares a non-positive size");
                Assert.True(e.SizeMb < 30_000, $"{e.Id} size is implausible");
                Assert.True(e.MinRamGb >= 2, $"{e.Id} MinRamGb too low");
                Assert.False(string.IsNullOrWhiteSpace(e.Title));
                Assert.False(string.IsNullOrWhiteSpace(e.Description));
            }
        }

        [Fact]
        public void Recommendation_never_exceeds_machine_ram_and_prefers_the_largest_fit()
        {
            // 4 GB box → must suggest something suited to ~4 GB
            var rec4 = LocalModelCatalog.RecommendFor(4);
            Assert.True(rec4.MinRamGb <= 4);

            // 16 GB box → the 7B tier is the preferred class
            var rec16 = LocalModelCatalog.RecommendFor(16);
            Assert.Equal(16, rec16.MinRamGb);

            // Weakest box → smallest model always available
            var rec1 = LocalModelCatalog.RecommendFor(1);
            Assert.Equal(LocalModelCatalog.All.Min(e => e.MinRamGb), rec1.MinRamGb);
        }

        [Fact]
        public void Find_is_case_insensitive_and_null_safe()
        {
            var a = LocalModelCatalog.Find("qwen2.5-1.5b");
            Assert.NotNull(a);
            Assert.Equal(a, LocalModelCatalog.Find("QWEN2.5-1.5B"));
            Assert.Null(LocalModelCatalog.Find(null));
            Assert.Null(LocalModelCatalog.Find(""));
            Assert.Null(LocalModelCatalog.Find("does-not-exist"));
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidTokenPlaneTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P1 — token plane integrity pins (2026-09-05).
//
// The Crystal Fluid token plane was re-founded on the authored
// "Ios-17pm" reference system:
//   CrystalFluidColors.xaml   (47 colors, byte-verbatim reference)
//   CrystalFluidBrushes.xaml  (60 brushes, byte-verbatim reference)
//   CrystalFluidMetrics.xaml  (47 metrics, byte-verbatim reference)
//   CrystalFluidTokens.xaml   (pipeline extensions — shrink-wrapped)
//
// These tests pin the contract so a future edit cannot silently:
//   · duplicate a key across the four dictionaries (last-merge-wins
//     shadowing — the classic "theme stopped responding" bug class),
//   · reference a token that no dictionary defines (runtime XDG0008
//     fallout the moment a consumer inflates),
//   · break the authored load order in App.xaml (brushes resolve
//     StaticResource colors only because colors merge first),
//   · drift the adopted authority values (reflection 0.80, caustic
//     0.70/11s, ambient 18s, default water level 0.30),
//   · re-inflate the tokens file with nouns that belong to the
//     authored trio (single-home rule).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Tests.TestSupport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidTokenPlaneTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Project root not found");
        }

        private const string Colors =
            "Themes/CrystalFluid/CrystalFluidColors.xaml";
        private const string Brushes =
            "Themes/CrystalFluid/CrystalFluidBrushes.xaml";
        private const string Metrics =
            "Themes/CrystalFluid/CrystalFluidMetrics.xaml";
        private const string Tokens =
            "Themes/CrystalFluid/CrystalFluidTokens.xaml";

        private static readonly string[] PlaneFiles =
            { Colors, Brushes, Metrics, Tokens };

        private static List<string> KeysOf(string xaml) =>
            Regex.Matches(xaml, @"x:Key=""([^""]+)""")
                 .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        private static IEnumerable<string> StaticRefsOf(string xaml) =>
            Regex.Matches(xaml, @"\{StaticResource\s+([A-Za-z0-9_]+)\s*\}")
                 .Cast<Match>().Select(m => m.Groups[1].Value);

        // ── 1. CROSS-DICTIONARY KEY UNIQUENESS ──────────────────────────
        [Fact]
        public void Crystal_dictionaries_define_no_duplicate_keys()
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var clashes = new List<string>();

            foreach (var file in PlaneFiles)
            {
                foreach (var key in KeysOf(Read(file)))
                {
                    if (seen.TryGetValue(key, out var first))
                        clashes.Add($"{key}  ({first} + {Path.GetFileName(file)})");
                    else
                        seen[key] = Path.GetFileName(file);
                }
            }

            Assert.True(clashes.Count == 0,
                "Duplicate keys across the crystal plane (last-merge-wins " +
                "shadowing):\n  " + string.Join("\n  ", clashes));
        }

        // ── 2. TOKEN CLOSURE — EVERY STATIC REFERENCE RESOLVES ──────────
        [Fact]
        public void Crystal_dictionaries_are_self_contained()
        {
            var defined = new HashSet<string>(
                PlaneFiles.SelectMany(f => KeysOf(Read(f))),
                StringComparer.Ordinal);

            // Ios-17pm P4b (2026-09-05): the interim CrystalWater
            // control is retired; the plane's live parse-time consumer
            // is the authored controls dictionary (its templates bind
            // plane nouns statically). The StatCard host binds its water
            // layer through DynamicResource only — plane closure for it
            // is proven by the P4b battery.
            var consumerFiles = PlaneFiles.Concat(new[]
            {
                "Themes/CrystalFluid/CrystalFluidControls.xaml",
            });

            var missing = consumerFiles
                .SelectMany(f => StaticRefsOf(Read(f))
                    .Where(r => !defined.Contains(r))
                    .Select(r => $"{r}  (referenced by {Path.GetFileName(f)})"))
                .Distinct()
                .ToList();

            Assert.True(missing.Count == 0,
                "Unresolved {StaticResource} in the crystal plane:\n  " +
                string.Join("\n  ", missing));
        }

        // ── 3. APP.XAML MERGES THE PLANE IN AUTHORED ORDER ──────────────
        [Fact]
        public void App_xaml_merges_crystal_plane_in_authored_order()
        {
            string app = Read("App.xaml");

            int colors = app.IndexOf(Colors, StringComparison.Ordinal);
            int brushes = app.IndexOf(Brushes, StringComparison.Ordinal);
            int metrics = app.IndexOf(Metrics, StringComparison.Ordinal);
            int tokens = app.IndexOf(Tokens, StringComparison.Ordinal);
            int designTokens = app.IndexOf(
                "Themes/Styles/DesignTokens.xaml", StringComparison.Ordinal);

            Assert.True(designTokens >= 0, "DesignTokens.xaml merge missing.");
            Assert.True(colors > 0, "CrystalFluidColors.xaml not merged.");
            Assert.True(colors > designTokens,
                "Crystal colors must merge after the base design tokens.");
            Assert.True(colors < brushes && brushes < metrics && metrics < tokens,
                "Authored load order is colors → brushes → metrics → " +
                "pipeline tokens (brushes resolve StaticResource colors " +
                "only because colors merge first).");
        }

        // ── 4. AUTHORITY VALUES ADOPTED FROM THE REFERENCE SYSTEM ───────
        [Fact]
        public void Authored_baselines_are_adopted()
        {
            string metrics = Read(Metrics);

            Assert.Contains(
                "<sys:Double x:Key=\"CrystalReflectionOpacity\">0.80</sys:Double>",
                metrics, StringComparison.Ordinal);
            Assert.Contains(
                "<sys:Double x:Key=\"CrystalCausticOpacity\">0.70</sys:Double>",
                metrics, StringComparison.Ordinal);
            Assert.Contains(
                "<sys:Double x:Key=\"CrystalDefaultWaterLevel\">0.30</sys:Double>",
                metrics, StringComparison.Ordinal);
            Assert.Contains(
                "<Duration x:Key=\"CrystalCausticDuration\">0:0:11</Duration>",
                metrics, StringComparison.Ordinal);
            Assert.Contains(
                "<Duration x:Key=\"CrystalAmbientDuration\">0:0:18</Duration>",
                metrics, StringComparison.Ordinal);

            // Default water level 0.30 is pinned in the StatCard DP
            // default — the retired 0.42 ambience must not reappear.
            // Ios-17pm P4b: the interim CrystalWater control is deleted;
            // the authored CrystalWaterControl's default travels via its
            // TargetType style (authored metrics), not a code default.
            string stat = Read("Controls/StatCard/StatCardControl.xaml.cs");
            Assert.Contains("0.30", stat, StringComparison.Ordinal);
            Assert.DoesNotContain("0.42", stat, StringComparison.Ordinal);
        }

        // ── 5. TOKENS FILE STAYS SHRINK-WRAPPED (SINGLE-HOME RULE) ──────
        [Fact]
        public void Tokens_file_carries_only_pipeline_and_transitional_keys()
        {
            string tokens = Read(Tokens);

            // (A) pipeline keys — engine/studio-managed, app-lifecycle.
            foreach (var k in new[]
            {
                "CrystalWaterSpeed", "CrystalBubbleOpacity",
                "CrystalReducedMotion", "CrystalWaterDefaultEnabled",
                "CrystalCardDuration",
            })
            {
                Assert.Contains($"x:Key=\"{k}\"", tokens, StringComparison.Ordinal);
            }

            // (B) transitional brushes — Ios-17pm P4b TOMBSTONE EXECUTED:
            // the interim control is deleted and its three interim
            // brushes retired with it.
            foreach (var k in new[]
            {
                "CrystalWaterBrush", "CrystalWaterSurfaceBrush",
                "CrystalBubbleBrush",
            })
            {
                Assert.DoesNotContain($"x:Key=\"{k}\"", tokens,
                    StringComparison.Ordinal);
            }

            // Retired nouns now live ONLY in the authored trio — they must
            // never creep back into the pipeline file (duplicate shadowing).
            foreach (var k in new[]
            {
                "CrystalInkColor", "CrystalMutedColor", "CrystalAccentColor",
                "CrystalGlassBrush", "CrystalGlassStrongBrush",
                "CrystalCausticBrush", "CrystalReflectionBrush",
                "CrystalRadiusCard", "CrystalRadiusButton",
                "CrystalWaterLevelDefault", "CrystalBubbleCount",
                "CrystalWaterOpacity", "CrystalCausticOpacity",
                "CrystalReflectionOpacity", "CrystalHoverDuration",
                "CrystalAmbientDuration",
            })
            {
                Assert.DoesNotContain($"x:Key=\"{k}\"", tokens,
                    StringComparison.Ordinal);
            }
        }
    }
}

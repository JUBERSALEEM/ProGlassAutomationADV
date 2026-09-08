// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicenseGateNavigationCoverageTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Licensing — every navigation target must be a declared decision
//
// WHY THIS EXISTS
// ───────────────
// LicenseGate.CanNavigate returns Allow() when a module key is not in its map.
// That default is correct for the modules that ship in every edition, but it
// makes two very different situations indistinguishable:
//
//     "this module is free in every edition"
//     "somebody added a premium module and forgot the map entry"
//
// The failure mode is one-directional and silent. Forget an entry and the
// module ships ungated to every edition — no exception, no log line, nothing
// for a reviewer to see, because the code path that allows it is the same one
// that has always allowed About and Settings.
//
// The map now lists every key the navigation switch can produce, using an empty
// array for the free ones. That costs nothing at runtime (an empty array hits
// the `required.Length == 0` early return, exactly as the miss did) and it turns
// a forgotten entry into a failing test instead of free premium access.
//
// This is a source-scanning test on purpose. The map is private and the
// navigation keys are string literals in a switch, so there is no symbol the
// compiler can tie together — the relationship only exists as a convention, and
// a convention nobody asserts is a convention that rots.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class LicenseGateNavigationCoverageTests
    {
        private const string NavFile = "ViewModels/MainViewModel.Part2.cs";
        private const string GateFile = "Services/Licensing/LicenseGate.cs";

        [Fact]
        public void Every_navigation_target_is_declared_in_the_gate_map()
        {
            var nav = NavigationTargets();
            var mapped = GateMapKeys();

            var undeclared = nav.Except(mapped, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

            Assert.True(
                undeclared.Count == 0,
                "These navigation targets are not in LicenseGate.Map, so CanNavigate " +
                "allows them by default. That is fine if they ship in every edition — " +
                "but say so explicitly by adding the key with Array.Empty<string>(). " +
                "Leaving them out means a future PREMIUM module that is forgotten here " +
                "ships ungated to every licence, silently:\n  " +
                string.Join("\n  ", undeclared));
        }

        [Fact]
        public void The_gate_map_has_no_entries_the_navigation_switch_cannot_reach()
        {
            var nav = NavigationTargets();
            var mapped = GateMapKeys();

            var dead = mapped.Except(nav, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

            Assert.True(
                dead.Count == 0,
                "These LicenseGate.Map keys do not appear in the navigation switch, so " +
                "nothing ever asks about them. Either the module was renamed — in which " +
                "case the gate silently stopped protecting it — or the entry is stale:\n  " +
                string.Join("\n  ", dead));
        }

        [Fact]
        public void The_gated_modules_are_the_production_and_reporting_ones()
        {
            // Pinned so that changing what costs money is a deliberate edit to a
            // test, not a quiet change to a dictionary. Empty-array entries are
            // the free ones; anything else carries a feature pattern.
            var gated = GatedModules();

            Assert.Equal(
                new[]
                {
                    "Cutting", "DoublingLamination", "EdgeDeletion", "HolesCutouts",
                    "JobProfitability", "Mitring", "Optimization", "Pairing",
                    "Polish", "Tempering"
                },
                gated);
        }

        [Fact]
        public void The_two_lists_are_the_same_size()
        {
            // A cheap tripwire that fires on any drift, with a message that
            // points at the two counts rather than at a diff of 33 strings.
            Assert.Equal(NavigationTargets().Count, GateMapKeys().Count);
        }

        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The string literals the navigation switch in MainViewModel dispatches
        /// on. These are the only values <c>CanNavigate</c> is ever called with.
        /// </summary>
        private static SortedSet<string> NavigationTargets()
        {
            var source = Read(NavFile);

            int at = source.IndexOf("switch (targetKey)", StringComparison.Ordinal);
            Assert.True(at >= 0, $"could not find 'switch (targetKey)' in {NavFile}");

            var matches = Regex.Matches(
                source[at..], @"case\s+""([A-Za-z0-9_]+)""");

            Assert.True(
                matches.Count > 20,
                $"only {matches.Count} navigation cases found in {NavFile} — the " +
                "switch may have been restructured, which would make this test " +
                "pass vacuously.");

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in matches) keys.Add(m.Groups[1].Value);
            return keys;
        }

        /// <summary>
        /// Keys present in LicenseGate.Map. Read from the source between the map
        /// declaration and the method that consumes it, so entries added anywhere
        /// else in the file are not mistaken for gates.
        /// </summary>
        private static SortedSet<string> GateMapKeys()
        {
            var source = Read(GateFile);

            int start = source.IndexOf(
                "private static readonly Dictionary<string, string[]> Map",
                StringComparison.Ordinal);
            Assert.True(start >= 0, $"could not find the Map declaration in {GateFile}");

            int end = source.IndexOf(
                "public static LicenseGateResult CanNavigate",
                StringComparison.Ordinal);
            Assert.True(end > start, $"could not find CanNavigate after the Map in {GateFile}");

            var matches = Regex.Matches(source[start..end], @"\[""([A-Za-z0-9_]+)""\]");

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in matches) keys.Add(m.Groups[1].Value);

            Assert.True(
                keys.Count > 20,
                $"only {keys.Count} keys found in LicenseGate.Map — the map may have " +
                "been restructured, which would make this test pass vacuously.");

            return keys;
        }

        /// <summary>
        /// Map keys whose value is not an empty array — i.e. the modules that
        /// actually require a licence feature.
        /// </summary>
        private static string[] GatedModules()
        {
            var source = Read(GateFile);

            int start = source.IndexOf(
                "private static readonly Dictionary<string, string[]> Map",
                StringComparison.Ordinal);
            int end = source.IndexOf(
                "public static LicenseGateResult CanNavigate",
                StringComparison.Ordinal);

            var result = new List<string>();
            foreach (var line in source[start..end].Split('\n'))
            {
                var m = Regex.Match(line, @"\[""([A-Za-z0-9_]+)""\]\s*=\s*(.+?);?\s*$");
                if (!m.Success) continue;
                if (m.Groups[2].Value.Contains("Array.Empty", StringComparison.Ordinal)) continue;
                result.Add(m.Groups[1].Value);
            }

            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static string Read(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            var path = Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path), $"expected source file not found: {relative}");
            return File.ReadAllText(path);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ThemeKeyParityTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Theme — the dark and light dictionaries must define the same keys
//
// WHY THIS EXISTS
// ───────────────
// WPF resolves a resource key at runtime, not at compile time. Add a brush to
// DarkTheme.xaml and forget LightTheme.xaml and nothing fails to build, nothing
// fails to parse, and the app starts normally. The light theme simply falls
// through to whatever dictionary was merged before it — so one control on one
// screen renders in the wrong palette, usually with the dark theme's colour,
// and only when the operator happens to be on light.
//
// Measured 2026-09-09, that had not happened: DarkTheme.xaml and
// LightTheme.xaml each declare exactly 225 keys and the two sets are identical.
// Scanning all 43 theme XAML files also found ZERO keys declared twice inside
// the same file, so nothing is being silently shadowed either.
//
// Both are good facts that are true only because somebody has been careful.
// Nothing enforced them. This test does.
//
// It is deliberately a parity test rather than a consolidation. Merging 43
// dictionaries and 4.6 MB of XAML into a smaller set is real work with real
// risk, and the risk is invisible: a shadowed key changes a colour and fails
// nothing. Parity is the property worth locking down first, because it is the
// one that silently breaks and the one a test can actually prove.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class ThemeKeyParityTests
    {
        private const string Dark = "Themes/DarkTheme.xaml";
        private const string Light = "Themes/LightTheme.xaml";

        /// <summary>
        /// Raised when a new token is added to both themes, which is the
        /// intended way for this number to move.
        ///
        /// MEASURED 2026-09-09: 225. This lock was FAILING at 245 before
        /// 2026-09-15 — the constant had been left behind by later token
        /// additions and nobody saw it, because the test project did not
        /// build at all (the CS1503/CS1009 defects fixed in PR #14) so no
        /// test in this assembly had run for some time. Re-measured 245 and
        /// then +25 for the About module palette, which moved out of
        /// Views/About/AboutIos26Theme.xaml into both themes so the About
        /// page finally follows Light/Dark. See
        /// docs/ABOUT_DYNAMIC_THEME_CONVERSION_2026-09-15.md §1.1.
        /// </summary>
        private const int BaselineTokenCount = 270;

        private static readonly Regex KeyAttr =
            new("x:Key=\"([^\"]+)\"", RegexOptions.Compiled);

        [Fact]
        public void Both_themes_define_exactly_the_same_keys()
        {
            var dark = Keys(Dark);
            var light = Keys(Light);

            var missingFromLight = dark.Except(light).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var missingFromDark = light.Except(dark).OrderBy(k => k, StringComparer.Ordinal).ToList();

            Assert.True(
                missingFromLight.Count == 0 && missingFromDark.Count == 0,
                "The two themes have drifted apart. WPF resolves resource keys " +
                "at runtime, so a key missing from one theme does not fail to " +
                "build — that theme falls through to whatever was merged before " +
                "it and one control renders in the wrong palette, only on that " +
                "theme.\n\nMissing from LightTheme.xaml:\n  " +
                string.Join("\n  ", missingFromLight) +
                "\n\nMissing from DarkTheme.xaml:\n  " +
                string.Join("\n  ", missingFromDark));
        }

        [Fact]
        public void The_token_count_matches_the_baseline()
        {
            var dark = Keys(Dark);

            Assert.Equal(BaselineTokenCount, dark.Count);
        }

        [Fact]
        public void Neither_theme_declares_the_same_key_twice()
        {
            // A duplicate x:Key inside one file is a silent shadow: the later
            // declaration wins and the earlier one is dead. Scanning all 43
            // theme files on 2026-09-09 found none, which is worth keeping true
            // because the failure is a colour, not an error.
            var root = Root();
            var offenders = new List<string>();

            foreach (var path in Directory.EnumerateFiles(
                         Path.Combine(root, "Themes"), "*.xaml", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var line = 0;

                foreach (var raw in File.ReadLines(path))
                {
                    line++;
                    var m = KeyAttr.Match(raw);
                    if (!m.Success) continue;
                    if (seen.Add(m.Groups[1].Value)) continue;
                    offenders.Add($"{rel}:{line}  {m.Groups[1].Value}");
                }
            }

            Assert.True(
                offenders.Count == 0,
                "These keys are declared more than once inside the same theme " +
                "file. The later declaration silently wins:\n  " +
                string.Join("\n  ", offenders));
        }

        private static HashSet<string> Keys(string relative)
        {
            var path = Path.Combine(
                Root(), relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path), $"expected theme file not found: {relative}");

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in File.ReadLines(path))
            {
                var m = KeyAttr.Match(raw);
                if (m.Success) keys.Add(m.Groups[1].Value);
            }

            // Guard against a vacuous pass if the file is ever moved or
            // restructured: an empty key set would satisfy the parity test
            // trivially, since both sides would be empty.
            Assert.True(
                keys.Count > 100,
                $"only {keys.Count} keys found in {relative} — the file may have " +
                "been restructured, which would let the parity test pass on two " +
                "empty sets.");

            return keys;
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}

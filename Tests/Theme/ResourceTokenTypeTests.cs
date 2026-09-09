// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ResourceTokenTypeTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Theme — a resource fed into a typed Setter must be the right type
//
// WHY THIS EXISTS
// ───────────────
// On 2026-09-09 the application crashed on startup with:
//
//     InvalidOperationException: '7' is not a valid value for
//     property 'Margin'                        (ERR-F4C120)
//
// The cause was one line in Styles/DashboardKit.xaml:
//
//     <Setter Property="Margin" Value="{StaticResource DbGutterHalf}"/>
//
// where DbGutterHalf was declared <sys:Double>7</sys:Double>. Margin is a
// Thickness. WPF does not run a type converter over the result of a
// StaticResource lookup — the Double goes straight into the dependency
// property and is rejected.
//
// The reason this deserves a test rather than just a fix is WHEN it threw. Not
// at parse time, and not when the dictionary was loaded. It threw inside
// MeasureCore, during layout, on the first dashboard carrying a KPI band — so
// the app started, signed the user in, loaded the VAT figures, logged them, and
// only then died. Four CRITICAL entries and a crash dialog, from a token whose
// type was wrong.
//
// A XAML parser cannot catch this: both files are well-formed, and each is
// individually valid. It is a mismatch between two files. That is exactly the
// shape of bug a source scan is for.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class ResourceTokenTypeTests
    {
        /// <summary>
        /// Properties whose value type is known, mapped to the element type a
        /// resource must be declared as. Deliberately conservative: only
        /// properties where the expected type is unambiguous are listed, so
        /// this cannot fail on a legitimate use it does not understand.
        /// </summary>
        private static readonly Dictionary<string, string> ExpectedType =
            new(StringComparer.Ordinal)
            {
                ["Margin"] = "Thickness",
                ["Padding"] = "Thickness",
                ["BorderThickness"] = "Thickness",
                ["CornerRadius"] = "CornerRadius",
                ["FontSize"] = "Double",
                ["Height"] = "Double",
                ["Width"] = "Double",
                ["MinHeight"] = "Double",
                ["MinWidth"] = "Double",
                ["MaxHeight"] = "Double",
                ["MaxWidth"] = "Double",
                ["Opacity"] = "Double",
                ["Background"] = "Brush",
                ["Foreground"] = "Brush",
                ["BorderBrush"] = "Brush",
                ["Fill"] = "Brush",
                ["Stroke"] = "Brush",
                ["Effect"] = "Effect",
            };

        private static readonly Regex Declaration =
            new("<([\\w:]+)\\s+x:Key=\"([^\"]+)\"", RegexOptions.Compiled);

        private static readonly Regex TypedSetter =
            new("<Setter\\s+Property=\"([^\"]+)\"\\s+Value=\"\\{StaticResource\\s+([^}\\s]+)\\s*\\}\"",
                RegexOptions.Compiled);

        [Fact]
        public void Every_typed_setter_is_fed_a_resource_of_the_right_type()
        {
            var declared = CollectDeclarations();
            var mismatches = new List<string>();
            int checkedPairs = 0;

            foreach (var path in EnumerateXaml())
            {
                var rel = Relative(path);
                var line = 0;

                foreach (var raw in File.ReadLines(path))
                {
                    line++;
                    foreach (Match m in TypedSetter.Matches(raw))
                    {
                        // "FrameworkElement.Margin" and "Margin" are the same
                        // property; take the last segment.
                        var property = m.Groups[1].Value;
                        var dot = property.LastIndexOf('.');
                        if (dot >= 0) property = property.Substring(dot + 1);

                        if (!ExpectedType.TryGetValue(property, out var expected))
                            continue;

                        var key = m.Groups[2].Value;
                        checkedPairs++;

                        if (!declared.TryGetValue(key, out var sites))
                        {
                            // An undeclared key is a different failure, and a
                            // StaticResource for one throws at parse time with
                            // its own clear message. Reported, not asserted
                            // here, so one class of bug does not mask another.
                            mismatches.Add(
                                $"{rel}:{line}  Property=\"{property}\" -> " +
                                $"StaticResource {key} is not declared in any XAML file");
                            continue;
                        }

                        foreach (var site in sites)
                        {
                            if (IsCompatible(site.Type, expected)) continue;
                            mismatches.Add(
                                $"{rel}:{line}  Property=\"{property}\" -> " +
                                $"StaticResource {key} is <{site.Type}> " +
                                $"({site.Path}:{site.Line}) but {property} needs {expected}");
                        }
                    }
                }
            }

            // Guard against a vacuous pass. There were 383 such pairs on
            // 2026-09-09; if this ever drops to a handful the regex has stopped
            // matching and the test is asserting nothing.
            Assert.True(
                checkedPairs > 200,
                $"only {checkedPairs} typed Setter/StaticResource pairs were " +
                "found, expected several hundred — the regex has probably " +
                "stopped matching, which would let this test pass on nothing.");

            Assert.True(
                mismatches.Count == 0,
                "A resource of the wrong type is being assigned to a typed " +
                "dependency property. WPF does not type-convert a " +
                "StaticResource result, so this throws at MEASURE time rather " +
                "than parse time — the app starts, signs in, loads data, and " +
                "then dies during layout. That was crash ERR-F4C120.\n\n" +
                string.Join(Environment.NewLine, mismatches));
        }

        [Fact]
        public void The_dashboard_gutter_margin_token_is_a_thickness()
        {
            // The specific token that caused ERR-F4C120, pinned by name so the
            // regression is obvious rather than showing up as a generic
            // mismatch. DbGutterHalf was a sys:Double used as a Margin; it is
            // now DbGutterHalfMargin, a Thickness.
            var declared = CollectDeclarations();

            Assert.False(
                declared.ContainsKey("DbGutterHalf"),
                "DbGutterHalf is back. It was a sys:Double assigned to a Margin, " +
                "which is what crashed the app on 2026-09-09. The margin token " +
                "is DbGutterHalfMargin and must be a Thickness.");

            Assert.True(
                declared.TryGetValue("DbGutterHalfMargin", out var sites),
                "DbGutterHalfMargin is not declared. DbKpiBandStyle sets a " +
                "ContentPresenter Margin from it, so a dashboard KPI band would " +
                "fail to load its style.");

            Assert.All(sites, s => Assert.Equal("Thickness", s.Type));
        }

        // ── helpers ───────────────────────────────────────────────────

        private sealed record Site(string Type, string Path, int Line);

        private static bool IsCompatible(string declared, string expected)
        {
            var type = declared;
            var colon = type.LastIndexOf(':');
            if (colon >= 0) type = type.Substring(colon + 1);

            // Brushes and effects have many concrete subtypes
            // (SolidColorBrush, LinearGradientBrush, DropShadowEffect), so
            // those match on suffix. Scalars and structs match exactly.
            return expected switch
            {
                "Brush" => type.EndsWith("Brush", StringComparison.Ordinal),
                "Effect" => type.EndsWith("Effect", StringComparison.Ordinal),
                "Double" => type == "Double",
                _ => type == expected,
            };
        }

        private static Dictionary<string, List<Site>> CollectDeclarations()
        {
            var result = new Dictionary<string, List<Site>>(StringComparer.Ordinal);

            foreach (var path in EnumerateXaml())
            {
                var rel = Relative(path);
                var line = 0;
                foreach (var raw in File.ReadLines(path))
                {
                    line++;
                    foreach (Match m in Declaration.Matches(raw))
                    {
                        var key = m.Groups[2].Value;
                        if (!result.TryGetValue(key, out var list))
                            result[key] = list = new List<Site>();
                        list.Add(new Site(m.Groups[1].Value, rel, line));
                    }
                }
            }

            // A vacuous declaration map would make the main test pass on
            // nothing but its own guard, so pin the scale here too.
            Assert.True(
                result.Count > 1000,
                $"only {result.Count} distinct x:Key declarations found across " +
                "the XAML tree, expected several thousand — the scan is not " +
                "reaching the files.");

            return result;
        }

        private static IEnumerable<string> EnumerateXaml()
        {
            var root = Root();
            foreach (var path in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (rel.StartsWith("obj/", StringComparison.Ordinal)) continue;
                if (rel.Contains("/obj/", StringComparison.Ordinal)) continue;
                yield return path;
            }
        }

        private static string Relative(string path) =>
            Path.GetRelativePath(Root(), path).Replace('\\', '/');

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

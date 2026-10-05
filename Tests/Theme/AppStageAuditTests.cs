// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/AppStageAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: App stage (page background) + About type scale — regression locks
//
// WHY THIS EXISTS
// ───────────────
// 2026-09-15 owner directive, two halves:
//
//   "convert all cards and fonts safely move to dynamic so this about
//    module can adapt the theme style … Keeping the cards as it is
//    design … Background page dont make it dynamic it would be awesome if
//    you add this background globally everywhere the one about module is
//    having."
//
// That is a pair of opposite requirements held at the same time, and both
// of them fail SILENTLY if a future edit gets them backwards:
//
//  • Panels and type that stop following the theme — the About module was
//    in exactly that state for a day, because a palette declared inside a
//    locally merged dictionary shadows Application.Resources and nothing
//    fails to build.
//  • A page background that starts following the theme again — the key
//    would still resolve, still paint, and simply re-tint on a switch.
//
// So the invariance and the dynamism each get a lock. Both are source-file
// checks plus one pure-function check; no WPF Application is needed.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.Theme;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class AppStageAuditTests
    {
        private const string StageFile = "Themes/Styles/AppStage.xaml";
        private const string TypeScaleFile = "Themes/Styles/AboutTypeScale.xaml";
        private const string Light = "Themes/LightTheme.xaml";
        private const string Dark = "Themes/DarkTheme.xaml";

        /// <summary>The five stage colours, in declaration order. They are
        /// the About mockup stage, verbatim from the approved design.</summary>
        private static readonly string[] StageColours =
        {
            "#F1F5FB", // canvas
            "#BFE8E3", // orb — teal
            "#C5D8F6", // orb — blue
            "#CFD4F5", // orb — lavender
            "#D2EBD6", // orb — green
        };

        // ── 1. THE PAGE BACKGROUND MUST NOT BE THEME-DEPENDENT ────────────

        [Fact]
        public void App_stage_background_is_byte_identical_in_both_themes()
        {
            var root = LocateProjectRoot();

            var light = Block(Path.Combine(root, Light), "AppBackgroundBrush");
            var dark = Block(Path.Combine(root, Dark), "AppBackgroundBrush");

            // WPF cannot alias a dictionary value, so the drawing is authored
            // twice on purpose. This assertion is what keeps the duplication
            // honest: the moment one side is re-tinted, the page background
            // has quietly become dynamic again, which the owner rejected.
            Assert.True(
                string.Equals(light, dark, StringComparison.Ordinal),
                "AppBackgroundBrush differs between LightTheme.xaml and " +
                "DarkTheme.xaml. The page background is deliberately INVARIANT " +
                "(\"Background page dont make it dynamic\") — both themes must " +
                "carry the same stage drawing, and no runtime publisher may " +
                "re-tint it.");
        }

        [Fact]
        public void App_stage_colours_are_declared_once_outside_the_theme_files()
        {
            var root = LocateProjectRoot();

            var stage = File.ReadAllText(Path.Combine(root, StageFile));
            foreach (var colour in StageColours)
            {
                Assert.Contains($"Color=\"{colour}\"", stage);
            }

            // A themed copy of a stage colour would re-tint the page. The
            // drawing in the two theme files paints these colours with
            // Brush="…" inside the AppBackgroundBrush block, which is what the
            // lock above pins; a Color="…" anywhere in a theme file means a
            // token, and a stage token in a theme file is the regression.
            foreach (var rel in new[] { Light, Dark })
            {
                var theme = File.ReadAllText(Path.Combine(root, rel));
                foreach (var colour in StageColours)
                {
                    Assert.DoesNotContain($"Color=\"{colour}\"", theme);
                }
            }
        }

        [Fact]
        public void App_stage_keys_are_declared_outside_the_theme_files()
        {
            var root = LocateProjectRoot();
            var stage = File.ReadAllText(Path.Combine(root, StageFile));

            foreach (var key in new[]
            {
                "Ios26CanvasBrush", "Ios26OrbTealBrush", "Ios26OrbBlueBrush",
                "Ios26OrbLavenderBrush", "Ios26OrbGreenBrush",
            })
            {
                Assert.Contains($"x:Key=\"{key}\"", stage);
            }

            foreach (var rel in new[] { Light, Dark })
            {
                var theme = File.ReadAllText(Path.Combine(root, rel));
                foreach (var key in new[] { "Ios26CanvasBrush", "Ios26OrbTealBrush" })
                {
                    Assert.DoesNotContain($"x:Key=\"{key}\"", theme);
                }
            }
        }

        // ── 1b. THE PALETTE MUST ACTUALLY BE THEME-DEPENDENT ─────────────

        [Fact]
        public void About_palette_differs_between_light_and_dark()
        {
            var root = LocateProjectRoot();
            var light = BrushColors(Path.Combine(root, Light));
            var dark = BrushColors(Path.Combine(root, Dark));

            // The regression this catches: a palette copied into both themes
            // with the SAME value. Every key exists, every lock passes, and
            // switching to Dark changes nothing on the About page — which is
            // exactly what the owner reported.
            int differing = 0;
            foreach (var key in AboutModuleTokenGuard.PaletteKeys)
            {
                Assert.True(light.ContainsKey(key), $"LightTheme.xaml does not define {key}.");
                Assert.True(dark.ContainsKey(key), $"DarkTheme.xaml does not define {key}.");

                if (!string.Equals(light[key], dark[key], StringComparison.OrdinalIgnoreCase))
                {
                    differing++;
                }
            }

            // Two keys are intentionally identical (ink on accent is white in
            // both modes; the danger dot already is the dark tone). Everything
            // else has to move.
            Assert.True(differing >= AboutModuleTokenGuard.PaletteKeys.Length - 2,
                $"Only {differing} of {AboutModuleTokenGuard.PaletteKeys.Length} About " +
                "palette keys differ between Light and Dark, so the About module " +
                "cannot be following the theme.");

            // The three the eye notices first.
            foreach (var key in new[]
                     {
                         "Ios26CardBrush", "Ios26CardBorderBrush", "Ios26TextTitleBrush",
                     })
            {
                Assert.NotEqual(light[key], dark[key]);
            }
        }

        [Fact]
        public void About_card_edge_is_visible_and_not_pure_white_alpha()
        {
            var root = LocateProjectRoot();

            // 2026-09-16 owner report: "no border lines on cards". A 60 % white
            // edge on a white card is invisible by construction, so the light
            // column must not go back to a white-with-alpha value for the card
            // edge, and the two columns must not hold the same value.
            var light = BrushColors(Path.Combine(root, Light));
            var dark = BrushColors(Path.Combine(root, Dark));

            var lightEdge = light["Ios26CardBorderBrush"];
            Assert.False(
                string.Equals(lightEdge, "#99FFFFFF", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lightEdge, "#FFFFFFFF", StringComparison.OrdinalIgnoreCase),
                "Ios26CardBorderBrush is a white-with-alpha value again, which is " +
                "invisible on the white card in Light mode.");

            Assert.NotEqual(lightEdge, dark["Ios26CardBorderBrush"]);
        }

        // ── 1c. THE RUNTIME GUARD MUST BE ABLE TO REPAIR ─────────────────

        [Fact]
        public void About_guard_tables_match_the_dictionaries_they_protect()
        {
            var root = LocateProjectRoot();

            // The guard repairs missing About tokens from live theme tokens.
            // If its tables drift from the real dictionaries it would repair
            // with the wrong key — which is worse than doing nothing.
            var light = File.ReadAllText(Path.Combine(root, Light));
            var dark = File.ReadAllText(Path.Combine(root, Dark));
            var stage = File.ReadAllText(Path.Combine(root, StageFile));

            foreach (var key in AboutModuleTokenGuard.PaletteKeys)
            {
                Assert.Contains($"x:Key=\"{key}\"", light);
                Assert.Contains($"x:Key=\"{key}\"", dark);
            }

            Assert.Equal(AboutModuleTokenGuard.PaletteKeys.Length,
                AboutModuleTokenGuard.PaletteKeys.Distinct().Count());

            // Stage colours: the guard's table must equal AppStage.xaml.
            Assert.Equal(AboutModuleTokenGuard.StageKeys.Length,
                AboutModuleTokenGuard.StageColors.Length);
            for (int i = 0; i < AboutModuleTokenGuard.StageKeys.Length; i++)
            {
                Assert.Contains($"x:Key=\"{AboutModuleTokenGuard.StageKeys[i]}\"", stage);
                Assert.Contains($"Color=\"{AboutModuleTokenGuard.StageColors[i]}\"", stage);
            }
        }

        [Fact]
        public void About_guard_fallback_tokens_all_exist_in_both_themes()
        {
            var root = LocateProjectRoot();
            var light = File.ReadAllText(Path.Combine(root, Light));
            var dark = File.ReadAllText(Path.Combine(root, Dark));

            // A fallback that does not exist in the theme dictionaries would
            // make the repair a no-op — the exact silent failure the guard is
            // there to prevent. Reflection-free: the table is internal and
            // visible to this assembly.
            foreach (var token in AboutModuleTokenGuard.FallbackTokenKeys)
            {
                Assert.Contains($"x:Key=\"{token}\"", light);
                Assert.Contains($"x:Key=\"{token}\"", dark);
            }
        }

        // ── 2. THE TYPE SCALE MUST STAY DYNAMIC **AND** WHOLE-PIXEL ──────

        [Fact]
        public void About_type_scale_stays_whole_pixel_at_every_density()
        {
            // Pure function — no Application, so this runs in CI.
            foreach (var density in new[]
                     {
                         "Compact", "Normal", "Comfortable", "Spacious",
                         "Unrecognised", string.Empty,
                     })
            {
                var sizes = AboutDensityTypeScale.Sizes(density);
                Assert.Equal(AboutDensityTypeScale.Keys.Length, sizes.Length);

                for (int i = 0; i < sizes.Length; i++)
                {
                    var size = sizes[i];
                    Assert.True(size > 0, $"{density}: {AboutDensityTypeScale.Keys[i]} " +
                        "has a non-positive size.");

                    // Fractional sizes are banned outright: under
                    // TextFormattingMode=Display a half-pixel grid smears the
                    // Bold / SemiBold cuts (the 2026-09-15 typography defect).
                    Assert.True(
                        Math.Abs(size - Math.Round(size)) < 0.0001,
                        $"{density}: {AboutDensityTypeScale.Keys[i]} = {size} is not " +
                        "a whole pixel.");
                }

                // The ladder must stay strictly increasing — equal neighbours
                // would collapse two roles into one size.
                for (int i = 1; i < sizes.Length; i++)
                {
                    Assert.True(sizes[i] > sizes[i - 1],
                        $"{density}: the About ladder is not strictly increasing at " +
                        $"{AboutDensityTypeScale.Keys[i]} ({sizes[i - 1]} → {sizes[i]}).");
                }
            }
        }

        [Fact]
        public void About_type_scale_reproduces_the_approved_mockup_on_compact()
        {
            // Compact is the default profile, so the approved mockup ladder
            // must come back unchanged — this is the "keeping it as it is"
            // half of the directive.
            var expected = new[]
            {
                10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 22, 24, 28, 30,
            };

            var sizes = AboutDensityTypeScale.Sizes("Compact");
            Assert.Equal(expected.Length, sizes.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                // Explicit cast: an int[] vs double[] comparison would lean on
                // overload resolution between Assert.Equal<T>(T, T) and
                // Assert.Equal(double, double).
                Assert.Equal((double)expected[i], sizes[i]);
            }

            // An unknown profile must fall back to Compact, never to a guess.
            Assert.Equal(sizes, AboutDensityTypeScale.Sizes("Nonsense"));
        }

        [Fact]
        public void About_type_scale_leaves_micro_labels_alone_at_every_density()
        {
            var compact = AboutDensityTypeScale.Sizes("Compact");
            var spacious = AboutDensityTypeScale.Sizes("Spacious");

            for (int i = 0; i < compact.Length; i++)
            {
                if (compact[i] < AboutDensityTypeScale.GrowthFloor)
                {
                    Assert.Equal(compact[i], spacious[i]);
                }
                else
                {
                    Assert.True(spacious[i] > compact[i],
                        $"{AboutDensityTypeScale.Keys[i]} should grow with density.");
                }
            }
        }

        [Fact]
        public void About_type_scale_source_matches_the_published_ladder()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, TypeScaleFile));

            // The XAML holds the COMPACT fallback (what paints before the
            // density publisher has ever run) — it must agree with the
            // publisher's Compact ladder, or the module would jump on the
            // first density apply.
            int seen = 0;
            foreach (Match m in Regex.Matches(
                         src, "<sys:Double x:Key=\"(Ios26FontSize\\w+)\">([0-9.]+)</sys:Double>"))
            {
                seen++;
                var key = m.Groups[1].Value;
                var value = double.Parse(m.Groups[2].Value,
                    System.Globalization.CultureInfo.InvariantCulture);

                int index = Array.IndexOf(AboutDensityTypeScale.Keys, key);
                Assert.True(index >= 0, $"{key} is not part of the published ladder.");

                Assert.Equal(AboutDensityTypeScale.Sizes("Compact")[index], value);
            }

            // No vacuous pass: the file must declare the whole ladder.
            Assert.Equal(AboutDensityTypeScale.Keys.Length, seen);
        }

        // ── helpers ──────────────────────────────────────────────────────

        /// <summary>x:Key -> Color for every SolidColorBrush in a theme file.</summary>
        private static Dictionary<string, string> BrushColors(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var pattern = new Regex(
                "<SolidColorBrush x:Key=\"([^\"]+)\"\\s+Color=\"(#[0-9A-Fa-f]{6,8})\"");
            foreach (Match m in pattern.Matches(File.ReadAllText(path)))
            {
                map[m.Groups[1].Value] = m.Groups[2].Value;
            }

            return map;
        }

        /// <summary>Returns the full text of the element that declares
        /// <paramref name="key"/>, trimmed, so two files can be compared.</summary>
        private static string Block(string path, string key)
        {
            var src = File.ReadAllText(path);
            int at = src.IndexOf($"x:Key=\"{key}\"", StringComparison.Ordinal);
            Assert.True(at >= 0, $"{path} does not declare {key}.");

            int start = src.LastIndexOf('<', at);
            Assert.True(start >= 0, $"{path}: could not find the start of {key}.");

            var tag = Regex.Match(src.Substring(start), "^<([A-Za-z0-9:]+)");
            Assert.True(tag.Success, $"{path}: could not read the element name of {key}.");

            int end = src.IndexOf($"</{tag.Groups[1].Value}>", at, StringComparison.Ordinal);
            Assert.True(end >= 0, $"{path}: {key} is not a closed element.");

            end += $"</{tag.Groups[1].Value}>".Length;
            return src.Substring(start, end - start).Trim();
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "About", "AboutView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

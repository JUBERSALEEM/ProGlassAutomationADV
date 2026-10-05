// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/AboutSurfaceAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: About card geometry + edge — behind the engine, not in front of it
//
// WHY THIS EXISTS
// ───────────────
// 2026-09-16 owner report: "why about page cards still not accepting
// dynamic card corner border color from monet engine etc."
//
// Two About contracts existed and never met:
//   • the engine published AboutCardCornerRadius / AboutPanelCornerRadius /
//     AboutCardEffect / AboutCardBorderBrush — consumed by no view at all;
//   • the About views consumed Ios26Radius* / Ios26CardBorderBrush, of
//     which the radii were LOCAL CONSTANTS and the edge was theme-only.
//
// The fix republishes the About scale from the one radius authority
// (CardCornerRadius) and the edge from the engine's About edge token. These
// locks pin the three properties that make it work:
//   1. the maths reproduces the approved mockup at the default, scales
//      monotonically, and can never produce a fractional or silly value;
//   2. nothing in Views/About re-declares the scale (a local key shadows
//      Application.Resources, which would silently re-freeze the cards);
//   3. the wiring is actually there — a publisher nobody calls is the same
//      bug as a token nobody reads.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.Theme;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class AboutSurfaceAuditTests
    {
        private const string SurfaceFile = "Themes/Styles/AboutSurface.xaml";
        private const string AboutTheme = "Views/About/AboutIos26Theme.xaml";
        private const string Part1 = "Services/ThemeManager.Part1.cs";
        private const string Part2 = "Services/ThemeManager.Part2.cs";

        [Fact]
        public void About_radius_scale_reproduces_the_approved_mockup_at_the_default()
        {
            // The default card radius is 12 (ThemeManager._cardCornerRadius,
            // Settings → Appearance default). At 12 the live scale must equal
            // the approved mockup exactly — this is the "keeping the cards as
            // it is design" half of the directive.
            var radii = AboutSurfaceTokens.Radii(AboutSurfaceTokens.MockupCardRadius);

            Assert.Equal(AboutSurfaceTokens.MockupRadii.Length, radii.Length);
            for (int i = 0; i < radii.Length; i++)
            {
                Assert.Equal(
                    AboutSurfaceTokens.MockupRadii[i],
                    radii[i]);
            }

            // ...and the mockup table itself must match the XAML fallback, so
            // the first frame (before any theme apply) paints the design too.
            var src = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), SurfaceFile));

            for (int i = 0; i < AboutSurfaceTokens.RadiusKeys.Length; i++)
            {
                Assert.Contains(
                    $"<CornerRadius x:Key=\"{AboutSurfaceTokens.RadiusKeys[i]}\">" +
                    $"{AboutSurfaceTokens.MockupRadii[i]}</CornerRadius>",
                    src);
            }
        }

        [Fact]
        public void About_radius_scale_tracks_the_card_radius_monotonically()
        {
            // The regression this catches: a scale that is "dynamic" in name
            // only, or that jumps around as the slider moves.
            double[] previous = AboutSurfaceTokens.Radii(0);

            for (int step = 1; step <= 28; step++)
            {
                var radii = AboutSurfaceTokens.Radii(step);

                for (int i = 0; i < radii.Length; i++)
                {
                    Assert.True(radii[i] >= previous[i],
                        $"step {step}: {AboutSurfaceTokens.RadiusKeys[i]} went " +
                        $"down ({previous[i]} → {radii[i]}).");

                    Assert.True(Math.Abs(radii[i] - Math.Round(radii[i])) < 0.0001,
                        $"step {step}: {AboutSurfaceTokens.RadiusKeys[i]} is not a " +
                        "whole pixel.");

                    Assert.True(radii[i] >= 0 && radii[i] <= AboutSurfaceTokens.MaxRadius,
                        $"step {step}: {AboutSurfaceTokens.RadiusKeys[i]} = " +
                        $"{radii[i]} is outside 0..{AboutSurfaceTokens.MaxRadius}.");
                }

                // The card is the roundest step at every setting; the icon
                // tile is the tightest. If that flips, the card hierarchy is
                // upside down.
                Assert.True(radii[0] >= radii[4]);
                Assert.True(radii[4] <= radii[1]);

                previous = radii;
            }

            // The design must MOVE with the slider — otherwise the fix is a
            // no-op, which is what the owner reported.
            Assert.True(AboutSurfaceTokens.Radii(12)[0] != AboutSurfaceTokens.Radii(18)[0],
                "The About card radius does not follow the card-radius setting.");
        }

        [Fact]
        public void About_surface_tokens_are_not_declared_inside_the_module()
        {
            var root = LocateProjectRoot();
            var theme = File.ReadAllText(Path.Combine(root, AboutTheme));

            // A local declaration shadows the application-scope key, so the
            // publisher would write a value nothing reads. Only the bar's
            // full round may stay (it is not a card).
            foreach (var key in AboutSurfaceTokens.RadiusKeys)
            {
                Assert.DoesNotContain($"x:Key=\"{key}\"", theme);
            }

            Assert.Contains("x:Key=\"Ios26RadiusBar\"", theme);
        }

        [Fact]
        public void About_surface_publisher_is_wired_to_the_radius_and_theme_paths()
        {
            var root = LocateProjectRoot();

            // A publisher nobody calls is the same defect as a token nobody
            // reads — that is exactly how the engine's About keys died.
            var part2 = File.ReadAllText(Path.Combine(root, Part2));
            Assert.Contains("AboutSurfaceTokens.Publish", part2);
            Assert.Contains("ApplyCardCornerRadius", part2);

            var part1 = File.ReadAllText(Path.Combine(root, Part1));
            Assert.Contains("AboutSurfaceTokens.Publish", part1);

            // And the guard repairs the surface tokens if the dictionary
            // never reached the running build.
            var guard = File.ReadAllText(Path.Combine(
                root, "Services/Theme/AboutModuleTokenGuard.cs"));
            Assert.Contains("AboutSurfaceTokens.Publish", guard);
            Assert.Contains("Ios26CardBorderBrush", guard);
        }

        [Fact]
        public void About_card_edge_follows_the_engine_token()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(
                root, "Services/Theme/AboutSurfaceTokens.cs"));

            // The edge must come from the engine's About edge token, with the
            // theme dictionary value as the fallback when the engine has not
            // published one.
            Assert.Contains("AboutCardBorderBrush", src);
            Assert.Contains("Ios26CardBorderBrush", src);

            // The engine really does publish that key, live, on every accent
            // pass — otherwise the alias would be dead.
            var publish = File.ReadAllText(Path.Combine(root, Part1));
            Assert.Contains("\"AboutCardBorderBrush\"", publish);

            // A white-with-alpha edge on a white card is invisible; the light
            // column must not carry one for the About edge fallback either.
            var light = File.ReadAllText(Path.Combine(root, "Themes/LightTheme.xaml"));
            var match = Regex.Match(light,
                "<SolidColorBrush x:Key=\"Ios26CardBorderBrush\"\\s+Color=\"(#[0-9A-Fa-f]{6,8})\"");
            Assert.True(match.Success, "LightTheme.xaml no longer defines the About card edge.");
            Assert.NotEqual("#99FFFFFF", match.Groups[1].Value.ToUpperInvariant());
            Assert.NotEqual("#FFFFFFFF", match.Groups[1].Value.ToUpperInvariant());
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

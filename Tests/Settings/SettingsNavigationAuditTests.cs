// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Settings/SettingsNavigationAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Settings — every navigation surface shows every registered page
//
// WHY THIS EXISTS
// ───────────────
// 2026-09-16 owner report:
//   "check setting top panel … there are only 10 button when i have in
//    setting side bar 12 button … button too overlapping apply proper
//    spacing"
//
// The rail became data-driven in Phase 3 (one registry, one ItemsControl),
// but the top-panel quick-settings list was still ten hand-copied Buttons.
// It had drifted exactly the way hand-copied lists do:
//   • two pages (Themes, Sharing) were unreachable from that panel;
//   • two glyphs disagreed with the rail (Features drew E762, the rail's
//     License glyph; Agent drew E99A instead of E9F5), so one page wore two
//     different icons depending on which surface you used;
//   • rows had no margin and icons no gap, so they touched — with exactly
//     one hand-added Margin="0,0,0,10" proving the gap was known.
//
// These locks make that class of drift impossible:
//   1. no settings navigation surface may hard-code a page key;
//   2. the panel must render the registry, and the registry must cover every
//      hosted section;
//   3. glyphs are declared once, in the registry.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Settings
{
    public sealed class SettingsNavigationAuditTests
    {
        private const string NavigationVm = "ViewModels/Settings/SettingsViewModel.Navigation.cs";
        private const string SettingsView = "Views/Settings/SettingsView.xaml";
        private const string QuickPanel = "Views/Shared/QuickSettingsPanel.xaml";

        [Fact]
        public void Settings_registry_is_the_only_place_pages_are_declared()
        {
            var root = LocateProjectRoot();

            // The registry still declares the page keys...
            var nav = File.ReadAllText(Path.Combine(root, NavigationVm));
            var registered = Regex.Matches(nav, "new\\(\"([A-Za-z]+)\",\\s*\"\\\\u[0-9A-F]{4}\"")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.True(registered.Count >= 12,
                $"The settings registry declares only {registered.Count} pages; the " +
                "shell hosts twelve sections.");

            // ...and no navigation surface may hand-copy one again.
            foreach (var rel in new[] { QuickPanel, SettingsView })
            {
                var src = File.ReadAllText(Path.Combine(root, rel));
                var literals = Regex.Matches(src, "CommandParameter=\"([A-Za-z]+)\"")
                    .Select(m => m.Groups[1].Value)
                    .ToList();

                Assert.True(literals.Count == 0,
                    $"{rel} hard-codes page key(s) {string.Join(", ", literals)}. " +
                    "Bind SettingsViewModel.NavRows instead: a hand-copied list is " +
                    "how Themes and Sharing became unreachable from the top panel.");
            }
        }

        [Fact]
        public void Quick_panel_renders_the_registry_and_keeps_its_spacing()
        {
            var root = LocateProjectRoot();
            var panel = File.ReadAllText(Path.Combine(root, QuickPanel));

            // Data-driven from the one registry, grouped on the localized
            // caption so the header template stays a single binding.
            Assert.Contains("SettingsVM.NavRows", panel);
            Assert.Contains("PropertyGroupDescription", panel);
            Assert.Contains("GroupLabel", panel);

            // The single command the panel needs, taken from the shell.
            Assert.Contains("OpenSettingsPageCommand", panel);

            // Spacing: rows carry a bottom margin and the label a left margin,
            // so no row touches its neighbour and no glyph touches its label.
            Assert.Contains("Margin=\"0,0,0,6\"", panel);
            Assert.Contains("Margin=\"10,0,0,0\"", panel);

            // The popup chrome is untouched: an opaque liquid surface.
            Assert.Contains("ShellLiquidBrush", panel);
            Assert.Contains("LiquidShadowColor", panel);
        }

        [Fact]
        public void Every_registered_page_has_a_host_and_a_rail_row()
        {
            var root = LocateProjectRoot();

            var nav = File.ReadAllText(Path.Combine(root, NavigationVm));
            var registered = Regex.Matches(nav, "new\\(\"([A-Za-z]+)\",\\s*\"\\\\u[0-9A-F]{4}\"")
                .Select(m => m.Groups[1].Value)
                .ToList();

            var view = File.ReadAllText(Path.Combine(root, SettingsView));
            // Case-INSENSITIVE on purpose: EqualityToVisibilityConverter tries
            // Equals() first and then falls back to
            // StringComparison.OrdinalIgnoreCase, so the registry's "AI" and
            // the host's ConverterParameter=Ai do resolve. The comparison here
            // must mirror the converter, not the other way round — a
            // case-sensitive check would report a dead page that is live.
            var hosted = Regex.Matches(view, "ConverterParameter=([A-Za-z]+),")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missingHost = registered
                .Where(k => !hosted.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();
            Assert.True(missingHost.Count == 0,
                "These registered settings pages have no section host in " +
                $"{SettingsView}: {string.Join(", ", missingHost)}");

            var orphanHost = hosted
                .Where(k => !registered.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();
            Assert.True(orphanHost.Count == 0,
                "These section hosts have no registry entry, so nothing can " +
                $"navigate to them: {string.Join(", ", orphanHost)}");

            // Glyphs are declared once, in the registry, and stay unique so two
            // rows can never look identical (the old AI/Agent pair, E8F2 twice).
            var glyphs = Regex.Matches(nav, "\"\\\\u([0-9A-F]{4})\"")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.Equal(glyphs.Count, glyphs.Distinct().Count());

            // Every row belongs to a group the panel can caption.
            var groupKeys = Regex.Matches(nav, "new\\(\"[A-Za-z]+\",\\s*\"\\\\u[0-9A-F]{4}\",\\s*\"[^\"]+\",\\s*\"([^\"]+)\"\\)")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.Equal(registered.Count, groupKeys.Count);
            Assert.DoesNotContain(string.Empty, groupKeys);
        }

        [Fact]
        public void Settings_sections_reach_theme_brushes_dynamically()
        {
            var root = LocateProjectRoot();

            // 2026-09-16, found while auditing "many issues in Settings →
            // Appearance": three sites bound a theme brush with
            // {StaticResource}. That resolves once, at parse time, against
            // whatever dictionary happens to be merged then — so the colour
            // is frozen for the life of the window and a Light/Dark or family
            // switch cannot repaint it. The brush in question
            // (CrystalPositiveBrush) is a Crystal-family-only token, so in
            // any other family the StaticResource form resolves against a
            // dictionary that may not even be there.
            //
            // Theme brushes must always be {DynamicResource}. Exceptions: the
            // converter/style keys (they are objects, not brushes) and
            // resources declared INSIDE the same file as literals, which the
            // hex/colour locks already police.
            var offenders = new List<string>();
            var pattern = new Regex(
                "\\{StaticResource ([A-Za-z0-9_.]*Brush)\\}");

            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(root, "Views", "Settings"),
                         "*.xaml", SearchOption.AllDirectories))
            {
                foreach (Match m in pattern.Matches(File.ReadAllText(file)))
                {
                    offenders.Add(
                        Path.GetFileName(file) + ": " + m.Groups[1].Value);
                }
            }

            Assert.True(offenders.Count == 0,
                "Settings XAML binds a theme brush with {StaticResource}, so the " +
                "colour freezes at load and theme / family switches cannot repaint " +
                "it. Use {DynamicResource}:\n  " + string.Join("\n  ", offenders));
        }

        [Fact]
        public void Settings_pages_use_the_grammar_not_the_generic_surfaces()
        {
            var root = LocateProjectRoot();
            var offenders = new List<string>();

            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(root, "Views", "Settings", "Sections"),
                         "*.xaml", SearchOption.TopDirectoryOnly))
            {
                var src = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                // The approved grammar is the only card/tile system a settings
                // page may use. The generic surfaces are what the twelve pages
                // used before the 2026-09-16 rewrite, each with its own radius,
                // padding and wash — that drift is what the owner reported as
                // "many issues in Settings".
                if (src.Contains("Style=\"{StaticResource SurfacePrimaryStyle}\"")
                    || src.Contains("Style=\"{StaticResource SurfaceDetailStyle}\""))
                {
                    offenders.Add(name + ": uses a generic Surface* style");
                }

                // Every page must be able to resolve the grammar.
                if (!src.Contains("SettingsCardStyles.xaml"))
                {
                    offenders.Add(name + ": does not merge SettingsCardStyles.xaml");
                }

                // A local page-title block is the duplicate header the shell
                // strip replaced (Margin 32,26 is its signature).
                if (src.Contains("Margin=\"32,26,32,14\""))
                {
                    offenders.Add(name + ": still declares a local page title block");
                }

                // A card style used as a vertical gap.
                if (src.Contains("<Border Style=\"{StaticResource SettingsPageCardStyle}\"/>"))
                {
                    offenders.Add(name + ": uses a card as an empty spacer");
                }
            }

            Assert.True(offenders.Count == 0,
                "Settings pages must compose from Views/Settings/SettingsCardStyles.xaml " +
                "(the About card grammar):\n  " + string.Join("\n  ", offenders));
        }

        [Fact]
        public void Settings_grammar_is_built_on_the_live_about_tokens()
        {
            var root = LocateProjectRoot();
            var grammar = File.ReadAllText(Path.Combine(
                root, "Views/Settings/SettingsCardStyles.xaml"));

            // The grammar must be built on the tokens that are theme-, family-,
            // accent- and density-dynamic — that is what makes the settings
            // module follow a theme switch at all. A literal here (hex, radius
            // number, font size number) would freeze the module again.
            //
            // 2026-09-17: the three About SURFACE keys moved out of this list
            // and the five Settings crystal keys moved in. The card, the inner
            // tile and the shadow are no longer borrowed from About — the
            // material is Settings' own now (Settings_cards_carry_the_crystal_
            // material below pins it). Geometry and type still come from About.
            foreach (var key in new[]
            {
                "Ios26TileBrush",
                "Ios26RadiusCard", "Ios26RadiusInner",
                "Ios26RadiusTile", "Ios26RadiusChip", "Ios26RadiusSmall",
                "Ios26TextTitleBrush", "Ios26TextSecondaryBrush", "Ios26TextMutedBrush",
                "Ios26AccentBrush", "Ios26AccentSoftBrush", "Ios26SuccessSoftBrush",
                "Ios26FontSizeCardTitle", "Ios26FontSizeBody", "Ios26FontSizeValue",
                "SettingsCardFaceBrush", "SettingsCardEdgeBrush", "SettingsCardShadow",
                "SettingsInnerFaceBrush", "SettingsInnerEdgeBrush",
            })
            {
                Assert.Contains("DynamicResource " + key, grammar);
            }

            // No literal colours, radii or sizes in the grammar itself.
            Assert.DoesNotMatch(new Regex("#[0-9A-Fa-f]{6,8}"), grammar);
            Assert.DoesNotMatch(new Regex("CornerRadius=\"[0-9]"), grammar);
            Assert.DoesNotMatch(new Regex("FontSize=\"[0-9]"), grammar);
        }

        /// <summary>
        /// 2026-09-17 owner report: "current one is not applied as expected cards
        /// are too close design is still same. Generate the new mockup with
        /// crystal liquid ios 26 theme type."
        ///
        /// Two failures were behind it, and both were in the SPEC rather than in
        /// the code: the approved sheets asked for a flat card (one #CCFFFFFF
        /// fill, a 1 px hairline edge, a 10 % shadow) and they spaced the cards
        /// at 14 — at which point the cards touch and the page reads as a single
        /// pale sheet. This lock pins the replacement material and rhythm so the
        /// module cannot drift back to a flat fill while every other test stays
        /// green (a flat fill resolves, parses and renders — nothing else
        /// notices).
        /// </summary>
        [Fact]
        public void Settings_cards_carry_the_crystal_material()
        {
            var root = LocateProjectRoot();
            var grammar = File.ReadAllText(Path.Combine(
                root, "Views/Settings/SettingsCardStyles.xaml"));

            // 1. The card composes the crystal layers instead of a flat fill.
            Assert.Contains("Background\" Value=\"{DynamicResource SettingsCardFaceBrush}\"", grammar);
            Assert.Contains("BorderBrush\" Value=\"{DynamicResource SettingsCardEdgeBrush}\"", grammar);
            Assert.Contains("Effect\" Value=\"{DynamicResource SettingsCardShadow}\"", grammar);

            // 2. The inner panel is translucent glass, not the opaque tile that
            //    made every row look like a sticker pasted on the card.
            Assert.Contains("Background\" Value=\"{DynamicResource SettingsInnerFaceBrush}\"", grammar);
            Assert.Contains("Background\" Value=\"{DynamicResource Ios26TileBrush}\"", grammar); // action bar + danger wash keep it

            // 3. The card rhythm is the ABOUT module's rhythm, token for
            //    token — the owner benchmarked the settings pages against
            //    About twice (2026-09-17 "cards are too close", 2026-09-16
            //    "cards are too big … check About module cards spacing"),
            //    and About uses Padding 22,20 with a 14 px card gap
            //    (AboutIos26Theme.xaml Ios26CardStyle + the Margin every
            //    About card carries). Anything else here is drift.
            Assert.Contains("<Thickness x:Key=\"SettingsCardMargin\">0,0,0,14</Thickness>", grammar);
            Assert.Contains("<sys:Double x:Key=\"SettingsGapCard\">14</sys:Double>", grammar);
            Assert.Contains("<Thickness x:Key=\"SettingsCardPadding\">22,20</Thickness>", grammar);
            Assert.Contains("<Thickness x:Key=\"SettingsPagePadding\">24,20,24,24</Thickness>", grammar);

            // 4. All five keys must exist in BOTH theme columns, or one theme
            //    silently falls through to the other theme's material (the
            //    failure class ThemeKeyParityTests exists for). A DrawingBrush /
            //    LinearGradientBrush / DropShadowEffect has no Color= attribute
            //    for the other audits to read, so it is asserted here.
            foreach (var theme in new[] { "Themes/LightTheme.xaml", "Themes/DarkTheme.xaml" })
            {
                var src = File.ReadAllText(Path.Combine(root, theme.Replace('/', Path.DirectorySeparatorChar)));
                foreach (var key in new[]
                         {
                             "SettingsCardFaceBrush", "SettingsCardEdgeBrush",
                             "SettingsInnerFaceBrush", "SettingsInnerEdgeBrush",
                             "SettingsCardShadow",
                         })
                {
                    Assert.Contains("x:Key=\"" + key + "\"", src);
                }
            }
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

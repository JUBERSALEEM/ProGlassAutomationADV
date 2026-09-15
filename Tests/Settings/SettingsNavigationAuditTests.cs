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

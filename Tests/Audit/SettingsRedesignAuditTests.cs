// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Audit/SettingsRedesignAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Settings v3 redesign — regression locks
//
// The 2026-09-17 redesign fixed four failure classes that all regress
// SILENTLY (nothing fails to build when they come back):
//
//   • the lazy page host replaced by a stacked-Visibility Grid again
//     (twelve page trees instantiated at window open);
//   • the spacing scale replaced by hand-typed Margin literals (the exact
//     mechanism that made "give the cards air" unfixable for a year);
//   • colour/radius/type literals creeping back into the sections;
//   • interactive controls losing their UI Automation names (80 controls
//     renamed in the A1 sweep; a screen reader must never hear a bare
//     "toggle button" on this module again).
//
// Plus two file-size budgets so the god-file rot cannot return: no
// SettingsViewModel partial over 350 lines, shell under 420.
// All checks are source-file scans; no WPF Application is needed.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Audit
{
    public sealed class SettingsRedesignAuditTests
    {
        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Views", "Settings", "SettingsView.xaml")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }

        private static IEnumerable<string> Sections() =>
            Directory.EnumerateFiles(Path.Combine(Root(), "Views", "Settings", "Sections"), "*.xaml");

        // ── 1. LAZY HOST ──────────────────────────────────────────────────

        [Fact]
        public void Shell_Hosts_One_Page_Via_Template_Selector()
        {
            var shell = File.ReadAllText(Path.Combine(Root(), "Views", "Settings", "SettingsView.xaml"));
            Assert.Contains("ContentTemplateSelector", shell);
            Assert.DoesNotContain("EqualityToVisibilityConverter", shell);
            Assert.DoesNotContain("sections:Settings", shell);
        }

        // ── 2. SPACING SCALE ──────────────────────────────────────────────

        private static readonly Regex LiteralMargin = new(@"Margin=""([^""]+)""");

        [Fact]
        public void Sections_Carry_No_Rhythmic_Margin_Literals()
        {
            foreach (var file in Sections())
            {
                foreach (Match m in LiteralMargin.Matches(File.ReadAllText(file)))
                {
                    var parts = m.Groups[1].Value.Split(',');
                    var vals = parts.Select(p => int.TryParse(p.Trim(), out var v) ? Math.Abs(v) : 99).ToList();
                    // 0-4 px optical nudges and Margin="0" resets stay literal;
                    // anything rhythmic (6 px and up) must be a shared token.
                    Assert.True(vals.All(v => v <= 4),
                        $"{Path.GetFileName(file)}: rhythmic literal Margin=\"{m.Groups[1].Value}\" — use a Settings* Thickness token");
                }
            }
        }

        [Fact]
        public void Spacing_Scale_Defines_The_Module_Gaps()
        {
            var dict = File.ReadAllText(Path.Combine(Root(), "Views", "Settings", "SettingsCardStyles.xaml"));
            foreach (var key in new[]
                     {
                         "SettingsCardMargin", "SettingsRowMargin", "SettingsTrailingGap",
                         "SettingsStackGap8", "SettingsInlineGap8R", "SettingsPagePadding",
                     })
                Assert.Contains($"x:Key=\"{key}\"", dict);
        }

        // ── 3. NO COLOUR / RADIUS / TYPE LITERALS ─────────────────────────

        [Fact]
        public void Sections_Carry_No_Visual_Literals()
        {
            foreach (var file in Sections())
            {
                var txt = File.ReadAllText(file);
                var name = Path.GetFileName(file);
                Assert.True(!Regex.IsMatch(txt, @"#[0-9A-Fa-f]{6,8}"), name + ": hex colour literal");
                Assert.True(!Regex.IsMatch(txt, @"CornerRadius=""[0-9]"), name + ": CornerRadius literal");
                Assert.True(!Regex.IsMatch(txt, @"FontSize=""[0-9]"), name + ": FontSize literal");
            }
        }

        // ── 4. ACCESSIBILITY NAMES ────────────────────────────────────────

        [Fact]
        public void Every_Interactive_Control_Carries_An_Automation_Name()
        {
            foreach (var file in Sections())
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!Regex.IsMatch(lines[i], @"^\s*<(ToggleButton|Slider|CheckBox|ListBox)\b")) continue;
                    var window = string.Join("\n", lines.Skip(i).Take(4));
                    Assert.True(window.Contains("AutomationProperties.Name"),
                        $"{Path.GetFileName(file)}:{i + 1}: interactive control without AutomationProperties.Name");
                }
            }
        }

        // ── 5. FILE-SIZE BUDGETS ──────────────────────────────────────────

        [Fact]
        public void Xaml_No_Style_Set_Twice_And_No_PropertyElement_Attributes()
        {
            // REGRESSION LOCKS (owner build 2026-09-16):
            //  MC3024/XLS0501 — a Border with BOTH a Style attribute and a
            //  <Border.Style> property element; MC3057 — attributes set on
            //  property elements (e.g. <ListBox.ItemTemplate AutomationProperties...>).
            var dup = new System.Text.RegularExpressions.Regex(
                "<Border[^>]*\\sStyle=\"[^>]*>\\s*<Border\\.Style>");
            var prop = new System.Text.RegularExpressions.Regex(
                "<(\\w+\\.[A-Z]\\w+)\\s+[A-Za-z][^>]*>");
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(Root(), "Views"), "*.xaml", SearchOption.AllDirectories))
            {
                string x = File.ReadAllText(file);
                Assert.False(dup.IsMatch(x),
                    Path.GetFileName(file) + ": Border sets Style twice (attribute + property element)");
                foreach (System.Text.RegularExpressions.Match m in prop.Matches(x))
                    Assert.True(m.Groups[1].Value.Contains(":"),
                        Path.GetFileName(file) + ": property element " +
                        m.Groups[1].Value + " carries attributes (MC3057)");
            }
        }

        // ── 6. BUILD-INTEGRITY LOCKS (owner build 2026-09-16) ────────────

        [Fact]
        public void No_Two_Xaml_Files_Share_An_XClass()
        {
            // CS8646 / CS0121 / CS0102: two .xaml files declaring the SAME
            // x:Class make the markup compiler emit the generated partial
            // twice (once per obj sub-folder), so Connect() and
            // InitializeComponent() exist twice and every x:Name field is
            // duplicated. A stray copy of two settings sections lived under
            // Themes/Languages and broke the build.
            var owners = new Dictionary<string, string>();
            foreach (var file in Directory.EnumerateFiles(
                         Root(), "*.xaml", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(Root(), file).Replace('\\', '/');
                // EnumerateFiles(AllDirectories) reaches into the HIDDEN .git
                // folder on Windows and can throw — never scan it.
                if (rel.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
                    rel.Contains("/.git/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                    rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                    continue;
                var q = new string((char)34, 1);
                var m = Regex.Match(File.ReadAllText(file),
                    "x:Class=" + q + "([^" + q + "]+)" + q);
                if (!m.Success) continue;
                Assert.False(owners.TryGetValue(m.Groups[1].Value, out var other),
                    m.Groups[1].Value + " is declared by both " + other + " and " + rel);
                owners[m.Groups[1].Value] = rel;
            }
        }

        [Fact]
        public void Language_Pack_Folder_Holds_Only_Resource_Dictionaries()
        {
            // Themes/Languages carries the 15 L1 language dictionaries and
            // nothing else. A UserControl dropped there is compiled as a
            // second Page for the same x:Class (the stray-copy bug above).
            var dir = Path.Combine(Root(), "Themes", "Languages");
            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml"))
            {
                var x = File.ReadAllText(file);
                Assert.DoesNotContain("x:Class", x);
                Assert.Contains("<ResourceDictionary", x);
            }
        }

        [Fact]
        public void Settings_Page_ViewModels_Live_In_The_Pages_Namespace()
        {
            // CS0234: the shell and the section code-behinds qualify the
            // promoted page VMs as ViewModels.Settings.Pages.*; the seven
            // files under Pages/ were still declaring the PARENT namespace,
            // so the qualified name did not exist. Folder == namespace here.
            var pages = Path.Combine(Root(), "ViewModels", "Settings", "Pages");
            foreach (var file in Directory.EnumerateFiles(pages, "*.cs"))
                Assert.Contains("namespace ProGlassAutomation.ViewModels.Settings.Pages",
                    File.ReadAllText(file));

            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(Root(), "ViewModels", "Settings"), "*.cs"))
                Assert.Contains("namespace ProGlassAutomation.ViewModels.Settings",
                    File.ReadAllText(file));
        }

        [Fact]
        public void Shell_Helpers_Are_Defined()
        {
            // REGRESSION LOCK: the phase-3a split once dropped SaveConfig()'s
            // definition while 26 call sites survived — the branch could not
            // compile. Any helper that partials call across file boundaries
            // must be verifiably defined inside the Settings VM tree.
            string all = "";
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(Root(), "ViewModels", "Settings"), "*.cs",
                         SearchOption.AllDirectories))
                all += File.ReadAllText(file);

            foreach (var helper in new[] { "SaveConfig", "ScheduleDebouncedSave", "InitializeV2Sections" })
                Assert.Contains(helper + "(", all.Replace("void " + helper, "void " + helper)); // call sites exist
            Assert.Contains("void SaveConfig()", all);
            Assert.Contains("void ScheduleDebouncedSave(", all);
            Assert.Contains("void InitializeV2Sections()", all);
        }

        [Fact]
        public void No_SettingsViewModel_Partial_Exceeds_350_Lines()
        {
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(Root(), "ViewModels", "Settings"), "SettingsViewModel*.cs"))
                Assert.True(File.ReadAllLines(file).Length <= 350,
                    Path.GetFileName(file) + " exceeds the 350-line budget — split it");

            // Per-page ViewModels promoted out of the god class (v3 pilot:
            // Sharing) live under Pages/ and obey the same budget.
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(Root(), "ViewModels", "Settings", "Pages"), "*PageViewModel*.cs"))
                Assert.True(File.ReadAllLines(file).Length <= 350,
                    Path.GetFileName(file) + " exceeds the 350-line budget — split it");
        }

        [Fact]
        public void Shell_Stays_Under_420_Lines()
        {
            var shell = Path.Combine(Root(), "Views", "Settings", "SettingsView.xaml");
            Assert.True(File.ReadAllLines(shell).Length <= 420, "SettingsView.xaml exceeded 420 lines");
        }

        [Fact]
        public void Sections_Stay_Under_400_Lines()
        {
            foreach (var file in Sections())
                Assert.True(File.ReadAllLines(file).Length <= 400,
                    Path.GetFileName(file) + " exceeded 400 lines — split its cards");
        }

        [Fact]
        public void Cards_Stay_Under_520_Lines()
        {
            var cards = Path.Combine(Root(), "Views", "Settings", "Sections", "Cards");
            if (!Directory.Exists(cards)) return;
            foreach (var file in Directory.EnumerateFiles(cards, "*.xaml"))
                Assert.True(File.ReadAllLines(file).Length <= 520,
                    Path.GetFileName(file) + " exceeded 520 lines — split its tiles");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/About/AboutModuleAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: About page architecture / UX regression locks
//
// These are source-first guardrails for the About module. They protect the
// exact issues reported by the user:
//   • fixed hex colours / fixed corner radii that ignore live theme changes,
//   • hard-coded edition text instead of the shared licensing surface,
//   • missing hero metrics bindings,
//   • non-responsive fixed grids,
//   • About palette keys missing from ThemeManager / theme dictionaries.
//
// No WPF runtime is required for the source-file checks. The small VM tests
// are pure CLR checks and verify the visible metrics stay aligned with the
// underlying data.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using ProGlassAutomation.ViewModels.About;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.About
{
    public sealed class AboutModuleAuditTests
    {
        private static readonly string[] AboutFiles =
        {
            "Views/About/AboutView.xaml",
            "Views/About/AboutStyles.xaml",
            "Views/About/Sections/AboutOverviewSection.xaml",
            "Views/About/Sections/AboutLicenseCard.xaml",
            "Views/About/Sections/AboutCrashReportsCard.xaml",
            "Views/About/Sections/ModulesSection.xaml",
            "Views/About/Sections/DeveloperSection.xaml",
            "Views/About/Sections/SystemSection.xaml",
            "Views/About/Sections/DiagnosticsSection.xaml",
        };

        [Fact]
        public void About_xaml_files_parse_cleanly()
        {
            var root = LocateProjectRoot();
            foreach (var rel in AboutFiles)
            {
                var path = Path.Combine(root, rel);
                Assert.True(File.Exists(path), $"Missing: {rel}");
                var xdoc = new XmlDocument();
                xdoc.Load(path);
            }
        }

        [Fact]
        public void About_views_do_not_use_fixed_hex_colors_or_literal_corner_radii()
        {
            var root = LocateProjectRoot();
            var hex = new Regex("#[0-9A-Fa-f]{6,8}", RegexOptions.Compiled);
            var literalCorner = new Regex("CornerRadius=\"[0-9]+(?:,[0-9]+){0,3}\"",
                RegexOptions.Compiled);

            foreach (var rel in AboutFiles)
            {
                var src = File.ReadAllText(Path.Combine(root, rel));
                Assert.False(hex.IsMatch(src),
                    $"{rel} still contains a hard-coded hex colour. " +
                    "About must bind theme resources so accent / Light / Dark " +
                    "changes restyle live.");
                Assert.False(literalCorner.IsMatch(src),
                    $"{rel} still contains a literal CornerRadius. " +
                    "About must follow the shared CardCornerRadius tokens.");
            }
        }

        [Fact]
        public void About_shell_and_developer_section_use_live_shared_bindings()
        {
            var root = LocateProjectRoot();
            var shell = File.ReadAllText(Path.Combine(root, "Views/About/AboutView.xaml"));
            var dev = File.ReadAllText(Path.Combine(root, "Views/About/Sections/DeveloperSection.xaml"));

            Assert.Contains("ItemsSource=\"{Binding LeftPillars}\"", shell);
            Assert.Contains("ItemsSource=\"{Binding RightPillars}\"", shell);
            Assert.Contains("Text=\"{Binding LicenseStatus.EditionDisplay}\"", shell);
            Assert.Contains("Text=\"{Binding LicenseStatus.EditionDisplay}\"", dev);
            Assert.True(dev.IndexOf("Enterprise Edition", StringComparison.Ordinal) < 0,
                "DeveloperSection.xaml still hard-codes the edition instead of binding the shared license status.");
        }

        [Fact]
        public void About_sections_use_responsive_wrap_layouts()
        {
            var root = LocateProjectRoot();
            var overview = File.ReadAllText(Path.Combine(root, "Views/About/Sections/AboutOverviewSection.xaml"));
            var modules = File.ReadAllText(Path.Combine(root, "Views/About/Sections/ModulesSection.xaml"));
            var developer = File.ReadAllText(Path.Combine(root, "Views/About/Sections/DeveloperSection.xaml"));

            Assert.Contains("<WrapPanel", overview);
            Assert.Contains("<WrapPanel", modules);
            Assert.Contains("<WrapPanel", developer);

            Assert.True(modules.IndexOf("<UniformGrid Columns=\"2\"/>", StringComparison.Ordinal) < 0,
                "ModulesSection.xaml regressed to a fixed 2-column grid.");
            Assert.True(modules.IndexOf("<UniformGrid Rows=\"2\" Columns=\"4\"/>", StringComparison.Ordinal) < 0,
                "ModulesSection.xaml regressed to a fixed 2x4 workflow grid.");
            Assert.True(developer.IndexOf("<UniformGrid Rows=\"2\" Columns=\"4\"/>", StringComparison.Ordinal) < 0,
                "DeveloperSection.xaml regressed to a fixed tech-stack grid.");
        }

        /// <summary>Reads a file plus its .PartN.cs siblings (Phase 3.8
        /// partial families — source assertions must see all content).</summary>
        private static string ReadFamily(string root, string relative)
        {
            var sb = new System.Text.StringBuilder(
                File.ReadAllText(Path.Combine(root, relative)));
            string dir = Path.GetDirectoryName(Path.Combine(root, relative))!;
            string name = Path.GetFileNameWithoutExtension(relative);
            foreach (var part in Directory.EnumerateFiles(dir, name + ".Part*.cs")
                         .OrderBy(f => f, StringComparer.Ordinal))
                sb.Append(File.ReadAllText(part));
            return sb.ToString();
        }

        [Fact]
        public void ThemeManager_publishes_the_about_palette_contract()
        {
            var root = LocateProjectRoot();
            var src = ReadFamily(root, "Services/ThemeManager.cs");

            Assert.Contains("PublishAboutPalette", src);
            foreach (var key in new[]
            {
                "LiquidAccent",
                "LiquidAccentDeep",
                "LiquidCerulean",
                "LiquidCard",
                "LiquidSheen",
                "LiquidOrbA",
                "LiquidOrbB",
                "AboutInteractiveBrush",
                "AboutInteractiveHoverBrush",
                "AboutInteractiveBorderBrush",
                "AboutInteractiveBorderStrongBrush",
                "AboutCardBorderBrush",
                "AboutDividerBrush",
            })
            {
                Assert.Contains($"\"{key}\"", src);
            }
        }

        [Fact]
        public void Themes_define_about_palette_fallback_keys()
        {
            var root = LocateProjectRoot();
            foreach (var rel in new[] { "Themes/LightTheme.xaml", "Themes/DarkTheme.xaml" })
            {
                var src = File.ReadAllText(Path.Combine(root, rel));
                foreach (var key in new[]
                {
                    "AboutInteractiveBrush",
                    "AboutInteractiveHoverBrush",
                    "AboutInteractiveBorderBrush",
                    "AboutInteractiveBorderStrongBrush",
                    "AboutCardBorderBrush",
                    "AboutDividerBrush",
                })
                {
                    Assert.Contains($"x:Key=\"{key}\"", src);
                }
            }
        }

        [Fact]
        public void AboutViewModel_module_metric_matches_the_actual_module_count()
        {
            var vm = new AboutViewModel();
            Assert.True(vm.LeftPillars.Count > 0, "Hero metrics are missing.");
            Assert.True(int.TryParse(vm.LeftPillars[0].Value, out var count),
                "The first hero pillar should show a numeric module count.");
            Assert.Equal(vm.Modules.Count, count);
        }

        [Fact]
        public void AboutOverview_read_only_count_binding_is_explicitly_one_way()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/About/Sections/AboutOverviewSection.xaml"));

            Assert.Contains("{Binding Modules.Count, Mode=OneWay}", src);
        }

        /// <summary>
        /// Regression lock for ERR-3C0789 / ERR-38B0CA: several WPF
        /// property defaults bind TwoWay (ProgressBar.Value/Maximum,
        /// Run.Text, TextBox.Text, checkbox/combo/selection DPs).
        /// Binding them to read-only VM properties throws
        /// InvalidOperationException the moment the view lays out and
        /// takes the page down. Every such binding in the About views
        /// must carry an explicit Mode — OneWay for read-only sources.
        /// </summary>
        [Fact]
        public void About_views_bind_two_way_default_properties_with_explicit_mode()
        {
            var root = LocateProjectRoot();

            var runRe = new Regex(
                @"<Run\b[^>]*Text=""\{Binding[^}]*\}[^>]*>",
                RegexOptions.Singleline | RegexOptions.Compiled);
            var barRe = new Regex(
                @"<ProgressBar\b[^>]*>",
                RegexOptions.Singleline | RegexOptions.Compiled);

            foreach (var rel in AboutFiles)
            {
                var src = File.ReadAllText(Path.Combine(root, rel));

                foreach (Match m in runRe.Matches(src))
                {
                    Assert.Contains("Mode=OneWay", m.Value,
                        $"Run.Text is TwoWay-by-default — {rel} binds it " +
                        "to a source without an explicit mode: " +
                        m.Value.Replace("\r", " ").Replace("\n", " "));
                }

                foreach (Match m in barRe.Matches(src))
                {
                    bool bindsSource =
                        m.Value.Contains("Value=\"{Binding", StringComparison.Ordinal) ||
                        m.Value.Contains("Maximum=\"{Binding", StringComparison.Ordinal);
                    if (bindsSource)
                    {
                        Assert.Contains("Mode=OneWay", m.Value,
                            $"ProgressBar.Value/Maximum are TwoWay-by-default " +
                            $"— {rel} binds them without an explicit mode: " +
                            m.Value.Replace("\r", " ").Replace("\n", " "));
                    }
                }
            }
        }

        [Fact]
        public void AboutViewModel_selected_tab_index_is_clamped()
        {
            var vm = new AboutViewModel();

            vm.SelectedTabIndex = -1;
            Assert.Equal(0, vm.SelectedTabIndex);

            vm.SelectedTabIndex = 999;
            Assert.Equal(vm.Tabs.Count - 1, vm.SelectedTabIndex);
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
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

        /// <summary>Code-behind of every About section host — the files that
        /// are allowed to keep their own view model, but only on an INNER
        /// element (never on the UserControl root).</summary>
        private static readonly string[] AboutSectionCodeBehind =
        {
            "Views/About/Sections/AboutOverviewSection.xaml.cs",
            "Views/About/Sections/AboutLicenseCard.xaml.cs",
            "Views/About/Sections/AboutCrashReportsCard.xaml.cs",
            "Views/About/Sections/ModulesSection.xaml.cs",
            "Views/About/Sections/DeveloperSection.xaml.cs",
            "Views/About/Sections/SystemSection.xaml.cs",
            "Views/About/Sections/DiagnosticsSection.xaml.cs",
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

        // ══════════════════════════════════════════════════════════════
        //  2026-09-15 — "the License tab is STILL empty" regression locks
        //
        //  Root cause: AboutLicenseCard assigned LicenseStatusViewModel
        //  .Instance to its ROOT DataContext. The shell switched sections
        //  with an inline <X.Style> DataTrigger bound to
        //  {Binding SelectedTabIndex}; a binding on an element resolves
        //  against THAT element's DataContext, so the trigger asked a
        //  license VM for SelectedTabIndex, never fired, and the License
        //  section sat at its Collapsed default for ever — a blank page
        //  under a perfectly working tab strip.
        //
        //  The locks below make both halves of that failure impossible to
        //  reintroduce: the switch must resolve against the shell, and no
        //  About section may re-point its own root DataContext.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void About_section_switches_read_the_shell_datacontext()
        {
            var root = LocateProjectRoot();
            var shell = File.ReadAllText(Path.Combine(root, "Views/About/AboutView.xaml"));

            Assert.Contains("x:Name=\"AboutShell\"", shell);

            var hosts = new (string Tag, string Name, int Index)[]
            {
                ("AboutOverviewSection", "OverviewSection", 0),
                ("AboutLicenseCard", "LicenseSectionHost", 1),
                ("ModulesSection", "ModulesSectionHost", 2),
                ("DeveloperSection", "DeveloperSectionHost", 3),
                ("SystemSection", "SystemSectionHost", 4),
                ("DiagnosticsSection", "DiagnosticsSectionHost", 5),
            };

            foreach (var (tag, name, index) in hosts)
            {
                int at = shell.IndexOf($"<aboutSections:{tag} x:Name=\"{name}\"",
                    StringComparison.Ordinal);
                Assert.True(at >= 0, $"AboutView.xaml no longer hosts {tag} as {name}.");

                int end = shell.IndexOf('>', at);
                string openTag = shell.Substring(at, end - at + 1);

                Assert.Contains("ElementName=AboutShell", openTag);
                Assert.Contains("DataContext.SelectedTabIndex", openTag);
                Assert.Contains(
                    $"ConverterParameter={index}", openTag);

                // The unanchored form is what broke: on an element whose
                // DataContext is re-pointed by its own constructor it never
                // resolves. It is banned on the section hosts for good.
                Assert.DoesNotContain("Visibility=\"{Binding SelectedTabIndex", openTag);
            }
        }

        [Fact]
        public void About_views_do_not_hijack_a_hosted_section_datacontext()
        {
            var root = LocateProjectRoot();

            foreach (var rel in AboutSectionCodeBehind)
            {
                var path = Path.Combine(root, rel);
                Assert.True(File.Exists(path), $"Missing: {rel}");

                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;

                    // `DataContext = …` (no receiver) targets the UserControl
                    // ROOT and re-points every binding a host declared on it.
                    Assert.False(
                        Regex.IsMatch(trimmed, @"^DataContext\s*="),
                        $"{rel} assigns the ROOT DataContext ({trimmed.Trim()}). " +
                        "A hosted section must keep its own VM on an inner " +
                        "element (see AboutLicenseCard.CardRoot) — assigning the " +
                        "root silently re-targets the host's bindings.");
                }
            }

            // …and the License card keeps its VM on the inner container.
            var license = File.ReadAllText(Path.Combine(root,
                "Views/About/Sections/AboutLicenseCard.xaml.cs"));
            Assert.Contains("CardRoot.DataContext = vm;", license);

            var licenseXaml = File.ReadAllText(Path.Combine(root,
                "Views/About/Sections/AboutLicenseCard.xaml"));
            Assert.Contains("x:Name=\"CardRoot\"", licenseXaml);
        }

        // ══════════════════════════════════════════════════════════════
        //  2026-09-15 — typography contract ("proper bold fonts")
        //
        //  The module used to carry 20 hard-coded font sizes, half of them
        //  fractional (13.5 / 12.5 / 11.5 / 10.5). Under
        //  TextFormattingMode="Display" a fractional size is hinted at a
        //  half-pixel grid, which makes the bold cuts look muddy, and 97
        //  hand-set weights (68 Bold) meant everything shouted at once, so
        //  nothing read as emphasised. Sizes now come from the About type
        //  scale and weights from the ONE global weight scale.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void About_views_use_the_about_type_scale_and_the_global_weights()
        {
            var root = LocateProjectRoot();

            var literalSize = new Regex(@"FontSize=""[0-9]", RegexOptions.Compiled);
            var literalWeight = new Regex(
                @"FontWeight=""(Bold|SemiBold|Normal|Light|Medium)""", RegexOptions.Compiled);
            var fractional = new Regex(@"FontSize=""[0-9]+\.[0-9]", RegexOptions.Compiled);

            foreach (var rel in AboutFiles)
            {
                var src = File.ReadAllText(Path.Combine(root, rel));

                var size = literalSize.Match(src);
                Assert.False(size.Success,
                    $"{rel} hard-codes a font size ({size.Value}) — use the " +
                    "Ios26FontSize* scale from AboutIos26Theme.xaml. " +
                    "Fractional sizes are banned outright: they hint badly " +
                    "under TextFormattingMode=Display.");

                var fraction = fractional.Match(src);
                Assert.False(fraction.Success,
                    $"{rel} still contains the fractional size {fraction.Value}.");

                var weight = literalWeight.Match(src);
                Assert.False(weight.Success,
                    $"{rel} hard-codes a font weight ({weight.Value}) — use " +
                    "Weight.Regular / Weight.SemiBold / Weight.Bold.");
            }
        }

        [Fact]
        public void About_weight_hierarchy_is_disciplined()
        {
            var root = LocateProjectRoot();

            var smallSizes = new[]
            {
                "Ios26FontSizeCaption", "Ios26FontSizeLabel",
            };
            var titleSizes = new[]
            {
                "Ios26FontSizeCardTitleSm", "Ios26FontSizeCardTitle",
                "Ios26FontSizeSectionTitle", "Ios26FontSizeH2",
                "Ios26FontSizeH1", "Ios26FontSizeMetric",
                "Ios26FontSizeStat", "Ios26FontSizeDisplay",
            };

            var tag = new Regex(@"<[A-Za-z][\w.:]*\b[^>]*?>",
                RegexOptions.Singleline | RegexOptions.Compiled);
            var size = new Regex(@"FontSize=""\{DynamicResource ([^}]+)\}""", RegexOptions.Compiled);
            var weight = new Regex(@"FontWeight=""\{DynamicResource ([^}]+)\}""", RegexOptions.Compiled);

            foreach (var rel in AboutFiles)
            {
                var src = File.ReadAllText(Path.Combine(root, rel));

                foreach (Match m in tag.Matches(src))
                {
                    var s = size.Match(m.Value);
                    var w = weight.Match(m.Value);
                    if (!s.Success || !w.Success) continue;

                    if (smallSizes.Contains(s.Groups[1].Value))
                    {
                        Assert.NotEqual("Weight.Bold", w.Groups[1].Value);
                    }

                    if (titleSizes.Contains(s.Groups[1].Value))
                    {
                        Assert.Equal("Weight.Bold", w.Groups[1].Value);
                    }
                }
            }
        }

        [Fact]
        public void About_theme_defines_the_type_scale_and_whole_pixel_sizes()
        {
            var root = LocateProjectRoot();
            var theme = File.ReadAllText(Path.Combine(root,
                "Views/About/AboutIos26Theme.xaml"));

            foreach (var key in new[]
            {
                "Ios26FontSizeCaption", "Ios26FontSizeLabel", "Ios26FontSizeSmall",
                "Ios26FontSizeBody", "Ios26FontSizeValue", "Ios26FontSizeSubtitle",
                "Ios26FontSizeCardTitleSm", "Ios26FontSizeCardTitle",
                "Ios26FontSizeSectionTitle", "Ios26FontSizeGlyph", "Ios26FontSizeH2",
                "Ios26FontSizeH1", "Ios26FontSizeMetric", "Ios26FontSizeStat",
                "Ios26FontSizeDisplay",
            })
            {
                Assert.Contains($"x:Key=\"{key}\"", theme);
            }

            foreach (var style in new[]
            {
                "Ios26CardTitleStyle", "Ios26FieldLabelStyle", "Ios26FieldValueStyle",
            })
            {
                Assert.Contains($"x:Key=\"{style}\"", theme);
            }

            // Every About size token must be a whole pixel value.
            var token = new Regex(
                @"<sys:Double x:Key=""Ios26FontSize\w+"">([0-9.]+)</sys:Double>",
                RegexOptions.Compiled);
            foreach (Match m in token.Matches(theme))
            {
                Assert.DoesNotContain(".", m.Groups[1].Value);
            }
        }

        [Fact]
        public void About_license_card_field_rows_follow_the_label_value_roles()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/About/Sections/AboutLicenseCard.xaml"));

            // The detail grid = 8 label cells + 6 plain value cells (the
            // license-id and fingerprint rows are pill + copy button); labels
            // stay quiet, values carry the bold cut.
            Assert.True(
                Regex.Matches(src, "Style=\"\{StaticResource Ios26FieldLabelStyle\}\"").Count >= 8,
                "The license detail rows no longer use Ios26FieldLabelStyle.");
            Assert.True(
                Regex.Matches(src, "Style=\"\{StaticResource Ios26FieldValueStyle\}\"").Count >= 6,
                "The license detail values no longer use Ios26FieldValueStyle.");

            Assert.Contains("Ios26CardTitleStyle", src);
            Assert.False(new Regex(@"FontSize=\"[0-9]").IsMatch(src),
                "AboutLicenseCard.xaml hard-codes a font size again.");
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
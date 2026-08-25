// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/JobOrder/JobOrderProductionRedesignTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Job Order — Production panel redesign (senior audit — Aug 2026)
//
// Hermetic source-file tests that lock in the Job Order detail unification.
// They do NOT run WPF, a DB, or touch the filesystem beyond reading the
// XAML relative to the project root (same LocateProjectRoot walk used by
// the Settings coverage tests):
//
//   1. The JobOrderDetailView.xaml + both sections + styles all parse.
//   2. JoSectionStyles.xaml exposes the new compact tile styles.
//   3. The production panel binds to every ViewModel property it used to
//      (13 production fields) — no binding dropped in the tile rewrite.
//   4. The action buttons (Create DN / PDF / Excel / CSV / Save / Back /
//      Master DN) moved UP to the detail header action bar, so the
//      production panel no longer owns them (the "too much space for
//      small buttons" fix).
//   5. The product form is a compact tile grid (WrapPanel + tiles) host
//      above the glass-panels grid — production appears before the grid.
//   6. No element sets the same property both inline and as a property-
//      element child (XLS0501 / MC3024 anti-pattern).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.JobOrder
{
    public class JobOrderProductionRedesignTests
    {
        private const string DetailPath    = "Views/JobOrder/JobOrderDetail/JobOrderDetailView.xaml";
        private const string SectionPath   = "Views/JobOrder/JobOrderDetail/Sections/JoProductionSection.xaml";
        private const string OverviewPath  = "Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml";
        private const string StylesPath    = "Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml";

        private static readonly string[] ProductionBindings =
        {
            "StatusOptions", "SelectedStatus", "PriorityOptions", "SelectedPriority",
            "DeliveryDate", "SiteDeliveryContact", "SiteDeliveryPhone",
            "SiteDeliveryAddress", "AssignedTo", "CuttingInstructions",
            "DrawingReference", "InternalNotes", "LastEditedDisplay",
        };

        // Every action button must appear in the detail HEADER, not the
        // production panel. If any of these stop binding in the header,
        // the user cannot save/export/create a DN without scrolling.
        private static readonly string[] HeaderActions =
        {
            "BackToListCommand", "CreateDeliveryNoteCommand", "OpenMasterDnCommand",
            "ExportPdfCommand", "ExportExcelCommand", "ExportCsvCommand", "SaveCommand",
        };

        [Fact]
        public void Detail_view_sections_and_styles_parse()
        {
            var root = LocateProjectRoot();
            foreach (var rel in new[] { DetailPath, SectionPath, OverviewPath, StylesPath })
            {
                var path = Path.Combine(root, rel);
                Assert.True(File.Exists(path), $"Missing: {rel}");
                var xdoc = new XmlDocument();
                xdoc.Load(path);
            }
        }

        [Fact]
        public void Section_styles_expose_the_compact_form_tiles()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, StylesPath));
            foreach (var key in new[]
            {
                "JoFieldTileStyle", "JoFieldTitleStyle", "JoCompactLabelStyle",
                "JoCompactInputStyle", "JoCompactMultiInputStyle",
                "JoCompactComboStyle", "JoCompactDateStyle",
                "JoFormCardStyle", "JoActionBarStyle",
            })
            {
                Assert.True(src.Contains($"x:Key=\"{key}\""),
                    $"JoSectionStyles.xaml is missing the '{key}' Style.");
            }
        }

        [Theory]
        [InlineData("StatusOptions")]
        [InlineData("SelectedStatus")]
        [InlineData("PriorityOptions")]
        [InlineData("SelectedPriority")]
        [InlineData("DeliveryDate")]
        [InlineData("SiteDeliveryContact")]
        [InlineData("SiteDeliveryPhone")]
        [InlineData("SiteDeliveryAddress")]
        [InlineData("AssignedTo")]
        [InlineData("CuttingInstructions")]
        [InlineData("DrawingReference")]
        [InlineData("InternalNotes")]
        [InlineData("LastEditedDisplay")]
        public void Production_section_still_binds_to(string binding)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, SectionPath));
            var pattern = @"\{Binding\s+(?:Path\s*=\s*)?" + Regex.Escape(binding) + @"\s*[,}\s]";
            Assert.True(Regex.IsMatch(src, pattern),
                $"Binding '{binding}' vanished from JoProductionSection.xaml. " +
                "The tile redesign must not drop a ViewModel binding.");
        }

        [Theory]
        [InlineData("BackToListCommand")]
        [InlineData("CreateDeliveryNoteCommand")]
        [InlineData("OpenMasterDnCommand")]
        [InlineData("ExportPdfCommand")]
        [InlineData("ExportExcelCommand")]
        [InlineData("ExportCsvCommand")]
        [InlineData("SaveCommand")]
        public void Action_buttons_live_in_the_header_action_bar(string command)
        {
            var root = LocateProjectRoot();
            var detail = File.ReadAllText(Path.Combine(root, DetailPath));
            var pattern = @"\{Binding\s+(?:Path\s*=\s*)?" + Regex.Escape(command) + @"\s*[,}\s]";

            // The action bound in the header:
            Assert.True(Regex.IsMatch(detail, pattern),
                $"{command} no longer binds in JobOrderDetailView.xaml. " +
                "Action buttons were supposed to move UP into the header.");

            // And the production panel must NOT own the toolbar anymore:
            var prod = File.ReadAllText(Path.Combine(root, SectionPath));
            Assert.False(Regex.IsMatch(prod, pattern),
                $"{command} still binds inside JoProductionSection.xaml. " +
                "Create DN / PDF / Excel / CSV / Save must live in the header, not the panel.");
        }

        [Fact]
        public void Production_panel_is_compact_tiles_and_renders_before_the_grid()
        {
            var root = LocateProjectRoot();
            var prod = File.ReadAllText(Path.Combine(root, SectionPath));
            var overview = File.ReadAllText(Path.Combine(root, OverviewPath));

            // Compact tile grid, not full-width stacked cards.
            Assert.Contains("<WrapPanel", prod);
            Assert.Contains("JoFieldTileStyle", prod);
            Assert.Contains("JoActionBarStyle", prod);   // still available (harmless)

            // The full-width action row is GONE from the panel.
            Assert.DoesNotContain("DockPanel.Dock=\"Right\"", prod);

            // The production form is hosted INSIDE the overview page, above
            // the glass-panels identity strip + grid.
            Assert.Contains("sections:JoProductionSection", overview);
            Assert.Contains("GLASS PANELS", overview);
            Assert.True(overview.IndexOf("sections:JoProductionSection") <
                        overview.IndexOf("GLASS PANELS"),
                "The production form must render ABOVE the glass-panels grid.");
        }

        [Fact]
        public void No_job_order_xaml_sets_the_same_property_twice()
        {
            var root = LocateProjectRoot();
            var failures = new List<string>();

            foreach (var rel in new[] { DetailPath, SectionPath, OverviewPath, StylesPath })
            {
                var path = Path.Combine(root, rel);
                var xdoc = new XmlDocument();
                xdoc.Load(path);
                if (xdoc.DocumentElement != null)
                    WalkForDoubleSet(xdoc.DocumentElement, rel, failures);
            }

            Assert.True(failures.Count == 0,
                "XAML property-set-twice regression (XLS0501 / MC3024):" +
                Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        private static void WalkForDoubleSet(
            XmlElement elem, string file, List<string> failures)
        {
            var localName = elem.LocalName;
            foreach (XmlNode child in elem.ChildNodes)
            {
                if (child is not XmlElement ce) continue;
                var cname = ce.LocalName;
                var idx = cname.IndexOf('.');
                if (idx <= 0) continue;
                var owner = cname.Substring(0, idx);
                var prop = cname.Substring(idx + 1);
                if (!string.Equals(owner, localName, StringComparison.Ordinal)) continue;
                if (elem.HasAttribute(prop))
                {
                    failures.Add(
                        $"{file}: <{localName}> sets '{prop}' both inline and " +
                        $"as <{localName}.{prop}> — remove the inline attribute.");
                }
            }
            foreach (XmlNode child in elem.ChildNodes)
                if (child is XmlElement ce)
                    WalkForDoubleSet(ce, file, failures);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

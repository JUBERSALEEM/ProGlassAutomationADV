// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/JobOrder/JobOrderProductionRedesignTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Job Order — Production panel redesign (senior audit — Aug 2026)
//
// Hermetic source-file tests that lock in the JoProductionSection redesign.
// They do NOT run WPF, a DB, or touch the filesystem beyond reading the
// XAML relative to the project root (same LocateProjectRoot walk used by
// the Settings coverage tests):
//
//   1. JoProductionSection.xaml and JoSectionStyles.xaml still parse.
//   2. JoSectionStyles.xaml exposes the new JoFormCardStyle / JoActionBarStyle.
//   3. The production panel still binds to all 19 ViewModel properties /
//      commands it bound to before the redesign.
//   4. No element sets the same property both inline and as a property-
//      element child (XLS0501 / MC3024 anti-pattern).
//   5. The panel actually uses the grouped-card layout and a compact
//      action bar (so the "too much space for small buttons" fix sticks).
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
        private const string SectionPath = "Views/JobOrder/JobOrderDetail/Sections/JoProductionSection.xaml";
        private const string StylesPath  = "Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml";

        private static readonly string[] ExpectedBindings =
        {
            "StatusOptions", "SelectedStatus", "PriorityOptions", "SelectedPriority",
            "DeliveryDate", "SiteDeliveryContact", "SiteDeliveryPhone",
            "SiteDeliveryAddress", "AssignedTo", "CuttingInstructions",
            "DrawingReference", "InternalNotes", "LastEditedDisplay",
            "CreateDeliveryNoteCommand", "BackToListCommand", "ExportPdfCommand",
            "ExportExcelCommand", "ExportCsvCommand", "SaveCommand",
        };

        [Fact]
        public void Production_section_and_styles_parse()
        {
            var root = LocateProjectRoot();
            foreach (var rel in new[] { SectionPath, StylesPath })
            {
                var path = Path.Combine(root, rel);
                Assert.True(File.Exists(path), $"Missing: {rel}");
                var xdoc = new XmlDocument();
                xdoc.Load(path);
            }
        }

        [Fact]
        public void Section_styles_expose_the_production_form_cards()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, StylesPath));
            Assert.Contains("x:Key=\"JoFormCardStyle\"", src);
            Assert.Contains("x:Key=\"JoActionBarStyle\"", src);
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
        [InlineData("CreateDeliveryNoteCommand")]
        [InlineData("BackToListCommand")]
        [InlineData("ExportPdfCommand")]
        [InlineData("ExportExcelCommand")]
        [InlineData("ExportCsvCommand")]
        [InlineData("SaveCommand")]
        public void Production_section_still_binds_to(string binding)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, SectionPath));
            var pattern = @"\{Binding\s+(?:Path\s*=\s*)?" + Regex.Escape(binding) + @"\s*[,}\s]";
            Assert.True(Regex.IsMatch(src, pattern),
                $"Binding '{binding}' vanished from JoProductionSection.xaml. " +
                "The redesign must not drop a ViewModel binding.");
        }

        [Fact]
        public void Production_section_uses_the_grouped_field_layout()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, SectionPath));

            Assert.Contains("JoFormCardStyle", src);
            Assert.Contains("JoActionBarStyle", src);
            // Two-column reflow grid appears in the form cards.
            Assert.Contains("ColumnDefinition Width=\"*\"", src);
            // "small buttons" fix: the action bar docks the primary actions
            // to the right rather than scattering them across the width.
            Assert.Contains("DockPanel.Dock=\"Right\"", src);
            Assert.Contains("Content=\"CREATE DN\"", src);
        }

        [Fact]
        public void No_production_xaml_sets_the_same_property_twice()
        {
            var root = LocateProjectRoot();
            var failures = new List<string>();

            foreach (var rel in new[] { SectionPath, StylesPath })
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

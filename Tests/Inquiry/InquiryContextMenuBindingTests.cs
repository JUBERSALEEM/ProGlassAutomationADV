// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Inquiry/InquiryContextMenuBindingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Source-file regression locks for the Inquiry register's right-click
// menus. ContextMenu lives in a Popup outside the normal visual tree, so
// ElementName=Root bindings inside it are fragile and were the reason the
// entire right-click surface looked alive but executed nothing.
//
// These tests keep the fixed binding contract in place:
//   • row context menu = row on PlacementTarget.DataContext,
//   • InquiryListViewModel = row Border.Tag,
//   • stage context menus = SelectedInquiry from PlacementTarget.DataContext,
//   • stage "Open" buttons in the rail remain normal visual-tree bindings.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public sealed class InquiryContextMenuBindingTests
    {
        private const string XamlPath = "Views/Inquiry/InquiryListView.xaml";

        [Fact]
        public void Inquiry_list_xaml_parses_cleanly()
        {
            var root = LocateProjectRoot();
            var path = Path.Combine(root, XamlPath);
            Assert.True(File.Exists(path), $"Missing: {XamlPath}");
            var xdoc = new XmlDocument();
            xdoc.Load(path);
        }

        [Fact]
        public void Row_context_menu_binds_to_the_right_clicked_row_and_vm()
        {
            var src = ReadXaml();

            Assert.Contains("Tag=\"{Binding DataContext, ElementName=Root}\"", src);
            Assert.Contains("DataContext=\"{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}\"", src);

            foreach (var command in new[]
            {
                "EditInquiryRowCommand",
                "ConvertToPiRowCommand",
                "PrepareRowCommand",
                "SendRowCommand",
                "ConfirmRowCommand",
                "HoldRowCommand",
                "CancelRowCommand",
                "SetUrgentPriorityCommand",
                "SetHighPriorityCommand",
                "SetNormalPriorityCommand",
                "SetLowPriorityCommand",
                "DuplicateInquiryRowCommand",
                "DeleteInquiryRowCommand",
                "CopyInquiryNumberCommand",
                "CopySummaryCommand",
                "OpenAttachmentsFolderCommand",
            })
            {
                Assert.Contains($"PlacementTarget.Tag.{command}", src);
            }
        }

        [Fact]
        public void Stage_context_menus_bind_through_placement_target_instead_of_element_name()
        {
            var src = ReadXaml();

            // Five downstream-stage context menus: PI / JO / DN / TI / STMT.
            Assert.Equal(5,
                Count(src,
                    "DataContext=\"{Binding PlacementTarget.DataContext.SelectedInquiry, RelativeSource={RelativeSource Self}}\""));

            foreach (var command in new[]
            {
                "OpenProformaForRowCommand",
                "CopyProformaNumberCommand",
                "OpenJobOrderForRowCommand",
                "CopyJobOrderNumberCommand",
                "OpenDeliveryNoteForRowCommand",
                "CopyDeliveryNoteNumberCommand",
                "OpenTaxInvoiceForRowCommand",
                "CopyTaxInvoiceNumberCommand",
                "OpenStatementForRowCommand",
                "OpenWorkspaceRowCommand",
            })
            {
                Assert.Contains($"PlacementTarget.DataContext.{command}", src);
            }
        }

        [Fact]
        public void Stage_open_buttons_stay_in_the_visual_tree_binding_path()
        {
            var src = ReadXaml();

            foreach (var command in new[]
            {
                "OpenProformaForRowCommand",
                "OpenJobOrderForRowCommand",
                "OpenDeliveryNoteForRowCommand",
                "OpenTaxInvoiceForRowCommand",
                "OpenStatementForRowCommand",
            })
            {
                Assert.Contains($"ElementName=Root, Path=DataContext.{command}", src);
            }
        }

        private static int Count(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        private static string ReadXaml() =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(), XamlPath));

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Inquiry", "InquiryListView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

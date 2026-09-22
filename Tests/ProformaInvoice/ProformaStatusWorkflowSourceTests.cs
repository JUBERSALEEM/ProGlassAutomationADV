using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Tests.TestSupport;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class ProformaStatusWorkflowSourceTests
    {
        [Fact]
        public void Step7_save_button_is_save_only_not_save_draft()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Views", "ProformaInvoice", "Step7PrintPreview", "PrintPreviewView.xaml"));

            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace key = "http://schemas.microsoft.com/winfx/2006/xaml";
            var save = XDocument.Parse(xaml).Descendants(wpf + "Button").Single(b =>
                (string?)b.Attribute("Command") == "{Binding PrintableInvoice.SaveInvoiceCommand}");
            Assert.Equal("{DynamicResource ProformaInvoice.SAVE}", (string?)save.Attribute("Content"));
            Assert.Equal("{DynamicResource ProformaInvoice.SaveInvoiceConfirmationHappensStep8}",
                (string?)save.Attribute("ToolTip"));
            var english = XDocument.Load(Path.Combine(root, "Themes", "Languages", "en-US.xaml"));
            string Label(string name) => english.Root!.Elements()
                .Single(e => (string?)e.Attribute(key + "Key") == name).Value;
            Assert.Equal("SAVE ONLY", Label("ProformaInvoice.SAVE"));
            Assert.Contains("Confirmation happens through Step 8 attachments",
                Label("ProformaInvoice.SaveInvoiceConfirmationHappensStep8"));
        }

        [Fact]
        public void Mapper_and_snapshot_do_not_promote_printable_status_into_workflow_confirmation()
        {
            string root = LocateProjectRoot();
            string mapper = SourceFamily.Read(root, Path.Combine(
                "Services", "ProformaInvoice", "ProformaInvoiceRepository", "ProformaInvoiceMapper.cs"));
            string snapshot = File.ReadAllText(Path.Combine(root,
                "Models", "ProformaInvoice", "SavedInvoice", "ProformaInvoiceSnapshot.cs"));
            string printable = SourceFamily.Read(root, Path.Combine(
                "ViewModels", "ProformaInvoice", "Step7PrintPreview", "PrintableInvoice", "PrintableInvoiceViewModel.cs"));
            string printableXaml = File.ReadAllText(Path.Combine(root,
                "Views", "ProformaInvoice", "Step7PrintPreview", "PrintableInvoice", "PrintableInvoiceView.xaml"));

            Assert.Contains("string effectiveStatus = company.ProformaStatus ?? \"Draft\";", mapper);
            Assert.DoesNotContain("printable?.PreparedByStatus", mapper);
            Assert.Contains("PreparedByStatus = company.ProformaStatus ?? \"\"", mapper);
            Assert.Contains("printable.PreparedByStatus = snap.Company?.ProformaStatus ?? \"Draft\";", mapper);
            Assert.Contains("public string PreparedByStatus", printable);
            Assert.Contains("Company.ProformaStatus", printable);
            Assert.DoesNotContain("= \"APPROVED\"", snapshot);
            Assert.Contains("PreparedByStatus { get; set; } = \"\";", snapshot);
            Assert.DoesNotContain("<TextBox Text=\"{Binding PreparedByStatus", printableXaml);
            Assert.Contains("<TextBlock Text=\"{Binding PreparedByStatus, TargetNullValue=Draft}\"", printableXaml);
        }

        [Fact]
        public void Saved_list_only_shows_confirmed_when_approved_and_step8_attachment_exists()
        {
            string root = LocateProjectRoot();
            string listVm = SourceFamily.Read(root, Path.Combine(
                "ViewModels", "ProformaInvoice", "SavedInvoicesList", "SavedInvoicesListViewModel.cs"));

            Assert.Contains("LoadApprovalProofStateAsync", listVm);
            Assert.Contains("hasApprovalProof", listVm);
            Assert.Contains("DocumentListStatus.ForProforma(", listVm);
            Assert.Matches(@"DocumentListStatus\.ForProforma\(\s*e\.Status \?\? ""Draft"",\s*hasApprovalProof,", listVm);
            Assert.Contains("\"PI created\", \"PI sent\", \"PI confirmed\"", listVm);
        }

        // Exercise the shared status service actually consumed by the saved list.
        // A stored approval without Step 8 proof must not be displayed as confirmed.
        [Theory]
        [InlineData("Approved", false, "PI created", "NeutralBrush")]
        [InlineData("Approved", true, "PI confirmed", "AccentBrush")]
        [InlineData("approved", true, "PI confirmed", "AccentBrush")]
        [InlineData("Draft", true, "PI created", "NeutralBrush")]
        [InlineData("Sent", true, "PI sent", "AccentDarkBrush")]
        public void List_confirmation_requires_both_stored_approval_and_attachment(
            string storedStatus, bool hasProof, string status, string brush)
        {
            var actual = DocumentListStatus.ForProforma(storedStatus, hasProof, null);
            Assert.Equal(status, actual.Status);
            Assert.Equal(brush, actual.BrushKey);
        }

        [Fact]
        public void Save_only_path_normalizes_illegal_manual_approved_status_back_to_workflow_state()
        {
            string root = LocateProjectRoot();
            string printable = SourceFamily.Read(root, Path.Combine(
                "ViewModels", "ProformaInvoice", "Step7PrintPreview", "PrintableInvoice", "PrintableInvoiceViewModel.cs"));

            Assert.Contains("NormalizeSaveOnlyStatus", printable);
            Assert.Contains("GetByProformaIdAsync", printable);
            Assert.Contains("Company.ProformaStatus = entity.Status;", printable);
            Assert.Contains("if (string.Equals(current, \"Approved\"", printable);
            Assert.Contains("if (string.Equals(requested, \"Approved\"", printable);
            Assert.Contains("return current;", printable);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "ProformaInvoice", "ProformaInvoiceViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

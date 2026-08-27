using System;
using System.IO;
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

            Assert.Contains("SAVE ONLY", xaml);
            Assert.DoesNotContain("SAVE DRAFT", xaml);
            Assert.Contains("Confirmation happens through Step 8 attachments", xaml);
        }

        [Fact]
        public void Mapper_and_snapshot_do_not_promote_printable_status_into_workflow_confirmation()
        {
            string root = LocateProjectRoot();
            string mapper = File.ReadAllText(Path.Combine(root,
                "Services", "ProformaInvoice", "ProformaInvoiceRepository", "ProformaInvoiceMapper.cs"));
            string snapshot = File.ReadAllText(Path.Combine(root,
                "Models", "ProformaInvoice", "SavedInvoice", "ProformaInvoiceSnapshot.cs"));
            string printable = File.ReadAllText(Path.Combine(root,
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
            string listVm = File.ReadAllText(Path.Combine(root,
                "ViewModels", "ProformaInvoice", "SavedInvoicesList", "SavedInvoicesListViewModel.cs"));

            Assert.Contains("LoadApprovalProofStateAsync", listVm);
            Assert.Contains("hasApprovalProof", listVm);
            Assert.Contains("string.Equals(storedStatus, \"Approved\", StringComparison.OrdinalIgnoreCase)", listVm);
            Assert.Contains("&& hasConfirmationAttachment", listVm);
            Assert.Contains("return (\"PI confirmed\", \"AccentBrush\");", listVm);
            Assert.Contains("return (\"PI sent\", \"AccentDarkBrush\");", listVm);
            Assert.Contains("return (\"PI created\", \"NeutralBrush\");", listVm);
            Assert.Contains("\"PI created\", \"PI sent\", \"PI confirmed\"", listVm);
        }

        [Fact]
        public void Save_only_path_normalizes_illegal_manual_approved_status_back_to_workflow_state()
        {
            string root = LocateProjectRoot();
            string printable = File.ReadAllText(Path.Combine(root,
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

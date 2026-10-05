using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class TaxInvoiceHardeningSourceTests
    {
        [Fact]
        public void Repository_mutations_are_guarded_by_read_only_mode()
        {
            string root = LocateProjectRoot();
            string repo = File.ReadAllText(Path.Combine(root,
                "Services", "TaxInvoice", "Repository", "TaxInvoiceRepository.cs"));

            Assert.Contains("ReadOnlyGate.Demand(\"create a tax invoice\")", repo, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"update a tax invoice\")", repo, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a tax invoice\")", repo, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"change a tax invoice status\")", repo, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"add a tax invoice attachment\")", repo, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a tax invoice attachment\")", repo, StringComparison.Ordinal);
        }

        [Fact]
        public void Family_queries_backfill_proforma_totals_for_progress_and_balance()
        {
            string root = LocateProjectRoot();
            string repo = File.ReadAllText(Path.Combine(root,
                "Services", "TaxInvoice", "Repository", "TaxInvoiceRepository.cs"));

            Assert.Contains("PopulateProformaTotalsAsync(families)", repo, StringComparison.Ordinal);
            Assert.Contains("ResolveProformaTotalAsync(proformaId", repo, StringComparison.Ordinal);
        }

        [Fact]
        public void Family_row_viewmodel_uses_family_rollup_for_any_issued_flag()
        {
            string root = LocateProjectRoot();
            string vm = File.ReadAllText(Path.Combine(root,
                "ViewModels", "TaxInvoice", "TaxInvoiceList", "InvoiceFamilyRowViewModel.cs"));

            Assert.Contains("AnyIssued = family.HasIssuedInvoices;", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void Detail_viewmodel_enforces_read_only_mode_for_save_delete_attachment_and_pdf_export()
        {
            string root = LocateProjectRoot();
            string vm = File.ReadAllText(Path.Combine(root,
                "ViewModels", "TaxInvoice", "TaxInvoiceDetail", "TaxInvoiceDetailViewModel.cs"));

            Assert.Contains("ReadOnlyGate.Demand(\"save a tax invoice\")", vm, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a tax invoice\")", vm, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"add a tax invoice attachment\")", vm, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a tax invoice attachment\")", vm, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"export a tax invoice PDF\")", vm, StringComparison.Ordinal);
            Assert.Contains("catch (ReadOnlyModeException ex)", vm, StringComparison.Ordinal);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "TaxInvoice", "Repository", "TaxInvoiceRepository.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

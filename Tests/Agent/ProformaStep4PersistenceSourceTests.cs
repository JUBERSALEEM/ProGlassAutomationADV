using System;
using System.IO;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ProformaStep4PersistenceSourceTests
    {
        /// <summary>Reads a file plus its .PartN.cs siblings (Phase 3.8 partial families).</summary>
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
        public void Snapshot_and_mapper_include_step4_persistence_surface()
        {
            string root = LocateProjectRoot();
            string snapshot = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs"));
            string mapper = ReadFamily(root,
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.cs");

            Assert.Contains("Step4PricingSnapshot", snapshot);
            Assert.Contains("Step4PricingSections", snapshot);
            Assert.Contains("Step4SelectedSectionNumber", snapshot);
            Assert.Contains("SelectedGlassTypeCode", snapshot);
            Assert.Contains("snap.Step4Pricing = new Step4PricingSnapshot", mapper);
            Assert.Contains("CaptureSectionSnapshots", mapper);
            Assert.Contains("Step4SelectedSectionNumber", mapper);
            Assert.Contains("printable.Step4Pricing.RestoreFromSnapshot", mapper);
        }

        [Fact]
        public void Wizard_resets_step4_and_clears_per_invoice_sheet_registry()
        {
            string root = LocateProjectRoot();
            string pi = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));
            string pricing = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs");

            Assert.Contains("Step4VM.ResetForNewInvoice()", pi);
            Assert.Contains("SpecSheetRegistry.Clear()", pi);
            Assert.Contains("HasRestoredSnapshotState", pricing);
            Assert.Contains("RestoreFromSnapshot", pricing);
        }


        [Fact]
        public void Step4_view_and_vm_expose_multi_section_switching()
        {
            string root = LocateProjectRoot();
            string view = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step4PricingEstimator/PricingEstimatorView.xaml"));
            string vm = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs");
            string shell = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));
            string step5 = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs"));

            Assert.Contains("CurrentCalculationSectionLabel", view);
            Assert.Contains("AddCalculationSectionCommand", view);
            Assert.Contains("RemoveCalculationSectionCommand", view);
            Assert.Contains("SelectCalculationSection", vm);
            Assert.Contains("CalculationSectionCount", vm);
            Assert.Contains("CaptureSectionSnapshots", vm);
            Assert.Contains("GeneratedSpecName", vm);
            Assert.Contains("SyncStep5SelectionsFromStep4", shell);
            Assert.Contains("LastSyncedStep4SpecName", ReadFamily(root,
                "ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.cs"));
            Assert.Contains("EnsureSpecGroupCount", shell);
            Assert.Contains("EnsureSpecGroupCount", step5);
        }

        [Fact]
        public void Step5_preserves_saved_spec_text_and_displays_sheet_selection_summary()
        {
            string root = LocateProjectRoot();
            string specGroup = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.cs");
            string step5View = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml"));

            Assert.Contains("keepCommittedTypedText", specGroup);
            Assert.Contains("UInsertPricePerSqm = selection.UInsertPricePerSqm", specGroup);
            Assert.Contains("OuterSheetPricePerSqm = selection.OuterPricePerSqm", specGroup);
            Assert.Contains("InnerSheetPricePerSqm = selection.InnerPricePerSqm", specGroup);
            Assert.Contains("SheetSelectionSummary", specGroup);
            Assert.Contains("SheetSelectionSummary", step5View);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
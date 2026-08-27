// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OtherChargesAdvancedSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Source-level regression locks for the advanced Step 6 / Step 9 plumbing.
// They avoid booting WPF while still protecting the binding/wiring surface.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OtherChargesAdvancedSourceTests
    {
        [Fact]
        public void Step6_xaml_exposes_the_advanced_mode_toggle()
        {
            var root = LocateProjectRoot();
            var path = Path.Combine(root,
                "Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml");
            var src = File.ReadAllText(path);
            var vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/OtherChargesViewModel.cs"));
            var pi = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));

            var xdoc = new XmlDocument();
            xdoc.Load(path);

            Assert.Contains("UseAdvancedAutomation, Mode=TwoWay", src);
            Assert.Contains("Advanced auto-charge mode", src);
            Assert.Contains("AutomationModeLabel", src);
            Assert.Contains("_useAdvancedAutomation = true", vm);
            Assert.Contains("BindStep4Pricing", vm);
            Assert.Contains("public void Dispose()", vm);
            Assert.Contains("Step6VM?.Dispose()", pi);
        }

        [Fact]
        public void Step9_is_bound_to_step6_and_includes_active_step6_costs()
        {
            var root = LocateProjectRoot();
            var pi = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));
            var pnl = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"));

            Assert.Contains("Step6VM", pi);
            Assert.Contains("Step9VM.Bind(", pi);
            Assert.Contains("AppendStep6Costs", pnl);
            Assert.Contains("active Step 6 charges plus advanced inferred charges are included as cost lines", pnl);
            Assert.Contains("wastage rows are excluded", pnl);
            Assert.DoesNotContain("_otherChargesVm.WastagesSection.WastageRows", pnl);
        }


        [Fact]
        public void Wastage_view_exposes_the_optimization_formula_fields()
        {
            var root = LocateProjectRoot();
            var wastageView = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml"));
            var wastageRow = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/Step6OtherCharges/Wastages/WastageRow.cs"));

            Assert.Contains("UseOptimizationFormula", wastageView);
            Assert.Contains("FormulaPricePerSqmText", wastageView);
            Assert.Contains("OptimizationPercentText", wastageView);
            Assert.Contains("SelectedSheetWidthText", wastageView);
            Assert.Contains("SelectedSheetHeightText", wastageView);
            Assert.Contains("WASTAGE SETUP", wastageView);
            Assert.Contains("SetupToggle", wastageView);
            Assert.Contains("UTILIZED SQM", wastageView);
            Assert.Contains("SelectedSourcePricePerSqm", wastageRow);
            Assert.Contains("SelectedSheetWidthText", wastageRow);
            Assert.Contains("SetAutoFormulaPrice", wastageRow);
            Assert.Contains("ExtractColorKey", File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs")));
            Assert.Contains("ColorName = group.OuterGlassColor", File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs")));
            Assert.Contains("ColorName = group.InnerGlassColor", File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs")));
            Assert.Contains("OptimizationSummaryText", wastageRow);
            Assert.DoesNotContain("private decimal _rate = 40m;", wastageRow);
        }


        [Fact]
        public void Wastage_advanced_refresh_does_not_force_rows_on()
        {
            var root = LocateProjectRoot();
            var wastageVm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));

            Assert.Contains("IsActive = previousRow?.IsActive ?? false", wastageVm);
            Assert.DoesNotContain("IsActive = true,", wastageVm);
        }

        [Fact]
        public void Step6_predefined_rows_start_off_without_counting_as_manual_override()
        {
            var root = LocateProjectRoot();
            var vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/OtherChargesViewModel.cs"));

            Assert.Contains("row.ClearManualActiveOverride();", vm);
            Assert.DoesNotContain("IsActive = false", vm);
        }

        [Fact]
        public void Dgu_view_and_handoff_expose_u_insert_price()
        {
            var root = LocateProjectRoot();
            var dguView = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml"));
            var dguVm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersViewModel.cs"));
            var pricing = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs"));

            Assert.Contains("HasUInsertExtra", dguView);
            Assert.Contains("U-INSERT / SQM", dguView);
            Assert.Contains("UInsertPricePerSqm", dguVm);
            Assert.Contains("UInsertPricePerSqm", pricing);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step6OtherCharges", "OtherChargesView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}

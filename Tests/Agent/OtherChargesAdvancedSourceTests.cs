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
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OtherChargesAdvancedSourceTests
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
            // Step 6 wastage (outer + inner per specification) now flows into
            // Step 9 so the P&L outer/inner wastage quantities match Step 6.
            Assert.Contains("wastage costs now flow into Step 9", pnl);
            Assert.Contains("AppendWastageCostRows", pnl);
            Assert.Contains("per checked wastage source", pnl);
            Assert.DoesNotContain("wastage rows remain OUTSIDE Step 9", pnl);
            // Glass pane QTY comes from the Step 6 wastage setup SHEET QTY
            // box (outer AND inner) instead of Sheet Store rounding; the box
            // is read even when the wastage charge row is disabled.
            Assert.Contains("ResolveStep6WastageSheetQuantity", pnl);
            Assert.Contains("SHEET QTY from Step 6 wastage setup", pnl);
            Assert.Contains("A DISABLED wastage row still owns", pnl);
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
            // Same-colour outer and inner rows are told apart by the side
            // badge beside the colour.
            Assert.Contains("SideDisplay", wastageView);
            Assert.Contains("SideDisplay", wastageRow);
            var wastageVm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));
            Assert.Contains("ExtractColorKey", wastageVm);
            Assert.Contains("ResolveDguLamPaneColors", wastageVm);
            Assert.Contains("Lam Outer (W2/H2)", wastageVm);
            Assert.Contains("Lam Inner (W2/H2)", wastageVm);
            // BUG: markerless DGU spec names must not drop the inner source;
            // sources resolve the build the same way the rules do.
            Assert.Contains("ResolveEffectiveGlassType", wastageVm);
            Assert.Contains("Inner (W2/H2)", wastageVm);
            // A DGU's outer and inner glass are SEPARATE wastage charges:
            // same-colour sides must not be folded into one row, and each
            // row keeps its own setup SHEET QTY across advanced refreshes.
            Assert.Contains("WastageRowKey", wastageVm);
            Assert.Contains("NormalizeSideKey", wastageVm);
            Assert.Contains("SheetQuantity = previousRow?.SheetQuantity", wastageVm);
            Assert.Contains("OptimizationSummaryText", wastageRow);
            Assert.DoesNotContain("private decimal _rate = 40m;", wastageRow);
        }


        [Fact]
        public void Wastage_advanced_refresh_does_not_force_rows_on()
        {
            var root = LocateProjectRoot();
            var wastageVm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));
            var rules = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/OtherChargeAutomationRules.cs"));

            Assert.Contains("IsActive = previousRow?.IsActive ?? true", wastageVm);
            Assert.Contains("Lam Outer (W2/H2)", rules);
            Assert.Contains("Lam Inner (W2/H2)", rules);
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
        public void Step6_reset_and_restore_clear_live_row_subscriptions_before_rebuilding()
        {
            var root = LocateProjectRoot();
            var step6 = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/OtherChargesViewModel.cs"));
            var wastage = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));
            var custom = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionViewModel.cs"));
            var mapper = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.cs"));

            Assert.Contains("public void ClearWastageRows()", wastage);
            Assert.Contains("DetachRow(row);", wastage);
            Assert.Contains("WastagesSection.ClearWastageRows();", step6);
            Assert.Contains("otherCharges.WastagesSection.ClearWastageRows();", mapper);
            Assert.Contains("public void ClearCharges()", custom);
            Assert.Contains("DetachCharge(r);", custom);
            Assert.Contains("CustomChargesSection.ClearCharges();", step6);
            Assert.Contains("otherCharges.CustomChargesSection.ClearCharges();", mapper);
        }

        [Fact]
        public void Hybrid_metric_panel_uses_pane_factor_for_physical_pane_count()
        {
            var root = LocateProjectRoot();
            var metric = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/Step6OtherCharges/SpecGroupMetric.cs"));

            Assert.Contains("Qty * (PaneFactor <= 0 ? 1 : PaneFactor)", metric);
            Assert.DoesNotContain("IsDoubleGlass ? Qty * 2 : Qty", metric);
        }

        [Fact]
        public void Advanced_auto_charge_now_uses_spec_grammar_when_step5_type_is_stale()
        {
            var root = LocateProjectRoot();
            var rules = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/OtherChargeAutomationRules.cs"));
            var detector = File.ReadAllText(Path.Combine(root,
                "Utilities/GlassTypeDetector.cs"));
            var specGroup = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.cs");

            Assert.Contains("ResolveEffectiveGlassType", rules);
            Assert.Contains("PnLSpecParser.Spacer(spec)", rules);
            Assert.Contains("PnLSpecParser.Interlayer(spec)", rules);
            Assert.Contains("PnLSpecParser.Interlayer(specName).Success", detector);
            Assert.Contains("ResetSpecSelectionForManualGlassTypeChange", specGroup);
            Assert.Contains("SelectedGlassType = detected;", specGroup);
            // BUG: the Step 5 auto-switch must not overwrite the authoritative
            // type with SGU when a markerless multi-pane spec is committed.
            Assert.Contains("IsMultiPaneWithoutBuildMarkers", detector);
            Assert.Contains("IsMultiPaneWithoutBuildMarkers", specGroup);
            Assert.Contains("paneCount > 1", detector);
        }

        [Fact]
        public void Dgu_view_and_handoff_expose_u_insert_price()
        {
            var root = LocateProjectRoot();
            var dguView = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml"));
            var dguVm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersViewModel.cs"));
            var pricing = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs");

            Assert.Contains("HasUInsertExtra", dguView);
            Assert.Contains("U-INSERT / SQM", dguView);
            Assert.Contains("UInsertPricePerSqm", dguVm);
            Assert.Contains("UInsertPricePerSqm", pricing);
        }

        [Fact]
        public void Wastage_sheet_quantity_is_persisted_and_restored()
        {
            var root = LocateProjectRoot();
            var model = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs"));
            var mapper = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.cs"));

            Assert.Contains("public int SheetQuantity", model);
            Assert.Contains("SheetQuantity = wastage.SheetQuantity", mapper);
            Assert.Contains("SheetQuantity = wSnap.SheetQuantity", mapper);
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
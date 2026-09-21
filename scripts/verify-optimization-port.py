#!/usr/bin/env python3
"""Portable port-boundary checks. Not a substitute for the Windows build/tests."""
from pathlib import Path
import hashlib
import json
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
def read(path): return (ROOT / path).read_text(encoding="utf-8-sig")

class OptimizationPort(unittest.TestCase):
    def test_engine_preserved_except_documented_house_table_correction(self):
        manifest = json.loads(read("docs/optimization/protected-engine-manifest.json"))
        approved = manifest.get("approved_changes", {})
        self.assertEqual({"Services/Optimization/OptimizationModels.cs"}, set(approved))
        for path, expected in manifest["sha256"].items():
            if path in approved:
                expected = approved[path]["sha256"]
            with self.subTest(path=path):
                self.assertEqual(expected, hashlib.sha256((ROOT / path).read_bytes()).hexdigest())

    def test_all_ported_views_are_valid_xml(self):
        for path in ("Views/Optimization", "Views/ProformaInvoice/Step4PricingEstimator",
                     "Views/ProformaInvoice/Step5DataGrid"):
            for file in (ROOT / path).rglob("*.xaml"):
                with self.subTest(file=file): ET.parse(file)

    def test_large_run_dispatch_and_limit(self):
        run = read("ViewModels/Optimization/OptimizationViewModel.Run.cs")
        self.assertIn("GuillotinePacker.PackOptimizePartitioned", run)
        self.assertIn("GuillotinePacker.PackOptimize(", run)
        self.assertIn("GuillotinePacker.PackQuick", run)
        self.assertIn("VerifyComprehensive(result, groupPieces, groupMargins)", run)
        self.assertIn("MaxExpandedParts = 2_000_000", read("ViewModels/Optimization/OptimizationViewModel.Margins.cs"))
        self.assertNotRegex(run, r"(?:Take|Count\s*>).*20_?000")

    def test_no_synthetic_jobs_or_other_glass_fallback(self):
        jobs = read("ViewModels/Optimization/OptimizationViewModel.Jobs.cs")
        parts = read("ViewModels/Optimization/OptimizationViewModel.Parts.cs")
        run = read("ViewModels/Optimization/OptimizationViewModel.Run.cs")
        self.assertNotIn("AddSyntheticSpecFromStep5Rows", jobs)
        self.assertNotIn(".Take(100)", jobs)
        self.assertNotIn('("11",1050,2127,2)', parts)
        self.assertNotIn('packed on other specs', run)

    def test_feed_and_saved_schema_compatibility(self):
        feed = read("ViewModels/Optimization/OptimizationViewModel.Feed.cs")
        session = read("ViewModels/Optimization/OptimizationViewModel.Session.cs")
        for key in ("runId: _lastRunId", "inputFingerprint: _lastInputFingerprint"):
            self.assertIn(key, feed)
        for key in ("OptimizationSessionIdentity.Serialize", "OptimizationSessionIdentity.Parse",
                    "PricePerSqm = st.PricePerSqm", "SpecNumber = st.SpecNumber"):
            self.assertIn(key, session)

    def test_live_and_restored_group_initializers_match_result_contract(self):
        # Regression for CS0117: the source UI dropped StockSize while the
        # retained session reader still initialized it. Check all assigned
        # members in both projections, not just whether their syntax parses.
        run = read("ViewModels/Optimization/OptimizationViewModel.Run.cs")
        session = read("ViewModels/Optimization/OptimizationViewModel.Session.cs")
        model = run.split("public sealed class CuttingGroupResultVm", 1)[1].split(
            "public ObservableCollection<CuttingGroupResultVm>", 1)[0]
        members = set(re.findall(r"public\s+\w+\s+(\w+)\s*(?:\{|=>)", model))
        self.assertIn("StockSize", members)
        for name, source in (("live", run), ("restored", session)):
            with self.subTest(projection=name):
                initializers = re.findall(
                    r"new CuttingGroupResultVm\s*\{(.*?)\n\s*\}", source, re.S)
                self.assertTrue(initializers, f"Missing {name} result projection")
                for initializer in initializers:
                    assigned = set(re.findall(r"^\s*(\w+)\s*=", initializer, re.M))
                    self.assertTrue(assigned <= members,
                                    f"Undeclared result members: {assigned - members}")
                    self.assertIn("StockSize", assigned)

    def test_integer_import_parser_assigns_its_out_value(self):
        source = read("ViewModels/Optimization/OptimizationViewModel.Parts.cs")
        parser = source.split("private static bool ParsePositiveInt", 1)[1].split(
            "public ObservableCollection<PieceEntryVm>", 1)[0]
        # The previous helper returned true for integers but left value = 0.
        self.assertRegex(parser, r"int\.TryParse\(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value\)\)\s*return value > 0;")
        self.assertNotIn("out int direct", parser)

    def test_job_selection_import_button_and_double_clicks_are_wired(self):
        ns = {"w": "http://schemas.microsoft.com/winfx/2006/xaml/presentation"}
        xname = "{http://schemas.microsoft.com/winfx/2006/xaml}Name"
        view = ET.fromstring(read("Views/Optimization/Tabs/JobsTabView.xaml"))
        picker = next(node for node in view.iter() if node.get(xname) == "JobPicker")
        self.assertEqual("{Binding SelectedJob, Mode=TwoWay}", picker.get("SelectedItem"))
        button = next(node for node in view.iter() if node.get(xname) == "AddSelectedPanesButton")
        self.assertEqual("{Binding AddSelectedPanesCommand}", button.get("Command"))
        self.assertEqual("{Binding AddPanesButtonLabel}", button.get("Content"))
        handlers = {node.get("MouseLeftButtonDown") for node in view.findall(".//w:Border", ns)}
        code = read("Views/Optimization/Tabs/JobsTabView.xaml.cs")
        for handler in ("PaneRow_MouseLeftButtonDown", "CuttingRow_MouseLeftButtonDown"):
            self.assertIn(handler, handlers)
            self.assertIn("private void " + handler, code)
        for command in ("ImportPaneSizesCommand", "OpenPieceSizesCommand"):
            self.assertIn(command + ".CanExecute", code)
            self.assertIn(command + ".Execute", code)
        jobs = read("ViewModels/Optimization/OptimizationViewModel.Jobs.cs")
        self.assertIn("value.IsSelected = true;", jobs)
        self.assertIn("options.Add(BuildJobOption(", jobs)
        self.assertIn("ProductionDistributionEngine.Distribute(jobOrderId, invoice)", jobs)
        self.assertIn("ImportPaneLayers(PaneChecklist.Where", jobs)

    def test_sizes_refresh_when_filter_changes_or_is_removed(self):
        sizes = read("ViewModels/Optimization/OptimizationViewModel.Sizes.cs")
        setter = sizes.split("public string SelectedCuttingGroup", 1)[1].split(
            "public const string AllCuttingGroups", 1)[0]
        self.assertIn("RefreshSizeViews();", setter)
        source = read("ViewModels/Optimization/OptimizationViewModel.Parts.cs")
        derive = source.split("private void RefreshDerived()", 1)[1].split(
            "private void RefreshSizeViews()", 1)[0]
        self.assertLess(derive.index("RebuildCuttingGroups();"), derive.index("RefreshSizeViews();"))

    def test_inactive_tabs_collapse_at_usercontrol_boundary(self):
        indices = {"Jobs": 0, "Sizes": 1, "Stocks": 2, "Settings": 3,
                   "Summary": 4, "TwoDView": 5, "Report": 6, "CutList": 7}
        for name, index in indices.items():
            with self.subTest(tab=name):
                view = ET.fromstring(read(f"Views/Optimization/Tabs/{name}TabView.xaml"))
                self.assertEqual("{Binding SelectedTabIndex, Converter={StaticResource IndexEqualsConverter}, ConverterParameter="
                                 + str(index) + "}", view.get("Visibility"))

    def test_full_spec_fingerprint_sync_and_programmatic_rate_guard(self):
        shell = read("ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs")
        handler = shell.split("private void OnStep4PropertyChanged", 1)[1].split("private void SyncStep5BaseRatesFromStep4", 1)[0]
        self.assertIn("PricingEstimatorViewModel.CurrentSpecFingerprint", handler)
        self.assertIn("Step4VM.IsRestoringPricingSnapshot", handler)
        self.assertIn("SyncStep5SelectionsFromStep4();", handler)
        self.assertIn("if (crossesStep4Boundary || leavesStep4)", shell)
        self.assertIn("if (CurrentStepNumber == 4) PrepareStep5FromStep4();", shell)
        group = read("ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs")
        rate = group.split("public void ApplyStep4RateSnapshot", 1)[1].split("private decimal _step4EstimatedRatePerSqm", 1)[0]
        self.assertLess(rate.index("_isSynchronizingFromStep4 = true"), rate.index("BaseRate ="))
        self.assertIn("finally { _isSynchronizingFromStep4 = wasSynchronizing; }", rate)
        self.assertIn("!HasManualStep5BaseRateOverride", rate)
        self.assertNotIn("SelectedGlassType =", rate)

    def test_five_stages_breakout_and_spec_thickness_controls_are_reachable(self):
        settings = read("Views/Optimization/Tabs/SettingsTabView.xaml")
        for binding in ("StageStrategies", "SelectedStageStrategy", "BreakoutOptions", "SelectedBreakoutOption", "MarginHint", "ThicknessSourceHint"):
            self.assertIn("{Binding " + binding + "}", settings)
        vm = read("ViewModels/Optimization/OptimizationViewModel.Margins.cs")
        for stage in range(1, 6): self.assertIn(f'"Stage {stage}:', vm)
        self.assertIn("SyncStageStrategyFromEffort(value)", vm)
        self.assertIn("SyncEffortFromStageStrategy(value)", vm)
        for name in ("Parts", "Sizes"):
            self.assertIn("SyncThicknessFromInputs();", read(f"ViewModels/Optimization/OptimizationViewModel.{name}.cs"))
        model = read("Services/Optimization/OptimizationModels.cs")
        thick = model.split("case ThicknessBucket.Thick:", 1)[1].split("case ThicknessBucket.Medium:", 1)[0]
        self.assertIn("left = right = bottom = 50;", thick)
        self.assertIn("top = 0;", thick)

    def test_sgu_extras_and_estimate_tooltip_roundtrip_wiring(self):
        step4 = read("ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part1.cs")
        for field in ("PanelExtra", "UseCustomPanelExtra", "CustomPanelExtra"):
            self.assertIn(f"{field} = SguVM.{field}", step4)
            self.assertIn(f"SguVM.{field} = snapshot.Sgu.{field}", step4)
        mapper = read("Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs")
        for field in ("PanelExtra", "UseCustomPanelExtra", "CustomPanelExtra"):
            self.assertIn(f"{field} = step4.SguVM.{field}", mapper)
        self.assertIn("Step4EstimatedRatePerSqm = group.Step4EstimatedRatePerSqm", mapper)
        self.assertIn("Step4EstimatedRatePerSqm = gSnap.Step4EstimatedRatePerSqm", mapper)
        view = read("Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml")
        self.assertIn('Text="{Binding SheetSelectionSummary}" ToolTip="{Binding Step4RateTooltip}"', view)

    def test_headless_host_and_rate_only_sync(self):
        composer = read("ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs")
        self.assertIn("OptimizationViewModel(isHeadlessHost: true)", composer)
        self.assertIn("SyncStep5BaseRatesFromStep4();", composer)
        method = composer.split("private void SyncStep5BaseRatesFromStep4()", 1)[1].split("private void OnStep2PropertyChanged", 1)[0]
        self.assertNotIn("ApplyStep4PricingSnapshot", method)
        self.assertNotRegex(method, r"group\.(SelectedGlassType|SelectedSpecName)\s*=")

if __name__ == "__main__": unittest.main(verbosity=2)

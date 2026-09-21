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
    def test_engine_is_byte_for_byte_preserved(self):
        manifest = json.loads(read("docs/optimization/protected-engine-manifest.json"))
        for path, expected in manifest["sha256"].items():
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

    def test_headless_host_and_rate_only_sync(self):
        composer = read("ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs")
        self.assertIn("OptimizationViewModel(isHeadlessHost: true)", composer)
        self.assertIn("SyncStep5BaseRatesFromStep4();", composer)
        method = composer.split("private void SyncStep5BaseRatesFromStep4()", 1)[1].split("private void OnStep2PropertyChanged", 1)[0]
        self.assertNotIn("ApplyStep4PricingSnapshot", method)
        self.assertNotRegex(method, r"group\.(SelectedGlassType|SelectedSpecName)\s*=")

if __name__ == "__main__": unittest.main(verbosity=2)

#!/usr/bin/env python3
"""Source regression checks for the production Indigo Overview (not a WPF build).
Run from any directory: python3 scripts/verify-indigo-overview.py
No database, sample records, screenshot generator or third-party dependency.
"""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
VIEW = ROOT / "Views/Dashboard/Overview/OverviewDashboardView.xaml"
SHELL = ROOT / "Views/Dashboard/DashboardView.xaml"
RESOURCES = VIEW.with_name("IndigoStudioResources.xaml")
NS = {"p": "http://schemas.microsoft.com/winfx/2006/xaml/presentation",
      "x": "http://schemas.microsoft.com/winfx/2006/xaml"}
KEY = "{" + NS["x"] + "}Key"
BINDING = re.compile(r"\{Binding (?:Path=)?([A-Za-z][A-Za-z0-9_.]*)")


def source(path):
    return path.read_text(encoding="utf-8-sig")


def template(key):
    return next(e for e in ET.parse(RESOURCES).getroot() if e.get(KEY) == key)


class IndigoOverviewContracts(unittest.TestCase):
    def test_all_original_page_bindings_retained(self):
        # Contract recorded before redesign at 61a9ce1; no data source deleted.
        expected = set("""ChartTitle DeliveryMixSlices DeliveryTiles ErrorMessage
            HasError HasOverdueJobs InProductionDisplay InventoryTiles IsLoading
            LastRefreshed MonthlySalesBars NewInvoiceCommand OverdueDisplay
            PendingPcsDisplay ProductionTiles PulseDisplay PulseHint PulseSpark
            RecentActivity SalesTiles ViewDeliveriesCommand ViewJobsCommand
            WorkflowStages""".split())
        self.assertFalse(expected - set(BINDING.findall(source(VIEW))))

    def test_shell_bindings_and_selection_identity_retained(self):
        text = source(SHELL)
        expected = set("""ActivePage ActivePage.LastRefreshed ActivePage.TabSubtitle
            CurrentUserDisplay DataContext.SelectTabCommand IsMouseOver Pages
            TabSubtitle TabTitle""".split())
        self.assertFalse(expected - set(BINDING.findall(text)))
        self.assertIn('CommandParameter="{Binding}"', text)
        self.assertIn('Converter="{StaticResource ReferenceEqualityConverter}"', text)
        self.assertIn('Path="DataContext.ActivePage"', text)
        self.assertIn('<Binding Path="."/>', text)
        typed = [e for e in ET.parse(SHELL).iter() if e.get("DataType")]
        self.assertEqual(7, len(typed))

    def test_all_17_kpis_and_five_stages_kept(self):
        view = ET.parse(VIEW)
        vm = source(ROOT / "ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs")
        for collection, count in (("Sales", 4), ("Production", 4), ("Inventory", 4), ("Delivery", 5)):
            hosts = [e for e in view.iter() if e.get("ItemsSource") == "{Binding " + collection + "Tiles}"]
            self.assertEqual(1, len(hosts))
            self.assertEqual("{StaticResource Ov.KpiHost}", hosts[0].get("Style"))
            self.assertEqual(count, vm.count("_" + collection.lower() + "Tiles.Add("))
            self.assertIsNone(hosts[0].get("Height"))
            self.assertIsNone(hosts[0].get("MaxHeight"))
        self.assertIsNotNone(template("Ov.KpiHost").find(".//p:StackPanel", NS))
        self.assertIn('ItemsSource="{Binding WorkflowStages}"', source(VIEW))
        # No fixed height / clipping on any ancestor of a KPI collection.
        def walk(element, ancestors):
            if element.get("ItemsSource", "").endswith("Tiles}"):
                for parent in ancestors:
                    self.assertIsNone(parent.get("Height"))
                    self.assertIsNone(parent.get("MaxHeight"))
                    self.assertNotEqual("True", parent.get("ClipToBounds"))
            for child in element:
                walk(child, ancestors + [element])
        walk(view.getroot(), [])

    def test_row_model_binding_contracts(self):
        contracts = {
            "Ov.KpiTemplate": "Title Icon DisplayValue SubLabel IsAlert",
            "Ov.FeedTemplate": "Icon Title SubTitle ValueDisplay StatusLabel StatusColor TintColor",
            "Ov.StageTemplate": "Glyph Name ValueDisplay Foot AccentKey IsActive IsFirst",
            "Ov.DeliveryTemplate": "Label ValueDisplay",
        }
        for key, paths in contracts.items():
            actual = set(BINDING.findall(ET.tostring(template(key), encoding="unicode")))
            self.assertFalse(set(paths.split()) - actual, key)

    def test_no_duplicate_property_assignment_or_mid_content_properties(self):
        for path in (VIEW, SHELL, RESOURCES):
            for element in ET.parse(path).iter():
                content_started = False
                property_after_content = False
                assigned = set()
                for child in element:
                    name = child.tag.split("}")[-1]
                    if "." not in name:
                        self.assertFalse(property_after_content, f"{path.name}: property in middle of content")
                        content_started = True
                        continue
                    prop = name.split(".", 1)[1]
                    self.assertNotIn(prop, assigned, f"{path.name}: repeated property {prop}")
                    self.assertNotIn(prop, element.attrib, f"{path.name}: attribute + property {prop}")
                    assigned.add(prop)
                    property_after_content |= content_started

    def test_view_only_responsive_properties_are_registered(self):
        code = source(VIEW.with_suffix(".xaml.cs"))
        for name in ("KpiColumns", "StatColumns", "PipelineColumns", "DeliveryColumns"):
            self.assertIn("DependencyProperty.Register(nameof(" + name + ")", code)
            self.assertIn("public int " + name, code)
            self.assertIn("{Binding " + name + ", RelativeSource=", source(VIEW))
        self.assertIn('SizeChanged="OnOverviewSizeChanged"', source(VIEW))
        self.assertIn("ArrangePair(PulseCard, DeliveryCard", code)
        self.assertIn("Grid.SetRowSpan(ActivityCard, stackedAnalytics ? 1 : 2)", code)
        self.assertNotIn("DataContext =", code)

    def test_live_charts_units_states_and_full_feed(self):
        text = source(VIEW)
        self.assertIn('Points="{Binding MonthlySalesBars}" SeriesKind="Area"', text)
        self.assertIn('Values="{Binding PulseSpark}"', text)
        self.assertIn('ItemsSource="{Binding DeliveryMixSlices}"', text)
        self.assertNotIn("DonutChartControl", text)
        self.assertIn("Proforma value is not collected revenue", text)
        self.assertIn('HasRecentActivity, Converter=', text)
        self.assertIn('Binding HasError', text)
        self.assertIn('Binding IsLoading', text)
        self.assertIn('SmartScrollBubblingBehavior.IsEnabled="True"', text)
        self.assertIn('VerticalScrollBarVisibility="Auto"', text)
        self.assertIn("Sent out · notes", source(RESOURCES))
        self.assertIn("Pending · pieces", source(RESOURCES))
        self.assertNotIn("ItemsSource=\"{Binding RecentActivity[", text)
        self.assertNotIn("ItemsSource=\"{Binding DeliveryTiles[", text)

    def test_loading_and_failure_hide_entire_snapshot_badge(self):
        name = "{" + NS["x"] + "}Name"
        badge = next(e for e in ET.parse(VIEW).iter() if e.get(name) == "SnapshotBadge")
        style = badge.find("p:Border.Style/p:Style", NS)
        self.assertIsNotNone(style)
        triggers = style.findall("p:Style.Triggers/p:DataTrigger", NS)
        for property_name in ("IsLoading", "HasError"):
            trigger = next(e for e in triggers if e.get("Binding") == "{Binding " + property_name + "}")
            self.assertEqual("True", trigger.get("Value"))
            self.assertTrue(any(e.get("Property") == "Visibility" and e.get("Value") == "Collapsed" for e in trigger))

    def test_layout_applies_after_initialization_and_on_resize(self):
        code = source(VIEW.with_suffix(".xaml.cs"))
        root = ET.parse(VIEW).getroot()
        self.assertEqual("OnOverviewLoaded", root.get("Loaded"))
        self.assertEqual("OnOverviewSizeChanged", root.get("SizeChanged"))
        self.assertIn("ApplyLayout(ActualWidth)", code)
        self.assertIn("ApplyLayout(e.NewSize.Width)", code)
        for name in ("StatsBand", "PulseCard", "DeliveryCard", "TrendCard", "ActivityCard"):
            self.assertIn(name + " == null", code)

    def test_narrow_shell_reflows_without_hiding_bound_user_or_subtitle(self):
        root = ET.parse(SHELL).getroot()
        self.assertEqual("OnDashboardSizeChanged", root.get("SizeChanged"))
        code = source(SHELL.with_suffix(".xaml.cs"))
        self.assertIn("ApplyHeaderLayout(ActualWidth)", code)
        self.assertIn("Grid.SetColumnSpan(BrandIdentity, compact ? 3 : 1)", code)
        self.assertIn("Grid.SetRow(PageSubtitle, compact ? 1 : 0)", code)
        self.assertIn("Grid.SetRow(UserBadge, compact ? 1 : 0)", code)
        self.assertNotIn("Visibility.Collapsed", code)
        self.assertIn("vm.OnViewLoaded()", code)

    def test_scoped_normal_text_colour_pairs_have_aa_contrast(self):
        colours = {e.get(KEY): e.get("Color") for e in ET.parse(RESOURCES).getroot() if e.get("Color")}

        def luminance(hex_colour):
            rgb = [int(hex_colour[i:i+2], 16) / 255 for i in (1, 3, 5)]
            linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in rgb]
            return sum(v * weight for v, weight in zip(linear, (0.2126, 0.7152, 0.0722)))

        for foreground, background in (
            ("Muted", "Surface"), ("Muted", "Soft"), ("Ink", "Surface"), ("Ink", "AreaEnd"),
            ("OnAccent", "Accent"), ("OnAccent", "AccentHover"),
            ("OnAccentMuted", "Accent"), ("OnAccentMuted", "AccentHover"),
            ("Success", "SuccessSoft"), ("Warning", "WarningSoft"), ("Danger", "DangerSoft"),
        ):
            a, b = sorted((luminance(colours["Ov." + foreground]), luminance(colours["Ov." + background])))
            self.assertGreaterEqual((b + 0.05) / (a + 0.05), 4.5, (foreground, background))

    def test_palette_does_not_replace_app_theme(self):
        # Every dictionary entry is keyed. No implicit app-wide control styles.
        for entry in ET.parse(RESOURCES).getroot():
            self.assertTrue(entry.get(KEY, "").startswith("Ov."))
        self.assertNotIn("Application.Current", source(VIEW.with_name("IndigoStatusBrushConverter.cs")))
        self.assertNotIn("BrushKeyConverter}", source(RESOURCES))
        self.assertNotIn("IndigoStudioResources", source(ROOT / "App.xaml"))


if __name__ == "__main__":
    unittest.main(verbosity=2)

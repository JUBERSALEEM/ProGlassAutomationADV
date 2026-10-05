#!/usr/bin/env python3
"""Portable source-contract checks, NOT a native WPF build/render test.
Baseline contracts contain binding expressions only, never business records.
"""
from collections import Counter
from pathlib import Path
import json
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = json.loads((ROOT / 'Tests/Dashboard/Contracts/indigo-modules-bindings.json').read_text())
PAGES = CONTRACT['pages']
W = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
LAYOUT = '{clr-namespace:ProGlassAutomation.Views.Dashboard}IndigoDashboardPanel'
RESOURCE = ROOT / 'Views/Dashboard/IndigoDashboardResources.xaml'


def read(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


def normalize(text):
    return re.sub(r'\s+', ' ', text.replace('BrushKeyConverter', 'Ov.StatusBrush')).strip()


def bindings(root):
    return Counter((attribute.split('}')[-1], normalize(value))
                   for element in root.iter() for attribute, value in element.attrib.items()
                   if '{Binding' in value)


def scoped_elements(root, scope='$page'):
    if root.tag in (W + 'DataTemplate', W + 'ControlTemplate'):
        scope = root.get(X + 'Key') or root.get(X + 'Name') or scope + '/$inline-template'
    yield scope, root
    for child in root:
        yield from scoped_elements(child, scope)


def scoped_bindings(root):
    return Counter((scope, attribute.split('}')[-1], normalize(value))
                   for scope, element in scoped_elements(root)
                   for attribute, value in element.attrib.items() if '{Binding' in value)


class IndigoModuleContracts(unittest.TestCase):
    def test_every_original_binding_expression_is_retained(self):
        total = 0
        for path, baseline in PAGES.items():
            expected = Counter()
            for binding in baseline['bindings']:
                attribute = binding['attribute']
                # Deliberate presentation correction: saved balances are independent
                # counters, no longer slices of a misleading percentage partition.
                if binding['expression'] == '{Binding BalanceMixSlices}':
                    self.assertEqual('Slices', attribute)
                    attribute = 'ItemsSource'
                expected[(attribute, normalize(binding['expression']))] += 1
            current = bindings(ET.parse(ROOT / path).getroot())
            self.assertFalse(expected - current, (path, expected - current))
            total += sum(expected.values())
        self.assertEqual(164, total)

    def test_binding_expressions_stay_in_their_original_data_template_scope(self):
        for path, baseline in PAGES.items():
            expected = Counter()
            for binding in baseline['bindings']:
                attribute = 'ItemsSource' if binding['expression'] == '{Binding BalanceMixSlices}' else binding['attribute']
                expected[(binding['scope'], attribute, normalize(binding['expression']))] += 1
            actual = scoped_bindings(ET.parse(ROOT / path).getroot())
            self.assertFalse(expected - actual, (path, expected - actual))

    def test_context_checker_detects_swapped_bindings_that_global_counts_miss(self):
        original = ET.fromstring('<Grid xmlns="' + W[1:-1] + '" xmlns:x="' + X[1:-1] + '">'
            '<TextBlock Text="{Binding HasOverdue}"/>'
            '<DataTemplate x:Key="Row"><TextBlock Text="{Binding Title}"/></DataTemplate></Grid>')
        changed = ET.fromstring(ET.tostring(original))
        texts = list(changed.iter(W + 'TextBlock'))
        texts[0].set('Text', '{Binding Title}')
        texts[1].set('Text', '{Binding HasOverdue}')
        self.assertEqual(bindings(original), bindings(changed))
        self.assertNotEqual(scoped_bindings(original), scoped_bindings(changed))

    def test_every_collection_still_uses_the_correct_row_template(self):
        mappings = {'{StaticResource PgKpiTileTemplate}': '{StaticResource Ds.KpiTileTemplate}',
                    '{StaticResource PgFeedRowTemplate}': '{StaticResource Ov.FeedTemplate}'}
        for path, baseline in PAGES.items():
            elements = list(scoped_elements(ET.parse(ROOT / path).getroot()))
            for host in baseline['collection_templates']:
                self.assertTrue(any(scope == host['scope'] and element.tag == W + 'ItemsControl'
                    and element.get('ItemsSource') == host['source']
                    and element.get('ItemTemplate') == mappings.get(host['template'], host['template'])
                    for scope, element in elements), (path, host))

    def test_each_action_keeps_its_own_label_command_parameter_and_enabled_state(self):
        for path, baseline in PAGES.items():
            elements = list(scoped_elements(ET.parse(ROOT / path).getroot()))
            for action in baseline['command_endpoints']:
                self.assertTrue(any(scope == action['scope'] and element.tag.split('}')[-1] == action['element']
                    and all(normalize(element.get(key, '')) == normalize(value)
                            for key, value in action.items() if key not in ('scope', 'element'))
                    for scope, element in elements), (path, action))

    def test_page_identity_and_inherited_data_context_are_unchanged(self):
        for path, baseline in PAGES.items():
            root = ET.parse(ROOT / path).getroot()
            self.assertEqual(baseline['root_class'], root.get(X + 'Class'))
            for element in root.iter():
                self.assertNotIn('DataContext', element.attrib)
                self.assertNotEqual('DataContext', element.tag.split('}')[-1].split('.')[-1])

    def test_kpi_ancestors_do_not_crop_or_cap_the_collection(self):
        for path in PAGES:
            root = ET.parse(ROOT / path).getroot()
            parents = {child: parent for parent in root.iter() for child in parent}
            for host in root.iter(W + 'ItemsControl'):
                if host.get('ItemsSource') not in ('{Binding KpiCards}', '{Binding KpiTiles}'):
                    continue
                current = host
                while current is not None:
                    self.assertIsNone(current.get('Height'), (path, current.tag))
                    self.assertIsNone(current.get('MaxHeight'), (path, current.tag))
                    self.assertNotEqual('True', current.get('ClipToBounds'), (path, current.tag))
                    current = parents.get(current)

    def test_all_six_pages_use_scoped_indigo_and_reflow(self):
        self.assertEqual(6, len(PAGES))
        for path in PAGES:
            root = ET.parse(ROOT / path).getroot()
            self.assertEqual('{DynamicResource Ov.Canvas}', root.get('Background'))
            self.assertEqual('{DynamicResource Ov.Ink}', root.get('Foreground'))
            self.assertNotIn('ErpPageRootStyle', read(path))
            self.assertIsNotNone(root.find('.//' + LAYOUT))
            self.assertIsNotNone(root.find('.//' + W + 'StackPanel[@Style="{StaticResource Ds.Content}"]'))
            for dictionary in root.iter(W + 'ResourceDictionary'):
                if dictionary.get('Source'):
                    self.assertEqual(RESOURCE, (ROOT / path).parent.joinpath(dictionary.get('Source')).resolve())
            self.assertNotRegex(read(path), r'\{(?:Static|Dynamic)Resource Pg\w+')
        self.assertNotIn('IndigoDashboardResources', read('App.xaml'))

    def test_all_25_kpis_keep_their_complete_collection(self):
        for path in PAGES:
            root = ET.parse(ROOT / path).getroot()
            hosts = [e for e in root.iter(W + 'ItemsControl') if e.get('ItemsSource') in ('{Binding KpiCards}', '{Binding KpiTiles}')]
            self.assertEqual(1, len(hosts), path)
            host = hosts[0]
            self.assertEqual('{StaticResource Ds.KpiTileTemplate}', host.get('ItemTemplate'))
            self.assertEqual('{StaticResource Ds.KpiBandPanel}', host.get('ItemsPanel'))
            self.assertEqual('{StaticResource Ds.KpiBandStyle}', host.get('Style'))
            self.assertIsNone(host.get('Height'))
            self.assertIsNone(host.get('MaxHeight'))
        # Counting actual construction sites, not assuming five modules' counts.
        counts = []
        for path in PAGES:
            vm = path.replace('Views/', 'ViewModels/').replace('View.xaml', 'ViewModel.cs')
            text = read(vm)
            if 'TiDashboard' in path:
                counts.append(len(re.findall(r'new KpiTileModel\s*\{', text)))
            else:
                counts.append(len(re.findall(r'new (?:ProGlassAutomation.Models.Dashboard.)?KpiCardModel\s*\{', text)))
        self.assertEqual([4, 4, 4, 4, 4, 5], sorted(counts))
        self.assertEqual(25, sum(counts))

    def test_shared_kpi_template_supports_both_real_model_types(self):
        root = ET.parse(RESOURCE).getroot()
        template = next(e for e in root if e.get(X + 'Key') == 'Ds.KpiTileTemplate')
        text = ET.tostring(template, encoding='unicode')
        for name in ('Title', 'Icon', 'DisplayValue', 'SubLabel', 'IsAlert', 'BadgeColor'):
            self.assertIn('{Binding ' + name, text)
        self.assertNotIn('{Binding Accent', text)
        self.assertNotIn('{Binding TintColor', text)
        self.assertIn('StretchDirection="DownOnly"', text)
        self.assertNotIn('MaxHeight=', text)

    def test_failure_gate_does_not_hide_error_message(self):
        resources = ET.parse(RESOURCE).getroot()
        content = next(e for e in resources if e.get(X + 'Key') == 'Ds.Content')
        text = ET.tostring(content, encoding='unicode')
        for state in ('IsLoading', 'HasError'):
            self.assertIn('{Binding ' + state + '}', text)
        self.assertEqual(2, text.count('Value="Collapsed"'))
        for path in PAGES:
            root = ET.parse(ROOT / path).getroot()
            gate = root.find('.//' + W + 'StackPanel[@Style="{StaticResource Ds.Content}"]')
            self.assertNotIn('{Binding ErrorMessage}', ET.tostring(gate, encoding='unicode'))
            self.assertIn('{Binding ErrorMessage}', read(path))

    def test_document_commands_have_same_ancestor_and_parameter(self):
        for path, baseline in PAGES.items():
            root = ET.parse(ROOT / path).getroot()
            for old in baseline['bindings']:
                if old['attribute'] not in ('Command', 'CommandParameter', 'IsEnabled'):
                    continue
                self.assertTrue(any(normalize(e.get(old['attribute'], '')) == normalize(old['expression'])
                                    for e in root.iter() if e.tag.split('}')[-1] == old['element']), (path, old))
        tax = ET.parse(ROOT / next(p for p in PAGES if 'TiDashboard' in p)).getroot()
        keys = {e.get('Key') for e in tax.iter(W + 'KeyBinding')}
        self.assertTrue({'Enter', 'Space'} <= keys)
        self.assertIsNotNone(tax.find('.//' + W + 'Border[@Focusable="True"]'))

    def test_every_document_feed_has_bounded_scroll_and_wheel_bubbling(self):
        for path in PAGES:
            root = ET.parse(ROOT / path).getroot()
            parents = {child: parent for parent in root.iter() for child in parent}
            for host in root.iter(W + 'ItemsControl'):
                if host.get('ItemsSource') not in ('{Binding RecentFeed}', '{Binding RecentInvoices}', '{Binding RecentStatements}', '{Binding TopCustomers}', '{Binding AgingBrackets}'):
                    continue
                scroll = parents[host]
                self.assertEqual(W + 'ScrollViewer', scroll.tag)
                self.assertEqual('Auto', scroll.get('VerticalScrollBarVisibility'))
                self.assertEqual('True', scroll.get('{clr-namespace:ProGlassAutomation.Behaviors}SmartScrollBubblingBehavior.IsEnabled'))
                self.assertIsNotNone(scroll.get('MaxHeight'))

    def test_chart_types_and_units_match_the_actual_metric(self):
        for path in PAGES:
            root = ET.parse(ROOT / path).getroot()
            for chart in root.iter():
                if chart.tag.endswith('}TrendChartControl'):
                    expected = 'Area' if chart.get('Points') == '{Binding RevenueBars}' else 'Bar'
                    self.assertEqual(expected, chart.get('SeriesKind'))
                    self.assertIn('Ov.', chart.get('SeriesBrush', ''))
        tax = read(next(p for p in PAGES if 'TiDashboard' in p))
        self.assertNotIn('InvoicedAmountLast12Months', tax)
        statement = read(next(p for p in PAGES if 'Statement' in p))
        self.assertNotIn('DonutChartControl', statement)
        self.assertIn('ItemsSource="{Binding BalanceMixSlices}"', statement)
        self.assertIn('overdue is included in outstanding', statement.lower())

    def test_property_elements_are_not_duplicated_or_between_content(self):
        for path in list(PAGES) + ['Views/Dashboard/IndigoDashboardResources.xaml']:
            for element in ET.parse(ROOT / path).iter():
                assigned = set()
                content = False
                after_content = False
                for child in element:
                    name = child.tag.split('}')[-1]
                    if '.' not in name:
                        self.assertFalse(after_content, (path, element.tag))
                        content = True
                    else:
                        prop = name.split('.', 1)[1]
                        self.assertNotIn(prop, assigned, (path, prop))
                        self.assertNotIn(prop, element.attrib, (path, prop))
                        assigned.add(prop)
                        after_content |= content

    def test_empty_statement_refresh_rebuilds_the_kpi_collection(self):
        text = read('ViewModels/Dashboard/Statement/StDashboardViewModel.cs')
        start = text.index('if (metrics.TotalStatements == 0)')
        branch = text[start:text.index('return;', start)]
        for assignment in ('_totalStatements = 0', '_totalInvoiced = 0m', '_totalOutstanding = 0m', '_overdueAmount = 0m'):
            self.assertIn(assignment, branch)
        self.assertIn('RebuildBalanceMix();', branch)
        self.assertIn('RebuildKpiCards();', branch)
        self.assertLess(branch.index('RebuildKpiCards();'), branch.index('OnPropertyChanged(string.Empty)'))

    def test_missing_metrics_service_is_not_presented_as_empty_success(self):
        for path in (
            'ViewModels/Dashboard/SheetStore/SheetStoreDashboardViewModel.cs',
            'ViewModels/Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs',
            'ViewModels/Dashboard/Statement/StDashboardViewModel.cs',
        ):
            text = read(path)
            start = text.index('protected override async Task LoadCoreAsync()')
            guard = text[start:text.index('return;', start)]
            self.assertIn('App.DashboardMetrics == null', guard)
            self.assertIn('HasError = true;', guard)
            self.assertIn('ErrorMessage =', guard)

    def test_chart_legend_uses_page_palette_not_application_only(self):
        chart = read('Controls/DonutChart/DonutChartControl.xaml.cs')
        self.assertIn('private FrameworkElement BuildLegend', chart)
        self.assertIn('TryFindResource("PrimaryForegroundBrush")', chart)
        self.assertNotIn('Application.Current.TryFindResource("PrimaryForegroundBrush")', chart)
        self.assertNotIn('Application.Current.TryFindResource("SecondaryForegroundBrush")', chart)


if __name__ == '__main__':
    unittest.main(verbosity=2)

#!/usr/bin/env python3
"""Source contracts only: this is not WPF compilation, binding tracing or rendering."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = json.loads((ROOT / 'Tests/Lists/Contracts/studio-list-bindings.json').read_text())
PAGES = CONTRACT['pages']
W = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
PANEL = '{clr-namespace:ProGlassAutomation.Views.Dashboard}IndigoDashboardPanel'
RESOURCE = ROOT / 'Views/Shared/Lists/StudioListResources.xaml'


def read(p):
    return (ROOT / p).read_text(encoding='utf-8-sig')


def normalize(s):
    # Authorized presentation mapping only; paths, sources and parameters otherwise remain exact.
    return re.sub(r'\s+', ' ', s.replace('BrushKeyConverter', 'Sl.StatusBrush')
                  .replace(', ConverterParameter=soft', '').replace(', ConverterParameter=ink', '')).strip()


def walk(root, scope='$page'):
    if root.tag in (W + 'DataTemplate', W + 'ControlTemplate'):
        scope = root.get(X + 'Key') or scope + '/$inline-template'
    yield scope, root
    for child in root:
        yield from walk(child, scope)


def scoped(root):
    return Counter((scope, key, normalize(value)) for scope, e in walk(root)
                   for key, value in e.attrib.items() if '{Binding' in value)


def roots():
    return [(p, ET.parse(ROOT / p).getroot(), b) for p, b in PAGES.items()]


class StudioListContracts(unittest.TestCase):
    def test_all_309_original_binding_attributes_keep_scope_and_multiplicity(self):
        total = 0
        for p, r, b in roots():
            expected = Counter((x['scope'], x['attribute'], normalize(x['expression'])) for x in b['bindings'])
            self.assertFalse(expected - scoped(r), (p, expected - scoped(r)))
            total += sum(expected.values())
        self.assertEqual(309, total)

    def test_all_seven_object_bindings_keep_paths_sources_and_template_scope(self):
        total = 0
        for p, r, b in roots():
            actual = Counter((scope, tuple(sorted((k, normalize(v)) for k, v in e.attrib.items())))
                             for scope, e in walk(r) if e.tag == W + 'Binding')
            expected = Counter((e['scope'], tuple(sorted((k, normalize(v)) for k, v in e['attributes'].items())))
                               for e in b['object_bindings'])
            self.assertFalse(expected - actual, (p, expected - actual))
            total += sum(expected.values())
        self.assertEqual(7, total)

    def test_context_swap_negative_control(self):
        a = ET.fromstring(f'<Grid xmlns="{W[1:-1]}" xmlns:x="{X[1:-1]}"><TextBlock Text="{{Binding A}}"/><DataTemplate x:Key="Row"><TextBlock Text="{{Binding B}}"/></DataTemplate></Grid>')
        b = ET.fromstring(ET.tostring(a))
        items = list(b.iter(W + 'TextBlock'))
        items[0].set('Text', '{Binding B}')
        items[1].set('Text', '{Binding A}')
        flat = lambda r: Counter(v for e in r.iter() for v in e.attrib.values() if '{Binding' in v)
        self.assertEqual(flat(a), flat(b))
        self.assertNotEqual(scoped(a), scoped(b))

    def test_every_existing_command_retains_its_own_label_parameter_tooltip_and_enabled_gate(self):
        for p, r, b in roots():
            for action in b['actions']:
                self.assertTrue(any(scope == action['scope'] and e.tag == W + action['element']
                    and all(normalize(e.get(k, '')) == normalize(v) for k, v in action.items()
                            if k not in ('scope', 'element')) for scope, e in walk(r)), (p, action))

    def test_collections_templates_and_events_are_preserved(self):
        for p, r, b in roots():
            for c in b['collections']:
                self.assertTrue(any(scope == c['scope'] and e.tag == W + c['element']
                    and e.get('ItemsSource') == c['source'] and e.get('ItemTemplate') == c['template']
                    for scope, e in walk(r)), (p, c))
            for event in b['events']:
                self.assertTrue(any(scope == event['scope'] and e.get(event['event']) == event['handler']
                                    for scope, e in walk(r)), (p, event))

    def test_same_identity_no_replacement_data_context_or_sample_source(self):
        for p, r, b in roots():
            self.assertEqual(b['class'], r.get(X + 'Class'))
            for e in r.iter():
                self.assertNotIn('DataContext', e.attrib)
                self.assertNotEqual(W + 'UserControl.DataContext', e.tag)
            self.assertNotRegex(read(p), r'PI-026[0-4]|Customer [A-F]|154,350|Illustrative data|\.png|\.jpg')

    def test_all_five_use_one_scoped_bento_resource_and_live_summary(self):
        self.assertEqual(5, len(PAGES))
        for p, r, _ in roots():
            self.assertEqual('{DynamicResource Ov.Canvas}', r.get('Background'))
            sources = [e.get('Source') for e in r.iter(W + 'ResourceDictionary') if e.get('Source')]
            self.assertEqual([RESOURCE], [(ROOT / p).parent.joinpath(s).resolve() for s in sources])
            self.assertTrue(any(e.get('Style') == '{StaticResource Sl.Hero}' for e in r.iter(W + 'Border')))
            self.assertTrue(any(e.get('MaxColumns') == '3' for e in r.iter(PANEL)))
        self.assertNotIn('StudioListResources', read('App.xaml'))

    def test_all_thirteen_lists_delegate_scrolling_and_recycle_without_truncation(self):
        count = 0
        for p, r, _ in roots():
            for e in r.iter():
                if e.tag not in (W + 'ListBox', W + 'ListView'):
                    continue
                count += 1
                self.assertEqual('True', e.get('ScrollViewer.CanContentScroll'), p)
                self.assertEqual('Visible', e.get('ScrollViewer.VerticalScrollBarVisibility'), p)
                self.assertTrue(any(e.get(k) == 'Recycling' for k in ['VirtualizingPanel.VirtualizationMode', 'VirtualizingStackPanel.VirtualizationMode']))
                self.assertTrue(any(e.get(k) == 'Pixel' for k in ['VirtualizingPanel.ScrollUnit', 'VirtualizingStackPanel.ScrollUnit']))
                self.assertNotIn('Height', e.attrib)
                self.assertNotIn('MaxHeight', e.attrib)
        self.assertEqual(13, count)

    def test_horizontal_viewport_is_shared_by_headers_and_finite_list_body(self):
        for p, r, _ in roots():
            sv = next(e for e in r.iter(W + 'ScrollViewer') if e.get(X + 'Name') == 'RegisterViewport')
            self.assertEqual('Disabled', sv.get('VerticalScrollBarVisibility'))
            grid = sv.find(W + 'Grid')
            self.assertIn('ViewportWidth', grid.get('Width'))
            self.assertIn('ViewportHeight', grid.get('Height'))
            self.assertGreaterEqual(float(grid.get('MinWidth')), 1000)
            self.assertTrue(any(e.get('Style') == '{StaticResource Sl.TableHeader}' or e.get('BasedOn') == '{StaticResource Sl.TableHeader}' for e in grid.iter()))
            self.assertTrue(any(e.tag in (W + 'ListBox', W + 'ListView') for e in grid.iter()))

    def test_header_and_row_columns_match_including_action_width(self):
        for p, r, _ in roots():
            signatures = []
            for grid in r.iter(W + 'Grid'):
                cols = grid.find(W + 'Grid.ColumnDefinitions')
                if cols is not None and len(cols) >= 6 and cols[0].get('Width') == '3':
                    signatures.append(tuple((e.get('Width'), e.get('MinWidth')) for e in cols))
            self.assertGreaterEqual(len(signatures), 2, p)
            self.assertEqual(1, len(set(signatures)), p)

    def test_six_delivery_and_four_tax_tabs_keep_indices_and_original_sources(self):
        for p, r, _ in roots():
            tabs = [e for e in r.iter(W + 'RadioButton')]
            expected = 6 if 'DeliveryNoteList' in p else 4 if 'TaxInvoiceList' in p else 0
            self.assertEqual(expected, len(tabs), p)
            for i, tab in enumerate(tabs):
                self.assertIn('ConverterParameter=' + str(i), tab.get('IsChecked'))
                self.assertEqual('{StaticResource Sl.Tab}', tab.get('Style'))

    def test_new_more_buttons_call_shared_original_menu_builders(self):
        for p, r, _ in roots():
            if 'StatementList' in p:
                continue
            self.assertTrue(any(e.get('Click') == 'OnRowMoreClick' for e in r.iter(W + 'Button')), p)
            code = read(p + '.cs')
            self.assertIn('ShowRowMenu(sender as FrameworkElement, atPointer: true)', code)
            self.assertIn('ShowRowMenu(sender as FrameworkElement, atPointer: false)', code)

    def test_dossier_tag_stays_a_vm_bridge_not_an_icon(self):
        self.assertIn('value is string text ? text : string.Empty', read('Views/Shared/Lists/StudioListConverters.cs'))
        p = next(p for p in PAGES if 'SavedInvoicesList' in p)
        r = ET.parse(ROOT / p).getroot()
        dossier = next(e for e in r.iter(W + 'Button') if e.find(W + 'Button.Tag') is not None)
        self.assertEqual('DataContext', dossier.find(W + 'Button.Tag/' + W + 'Binding').get('Path'))
        self.assertEqual(4, len(list(dossier.iter(W + 'MenuItem'))))

    def test_statement_pdf_share_guards_and_selection_survive(self):
        p = next(p for p in PAGES if 'StatementList' in p)
        r = ET.parse(ROOT / p).getroot()
        self.assertEqual('{Binding SelectedRow}', next(r.iter(W + 'ListView')).get('SelectedItem'))
        gated = [e for e in r.iter(W + 'Button') if e.get('IsEnabled') == '{Binding HasPdf}']
        self.assertEqual(2, len(gated))

    def test_failed_initial_load_not_empty_success_and_export_feedback_is_visible(self):
        pi = read('ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs')
        st = read('ViewModels/Reports/StatementList/StatementListViewModel.cs')
        self.assertIn('HasError = true;', pi)
        self.assertIn('public bool HasError', pi)
        self.assertIn('!HasError && Rows.Count == 0', st)
        self.assertIn('await UpdateUiAsync', st)
        self.assertNotIn('Dispatcher.BeginInvoke(', st)
        p = next(p for p in PAGES if 'SavedInvoicesList' in p)
        self.assertIn('{Binding LastExportStatus}', read(p))
        gate = next(e for e in ET.parse(RESOURCE).getroot().iter(W + 'Style') if e.get(X + 'Key') == 'Sl.DataArea')
        self.assertTrue(any(e.get('Binding') == '{Binding HasError}' for e in gate.iter()))

    def test_tax_queued_reload_and_zero_count_do_not_use_stale_all_tab(self):
        code = read('ViewModels/TaxInvoice/TaxInvoiceList/TaxInvoiceListViewModel.cs')
        self.assertIn('public int AllCount => _allCount;', code)
        self.assertIn('Interlocked.CompareExchange(ref _reloadQueued, 0, 1) == 1', code)
        self.assertIn('_ = LoadAsync();', code)
        self.assertIn('familyVms.FirstOrDefault(f => f.ProformaId == selectedReference)', code)

    def test_parent_creation_scope_and_all_36_shell_bindings_are_preserved(self):
        b = CONTRACT['parent']
        root = ET.parse(ROOT / b['path']).getroot()
        expected = Counter((k, normalize(v)) for k, v in b['bindings'])
        actual = Counter((k, normalize(v)) for e in root.iter() for k, v in e.attrib.items() if '{Binding' in v)
        self.assertFalse(expected - actual)
        self.assertEqual(36, sum(expected.values()))
        ids = [e.get('AutomationProperties.AutomationId') for e in root.iter()]
        for identity in b['automation_ids']:
            self.assertIn(identity, ids)
        # Native wizard view is not themed by the list's resource dictionary.
        root_grid = root.find(W + 'Grid')
        self.assertEqual(1, len([e for e in root_grid if e.find(W + 'Grid.Resources') is not None]))

    def test_toolbar_scrolls_when_short_without_unbounded_list_measure(self):
        for p, r, _ in roots():
            toolbar = next(e for e in r.iter(W + 'ScrollViewer') if e.get(X + 'Name') == 'ToolbarViewport')
            self.assertEqual('Auto', toolbar.get('VerticalScrollBarVisibility'))
            self.assertIn('Sl.ToolbarHeight', toolbar.get('MaxHeight'))
            self.assertFalse(any(e.tag in (W + 'ListBox', W + 'ListView') for e in toolbar.iter()))
            self.assertTrue(any(e.tag == PANEL for e in toolbar.iter()))

    def test_picker_owns_disposal_and_history_remains_viewable_without_restore_callback(self):
        code = read('Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListDialog.xaml.cs')
        self.assertIn('protected override void OnClosed(EventArgs e)', code)
        self.assertIn('disposable.Dispose();', code)
        vm = read('ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs')
        self.assertIn('canRestore: OnSnapshotRestoreRequested != null', vm)
        dialog = read('Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml.cs')
        self.assertIn('bool canRestore = true', dialog)  # Other callers retain normal restoration.
        self.assertIn('if (!CanRestore) return;', dialog)
        self.assertIn('OnInvoiceApproveRequested == null', read('ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs'))

    def test_all_21_original_summary_values_remain_in_bento(self):
        expected = [3, 4, 5, 5, 4]
        for (p, r, _), count in zip(roots(), expected):
            summary = next(e for e in r.iter(PANEL) if e.get('MaxColumns') == '3')
            values = [e for e in summary.iter(W + 'TextBlock')
                      if e.get('Style') == '{StaticResource Sl.Value}' or
                      any(c.tag == W + 'TextBlock.Style' and 'Sl.Value' in ET.tostring(c, encoding='unicode') for c in e)]
            self.assertEqual(count, len(values), p)

    def test_native_list_actions_do_not_gain_fictional_proforma_features(self):
        for p, r, _ in roots():
            if 'SavedInvoicesList' in p:
                continue
            buttons = [e.get('Command', '') for e in r.iter(W + 'Button')]
            self.assertFalse(any('ExportDossierCommand' in c or 'DuplicateInvoiceCommand' in c for c in buttons))
        st = read('Views/Reports/StatementList/StatementListView.xaml')
        self.assertNotIn('FromDate', st)
        self.assertNotIn('SortPresetOptions', st)

    def test_complete_original_menu_bodies_preserve_business_actions_and_guards(self):
        for path, contract in CONTRACT['menus'].items():
            source = read(path)
            start = source.index('private void ShowRowMenu')
            start = source.index('{', start)
            i, depth = start + 1, 1
            while depth:
                if source[i] == '{':
                    depth += 1
                elif source[i] == '}':
                    depth -= 1
                i += 1
            body = source[start + 1:i - 1]
            body = re.sub(r'Placement = atPointer \? System.Windows.Controls.Primitives.PlacementMode.MousePoint\s*: System.Windows.Controls.Primitives.PlacementMode.Bottom,',
                          'Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,', body)
            digest = hashlib.sha256(re.sub(r'\s+', ' ', body).strip().encode()).hexdigest()
            self.assertEqual(contract['normalized_body_sha256'], digest, path)

    def test_tinted_status_keys_use_strong_ink_not_same_color_as_fill(self):
        for p, r, _ in roots():
            for e in r.iter():
                fore = e.get('Foreground', '')
                if 'Sl.StatusBrush' in fore:
                    self.assertIn('ConverterParameter=ink', fore, (p, fore))
        self.assertIn('bool soft = !ink', read('Views/Shared/Lists/StudioListConverters.cs'))
        palette = ET.parse(ROOT / 'Views/Dashboard/Overview/IndigoStudioResources.xaml').getroot()
        colors = {e.get(X + 'Key'): e.get('Color') for e in palette if e.tag == W + 'SolidColorBrush'}
        def luminance(code):
            rgb = [int(code[i:i+2], 16) / 255 for i in (1, 3, 5)]
            linear = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in rgb]
            return sum(x * y for x, y in zip(linear, [0.2126, 0.7152, 0.0722]))
        for ink, fill in [('Ov.Accent', 'Ov.Soft'), ('Ov.Muted', 'Ov.Soft'), ('Ov.Success', 'Ov.SuccessSoft'), ('Ov.Warning', 'Ov.WarningSoft'), ('Ov.Danger', 'Ov.DangerSoft')]:
            a, b = sorted([luminance(colors[ink]), luminance(colors[fill])])
            self.assertGreaterEqual((b + 0.05) / (a + 0.05), 4.5, (ink, fill))

    def test_no_duplicate_property_assignments(self):
        for p, r, _ in roots():
            for e in r.iter():
                properties = [c.tag.split('}')[-1].split('.', 1)[1] for c in e if '.' in c.tag.split('}')[-1]]
                self.assertEqual(len(properties), len(set(properties)), (p, e.tag))
                for prop in properties:
                    self.assertNotIn(prop, e.attrib, (p, e.tag, prop))


if __name__ == '__main__':
    unittest.main(verbosity=2)

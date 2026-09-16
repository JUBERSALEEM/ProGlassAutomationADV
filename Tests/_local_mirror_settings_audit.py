# Local mirror of the pure-file audit locks in Tests/Audit/SettingsRedesignAuditTests.cs.
# No .NET SDK in this sandbox: this reproduces the file-level rules in python.
import glob, re, sys
fails = []
def nlines(p): return len(open(p, encoding='utf-8').read().split('\n'))
# shell <= 420
if nlines('Views/Settings/SettingsView.xaml') > 420: fails.append('shell over 420')
# VM partials <= 350 (SettingsViewModel*.cs + Pages/*PageViewModel*.cs)
for f in glob.glob('ViewModels/Settings/SettingsViewModel*.cs') + glob.glob('ViewModels/Settings/Pages/*PageViewModel*.cs'):
    if nlines(f) > 350: fails.append(f + ' over 350')
# sections <= 400
for f in glob.glob('Views/Settings/Sections/*.xaml'):
    if nlines(f) > 400: fails.append(f + ' over 400')
# cards <= 520
for f in glob.glob('Views/Settings/Sections/Cards/*.xaml'):
    if nlines(f) > 520: fails.append(f + ' over 520')
# literal-free section markup
for f in glob.glob('Views/Settings/Sections/*.xaml'):
    s = open(f, encoding='utf-8').read()
    if re.search(r'#[0-9A-Fa-f]{6}', s): fails.append(f + ': hex literal')
    if re.search(r'CornerRadius="\d', s): fails.append(f + ': CornerRadius literal')
    if re.search(r'FontSize="\d', s): fails.append(f + ': FontSize literal')
# ── build-integrity locks (owner build 2026-09-16) ─────────────────────
# CS8646/CS0121/CS0102 — one x:Class per .xaml file, repo-wide
_owners = {}
for f in glob.glob('**/*.xaml', recursive=True):
    if '/obj/' in f or '/bin/' in f or f.startswith(('obj/', 'bin/')):
        continue
    m = re.search(r'x:Class="([^"]+)"', open(f, encoding='utf-8').read())
    if not m:
        continue
    if m.group(1) in _owners:
        fails.append('duplicate x:Class %s (%s + %s)' % (m.group(1), _owners[m.group(1)], f))
    _owners[m.group(1)] = f
# Themes/Languages holds only L1 resource dictionaries
for f in glob.glob('Themes/Languages/*.xaml'):
    x = open(f, encoding='utf-8').read()
    if 'x:Class' in x or '<ResourceDictionary' not in x:
        fails.append(f + ': not a plain language resource dictionary')
# CS0234 — folder == namespace for the promoted page VMs
for f in glob.glob('ViewModels/Settings/Pages/*.cs'):
    if 'namespace ProGlassAutomation.ViewModels.Settings.Pages' not in open(f, encoding='utf-8').read():
        fails.append(f + ': wrong namespace (must end .Settings.Pages)')
for f in glob.glob('ViewModels/Settings/*.cs'):
    if 'namespace ProGlassAutomation.ViewModels.Settings' not in open(f, encoding='utf-8').read():
        fails.append(f + ': wrong namespace (must end .Settings)')
# XLS0501/MC3024 — Style attribute + <Border.Style>; MC3057 — attrs on property elements
for f in glob.glob('Views/**/*.xaml', recursive=True):
    x = open(f, encoding='utf-8').read()
    for m in re.finditer(r'<(\w[\w.-]*)\.Style[\s>/]', x):
        owner = m.group(1)
        j = x.rfind('<' + owner, 0, m.start())
        while j != -1 and x[j + len(owner) + 1:j + len(owner) + 2] == '.':
            j = x.rfind('<' + owner, 0, j)
        if j == -1:
            continue
        tag = x[j:x.find('>', j) + 1]
        if re.search(r'(?<![\w.-])Style\s*=', tag):
            fails.append(f + ': Style set twice on ' + owner)
    for m in re.finditer(r'<\w+\.\w+[^>]*?>', x):
        if re.search(r'\s\w+[:\w.]*\s*=', m.group(0)):
            fails.append(f + ': attributes on property element ' + m.group(0)[:40])
# ── code-behind contract locks (owner build 2026-09-17) ───────────────
import os
_EV = ('Click|PasswordChanged|Checked|Unchecked|Toggled|SelectionChanged|'
       'SelectedIndexChanged|TextChanged|LostFocus|GotFocus|KeyDown|KeyUp|'
       'PreviewKeyDown|Loaded|Unloaded|ValueChanged|DragOver|Drop|'
       'MouseLeftButtonDown|MouseLeftButtonUp|MouseDoubleClick|MouseEnter|'
       'MouseLeave|Expanded|Collapsed|Completed|ScrollChanged|SizeChanged|'
       'CurrentCellChanged|BeginningEdit|CellEditEnding|RowEditEnding|'
       'AutoGeneratingColumn|LoadingRow|PreparingCellForEdit|RequestNavigate|'
       'ManipulationDelta|ManipulationCompleted|PreviewMouseLeftButtonDown')

def _strip(code):
    code = re.sub(r'/\*.*?\*/', '', code, flags=re.S)
    return re.sub(r'//[^\r\n]*', '', code)

_views = [f for f in glob.glob('Views/**/*.xaml', recursive=True)]
# CS1061 — every XAML event handler must exist in the paired code-behind
for f in _views:
    cs = f + '.cs'
    if not os.path.exists(cs):
        continue
    body = _strip(open(cs, encoding='utf-8').read())
    for ev, name in re.findall(r'\b(' + _EV + r')="(\w+)"', open(f, encoding='utf-8').read()):
        if not re.search(r'(?:void|Task|async\s+\w+)\s+' + re.escape(name) + r'\s*\(', body):
            fails.append('%s wires %s=%s but %s does not define it' % (f, ev, name, cs))
# CS0103 — an x:Name only exists in its own namescope
_declared = {}
for f in _views:
    for n in re.findall(r'\bx:Name="(\w+)"', open(f, encoding='utf-8').read()):
        _declared.setdefault(n, f)
for cs in glob.glob('Views/Settings/**/*.xaml.cs', recursive=True):
    own = os.path.basename(cs)[:-3]
    body = _strip(open(cs, encoding='utf-8').read())
    for n, src in _declared.items():
        if os.path.basename(src) == own:
            continue
        if re.search(r'(?<![\w.$])' + re.escape(n) + r'\s*\.', body):
            fails.append('%s uses %s declared in %s' % (cs, n, src))
# CS0246 — page VM references must be fully qualified
for f in [x for x in glob.glob('**/*.cs', recursive=True)
          if not any(p in x for p in ('/obj/', '/bin/', 'Tests/', 'Tools/', '.git/'))]:
    for m in re.finditer(r'(?<![\w.])(?:ViewModels\.Settings\.)?Pages\.\w+', _strip(open(f, encoding='utf-8').read())):
        fails.append('%s references page VMs relatively (%s)' % (f, m.group(0)))
# XDG0046 — a keyed entry must never sit BESIDE a keyless ResourceDictionary
# inside <X.Resources>: that makes Resources an implicit dictionary and the
# merged dictionaries unreachable (every style in the file stops resolving).
from xml.dom import minidom
for f in glob.glob('Views/**/*.xaml', recursive=True):
    try:
        dom = minidom.parse(f)
    except Exception as e:
        fails.append('%s: not well-formed (%s)' % (f, e)); continue
    for res in dom.getElementsByTagName('*'):
        if not res.tagName.endswith('.Resources'):
            continue
        kids = [k for k in res.childNodes if k.nodeType == k.ELEMENT_NODE]
        loose = [k for k in kids if k.getAttribute('x:Key')]
        keyless = [k for k in kids if k.tagName == 'ResourceDictionary'
                   and not k.getAttribute('x:Key')]
        if loose and keyless:
            fails.append('%s: <%s> mixes keyed entries with a keyless '
                         'ResourceDictionary (XDG0046)' % (f, res.tagName))
# ── reports table-header grammar + DataGrid column-header bindings ──────
for f in glob.glob('Views/Reports/**/*.xaml', recursive=True):
    dom = minidom.parse(f)
    for border in dom.getElementsByTagName('Border'):
        grid = None
        for kid in border.childNodes:
            if kid.nodeType == kid.ELEMENT_NODE and kid.tagName == 'Grid':
                for gk in kid.childNodes:
                    if (gk.nodeType == gk.ELEMENT_NODE and
                            gk.tagName == 'Grid.ColumnDefinitions'):
                        grid = kid
                        break
        if grid is None:
            continue
        bar = 'ReportTableHeaderStyle' in (border.getAttribute('Style') or '')
        labels = [t for t in grid.getElementsByTagName('TextBlock')
                  if t.getAttribute('Grid.Column')]
        if len(labels) < 3:
            continue
        for t in labels:
            st = t.getAttribute('Style') or ''
            if bar and 'ReportHeaderTextStyle' not in st:
                fails.append('%s: accent header bar with an unstyled label '
                             '(%s)' % (f, t.getAttribute('Text')[:24]))
            if not bar and 'ReportHeaderTextStyle' in st:
                fails.append('%s: white header label with no accent bar '
                             '(%s)' % (f, t.getAttribute('Text')[:24]))
# REPLACED 2026-09-18. The old rule ("a column Header binding must contain
# RelativeSource") was WRONG and let the defect ship twice: a RelativeSource
# ancestor walk starts at the binding TARGET and climbs its visual/logical
# parents, and a DataGridColumn is in NEITHER tree - there is no ancestor to
# climb. Only HeaderTemplate (instantiated by the DataGridColumnHeader, which
# IS in the tree) or an explicit Source= (Utilities/BindingProxy) can work.
for f in glob.glob('Views/**/*.xaml', recursive=True):
    # comments stripped first: the banner comments that document this bug
    # quote the broken markup verbatim
    markup = re.sub(r'<!--.*?-->', '', open(f, encoding='utf-8').read(), flags=re.S)
    for tag in re.finditer(r'<DataGrid[\w.]*Column\b[^>]*>', markup, re.S):
        for a in re.finditer(r'([\w.]+)="([^"]*)"', tag.group(0), re.S):
            name, value = a.group(1), a.group(2)
            if '{Binding' not in value:
                continue
            # Binding / DataMemberBinding / ClipboardContentBinding are
            # evaluated per CELL, and a cell IS in the visual tree with the row
            # item as its DataContext - that is the correct, universal pattern.
            if name in ('Binding', 'DataMemberBinding', 'ClipboardContentBinding'):
                continue
            if 'Source=' not in value:
                fails.append('%s: %s= is a column-level property and its '
                             'binding resolves against nothing - the column is '
                             'in neither the visual nor the logical tree. Use '
                             'HeaderTemplate or Source={StaticResource proxy} '
                             '(%s)' % (f, name, value[:48]))
# ── settings card rhythm == the About module's rhythm ─────────────────
grammar = open('Views/Settings/SettingsCardStyles.xaml', encoding='utf-8').read()
for pin in ('<Thickness x:Key="SettingsCardMargin">0,0,0,14</Thickness>',
            '<sys:Double x:Key="SettingsGapCard">14</sys:Double>',
            '<Thickness x:Key="SettingsCardPadding">22,20</Thickness>',
            '<Thickness x:Key="SettingsPagePadding">24,20,24,24</Thickness>'):
    if pin not in grammar:
        fails.append('SettingsCardStyles.xaml: lost the About-benchmark '
                     'spacing token ' + pin)
# the benchmark itself must not move silently under the settings module
about = open('Views/About/AboutIos26Theme.xaml', encoding='utf-8').read()
if '<Setter Property="Padding" Value="22,20"/>' not in about:
    fails.append('AboutIos26Theme.xaml: Ios26CardStyle padding is no longer '
                 '22,20 - re-benchmark SettingsCardPadding')

# ── Proforma Step 7: preview grid + PDF columns stay possible ─────────
pi = open('Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/'
          'PrintableInvoiceView.xaml', encoding='utf-8').read()
pi_dom = minidom.parseString(pi)
spec_grids = [g for g in pi_dom.getElementsByTagName('Grid')
              if len(g.getElementsByTagName('ColumnDefinition')) == 11]
if len(spec_grids) < 3:
    fails.append('PrintableInvoiceView: expected the header, data-row and '
                 'sub-total grids (11 columns each), found %d'
                 % len(spec_grids))
sigs = set()
for g in spec_grids:
    sig = tuple(c.getAttribute('Width') for c in
                g.getElementsByTagName('ColumnDefinition'))
    sigs.add(sig)
    # a fixed-pixel money/description column is what starved the identity
    # columns on a narrow paper
    for idx in (1, 6, 7, 8, 9, 10):
        if not sig[idx].endswith('*'):
            fails.append('PrintableInvoiceView: spec column %d is fixed '
                         '(%s) - it cannot shrink with the paper'
                         % (idx, sig[idx]))
if len(sigs) > 1:
    fails.append('PrintableInvoiceView: header/rows/sub-total use %d '
                 'different column sets - the totals cannot line up'
                 % len(sigs))
# rows show bare digits; the column header carries "(AED)"
for m in re.finditer(r'Text="\{Binding (?:Rate|Value|FooterTotalValue)[^"]*\}"', pi):
    if 'ConverterParameter=number' not in m.group(0):
        fails.append('PrintableInvoiceView: amount row still renders the '
                     'currency token (%s)' % m.group(0)[:52])
conv = open('Converters/CurrencyDisplayConverter.cs', encoding='utf-8').read()
if '"number" => svc.Digits(amount)' not in conv:
    fails.append('CurrencyDisplayConverter: the "number" mode is gone')
if 'public string Digits(decimal baseAmount)' not in \
        open('Services/Localization/CurrencyService.cs', encoding='utf-8').read():
    fails.append('CurrencyService: Digits() is gone')

rend = open('Services/ProformaInvoice/Export/PiPreviewParityRenderer.cs',
            encoding='utf-8').read()
if 'private static float Widest(' not in rend:
    fails.append('PiPreviewParityRenderer: header-aware column fit is gone')
for token in ('PiStr.ColRate', 'PiStr.ColValue', 'PiStr.ColGlassRef'):
    body = rend[rend.index('private static void DefineSpecColumns'):]
    if token not in body.split('private static float MinNumericColumn')[0]:
        fails.append('PiPreviewParityRenderer: %s is not measured into its '
                     'column width - the header can overflow and QuestPDF '
                     'fails the whole export' % token)
if 'MoneyBare(row.Rate)' not in rend or 'MoneyBare(row.Total)' not in rend:
    fails.append('PiPreviewParityRenderer: spec rows are back to Money() - '
                 'preview and PDF drift on the currency token')
if 'float room = Math.Max(availablePt - refMin, sum * 0.5f);' not in rend:
    fails.append('PiPreviewParityRenderer: the unconditional width clamp is '
                 'gone - conflicting-size-constraints can return')

# ── Step 7 exports carry the currency in the HEADER, not on every row ──
pdf_layout = open('Services/ProformaInvoice/Export/ProformaInvoicePdfLayout.cs',
                  encoding='utf-8').read()
if re.search(r'RateLabel = \$"AED ', pdf_layout):
    fails.append('ProformaInvoicePdfLayout: charge RateLabel hard-codes "AED " '
                 'next to the renderer\'s own "Rate:" label')
xl = open('Services/ProformaInvoice/Export/ProformaInvoiceExcelLayout.cs',
          encoding='utf-8').read()
if 'string Qty, decimal Rate, decimal Amount' not in xl:
    fails.append('ProformaInvoiceExcelLayout: the charges Rate column is text '
                 'again - "(AED)" header + "AED " cell, and Excel cannot sum it')
if re.search(r'\$"AED \{[cw]\.Rate:N2\}', xl):
    fails.append('ProformaInvoiceExcelLayout: a charge rate cell still carries '
                 'the currency token')


# ── an AREA never runs through the CURRENCY converter ─────────────────
# (the converter's own header records this trap: an area rendered as a
# price reads "AED 45.70 / m²" or "AED 45.70 m²")
for f in glob.glob('Views/**/*.xaml', recursive=True):
    src = open(f, encoding='utf-8').read()
    for m in re.finditer(r'Text="\{Binding ([^"]*)\}"', src):
        binding = m.group(1)
        if not re.search(r'\b(TotalSqm|TotalSqmBalance|TotalArea|UnitArea)\b',
                         binding):
            continue
        if 'CurrencyConverter' in binding and \
                'ConverterParameter=area' not in binding:
            fails.append('%s: an area is rendered as money (%s)'
                         % (f, binding[:64]))


# ══════════════════════════════════════════════════════════════════════
#  WAVE 6 RUNTIME-SEMANTICS MIRRORS
#  (python twins of Tests/Audit/RuntimeSemanticsAuditTests.cs - shape checks
#  were green while the app was broken, so these model the consuming engine)
# ══════════════════════════════════════════════════════════════════════
from xml.dom import minidom as _md

def _xaml(folder):
    return [f for f in glob.glob(folder + '/**/*.xaml', recursive=True)
            if '/obj/' not in f and '/bin/' not in f]

def _kids(e):
    return [k for k in e.childNodes if k.nodeType == k.ELEMENT_NODE]

def _desc(e):
    for k in _kids(e):
        yield k
        for d in _desc(k):
            yield d

def _chain(code, at):
    i, d = at, 0
    while i < len(code):
        c = code[i]
        if c in '([{':
            d += 1
        elif c in ')]}':
            if d == 0:
                break
            d -= 1
        elif c == ';' and d == 0:
            break
        i += 1
    return code[at:i]

# 1. QuestPDF: an AutoItem measures its child with INFINITE width, so a
#    RelativeItem inside that subtree has no finite space -> "conflicting
#    size constraints / forward from child" and every export dies.
for f in glob.glob('Services/ProformaInvoice/**/*.cs', recursive=True) + \
         glob.glob('Services/Export/**/*.cs', recursive=True):
    code = _strip(open(f, encoding='utf-8').read())
    at = code.find('.AutoItem(')
    while at >= 0:
        if 'RelativeItem' in _chain(code, at):
            fails.append('%s: an AutoItem subtree contains a RelativeItem - '
                         'infinite parent width, no finite space to divide, '
                         'QuestPDF fails the document' % f)
        at = code.find('.AutoItem(', at + 1)

# 2. WPF clamps an out-of-range Grid.Row/Column into the LAST track and
#    paints the child over that track's content (Companies hub overlap).
for f in _xaml('Views'):
    try:
        dom = _md.parse(f)
    except Exception:
        continue
    for g in dom.getElementsByTagName('Grid'):
        rd = [c for c in _kids(g) if c.localName == 'Grid.RowDefinitions']
        cd = [c for c in _kids(g) if c.localName == 'Grid.ColumnDefinitions']
        nr = len([x for x in _kids(rd[0]) if x.localName == 'RowDefinition']) if rd else 0
        nc = len([x for x in _kids(cd[0]) if x.localName == 'ColumnDefinition']) if cd else 0
        for k in _kids(g):
            def _ix(name):
                v = k.getAttribute(name)
                return int(v) if v.lstrip('-').isdigit() else -1
            r, rs = _ix('Grid.Row'), max(1, _ix('Grid.RowSpan'))
            c, cs = _ix('Grid.Column'), max(1, _ix('Grid.ColumnSpan'))
            if nr and r >= 0 and r + rs > nr:
                fails.append('%s: <%s Grid.Row=%d span=%d> exceeds %d row '
                             'track(s) - WPF clamps it onto the last row' % (f, k.tagName, r, rs, nr))
            if nc and c >= 0 and c + cs > nc:
                fails.append('%s: <%s Grid.Column=%d span=%d> exceeds %d '
                             'column track(s)' % (f, k.tagName, c, cs, nc))

# 3. Option tiles inside a settings card must keep the 14 px card rhythm:
#    stacked tiles -> SettingsStackGap14; tile grids -> SettingsTileGridMargin
#    on every child + SettingsTileGridBleed on the grid.
for f in _xaml('Views/Settings'):
    try:
        dom = _md.parse(f)
    except Exception:
        continue
    for tag in ('StackPanel', 'UniformGrid', 'Grid', 'WrapPanel', 'DockPanel'):
        for e in dom.getElementsByTagName(tag):
            if tag == 'StackPanel' and e.getAttribute('Orientation') == 'Horizontal':
                continue
            touching = sum(1 for k in _kids(e)
                           if 'SettingsInnerTileStyle' in (k.getAttribute('Style') or '')
                           and not k.getAttribute('Margin'))
            if touching >= 2:
                fails.append('%s: a %s holds %d option tiles with no Margin - '
                             'they touch' % (f, tag, touching))

# 4. Print.TableHeader is a DARK band in every PdfTheme; its labels must be
#    the white bold header type the exporter paints (SpecHeaderCell).
for f in _xaml('Views'):
    try:
        dom = _md.parse(f)
    except Exception:
        continue
    for b in dom.getElementsByTagName('Border'):
        if 'Print.TableHeader' not in (b.getAttribute('Background') or ''):
            continue
        for t in b.getElementsByTagName('TextBlock'):
            if t.getAttribute('Grid.Column') and \
               'PrintTableHeaderTextStyle' not in (t.getAttribute('Style') or ''):
                fails.append('%s: a print table-header label is not '
                             'PrintTableHeaderTextStyle (dark small text on a '
                             'dark band)' % f)

# 5. A card whose fill equals its stage is invisible: the iOS 26 light ramp
#    painted every level white and the theme page read as "empty".
_ios = open('Services/Theme/VisualThemeEngine.Ios26.cs', encoding='utf-8').read()
_at = _ios.index('Ios26GlobalLight()')
_tbl = _chain(_ios, _ios.index('{', _at))
def _rgb(key):
    m = re.search(r'\["' + re.escape(key) + r'"\]\s*=\s*Ios26\w+\([^"#]*"#([0-9A-Fa-f]{6,8})"', _tbl)
    return m.group(1)[-6:].upper() if m else None
_stage = _rgb('WindowBackgroundBrush') or _rgb('ModuleBackgroundBrush')
_card = _rgb('Surface.Primary.BackgroundBrush')
if _stage and _card and _stage == _card:
    fails.append('iOS26 light: stage %s == card %s - a card that equals its '
                 'stage has no boundary and the theme reads as empty' % (_stage, _card))
_mb = re.search(r'\["Surface.Primary.BorderBrush"\]\s*=\s*Ios26\w+\([^"#]*"#([0-9A-Fa-f]{8})"', _tbl)
if _mb and int(_mb.group(1)[:2], 16) < 0x1A:
    fails.append('iOS26 light: Surface.Primary.BorderBrush alpha %s is below '
                 '10%% - an invisible card edge' % _mb.group(1)[:2])

# 6. Report hubs that speak the Proforma header grammar must speak it on
#    EVERY table header (a header = a grid whose column widths match the row
#    template underneath and whose cells are labels, not bindings).
for f in _xaml('Views/Reports'):
    txt = open(f, encoding='utf-8').read()
    if 'ReportTableHeaderStyle' not in txt:
        continue          # wizard steps / PI wizard use their own language
    try:
        dom = _md.parse(f)
    except Exception:
        continue
    groups = {}
    for g in dom.getElementsByTagName('Grid'):
        cds = [c for c in _kids(g) if c.localName == 'Grid.ColumnDefinitions']
        if not cds:
            continue
        widths = [c.getAttribute('Width') for c in _kids(cds[0]) if c.localName == 'ColumnDefinition']
        if len(widths) < 3:
            continue
        labels = [t for t in _desc(g) if t.localName == 'TextBlock' and t.getAttribute('Grid.Column')]
        if len(labels) < 3:
            continue
        groups.setdefault('|'.join(widths), []).append((g, labels))
    for sig, members in groups.items():
        if len(members) < 2:
            continue
        for g, labels in members:
            if any('{Binding' in (t.getAttribute('Text') or '') for t in labels):
                continue
            par = g.parentNode
            bar = par is not None and par.nodeType == par.ELEMENT_NODE and \
                'ReportTableHeaderStyle' in (par.getAttribute('Style') or '')
            if not bar:
                fails.append('%s: a %d-column table header has no '
                             'ReportTableHeaderStyle bar (not the Proforma hub '
                             'type)' % (f, len(labels)))
            elif not all('ReportHeaderTextStyle' in (t.getAttribute('Style') or '') for t in labels):
                fails.append('%s: table-header labels are not all '
                             'ReportHeaderTextStyle' % f)


# ── tile-grid gutter lock (owner report 2026-09-16: "cards under cards of
#    options are too close") ─────────────────────────────────────────────
#    A UniformGrid of option tiles must use the pair scheme: every child
#    carries SettingsTileGridMargin (7,7) and the grid carries a
#    SettingsTileGridBleed* (-7,...) so neighbours are 14 px apart ON BOTH
#    AXES and the outer edges stay flush. The historical defect was
#    SettingsTrailingGap (0,0,12,0) — a RIGHT-ONLY gap, so the second and
#    third ROWS of tiles touched with zero vertical gutter while the file
#    still looked "conforming" to any margin-presence check.
from xml.dom import minidom as _minidom
_TILE_STYLES = ('SettingsInnerTileStyle', 'SettingsIconTileStyle', 'SettingsWashTileStyle')
def _is_tile(el):
    st = el.getAttribute('Style') or ''
    return (any(t in st for t in _TILE_STYLES)
            or el.tagName.endswith('Tile') or 'CrystalKpiCard' in el.tagName)
def tile_grid_gutter_fails(path):
    bad = []
    try:
        dom = _minidom.parse(path)
    except Exception as e:
        return ['%s: unparsable (%s)' % (path, e)]
    for g in dom.getElementsByTagName('UniformGrid'):
        kk = [k for k in g.childNodes if k.nodeType == k.ELEMENT_NODE]
        if len([k for k in kk if _is_tile(k)]) < 2:
            continue
        gm = g.getAttribute('Margin') or ''
        if 'SettingsTileGridBleed' not in gm:
            bad.append('%s: tile grid Margin=%r is not a SettingsTileGridBleed* '
                       'token, so the 7,7 children are not cancelled at the '
                       'edges' % (path, gm))
        for k in kk:
            m = k.getAttribute('Margin') or ''
            if m != '{StaticResource SettingsTileGridMargin}':
                bad.append('%s: tile-grid child <%s> Margin=%r != '
                           'SettingsTileGridMargin (a right-only or literal gap '
                           'leaves the next ROW of tiles touching)'
                           % (path, k.tagName, m or '(none)'))
    return bad

# ── a11y name coherence lock (owner report 2026-09-16: light/dark mode
#    "broken nothing working") ──────────────────────────────────────────
#    Four settings controls announced themselves with a name copy-pasted
#    from a different card (the Light/Dark segment read "Motion &
#    Performance", density read "Motion & Performance", reduced-motion and
#    auto-suggest read "Module Wallpapers"). The control still worked, but
#    a screen-reader user could not tell what it did — and it is the shape
#    of defect that survives every structural scan.
def _words(key):
    key = re.sub(r'^[^.]*\.', '', key or '')          # drop the namespace segment
    # split camelCase BEFORE lowering, else "ThemeMode" collapses to one word
    return [w.lower() for w in re.findall(r'[A-Z]+(?![a-z])|[A-Z]?[a-z0-9]+', key)
            if len(w) >= 3]
def _a11y_coherent(aid, content, name):
    pool = _words(aid) + _words(content)
    nw = _words(name)
    if not nw or not pool:
        return True
    return any(x == y or x.startswith(y) or y.startswith(x) for x in pool for y in nw)
def a11y_name_fails(path):
    s = open(path, encoding='utf-8').read()
    bad = []
    for m in re.finditer(r'<([A-Za-z][\w.]*)((?:"[^"]*"|[^>"\'])*)>', s, re.S):
        attrs = m.group(2)
        aid = re.search(r'AutomationProperties\.AutomationId="([^"]+)"', attrs)
        nm = re.search(r'AutomationProperties\.Name="\{DynamicResource ([^}]+)\}"', attrs)
        if not (aid and nm):
            continue
        ct = re.search(r'(?:Content|Text)="\{DynamicResource ([^}]+)\}"', attrs)
        if not _a11y_coherent(aid.group(1), ct.group(1) if ct else '', nm.group(1)):
            bad.append('%s: <%s> AutomationId=%s is announced as %s '
                       '(a11y name copied from another card)'
                       % (path, m.group(1), aid.group(1), nm.group(1)))
    return bad

for f in sorted(glob.glob('Views/Settings/**/*.xaml', recursive=True)):
    fails.extend(tile_grid_gutter_fails(f))
    fails.extend(a11y_name_fails(f))

# ── negative tests: both locks must FAIL on the defect they were written
#    for, and pass on the corrected shape (owner rule: never commit a lock
#    that has not been negative-tested). ────────────────────────────────
import tempfile, os as _os
_neg = tempfile.mkdtemp(prefix='mirror-neg-')
_bad_grid = _os.path.join(_neg, 'BadGrid.xaml')
open(_bad_grid, 'w', encoding='utf-8').write(
    '<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">'
    '<UniformGrid Columns="4" Margin="{StaticResource SettingsRowMargin}">'
    '<Border Style="{StaticResource SettingsInnerTileStyle}" Margin="{StaticResource SettingsTrailingGap}"/>'
    '<Border Style="{StaticResource SettingsInnerTileStyle}" Margin="{StaticResource SettingsTrailingGap}"/>'
    '<Border Style="{StaticResource SettingsInnerTileStyle}" Margin="{StaticResource SettingsTrailingGap}"/>'
    '<Border Style="{StaticResource SettingsInnerTileStyle}"/>'
    '</UniformGrid></UserControl>')
_good_grid = _os.path.join(_neg, 'GoodGrid.xaml')
open(_good_grid, 'w', encoding='utf-8').write(
    '<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">'
    '<UniformGrid Columns="4" Margin="{StaticResource SettingsTileGridBleed}">'
    '<Border Style="{StaticResource SettingsInnerTileStyle}" Margin="{StaticResource SettingsTileGridMargin}"/>'
    '<Border Style="{StaticResource SettingsInnerTileStyle}" Margin="{StaticResource SettingsTileGridMargin}"/>'
    '</UniformGrid></UserControl>')
_bad_a11y = _os.path.join(_neg, 'BadA11y.xaml')
open(_bad_a11y, 'w', encoding='utf-8').write(
    '<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">'
    '<ListBox AutomationProperties.Name="{DynamicResource Settings.MotionPerformance}" '
    'AutomationProperties.AutomationId="Settings.ThemeMode"/></UserControl>')
_good_a11y = _os.path.join(_neg, 'GoodA11y.xaml')
open(_good_a11y, 'w', encoding='utf-8').write(
    '<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">'
    '<ListBox AutomationProperties.Name="{DynamicResource Settings.Mode}" '
    'AutomationProperties.AutomationId="Settings.ThemeMode"/>'
    '<CheckBox Content="{DynamicResource Settings.LblAutoDetect}" '
    'AutomationProperties.Name="{DynamicResource Settings.LblAutoDetect}" '
    'AutomationProperties.AutomationId="Settings.AutoSuggestByRegion"/>'
    '<ToggleButton AutomationProperties.Name="{DynamicResource Settings.Microphone}" '
    'AutomationProperties.AutomationId="DemoCenter.CaptureMic"/></UserControl>')
if not tile_grid_gutter_fails(_bad_grid):
    fails.append('NEGATIVE TEST FAILED: right-only SettingsTrailingGap tile grid was accepted')
if tile_grid_gutter_fails(_good_grid):
    fails.append('negative-test false positive: the corrected pair scheme was rejected')
if not a11y_name_fails(_bad_a11y):
    fails.append('NEGATIVE TEST FAILED: ThemeMode announced as MotionPerformance was accepted')
if a11y_name_fails(_good_a11y):
    fails.append('negative-test false positive: coherent a11y names were rejected')

print('MIRROR FAIL: ' + '; '.join(fails) if fails else 'MIRROR PASS (all file-level locks)')
sys.exit(1 if fails else 0)

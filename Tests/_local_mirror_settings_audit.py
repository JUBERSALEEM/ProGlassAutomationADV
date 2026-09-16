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
for f in glob.glob('Views/**/*.xaml', recursive=True):
    # comments stripped first: the banner comment that documents this bug
    # quotes the broken markup verbatim
    markup = re.sub(r'<!--.*?-->', '', open(f, encoding='utf-8').read(), flags=re.S)
    for m in re.finditer(r'Header="\{Binding[^}]*\}"', markup):
        if 'RelativeSource' not in m.group(0):
            fails.append('%s: DataGridColumn header binding without an '
                         'ancestor (%s)' % (f, m.group(0)[:40]))
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


print('MIRROR FAIL: ' + '; '.join(fails) if fails else 'MIRROR PASS (all file-level locks)')
sys.exit(1 if fails else 0)

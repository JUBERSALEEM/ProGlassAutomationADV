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
print('MIRROR FAIL: ' + '; '.join(fails) if fails else 'MIRROR PASS (all file-level locks)')
sys.exit(1 if fails else 0)

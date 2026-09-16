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
print('MIRROR FAIL: ' + '; '.join(fails) if fails else 'MIRROR PASS (all file-level locks)')
sys.exit(1 if fails else 0)

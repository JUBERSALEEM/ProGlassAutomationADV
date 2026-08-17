"""
Detect CS0103 "The name 'X' does not exist in the current context" caused
by a MISSING using directive.

WHY THIS EXISTS
AutomationProperties.SetName(...) was added to TrendChartControl.xaml.cs
without `using System.Windows.Automation;`. Every other checker passed:
the file is brace-balanced, the XAML is valid, the type name is spelled
correctly. Nothing validated that the namespace was actually imported,
so the error reached the user's build.

The XAML attribute form (AutomationProperties.Name="...") resolves
through the default WPF namespace, which is why the same type worked in
markup and failed in code-behind.

SCOPE
Deliberately a curated table of WPF/BCL types this project actually
uses, not a general resolver. A real resolver needs the compiler; a
guessed one produces noise. Add a row when a new type is introduced.

GENERICS: `IEnumerable<T>` lives in System.Collections.Generic while the
non-generic `IEnumerable` lives in System.Collections. The check ignores
a name immediately followed by '<' unless the entry opts in, otherwise
it reports false positives on every generic usage.
"""
import re
import sys
import os

# type -> (namespace, generic_ok)
#   generic_ok=True  means `Name<...>` also requires this namespace
TYPES = {
    # CsvHelper: the writer type is in CsvHelper, but configuration types
    # are in CsvHelper.Configuration. Easy to miss because "using CsvHelper"
    # is already present and looks sufficient (audit fix C-3).
    'CsvConfiguration':     ('CsvHelper.Configuration', False),
    'InjectionOptions':     ('CsvHelper.Configuration', False),
    'AutomationProperties': ('System.Windows.Automation', False),
    'SystemParameters':     ('System.Windows', False),
    'DoubleAnimation':      ('System.Windows.Media.Animation', False),
    'CubicEase':            ('System.Windows.Media.Animation', False),
    'EasingMode':           ('System.Windows.Media.Animation', False),
    'Storyboard':           ('System.Windows.Media.Animation', False),
    'PathGeometry':         ('System.Windows.Media', False),
    'PathFigure':           ('System.Windows.Media', False),
    'LineSegment':          ('System.Windows.Media', False),
    'DoubleCollection':     ('System.Windows.Media', False),
    'Brushes':              ('System.Windows.Media', False),
    'PenLineCap':           ('System.Windows.Media', False),
    'PenLineJoin':          ('System.Windows.Media', False),
    'Rectangle':            ('System.Windows.Shapes', False),
    'Ellipse':              ('System.Windows.Shapes', False),
    'Canvas':               ('System.Windows.Controls', False),
    'KeyEventArgs':         ('System.Windows.Input', False),
    'KeyboardFocusChangedEventArgs': ('System.Windows.Input', False),
    'MouseEventArgs':       ('System.Windows.Input', False),
    'CultureInfo':          ('System.Globalization', False),
    'NumberStyles':         ('System.Globalization', False),
    'Interlocked':          ('System.Threading', False),
    'CancellationToken':    ('System.Threading', False),
    'BindingFlags':         ('System.Reflection', False),
    'ObservableCollection': ('System.Collections.ObjectModel', True),
    'DbContext':            ('Microsoft.EntityFrameworkCore', False),
}


def strip_noise(src):
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'"(?:\\.|[^"\\])*"', '""', src)
    src = re.sub(r'//.*', '', src)
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return src


def check(path):
    src = open(path, encoding='utf-8-sig').read()
    usings = set(re.findall(r'^\s*using\s+([\w\.]+)\s*;', src, flags=re.M))

    # File-scoped or block namespace: a type in the SAME namespace needs
    # no using, so treat the declared namespace as available too.
    for ns in re.findall(r'^\s*namespace\s+([\w\.]+)', src, flags=re.M):
        parts = ns.split('.')
        for i in range(len(parts)):
            usings.add('.'.join(parts[:i + 1]))

    body = re.sub(r'^\s*using .*?;$', '', src, flags=re.M)
    body = strip_noise(body)

    issues = 0
    for t, (ns, generic_ok) in TYPES.items():
        pattern = r'(?<![\w.])' + t + (r'\s*<' if generic_ok else r'(?![\w<])')
        if re.search(pattern, body) and ns not in usings:
            print(f"  MISSING USING {path}: '{t}' needs 'using {ns};'")
            issues += 1
    return issues


total = 0
for p in sys.argv[1:]:
    if os.path.exists(p):
        total += check(p)

print(f"\n{len(sys.argv)-1} file(s), {total} missing using(s)")
sys.exit(1 if total else 0)

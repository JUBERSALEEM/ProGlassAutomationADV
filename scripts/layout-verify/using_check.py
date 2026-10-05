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
    # C# 11 raw strings first — SQL/JSON text inside them is not code
    # (a table named "Brushes" used to trip the type scan).
    src = re.sub(r'\$*"{3,}.*?"{3,}', '""', src, flags=re.S)
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

    # A name the file DECLARES itself (const field, local, class) is not a
    # type reference: `private const string Brushes = "…Brushes.xaml"` is a
    # field that happens to share a name with System.Windows.Media.Brushes.
    declared = set(re.findall(
        r'\b(?:const|static|readonly|var)\s+[^\s=;(]+\s+(\w+)\s*[=;(]', body))
    declared |= set(re.findall(r'\bclass\s+(\w+)', body))

    issues = 0
    for t, (ns, generic_ok) in TYPES.items():
        # (?<![\w.])  — not part of a longer name and not qualified:
        #               System.Windows.Media.Animation.EasingMode needs no using.
        # (?!\s*=(?!=)) — not a PROPERTY/field assignment: the line
        #               `EasingMode = System.Windows.Media.Animation.
        #                EasingMode.EaseOut` names a property, not the type,
        #               and used to be reported as a missing using.
        if t in declared:
            continue
        tail = r'\s*<' if generic_ok else r'(?![\w<])(?![ \t]*=(?!=))'
        pattern = r'(?<![\w.])' + t + tail
        if re.search(pattern, body) and ns not in usings:
            print(f"  MISSING USING {path}: '{t}' needs 'using {ns};'")
            issues += 1
    return issues


SELFTEST_CASES = {
    # (source, expected_findings, description)
    ("using System;\nclass C { void M() { var e = EasingMode.EaseOut; } }", 1,
     "bare type usage is reported"),
    ("using System;\nclass C { void M() { var e = "
     "System.Windows.Media.Animation.EasingMode.EaseOut; } }", 0,
     "fully qualified usage is NOT reported"),
    ("using System;\nclass C { void M() { "
     "F = new System.Windows.Media.Animation.CubicEase { "
     "EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }; } }", 0,
     "assignment to a PROPERTY named like a type is NOT reported"),
    ("using System;\nclass C { void M() { var t = \"\"\"\n"
     "  CREATE TABLE Brushes (Id INTEGER);\n\"\"\"; } }", 0,
     "type-like text inside a raw string is NOT reported"),
    ("using System;\nclass C { private const string Brushes = \"a.xaml\"; "
     "void M() { var s = Brushes; } }", 0,
     "a field NAMED like a type is NOT reported"),
}


def selftest():
    import tempfile
    failures = 0
    for source, expected, name in SELFTEST_CASES:
        with tempfile.NamedTemporaryFile("w", suffix=".cs", delete=False,
                                         encoding="utf-8") as fh:
            fh.write(source)
            tmp = fh.name
        try:
            import io, contextlib
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                found = check(tmp)
        finally:
            os.unlink(tmp)
        ok = found == expected
        print(f"  {'ok  ' if ok else 'FAIL'} {name} "
              f"(expected {expected}, got {found})")
        failures += 0 if ok else 1
    print(f"\nselftest: {len(SELFTEST_CASES) - failures}/{len(SELFTEST_CASES)} checks passed")
    return 1 if failures else 0


if '--selftest' in sys.argv:
    sys.exit(selftest())

total = 0
for p in sys.argv[1:]:
    if os.path.exists(p):
        total += check(p)

print(f"\n{max(0, len(sys.argv)-1)} file(s), {total} missing using(s)")
sys.exit(1 if total else 0)

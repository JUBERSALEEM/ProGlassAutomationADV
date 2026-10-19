#!/usr/bin/env python3
"""
symbol-audit.py -- namespace / member / named-argument checker for C#.

WHY THIS EXISTS
---------------
A theme engine written without a compiler will happily reference types,
members, overloads and named parameters that do not exist. Checks that
only assert arity or brace balance pass all of those: a method call with
the right number of arguments but the wrong parameter NAME compiles
nothing and balances every brace in sight.

This script catches the three classes of error that actually showed up
in the eight-theme work:

  CS0246  a `using` directive naming a namespace nothing declares
  CS0117  a `Type.Member` where the member is not declared on Type
  CS1739  a `name:` argument matching no parameter of any overload

It is NOT a C# compiler and does not pretend to be. It resolves
identifiers textually against what the repository actually declares,
so it will report false positives on anything it cannot see -- inherited
WPF members (Control.Template, ControlTemplate.FindName), array .Length,
and fields whose declaration shape it does not model (tuple arrays).
Every finding must be confirmed by reading the source before it is
acted on. Treat output as a worklist, not a verdict.

The value is in the false-negative rate, not the false-positive rate:
it will not tell you a call is correct, but it will reliably tell you
when a symbol does not exist.

USAGE
-----
    python3 scripts/symbol-audit.py                     # uncommitted changes
    python3 scripts/symbol-audit.py --all-theme         # theme surface + tests
    python3 scripts/symbol-audit.py path/to/File.cs ... # explicit files

Exit code is 0 even with findings: this is advisory, not a build gate.
"""

import glob
import os
import re
import subprocess
import sys

# ---------------------------------------------------------------------------
# Where well-known framework types LIVE.
#
# The BCL_NS set above answers "does this using directive name something
# that exists". It cannot answer the question that actually caused the
# first round of eight-theme errors, which is the reverse: a type is
# USED in the file, but the namespace that declares it was never
# imported. That is CS0246 and it is invisible to a using-directive
# check, because every directive in the file is perfectly valid.
#
# Keep this table honest rather than exhaustive. An entry that names the
# wrong namespace produces a false positive, which is worse than a
# missing entry, because it teaches people to ignore the check.
# ---------------------------------------------------------------------------
TYPE_NS = {
    # System.Windows.Media
    "SolidColorBrush": "System.Windows.Media", "Brush": "System.Windows.Media",
    "Color": "System.Windows.Media", "Colors": "System.Windows.Media",
    "LinearGradientBrush": "System.Windows.Media",
    "RadialGradientBrush": "System.Windows.Media",
    "GradientStop": "System.Windows.Media", "GradientStopCollection":
    "System.Windows.Media", "Transform": "System.Windows.Media",
    "ScaleTransform": "System.Windows.Media", "RotateTransform":
    "System.Windows.Media", "TranslateTransform": "System.Windows.Media",
    "Visual": "System.Windows.Media", "Freezable": "System.Windows.Media",
    "PixelFormats": "System.Windows.Media", "BitmapSource":
    "System.Windows.Media.Imaging", "TransformedBitmap":
    "System.Windows.Media.Imaging",
    # System.Windows.Media.Effects
    "DropShadowEffect": "System.Windows.Media.Effects",
    "BlurEffect": "System.Windows.Media.Effects",
    "Effect": "System.Windows.Media.Effects",
    # System.Windows
    "Thickness": "System.Windows", "CornerRadius": "System.Windows",
    "Point": "System.Windows", "Size": "System.Windows", "Rect": "System.Windows",
    "DependencyObject": "System.Windows", "DependencyProperty": "System.Windows",
    "FrameworkElement": "System.Windows", "RoutedEventArgs": "System.Windows",
    "Application": "System.Windows", "Window": "System.Windows",
    "HorizontalAlignment": "System.Windows", "VerticalAlignment": "System.Windows",
    "Visibility": "System.Windows", "ControlTemplate": "System.Windows",
    "DataTemplate": "System.Windows", "ContentPresenter": "System.Windows",
    "GridLength": "System.Windows", "GridUnitType": "System.Windows",
    "MessageBox": "System.Windows", "MessageBoxResult": "System.Windows",
    # System.Windows.Controls
    "Grid": "System.Windows.Controls", "Panel": "System.Windows.Controls",
    "Canvas": "System.Windows.Controls", "StackPanel": "System.Windows.Controls",
    "DockPanel": "System.Windows.Controls", "WrapPanel": "System.Windows.Controls",
    "TextBlock": "System.Windows.Controls", "TextBox": "System.Windows.Controls",
    "Button": "System.Windows.Controls", "Border": "System.Windows.Controls",
    "Popup": "System.Windows.Controls", "Separator": "System.Windows.Controls",
    "ContextMenu": "System.Windows.Controls", "MenuItem": "System.Windows.Controls",
    "ListBox": "System.Windows.Controls", "ListBoxItem": "System.Windows.Controls",
    "ComboBox": "System.Windows.Controls", "ScrollViewer": "System.Windows.Controls",
    "UserControl": "System.Windows.Controls", "ItemsControl":
    "System.Windows.Controls", "ItemsPresenter": "System.Windows.Controls",
    "ContentControl": "System.Windows.Controls", "Control": "System.Windows.Controls",
    "ToolTip": "System.Windows.Controls", "ProgressBar": "System.Windows.Controls",
    "ControlTemplate": "System.Windows", "FrameworkElement": "System.Windows",
    # System.Windows.Controls.Primitives
    "ButtonBase": "System.Windows.Controls.Primitives",
    "ToggleButton": "System.Windows.Controls.Primitives",
    "CheckBox": "System.Windows.Controls.Primitives",
    "RadioButton": "System.Windows.Controls.Primitives",
    "Slider": "System.Windows.Controls.Primitives",
    "ScrollBar": "System.Windows.Controls.Primitives",
    # System.Windows.Input
    "KeyboardNavigation": "System.Windows.Input",
    "Keyboard": "System.Windows.Input", "Mouse": "System.Windows.Input",
    "Cursors": "System.Windows.Input", "Cursor": "System.Windows.Input",
    "ICommand": "System.Windows.Input", "CommandManager":
    "System.Windows.Input", "KeyEventArgs": "System.Windows.Input",
    "RoutedCommand": "System.Windows.Input",
    # System.Windows.Documents
    "Run": "System.Windows.Documents", "TextElement": "System.Windows.Documents",
    "Inline": "System.Windows.Documents", "FormattedText": "System.Windows.Documents",
    # System.Windows.Shapes
    "Rectangle": "System.Windows.Shapes", "Ellipse": "System.Windows.Shapes",
    "Line": "System.Windows.Shapes",
    "Polygon": "System.Windows.Shapes", "Polyline": "System.Windows.Shapes",
    # NOTE: Path is deliberately absent. It exists in both System.IO
    # (an implicit using) and System.Windows.Shapes, so mapping it here
    # produced eight false positives on Path.Combine. Ambiguous short
    # names do not belong in this table.
    # System.Windows.Automation
    "AutomationProperties": "System.Windows.Automation",
    "AutomationPeer": "System.Windows.Automation.Peers",
    # System.Windows.Data
    "IValueConverter": "System.Windows.Data", "Binding": "System.Windows.Data",
    "PropertyChangedEventArgs": "System.Windows",
    "INotifyPropertyChanged": "System.ComponentModel",
    "DependencyPropertyDescriptor": "System.ComponentModel",
    "PropertyDescriptor": "System.ComponentModel",
    "PropertyChangedEventHandler": "System.ComponentModel",
    # System.Threading / Diagnostics / Globalization
    "Dispatcher": "System.Windows.Threading", "Stopwatch": "System.Diagnostics",
    "CultureInfo": "System.Globalization", "StringComparer":
    "System.Collections.Generic", "Tuple": "System",
}

# Namespaces the SDK injects because ImplicitUsings is enabled, plus the
# one this project adds by hand. Types reached through these need no
# using directive. Everything else -- and that includes all of
# System.Windows.* -- does.
IMPLICIT_NS = {
    "System", "System.Collections.Generic", "System.IO", "System.Linq",
    "System.Net.Http", "System.Threading", "System.Threading.Tasks",
}

# ---------------------------------------------------------------------------
# Namespaces provided by the BCL / WPF / the test SDK. Anything here is
# assumed to exist without being declared in this repository.
# ---------------------------------------------------------------------------
BCL_NS = {
    n for n in """
    System System.Collections System.Collections.Concurrent
    System.Collections.Generic System.Collections.ObjectModel
    System.Collections.Specialized
    System.ComponentModel System.Diagnostics System.Globalization
    System.IO System.Linq System.Linq.Expressions System.Net.Http
    System.Reflection System.Resources System.Security.Cryptography
    System.Text System.Text.RegularExpressions System.Threading
    System.Threading.Tasks System.Xml System.Xml.Linq
    System.Windows System.Windows.Controls System.Windows.Controls.Primitives
    System.Windows.Data System.Windows.Documents System.Windows.Input
    System.Windows.Interop System.Windows.Markup System.Windows.Shapes
    System.Windows.Threading
    System.Windows.Automation System.Windows.Automation.Peers
    System.Windows.Media System.Windows.Media.Animation
    System.Windows.Media.Effects System.Windows.Media.Imaging
    System.Windows.Media.ThreeD
    Xunit Xunit.Abstractions
    """.split()
}

# Inherited members the checker cannot see because it does not model the
# WPF base chain. Listed so recurring hits do not have to be re-derived.
KNOWN_INHERITED = {
    "Template",       # Control.Template -> ControlTemplate
    "LayoutTransform",
    "RenderTransform",
}

THEME_GLOBS = (
    "Controls/Claymorphism/*.cs", "Controls/Neumorphism/*.cs",
    "Controls/Swiss/*.cs", "Controls/Holo/*.cs",
    "Services/Theme/Clay/*.cs", "Services/Theme/Neu/*.cs",
    "Services/Theme/Swiss/*.cs", "Services/Theme/Holo/*.cs",
    "Services/Theme/Eight/*.cs", "Tests/Theme/*.cs",
)

SKIP_DIRS = (".git", "obj", "bin", "node_modules", "packages")


def repo_root():
    out = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        capture_output=True, text=True,
    )
    return out.stdout.strip() or "."


def strip_noise(src):
    """Remove comments and string literals so only real code is scanned."""
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'"(?:[^"\\\n]|\\.)*"', '""', src)
    src = re.sub(r"'(?:\\.|[^'\\])'", "'x'", src)
    src = re.sub(r"//[^\n]*", "", src)
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return src


def collect_repo_symbols():
    """
    Map every declared type to its members, and every namespace.

    Members are matched from the declaration list inside each type body.
    This is deliberately generous: over-collecting costs a missed
    finding, under-collecting produces a false positive that a human
    then has to chase down.
    """
    repo_ns, members = set(), {}

    decl_re = r'\b(?:class|struct|interface|record)\s+(\w+)'
    member_re = re.compile(
        r'\b(?:public|internal|protected)\s+'
        r'(?:static\s+|readonly\s+|const\s+|override\s+|virtual\s+|'
        r'abstract\s+|sealed\s+|async\s+|new\s+|partial\s+|required\s+)*'
        r'(?:[\w\.\<\>\[\]\?\,\s]+?)\s(\w+)\s*(?:\(|\{|=>|;|=)'
    )
    getprop_re = re.compile(
        r'^\s*(?:public|internal)\s+(?:static\s+)?(?:partial\s+)?'
        r'(?:[\w\.\<\>\[\]\?\,\s]+?)\s(\w+)\s*\{\s*get', re.M
    )
    # NOTE the space inside the type character class. Without it this
    # regex matches nothing at all for any tuple-typed field, because a
    # tuple type like `(string Key, string Label)[]` contains spaces and
    # cannot be consumed by a space-free class.
    field_re = re.compile(
        r'^[ \t]*(?:public|internal)[ \t]+(?:static[ \t]+)?'
        r'(?:readonly[ \t]+|const[ \t]+)?'
        r'(?:[\w\.\<\>\[\]\,\(\)\ \t]+?)[ \t]+(\w+)[ \t]*(?:=|;)', re.M
    )
    enum_re = re.compile(r'\benum\s+(\w+)\s*\{([^}]*)\}', re.S)
    next_type_re = re.compile(
        r'\n    (?:public|internal|private|protected)?\s*'
        r'(?:sealed |abstract |static |partial )*'
        r'(?:class|struct|interface|record)\s+\w+'
    )

    for root, dirs, files in os.walk("."):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if not f.endswith(".cs"):
                continue
            src = open(os.path.join(root, f), encoding="utf-8",
                       errors="replace").read()
            for m in re.finditer(r'^namespace\s+([\w.]+)', src, re.M):
                repo_ns.add(m.group(1))

            for tm in re.finditer(decl_re, src):
                name = tm.group(1)
                nxt = next_type_re.search(src[tm.end():])
                body = src[tm.end(): tm.end() + (nxt.start() if nxt else len(src))]
                acc = members.setdefault(name, set())
                for mm in member_re.finditer(body):
                    acc.add(mm.group(1))
                for mm in getprop_re.finditer(body):
                    acc.add(mm.group(1))
                for mm in field_re.finditer(body):
                    acc.add(mm.group(1))
                # a type always "has" itself
                acc.add(name)

            for em in enum_re.finditer(src):
                members.setdefault(em.group(1), set()).update(
                    re.findall(r'\b(\w+)\b', em.group(2))
                )

    return repo_ns, members


def gather_parameter_names(root):
    """Every identifier that appears in any parameter list, keyed by method
    name. Used to decide whether a `name:` argument is plausible at all."""
    params = set()
    decl = re.compile(r'\b\w+\s*(?:<[^>]*>)?\s*\(([^()]*(?:\([^()]*\)[^()]*)*)\)'
                      r'\s*(?:where\b[^{=>]*)?(?:=>|\{|;)')
    for root_dir, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if not f.endswith(".cs"):
                continue
            src = strip_noise(open(os.path.join(root_dir, f),
                                   encoding="utf-8", errors="replace").read())
            for d in decl.finditer(src):
                args = d.group(1)
                params.update(
                    re.findall(r'(?:[\w\.\<\>\[\]\?]+\s+)(\w+)\s*(?:=[^,]+)?'
                               r'(?:,|$)', args)
                )
                params.update(re.findall(r'\(\s*[\w\.\<\>\[\]\?]+\s+(\w+)', args))
    return params


def select_files(args, root):
    if args and args[0] == "--all-theme":
        files = []
        for pat in THEME_GLOBS:
            files += glob.glob(os.path.join(root, pat))
        return sorted(set(files))
    if args:
        return [a for a in args if a.endswith(".cs") and os.path.exists(a)]
    changed = subprocess.run(["git", "diff", "--name-only"],
                             capture_output=True, text=True, cwd=root)
    return sorted(set(changed.stdout.split()))


def main():
    root = repo_root()
    os.chdir(root)
    files = [f for f in select_files(sys.argv[1:], root)
             if f.endswith(".cs") and os.path.exists(f)]
    if not files:
        print("No C# files to check.")
        return

    repo_ns, members = collect_repo_symbols()
    params = gather_parameter_names(root)

    findings = []
    for f in files:
        raw = open(f, encoding="utf-8", errors="replace").read()
        code = strip_noise(raw)

        # CS0246 -- namespace not found
        for i, line in enumerate(code.split("\n"), 1):
            m = re.match(r'\s*using\s+(?:static\s+)?([\w.]+)\s*;', line)
            if not m:
                continue
            ns = m.group(1)
            if ns in BCL_NS or ns in repo_ns:
                continue
            if any(ns == r or ns.startswith(r + ".") for r in repo_ns):
                continue
            findings.append((f, i, f"CS0246 namespace not found: {ns}"))

        # CS0117 -- member not declared
        for i, line in enumerate(code.split("\n"), 1):
            for tm in re.finditer(r'\b([A-Z]\w+)\s*\.\s*(\w+)', line):
                tn, mn = tm.group(1), tm.group(2)
                if tn in KNOWN_INHERITED or tn not in members:
                    continue
                if tn in BCL_NS:
                    continue
                if mn in members[tn]:
                    continue
                # inherited / array / indexer shapes the checker cannot see
                if mn in ("Length", "Count", "Key", "Item", "Default"):
                    continue
                findings.append((f, i, f"CS0117 {tn}.{mn} not declared"))

        # CS1739 -- named argument matching no parameter
        keywords = {"case", "default", "var", "return", "new", "nameof",
                    "ref", "out", "in", "this", "base", "is", "as", "when"}
        for i, line in enumerate(code.split("\n"), 1):
            for nm in re.findall(r'(?<![\w.])(\w+):\s', line):
                if nm in keywords or nm in params:
                    continue
                findings.append(
                    (f, i, f"CS1739 named argument '{nm}:' matches no parameter")
                )

        # CS0246 (reverse direction) -- a framework type is USED but the
        # namespace declaring it was never imported. Every using in the
        # file can be valid and this still fires.
        #
        # Iterated line by line over `code` rather than over a
        # using-stripped copy: stripping the usings first renumbers every
        # line, and reporting the stripped line number points the reader
        # at the wrong line entirely.
        imported = set(re.findall(r'^using\s+([\w.]+)\s*;', code, re.M))
        declared_ns = re.search(r'^namespace\s+([\w.]+)', code, re.M)
        own_ns = declared_ns.group(1) if declared_ns else ""
        # An enclosing namespace is implicitly in scope in C#, so a type
        # in a parent namespace needs no using.
        in_scope = set()
        cur = own_ns
        while cur:
            in_scope.add(cur)
            cur = cur.rpartition(".")[0]

        missing_ns = {
            ns for _, ns in TYPE_NS.items()
            if ns not in imported and ns not in in_scope and ns not in IMPLICIT_NS
        }
        if missing_ns:
            # A bare identifier match is not enough. "Window" and
            # "Control" are property names on EightColorRamp, and "Path"
            # and "Line" are record members in the test files -- none of
            # those are type references and all of them fired on the
            # first run of this check.
            #
            # So the name has to appear in TYPE POSITION: after new,
            # typeof(, a cast, is/as, or a parameter/argument separator.
            # It must also not be a member access, which rules out
            # Path.Combine.
            TYPE_POSITION = (
                r'(?:new\s+|typeof\s*\(\s*|\bis\s+|\bas\s+|'
                r'\(\s*|\(\s*[^)]*,\s*|,\s*|<\s*)'
            )
            for i, line in enumerate(code.split("\n"), 1):
                if line.lstrip().startswith("using "):
                    continue
                for t, ns in sorted(TYPE_NS.items()):
                    if ns not in missing_ns:
                        continue
                    pattern = (TYPE_POSITION + re.escape(t)
                               + r'\b(?!\s*\.)')
                    if re.search(pattern, line):
                        findings.append(
                            (f, i, f"CS0246 '{t}' used but '{ns}' not imported")
                        )
                        missing_ns.discard(ns)
                        break

    seen, unique = set(), []
    for f, i, msg in findings:
        if (f, msg) in seen:
            continue
        seen.add((f, msg))
        unique.append((f, i, msg))

    print(f"symbol-audit: {len(files)} files, "
          f"{len(repo_ns)} namespaces, {len(members)} types")
    if not unique:
        print("  clean -- no unresolved namespace, member or named argument.")
        return
    print(f"  {len(unique)} finding(s):\n")
    for f, i, msg in sorted(unique):
        print(f"  {f}:{i}  {msg}")
    print("\n  Confirm each against the source before acting. Inherited WPF "
          "members,\n  array .Length and fields whose declaration shape this "
          "script does not model\n  will appear here as false positives.")


if __name__ == "__main__":
    main()
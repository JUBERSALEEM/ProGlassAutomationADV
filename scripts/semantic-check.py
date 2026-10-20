#!/usr/bin/env python3
"""
Semantic checks for WPF code.

Every rule here exists because it caught a REAL defect that the existing
battery reported as clean. None is speculative.

WHY THIS FILE EXISTS
--------------------
csc-check resolves REPO TYPES only, so it cannot see the BCL at all: a
missing `using System.Windows.Media;` passes every check and then fails
the Windows build. Nine such defects shipped before this rule existed.

The rest are RUNTIME semantics nothing here can otherwise see:

  · freezing a TransformGroup, then animating its children -- throws on
    the first frame. Shipped; it crashed every Aurora theme load.
  · IsHitTestVisible=False on a control that WRAPS content -- disables
    hit-testing for the entire subtree. Shipped; the whole app became
    unclickable while looking perfect.
  · gating motion on a TRANSPARENCY flag, because high contrast was
    mistaken for reduced motion. Shipped twice.
  · resolving a palette with a hardcoded light/dark literal, so dark
    mode painted the wrong ramp.

WHY CSB001 USES TYPE POSITIONS, NOT NAMES
-------------------------------------------
The first version of this rule matched bare names and produced 33
findings of which 21 were noise:

  · ImplicitUsings is enabled, so System, System.Collections.Generic and
    System.Linq are already imported -- the List/IEnumerable findings were
    wrong
  · `public Color Window { get; }` and `BentoGrid = "Bento Grid"` match
    the word but are not type references
  · `(string Key, string Name)` is a record parameter name

A bare name cannot tell a type from a property, a local, a string, or an
enum member. So this rule only fires when the symbol appears in a
position where a TYPE must appear: a base-list entry, `new X(`, `typeof`,
`is`, `as`, a cast, a generic argument, or a declaration head.

Run:  python3 scripts/semantic-check.py
Exit: 1 if any rule fires.
"""

import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

# ImplicitUsings=enable injects these for a WPF net8.0 project.
IMPLICIT_USINGS = {
    "System", "System.Collections.Generic", "System.IO", "System.Linq",
    "System.Net.Http", "System.Threading", "System.Threading.Tasks",
}

# ── BCL SYMBOL -> DECLARING NAMESPACE ─────────────────────────────────
# Only symbols that are genuinely ambiguous with an everyday word. A
# symbol absent from this map is simply unchecked, which is safe; a
# symbol with the WRONG namespace produces false alarms, which is not.
BCL = {
    # Ambiguous with common property / variable names, so worth checking.
    "ButtonBase": "System.Windows.Controls.Primitives",
    "Thumb": "System.Windows.Controls.Primitives",
    "RangeBase": "System.Windows.Controls.Primitives",
    "KeyboardNavigation": "System.Windows.Input",
    "ModifierKeys": "System.Windows.Input",
    "DependencyPropertyChangedEventArgs": "System.Windows",
    "FrameworkPropertyMetadata": "System.Windows",
    "CornerRadius": "System.Windows",
    "SystemParameters": "System.Windows",
    "ThicknessAnimation": "System.Windows.Media.Animation",
    "DrawingVisual": "System.Windows.Media",
    "DropShadowEffect": "System.Windows.Media.Effects",
    "OuterShadowEffect": "System.Windows.Media.Effects",
    "BitmapCache": "System.Windows.Media",
    "TextBlock": "System.Windows.Controls",
    "UserControl": "System.Windows.Controls",
    "ScrollViewer": "System.Windows.Controls",
    "ItemsControl": "System.Windows.Controls",
    "ContentControl": "System.Windows.Controls",
    "Decorator": "System.Windows.Controls",
    "ToolTip": "System.Windows",
    "ContextMenu": "System.Windows.Controls",
    "GlyphRun": "System.Windows.Media",
    "FormattedText": "System.Windows.Media",
    "Dispatcher": "System.Windows.Threading",
    "Application": "System.Windows",
}

# Types that can WRAP arbitrary content through a Content property. Only
# these can host the app inside them, which is what makes
# IsHitTestVisible=False on them a bug.
#
# A bare Canvas is deliberately excluded. CrystalAmbientBackground is a
# Canvas that opts out of hit-testing precisely because it is an ambient
# background that must never intercept a click -- the same visual intent
# as the decorative Ellipses, just applied to the whole control. The rule
# cannot tell an ambient backdrop from a content wrapper, so it errs
# toward the unambiguous case and says so.
CONTENT_HOSTS = {
    "ContentControl", "Control", "UserControl", "Decorator",
    "ItemsControl", "Window", "ContentPresenter", "ListBox", "ListBoxItem",
}


def csharp_files():
    out = []
    for pat in ("Controls/**/*.cs", "Services/Theme/**/*.cs", "Behaviors/*.cs",
                "Scripts/*.cs", "Converters/*.cs", "ViewModels/**/*.cs"):
        out += glob.glob(pat, recursive=True)
    return sorted(f for f in out
                  if "/obj/" not in f and "/bin/" not in f)


def xaml_files():
    """Every XAML file in the tree, excluding build output.

    SEM006/SEM007 need the whole tree, not a family directory: a part
    name is declared in one dictionary and consumed by C# in a directory
    that shares nothing but the name.
    """
    out = []
    for pat in ("Themes/**/*.xaml", "Views/**/*.xaml", "Controls/**/*.xaml",
                "Styles/**/*.xaml", "*.xaml"):
        out += glob.glob(pat, recursive=True)
    return sorted(f for f in out
                  if "/obj/" not in f and "/bin/" not in f)


def strip_comments_only(src):
    """Strip comments but KEEP string literals.

    CSB001 needs literals removed -- "Bento Grid" must not match Grid --
    but SEM005 needs the literal "PART_AuroraFocus" to survive, because
    that string IS the part name it is looking for.

    SEM005 originally reused strip() and therefore deleted the evidence it
    was searching for, and reported a focus cue that demonstrably existed
    in the file. A checker that cannot see its own subject is worse than
    no checker, so the two jobs now have two functions."""
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return "\n".join(l.split("//")[0] for l in src.splitlines())


def strip(src):
    """Remove comments AND string/char literals.

    Comments are prose and must not trigger an absence rule. String
    contents -- "Bento Grid", "Neo-Brutalism" -- are data, and matching
    them for a type named Grid is meaningless."""
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'"(?:\\.|[^"\\])*"', '""', src)
    src = re.sub(r"'(?:\\.|[^'\\])'", "'x'", src)
    return "\n".join(l.split("//")[0] for l in src.splitlines())


def used_as_type(code, sym):
    """True only where a TYPE is grammatically required."""
    S = re.escape(sym)
    positions = [
        rf":\s*{S}\b",                          # base list
        rf"\bnew\s+{S}\s*[(<]",                  # object creation
        rf"typeof\s*\(\s*{S}\b",                # typeof
        rf"\bis\s+{S}\b",                       # type test
        rf"\bas\s+{S}\b",                       # cast
        rf"\({S}\s*\)",                         # cast
        rf"\b{S}\s*<",                          # generic
        rf"\b{S}\s+_?\w+\s*(?:[=;,)])",          # declaration
        rf"\b{S}\s*\.\s*(?:Create|Default|Parse)",  # static member
    ]
    return any(re.search(p, code) for p in positions)


def usings(code):
    return set(re.findall(r"using\s+([\w.]+)\s*;", code))


def file_namespace(src):
    m = re.search(r"^namespace\s+([\w.]+)", src, re.M)
    return m.group(1) if m else ""


def satisfied(ns, us, ns_of):
    if ns in us or ns in IMPLICIT_USINGS:
        return True
    return ns_of == ns or ns_of.startswith(ns + ".")


# ── RULES ──────────────────────────────────────────────────────────────

def rule_csb001(findings):
    """A BCL type used with no namespace that declares it (CS0246)."""
    for path in csharp_files():
        raw = open(path, encoding="utf-8", errors="ignore").read()
        code = strip(raw)
        us, ns_of = usings(code), file_namespace(raw)

        for sym, ns in BCL.items():
            if not used_as_type(code, sym):
                continue
            if satisfied(ns, us, ns_of):
                continue
            findings.append(
                f"{path}: CSB001 uses {sym} as a type with no "
                f"'using {ns};' (invisible to csc-check)")


def rule_sem001(findings):
    """A frozen Freezable whose properties are then animated.

    TransformGroup.Freeze() propagates to its Freezable children, so
    BeginAnimation on them throws InvalidOperationException."""
    for path in csharp_files():
        code = strip(open(path, encoding="utf-8", errors="ignore").read())
        frozen = {}
        for m in re.finditer(r"(\w+)\.Freeze\(\)", code):
            frozen.setdefault(m.group(1), m.start())
        for m in re.finditer(r"\b(\w+)\.BeginAnimation", code):
            v = m.group(1)
            if v in frozen and frozen[v] < m.start():
                findings.append(
                    f"{path}: SEM001 {v} is frozen then animated; "
                    f"BeginAnimation on a frozen Freezable throws")


def rule_sem002(findings):
    """IsHitTestVisible=False as a STATEMENT on a class that wraps content.

    Statement form only. `IsHitTestVisible = false` inside a
    `new Ellipse { ... }` initializer is a decorative element opting out,
    which is correct and is what the crystals and chart glows do."""
    for path in csharp_files():
        raw = open(path, encoding="utf-8", errors="ignore").read()
        code = strip(raw)
        m = re.search(r"class\s+(\w+)\s*:\s*([\w.]+)", raw)
        if not m:
            continue
        cls, base = m.group(1), m.group(2).split(".")[-1]
        if base not in CONTENT_HOSTS:
            continue
        if re.search(r"^\s*IsHitTestVisible\s*=\s*false\s*;\s*$", code, re.M):
            findings.append(
                f"{path}: SEM002 {cls} (: {base}) sets "
                f"IsHitTestVisible=False as a statement; it wraps "
                f"content, disabling hit-testing for everything inside")


def rule_sem003(findings):
    """A TRANSPARENCY flag used as a motion preference.

    High contrast strips translucency; it is not an animation
    preference. Shipped twice before this rule existed."""
    for path in csharp_files():
        code = strip(open(path, encoding="utf-8", errors="ignore").read())
        for m in re.finditer(r"TransparencySuppressed", code):
            win = code[max(0, m.start() - 200):m.start() + 200]
            if re.search(r"ReducedMotion|Motion|Drift|Bounce|Oscillat", win):
                findings.append(
                    f"{path}: SEM003 TransparencySuppressed used near a "
                    f"motion decision; use ReducedMotionRequested")


def rule_sem004(findings):
    """A control that hardcodes Palette.For(false/true)."""
    for path in csharp_files():
        code = strip(open(path, encoding="utf-8", errors="ignore").read())
        # The group MUST be capturing: the message below reads group(1).
        # It was (?:...) which raised IndexError, killed the whole rule, and
        # the crash was printed among the findings -- so this rule had never
        # once actually run. Fault-injecting it is the only thing that found it.
        for m in re.finditer(r"Palette\.For\(\s*(false|true)\s*\)", code):
            findings.append(
                f"{path}: SEM004 hardcodes Palette.For({m.group(1)}); "
                f"resolve the ramp for the current theme instead")


def rule_sem005(findings):
    """FocusVisualStyle=null with no focus cue anywhere in the family.

    The focus part is usually looked up by the family's Parts class
    rather than the control class, and drawn by the template, so this
    checks the whole family directory plus the theme dictionary."""
    for path in csharp_files():
        raw = open(path, encoding="utf-8", errors="ignore").read()
        if not re.search(r"FocusVisualStyle\s*=\s*null", strip(raw)):
            continue
        code = strip_comments_only(raw)
        if re.search(r'FindName\(\s*"PART_\w*[Ff]ocus', code):
            continue
        fam = path.split(os.sep)[1] if path.count(os.sep) > 1 else ""
        if not fam:
            continue
        fam_dir = os.path.join("Controls", fam)
        others = []
        if os.path.isdir(fam_dir):
            others = [strip_comments_only(
                          open(p, encoding="utf-8",
                               errors="ignore").read())
                      for p in glob.glob(os.path.join(fam_dir, "*.cs"))]
        if any(re.search(r'FindName\(\s*"PART_\w*[Ff]ocus', o)
               for o in others):
            continue
        # A missing directory is just another way of NOT finding a cue; it
        # must not skip the finding. The old shape nested the report inside
        # `if isdir(theme_dir): ... for/else:`, so when Themes/<fam>/ did
        # not exist the function fell off the end and passed in silence --
        # any family with no theme folder got a free ride on this rule.
        # glob() on a missing directory returns [], so absence needs no test.
        theme_dir = os.path.join("Themes", fam)
        themed = [strip(open(x, encoding="utf-8", errors="ignore").read())
                  for x in glob.glob(os.path.join(theme_dir, "*.xaml"))]
        if any(re.search(r"PART_\w*[Ff]ocus|FocusRing", xc) for xc in themed):
            continue
        cls = re.search(r"class\s+(\w+)", raw)
        findings.append(
            f"{path}: SEM005 {cls.group(1) if cls else '?'} sets "
            f"FocusVisualStyle=null and neither its family "
            f"directory nor Themes/{fam}/ declares a focus cue")


def rule_sem006(findings):
    """A Template= attribute applied to an element that has no Template.

    Shipped as MC3072 in Themes/Claymorphism/ClayControls.xaml: the
    slider thumb was a <Border ... Template="..."/>. Border derives from
    Decorator, which derives from FrameworkElement -- NOT Control -- and
    Template is declared by Control. The markup compiler rejects it, and
    a markup-compile failure produces no assembly at all, so every
    clr-namespace type in the solution then reports XDG0008 in the
    designer. One bad attribute made three unrelated families look
    broken.

    Note what this is NOT: it is not a type-existence check. xaml-check
    already proves every clr-namespace name resolves, and it reported
    this file clean, because Border certainly does exist. The defect is
    an attribute that is illegal FOR THAT ELEMENT, which no amount of
    name resolution will ever catch.
    """
    # Elements WPF defines that are FrameworkElement-derived but not
    # Control-derived, and so have no Template property to set.
    not_control = {
        "Border", "Decorator", "Viewbox", "BulletDecorator",
        "AdornerDecorator", "InkPresenter", "Grid", "StackPanel",
        "Canvas", "DockPanel", "WrapPanel", "UniformGrid",
        "VirtualizingStackPanel", "Panel", "Rectangle", "Ellipse",
        "Line", "Path", "Polygon", "Polyline", "TextBlock", "Image",
        "Shape", "AccessText", "ContentPresenter", "ItemsPresenter",
        "ScrollContentPresenter",
    }
    for path in xaml_files():
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue  # malformed XAML is xaml-check's problem, not ours
        src = open(path, encoding="utf-8", errors="ignore").read().splitlines()
        for el in tree.iter():
            if "Template" not in el.attrib:
                continue
            local = el.tag.split("}")[-1]
            if local not in not_control:
                continue
            line = 0
            for i, text in enumerate(src, 1):
                if "Template=" in text:
                    line = i
                    break
            findings.append(
                f"{path}:{line}: SEM006 <{local}> sets Template=, but "
                f"{local} derives from FrameworkElement, not Control, so "
                f"it has no Template property (MC3072). Use a "
                f"Control-derived element, or move the template onto one")


def rule_sem007(findings):
    """FindName("X") as Control where the template declares X as a
    non-Control element.

    Same root cause as SEM006 seen from the C# side, and it fails
    QUIETLY instead of loudly: `as` yields null, the `if (x is not null)`
    guard skips the work, and the part is simply never painted. No
    exception, no log line, no compile error. The clay slider thumb
    shipped exactly this -- Border, cast as Control -- so its second
    mould (highlight, shade, edge) was never bound and the thumb
    rendered as bare geometry for as long as the control existed.

    SEM006 catches the compile-time half. This catches the runtime half,
    which survives even when someone "fixes" the XAML by removing the
    Template= attribute and leaving the element type alone.
    """
    # x:Name -> element tag across every XAML file
    name_to_tag = {}
    for path in xaml_files():
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for el in tree.iter():
            n = el.get("{http://schemas.microsoft.com/winfx/2006/xaml}Name")
            if n:
                name_to_tag.setdefault(n, []).append(
                    (path, el.tag.split("}")[-1]))

    # Casts that demand a Control-derived element.
    cast_re = re.compile(
        r'FindName\(\s*"([^"]+)"[^)]*\)\s*as\s*(Control|ContentControl|'
        r'ButtonBase|Button|Thumb|TextBoxBase)\b')
    not_control = {
        "Border", "Decorator", "Viewbox", "Grid", "StackPanel", "Canvas",
        "DockPanel", "WrapPanel", "UniformGrid", "Panel", "Rectangle",
        "Ellipse", "Line", "Path", "Polygon", "Polyline", "TextBlock",
        "Image", "Shape", "AccessText", "ContentPresenter",
        "ItemsPresenter", "ScrollContentPresenter",
    }
    for path in csharp_files():
        code = strip_comments_only(
            open(path, encoding="utf-8", errors="ignore").read())
        for i, line in enumerate(code.splitlines(), 1):
            for m in cast_re.finditer(line):
                name, cast = m.group(1), m.group(2)
                for xp, tag in name_to_tag.get(name, []):
                    if tag not in not_control:
                        continue
                    findings.append(
                        f"{path}:{i}: SEM007 FindName(\"{name}\") as "
                        f"{cast}, but {xp} declares it as <{tag}> -- "
                        f"{tag} is not a Control, so `as` yields null and "
                        f"the part is silently never bound")


def rule_sem008(findings):
    """A member accessed on a type that provably does not declare it.

    This is the defect class that shipped FORTY-THREE times in one
    build: FrameworkElement.Template, FrameworkElement.Background,
    Control.IsPressed, DoubleAnimation.KeyFrames, IReadOnlyList.Add,
    RadialGradientBrush.GradientUnits, `new DoubleKeyFrame(...)`.

    Every one of them is CS1061/CS0103/CS0119/CS0144, and every one
    passed every check this repository had. csc-check's footer says so
    out loud: "Frameworks methods (WPF/BCL) are NOT resolved". The tool
    was honest; the mistake was reporting "every call and override
    resolves" as though that meant the code compiles.

    WHY THIS IS A DENY LIST AND NOT AN ALLOW LIST
    ---------------------------------------------
    csc-check already has an allow list of known WPF virtuals, and it
    hid a real CS0115 for the entire life of that rule, because
    "OnSizeChanged" was on it. An allow list fails OPEN: anything not
    enumerated is treated as fine. A deny list fails CLOSED: it only
    fires on combinations proven impossible, and it cannot silently
    stop working just because someone added a plausible-sounding name.

    Every entry below was reported by a real Windows compiler. Nothing
    is here because it sounded right.
    """
    # type -> members that type provably does not have.
    impossible = {
        # Template/Background/Padding/Content all live below
        # FrameworkElement, on Control, Border or Panel.
        "FrameworkElement": ["Template", "Background", "BorderBrush",
                             "BorderThickness", "Padding", "Content",
                             "Foreground", "CornerRadius", "IsPressed"],
        "UIElement": ["Template", "Background", "BorderThickness",
                      "Padding", "Content", "Foreground", "IsPressed"],
        # IsPressed is on ButtonBase; Control is one level too high.
        "Control": ["IsPressed", "IsChecked"],
        # KeyFrames is on *AnimationUsingKeyFrames, not on the
        # plain from/to animation.
        "DoubleAnimation": ["KeyFrames"],
        "ColorAnimation": ["KeyFrames"],
        "PointAnimation": ["KeyFrames"],
        # WPF spells this MappingMode / BrushMappingMode.
        "GradientBrush": ["GradientUnits"],
        "LinearGradientBrush": ["GradientUnits"],
        "RadialGradientBrush": ["GradientUnits"],
        # Read-only views have no mutators.
        "IReadOnlyList": ["Add", "Remove", "Clear", "Insert", "RemoveAt"],
        "IReadOnlyCollection": ["Add", "Remove", "Clear", "Insert"],
        "IReadOnlyDictionary": ["Add", "Remove", "Clear"],
    }

    # `as Border x` / `is Border x` / `IReadOnlyList<string> xs`
    simple = re.compile(r'\b(?:as|is)\s+([A-Z]\w*)\s+(\w+)\b')
    generic = re.compile(r'\b(?:as|is)\s+([A-Z]\w*)\s*<[^>]*>\s+(\w+)\b')
    # a declared parameter/local: `FrameworkElement root)`
    param = re.compile(r'\b([A-Z]\w*)\s+(\w+)\s*\)')

    for path in csharp_files():
        code = strip(open(path, encoding="utf-8", errors="ignore").read())
        lines = code.splitlines()
        typed = []
        for i, line in enumerate(lines):
            for pat in (generic, simple, param):
                for m in pat.finditer(line):
                    typed.append((i, m.group(1), m.group(2)))
        for i, tname, var in typed:
            bad = impossible.get(tname)
            if not bad:
                continue
            for j in range(i, min(len(lines), i + 30)):
                for mem in bad:
                    if re.search(r'\b%s\s*\.\s*%s\b' % (re.escape(var), mem),
                                 lines[j]):
                        findings.append(
                            f"{path}:{j + 1}: SEM008 '{var}' is typed "
                            f"{tname}, which has no '{mem}' -- "
                            f"{mem} is declared further down the WPF "
                            f"hierarchy (CS1061). Check the declared "
                            f"type of the variable, not the call site")


def rule_sem009(findings):
    """Instantiating an abstract WPF type.

    CS0144. The *KeyFrame classes are abstract; the concrete ones are
    Linear/Easing/Discrete/Spline<Type>KeyFrame. `new DoubleKeyFrame(...)`
    reads perfectly reasonably and cannot be told apart from
    `new LinearDoubleKeyFrame(...)` without knowing the hierarchy --
    which is precisely what no text-based check in this repository has.
    """
    abstract = ("Double", "Color", "Point", "Size", "Object", "String",
                "Boolean", "Char", "Int16", "Int32", "Int64", "Single",
                "Matrix", "Rect", "Vector", "Thickness", "CornerRadius",
                "Decimal", "Byte", "Int16")
    pat = re.compile(r'\bnew\s+(?:%s)KeyFrame\s*\(' % "|".join(abstract))
    for path in csharp_files():
        code = strip(open(path, encoding="utf-8", errors="ignore").read())
        for i, line in enumerate(code.splitlines(), 1):
            for m in pat.finditer(line):
                name = m.group(0)[4:m.group(0).index("(")].strip()
                findings.append(
                    f"{path}:{i}: SEM009 '{name}' is abstract and cannot "
                    f"be instantiated (CS0144). Use "
                    f"Linear{name}, Easing{name}, Discrete{name} or "
                    f"Spline{name}")


RULES = [rule_csb001, rule_sem001, rule_sem002,
         rule_sem003, rule_sem004, rule_sem005,
         rule_sem006, rule_sem007, rule_sem008, rule_sem009]


def main():
    os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    files = csharp_files()

    findings, crashed = [], []
    for rule in RULES:
        try:
            rule(findings)
        except Exception as exc:
            crashed.append(f"{rule.__name__}: {type(exc).__name__}: {exc}")

    seen, unique = set(), []
    for f in findings:
        if f not in seen:
            seen.add(f)
            unique.append(f)

    for c in crashed:
        print(f"  !! RULE CRASHED, ITS CHECKS DID NOT RUN: {c}")
    if crashed:
        print(f"semantic-check: {len(files)} file(s), {len(RULES)} rules, "
              f"{len(RULES) - len(crashed)} ran")
        print("  CHECKER BROKEN -- results above are incomplete.")
        return 2
    for f in unique:
        print(f"  {f}")
    print(f"semantic-check: {len(files)} file(s), {len(RULES)} rules, "
          f"{len(unique)} finding(s)")
    print("  clean." if not unique else "  NOT clean.")
    return 1 if unique else 0


if __name__ == "__main__":
    sys.exit(main())

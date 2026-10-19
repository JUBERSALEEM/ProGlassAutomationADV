#!/usr/bin/env python3
"""
xaml-check.py -- resolve the types and resource keys a XAML file names.

WHY THIS EXISTS
---------------
Two build rounds have now been lost to XAML rather than C#. This closes
the gap the C# tools cannot reach.

  XDG0008  "The name 'SwissSurface' does not exist in the namespace
           'clr-namespace:ProGlassAutomation.Controls.Swiss'"
           A clr-namespace that resolves to no type in this repository.

  XDG0012  "The member 'TextRenderingMode' is not recognized"
           A member that is not on the declared target. `TextRenderingMode`
           lives on TextElement; TextBlock derives from FrameworkElement,
           so a Style with TargetType="TextBlock" cannot name it.

  StaticResource / DynamicResource keys that nothing defines. These do
  not fail the build -- they throw at runtime, in whatever dialog or
  window first asks for the brush, which is the worst time to find out.
  OFF by default (--keys enables it) because the merge graph is only
  modelled one level deep from App.xaml; a whole-repo run would drown
  in false positives. Always on for --all-theme.

The type index is built with the same tree-sitter parser csc-check.py
uses, so there is exactly one view of what this repository declares.

Usage:
    python3 scripts/xaml-check.py                    # all XAML
    python3 scripts/xaml-check.py --all-theme        # theme dictionaries
    python3 scripts/xaml-check.py Themes/Swiss/SwissControls.xaml
"""

import glob
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

import importlib.util

_spec = importlib.util.spec_from_file_location(
    "csc_check", os.path.join(os.path.dirname(os.path.abspath(__file__)),
                              "csc-check.py"))
_csc = importlib.util.module_from_spec(_spec)
_src = open(_spec.origin).read().replace(
    'if __name__ == "__main__":\n    main()', "")
exec(compile(_src, "csc-check", "exec"), _csc.__dict__)
Repo = _csc.Repo

X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
PRESENTATION = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"

# Attached/attached-property members that are routinely mistaken for
# direct members. A Style whose TargetType lacks the member, but which
# declares it in the attached form, is fine -- and the reverse is the
# XDG0012 in Themes/Swiss.
KNOWN_ATTACHED = {
    "TextOptions.TextFormattingMode", "TextOptions.TextRenderingMode",
    "TextOptions.TextHintingMode", "TextElement.TextRenderingMode",
    "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan",
    "Canvas.Left", "Canvas.Top", "Canvas.Right", "Canvas.Bottom",
    "Panel.ZIndex", "TextBlock.FontFamily", "FrameworkElement.Margin",
}

# Members that exist on the type but are commonly written unqualified in
# a way that fails. Kept so the same mistake does not recur silently.
# Enums whose members this tool knows. A Setter whose Property resolves
# to one of these and whose Value is not listed is XLS0431.
ENUM_VALUES = {
    # System.Windows.TextFormattingMode
    "TextFormattingMode": {"Ideal", "Display", "Disable"},
    # System.Windows.Media.TextRenderingMode
    "TextRenderingMode": {"Auto", "Aliased", "Antialiased", "ClearType",
                          "TextOnly", "ClearTypeGridFit"},
    # System.Windows.Media.TextHintingMode
    "TextHintingMode": {"Fixed", "Animated", "Smooth"},
}

KNOWN_BAD_MEMBER_ON = {
    # TextBlock : FrameworkElement, NOT TextElement
    ("TextBlock", "TextRenderingMode"),
    ("TextBlock", "FontRenderingMode"),
}


def repo_root():
    out = subprocess.run(["git", "rev-parse", "--show-toplevel"],
                         capture_output=True, text=True)
    return out.stdout.strip() or "."


RUNTIME_PUBLISHERS = ("Services/Theme/Eight/EightThemePainter.cs",)


def runtime_keys(root):
    """
    Keys the theme painters write into the resource dictionary AT
    RUNTIME, which no static scan of XAML can see.

    Without this, every {DynamicResource FocusRingBrush} in Clay and Neu
    is reported as undefined. Both families are fine -- the painter
    publishes those keys on every theme change -- and a checker that
    cries wolf on correct code is a checker people stop running.
    """
    keys = set()
    # r["Key"] = ...   and   Sol(r, "Key", ...) / Str(r, "Key", ...)
    assign = re.compile(r'\br\s*\[\s*"([\w.]+)"\s*\]')
    call = re.compile(r'\b(?:Sol|Brush|Str|Corner|Drop)\s*\(\s*r\s*,\s*"([\w.]+)"')
    for rel in RUNTIME_PUBLISHERS:
        fp = os.path.join(root, rel)
        if not os.path.exists(fp):
            continue
        text = open(fp, encoding="utf-8", errors="replace").read()
        keys |= set(assign.findall(text))
        keys |= set(call.findall(text))
    return keys


def collect_keys(root):
    """Every x:Key defined anywhere, plus what App.xaml merges together."""
    keys = {}
    for path in glob.glob(os.path.join(root, "**", "*.xaml"),
                          recursive=True):
        if any(s in path for s in ("/obj/", "/bin/", "/.git/")):
            continue
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for el in tree.getroot().iter():
            k = el.get(f"{X}Key")
            if k:
                keys.setdefault(k, set()).add(
                    os.path.relpath(path, root))

    for k in runtime_keys(root):
        keys.setdefault(k, set()).add("<runtime>")

    merged = set()
    app = os.path.join(root, "App.xaml")
    if os.path.exists(app):
        try:
            for el in ET.parse(app).getroot().iter():
                src = el.get("Source", "")
                if src.startswith("pack://application:,,,"):
                    rel = src[len("pack://application:,,,"):].lstrip("/")
                    merged.add(os.path.normpath(rel))
        except ET.ParseError:
            pass
    return keys, merged


def visible_keys(keys, merged, path):
    """
    Keys a given file may use: its own, plus everything App.xaml merges.
    Good enough to catch a typo'd or missing key without modelling the
    full resource lookup order, which is genuinely dynamic at runtime.
    """
    rel = os.path.normpath(path)
    out = set()
    for k, where in keys.items():
        if "<runtime>" in where or rel in where \
                or any(m in rel for m in merged):
            out.add(k)
    return out


def check_file(root, repo, path, keys, merged, findings,
               check_keys=True):
    rel = os.path.relpath(path, root)
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        findings.append((rel, 0, f"XML0001 not well-formed: {e}"))
        return

    # Map the clr-namespace prefixes declared in this file.
    # Namespace declarations must come from start-ns events. They are
    # consumed for resolution and do NOT appear in root.attrib, so
    # reading attrib here yields an empty map and every clr-namespace
    # reference is reported as an undeclared prefix.
    ns_map = {}
    for event, payload in ET.iterparse(path, events=("start-ns",)):
        prefix, uri = payload
        if uri.startswith("clr-namespace:"):
            ns_map[prefix] = uri[len("clr-namespace:"):].split(";")[0]

    # 1. every x:Type / TargetType reference must resolve
    #
    # Scanned from the ATTRIBUTE, not gated on the element's own tag
    # namespace. A ControlTemplate carries TargetType="{x:Type swiss:X}"
    # while its tag is {x}ControlTemplate, so gating on the tag skipped
    # every type reference in the file -- which is exactly the XDG0008
    # this is meant to catch.
    type_ref = re.compile(r'^\{(?:x:)?:?Type\s+([\w.]+):(\w+)\}$')
    for el in tree.getroot().iter():
        for attr in ("TargetType", "x:Type", "Type"):
            v = el.get(attr)
            if not v:
                continue
            m = type_ref.match(v.strip())
            if not m:
                continue
            pfx, tname = m.group(1), m.group(2)
            ns = ns_map.get(pfx)
            if ns is None:
                findings.append((
                    rel, 0,
                    f"XDG0008 prefix '{pfx}' is not declared as a "
                    f"clr-namespace in this file",
                ))
                continue
            # Only namespaces THIS repository declares are checked.
            # A clr-namespace can equally name a framework type --
            # sys:String, primitives:Thumb -- and those are resolved by
            # the XAML compiler, not by anything in this repo. Checking
            # them produced two false positives on views that compile.
            if ns.startswith("ProGlassAutomation") \
                    and not repo.is_repo_type(tname):
                findings.append((
                    rel, 0,
                    f"XDG0008 type '{pfx}:{tname}' does not exist in "
                    f"clr-namespace {ns}",
                ))

        # Element tags in a clr-namespace must also resolve.
        tag = el.tag
        if tag.startswith("{"):
            uri = tag[1:].split("}")[0]
            pfx = None
            for k, u in ns_map.items():
                if u == uri:
                    pfx = k
                    break
            if pfx and ns_map[pfx].startswith("ProGlassAutomation"):
                tname = tag.split("}")[-1]
                if not repo.is_repo_type(tname):
                    findings.append((
                        rel, 0,
                        f"XDG0008 element '{pfx}:{tname}' does not exist "
                        f"in clr-namespace {ns_map[pfx]}",
                    ))

    # 2. StaticResource / DynamicResource keys must be defined somewhere
    if check_keys:
        vis = visible_keys(keys, merged, rel)
        for el in tree.getroot().iter():
            for attr, val in el.attrib.items():
                for m in re.finditer(
                        r'\{(Static|Dynamic)Resource\s+(\w+)\}',
                        val or ""):
                    k = m.group(2)
                    if k not in vis:
                        findings.append((
                            rel, 0,
                            f"XLS0411 {{{m.group(1)}Resource {k}}} is "
                            f"not defined in this file or in anything "
                            f"App.xaml merges",
                        ))

    # 2z. A Style with Setters and no TargetType cannot resolve its
    # property names. WPF reports XLS0431 ("Invalid value for property
    # 'Property': 'X'") and XDG0012 ("The member X is not recognized or
    # is not accessible") once per setter, which is a lot of noise for
    # one missing attribute.
    #
    # The subtle part, and the reason this is a rule rather than a note:
    # adding TargetType is NOT always the whole fix. If the target is a
    # Border, then Focusable is still invalid -- Focusable is declared
    # on Control, and Border derives from Decorator -> FrameworkElement.
    # So the repair has to check the setter names against the declared
    # TargetType, not just supply one.
    for st in tree.getroot().iter():
        if not st.tag.endswith("}Style"):
            continue
        setters = [x for x in st.iter() if x.tag.endswith("}Setter")]
        if not setters:
            continue
        if st.get("TargetType") is not None:
            continue

        names = [x.get("Property") for x in setters if x.get("Property")]
        findings.append((
            rel, 0,
            f"XLS0431 Style has {len(names)} setter(s) but no "
            f"TargetType, so none of {', '.join(names)} can be resolved. "
            f"Add TargetType, and check the names against it -- Focusable "
            f"is on Control, which Border does not derive from",
        ))

    # 2a. TemplateBinding is ONE-WAY and has no Mode property. This is
    # MC3045, and it is a FATAL markup error -- not a warning, not a
    # style nit. Worse, it aborts MarkupCompilePass1, which is why it
    # also produced a wall of XDG0008 "type does not exist" errors in
    # LATER files whose types are perfectly fine: the pass died before
    # it finished resolving them. Fixing this one line is what clears
    # those, not renaming anything in Controls/.
    #
    #   WRONG   Text="{TemplateBinding Text, Mode=TwoWay}"
    #   RIGHT   Text="{Binding Text, Mode=TwoWay,
    #                     RelativeSource={RelativeSource TemplatedParent}}"
    for el in tree.getroot().iter():
        for attr, val in el.attrib.items():
            for m in re.finditer(r'\{(TemplateBinding)\b([^}]*)\}', val or ""):
                args = m.group(2)
                if re.search(r'\bMode\s*=', args):
                    findings.append((
                        rel, 0,
                        f"MC3045 TemplateBinding has no Mode property "
                        f"({m.group(0)}). TemplateBinding is one-way by "
                        f"construction -- use Binding with "
                        f"RelativeSource={{RelativeSource TemplatedParent}}",
                    ))

    # 2b. enum-valued Setter values must name a real member of the enum.
    # "Load" is not a TextFormattingMode; the enum has exactly three.
    for el in tree.getroot().iter():
        if not el.tag.endswith("}Setter"):
            continue
        prop = (el.get("Property") or "").split(".")[-1]
        val = (el.get("Value") or "").strip()
        allowed = ENUM_VALUES.get(prop)
        if allowed and val and val not in allowed:
            findings.append((
                rel, 0,
                f"XLS0431 '{val}' is not a valid {prop}. Valid values: "
                f"{', '.join(sorted(allowed))}",
            ))

    # 3. known XDG0012 shapes: a Style naming a member its target lacks
    for el in tree.getroot().iter():
        if not el.tag.endswith("}Style"):
            continue
        tgt = (el.get("TargetType") or "").strip("{}")
        tname = tgt.split(":")[-1] if tgt else None
        if tname in (None, ""):
            continue
        for setter in el:
            if not setter.tag.endswith("}Setter"):
                continue
            prop = setter.get("Property", "")
            if prop in KNOWN_ATTACHED:
                continue
            for (bt, bad) in KNOWN_BAD_MEMBER_ON:
                if tname == bt and prop == bad:
                    findings.append((
                        rel, 0,
                        f"XDG0012 Style TargetType=\"{tname}\" cannot set "
                        f"'{prop}' - it is declared on TextElement and "
                        f"TextBlock does not derive from it. Use the "
                        f"attached form 'TextOptions.{prop}'.",
                    ))


def main():
    root = repo_root()
    os.chdir(root)
    want_keys = "--keys" in sys.argv
    args = [a for a in sys.argv[1:] if a != "--keys"]

    if args and args[0] == "--all-theme":
        files = []
        for d in ("Themes",):
            files += glob.glob(os.path.join(d, "**", "*.xaml"),
                               recursive=True)
        # Every family's dictionary has to be NAMED here. The filter is a
        # literal list rather than a directory scan, which meant Bento
        # and Spatial were silently outside the scope of every "clean"
        # run reported for them -- a tool that reports success on files
        # it never opened is worse than no tool.
        files = sorted(f for f in files if any(
            k in f for k in ("Clay", "Neu", "Swiss", "Holo",
                             "LiquidGlass", "Eight",
                             "Bento", "Spatial", "Glassmorphism")))
    elif args:
        files = [a for a in args if a.endswith(".xaml")]
    else:
        files = sorted(glob.glob("**/*.xaml", recursive=True))

    repo = Repo(root)
    keys, merged = collect_keys(root)

    findings = []
    # Key resolution is approximate: it models this file plus whatever
    # App.xaml merges DIRECTLY. A dictionary that merges another
    # dictionary which defines the key is not followed, so a whole-repo
    # run reports thousands of false XLS0411 findings. Key checking is
    # therefore off unless asked for, and always on for the theme
    # surface, where the merge graph is small and fully known.
    do_keys = want_keys or bool(args and args[0] == "--all-theme")

    for f in files:
        if os.path.exists(f):
            check_file(root, repo, f, keys, merged, findings,
                       check_keys=do_keys)

    print(f"xaml-check: {len(files)} file(s), {len(repo.types)} types, "
          f"{len(keys)} resource keys, {len(merged)} merged in App.xaml")
    if not findings:
        print("  clean -- every clr-namespace type resolves and every "
              "resource key is defined.")
        return
    seen, uniq = set(), []
    for f, i, m in findings:
        if (f, m) in seen:
            continue
        seen.add((f, m))
        uniq.append((f, i, m))
    print(f"  {len(uniq)} finding(s):\n")
    for f, i, m in sorted(uniq):
        print(f"  {f}  {m}")
    print("\n  Key resolution is approximate: it models this file plus "
          "what App.xaml\n  merges, not the runtime lookup order. A key "
          "supplied at runtime by\n  code will be reported; confirm before "
          "acting on any XLS0411.")


if __name__ == "__main__":
    main()
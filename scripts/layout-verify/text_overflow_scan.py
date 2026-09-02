#!/usr/bin/env python3
"""
Detect text that visually collides in WPF because it cannot wrap.

In a Grid row of [ * text | Auto control ], a long TextBlock in the star
column normally wraps. If TextWrapping is not set (default NoWrap), the
TextBlock keeps its full desired width, the star column cannot shrink
below it, and the row pushes wider than the page -- content then appears
to run into / underneath the neighbouring control and off the edge.

Flags a TextBlock when ALL of:
  - it has literal Text (not just a binding) longer than THRESHOLD chars
  - TextWrapping is not set on the element or via its Style
  - it is not inside a control that wraps for you
"""
import sys, os, re
import xml.etree.ElementTree as ET

NS = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
X  = "{http://schemas.microsoft.com/winfx/2006/xaml}"
THRESHOLD = 60

def local(t):
    return t.rsplit('}', 1)[-1] if '}' in t else t

def style_ref_key(value):
    if not value:
        return None
    m = re.match(r"\{(?:Static|Dynamic)Resource\s+([^}]+)\}", value.strip())
    return m.group(1).strip() if m else None

def build_wrapping_styles(root_dir="."):
    """x:Key of every Style that sets TextWrapping on the element itself."""
    keys = set()
    for dirpath, _, filenames in os.walk(root_dir):
        if ".git" in dirpath:
            continue
        for fn in filenames:
            if not fn.endswith(".xaml"):
                continue
            try:
                t = ET.parse(os.path.join(dirpath, fn))
            except ET.ParseError:
                continue
            for st in t.getroot().iter():
                if local(st.tag) != "Style":
                    continue
                key = st.get(X + "Key")
                if not key:
                    continue
                def own(node, in_tpl=False):
                    for d in node:
                        lt = local(d.tag)
                        nested = in_tpl or lt in (
                            "ControlTemplate", "DataTemplate")
                        if (lt == "Setter" and not nested
                                and d.get("Property") == "TextWrapping"
                                and d.get("Value") in ("Wrap", "WrapWithOverflow")):
                            return True
                        if own(d, nested):
                            return True
                    return False
                if own(st):
                    keys.add(key)
    return keys

WRAPPERS = None

def scan(path):
    problems = []
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        return [f"XML parse error: {e}"]

    for el in tree.getroot().iter():
        if local(el.tag) != "TextBlock":
            continue
        text = el.get("Text") or ""
        # bindings are unknown length at design time -- skip
        if text.strip().startswith("{"):
            continue
        if len(text) <= THRESHOLD:
            continue
        if el.get("TextWrapping"):
            continue
        if el.get("TextTrimming"):
            continue
        k = style_ref_key(el.get("Style"))
        if k and WRAPPERS and k in WRAPPERS:
            continue
        if el.get("Width") or el.get("MaxWidth"):
            continue
        problems.append(
            f"NO-WRAP: {len(text)}-char TextBlock cannot wrap "
            f"(style={k or 'none'}): \"{text[:64]}...\"")
    return problems

def main(paths):
    global WRAPPERS
    WRAPPERS = build_wrapping_styles(".")
    total = 0
    for p in paths:
        pr = scan(p)
        if pr:
            total += len(pr)
            print(f"\n{p}")
            for x in pr:
                print(f"   {x}")
    print(f"\n{len(paths)} file(s) scanned, {total} problem(s)")
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

#!/usr/bin/env python3
"""
Detect ScrollViewers that grant INFINITE width/height to their content.

WPF measure rule:
  HorizontalScrollBarVisibility = Auto|Visible  -> child measured with
      availableWidth = infinity. Content never wraps to the viewport;
      TextWrapping and WrapPanel become INERT and content runs off-screen.
  HorizontalScrollBarVisibility = Disabled      -> child measured with
      availableWidth = viewport width. Wrapping works.

Same rule applies vertically for VerticalScrollBarVisibility.

Flags a ScrollViewer whose content DEPENDS on wrapping (contains a
WrapPanel, or a TextBlock/style that wraps) while horizontal scrolling is
not Disabled. That combination is almost always a bug.
"""
import sys, os, re
import xml.etree.ElementTree as ET

X = "{http://schemas.microsoft.com/winfx/2006/xaml}"

def local(t):
    return t.rsplit('}', 1)[-1] if '}' in t else t

def style_ref_key(v):
    if not v:
        return None
    m = re.match(r"\{(?:Static|Dynamic)Resource\s+([^}]+)\}", v.strip())
    return m.group(1).strip() if m else None

def build_wrapping_styles(root_dir="."):
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
                k = st.get(X + "Key")
                if not k:
                    continue
                for d in st.iter():
                    if (local(d.tag) == "Setter"
                            and d.get("Property") == "TextWrapping"
                            and d.get("Value") in ("Wrap", "WrapWithOverflow")):
                        keys.add(k)
                        break
    return keys

WRAPPERS = set()

def depends_on_wrapping(sv):
    reasons = []
    for el in sv.iter():
        lt = local(el.tag)
        if lt == "WrapPanel":
            reasons.append("contains a WrapPanel")
            break
    for el in sv.iter():
        if local(el.tag) != "TextBlock":
            continue
        if el.get("TextWrapping") in ("Wrap", "WrapWithOverflow"):
            reasons.append("contains wrapping TextBlock(s)")
            break
        k = style_ref_key(el.get("Style"))
        if k and k in WRAPPERS:
            reasons.append(f"contains TextBlock using wrapping style {k}")
            break
    return reasons

def scan(path):
    problems = []
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        return [f"XML parse error: {e}"]

    for sv in tree.getroot().iter():
        if local(sv.tag) != "ScrollViewer":
            continue
        h = (sv.get("HorizontalScrollBarVisibility") or "Auto").strip()
        if h.lower() == "disabled":
            continue
        reasons = depends_on_wrapping(sv)
        if reasons:
            problems.append(
                f"INFINITE WIDTH: ScrollViewer has "
                f"HorizontalScrollBarVisibility=\"{h}\" so its content is "
                f"measured at infinite width -- wrapping is INERT. It "
                f"{reasons[0]}. Set it to \"Disabled\".")
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

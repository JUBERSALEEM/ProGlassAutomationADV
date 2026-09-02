#!/usr/bin/env python3
"""
Simulate WPF horizontal layout to find content that CLIPS or COLLIDES.

Key WPF facts this models:
  - StackPanel Orientation="Horizontal" gives its children INFINITE width.
    Children never shrink, so a long TextBlock overflows and is clipped by
    the nearest bounding Border/ScrollViewer.
  - ScrollViewer defaults HorizontalScrollBarVisibility="Disabled", so
    content is HARD-CLIPPED at the viewport width, not scrolled.
  - A DockPanel's fill child receives (available - sum(docked desired)).
    If that is <= 0 the fill child collapses to zero width and vanishes.

Text width estimate for Segoe UI ~= 0.52 * fontSize per character,
bold ~= 0.56. Deliberately conservative: it UNDER-estimates, so anything
it flags is very likely real.
"""
import sys
import xml.etree.ElementTree as ET

def local(t):
    return t.rsplit('}', 1)[-1] if '}' in t else t

def num(v, default=0.0):
    try:
        return float(str(v).strip())
    except (TypeError, ValueError):
        return default

def margin_h(el):
    m = el.get("Margin")
    if not m:
        return 0.0
    parts = [num(x) for x in m.split(',')]
    if len(parts) == 1:
        return parts[0] * 2
    if len(parts) == 2:
        return parts[0] * 2
    if len(parts) == 4:
        return parts[0] + parts[2]
    return 0.0

def padding_h(el):
    return margin_h_attr(el, "Padding")

def margin_h_attr(el, attr):
    m = el.get(attr)
    if not m:
        return 0.0
    parts = [num(x) for x in m.split(',')]
    if len(parts) == 1:
        return parts[0] * 2
    if len(parts) == 2:
        return parts[0] * 2
    if len(parts) == 4:
        return parts[0] + parts[2]
    return 0.0

def text_width(el):
    """Desired width of a TextBlock's literal text."""
    txt = el.get("Text") or ""
    if txt.strip().startswith("{"):
        # binding -- assume a modest 18 chars
        txt = "x" * 18
    if not txt:
        # may have <Run> children
        runs = []
        for r in el.iter():
            if local(r.tag) == "Run":
                t = r.get("Text") or ""
                runs.append("x" * 18 if t.strip().startswith("{") else t)
        txt = "".join(runs)
    fs = num(el.get("FontSize"), 12.0)
    bold = (el.get("FontWeight") or "") in ("Bold", "SemiBold")
    per = 0.56 if bold else 0.52
    return len(txt) * fs * per

def desired_width(el, depth=0):
    """Conservative desired width of an element subtree."""
    if depth > 14:
        return 0.0
    w = el.get("Width")
    if w and num(w) > 0:
        return num(w) + margin_h(el)

    lt = local(el.tag)
    own = margin_h(el) + padding_h(el)

    if lt == "TextBlock":
        return text_width(el) + own
    if lt in ("Ellipse", "Rectangle"):
        return num(el.get("Width"), 12.0) + own
    if lt == "Button":
        # content text + padding; MinWidth respected
        inner = 0.0
        c = el.get("Content")
        if c and not str(c).strip().startswith("{"):
            inner = len(str(c)) * 10 * 0.56
        inner = max(inner, num(el.get("MinWidth"), 0.0))
        return inner + own + 24
    if lt == "Slider":
        return num(el.get("Width"), 100.0) + own
    if lt == "CheckBox":
        return 18 + own

    kids = [c for c in el if '.' not in local(c.tag)]
    if not kids:
        return own

    if lt == "StackPanel" and (el.get("Orientation") or "") == "Horizontal":
        return own + sum(desired_width(k, depth + 1) for k in kids)
    if lt == "DockPanel":
        return own + sum(desired_width(k, depth + 1) for k in kids)

    # vertical stack / border / grid -> widest child
    return own + max(desired_width(k, depth + 1) for k in kids)

def scan(path, avail):
    problems = []
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        return [f"XML parse error: {e}"]

    for el in tree.getroot().iter():
        lt = local(el.tag)
        if lt == "StackPanel" and (el.get("Orientation") or "") == "Horizontal":
            d = desired_width(el)
            if d > avail:
                kids = [local(k.tag) for k in el if '.' not in local(k.tag)]
                problems.append(
                    f"CLIPS: horizontal StackPanel needs ~{d:.0f}px but only "
                    f"~{avail:.0f}px available -> overflows by {d-avail:.0f}px "
                    f"({', '.join(kids[:5])})")
        if lt == "DockPanel":
            kids = [c for c in el if '.' not in local(c.tag)]
            if len(kids) >= 2:
                docked = sum(desired_width(k) for k in kids[:-1])
                fill_needs = desired_width(kids[-1])
                if docked + fill_needs > avail:
                    problems.append(
                        f"DOCK SQUEEZE: docked children need ~{docked:.0f}px and "
                        f"the fill child needs ~{fill_needs:.0f}px = "
                        f"~{docked+fill_needs:.0f}px, but only ~{avail:.0f}px "
                        f"available -> <{local(kids[-1].tag)}> is clipped or vanishes")
    return problems

def main(argv):
    avail = float(argv[0])
    paths = argv[1:]
    total = 0
    for p in paths:
        pr = scan(p, avail)
        if pr:
            total += len(pr)
            print(f"\n{p}")
            for x in pr:
                print(f"   {x}")
    print(f"\n{len(paths)} file(s) at {avail:.0f}px content width, {total} problem(s)")
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

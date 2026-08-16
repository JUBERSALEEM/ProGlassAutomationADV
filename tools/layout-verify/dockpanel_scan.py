#!/usr/bin/env python3
"""
Detect DockPanel ordering faults that cause OVERLAP in WPF.

DockPanel semantics:
  - Children are docked IN DECLARATION ORDER.
  - With LastChildFill=True (the DEFAULT), the LAST child ignores its
    Dock and fills all remaining space.

FAULT A (overlap / squeeze):
  A child declared BEFORE the last one has no Dock attribute. It then
  defaults to Dock=Left and takes its full desired width, while the last
  child fills the rest. If the earlier child is a wide text block, it
  eats the row and the docked-Right control gets pushed or clipped.

FAULT B (the real overlap):
  The LAST child carries DockPanel.Dock="Right" (or Top/Bottom/Left).
  Because LastChildFill=True overrides it, that Dock is IGNORED and the
  element is stretched across the WHOLE remaining area -- it renders
  UNDERNEATH/ACROSS its siblings instead of hugging the right edge.
  This is a silent, very common WPF mistake.
"""
import sys, os
import xml.etree.ElementTree as ET

X = "{http://schemas.microsoft.com/winfx/2006/xaml}"

def local(t):
    return t.rsplit('}', 1)[-1] if '}' in t else t

def scan(path):
    problems = []
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        return [f"XML parse error: {e}"]

    for dp in tree.getroot().iter():
        if local(dp.tag) != "DockPanel":
            continue

        lcf = (dp.get("LastChildFill") or "True").strip().lower() != "false"

        kids = [c for c in dp if '.' not in local(c.tag)]
        if len(kids) < 2:
            continue

        last = kids[-1]
        last_dock = last.get("DockPanel.Dock")

        if lcf and last_dock:
            problems.append(
                f"DOCK IGNORED: <{local(last.tag)}> is the LAST child of a "
                f"DockPanel with LastChildFill=True, so its "
                f"Dock=\"{last_dock}\" is IGNORED -- it stretches across the "
                f"whole remaining area instead of hugging {last_dock.lower()}. "
                f"Either move it before the fill child or set "
                f"LastChildFill=\"False\".")

        # earlier children with no Dock silently default to Left
        for k in kids[:-1]:
            if k.get("DockPanel.Dock") is None:
                problems.append(
                    f"IMPLICIT DOCK: <{local(k.tag)}> has no DockPanel.Dock so "
                    f"it defaults to Left and claims its full desired width, "
                    f"squeezing the fill child.")
    return problems

def main(paths):
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

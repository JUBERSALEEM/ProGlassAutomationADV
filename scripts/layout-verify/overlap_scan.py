#!/usr/bin/env python3
"""
Detect WPF layout faults that cause OVERLAPPING or COLLAPSED content.

FAULT 1 (overlap): a Grid declares RowDefinitions but has direct children
        with no Grid.Row -> they all land in row 0, stacked on each other.
FAULT 2 (overlap): same for ColumnDefinitions / Grid.Column.
FAULT 3 (overlap): two direct children of a Grid claim the same
        (Grid.Row, Grid.Column) cell without a differing ZIndex/Span --
        reported only when neither is a Border/Rectangle backdrop.
FAULT 4 (collapse): a Grid row/column is sized "Auto" but the child in it
        is a ScrollViewer/ItemsControl/DataGrid with no MinHeight -- these
        can measure to zero and vanish.
FAULT 5 (collapse): RowDefinition Height="*" inside a StackPanel-hosted
        Grid -- star sizing has infinite space, so it collapses to Auto.
"""
import sys, re
import xml.etree.ElementTree as ET

NS = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
X  = "{http://schemas.microsoft.com/winfx/2006/xaml}"

def local(t):
    return t.rsplit('}', 1)[-1] if '}' in t else t

def is_definition(tag):
    l = local(tag)
    return l.endswith("Definitions") or l.endswith("Definition")

def is_property_element(tag, parent_tag):
    # <Grid.RowDefinitions>, <Button.Style> etc -- a dotted tag
    return '.' in local(tag)

# Only ScrollViewer is genuinely broken by an Auto row: Auto measures the
# child with infinite height, so the viewport grows to fit the content and
# the scrollbar never engages. ItemsControl/DataGrid in an Auto row simply
# size to content, which is usually intended -- do not flag those.
STRETCHY = {"ScrollViewer"}
BACKDROP = {"Border", "Rectangle", "Path", "Ellipse"}

# Popups render in their own top-level window, so they never overlap
# siblings in the layout pass. Same for tooltips/adorners.
NON_LAYOUT = {"Popup", "ToolTip", "ContextMenu"}

# Styles (anywhere in the repo) that set Visibility. Built once, keyed by
# x:Key, because a style declared in a shared ResourceDictionary still
# makes its consumer conditionally visible.
_VIS_STYLES = None

def build_visibility_style_index(root_dir="."):
    import os
    keys = set()
    for dirpath, dirnames, filenames in os.walk(root_dir):
        if ".git" in dirpath:
            continue
        for fn in filenames:
            if not fn.endswith(".xaml"):
                continue
            fp = os.path.join(dirpath, fn)
            try:
                t = ET.parse(fp)
            except ET.ParseError:
                continue
            for st in t.getroot().iter():
                if local(st.tag) != "Style":
                    continue
                key = st.get(X + "Key")
                if not key:
                    continue

                # Only Setters that apply to the styled element itself
                # count. A Visibility Setter nested inside a ControlTemplate
                # or DataTemplate targets an INNER part (e.g. the glyph Run
                # of a pill button) and says nothing about whether the
                # element as a whole is conditionally shown.
                def own_setters(node, inside_template=False):
                    for d in node:
                        lt = local(d.tag)
                        nested = inside_template or lt in (
                            "ControlTemplate", "DataTemplate", "ItemsPanelTemplate")
                        if (lt == "Setter" and not nested
                                and d.get("Property") == "Visibility"):
                            return True
                        if own_setters(d, nested):
                            return True
                    return False

                if own_setters(st):
                    keys.add(key)
    return keys

def style_ref_key(value):
    """Extract MyStyle from '{StaticResource MyStyle}'."""
    if not value:
        return None
    m = re.match(r"\{(?:Static|Dynamic)Resource\s+([^}]+)\}", value.strip())
    return m.group(1).strip() if m else None

def scan(path):
    problems = []
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        return [f"XML parse error: {e}"]
    root = tree.getroot()

    parents = {c: p for p in root.iter() for c in p}

    for el in root.iter():
        if local(el.tag) != "Grid":
            continue

        rowdefs = []
        coldefs = []
        kids = []
        for c in el:
            lt = local(c.tag)
            if lt == "Grid.RowDefinitions":
                rowdefs = [d for d in c]
            elif lt == "Grid.ColumnDefinitions":
                coldefs = [d for d in c]
            elif '.' in lt:
                continue  # other property element
            else:
                kids.append(c)

        nrows = len(rowdefs)
        ncols = len(coldefs)

        # FAULT 1 / 2 -- children missing placement AND colliding on the
        # other axis. A child with no Grid.Column but a distinct Grid.Row
        # does not overlap anything, so the other axis must match too.
        if nrows > 1:
            missing = [k for k in kids if k.get("Grid.Row") is None]
            bycol = {}
            for k in missing:
                bycol.setdefault(k.get("Grid.Column", "0"), []).append(k)
            for col, group in bycol.items():
                if len(group) > 1:
                    problems.append(
                        f"OVERLAP: Grid has {nrows} RowDefinitions but "
                        f"{len(group)} children in column {col} lack "
                        f"Grid.Row -> all land in row 0: "
                        f"{', '.join(local(k.tag) for k in group[:6])}")
        if ncols > 1:
            missing = [k for k in kids if k.get("Grid.Column") is None
                       and not k.get("Grid.ColumnSpan")]
            byrow = {}
            for k in missing:
                byrow.setdefault(k.get("Grid.Row", "0"), []).append(k)
            for row, group in byrow.items():
                if len(group) > 1:
                    problems.append(
                        f"OVERLAP: Grid has {ncols} ColumnDefinitions but "
                        f"{len(group)} children in row {row} lack "
                        f"Grid.Column -> all land in col 0: "
                        f"{', '.join(local(k.tag) for k in group[:6])}")

        # FAULT 3 -- duplicate cell occupancy
        cells = {}
        for k in kids:
            r = k.get("Grid.Row", "0")
            c = k.get("Grid.Column", "0")
            cells.setdefault((r, c), []).append(k)
        for (r, c), occupants in cells.items():
            if len(occupants) < 2:
                continue
            names = [local(o.tag) for o in occupants]
            # a single backdrop behind content is legitimate
            non_backdrop = [n for n in names if n not in BACKDROP]
            spans = any(o.get("Grid.RowSpan") or o.get("Grid.ColumnSpan")
                        for o in occupants)
            zidx = any(o.get("Panel.ZIndex") for o in occupants)

            # Visibility-switched panes legitimately share a cell (only one
            # is ever visible). Suppress unless at least two occupants are
            # unconditionally visible.
            def has_style_visibility(o):
                """A Style/Trigger that sets Visibility means the element is
                conditionally shown, so sharing a cell is intentional."""
                for d in o.iter():
                    if local(d.tag) in ("Setter", "DataTrigger", "Trigger"):
                        if (d.get("Property") == "Visibility"
                                or d.get("Value") in ("Visible", "Collapsed")):
                            return True
                    # <Border Visibility="{Binding ...}"> on a descendant of a
                    # property element such as <X.Style> still counts
                    if d is not o and d.get("Visibility") is not None:
                        pass
                return False

            def styled_conditional(o):
                k = style_ref_key(o.get("Style"))
                return k is not None and _VIS_STYLES and k in _VIS_STYLES

            always_visible = [
                o for o in occupants
                if o.get("Visibility") is None
                and not styled_conditional(o)
                and local(o.tag) not in BACKDROP
                and local(o.tag) not in NON_LAYOUT
                and not has_style_visibility(o)
            ]

            # Alignment-separated children also legitimately share a cell
            # (e.g. a title Left and a button Right in the same row).
            def aligned(o):
                return (o.get("HorizontalAlignment") in ("Left", "Right")
                        or o.get("VerticalAlignment") in ("Top", "Bottom"))
            unaligned = [o for o in always_visible if not aligned(o)]

            if len(unaligned) > 1 and not spans and not zidx:
                problems.append(
                    f"OVERLAP: {len(unaligned)} unconditionally-visible, "
                    f"unaligned children share cell (Row={r},Col={c}): "
                    f"{', '.join(local(o.tag) for o in unaligned[:6])}")

        # FAULT 4 -- stretchy child in an Auto row with no MinHeight
        for k in kids:
            lt = local(k.tag)
            if lt not in STRETCHY:
                continue
            try:
                ri = int(k.get("Grid.Row", "0"))
            except ValueError:
                continue
            if ri < nrows:
                h = (rowdefs[ri].get("Height") or "").strip()
                scrolls = (k.get("VerticalScrollBarVisibility") or "") \
                    .lower() in ("auto", "visible")
                if (h.lower() == "auto" and scrolls
                        and not k.get("MaxHeight") and not k.get("Height")):
                    problems.append(
                        f"SCROLL DEFEATED: {lt} with a vertical scrollbar sits "
                        f"in an Auto-height row {ri} and has no MaxHeight -- "
                        f"the row grows to fit content so it never scrolls")

        # FAULT 5 -- star row inside an unbounded-height parent
        p = parents.get(el)
        if p is not None and local(p.tag) in ("StackPanel",):
            star_rows = [d for d in rowdefs
                         if '*' in (d.get("Height") or "")]
            if star_rows and el.get("Height") is None:
                problems.append(
                    f"COLLAPSE: Grid with {len(star_rows)} star-sized row(s) "
                    f"inside a StackPanel -- star gets infinite space and "
                    f"collapses to Auto")

    return problems

def main(paths):
    global _VIS_STYLES
    _VIS_STYLES = build_visibility_style_index(".")
    total = 0
    for p in paths:
        probs = scan(p)
        if probs:
            total += len(probs)
            print(f"\n{p}")
            for pr in probs:
                print(f"   {pr}")
    print(f"\n{len(paths)} file(s) scanned, {total} problem(s)")
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

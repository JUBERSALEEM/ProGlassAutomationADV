#!/usr/bin/env python3
"""Settings rewrite — remove tile-in-tile boxing.

WHY THIS EXISTS
───────────────
The first mechanical pass over these pages wrapped each row's icon + title in a
container and gave that container the inner-tile style:

    <Border Style="{StaticResource SettingsInnerTileStyle}">     ← the row
        <StackPanel>
            <Border Style="{StaticResource SettingsInnerTileStyle}">  ← the box
                <StackPanel Orientation="Horizontal">
                    <TextBlock Style="{StaticResource Typography.Icon}" …/>
                    <TextBlock Style="{StaticResource SettingsRowTitleStyle}" …/>
                </StackPanel>
            </Border>
            …

A tile inside a tile paints the same fill, the same hairline and the same
radius twice, so the inner box reads as a stray bordered panel around the row's
title, and the approved sheets show no such thing: a row is a disc, a title, a
helper line and a trailing control, sitting directly in its card (sheet 06 rows,
sheet 08 backup rows, sheet 10 module rows).

WHY NOT BY HAND
───────────────
The pattern is uniform but its indentation is not — these files have been
through several scripted restyles, so the closing tag is not reliably at the
opening tag's indent, and a mistaken match deletes a container that something
else depends on.

So the file is parsed into an element table (tag, first line, last line, parent)
and only the element whose parent chain is

    Border[InnerTile] → StackPanel → Border[InnerTile]

is removed — its opening lines and its own closing line, with every child kept
exactly as it was. Nothing else about the file changes.

USAGE
    python3 scripts/settings-rewrite/flatten_tile_in_tile.py            # report
    python3 scripts/settings-rewrite/flatten_tile_in_tile.py --apply
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SECTIONS = REPO / "Views" / "Settings" / "Sections"

TAG = re.compile(r'<(/?)([A-Za-z][\w.:]*)((?:"[^"]*"|[^>"])*?)(/?)>')
COMMENT = re.compile(r"<!--.*?-->", re.S)
TILE = "SettingsInnerTileStyle"


def blank_comments(text: str) -> str:
    return COMMENT.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)


def elements(text: str) -> list[dict]:
    out: list[dict] = []
    stack: list[dict] = []
    for m in TAG.finditer(blank_comments(text)):
        closing, tag, attrs, selfclose = m.groups()
        line = text.count("\n", 0, m.start()) + 1
        if closing:
            stack.pop()["last"] = line
            continue
        el = {"tag": tag, "attrs": attrs, "first": line, "last": line,
              "parent": stack[-1]["id"] if stack else None, "id": len(out)}
        out.append(el)
        if not selfclose:
            stack.append(el)
    return out


def boxes(path: Path) -> list[dict]:
    """The pure header boxes: a bare inner tile holding a row's icon + title.

    Three conditions, all of them necessary — the first draft of this tool only
    checked nesting and it deleted real panels:

      * parent chain  Border[inner tile] → StackPanel → Border[inner tile];
      * the box carries NO attribute but its Style, because the box was emitted
        mechanically and a hand-written panel that happens to sit in a tile
        states its own Background / BorderBrush / Height (Data's amber
        "network root" status, Themes' crystal preview) — those are content;
      * the box holds nothing but TextBlocks, so a box that wraps a control
        (a slider, a list, a crystal preview) is never touched.
    """
    text = path.read_text(encoding="utf-8")
    els = elements(text)
    by_id = {e["id"]: e for e in els}
    found = []
    for el in els:
        if el["tag"] != "Border" or TILE not in el["attrs"]:
            continue
        if el["attrs"].count("=") != 1:          # Style=… and nothing else
            continue
        parent = by_id.get(el["parent"])
        grand = by_id.get(parent["parent"]) if parent else None
        if not parent or parent["tag"] != "StackPanel" or not grand:
            continue
        if not (grand["tag"] == "Border" and TILE in grand["attrs"]):
            continue
        # …and everything inside it is text: a box wrapping a control is a
        # panel (the box around the crystal preview holds a
        # CrystalWaterControl), not a row header.
        stack = [e["id"] for e in els if e["parent"] == el["id"]]
        descendants: list[dict] = []
        while stack:
            cur = stack.pop()
            for child in (e for e in els if e["parent"] == cur):
                descendants.append(child)
                stack.append(child["id"])
        if any(d["tag"] not in ("TextBlock", "StackPanel", "Run", "Border")
               for d in descendants):
            continue
        found.append(el)
    return found


def apply(path: Path) -> int:
    text = path.read_text(encoding="utf-8")
    found = boxes(path)
    if not found:
        return 0
    lines = text.splitlines()
    drop = set()
    for el in found:
        # the element's own lines: everything from its first line up to the
        # line before its first CHILD (the rest is content), plus its closing
        # tag line. Self-closing boxes have no content and are dropped whole.
        kids = min((c["first"] for c in elements(text) if c["parent"] == el["id"]),
                   default=None)
        if kids is None:
            drop.update(range(el["first"] - 1, el["last"]))
        else:
            drop.update(range(el["first"] - 1, kids - 1))
            drop.add(el["last"] - 1)
    kept = [l for i, l in enumerate(lines) if i not in drop]
    path.write_text("\n".join(kept) + "\n", encoding="utf-8")
    return len(found)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("files", nargs="*")
    args = ap.parse_args()

    files = ([Path(f) for f in args.files] if args.files
             else sorted(SECTIONS.glob("*.xaml")))
    total = 0
    for path in files:
        if not path.is_absolute():
            path = REPO / path
        n = apply(path) if args.apply else len(boxes(path))
        total += n
        print(f"{'removed' if args.apply else 'found'} {n:2d} tile-in-tile box(es) in {path.name}")
    print(f"\n{total} box(es)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

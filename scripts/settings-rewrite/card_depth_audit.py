#!/usr/bin/env python3
"""Settings rewrite — card hierarchy audit.

THE RULE (from the approved mockups)
────────────────────────────────────
    Page body ─┬─ PAGE CARD   (SettingsPageCardStyle / SettingsDangerCardStyle)
               │     └─ inner tile (SettingsInnerTileStyle and friends)
               └─ PAGE CARD
                     └─ inner tile

A page card is the white sheet with the card radius and the module shadow; an
inner tile is the quieter container that lives INSIDE one (smaller radius,
flatter fill, no shadow). The mockups never nest two page cards — sheet 06's
"Capabilities" card holds plain rows, sheet 08's "Backups" card holds tile
rows, sheet 10's "Module flags" card holds an inner tile of cap groups. When a
page card is nested inside another page card the material reads as card-in-card
and the hierarchy is lost.

WHY A TOOL AND NOT AN EYEBALL
─────────────────────────────
Nesting cannot be judged from indentation: this repo's section files went
through several scripted restyles, so some deeply nested elements sit at a
shallow indent (and vice versa). This walks a real tag stack, so a card's depth
is its actual ancestor count.

USAGE
    python3 scripts/settings-rewrite/card_depth_audit.py            # report
    python3 scripts/settings-rewrite/card_depth_audit.py --fail-on-nested
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SECTIONS = REPO / "Views" / "Settings" / "Sections"
SHELL = [REPO / "Views" / "Settings" / "SettingsView.xaml"]

# One tag, possibly spanning lines, quoted attribute values respected.
TAG = re.compile(r'<(/?)([A-Za-z][\w.:]*)((?:"[^"]*"|[^>"])*?)(/?)>')
CARD_STYLES = ("SettingsPageCardStyle", "SettingsDangerCardStyle")

# Comments in these files quote markup ("<!-- <Border …> -->") and even carry
# commented-out XAML. Scanning them would push phantom elements onto the stack,
# so they are blanked out first — space for space, to keep the line numbers.
COMMENT = re.compile(r"<!--.*?-->", re.S)


def blank_comments(text: str) -> str:
    return COMMENT.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)


def audit(path: Path) -> list[dict]:
    """Return the page cards nested inside another page card.

    Offsets point into the ORIGINAL text: comments are blanked space-for-space,
    so a match found in the blanked copy is at the same offset in the file.
    """
    raw = path.read_text(encoding="utf-8")
    text = blank_comments(raw)
    stack: list[bool] = []                    # is_page_card, one per open tag
    nested: list[dict] = []
    for m in TAG.finditer(text):
        closing, tag, attrs, selfclose = m.groups()
        line = text.count("\n", 0, m.start()) + 1
        if closing:
            if stack:
                stack.pop()
            else:
                # A close with no open means the scan desynchronised; better to
                # say so than to report a plausible-looking wrong depth.
                raise SystemExit(f"{path.name}: unmatched </{tag}> at line {line}")
            continue
        is_card = tag == "Border" and any(s in attrs for s in CARD_STYLES)
        depth = sum(1 for c in stack if c)
        if is_card and depth >= 1 and "SettingsPageCardStyle" in attrs:
            start = m.start(3) + attrs.index("SettingsPageCardStyle")
            nested.append({"line": line, "depth": depth + 1,
                           "start": start, "end": start + len("SettingsPageCardStyle"),
                           "text": attrs.strip()[:70].replace("\n", " ")})
        if not selfclose:
            stack.append(is_card)
    return nested


def demote(path: Path) -> int:
    """Rewrite every nested page card as an inner tile, in place."""
    nested = audit(path)
    if not nested:
        return 0
    raw = path.read_text(encoding="utf-8")
    for item in sorted(nested, key=lambda i: -i["start"]):
        raw = raw[:item["start"]] + "SettingsInnerTileStyle" + raw[item["end"]:]
    path.write_text(raw, encoding="utf-8")
    return len(nested)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--fail-on-nested", action="store_true")
    ap.add_argument("--demote", action="store_true",
                    help="nested page cards become inner tiles (in place)")
    args = ap.parse_args()

    files = sorted(SECTIONS.glob("*.xaml")) + [p for p in SHELL if p.exists()]
    total = 0
    for path in files:
        if args.demote:
            n = demote(path)
            total += n
            print(f"{path.name}: {'demoted ' + str(n) + ' nested page card(s)' if n else 'clean'}")
            continue
        nested = audit(path)
        total += len(nested)
        if nested:
            print(f"{path.name}: {len(nested)} nested page card(s)")
            for item in nested[:40]:
                print(f"    L{item['line']:<5} depth={item['depth']} {item['text']}")
        else:
            print(f"{path.name}: clean")
    print(f"\n{total} nested page card(s)")
    return 1 if (args.fail_on_nested and total) else 0


if __name__ == "__main__":
    sys.exit(main())

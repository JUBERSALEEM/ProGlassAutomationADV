#!/usr/bin/env python3
"""Settings rewrite — turn band captions into grammar cards.

WHY THIS EXISTS
───────────────
Every Settings page grew the same way: a stack of "band captions" —

    <!-- ═══════ GROUP: BACKUP & RESTORE ═══════ -->
    <TextBlock Text="{DynamicResource Settings.BackupRestore}"
               Style="{StaticResource SettingsGroupCaptionStyle}"/>
    <TextBlock Text="{DynamicResource Settings.BackupSetCopiesDatabaseSettingsOne}"
               Style="{StaticResource SettingsRowMetaStyle}"/>

— a heading and a recap line floating above a loose pile of control rows. The
approved mockups read the other way round: each group is a WHITE CARD, and the
heading is the card's own title with the recap line under it as its helper
line. Same words, same order, completely different structure — which is exactly
the kind of change that is easy to get subtly wrong by hand across twelve
files.

This tool performs that one transformation mechanically:

    band caption + recap  ->  <Border Style="SettingsPageCardStyle">
                                 title (SettingsCardTitleStyle)
                                 helper (SettingsCardNoteStyle)
                                 … the group body, unchanged …

and closes the card immediately before the next group starts (or before the
page's closing StackPanel for the last group), re-indenting the body so the
file still reads at its original depth.

WHAT IT DELIBERATELY DOES NOT DO
  * No text changes: the title keeps its DynamicResource key, the helper keeps
    its own, and any group without a recap line simply gets a title-only card.
  * No element removal: everything between the caption and the next caption is
    copied line-for-line, only its indentation changes.
  * No disc/icon in the header — the mockups put icons on ROWS, never on card
    headings (see docs/SETTINGS_REDESIGN_ANALYSIS_AND_MOCKUPS_2026-09-16.md).

Run it, then `verify_bindings.py` to prove nothing was dropped and
`scripts/xaml-guard.py` for well-formedness.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

INDENT = " " * 16
BODY_CLOSE = " " * 12 + "</StackPanel>"
TITLE_STYLE = 'Style="{StaticResource SettingsCardTitleStyle}"/>'
NOTE_STYLE = 'Style="{StaticResource SettingsCardNoteStyle}"/>'


def band_sites(lines: list[str]) -> list[dict]:
    """Locate every band caption, its element span and any recap line after it."""
    sites = []
    for i, line in enumerate(lines):
        if "SettingsGroupCaptionStyle" not in line:
            continue
        # The caption element opens on the Text line above the style line and
        # ends on the line that closes it (`/>`), which is the style line for
        # the common two-line shape and one line lower when Margin follows.
        start = i - 1 if lines[i - 1].strip().startswith("<TextBlock") else i
        end = i
        while end < len(lines) and not lines[end].rstrip().endswith("/>"):
            end += 1
        meta_text, meta_style, consumed = None, None, 0
        if end + 2 < len(lines) and lines[end + 1].strip().startswith("<TextBlock") \
                and "SettingsRowMetaStyle" in lines[end + 2]:
            meta_text, meta_style, consumed = lines[end + 1].strip(), lines[end + 2].strip(), 3
        comment = lines[start - 1] if start >= 1 and "<!--" in lines[start - 1] else None
        sites.append({
            "start": start, "caption": i, "end": end,
            "title": lines[start].strip(),
            "meta_text": meta_text, "meta_style": meta_style, "consumed": consumed,
            "comment": comment,
        })
    return sites


def card_name(site: dict, fallback: str) -> str:
    """A short banner for the card comment, taken from the band comment."""
    if site["comment"]:
        m = re.search(r"GROUP:\s*([^═]+?)\s*═", site["comment"])
        if m:
            return m.group(1).strip()
        m = re.search(r"<!--\s*(.+?)\s*-->", site["comment"])
        if m:
            return m.group(1).strip()
    return fallback


def convert(path: Path, dry_run: bool = False) -> tuple[int, list[str]]:
    lines = path.read_text(encoding="utf-8").splitlines()
    sites = band_sites(lines)
    if not sites:
        return 0, []

    # The body ends at the closing StackPanel that precedes </ScrollViewer>.
    closes = [i for i, l in enumerate(lines) if l == BODY_CLOSE]
    if not closes:
        raise SystemExit(f"{path.name}: no page body close found")
    body_close = closes[-1]

    starts = [s["start"] for s in sites]                 # <TextBlock Text=…>
    body_from = [s["end"] + 1 + s["consumed"] for s in sites]
    ends = starts[1:] + [body_close]

    out: list[str] = []
    cursor = 0
    names: list[str] = []
    for n, site in enumerate(sites):
        start, end = starts[n], ends[n]
        # anything before this group stays put (page-level intro, cards, …)
        out += lines[cursor:start]
        # drop the band comment — the card banner replaces it
        while out and (out[-1].strip().startswith("<!--") and out[-1].strip().endswith("-->")
                       and "═" in out[-1]):
            out.pop()
        name = card_name(site, f"GROUP {n + 1}")
        names.append(name)
        out.append(f"{INDENT}<!-- ═══ CARD — {name} ═══ -->")
        out.append(f'{INDENT}<Border Style="{{StaticResource SettingsPageCardStyle}}">')
        out.append(f"{INDENT}    <StackPanel>")
        title_role = "SettingsCardTitleStyle" if site["meta_text"] else "SettingsCardHeaderStyle"
        out.append(f"{INDENT}        {site['title']}")
        out.append(f'{INDENT}                   Style="{{StaticResource {title_role}}}"/>')
        if site["meta_text"]:
            out.append(f"{INDENT}        {site['meta_text']}")
            out.append(f'{INDENT}                   Style="{{StaticResource SettingsCardHeaderNoteStyle}}"/>')
        # the group body, re-indented one level, with the band comment gone
        body = lines[body_from[n]:end]
        while body and not body[0].strip():
            body.pop(0)
        while body and not body[-1].strip():
            body.pop()
        for bl in body:
            out.append("        " + bl if bl.strip() else bl)
        out.append(f"{INDENT}    </StackPanel>")
        out.append(f"{INDENT}</Border>")
        out.append("")
        cursor = end

    # everything after the last group (the page body's own close and the
    # ScrollViewer/Grid/UserControl tail) keeps its original depth
    tail = lines[body_close:]
    for n, tl in enumerate(tail):
        keep = n == 0                       # the body's own close keeps its depth
        out.append(tl if keep or not tl.startswith(" " * 8) else tl[8:])

    if not dry_run:
        path.write_text("\n".join(out) + "\n", encoding="utf-8")
    return len(sites), names


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    for raw in args.files:
        path = Path(raw)
        if not path.is_absolute():
            path = Path(__file__).resolve().parents[2] / path
        count, names = convert(path, args.dry_run)
        label = "would card" if args.dry_run else "carded"
        print(f"{label} {count:2d} group(s) in {path.name}: " + ", ".join(names))
    return 0


if __name__ == "__main__":
    sys.exit(main())

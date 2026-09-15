#!/usr/bin/env python3
"""Settings rewrite — turn band captions into grammar cards.

WHY THIS EXISTS
───────────────
Every Settings page grew the same way: a stack of "band captions" —

    <!-- ═══════ GROUP: BACKUP & RESTORE ═══════ -->
    <TextBlock Text="{DynamicResource Settings.BackupRestore}"
               Style="{StaticResource SettingsGroupCaptionStyle}"/>
    <TextBlock Text="{DynamicResource Settings.BackupSetData…}"
               Style="{StaticResource SettingsRowMetaStyle}"/>

— a heading and a recap line floating above a loose pile of control rows. The
approved mockups read the other way round: each group is a WHITE CARD and the
heading is the card's own title, with the recap line under it as its helper
line:

    <Border Style="{StaticResource SettingsPageCardStyle}">
        <StackPanel>
            <TextBlock Text="{DynamicResource Settings.BackupRestore}"
                       Style="{StaticResource SettingsCardTitleStyle}"/>
            <TextBlock Text="{DynamicResource Settings.BackupSetData…}"
                       Style="{StaticResource SettingsCardHeaderNoteStyle}"/>
            … the group body, unchanged …

Same words, same order, different structure — and the kind of change that is
easy to get subtly wrong by hand across twelve files.

HOW IT DECIDES WHERE A CARD STARTS AND ENDS
───────────────────────────────────────────
Not by indentation, and not by "everything up to the next caption". Both were
tried and both broke on real pages:

  * Indentation lies. These files have been through several scripted restyles,
    so a deeply nested element can sit at a shallow indent.
  * "Up to the next caption" swallows the next group's wrapper. Themes hides
    its whole crystal group behind a StackPanel whose Visibility is bound, and
    the crystal band lives INSIDE that wrapper — so the previous card's close
    landed inside an open element and the file stopped being well-formed.

So the file is parsed into elements first (tag, first line, last line, parent,
indent) and every decision is structural:

  * the BODY is the outermost StackPanel that contains every band caption;
  * a group's frame is the body's own child that contains its caption — the
    caption itself for a plain group, or the wrapper for a wrapped one;
  * a plain group's card spans from its caption to the start of the next
    group's frame; a wrapped group's card is emitted INSIDE its wrapper, so the
    wrapper's Visibility still governs the whole card and its close stays
    outside it.

WHAT IT DELIBERATELY DOES NOT DO
  * No text changes: the title keeps its DynamicResource key, the helper keeps
    its own, and a group without a recap line gets a title-only card.
  * No element removal: everything inside a group is copied line-for-line;
    only its indentation changes.
  * No disc/icon in the header — the mockups put icons on ROWS, never on card
    headings (see docs/SETTINGS_REDESIGN_ANALYSIS_AND_MOCKUPS_2026-09-16.md).

USAGE
    python3 scripts/settings-rewrite/groups_to_cards.py FILE [FILE…]
        [--dry-run] [--only KEY,KEY] [--stop-before REGEX]

    --only        card just these groups (Features nests a second level of
                  bands that must stay bands).
    --stop-before for the last group: cut it where this regex first matches —
                  a page can end on a peer card that is nobody's child.

Then run verify_bindings.py (nothing dropped) and scripts/xaml-guard.py
(well-formed) before committing.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]

TAG = re.compile(r'<(/?)([A-Za-z][\w.:]*)((?:"[^"]*"|[^>"])*?)(/?)>')
COMMENT = re.compile(r"<!--.*?-->", re.S)
BODY_CLOSE = " " * 12 + "</StackPanel>"


def blank_comments(text: str) -> str:
    """Comments quote markup; blank them space-for-space so offsets survive."""
    return COMMENT.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)


def elements(text: str) -> list[dict]:
    """A flat element table: tag, first/last line, parent id, indent."""
    raw_lines = text.splitlines()
    out: list[dict] = []
    stack: list[dict] = []
    for m in TAG.finditer(blank_comments(text)):
        closing, tag, _attrs, selfclose = m.groups()
        line = text.count("\n", 0, m.start()) + 1
        if closing:
            if not stack:
                raise SystemExit(f"unmatched </{tag}> at line {line}")
            stack.pop()["last"] = line
            continue
        text_line = raw_lines[line - 1] if line - 1 < len(raw_lines) else ""
        el = {"tag": tag, "first": line, "last": line,
              "parent": stack[-1]["id"] if stack else None,
              "id": len(out),
              "indent": len(text_line) - len(text_line.lstrip())}
        out.append(el)
        if selfclose:
            continue
        stack.append(el)
    if stack:
        raise SystemExit(f"unclosed <{stack[-1]['tag']}> at line {stack[-1]['first']}")
    return out


def band_sites(lines: list[str]) -> list[dict]:
    """Every band caption: its element span, its recap line, its indent."""
    sites = []
    for i, line in enumerate(lines):
        if "SettingsGroupCaptionStyle" not in line:
            continue
        start = i - 1 if lines[i - 1].strip().startswith("<TextBlock") else i
        end = i
        while end < len(lines) and not lines[end].rstrip().endswith("/>"):
            end += 1
        meta_text, consumed = None, 0
        if end + 2 < len(lines) and lines[end + 1].strip().startswith("<TextBlock") \
                and "SettingsRowMetaStyle" in lines[end + 2]:
            meta_text, consumed = lines[end + 1].strip(), 3
        sites.append({
            "start": start, "end": end, "consumed": consumed,
            "title": lines[start].strip(), "meta_text": meta_text,
            "indent": len(lines[start]) - len(lines[start].lstrip()),
            "line": start + 1,
        })
    return sites


def banner(lines: list[str], site: dict, fallback: str) -> str:
    """The card's comment banner: the band comment, else the band's own key."""
    for k in range(site["start"] - 1, max(-1, site["start"] - 15), -1):
        if "GROUP:" in lines[k]:
            site["banner_line"] = k
            m = re.search(r"GROUP:\s*([^═]+?)\s*═", lines[k])
            if m:
                return m.group(1).strip()
    key = re.search(r'(?:DynamicResource\s+|Text=")([^}"]+)', site["title"])
    if key:
        words = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", key.group(1).split(".")[-1])
        return words.upper()
    return fallback


def convert(path: Path, dry_run: bool = False, only: list[str] | None = None,
            stop_before: str | None = None) -> tuple[int, list[str]]:
    lines = path.read_text(encoding="utf-8").splitlines()
    sites = band_sites(lines)
    if not sites:
        return 0, []
    if only:
        # A page may nest a second level of bands (Features' per-wizard-step
        # bands inside a module). Those stay bands on purpose — the mockups put
        # cap groups inside a card — so the caller names the card headings.
        keep = []
        for site in sites:
            key = re.search(r'(?:DynamicResource\s+|Text=")([^}"]+)', site["title"])
            name = (key.group(1) if key else "").strip()
            if any(o in name or o.lower() in site["title"].lower() for o in only):
                keep.append(site)
        sites = keep
        if not sites:
            return 0, []

    els = elements("\n".join(lines))
    body_close = [i for i, l in enumerate(lines) if l == BODY_CLOSE][-1]

    # The body: the outermost StackPanel holding every caption.
    caption_lines = [s["line"] for s in sites]
    body = None
    for el in els:
        if el["tag"] != "StackPanel":
            continue
        if all(el["first"] <= c <= el["last"] for c in caption_lines):
            body = el
            break
    if body is None:
        raise SystemExit(f"{path.name}: no body StackPanel contains the bands")

    # Frames: the body's own child that holds each caption (caption or wrapper).
    for site in sites:
        frame = None
        for el in els:
            if el["parent"] == body["id"] and el["first"] <= site["line"] <= el["last"]:
                frame = el
                break
        if frame is None:
            raise SystemExit(f"{path.name}: no frame for band at line {site['line']}")
        site["frame"] = frame
        site["wrapped"] = frame["first"] < site["start"] + 1
        if site["wrapped"]:
            # The card goes inside the frame: every wrapper between the frame
            # and the caption would be left behind otherwise.
            inner = [e for e in els
                     if e["first"] <= site["line"] <= e["last"]
                     and e["id"] != frame["id"]                 # the band itself
                     and e["first"] != site["start"] + 1        # …and its text
                     and e["first"] > frame["first"]]
            if inner:
                raise SystemExit(f"{path.name}: band at line {site['line']} sits "
                                 f"inside {len(inner)} nested element(s) — "
                                 f"case not handled")

    # Boundaries: a plain group runs to the next frame; a wrapped one stops at
    # its own frame's closing line.
    stops = []
    for n, site in enumerate(sites):
        last = n == len(sites) - 1
        if site["wrapped"]:
            stops.append(site["frame"]["last"] - 1)
        elif last:
            cut = body_close
            if stop_before:
                pat = re.compile(stop_before)
                for i in range(site["start"] + 1, body_close):
                    if pat.search(lines[i]):
                        cut = i
                        break
            stops.append(cut)
        else:
            stops.append(sites[n + 1]["frame"]["first"] - 1)

    out: list[str] = []
    cursor = 0
    names: list[str] = []
    n_cards = len(sites)
    for n, site in enumerate(sites):
        pad = " " * site["indent"]
        inner = " " * (site["indent"] + 8)
        attr = " " * (site["indent"] + 19)      # aligns under `Text=`
        name = banner(lines, site, f"GROUP {n + 1}")
        names.append(name)
        banner_at = site.get("banner_line")      # the comment the card re-emits

        def emit(upto: int) -> None:
            """Copy [cursor, upto) but never the banner the card re-emits."""
            for i in range(cursor, upto):
                if i != banner_at:
                    out.append(lines[i])

        if site["wrapped"]:
            # Everything up to the wrapper's opening tag first: the wrapper
            # owns the card, not the other way round, so its Visibility still
            # hides the whole group and its close stays outside the card.
            emit(site["start"])
            body_from = site["end"] + 1 + site["consumed"]
            body = lines[body_from:site["frame"]["last"] - 1]
            tail_after = lines[site["frame"]["last"] - 1:site["frame"]["last"]]
            cursor = site["frame"]["last"]
        else:
            emit(site["start"])
            body_from = site["end"] + 1 + site["consumed"]
            body = lines[body_from:stops[n]]
            tail_after = []
            cursor = stops[n]

        while body and not body[0].strip():
            body.pop(0)
        while body and not body[-1].strip():
            body.pop()
        # The next group's banner documents the NEXT card; leaving it here would
        # park it at the bottom of this card.
        while body and body[-1].strip().startswith("<!--") and "GROUP:" in body[-1]:
            body.pop()
            while body and not body[-1].strip():
                body.pop()

        out.append(f"{pad}<!-- ═══ CARD — {name} ═══ -->")
        out.append(f'{pad}<Border Style="{{StaticResource SettingsPageCardStyle}}">')
        out.append(f"{pad}    <StackPanel>")
        out.append(f"{inner}{site['title']}")
        role = "SettingsCardTitleStyle" if site["meta_text"] else "SettingsCardHeaderStyle"
        out.append(f'{attr}Style="{{StaticResource {role}}}"/>')
        if site["meta_text"]:
            out.append(f"{inner}{site['meta_text']}")
            out.append(f'{attr}Style="{{StaticResource SettingsCardHeaderNoteStyle}}"/>')
        out += ["        " + bl if bl.strip() else bl for bl in body]
        out.append(f"{pad}    </StackPanel>")
        out.append(f"{pad}</Border>")
        out.append("")
        out += tail_after

    # Peer content after the last group is page-level: it keeps its depth, and
    # only the page's own closing lines sit one level shallower than the cards.
    out += lines[cursor:body_close]
    # The page's own closing lines sit outside every card, so they were never
    # re-indented — they are copied verbatim.
    out += lines[body_close:]

    # Self-check: the transformation only ever opens one Border + one
    # StackPanel per card and swaps one TextBlock band (+ its recap line) for
    # the card's own title (+ helper). Any other drift means an extent was
    # mis-resolved — refuse to write a file that would stop being XAML.
    result = "\n".join(out) + "\n"
    before, after = count_tags(lines), count_tags(result.splitlines())
    expect = before[:]
    expect[0] += n_cards
    expect[1] += n_cards
    if after != expect:
        raise SystemExit(
            f"{path.name}: tag balance drifted — expected {expect}, got {after} "
            f"(order: Border, StackPanel, TextBlock, Button, ItemsControl)")
    elements(result)          # raises if any tag is unclosed or closes early

    if not dry_run:
        path.write_text(result, encoding="utf-8")
    return len(sites), names


def count_tags(lines: list[str]) -> list[int]:
    """A coarse fingerprint: how many of the structural tags the file opens."""
    text = "\n".join(lines)
    blanked = blank_comments(text)
    return [len(re.findall(r"<" + tag + r"\b", blanked))
            for tag in ("Border", "StackPanel", "TextBlock", "Button", "ItemsControl")]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--only", help="comma-separated group keys to card (default: all)")
    ap.add_argument("--stop-before", dest="stop_before",
                    help="regex ending the LAST group early (a peer card follows)")
    args = ap.parse_args()

    for raw in args.files:
        path = Path(raw)
        if not path.is_absolute():
            path = REPO / path
        only = [o.strip() for o in args.only.split(",")] if args.only else None
        count, names = convert(path, args.dry_run, only, args.stop_before)
        verb = "would card" if args.dry_run else "carded"
        print(f"{verb} {count:2d} group(s) in {path.name}: " + ", ".join(names))
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Settings rewrite — apply the approved grammar to a section file.

WHAT THIS DOES (and deliberately does NOT do)
────────────────────────────────────────────
The redesign is one grammar applied to twelve pages. Ten of them share the
identical skeleton (page-title StackPanel, Grid + ScrollViewer, content
StackPanel with 32px gutters), so the mechanical part is scripted — but only
the mechanical part:

  1. drop the LOCAL page title/subtitle block (the shell header strip is the
     single page header — every page printed its title twice)
  2. normalise the page padding to the Settings scale (32px → 18/14)
  3. SurfacePrimaryStyle  → SettingsPageCardStyle   (the About card)
     SurfaceDetailStyle   → SettingsInnerTileStyle   (the About tile)
  4. an EMPTY SurfaceDetailStyle Border (a card used as a vertical gap) →
     SettingsRowSeparatorStyle
  5. Typography.* roles and the group caption → the grammar's type roles
  6. merge SettingsCardStyles.xaml into the page's resources

It does NOT touch bindings, commands, converters, control names, form fields
or text keys. The companion tool (verify_bindings.py) proves that afterwards.

Anything a regex cannot judge — a chip that happens to wear a card style, a
card whose header needs an icon disc — is REPORTED for a human edit instead
of guessed at. `--report` prints those sites and changes nothing.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SECTIONS = REPO / "Views" / "Settings" / "Sections"

TITLE_BLOCK = re.compile(
    r"[ \t]*<StackPanel Grid\.Row=\"0\" Margin=\"32,26,32,14\">.*?</StackPanel>\n",
    re.S,
)

MERGES = re.compile(
    r"(<ResourceDictionary\.MergedDictionaries>\s*\n)"
    r"(\s*)<ResourceDictionary Source=\"\.\./SettingsSectionStyles\.xaml\"/>",
    re.S,
)

# A Border wearing the CARD style: the opening tag, so we can inspect it.
CARD_OPEN = re.compile(r"<Border\b[^>]*Style=\"\{StaticResource SurfacePrimaryStyle\}\"")


def chip_like(opening_tag: str) -> bool:
    """True when a Border that wears a card style is actually a small
    badge/chip: a chip has small symmetric padding and usually a solid
    Background of its own. Those must become grammar chips, not cards —
    a 22,20 padded 26px card in a chip slot is a visible regression."""
    if re.search(r'Padding="\d+,\s*[0-7]"', opening_tag):
        return True
    if re.search(r'(Width|Height)="\d{1,2}"', opening_tag):
        return True
    return False



# ── HAND-BUILT CARDS ────────────────────────────────────────────────
# The old module declared card chrome inline on the element:
#     <Border BorderBrush="{DynamicResource BorderDefaultBrush}"
#             BorderThickness="{DynamicResource CardBorderThickness}"
#             Background="{DynamicResource SurfaceTertiaryBrush}"
#             CornerRadius="{DynamicResource CardCornerRadius}"
#             Padding="14,10" Grid.Column="0">
# Twelve pages × their own padding/radius/token choice = twelve slightly
# different cards. Each one becomes the grammar tile that matches its wash,
# with the inline chrome removed (the style owns it now) and everything that
# positions the element (Grid.Column, Grid.Row, Margin, Width) preserved.
# The terminator is captured SEPARATELY: the first version of this tool
# re-emitted the opening tag without its ">" and produced malformed
# XAML (caught by diff review, not by any test).
INLINE_CARD = re.compile(r"<Border\b(?P<attrs>[^>]*?)(?P<end>/?>)", re.S)
CHROME_ATTRS = ("BorderBrush", "BorderThickness", "Background", "CornerRadius",
                "Padding", "Effect", "SnapsToDevicePixels")

WASH_TO_STYLE = (
    ("TintBlueBrush", "SettingsInfoTileStyle"),
    ("Ios26AccentSoftBrush", "SettingsInfoTileStyle"),
    ("TintGreenBrush", "SettingsSuccessTileStyle"),
    ("SuccessSoftBrush", "SettingsSuccessTileStyle"),
    ("Ios26SuccessSoftBrush", "SettingsSuccessTileStyle"),
    ("TintAmberBrush", "SettingsWashTileStyle"),
    ("SurfaceTertiaryBrush", "SettingsInnerTileStyle"),
    ("SurfaceDetailBrush", "SettingsInnerTileStyle"),
    ("SurfaceSecondaryBrush", "SettingsInnerTileStyle"),
    ("Ios26ValuePillBrush", "SettingsWashTileStyle"),
)


def tile_for(attrs: str) -> str | None:
    """The grammar tile for a hand-built card, or None when the Border is
    not a card (no Background at all → a layout wrapper)."""
    if "Background=" not in attrs:
        return None
    if "CornerRadius=" not in attrs:
        return None
    m = re.search(r'Background="\{DynamicResource ([A-Za-z0-9_.]+)\}', attrs)
    if not m:
        return None
    token = m.group(1)
    for needle, style in WASH_TO_STYLE:
        if token == needle:
            return style
    return "SettingsInnerTileStyle"


def strip_chrome(attrs: str) -> str:
    for name in CHROME_ATTRS:
        attrs = re.sub(r'\s*' + name + r'="[^"]*"', "", attrs)
    cleaned = re.sub(r"\s+", " ", attrs)
    return (" " + cleaned.strip()) if cleaned.strip() else ""


def convert_inline_cards(src: str) -> tuple[str, list[str]]:
    notes: list[str] = []
    out = []
    last = 0
    for m in INLINE_CARD.finditer(src):
        attrs = m.group("attrs")
        if "Style=" in attrs:
            continue

        # A Border whose style arrives as the <Border.Style> PROPERTY ELEMENT
        # must be left alone: injecting a Style attribute next to it makes the
        # XAML compiler report "property set more than once" (caught by the
        # duplicate-property gate stage, not by inspection).
        lookahead = src[m.end():m.end() + 120].lstrip()
        if lookahead.startswith("<Border.Style>"):
            notes.append("REVIEW <Border.Style> property-element form left as is")
            continue

        style = tile_for(attrs)
        if style is None:
            continue
        out.append(src[last:m.start()])
        out.append(f'<Border Style="{{StaticResource {style}}}"'
                   f'{strip_chrome(attrs)}{m.group("end")}')
        last = m.end()
    if not out:
        return src, notes
    out.append(src[last:])
    result = "".join(out)
    n = result.count('Style="{StaticResource Settings') - src.count('Style="{StaticResource Settings')
    notes.append(f"hand-built inline card(s) → grammar tile(s) ({n})")
    return result, notes

def transform(src: str) -> tuple[str, list[str]]:
    notes: list[str] = []
    out = src

    # 1 — local page title block
    if TITLE_BLOCK.search(out):
        out = TITLE_BLOCK.sub("", out, count=1)
        notes.append("removed the local page title/subtitle block")
    else:
        notes.append("no local title block found (already converted?)")

    # 2 — page padding
    if '<StackPanel Margin="32,0,32,32"' in out:
        out = out.replace('<StackPanel Margin="32,0,32,32"',
                          '<StackPanel Margin="18,14,18,18"', 1)
        notes.append("page gutters 32 → 18,14 (the Settings page scale)")

    # 3a — report card-style sites that are really chips
    for m in CARD_OPEN.finditer(out):
        tag = m.group(0)
        if chip_like(tag):
            notes.append("REVIEW chip-like site: " + " ".join(tag.split())[:110])

    # 3b — empty card used as a spacer → a real separator
    n_spacer = len(re.findall(r'<Border Style="\{StaticResource SurfaceDetailStyle\}"/>', out))
    out = out.replace('<Border Style="{StaticResource SurfaceDetailStyle}"/>',
                      '<Border Style="{StaticResource SettingsRowSeparatorStyle}"/>')
    if n_spacer:
        notes.append(f"replaced {n_spacer} empty-card spacer(s) with a real separator")

    # 4 — the grammar
    n_card = len(re.findall(r'Style="\{StaticResource SurfacePrimaryStyle\}"', out))
    out = out.replace('Style="{StaticResource SurfacePrimaryStyle}"',
                      'Style="{StaticResource SettingsPageCardStyle}"')
    n_tile = len(re.findall(r'Style="\{StaticResource SurfaceDetailStyle\}"', out))
    out = out.replace('Style="{StaticResource SurfaceDetailStyle}"',
                      'Style="{StaticResource SettingsInnerTileStyle}"')
    notes.append(f"card style → SettingsPageCardStyle ({n_card} site(s)), "
                 f"tile style → SettingsInnerTileStyle ({n_tile} site(s))")

    for old, new in (
        ("SettingsGroupHeaderTextStyle", "SettingsGroupCaptionStyle"),
        ("InfoGlyphBadgeStyle", "SettingsIconTileStyle"),
        ("InfoGlyphTextStyle", "SettingsIconGlyphStyle"),
        ("Typography.Title", "SettingsRowTitleStyle"),
        ("Typography.BodySmall", "SettingsRowMetaStyle"),
        ("Typography.Caption", "SettingsRowMetaStyle"),
    ):
        n = out.count(old)
        if n:
            out = out.replace(old, new)
            notes.append(f"{old} → {new} ({n})")

    # 4b — hand-built inline cards
    out, inline_notes = convert_inline_cards(out)
    notes.extend(inline_notes)

    # 5 — merge the grammar dictionary
    if "SettingsCardStyles.xaml" in out:
        notes.append("grammar already merged")
    else:
        out, n = MERGES.subn(
            r'\1\2<ResourceDictionary Source="../SettingsCardStyles.xaml"/>\n'
            r'\2<ResourceDictionary Source="../SettingsSectionStyles.xaml"/>',
            out, count=1)
        notes.append("merged SettingsCardStyles.xaml" if n
                     else "WARNING: could not find the merge block")

    return out, notes


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--report", action="store_true",
                    help="print the plan and change nothing")
    args = ap.parse_args()

    for name in args.files:
        path = Path(name)
        if not path.is_absolute():
            path = SECTIONS / path
        src = path.read_text(encoding="utf-8-sig")
        out, notes = transform(src)

        print(f"── {path.name}")
        for note in notes:
            print(f"   {note}")

        if not args.report:
            path.write_text(out, encoding="utf-8")
            print(f"   written ({len(src.splitlines())} → {len(out.splitlines())} lines)")

    return 0


if __name__ == "__main__":
    sys.exit(main())

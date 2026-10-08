#!/usr/bin/env python3
"""Measure the top panel, and say whether it fits.

The header is the one part of this site whose width budget can be worked out
exactly. It is a fixed-height row of fixed-size things — a lockup, six nav
cards, three settings controls, a call to action and the build stamp — and the
only unknown in the arithmetic is how wide a word is, which is not unknown at
all: the font files are in `src/fonts`, and `fontTools` reads the advance width
of every glyph out of them at any weight.

That matters because the panel cannot wrap and must not squeeze. Everything in
it is a flex item in a `nowrap` row, so a row that is over budget does not
overflow politely: the flexible children shrink, and the labels inside them wrap
mid-phrase — "Request a / Demo" — inside a bar whose height is fixed. The result
is a navigation bar that looks broken, and it is the failure this script exists
to catch before a browser does.

    python3 scripts/measure-panel.py            # the ladder, with verdicts
    python3 scripts/measure-panel.py --json     # the same numbers, machine-readable

Requires `fontTools` and `brotli` (`pip install --break-system-packages
fonttools brotli`). Exit code 1 if any tier is over its budget.

## What it mirrors

It reads the real components rather than a copy of them: the nav entries are
parsed out of `src/components/layout/Navbar.tsx`'s `entries` array, the strings
come from the same file and from `LanguageMenu.tsx`, `ThemeToggle.tsx`,
`ThemeModeSwitch.tsx`, `BuildStamp.tsx` and `src/lib/site.ts`, and the box model
of each — padding, borders, gaps, icon sizes — is transcribed beside the class
string it comes from.

Transcription is where a tool like this rots, so the transcription is checked:
`check_markup()` reads the components and fails the run when the classes the
model assumes are not the classes in the markup. It exists because they once
were not — the nav grew a seventh entry, and the model went on measuring six
while reporting slack that the browser did not have.

## The ladder

The panel sheds its least important items as the window narrows, in a fixed
order, so that nothing is ever squeezed. Each tier is what it is because the
arithmetic says the tier below cannot hold it, not because it seemed tidy:

    wide  1920+   all seven nav cards with glyph icons, palette word, divider,
                  primary CTA, stamp
    2xl   1536    five nav cards + Menu for the other two, no palette word,
                  primary CTA, divider, stamp
    xl    1280    three nav cards + Menu for the other four, no palette word,
                  no divider, primary CTA, stamp
    lg    1024    no nav cards: Menu holds all seven, primary CTA, stamp
    <lg           the phone row: settings and the menu button, stamp in the sheet

The stamp itself carries the site's name, the date and the time in Gulf Standard
Time, which is what makes it 126px wide rather than 75 — and what the two widest
tiers pay for by showing fewer nav cards.
"""
import json
import pathlib
import re
import sys

try:
    from fontTools.ttLib import TTFont
    from fontTools.varLib.instancer import instantiateVariableFont
except ImportError:  # pragma: no cover - the message is the feature
    raise SystemExit(
        "measure-panel.py needs fontTools and brotli:\n"
        "    pip install --break-system-packages fonttools brotli"
    )

ROOT = pathlib.Path(__file__).resolve().parent.parent
FONTS = ROOT / "src" / "fonts"

# ── the fonts ────────────────────────────────────────────────────────────────
# Inter for the interface, JetBrains Mono for the technical labels — the same
# two files the site self-hosts. Instanced at the weight the class asks for.
_INTER = {}
_MONO = {}


def _instance(path, weight, cache):
    if weight not in cache:
        font = TTFont(path)
        cache[weight] = instantiateVariableFont(font, {"wght": weight}, inplace=True)
    return cache[weight]


def advance(font, text, size, tracking=0.0):
    """The width of `text` in px at `size`, tracking included after every glyph.

    Browsers apply letter-spacing after the last character too, so the trailing
    space is counted — it is the difference between a label that fits and one
    that wraps, on a row that cannot wrap.
    """
    upem = font["head"].unitsPerEm
    cmap = font.getBestCmap()
    hmtx = font["hmtx"]
    total = 0
    for char in text:
        glyph = cmap.get(ord(char)) or cmap.get(ord(" "))
        if glyph is None:
            continue
        total += hmtx[glyph][0]
    return total * size / upem + tracking * size * len(text)


def inter(text, size, weight=600, tracking=0.0):
    return advance(_instance(FONTS / "Inter-Variable-latin.woff2", weight, _INTER), text, size, tracking)


def mono(text, size, weight=600, tracking=0.0):
    return advance(_instance(FONTS / "JetBrainsMono-Variable-latin.woff2", weight, _MONO), text, size, tracking)


# ── the pieces, each beside the class it was read from ───────────────────────
#
# Tailwind's spacing scale, in px. `2` = 0.5rem = 8px, and so on.
P = {"0.5": 2, "1": 4, "1.5": 6, "2": 8, "2.5": 10, "3": 12, "3.5": 14, "4": 16}

NAVBAR = ROOT / "src" / "components" / "layout" / "Navbar.tsx"
THEME_TOGGLE = ROOT / "src" / "components" / "layout" / "ThemeToggle.tsx"

# How many nav cards each tier shows, in the order the markup lists them. The
# rest are in the Menu button's sheet, so nothing is unreachable at any width.
CARDS_AT_XL = 3
CARDS_AT_2XL = 5


def read_nav_entries():
    """The `entries` array in `Navbar.tsx`: (label, opens a menu), in order.

    Parsed rather than copied, because the copy is what went stale: the markup
    grew a seventh entry and this tool went on measuring six. Braces are counted
    so a nested `children` array cannot be mistaken for the next entry, and an
    empty result is an error rather than a zero-width nav — a parser that
    silently measures nothing is worse than the stale list it replaced.
    """
    source = NAVBAR.read_text(encoding="utf8")
    marker = "const entries: NavEntry[] = ["
    if marker not in source:
        raise SystemExit(
            f"measure-panel.py: cannot find `{marker}` in {NAVBAR.name} — "
            "the nav parser needs updating before this measurement means anything"
        )
    depth = 1  # the array's own bracket is already open
    entries: list = []
    current = ""
    in_entry = False
    for char in source[source.index(marker) + len(marker):]:
        if char == "[":
            depth += 1
        elif char == "]":
            depth -= 1
            if depth == 0:
                break
        elif char == "{":
            if depth == 1:
                current = ""
                in_entry = True
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 1 and in_entry:
                label = re.search(r'label:\s*"([^"]+)"', current)
                if label:
                    entries.append((label.group(1), "children:" in current))
                current = ""
                in_entry = False
        elif in_entry:
            current += char
    if not entries:
        raise SystemExit(
            f"measure-panel.py: read no nav entries out of {NAVBAR.name}, which "
            "cannot be right — the panel has a nav"
        )
    return entries


NAV_ENTRIES = read_nav_entries()
WORDMARK = "ProGlass"
WORDMARK_SUB = "AUTOMATION"


def brand(wordmark=True):
    """`<Logo />` — mark 34px, `gap-2.5`, then the wordmark stack."""
    width = 34
    if wordmark:
        width += P["2.5"] + max(
            inter(WORDMARK, 15.2),
            mono(WORDMARK_SUB, 11, tracking=0.28),
        )
    return width


def nav_pill(labels=None, glyphs=False, wide=False):
    """`NAV_BAR` + `NAV_CARD` — the cards a tier shows, inside one pill.

    `labels` is the subset of `NAV_ENTRIES` that tier displays; the default is
    all of them, which is the widest tier's answer.
    """
    cards = 0
    entries = NAV_ENTRIES if labels is None else labels
    for label, has_menu in entries:
        card = (
            (P["3"] * 2 if wide else P["2.5"] * 2)  # px-3 / px-2.5
            + 2 * 2  # border-2
            + (14 if glyphs else 0)  # NavGlyph h-3.5
            + (P["1.5"] if glyphs else 0)  # gap-1.5, only if there is a glyph
            + inter(label, 14)
            + ((P["1.5"] + 10) if has_menu else 0)  # chevron h-2.5
        )
        cards += card
    return cards + (len(entries) - 1) * P["0.5"] + P["1"] * 2 + 2 * 2


def language_menu():
    """`LanguageMenu` trigger: globe 16, label, chevron 10, px-2, border-2."""
    return P["2"] * 2 + 2 * 2 + 16 + P["1.5"] + inter("English", 14) + P["1.5"] + 10


def reading_switch(segment):
    """`ThemeModeSwitch`: three segments, `p-0.5`, one border."""
    return 3 * segment + P["0.5"] * 2 + 2


def palette(word=False):
    """`ThemeToggle` trigger: swatch 14, optional family word, chevron 12, px-2.5."""
    label = mono("STUDIO", 10, tracking=0.12) if word else 0
    return P["2.5"] * 2 + 2 + 14 + P["2"] + label + P["2"] + 12


def cta(label="Request a Demo", ghost=False):
    """`ButtonLink size="sm"`: px-3.5, label 13px, `gap-2`, ArrowRight 14px."""
    return P["3.5"] * 2 + inter(label, 13) + P["2"] + 14 + (2 if ghost else 0)


def compact_language_menu():
    """`LanguageMenu compact` on the phone row — same box, `max-w-[5.5rem]` label."""
    return P["2"] * 2 + 2 * 2 + 16 + P["1.5"] + min(inter("English", 14), 88) + P["1.5"] + 10


def menu_icon_button():
    """The phone menu button: `h-11 w-11`."""
    return 44


def menu_button():
    """The lg-tier Menu button: a card at `px-2.5` with a 16px glyph."""
    return P["2.5"] * 2 + 2 * 2 + 16 + P["1.5"] + inter("Menu", 14)


def stamp_pill():
    """`BuildStampPill`: three mono lines at 9.5px, `px-2`, one border.

    The lines are the site's name, the date and the time in Gulf Standard Time,
    so the widest is whichever of the three is longest — measured, not assumed.
    """
    return P["2"] * 2 + 2 + max(
        mono("ProGlass Automation", 9.5),
        mono("2026-10-07", 9.5),
        mono("16:08 GST", 9.5),
    )


DIVIDER = P["1"] * 2 + 1  # `mx-1` around a hairline


def cluster(items):
    """The settings cluster, which is `gap-2` between everything it holds."""
    return sum(items) + P["2"] * (len(items) - 1)


# ── the tiers ────────────────────────────────────────────────────────────────
# `shell` padding is `px-10 lg` / `px-12 xl` / `px-16 2xl`, counted both sides;
# 15px is a desktop scrollbar, because the row is laid out in the viewport minus
# the scrollbar and a budget that forgets it is a budget that fails in a browser.
TIERS = [
    # name, narrowest width of the tier, shell padding, nav, cluster
    ("phone", 360, 40, [], 0),
    ("sm", 640, 56, [], 0),
    ("lg", 1024, 80, [menu_button()], 0),
    ("xl", 1280, 96, 0, 0),
    ("2xl", 1536, 128, 0, 0),
    ("wide", 1920, 128, 0, 0),
]

SCROLLBAR = 15
GAP = P["4"]  # `gap-4` between the lockup, the nav and the cluster


def tier_rows():
    """Every tier, its pieces and its verdict."""
    rows = []
    for name, viewport, padding, nav_extra, _ in TIERS:
        if name == "phone":
            # `px-5`: the mark alone (the wordmark is `hidden lg:flex`), the
            # compact language button, the palette, and the 44px menu button.
            nav = []
            cluster_items = [
                # No language button below `sm` — see the note in Navbar.tsx.
                palette(),
                menu_icon_button(),
            ]
            wordmark = False
        elif name == "sm":
            # `px-7`, and the reading switch joins the row at `sm`.
            nav = []
            cluster_items = [
                compact_language_menu(),
                reading_switch(40),  # w-10 below lg
                palette(),
                menu_icon_button(),
            ]
            wordmark = False
        elif name == "lg":
            # The Menu button is *in the cluster* (before the language button),
            # not in the nav — counting it in both places was how this tool once
            # reported 84px of slack that did not exist.
            nav = []
            cluster_items = [
                menu_button(),
                language_menu(),
                reading_switch(32),  # lg:w-8
                palette(),
                cta(),
                # No divider below 2xl: the stamp carries its own edge, and the
                # hairline is 17px the narrow tiers cannot spare.
                stamp_pill(),
            ]
            wordmark = True
        elif name == "xl":
            # Three cards and the Menu button: measured, the seven cards plus
            # the settings, the CTA and the stamp need about 1450px here.
            nav = [nav_pill(NAV_ENTRIES[:CARDS_AT_XL])]
            cluster_items = [
                menu_button(),
                language_menu(),
                reading_switch(32),  # lg:w-8 (xl keeps it until 2xl)
                palette(),
                cta(),
                stamp_pill(),
            ]
            wordmark = True
        elif name == "2xl":
            # Five cards and the Menu button for the other two. The palette
            # word is what the stamp's own width costs here.
            nav = [nav_pill(NAV_ENTRIES[:CARDS_AT_2XL])]
            cluster_items = [
                menu_button(),
                language_menu(),
                reading_switch(36),  # 2xl:w-9
                palette(),
                cta(),
                DIVIDER,
                stamp_pill(),
            ]
            wordmark = True
        else:  # wide
            # Everything the panel owns, and the only tier that shows the nav's
            # glyph icons and the palette's word. No Menu button: all seven
            # cards are on screen, so there is nothing left for it to hold.
            nav = [nav_pill(glyphs=True, wide=True)]
            cluster_items = [
                language_menu(),
                reading_switch(36),
                palette(word=True),
                cta(),
                DIVIDER,
                stamp_pill(),
            ]
            wordmark = True

        middle = sum(nav)
        settings = cluster(cluster_items)
        content = brand(wordmark) + GAP + middle + GAP + settings
        available = viewport - padding - SCROLLBAR
        rows.append(
            {
                "tier": name,
                "viewport": viewport,
                "brand": round(brand(wordmark), 1),
                "nav": round(middle, 1),
                "cluster": round(settings, 1),
                "content": round(content, 1),
                "available": available,
                "slack": round(available - content, 1),
                "fits": content <= available,
            }
        )
    return rows


# ── the check that keeps the transcription honest ────────────────────────────
# Every entry is (what it proves, file, regex, must be there). A model that has
# drifted from the markup is worse than no model: it reports slack the browser
# does not have. This is where the drift is caught.
MARKUP_CHECKS = [
    (
        "the nav container holds cards from xl",
        NAVBAR,
        r'NAV_BAR = "[^"]*xl:flex',
        True,
    ),
    (
        "the first three cards arrive at xl",
        NAVBAR,
        r'"hidden xl:flex"',
        True,
    ),
    (
        "cards four and five arrive at 2xl",
        NAVBAR,
        r'"hidden 2xl:flex"',
        True,
    ),
    (
        "the last two cards wait for wide",
        NAVBAR,
        r'"hidden wide:flex"',
        True,
    ),
    (
        "the first split matches CARDS_AT_XL",
        NAVBAR,
        rf"NAV_CARD_FIRST_TIER = {CARDS_AT_XL}\b",
        True,
    ),
    (
        "the second split matches CARDS_AT_2XL",
        NAVBAR,
        rf"NAV_CARD_SECOND_TIER = {CARDS_AT_2XL}\b",
        True,
    ),
    (
        "the Menu button serves lg through 2xl",
        NAVBAR,
        r"lg:flex wide:hidden",
        True,
    ),
    (
        "the build stamp is in the panel from lg",
        NAVBAR,
        r'BuildStampPill t=\{t\} className="hidden lg:block"',
        True,
    ),
    (
        "the divider belongs to 2xl and up",
        NAVBAR,
        r"hidden h-6 w-px bg-line/70 2xl:block",
        True,
    ),
    (
        "the primary CTA is in the row at every tier",
        NAVBAR,
        r'<ButtonLink href="/contact" size="sm"',
        True,
    ),
    (
        # The label goes through the translator, so the check looks for that
        # rather than the bare words — which a comment above the CTA quotes, on
        # purpose, to say where the button went.
        "the ghost shortcut is gone, not merely hidden",
        NAVBAR,
        r't\("See the app"\)',
        False,
    ),
    (
        "the palette word waits for wide",
        THEME_TOGGLE,
        r"wide:inline",
        True,
    ),
]


def check_markup() -> list:
    """Read the components and hold the model against the classes in them."""
    problems = []
    for what, path, pattern, present in MARKUP_CHECKS:
        found = re.search(pattern, path.read_text(encoding="utf8")) is not None
        if found != present:
            problems.append(f"{what}: {path.name} {'lacks' if present else 'still has'} /{pattern}/")
    return problems


def main() -> int:
    problems = check_markup()
    rows = tier_rows()
    machine = "--json" in sys.argv

    if problems:
        print("\nthe model and the markup disagree — fix one of them:\n")
        for problem in problems:
            print(f"  - {problem}")
        print()
        return 1
    if not machine:
        print(
            f"\n  {len(MARKUP_CHECKS)} markup checks pass: the classes this model "
            f"measures are the classes in the components.\n"
        )

    if machine:
        print(json.dumps(rows, indent=2))
    else:
        print("\nthe top panel, measured from the site's own font files\n")
        print(f"  {'tier':6s} {'from':>6s} {'brand':>7s} {'nav':>7s} {'settings':>9s} {'needs':>8s} {'have':>7s} {'slack':>7s}")
        for row in rows:
            flag = " " if row["fits"] else "!"
            print(
                f" {flag}{row['tier']:6s} {row['viewport']:6d} {row['brand']:7.1f} {row['nav']:7.1f} "
                f"{row['cluster']:9.1f} {row['content']:8.1f} {row['available']:7d} {row['slack']:+7.1f}"
            )
        print(
            "\n  `needs` is what the row's contents measure; `have` is the viewport at the\n"
            "  narrowest width of the tier, minus the shell's own padding and a scrollbar.\n"
        )

    over = [row for row in rows if not row["fits"]]
    if over:
        print("OVER BUDGET:")
        for row in over:
            print(f"  - {row['tier']}: needs {row['content']:.0f}px in {row['available']}px")
        return 1
    if not machine:
        print("Every tier fits. The panel sheds items in the order documented in Navbar.tsx.\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

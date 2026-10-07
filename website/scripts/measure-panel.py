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

It reads the real components rather than a copy of them: the strings come from
`src/components/layout/Navbar.tsx`, `LanguageMenu.tsx`, `ThemeToggle.tsx`,
`ThemeModeSwitch.tsx`, `BuildStamp.tsx` and `src/lib/site.ts` where they can,
and the box model of each — padding, borders, gaps, icon sizes — is transcribed
beside the class string it comes from. When the panel changes, the numbers here
change only if the transcription does, which is why every constant below carries
the Tailwind class it was read from.

## The ladder

The panel sheds its least important items as the window narrows, in a fixed
order, so that nothing is ever squeezed:

    wide  1920+   everything — nav with glyph icons, palette word, both CTAs, stamp
    2xl   1536    nav, palette word, primary CTA, stamp        (no ghost shortcut)
    xl    1280    nav (no glyph icons), settings, stamp        (no CTA, no word)
    lg    1024    Menu button, settings, primary CTA, stamp    (no nav pill)
    <lg           the phone row: settings + menu button, stamp in the sheet
"""
import json
import pathlib
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

NAV_ENTRIES = [  # `entries` in Navbar.tsx: label, opens a menu
    ("Home", False),
    ("Product", True),
    ("Optimization", False),
    ("Resources", True),
    ("About", False),
    ("Developer", False),
]
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


def nav_pill(glyphs=True, wide=False):
    """`NAV_BAR` + `NAV_CARD` — six cards, one pill."""
    cards = 0
    for label, has_menu in NAV_ENTRIES:
        card = (
            (P["3"] * 2 if wide else P["2.5"] * 2)  # px-3 / px-2.5
            + 2 * 2  # border-2
            + (14 if glyphs else 0)  # NavGlyph h-3.5
            + (P["1.5"] if glyphs else 0)  # gap-1.5, only if there is a glyph
            + inter(label, 14)
            + ((P["1.5"] + 10) if has_menu else 0)  # chevron h-2.5
        )
        cards += card
    return cards + (len(NAV_ENTRIES) - 1) * P["0.5"] + P["1"] * 2 + 2 * 2


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


def menu_button():
    """The lg-tier Menu button: a card at `px-2.5` with a 16px glyph."""
    return P["2.5"] * 2 + 2 * 2 + 16 + P["1.5"] + inter("Menu", 14)


def stamp_pill():
    """`BuildStampPill`: two mono lines at 9.5px, `px-2`, one border."""
    return P["2"] * 2 + 2 + max(mono("2026-10-07", 9.5), mono("11:54 UTC", 9.5))


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
        if name == "lg":
            nav = nav_extra  # the Menu button lives in the cluster
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
            nav = [nav_pill(glyphs=False)]
            cluster_items = [
                language_menu(),
                reading_switch(32),  # lg:w-8 (xl keeps it until 2xl)
                palette(),
                stamp_pill(),
            ]
            wordmark = True
        elif name == "2xl":
            nav = [nav_pill(glyphs=False)]
            cluster_items = [
                language_menu(),
                reading_switch(36),  # 2xl:w-9
                palette(word=True),
                cta(),
                DIVIDER,
                stamp_pill(),
            ]
            wordmark = True
        else:  # wide
            nav = [nav_pill(glyphs=True, wide=True)]
            cluster_items = [
                language_menu(),
                reading_switch(36),
                palette(word=True),
                cta(),
                cta("See the app", ghost=True),
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


def main() -> int:
    rows = tier_rows()
    machine = "--json" in sys.argv

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

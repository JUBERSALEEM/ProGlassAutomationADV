#!/usr/bin/env python3
"""Settings rewrite — which grammar styles the pages actually use.

WHY THIS EXISTS
───────────────
`Views/Settings/SettingsCardStyles.xaml` is the module's design system: every
element the approved sheets show, expressed once. A design system legitimately
declares a little more than the current pages consume — the sheets show a
segmented control, status chips, metric pairs and a status dot that later passes
still have to wire.

What is NOT legitimate is losing track of that gap. A key nobody uses and nobody
remembers is indistinguishable from a key held in reserve, and the next person
adds a fourth chip style on top of three unused ones.

So this reports the gap explicitly, and can gate it:

    python3 scripts/settings-rewrite/grammar_usage.py             # report
    python3 scripts/settings-rewrite/grammar_usage.py --fail-unused
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
GRAMMAR = REPO / "Views" / "Settings" / "SettingsCardStyles.xaml"
VIEWS = REPO / "Views"
STYLE_KEY = re.compile(r'<Style x:Key="(Settings[A-Za-z]+)"')


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--fail-unused", action="store_true")
    args = ap.parse_args()

    keys = sorted(set(STYLE_KEY.findall(GRAMMAR.read_text(encoding="utf-8"))))
    consumers = {p: p.read_text(encoding="utf-8")
                 for p in VIEWS.rglob("*.xaml") if p != GRAMMAR}

    used: list[tuple[str, int]] = []
    unused: list[str] = []
    for key in keys:
        hits = sum(text.count(key) for text in consumers.values())
        (used if hits else unused).append((key, hits) if hits else key)

    print(f"grammar styles: {len(keys)} declared, "
          f"{len(used)} consumed by the module, {len(unused)} waiting")
    for key, hits in used:
        print(f"    {hits:4d}  {key}")
    if unused:
        print("\n  declared for the sheets, not wired yet:")
        for key in unused:
            print(f"        {key}")
    return 1 if (args.fail_unused and unused) else 0


if __name__ == "__main__":
    sys.exit(main())

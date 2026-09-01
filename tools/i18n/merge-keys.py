#!/usr/bin/env python3
"""tools/i18n/merge-keys.py — merge sweep keys + authored translations.

Usage:
  python3 tools/i18n/merge-keys.py [--langs ar,hi,...] [--report]

  • Reads the sweep key table (tools/i18n/sweep-keys.json).
  • Reads authored translations from tools/i18n/translations/{code}.json
    (partial files allowed — missing keys fall back to the English value,
    so every shipped dictionary ALWAYS carries the identical key set and
    a missing translation shows English, never a blank).
  • Rewrites every tools/i18n/{code}.py STRINGS dict (kept sorted).
  • --report prints per-language coverage (keys whose value differs from
    English) after the merge.

Run build.py afterwards to regenerate Themes/Languages/*.xaml.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
T = HERE / "translations"


def load_py(code: str) -> dict:
    spec = importlib.util.spec_from_file_location(f"i18n_{code}", HERE / f"{code}.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return dict(mod.STRINGS)


def write_py(code: str, table: dict) -> None:
    body = "".join(
        f'    {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)},\n'
        for k, v in sorted(table.items()))
    header = f"""# ═══════════════════════════════════════════════════════════════════════
# {code}.py — UI strings for this language. One short file per language.
# Edit here, then run `python tools/i18n/build.py` to regenerate
# Themes/Languages/{code}.xaml.  (Kept sorted by tools/i18n/merge-keys.py;
# missing translations fall back to English automatically.)
# ═══════════════════════════════════════════════════════════════════════

STRINGS = {{
"""
    (HERE / f"{code}.py").write_text(header + body + "}\n", encoding="utf-8")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--langs", default="")
    ap.add_argument("--report", action="store_true")
    args = ap.parse_args()

    english = load_py("en-US")
    sweep = json.loads((ROOT / "tools" / "i18n" / "sweep-keys.json")
                       .read_text(encoding="utf-8"))
    # en-US: sweep keys are authoritative (they ARE the English text).
    merged_en = dict(english)
    for k, v in sweep.items():
        merged_en.setdefault(k, v)

    langs = args.langs.split(",") if args.langs else []
    files = sorted(HERE.glob("*.py"))
    codes = [f.stem for f in files
             if not f.stem.startswith(("build", "merge", "sweep", "__"))]

    def current(code: str) -> dict:
        return merged_en if code == "en-US" else load_py(code)

    coverage: dict[str, tuple[int, int]] = {}

    for code in codes:
        if langs and code not in langs:
            continue
        table = current(code)
        # drop keys that no longer exist in English (cleanup)
        table = {k: v for k, v in table.items() if k in merged_en}
        # add every missing key (English fallback value)
        for k, v in merged_en.items():
            table.setdefault(k, v)
        # apply authored translations (all tools/i18n/translations/{code}*.json)
        overlays: dict[str, list[tuple[str, str]]] = {}
        for tf in sorted(T.glob(f"{code}*.json")):
            auth = json.loads(tf.read_text(encoding="utf-8"))
            for k, v in auth.items():
                if k not in table:
                    print(f"  ! {code}: unknown key {k!r} in {tf.name} (ignored)")
                    continue
                if not isinstance(v, str) or not v.strip():
                    print(f"  ! {code}: empty value for {k!r} in {tf.name} (ignored)")
                    continue
                overlays.setdefault(k, []).append((tf.name, v))
        # guard against silent override: same key in several overlays must agree
        for k, hits in sorted(overlays.items()):
            distinct = {(v) for _, v in hits}
            if len(distinct) > 1:
                print(f"  ! {code}: CONFLICT on {k!r}: " +
                      ", ".join(f"{f}={v!r}" for f, v in hits))
        for k, hits in overlays.items():
            table[k] = hits[-1][1]  # last (sorted) overlay wins, as before
        if code != "en-US":
            diff = sum(1 for k in merged_en if table[k] != merged_en[k])
            coverage[code] = (diff, len(merged_en))
        write_py(code, table)
        print(f"  ✓ {code}.py  ({len(table)} keys)")

    if args.report and coverage:
        print("\nCoverage (translated keys / total, per language):")
        for code, (d, total) in sorted(coverage.items(),
                                       key=lambda kv: -kv[1][0] / kv[1][1]):
            print(f"  {code}: {d}/{total}  ({100 * d / total:.1f}%)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

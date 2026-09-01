#!/usr/bin/env python3
"""Phase D — wire keyed MessageBox.Show call sites to the localization service.

Replaces the literal message/caption arguments of every call site recorded in
tools/i18n/msg-spec.json with lookups through
LocalizationService.Instance.T("Msg.*"), so the existing 15-language
ResourceDictionary pipeline drives ALL code-behind dialogs.

Rewrites per site:
  • message  ->  LocalizationService.Instance.T("Msg.X")
                 (or string.Format(T("Msg.X"), arg1, arg2) when the source
                 used an interpolated string with format arguments)
  • caption  ->  LocalizationService.Instance.T("Msg.CapX")
                 (only when the caption was a plain string literal)

Safety:
  • processes call sites bottom-up so line numbers stay valid
  • skips sites already wired (idempotent)
  • preserves the original comma/newline layout around replaced args
  • adds `using ProGlassAutomation.Services.Localization;` when needed
  • dry-run by default; pass --apply to write files
  • verifies bracket balance per file before/after

Usage:
  python3 tools/i18n/msg-wire.py            # dry run
  python3 tools/i18n/msg-wire.py --apply    # rewrite files
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
SPEC = json.load(open(HERE / "msg-spec.json", encoding="utf-8"))
CALLS = SPEC["calls"]

# Msg.Cap* slugs that reuse an EXISTING dictionary key (defined by
# msg-gen.py).  Those Msg.Cap names are deliberately NOT added to the
# dictionaries, so the wiring must emit the real key (e.g. Action.Export)
# or T() would return the raw key text.
_ms = importlib.util.spec_from_file_location("msg_keys_mod", HERE / "msg-keys.py")
_mm = importlib.util.module_from_spec(_ms)
_ms.loader.exec_module(_mm)  # noqa: S102 - trusted repo tool
REUSE_CAPTIONS = dict(_mm.REUSE_CAPTIONS)


def caption_key(cap: str) -> str:
    return REUSE_CAPTIONS.get(cap, cap)

# Sites whose interpolated arguments carry C# format specifiers
# (e.g. {0:dd MMM yyyy}) that string.Format cannot express from the
# resource string alone.  The arg expressions below pre-format them
# culture-aware, mirroring the original display.
FORMAT_OVERRIDES = {
    ("ViewModels/TaxInvoice/TaxInvoiceViewModel.cs", 510): [
        "proformaId",
        "existingDraft.InvoiceNumber",
        'existingDraft.IssueDate.ToString("dd MMM yyyy", '
        "LocalizationService.Instance.CurrentCulture)",
        'existingDraft.GrandTotal.ToString("N2", '
        "LocalizationService.Instance.CurrentCulture)",
    ],
}

USING = "using ProGlassAutomation.Services.Localization;"


def split_args(args_text: str):
    """Split on top-level commas.  Returns list of (start, end) raw spans
    (excluding the commas), mirroring msg-extract.py's scanner but tracking
    offsets.  Handles \" escapes inside string literals."""
    parts = []
    depth = 0
    inquote = False
    cur_start = 0
    i = 0
    while i < len(args_text):
        c = args_text[i]
        if c == '"' and (i == 0 or args_text[i - 1] != "\\"):
            inquote = not inquote
        elif c == "(" and not inquote:
            depth += 1
        elif c == ")" and not inquote:
            depth -= 1
        elif c == "," and depth == 0 and not inquote:
            parts.append((cur_start, i))
            cur_start = i + 1
        i += 1
    parts.append((cur_start, len(args_text)))
    return parts


def trim(span, text):
    s, e = span
    while s < e and text[s] in " \t\r\n":
        s += 1
    while e > s and text[e - 1] in " \t\r\n":
        e -= 1
    return s, e


def pure_literal(expr: str) -> bool:
    return bool(re.fullmatch(r'\$?"((?:[^"\\]|\\.)*)"', expr, re.S))


def locate_call(src: str, line: int):
    """Return (match, close_paren_index) for the MessageBox.Show call whose
    opening paren sits on the given source line, or None."""
    for m in re.finditer(r"(?:MessageBox|App\.Dialogs)\.Show\s*\(", src):
        if src.count("\n", 0, m.start()) + 1 == line:
            depth = 0
            j = m.end() - 1
            while j < len(src):
                if src[j] == "(":
                    depth += 1
                elif src[j] == ")":
                    depth -= 1
                    if depth == 0:
                        return m, j
                j += 1
            return m, None
    return None


def balance(src: str) -> tuple:
    return (src.count("("), src.count(")"),
            src.count("{"), src.count("}"),
            src.count("["), src.count("]"))


def ensure_using(src: str) -> str:
    if USING in src:
        return src
    lines = src.split("\n")
    last_using = -1
    for i, ln in enumerate(lines):
        if re.match(r"^\s*using\s+[A-Za-z0-9_.]+\s*;", ln):
            last_using = i
    if last_using == -1:
        return src  # no using block; leave file untouched (unexpected)
    lines.insert(last_using + 1, USING)
    return "\n".join(lines)


def wire_file(fp: Path, sites: list) -> tuple[int, list[str]]:
    src = fp.read_text(encoding="utf-8")
    before = balance(src)
    done = 0
    notes = []
    apply_changes = _APPLY
    for line, msg_key, cap_key, args in sorted(sites, key=lambda s: -s[0]):
        hit = locate_call(src, line)
        if hit is None:
            notes.append(f"  ! {fp.name}:{line} call site not found")
            continue
        m, close = hit
        args_text = src[m.end():close]
        spans = split_args(args_text)
        if not spans:
            notes.append(f"  ! {fp.name}:{line} no arguments parsed")
            continue
        p0 = trim(spans[0], args_text)
        p0_text = args_text[p0[0]:p0[1]]
        # caption: only when it was a plain literal and we have a key
        new_p1 = None
        if cap_key and len(spans) > 1:
            p1 = trim(spans[1], args_text)
            cap_expr = args_text[p1[0]:p1[1]]
            if pure_literal(cap_expr):
                new_p1 = (f'LocalizationService.Instance.T("'
                          f'{caption_key(cap_key)}")')
        if p0_text.find("LocalizationService.Instance.T(") != -1:
            if new_p1 is None:
                continue  # already fully wired
            # message already wired; wire the caption only
            p1 = trim(spans[1], args_text)
            new_args = args_text[:p1[0]] + new_p1 + args_text[p1[1]:]
            src = src[:m.end()] + new_args + src[close:]
            done += 1
            continue
        if msg_key:
            t_call = f'LocalizationService.Instance.T("{msg_key}")'
            if len(args) == 0:
                new_p0 = t_call
            else:
                rel = fp.relative_to(ROOT).as_posix()
                if (rel, line) in FORMAT_OVERRIDES:
                    args = FORMAT_OVERRIDES[(rel, line)]
                new_p0 = f"string.Format({t_call}, " + ", ".join(args) + ")"
        else:
            new_p0 = p0_text  # dynamic message stays as-is
        if new_p1 is not None:
            p1 = trim(spans[1], args_text)
            new_args = (args_text[:p0[0]] + new_p0 + args_text[p0[1]:p1[0]]
                        + new_p1 + args_text[p1[1]:])
        elif msg_key:
            new_args = args_text[:p0[0]] + new_p0 + args_text[p0[1]:]
        else:
            continue  # nothing to change
        src = src[:m.end()] + new_args + src[close:]
        done += 1
    if done:
        src = ensure_using(src)
        after = balance(src)
        if after[0] != after[1] or after[2] != after[3] or after[4] != after[5]:
            notes.append(f"  ! {fp.name}: UNBALANCED BRACKETS AFTER "
                         f"{after}")
        if apply_changes:
            fp.write_text(src, encoding="utf-8")
    return done, notes


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    global _APPLY
    _APPLY = ap.parse_args().apply

    by_file: dict[str, list] = {}
    for c in CALLS:
        # c = [file, line, msgKey, capKey, args]
        if not c[2] and not c[3]:
            continue  # nothing keyed at this site
        by_file.setdefault(c[0], []).append((c[1], c[2], c[3], c[4]))

    total_msg = 0
    total_cap = 0
    for rel, sites in sorted(by_file.items()):
        fp = ROOT / rel
        if not fp.exists():
            print(f"  ! missing file: {rel}")
            continue
        n, notes = wire_file(fp, sites)
        total_msg += n
        if n or notes:
            print(f"  {'✓' if n else '·'} {rel}: {n} site(s) wired")
            for note in notes:
                print(note)
    keyed = sum(1 for c in CALLS if c[2])
    cap_only = sum(1 for c in CALLS if not c[2] and c[3])
    print(f"\nWired messages at {total_msg}/{keyed} keyed sites "
          f"(captions handled inside; {cap_only} dynamic-message sites "
          "also get captions).")
    return 0


if __name__ == "__main__":
    sys.exit(main())

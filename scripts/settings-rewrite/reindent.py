#!/usr/bin/env python3
"""Settings rewrite — re-indent the section files from the real tree.

WHY THIS EXISTS
───────────────
These twelve files have been through several scripted restyles. Each one moved
elements without moving the whitespace in front of them, and the result is a
tree whose indentation actively lies:

    <StackPanel>
        <Border
    Style="{StaticResource SettingsInnerTileStyle}">
            <StackPanel Orientation="Horizontal">

Reading that in the IDE, it is impossible to see what is inside what — and the
boxes that nesting hid were exactly what the redesign had to remove. Nothing
about the running UI depends on it, which is why no guard ever caught it.

WHAT IT DOES
────────────
Parses each file into an element table (tag, first/last line, parent) and
rewrites ONLY the leading whitespace of lines that start or close an element:

    element start / self-closing tag   indent = depth · 4
    attribute continuation line        indent = element indent + tag width + 2
    closing tag                        indent = its element's indent

Lines that begin with text rather than a tag are left untouched, so no string
literal, `<Run>` sequence or inline content can be reflowed by accident.

VERIFYING IT
────────────
The change is whitespace-only by construction, so the proof is mechanical:

    git diff -w --stat        # must be empty
    python3 scripts/xaml-guard.py
    python3 scripts/settings-rewrite/verify_bindings.py
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SECTIONS = REPO / "Views" / "Settings" / "Sections"
SHELL = REPO / "Views" / "Settings" / "SettingsView.xaml"

TAG = re.compile(r'<(/?)([A-Za-z][\w.:]*)((?:"[^"]*"|[^>"])*?)(/?)>')
COMMENT = re.compile(r"<!--.*?-->", re.S)
INDENT = 4


def blank_comments(text: str) -> str:
    return COMMENT.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)


def plan(text: str) -> dict[int, int]:
    """Line number (1-based) → leading spaces it should have."""
    lines = text.splitlines()
    blanked = blank_comments(text)
    want: dict[int, int] = {}
    stack: list[dict] = []
    for m in TAG.finditer(blanked):
        closing, tag, _attrs, selfclose = m.groups()
        line = text.count("\n", 0, m.start()) + 1
        start_col = m.start() - (text.rfind("\n", 0, m.start()) + 1)
        tag_end = bookkeeping = None
        del tag_end, bookkeeping
        if closing:
            el = stack.pop()
            want[line] = el["indent"]
            continue
        indent = len(stack) * INDENT
        want[line] = indent
        stack.append({"tag": tag, "line": line, "indent": indent,
                      "attr_col": start_col + 1 + len(tag) + 1})
        if selfclose:
            stack.pop()
            continue
        # Attribute continuation lines: everything between this line and the
        # line the tag closes on, excluding the line the next element starts on.
        end_line = text.count("\n", 0, m.end()) + 1
        for cont in range(line + 1, end_line):
            want.setdefault(cont, indent + len(tag) + 2)
    return want


def reindent(path: Path, dry_run: bool = False) -> int:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    want = plan(text)
    changed = 0
    for n, target in want.items():
        if n - 1 >= len(lines):
            continue
        line = lines[n - 1]
        if not line.strip() or not line.lstrip().startswith("<"):
            continue                     # text content: never touched
        fixed = " " * target + line.lstrip()
        if fixed != line:
            lines[n - 1] = fixed
            changed += 1
    if changed and not dry_run:
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return changed


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("files", nargs="*")
    args = ap.parse_args()

    files = ([Path(f) for f in args.files] if args.files
             else sorted(SECTIONS.glob("*.xaml")) + [SHELL])
    total = 0
    for path in files:
        if not path.is_absolute():
            path = REPO / path
        n = reindent(path, args.dry_run)
        total += n
        print(f"{'would re-indent' if args.dry_run else 're-indented'} {n:4d} line(s) in {path.name}")
    print(f"\n{total} line(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

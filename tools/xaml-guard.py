#!/usr/bin/env python3
"""
xaml-guard — fail a commit if WPF markup would not compile.

Catches the errors that repeatedly burn this repo:
  * Well-formedness (MC3000) — unclosed tags, missing '>', raw '<'
  * Style= attribute AND <Element.Style> on the same element (MC3024)
  * Content= attribute AND <Element.Content> on the same element
  * Undeclared xmlns prefixes
  * LetterSpacing / CharacterSpacing (not valid on WPF TextBlock)
  * Duplicate attributes on one opening tag (real ones, not Binding Mode=)

False-positive note: FontStyle / BasedOn Style= must NOT trip the Style=
detector. The attribute matcher requires a non-letter before "Style".

Exit 0 = clean, 1 = violations.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

# Attribute named exactly Style= (not FontStyle)
STYLE_ATTR = re.compile(
    r"<([A-Za-z][\w.]*)\b([^>]*?(?<![A-Za-z])Style\s*=\s*\"[^\"]+\"[^>]*)>",
    re.S,
)
CONTENT_ATTR = re.compile(
    r"<([A-Za-z][\w.]*)\b([^>]*?(?<![A-Za-z])Content\s*=\s*\"[^\"]+\"[^>]*)>",
    re.S,
)
OPEN_TAG = re.compile(
    r"<([A-Za-z][\w.]*)((?:\s+[\w.:]+\s*=\s*\"[^\"]*\")+)\s*/?>"
)
# </StackPanel  with no closing >
UNCLOSED_END = re.compile(r"</[A-Za-z][\w.]*(?:\s[^>]*)?$", re.M)
UNCLOSED_START = re.compile(r"<[A-Za-z][\w.:]*(?:\s[^>]*)?$", re.M)

INVALID_PROPS = (
    "LetterSpacing",
    "CharacterSpacing",
    "letter-spacing",
)

XMLNS_DECL = re.compile(r"xmlns:([A-Za-z_][\w]*)\s*=")


def xaml_files() -> list[Path]:
    skip = {".git", "bin", "obj"}
    return [
        p
        for p in ROOT.rglob("*.xaml")
        if not any(part in skip for part in p.parts)
    ]


def check_wellformed(path: Path, text: str, errors: list[str]) -> None:
    # Fast line-level: a tag that starts and never closes on that line
    # and is not continued (next non-empty line starts a new tag).
    lines = text.splitlines()
    for i, line in enumerate(lines):
        stripped = line.rstrip()
        if not stripped or stripped.lstrip().startswith("<!--"):
            continue
        # End tag missing '>'
        if UNCLOSED_END.search(stripped) and ">" not in stripped[stripped.rfind("</") :]:
            errors.append(
                f"{path.relative_to(ROOT)}:{i + 1}: "
                f"unclosed end tag (MC3000) — missing '>' : {stripped.strip()!r}"
            )
            continue
        # Start tag that ends mid-attribute and the next line does not
        # continue the tag (doesn't start with whitespace + attr or '>').
        if stripped.startswith("<") and ">" not in stripped and not stripped.startswith("</"):
            nxt = lines[i + 1] if i + 1 < len(lines) else ""
            if nxt and not nxt[:1].isspace() and not nxt.lstrip().startswith((">", "/", "<!--")):
                errors.append(
                    f"{path.relative_to(ROOT)}:{i + 1}: "
                    f"unclosed start tag (MC3000) — missing '>' : {stripped.strip()!r}"
                )

    try:
        ET.fromstring(text)
    except ET.ParseError as exc:
        errors.append(
            f"{path.relative_to(ROOT)}: XML is not well-formed (MC3000): {exc}"
        )


def check_style_plus_element(path: Path, text: str, errors: list[str]) -> None:
    for m in STYLE_ATTR.finditer(text):
        tag = m.group(1)
        window = text[m.end() : m.end() + 240]
        if re.match(rf"\s*<{re.escape(tag)}\.Style>", window):
            line = text[: m.start()].count("\n") + 1
            errors.append(
                f"{path.relative_to(ROOT)}:{line}: "
                f"<{tag} Style=...> plus <{tag}.Style> — property set twice (MC3024)"
            )


def check_content_plus_element(path: Path, text: str, errors: list[str]) -> None:
    for m in CONTENT_ATTR.finditer(text):
        tag = m.group(1)
        window = text[m.end() : m.end() + 240]
        if re.match(rf"\s*<{re.escape(tag)}\.Content>", window):
            line = text[: m.start()].count("\n") + 1
            errors.append(
                f"{path.relative_to(ROOT)}:{line}: "
                f"<{tag} Content=...> plus <{tag}.Content> — property set twice (MC3024)"
            )


def check_invalid_props(path: Path, text: str, errors: list[str]) -> None:
    for i, line in enumerate(text.splitlines(), 1):
        for prop in INVALID_PROPS:
            if prop in line and not line.lstrip().startswith("<!--"):
                errors.append(
                    f"{path.relative_to(ROOT)}:{i}: "
                    f"invalid WPF property '{prop}' (TextBlock has no letter-spacing)"
                )


def check_undeclared_prefixes(path: Path, text: str, errors: list[str]) -> None:
    declared = set(XMLNS_DECL.findall(text))
    declared.update({"x", "d", "mc", "po", "sys"})
    seen: set[str] = set()
    in_comment = False
    for i, line in enumerate(text.splitlines(), 1):
        if "<!--" in line:
            in_comment = "-->" not in line
            continue
        if in_comment:
            if "-->" in line:
                in_comment = False
            continue
        for m in re.finditer(r"</?([A-Za-z_][\w]*):", line):
            prefix = m.group(1)
            if prefix not in declared and prefix not in seen:
                seen.add(prefix)
                errors.append(
                    f"{path.relative_to(ROOT)}:{i}: "
                    f"undeclared xmlns prefix '{prefix}:' — WPF will fail to compile"
                )


def check_duplicate_attrs(path: Path, text: str, errors: list[str]) -> None:
    for m in OPEN_TAG.finditer(text):
        tag = m.group(1)
        attrs = re.findall(r"([\w.:]+)\s*=", m.group(2))
        seen: dict[str, int] = {}
        for a in attrs:
            if a in {
                "Converter",
                "Mode",
                "ElementName",
                "Path",
                "RelativeSource",
                "AncestorType",
                "Source",
                "FallbackValue",
                "StringFormat",
                "UpdateSourceTrigger",
            }:
                continue
            seen[a] = seen.get(a, 0) + 1
        dups = [a for a, c in seen.items() if c > 1]
        if dups:
            line = text[: m.start()].count("\n") + 1
            errors.append(
                f"{path.relative_to(ROOT)}:{line}: "
                f"<{tag}> duplicate attribute(s) {dups}"
            )


def main() -> int:
    errors: list[str] = []
    files = xaml_files()
    for p in files:
        text = p.read_text(errors="replace")
        check_wellformed(p, text, errors)
        check_style_plus_element(p, text, errors)
        check_content_plus_element(p, text, errors)
        check_invalid_props(p, text, errors)
        check_duplicate_attrs(p, text, errors)
        check_undeclared_prefixes(p, text, errors)

    if errors:
        print(f"xaml-guard: {len(errors)} violation(s) in {len(files)} files")
        for e in errors:
            print("  ", e)
        return 1

    print(f"xaml-guard: clean ({len(files)} XAML files)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

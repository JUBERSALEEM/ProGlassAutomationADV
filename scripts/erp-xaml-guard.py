#!/usr/bin/env python3
"""Static XAML safety checks for the shared ERP design-system migration.

Does not require MSBuild. Exit 0 if clean, 1 if findings.
"""
from __future__ import annotations

import collections
import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
XAML = [p for p in ROOT.rglob("*.xaml") if ".git" not in p.parts]


def main() -> int:
    errors: list[str] = []
    gds = ROOT / "Themes/Styles/GlobalDesignSystem.xaml"
    text = gds.read_text(encoding="utf-8")
    keys = re.findall(r'x:Key="([^"]+)"', text)
    dups = [k for k, n in collections.Counter(keys).items() if n > 1]
    if dups:
        errors.append(f"GDS duplicate keys: {dups}")
    if text.count("</ResourceDictionary>") != 1:
        errors.append("GDS must have exactly one ResourceDictionary close")

    for p in XAML:
        raw = p.read_text(encoding="utf-8")
        try:
            ET.fromstring(raw)
        except ET.ParseError as exc:
            errors.append(f"XML {p.relative_to(ROOT)}: {exc}")
            continue
        m = re.search(r"<UserControl\b([^>]*)>", raw, re.S)
        if m and m.group(1).count("Style=") > 1:
            errors.append(f"duplicate Style= on UserControl: {p.relative_to(ROOT)}")

    if errors:
        print("erp-xaml-guard FAIL")
        for e in errors[:50]:
            print(" ", e)
        print(f"({len(errors)} findings)")
        return 1
    print(f"erp-xaml-guard OK  files={len(XAML)}  gds_keys={len(keys)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

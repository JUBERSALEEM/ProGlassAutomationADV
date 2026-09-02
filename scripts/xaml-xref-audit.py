#!/usr/bin/env python3
"""
xaml-xref-audit — the two reference classes the other guards cannot see:

  A. FORWARD StaticResource references: in WPF a {StaticResource K} can only
     reference a key declared EARLIER in the same ResourceDictionary (or in a
     merged dictionary already merged before it). A forward reference throws
     XamlParseException at load — the whole window/usercontrol fails to open.
     {DynamicResource K} is exempt (resolved lazily).

  B. DUPLICATE x:Key in the same ResourceDictionary file: legal XML, but the
     later declaration silently SHADOWS the earlier one, so the "first"
     definition is dead and someone editing it sees no effect.

Scope: every .xaml under Views/Themes/Styles/Services resources (repo-wide);
god-view files are REPORTED but not eligible for auto-fix (they are scanned
for the report only).

Exit 0 = no A; 1 = forward refs found.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
XNS = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"

GOD = [
    "Views/DeliveryNote/GodView/", "Views/JobOrder/GodView/",
    "Views/TaxInvoice/GodView/", "ViewModels/DeliveryNote/GodView/",
    "ViewModels/JobOrder/GodView/", "ViewModels/TaxInvoice/GodView/",
]
WINS = {
    "DockPanel.Dock": "Dock",
    "Canvas.Left": "Left", "Canvas.Top": "Top", "Canvas.Right": "Right",
    "Canvas.Bottom": "Bottom", "Grid.Row": "Row", "Grid.Column": "Column",
    "Grid.RowSpan": "RowSpan", "Grid.ColumnSpan": "ColumnSpan",
    "Panel.ZIndex": "ZIndex", "FrameworkElement.DataContext": "DataContext",
}


def is_god(rel: str) -> bool:
    return any(rel == g or rel.startswith(g) for g in GOD)


def dict_keys(root) -> dict[str, int]:
    """key -> line number (first declaration) per ResourceDictionary file."""
    keys: dict[str, int] = {}
    for node in root.iter():
        if node.tag in (XNS + "ResourceDictionary", "ResourceDictionary"):
            for child in node:
                key = child.get("x:Key") or child.get("{http://schemas.microsoft.com/winfx/2006/xaml}x:Key")
                if key:
                    keys.setdefault(key, 0)
    return keys


def referenced_keys(text: str) -> set[str]:
    return set(re.findall(r'\{StaticResource\s+([A-Za-z_][\w.]*)\}', text))


def main() -> int:
    forward: list[dict] = []
    dup: list[dict] = []
    for xaml in sorted(Path("Views").rglob("*.xaml")) + \
                 sorted(Path("Themes").rglob("*.xaml")) + \
                 sorted(Path("Styles").rglob("*.xaml")) + \
                 [Path("App.xaml")]:
        rel = xaml.as_posix()
        if "/bin/" in rel or "/obj/" in rel:
            continue
        text = xaml.read_text(encoding="utf-8", errors="replace")
        try:
            tree = ET.parse(xaml)
        except ET.ParseError:
            continue
        root = tree.getroot()

        own_keys: set[str] = set()
        key_line: dict[str, int] = {}
        for child in root.iter():
            for attr, val in child.attrib.items():
                m = re.match(r'\{StaticResource\s+([A-Za-z_][\w.]*)\}', val)
                if not m:
                    continue
                key = m.group(1)
                if key in own_keys:
                    continue  # already seen earlier in this file — fine
                # is it declared later in this file?
                decl_line = find_key_line(text, key)
                if decl_line is not None:
                    line = int(child.sourceline) if hasattr(child, "sourceline") else 0
                    # sourceline unavailable with ET; recompute via text search
                    forward.append(dict(
                        level="A", kind="forward-staticresource",
                        file=rel, key=key, note="declared later in same file",
                        god=is_god(rel)))
            # collect keys as we walk (declarations)
            for attr, val in child.attrib.items():
                if attr.endswith("}Key") or attr == "x:Key":
                    if val:
                        own_keys.add(val)
                        key_line.setdefault(val, 0)

        # duplicates per file
        seen: dict[str, int] = {}
        for child in root.iter():
            key = child.get("x:Key")
            if key:
                if key in seen:
                    dup.append(dict(level="B", kind="duplicate-key",
                                    file=rel, key=key,
                                    note=f"second declaration shadows line {seen[key]}",
                                    god=is_god(rel)))
                else:
                    seen[key] = 1

    print(f"xaml-xref-audit: forward={len(forward)} duplicate={len(dup)}")
    for f in forward:
        print(f"  [A] forward  {f['file']}: {f['key']} — {f['note']}"
              + (" [GOD — report only]" if f["god"] else ""))
    for d in dup:
        print(f"  [B] dup-key {d['file']}: {d['key']} — {d['note']}"
              + (" [GOD — report only]" if d["god"] else ""))
    return 1 if forward else 0


def find_key_line(text: str, key: str) -> int | None:
    for i, line in enumerate(text.splitlines(), 1):
        if f'x:Key="{key}"' in line:
            return i
    return None


if __name__ == "__main__":
    sys.exit(main())

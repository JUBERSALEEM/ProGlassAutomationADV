#!/usr/bin/env python3
"""
i18n-audit.py — static audit of the system-wide language switch.

Checks, in order:
  P1  Every {DynamicResource KEY} used in Views/* resolves to a key in
      Themes/Languages/en-US.xaml (the always-merged fallback) and every
      shipped language dictionary carries the exact same key set.
  P2  Raw literal UI strings ("Text=", "Content=", "Header=", "Title=",
      "ToolTip=") that are still hard-coded in XAML — i.e. NOT yet bound
      to a language key. Glyphs (&#x…;), bindings, and formatting
      placeholders are ignored.
  P3  Locale keys referenced with {StaticResource} — those would NOT
      retranslate live; must be DynamicResource.
  P4  Frozen GodView files (never to be touched) + how many literals they
      still carry — REPORT ONLY, they are deliberately deferred.

Exit code 0 = no P1; 1 = P1 defects.
"""
from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LANG_DIR = ROOT / "Themes" / "Languages"
VIEWS = ROOT / "Views"

GOD_FROZEN = (
    "ViewModels/DeliveryNote/GodView/", "ViewModels/JobOrder/GodView/",
    "ViewModels/TaxInvoice/GodView/", "Views/DeliveryNote/GodView/",
    "Views/JobOrder/GodView/", "Views/TaxInvoice/GodView/",
    "ViewModels/Shared/GodViewKpiViewModel.cs",
    "ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs",
    "ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.ImportExport.cs",
    "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml",
    "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml.cs",
    "ViewModels/Settings/SettingsViewModel.cs",
    "ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs",
    "ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs",
    "ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.cs",
    "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs",
    "ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.cs",
    "Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml",
)

DYNRES = re.compile(r"\{\s*DynamicResource\s+([A-Za-z0-9._]+)\s*\}")
STATICRES = re.compile(r"\{\s*StaticResource\s+([A-Za-z0-9._]+)\s*\}")
LITERAL = re.compile(
    r'(?P<attr>Text|Content|Header|Title|ToolTip|Watermark|PlaceholderText)'
    r'="(?P<val>[^"{}]*)"')


def is_god(path: Path) -> bool:
    rel = path.relative_to(ROOT).as_posix()
    return any(rel.startswith(g) or rel == g for g in GOD_FROZEN)


def lang_keys() -> dict[str, set[str]]:
    keys: dict[str, set[str]] = {}
    for f in sorted(LANG_DIR.glob("*.xaml")):
        keys[f.stem] = set()
        tree = ET.parse(f)
        for el in tree.getroot():
            k = el.get("{http://schemas.microsoft.com/winfx/2006/xaml}Key")
            if k:
                keys[f.stem].add(k)
    return keys


def main() -> int:
    keys = lang_keys()
    if not keys:
        print("NO THEMES/Languages/*.xaml FOUND")
        return 1

    english = keys.get("en-US", set())
    unequal = [c for c, k in keys.items() if k != english]
    print(f"Languages: {len(keys)} dictionaries; en-US keys = {len(english)}")
    if unequal:
        print(f"P1 KEY-SET MISMATCH: {unequal}")

    # Locale keys live under these namespaces; everything else
    # (brushes, thicknesses, styles) is theme machinery, not localized.
    LOCALE_PREFIXES = (
        "Action.", "Shell.", "Module.", "Settings.", "Login.", "Splash.",
        "About.", "Currency.", "Phone.", "Common.", "Nav.", "ToolTip.",
    )
    def is_locale(k: str) -> bool:
        return k.startswith(LOCALE_PREFIXES)

    xamls = sorted(VIEWS.rglob("*.xaml"))
    dynrefs, staticrefs, literals, frozen_literals = [], [], [], []
    dyn_missing: list[tuple[str, str]] = []

    for f in xamls:
        rel = f.relative_to(ROOT).as_posix()
        try:
            text = f.read_text(encoding="utf-8")
            ET.parse(f)  # structural validity
        except Exception as e:
            print(f"P1 XML-INVALID {rel}: {e}")
            continue

        for m in DYNRES.finditer(text):
            dynrefs.append((rel, m.group(1)))
            if is_locale(m.group(1)) and m.group(1) not in english:
                dyn_missing.append((rel, m.group(1)))
        for m in STATICRES.finditer(text):
            staticrefs.append((rel, m.group(1)))

        file_literals = []
        for m in LITERAL.finditer(text):
            val = m.group("val").strip()
            if not val:
                continue
            if val.startswith("&#x"):
                continue
            file_literals.append((m.group("attr"), val))
        rel_snips = [f"({a}) {v[:60]}" for a, v in file_literals]
        if rel_snips:
            if is_god(f):
                frozen_literals.append((rel, len(file_literals)))
            else:
                literals.append((rel, len(file_literals), rel_snips))

    print(f"\nDynamicResource references in Views: {len(dynrefs)}")
    if dyn_missing:
        print(f"P1 UNDEFINED DYNAMIC RESOURCE KEYS: {len(dyn_missing)}")
        for rel, k in dyn_missing[:25]:
            print(f"   {rel}: {k}")
    else:
        print("P1 undefined keys: 0")

    locale_keys = set(english)
    static_hits = [(rel, k) for rel, k in staticrefs if k in locale_keys]
    if static_hits:
        print(f"P3 LOCALE KEYS VIA StaticResource (won't retranslate): {len(static_hits)}")
        for rel, k in static_hits[:20]:
            print(f"   {rel}: {k}")
    else:
        print("P3 StaticResource locale use: 0")

    total_lit = sum(c for _, c, _ in literals)
    used_locale = sum(1 for _, k in dynrefs if is_locale(k))
    print(f"\nLocale DynamicResource references in non-god Views: {used_locale}")
    print(f"P2 hard-coded UI literals (non-god Views): {len(literals)} files, {total_lit} strings")
    for rel, c, snips in sorted(literals, key=lambda t: -t[1])[:20]:
        print(f"   {rel}: {c} literals | {' ; '.join(snips[:3])}")

    frozen_total = sum(c for _, c in frozen_literals)
    print(f"\nP4 FROZEN GodView literals (deferred by design): "
          f"{len(frozen_literals)} files, {frozen_total} strings")
    for rel, c in sorted(frozen_literals, key=lambda t: -t[1])[:10]:
        print(f"   {rel}: {c}")

    ok = not dyn_missing and not unequal
    print(f"\nRESULT: {'PASS' if ok else 'P1 DEFECTS'}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

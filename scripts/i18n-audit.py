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

# Freeze lifted 2026-09-01: GodViews / DataGrid / DeliveryNoteList /
# SettingsViewModel may now be edited. Remaining unlocalized strings are
# reported, not exempted.
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

ALL_ATTR = re.compile(
    r'(?P<attr>Text|Content|Header|Title|ToolTip|Watermark|PlaceholderText)'
    r'="(?P<val>[^"]*)"')

# ── String classification (master-prompt §20) ─────────────────────────────
# Hard-coded attribute values that are NOT user-facing copy to translate:
# icons, separators, numbers, currency codes, units, all-caps acronyms,
# dimension tokens (W1/H2), and the product/brand name.  Everything else
# that survives this filter is genuine UI text that must use a dictionary
# key — so P2/P4 reports list only real translatable strings.
CURRENCY_CODES = {
    "AED", "USD", "EUR", "GBP", "INR", "SAR", "QAR", "KWD", "BHD", "OMR",
    "CNY", "JPY", "AUD", "CAD", "CHF", "SGD", "MYR", "PKR", "TRY", "ZAR",
    "KRW", "EGP", "RUB", "BRL", "BDT", "IDR", "PHP", "THB", "VND", "LKR",
    "NPR", "ILS", "HKD", "NZD", "MXN", "PLN", "SEK", "NOK", "DKK", "HUF",
    "CZK", "RON", "BGN", "HRK", "UAH", "KZT", "NGN", "KES", "GHS", "MAD",
}
UNITS = {"pcs", "pc", "m²", "m2", "m³", "m3", "kg", "g", "mm", "cm", "km",
         "ml", "l", "sqm", "sqft", "ft", "in", "yd", "hrs", "h", "min", "s",
         "AED", "W", "kW", "V", "A", "%", "bytes", "KB", "MB", "GB", "TB"}
BRAND_NAMES = {"ProGlass", "ProGlass Automation", "Microsoft Print to PDF",
               "Microsoft"}
ACRONYM = re.compile(r"^[A-Z][A-Z0-9]{0,4}$")      # TRN, PI, JO, VAT, W1, H2

# Date-format tokens ("dd MMM yyyy", "HH:mm") are culture patterns, not
# copy — exempt from the StringFormat phrase check.
DATE_TOKEN = re.compile(
    r"^(d{1,4}|M{1,5}|y{1,5}|H{1,2}|h{1,2}|m{1,2}|s{1,2}|t{1,2}|z{1,3}|K{1,2}"
    r"|F{1,7}|g{1,2}|:|\s|/|-|,|\.|'|·|\(|\))*$")


def text_outside_markup(val: str) -> str:
    """Characters of an attribute value that sit OUTSIDE balanced {..}
    markup-extension spans. Non-empty letters here mean WPF renders raw
    copy next to a binding (e.g. Text=\"Made in India · {Binding X}\")."""
    out = []
    depth = 0
    for ch in val:
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth = max(0, depth - 1)
        elif depth == 0:
            out.append(ch)
    return ''.join(out)


def extract_string_format(val: str) -> str:
    """The raw StringFormat= value from an attribute text (val starts at
    'StringFormat='). Quoted formats ('dd MMM yyyy') end at the closing
    quote; unquoted formats end at the first '}' that is followed by a
    binding-arg comma, the attribute end, or the extension close — so
    args like Mode=OneWay / TargetNullValue=— never leak into the scan."""
    i = val.find('StringFormat=')
    if i < 0:
        return ''
    v = val[i + len('StringFormat='):]
    if v.startswith("'"):
        end = v.find("'", 1)
        return v[1:end] if end > 0 else v[1:]
    j = 0
    while j < len(v):
        if v[j] == '}' and (j + 1 >= len(v) or v[j + 1] in ',>}"'):
            return v[:j]
        j += 1
    return v


def string_format_words(val: str) -> str:
    """Genuine words inside a StringFormat= value, or '' when the format
    is pure placeholders/units/dates."""
    fmt = extract_string_format(val)
    rest = re.sub(r'\{[^{}]*\}', '', fmt)   # drop {..} placeholders + {} escapes
    rest = rest.strip().strip("'").strip()
    rest = re.sub(r'^[\s·\-–—.,/()|]+|[\s·\-–—.,/()|]+$', '', rest)
    if not rest:
        return ''
    if rest in UNITS or rest in CURRENCY_CODES or rest in BRAND_NAMES:
        return ''
    if DATE_TOKEN.fullmatch(rest):
        return ''
    if ACRONYM.fullmatch(rest):
        return ''
    return rest


def is_translatable_literal(val: str) -> bool:
    """True when the literal is genuine UI copy that should be localized."""
    if not val:
        return False
    if val.startswith("&#x"):              # Segoe MDL2 / icon glyphs
        return False
    if val in CURRENCY_CODES or val in UNITS or val in BRAND_NAMES:
        return False
    if ACRONYM.fullmatch(val):
        return False
    if re.fullmatch(r"[\d\s:.,/+-]+", val):
        return False                      # step numbers 01–09, 00:00, counts
    if not any(c.isalpha() for c in val):
        return False                      # separators: · / → — ✓ ✕ ● ≡ ⌕ ↻ ⤴ ×
    # anything left contains letters in a word → translatable
    return True


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
        "God.", "Dg.", "Dnl.",
        # module-scoped keys introduced by tools/i18n/sweep.py
        "Reports.", "Inquiry.", "SheetStore.", "DeliveryNote.", "JobOrder.",
        "TaxInvoice.", "ProformaInvoice.", "Dashboard.", "SystemCenter.",
        "UserCenter.", "CompanyCenter.", "Licensing.", "CrashReports.",
        "Diagnostics.", "Optimization.", "Production.", "JobProfitability.",
        "Shared.", "AI.", "App.", "Message.", "Validation.", "Print.",
        "Statement.",
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
        # All attribute values (braces allowed): text outside markup
        # spans is what the user actually sees — pure literals AND mixed
        # literal+binding ("Made in India · {Binding X}") alike.
        for m in ALL_ATTR.finditer(text):
            val = m.group("val").strip()
            if not val:
                continue
            outside = text_outside_markup(val).strip()
            if outside and is_translatable_literal(outside):
                file_literals.append((m.group("attr"), outside))
        # Genuine words inside StringFormat= values are user-visible copy
        # glued to a number (e.g. "{0} pending approval").
        for m in re.finditer(r'StringFormat=(?P<val>[^"][^"]*)', text):
            words = string_format_words(m.group("val"))
            if words and is_translatable_literal(words):
                file_literals.append(("StringFormat", words))
        # Element TEXT content ("<TextBlock>• Light theme — ...</TextBlock>")
        # is user-visible copy that attribute scanning can never see.
        for m in re.finditer(
                r'<(?:TextBlock|Label|Button|TabItem|MenuItem|CheckBox|'
                r'RadioButton|GroupBox)[^>]*>(?P<val>[^<{][^<]*)</',
                text):
            val = m.group("val").strip()
            if val and is_translatable_literal(val):
                file_literals.append(("Text", val))
        rel_snips = [f"({a}) {v[:60]}" for a, v in file_literals]
        if rel_snips:
            if is_god(f):
                frozen_literals.append((rel, len(file_literals)))
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
    print(f"\nP4 EX-GOD-FREEZE files remaining literals: "
          f"{len(frozen_literals)} files, {frozen_total} strings (now editable)")
    for rel, c in sorted(frozen_literals, key=lambda t: -t[1])[:10]:
        print(f"   {rel}: {c}")

    ok = not dyn_missing and not unequal
    print(f"\nRESULT: {'PASS' if ok else 'P1 DEFECTS'}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

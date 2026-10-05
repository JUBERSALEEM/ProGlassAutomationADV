#!/usr/bin/env python3
"""tools/i18n/sweep.py — the P2 literal sweep.

Scans every Views/**/*.xaml for hard-coded user-facing strings
(Text=, Content=, Header=, Title=, ToolTip=, Watermark=,
PlaceholderText=) and rewrites them to {DynamicResource Key} where the
value is genuinely translatable UI text.

Classification (see LOCALIZATION_2026-09-01.md §5.2):
  SKIP (left as literal):
    • glyph entities            (&#xE8D4; icons)
    • pure data                 (numbers, percents, decimals, dates)
    • units & standard symbols  (m², mm, kg, %, ·, —, •, ✓, …)
    • business abbreviations    (AED, VAT, TRN, PI, JO, DN, TI, PDF,
                                 CSV, XLSX, SQM, REF, NO., PCS …)
    • brand / URLs / emails     (ProGlass Automation, www.…)
  TRANSLATE → {DynamicResource Key} with a stable module-scoped key:
      Views/Reports/PiReport/PiReportView.xaml  →  Reports.PiReport.<Slug>
    identical values anywhere in the app share ONE key (global registry).

Output:
  • rewrites the .xaml files in place
  • writes tools/i18n/sweep-keys.json  { key: enValue }        (new keys)
  • writes tools/i18n/sweep-report.txt  per-file summary

Run:  python3 tools/i18n/sweep.py
"""
from __future__ import annotations

import html
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VIEWS = ROOT / "Views"
EN_PY = ROOT / "tools" / "i18n" / "en-US.py"
OUT_KEYS = ROOT / "tools" / "i18n" / "sweep-keys.json"
OUT_REPORT = ROOT / "tools" / "i18n" / "sweep-report.txt"

# ── attribute set handled by the audit (keep in sync with i18n-audit.py)
ATTRS = ("Text", "Content", "Header", "Title", "ToolTip", "Watermark",
         "PlaceholderText")
LITERAL = re.compile(
    rf'(?P<attr>{"|".join(ATTRS)})="(?P<val>[^"{{}}]*)"')
COMMENT = re.compile(r"<!--.*?-->", re.S)

# ─────────────────────────────────────────────────────────────────────
# Classification tables
# ─────────────────────────────────────────────────────────────────────
UNITS = {
    "pcs", "PCS", "Pcs", "mm", "MM", "mm²", "mm³", "m²", "m³", "m2", "sqm",
    "SQM", "cm", "kg", "KM", "km", "m", "ft", "in", "W", "kWh", "°", "%",
    "sq.m", "sqm.", "/m²", "per m²", "each", "EA", "ea", "nos", "NOS",
    "QTY", "qty", "Qty", "No.", "No", "NO.", "Nos.", "pcs.",
}
ABBREV = {
    "AED", "USD", "EUR", "GBP", "INR", "SAR", "QAR", "KWD", "BHD", "OMR",
    "CNY", "JPY", "AUD", "CAD", "CHF", "SGD", "MYR", "PKR", "TRY", "ZAR",
    "VAT", "TRN", "PI", "JO", "DN", "TI", "PO", "PDF", "CSV", "XLSX",
    "REF", "ID", "SKU", "EIN", "TIN", "GST", "HSN", "SAC", "TEL", "FAX",
    "EXT", "ATTN", "CC", "BCC", "RE", "FW", "AMT", "DISC", "TAX",
    "NET", "GROSS", "SUB", "TOTAL", "BAL", "PAID", "DUE",
    "THK", "W1", "W2", "H1", "H2", "WL", "HL",
    "MDL2", "XML", "JSON", "SQL", "DB", "API", "URL", "PC", "OS", "IP",
}
DECOR = {
    "—", "–", "-", "·", "•", "●", "○", "✓", "✕", "×", "√", "►", "»", "«",
    "›", "‹", "★", "☆", "◆", "◇", "→", "←", "↑", "↓", "↔", "|", "/", ":",
    ",", ".", "…", "...", ">>", "<<", "::", "===", "***", "~", "=", "+",
    "(", ")", "[", "]", "{", "}", "1", "2", "3", "0", "5", "05", "10",
    "25", "50", "75", "100", "1/2", "1/4", "3/4", "1.5", "0.5", "2.5",
    "↻", "#",
}
BRAND = {
    "ProGlass Automation", "proglass", "ProGlass", "PROGLASS",
    "www.proglass-automation.com", "www.proglassautomation.com",
    "Premium Glass Glazing, High-Yield Manufacturing & Logistics",
    "Premium Glass Glazing, High-Yield Manufacturing &amp; Logistics",
    "Made in India for the UAE glass industry",
    "PROGLASS AUTOMATION",
    "ProGlassAutomation",
}
NUMISH = re.compile(
    r"^[\d\s.,%+()/\-−–—·•×*:'\"°\u00a0]+$")
PHONEISH = re.compile(
    r"^\+?[\d\s()\-]{7,}$")
URLISH = re.compile(
    r"^(https?://|www\.|mailto:|[\w.+-]+@[\w-]+\.[\w.]+)", re.I)
TIMEISH = re.compile(r"^\d{1,2}:\d{2}(:\d{2})?(\s?(AM|PM))?$", re.I)
DATEISH = re.compile(
    r"^\d{1,4}[/\-.]\d{1,2}[/\-.]\d{1,4}(\s\d{1,2}:\d{2})?$")

# Values that are standardized even in translated UIs (keep as literal).
STANDARD = {
    "English", "العربية", "中文", "हिन्दी", "Español", "Français",
    "Deutsch", "Русский", "Português", "Türkçe", "اردو", "বাংলা",
    "Bahasa Indonesia", "日本語", "한국어", "Italiano", "தமிழ்",
    "తెలుగు", "മലയാളം", "ಕನ್ನಡ",
    "AED (UAE Dirham)", "AED/SQM",
}

# ── module prefix per directory (file path → key namespace)
PREFIX_MAP = [
    ("Views/Reports/", "Reports"),
    ("Views/Inquiry/", "Inquiry"),
    ("Views/Settings/", "Settings"),
    ("Views/SheetStore/", "SheetStore"),
    ("Views/DeliveryNote/", "DeliveryNote"),
    ("Views/JobOrder/", "JobOrder"),
    ("Views/TaxInvoice/", "TaxInvoice"),
    ("Views/ProformaInvoice/", "ProformaInvoice"),
    ("Views/Dashboard/", "Dashboard"),
    ("Views/SystemCenter/", "SystemCenter"),
    ("Views/UserCenter/", "UserCenter"),
    ("Views/CompanyCenter/", "CompanyCenter"),
    ("Views/About/", "About"),
    ("Views/Login/", "Login"),
    ("Views/Splash/", "Splash"),
    ("Views/Licensing/", "Licensing"),
    ("Views/CrashReports/", "CrashReports"),
    ("Views/Diagnostics/", "Diagnostics"),
    ("Views/Optimization/", "Optimization"),
    ("Views/Production/", "Production"),
    ("Views/JobProfitability/", "JobProfitability"),
    ("Views/Shared/", "Shared"),
    ("Views/AI/", "AI"),
]
# sub-prefix for report sub-folders (Views/Reports/PiReport → Reports.PiReport)
REPORT_SUB = {
    "DeliveryReport": "DeliveryReport", "PiReport": "PiReport",
    "VatSubmission": "VatSubmission", "JoReport": "JoReport",
    "TaxInvoiceReport": "TaxInvoiceReport", "StatementReport": "StatementReport",
    "StatementGenerator": "StatementGenerator", "SalesReport": "SalesReport",
}


def module_prefix(rel: str) -> str:
    for folder, prefix in PREFIX_MAP:
        if rel.startswith(folder):
            rest = rel[len(folder):]
            first = rest.split("/")[0]
            sub = REPORT_SUB.get(first)
            if sub:
                return f"{prefix}.{sub}"
            return prefix
    return "App"


STOPWORDS = {
    "a", "an", "the", "of", "for", "on", "in", "to", "with", "by", "from",
    "per", "and", "or", "not", "your", "its", "it", "this", "that",
    "these", "those", "all", "any", "at", "as", "is", "are", "be", "been",
    "was", "were", "over", "under", "into", "out", "up", "down", "off",
    "via", "across", "through", "between", "among", "each", "every",
    "both", "either", "neither", "such", "only", "own", "same", "so",
    "than", "too", "very", "can", "will", "just", "but", "does", "do",
    "has", "have", "had", "you", "we", "they", "them", "his", "her",
    "their", "our", "my", "me", "us", "am", "are", "been", "being",
}


def slugify(value: str) -> str:
    """English value → CamelCase key slug (no stopwords, ≤ 44 chars)."""
    s = html.unescape(value).strip()
    words = [w for w in re.split(r"[^A-Za-z0-9]+", s) if w]
    keep = [w for w in words if w.lower() not in STOPWORDS]
    if not keep:
        keep = words
    parts = []
    for w in keep:
        # keep short all-caps acronyms (VAT, AED, SQM …) as-is
        if w.isupper() and len(w) <= 4:
            parts.append(w)
        else:
            parts.append(w[:1].upper() + w[1:].lower())
    out = "".join(parts)
    if len(out) > 44:
        while len(out) > 44 and len(parts) > 1:
            parts.pop()
            out = "".join(parts)
    return out[:48] or "Label"


def load_english_keys() -> tuple[dict, dict]:
    """(key→value, normalizedValue→key) from the current en-US.py."""
    spec = None
    import importlib.util
    spec = importlib.util.spec_from_file_location(
        "i18n_en_current", EN_PY)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    kv = dict(mod.STRINGS)
    rev: dict[str, str] = {}
    for k, v in kv.items():
        rev.setdefault(norm(v), k)
    return kv, rev


def norm(v: str) -> str:
    d = html.unescape(v).strip()
    d = re.sub(r"[\s\u00a0]+", " ", d)
    d = re.sub(r"[.…:]+$", "", d)
    return d.casefold()


def is_skip(value: str) -> bool:
    v = value.strip()
    if not v or v.startswith("&#x"):
        return True
    if v in DECOR or v in UNITS or v in ABBREV or v in BRAND or v in STANDARD:
        return True
    if NUMISH.fullmatch(v):
        return True
    if PHONEISH.fullmatch(v) or URLISH.match(v):
        return True
    if TIMEISH.fullmatch(v) or DATEISH.fullmatch(v):
        return True
    # pure symbols (icons, separators: ⌕ ≡ ⏳ ✓ →) carry no language text
    if not any(ch.isalnum() for ch in v):
        return True
    return False


def main() -> int:
    existing_kv, rev = load_english_keys()
    registry: dict[str, str] = {}          # normValue → key
    key_values: dict[str, str] = {}        # key → en value
    used_slugs: dict[str, int] = {}        # "prefix.slug" → count
    report: list[str] = []
    total_replaced = 0

    files = sorted(VIEWS.rglob("*.xaml"))
    for f in files:
        rel = f.relative_to(ROOT).as_posix()
        original = f.read_text(encoding="utf-8")
        masked = COMMENT.sub(lambda m: " " * len(m.group(0)), original)
        prefix = module_prefix(rel)
        replaced = 0
        new_text = original

        # process matches right-to-left so spans stay valid
        matches = list(LITERAL.finditer(masked))
        for m in reversed(matches):
            attr = m.group("attr")
            value = m.group("val").strip()
            if is_skip(value):
                continue
            n = norm(value)
            if n in rev and not rev[n].startswith(("God.", "Dg.", "Dnl.")):
                key = rev[n]
            elif n in registry:
                key = registry[n]
            else:
                slug = slugify(value)
                cand = f"{prefix}.{slug}"
                while cand in key_values and key_values[cand] != value:
                    used_slugs[cand] = used_slugs.get(cand, 1) + 1
                    cand = f"{prefix}.{slug}{used_slugs[cand]}"
                key = cand
                registry[n] = key
                key_values[key] = html.unescape(value)
            start, end = m.span()
            new_text = (new_text[:start]
                        + f'{attr}="{{DynamicResource {key}}}"'
                        + new_text[end:])
            replaced += 1
        if replaced:
            f.write_text(new_text, encoding="utf-8")
            total_replaced += replaced
            report.append(f"{rel}: {replaced} literals → keys")

    OUT_KEYS.write_text(
        json.dumps(key_values, ensure_ascii=False, indent=2, sort_keys=True)
        + "\n", encoding="utf-8")
    OUT_REPORT.write_text("\n".join(report) + "\n", encoding="utf-8")
    print(f"Files touched: {len(report)}   literals replaced: {total_replaced}")
    print(f"New keys: {len(key_values)}   → {OUT_KEYS.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

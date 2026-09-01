#!/usr/bin/env python3
"""Split a language's untranslated diff into authoring chunks (key names only).

Usage: python3 chunk-split.py <code>
Writes /tmp/i18n-chunks/{code}-{chunk}.keys.txt  (one key per line, tab + English value)
Chunks: settings (About/Login/Settings/Splash), pi (CompanyCenter/Inquiry/ProformaInvoice/Shared/SheetStore),
        reports (Reports), ops (AI/CrashReports/Dashboard/DeliveryNote/Diagnostics/JobOrder/JobProfitability/SystemCenter/TaxInvoice/UserCenter),
        misc (everything else).
"""
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
code = sys.argv[1]

def load(code: str) -> dict:
    src = (HERE / f"{code}.py").read_text(encoding="utf-8")
    pat = re.compile(r'^    "((?:[^"\\]|\\.)*)": "((?:[^"\\]|\\.)*)",?$', re.M)
    return dict((m.group(1), m.group(2)) for m in pat.finditer(src))

en = load("en-US")
lang = load(code)
untrans = {k: en[k] for k in en if lang.get(k) == en.get(k)}

def chunk_of(k: str) -> str:
    if k.startswith(("About.", "Login.", "Settings.", "Splash.")):
        return "settings"
    if k.startswith(("CompanyCenter.", "Inquiry.", "ProformaInvoice.", "Shared.", "SheetStore.")):
        return "pi"
    if k.startswith("Reports."):
        return "reports"
    if k.startswith(("AI.", "CrashReports.", "Dashboard.", "DeliveryNote.", "Diagnostics.",
                     "JobOrder.", "JobProfitability.", "SystemCenter.", "TaxInvoice.", "UserCenter.")):
        return "ops"
    return "misc"

groups: dict[str, list[str]] = {}
for k in sorted(untrans):
    groups.setdefault(chunk_of(k), []).append(k)

outdir = Path("/tmp/i18n-chunks")
outdir.mkdir(exist_ok=True)
for g, keys in sorted(groups.items()):
    p = outdir / f"{code}-{g}.keys.txt"
    p.write_text("\n".join(f"{k}\t{untrans[k]}" for k in keys) + "\n", encoding="utf-8")
    print(f"{g}: {len(keys)} keys -> {p.name}")
print("TOTAL untranslated:", len(untrans))

#!/usr/bin/env python3
"""
Static verification for the ProGlassAutomation dashboard grammar work.

There is no .NET SDK in this sandbox, so nothing here can compile. These
checks are what stand in for a build, and they are the checks that caught
real regressions on the previous change:

  1. XAML well-formedness for every .xaml in the repo.
  2. Every {StaticResource K} / {DynamicResource K} used by a dashboard
     view resolves to a key defined somewhere in the app.
  3. Every {Binding Path} in a dashboard view resolves to a public
     property on the ViewModel the page is bound to.
  4. Brace / paren / bracket balance for every C# file touched.
"""
import os, re, sys, glob
import xml.dom.minidom as minidom

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# ── 1. XAML well-formedness ───────────────────────────────────────────
def check_xaml():
    bad = []
    files = glob.glob(os.path.join(ROOT, "**", "*.xaml"), recursive=True)
    for f in files:
        try:
            minidom.parse(f)
        except Exception as e:
            bad.append((f, str(e)))
    return files, bad

# ── 2. resource key universe ──────────────────────────────────────────
def key_universe():
    keys = set()
    for f in glob.glob(os.path.join(ROOT, "**", "*.xaml"), recursive=True):
        try:
            s = open(f, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        keys |= set(re.findall(r'x:Key="([^"]+)"', s))
    return keys

# ── 3. VM public property surface ─────────────────────────────────────
def vm_props(path):
    """Public property + field names declared in a VM file."""
    if not os.path.exists(path):
        return set()
    s = open(path, encoding="utf-8", errors="replace").read()
    # this codebase puts the opening brace on its own line, so collapse
    # whitespace before matching or multi-line declarations are missed
    s = re.sub(r"\s+", " ", s)
    props = set(re.findall(r'public\s+(?:static\s+|virtual\s+|override\s+|new\s+|sealed\s+)*'
                           r'[A-Za-z0-9_<>,\?\.\[\] ]+?\s+([A-Z][A-Za-z0-9_]*)\s*(?:\{\s*(?:get|private|protected|set)|=>)', s))
    props |= set(re.findall(r'public\s+(?:static\s+)?[A-Za-z0-9_<>,\?\.\[\] ]+?\s+([A-Z][A-Za-z0-9_]*)\s*;', s))
    return props

BASE_PROPS = {"IsLoading", "HasError", "ErrorMessage", "LastRefreshed",
              "TabTitle", "TabIcon", "TabSubtitle"}

# view -> (xaml, vm file)
PAGES = {
    "Overview":     ("Views/Dashboard/Overview/OverviewDashboardView.xaml",
                     "ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs"),
    "SheetStore":   ("Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml",
                     "ViewModels/Dashboard/SheetStore/SheetStoreDashboardViewModel.cs"),
    "Proforma":     ("Views/Dashboard/ProformaInvoice/PiDashboardView.xaml",
                     "ViewModels/Dashboard/ProformaInvoice/PiDashboardViewModel.cs"),
    "JobOrder":     ("Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml",
                     "ViewModels/Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs"),
    "DeliveryNote": ("Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml",
                     "ViewModels/Dashboard/DeliveryNote/DnDashboard/DnDashboardViewModel.cs"),
    "TaxInvoice":   ("Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml",
                     "ViewModels/Dashboard/TaxInvoice/TiDashboard/TiDashboardViewModel.cs"),
    "Statement":    ("Views/Dashboard/Statement/StDashboardView.xaml",
                     "ViewModels/Dashboard/Statement/StDashboardViewModel.cs"),
}

# properties that legitimately appear on nested row models, not the page VM
NESTED_OK = {
    "Title", "SubTitle", "ValueDisplay", "StatusLabel", "StatusColor", "TintColor",
    "Icon", "DisplayValue", "Label", "SubLabel", "BadgeColor", "Accent", "IsAlert",
    "Name", "Value", "Count", "CustomerName", "Status", "ProformaId", "JobOrderId",
    "DeliveryNoteId", "StatementId", "AmountDisplay", "BalanceDisplay", "DueDisplay",
    "TotalDisplay", "DaysDisplay", "Bracket", "InvoiceCount", "InvoiceId", "DateDisplay",
    "TotalAmountDisplay", "OutstandingDisplay", "PaidDisplay", "Percentage",
    "ValueDisplayShort", "MonthLabel", "RevenueDisplay", "Rank", "Glyph", "Foot",
    "AccentKey", "IsActive", "ValueDisplayLong", "SparklineValues",
    "Index", "StatementNumber", "HasAttachments", "HasPdf", "CreatedAtDisplay",
    "StatementId", "CustomerDisplay", "PeriodDisplay", "GrandTotalDisplay",
    # verified present on Models/Dashboard/TaxInvoiceDashboard/* row models
    "InvoiceNumber", "IssueDateDisplay", "CountDisplay", "SeverityColor",
    "NormalizedWidth", "BarColor", "InvoiceCountDisplay", "TaxInvoiceId",
    "DueDate", "GrandTotal", "StatusIcon", "IsOverdue", "DaysFrom", "DaysTo",
    "NormalizedHeight", "IsHighlight", "IsCurrentMonth", "MedalIcon",
    "TotalInvoicedDisplay", "OutstandingDisplay", "OverdueDisplay", "Title", "SubTitle",
}

def balance(path):
    s = open(path, encoding="utf-8", errors="replace").read()
    out, i, n = [], 0, len(s)
    depth = {"{": 0, "(": 0, "[": 0}
    pairs = {"}": "{", ")": "(", "]": "["}
    while i < n:
        c = s[i]
        if c == '/' and i + 1 < n and s[i+1] == '/':
            j = s.find("\n", i); i = n if j < 0 else j; continue
        if c == '/' and i + 1 < n and s[i+1] == '*':
            j = s.find("*/", i + 2); i = n if j < 0 else j + 2; continue
        if c == '"':
            if s.startswith('"""', i):
                j = s.find('"""', i + 3); i = n if j < 0 else j + 3; continue
            if s.startswith('@"', i - 1):
                i += 1
                while i < n:
                    if s[i] == '"':
                        if i + 1 < n and s[i+1] == '"': i += 2; continue
                        break
                    i += 1
                i += 1; continue
            i += 1
            while i < n and s[i] != '"':
                if s[i] == '\\': i += 1
                i += 1
            i += 1; continue
        if c == "'":
            i += 1
            while i < n and s[i] != "'":
                if s[i] == '\\': i += 1
                i += 1
            i += 1; continue
        if c in depth: depth[c] += 1
        elif c in pairs: depth[pairs[c]] -= 1
        i += 1
    return depth

def style_targets():
    """key -> TargetType for every <Style x:Key=...> in the app."""
    out = {}
    for f in glob.glob(os.path.join(ROOT, "**", "*.xaml"), recursive=True):
        try:
            doc = minidom.parse(f)
        except Exception:
            continue
        for st in doc.getElementsByTagName("Style"):
            k = st.getAttribute("x:Key")
            t = st.getAttribute("TargetType")
            if k and t:
                out.setdefault(k, t.split(":")[-1])
    return out

def check_style_targets(targets, pages):
    """An element using Style={StaticResource K} must be a K's TargetType."""
    bad = []
    for name, (xaml, _) in pages.items():
        try:
            doc = minidom.parse(os.path.join(ROOT, xaml))
        except Exception:
            continue
        def walk(node):
            if node.nodeType != node.ELEMENT_NODE: return
            st = node.getAttribute("Style")
            m = re.match(r"\{StaticResource ([A-Za-z0-9\.]+)\}", st or "")
            if m:
                want = targets.get(m.group(1))
                got = node.tagName.split(":")[-1]
                if want and want != got:
                    bad.append((name, got, m.group(1), want))
            for c in node.childNodes: walk(c)
        walk(doc.documentElement)
    return bad

def main():
    rc = 0
    files, bad = check_xaml()
    print(f"[1] XAML well-formed: {len(files)-len(bad)}/{len(files)}")
    for f, e in bad:
        print(f"    FAIL {os.path.relpath(f, ROOT)}: {e}"); rc = 1

    keys = key_universe()
    print(f"[2] resource key universe: {len(keys)} keys")

    miss_static, miss_dyn, miss_bind = [], [], []
    for name, (xaml, vm) in PAGES.items():
        p = os.path.join(ROOT, xaml)
        s = open(p, encoding="utf-8", errors="replace").read()
        for k in re.findall(r'\{StaticResource ([A-Za-z0-9\.]+)\}', s):
            if k not in keys: miss_static.append((name, k))
        for k in re.findall(r'\{DynamicResource ([A-Za-z0-9\.]+)\}', s):
            if k not in keys: miss_dyn.append((name, k))
        surface = vm_props(os.path.join(ROOT, vm)) | BASE_PROPS | NESTED_OK
        for b in re.findall(r'\{Binding (?:Path=)?([A-Za-z][A-Za-z0-9_\.]*)', s):
            parts = [p for p in b.split(".") if p != "DataContext"]
            if not parts: continue
            if parts[0] not in surface: miss_bind.append((name, b))

    print(f"[3] StaticResource refs unresolved: {len(miss_static)}")
    for n, k in miss_static: print(f"    {n}: {k}")
    print(f"[4] DynamicResource refs unresolved: {len(miss_dyn)}")
    for n, k in miss_dyn: print(f"    {n}: {k}")
    print(f"[5] Binding heads not on VM surface: {len(miss_bind)}")
    for n, b in miss_bind: print(f"    {n}: {b}")
    if miss_static: rc = 1

    # style-kit usage discipline
    print("[6] grammar discipline (dashboard pages):")
    for name, (xaml, _) in PAGES.items():
        s = open(os.path.join(ROOT, xaml), encoding="utf-8", errors="replace").read()
        legacy = sorted(set(re.findall(r'\{StaticResource ((?:Dash|Soft|IndustrialDashboard|Kpi)[A-Za-z]*)\}', s)))
        hexes = sorted(set(re.findall(r'#[0-9A-Fa-f]{6,8}', s)))
        fs = sorted(set(re.findall(r'FontSize="(\d+)"', s)))
        flag = "  <-- REVIEW" if (legacy or hexes or fs) else ""
        print(f"    {name:<13} legacy={len(legacy):<2} hex={len(hexes):<2} literalFontSize={len(fs)}{flag}")
        if legacy: print(f"        legacy keys: {', '.join(legacy)}")

    targets = style_targets()
    st_bad = check_style_targets(targets, PAGES)
    print(f"[8] Style= used on wrong element type: {len(st_bad)}")
    for n, got, key, want in st_bad:
        print(f"    {n}: <{got}> uses {key} which targets <{want}>")
    if st_bad: rc = 1

    # KPI bands must use the canonical host + panel, or the tiles lose
    # their gutter and the bands stop lining up between tabs.
    kpi_bad = []
    for name, (xaml, _) in PAGES.items():
        txt = open(os.path.join(ROOT, xaml), encoding="utf-8", errors="replace").read()
        for mt in re.finditer(r'<ItemsControl\b[^>]*?>', txt, re.S):
            tag = mt.group(0)
            if 'DbKpiTileTemplate' not in tag: continue
            if 'DbKpiBandStyle' not in tag or 'DbKpiBandPanel' not in tag:
                kpi_bad.append((name, txt[:mt.start()].count("\n") + 1))
    print(f"[9] KPI bands not on canonical host+panel: {len(kpi_bad)}")
    for n, ln in kpi_bad: print(f"    {n} line {ln}")
    if kpi_bad: rc = 1

    touched = sys.argv[1:]
    if touched:
        print(f"[7] brace balance ({len(touched)} files):")
        for t in touched:
            d = balance(os.path.join(ROOT, t))
            ok = all(v == 0 for v in d.values())
            print(f"    {'ok  ' if ok else 'FAIL'} {t} {d if not ok else ''}")
            if not ok: rc = 1
    return rc

if __name__ == "__main__":
    sys.exit(main())

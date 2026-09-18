#!/usr/bin/env python3
"""Pattern census for the MASTER UI MODERNIZATION (Phase 1 gap analysis).

For every XAML file, measures:
  - module (top-level dir)
  - DynamicResource key families consumed (top prefix)
  - local style/template definitions (duplication pressure)
  - hardcoded FontSize / CornerRadius / numeric Margin-Padding literals
  - uses of the shared chrome dictionaries (ListPage/DataGrid/Dialog/Input/
    Button/Pill/Card/Section styles)
  - presence of the 14 standard patterns (page surface, header, toolbar,
    section card, KPI, form field, table, dialog, empty, loading, error,
    action buttons, navigation)

Output: docs/ui-audit/PATTERN_CENSUS.json + per-module summary on stdout.
Deterministic; rerun after each phase to show convergence.
"""
import re, os, json
from collections import defaultdict

ROOT = os.getcwd()
FAM = re.compile(r"\{DynamicResource\s+([A-Za-z0-9]+)\.")  # prefix before first dot
BARE = re.compile(r"\{DynamicResource\s+([A-Za-z0-9]+)\b(?!\.|\s*[,}])")

SHARED_MARKERS = {
    "ListPage": r"ListPage[A-Za-z]*",
    "DataGrid": r"DataGrid[A-Za-z]*|LineItem[A-Za-z]*",
    "Dialog": r"Dialog[A-Za-z]*Style|DialogCard|DialogFooter|DialogTitle",
    "Input": r"Input[A-Za-z]*Style|ThemedInput",
    "Button": r"Button[A-Za-z]*Style|PrimaryButton|SecondaryButton|DangerButton",
    "Pill": r"Pill[A-Za-z]*|StatusChip",
    "Card": r"Card[A-Za-z]*Style|SurfaceCard",
    "Section": r"Section[A-Za-z]*Style|SectionHeader",
}
PAT_MARKERS = {
    "page_surface": r'Background="\{DynamicResource (Page|Surface|ProGlassSurface)[A-Za-z.]*"\}',
    "header": r"(TextTitle|PgFontCardTitle|PgFontHero|PageHeader|ModuleHeader)",
    "toolbar": r"(ToolBar|Toolbar|SearchBox|SearchTextBox)",
    "section_card": r"(CornerRadius.*\{DynamicResource (Card|PgCard|Radius)[A-Za-z.]*\})",
    "kpi": r"(KpiTile|StatCard|Kpi[A-Za-z]*Style)",
    "form_field": r"(Label.*\{DynamicResource Text[A-Za-z.]*\}|InputStyle|TextBox.*Style)",
    "table": r"(DataGrid|ListView.*GridView|ItemsControl.*Row)",
    "dialog": r"Dialog",
    "empty": r"(EmptyState|NoData|IsEmpty)",
    "loading": r"(Loading|IsBusy|ProgressRing|LoadingOverlay)",
    "error": r"(Error|Validation|IsError|Danger)",
    "action_buttons": r"(Button Command=|PrimaryButton|ActionButton)",
    "navigation": r"(NavigationView|NavRail|TabItem|ModuleTab)",
}

def families(t):
    cf, cb = defaultdict(int), defaultdict(int)
    for m in FAM.finditer(t):
        cf[m.group(1)] += 1
    for m in BARE.finditer(t):
        cb[m.group(1)] += 1
    return cf, cb

def main():
    files = []
    for dp, dn, fn in os.walk(ROOT):
        dn[:] = [d for d in dn if d not in (".git", "node_modules")]
        for f in sorted(fn):
            if f.endswith(".xaml"):
                files.append(os.path.relpath(os.path.join(dp, f), ROOT))
    rows = []
    for p in files:
        t = open(p, encoding="utf-8", errors="ignore").read()
        cf, cb = families(t)
        local_styles = len(re.findall(r"<Style\b[^>]*x:Key=", t))
        local_templates = len(re.findall(r"<(DataTemplate|ControlTemplate)\b[^>]*x:Key=", t))
        row = {
            "file": p,
            "module": p.split("/")[0] if "/" in p else "(root)",
            "fams": dict(sorted(cf.items(), key=lambda kv: -kv[1])),
            "fams_bare_top5": dict(sorted(cb.items(), key=lambda kv: -kv[1])[:5]),
            "local_styles": local_styles,
            "local_templates": local_templates,
            "hard_fontsize": len(re.findall(r'FontSize="\d+(\.\d+)?"', t)),
            "hard_corner": len(re.findall(r'CornerRadius="\d', t)),
            "shared": [k for k, pat in SHARED_MARKERS.items() if re.search(pat, t)],
            "patterns": [k for k, pat in PAT_MARKERS.items() if re.search(pat, t)],
        }
        rows.append(row)
    # module summary
    bymod = defaultdict(lambda: {"files": 0, "local_styles": 0, "local_templates": 0,
                                 "hard_fontsize": 0, "hard_corner": 0,
                                 "fams": defaultdict(int), "dyn": 0, "sta": 0})
    for r in rows:
        m = bymod[r["module"]]
        m["files"] += 1
        m["local_styles"] += r["local_styles"]
        m["local_templates"] += r["local_templates"]
        m["hard_fontsize"] += r["hard_fontsize"]
        m["hard_corner"] += r["hard_corner"]
        for k, v in r["fams"].items():
            m["fams"][k] += v
        t = open(r["file"], encoding="utf-8", errors="ignore").read()
        m["dyn"] += len(re.findall(r"\{DynamicResource\b", t))
        m["sta"] += len(re.findall(r"\{StaticResource\b", t))
    os.makedirs("docs/ui-audit", exist_ok=True)
    json.dump({"generated": __import__("datetime").date.today().isoformat(),
               "files": len(rows), "rows": rows,
               "modules": {k: {**v, "fams": dict(sorted(v["fams"].items(), key=lambda kv: -kv[1]))}
                           for k, v in sorted(bymod.items())}},
              open("docs/ui-audit/PATTERN_CENSUS.json", "w"), indent=1)
    print(f"{'module':22} {'files':>5} {'styles':>6} {'tmpl':>5} {'fsize':>6} {'corner':>6} {'dyn':>6} {'sta':>6}  top families")
    for k, v in sorted(bymod.items(), key=lambda kv: -kv[1]["files"]):
        top = ", ".join(f"{a}:{b}" for a, b in list(v["fams"].items())[:5])
        print(f"{k:22} {v['files']:>5} {v['local_styles']:>6} {v['local_templates']:>5} "
              f"{v['hard_fontsize']:>6} {v['hard_corner']:>6} {v['dyn']:>6} {v['sta']:>6}  {top}")

if __name__ == "__main__":
    main()

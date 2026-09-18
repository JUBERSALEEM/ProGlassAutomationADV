#!/usr/bin/env python3
"""ProGlass UI inventory — full-repo XAML census.

Outputs:
  docs/ui-audit/UI_VIEW_INVENTORY.json  (machine-readable, every file)
  docs/ui-audit/UI_VIEW_INVENTORY.md    (human-readable summary + full table)

Method: ElementTree parse per file (comments/PIs excluded from counts);
resource markers, hardcoded visuals and local chrome are counted from
element attributes + text nodes. Files that fail XML parse are reported,
never skipped.
"""
import json, os, re, sys, datetime, subprocess
import xml.etree.ElementTree as ET

ROOT = os.getcwd()
MERIDIAN_REWRITTEN = {
    "MainWindow.xaml",
    "Views/Dashboard/DashboardView.xaml",
    "Views/Dashboard/Overview/OverviewDashboardView.xaml",
    "Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml",
    "Views/Dashboard/ProformaInvoice/PiDashboardView.xaml",
    "Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml",
    "Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml",
    "Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml",
    "Views/Dashboard/Statement/StDashboardView.xaml",
}
NUM = r"^\d+(\.\d+)?$"
NUM_THICK = re.compile(r"^\d+(\.\d+)?(,\d+(\.\d+)?){0,3}$")
HEX = re.compile(r"#[0-9A-Fa-f]{6,8}\b")
WEIGHT_LIT = re.compile(r"^(Bold|SemiBold|Normal|Light|Medium|ExtraBold)$")

def module_of(p):
    parts = p.split(os.sep)
    if p in ("App.xaml", "MainWindow.xaml"):
        return "Shell"
    if parts[0] == "Views":
        return parts[1] if len(parts) > 2 else "Views"
    if parts[0] == "Controls":
        return "Controls" + ("/" + parts[1] if len(parts) > 2 else "")
    if parts[0] == "Themes":
        return "Themes"
    if parts[0] == "Styles":
        return "Styles"
    if parts[0] == "Tools":
        return "Tools/" + (parts[1] if len(parts) > 1 else "")
    return parts[0] if parts[0] else "(root)"

def scan_file(p):
    r = {
        "path": p, "module": module_of(p), "root": None, "kind": "other",
        "xClass": None, "xNames": 0, "localStyles": 0, "localTemplates": 0,
        "xKeys": 0, "dynamicResource": 0, "staticResource": 0,
        "hardHex": 0, "hardFontSize": 0, "hardWeight": 0,
        "hardThickness": 0, "hardRadius": 0, "hardFixedSize": 0,
        "bindings": 0, "commands": 0, "behaviors": 0, "parseError": None,
    }
    try:
        tree = ET.parse(p)
    except ET.ParseError as e:
        r["parseError"] = str(e)
        return r
    root = tree.getroot()
    tag = root.tag.split("}")[-1]
    r["root"] = tag
    r["kind"] = {"Window": "window", "ThemedDialogWindow": "window",
                 "Page": "page", "UserControl": "usercontrol",
                 "ProductionStagePageBase": "usercontrol",
                 "ResourceDictionary": "resourcedict"}.get(tag, "other")
    xc = root.get("{http://schemas.microsoft.com/winfx/2006/xaml}Class") or root.get("x:Class")
    if xc:
        r["xClass"] = xc
    n_dyn = n_sta = n_hex = n_fs = n_fw = n_th = n_rad = n_fix = n_bind = n_cmd = n_beh = n_names = n_sty = n_tpl = n_keys = 0
    for el in root.iter():
        t = el.tag
        if not isinstance(t, str):
            continue
        local = t.split("}")[-1]
        if local in ("Style",):
            n_sty += 1
        elif local in ("DataTemplate", "ControlTemplate", "StyleTrigger"):
            n_tpl += 1
        for k, v in el.attrib.items():
            if k in ("x:Name", "Name"):
                n_names += 1
            if k == "x:Key":
                n_keys += 1
            if "DynamicResource" in v:
                n_dyn += v.count("{DynamicResource")
            if "StaticResource" in v:
                n_sta += v.count("{StaticResource")
            if "Binding" in v and "{" in v:
                n_bind += v.count("{Binding")
            if k == "Command" and "Binding" in v:
                n_cmd += 1
            if "behaviors:" in k or "behavior" in k.lower():
                n_beh += 1
            if HEX.search(v):
                n_hex += len(HEX.findall(v))
            if k == "FontSize" and re.match(NUM, v):
                n_fs += 1
            if k == "FontWeight" and WEIGHT_LIT.match(v):
                n_fw += 1
            if k in ("Margin", "Padding", "BorderThickness") and NUM_THICK.match(v):
                n_th += 1
            if k == "CornerRadius" and NUM_THICK.match(v):
                n_rad += 1
            if k in ("Width", "Height", "MinWidth", "MaxWidth", "MinHeight", "MaxHeight",
                     "MaxHeight", "FontSize") and re.match(NUM, v):
                if k != "FontSize":
                    n_fix += 1
            elif k == "Width" and v.endswith("*"):
                pass
        if isinstance(el.text, str):
            n_hex += len(HEX.findall(el.text))
    r.update(xNames=n_names, localStyles=n_sty, localTemplates=n_tpl, xKeys=n_keys,
             dynamicResource=n_dyn, staticResource=n_sta, hardHex=n_hex,
             hardFontSize=n_fs, hardWeight=n_fw, hardThickness=n_th, hardRadius=n_rad,
             hardFixedSize=n_fix, bindings=n_bind, commands=n_cmd, behaviors=n_beh)
    return r

def main():
    xamls, xamlcs = [], []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in (".git", ".claude", "node_modules")]
        for f in filenames:
            fp = os.path.join(dirpath, f)
            rel = os.path.relpath(fp, ROOT)
            if f.endswith(".xaml"):
                xamls.append(rel)
            elif f.endswith(".xaml.cs"):
                xamlcs.append(rel)
    xamls.sort()
    rows = [scan_file(p) for p in xamls]

    # host / navigation cross-reference: for each x:Class, which OTHER xaml
    # files reference the class (short name via xmlns-prefixed usage or
    # DataType). Records parents / navigation entry points.
    shortnames = {}
    for r in rows:
        if r["xClass"]:
            short = r["xClass"].rsplit(".", 1)[-1]
            shortnames.setdefault(short, []).append(r["path"])
    for r in rows:
        refs = []
        if r["xClass"]:
            short = r["xClass"].rsplit(".", 1)[-1]
            for p in xamls:
                if p == r["path"]:
                    continue
                try:
                    s = open(p, encoding="utf-8").read()
                except OSError:
                    continue
                if re.search(r'(?::\s*' + re.escape(short) + r'\b|"\s*' + re.escape(short) + r'|\bDataType[^>]*' + re.escape(short) + r"\b)", s):
                    refs.append(p)
        r["referencedBy"] = refs[:6]
    for r in rows:
        r["meridian"] = "rewritten" if r["path"] in MERIDIAN_REWRITTEN else "untouched"
        # code-behind pairing (x:Class -> expected .xaml.cs)
        r["codeBehind"] = None
        if r["xClass"]:
            rel_cls = r["xClass"].replace("ProGlassAutomation.", "").replace(".", "/")
            cand = [c for c in xamlcs if c == rel_cls + ".xaml.cs"]
            r["codeBehind"] = cand[0] if cand else ("MISSING" if r["kind"] in ("window", "usercontrol", "page") else None)
        # risk heuristic
        risk = []
        if r["parseError"]:
            risk.append("PARSE-ERROR")
        if r.get("codeBehind") == "MISSING":
            risk.append("MISSING-CODEBEHIND")
        if r["hardHex"] > 0:
            risk.append(f"HARDCODED-HEX({r['hardHex']})")
        if r["localStyles"] >= 8:
            risk.append(f"LOCAL-STYLE-FAMILY({r['localStyles']})")
        if r["hardFontSize"] >= 10:
            risk.append(f"UNTOKENED-FONTSIZE({r['hardFontSize']})")
        r["risk"] = risk

    # ---- JSON ----
    head = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    out_json = {
        "generated": datetime.date.today().isoformat(),
        "commit": head,
        "method": "ElementTree census; comments/PIs excluded; resource markers counted in attributes",
        "totals": {
            "xamlFiles": len(xamls),
            "xamlCsFiles": len(xamlcs),
            "windows": sum(1 for r in rows if r["kind"] == "window"),
            "pages": sum(1 for r in rows if r["kind"] == "page"),
            "userControls": sum(1 for r in rows if r["kind"] == "usercontrol"),
            "resourceDictionaries": sum(1 for r in rows if r["kind"] == "resourcedict"),
            "otherRoots": sum(1 for r in rows if r["kind"] == "other"),
            "parseErrors": sum(1 for r in rows if r["parseError"]),
            "meridianRewritten": sum(1 for r in rows if r["meridian"] == "rewritten"),
            "dynamicResourceTotal": sum(r["dynamicResource"] for r in rows),
            "staticResourceTotal": sum(r["staticResource"] for r in rows),
            "hardcodedHexTotal": sum(r["hardHex"] for r in rows),
            "hardcodedFontSizeTotal": sum(r["hardFontSize"] for r in rows),
            "filesWithHardHex": sum(1 for r in rows if r["hardHex"] > 0),
        },
        "byModule": {},
        "files": rows,
    }
    for r in rows:
        m = out_json["byModule"].setdefault(r["module"], {"xaml": 0, "views": 0, "resourcedict": 0,
            "dynamicResource": 0, "staticResource": 0, "hardHex": 0, "hardFontSize": 0,
            "localStyles": 0, "rewritten": 0, "parseErrors": 0})
        m["xaml"] += 1
        if r["kind"] in ("window", "page", "usercontrol"):
            m["views"] += 1
        if r["kind"] == "resourcedict":
            m["resourcedict"] += 1
        for k_src, k_dst in (("dynamicResource", "dynamicResource"), ("staticResource", "staticResource"),
                             ("hardHex", "hardHex"), ("hardFontSize", "hardFontSize"), ("localStyles", "localStyles")):
            m[k_dst] += r[k_src]
        if r["meridian"] == "rewritten":
            m["rewritten"] += 1
        if r["parseError"]:
            m["parseErrors"] += 1
    os.makedirs("docs/ui-audit", exist_ok=True)
    with open("docs/ui-audit/UI_VIEW_INVENTORY.json", "w") as f:
        json.dump(out_json, f, indent=1)

    # ---- MD ----
    t = out_json["totals"]
    lines = []
    A = lines.append
    A("# ProGlass UI — Complete XAML Inventory (Phase 1 of release-safe modernization)")
    A("")
    A(f"**Generated:** {out_json['generated']} · **Commit:** `{out_json['commit']}` · **Method:** {out_json['method']}")
    A("")
    A("## Totals (measured, not assumed)")
    A("")
    A("| Metric | Count |")
    A("|---|---|")
    A(f"| .xaml files | **{t['xamlFiles']}** |")
    A(f"| .xaml.cs files | {t['xamlCsFiles']} |")
    A(f"| Windows | {t['windows']} |")
    A(f"| Pages | {t['pages']} |")
    A(f"| UserControls | {t['userControls']} |")
    A(f"| ResourceDictionaries | {t['resourceDictionaries']} |")
    A(f"| Other roots | {t['otherRoots']} |")
    A(f"| XML parse errors | {t['parseErrors']} |")
    A(f"| MERIDIAN-rewritten views | {t['meridianRewritten']} |")
    A(f"| {{DynamicResource}} markers (app-wide) | {t['dynamicResourceTotal']:,} |")
    A(f"| {{StaticResource}} markers (app-wide) | {t['staticResourceTotal']:,} |")
    A(f"| Hardcoded hex colour values | {t['hardcodedHexTotal']:,} (in {t['filesWithHardHex']} files) |")
    A(f"| Hardcoded numeric FontSize | {t['hardcodedFontSizeTotal']:,} |")
    A("")
    A("## By module")
    A("")
    A("| Module | XAML | Views (Win/Pg/UC) | ResDicts | MERIDIAN | DynRes | StatRes | Hex | LitFSz | LocalStyles |")
    A("|---|---|---|---|---|---|---|---|---|---|")
    for mname in sorted(out_json["byModule"], key=lambda k: -out_json["byModule"][k]["xaml"]):
        m = out_json["byModule"][mname]
        A(f"| {mname} | {m['xaml']} | {m['views']} | {m['resourcedict']} | {m['rewritten']} | {m['dynamicResource']:,} | {m['staticResource']:,} | {m['hardHex']} | {m['hardFontSize']} | {m['localStyles']} |")
    A("")
    A("## Full file table")
    A("")
    A("kind: W=Window, P=Page, U=UserControl, R=ResourceDictionary, O=other. `RISK` = heuristic flags.")
    A("")
    A("| File | Kind | DynRes | StatRes | Hex | LitFSz | LocalStyles | Templates | Hosts/parents (heuristic) | MERIDIAN | RISK |")
    A("|---|---|---|---|---|---|---|---|---|---|---|")
    kind_abbr = {"window": "W", "page": "P", "usercontrol": "U", "resourcedict": "R", "other": "O"}
    for r in rows:
        hosts = r.get("referencedBy") or []
        host_txt = "<br>".join("…/" + h.rsplit("/", 1)[-1] for h in hosts[:3]) + (f" (+{len(hosts)-3})" if len(hosts) > 3 else "")
        A(f"| `{r['path']}` | {kind_abbr[r['kind']]} | {r['dynamicResource']} | {r['staticResource']} | "
          f"{r['hardHex']} | {r['hardFontSize']} | {r['localStyles']} | {r['localTemplates']} | {host_txt} | "
          f"{'**REWROTE**' if r['meridian']=='rewritten' else ''} | {'; '.join(r['risk'])} |")
    A("")
    A("## Parse failures (must stay zero)")
    A("")
    pe = [r for r in rows if r["parseError"]]
    if pe:
        for r in pe:
            A(f"- `{r['path']}`: {r['parseError']}")
    else:
        A("None — every .xaml file in the repository parses as well-formed XML.")
    A("")
    with open("docs/ui-audit/UI_VIEW_INVENTORY.md", "w") as f:
        f.write("\n".join(lines))
    print(json.dumps(t, indent=1))
    print("WROTE docs/ui-audit/UI_VIEW_INVENTORY.{json,md}")

if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""ProGlass XAML safety validator (release-safe protocol, Phases 5–8 + 17).

Static analysis gate for every .xaml file in the repository.

  W1  malformed XML (parse failure)
  W2  duplicate x:Name within a namescope (file root / template / style)
  W3  duplicate x:Key within a dictionary file
  W4  duplicate attributes on one element
  W5  StaticResource declared AFTER its use within the same file
  W6  StaticResource / BasedOn key not resolvable in the file's parse scope
      (same file, or a dictionary merged earlier than this file in App.xaml)
  W7  DynamicResource key declared nowhere and not runtime-published
  W8  invalid Thickness / CornerRadius / GridLength / numeric syntax / negatives
  W9  impossible Min > Max on the same element
  W10 Border / ContentControl / ScrollViewer with >1 content child
  W11 ItemsControl with both Items and ItemsSource
  W12 Grid.Row / Grid.Column beyond declared definitions (NOTE: WPF auto-extends)
  W14 x:Class without matching .xaml.cs
  W15 FindName("X") in code-behind where X is not declared in the XAML
  W16 unauthorized StaticResource on runtime-owned tokens (Pg font / density /
      radius tokens must be DynamicResource — 2026-09-18 policy flip)
  W17 hardcoded hex colour in a view (NOTE; theme files are exempt)

Usage:  python3 tools/xaml_validator.py [--files <paths...>]
Writes: docs/ui-audit/XAML_VALIDATION_REPORT.{md,json}
Exit:   0 = no ERROR-level findings, 1 = ERRORs present.

HONESTY GATE: static analysis only — it cannot prove runtime layout, visual
tree construction or actual WPF parse of merged dictionaries. BUILD / RUNTIME
verification = NOT RUN unless a .NET SDK is available.
"""
import re, os, sys, json, datetime
import xml.etree.ElementTree as ET
from collections import defaultdict

ROOT = os.getcwd()
XAML_NS = "http://schemas.microsoft.com/winfx/2006/xaml"

def _load(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

# --- runtime-published tokens (services write to Application.Resources)
# Two publish styles exist: resources["Key"] = … (ThemeManager) and
# indexer d["Key"] = … (PrintTokenService print-time dictionary).
def _runtime_keys():
    s = set()
    for dp, dn, fn in os.walk("Services"):
        for f in fn:
            if f.endswith(".cs"):
                t = _load(os.path.join(dp, f))
                s |= set(re.findall(r'resources\["([A-Za-z0-9_.]+)"\]\s*=', t))
                s |= set(re.findall(r'\["([A-Za-z0-9_.]+)"\]\s*=', t))
    return s

RUNTIME_PUBLISHED = _runtime_keys()
RUNTIME_PUBLISHED |= {
    "DensityInputHeight", "DensityButtonHeight", "DensityRowHeight",
    "DensityTitleFontSize", "DensityKpiValueFontSize", "DensityBodyFontSize",
    "DensityButtonMinWidth", "DensityCardPadding", "DensityStripPadding",
    "DensityPageMargin",
}
# --- runtime-owned tokens that MUST be consumed dynamically
MUST_BE_DYNAMIC = {"PgFontHero", "PgFontHeroSub", "PgFontCardTitle", "PgFontCardSub",
    "PgFontKpiValue", "PgFontKpiLabel", "PgFontKpiFoot", "PgFontStatValue",
    "PgFontStatLabel", "PgFontStatFoot", "PgFontStageValue", "PgFontStageName",
    "PgFontStageFoot", "PgFontBody", "PgFontBodySm", "PgFontMeta", "PgFontBtn",
    "PgFontGlyph", "PgFontGlyphSm", "PgTopFont", "PgTopFontBtn", "PgTopFontCap",
    "PgPageMargin", "PgCardPadding", "PgTilePadding", "PgRowPadding",
    "PgGutter", "PgGutterHalfMargin", "PgKpiTileMinHeight", "PgChartMinHeight",
    "PgCardRadius", "PgTileRadius", "PgCtrlRadius", "PgIconRadius", "PgIconSmRadius"}

NUM = re.compile(r"^-?\d+(\.\d+)?$")
THICK = re.compile(r"^(-?\d+(\.\d+)?|Auto|\*)(,(-?\d+(\.\d+)?)){0,3}$")
CORNER = re.compile(r"^\d+(\.\d+)?(,\d+(\.\d+)?){0,3}$")

# property-element local names that are NOT content children
PROP_EL = {
    "Background", "BorderBrush", "BorderThickness", "Padding", "Margin", "Effect",
    "Child", "Content", "ContentStringFormat", "ContentTemplate",
    "ContentTemplateSelector", "Style", "Template", "Tag", "ToolTip", "Resources",
    "InputBindings", "CornerRadius", "SnapsToDevicePixels", "SnapsToDevicePixels",
    "RenderOptions.EdgeMode", "VerticalAlignment", "HorizontalAlignment",
    "FontSize", "FontWeight", "Foreground", "Background", "Opacity", "Cursor",
    "FocusVisualStyle", "Visibility", "Width", "Height", "Items", "ItemsSource",
    "ItemTemplate", "ItemsPanel", "Command", "CommandParameter", "IsEnabled",
    "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan", "Text",
    "Grid.RowDefinitions", "Grid.ColumnDefinitions", "Trigger", "DataTrigger",
    "Style.Triggers", "ControlTemplate.Triggers", "Setter", "Setter.Value",
    "Setter.Triggers", "ControlTemplate.Resources",
}

def local(tag):
    return tag.split("}")[-1] if isinstance(tag, str) else ""

def find_xamls():
    out = []
    for dp, dn, fn in os.walk(ROOT):
        dn[:] = [d for d in dn if d not in (".git", ".claude", "node_modules")]
        for f in fn:
            if f.endswith(".xaml"):
                out.append(os.path.relpath(os.path.join(dp, f), ROOT))
    return sorted(out)

def app_merge_order():
    app = _load("App.xaml")
    order, index = [], {}
    all_x = find_xamls()
    for i, m in enumerate(re.finditer(r'<ResourceDictionary\s+Source="([^"]+)"', app)):
        base = m.group(1).split("/")[-1]
        cands = [c for c in all_x if c == base or c.endswith("/" + base)]
        if cands:
            index[cands[0]] = i
            order.append(cands[0])
    return order, index

def i18n_keys():
    s = set()
    for dp, dn, fn in os.walk(os.path.join(ROOT, "Themes", "Languages")):
        for f in fn:
            if f.endswith(".xaml"):
                s |= set(re.findall(r'x:Key="([^"]+)"', _load(os.path.join(dp, f))))
    return s

def content_children(el):
    """Direct children that are actual CONTENT (not property elements).
    Property elements appear two ways: bare (<Style>, <Template>, …) or
    dot-qualified (<Border.Background>, <TextBlock.Style>) — the latter keeps
    the full 'Parent.Property' name as its local part in ElementTree."""
    pl = local(el.tag)
    out = []
    for c in el:
        if not isinstance(c.tag, str):
            continue
        lt = local(c.tag)
        if lt in PROP_EL:
            continue
        if "." in lt and lt.split(".", 1)[0] == pl:
            continue
        out.append(c)
    return out

def check_file(path, merge_index, all_declared, i18n, findings):
    F = lambda level, code, msg: findings.append({"file": path, "level": level, "code": code, "msg": msg})
    raw = _load(path)
    nocomm = re.sub(r"<!--.*?-->", "", raw, flags=re.S)
    try:
        root = ET.fromstring(raw)
    except ET.ParseError as e:
        F("ERROR", "W1", f"malformed XML: {e}")
        return
    # (W13 removed: ET parsing already guarantees xmlns prefixes are declared —
    #  an undeclared prefix is a W1 parse failure, not a separate check.)
    # W4 duplicate attributes (raw scan, comments stripped, QUOTE-AWARE tokenizer
    # so 'ElementName=' inside a binding string is not mistaken for an attr)
    ATTR_RE = re.compile(r"([A-Za-z_][\w.:-]*)\s*=\s*(\"([^\"]*)\"|'([^']*)'|[^\s>]+)")
    TAG_RE = re.compile(r"<([A-Za-z_][\w.:-]*)((?:[^>\"']|\"[^\"]*\"|'[^']*')*)>")
    for m in TAG_RE.finditer(nocomm):
        tagname = m.group(1)
        attrregion = m.group(2)
        attrnames = [mm.group(1) for mm in ATTR_RE.finditer(attrregion)]
        dupes = sorted({a for a in attrnames if attrnames.count(a) > 1})
        if dupes:
            F("ERROR", "W4", f"duplicate attribute(s) {dupes} on <{tagname}>")
    # parent map for scopes
    parent = {c: p for p in root.iter() for c in p}
    # W2 / W3
    names = defaultdict(set)
    keys, keydup = set(), set()
    for el in root.iter():
        if not isinstance(el.tag, str):
            continue
        scope = "root"
        anc = parent.get(el)
        while anc is not None:
            if local(anc.tag) in ("DataTemplate", "ControlTemplate", "Style"):
                scope = f"scope@{id(anc)}"
                break
            anc = parent.get(anc)
        nm = el.get(f"{{{XAML_NS}}}Name") or el.get("Name") or el.get("x:Name")
        if nm:
            if nm in names[scope]:
                F("ERROR", "W2", f"duplicate x:Name '{nm}' (scope: {scope})")
            names[scope].add(nm)
        k = el.get(f"{{{XAML_NS}}}Key") or el.get("x:Key")
        if k is not None:
            if k in keys:
                keydup.add(k)
            keys.add(k)
    for k in sorted(keydup):
        F("WARN", "W3", f"duplicate x:Key '{k}' — later declaration shadows the earlier one (latent bug)")
    all_names = {n for s in names.values() for n in s}
    # W5 / W6 / W7 — resource markers
    is_merged_dict = path in merge_index
    my_order = merge_index.get(path, 999999)
    for el in root.iter():
        if not isinstance(el.tag, str):
            continue
        for k, v in el.attrib.items():
            for marker, key in re.findall(r"\{(StaticResource|DynamicResource)\s+([A-Za-z0-9_.]+)", v):
                if key in keys:
                    if marker == "StaticResource":
                        # positions in comment-stripped text (comment mentions
                        # of the key must not count as a "use")
                        use_pos = nocomm.find(key)
                        decl_pos = nocomm.find(f'x:Key="{key}"')
                        if use_pos != -1 and decl_pos != -1 and decl_pos > use_pos:
                            F("WARN", "W5", f"StaticResource '{key}' declared after first use in file")
                    continue
                if marker == "StaticResource":
                    if is_merged_dict:
                        # must be in same file (checked) or an EARLIER-merged file
                        ok = any(
                            p in merge_index and merge_index[p] < my_order and
                            f'x:Key="{key}"' in _load(p)
                            for p in merge_index
                        )
                        if not ok:
                            F("ERROR", "W6", f"StaticResource '{key}' not resolvable in parse scope (dict merged at order {my_order})")
                    else:
                        if key not in all_declared:
                            F("ERROR", "W6", f"StaticResource '{key}' not declared anywhere")
                else:
                    if key not in all_declared and key not in RUNTIME_PUBLISHED and key not in i18n:
                        F("WARN", "W7", f"DynamicResource '{key}' declared nowhere & not runtime-published")
    # W16 unauthorized static on runtime-owned tokens
    for k in re.findall(r"\bStaticResource\s+(Pg[A-Za-z0-9]+)", nocomm):
        if k in MUST_BE_DYNAMIC:
            F("ERROR", "W16", f"StaticResource {k} — runtime-owned token must be DynamicResource")
    # element checks
    for el in root.iter():
        if not isinstance(el.tag, str):
            continue
        t = local(el.tag)
        a = el.attrib
        children = content_children(el)
        if t in ("Border", "ScrollViewer") and len(children) > 1:
            F("ERROR", "W10", f"<{t}> has {len(children)} content children (must be 1)")
        if t == "ContentControl" and len(children) > 1:
            F("ERROR", "W10", f"<ContentControl> has {len(children)} content children (must be 1)")
        if t == "ItemsControl":
            if "ItemsSource" in a:
                for c in el:
                    if isinstance(c.tag, str) and local(c.tag) == "Items":
                        F("ERROR", "W11", "ItemsControl has <Items> child AND ItemsSource")
        # W8 — skip values that are markup extensions ({...}): the value's
        # type comes from the resolved resource, not the literal.
        for prop in ("Margin", "Padding", "BorderThickness"):
            if prop in a and not a[prop].startswith("{") and not THICK.match(a[prop]):
                F("ERROR", "W8", f"invalid {prop} '{a[prop]}'")
        for prop in ("Width", "Height", "MinWidth", "MaxWidth", "MinHeight",
                     "MaxHeight", "FontSize", "Opacity"):
            if prop in a:
                v2 = a[prop]
                if v2.startswith("{"):
                    continue
                if v2 in ("Auto", "*"):
                    continue
                if NUM.match(v2):
                    if float(v2) < 0:
                        F("ERROR", "W8", f"negative {prop} '{v2}'")
                elif not re.match(r"^\d+(\.\d+)?\*$", v2):
                    F("ERROR", "W8", f"invalid {prop} '{v2}'")
        if "CornerRadius" in a and not a["CornerRadius"].startswith("{") \
           and not CORNER.match(a["CornerRadius"]):
            F("ERROR", "W8", f"invalid CornerRadius '{a['CornerRadius']}'")
        for mn, mx in (("MinWidth", "MaxWidth"), ("MinHeight", "MaxHeight")):
            if mn in a and mx in a and NUM.match(a[mn]) and NUM.match(a[mx]) \
               and float(a[mn]) > float(a[mx]):
                F("ERROR", "W9", f"{mn} {a[mn]} > {mx} {a[mx]}")
        if t == "Grid":
            rows = cols = 0
            for c in el:
                if not isinstance(c.tag, str):
                    continue
                lt = local(c.tag)
                if lt in ("RowDefinitions", "Grid.RowDefinitions"):
                    rows += len([x for x in c if isinstance(x.tag, str)])
                elif lt in ("ColumnDefinitions", "Grid.ColumnDefinitions"):
                    cols += len([x for x in c if isinstance(x.tag, str)])
            for c in el:
                if not isinstance(c.tag, str):
                    continue
                ca = c.attrib
                if rows and "Grid.Row" in ca and NUM.match(ca["Grid.Row"]) and int(ca["Grid.Row"]) >= rows:
                    F("NOTE", "W12", f"Grid.Row={ca['Grid.Row']} >= {rows} RowDefinitions (WPF auto-extends)")
                if cols and "Grid.Column" in ca and NUM.match(ca["Grid.Column"]) and int(ca["Grid.Column"]) >= cols:
                    F("NOTE", "W12", f"Grid.Column={ca['Grid.Column']} >= {cols} ColumnDefinitions (WPF auto-extends)")
    # W14 / W15 code-behind
    xc = root.get(f"{{{XAML_NS}}}Class")
    if xc:
        rel_cls = xc.replace("ProGlassAutomation.", "").replace(".", "/")
        cb = os.path.join(ROOT, rel_cls + ".xaml.cs")
        if not os.path.exists(cb) and not path.startswith("Tools/"):
            F("ERROR", "W14", f"x:Class {xc} has no code-behind ({rel_cls}.xaml.cs)")
        elif os.path.exists(cb):
            cs = _load(cb)
            for fn_ in re.findall(r'FindName\("([^"]+)"\)', cs):
                if fn_ not in all_names:
                    F("ERROR", "W15", f'FindName("{fn_}") in code-behind but not declared in XAML')
    # W17 hex in views
    hexc = len(re.findall(r"#[0-9A-Fa-f]{6,8}\b", nocomm))
    if hexc and not (path.startswith("Themes/") or path.startswith("Tools/")):
        F("NOTE", "W17", f"{hexc} hardcoded hex value(s) in view")

def main():
    args = sys.argv[1:]
    files = args[1:] if args and args[0] == "--files" else find_xamls()
    _, merge_index = app_merge_order()
    all_declared = set()
    for p in find_xamls():
        all_declared |= set(re.findall(r'x:Key="([A-Za-z0-9_.]+)"', _load(p)))
    i18n = i18n_keys()
    findings = []
    for p in files:
        check_file(p, merge_index, all_declared, i18n, findings)
    os.makedirs("docs/ui-audit", exist_ok=True)
    errors = sum(1 for f in findings if f["level"] == "ERROR")
    warns = sum(1 for f in findings if f["level"] == "WARN")
    notes = sum(1 for f in findings if f["level"] == "NOTE")
    with open("docs/ui-audit/XAML_VALIDATION_REPORT.json", "w") as fh:
        json.dump({"generated": datetime.date.today().isoformat(), "files": len(files),
                   "errors": errors, "warns": warns, "notes": notes,
                   "findings": findings}, fh, indent=1)
    L = []
    A = L.append
    A("# XAML Safety Validation Report (Phase 17 gate)")
    A("")
    A(f"**Generated:** {datetime.date.today().isoformat()} · **Files checked:** {len(files)} · "
      f"**ERROR {errors} / WARN {warns} / NOTE {notes}**")
    A("")
    A("**Legend:** W1 malformed · W2 dup x:Name · W3 dup x:Key · W4 dup attribute · W5 static declared-after-use · "
      "W6 static unresolved in parse scope · W7 dynamic unresolved & not runtime-published · W8 invalid numeric syntax · "
      "W9 Min>Max · W10 single-child violation · W11 Items+ItemsSource · W12 grid out-of-bounds (auto-extends) · "
      "W14 missing code-behind · W15 FindName missing · W16 unauthorized static on runtime token · "
      "W17 hardcoded hex in view. W3 duplicate x:Key is WARN (WPF last-wins shadowing, latent). "
      "W7 covers keys published at runtime by Services (ThemeManager, PrintTokenService, …).")
    A("")
    A("**Honesty gate:** static analysis only. It cannot prove runtime layout, visual-tree construction or the "
      "actual WPF parse of merged dictionaries. **BUILD / RUNTIME verification: NOT RUN** (no .NET SDK in this "
      "environment) — do not treat this report as build or runtime proof.")
    A("")
    for lvl in ("ERROR", "WARN", "NOTE"):
        sub = [f for f in findings if f["level"] == lvl]
        A(f"## {lvl} ({len(sub)})")
        A("")
        if not sub:
            A("_none_")
            A("")
            continue
        for f in sub:
            A(f"- `{f['code']}` **{f['file']}** — {f['msg']}")
        A("")
    with open("docs/ui-audit/XAML_VALIDATION_REPORT.md", "w") as fh:
        fh.write("\n".join(L))
    print(f"files={len(files)} ERROR={errors} WARN={warns} NOTE={notes}")
    for f in findings:
        if f["level"] == "ERROR":
            print(f"  ERROR {f['code']} {f['file']}: {f['msg'][:140]}")
    sys.exit(1 if errors else 0)

if __name__ == "__main__":
    main()

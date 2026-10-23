#!/usr/bin/env python3
"""
v3-phase1-check.py — portable (no Windows, no .NET) Phase 1 verifier.

Checks, in order, and exits non-zero on the first hard failure class:

  [A] V3 Bento package completeness — every ThemeContract.Required token
      and RequiredStyles style present in Light+Dark+components, zero
      duplicate keys. Mirrors what ResourceValidation.Validate() would do
      at ThemeService.Load() time, so a package that would throw at
      startup is caught here instead.
  [B] V3CompatibilityHub coverage —
        * every VisualThemeEngine ManagedKeys entry either hub-mapped or
          on the explicit Effect exemption list (V3's contract has no
          Effect tokens);
        * every Ov.* / Ios26* key referenced from Views/Controls is
          hub-mapped (no blank/stale references after Apply);
        * all spec-mandated CompatibilityHub rows present.
  [C] Brace/paren balance on the Phase 1 C# surface (rough filter; the
      committed-version comparison in review is authoritative).

NOT a compile. NOT a runtime proof. Source-level only.
"""

import os
import re
import sys
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
os.chdir(ROOT)

FAILS = []


def read(path):
    return pathlib.Path(path).read_text(encoding="utf-8-sig", errors="ignore")


# ── [A] package completeness ──────────────────────────────────────────────
def collect_package(root):
    """Transitively collect x:Key entries + implicit Style TargetType keys."""
    keys, dupes, seen = {}, [], []

    def walk(path):
        if path in seen:
            return
        seen.append(path)
        p = pathlib.Path(path)
        if not p.exists():
            dupes.append(f"MISSING FILE {path}")
            return
        t = read(path)
        for m in re.finditer(r'x:Key="([^"]+)"', t):
            if m.group(1) in keys:
                dupes.append(f"dup {m.group(1)} in {path}")
            else:
                keys[m.group(1)] = path
        for m in re.finditer(r'<Style (?:BasedOn="[^"]*" )?TargetType="(?:\w+:)?(\w+)"\s*/?>', t):
            keys.setdefault(f"@type:{m.group(1)}", path)
        base = p.parent
        for mm in re.finditer(r'Source="([^"]+)"', t):
            src = mm.group(1)
            if src.startswith("pack://"):
                src = src.split("/component/")[-1]
            norm = os.path.normpath(str(base / src)).replace(os.sep, "/")
            if norm.endswith(".xaml"):
                walk(norm)

    walk(root)
    return keys, dupes


def check_package():
    tc = read("Theme/V3/step1.a.ThemeEngine/step1.a.1.ThemeContract.cs")
    tokens = re.findall(
        r'\["([A-Za-z0-9_]+)"\] = typeof\((\w+)\)',
        re.search(r"Required\s*\{.*?Dictionary<string, Type>\s*\{(.*?)\n\s*\}\);", tc, re.S).group(1),
    )
    rs_block = re.search(
        r"RequiredStyles\s*\{.*?Dictionary<object, Type>\s*\{(.*?)\n\s*\}\);", tc, re.S
    ).group(1)
    str_styles = re.findall(r'\["([^"]+)"\]', rs_block)
    type_styles = re.findall(r"\[typeof\((\w+)\)\]", rs_block)

    ok = True
    for mode in ("Light", "Dark"):
        keys, dupes = collect_package(f"Theme/V3/ThemeBento/ThemeBento.{mode}.xaml")
        ckeys, cdupes = collect_package(
            "Theme/V3/ThemeBento/step1.e.Components/step1.e.1.Components.xaml"
        )
        for k, v in ckeys.items():
            if k in keys and k.startswith("@type:"):
                continue
            if k in keys:
                dupes.append(f"dup across token/component: {k}")
            else:
                keys[k] = v
        missing = [k for k, _ in tokens if k not in keys]
        miss_s = [k for k in str_styles if k not in keys]
        miss_t = [t for t in type_styles if f"@type:{t}" not in keys]
        if missing or miss_s or miss_t or dupes:
            ok = False
            FAILS.append(
                f"[A] Bento {mode}: missing tokens={missing} styles={miss_s + miss_t} dupes={dupes}"
            )
        else:
            print(f"[A] Bento {mode}: {len(keys)} keys, 0 missing, 0 dupes  OK")
    if ok:
        print(
            f"[A] contract: {len(tokens)} required tokens, "
            f"{len(str_styles)}+{len(type_styles)} required styles — all present"
        )
    return ok


# ── [B] hub coverage ──────────────────────────────────────────────────────
EFFECT_EXEMPTION = {
    "ShadowSoft", "ShadowElevated", "GlassShadowSoft", "GlassShadowSoftBorder",
    "GlassShadowSoftButton", "GlassShadowSoftCheckBox", "GlassShadowSoftTextBox",
    "GlassShadowSoftToggle",
}


def check_hub():
    import glob as g

    hub = read("Theme/V3/V3CompatibilityHub.cs")
    targets = set(re.findall(r'\("([A-Za-z0-9_.]+)",\s*"[^"]+"\)', hub))
    targets |= set(re.findall(r'(?:SetCorner|SetBrush)\(app,\s*"([^"]+)"', hub))
    targets |= set(re.findall(r'app\["([^"]+)"\]', hub))
    targets |= set(re.findall(r'SetValue\(app,\s*"([^"]+)"', hub))

    ok = True
    m = re.search(r"ManagedKeys\s*=\s*\{(.*?)\n\s*\};", read("Services/Theme/VisualThemeEngine.Part1.cs"), re.S)
    managed = re.findall(r'"([A-Za-z0-9_]+)"', m.group(1))
    uncovered = [k for k in managed if k not in targets and k not in EFFECT_EXEMPTION]
    if uncovered:
        ok = False
        FAILS.append(f"[B] ManagedKeys uncovered (not an Effect): {uncovered}")
    else:
        eff = [k for k in managed if k in EFFECT_EXEMPTION]
        print(f"[B] ManagedKeys: {len(managed) - len(eff)}/72 hub-mapped, {len(eff)} Effect-exempt  OK")

    refs = set()
    for f in g.glob("Views/**/*.xaml", recursive=True) + g.glob("Controls/**/*.xaml", recursive=True):
        refs |= set(re.findall(r"\{DynamicResource ((?:Ov|Ios26)[A-Za-z0-9_.]+)\}", read(f)))
    ref_uncovered = sorted(k for k in refs if k not in targets and k != "Ios26CardShadow")
    if ref_uncovered:
        ok = False
        FAILS.append(f"[B] referenced Ov.*/Ios26* uncovered: {ref_uncovered}")
    else:
        print(f"[B] referenced Ov.*/Ios26*: {len(refs)} keys — all hub-mapped (Ios26CardShadow=Effect)  OK")

    spec_rows = [
        "WindowBackgroundBrush", "BackgroundBrush", "Ov.Canvas", "SurfacePrimaryBrush",
        "Ov.Surface", "GlassSurfaceBrush", "LiquidCard", "ModuleBackgroundBrush",
        "SurfaceSecondaryBrush", "Ov.Soft", "ElevatedSurfaceBrush", "SurfaceElevatedBrush",
        "GlassSurfaceElevated", "TextPrimaryBrush", "PrimaryForegroundBrush", "Ov.Ink",
        "TextSecondaryBrush", "SecondaryForegroundBrush", "MutedForegroundBrush",
        "TextTertiaryBrush", "AccentBrush", "Ov.Accent", "AccentHoverBrush",
        "AccentPressedBrush", "AccentSoftBrush", "AccentLightBrush",
        "OnAccentForegroundBrush", "Ov.OnAccent", "BorderSubtleBrush", "BorderDefaultBrush",
        "OutlineBrush", "Ov.Line", "DividerBrush", "BorderFocusBrush", "LiquidFocusBrush",
        "MenuHoverBrush", "LiquidControlHoverBrush", "LiquidSelectionBrush", "SelectionBrush",
        "TintGreenBrush", "TintRedBrush", "TintAmberBrush", "CardCornerRadius",
        "ControlCornerRadius",
    ]
    miss = [k for k in spec_rows if k not in targets]
    if miss:
        ok = False
        FAILS.append(f"[B] spec-table rows missing: {miss}")
    else:
        print(f"[B] spec CompatibilityHub rows: {len(spec_rows)}/{len(spec_rows)} present  OK")
    return ok


# ── [C] balance ───────────────────────────────────────────────────────────
def check_balance():
    files = [
        "Theme/V3/V3CompatibilityHub.cs",
        "Theme/V3/V3ThemeApplier.cs",
        "Services/Theme/ThemePipeline.cs",
        "Services/Settings/AppearanceService.cs",
        "App.StartupCore.cs",
        "Services/Theme/VisualThemeEngine.Part1.cs",
        "Theme/V3/step1.a.ThemeEngine/step1.a.2.ThemeService.cs",
        "Theme/V3/step1.a.ThemeEngine/step1.a.6.ThemeCatalog.cs",
    ]
    ok = True
    for f in files:
        t = re.sub(r"//.*", "", read(f))
        t = re.sub(r'"(\\.|[^"\\])*"', '""', t)
        b, p = t.count("{") - t.count("}"), t.count("(") - t.count(")")
        if b or p:
            ok = False
            FAILS.append(f"[C] {f}: braces={b} parens={p}")
    if ok:
        print(f"[C] brace/paren balance: {len(files)} files OK")
    return ok


def main():
    results = [check_package(), check_hub(), check_balance()]
    print()
    if all(results) and not FAILS:
        print("v3-phase1-check: ALL PASS (source-level only — not a compile, not a runtime proof)")
        return 0
    print(f"v3-phase1-check: {len(FAILS)} FAILURE(S)")
    for f in FAILS:
        print("  FAIL:", f)
    return 1


if __name__ == "__main__":
    sys.exit(main())

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
# ManagedKeys = ManagedKeySeeds.Concat(EightThemePainter.AllTokenKeys)
# since de3b1b4. Exemptions, each with the reason it is not a hub row:
EFFECT_EXEMPTION = {
    # DropShadowEffect — V3's contract has no Effect tokens; authored
    # values live in merged dictionaries and resolve for both modes.
    "ShadowSoft", "ShadowElevated", "GlassShadowSoft", "GlassShadowSoftBorder",
    "GlassShadowSoftButton", "GlassShadowSoftCheckBox", "GlassShadowSoftTextBox",
    "GlassShadowSoftToggle",
    "ShadowDeep", "DashCardShadowEffect", "DashSubtleShadowEffect",
    "DashInteractiveShadowEffect", "GlassShadowPanel", "GlassShadowActive",
    "GlassShadowActiveButton",
    # Neumorphism extrude effects — orphaned (zero consumers outside the
    # retired painter); the Neu family package is Phase 4.
    "NeuLightEffect", "NeuShadeEffect", "NeuPressedLightEffect",
    "NeuPressedShadeEffect",
}
GRADIENT_EXEMPTION = {
    # LinearGradientBrush, consumed only inside merged theme styles (0
    # view references). Flattening them to a solid contract token would
    # REDUCE the authored material; both modes keep their authored value.
    "LiquidGlassFrostedBrush", "LiquidAccentGradient", "DashHeroBlueBrush",
    "DashLiquidBorderBrush",
}
MOTION_EXEMPTION = {
    # Mode-invariant authored values: MotionFast/Normal/Slow/Reduced in
    # SemanticMotion.xaml (App-reached via ProGlassDesignSystem), the two
    # KeySplines in a merged token file. The painters' per-family motion
    # tuning is retired with them.
    "MotionFast", "MotionNormal", "MotionSlow", "MotionReduced",
    "MotionEaseOutKeySpline", "MotionSpringKeySpline",
}
RADIUS_EXEMPTION = {
    # Mode-invariant authored geometry: defined in LightTheme +
    # ProGlassDesignSystem/DesignTokens (both App-merged) and referenced by
    # views — they resolve in both modes. The painter's family geometry
    # (Control-4 / Control / Card / profile Hero) is retired with the
    # painters; a V3-derived substitute would change production-tested
    # view radii without a V3 token to derive Hero from.
    "Radius.Small", "Radius.Medium", "Radius.Large", "Radius.Hero",
}
SURFACE_PASS_EXEMPTION = {
    # Surface.*.Background/Border/CornerRadius: written by the applier's
    # post-pass (ReapplySemanticSurfaces + PublishProGlassDesignSystem),
    # not by the hub map itself.
    "Surface.Primary.BackgroundBrush", "Surface.Secondary.BackgroundBrush",
    "Surface.Detail.BackgroundBrush", "Surface.Primary.BorderBrush",
    "Surface.Secondary.BorderBrush", "Surface.Detail.BorderBrush",
    "Surface.Primary.CornerRadius", "Surface.Secondary.CornerRadius",
    "Surface.Detail.CornerRadius",
}
EXEMPT = (EFFECT_EXEMPTION | GRADIENT_EXEMPTION | MOTION_EXEMPTION
          | SURFACE_PASS_EXEMPTION | RADIUS_EXEMPTION)


def effective_managed_keys():
    """ManagedKeySeeds ∪ EightThemePainter.AllTokenKeys (both de3b1b4+)."""
    part1 = read("Services/Theme/VisualThemeEngine.Part1.cs")
    m = re.search(r"ManagedKeySeeds\s*=\s*\n?\s*\{(.*?)\n\s*\};", part1, re.S)
    seeds = re.findall(r'"([A-Za-z0-9_.]+)"', m.group(1)) if m else []
    painter = read("Services/Theme/Eight/EightThemePainter.cs")
    keys, seen = list(seeds), set(seeds)
    # each group array
    for g in re.findall(
        r'(?:private|internal) static readonly string\[\] (\w+)\s*=\s*\n?\s*\{(.*?)\n\s*\};',
        painter, re.S,
    ):
        for k in re.findall(r'"([A-Za-z0-9_.]+)"', g[1]):
            if k not in seen:
                seen.add(k); keys.append(k)
    # the trailing new[] { ... } literal inside Combine(...)
    m2 = re.search(r"new\[\]\s*\{(.*?)\}\);", painter, re.S)
    if m2:
        for k in re.findall(r'"([A-Za-z0-9_.]+)"', m2.group(1)):
            if k not in seen:
                seen.add(k); keys.append(k)
    return keys


def check_hub():
    import glob as g

    hub = read("Theme/V3/V3CompatibilityHub.cs")
    targets = set(re.findall(r'\("([A-Za-z0-9_.]+)",\s*"[^"]+"\)', hub))
    targets |= set(re.findall(r'(?:SetCorner|SetBrush)\(app,\s*"([^"]+)"', hub))
    targets |= set(re.findall(r'app\["([^"]+)"\]', hub))
    targets |= set(re.findall(r'SetValue\(app,\s*"([^"]+)"', hub))

    ok = True
    managed = effective_managed_keys()
    uncovered = [k for k in managed if k not in targets and k not in EXEMPT]
    if uncovered:
        ok = False
        FAILS.append(f"[B] ManagedKeys uncovered (no hub row, no exemption): {uncovered}")
    else:
        mapped = sum(1 for k in managed if k in targets)
        print(f"[B] ManagedKeys: {mapped}/{len(managed)} hub-mapped, "
              f"{len(managed) - mapped} exempt (effects/gradients/motion/radius/surface-pass)  OK")

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

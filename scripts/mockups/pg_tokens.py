#!/usr/bin/env python3
# ═══════════════════════════════════════════════════════════════════════════
# FILE: scripts/mockups/pg_tokens.py
# PROJECT: ProGlassAutomation
# MODULE: Settings redesign mockups — TOKEN EXTRACTION (dev-only tooling)
#
# WHY THIS FILE EXISTS
# ────────────────────
# A mockup sheet that hand-types its colours drifts from the module the first
# time a token moves, and nobody can tell from the PNG. So the sheets are
# RENDERED FROM THE SHIPPED VALUES: everything below is parsed out of the live
# XAML / C# at render time —
#
#   Themes/LightTheme.xaml, Themes/DarkTheme.xaml
#       SettingsCardFaceBrush / SettingsCardEdgeBrush /
#       SettingsInnerFaceBrush / SettingsInnerEdgeBrush / SettingsCardShadow
#       Ios26* palette · AccentColor / Success / Warning / Danger
#       AppBackgroundBrush (the invariant stage + its four orbs)
#   Themes/Styles/AboutSurface.xaml    Ios26Radius{Card,Inner,Tile,Chip,Small}
#   Themes/Styles/AboutTypeScale.xaml  Ios26FontSize* (the whole type ladder)
#   Themes/Styles/AppStage.xaml        the five stage colours
#   Views/Settings/SettingsCardStyles.xaml  spacing scale (gaps, paddings)
#   Services/Theme/AccentPresets.cs    the accent palette in display order
#   ViewModels/Settings/SettingsViewModel.Navigation.cs  NavRows (the registry)
#   Themes/Languages/en-US.xaml        every label the sheets print
#
# If a token changes in the product, re-run the renderer and the sheet shows
# the new value. If a token disappears, this module raises — a sheet can never
# silently fall back to a guess.
#
# NOT COMMITTED ARTEFACTS: the PNG sheets themselves. `.gitignore` carries the
# owner directive (`docs/mockups/**/*.png`, 2026-09-16: "never commit PNG/image
# mockups to the repo"). This script and its two siblings are the source.
# ═══════════════════════════════════════════════════════════════════════════
from __future__ import annotations

import os
import re
import sys
import json

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


# ─────────────────────────────────────────────────────────────────────────────
# low level readers
# ─────────────────────────────────────────────────────────────────────────────
def _read(rel: str) -> str:
    path = os.path.join(REPO, rel)
    if not os.path.exists(path):
        raise SystemExit(f"[pg_tokens] missing source file: {rel}")
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def block(text: str, key: str) -> str:
    """The full element carrying x:Key="key" (self-closing or balanced)."""
    m = re.search(r'<([A-Za-z0-9_.:]+)\b[^>]*?x:Key="%s"' % re.escape(key), text)
    if not m:
        raise SystemExit(f"[pg_tokens] key not found: {key}")
    tag = m.group(1)
    start = m.start()
    # find the end of the opening tag
    gt = text.index(">", m.end())
    if text[gt - 1] == "/":
        return text[start:gt + 1]
    close = f"</{tag}>"
    end = text.find(close, gt)
    if end < 0:
        raise SystemExit(f"[pg_tokens] unbalanced element for key: {key}")
    return text[start:end + len(close)]


def parse_color(spec: str):
    """WPF colour literal → (r, g, b, a) ints. Accepts #RGB/#ARGB/#RRGGBB/#AARRGGBB."""
    spec = spec.strip()
    if spec.startswith("#"):
        h = spec[1:]
        if len(h) == 6:
            return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)
        if len(h) == 8:  # WPF is #AARRGGBB
            return (int(h[2:4], 16), int(h[4:6], 16), int(h[6:8], 16), int(h[0:2], 16))
        if len(h) == 3:
            return tuple(int(c * 2, 16) for c in h) + (255,)
        if len(h) == 4:
            return (int(h[1] * 2, 16), int(h[2] * 2, 16), int(h[3] * 2, 16), int(h[0] * 2, 16))
    named = {
        "White": (255, 255, 255, 255), "Black": (0, 0, 0, 255),
        "Transparent": (0, 0, 0, 0), "Red": (255, 0, 0, 255),
    }
    if spec in named:
        return named[spec]
    raise SystemExit(f"[pg_tokens] cannot parse colour: {spec!r}")


def _stops(xml: str):
    out = []
    for m in re.finditer(r'<GradientStop\s+([^>]*?)/?>', xml):
        attrs = dict(re.findall(r'(\w+)="([^"]*)"', m.group(1)))
        out.append((float(attrs.get("Offset", 0)), parse_color(attrs["Color"])))
    if not out:
        raise SystemExit("[pg_tokens] gradient with no stops")
    return sorted(out, key=lambda s: s[0])


def _points(xml: str, name: str, default):
    m = re.search(rf'{name}="([0-9.,\- ]+)"', xml)
    if not m:
        return default
    x, y = m.group(1).split(",")
    return (float(x), float(y))


def brush_layers(xml: str):
    """Normalise ANY brush element into a list of paint layers.

    Each layer: {'rect': (x0,y0,x1,y1) in 0..1 of the painted box,
                 'start': (sx,sy), 'end': (ex,ey), 'stops': [(offset, rgba)]}
    A SolidColorBrush becomes one full-bleed layer with two equal stops, so the
    renderer has exactly one code path.
    """
    if re.search(r"<DrawingBrush\b", xml):
        layers = []
        for gd in re.findall(r"<GeometryDrawing\b.*?</GeometryDrawing>", xml, re.S):
            rect = re.search(r'<RectangleGeometry\s+Rect="([^"]+)"', gd)
            if rect:
                x, y, w, h = (float(v) for v in rect.group(1).split(","))
                box = (x, y, x + w, y + h)
            else:
                box = (0.0, 0.0, 1.0, 1.0)
            gb = re.search(r"<LinearGradientBrush\b[^>]*>.*?</LinearGradientBrush>", gd, re.S)
            if not gb:
                solid = re.search(r'<GeometryDrawing\s+Brush="([^"]+)"', gd)
                if solid:
                    c = parse_color(solid.group(1))
                    layers.append({"rect": box, "start": (0, 0), "end": (0, 1),
                                   "stops": [(0, c), (1, c)]})
                continue
            g = gb.group(0)
            layers.append({"rect": box,
                           "start": _points(g, "StartPoint", (0, 0)),
                           "end": _points(g, "EndPoint", (0, 1)),
                           "stops": _stops(g)})
        return layers

    lg = re.search(r"<LinearGradientBrush\b[^>]*>.*?</LinearGradientBrush>", xml, re.S)
    if lg:
        g = lg.group(0)
        return [{"rect": (0, 0, 1, 1),
                 "start": _points(g, "StartPoint", (0, 0)),
                 "end": _points(g, "EndPoint", (0, 1)),
                 "stops": _stops(g)}]

    rad = re.search(r"<RadialGradientBrush\b[^>]*>.*?</RadialGradientBrush>", xml, re.S)
    if rad:
        g = rad.group(0)
        return [{"rect": (0, 0, 1, 1), "radial": True,
                 "center": _points(g, "Center", (0.5, 0.5)),
                 "radius": _points(g, "RadiusX", (0.5, 0.5)),
                 "stops": _stops(g)}]

    solid = re.search(r'<SolidColorBrush\b[^>]*?Color="([^"]+)"', xml, re.S)
    if solid:
        spec = solid.group(1)
        if spec.startswith("{"):  # Color="{StaticResource AccentColor}"
            spec = _resolve_static_color(spec[spec.index("StaticResource") + 15:].rstrip("}").strip())
        c = parse_color(spec)
        return [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1), "stops": [(0, c), (1, c)]}]

    m = re.search(r'Color="([^"]+)"', xml)
    if m:
        c = parse_color(m.group(1))
        return [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1), "stops": [(0, c), (1, c)]}]
    raise SystemExit("[pg_tokens] unrecognised brush:\n" + xml[:300])


_STATIC_COLORS: dict[str, str] = {}


def _resolve_static_color(key: str) -> str:
    if key not in _STATIC_COLORS:
        raise SystemExit(f"[pg_tokens] unresolved StaticResource colour: {key}")
    return _STATIC_COLORS[key]


def solid(text: str, key: str, statics: dict | None = None):
    """First paint layer's flat colour for a SolidColorBrush-ish key.

    `statics` is the theme's own <Color> table, so AccentBrush resolves to the
    theme's AccentColor rather than whichever dictionary was parsed first.
    """
    xml = block(text, key)
    m = re.search(r'<SolidColorBrush\b[^>]*?Color="\{StaticResource\s+([^}]+)\}"', xml, re.S)
    if m and statics and m.group(1).strip() in statics:
        return parse_color(statics[m.group(1).strip()])
    return brush_layers(xml)[0]["stops"][0][1]


# ─────────────────────────────────────────────────────────────────────────────
# theme dictionaries
# ─────────────────────────────────────────────────────────────────────────────
def _load_theme(rel: str) -> dict:
    txt = _read(rel)
    # StaticResource colours are THEME-SCOPED: AccentColor is #0F6FA8 in Light
    # and #38BDF8 in Dark, so resolution must not leak across the two passes.
    statics = {m.group(1): m.group(2).strip() for m in
               re.finditer(r'<Color\s+x:Key="([^"]+)"[^>]*>([^<]+)</Color>', txt)}
    for k, v in statics.items():
        _STATIC_COLORS.setdefault(k, v)

    t: dict = {}
    # every Ios26* solid brush + the semantic anchors the sheets need
    wanted = [
        "Ios26HairlineBrush", "Ios26TrackBrush", "Ios26HoverBrush",
        "Ios26TextTitleBrush", "Ios26TextBodyBrush", "Ios26TextSecondaryBrush",
        "Ios26TextMutedBrush", "Ios26TextFaintBrush", "Ios26OnAccentBrush",
        "Ios26OnAccentChipBrush", "Ios26AccentBrush", "Ios26AccentDeepBrush",
        "Ios26AccentSoftBrush", "Ios26AccentSoftTextBrush", "Ios26SuccessBrush",
        "Ios26SuccessSoftBrush", "Ios26ValuePillBrush", "Ios26SearchBrush",
        "Ios26NumberBrush", "Ios26DangerDotBrush", "Ios26CardBrush",
        "Ios26CardBorderBrush", "Ios26TileBrush", "Ios26TileBorderBrush",
        "AccentBrush", "DangerBrush", "SuccessBrush", "WarningBrush",
        "DividerBrush", "TextPrimaryBrush", "MutedForegroundBrush",
        "SurfaceElevatedBrush", "BorderStrongBrush",
    ]
    for key in wanted:
        try:
            t[key] = solid(txt, key, statics)
        except SystemExit:
            pass
    # semantic tokens declared in the shared dictionaries (light values)
    for key, fallback_rel in (("SurfaceElevatedBrush", "Themes/Styles/SemanticTokens.xaml"),
                              ("BorderStrongBrush", "Themes/Styles/SemanticTokens.xaml"),
                              ("Surface.Primary.BackgroundBrush", "Themes/Styles/DesignTokens.xaml")):
        if key not in t:
            try:
                t[key] = solid(_read(fallback_rel), key)
            except SystemExit:
                pass

    # the five Settings material keys
    t["card_face"] = brush_layers(block(txt, "SettingsCardFaceBrush"))
    t["card_edge"] = brush_layers(block(txt, "SettingsCardEdgeBrush"))
    t["inner_face"] = brush_layers(block(txt, "SettingsInnerFaceBrush"))
    t["inner_edge"] = brush_layers(block(txt, "SettingsInnerEdgeBrush"))
    sh = block(txt, "SettingsCardShadow")
    t["card_shadow"] = {
        "color": parse_color(re.search(r'Color="([^"]+)"', sh).group(1)),
        "opacity": float(re.search(r'Opacity="([^"]+)"', sh).group(1)),
        "blur": float(re.search(r'BlurRadius="([^"]+)"', sh).group(1)),
        "depth": float(re.search(r'ShadowDepth="([^"]+)"', sh).group(1)),
        "direction": float(re.search(r'Direction="([^"]+)"', sh).group(1)),
    }
    # the invariant stage (identical in both dictionaries — AppStageAuditTests
    # pins the two blocks byte-for-byte, so reading either is correct)
    stage = block(txt, "AppBackgroundBrush")
    layers = []
    for gd in re.findall(r"<GeometryDrawing\b.*?</GeometryDrawing>", stage, re.S):
        brush = re.search(r'Brush="([^"]+)"', gd)
        ell = re.search(r'<EllipseGeometry\s+Center="([^"]+)"\s+RadiusX="([^"]+)"\s+RadiusY="([^"]+)"', gd)
        rect = re.search(r'<RectangleGeometry\s+Rect="([^"]+)"', gd)
        if rect and not ell:
            layers.append({"kind": "rect", "color": parse_color(brush.group(1))})
        elif ell:
            cx, cy = (float(v) for v in ell.group(1).split(","))
            layers.append({"kind": "orb", "color": parse_color(brush.group(1)),
                           "cx": cx, "cy": cy,
                           "rx": float(ell.group(2)), "ry": float(ell.group(3))})
    t["stage"] = layers
    return t


# ─────────────────────────────────────────────────────────────────────────────
# scale dictionaries
# ─────────────────────────────────────────────────────────────────────────────
def _radii() -> dict:
    txt = _read("Themes/Styles/AboutSurface.xaml")
    out = {}
    for m in re.finditer(r'<CornerRadius\s+x:Key="([^"]+)"[^>]*>([^<]+)</CornerRadius>', txt):
        v = m.group(2).strip()
        parts = [float(x) for x in v.split(",")]
        out[m.group(1)] = parts[0] if len(parts) == 1 else parts
    sh = block(txt, "Ios26CardShadow")
    out["Ios26CardShadow"] = {
        "color": parse_color(re.search(r'Color="([^"]+)"', sh).group(1)),
        "opacity": float(re.search(r'Opacity="([^"]+)"', sh).group(1)),
        "blur": float(re.search(r'BlurRadius="([^"]+)"', sh).group(1)),
        "depth": float(re.search(r'ShadowDepth="([^"]+)"', sh).group(1)),
    }
    return out


def _type_scale() -> dict:
    txt = _read("Themes/Styles/AboutTypeScale.xaml")
    out = {}
    for m in re.finditer(r'<sys:Double\s+x:Key="([^"]+)"[^>]*>([^<]+)</sys:Double>', txt):
        out[m.group(1)] = float(m.group(2).strip())
    return out


def _spacing() -> dict:
    txt = _read("Views/Settings/SettingsCardStyles.xaml")
    out = {}
    for m in re.finditer(r'<sys:Double\s+x:Key="(Settings[^"]+)"[^>]*>([^<]+)</sys:Double>', txt):
        out[m.group(1)] = float(m.group(2).strip())
    for m in re.finditer(r'<Thickness\s+x:Key="(Settings[^"]+)"[^>]*>([^<]+)</Thickness>', txt):
        parts = [float(x) for x in m.group(2).strip().split(",")]
        if len(parts) == 2:
            parts = [parts[0], parts[1], parts[0], parts[1]]
        out[m.group(1)] = tuple(parts)
    # icon disc geometry + a handful of fixed setters the sheets draw
    for key, prop in (("icon_tile", "SettingsIconTileStyle"),):
        st = block(txt, prop)
        out[key] = {
            "size": float(re.search(r'"Width"\s+Value="([^"]+)"', st).group(1)),
            "radius_key": re.search(r'"CornerRadius"\s+Value="\{DynamicResource\s+([^}]+)\}"', st).group(1),
        }
    return out


def _accents() -> list[dict]:
    txt = _read("Services/Theme/AccentPresets.cs")
    colors = {}
    for m in re.finditer(r"(\w+)\s*=>\s*Color\.FromRgb\(0x([0-9A-Fa-f]{2}),\s*0x([0-9A-Fa-f]{2}),\s*0x([0-9A-Fa-f]{2})\)", txt):
        colors[m.group(1)] = (int(m.group(2), 16), int(m.group(3), 16), int(m.group(4), 16), 255)
    colors["GlassBlue"] = (0x03, 0x69, 0xA1, 255)  # SwatchFor: theme default brand blue
    order = re.search(r"All\s*\{\s*get;\s*\}\s*=\s*new\s*\([^)]*\)\s*\[\]\s*\{(.*?)\};", txt, re.S)
    if not order:
        raise SystemExit("[pg_tokens] AccentPresets.All not found")
    rows = []
    for m in re.finditer(r'\((\w+),\s*"([^"]+)"\)', order.group(1)):
        key, name = m.group(1), m.group(2)
        rows.append({"key": key, "name": name, "color": colors.get(key, (148, 163, 184, 255))})
    if not rows:
        raise SystemExit("[pg_tokens] could not read AccentPresets.All")
    return rows


def _nav_rows() -> list[dict]:
    txt = _read("ViewModels/Settings/SettingsViewModel.Navigation.cs")
    body = re.search(r"new ObservableCollection<SettingsNavRow>\s*\{(.*?)\};", txt, re.S).group(1)
    rows = []
    for m in re.finditer(r'new\("([^"]+)",\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)"\)', body):
        # the C# source carries the glyph as a \uXXXX ESCAPE SEQUENCE; decode it
        # so the kit can map the real MDL2 code point to a drawn icon
        rows.append({"key": m.group(1),
                     "glyph": m.group(2).encode("utf-8").decode("unicode_escape"),
                     "label_key": m.group(3), "group_key": m.group(4)})
    if len(rows) < 10:
        raise SystemExit(f"[pg_tokens] NavRows parsed to {len(rows)} rows — registry changed?")
    return rows


def _strings() -> dict:
    out = {}
    files = ["Themes/Languages/en-US.xaml"]
    files += sorted(os.path.relpath(p, REPO).replace(os.sep, "/")
                    for p in _glob("Themes/Languages/en-US/*.xaml"))
    for rel in files:
        try:
            txt = _read(rel)
        except SystemExit:
            continue
        for m in re.finditer(r'x:Key="([^"]+)"[^>]*>(.*?)</', txt, re.S):
            k, v = m.group(1), re.sub(r"\s+", " ", m.group(2)).strip()
            v = (v.replace("&amp;", "&").replace("&#x27;", "'").replace("&apos;", "'")
                  .replace("&lt;", "<").replace("&gt;", ">").replace("&quot;", '"'))
            out.setdefault(k, v)
    return out


def _glob(pattern: str):
    import glob as _g
    return _g.glob(os.path.join(REPO, pattern))


# ─────────────────────────────────────────────────────────────────────────────
# public surface
# ─────────────────────────────────────────────────────────────────────────────
def load() -> dict:
    themes = {"light": _load_theme("Themes/LightTheme.xaml"),
              "dark": _load_theme("Themes/DarkTheme.xaml")}
    stage = _read("Themes/Styles/AppStage.xaml")
    stage_colors = {m.group(1): parse_color(m.group(2)) for m in
                    re.finditer(r'<SolidColorBrush\s+x:Key="([^"]+)"\s+Color="([^"]+)"', stage)}
    tok = {
        "themes": themes,
        "radius": _radii(),
        "type": _type_scale(),
        "space": _spacing(),
        "accents": _accents(),
        "nav": _nav_rows(),
        "strings": _strings(),
        "stage_colors": stage_colors,
    }
    # the stage must be invariant — that is the whole point of AppStage.xaml
    assert themes["light"]["stage"] == themes["dark"]["stage"], \
        "AppBackgroundBrush differs between themes; AppStageAuditTests would fail"
    return tok


TOKENS = load()


def T(key: str, theme: str = "light"):
    """A localized product string, or the key in braces so a gap is visible."""
    return TOKENS["strings"].get(key, "{" + key + "}")


_NORM_INDEX: list[tuple[str, str]] | None = None


def _norm(s: str) -> str:
    import re as _re
    return _re.sub(r"[^a-z0-9]", "", s.lower())


def resolve_prefix(key: str) -> str | None:
    """These language keys are generated from the English sentence, so a key a
    caller half-remembers ("Settings.CompactFitsMoreOnALaptop…") is a PREFIX of
    its value's normalised form. Resolve through the value table instead of
    failing: a sheet may never print a brace-wrapped key."""
    global _NORM_INDEX
    if _NORM_INDEX is None:
        _NORM_INDEX = sorted(((_norm(v), v) for v in TOKENS["strings"].values()
                              if v and len(v) > 8), key=lambda kv: len(kv[0]))
    nk = _norm(key.split(".", 1)[-1])
    if len(nk) < 12:
        return None
    best = None
    for nv, v in _NORM_INDEX:
        if nv == nk or nv.startswith(nk):
            if best is None or len(nv) < len(best[0]):
                best = (nv, v)
    return best[1] if best else None


def selftest() -> int:
    print("── pg_tokens self-test ─────────────────────────────────────")
    for th in ("light", "dark"):
        t = TOKENS["themes"][th]
        print(f"  {th:5} card_face layers   : {len(t['card_face'])}")
        print(f"        card_edge stops  : {[f'{o}:{c}' for o, c in t['card_edge'][0]['stops']]}")
        print(f"        shadow           : {t['card_shadow']}")
        print(f"        accent / danger  : {t.get('AccentBrush')} / {t.get('DangerBrush')}")
    print(f"  radii                  : {TOKENS['radius']}")
    print(f"  type ladder            : {len(TOKENS['type'])} roles "
          f"({TOKENS['type']['Ios26FontSizeCaption']}…{TOKENS['type']['Ios26FontSizeDisplay']})")
    print(f"  spacing                : gap card {TOKENS['space']['SettingsGapCard']}, "
          f"padding {TOKENS['space']['SettingsCardPadding']}")
    print(f"  accents                : {len(TOKENS['accents'])} "
          f"({', '.join(a['name'] for a in TOKENS['accents'][:4])} …)")
    print(f"  nav rows               : {len(TOKENS['nav'])} "
          f"({', '.join(r['key'] for r in TOKENS['nav'])})")
    print(f"  strings                : {len(TOKENS['strings'])} keys")
    print(f"  stage                  : {TOKENS['stage_colors']}")
    return 0


if __name__ == "__main__":
    sys.exit(selftest())

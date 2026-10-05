#!/usr/bin/env python3
# ═══════════════════════════════════════════════════════════════════════════
# FILE: scripts/mockups/pg_kit.py
# PROJECT: ProGlassAutomation
# MODULE: Settings redesign mockups — DRAWING KIT (dev-only tooling)
#
# WHAT THIS IS
# ────────────
# The renderer behind docs/mockups/settings-redesign-v3/*.png. It paints the
# Settings module's CRYSTAL LIQUID material exactly as the shipped tokens
# define it — the six-layer card (L0 tinted shadow · L1 tri-stop crystal edge ·
# L2 frosted base · L3 specular rim · L4 diagonal sheen · L5 refraction lip),
# the translucent inner panel, the About radius scale, the module type ladder
# and the grammar controls (segmented track, chip, value pill, 12 px buttons,
# icon disc, toggle, fill-bar slider, progress).
#
# Every number comes from pg_tokens.py, which reads it out of the live XAML/C#.
# Nothing here is a hand-typed colour: change Themes/LightTheme.xaml and the
# sheet changes with it.
#
# FONTS
# ─────
# The product runs "Segoe UI Variable, Segoe UI", which is not redistributable
# and is not on this box. The kit prefers Inter Variable (metrically the
# closest open substitute, and it carries the same three weights the module's
# type scale uses: Regular / SemiBold / Bold) and falls back to DejaVu Sans.
# Drop InterVariable.ttf in any of FONT_CANDIDATES — it is deliberately kept
# OUTSIDE the repository (no font binary in git).
# ═══════════════════════════════════════════════════════════════════════════
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pg_tokens import TOKENS  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

FONT_CANDIDATES = [
    os.environ.get("PG_MOCKUP_FONT", ""),
    os.path.expanduser("~/mockup-assets/InterVariable.ttf"),
    os.path.join(REPO, "scripts/mockups/assets/InterVariable.ttf"),
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
]


# ─────────────────────────────────────────────────────────────────────────────
# colour helpers
# ─────────────────────────────────────────────────────────────────────────────
def rgba(c, alpha=None):
    r, g, b, a = c
    if alpha is not None:
        a = int(round(alpha * 255))
    return (int(r), int(g), int(b), int(a))


def mix(c1, c2, t):
    """Straight-alpha composite of c2 over c1, both RGBA 0..255."""
    a2 = c2[3] / 255.0
    a1 = c1[3] / 255.0
    ao = a2 + a1 * (1 - a2)
    if ao <= 0:
        return (0, 0, 0, 0)
    out = []
    for i in range(3):
        out.append(int(round((c2[i] * a2 + c1[i] * a1 * (1 - a2)) / ao)))
    return (out[0], out[1], out[2], int(round(ao * 255)))


def with_alpha(c, a):
    return (c[0], c[1], c[2], int(round(c[3] * a)))


def hexs(c):
    return "#{:02X}{:02X}{:02X}".format(c[0], c[1], c[2]) + (
        "" if c[3] == 255 else " @{:.0%}".format(c[3] / 255))


def lum(c):
    return (0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) / 255.0


# ─────────────────────────────────────────────────────────────────────────────
# fonts
# ─────────────────────────────────────────────────────────────────────────────
class Fonts:
    """Inter Variable with the weight axis driven per role.

    The module's type scale (Themes/Styles/AboutTypeScale.xaml) allows exactly
    three weights — Regular / SemiBold / Bold — and bans fractional sizes, so
    the cache key is (whole px size, weight).
    """

    VARIATIONS = {"regular": 400, "semibold": 600, "bold": 700, "medium": 500}

    def __init__(self):
        self.path = next((p for p in FONT_CANDIDATES if p and os.path.exists(p)), None)
        if not self.path:
            raise SystemExit("[pg_kit] no usable font found; see FONT_CANDIDATES")
        self.variable = False
        probe = ImageFont.truetype(self.path, 20)
        try:
            axes = [a["name"] for a in probe.get_variation_axes()]
            self.variable = any(b"Weight" in a or "Weight" in str(a) for a in axes)
        except Exception:
            self.variable = False
        self._cache: dict[tuple[int, str], ImageFont.FreeTypeFont] = {}

    def get(self, size: float, weight: str = "regular") -> ImageFont.FreeTypeFont:
        size = max(6, int(round(size)))
        key = (size, weight)
        if key in self._cache:
            return self._cache[key]
        f = ImageFont.truetype(self.path, size)
        if self.variable:
            w = self.VARIATIONS.get(weight, 400)
            try:
                # axis order as reported: [Optical size, Weight]
                f.set_variation_by_axes([min(32, max(14, size)), w])
            except Exception:
                try:
                    f.set_variation_by_name(
                        {"regular": "Regular", "medium": "Medium",
                         "semibold": "SemiBold", "bold": "Bold"}[weight])
                except Exception:
                    pass
        else:  # DejaVu fallback: pick the bold face for SemiBold/Bold
            if weight in ("semibold", "bold"):
                alt = self.path.replace("DejaVuSans.ttf", "DejaVuSans-Bold.ttf")
                if os.path.exists(alt):
                    f = ImageFont.truetype(alt, size)
        self._cache[key] = f
        return f

    def measure(self, s: str, size: float, weight: str = "regular"):
        f = self.get(size, weight)
        l, t, r, b = f.getbbox(s)
        return (r - l, b - t, l, t)


FONTS = Fonts()


# ─────────────────────────────────────────────────────────────────────────────
# theme access
# ─────────────────────────────────────────────────────────────────────────────
class Theme:
    """Resolved view of one theme dictionary + the shared scale tables."""

    def __init__(self, name: str = "light"):
        self.name = name
        self.dark = name == "dark"
        self.t = TOKENS["themes"][name]
        self.radius = TOKENS["radius"]
        self.type = TOKENS["type"]
        self.space = TOKENS["space"]
        self.strings = TOKENS["strings"]
        self.accents = TOKENS["accents"]
        self.nav = TOKENS["nav"]

    # ── colours ──
    def c(self, key: str, fallback=(148, 163, 184, 255)):
        return self.t.get(key, fallback)

    @property
    def accent(self):
        # Ios26AccentBrush is the module's own accent token (#0369A1 / #38BDF8)
        return self.c("Ios26AccentBrush")

    @property
    def accent_deep(self):
        return self.c("Ios26AccentDeepBrush")

    @property
    def accent_soft(self):
        return self.c("Ios26AccentSoftBrush")

    @property
    def accent_soft_text(self):
        return self.c("Ios26AccentSoftTextBrush")

    @property
    def on_accent(self):
        return self.c("Ios26OnAccentBrush")

    @property
    def title(self):
        return self.c("Ios26TextTitleBrush")

    @property
    def body(self):
        return self.c("Ios26TextBodyBrush")

    @property
    def secondary(self):
        return self.c("Ios26TextSecondaryBrush")

    @property
    def muted(self):
        return self.c("Ios26TextMutedBrush")

    @property
    def faint(self):
        return self.c("Ios26TextFaintBrush")

    @property
    def hairline(self):
        return self.c("Ios26HairlineBrush")

    @property
    def hover(self):
        return self.c("Ios26HoverBrush")

    @property
    def pill(self):
        return self.c("Ios26ValuePillBrush")

    @property
    def success(self):
        return self.c("Ios26SuccessBrush")

    @property
    def success_soft(self):
        return self.c("Ios26SuccessSoftBrush")

    @property
    def danger(self):
        return self.c("DangerBrush", (220, 38, 38, 255))

    @property
    def danger_dot(self):
        return self.c("Ios26DangerDotBrush")

    @property
    def warning(self):
        return self.c("WarningColor", (217, 119, 6, 255)) or (217, 119, 6, 255)

    @property
    def tile(self):
        return self.c("Ios26TileBrush")

    @property
    def tile_edge(self):
        return self.c("Ios26TileBorderBrush")

    # ── geometry ──
    def r(self, key: str, default: float = 12.0) -> float:
        v = self.radius.get(key, default)
        return v if isinstance(v, (int, float)) else v[0]

    def gap(self, key: str) -> float:
        return self.space.get(key, 22.0)

    def pad(self, key: str):
        return self.space.get(key, (24, 22, 24, 22))

    def fs(self, role: str) -> float:
        return self.type.get(role, 13.0)

    #: page-level overrides for labels the product does not localise yet
    alias: dict[str, str] = {}

    def tr(self, key: str, default=None):
        v = self.strings.get(key) or self.alias.get(key)
        if v is None:
            from pg_tokens import resolve_prefix
            v = resolve_prefix(key)
        return v if v is not None else (default if default is not None else "{" + key + "}")


# type roles: (token, weight, colour attr) — transcribed from
# Views/Settings/SettingsCardStyles.xaml so a sheet cannot invent a size.
TYPE_ROLES = {
    "card_title":     ("Ios26FontSizeCardTitle", "bold", "title"),
    "card_note":      ("Ios26FontSizeSmall", "regular", "muted"),
    "page_title":     ("Ios26FontSizeH1", "bold", "title"),
    "page_subtitle":  ("Ios26FontSizeBody", "regular", "muted"),
    "group_caption":  ("Ios26FontSizeLabel", "semibold", "muted"),
    "field_label":    ("Ios26FontSizeBody", "regular", "muted"),
    "field_value":    ("Ios26FontSizeValue", "semibold", "title"),
    "row_label":      ("Ios26FontSizeValue", "semibold", "body"),
    "row_title":      ("Ios26FontSizeValue", "semibold", "body"),
    "row_meta":       ("Ios26FontSizeLabel", "regular", "faint"),
    "metric_value":   ("Ios26FontSizeH2", "bold", "title"),
    "metric_label":   ("Ios26FontSizeCaption", "semibold", "muted"),
    "chip":           ("Ios26FontSizeCaption", "semibold", "accent_soft_text"),
    "pill":           ("Ios26FontSizeSmall", "semibold", "secondary"),
    "button":         ("Ios26FontSizeBody", "semibold", "on_accent"),
    "stat":           ("Ios26FontSizeStat", "bold", "title"),
    "display":        ("Ios26FontSizeDisplay", "bold", "title"),
    "section_title":  ("Ios26FontSizeSectionTitle", "bold", "title"),
    "glyph":          ("Ios26FontSizeGlyph", "regular", "accent_soft_text"),
    "small":          ("Ios26FontSizeSmall", "regular", "secondary"),
    "label":          ("Ios26FontSizeLabel", "regular", "muted"),
    "caption":        ("Ios26FontSizeCaption", "semibold", "faint"),
}


# ─────────────────────────────────────────────────────────────────────────────
# canvas
# ─────────────────────────────────────────────────────────────────────────────
class Canvas:
    """Logical-coordinate RGBA canvas rendered at `scale` (2× by default)."""

    def __init__(self, w: float, h: float, bg=(10, 12, 16, 255), scale: int = 2):
        self.w, self.h, self.s = w, h, scale
        self.img = Image.new("RGBA", (int(w * scale), int(h * scale)), rgba(bg))
        self.d = ImageDraw.Draw(self.img)

    # logical → device
    def X(self, v):
        return v * self.s

    def box(self, b):
        return (b[0] * self.s, b[1] * self.s, b[2] * self.s, b[3] * self.s)

    def paste(self, layer: Image.Image, x: float, y: float):
        self.img.alpha_composite(layer, (int(round(x * self.s)), int(round(y * self.s))))

    def save(self, path: str):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.save(path, "PNG", optimize=True)
        return path


# ─────────────────────────────────────────────────────────────────────────────
# geometry: analytic rounded-rect mask (single sample, 1 px AA)
# ─────────────────────────────────────────────────────────────────────────────
def rounded_mask(w: int, h: int, r: float) -> Image.Image:
    """'L' mask, exact signed-distance AA. w/h in DEVICE px."""
    w, h = max(1, int(w)), max(1, int(h))
    r = float(min(max(r, 0.0), min(w, h) / 2.0))
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    xx = xx + 0.5
    yy = yy + 0.5
    cx, cy = w / 2.0, h / 2.0
    hw, hh = w / 2.0, h / 2.0
    qx = np.abs(xx - cx) - (hw - r)
    qy = np.abs(yy - cy) - (hh - r)
    outside = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2)
    inside = np.minimum(np.maximum(qx, qy), 0)
    d = outside + inside - r
    a = np.clip(0.5 - d, 0.0, 1.0)
    return Image.fromarray((a * 255).astype(np.uint8), "L")


def _mask_cache() -> dict:
    return _MASK_CACHE


_MASK_CACHE: dict[tuple[int, int, int], Image.Image] = {}


def cached_mask(w: int, h: int, r: float) -> Image.Image:
    key = (int(w), int(h), int(round(r * 4)))
    m = _MASK_CACHE.get(key)
    if m is None:
        m = rounded_mask(w, h, r / 4.0 * 1.0) if False else rounded_mask(w, h, key[2] / 4.0)
        if len(_MASK_CACHE) > 400:
            _MASK_CACHE.clear()
        _MASK_CACHE[key] = m
    return m


# ─────────────────────────────────────────────────────────────────────────────
# paint: gradient layers + masks
# ─────────────────────────────────────────────────────────────────────────────
def _gradient_array(w: int, h: int, layer: dict,
                    full_w: float | None = None, full_h: float | None = None,
                    ox: float = 0.0, oy: float = 0.0) -> np.ndarray:
    """RGBA float32 (0..1) array for a visible slice of one paint layer.

    `full_w/full_h` are the layer's OWN pixel size and `ox/oy` the slice origin
    inside it, so a layer whose fractional rect bleeds past the painted box (the
    stage orbs do) is sampled in its own coordinate space and clipped — the
    gradient never re-stretches to the visible part.
    """
    fw = float(full_w if full_w else w)
    fh = float(full_h if full_h else h)
    stops = layer["stops"]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (ox + xx + 0.5) / max(fw, 1e-6)
    v = (oy + yy + 0.5) / max(fh, 1e-6)
    if layer.get("radial"):
        cx, cy = layer.get("center", (0.5, 0.5))
        rx, ry = layer.get("radius", (0.5, 0.5))
        t = np.sqrt(((u - cx) / max(rx, 1e-6)) ** 2 + ((v - cy) / max(ry, 1e-6)) ** 2)
    else:
        sx, sy = layer.get("start", (0, 0))
        ex, ey = layer.get("end", (0, 1))
        dx, dy = ex - sx, ey - sy
        denom = dx * dx + dy * dy
        t = ((u - sx) * dx + (v - sy) * dy) / denom if denom > 1e-9 else np.zeros_like(u)
    hard = layer.get("hard")
    t_raw = t                      # the unclipped distance: the hard-edge
    t = np.clip(t, 0.0, 1.0)       # feather needs to see past the rim

    out = np.zeros((h, w, 4), dtype=np.float32)
    offs = np.array([s[0] for s in stops], dtype=np.float32)
    cols = np.array([[c / 255.0 for c in s[1]] for s in stops], dtype=np.float32)
    for ch in range(4):
        out[..., ch] = np.interp(t, offs, cols[:, ch])
    if hard:
        # 1 px AA feather on the disc's rim: alpha 1 inside, 0 outside
        edge = max(1.0, min(fw, fh) / 2.0)
        out[..., 3] *= np.clip(0.5 + (1.0 - t_raw) * edge, 0.0, 1.0)
    return out


def _over(dst: np.ndarray, src: np.ndarray) -> np.ndarray:
    sa = src[..., 3:4]
    da = dst[..., 3:4]
    oa = sa + da * (1 - sa)
    rgb = (src[..., :3] * sa + dst[..., :3] * da * (1 - sa))
    out = np.zeros_like(dst)
    nz = oa[..., 0] > 1e-6
    out[..., :3][nz] = rgb[nz] / oa[nz]
    out[..., 3:4] = oa
    return out


def paint_layers(w: int, h: int, layers: list[dict], mask: Image.Image | None = None) -> Image.Image:
    """Compose paint layers (each with a fractional rect) into an RGBA image."""
    w, h = max(1, int(w)), max(1, int(h))
    acc = np.zeros((h, w, 4), dtype=np.float32)
    for layer in layers:
        x0, y0, x1, y1 = layer.get("rect", (0, 0, 1, 1))
        lx, ly = x0 * w, y0 * h
        lw, lh = max(1.0, (x1 - x0) * w), max(1.0, (y1 - y0) * h)
        dx0, dy0 = max(0, int(round(lx))), max(0, int(round(ly)))
        dx1, dy1 = min(w, int(round(lx + lw))), min(h, int(round(ly + lh)))
        if dx1 <= dx0 or dy1 <= dy0:
            continue
        sub = _gradient_array(dx1 - dx0, dy1 - dy0, layer,
                              full_w=lw, full_h=lh, ox=dx0 - lx, oy=dy0 - ly)
        acc[dy0:dy1, dx0:dx1] = _over(acc[dy0:dy1, dx0:dx1], sub)
    img = Image.fromarray((np.clip(acc, 0, 1) * 255).astype(np.uint8), "RGBA")
    if mask is not None:
        if mask.size != img.size:
            mask = mask.resize(img.size, Image.BILINEAR)
        a = np.asarray(img).astype(np.float32)
        a[..., 3] *= np.asarray(mask).astype(np.float32) / 255.0
        img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")
    return img


def shadow_layer(w: int, h: int, r: float, spec: dict, scale: int, strength: float = 1.0,
                 inset: float = 0.0) -> tuple[Image.Image, int, int]:
    """WPF DropShadowEffect → blurred RGBA layer + its (dx, dy) origin offset.

    Returns (image, paste_dx_logical, paste_dy_logical) sized w+2*pad × h+2*pad.
    """
    blur = spec["blur"] * scale
    depth = spec["depth"] * scale
    direction = math.radians(spec.get("direction", 90.0))
    pad = int(math.ceil(blur * 1.6 + abs(depth)))
    sw, sh = int(w + 2 * pad), int(h + 2 * pad)
    layer = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    m = cached_mask(int(w), int(h), r)
    col = spec["color"]
    a = int(round(spec["opacity"] * 255 * strength))
    tinted = Image.new("RGBA", (int(w), int(h)), rgba(col, a / 255.0))
    tinted.putalpha(m.point(lambda p: int(p * a / 255)))
    layer.alpha_composite(tinted, (pad, pad))
    layer = layer.filter(ImageFilter.GaussianBlur(radius=max(0.6, blur / 2.0)))
    dx = math.cos(direction) * depth
    dy = math.sin(direction) * depth
    # device → logical offset of the layer's top-left corner
    ox = (pad - dx) / scale + inset
    oy = (pad - dy) / scale + inset
    return layer, ox, oy


def stroke_ring(w: int, h: int, r: float, thickness: float, layers: list[dict]) -> Image.Image:
    """A gradient border: outer rounded mask minus the inset one."""
    outer = np.asarray(cached_mask(int(w), int(h), r)).astype(np.float32) / 255.0
    iw, ih = int(w - 2 * thickness), int(h - 2 * thickness)
    if iw <= 2 or ih <= 2:
        inner = np.zeros_like(outer)
    else:
        inner = np.asarray(cached_mask(iw, ih, max(0.0, r - thickness))).astype(np.float32) / 255.0
        pad = np.zeros_like(outer)
        pad[int(thickness):int(thickness) + ih, int(thickness):int(thickness) + iw] = inner
        inner = pad
    ring = np.clip(outer - inner, 0, 1)
    full = paint_layers(int(w), int(h), layers)
    a = np.asarray(full).astype(np.float32)
    a[..., 3] = a[..., 3] * ring * 255.0
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


# ─────────────────────────────────────────────────────────────────────────────
# surfaces
# ─────────────────────────────────────────────────────────────────────────────
def stage(cv: Canvas, box, th: Theme, dim: float = 0.0):
    """The invariant About stage: pastel canvas + four ambient orbs.

    Painted from AppBackgroundBrush (identical in both theme dictionaries —
    AppStageAuditTests pins the two blocks byte-for-byte), so a Light/Dark
    switch never re-tints the page background.
    """
    x0, y0, x1, y1 = box
    w, h = int((x1 - x0) * cv.s), int((y1 - y0) * cv.s)
    layers = []
    for l in th.t["stage"]:
        if l["kind"] == "rect":
            layers.append({"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, l["color"]), (1, l["color"])]})
        else:
            cx, cy, rx, ry = l["cx"], l["cy"], l["rx"], l["ry"]
            # the product paints hard ellipses (AboutView's four <Ellipse> fills
            # and AppBackgroundBrush's EllipseGeometry layers) — 'hard' keeps the
            # disc's edge crisp with a 1 px AA feather instead of a soft falloff
            layers.append({"rect": (cx - rx, cy - ry, cx + rx, cy + ry),
                           "radial": True, "hard": True,
                           "center": (0.5, 0.5), "radius": (0.5, 0.5),
                           "stops": [(0, l["color"]), (1, l["color"])]})
    img = paint_layers(w, h, layers)
    if dim:
        veil = Image.new("RGBA", (w, h), rgba((6, 10, 20, 255), dim))
        img = Image.alpha_composite(img, veil)
    cv.paste(img, x0, y0)
    return img


def card(cv: Canvas, box, th: Theme, kind: str = "page", radius: float | None = None,
         shadow: bool = True, shadow_strength: float = 1.0) -> tuple:
    """The module's page card (SettingsPageCardStyle) and its variants.

    kind: page · flat · danger · inner · interactive(hover) · info · success · wash
    Returns the box actually painted.
    """
    x0, y0, x1, y1 = box
    w, h = int(round((x1 - x0) * cv.s)), int(round((y1 - y0) * cv.s))
    if w < 2 or h < 2:
        return box
    if kind == "inner":
        r = (radius if radius is not None else th.r("Ios26RadiusInner")) * cv.s
        face, edge = th.t["inner_face"], th.t["inner_edge"]
        shadow = False
    else:
        r = (radius if radius is not None else th.r("Ios26RadiusCard")) * cv.s
        face, edge = th.t["card_face"], th.t["card_edge"]
        if kind == "flat":
            shadow = False
            face = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.tile), (1, th.tile)]}]
            edge = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.tile_edge), (1, th.tile_edge)]}]
        elif kind == "danger":
            face = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.tile), (1, th.tile)]}]
            edge = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.danger), (1, th.danger)]}]
        elif kind == "info":
            face = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.accent_soft), (1, th.accent_soft)]}]
            edge = face
            shadow = False
        elif kind == "success":
            face = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.success_soft), (1, th.success_soft)]}]
            edge = face
            shadow = False
        elif kind == "wash":
            face = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.pill), (1, th.pill)]}]
            edge = face
            shadow = False
        elif kind == "hover":
            face = [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                     "stops": [(0, th.hover), (1, th.hover)]}]
            edge = th.t["inner_edge"]
            shadow = False

    if shadow:
        # L0 — the tinted drop shadow, painted BEFORE the face so the card sits
        # on it. ox/oy are the layer origin relative to the card box.
        sl, ox, oy = shadow_layer(w, h, r, th.t["card_shadow"], cv.s, shadow_strength)
        cv.paste(sl, x0 - ox, y0 - oy)
    mask = cached_mask(w, h, r)
    cv.paste(paint_layers(w, h, face, mask), x0, y0)
    cv.paste(stroke_ring(w, h, r, max(1.0, 1.0 * cv.s), edge), x0, y0)
    return box


def inner(cv: Canvas, box, th: Theme, kind: str = "inner", radius: float | None = None):
    return card(cv, box, th, kind=kind, radius=radius)


# ─────────────────────────────────────────────────────────────────────────────
# text
# ─────────────────────────────────────────────────────────────────────────────
def text(cv: Canvas, xy, s: str, th: Theme, role: str = "small", color=None,
         anchor: str = "lt", size: float | None = None, weight: str | None = None,
         maxw: float | None = None, ellipsis: bool = True, tracking: float = 0.0,
         line_height: float = 1.28, wrap: bool = False):
    """Draw text in a grammar role. Returns (width, height) in logical px."""
    tok, w_default, cattr = TYPE_ROLES[role]
    size = size if size is not None else th.fs(tok)
    weight = weight or w_default
    color = color if color is not None else getattr(th, cattr)
    size_d = size * cv.s
    f = FONTS.get(size_d, weight)
    if maxw and ellipsis and not wrap:
        maxw_d = maxw * cv.s
        while s and f.getlength(s) > maxw_d:
            s = s[:-2] if len(s) > 2 else s[:-1]
            s = s.rstrip() + "…"
            if len(s) < 3:
                break
    if wrap and maxw:
        lines = _wrap(s, f, maxw * cv.s)
    else:
        lines = [s]
    lh = size * line_height
    total_h = lh * len(lines)
    # anchor = <horizontal><vertical>: h in l|m|r, v in t|m|b (default "lt").
    # Placement is by the LINE BOX top-left, so a role's rhythm is identical
    # everywhere on a sheet.
    ax, ay = xy
    hz = anchor[0]
    vt = anchor[1] if len(anchor) > 1 else "t"
    ascent, descent = f.getmetrics()
    natural = (ascent + descent) / cv.s          # the glyph's own line box
    pad_box = (lh - natural) / 2.0               # centre it in the role's box
    block_top = ay - (0.0 if vt == "t" else total_h / 2 if vt == "m" else total_h)
    for i, line in enumerate(lines):
        lw = f.getlength(line) / cv.s
        dx = ax - (lw / 2 if hz == "m" else lw if hz == "r" else 0)
        dy = block_top + i * lh + pad_box
        if tracking:
            _tracked(cv, line, (dx, dy), f, color, tracking * cv.s)
        else:
            cv.d.text((dx * cv.s, dy * cv.s), line, font=f, fill=rgba(color),
                      anchor="la")
    return (max((f.getlength(l) for l in lines), default=0) / cv.s, total_h)


def _wrap(s: str, f, maxw: float) -> list[str]:
    words, lines, cur = s.split(), [], ""
    for w in words:
        trial = (cur + " " + w).strip()
        if f.getlength(trial) <= maxw or not cur:
            cur = trial
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def _tracked(cv: Canvas, s: str, xy, f, color, track: float):
    x, y = xy[0] * cv.s, xy[1] * cv.s
    for ch in s:
        cv.d.text((x, y), ch, font=f, fill=rgba(color), anchor="la")
        x += f.getlength(ch) + track


def measure(s: str, th: Theme, role: str = "small", size: float | None = None,
            weight: str | None = None, scale: int = 2) -> tuple[float, float]:
    tok, w_default, _ = TYPE_ROLES[role]
    size = size if size is not None else th.fs(tok)
    weight = weight or w_default
    f = FONTS.get(size * scale, weight)
    return (f.getlength(s) / scale, size * 1.28)


def text_w(s: str, th: Theme, role: str = "small", size=None, weight=None, scale=2) -> float:
    return measure(s, th, role, size, weight, scale)[0]


# ─────────────────────────────────────────────────────────────────────────────
# icons — 24×24 unit line set (the MDL2 code points the module binds, drawn as
# vectors so a sheet never shows a missing-glyph box)
# ─────────────────────────────────────────────────────────────────────────────
class Pen:
    def __init__(self, cv: Canvas, box, color, width: float = 1.7, fill=None):
        x0, y0, x1, y1 = box
        self.cv, self.x0, self.y0 = cv, x0, y0
        self.k = (x1 - x0) / 24.0
        self.color = color
        self.width = width * self.k * cv.s
        self.fill = fill

    def p(self, x, y):
        # the canvas draws in DEVICE pixels: unit grid → device space
        return ((self.x0 + x * self.k) * self.cv.s,
                (self.y0 + y * self.k) * self.cv.s)

    def line(self, a, b):
        self.cv.d.line([self.p(*a), self.p(*b)], fill=rgba(self.color),
                       width=max(1, int(round(self.width))), joint="curve")

    def poly(self, pts, closed=False):
        pp = [self.p(*q) for q in pts]
        self.cv.d.line(pp + ([pp[0]] if closed else []), fill=rgba(self.color),
                       width=max(1, int(round(self.width))), joint="curve")

    def circle(self, c, r, fill=None):
        (cx, cy) = self.p(*c)
        rr = r * self.k * self.cv.s
        box = (cx - rr, cy - rr, cx + rr, cy + rr)
        if fill:
            self.cv.d.ellipse(box, fill=rgba(fill))
        else:
            self.cv.d.ellipse(box, outline=rgba(self.color),
                              width=max(1, int(round(self.width))))

    def rrect(self, x, y, w, h, r, fill=None):
        (ax, ay) = self.p(x, y)
        (bx, by) = self.p(x + w, y + h)
        rr = r * self.k
        if fill:
            self.cv.d.rounded_rectangle((ax, ay, bx, by), radius=rr, fill=rgba(fill))
        else:
            self.cv.d.rounded_rectangle((ax, ay, bx, by), radius=rr,
                                        outline=rgba(self.color),
                                        width=max(1, int(round(self.width))))

    def ellipse(self, x0, y0, x1, y1, fill=None):
        a = self.p(x0, y0)
        b = self.p(x1, y1)
        if fill:
            self.cv.d.ellipse([a, b], fill=rgba(fill))
        else:
            self.cv.d.ellipse([a, b], outline=rgba(self.color),
                              width=max(1, int(round(self.width))))

    def arc(self, c, r, a0, a1, width=None):
        (cx, cy) = self.p(*c)
        rr = r * self.k * self.cv.s
        self.cv.d.arc((cx - rr, cy - rr, cx + rr, cy + rr), start=a0, end=a1,
                      fill=rgba(self.color),
                      width=max(1, int(round(width or self.width))))


def icon(cv: Canvas, box, name: str, color, width: float = 1.7):
    pen = Pen(cv, box, color, width)
    fn = ICONS.get(name)
    if fn is None:
        # unknown glyph → a neutral dot so the omission is visible, not a box
        pen.circle((12, 12), 3.2)
        return
    fn(pen)


def _i_gear(p):
    p.circle((12, 12), 3.1)
    p.circle((12, 12), 6.4)
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        p.line((12 + math.cos(a) * 6.4, 12 + math.sin(a) * 6.4),
               (12 + math.cos(a) * 8.8, 12 + math.sin(a) * 8.8))


def _i_palette(p):
    p.arc((12, 12), 8.2, 120, 420)
    p.line((6.2, 17.6), (10.5, 17.6))
    p.circle((9.0, 8.6), 1.15, fill=p.color)
    p.circle((14.4, 7.6), 1.15, fill=p.color)
    p.circle((17.4, 12.2), 1.15, fill=p.color)


def _i_sparkle(p):
    p.poly([(12, 3.4), (13.9, 9.4), (20, 11.4), (13.9, 13.4), (12, 19.6),
            (10.1, 13.4), (4, 11.4), (10.1, 9.4)], closed=True)
    p.line((18.4, 17.2), (18.4, 20.6))
    p.line((16.7, 18.9), (20.1, 18.9))


def _i_robot(p):
    p.rrect(4.6, 7.6, 14.8, 11.0, 3.0)
    p.line((12, 4.0), (12, 7.4))
    p.circle((12, 3.4), 1.1, fill=p.color)
    p.circle((9.2, 12.4), 1.15, fill=p.color)
    p.circle((14.8, 12.4), 1.15, fill=p.color)
    p.line((9.8, 15.8), (14.2, 15.8))
    p.line((2.6, 11.4), (4.4, 11.4))
    p.line((19.6, 11.4), (21.4, 11.4))


def _i_mic(p):
    p.rrect(9.4, 2.8, 5.2, 11.0, 2.6)
    p.arc((12, 12.4), 6.6, 20, 160)
    p.line((12, 19.0), (12, 21.4))
    p.line((9.0, 21.4), (15.0, 21.4))


def _i_sync(p):
    p.arc((12, 12), 7.4, 300, 190)
    p.poly([(19.2, 8.2), (19.4, 12.4), (15.4, 10.6)])
    p.arc((12, 12), 7.4, 120, 10)
    p.poly([(4.8, 15.8), (4.6, 11.6), (8.6, 13.4)])


def _i_download(p):
    p.line((12, 3.6), (12, 14.8))
    p.poly([(7.8, 11.0), (12, 15.4), (16.2, 11.0)])
    p.poly([(4.4, 17.6), (4.4, 20.2), (19.6, 20.2), (19.6, 17.6)])


def _i_database(p):
    p.ellipse(4.2, 3.4, 19.8, 9.0)
    p.line((4.2, 6.2), (4.2, 17.8))
    p.line((19.8, 6.2), (19.8, 17.8))
    p.arc((12, 6.2), 7.8, 20, 160)
    p.ellipse(4.2, 15.0, 19.8, 20.6)
    p.arc((12, 11.4), 7.8, 20, 160)


def _i_film(p):
    p.rrect(3.2, 5.0, 17.6, 14.0, 2.4)
    p.line((7.6, 5.0), (7.6, 19.0))
    p.line((16.4, 5.0), (16.4, 19.0))
    p.line((3.2, 12.0), (20.8, 12.0))


def _i_key(p):
    p.circle((8.0, 12.0), 4.0)
    p.line((12.0, 12.0), (20.6, 12.0))
    p.line((17.4, 12.0), (17.4, 15.2))
    p.line((20.0, 12.0), (20.0, 14.6))


def _i_flag(p):
    p.line((5.6, 3.4), (5.6, 20.8))
    p.poly([(5.6, 4.6), (18.6, 4.6), (16.0, 8.6), (18.6, 12.6), (5.6, 12.6)], closed=True)


def _i_share(p):
    p.circle((6.2, 12.0), 2.6)
    p.circle((17.6, 6.0), 2.6)
    p.circle((17.6, 18.0), 2.6)
    p.line((8.6, 10.8), (15.2, 7.2))
    p.line((8.6, 13.2), (15.2, 16.8))


def _i_info(p):
    p.circle((12, 12), 8.4)
    p.line((12, 11.0), (12, 16.6))
    p.circle((12, 8.2), 0.95, fill=p.color)


def _i_search(p):
    p.circle((10.8, 10.8), 6.0)
    p.line((15.2, 15.2), (20.0, 20.0))


def _i_sun(p):
    p.circle((12, 12), 4.2)
    for i in range(8):
        a = math.radians(i * 45)
        p.line((12 + math.cos(a) * 6.4, 12 + math.sin(a) * 6.4),
               (12 + math.cos(a) * 8.8, 12 + math.sin(a) * 8.8))


def _i_moon(p):
    p.arc((12, 12), 8.0, 60, 300)
    p.arc((15.6, 9.6), 6.4, 200, 340)


def _i_chevron_r(p):
    p.poly([(9.4, 5.6), (16.0, 12.0), (9.4, 18.4)])


def _i_chevron_d(p):
    p.poly([(5.6, 9.4), (12.0, 16.0), (18.4, 9.4)])


def _i_check(p):
    p.poly([(4.8, 12.6), (9.8, 17.6), (19.2, 6.8)])


def _i_close(p):
    p.line((6.2, 6.2), (17.8, 17.8))
    p.line((17.8, 6.2), (6.2, 17.8))


def _i_folder(p):
    p.poly([(3.4, 6.6), (9.6, 6.6), (11.4, 9.0), (20.6, 9.0), (20.6, 18.6), (3.4, 18.6)],
           closed=True)


def _i_copy(p):
    p.rrect(8.2, 3.6, 12.2, 12.6, 2.2)
    p.poly([(15.8, 19.0), (5.8, 19.0), (5.8, 8.2)])


def _i_play(p):
    p.poly([(7.6, 4.8), (19.4, 12.0), (7.6, 19.2)], closed=True)


def _i_trash(p):
    p.line((4.6, 7.0), (19.4, 7.0))
    p.poly([(9.2, 7.0), (9.2, 4.4), (14.8, 4.4), (14.8, 7.0)])
    p.poly([(6.6, 7.0), (7.8, 20.2), (16.2, 20.2), (17.4, 7.0)])
    p.line((10.4, 10.4), (10.8, 17.0))
    p.line((13.6, 10.4), (13.2, 17.0))


def _i_cloud(p):
    p.arc((9.0, 13.4), 4.4, 120, 300)
    p.arc((14.6, 12.4), 5.2, 220, 40)
    p.line((5.2, 17.6), (18.8, 17.6))


def _i_shield(p):
    p.poly([(12, 3.2), (19.4, 6.2), (19.4, 12.4), (12, 20.8), (4.6, 12.4), (4.6, 6.2)],
           closed=True)
    p.poly([(8.8, 11.8), (11.4, 14.4), (15.6, 9.6)])


def _i_wifi(p):
    p.arc((12, 18.4), 12.6, 225, 315)
    p.arc((12, 18.4), 8.6, 225, 315)
    p.arc((12, 18.4), 4.6, 225, 315)
    p.circle((12, 18.4), 1.1, fill=p.color)


def _i_sliders(p):
    p.line((4.4, 8.0), (19.6, 8.0))
    p.line((4.4, 16.0), (19.6, 16.0))
    p.circle((9.4, 8.0), 2.3, fill=(0, 0, 0, 0))
    p.circle((15.0, 16.0), 2.3)


def _i_type(p):
    p.line((5.0, 6.4), (13.6, 6.4))
    p.line((9.3, 6.4), (9.3, 19.2))
    p.line((6.6, 19.2), (12.0, 19.2))
    p.line((15.6, 12.4), (20.6, 12.4))
    p.line((18.1, 12.4), (18.1, 19.2))


def _i_image(p):
    p.rrect(3.4, 5.0, 17.2, 14.0, 2.2)
    p.circle((8.4, 9.8), 1.6)
    p.poly([(4.0, 17.4), (10.0, 12.4), (14.0, 16.0), (16.8, 13.6), (20.2, 17.4)])


def _i_globe(p):
    p.circle((12, 12), 8.4)
    p.ellipse(7.4, 3.6, 16.6, 20.4)
    p.line((3.6, 12.0), (20.4, 12.0))


def _i_bell(p):
    p.arc((12, 11.0), 6.4, 180, 360)
    p.line((5.6, 11.0), (5.6, 15.4))
    p.line((18.4, 11.0), (18.4, 15.4))
    p.line((4.0, 16.2), (20.0, 16.2))
    p.arc((12, 17.6), 2.2, 20, 160)


def _i_lock(p):
    p.rrect(5.4, 10.4, 13.2, 9.6, 2.4)
    p.arc((12, 10.4), 4.2, 180, 360)
    p.circle((12, 15.0), 1.2, fill=p.color)


def _i_plus(p):
    p.line((12, 5.2), (12, 18.8))
    p.line((5.2, 12), (18.8, 12))


def _i_minus(p):
    p.line((5.2, 12), (18.8, 12))


def _i_home(p):
    p.poly([(3.8, 11.2), (12, 4.2), (20.2, 11.2)])
    p.poly([(6.2, 10.0), (6.2, 19.8), (17.8, 19.8), (17.8, 10.0)])
    p.rrect(10.2, 14.0, 3.6, 5.8, 0.8)


def _i_chart(p):
    p.line((4.2, 19.4), (20.0, 19.4))
    p.rrect(6.0, 12.0, 3.0, 6.4, 0.9)
    p.rrect(10.6, 8.0, 3.0, 10.4, 0.9)
    p.rrect(15.2, 4.6, 3.0, 13.8, 0.9)


def _i_cpu(p):
    p.rrect(6.4, 6.4, 11.2, 11.2, 2.0)
    p.rrect(9.6, 9.6, 4.8, 4.8, 1.0)
    for x in (9.2, 12.0, 14.8):
        p.line((x, 3.6), (x, 6.2))
        p.line((x, 17.8), (x, 20.4))
    for y in (9.2, 12.0, 14.8):
        p.line((3.6, y), (6.2, y))
        p.line((17.8, y), (20.4, y))


def _i_layers(p):
    p.poly([(12, 3.6), (20.6, 8.2), (12, 12.8), (3.4, 8.2)], closed=True)
    p.poly([(3.4, 12.6), (12, 17.2), (20.6, 12.6)])
    p.poly([(3.4, 16.6), (12, 21.2), (20.6, 16.6)])


def _i_book(p):
    p.poly([(4.6, 4.4), (12.0, 6.4), (19.4, 4.4), (19.4, 17.6), (12.0, 19.6), (4.6, 17.6)],
           closed=True)
    p.line((12.0, 6.4), (12.0, 19.6))


def _i_users(p):
    p.circle((9.4, 8.6), 3.2)
    p.arc((9.4, 19.6), 6.0, 190, 350)
    p.arc((16.8, 9.6), 2.6, 220, 340)
    p.arc((17.6, 19.4), 4.6, 230, 330)


def _i_tag(p):
    p.poly([(4.2, 11.6), (11.6, 4.2), (20.0, 4.2), (20.0, 12.4), (12.4, 20.0)], closed=True)
    p.circle((16.2, 8.0), 1.4, fill=p.color)


def _i_grid(p):
    for (x, y) in ((4.2, 4.2), (12.6, 4.2), (4.2, 12.6), (12.6, 12.6)):
        p.rrect(x, y, 7.2, 7.2, 1.8)


def _i_list(p):
    for y in (6.4, 12.0, 17.6):
        p.circle((5.4, y), 1.1, fill=p.color)
        p.line((9.0, y), (19.6, y))


def _i_star(p):
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = 8.6 if i % 2 == 0 else 3.8
        pts.append((12 + math.cos(a) * r, 12 + math.sin(a) * r))
    p.poly(pts, closed=True)


def _i_alert(p):
    p.poly([(12, 3.8), (21.0, 19.6), (3.0, 19.6)], closed=True)
    p.line((12, 9.6), (12, 14.2))
    p.circle((12, 16.8), 0.95, fill=p.color)


def _i_wand(p):
    p.line((4.6, 19.4), (15.4, 8.6))
    p.poly([(17.2, 3.6), (18.4, 6.4), (21.2, 7.6), (18.4, 8.8), (17.2, 11.6),
            (16.0, 8.8), (13.2, 7.6), (16.0, 6.4)], closed=True)


def _i_eye(p):
    p.poly([(2.6, 12.0), (12.0, 5.6), (21.4, 12.0), (12.0, 18.4)], closed=True)
    p.circle((12, 12), 2.8)


def _i_terminal(p):
    p.rrect(3.2, 4.8, 17.6, 14.4, 2.2)
    p.poly([(7.2, 9.6), (10.2, 12.0), (7.2, 14.4)])
    p.line((12.4, 14.8), (16.8, 14.8))


def _i_pin(p):
    p.poly([(12, 21.4), (12, 13.6)])
    p.circle((12, 9.4), 5.2)
    p.circle((12, 9.4), 1.6, fill=p.color)


def _i_clock(p):
    p.circle((12, 12), 8.4)
    p.line((12, 7.4), (12, 12.2))
    p.line((12, 12.2), (15.6, 14.2))


def _i_refresh(p):
    p.arc((12, 12), 7.4, 320, 220)
    p.poly([(5.4, 8.0), (5.0, 12.4), (9.2, 10.4)])
    p.line((12, 12), (12, 12))


def _i_menu(p):
    for y in (7.0, 12.0, 17.0):
        p.line((4.2, y), (19.8, y))


def _i_arrow_up(p):
    p.line((12, 19.6), (12, 5.4))
    p.poly([(6.6, 10.6), (12, 5.0), (17.4, 10.6)])


def _i_arrow_right(p):
    p.line((4.6, 12), (19.0, 12))
    p.poly([(13.8, 6.8), (19.4, 12), (13.8, 17.2)])


def _i_wave(p):  # audio waveform
    for x, h in ((5.4, 3.0), (8.4, 6.2), (11.4, 8.6), (14.4, 5.2), (17.4, 7.2), (20.0, 3.4)):
        p.line((x, 12 - h), (x, 12 + h))


def _i_crop(p):
    p.line((6.6, 3.4), (6.6, 17.4))
    p.line((6.6, 17.4), (20.6, 17.4))
    p.line((3.4, 6.6), (17.4, 6.6))
    p.line((17.4, 6.6), (17.4, 20.6))


def _i_building(p):
    p.rrect(4.4, 4.0, 8.6, 16.2, 1.4)
    p.rrect(13.0, 8.4, 6.6, 11.8, 1.4)
    for y in (7.2, 10.4, 13.6, 16.8):
        p.line((6.4, y), (11.0, y))
    for y in (11.6, 14.8, 18.0):
        p.line((14.8, y), (17.8, y))


def _i_doc(p):
    p.poly([(6.0, 3.4), (14.6, 3.4), (18.4, 7.2), (18.4, 20.6), (6.0, 20.6)], closed=True)
    p.poly([(14.4, 3.6), (14.4, 7.4), (18.2, 7.4)])
    for y in (11.4, 14.4, 17.4):
        p.line((8.8, y), (15.6, y))


ICONS = {
    "gear": _i_gear, "palette": _i_palette, "sparkle": _i_sparkle, "robot": _i_robot,
    "mic": _i_mic, "sync": _i_sync, "download": _i_download, "database": _i_database,
    "film": _i_film, "key": _i_key, "flag": _i_flag, "share": _i_share, "info": _i_info,
    "search": _i_search, "sun": _i_sun, "moon": _i_moon, "chevron_r": _i_chevron_r,
    "chevron_d": _i_chevron_d, "check": _i_check, "close": _i_close, "folder": _i_folder,
    "copy": _i_copy, "play": _i_play, "trash": _i_trash, "cloud": _i_cloud,
    "shield": _i_shield, "wifi": _i_wifi, "sliders": _i_sliders, "type": _i_type,
    "image": _i_image, "globe": _i_globe, "bell": _i_bell, "lock": _i_lock,
    "plus": _i_plus, "minus": _i_minus, "home": _i_home, "chart": _i_chart,
    "cpu": _i_cpu, "layers": _i_layers, "book": _i_book, "users": _i_users,
    "tag": _i_tag, "grid": _i_grid, "list": _i_list, "star": _i_star,
    "alert": _i_alert, "wand": _i_wand, "eye": _i_eye, "terminal": _i_terminal,
    "pin": _i_pin, "clock": _i_clock, "refresh": _i_refresh, "menu": _i_menu,
    "arrow_up": _i_arrow_up, "arrow_right": _i_arrow_right, "wave": _i_wave,
    "crop": _i_crop, "building": _i_building, "doc": _i_doc,
}

# the rail's MDL2 code points (SettingsViewModel.NavRows) → kit icon names
GLYPH_ICON = {
    "E790": "palette", "E792": "layers", "E8F2": "sparkle", "E9F5": "robot",
    "E720": "mic", "E72C": "sync", "E74E": "database", "E714": "film",
    "E762": "key", "E912": "flag", "E72D": "share", "E946": "info",
    "E713": "gear", "E700": "menu", "E76B": "chevron_r", "E76C": "chevron_r",
    "E708": "moon", "E706": "sun", "E99A": "robot", "E8F1": "search",
    "E72E": "copy", "E8E5": "folder", "E74D": "share", "E768": "play",
    "E74D ": "share", "E896": "trash", "E753": "cloud", "E72B": "download",
    "E71E": "lock", "E9D5": "eye", "E9D9": "sliders", "E7C3": "chevron_d",
    "E73E": "close", "E73E ": "close", "E8FB": "check", "E922": "book",
}


def glyph_icon(glyph: str) -> str:
    """MDL2 code point -> kit icon name (the rail's glyphs come from NavRows)."""
    code = ""
    for ch in glyph:
        if ord(ch) > 0xE000:
            code = "{:04X}".format(ord(ch))
    return GLYPH_ICON.get(code, "gear")


# ─────────────────────────────────────────────────────────────────────────────
# grammar controls
# ─────────────────────────────────────────────────────────────────────────────
def icon_disc(cv: Canvas, xy, name: str, th: Theme, size: float | None = None,
              bg=None, fg=None, radius=None):
    """SettingsIconTileStyle + SettingsIconGlyphStyle: a 34 px accent-soft disc
    carrying a 19 px glyph. Discs belong to ROWS and to the shell rail — never
    to a card heading (redesign rule 2)."""
    size = size or th.space["icon_tile"]["size"]
    r = (radius if radius is not None
         else th.r(th.space["icon_tile"]["radius_key"], 10))
    bg = th.accent_soft if bg is None else bg
    fg = th.accent_soft_text if fg is None else fg
    x, y = xy
    w = h = int(round(size * cv.s))
    cv.paste(paint_layers(w, h, [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                  "stops": [(0, bg), (1, bg)]}],
                          cached_mask(w, h, r * cv.s)), x, y)
    pad = size * 0.235
    icon(cv, (x + pad, y + pad, x + size - pad, y + size - pad), name, fg,
         width=1.5 + 0.55 * (size / 34.0))
    return size


def chip(cv: Canvas, xy, label: str, th: Theme, variant: str = "accent",
         dot: str | None = None, size: float | None = None):
    """SettingsChipStyle: radius 12, padding 9,3, 10 px SemiBold caption."""
    x, y = xy
    pad_x, pad_y = 9.0, 3.0
    fs = th.fs("Ios26FontSizeCaption")
    tw = text_w(label, th, "chip", scale=cv.s)
    dot_w = (7 + 6) if dot else 0
    h = fs * 1.28 + pad_y * 2
    w = tw + pad_x * 2 + dot_w
    bg, fg = {
        "accent": (th.accent_soft, th.accent_soft_text),
        "green": (th.success_soft, th.success),
        "muted": (th.pill, th.muted),
        "danger": (th.pill, th.danger_dot),
        "warning": (with_alpha(th.c("WarningBrush", (217, 119, 6, 255)), 0.16),
                    th.c("WarningBrush", (180, 83, 9, 255))),
        "on_accent": (with_alpha(th.on_accent, 0.22), th.on_accent),
    }[variant]
    r = th.r("Ios26RadiusChip")
    layer = paint_layers(int(w * cv.s), int(h * cv.s),
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, bg), (1, bg)]}],
                         cached_mask(int(w * cv.s), int(h * cv.s), r * cv.s))
    cv.paste(layer, x, y)
    tx = x + pad_x
    if dot:
        cv.d.ellipse([(tx + 0) * cv.s, (y + h / 2 - 3.5) * cv.s,
                      (tx + 7) * cv.s, (y + h / 2 + 3.5) * cv.s], fill=rgba(dot if isinstance(dot, tuple) else fg))
        tx += dot_w
    text(cv, (tx, y + h / 2), label, th, "chip", color=fg, anchor="lm")
    return (w, h)


def chip_size(th: Theme, label: str, dot: str | None = None, scale: int = 2):
    fs = th.fs("Ios26FontSizeCaption")
    tw = text_w(label, th, "chip", scale=scale)
    h = fs * 1.28 + 6.0
    return (tw + 18.0 + ((7 + 6) if dot else 0), h)


def pill_size(th: Theme, label: str, trailing: str | None = None, scale: int = 2):
    fs = th.fs("Ios26FontSizeSmall")
    tw = text_w(label, th, "pill", scale=scale)
    return (tw + 18.0 + (26 if trailing else 0), fs * 1.28 + 8.0)


def button_size(th: Theme, label: str, kind: str = "primary", icon_name=None,
                scale: int = 2):
    fs = th.fs("Ios26FontSizeBody")
    if kind == "icon":
        return (30.0, 30.0)
    tw = text_w(label, th, "button", size=fs, scale=scale) if label else 0
    px = 16.0 if kind == "primary" else 14.0
    return (tw + ((fs + 8) if icon_name else 0) + px * 2, max(32.0, fs * 1.28 + 14.0))


def value_pill(cv: Canvas, xy, label: str, th: Theme, trailing: str | None = None,
               maxw: float | None = None):
    """SettingsValuePillStyle: radius 6, padding 9,4, 12 px SemiBold."""
    x, y = xy
    pad_x, pad_y = 9.0, 4.0
    fs = th.fs("Ios26FontSizeSmall")
    if maxw:
        label = _clip(label, th, "pill", maxw - pad_x * 2 - (28 if trailing else 0), cv.s)
    tw = text_w(label, th, "pill", scale=cv.s)
    h = fs * 1.28 + pad_y * 2
    w = tw + pad_x * 2 + (26 if trailing else 0)
    r = th.r("Ios26RadiusSmall")
    cv.paste(paint_layers(int(w * cv.s), int(h * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.pill), (1, th.pill)]}],
                          cached_mask(int(w * cv.s), int(h * cv.s), r * cv.s)), x, y)
    text(cv, (x + pad_x, y + h / 2), label, th, "pill", anchor="lm")
    if trailing:
        bx = x + w - 24
        icon(cv, (bx, y + h / 2 - 7, bx + 14, y + h / 2 + 7), trailing, th.muted, 1.6)
    return (w, h)


def _clip(s: str, th: Theme, role: str, maxw: float, scale: int) -> str:
    if maxw <= 0:
        return ""
    if text_w(s, th, role, scale=scale) <= maxw:
        return s
    while s and text_w(s + "…", th, role, scale=scale) > maxw:
        s = s[:-1]
    return s.rstrip() + "…"


def button(cv: Canvas, xy, label: str, th: Theme, kind: str = "primary",
           icon_name: str | None = None, size: float | None = None, pad_x: float | None = None):
    """SettingsPrimaryButtonStyle / Secondary / Icon — 12 px radius, never a pill."""
    x, y = xy
    fs = size or th.fs("Ios26FontSizeBody")
    r = th.r("Ios26RadiusChip")
    px = pad_x if pad_x is not None else (16.0 if kind == "primary" else 14.0)
    py = 7.0
    min_h = 32.0
    if kind == "icon":
        w = h = 30.0
    else:
        tw = text_w(label, th, "button", size=fs, scale=cv.s) if label else 0
        iw = (fs + 8) if icon_name else 0
        h = max(min_h, fs * 1.28 + py * 2)
        w = tw + iw + px * 2
    if kind == "primary":
        bg, fg = th.accent, th.on_accent
    elif kind == "danger":
        bg, fg = th.danger, (255, 255, 255, 255)
    else:
        bg, fg = th.tile, th.secondary
    layer = paint_layers(int(w * cv.s), int(h * cv.s),
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, bg), (1, bg)]}],
                         cached_mask(int(w * cv.s), int(h * cv.s), r * cv.s))
    cv.paste(layer, x, y)
    if kind == "secondary":
        cv.paste(stroke_ring(int(w * cv.s), int(h * cv.s), r * cv.s, cv.s,
                             [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                               "stops": [(0, th.tile_edge), (1, th.tile_edge)]}]), x, y)
    if kind == "icon":
        if icon_name:
            icon(cv, (x + 8, y + 8, x + 22, y + 22), icon_name, fg, 1.7)
    else:
        cx = x + px
        if icon_name:
            icon(cv, (cx, y + h / 2 - fs / 2, cx + fs, y + h / 2 + fs / 2), icon_name, fg, 1.7)
            cx += fs + 8
        if label:
            text(cv, (cx, y + h / 2), label, th, "button", color=fg, anchor="lm", size=fs)
    return (w, h)


def toggle(cv: Canvas, xy, on: bool, th: Theme, size: float = 1.0):
    """LiquidGlassToggleSwitch: 62×36 track, 30 px thumb, accent when checked.

    The thumb's shadow is painted BEFORE the thumb (a shadow pasted on top of
    the knob reads as a dark ring — the first render did exactly that).
    """
    x, y = xy
    w, h = 62 * size, 36 * size
    r = h / 2
    track = th.accent if on else (th.pill if not th.dark else (58, 62, 74, 255))
    cv.paste(paint_layers(int(w * cv.s), int(h * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, track), (1, track)]}],
                          cached_mask(int(w * cv.s), int(h * cv.s), r * cv.s)), x, y)
    # L: top reflection sweep across the upper half of the track
    cv.paste(paint_layers(int((w - 2) * cv.s), int((h * 0.5) * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, with_alpha((255, 255, 255, 255), 0.34 if on else 0.62)),
                                      (1, (255, 255, 255, 0))]}],
                          cached_mask(int((w - 2) * cv.s), int((h * 0.5) * cv.s),
                                      (r - 1) * cv.s)), x + 1, y + 1)
    if not on:
        cv.paste(stroke_ring(int(w * cv.s), int(h * cv.s), r * cv.s, cv.s,
                             [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                               "stops": [(0, with_alpha((15, 23, 42, 255), 0.10)),
                                         (1, with_alpha((15, 23, 42, 255), 0.16))]}]),
                 x, y)
    tw = 30 * size
    tx = x + (3 + (26 if on else 0)) * size
    ty = y + (h - tw) / 2
    # shadow FIRST …
    sl, ox, oy = shadow_layer(int(tw * cv.s), int(tw * cv.s), tw * cv.s / 2,
                              {"color": (15, 23, 42, 255), "opacity": 0.25,
                               "blur": 7, "depth": 2.0, "direction": 90}, cv.s)
    cv.paste(sl, tx - ox, ty - oy)
    # … then the glass knob: white sphere, cool base, crisp rim
    hi = (255, 255, 255, 255)
    lo = (226, 232, 240, 255) if not on else (191, 219, 254, 255)
    cv.paste(paint_layers(int(tw * cv.s), int(tw * cv.s),
                          [{"rect": (0, 0, 1, 1), "radial": True,
                            "center": (0.32, 0.26), "radius": (0.95, 0.95),
                            "stops": [(0, hi), (0.45, hi if not on else (240, 249, 255, 255)),
                                      (1, lo)]}],
                          cached_mask(int(tw * cv.s), int(tw * cv.s), (tw / 2) * cv.s)), tx, ty)
    cv.paste(stroke_ring(int(tw * cv.s), int(tw * cv.s), (tw / 2) * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, with_alpha((255, 255, 255, 255), 0.9)),
                                     (1, with_alpha((148, 163, 184, 255), 0.45))]}]), tx, ty)
    return (w, h)


def slider(cv: Canvas, box, value: float, th: Theme, label: str | None = None,
           height: float = 40.0):
    """ModernSliderStyle: a 40 px rounded bar, accent fill, centred value label."""
    x0, y0, x1, y1 = box
    w = x1 - x0
    r = th.r("Ios26RadiusInner")
    # base bar
    cv.paste(paint_layers(int(w * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.pill), (1, th.pill)]}],
                          cached_mask(int(w * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    cv.paste(stroke_ring(int(w * cv.s), int(height * cv.s), r * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, th.hairline), (1, th.hairline)]}]), x0, y0)
    fw = max(height, w * value)
    cv.paste(paint_layers(int(fw * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.accent), (1, th.accent_deep)]}],
                          cached_mask(int(fw * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    # thumb: 16 px accent disc inside a 20 % halo, white rim (ModernSliderThumbStyle)
    tx = x0 + fw
    cy = y0 + height / 2
    cv.paste(paint_layers(int(24 * cv.s), int(24 * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, with_alpha(th.accent, 0.20)),
                                      (1, with_alpha(th.accent, 0.20))]}],
                          cached_mask(int(24 * cv.s), int(24 * cv.s), 12 * cv.s)),
             tx - 12, cy - 12)
    cv.paste(paint_layers(int(16 * cv.s), int(16 * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.accent), (1, th.accent)]}],
                          cached_mask(int(16 * cv.s), int(16 * cv.s), 8 * cv.s)),
             tx - 8, cy - 8)
    cv.paste(stroke_ring(int(16 * cv.s), int(16 * cv.s), 8 * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, with_alpha((255, 255, 255, 255), 0.85)),
                                     (1, with_alpha((255, 255, 255, 255), 0.35))]}]),
             tx - 8, cy - 8)
    if label:
        # centred like ModernSliderStyle, but nudged clear of the thumb so the
        # value never sits under the knob
        _tok, _wt, _ = TYPE_ROLES["field_value"]
        _f = FONTS.get(th.fs(_tok) * cv.s, _wt)
        tw_ = _f.getlength(label) / cv.s
        cx = (x0 + x1) / 2
        if abs(cx - tx) < tw_ / 2 + 12:
            right_c = (tx + 12 + x1) / 2
            left_c = (x0 + tx - 12) / 2
            cx = right_c if (x1 - (tx + 12)) > tw_ + 8 else left_c
        on_fill = abs(cx - x0) < fw
        text(cv, (cx, y0 + height / 2), label, th, "field_value",
             color=th.on_accent if on_fill else th.body, anchor="mm")
    return (w, height)

def segmented(cv: Canvas, box, items: list[str], selected: int, th: Theme,
              height: float = 34.0, fill: bool = False):
    """SettingsSegmentTrackStyle + SettingsSegmentItemStyle (a ListBox pair)."""
    x0, y0, x1, y1 = box
    w = (x1 - x0) if fill else sum(
        max(text_w(i, th, "button", scale=cv.s) + 32, 64) for i in items) + 4
    r = th.r("Ios26RadiusChip")
    cv.paste(paint_layers(int(w * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.pill), (1, th.pill)]}],
                          cached_mask(int(w * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    cv.paste(stroke_ring(int(w * cv.s), int(height * cv.s), r * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, with_alpha(th.hairline, 0.8)),
                                     (1, with_alpha(th.hairline, 0.8))]}]), x0, y0)
    seg_w = (w - 4) / len(items)
    for i, label in enumerate(items):
        sx = x0 + 2 + i * seg_w
        if i == selected:
            cv.paste(paint_layers(int(seg_w * cv.s), int((height - 4) * cv.s),
                                  [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                    "stops": [(0, th.accent), (1, th.accent)]}],
                                  cached_mask(int(seg_w * cv.s), int((height - 4) * cv.s),
                                              (r - 2) * cv.s)), sx, y0 + 2)
            fg = th.on_accent
        else:
            fg = th.secondary
        text(cv, (sx + seg_w / 2, y0 + height / 2), label, th, "button",
             color=fg, anchor="mm")
    return (w, height)


def combo(cv: Canvas, box, value: str, th: Theme, height: float = 34.0):
    x0, y0, x1, y1 = box
    w, r = x1 - x0, th.r("Ios26RadiusChip")
    cv.paste(paint_layers(int(w * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.tile), (1, th.tile)]}],
                          cached_mask(int(w * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    cv.paste(stroke_ring(int(w * cv.s), int(height * cv.s), r * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, th.tile_edge), (1, th.tile_edge)]}]), x0, y0)
    text(cv, (x0 + 12, y0 + height / 2), value, th, "row_title", anchor="lm",
         maxw=w - 40)
    icon(cv, (x1 - 26, y0 + height / 2 - 7, x1 - 12, y0 + height / 2 + 7),
         "chevron_d", th.muted, 1.8)
    return (w, height)


def input_box(cv: Canvas, box, value: str, th: Theme, height: float = 34.0,
              placeholder: bool = False, mono: bool = False,
              icon_left: str | None = None):
    x0, y0, x1, y1 = box
    w, r = x1 - x0, th.r("Ios26RadiusChip")
    cv.paste(paint_layers(int(w * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.c("Ios26SearchBrush")), (1, th.c("Ios26SearchBrush"))]}],
                          cached_mask(int(w * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    cv.paste(stroke_ring(int(w * cv.s), int(height * cv.s), r * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, th.tile_edge), (1, th.tile_edge)]}]), x0, y0)
    pad_l = 12 + (24 if icon_left else 0)
    if icon_left:
        icon(cv, (x0 + 10, y0 + height / 2 - 8, x0 + 26, y0 + height / 2 + 8),
             icon_left, th.faint, 1.7)
    text(cv, (x0 + pad_l, y0 + height / 2), value, th, "row_title",
         color=th.faint if placeholder else th.body, anchor="lm", maxw=w - pad_l - 12)
    return (w, height)


def progress(cv: Canvas, box, value: float, th: Theme, height: float = 8.0,
             color=None):
    """Ios26BarStyle: full-round track + accent fill."""
    x0, y0, x1, y1 = box
    w, r = x1 - x0, height / 2
    track = th.pill if not th.dark else (40, 44, 56, 255)
    cv.paste(paint_layers(int(w * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, track), (1, track)]}],
                          cached_mask(int(w * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    fw = max(height, w * value)
    col = color or th.accent
    cv.paste(paint_layers(int(fw * cv.s), int(height * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, col), (1, col)]}],
                          cached_mask(int(fw * cv.s), int(height * cv.s), r * cv.s)), x0, y0)
    return (w, height)


def separator(cv: Canvas, x0: float, y: float, x1: float, th: Theme):
    """SettingsRowSeparatorStyle — a real hairline, never an empty card."""
    cv.paste(paint_layers(int((x1 - x0) * cv.s), max(1, int(1 * cv.s)),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, th.hairline), (1, th.hairline)]}]), x0, y)


def scrollbar(cv: Canvas, box, th: Theme, top_frac: float = 0.0, size_frac: float = 0.42):
    x0, y0, x1, y1 = box
    track_w = 8.0
    x = x1 - track_w - 2
    h = (y1 - y0)
    thumb_h = max(40, h * size_frac)
    ty = y0 + (h - thumb_h) * top_frac
    cv.paste(paint_layers(int(track_w * cv.s), int(thumb_h * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, with_alpha(th.muted, 0.55)),
                                      (1, with_alpha(th.muted, 0.55))]}],
                          cached_mask(int(track_w * cv.s), int(thumb_h * cv.s),
                                      (track_w / 2) * cv.s)), x, ty)


def swatch(cv: Canvas, box, color, th: Theme, selected: bool = False, radius=8.0):
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    cv.paste(paint_layers(int(w * cv.s), int(h * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, color), (1, color)]}],
                          cached_mask(int(w * cv.s), int(h * cv.s), radius * cv.s)), x0, y0)
    if selected:
        cv.paste(stroke_ring(int(w * cv.s), int(h * cv.s), radius * cv.s, 2.4 * cv.s,
                             [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                               "stops": [(0, th.title), (1, th.title)]}]), x0, y0)
        icon(cv, (x0 + w / 2 - 6, y0 + h / 2 - 6, x0 + w / 2 + 6, y0 + h / 2 + 6),
             "check", (255, 255, 255, 255), 2.4)
    else:
        cv.paste(stroke_ring(int(w * cv.s), int(h * cv.s), radius * cv.s, cv.s,
                             [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                               "stops": [(0, with_alpha((255, 255, 255, 255), 0.35)),
                                        (1, with_alpha((15, 23, 42, 255), 0.10))]}]), x0, y0)

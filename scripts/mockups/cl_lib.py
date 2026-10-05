#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
FILE: scripts/mockups/cl_lib.py
PROJECT: ProGlassAutomation
MODULE: Crystal Liquid mockup library (2026-09-17)

WHY THIS EXISTS
───────────────
The approved sheet set (docs/mockups/settings-redesign/01..10) was drawn as a
flat "soft UI" target: 1 px #E2E8F0 border, #CCFFFFFF fill, small shadow, and
cards BUTTED TOGETHER with no gap. The module implemented that target
faithfully — which is exactly why the owner said "design is still same" and
"cards are too close". The gap was in the SPEC, not in the implementation.

This library renders the replacement spec: the app's OWN crystal material.
Everything here is expressed in the tokens the app already ships:

  stage      AppBackgroundBrush  (Themes/LightTheme.xaml, byte-identical in
                                  DarkTheme.xaml — the stage is invariant by
                                  owner directive "Background page dont make
                                  it dynamic")
  radii      Ios26RadiusCard 26 / Inner 12 / Tile 10 / Chip 12  (AboutSurface)
  type       Ios26FontSize* (AboutTypeScale.xaml)
  palette    Ios26*Brush (both theme dictionaries)

and in the six layers the app already composes in Themes/Styles/
LiquidGlassDesign.xaml -> LiquidGlassSurface:

  L0  ambient shadow          (colour-tinted, wide, low alpha)
  L1  crystal edge            (gradient stroke, NOT a flat hairline)
  L2  frosted base            (backdrop sampled + blurred + tinted gradient)
  L3  specular rim            (bright top inner edge)
  L4  reflection sweep        (diagonal sheen)
  L5  refraction edge         (cool bottom inner edge)
  L6  content

Nothing here is decorative invention: each layer maps to a XAML setter or a
template layer, so the sheet is a shippable target rather than a picture.

Needs Pillow:  pip install pillow   (dev-only tool; never runs in the app)
"""

import math

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont

# ── supersampling: everything is drawn at 2x and downsampled once ──────────
S = 2
FONT_R = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONT_B = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


# ═══════════════════════════════════════════════════════════════════════════
# TOKENS — copied from the theme dictionaries, not invented
# ═══════════════════════════════════════════════════════════════════════════
def _c(h, a=None):
    """#RRGGBB[AA] -> RGBA tuple, optional alpha override 0..1."""
    h = h.lstrip("#")
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    alpha = int(h[6:8], 16) if len(h) == 8 else 255
    if a is not None:
        alpha = int(round(a * 255))
    return (r, g, b, alpha)


# the invariant stage (AppStage.xaml / AppBackgroundBrush)
CANVAS = "#F1F5FB"
ORBS = [
    # (cx, cy, rx, ry, colour) — DrawingBrush ellipses, verbatim
    (0.88, 0.13, 0.24, 0.24, "#BFE8E3"),
    (0.08, 0.88, 0.19, 0.26, "#C5D8F6"),
    (0.06, 0.09, 0.21, 0.30, "#CFD4F5"),
    (0.96, 0.93, 0.17, 0.25, "#D2EBD6"),
]

LIGHT = dict(
    key="light",
    card_top=_c("#FFFFFF", 0.72), card_bot=_c("#FFFFFF", 0.38),
    rail_top=_c("#FFFFFF", 0.70), rail_bot=_c("#FFFFFF", 0.42),
    tile=_c("#FFFFFF", 0.50), tile_edge=_c("#FFFFFF", 0.80),
    edge_top=_c("#FFFFFF", 0.98), edge_mid=_c("#B9C6D8", 0.55), edge_bot=_c("#FFFFFF", 0.85),
    specular=_c("#FFFFFF", 0.90), refraction=_c("#FFFFFF", 0.42),
    reflection=_c("#FFFFFF", 0.55),
    text_title=_c("#0F172A"), text_body=_c("#1E293B"), text_sec=_c("#475569"),
    text_muted=_c("#64748B"), text_faint=_c("#94A3B8"),
    accent=_c("#0369A1"), accent_soft=_c("#DBEAFE"), accent_soft_text=_c("#1D4ED8"),
    on_accent=_c("#FFFFFF"),
    pill=_c("#F1F5F9"), track=_c("#E2E8F0"), hover=_c("#E6ECF3"),
    success=_c("#16A34A"), success_soft=_c("#DCFCE7"),
    danger=_c("#E11D48"), danger_soft=_c("#E11D48", 0.14),
    shadow=(0x64, 0x74, 0x8B, 0.20), shadow_radius=34, shadow_dy=14,
    frost=16.0,
)

DARK = dict(
    key="dark",
    card_top=_c("#1B1D24", 0.80), card_bot=_c("#151720", 0.62),
    rail_top=_c("#171921", 0.84), rail_bot=_c("#101218", 0.68),
    tile=_c("#FFFFFF", 0.07), tile_edge=_c("#FFFFFF", 0.10),
    edge_top=_c("#FFFFFF", 0.34), edge_mid=_c("#FFFFFF", 0.12), edge_bot=_c("#FFFFFF", 0.22),
    specular=_c("#FFFFFF", 0.30), refraction=_c("#FFFFFF", 0.10),
    reflection=_c("#FFFFFF", 0.10),
    text_title=_c("#F5F5F7"), text_body=_c("#E6E8ED"), text_sec=_c("#A8B0BE"),
    text_muted=_c("#8B93A2"), text_faint=_c("#6E7687"),
    accent=_c("#38BDF8"), accent_soft=_c("#12324A"), accent_soft_text=_c("#7DD3FC"),
    on_accent=_c("#08131C"),
    pill=_c("#FFFFFF", 0.09), track=_c("#FFFFFF", 0.14), hover=_c("#FFFFFF", 0.12),
    success=_c("#34D399"), success_soft=_c("#123524"),
    danger=_c("#FB7185"), danger_soft=_c("#FB7185", 0.16),
    shadow=(0x02, 0x06, 0x17, 0.38), shadow_radius=40, shadow_dy=18,
    frost=18.0,
)

# geometry tokens
# Ios26Radius* at the default card radius (AboutSurface.xaml /
# AboutSurfaceTokens.Radii): card 26/12 · inner 12/12 · tile 10/12 ·
# chip 12/12 · small 6/12 of the LIVE card radius.
R_CARD, R_INNER, R_TILE, R_CHIP, R_SMALL = 26, 12, 10, 12, 6
_AUTO = object()   # tile(edge=_AUTO) -> use the token; edge=None -> no stroke
GAP_CARD = 22          # was 14 — the owner's "cards are too close"
PAD_CARD = (24, 22)    # x, y
FS = dict(caption=10, label=11, small=12, body=13, value=14, subtitle=15,
          card_title=16, section=18, glyph=19, metric=24, display=30)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET
# ═══════════════════════════════════════════════════════════════════════════
class Sheet(object):
    def __init__(self, w, h, theme=LIGHT, stage=True):
        self.w, self.h, self.t = w, h, theme
        size = (w * S, h * S)
        # stage — the invariant About canvas + its four orbs
        self.backdrop = Image.new("RGBA", size, _c(CANVAS))
        if stage:
            for cx, cy, rx, ry, col in ORBS:
                orb = Image.new("RGBA", size, (0, 0, 0, 0))
                d = ImageDraw.Draw(orb)
                d.ellipse([(cx - rx) * size[0], (cy - ry) * size[1],
                           (cx + rx) * size[0], (cy + ry) * size[1]], fill=_c(col))
                orb = orb.filter(ImageFilter.GaussianBlur(40 * S))
                self.backdrop = Image.alpha_composite(self.backdrop, orb)
        self.img = self.backdrop.copy()
        self.d = ImageDraw.Draw(self.img)
        self._fonts = {}

    # ── primitives ────────────────────────────────────────────────────────
    def box(self, b):
        return [b[0] * S, b[1] * S, b[2] * S, b[3] * S]

    @property
    def g(self):
        """Glyph-space draw: CSS pixels in, supersampled pixels out."""
        return _ScaledDraw(self.d, S)

    def font(self, size, bold=False):
        k = (size, bold)
        if k not in self._fonts:
            self._fonts[k] = ImageFont.truetype(FONT_B if bold else FONT_R, int(size * S))
        return self._fonts[k]

    def layer(self):
        return Image.new("RGBA", self.img.size, (0, 0, 0, 0))

    def mask(self, b, r, width=0):
        m = Image.new("L", self.img.size, 0)
        ImageDraw.Draw(m).rounded_rectangle(self.box(b), r * S,
                                            outline=255 if width else None,
                                            fill=None if width else 255,
                                            width=max(1, int(width * S)))
        return m

    def alpha_of(self, layer):
        return layer.getchannel("A")

    def cut(self, layer, mask):
        layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
        return layer

    def comp(self, layer):
        self.img = Image.alpha_composite(self.img, layer)
        self.d = ImageDraw.Draw(self.img)
        return self.img

    def vgrad(self, b, top, bot):
        """vertical gradient inside b (RGBA colours)."""
        x0, y0, x1, y1 = self.box(b)
        hh = max(1, int(y1 - y0))
        strip = Image.new("RGBA", (1, hh))
        px = strip.load()
        for y in range(hh):
            f = y / float(hh - 1) if hh > 1 else 0.0
            px[0, y] = tuple(int(top[i] + (bot[i] - top[i]) * f) for i in range(4))
        strip = strip.resize((max(1, int(x1 - x0)), hh), Image.Resampling.BILINEAR)
        out = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        out.paste(strip, (int(x0), int(y0)))
        return out

    def tri_grad(self, b, a, mid, z):
        """3-stop vertical gradient (for the crystal edge)."""
        x0, y0, x1, y1 = self.box(b)
        hh = max(1, int(y1 - y0))
        strip = Image.new("RGBA", (1, hh))
        px = strip.load()
        for y in range(hh):
            f = y / float(hh - 1) if hh > 1 else 0.0
            if f < 0.5:
                g, c0, c1 = f / 0.5, a, mid
            else:
                g, c0, c1 = (f - 0.5) / 0.5, mid, z
            px[0, y] = tuple(int(c0[i] + (c1[i] - c0[i]) * g) for i in range(4))
        strip = strip.resize((max(1, int(x1 - x0)), hh), Image.Resampling.BILINEAR)
        out = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        out.paste(strip, (int(x0), int(y0)))
        return out

    # ── glass ─────────────────────────────────────────────────────────────
    def glass(self, b, r=R_CARD, kind="card", frost=None, shadow=True,
              tint=None, edge=True, sheen=True, clip_extra=None):
        """Compose one crystal-liquid surface over the stage."""
        t = self.t
        frost = t["frost"] if frost is None else frost
        if shadow:
            self.soft_shadow(b, r, t["shadow"])
        x0, y0, x1, y1 = self.box(b)
        base = self.layer()
        # L2 frosted base: sample WHAT IS BEHIND THIS SURFACE — the live
        # composite, so a tile inside a card frosts the card (sampling the
        # pristine stage instead is what punched a visible hole in the parent
        # on the first render), then blur and let the material saturate it the
        # way a real backdrop-filter does.
        src = self.img
        crop = src.crop((int(x0), int(y0), int(x1), int(y1)))
        if frost > 0:
            crop = crop.filter(ImageFilter.GaussianBlur(frost * S))
            crop = ImageEnhance.Color(crop).enhance(1.22)
            crop = ImageEnhance.Brightness(crop).enhance(1.02)
        base.paste(crop, (int(x0), int(y0)))
        full = self.mask(b, r)
        if clip_extra is not None:
            full = ImageChops.multiply(full, clip_extra)
        base = self.cut(base, full)
        self.comp(base)
        # …then the material tint (this is what colourises the frost)
        if tint is None:
            top, bot = (t["card_top"], t["card_bot"]) if kind == "card" else \
                       (t["rail_top"], t["rail_bot"])
        else:
            top, bot = tint
        tg = self.vgrad(b, top, bot)
        self.comp(self.cut(tg, full))
        # L4 reflection sweep
        if sheen:
            sheen_l = self.layer()
            sd = ImageDraw.Draw(sheen_l)
            sd.polygon([(x0, y0 + (y1 - y0) * 0.55), (x0, y0 + (y1 - y0) * 0.10),
                        (x0 + (x1 - x0) * 0.62, y0), (x0 + (x1 - x0) * 0.20, y0)],
                       fill=t["reflection"])
            sheen_l = sheen_l.filter(ImageFilter.GaussianBlur(14 * S))
            self.comp(self.cut(sheen_l, full))
        # L1 crystal edge
        if edge:
            eg = self.tri_grad(b, t["edge_top"], t["edge_mid"], t["edge_bot"])
            self.comp(self.cut(eg, self.mask(b, r, 1.2)))
        if clip_extra is not None:
            return full
        return full

    def specular(self, b, r=R_CARD, mask_extra=None):
        """L3 — bright top inner edge, fading before the corners."""
        t = self.t
        m = self.mask(b, r, 1.4)
        x0, y0, x1, y1 = self.box(b)
        fade = Image.new("L", self.img.size, 0)
        fd = ImageDraw.Draw(fade)
        hgt = int((y1 - y0) * 0.42)
        for i in range(hgt):
            a = int(255 * (1 - i / float(max(1, hgt - 1))) ** 1.6)
            fd.line([(x0 - 4, y0 + i), (x1 + 4, y0 + i)], fill=a)
        m = ImageChops.multiply(m, fade)
        if mask_extra is not None:
            m = ImageChops.multiply(m, mask_extra)
        l = self.layer()
        ImageDraw.Draw(l).rectangle([x0, y0, x1, y1], fill=t["specular"])
        self.comp(self.cut(l, m))

    def refraction(self, b, r=R_CARD):
        """L5 — cool bottom inner edge."""
        t = self.t
        m = self.mask(b, r, 1.2)
        x0, y0, x1, y1 = self.box(b)
        fade = Image.new("L", self.img.size, 0)
        fd = ImageDraw.Draw(fade)
        hgt = int((y1 - y0) * 0.30)
        for i in range(hgt):
            a = int(200 * (i / float(max(1, hgt - 1))) ** 2)
            fd.line([(x0 - 4, y1 - i), (x1 + 4, y1 - i)], fill=a)
        m = ImageChops.multiply(m, fade)
        l = self.layer()
        ImageDraw.Draw(l).rectangle([x0, y0, x1, y1], fill=t["refraction"])
        self.comp(self.cut(l, m))

    def soft_shadow(self, b, r, col):
        r_, g_, b_, a = col
        layer = self.layer()
        ImageDraw.Draw(layer).rounded_rectangle(self.box(b), r * S,
                                                fill=(r_, g_, b_, int(a * 255)))
        layer = layer.filter(ImageFilter.GaussianBlur(self.t["shadow_radius"] * S * 0.5))
        out = self.layer()
        out.paste(layer, (0, int(self.t["shadow_dy"] * S)), layer)
        self.comp(out)

    def tile(self, b, r=R_INNER, fill=None, edge=_AUTO):
        t = self.t
        m = self.mask(b, r)
        l = self.layer()
        ImageDraw.Draw(l).rounded_rectangle(self.box(b), r * S, fill=fill or t["tile"])
        self.comp(self.cut(l, m))
        if edge is not _AUTO:
            if edge is not None:
                e = self.layer()
                ImageDraw.Draw(e).rounded_rectangle(self.box(b), r * S, outline=edge,
                                                    width=max(1, int(1 * S)))
                self.comp(e)
            return m
        e = self.layer()
        ImageDraw.Draw(e).rounded_rectangle(self.box(b), r * S, outline=t["tile_edge"],
                                            width=max(1, int(1 * S)))
        self.comp(e)
        return m

    # ── type ──────────────────────────────────────────────────────────────
    def text(self, xy, s, size=FS["body"], color=None, bold=False, anchor="la"):
        col = color if color else self.t["text_body"]
        self.d.text((xy[0] * S, xy[1] * S), s, font=self.font(size, bold), fill=col, anchor=anchor)
        return self

    def text_w(self, s, size=FS["body"], bold=False):
        return self.d.textlength(s, font=self.font(size, bold)) / float(S)

    # ── controls ──────────────────────────────────────────────────────────
    def switch(self, x, y, on=True, w=51, h=31):
        t = self.t
        tr = self.tile((x, y, x + w, y + h), r=h / 2.0,
                       fill=t["accent"] if on else _c("#8E8E93", 0.35 if t["key"] == "light" else 0.28))
        kx = x + w - h / 2.0 - 2 if on else x + h / 2.0 + 2
        kl = self.layer()
        ImageDraw.Draw(kl).ellipse([(kx - 13.5) * S, (y + h / 2.0 - 13.5) * S,
                                   (kx + 13.5) * S, (y + h / 2.0 + 13.5) * S],
                                  fill=_c("#FFFFFF"))
        sh = self.layer()
        ImageDraw.Draw(sh).ellipse([(kx - 13.5) * S, (y + h / 2.0 - 12.5) * S,
                                   (kx + 13.5) * S, (y + h / 2.0 + 14.5) * S],
                                  fill=(0x0F, 0x17, 0x2A, 70))
        sh = sh.filter(ImageFilter.GaussianBlur(4 * S))
        self.comp(sh)
        self.comp(kl)
        return tr

    def slider(self, x, y, w, v, h=5):
        t = self.t
        self.tile((x, y, x + w, y + h), r=h / 2.0, fill=t["track"], edge=None)
        self.tile((x, y, x + w * v, y + h), r=h / 2.0, fill=t["accent"], edge=None)
        kx = x + w * v
        kl = self.layer()
        ImageDraw.Draw(kl).ellipse([(kx - 11) * S, (y + h / 2.0 - 11) * S,
                                   (kx + 11) * S, (y + h / 2.0 + 11) * S], fill=_c("#FFFFFF"))
        self.comp(kl)

    def segment(self, b, options, sel, r=None, fs=None):
        """Glass track + selected capsule (the shipped ListBox pair)."""
        t = self.t
        r = r or (b[3] - b[1]) / 2.0
        self.tile(b, r=r, fill=t["track"], edge=_c("#FFFFFF", 0.35 if t["key"] == "light" else 0.10))
        w = (b[2] - b[0]) / float(len(options))
        for i, o in enumerate(options):
            x0, x1 = b[0] + i * w + 3, b[0] + (i + 1) * w - 3
            if i == sel:
                self.soft_shadow((x0, b[1] + 3, x1, b[3] - 3), r, (0x0F, 0x17, 0x2A, 0.16))
                self.tile((x0, b[1] + 3, x1, b[3] - 3), r=r - 3,
                          fill=_c("#FFFFFF", 0.94) if t["key"] == "light" else _c("#FFFFFF", 0.16),
                          edge=_c("#FFFFFF", 0.9) if t["key"] == "light" else _c("#FFFFFF", 0.22))
                col = t["text_title"]
            else:
                col = t["text_muted"]
            self.text(((x0 + x1) / 2.0, (b[1] + b[3]) / 2.0), o, fs or FS["small"], col, False, "mm")

    def chip(self, x, y, s, kind="soft", dot=None, size=FS["label"]):
        t = self.t
        pad, h = 10, 24
        w = self.text_w(s, size) + pad * 2 + (11 if dot else 0)
        fill = {"soft": t["accent_soft"], "good": t["success_soft"], "warn": t["danger_soft"],
                "muted": t["pill"], "onaccent": _c("#FFFFFF", 0.22)}[kind]
        fg = {"soft": t["accent_soft_text"], "good": t["success"], "warn": t["danger"],
              "muted": t["text_sec"], "onaccent": _c("#FFFFFF")}[kind]
        self.tile((x, y, x + w, y + h), r=h / 2.0, fill=fill,
                  edge=_c("#FFFFFF", 0.5) if t["key"] == "light" else _c("#FFFFFF", 0.12))
        tx = x + pad
        if dot:
            self.d.ellipse([(tx) * S, (y + h / 2.0 - 4) * S, (tx + 8) * S, (y + h / 2.0 + 4) * S],
                           fill=dot if isinstance(dot, tuple) else fg)
            tx += 11
        self.text((tx, y + h / 2.0 + 0.5), s, size, fg, True, "lm")
        return w

    def button(self, x, y, s, kind="primary", h=34, size=FS["small"]):
        t = self.t
        pad = 16
        w = self.text_w(s, size, bold=(kind != "ghost")) + pad * 2
        if kind == "primary":
            self.soft_shadow((x, y, x + w, y + h), h / 2.0, (0x03, 0x69, 0xA1, 0.32))
            self.tile((x, y, x + w, y + h), r=h / 2.0,
                      fill=t["accent"] if t["key"] == "light" else _c("#38BDF8"),
                      edge=_c("#FFFFFF", 0.35))
            self.text((x + w / 2.0, y + h / 2.0 + 0.5), s, size, self.t["on_accent"], True, "mm")
        elif kind == "danger":
            self.tile((x, y, x + w, y + h), r=h / 2.0, fill=t["danger_soft"], edge=t["danger"])
            self.text((x + w / 2.0, y + h / 2.0 + 0.5), s, size, t["danger"], True, "mm")
        else:
            self.tile((x, y, x + w, y + h), r=h / 2.0,
                      fill=_c("#FFFFFF", 0.55 if t["key"] == "light" else 0.10),
                      edge=t["edge_mid"])
            self.text((x + w / 2.0, y + h / 2.0 + 0.5), s, size, t["text_body"], False, "mm")
        return w

    def field(self, b, s, r=R_CHIP, placeholder=False):
        t = self.t
        self.tile(b, r=r, fill=_c("#FFFFFF", 0.72) if t["key"] == "light" else _c("#FFFFFF", 0.08),
                  edge=_c("#FFFFFF", 0.9) if t["key"] == "light" else _c("#FFFFFF", 0.14))
        self.text((b[0] + (16 if placeholder else 12), (b[1] + b[3]) / 2.0 + 0.5), s,
                  FS["body"], t["text_faint"] if placeholder else t["text_body"], False, "lm")
        if not placeholder:
            # caret sits at the text start of a focused field only
            self.d.line([(b[0] + 16) * S, (b[1] + 8) * S, (b[0] + 16) * S, (b[3] - 8) * S],
                        fill=t["accent"], width=max(1, int(1.5 * S)))

    def icon_tile(self, x, y, glyph, size=34, tint=None, soft=True):
        t = self.t
        r = size * 0.30
        bg = tint or (t["accent_soft"] if soft else t["pill"])
        self.tile((x, y, x + size, y + size), r=r, fill=bg,
                  edge=_c("#FFFFFF", 0.55) if t["key"] == "light" else _c("#FFFFFF", 0.12))
        gl = t["accent_soft_text"] if soft else t["text_sec"]
        ICONS[glyph](self, (x + size / 2.0, y + size / 2.0), size * 0.46, gl)
        return size

    def icon(self, cx, cy, glyph, size=18, color=None):
        ICONS[glyph](self, (cx, cy), size, color or self.t["text_sec"])

    # ── page furniture ────────────────────────────────────────────────────
    def rail(self, b, items, active, title="Settings"):
        t = self.t
        self.glass(b, r=R_CARD, kind="rail")
        x0, y0 = b[0], b[1]
        self.icon_tile(x0 + 18, y0 + 18, "gear", 36)
        self.text((x0 + 64, y0 + 36), title, FS["card_title"], t["text_title"], True, "lm")
        y = y0 + 74
        for label, glyph in items:
            row = (x0 + 12, y, b[2] - 12, y + 40)
            if label == active:
                self.tile(row, r=R_TILE, fill=t["accent"] if t["key"] == "light" else _c("#38BDF8", 0.22),
                          edge=_c("#FFFFFF", 0.30) if t["key"] == "light" else _c("#38BDF8", 0.45))
                col = t["on_accent"] if t["key"] == "light" else t["text_title"]
            else:
                col = t["text_body"]
            self.icon(row[0] + 20, y + 20, glyph, 17, col)
            self.text((row[0] + 42, y + 20), label, FS["body"], col, label == active, "lm")
            y += 44

    def page_head(self, b, title, chip_text=None, chip_kind="soft"):
        t = self.t
        self.glass(b, r=R_CARD)
        self.specular(b, r=R_CARD)
        cy = (b[1] + b[3]) / 2.0
        self.icon(b[0] + 26, cy, "menu", 16, t["text_sec"])
        self.text((b[0] + 52, cy), title, 22, t["text_title"], True, "lm")
        if chip_text:
            w = self.text_w(chip_text, FS["label"]) + 30
            self.chip(b[2] - 22 - w, cy - 12, chip_text, chip_kind, dot=t["accent"])

    def card(self, b, title=None, glyph=None, chip=None, chip_kind="soft", sub=None):
        t = self.t
        self.glass(b, r=R_CARD)
        self.specular(b, r=R_CARD)
        y = b[1] + PAD_CARD[1]
        x = b[0] + PAD_CARD[0]
        if title:
            if glyph:
                self.icon_tile(x, y - 6, glyph, 30)
                tx = x + 42
            else:
                tx = x
            self.text((tx, y + 9), title, FS["card_title"], t["text_title"], True, "lm")
            if chip:
                w = self.text_w(chip, FS["label"]) + 30
                self.chip(b[2] - PAD_CARD[0] - w, y, chip, chip_kind)
            if sub:
                self.text((tx, y + 30), sub, FS["small"], t["text_muted"], False, "lm")
            y += 46 if sub else 34
        return x, y, b[2] - PAD_CARD[0]

    def row(self, x0, x1, y, label, value=None, glyph=None, desc=None, control=None):
        t = self.t
        cy = y + 15
        if glyph:
            self.icon(x0 + 9, cy, glyph, 16, t["text_sec"])
            lx = x0 + 30
        else:
            lx = x0
        self.text((lx, cy), label, FS["body"], t["text_title"], False, "lm")
        if desc:
            self.text((lx, cy + 18), desc, FS["small"], t["text_muted"], False, "lm")
        if value:
            self.text((x1 - 10, cy), value, FS["body"], t["text_sec"], False, "rm")
        if control == "chev":
            self.icon(x1 - 12, cy, "chevr", 13, t["text_faint"])
        return cy

    # ── annotation ────────────────────────────────────────────────────────
    def callout(self, x, y, tx, ty, label, color=None, side="left"):
        col = color or self.t["accent_soft_text"]
        self.d.line([x * S, y * S, tx * S, ty * S], fill=col, width=max(1, int(1.2 * S)))
        self.d.ellipse([(x - 3.5) * S, (y - 3.5) * S, (x + 3.5) * S, (y + 3.5) * S], fill=col)
        anchor = "lm" if side == "left" else "rm"
        self.text((tx + (8 if side == "left" else -8), ty), label, FS["label"], col, True, anchor)

    def save(self, path):
        out = self.img.resize((self.w, self.h), Image.Resampling.LANCZOS)
        out.convert("RGB").save(path, "PNG", optimize=True)
        return path


class _ScaledDraw(object):
    """ImageDraw proxy that speaks CSS pixels.

    Every glyph below is authored in layout units; the sheet is rendered at
    S×, so coordinates, stroke widths and radii are scaled here instead of at
    40 call sites (a missed `* S` used to draw a glyph at half position).
    """

    def __init__(self, draw, s):
        self._d, self._s = draw, s

    def _pt(self, xy):
        if isinstance(xy, (list, tuple)):
            if xy and isinstance(xy[0], (list, tuple)):
                return [(p[0] * self._s, p[1] * self._s) for p in xy]
            return [v * self._s for v in xy]
        return xy

    def _w(self, w):
        return max(1, int(round(w * self._s)))

    def line(self, xy, fill=None, width=1, joint=None):
        return self._d.line(self._pt(xy), fill=fill, width=self._w(width), joint=joint)

    def polygon(self, xy, fill=None, outline=None, width=1):
        return self._d.polygon(self._pt(xy), fill=fill, outline=outline, width=self._w(width))

    def ellipse(self, xy, fill=None, outline=None, width=1):
        return self._d.ellipse(self._pt(xy), fill=fill, outline=outline, width=self._w(width))

    def arc(self, xy, start, end, fill=None, width=1):
        return self._d.arc(self._pt(xy), start, end, fill=fill, width=self._w(width))

    def pieslice(self, xy, start, end, fill=None, outline=None, width=1):
        return self._d.pieslice(self._pt(xy), start, end, fill=fill, outline=outline,
                                width=self._w(width))

    def rectangle(self, xy, fill=None, outline=None, width=1):
        return self._d.rectangle(self._pt(xy), fill=fill, outline=outline, width=self._w(width))

    def rounded_rectangle(self, xy, radius=0, fill=None, outline=None, width=1):
        return self._d.rounded_rectangle(self._pt(xy), radius=radius * self._s, fill=fill,
                                         outline=outline, width=self._w(width))

    def text(self, xy, s, **k):
        return self._d.text((xy[0] * self._s, xy[1] * self._s), s, **k)

    def textlength(self, s, **k):
        return self._d.textlength(s, **k) / float(self._s)


def _scale(box, s=S):
    return [v * s for v in box]


# ═══════════════════════════════════════════════════════════════════════════
# ICONS — line art, MDL2 stand-ins (the mockup only needs the silhouette)
# ═══════════════════════════════════════════════════════════════════════════
def _p(s, pts, col, w):
    s.g.line(pts, fill=col, width=w, joint="curve")


def _o(s, cx, cy, r, col, w):
    s.g.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=w)


def ic_gear(s, c, sz, col):
    cx, cy = c
    _o(s, cx, cy, sz * 0.62, col, 1.5)
    for i in range(8):
        a = math.pi * i / 4.0
        _p(s, [(cx + math.cos(a) * sz * 0.72, cy + math.sin(a) * sz * 0.72),
               (cx + math.cos(a) * sz * 0.98, cy + math.sin(a) * sz * 0.98)], col, 1.5)


def ic_contrast(s, c, sz, col):
    cx, cy = c
    _o(s, cx, cy, sz * 0.9, col, 1.5)
    s.g.pieslice([(cx - sz * 0.9), (cy - sz * 0.9), (cx + sz * 0.9), (cy + sz * 0.9)],
                 -90, 90, fill=col)


def ic_sun(s, c, sz, col):
    cx, cy = c
    _o(s, cx, cy, sz * 0.42, col, 1.5)
    for i in range(8):
        a = math.pi * i / 4.0
        _p(s, [(cx + math.cos(a) * sz * 0.66, cy + math.sin(a) * sz * 0.66),
               (cx + math.cos(a) * sz * 0.95, cy + math.sin(a) * sz * 0.95)], col, 1.5)


def ic_moon(s, c, sz, col):
    """Crescent = filled disc minus an offset disc (alpha punch, so it works
    on any surface colour underneath, glass included)."""
    cx, cy = c
    l = s.layer()
    ld = ImageDraw.Draw(l)
    ld.ellipse(_scale([cx - sz * 0.92, cy - sz * 0.92, cx + sz * 0.92, cy + sz * 0.92]), fill=col)
    m = Image.new("L", s.img.size, 0)
    ImageDraw.Draw(m).ellipse(_scale([cx - sz * 0.30, cy - sz * 1.22,
                                      cx + sz * 1.40, cy + sz * 0.48]), fill=255)
    l.putalpha(ImageChops.subtract(l.getchannel("A"), m))
    s.comp(l)


def ic_image(s, c, sz, col):
    cx, cy = c
    s.g.rounded_rectangle([(cx - sz * 0.95), (cy - sz * 0.78),
                           (cx + sz * 0.95), (cy + sz * 0.78)],
                          radius=sz * 0.28, outline=col, width=max(1, 1.5))
    _o(s, cx - sz * 0.36, cy - sz * 0.28, sz * 0.17, col, 1.4)
    _p(s, [(cx - sz * 0.8, cy + sz * 0.62), (cx - sz * 0.15, cy - sz * 0.10),
           (cx + sz * 0.35, cy + sz * 0.40), (cx + sz * 0.62, cy + sz * 0.16),
           (cx + sz * 0.9, cy + sz * 0.62)], col, 1.5)


def ic_sparkle(s, c, sz, col):
    cx, cy = c
    s.g.polygon([(cx, cy - sz), (cx + sz * 0.26, cy - sz * 0.26), (cx + sz, cy),
                 (cx + sz * 0.26, cy + sz * 0.26), (cx, cy + sz),
                 (cx - sz * 0.26, cy + sz * 0.26), (cx - sz, cy),
                 (cx - sz * 0.26, cy - sz * 0.26)], fill=col)


def ic_robot(s, c, sz, col):
    cx, cy = c
    s.g.rounded_rectangle([(cx - sz * 0.82), (cy - sz * 0.5),
                           (cx + sz * 0.82), (cy + sz * 0.85)],
                          radius=sz * 0.34, outline=col, width=max(1, 1.5))
    _o(s, cx - sz * 0.3, cy + sz * 0.1, sz * 0.13, col, 1.6)
    _o(s, cx + sz * 0.3, cy + sz * 0.1, sz * 0.13, col, 1.6)
    _p(s, [(cx, cy - sz * 0.5), (cx, cy - sz * 0.92)], col, 1.5)
    _o(s, cx, cy - sz * 1.02, sz * 0.12, col, 1.4)


def ic_mic(s, c, sz, col):
    cx, cy = c
    s.g.rounded_rectangle([(cx - sz * 0.3), (cy - sz * 0.95),
                           (cx + sz * 0.3), (cy + sz * 0.2)],
                          radius=sz * 0.3, outline=col, width=max(1, 1.5))
    s.g.arc([(cx - sz * 0.62), (cy - sz * 0.32),
             (cx + sz * 0.62), (cy + sz * 0.62)], 0, 180, fill=col,
            width=max(1, 1.5))
    _p(s, [(cx, cy + sz * 0.62), (cx, cy + sz * 0.95)], col, 1.5)
    _p(s, [(cx - sz * 0.34, cy + sz * 0.95), (cx + sz * 0.34, cy + sz * 0.95)], col, 1.5)


def ic_refresh(s, c, sz, col):
    cx, cy = c
    s.g.arc([(cx - sz * 0.85), (cy - sz * 0.85),
             (cx + sz * 0.85), (cy + sz * 0.85)], 40, 320, fill=col,
            width=max(1, 1.6))
    _p(s, [(cx + sz * 0.55, cy - sz * 0.72), (cx + sz * 0.9, cy - sz * 0.2),
           (cx + sz * 0.28, cy - sz * 0.16)], col, 1.5)


def ic_db(s, c, sz, col):
    cx, cy = c
    s.g.ellipse([(cx - sz * 0.82), (cy - sz * 0.92),
                 (cx + sz * 0.82), (cy - sz * 0.38)], outline=col,
                width=max(1, 1.5))
    _p(s, [(cx - sz * 0.82, cy - sz * 0.65), (cx - sz * 0.82, cy + sz * 0.62)], col, 1.5)
    _p(s, [(cx + sz * 0.82, cy - sz * 0.65), (cx + sz * 0.82, cy + sz * 0.62)], col, 1.5)
    s.g.arc([(cx - sz * 0.82), (cy + sz * 0.2),
             (cx + sz * 0.82), (cy + sz * 1.02)], 0, 180, fill=col,
            width=max(1, 1.5))
    s.g.arc([(cx - sz * 0.82), (cy - sz * 0.12),
             (cx + sz * 0.82), (cy + sz * 0.7)], 0, 180, fill=col,
            width=max(1, 1.3))


def ic_play(s, c, sz, col):
    cx, cy = c
    s.g.polygon([(cx - sz * 0.55, cy - sz * 0.85), (cx + sz * 0.85, cy),
                 (cx - sz * 0.55, cy + sz * 0.85)], fill=col)


def ic_key(s, c, sz, col):
    cx, cy = c
    _o(s, cx - sz * 0.45, cy - sz * 0.35, sz * 0.42, col, 1.6)
    _p(s, [(cx - sz * 0.15, cy - sz * 0.02), (cx + sz * 0.9, cy + sz * 0.85)], col, 1.6)
    _p(s, [(cx + sz * 0.5, cy + sz * 0.5), (cx + sz * 0.8, cy + sz * 0.2)], col, 1.6)


def ic_star(s, c, sz, col):
    cx, cy = c
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = sz if i % 2 == 0 else sz * 0.44
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    s.g.polygon(pts, fill=col)


def ic_share(s, c, sz, col):
    cx, cy = c
    pts = [(cx - sz * 0.7, cy - sz * 0.55), (cx + sz * 0.75, cy - sz * 0.85), (cx + sz * 0.7, cy + sz * 0.7)]
    _p(s, [pts[0], pts[1]], col, 1.4)
    _p(s, [pts[0], pts[2]], col, 1.4)
    for p in pts:
        s.g.ellipse([(p[0] - sz * 0.24), (p[1] - sz * 0.24),
                     (p[0] + sz * 0.24), (p[1] + sz * 0.24)], fill=col)


def ic_info(s, c, sz, col):
    cx, cy = c
    _o(s, cx, cy, sz * 0.9, col, 1.6)
    _p(s, [(cx, cy - sz * 0.35), (cx, cy + sz * 0.5)], col, 1.6)
    _o(s, cx, cy - sz * 0.62, sz * 0.08, col, 2.4)


def ic_menu(s, c, sz, col):
    cx, cy = c
    for i, dy in enumerate((-0.55, 0, 0.55)):
        _p(s, [(cx - sz, cy + sz * dy), (cx + sz, cy + sz * dy)], col, 1.6)


def ic_chevr(s, c, sz, col):
    cx, cy = c
    _p(s, [(cx - sz * 0.4, cy - sz * 0.75), (cx + sz * 0.45, cy),
           (cx - sz * 0.4, cy + sz * 0.75)], col, 1.6)


def ic_check(s, c, sz, col):
    cx, cy = c
    _p(s, [(cx - sz * 0.85, cy + sz * 0.05), (cx - sz * 0.2, cy + sz * 0.7),
           (cx + sz * 0.9, cy - sz * 0.7)], col, 2.0)


def ic_download(s, c, sz, col):
    cx, cy = c
    _p(s, [(cx, cy - sz * 0.9), (cx, cy + sz * 0.35)], col, 1.7)
    _p(s, [(cx - sz * 0.45, cy - sz * 0.05), (cx, cy + sz * 0.42),
           (cx + sz * 0.45, cy - sz * 0.05)], col, 1.7)
    _p(s, [(cx - sz * 0.85, cy + sz * 0.8), (cx + sz * 0.85, cy + sz * 0.8)], col, 1.7)


def ic_trash(s, c, sz, col):
    cx, cy = c
    _p(s, [(cx - sz * 0.85, cy - sz * 0.55), (cx + sz * 0.85, cy - sz * 0.55)], col, 1.6)
    _p(s, [(cx - sz * 0.6, cy - sz * 0.55), (cx - sz * 0.5, cy + sz * 0.85),
           (cx + sz * 0.5, cy + sz * 0.85), (cx + sz * 0.6, cy - sz * 0.55)], col, 1.6)
    _p(s, [(cx - sz * 0.3, cy - sz * 0.55), (cx - sz * 0.3, cy - sz * 0.95),
           (cx + sz * 0.3, cy - sz * 0.95), (cx + sz * 0.3, cy - sz * 0.55)], col, 1.6)


def ic_copy(s, c, sz, col):
    cx, cy = c
    s.g.rounded_rectangle([(cx - sz * 0.9), (cy - sz * 0.9),
                           (cx + sz * 0.25), (cy + sz * 0.25)],
                          radius=sz * 0.22, outline=col, width=max(1, 1.5))
    s.g.rounded_rectangle([(cx - sz * 0.25), (cy - sz * 0.25),
                           (cx + sz * 0.9), (cy + sz * 0.9)],
                          radius=sz * 0.22, outline=col, width=max(1, 1.5))


def ic_bolt(s, c, sz, col):
    cx, cy = c
    s.g.polygon([(cx + sz * 0.15, cy - sz), (cx - sz * 0.75, cy + sz * 0.12),
                 (cx - sz * 0.05, cy + sz * 0.12), (cx - sz * 0.2, cy + sz),
                 (cx + sz * 0.8, cy - sz * 0.18), (cx + sz * 0.05, cy - sz * 0.18)], fill=col)


def ic_warn(s, c, sz, col):
    cx, cy = c
    s.g.polygon([(cx, cy - sz * 0.95), (cx + sz * 0.95, cy + sz * 0.8),
                 (cx - sz * 0.95, cy + sz * 0.8)], outline=col, width=max(1, 1.6))
    _p(s, [(cx, cy - sz * 0.3), (cx, cy + sz * 0.28)], col, 1.7)
    _o(s, cx, cy + sz * 0.56, sz * 0.09, col, 2.2)


def ic_type(s, c, sz, col):
    s.g.text((c[0], c[1]), "Aa", font=s.font(int(sz * 1.15), True), fill=col, anchor="mm")


def ic_density(s, c, sz, col):
    cx, cy = c
    for i, (w, dy) in enumerate(((1.0, -0.6), (0.75, 0.0), (0.9, 0.6))):
        _p(s, [(cx - sz * w, cy + sz * dy), (cx + sz * w, cy + sz * dy)], col, 1.6 + i * 0.5)


def ic_grid(s, c, sz, col):
    cx, cy = c
    for dx in (-0.5, 0.5):
        for dy in (-0.5, 0.5):
            s.g.rounded_rectangle([(cx + sz * dx - sz * 0.38), (cy + sz * dy - sz * 0.38),
                                   (cx + sz * dx + sz * 0.38), (cy + sz * dy + sz * 0.38)],
                                  radius=sz * 0.12, outline=col, width=max(1, 1.4))


def ic_layers(s, c, sz, col):
    cx, cy = c
    for i, dy in enumerate((-0.55, 0, 0.55)):
        _p(s, [(cx - sz, cy + sz * dy), (cx, cy + sz * (dy - 0.35)),
               (cx + sz, cy + sz * dy), (cx, cy + sz * (dy + 0.35)), (cx - sz, cy + sz * dy)], col, 1.4)


def ic_wave(s, c, sz, col):
    cx, cy = c
    _p(s, [(cx - sz, cy), (cx - sz * 0.6, cy - sz * 0.45), (cx - sz * 0.2, cy + sz * 0.4),
           (cx + sz * 0.2, cy - sz * 0.7), (cx + sz * 0.6, cy + sz * 0.45),
           (cx + sz, cy)], col, 1.5)


ICONS = dict(gear=ic_gear, contrast=ic_contrast, sun=ic_sun, moon=ic_moon, image=ic_image,
             sparkle=ic_sparkle, robot=ic_robot, mic=ic_mic, refresh=ic_refresh, db=ic_db,
             play=ic_play, key=ic_key, star=ic_star, share=ic_share, info=ic_info,
             menu=ic_menu, chevr=ic_chevr, check=ic_check, download=ic_download,
             trash=ic_trash, copy=ic_copy, bolt=ic_bolt, warn=ic_warn, type=ic_type,
             density=ic_density, grid=ic_grid, layers=ic_layers, wave=ic_wave)

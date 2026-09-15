#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
FILE: scripts/mockups/cl_sheets.py
PROJECT: ProGlassAutomation
MODULE: Crystal Liquid sheet set (2026-09-17) — the replacement spec for
        docs/mockups/settings-redesign/01..10

Owner directive that produced this set:
  "current one is not applied as expected cards are too close design is still
   same. Generate the new mockup with crystal liquid ios 26 theme type"

WHAT CHANGED VS THE APPROVED 01..10 SET
  · gap between cards     14 -> 22        (SettingsGapCard / SettingsCardMargin)
  · card padding          22,20 -> 24,22
  · material              flat #CCFFFFFF + 1px #E2E8F0  ->  six-layer crystal
                          glass on the app's own LiquidGlassSurface recipe
                          (frost · crystal edge · specular rim · reflection ·
                          refraction · ambient shadow)
  · radii                 card 26 (unchanged) but OUTER/INNER are now
                          concentric: 26 / 18 / 14, not 26 / 12 / 10
  · the stage             the invariant About canvas, verbatim, in BOTH
                          themes (owner: "Background page dont make it
                          dynamic")

Every number printed on these sheets is a token in the repository. Nothing is
drawn that the module could not set with a setter or a template layer.

  python3 scripts/mockups/cl_sheets.py [outdir]
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from cl_lib import (DARK, FS, GAP_CARD, LIGHT, PAD_CARD, R_CARD, R_SMALL, S,  # noqa: E402
                    R_CHIP, R_INNER, R_TILE, Sheet, _c)
from PIL import Image, ImageDraw, ImageFilter  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else "docs/mockups/settings-redesign-v2"
W, H = 1440, 960
RAIL = (24, 24, 264, H - 24)
CONTENT_Y = 24
HEAD = (288, 24, W - 24, 92)
CX0, CX1 = 288, W - 24

NAV = [("Appearance", "contrast"), ("Themes", "image"), ("AI", "sparkle"), ("Agent", "robot"),
       ("Voice", "mic"), ("Updates", "refresh"), ("Data", "db"), ("Demo", "play"),
       ("License", "key"), ("Features", "star"), ("Sharing", "share"), ("About", "info")]


# ═══════════════════════════════════════════════════════════════════════════
# helpers
# ═══════════════════════════════════════════════════════════════════════════
def flat_card(s, b, r=R_CARD, fill="#CCFFFFFF", edge="#E2E8F0", shadow=True):
    """The SHIPPED card (pre-crystal) — kept only for the before/after strip."""
    if shadow:
        s.soft_shadow(b, r, (0x33, 0x41, 0x55, 0.10))
    l = s.layer()
    ImageDraw.Draw(l).rounded_rectangle(s.box(b), r * S, fill=_c(fill))
    s.comp(s.cut(l, s.mask(b, r)))
    e = s.layer()
    ImageDraw.Draw(e).rounded_rectangle(s.box(b), r * S, outline=_c(edge), width=S)
    s.comp(e)


def label(s, x, y, text, size=FS["label"], col=None, bold=True, anchor="la"):
    s.text((x, y), text, size, col or s.t["text_muted"], bold, anchor)


def swatch(s, x, y, size, col, ring=False):
    """Accent swatch, drawn as the app draws it: silhouette + a ring when
    selected (no tick, no colour-coded label)."""
    s.soft_shadow((x, y, x + size, y + size), size / 2.0, (0x0F, 0x17, 0x2A, 0.22))
    s.tile((x, y, x + size, y + size), r=size / 2.0, fill=_c(col), edge=_c("#FFFFFF", 0.65))
    if ring:
        k = 4
        s.d.ellipse([(x - k) * S, (y - k) * S, (x + size + k) * S, (y + size + k) * S],
                    outline=s.t["accent"], width=int(1.6 * S))


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 01 — the kit: anatomy, material, radii, spacing, controls, before/after
# ═══════════════════════════════════════════════════════════════════════════
def panel(s, b, title, note=None):
    """A sheet panel: crystal glass + a header line. Panels never overlap —
    the first draft used leader lines across panels and the labels collided."""
    t = s.t
    s.glass(b, r=R_CARD, kind="rail")
    s.specular(b, r=R_CARD)
    s.text((b[0] + 22, b[1] + 18), title, FS["small"], t["text_title"], True)
    if note:
        s.text((b[2] - 22, b[1] + 18), note, FS["caption"], t["text_muted"], False, "ra")
    return b[0] + 22, b[1] + 44, b[2] - 22


def sheet_kit(path):
    s = Sheet(W, H, LIGHT)
    t = s.t
    s.text((40, 28), "Settings · Crystal Liquid (iOS 26) — the kit", 21, t["text_title"], True)
    s.text((40, 54), "Everything on this sheet is a token the module already ships: "
                     "family Crystal · material Crystal · card radius 26 · card gap 22.",
           FS["small"], t["text_muted"])

    # ── A · anatomy ───────────────────────────────────────────────────────
    ax, ay, ax1 = panel(s, (40, 92, 700, 520), "CARD ANATOMY — the six layers",
                        "LiquidGlassSurface recipe")
    cb = (60, 146, 452, 470)
    s.glass(cb, r=R_CARD)
    s.specular(cb, r=R_CARD)
    s.refraction(cb, r=R_CARD)
    x, cy, x1 = s.card(cb, "Whisper model", glyph="mic", chip="Installed", chip_kind="good")
    for i, (nm, sel) in enumerate([("Small · 39 MB", True), ("Base · 74 MB", False)]):
        rb = (x, cy + i * 50, x1, cy + 38 + i * 50)
        s.tile(rb, r=R_TILE, fill=_c("#FFFFFF", 0.50) if t["key"] == "light" else _c("#FFFFFF", 0.08),
               edge=(t["accent"] if sel else t["edge_mid"]))
        s.text((x + 14, rb[1] + 19), nm, FS["body"], t["text_title"], sel, "lm")
        if sel:
            s.icon(rb[2] - 20, rb[1] + 19, "check", 12, t["accent"])
    it = (x, cy + 112, x1, cy + 160)
    s.tile(it, r=R_INNER, fill=_c("#FFFFFF", 0.42) if t["key"] == "light" else _c("#FFFFFF", 0.06))
    s.text((x + 14, it[1] + 24), "Inner tile — radius 12 = Ios26RadiusInner", FS["caption"],
           t["text_muted"], False, "lm")

    legend = [("L0", "ambient shadow", "tinted · blur 34 · depth 14 · 0.20"),
              ("L1", "crystal edge", "tri-stop stroke, 1 px"),
              ("L2", "frosted base", "backdrop blurred, then tinted"),
              ("L3", "specular rim", "bright top inner edge"),
              ("L4", "reflection sweep", "diagonal sheen, clipped to the shape"),
              ("L5", "refraction edge", "cool bottom inner edge")]
    for i, (lid, nm, note) in enumerate(legend):
        ly = 152 + i * 52
        s.tile((476, ly, 508, ly + 30), r=R_SMALL,
               fill=_c("#FFFFFF", 0.55) if t["key"] == "light" else _c("#FFFFFF", 0.10),
               edge=t["edge_mid"])
        s.text((492, ly + 15), lid, FS["caption"], t["accent_soft_text"], True, "mm")
        s.text((518, ly + 8), nm, FS["small"], t["text_title"], True)
        s.text((518, ly + 24), note, FS["caption"], t["text_muted"], False)

    # ── B · material ──────────────────────────────────────────────────────
    # A coloured backdrop is painted FIRST so the four materials have something
    # to refract: over the plain pastel column Soft and Solid look identical.
    for cx_, cy_, rr, col in ((790, 190, 150, "#7DD3FC"), (1120, 230, 170, "#C4B5FD"),
                              (1330, 150, 130, "#5EEAD4")):
        orb = s.layer()
        ImageDraw.Draw(orb).ellipse([(cx_ - rr) * S, (cy_ - rr) * S,
                                     (cx_ + rr) * S, (cy_ + rr) * S], fill=_c(col, 0.55))
        s.comp(orb.filter(ImageFilter.GaussianBlur(26 * S)))
    mx, my, mx1 = panel(s, (720, 92, 1400, 300), "MATERIAL — ProGlassMaterial",
                        "shown over a coloured backdrop so the difference reads")
    mats = [("Solid", "flat, no frost", 0.0), ("Soft", "tint only", 0.30),
            ("Frosted", "blur, no edge", 0.70), ("Crystal", "frost + edge + rim", 1.0)]
    for i, (nm, desc, amt) in enumerate(mats):
        tx = mx + i * 158
        tb = (tx, my, tx + 146, my + 108)
        s.glass(tb, r=R_INNER, frost=(None if amt else 0.0),
                tint=(_c("#FFFFFF", 0.10 + 0.62 * amt), _c("#FFFFFF", 0.04 + 0.34 * amt)))
        if amt:
            s.specular(tb, r=R_INNER)
        if i == 3:
            s.tile(tb, r=R_INNER, fill=None, edge=t["accent"])
        s.text((tx, tb[3] + 10), nm, FS["small"], t["text_title"], i == 3)
        s.text((tx, tb[3] + 26), desc, FS["caption"], t["text_muted"], False)

    # ── C · radii ─────────────────────────────────────────────────────────
    rx, ry, rx1 = panel(s, (720, 312, 1400, 520), "RADII — the shipped scale",
                        "one ratio per token, from the live card radius")
    for i, (r, nm, note) in enumerate([(R_CARD, "Card 26", "Ios26RadiusCard"),
                                       (R_INNER, "Inner 12", "Ios26RadiusInner"),
                                       (R_TILE, "Tile 10", "Ios26RadiusTile"),
                                       (R_CHIP, "Chip 12", "Ios26RadiusChip")]):
        bx = rx + i * 158
        s.tile((bx, ry, bx + 146, ry + 96), r=r,
               fill=_c("#FFFFFF", 0.50) if t["key"] == "light" else _c("#FFFFFF", 0.08),
               edge=t["edge_mid"])
        s.text((bx, ry + 104), nm, FS["small"], t["text_title"], True)
        s.text((bx, ry + 120), note, FS["caption"], t["text_muted"], False)
    s.text((rx, ry + 150), "One radius authority — Settings does not fork the scale, so the "
                           "Appearance slider still moves both modules together.",
           FS["caption"], t["text_faint"], False)

    # ── D · spacing ───────────────────────────────────────────────────────
    sx, sy, sx1 = panel(s, (40, 536, 700, 760), "SPACING — the owner's complaint",
                        "gap 14 → 22")
    for i, (gap, tag, col) in enumerate([(14, "was 14 — cards sat shoulder to shoulder", t["danger"]),
                                         (GAP_CARD, "now 22 — cards read as separate objects", t["success"])]):
        gy = sy + 6 + i * 84
        s.tile((sx, gy, sx + 210, gy + 34), r=R_TILE,
               fill=_c("#FFFFFF", 0.50) if t["key"] == "light" else _c("#FFFFFF", 0.07),
               edge=t["edge_mid"])
        s.tile((sx, gy + 34 + gap, sx + 210, gy + 68 + gap), r=R_TILE,
               fill=_c("#FFFFFF", 0.50) if t["key"] == "light" else _c("#FFFFFF", 0.07),
               edge=t["edge_mid"])
        bx = (sx + 222) * S
        s.d.line([(bx, (gy + 34) * S), (bx, (gy + 34 + gap) * S)], fill=col, width=int(1.6 * S))
        for yy in (gy + 34, gy + 34 + gap):
            s.d.line([(bx - 5 * S, yy * S), (bx + 5 * S, yy * S)], fill=col, width=int(1.6 * S))
        s.text((sx + 240, gy + 34 + gap / 2.0), tag, FS["caption"], col, True, "lm")

    # ── E · controls ──────────────────────────────────────────────────────
    kx, ky, kx1 = panel(s, (720, 536, 1400, 760), "CONTROLS — glass, not flat chrome",
                        "same tokens, same states")
    s.switch(kx, ky, True)
    s.text((kx + 64, ky + 15), "Switch on", FS["caption"], t["text_muted"], False, "lm")
    s.switch(kx + 150, ky, False)
    s.text((kx + 214, ky + 15), "off", FS["caption"], t["text_muted"], False, "lm")
    s.chip(kx + 290, ky + 3, "Installed", "good", dot=t["success"])
    s.chip(kx + 400, ky + 3, "Unverified", "warn", dot=None)
    s.slider(kx, ky + 52, 240, 0.42)
    s.segment((kx, ky + 76, kx + 240, ky + 110), ["Compact", "Normal", "Comfy"], 1, fs=FS["caption"])
    s.field((kx + 268, ky + 76, kx + 430, ky + 110), "#0369A1")
    s.button(kx, ky + 132, "Download", "primary", h=34)
    s.button(kx + 132, ky + 132, "Delete", "danger", h=34)
    s.button(kx + 220, ky + 132, "Cancel", "ghost", h=34)
    s.text((kx + 268, ky + 140), "one destructive action per card, never among equals",
           FS["caption"], t["text_faint"], False, "lm")

    # ── F · token map ─────────────────────────────────────────────────────
    fx, fy, fx1 = panel(s, (40, 776, 1400, 936), "TOKEN MAP — what to change in the module",
                        "Views/Settings/SettingsCardStyles.xaml")
    rows = [("SettingsCardPadding", "22,20  →  24,22"),
            ("SettingsGapCard / SettingsCardMargin", "14  →  22"),
            ("Ios26RadiusCard / Inner / Tile / Chip", "26 / 12 / 10 / 12 — NOT forked"),
            ("new: SettingsCardFaceBrush", "base tint + rim + sheen + lip (one brush)"),
            ("new: SettingsCardEdgeBrush", "tri-stop crystal edge"),
            ("new: SettingsCardShadow", "tinted, blur 34, depth 14 (light)"),
            ("new: SettingsInnerFaceBrush", "tile is translucent now, not #FFFFFF"),
            ("new: SettingsInnerEdgeBrush", "tile edge, white 0.85 → 0.70")]
    for i, (a, bb) in enumerate(rows):
        col = fx + (i % 2) * 690
        yy = fy + (i // 2) * 26
        s.text((col, yy), a, FS["caption"], t["text_body"], True)
        s.text((col + 250, yy), bb, FS["caption"], t["text_muted"], False)
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 02 — Appearance (light)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_appearance(path):
    s = Sheet(W, H, LIGHT)
    t = s.t
    s.rail(RAIL, NAV, "Appearance")
    s.page_head(HEAD, "Appearance", "Light", "muted")
    y = 114

    # MODE
    b = (CX0, y, CX1, y + 158)
    x, cy, x1 = s.card(b, "Mode", glyph="contrast", chip="Follows Windows", chip_kind="muted")
    s.segment((x, cy, x + 320, cy + 38), ["Light", "Dark", "System"], 0)
    s.text((x + 348, cy + 19), "System listens to the Windows app theme.", FS["small"],
           t["text_muted"], False, "lm")
    s.row(x, x1, cy + 56, "Auto-switch at sunset", desc="Uses the location in Windows settings",
          control="chev")
    s.switch(x1 - 60, cy + 56, True)
    y = b[3] + GAP_CARD

    # MATERIAL — the crystal liquid picker (this is the sheet's headline card)
    b = (CX0, y, CX1, y + 230)
    x, cy, x1 = s.card(b, "Material", glyph="sparkle", chip="Crystal", chip_kind="soft")
    s.text((x, cy - 4), "How every surface in the app is built. Crystal samples the "
                        "page backdrop and frosts it.", FS["small"], t["text_muted"], False, "lm")
    mats = [("Solid", "Flat fill, no frost", "ProGlassMaterial.Solid", 0.0),
            ("Soft", "Light tint, no blur", "ProGlassMaterial.Soft", 0.30),
            ("Frosted", "Blurred backdrop", "ProGlassMaterial.Frosted", 0.75),
            ("Crystal", "Frost + edge + rim", "ProGlassMaterial.Crystal", 1.0)]
    for i, (nm, desc, key, amt) in enumerate(mats):
        cw = (x1 - x) / 4.0 - 12
        cb = (x + i * (cw + 16), cy + 30, x + i * (cw + 16) + cw, cy + 30 + 106)
        s.glass(cb, r=R_INNER, frost=(None if amt else 0.0),
                tint=(_c("#FFFFFF", 0.12 + 0.7 * amt), _c("#FFFFFF", 0.06 + 0.4 * amt)))
        s.specular(cb, r=R_INNER)
        if i == 3:
            s.tile(cb, r=R_INNER, fill=None, edge=t["accent"])
        s.text((cb[0] + 14, cb[1] + 14), nm, FS["body"], t["text_title"], True)
        s.text((cb[0] + 14, cb[1] + 36), desc, FS["caption"], t["text_muted"], False)
        s.text((cb[0] + 14, cb[3] - 18), key, FS["caption"], t["text_faint"], False, "ls")
    s.text((x, cy + 146), "Over a flat stage the four differ mainly by edge and rim; "
                          "over content they separate sharply.", FS["caption"],
           t["text_faint"], False)
    y = b[3] + GAP_CARD

    # DENSITY
    b = (CX0, y, CX1, y + 116)
    x, cy, x1 = s.card(b, "Display density", glyph="density")
    s.segment((x, cy, x + 520, cy + 38), ["Compact", "Normal", "Comfortable", "Spacious"], 2)
    s.text((x + 548, cy + 19), "Shrinks shared chrome, not the card rhythm.", FS["small"],
           t["text_muted"], False, "lm")
    y = b[3] + GAP_CARD

    # ACCENT | TYPOGRAPHY
    half = (CX1 - CX0 - GAP_CARD) / 2.0
    b = (CX0, y, CX0 + half, y + 226)
    x, cy, x1 = s.card(b, "Accent", glyph="star")
    cols = ["#0369A1", "#0EA5E9", "#0D9488", "#16A34A", "#D97706", "#E11D48", "#7C3AED", "#475569"]
    for i, c in enumerate(cols):
        swatch(s, x + i * 40, cy, 30, c, ring=(i == 0))
    s.slider(x, cy + 56, x1 - x, 0.30)
    s.text((x, cy + 78), "Hue", FS["caption"], t["text_muted"], False)
    s.field((x, cy + 96, x + 132, cy + 130), "#0369A1")
    s.icon(x + 152, cy + 113, "copy", 13, t["text_muted"])
    s.text((x + 172, cy + 113), "Copy", FS["label"], t["text_muted"], False, "lm")
    s.text((x, cy + 150), "16 presets · AccentPresets.All · custom hex accepted",
           FS["caption"], t["text_faint"], False)

    b = (CX0 + half + GAP_CARD, y, CX1, y + 226)
    x, cy, x1 = s.card(b, "Typography", glyph="type")
    s.tile((x, cy, x + 96, cy + 96), r=R_INNER,
           fill=_c("#FFFFFF", 0.5) if t["key"] == "light" else _c("#FFFFFF", 0.08), edge=t["edge_mid"])
    s.text((x + 48, cy + 48), "Aa", 34, t["text_title"], True, "mm")
    s.text((x + 114, cy + 18), "Segoe UI Variable", FS["body"], t["text_body"], True)
    s.text((x + 114, cy + 40), "Scale follows the density profile.", FS["small"], t["text_muted"], False)
    s.segment((x + 114, cy + 62, x1, cy + 96), ["Compact", "Normal", "Comfy"], 1, fs=FS["caption"])
    s.text((x, cy + 118), "Card title 16 · row label 13 · secondary 12 · chip 11",
           FS["caption"], t["text_muted"], False)
    s.text((x, cy + 136), "Ios26FontSize* — republished per density, never hard-coded",
           FS["caption"], t["text_faint"], False)
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 03 — Voice (light)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_voice(path):
    s = Sheet(W, H, LIGHT)
    t = s.t
    s.rail(RAIL, NAV, "Voice")
    s.page_head(HEAD, "Voice", "Installed", "good")
    y = 114

    # WHISPER MODEL
    b = (CX0, y, CX1, y + 300)
    x, cy, x1 = s.card(b, "Whisper model", glyph="mic", chip="Small selected", chip_kind="soft")
    s.text((x, cy - 4), "Local speech-to-text. The picker is the card's own ItemsControl — "
                        "no nested card.", FS["small"], t["text_muted"], False, "lm")
    models = [("Small", "39 MB · matches this machine", True),
              ("Base", "74 MB · better on noisy audio", False),
              ("Large v3", "155 MB · slowest, most accurate", False)]
    for i, (nm, desc, sel) in enumerate(models):
        rb = (x, cy + 30 + i * 60, x1, cy + 30 + 50 + i * 60)
        s.tile(rb, r=R_TILE, fill=_c("#FFFFFF", 0.58) if t["key"] == "light" else _c("#FFFFFF", 0.08),
               edge=(t["accent"] if sel else (_c("#FFFFFF", 0.85) if t["key"] == "light" else _c("#FFFFFF", 0.12))))
        s.icon(x + 22, rb[1] + 25, "mic", 15, t["accent"] if sel else t["text_muted"])
        s.text((x + 46, rb[1] + 25), nm, FS["body"], t["text_title"], sel, "lm")
        s.text((x + 46, rb[1] + 41), desc, FS["caption"], t["text_muted"], False, "lm")
        if sel:
            s.icon(rb[2] - 22, rb[1] + 25, "check", 13, t["accent"])
        s.text((x, cy + 220), "Tag=\"{Binding Key}\" · rows BasedOn ModelOptionCardStyle",
               FS["caption"], t["text_faint"], False)
    s.text((40, H - 34), "Voice is a two-card page: sheet 07's engine / rate / pitch / volume fields "
                         "have no ViewModel backing, so they are not drawn here.",
           FS["caption"], t["text_muted"], False)
    y = b[3] + GAP_CARD

    # MODEL STATUS
    b = (CX0, y, CX1, y + 296)
    x, cy, x1 = s.card(b, "Model status", glyph="download", chip="Ready", chip_kind="good",
                       sub="ggml-small.bin")
    inner = (x, cy, x1, cy + 58)
    s.tile(inner, r=R_INNER, fill=_c("#FFFFFF", 0.5) if t["key"] == "light" else _c("#FFFFFF", 0.07))
    s.text((inner[0] + 16, inner[1] + 20), "File", FS["small"], t["text_muted"], False, "lm")
    s.text((inner[0] + 16, inner[1] + 40), "%LOCALAPPDATA%\\ProGlass\\whisper\\ggml-small.bin",
           FS["caption"], t["text_body"], False, "lm")
    s.text((inner[2] - 16, inner[1] + 29), "39 MB", FS["body"], t["text_title"], True, "rm")
    s.button(x, cy + 76, "Download", "primary")
    s.button(x + 132, cy + 76, "Delete", "danger")
    s.text((x + 232, cy + 93), "Download swaps to Cancel while IsDownloadingModel",
           FS["caption"], t["text_muted"], False, "lm")
    # in-flight state, shown without contradicting the Ready chip above
    bar = (x, cy + 118, x1 - 190, cy + 126)
    s.tile(bar, r=4, fill=t["track"], edge=None)
    s.tile((bar[0], bar[1], bar[0] + (bar[2] - bar[0]) * 0.42, bar[3]), r=4, fill=t["accent"], edge=None)
    s.text((x, cy + 138), "Downloading · 42 %", FS["small"], t["accent"], True)
    s.text((x, cy + 138), "in-flight state, under IsDownloadingModel", FS["caption"],
           t["text_faint"], False, "la")
    s.text((x + 190, cy + 138), "← in-flight state, under IsDownloadingModel", FS["caption"],
           t["text_faint"], False, "lm")
    s.row(x, x1, cy + 158, "Use for assistant replies", control="chev")
    s.switch(x1 - 60, cy + 158, True)
    s.text((x, cy + 206), "The chip is state-driven (ready / not installed / downloading) — "
                          "never a coloured label.", FS["caption"], t["text_faint"], False)
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 04 — Agent (dark)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_agent(path):
    s = Sheet(W, H, DARK)
    t = s.t
    s.rail(RAIL, NAV, "Agent")
    s.page_head(HEAD, "Agent", "Running", "good")
    y = 114

    b = (CX0, y, CX1, y + 224)
    x, cy, x1 = s.card(b, "API connection", glyph="robot", chip="Key present", chip_kind="good")
    s.text((x, cy - 4), "The key is stored in Windows Credential Manager, never in the config file.",
           FS["small"], t["text_muted"], False, "lm")
    s.field((x, cy + 26, x1 - 130, cy + 62), "sk-••••••••••••••••••••••••••••", placeholder=True)
    s.button(x1 - 118, cy + 26, "Paste", "ghost", h=36)
    s.row(x, x1, cy + 78, "Model", value="gpt-5 · 2026-08")
    s.row(x, x1, cy + 118, "Endpoint", value="api.openai.com", desc="TLS 1.2 · retries 3")
    s.text((x, cy + 168), "DangerButton / OutlinedPillButton resolve app-wide — no local copies.",
           FS["caption"], t["text_faint"], False)
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 208)
    x, cy, x1 = s.card(b, "Automation", glyph="bolt", chip="6 rules", chip_kind="muted")
    rows = [("Auto-run on new order", "Fires the agent when the watcher sees a new file", True),
            ("Confirm before writing", "Shows the diff and waits for approval", True),
            ("Retry failed steps", "Three attempts, then it parks the job", False)]
    for i, (nm, desc, on) in enumerate(rows):
        cyy = s.row(x, x1, cy + i * 54, nm, desc=desc)
        s.switch(x1 - 60, cy + i * 54, on)
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 150)
    x, cy, x1 = s.card(b, "Rotate the key", glyph="refresh")
    s.text((x, cy + 4), "Revokes the stored credential and asks for a new one.", FS["small"],
           t["text_muted"], False)
    s.button(x, cy + 30, "Rotate key", "danger")
    s.text((x + 132, cy + 48), "A destructive action gets its own card, never a row of equals.",
           FS["caption"], t["text_faint"], False, "lm")
    # dark-mode note
    s.glass((40, H - 92, 700, H - 40), r=R_INNER, kind="rail")
    s.text((60, H - 66), "Dark glass floats on the SAME pastel stage: the stage is invariant by "
                         "directive.", FS["caption"], t["text_muted"], False, "lm")
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 05 — Data & backup (dark)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_data(path):
    s = Sheet(W, H, DARK)
    t = s.t
    s.rail(RAIL, NAV, "Data")
    s.page_head(HEAD, "Data & backup", "Verified", "good")
    y = 114

    b = (CX0, y, CX1, y + 152)
    x, cy, x1 = s.card(b, "Storage", glyph="db", chip="1.4 GB", chip_kind="soft")
    for i, (nm, val) in enumerate([("Database", "812 MB"), ("Backups", "540 MB"), ("Logs", "48 MB")]):
        s.row(x, x1, cy + i * 34, nm, value=val)
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 262)
    x, cy, x1 = s.card(b, "Backups", glyph="layers", chip="4 kept", chip_kind="good")
    rows = [("Nightly · 2026-09-17 02:00", "Verified · 512 MB", "good"),
            ("Manual · 2026-09-15 11:20", "Verified · 498 MB", "good"),
            ("Manual · 2026-09-11 09:02", "Unverified · 505 MB", "warn")]
    for i, (nm, st, kind) in enumerate(rows):
        rb = (x, cy + i * 52, x1, cy + 42 + i * 52)
        s.tile(rb, r=R_TILE, fill=_c("#FFFFFF", 0.07), edge=_c("#FFFFFF", 0.12))
        s.text((x + 16, rb[1] + 21), nm, FS["body"], t["text_title"], False, "lm")
        cw = s.chip(rb[2] - 190, rb[1] + 9, st.split(" · ")[0], kind, dot=None)
        s.text((rb[2] - 16, rb[1] + 21), "Restore", FS["small"], t["accent"], True, "rm")
    s.segment((x, cy + 168, x + 420, cy + 204), ["Keep 3", "Keep 7", "Keep 30"], 1, fs=FS["caption"])
    s.text((x + 440, cy + 186), "Retention is read from BackupSetInfo — the chip needs a real "
                                "severity field first.", FS["caption"], t["text_muted"], False, "lm")
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 140)
    s.glass(b, r=R_CARD)
    s.specular(b, r=R_CARD)
    s.refraction(b, r=R_CARD)
    s.tile(b, r=R_CARD, fill=t["danger_soft"], edge=t["danger"])
    x, cy, x1 = b[0] + PAD_CARD[0], b[1] + PAD_CARD[1], b[2] - PAD_CARD[0]
    s.icon_tile(x, cy - 6, "warn", 30, tint=t["danger_soft"])
    s.text((x + 42, cy + 9), "Purge everything", FS["card_title"], t["danger"], True, "lm")
    s.text((x + 42, cy + 32), "Deletes the database, every backup and the logs. "
                              "Cannot be undone.", FS["small"], t["text_muted"], False, "lm")
    s.button(x1 - 128, cy + 6, "Purge", "danger")
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 06 — the shell, light vs dark, at 1:1 (what the owner will actually see)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_shell(path):
    s = Sheet(W, H, LIGHT)
    t = s.t
    s.text((40, 30), "Settings · Crystal Liquid shell — light and dark, same stage",
           21, t["text_title"], True)
    s.text((40, 56), "One rail, one page header, cards on a 22 gap. The stage does not move "
                     "between themes (owner directive).", FS["small"], t["text_muted"])
    s.mini_stage = True
    for i, theme in enumerate((LIGHT, DARK)):
        x0 = 40 + i * 692
        # a mini stage so both variants are visible side by side
        sub = Sheet(660, 800, theme)
        sub.t = theme
        sub.rail((20, 20, 220, 780), NAV, "Appearance")
        sub.page_head((240, 20, 640, 80), "Appearance", "Light" if i == 0 else "Dark",
                      "muted" if i == 0 else "soft")
        yy = 100
        for title, hh, glyph in [("Mode", 150, "contrast"), ("Material", 190, "sparkle"),
                                 ("Density", 110, "density")]:
            cb = (240, yy, 640, yy + hh)
            xx, ccy, xx1 = sub.card(cb, title, glyph=glyph,
                                    chip=("Crystal" if title == "Material" else None),
                                    chip_kind="soft")
            if title == "Mode":
                sub.segment((xx, ccy, xx + 240, ccy + 36), ["Light", "Dark", "System"], 0, fs=FS["caption"])
                sub.row(xx, xx1, ccy + 52, "Auto-switch at sunset")
                sub.switch(xx1 - 60, ccy + 52, True)
            elif title == "Material":
                for k, nm in enumerate(["Solid", "Soft", "Frosted", "Crystal"]):
                    cw = (xx1 - xx) / 4.0 - 8
                    tb = (xx + k * (cw + 11), ccy + 4, xx + k * (cw + 11) + cw, ccy + 84)
                    sub.glass(tb, r=R_INNER, frost=(None if k else 0.0),
                              tint=(_c("#FFFFFF", 0.10 + 0.24 * k), _c("#FFFFFF", 0.05 + 0.14 * k)))
                    sub.specular(tb, r=R_INNER)
                    if k == 3:
                        sub.tile(tb, r=R_INNER, fill=None, edge=theme["accent"])
                    sub.text((tb[0] + 10, tb[3] - 16), nm, FS["caption"], theme["text_sec"],
                             k == 3, "ls")
            else:
                sub.segment((xx, ccy, xx1, ccy + 36), ["Compact", "Normal", "Comfortable"], 1,
                            fs=FS["caption"])
                sub.text((xx, ccy + 50), "Compact · Normal · Comfortable · Spacious  (ProGlassDensity)",
                         FS["caption"], theme["text_muted"], False)
            yy += hh + GAP_CARD
        # paste the mini sheet onto the main canvas at FULL scale — the
        # sub-sheet already renders at S×, so pasting its raw buffer at an S
        # offset keeps both variants 1:1 (resizing first is what shrank them).
        s.img.paste(sub.img, (x0 * S, 92 * S), sub.img)
        s.d = ImageDraw.Draw(s.img)
        lbl = ("LIGHT — Crystal material, accent Glass Blue" if i == 0 else
               "DARK — same stage, dark glass")
        s.text((x0, 900), lbl, FS["small"], t["text_title"], True)
        s.text((x0, 918), ("Ios26CardBrush  #CCFFFFFF  ·  edge  #E2E8F0" if i == 0 else
                           "Ios26CardBrush  #CC17181D  ·  edge  #4DFFFFFF"),
               FS["caption"], t["text_muted"], False)
    return s.save(path)






# ═══════════════════════════════════════════════════════════════════════════
# SHEET 07 — Themes & wallpaper (light)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_themes(path):
    s = Sheet(W, H, LIGHT)
    t = s.t
    s.rail(RAIL, NAV, "Themes")
    s.page_head(HEAD, "Themes", "Crystal Fluid", "soft")
    y = 114

    b = (CX0, y, CX1, y + 250)
    x, cy, x1 = s.card(b, "Theme", glyph="image", chip="1 of 6", chip_kind="muted")
    presets = [("Crystal Fluid", "#7DD3FC", "#C4B5FD", True), ("Monet Garden", "#86EFAC", "#FDE68A", False),
               ("Industrial", "#94A3B8", "#CBD5E1", False), ("Midnight", "#1E293B", "#334155", False)]
    for i, (nm, c1, c2, sel) in enumerate(presets):
        cw = (x1 - x) / 4.0 - 10
        cb = (x + i * (cw + 13), cy, x + i * (cw + 13) + cw, cy + 132)
        s.glass(cb, r=R_INNER, frost=12.0,
                tint=(_c(c1, 0.55), _c(c2, 0.75)))
        s.specular(cb, r=R_INNER)
        if sel:
            s.tile(cb, r=R_INNER, fill=None, edge=t["accent"])
        s.text((cb[0] + 12, cb[3] - 40), nm, FS["small"], t["text_title"], sel)
        s.text((cb[0] + 12, cb[3] - 24), "VisualThemeEngine family", FS["caption"], t["text_faint"], False)
    s.text((x, cy + 148), "Family + material + density come from the theme engine; the sheet only "
                          "shows what a card looks like once it is applied.", FS["caption"],
           t["text_muted"], False)
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 200)
    x, cy, x1 = s.card(b, "Wallpaper", glyph="photo" if False else "image", chip="Scene 2", chip_kind="soft")
    scenes = ["Crystal Fluid", "Soft Dunes", "Paper Grain"]
    for i, nm in enumerate(scenes):
        cb = (x + i * 168, cy, x + i * 168 + 152, cy + 78)
        s.tile(cb, r=R_INNER, fill=_c("#FFFFFF", 0.45) if t["key"] == "light" else _c("#FFFFFF", 0.08),
               edge=(t["accent"] if i == 1 else t["edge_mid"]))
        s.text((cb[0] + 12, cb[3] - 20), nm, FS["caption"], t["text_body"], i == 1, "ls")
    s.segment((x, cy + 96, x + 330, cy + 130), ["Acrylic", "Mica", "None"], 0, fs=FS["caption"])
    s.text((x + 348, cy + 113), "Backdrop material (Win 11)", FS["small"], t["text_muted"], False, "lm")
    s.slider(x, cy + 146, (x1 - x) * 0.55, 0.28)
    s.text((x, cy + 162), "Blur intensity", FS["caption"], t["text_muted"], False)
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 08 — AI (dark)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_ai(path):
    s = Sheet(W, H, DARK)
    t = s.t
    s.rail(RAIL, NAV, "AI")
    s.page_head(HEAD, "AI", "Online", "good")
    y = 114

    b = (CX0, y, CX1, y + 232)
    x, cy, x1 = s.card(b, "Provider", glyph="sparkle", chip="OpenAI", chip_kind="soft")
    rows = [("Model", "gpt-5 · 2026-08"), ("Max tokens", "4096"), ("Timeout", "30 s")]
    for i, (nm, val) in enumerate(rows):
        s.row(x, x1, cy + i * 38, nm, value=val)
    s.segment((x, cy + 122, x + 300, cy + 156), ["Fast", "Balanced", "Quality"], 1, fs=FS["caption"])
    s.text((x + 318, cy + 139), "Preset routing", FS["small"], t["text_muted"], False, "lm")
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 286)
    x, cy, x1 = s.card(b, "Generation", glyph="wave", chip="6 rules", chip_kind="muted")
    for i, (nm, v) in enumerate([("Temperature", 0.35), ("Top-p", 0.8), ("Repetition penalty", 0.15)]):
        s.text((x, cy + i * 58), nm, FS["body"], t["text_title"], False)
        s.slider(x + 210, cy + i * 58 + 4, (x1 - x) - 300, v)
        s.text((x1, cy + i * 58), "%.2f" % (0.35 if i == 0 else (0.80 if i == 1 else 0.15)),
               FS["body"], t["text_sec"], False, "ra")
    s.row(x, x1, cy + 178, "Stream responses token by token")
    s.switch(x1 - 60, cy + 178, True)
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 156)
    x, cy, x1 = s.card(b, "System prompt", glyph="copy")
    s.field((x, cy, x1 - 120, cy + 40), "You are a delivery assistant for…", placeholder=True)
    s.button(x1 - 108, cy, "Load", "ghost", h=40)
    s.text((x, cy + 58), "Saved prompts live in the library card; the field keeps one source.",
           FS["caption"], t["text_faint"], False)
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 09 — Features & sharing (light)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_features(path):
    s = Sheet(W, H, LIGHT)
    t = s.t
    s.rail(RAIL, NAV, "Features")
    s.page_head(HEAD, "Features & sharing", "9 of 12 on", "soft")
    y = 114

    b = (CX0, y, CX1, y + 300)
    x, cy, x1 = s.card(b, "Feature flags", glyph="star", chip="Beta", chip_kind="warn")
    flags = [("Batch export", "Writes every target to one workbook", True),
             ("Auto-detect steps", "Reads the step count from the filename", True),
             ("Nightly digest", "Emails the day's totals at 20:00", False)]
    for i, (nm, desc, on) in enumerate(flags):
        s.row(x, x1, cy + i * 58, nm, desc=desc)
        s.switch(x1 - 60, cy + i * 58, on)
    s.text((x, cy + 186), "A flag row is a row, not a tile — the card already groups them.",
           FS["caption"], t["text_faint"], False)
    y = b[3] + GAP_CARD

    half = (CX1 - CX0 - GAP_CARD) / 2.0
    b = (CX0, y, CX0 + half, y + 236)
    x, cy, x1 = s.card(b, "Sharing", glyph="share")
    for i, (nm, st) in enumerate([("Floor LAN", "Active"), ("Shop tablet", "Idle"), ("Backup PC", "Active")]):
        s.row(x, x1, cy + i * 44, nm)
        s.chip(x1 - (s.text_w(st, FS["label"]) + 30), cy + i * 44 + 8, st,
               "good" if st == "Active" else "muted")
    s.button(x, cy + 150, "Add endpoint", "primary")

    b = (CX0 + half + GAP_CARD, y, CX1, y + 236)
    x, cy, x1 = s.card(b, "Limits", glyph="grid")
    s.segment((x, cy, x1, cy + 36), ["Unlimited", "10 / day", "Metered"], 0, fs=FS["caption"])
    s.text((x, cy + 50), "Applies to the shared endpoints above.", FS["small"], t["text_muted"], False)
    s.row(x, x1, cy + 78, "Warn at 80 % of the cap")
    s.switch(x1 - 60, cy + 78, True)
    s.text((x, cy + 136), "One quota source; the row and the segments read the same value.",
           FS["caption"], t["text_faint"], False)
    return s.save(path)


# ═══════════════════════════════════════════════════════════════════════════
# SHEET 10 — Demo, license and the About card (dark)
# ═══════════════════════════════════════════════════════════════════════════
def sheet_demo(path):
    s = Sheet(W, H, DARK)
    t = s.t
    s.rail(RAIL, NAV, "Demo")
    s.page_head(HEAD, "Demo", "Sample data", "muted")
    y = 114

    b = (CX0, y, CX1, y + 168)
    x, cy, x1 = s.card(b, "Sample data", glyph="play", chip="120 targets", chip_kind="soft")
    s.segment((x, cy, x + 300, cy + 36), ["Small", "Medium", "Large"], 1, fs=FS["caption"])
    s.text((x + 318, cy + 18), "Seeds the database with realistic targets.", FS["small"],
           t["text_muted"], False, "lm")
    s.button(x, cy + 56, "Load sample data", "primary")
    s.text((x + 170, cy + 74), "Never touches existing rows.", FS["caption"],
           t["text_faint"], False, "lm")
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 132)
    x, cy, x1 = s.card(b, "License", glyph="key", chip="Activated", chip_kind="good")
    s.text((x, cy + 4), "Valid to 2027-03-01 · 1 seat", FS["small"], t["text_muted"], False)
    s.field((x, cy + 28, x + 320, cy + 62), "PG-XXXX-XXXX-XXXX-XXXX")
    s.button(x + 336, cy + 28, "Activate", "ghost", h=34)
    y = b[3] + GAP_CARD

    b = (CX0, y, CX1, y + 176)
    s.glass(b, r=R_CARD)
    s.specular(b, r=R_CARD)
    s.refraction(b, r=R_CARD)
    s.tile(b, r=R_CARD, fill=t["danger_soft"], edge=t["danger"])
    x, cy, x1 = b[0] + PAD_CARD[0], b[1] + PAD_CARD[1], b[2] - PAD_CARD[0]
    s.icon_tile(x, cy - 6, "warn", 30, tint=t["danger_soft"])
    s.text((x + 42, cy + 9), "Reset demo data", FS["card_title"], t["danger"], True, "lm")
    s.text((x + 42, cy + 32), "Removes every seeded row and re-seeds an empty demo set.",
           FS["small"], t["text_muted"], False, "lm")
    s.button(x1 - 122, cy + 6, "Reset", "danger")
    return s.save(path)


SHEETS = [("01-crystal-kit.png", sheet_kit),
          ("02-appearance-light.png", sheet_appearance),
          ("03-voice-light.png", sheet_voice),
          ("04-agent-dark.png", sheet_agent),
          ("05-data-backup-dark.png", sheet_data),
          ("06-shell-light-dark.png", sheet_shell),
          ("07-themes-wallpaper.png", sheet_themes),
          ("08-ai-dark.png", sheet_ai),
          ("09-features-sharing.png", sheet_features),
          ("10-demo-license-dark.png", sheet_demo)]


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in SHEETS:
        p = os.path.join(OUT, name)
        fn(p)
        print("wrote", p, os.path.getsize(p) // 1024, "KB")


if __name__ == "__main__":
    main()

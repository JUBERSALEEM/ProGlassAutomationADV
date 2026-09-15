#!/usr/bin/env python3
# ═══════════════════════════════════════════════════════════════════════════
# FILE: scripts/mockups/pg_sheets.py
# PROJECT: ProGlassAutomation
# MODULE: Settings redesign mockups — THE TEN SHEETS (dev-only tooling)
#
# WHAT THIS PRODUCES
# ──────────────────
# docs/mockups/settings-redesign-v3/01 … 10 — one PNG per sheet, rendered from
# the shipped token values (pg_tokens) with the drawing kit (pg_kit). The PNGs
# are review artefacts only: `.gitignore` carries the owner directive
# "never commit PNG/image mockups to the repo" (docs/mockups/**/*.png).
#
#   01 crystal-kit        the shell + the material anatomy, radii, spacing,
#                         type ladder, control grammar and the token map
#   02 appearance-theme   Appearance · Theme & Layout                (Light)
#   03 appearance-type    Appearance · Typography, Density & Cards   (Light)
#   04 themes-wallpaper   Themes & Wallpaper                         (Light)
#   05 ai                 AI Assistant                               (Dark)
#   06 voice-agent        Voice (Light) + AI & Agent (Dark)
#   07 data-demo          Data & Backup + Demo Center                (Dark)
#   08 updates-license    Updates + License                          (Light)
#   09 features-sharing   Features + Sharing                         (Light)
#   10 about-parity       About & Help + the Light/Dark parity proof
#
# CONTENT IS THE PRODUCT'S OWN
# ────────────────────────────
# Every label comes from Themes/Languages/en-US.xaml through the same keys the
# sections bind; every rail row from SettingsViewModel.NavRows; every accent
# swatch from AccentPresets.All. A sheet that showed invented copy would not be
# a mockup of this module.
#
# USAGE
# ─────
#   python3 scripts/mockups/pg_sheets.py            # all ten
#   python3 scripts/mockups/pg_sheets.py 02 05      # a subset
# ═══════════════════════════════════════════════════════════════════════════
from __future__ import annotations

import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pg_kit import *          # noqa: F401,F403
from pg_kit import (Canvas, Theme, card, inner, chip, value_pill, button,    # noqa: F401
                    toggle, slider, segmented, combo, input_box, progress,
                    separator, scrollbar, swatch, icon, icon_disc, text,
                    text_w, measure, stage, paint_layers, cached_mask,
                    stroke_ring, shadow_layer, rgba, mix, with_alpha, hexs,
                    FONTS, TYPE_ROLES)
from pg_tokens import TOKENS  # noqa: F401

# PNGs are REFERENCE ARTEFACTS ONLY — they live OUTSIDE the repository tree.
# The repo keeps the renderer (code); the pictures it draws stay in
# /home/user/mockup-assets/settings-mockups and are never committed.
OUT = os.environ.get("PG_MOCKUP_OUT",
                     "/home/user/mockup-assets/settings-mockups")

# ── sheet chrome palette (the artboard is deliberately NOT the product) ──
BOARD = (13, 15, 19, 255)
BOARD_EDGE = (30, 34, 42, 255)
BOARD_TEXT = (226, 232, 240, 255)
BOARD_MUTED = (120, 128, 142, 255)
BOARD_ACCENT = (56, 189, 248, 255)


# ═══════════════════════════════════════════════════════════════════════════
# sheet frame
# ═══════════════════════════════════════════════════════════════════════════
class Sheet:
    """Artboard + header + footer around one or more app windows."""

    def __init__(self, number: str, title: str, subtitle: str, w: int, h: int,
                 scale: int = 2, badges: list[tuple[str, str]] | None = None,
                 plain: bool = False):
        self.plain = plain
        self.num, self.title, self.subtitle = number, title, subtitle
        self.cv = Canvas(w, h, bg=BOARD, scale=scale)
        self.w, self.h = w, h
        self.badges = badges or []
        self._header()
        self.notes: list[str] = []

    def size(self, w: int, h: int, badges=None):
        """Grow the artboard after a measure pass (header repainted at final h)."""
        self.w, self.h = w, h
        if badges is not None:
            self.badges = badges
        bg = Image.new("RGBA", (int(w * self.cv.s), int(h * self.cv.s)),
                       rgba(BOARD))
        old_img = self.cv.img
        bg.paste(old_img, (0, 0))
        self.cv.img = bg
        self.cv.d = ImageDraw.Draw(bg)
        self._header()

    def _header(self):
        if self.plain:
            return
        cv = self.cv
        # hairline rule under the header band
        cv.d.rectangle([0, 0, cv.X(self.w), cv.X(86)], fill=rgba((17, 20, 26, 255)))
        cv.d.rectangle([0, cv.X(86), cv.X(self.w), cv.X(87)], fill=rgba(BOARD_EDGE))
        # sheet number block
        cv.d.rectangle([0, 0, cv.X(4), cv.X(86)], fill=rgba(BOARD_ACCENT))
        text(cv, (28, 20), self.num, Theme("dark"), "display", color=BOARD_ACCENT,
             size=34, weight="bold")
        nw = text_w(self.num, Theme("dark"), "display", size=34, weight="bold")
        text(cv, (28 + nw + 18, 22), self.title, Theme("dark"), "section_title",
             color=BOARD_TEXT, size=21, weight="bold")
        text(cv, (28 + nw + 18, 52), self.subtitle, Theme("dark"), "small",
             color=BOARD_MUTED, size=12.5)
        # right badges: theme / page / scale
        x = self.w - 28
        for label, kind in reversed(self.badges):
            tw = text_w(label, Theme("dark"), "chip", size=10, weight="semibold") + 20
            x -= tw
            bg = ((56, 189, 248, 40) if kind == "theme"
                  else (255, 255, 255, 18) if kind == "page" else (255, 255, 255, 12))
            fg = BOARD_ACCENT if kind == "theme" else (170, 178, 192, 255)
            cv.paste(paint_layers(int(tw * cv.s), int(22 * cv.s),
                                  [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                    "stops": [(0, bg), (1, bg)]}],
                                  cached_mask(int(tw * cv.s), int(22 * cv.s), 11 * cv.s)),
                     x, 32)
            text(cv, (x + tw / 2, 43), label, Theme("dark"), "chip", color=fg,
                 anchor="mm", size=10, weight="semibold")
            x -= 8

    def save(self, path: str):
        return self.cv.save(path)

    def footer(self, lines: list[str]):
        """Spec footer: the token names behind what the sheet shows."""
        cv = self.cv
        y = self.h - 14 - 16 * len(lines)
        cv.d.rectangle([0, cv.X(self.h - 34 - 16 * len(lines)), cv.X(self.w), cv.X(self.h)],
                       fill=rgba((17, 20, 26, 255)))
        cv.d.rectangle([0, cv.X(self.h - 35 - 16 * len(lines)), cv.X(self.w),
                        cv.X(self.h - 34 - 16 * len(lines))], fill=rgba(BOARD_EDGE))
        for i, line in enumerate(lines):
            text(cv, (28, y + i * 16), line, Theme("dark"), "label",
                 color=BOARD_MUTED, size=10.5)
        return y

    def window(self, box, th: Theme, page_key: str, rail: bool = True,
               content=None, mode_chip: bool = True):
        """One app window: stage → top bar → settings shell → page content."""
        x0, y0, x1, y1 = box
        w, h = x1 - x0, y1 - y0
        sub = Canvas(w, h, bg=(0, 0, 0, 0), scale=self.cv.s)
        _window_body(sub, th, page_key, rail=rail, content=content,
                     mode_chip=mode_chip)
        # window frame: rounded clip + 1 px rim + drop shadow on the artboard
        r = 14.0
        sl, ox, oy = shadow_layer(int(w * self.cv.s), int(h * self.cv.s), r * self.cv.s,
                                  {"color": (0, 0, 0, 255), "opacity": 0.55,
                                   "blur": 46, "depth": 18, "direction": 90}, self.cv.s)
        self.cv.paste(sl, x0 - ox, y0 - oy)
        mask = cached_mask(int(w * self.cv.s), int(h * self.cv.s), r * self.cv.s)
        layer = sub.img.copy()
        layer.putalpha(Image.fromarray(
            (np.asarray(layer).astype(np.float32)[..., 3] *
             (np.asarray(mask).astype(np.float32) / 255.0)).astype(np.uint8), "L"))
        self.cv.paste(layer, x0, y0)
        self.cv.paste(stroke_ring(int(w * self.cv.s), int(h * self.cv.s), r * self.cv.s,
                                  self.cv.s,
                                  [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                    "stops": [(0, (255, 255, 255, 60)),
                                              (1, (255, 255, 255, 22))]}]), x0, y0)
        return (x0, y0, x1, y1)


# ═══════════════════════════════════════════════════════════════════════════
# app shell
# ═══════════════════════════════════════════════════════════════════════════
TOPBAR_H = 48
BODY_TOP = TOPBAR_H + 14 + 62 + 8 + 14      # top bar + rail margin + header strip


def measure_window_h(content, th: Theme, rail: bool, w: float) -> int:
    """True window height for a page: probe-render it, read the StackPanel end."""
    global _LAST_PAGE
    _LAST_PAGE = None
    probe = Canvas(w, 8000, bg=(0, 0, 0, 0), scale=1)
    rail_w = 264 if rail else 0
    tab_x = 14 + rail_w + (6 if rail else 0)
    cx0 = tab_x + 34
    content(probe, (cx0 + 18, BODY_TOP, w - 18, 8000 - 18), th)
    pg = _LAST_PAGE
    assert pg is not None, "page builder never opened a Page"
    return int(math.ceil(pg.y - pg.gap + 18))


def _window_body(cv: Canvas, th: Theme, page_key: str, rail: bool = True,
                 content=None, mode_chip: bool = True):
    w, h = cv.w, cv.h
    stage(cv, (0, 0, w, h), th)
    _top_bar(cv, th, w)
    # ── settings shell geometry (SettingsView.xaml) ──
    rail_w = 264 if rail else 0
    tab_x = 14 + rail_w + (6 if rail else 0)
    _rail(cv, th, (14, TOPBAR_H + 14, 14 + rail_w, h - 14), page_key) if rail else None
    _grab_tab(cv, th, (tab_x, TOPBAR_H + 14, tab_x + 34, h - 14), open=rail)
    cx0 = tab_x + 34
    # page header strip: Margin 18,14,18,8 + Padding 14,12 (the single page header)
    hx0, hy0, hx1 = cx0 + 18, TOPBAR_H + 14, w - 18
    card(cv, (hx0, hy0, hx1, hy0 + 62), th, kind="page")
    _hamburger(cv, th, (hx0 + 14, hy0 + 12))
    label = next((r["label_key"] for r in th.nav if r["key"] == page_key), "")
    text(cv, (hx0 + 14 + 38 + 10, hy0 + 31), th.tr(label, page_key), th,
         "page_title", anchor="lm")
    if mode_chip:
        tw = text_w("DARK" if th.dark else "LIGHT", th, "chip") + 18
        cxx = hx0 + 14 + 38 + 10 + text_w(th.tr(label, page_key), th, "page_title") + 10
        if th.dark:
            bg, fg = th.accent_soft, th.accent_soft_text
        else:
            bg, fg = th.pill, th.muted
        cv.paste(paint_layers(int(tw * cv.s), int(22 * cv.s),
                              [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                "stops": [(0, bg), (1, bg)]}],
                              cached_mask(int(tw * cv.s), int(22 * cv.s), 12 * cv.s)),
                 cxx, hy0 + 20)
        text(cv, (cxx + tw / 2, hy0 + 31), "DARK" if th.dark else "LIGHT", th,
             "chip", color=fg, anchor="mm")
    # content column: sections carry Margin 18,14,18,18
    body = (cx0 + 18, hy0 + 62 + 8 + 14, w - 18, h - 18)
    if content:
        content(cv, body, th)


def _top_bar(cv: Canvas, th: Theme, w: float):
    """MainWindow top panel: hamburger · logo · search · right cluster."""
    cv.d.rectangle([0, 0, cv.X(w), cv.X(TOPBAR_H)],
                   fill=rgba(with_alpha(th.tile if not th.dark else (18, 22, 30, 255), 0.72)))
    cv.d.rectangle([0, cv.X(TOPBAR_H - 1), cv.X(w), cv.X(TOPBAR_H)],
                   fill=rgba(with_alpha((255, 255, 255, 255), 0.35 if not th.dark else 0.10)))
    icon(cv, (16, 15, 34, 33), "menu", th.secondary, 1.8)
    # logo mark
    cv.paste(paint_layers(int(30 * cv.s), int(30 * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (1, 1),
                            "stops": [(0, th.accent), (1, th.accent_deep)]}],
                          cached_mask(int(30 * cv.s), int(30 * cv.s), 9 * cv.s)), 48, 9)
    icon(cv, (54, 15, 66, 27), "layers", (255, 255, 255, 255), 1.9)
    text(cv, (86, 12), "ProGlass", th, "row_title", color=th.title, size=14, weight="bold")
    text(cv, (86, 28), "Automation Suite", th, "caption", color=th.muted, size=9)
    # search
    sx0, sx1 = 300, min(w - 320, 760)
    input_box(cv, (sx0, 9, sx1, 39), "Search modules, jobs, settings…", th,
              height=30, placeholder=True, icon_left="search")
    text(cv, (sx1 - 12, 24), "⌘K", th, "caption", color=th.faint, anchor="rm", size=9)
    # right cluster
    rx = w - 24
    cv.paste(paint_layers(int(30 * cv.s), int(30 * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (1, 1),
                            "stops": [(0, th.accent_soft), (1, th.accent_soft)]}],
                          cached_mask(int(30 * cv.s), int(30 * cv.s), 15 * cv.s)),
             rx - 30, 9)
    text(cv, (rx - 15, 24), "UF", th, "chip", color=th.accent_soft_text, anchor="mm")
    rx -= 46
    for name, active in (("gear", True), ("robot", False), ("moon", False)):
        col = th.accent if active else th.secondary
        if active:
            cv.paste(paint_layers(int(30 * cv.s), int(30 * cv.s),
                                  [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                    "stops": [(0, with_alpha(th.accent, 0.14)),
                                              (1, with_alpha(th.accent, 0.14))]}],
                                  cached_mask(int(30 * cv.s), int(30 * cv.s), 9 * cv.s)),
                     rx - 30, 9)
        icon(cv, (rx - 25, 14, rx - 7, 32), name, col, 1.8)
        rx -= 40
    cv.d.rectangle([cv.X(rx + 26), cv.X(12), cv.X(rx + 27), cv.X(36)],
                   fill=rgba(th.hairline))


def _hamburger(cv: Canvas, th: Theme, xy):
    x, y = xy
    for i in range(3):
        yy = y + 12 + i * 6.5
        cv.paste(paint_layers(int(20 * cv.s), int(2.5 * cv.s),
                              [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                "stops": [(0, th.title), (1, th.title)]}],
                              cached_mask(int(20 * cv.s), int(2.5 * cv.s), 1.25 * cv.s)),
                 x + 9, yy)


def _grab_tab(cv: Canvas, th: Theme, box, open: bool = True):
    x0, y0, x1, y1 = box
    cv.paste(paint_layers(int((x1 - x0) * cv.s), int((y1 - y0) * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, with_alpha(th.tile, 0.85)),
                                      (1, with_alpha(th.tile, 0.75))]}],
                          cached_mask(int((x1 - x0) * cv.s), int((y1 - y0) * cv.s),
                                      10 * cv.s)), x0, y0)
    icon(cv, ((x0 + x1) / 2 - 7, (y0 + y1) / 2 - 7, (x0 + x1) / 2 + 7, (y0 + y1) / 2 + 7),
         "chevron_r", th.secondary, 1.8)


def _rail(cv: Canvas, th: Theme, box, page_key: str):
    """The navigation rail — the registry (NavRows) rendered as one list."""
    x0, y0, x1, y1 = box
    card(cv, box, th, kind="page")
    # identity header: accent card, gear disc, title + subtitle (SettingsView)
    hx0, hy0, hx1 = x0 + 14, y0 + 12, x1 - 14
    cv.paste(paint_layers(int((hx1 - hx0) * cv.s), int(58 * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (1, 1),
                            "stops": [(0, th.accent), (1, th.accent_deep)]}],
                          cached_mask(int((hx1 - hx0) * cv.s), int(58 * cv.s),
                                      th.r("Ios26RadiusInner") * cv.s)), hx0, hy0)
    cv.paste(paint_layers(int((hx1 - hx0 - 2) * cv.s), int(26 * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, with_alpha((255, 255, 255, 255), 0.28)),
                                      (1, (255, 255, 255, 0))]}],
                          cached_mask(int((hx1 - hx0 - 2) * cv.s), int(26 * cv.s),
                                      (th.r("Ios26RadiusInner") - 1) * cv.s)), hx0 + 1, hy0 + 1)
    icon_disc(cv, (hx0 + 10, hy0 + 10), "gear", th, size=38,
              bg=with_alpha((255, 255, 255, 255), 0.22), fg=(255, 255, 255, 255))
    text(cv, (hx0 + 58, hy0 + 15), th.tr("Settings.Title"), th, "row_title",
         color=(255, 255, 255, 255), size=14.5, weight="bold")
    text(cv, (hx0 + 58, hy0 + 34), th.tr("Settings.Subtitle"), th, "caption",
         color=with_alpha((255, 255, 255, 255), 0.78), size=9.5)
    icon(cv, (hx1 - 30, hy0 + 20, hx1 - 14, hy0 + 36), "chevron_r",
         with_alpha((255, 255, 255, 255), 0.85), 1.8)
    # separator
    separator(cv, x0 + 28, hy0 + 58 + 10, x1 - 28, th)
    # rows — flat list, exactly the registry order
    ry = hy0 + 58 + 22
    for row in th.nav:
        active = row["key"] == page_key
        rh = 34
        rx0, rx1 = x0 + 10, x1 - 10
        if active:
            cv.paste(paint_layers(int((rx1 - rx0) * cv.s), int(rh * cv.s),
                                  [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                    "stops": [(0, with_alpha(th.tile, 0.95) if not th.dark
                                               else with_alpha((255, 255, 255, 255), 0.10)),
                                              (1, with_alpha(th.tile, 0.95) if not th.dark
                                               else with_alpha((255, 255, 255, 255), 0.10))]}],
                                  cached_mask(int((rx1 - rx0) * cv.s), int(rh * cv.s),
                                              12 * cv.s)), rx0, ry)
            cv.paste(stroke_ring(int((rx1 - rx0) * cv.s), int(rh * cv.s), 12 * cv.s, cv.s,
                                 [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                   "stops": [(0, with_alpha(th.accent, 0.35)),
                                              (1, with_alpha(th.accent, 0.35))]}]), rx0, ry)
            cv.paste(paint_layers(int(3.5 * cv.s), int(22 * cv.s),
                                  [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                    "stops": [(0, th.accent), (1, th.accent)]}],
                                  cached_mask(int(3.5 * cv.s), int(22 * cv.s), 1.75 * cv.s)),
                     rx0 + 2, ry + 6)
        gcol = th.accent if active else th.secondary
        icon(cv, (rx0 + 12, ry + 8, rx0 + 30, ry + 26), glyph_icon(row["glyph"]),
             gcol, 1.7)
        text(cv, (rx0 + 40, ry + rh / 2), th.tr(row["label_key"], row["key"]), th,
             "row_label", color=th.title if active else th.secondary, anchor="lm")
        ry += rh + 4
    return ry


# ═══════════════════════════════════════════════════════════════════════════
# page / card flow context
# ═══════════════════════════════════════════════════════════════════════════
_LAST_PAGE: "Page | None" = None


class Page:
    """The section body: a StackPanel of page cards at the module's 22 px gap."""

    def __init__(self, cv: Canvas, box, th: Theme, scroll: float = 0.0):
        global _LAST_PAGE
        _LAST_PAGE = self
        self.cv, self.th = cv, th
        self.x0, self.y0, self.x1, self.y1 = box
        self.gap = th.gap("SettingsGapCard")
        self.y = self.y0
        self.scroll = scroll

    def card(self, title=None, note=None, chip=None, chip_v="accent",
             danger=False, icon_name=None, min_h=0):
        c = CardCtx(self, title=title, note=note, chip=chip, chip_v=chip_v,
                    danger=danger, icon_name=icon_name)
        return c

    def finish(self):
        if self.scroll > 0:
            scrollbar(self.cv, (self.x1 - 2, self.y0 - 6, self.x1 + 8, self.y1),
                      self.th, top_frac=self.scroll)
            # bottom fade = the page continues below the fold
            fade_h = 46
            self.cv.paste(paint_layers(int((self.x1 - self.x0) * self.cv.s),
                                       int(fade_h * self.cv.s),
                                       [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                         "stops": [(0, (0, 0, 0, 0)),
                                                   (1, with_alpha(
                                                       self.th.t["stage"][0]["color"], 0.9))]}]),
                          self.x0, self.y1 - fade_h)


class CardCtx:
    """One SettingsPageCardStyle card. Content is drawn into a transparent
    layer first, so the card face can be painted UNDER it at the measured
    height — the renderer's stand-in for WPF's measure pass."""

    def __init__(self, page: Page, title=None, note=None, chip=None,
                 chip_v="accent", danger=False, icon_name=None):
        self.page = page
        self.th = page.th
        self.cv = page.cv
        pad = self.th.pad("SettingsCardPadding")
        self.w = page.x1 - page.x0
        self.pad = pad
        self.x0, self.x1 = page.x0, page.x1
        self.layer = Canvas(self.w, 1500, bg=(0, 0, 0, 0), scale=self.cv.s)
        self.y = pad[1]
        # content coordinates are LAYER-relative: the layer is pasted at the
        # page's left edge, so the card padding — not the page offset — is the
        # origin. (Getting this wrong shifts every card's content right by the
        # page's x, which is exactly what the first render did.)
        self.cx0, self.cx1 = pad[0], self.w - pad[2]
        self.danger = danger
        if title:
            self.header(title, note, chip, chip_v, icon_name)

    # ── header ──
    def header(self, title, note=None, chip=None, chip_v="accent", icon_name=None):
        # NOTE: the `chip` argument shadows the kit's chip() painter inside this
        # method, so the painter is reached through the module globals.
        th, ly = self.th, self.layer
        paint_chip = globals()["chip"]
        if chip:
            cw, chh = chip_size(th, chip)
            paint_chip(ly, (self.cx1 - cw, self.y + 1), chip, th, variant=chip_v)
        text(ly, (self.cx0, self.y), title, th,
             "card_title", color=th.danger if self.danger else None)
        self.y += 17 * 1.28
        if note:
            text(ly, (self.cx0, self.y + 4), note, th, "card_note",
                 color=th.danger if self.danger else None,
                 maxw=self.cx1 - self.cx0, wrap=True)
            lines = 1 + int(text_w(note, th, "card_note") // (self.cx1 - self.cx0))
            self.y += 4 + 12 * 1.28 * lines + 12
        else:
            self.y += 12
        return self

    # ── rows ──
    def row(self, icon_name=None, title=None, meta=None, trailing=None,
            height=None, title_role="row_title", sep=True, indent=0):
        """A grammar row: optional icon disc + title/meta + trailing control."""
        th, ly = self.th, self.layer
        x = self.cx0 + indent
        h = height or (34 if (icon_name or trailing) else 22)
        cy = self.y + h / 2
        if icon_name:
            icon_disc(ly, (x, self.y + (h - 34) / 2), icon_name, th)
            x += 34 + 10
        if title:
            text(ly, (x, cy - (8 if meta else 0)), title, th, title_role, anchor="lm")
        if meta:
            text(ly, (x, cy + 9), meta, th, "row_meta", anchor="lm",
                 maxw=(self.cx1 - x - (330 if trailing else 0)))
        if trailing:
            trailing(ly, self, cy)
        self.y += h
        if sep:
            separator(ly, self.cx0, self.y + 4, self.cx1, th)
            self.y += 9
        return self

    def gap(self, g=None):
        self.y += g if g is not None else self.th.gap("SettingsGapRow")
        return self

    def sep(self):
        separator(self.layer, self.cx0, self.y + 4, self.cx1, self.th)
        self.y += 9
        return self

    def label(self, s, role="group_caption", dy=6):
        text(self.layer, (self.cx0 + 2, self.y + dy), s, self.th, role)
        self.y += dy + self.th.fs(TYPE_ROLES[role][0]) * 1.28 + 8
        return self

    # ── tiles / grids ──
    def tile(self, x0, y0, x1, y1, kind="inner"):
        inner(self.layer, (x0, y0, x1, y1), self.th, kind=kind)
        return self

    def close(self, min_h=0):
        """Paint the card face at the measured height and composite content."""
        th, page = self.th, self.page
        h = max(self.y + self.pad[3], min_h)
        box = (page.x0, page.y, page.x1, page.y + h)
        card(self.cv, box, th, kind="danger" if self.danger else "page")
        layer = self.layer.img.crop((0, 0, self.layer.img.width, int(h * self.cv.s)))
        self.cv.paste(layer, page.x0, page.y)
        page.y = page.y + h + page.gap
        return box


# ═══════════════════════════════════════════════════════════════════════════
# small control factories usable as `trailing=` callbacks
# ═══════════════════════════════════════════════════════════════════════════
def tr_toggle(on=True):
    def f(ly, c, cy):
        toggle(ly, (c.cx1 - 62, cy - 18), on, c.th)
    return f


def tr_chip(label, v="accent", dot=None):
    def f(ly, c, cy):
        w, h = chip_size(c.th, label, dot)
        chip(ly, (c.cx1 - w, cy - h / 2), label, c.th, variant=v, dot=dot)
    return f


def tr_pill(label, icon_name=None):
    def f(ly, c, cy):
        w, h = pill_size(c.th, label, icon_name)
        value_pill(ly, (c.cx1 - w, cy - h / 2), label, c.th, trailing=icon_name)
    return f


def tr_combo(label, width=190):
    def f(ly, c, cy):
        combo(ly, (c.cx1 - width, cy - 17, c.cx1, cy + 17), label, c.th)
    return f


def tr_button(label, kind="primary", icon_name=None, width=None):
    def f(ly, c, cy):
        w, h = button_size(c.th, label, kind, icon_name)
        button(ly, (c.cx1 - w, cy - h / 2), label, c.th, kind=kind, icon_name=icon_name)
    return f


def tr_slider(value, label=None, width=220):
    def f(ly, c, cy):
        slider(ly, (c.cx1 - width, cy - 20, c.cx1, cy + 20), value, c.th, label=label)
    return f


def tr_text(s, role="field_value", color=None):
    def f(ly, c, cy):
        text(ly, (c.cx1, cy), s, c.th, role, color=color, anchor="rm")
    return f


# ═══════════════════════════════════════════════════════════════════════════
# ALIASES — labels a sheet needs that the language files do not carry yet.
# Theme.tr() consults the en-US table first, then this table, then the
# prefix resolver; a brace-wrapped key may never reach a sheet.
# ═══════════════════════════════════════════════════════════════════════════
Theme.alias = {
    "Common.Configure": "Configure", "Common.Download": "Download",
    "Common.Edit": "Edit", "Common.Generate": "Generate",
    "Common.Install": "Install", "Common.Load": "Load",
    "Common.Purge": "Purge", "Common.Remove": "Remove",
    "Common.Reset": "Reset", "Common.Clear": "Clear",
    "Settings.CardGeometry": "Card geometry",
    "Settings.OneRadiusAuthorityEveryCardInTheAppFollowsIt":
        "One radius authority — every card in the app follows it",
    "Settings.Typography": "Typography",
    "Settings.TheWholeTypeLadderOneScaleNoFractionalSizes":
        "The whole type ladder, one scale, no fractional sizes",
    "Settings.Storage": "Storage",
    "Settings.LearnMore": "LEARN MORE",
    "Settings.Language": "Language",
    "Settings.Currency": "Currency",
    "Settings.Edition": "Edition", "Settings.Licensee": "Licensee",
    "Settings.Machines": "Machines", "Settings.RenewsOn": "Renews on",
    "Settings.LicenseKey": "License key",
    "Settings.ActivationId": "Activation ID",
    "Settings.Invoice": "Invoice",
    "Settings.RunningVersion": "RUNNING VERSION",
    "Settings.LatestRelease": "LATEST RELEASE",
    "Settings.Channel": "Channel",
    "Settings.Overview": "Overview",
    "Settings.History": "History",
    "Settings.Privacy": "Privacy",
    "Settings.Capabilities": "Capabilities",
    "Settings.WhatTheAgentMayDoOnYourBehalf": "What the agent may do on your behalf",
    "Settings.MemoryKnowledge": "Memory & knowledge",
    "Settings.Defaults": "Defaults",
    "Settings.MessageTemplates": "Message templates",
    "Settings.Workflow": "Workflow",
    "Settings.Isolation": "Isolation",
    "Settings.State": "State",
    "Settings.Generate": "Generate",
    "Settings.Session": "Session",
    "Settings.SignOut": "Sign out",
    "Settings.AutoSignIn": "Auto sign-in",
    "Settings.SessionSecurity": "Session security",
    "Settings.VisualTheme": "Visual theme",
    "Settings.FrostedModuleCards": "Frosted module cards",
    "Settings.TranslucentModuleCardsOverTheStage":
        "Translucent module cards over the stage",
    "Settings.ShowACrispKeylineOnEveryCard": "Show a crisp keyline on every card",
    "Settings.SoftDiagonalSheenOnCardsAndPanels":
        "Soft diagonal sheen on cards and panels",
    "Settings.GlobalCardCornerRadiusDIPs": "Global card corner radius (DIPs)",
    "Settings.UIScale": "UI scale",
    "Settings.ScalesEveryModuleWindowAndDialog":
        "Scales every module window and dialog",
    "Settings.FourProfilesLiftSizesThatAre13PxOrLarger":
        "Four profiles lift the sizes that are 13 px or larger",
    "Settings.FontWeightStyle": "Font weight & style",
    "Settings.RegularForBodySemiBoldForValuesBoldForTitles":
        "Regular for body · SemiBold for values · Bold for titles",
    "Settings.AppliesToEveryScreenIncludingPDFs":
        "Applies to every screen, including PDFs",
    "Settings.LanguageCurrency": "Language & currency",
    "Settings.FifteenLanguagesAuthoredNotMachineFallbacks":
        "Fifteen languages authored, not machine fallbacks",
    "Settings.AppliesToEveryMoneyFieldAndPDF":
        "Applies to every money field and PDF",
    "Settings.DetectsLocaleOnFirstRunOnly": "Detects locale on first run only",
    "Settings.PerModuleWallpapers": "Per-module wallpapers",
    "Settings.IndividualBackgroundsPerModule":
        "Individual backgrounds per module",
    "Settings.CrystalFluidMaterial": "Crystal Fluid material",
    "Settings.ResetToDefault": "Reset to default",
    "Settings.DataRootMode": "Data root mode",
    "Settings.LocalFolderOrASharedNetworkPath":
        "Local folder or a shared network path",
    "Settings.DataFolder": "Data folder",
    "Settings.EveryWorkstationMustPointAtTheSameFolder":
        "Every workstation must point at the same folder",
    "Settings.WorkstationsSeen": "Workstations seen",
    "Settings.LastSevenDaysOfWritesFromThisFolder":
        "Last seven days of writes from this folder",
    "Settings.BackupVault": "Backup vault",
    "Settings.WhereSetsAreWritten": "Where sets are written",
    "Settings.LoadSampleData": "Load sample data",
    "Settings.AddsTheDemoDatasetToTheLiveDatabases":
        "Adds the demo dataset to the live databases",
    "Settings.SchemaCurrentMigrationAppliedOnOpen":
        "Schema current — migration applied on open",
    "Settings.PurgeAllData": "Purge all data",
    "Settings.DeletesEveryDatabaseAndSettingsFromThisMachine":
        "Deletes every database and settings from this machine",
    "Settings.BitratePresetForTheCapture": "Bitrate preset for the capture",
    "Settings.SmoothnessVersusFileSize": "Smoothness versus file size",
    "Settings.SystemSoundFromTheAppWindow": "System sound from the app window",
    "Settings.NarrationTrackMixedIntoTheCapture":
        "Narration track mixed into the capture",
    "Settings.TheWorkspaceTheDemoLandsOn": "The workspace the demo lands on",
    "Settings.WritesAScriptedRunIntoTheDemoWorkspace":
        "Writes a scripted run into the demo workspace",
    "Settings.WritesGoToASidecarDatabaseOnly":
        "Writes go to a sidecar database only",
    "Settings.WhatTheLastRunDid": "What the last run did",
    "Settings.WhereTheLastCaptureWasWritten":
        "Where the last capture was written",
    "Settings.BundledEncoderNoInstallNeeded": "Bundled encoder — no install needed",
    "Settings.StableShipsTestedBetaShipsFirst":
        "Stable ships tested, Beta ships first",
    "Settings.LastCheck202609150812": "Last check 2026-09-15 08:12",
    "Settings.CheckNow": "Check now",
    "Settings.ModelFileOnDisk": "Model file on disk",
    "Settings.SizeOnDisk": "Size on disk",
    "Settings.DownloadedOnceSharedByEveryModule":
        "Downloaded once, shared by every module",
    "Settings.MasterSwitchesAndTheTuningValuesUsedOnEveryRequest":
        "Master switches and the tuning values used on every request",
    "Settings.TurnsTheAssistantAndEveryAIActionOnOrOff":
        "Turns the assistant and every AI action on or off",
    "Settings.AllowsTheAssistantToReadLiveModuleData":
        "Allows the assistant to read live module data",
    "Settings.OnlyTheActiveProviderProcessesRequests":
        "Only the active provider processes your requests",
    "Settings.UpperBoundPerRequest": "Upper bound per request",
    "Settings.CreativityVersusDeterminism": "Creativity versus determinism",
    "Settings.EncryptedAtRestWithTheMachineKey":
        "Encrypted at rest with the machine key",
    "Settings.DeletesTheEncryptedKeyFileImmediately":
        "Deletes the encrypted key file immediately",
    "Settings.WipesEveryStoredConversationOnThisMachine":
        "Wipes every stored conversation on this machine",
    "Settings.AgentNeverLeavesThisMachine": "The agent never leaves this machine",
    "Settings.AskBeforeAnyRequestLeavesTheMachine":
        "Ask before any request leaves the machine",
    "Settings.WhatThisMeans": "What this means",
    "Settings.LocalModelAdvice":
        "A local model keeps dictation and answers on this machine.",
    "Settings.SeparateFromBusinessDataByDesign":
        "Separate from business data by design",
    "Settings.SavedMemory": "Saved memory",
    "Settings.FactsTheAgentRemembersBetweenSessions":
        "Facts the agent remembers between sessions",
    "Settings.KnowledgeBase": "Knowledge base",
    "Settings.IndexedDocumentsTheAgentCanCite":
        "Indexed documents the agent can cite",
    "Settings.ForgetEverything": "Forget everything",
    "Settings.WipesMemoryAndTheIndexKeepsBusinessData":
        "Wipes memory and the index, keeps business data",
    "Settings.SendsThePDFAndAMessageLink": "Sends the PDF and a message link",
    "Settings.CompanyMailboxConfiguredOnce": "Company mailbox, configured once",
    "Settings.OptionalPaidGateway": "Optional paid gateway",
    "Settings.AlwaysAttachPDF": "Always attach PDF",
    "Settings.TheDocumentRidesAlongWithEveryMessage":
        "The document rides along with every message",
    "Settings.DefaultProvider": "Default provider",
    "Settings.PreSelectedInTheShareSheet": "Pre-selected in the share sheet",
    "Settings.DefaultRecipient": "Default recipient",
    "Settings.CustomerContactFromTheRecord": "Customer contact from the record",
    "Settings.DocumentRules": "Document rules",
    "Settings.WhichModulesMayShareAndWhere": "Which modules may share, and where",
    "Settings.TheTextAroundTheDocumentLink": "The text around the document link",
    "Settings.WhatAPickBecomesWhenADocumentOpensTheShareSheet":
        "What a pick becomes when a document opens the share sheet",
    "Settings.AboutTheThemeSystem": "About the theme system",
    "Settings.FamiliesAccentsDensityAndTheCrystalMaterial":
        "Families, accents, density and the crystal material",
    "Settings.AboutTheAIAssistant": "About the AI assistant",
    "Settings.ProvidersKeysAndWhatTheAgentMayTouch":
        "Providers, keys and what the agent may touch",
    "Settings.AboutYourAPIKeyPrivacy": "About your API key & privacy",
    "Settings.EncryptedAtRestNeverSentExceptToYourProvider":
        "Encrypted at rest, never sent except to your provider",
    "Settings.AboutVoiceModels": "About voice models",
    "Settings.WhisperRunsOnDeviceNothingIsTranscribedInTheCloud":
        "Whisper runs on device — nothing is transcribed in the cloud",
    "Settings.ShowOrHideOptionalFieldsInTheWizard":
        "Show or hide the optional fields of this document",
    "Settings.CreateBackupNow": "Create backup now",
    "Settings.DownloadAModelToSwitchSizes": "Download a model to switch sizes",
    "Settings.OfflineAIModelInstallOnceWorksWithoutInternet":
        "Offline AI model (install once, works without internet)",
    "Settings.SavedMemoryAndTheKnowledgeBaseLiveInTheAgentSOwnDatabase":
        "Saved memory and the knowledge base live in the agent's own database.",
    "Settings.ControlWhetherTheAgentTalksToTheInternet":
        "Control whether the agent talks to the internet, and what it is allowed to say.",
    "Settings.OnlyTheACTIVEProviderProcessesYourRequestsKeysAreEncrypted":
        "Only the ACTIVE provider processes your requests. Keys are encrypted at rest.",
    "Settings.ABackupSetCopiesEveryDatabaseSettingsIntoOneTimestampedFolder":
        "A backup set copies every database + settings into one timestamped folder.",
    "Settings.NewHereLoadASmallLinkedDemoDatasetToExplore":
        "New here? Load a small linked demo dataset to explore every module.",
    "Settings.ProbesEveryProGlassAutomationDatabaseFile":
        "Probes every ProGlass Automation database file on this machine.",
    "Settings.ALedgerOfEveryRecordingJobRunOnThisMachine":
        "A ledger of every recording job run on this machine.",
    "Settings.RecordTheRunningAppWindowWithoutAModuleWorkflow":
        "Record the running app window without a module workflow.",
    "Settings.CapturesOnlyThisRunningAppWindowNotTheDesktop":
        "Captures only this running app window — not the desktop, not other programs.",
    "Settings.PickAModuleGenerateLandsOnThatWorkspaceFirst":
        "Pick a module. Generate lands on that workspace first.",
    "Settings.DemoModeKeepsProductionDataUntouched":
        "Demo Mode keeps production data untouched while you record.",
    "Settings.WhatTheLastRunDidAndWhereItsOutputIs":
        "What the last run did and where its output is.",
    "Settings.YourRunningVersionAgainstTheLatestPublishedRelease":
        "Your running version against the latest published release.",
    "Settings.WhatThisReleaseChangesStraightFromThePublisher":
        "What this release changes, straight from the publisher.",
    "Settings.EveryCheckDownloadAndFailureRecordedOnThisMachine":
        "Every check, download and failure recorded on this machine.",
    "Settings.PasteTheKeyFromYourInvoiceAndActivate":
        "Paste the key from your invoice and activate this machine.",
    "Settings.SignOutForAShiftHandoverOrForgetTheAutoSignIn":
        "Sign out for a shift handover, or forget the auto-sign-in credentials.",
    "Settings.WhereDocumentsCanBeSentFrom": "Where documents can be sent from",
    "Settings.OneImageBehindEveryModulePlusPerModuleOverrides":
        "One image behind every module, plus per-module overrides.",
    "Settings.SixLiveAxesTuneTheCrystalWaterWaterCausticsBubbles":
        "Six live axes tune the crystal water — water, caustics, bubbles and reflection.",
    "Settings.IndustrialMonetIOS26OrIPhone17ProMaxRestyles":
        "Industrial, Monet, iOS 26 or iPhone 17 Pro Max — restyles the whole app.",
    "Settings.ColourIdentityAndTypographyAppliedAcrossEveryScreen":
        "Colour identity and typography — applied across every screen.",
    "Settings.CompactFitsMoreOnALaptopComfortableGivesLarger":
        "Compact fits more on a laptop; Comfortable gives larger touch targets.",
    "Settings.OverallLookCardWeightDensity":
        "Overall look, card weight and density.",
    "Settings.LightForDaylightDarkForNightShiftsEveryOtherLookSettingBuildsOnThis":
        "Light for daylight, Dark for night shifts — every other look setting builds on this.",
}


# ═══════════════════════════════════════════════════════════════════════════
# completion pass — the remaining Settings.* / Common.* labels the sheets print
Theme.alias.update({
    "Common.Configure": "Configure", "Common.Download": "Download",
    "Common.Edit": "Edit", "Common.Generate": "Generate",
    "Common.Install": "Install", "Common.Load": "Load",
    "Common.Purge": "Purge", "Common.Remove": "Remove", "Common.Reset": "Reset",
    "Settings.Activate": "Activate",
    "Settings.AppliesBehindEveryModuleUnlessOverridden": "Applies behind every module unless overridden",
    "Settings.BoundToThisMachineSHardware": "Bound to this machine's hardware",
    "Settings.CompanyLANWiFIDatabase": "Company LAN / Wi-Fi database",
    "Settings.ConnectAllOfficeWorkstationsToOneSharedProGlassDataFolder": "Connect every office workstation to one shared ProGlass data folder",
    "Settings.CreateACimestampedSetOfEveryDatabase": "Create a timestamped set of every database",
    "Settings.EndsTheSessionImmediately": "Ends the session immediately",
    "Settings.FFMPEG": "FFmpeg",
    "Settings.LockTheModuleAfterIdleMinutes": "Lock the module after idle minutes",
    "Settings.RememberThisOperatorOnThisMachine": "Remember this operator on this machine",
    "Settings.RemembersProviderKeysBetweenSessions": "Remembers provider keys between sessions",
    "Settings.ReplaceTheLiveDatabasesFromASet": "Replace the live databases from a set",
    "Settings.Restore": "Restore", "Settings.SampleData": "Sample data",
    "Settings.ThePurchaseDocumentThisKeyCameFrom": "The purchase document this key came from",
})


Theme.alias.update({
    "Common.Open": "Open", "Common.Copy": "Copy",
    "Settings.SupportDiagnostics": "SUPPORT & DIAGNOSTICS",
    "Settings.Documentation": "Documentation",
    "Settings.ShortcutsRecipesAndTheCrystalMaterialGuide": "Shortcuts, recipes and the crystal material guide",
    "Settings.OpenLogFolder": "Open log folder",
    "Settings.WhereEveryModuleWritesItsTrace": "Where every module writes its trace",
    "Settings.CopySystemReport": "Copy system report",
    "Settings.VersionThemeGpusAndLocaleToTheClipboard": "Version, theme, GPUs and locale to the clipboard",
})


# PAGE BUILDERS — one function per settings page. Content comes from the
# product: en-US strings, NavRows, AccentPresets, WhisperModelManager.
# ═══════════════════════════════════════════════════════════════════════════
def page_appearance_theme(cv, box, th):
    """Sheet 02 — Appearance · Theme & Layout."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Mode"), T("Settings.ModeHelp"))
    segmented(c.layer, (c.cx0, c.y, 0, 0),
              [T("Settings.ModeLight"), T("Settings.ModeDark")],
              1 if th.dark else 0, th)
    c.y += 44
    c.gap(2)
    c.close()

    c = p.card(T("Settings.ThemeLayout"), T("Settings.OverallLookCardWeightDensity"))
    c.row(icon_name="layers", title=T("Settings.VisualTheme"),
          meta=T("Settings.IndustrialMonetIOS26OrIPhone17ProMaxRestyles"),
          trailing=tr_combo("Industrial  —  Studio"))
    c.row(icon_name="sliders", title=T("Settings.CardCornerRadius"),
          meta=T("Settings.GlobalCardCornerRadiusDIPs"),
          trailing=tr_slider(12 / 28, label="12"), height=44)
    c.row(icon_name="crop", title=T("Settings.DefinedCardEdges"),
          meta=T("Settings.ShowACrispKeylineOnEveryCard"),
          trailing=tr_toggle(True))
    c.row(icon_name="wand", title=T("Settings.GradientSurfaces"),
          meta=T("Settings.SoftDiagonalSheenOnCardsAndPanels"),
          trailing=tr_toggle(True))
    c.row(icon_name="image", title=T("Settings.FrostedModuleCards"),
          meta=T("Settings.TranslucentModuleCardsOverTheStage"),
          trailing=tr_toggle(True), sep=False)
    c.close()

    c = p.card(T("Settings.DisplayDensity"),
               T("Settings.CompactFitsMoreOnALaptopComfortableGivesLarger"))
    segmented(c.layer, (c.cx0, c.y, c.cx1, c.y + 34),
              ["Compact", "Normal", "Comfortable", "Spacious"], 0, th, fill=True)
    c.y += 46
    c.row(icon_name="grid", title=T("Settings.UIScale"),
          meta=T("Settings.ScalesEveryModuleWindowAndDialog"),
          trailing=tr_slider(0.5, label="100%"), height=44, sep=False)
    c.close()

    c = p.card(T("Settings.AccentFONT"),
               T("Settings.ColourIdentityAndTypographyAppliedAcrossEveryScreen"))
    c.label(T("Settings.LblAccent").upper())
    # the palette in AccentPresets.All display order — the chips bind this list
    n = len(th.accents)
    cols = 9
    sw = 34
    gapx = (c.cx1 - c.cx0 - cols * sw) / (cols - 1)
    for i, a in enumerate(th.accents):
        col, row = i % cols, i // cols
        x = c.cx0 + col * (sw + gapx)
        y = c.y + row * (sw + 26)
        swatch(c.layer, (x, y, x + sw, y + sw), a["color"], th, selected=(i == 0))
        text(c.layer, (x + sw / 2, y + sw + 5), a["name"].split()[0], th,
             "caption", anchor="mt", size=8.5)
    c.y += 2 * (sw + 26) + 8
    c.sep()
    c.row(icon_name="type", title=T("Settings.FontFamily"),
          meta=T("Settings.AppliesToEveryScreenIncludingPDFs"),
          trailing=tr_combo("Segoe UI Variable"))
    c.row(icon_name="type", title=T("Settings.FontWeightStyle"),
          meta=T("Settings.RegularForBodySemiBoldForValuesBoldForTitles"),
          trailing=tr_combo("Regular · SemiBold · Bold"), sep=False)
    c.close()

    c = p.card(T("Settings.Wallpapers"),
               T("Settings.OneImageBehindEveryModulePlusPerModuleOverrides"))
    c.row(icon_name="image", title=T("Settings.GlobalWallpaper"),
          meta=T("Settings.AppliesBehindEveryModuleUnlessOverridden"),
          trailing=tr_combo("Studio sweep (accent-aware)"))
    c.row(icon_name="grid", title=T("Settings.PerModuleWallpapers"),
          meta=T("Settings.IndividualBackgroundsPerModule"),
          trailing=tr_button(T("Common.Configure"), kind="secondary"), sep=False)
    c.close()
    p.finish()


def page_appearance_type(cv, box, th):
    """Sheet 03 — Appearance · Typography, Density & Card Geometry."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Typography"), T("Settings.TheWholeTypeLadderOneScaleNoFractionalSizes"))
    roles = [("Ios26FontSizeDisplay", "Display", "ProGlass"),
             ("Ios26FontSizeStat", "Stat", "24 jobs"),
             ("Ios26FontSizeH1", "Page title", "Appearance"),
             ("Ios26FontSizeSectionTitle", "Section", "Theme & Layout"),
             ("Ios26FontSizeCardTitle", "Card title", "Accent & Font"),
             ("Ios26FontSizeSubtitle", "Subtitle", "Glass Blue"),
             ("Ios26FontSizeValue", "Field value", "Compact density"),
             ("Ios26FontSizeBody", "Body", "Applied across every screen"),
             ("Ios26FontSizeSmall", "Small", "config.json · saved"),
             ("Ios26FontSizeLabel", "Label", "CARD CORNER RADIUS"),
             ("Ios26FontSizeCaption", "Caption", "12 OF 28 PX")]
    half = (c.cx1 - c.cx0 - 24) / 2
    for i, (tok, name, sample) in enumerate(roles):
        col, row = i % 2, i // 2
        x = c.cx0 + col * (half + 24)
        y = c.y + row * 46
        inner(c.layer, (x, y, x + half, y + 40), th, kind="inner")
        text(c.layer, (x + 12, y + 20), f"{name} · {int(th.fs(tok))}", th,
             "caption", color=th.accent_soft_text, anchor="lm", size=9)
        text(c.layer, (x + 12 + 92, y + 20), sample, th, "small",
             color=th.title, anchor="lm", size=th.fs(tok),
             weight="bold" if th.fs(tok) >= 16 else "semibold" if th.fs(tok) == 14 else "regular",
             maxw=half - 110)
    c.y += ((len(roles) + 1) // 2) * 46 + 6
    c.close()

    c = p.card(T("Settings.CardGeometry"), T("Settings.OneRadiusAuthorityEveryCardInTheAppFollowsIt"))
    # live radius diagram: card → inner → tile → chip → small
    dy = c.y
    demo_x = c.cx0
    inner(c.layer, (demo_x, dy, demo_x + 300, dy + 132), th, kind="inner")
    card(c.layer, (demo_x + 12, dy + 12, demo_x + 288, dy + 72), th, kind="flat",
         radius=th.r("Ios26RadiusCard") * 0.5)
    inner(c.layer, (demo_x + 24, dy + 24, demo_x + 200, dy + 60), th, kind="wash",
          radius=th.r("Ios26RadiusInner") * 0.6)
    chip(c.layer, (demo_x + 210, dy + 30), "CHIP 12", th, variant="accent")
    chip(c.layer, (demo_x + 210, dy + 58), "SMALL 6", th, variant="muted")
    text(c.layer, (demo_x + 12, dy + 86), "card 26", th, "caption", size=9)
    text(c.layer, (demo_x + 80, dy + 86), "inner 12", th, "caption", size=9)
    text(c.layer, (demo_x + 150, dy + 86), "tile 10", th, "caption", size=9)
    text(c.layer, (demo_x + 214, dy + 86), "chip 12", th, "caption", size=9)
    text(c.layer, (demo_x + 12, dy + 104), "published from ThemeManager.ApplyCardCornerRadius "
         "(default 12) — ratios 26/12 · 12/12 · 10/12 · 12/12 · 6/12", th, "row_meta")
    # right column: the sliders
    rx = c.cx0 + 324
    for i, (name, key) in enumerate([("Card", "Ios26RadiusCard"), ("Inner", "Ios26RadiusInner"),
                                     ("Tile", "Ios26RadiusTile"), ("Chip", "Ios26RadiusChip"),
                                     ("Small", "Ios26RadiusSmall")]):
        y = dy + i * 26
        text(c.layer, (rx, y + 13), name, th, "field_label", anchor="lm")
        slider(c.layer, (rx + 60, y + 1, rx + 250, y + 25), th.r(key) / 48, th,
               label=str(int(th.r(key))), height=24)
        text(c.layer, (rx + 262, y + 13), f"{int(th.r(key))} px", th, "field_value",
             anchor="lm", size=12)
    c.y = dy + 140
    c.close()

    c = p.card(T("Settings.DisplayDensity"), T("Settings.FourProfilesLiftSizesThatAre13PxOrLarger"))
    cols = ["Compact", "Normal", "Comfortable", "Spacious"]
    segmented(c.layer, (c.cx0, c.y, c.cx1, c.y + 34), cols, 0, th, fill=True)
    c.y += 46
    rows = [("Body", "13", "13", "14", "15"),
            ("Field value", "14", "14", "15", "16"),
            ("Card title", "17", "17", "18", "19"),
            ("Row height", "28", "30", "34", "38")]
    hw = (c.cx1 - c.cx0) / 5
    for j, head in enumerate(["ROLE"] + cols):
        text(c.layer, (c.cx0 + j * hw + (0 if j == 0 else hw / 2), c.y),
             head, th, "group_caption", anchor="lt" if j == 0 else "mt")
    c.y += 22
    for r in rows:
        for j, val in enumerate(r):
            text(c.layer, (c.cx0 + j * hw + (0 if j == 0 else hw / 2), c.y),
                 val, th, "row_title" if j == 0 else "field_value",
                 anchor="lt" if j == 0 else "mt", size=12.5,
                 weight="semibold" if j == 0 else "regular",
                 color=th.body if j == 0 else th.secondary)
        c.y += 24
        separator(c.layer, c.cx0, c.y - 6, c.cx1, th)
    c.y += 4
    c.close()

    c = p.card(T("Settings.LanguageCurrency"), T("Settings.SwitchTheWholeAppInstantlyEnglishStaysPrimary"))
    c.row(icon_name="globe", title=T("Settings.Language"),
          meta=T("Settings.FifteenLanguagesAuthoredNotMachineFallbacks"),
          trailing=tr_combo("English (United States)"))
    c.row(icon_name="tag", title=T("Settings.Currency"),
          meta=T("Settings.AppliesToEveryMoneyFieldAndPDF"),
          trailing=tr_combo("AED · United Arab Emirates Dirham"))
    c.row(icon_name="clock", title=T("Settings.RegionalDetection"),
          meta=T("Settings.DetectsLocaleOnFirstRunOnly"),
          trailing=tr_toggle(True), sep=False)
    c.close()
    p.finish()


def page_themes_wallpaper(cv, box, th):
    """Sheet 04 — Themes & Wallpaper."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.VisualTheme"),
               T("Settings.IndustrialMonetIOS26OrIPhone17ProMaxRestyles"))
    families = [("Industrial", "Studio", "palette", True),
                ("Monet", "Golden Hour", "wand", False),
                ("iPhone 17 Pro Max", "Crystal Fluid", "sparkle", False),
                ("iOS 26", "Aurora", "layers", False)]
    tw = (c.cx1 - c.cx0 - 3 * 14) / 4
    for i, (name, scene, ic, sel) in enumerate(families):
        x = c.cx0 + i * (tw + 14)
        y = c.y
        hh = 118
        kind = "info" if sel else "inner"
        inner(c.layer, (x, y, x + tw, y + hh), th, kind=kind)
        if sel:
            cv.paste(stroke_ring(int(tw * cv.s), int(hh * cv.s),
                                 th.r("Ios26RadiusInner") * cv.s, 2 * cv.s,
                                 [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                   "stops": [(0, th.accent), (1, th.accent)]}]),
                     # ring must land on the card layer, not the page canvas
                     0, 0) if False else None
            stroke = stroke_ring(int(tw * cv.s), int(hh * cv.s),
                                 th.r("Ios26RadiusInner") * cv.s, 2 * cv.s,
                                 [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                   "stops": [(0, th.accent), (1, th.accent)]}])
            c.layer.paste(stroke, x, y)
        # mini scene preview
        scene_cols = [(3, 105, 161, 255), (217, 119, 6, 255),
                      (56, 189, 248, 255), (124, 58, 237, 255)]
        col = scene_cols[i]
        c.layer.paste(paint_layers(int((tw - 20) * cv.s), int(46 * cv.s),
                                   [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (1, 1),
                                     "stops": [(0, with_alpha(col, 0.85)),
                                               (0.6, with_alpha(col, 0.35)),
                                               (1, with_alpha(col, 0.12))]}],
                                   cached_mask(int((tw - 20) * cv.s), int(46 * cv.s),
                                               8 * cv.s)), x + 10, y + 10)
        icon(c.layer, (x + 12, y + 66, x + 34, y + 88), ic,
             th.accent_soft_text if sel else th.secondary, 1.7)
        text(c.layer, (x + 40, y + 70), name, th, "row_title", size=12.5)
        text(c.layer, (x + 40, y + 86), scene, th, "row_meta")
        if sel:
            chip(c.layer, (x + tw - 66, y + 10), "ACTIVE", th, variant="accent")
    c.y += 118 + 14
    c.row(icon_name="refresh", title=T("Settings.ResetToDefault"),
          meta=T("Settings.RestoreThemeAccentCardShapeDensityZoomFont"),
          trailing=tr_button(T("Common.Reset"), kind="secondary"), sep=False)
    c.close()

    c = p.card(T("Settings.CrystalFluidMaterial"),
               T("Settings.SixLiveAxesTuneTheCrystalWaterWaterCausticsBubbles"))
    axes = [("Water opacity", 0.62), ("Caustic intensity", 0.45),
            ("Bubble intensity", 0.30), ("Wave amplitude", 0.55),
            ("Reflection opacity", 0.40), ("Animation intensity", 0.70)]
    half = (c.cx1 - c.cx0 - 28) / 2
    for i, (name, v) in enumerate(axes):
        col, row = i % 2, i // 2
        x = c.cx0 + col * (half + 28)
        y = c.y + row * 52
        text(c.layer, (x, y), name, th, "field_label")
        slider(c.layer, (x, y + 16, x + half - 70, y + 44), v, th)
        text(c.layer, (x + half - 58, y + 30), f"{int(v * 100)}%", th,
             "field_value", anchor="lm", size=12)
    c.y += 3 * 52 + 6
    c.close()

    c = p.card(T("Settings.Wallpapers"),
               T("Settings.OneImageBehindEveryModulePlusPerModuleOverrides"))
    scenes = [("Studio", True), ("Golden Hour", False), ("Crystal Sea", False),
              ("Aurora", False), ("Slate", False), ("Ivory", False)]
    tw = (c.cx1 - c.cx0 - 2 * 14) / 3
    for i, (name, sel) in enumerate(scenes):
        col, row = i % 3, i // 3
        x = c.cx0 + col * (tw + 14)
        y = c.y + row * 96
        cols = [(148, 163, 184, 255), (217, 119, 6, 255), (8, 145, 178, 255),
                (124, 58, 237, 255), (71, 85, 105, 255), (226, 232, 240, 255)]
        c.layer.paste(paint_layers(int(tw * cv.s), int(64 * cv.s),
                                   [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (1, 1),
                                     "stops": [(0, with_alpha(cols[i], 0.9)),
                                               (1, with_alpha(cols[i], 0.25))]}],
                                   cached_mask(int(tw * cv.s), int(64 * cv.s),
                                               th.r("Ios26RadiusInner") * cv.s)), x, y)
        if sel:
            c.layer.paste(stroke_ring(int(tw * cv.s), int(64 * cv.s),
                                      th.r("Ios26RadiusInner") * cv.s, 2.4 * cv.s,
                                      [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                        "stops": [(0, th.title), (1, th.title)]}]), x, y)
            icon(c.layer, (x + tw - 26, y + 8, x + tw - 10, y + 24), "check",
                 (255, 255, 255, 255), 2.4)
        text(c.layer, (x + 2, y + 70), name, th, "row_title", size=12)
        if sel:
            text(c.layer, (x + tw, y + 70), "· applied", th, "row_meta", anchor="rt",
                 color=th.accent_soft_text)
    c.y += 2 * 96 + 10
    c.close()
    p.finish()


def page_ai(cv, box, th):
    """Sheet 05 — AI Assistant (dark)."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Assistant"),
               T("Settings.MasterSwitchesAndTheTuningValuesUsedOnEveryRequest"))
    c.row(icon_name="sparkle", title=T("Settings.EnableAIAssistant"),
          meta=T("Settings.TurnsTheAssistantAndEveryAIActionOnOrOff"),
          trailing=tr_toggle(True))
    c.row(icon_name="building", title=T("Settings.UseRealBusinessData"),
          meta=T("Settings.AllowsTheAssistantToReadLiveModuleData"),
          trailing=tr_toggle(False))
    c.row(icon_name="cpu", title=T("Settings.ActiveModel"),
          meta=T("Settings.OnlyTheActiveProviderProcessesRequests"),
          trailing=tr_combo("Google Gemini · gemini-2.0-flash"))
    c.row(icon_name="sliders", title=T("Settings.MaxTokens"),
          meta=T("Settings.UpperBoundPerRequest"),
          trailing=tr_slider(0.5, label="2048"), height=44)
    c.row(icon_name="sliders", title=T("Settings.Temperature"),
          meta=T("Settings.CreativityVersusDeterminism"),
          trailing=tr_slider(0.35, label="0.7"), height=44, sep=False)
    c.close()

    c = p.card(T("Settings.Providers"),
               T("Settings.OnlyTheACTIVEProviderProcessesYourRequestsKeysAreEncrypted"))
    providers = [("Google Gemini", "gemini-2.0-flash", True, "green", "ONLINE"),
                 ("Groq", "llama-3.3-70b", False, "muted", "NO KEY"),
                 ("Cohere", "command-r-plus", False, "muted", "NO KEY")]
    for name, model, sel, cvv, ctext in providers:
        y = c.y
        inner(c.layer, (c.cx0, y, c.cx1, y + 66), th,
              kind="info" if sel else "inner")
        # radio
        rr = 9
        c.layer.d.ellipse([(c.cx0 + 14) * cv.s, (y + 24) * cv.s,
                           (c.cx0 + 14 + rr * 2) * cv.s, (y + 24 + rr * 2) * cv.s],
                          outline=rgba(th.accent if sel else th.faint),
                          width=max(1, int(1.6 * cv.s)))
        if sel:
            c.layer.d.ellipse([(c.cx0 + 19) * cv.s, (y + 29) * cv.s,
                               (c.cx0 + 19 + 8) * cv.s, (y + 29 + 8) * cv.s],
                              fill=rgba(th.accent if not th.dark else (10, 20, 30, 255)))
        text(c.layer, (c.cx0 + 44, y + 20), name, th, "row_title")
        text(c.layer, (c.cx0 + 44, y + 40), model, th, "row_meta")
        if sel:
            value_pill(c.layer, (c.cx0 + 200, y + 24), "AIza••••••••••••4FkQ", th,
                       trailing="copy")
        w, h = chip_size(th, ctext)
        chip(c.layer, (c.cx1 - 12 - w, y + 24), ctext, th, variant=cvv)
        c.y = y + 66 + 10
    c.row(icon_name="key", title=T("Settings.KeysFile"),
          meta=T("Settings.EncryptedAtRestWithTheMachineKey"),
          trailing=tr_pill("%APPDATA%\\ProGlass\\ai-keys.enc", "folder"), sep=False)
    c.close()

    c = p.card(T("Settings.Security"),
               T("Settings.ControlKeyPersistenceAndWipeStoredConversationMemory"))
    c.row(icon_name="lock", title=T("Settings.KeepKeysSaved"),
          meta=T("Settings.RemembersProviderKeysBetweenSessions"),
          trailing=tr_toggle(True))
    c.row(icon_name="trash", title=T("Settings.RemoveSavedKeys"),
          meta=T("Settings.DeletesTheEncryptedKeyFileImmediately"),
          trailing=tr_button(T("Common.Remove"), kind="secondary"))
    c.row(icon_name="shield", title=T("Settings.ClearAIMemory"),
          meta=T("Settings.WipesEveryStoredConversationOnThisMachine"),
          trailing=tr_button(T("Common.Clear"), kind="danger"), sep=False)
    c.close()
    p.finish()


def page_agent(cv, box, th):
    """Sheet 06 (right window) — AI & Agent."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Privacy"),
               T("Settings.ControlWhetherTheAgentTalksToTheInternet"))
    c.row(icon_name="lock", title="Local only",
          meta=T("Settings.AgentNeverLeavesThisMachine"),
          trailing=tr_toggle(True))
    c.row(icon_name="cloud", title="Cloud consent",
          meta=T("Settings.AskBeforeAnyRequestLeavesTheMachine"),
          trailing=tr_toggle(True))
    inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 74), th, kind="info")
    text(c.layer, (c.cx0 + 12, c.y + 10), T("Settings.WhatThisMeans"), th,
         "row_title", color=th.accent_soft_text, size=12.5)
    text(c.layer, (c.cx0 + 12, c.y + 28),
         "With both switches on the agent answers from the local knowledge base and "
         "asks before any web lookup. Keys, jobs and customer data never leave this "
         "machine without a prompt.", th, "row_meta", maxw=c.cx1 - c.cx0 - 24, wrap=True)
    c.y += 74 + 12
    c.row(icon_name="cpu", title=T("Settings.OfflineAIModelInstallOnceWorksWithoutInternet"),
          meta=T("Settings.LocalModelAdvice"),
          trailing=tr_button(T("Common.Install"), kind="secondary"), sep=False)
    c.close()

    c = p.card(T("Settings.MemoryKnowledge"),
               T("Settings.SavedMemoryAndTheKnowledgeBaseLiveInTheAgentSOwnDatabase"))
    c.row(icon_name="database", title=T("Settings.AgentData"),
          meta=T("Settings.SeparateFromBusinessDataByDesign"),
          trailing=tr_pill("agent-store.db · 18 MB", "folder"))
    c.row(icon_name="book", title=T("Settings.SavedMemory"),
          meta=T("Settings.FactsTheAgentRemembersBetweenSessions"),
          trailing=tr_chip("42 ITEMS", "muted"))
    c.row(icon_name="layers", title=T("Settings.KnowledgeBase"),
          meta=T("Settings.IndexedDocumentsTheAgentCanCite"),
          trailing=tr_chip("7 DOCS", "green"))
    c.row(icon_name="trash", title=T("Settings.ForgetEverything"),
          meta=T("Settings.WipesMemoryAndTheIndexKeepsBusinessData"),
          trailing=tr_button(T("Common.Clear"), kind="danger"), sep=False)
    c.close()

    c = p.card(T("Settings.Capabilities"), T("Settings.WhatTheAgentMayDoOnYourBehalf"))
    caps = [("Draft documents", True), ("Read module data", True),
            ("Run workflows", False), ("Send messages", False),
            ("Edit prices", False), ("Export PDFs", True)]
    half = (c.cx1 - c.cx0 - 28) / 2
    for i, (name, on) in enumerate(caps):
        col, row = i % 2, i // 2
        x = c.cx0 + col * (half + 28)
        y = c.y + row * 44
        inner(c.layer, (x, y, x + half, y + 38), th, kind="inner")
        icon_disc(c.layer, (x + 8, y + 5), "check" if on else "close", th, size=28,
                  bg=th.success_soft if on else th.pill,
                  fg=th.success if on else th.muted)
        text(c.layer, (x + 44, y + 19), name, th, "row_title", anchor="lm", size=12.5)
        toggle(c.layer, (x + half - 54, y + 8), on, th, size=0.62)
    c.y += 3 * 44 + 8
    c.close()
    p.finish()


def page_voice(cv, box, th):
    """Sheet 06 (left window) — Voice (Whisper speech-to-text)."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.WhisperModel"),
               T("Settings.PickModelSizeDownloadOnceLargerModelsMore"))
    c.label(T("Settings.ChooseModel").upper())
    text(c.layer, (c.cx0 + 2, c.y), T("Settings.ClickCardSelectOneModelActiveTimeSwitch"),
         th, "row_meta")
    c.y += 22
    models = [("Base (Recommended)", 142, "95%+", "Fast (1-3 sec)",
               "Best balance of size, speed, and accuracy. Recommended for daily use.", True),
              ("Medium (High Accuracy)", 1500, "97%+", "Moderate (3-8 sec)",
               "Higher accuracy for noisy environments. Large download (~1.5 GB).", False),
              ("Large v3 (Maximum Accuracy)", 3100, "99%+", "Slow (5-15 sec)",
               "Best possible accuracy. Very large download (~3.1 GB). Needs 2+ GB RAM.", False)]
    for name, mb, acc, spd, desc, sel in models:
        y = c.y
        hh = 78
        inner(c.layer, (c.cx0, y, c.cx1, y + hh), th, kind="info" if sel else "inner")
        if sel:
            c.layer.paste(stroke_ring(int((c.cx1 - c.cx0) * cv.s), int(hh * cv.s),
                                      th.r("Ios26RadiusInner") * cv.s, 2 * cv.s,
                                      [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                        "stops": [(0, th.accent), (1, th.accent)]}]),
                          c.cx0, y)
        # radio + name
        c.layer.d.ellipse([(c.cx0 + 14) * cv.s, (y + 14) * cv.s,
                           (c.cx0 + 32) * cv.s, (y + 32) * cv.s],
                          outline=rgba(th.accent if sel else th.faint),
                          width=max(1, int(1.6 * cv.s)))
        if sel:
            c.layer.d.ellipse([(c.cx0 + 19) * cv.s, (y + 19) * cv.s,
                               (c.cx0 + 27) * cv.s, (y + 27) * cv.s],
                              fill=rgba(th.accent))
        text(c.layer, (c.cx0 + 44, y + 16), name, th, "row_title")
        text(c.layer, (c.cx0 + 44, y + 34),
             f"{mb} MB   ·   {acc} accuracy   ·   {spd}", th, "row_meta")
        text(c.layer, (c.cx0 + 44, y + 52), desc, th, "row_meta",
             maxw=c.cx1 - c.cx0 - 60)
        if sel:
            w, h = chip_size(th, "ACTIVE")
            chip(c.layer, (c.cx1 - 12 - w, y + 14), "ACTIVE", th, variant="accent")
        c.y = y + hh + 10
    c.close()

    c = p.card(T("Common.Status"), None, chip="INSTALLED", chip_v="green")
    c.row(icon_name="doc", title="ggml-base.bin",
          meta=T("Settings.ModelFileOnDisk"),
          trailing=tr_pill("C:\\ProGlass\\models\\ggml-base.bin", "folder"))
    c.row(icon_name="chart", title=T("Settings.SizeOnDisk"),
          meta=T("Settings.DownloadedOnceSharedByEveryModule"),
          trailing=tr_text("142 MB"))
    inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 64), th, kind="success")
    icon(c.layer, (c.cx0 + 12, c.y + 12, c.cx0 + 34, c.y + 34), "check", th.success, 2.2)
    text(c.layer, (c.cx0 + 44, c.y + 14), T("Settings.VoiceModelReady"), th,
         "row_title", color=th.success, size=13)
    text(c.layer, (c.cx0 + 44, c.y + 34),
         "Dictation is live in Inquiry, Job Order and the AI chat — hold ⌘ to speak.",
         th, "row_meta")
    c.y += 64 + 14
    c.row(icon_name="download", title=T("Settings.VoiceDownloadModel"),
          meta=T("Settings.DownloadAModelToSwitchSizes"),
          trailing=tr_button(T("Common.Download"), kind="secondary"), sep=False)
    c.close()
    p.finish()


def page_data(cv, box, th):
    """Sheet 07 (left) — Data & Backup."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.CompanyLANWiFIDatabase"),
               T("Settings.ConnectAllOfficeWorkstationsToOneSharedProGlassDataFolder"))
    c.row(icon_name="wifi", title=T("Settings.DataRootMode"),
          meta=T("Settings.LocalFolderOrASharedNetworkPath"),
          trailing=tr_combo("Shared network folder"))
    c.row(icon_name="folder", title=T("Settings.DataFolder"),
          meta=T("Settings.EveryWorkstationMustPointAtTheSameFolder"),
          trailing=tr_pill("\\\\UAE-SVR\\ProGlass\\Data", "copy"))
    c.row(icon_name="users", title=T("Settings.WorkstationsSeen"),
          meta=T("Settings.LastSevenDaysOfWritesFromThisFolder"),
          trailing=tr_chip("6 MACHINES", "green"), sep=False)
    c.close()

    c = p.card(T("Settings.BackupRestore"),
               T("Settings.ABackupSetCopiesEveryDatabaseSettingsIntoOneTimestampedFolder"))
    # storage bar
    inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 62), th, kind="inner")
    text(c.layer, (c.cx0 + 12, c.y + 10), T("Settings.Storage"), th, "metric_label")
    text(c.layer, (c.cx0 + 12, c.y + 24), "412 MB", th, "metric_value")
    text(c.layer, (c.cx1 - 12, c.y + 10), "of 2 GB in the vault · 9 sets kept",
         th, "row_meta", anchor="rt")
    progress(c.layer, (c.cx0 + 12, c.y + 46, c.cx1 - 12, c.y + 54), 0.21, th)
    c.y += 62 + 14
    c.row(icon_name="download", title=T("Settings.BackupActions"),
          meta=T("Settings.CreateACimestampedSetOfEveryDatabase"),
          trailing=tr_button(T("Settings.CreateBackupNow"), kind="primary"))
    c.row(icon_name="sync", title=T("Settings.Restore"),
          meta=T("Settings.ReplaceTheLiveDatabasesFromASet"),
          trailing=tr_combo("2026-09-14 02:00 · nightly"))
    sets = [("2026-09-15 02:00", "nightly · 412 MB", "green", "VERIFIED"),
            ("2026-09-14 02:00", "nightly · 408 MB", "green", "VERIFIED"),
            ("2026-09-12 18:41", "manual · 402 MB", "muted", "KEPT")]
    c.label(T("Settings.BackupSets").upper())
    for d, meta, v, ct in sets:
        c.row(icon_name="database", title=d, meta=meta,
              trailing=tr_chip(ct, v), height=38)
    c.row(icon_name="folder", title=T("Settings.BackupVault"),
          meta=T("Settings.WhereSetsAreWritten"),
          trailing=tr_pill("D:\\ProGlassBackups", "folder"), sep=False)
    c.close()

    c = p.card(T("Settings.SampleData"),
               T("Settings.NewHereLoadASmallLinkedDemoDatasetToExplore"))
    inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 58), th, kind="info")
    icon(c.layer, (c.cx0 + 12, c.y + 12, c.cx0 + 34, c.y + 34), "info",
         th.accent_soft_text, 1.8)
    text(c.layer, (c.cx0 + 44, c.y + 12), T("Settings.ExploreWithSampleData"), th,
         "row_title", size=12.5)
    text(c.layer, (c.cx0 + 44, c.y + 30),
         "38 linked records across Inquiry → Job Order → Delivery → Invoice. Removable in one click.",
         th, "row_meta")
    c.y += 58 + 12
    c.row(icon_name="play", title=T("Settings.LoadSampleData"),
          meta=T("Settings.AddsTheDemoDatasetToTheLiveDatabases"),
          trailing=tr_button(T("Common.Load"), kind="secondary"), sep=False)
    c.close()

    c = p.card(T("Settings.DatabaseHealth"),
               T("Settings.ProbesEveryProGlassAutomationDatabaseFile"),
               chip="ALL HEALTHY", chip_v="green")
    dbs = [("proglass_main.db", "green"), ("proglass_jobs.db", "green"),
           ("proglass_inventory.db", "green"), ("proglass_finance.db", "green")]
    for name, v in dbs:
        c.row(icon_name="database", title=name,
              meta=T("Settings.SchemaCurrentMigrationAppliedOnOpen"),
              trailing=tr_chip("OK", v), height=36)
    c.row(icon_name="alert", title=T("Settings.PurgeAllData"),
          meta=T("Settings.DeletesEveryDatabaseAndSettingsFromThisMachine"),
          trailing=tr_button(T("Common.Purge"), kind="danger"), sep=False)
    c.close()
    p.finish()


def page_demo(cv, box, th):
    """Sheet 07 (right) — Demo Center."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.RecordThisSession"),
               T("Settings.RecordTheRunningAppWindowWithoutAModuleWorkflow"))
    c.row(icon_name="film", title=T("Settings.FFMPEG"),
          meta=T("Settings.BundledEncoderNoInstallNeeded"),
          trailing=tr_pill("ffmpeg 7.1 · h264_nvenc", "check"))
    c.row(icon_name="play", title=T("Settings.RecordASession"),
          meta=T("Settings.CapturesOnlyThisRunningAppWindow"),
          trailing=tr_button(T("Settings.StartRecording"), kind="primary",
                             icon_name="play"))
    c.row(icon_name="sliders", title=T("Settings.Quality"),
          meta=T("Settings.BitratePresetForTheCapture"),
          trailing=tr_combo("High · 12 Mbps"))
    c.row(icon_name="grid", title=T("Settings.FrameRate"),
          meta=T("Settings.SmoothnessVersusFileSize"),
          trailing=tr_combo("30 fps"), sep=False)
    c.close()

    c = p.card(T("Settings.RecordingSettings"),
               T("Settings.CapturesOnlyThisRunningAppWindowNotTheDesktop"))
    c.row(icon_name="wave", title=T("Settings.InternalAudio"),
          meta=T("Settings.SystemSoundFromTheAppWindow"),
          trailing=tr_toggle(True))
    c.row(icon_name="mic", title=T("Settings.Microphone"),
          meta=T("Settings.NarrationTrackMixedIntoTheCapture"),
          trailing=tr_combo("Headset (Default)"), sep=False)
    c.close()

    c = p.card(T("Settings.Workflow"),
               T("Settings.PickAModuleGenerateLandsOnThatWorkspaceFirst"))
    c.row(icon_name="grid", title=T("Settings.Module"),
          meta=T("Settings.TheWorkspaceTheDemoLandsOn"),
          trailing=tr_combo("Job Order"))
    c.row(icon_name="sparkle", title=T("Settings.Generate"),
          meta=T("Settings.WritesAScriptedRunIntoTheDemoWorkspace"),
          trailing=tr_button(T("Common.Generate"), kind="secondary"), sep=False)
    c.close()

    c = p.card(T("Settings.Isolation"), T("Settings.DemoModeKeepsProductionDataUntouched"),
               chip="ISOLATED", chip_v="green")
    c.row(icon_name="shield", title=T("Settings.DemoMode"),
          meta=T("Settings.WritesGoToASidecarDatabaseOnly"),
          trailing=tr_toggle(True))
    inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 54), th, kind="info")
    icon(c.layer, (c.cx0 + 12, c.y + 10, c.cx0 + 34, c.y + 32), "shield",
         th.accent_soft_text, 1.8)
    text(c.layer, (c.cx0 + 44, c.y + 10),
         "Production data is untouched while Demo Mode is on — recordings and generated "
         "runs land in demo-sidecar.db.", th, "row_meta", maxw=c.cx1 - c.cx0 - 56, wrap=True)
    c.y += 54 + 12
    c.row(icon_name="clock", title=T("Settings.State"),
          meta=T("Settings.WhatTheLastRunDid"),
          trailing=tr_chip("IDLE", "muted"))
    c.row(icon_name="folder", title=T("Settings.LastOutput"),
          meta=T("Settings.WhereTheLastCaptureWasWritten"),
          trailing=tr_pill("D:\\ProGlass\\captures\\job-order-0914.mp4", "folder"),
          sep=False)
    c.close()

    c = p.card(T("Settings.History"), T("Settings.ALedgerOfEveryRecordingJobRunOnThisMachine"))
    for d, name, st, v in [("2026-09-14 16:02", "job-order-0914.mp4 · 41 MB", "DONE", "green"),
                           ("2026-09-13 11:47", "inquiry-walkthrough.mp4 · 28 MB", "DONE", "green"),
                           ("2026-09-12 09:15", "invoice-run.mp4 · —", "FAILED", "danger")]:
        c.row(icon_name="film", title=name, meta=d,
              trailing=tr_chip(st, v), height=38)
    c.gap(4)
    c.close(min_h=c.y + 8)
    p.finish()


def page_updates(cv, box, th):
    """Sheet 08 (left) — Updates."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Overview"),
               T("Settings.YourRunningVersionAgainstTheLatestPublishedRelease"),
               chip="UP TO DATE", chip_v="green")
    half = (c.cx1 - c.cx0 - 14) / 2
    inner(c.layer, (c.cx0, c.y, c.cx0 + half, c.y + 74), th, kind="inner")
    text(c.layer, (c.cx0 + 14, c.y + 12), T("Settings.RunningVersion"), th, "metric_label")
    text(c.layer, (c.cx0 + 14, c.y + 26), "4.2.118", th, "metric_value")
    text(c.layer, (c.cx0 + 14, c.y + 54), "installed 2026-09-08", th, "row_meta")
    inner(c.layer, (c.cx0 + half + 14, c.y, c.cx1, c.y + 74), th, kind="inner")
    text(c.layer, (c.cx0 + half + 28, c.y + 12), T("Settings.LatestRelease"), th, "metric_label")
    text(c.layer, (c.cx0 + half + 28, c.y + 26), "4.2.118", th, "metric_value")
    text(c.layer, (c.cx0 + half + 28, c.y + 54), "published 2026-09-07", th, "row_meta")
    c.y += 74 + 14
    c.row(icon_name="sync", title=T("Settings.Channel"),
          meta=T("Settings.StableShipsTestedBetaShipsFirst"),
          trailing=tr_combo("Stable"))
    c.row(icon_name="download", title=T("Settings.CheckForUpdates"),
          meta=T("Settings.LastCheck202609150812"),
          trailing=tr_button(T("Settings.CheckNow"), kind="secondary",
                             icon_name="refresh"), sep=False)
    c.close()

    c = p.card(T("Settings.ReleaseNotes"),
               T("Settings.WhatThisReleaseChangesStraightFromThePublisher"))
    notes = [("Settings", "Crystal-liquid material on every settings card; rail and "
                          "cards now read as one surface."),
             ("Voice", "Whisper model picker with per-model size, accuracy and speed."),
             ("Shell", "Quick-settings panel rebuilt from the page registry — all "
                       "twelve pages reachable.")]
    for head, body in notes:
        inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 52), th, kind="inner")
        chip(c.layer, (c.cx0 + 12, c.y + 9), head.upper(), th, variant="accent")
        text(c.layer, (c.cx0 + 12, c.y + 30), body, th, "row_meta",
             maxw=c.cx1 - c.cx0 - 24)
        c.y += 52 + 8
    c.close()

    c = p.card(T("Settings.UpdateHistory"),
               T("Settings.EveryCheckDownloadAndFailureRecordedOnThisMachine"))
    for d, what, st, v in [("2026-09-15 08:12", "check · stable channel", "NO UPDATE", "muted"),
                           ("2026-09-08 07:55", "install 4.2.118 · 96 MB", "INSTALLED", "green"),
                           ("2026-09-01 08:00", "download 4.2.110 · 94 MB", "FAILED", "danger")]:
        c.row(icon_name="clock", title=what, meta=d, trailing=tr_chip(st, v), height=38)
    c.gap(2)
    c.close()
    p.finish()


def page_license(cv, box, th):
    """Sheet 08 (right) — License."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.LicenseStatus"),
               "Activated on this machine on 2026-04-02. Seat 3 of 5.",
               chip="VALID", chip_v="green")
    rows = [(T("Settings.Edition"), "ProGlass Automation · Pro (UAE)"),
            (T("Settings.Licensee"), "Al Noor Glass Factory LLC, Sharjah"),
            (T("Settings.Machines"), "3 of 5 seats in use"),
            (T("Settings.RenewsOn"), "2027-03-31 · 198 days left")]
    for lab, val in rows:
        y = c.y
        inner(c.layer, (c.cx0, y, c.cx1, y + 40), th, kind="inner")
        text(c.layer, (c.cx0 + 12, y + 20), lab, th, "field_label", anchor="lm")
        text(c.layer, (c.cx1 - 12, y + 20), val, th, "field_value", anchor="rm",
             maxw=(c.cx1 - c.cx0) * 0.6)
        c.y = y + 40 + 8
    c.close()

    c = p.card(T("Settings.Activation"), T("Settings.PasteTheKeyFromYourInvoiceAndActivate"))
    c.label(T("Settings.LicenseKey").upper())
    input_box(c.layer, (c.cx0, c.y, c.cx1 - 130, c.y + 38),
              "PG-4F2K-9QMD-77XA-UE01", th, height=38)
    button(c.layer, (c.cx1 - 118, c.y + 2), T("Settings.Activate"), th, kind="primary")
    c.y += 38 + 14
    c.row(icon_name="key", title=T("Settings.ActivationId"),
          meta=T("Settings.BoundToThisMachineSHardware"),
          trailing=tr_pill("HW-88F2-11AC", "copy"))
    c.row(icon_name="doc", title=T("Settings.Invoice"),
          meta=T("Settings.ThePurchaseDocumentThisKeyCameFrom"),
          trailing=tr_pill("INV-2026-0417.pdf", "folder"), sep=False)
    c.close()
    p.finish()


def page_features(cv, box, th):
    """Sheet 09 (left) — Features (the flag page, one grammar)."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Session"), T("Settings.SignOutForAShiftHandoverOrForgetTheAutoSignIn"))
    c.row(icon_name="lock", title=T("Settings.SessionSecurity"),
          meta=T("Settings.LockTheModuleAfterIdleMinutes"),
          trailing=tr_toggle(True))
    c.row(icon_name="users", title=T("Settings.AutoSignIn"),
          meta=T("Settings.RememberThisOperatorOnThisMachine"),
          trailing=tr_toggle(False))
    c.row(icon_name="close", title=T("Settings.SignOut"),
          meta=T("Settings.EndsTheSessionImmediately"),
          trailing=tr_button(T("Settings.SignOut"), kind="secondary"), sep=False)
    c.close()

    modules = [("Proforma Invoice", "flag",
                [("FEDERAL TRN", True), ("HEADQUARTERS", False), ("REPRESENTATIVE", True),
                 ("COMPANY E-MAIL", True), ("BANK NAME", False), ("ACCOUNT / IBAN", True)]),
               ("Inquiry", "search",
                [("CUSTOMER TRN", True), ("REFERENCE CODE", False), ("SALES REPRESENTATIVE", True),
                 ("CLIENT PHONE", True), ("BILLING ADDRESS", False), ("PROJECT NAME", True)]),
               ("Job Order", "cpu",
                [("Status", True), ("Priority", True), ("Delivery date", True),
                 ("Profit margin %", True), ("Prepared by", False), ("Attention (Kind Attn.)", False)]),
               ("Delivery Note", "chart",
                [("Status", True), ("Vehicle no.", True), ("Driver name", False),
                 ("Gate pass no.", False), ("Received by", True), ("Site location", True)]),
               ("Tax Invoice", "doc",
                [("ISSUE DATE", True), ("VALID UNTIL", True), ("LPO / PO NUMBER", True),
                 ("Contract Number", False), ("BANK NAME", True), ("ACCOUNT / IBAN", True)])]
    for name, ic, flags in modules:
        on = sum(1 for _, v in flags if v)
        c = p.card(name, T("Settings.ShowOrHideOptionalFieldsInTheWizard"),
                   chip=f"{on}/{len(flags)} ON", chip_v="accent", icon_name=None)
        # flags as a two-column grid of switch rows inside one inner tile
        inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 3 * 40 + 12), th, kind="inner")
        half = (c.cx1 - c.cx0 - 24 - 24) / 2
        for i, (fname, fv) in enumerate(flags):
            col, row = i % 2, i // 2
            x = c.cx0 + 12 + col * (half + 24)
            y = c.y + 6 + row * 40
            icon(c.layer, (x, y + 9, x + 18, y + 27), "check" if fv else "minus",
                 th.success if fv else th.faint, 1.9)
            text(c.layer, (x + 26, y + 18), fname, th, "row_title", anchor="lm",
                 size=12.5, color=th.body if fv else th.muted)
            toggle(c.layer, (x + half - 46, y + 4), fv, th, size=0.6)
        c.y += 3 * 40 + 12 + 12
        c.close()
    p.finish()


def page_sharing(cv, box, th):
    """Sheet 09 (right) — Sharing."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card(T("Settings.Providers"), T("Settings.WhereDocumentsCanBeSentFrom"))
    for name, meta, on, ct, v in [("WhatsApp Business", T("Settings.SendsThePDFAndAMessageLink"), True, "LINKED", "green"),
                                  ("E-mail (SMTP)", T("Settings.CompanyMailboxConfiguredOnce"), True, "LINKED", "green"),
                                  ("SMS gateway", T("Settings.OptionalPaidGateway"), False, "NOT SET", "muted")]:
        c.row(icon_name="share" if "WhatsApp" in name else "doc" if "mail" in name.lower() else "wave",
              title=name, meta=meta, trailing=tr_toggle(on), height=40)
    c.row(icon_name="doc", title=T("Settings.AlwaysAttachPDF"),
          meta=T("Settings.TheDocumentRidesAlongWithEveryMessage"),
          trailing=tr_toggle(True), sep=False)
    c.close()

    c = p.card(T("Settings.Defaults"), T("Settings.WhatAPickBecomesWhenADocumentOpensTheShareSheet"))
    c.row(icon_name="share", title=T("Settings.DefaultProvider"),
          meta=T("Settings.PreSelectedInTheShareSheet"),
          trailing=tr_combo("WhatsApp Business"))
    c.row(icon_name="users", title=T("Settings.DefaultRecipient"),
          meta=T("Settings.CustomerContactFromTheRecord"),
          trailing=tr_combo("Primary contact"))
    c.row(icon_name="doc", title=T("Settings.DocumentRules"),
          meta=T("Settings.WhichModulesMayShareAndWhere"),
          trailing=tr_button(T("Common.Configure"), kind="secondary"), sep=False)
    c.close()

    c = p.card(T("Settings.MessageTemplates"), T("Settings.TheTextAroundTheDocumentLink"))
    for name, body in [("Invoice — polite", "Dear {contact}, please find invoice {doc} "
                                            "for {amount}. Thank you for your business."),
                       ("Job update — short", "Job {doc} is {status}. Delivery {date}. — {company}")]:
        inner(c.layer, (c.cx0, c.y, c.cx1, c.y + 56), th, kind="inner")
        text(c.layer, (c.cx0 + 12, c.y + 10), name, th, "row_title", size=12.5)
        text(c.layer, (c.cx0 + 12, c.y + 28), body, th, "row_meta",
             maxw=c.cx1 - c.cx0 - 90)
        button(c.layer, (c.cx1 - 76, c.y + 12), T("Common.Edit"), th, kind="secondary")
        c.y += 56 + 8
    c.close()
    p.finish()


def page_about(cv, box, th):
    """Sheet 10 (left) — About & Help, the module the grammar came from."""
    p = Page(cv, box, th, scroll=0.0)
    T = th.tr

    c = p.card()
    icon_disc(c.layer, (c.cx0, c.y), "layers", th, size=44)
    text(c.layer, (c.cx0 + 56, c.y + 2), "ProGlass Automation", th, "display", size=26)
    text(c.layer, (c.cx0 + 56, c.y + 34), "— built for UAE glass fabricators.", th,
         "card_note", size=12.5)
    c.y += 44 + 16
    c.row(icon_name="tag", title="Version 4.2.118 (build 20260908)",
          meta="Pro edition · 3 of 5 seats · renews 2027-03-31",
          trailing=tr_chip("VALID", "green"), height=40)
    c.close()

    c = p.card(T("Settings.LearnMore"), None)
    for ic, name, meta in [("palette", T("Settings.AboutTheThemeSystem"),
                            T("Settings.FamiliesAccentsDensityAndTheCrystalMaterial")),
                           ("sparkle", T("Settings.AboutTheAIAssistant"),
                            T("Settings.ProvidersKeysAndWhatTheAgentMayTouch")),
                           ("key", T("Settings.AboutYourAPIKeyPrivacy"),
                            T("Settings.EncryptedAtRestNeverSentExceptToYourProvider")),
                           ("mic", T("Settings.AboutVoiceModels"),
                            T("Settings.WhisperRunsOnDeviceNothingIsTranscribedInTheCloud"))]:
        c.row(icon_name=ic, title=name, meta=meta,
              trailing=tr_button("", kind="icon", icon_name="chevron_r"), height=44)
    c.gap(2)
    c.close()

    c = p.card(T("Settings.SupportDiagnostics"), None)
    c.row(icon_name="doc", title=T("Settings.Documentation"),
          meta=T("Settings.ShortcutsRecipesAndTheCrystalMaterialGuide"),
          trailing=tr_button(T("Common.Open")), height=44)
    c.row(icon_name="folder", title=T("Settings.OpenLogFolder"),
          meta=T("Settings.WhereEveryModuleWritesItsTrace"),
          trailing=tr_button(T("Common.Open")), height=44)
    c.row(icon_name="info", title=T("Settings.CopySystemReport"),
          meta=T("Settings.VersionThemeGpusAndLocaleToTheClipboard"),
          trailing=tr_button(T("Common.Copy")), height=44)
    c.gap(2)
    c.close()
    p.finish()


# ═══════════════════════════════════════════════════════════════════════════
# artboard panels (the spec column on sheet 01)
# ═══════════════════════════════════════════════════════════════════════════
def _panel(cv, box, title, sub=None):
    x0, y0, x1, y1 = box
    cv.paste(paint_layers(int((x1 - x0) * cv.s), int((y1 - y0) * cv.s),
                          [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                            "stops": [(0, (21, 24, 31, 255)), (1, (17, 20, 26, 255))]}],
                          cached_mask(int((x1 - x0) * cv.s), int((y1 - y0) * cv.s),
                                      14 * cv.s)), x0, y0)
    cv.paste(stroke_ring(int((x1 - x0) * cv.s), int((y1 - y0) * cv.s), 14 * cv.s, cv.s,
                         [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                           "stops": [(0, (52, 58, 72, 255)), (1, (34, 38, 48, 255))]}]),
             x0, y0)
    text(cv, (x0 + 18, y0 + 14), title, Theme("dark"), "row_title",
         color=BOARD_ACCENT, size=12.5, weight="bold")
    yy = y0 + 34
    if sub:
        text(cv, (x0 + 18, yy), sub, Theme("dark"), "label", color=BOARD_MUTED,
             size=10, maxw=x1 - x0 - 36, wrap=True)
        yy += 12 * 1.3 * (1 + int(text_w(sub, Theme("dark"), "label", size=10)
                                  // (x1 - x0 - 36))) + 6
    return yy


def _kv(cv, x, y, k, v, w=None, accent=False):
    text(cv, (x, y), k, Theme("dark"), "label", color=BOARD_MUTED, size=10)
    text(cv, (x + (w or 150), y), v, Theme("dark"), "small",
         color=BOARD_ACCENT if accent else BOARD_TEXT, size=10, weight="semibold")


def _spec_column(cv, x0, y0, x1, y1, th):
    """The kit: material anatomy, radii, spacing, type, controls, token map."""
    w = x1 - x0
    # ── 1 · the material ──
    y = _panel(cv, (x0, y0, x1, y0 + 240), "01 · THE CRYSTAL MATERIAL",
               "SettingsPageCardStyle composes six layers. Every value below is read "
               "from Themes/LightTheme.xaml at render time.")
    # zoomed card sample + exploded layer list
    sx, sy = x0 + 18, y + 6
    stage(cv, (sx - 8, sy - 8, sx + 258, sy + 128), th)
    card(cv, (sx, sy, sx + 250, sy + 120), th, kind="page")
    inner(cv, (sx + 14, sy + 14, sx + 236, sy + 66), th, kind="inner")
    text(cv, (sx + 24, sy + 26), "SettingsInnerTileStyle", Theme("dark"), "label",
         color=(30, 41, 59, 255), size=9.5)
    text(cv, (sx + 24, sy + 42), "translucent · radius 12", Theme("dark"), "label",
         color=(51, 65, 85, 255), size=9.5)
    chip(cv, (sx + 14, sy + 78), "L2 72→38%", th, variant="accent")
    layers = [("L0", "SettingsCardShadow", f"#{th.t['card_shadow']['color'][0]:02X}"
               f"{th.t['card_shadow']['color'][1]:02X}{th.t['card_shadow']['color'][2]:02X}"
               f" @ {th.t['card_shadow']['opacity']:.0%} · blur {th.t['card_shadow']['blur']:.0f} · depth {th.t['card_shadow']['depth']:.0f}"),
              ("L1", "SettingsCardEdgeBrush", "tri-stop · " +
               " / ".join(f"{c[3]/255:.0%}" for _, c in th.t["card_edge"][0]["stops"])),
              ("L2", "frosted base", "72% → 38% white, vertical"),
              ("L3", "specular rim", "top 20%, #E6FFFFFF → 0"),
              ("L4", "reflection sweep", "diagonal, top-left 62×34%"),
              ("L5", "refraction lip", "bottom 10% picks light back up")]
    lx = sx + 268
    for i, (tag, name, val) in enumerate(layers):
        yy = sy + i * 20
        cv.paste(paint_layers(int(26 * cv.s), int(14 * cv.s),
                              [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                                "stops": [(0, (30, 34, 44, 255)), (1, (30, 34, 44, 255))]}],
                              cached_mask(int(26 * cv.s), int(14 * cv.s), 4 * cv.s)), lx, yy)
        text(cv, (lx + 13, yy + 7), tag, Theme("dark"), "caption", color=BOARD_ACCENT,
             anchor="mm", size=8.5)
        text(cv, (lx + 34, yy), name, Theme("dark"), "small", color=BOARD_TEXT,
             size=10, weight="semibold")
        text(cv, (lx + 34, yy + 11), val, Theme("dark"), "label", color=BOARD_MUTED,
             size=8.6, maxw=w - (lx - x0) - 60)
    yy = sy + 132
    text(cv, (sx, yy), "gap between page cards: 22 px (was 14 — the “cards are too "
                       "close” report)", Theme("dark"), "label", color=BOARD_MUTED, size=9.5)

    # ── 2 · radii + spacing ──
    y2 = y0 + 254
    y = _panel(cv, (x0, y2, x1, y2 + 180), "02 · RADII & SPACING",
               "One radius authority (ThemeManager.ApplyCardCornerRadius) and the "
               "module spacing scale. Nothing else declares a number.")
    rows = [("Ios26RadiusCard", f"{int(th.r('Ios26RadiusCard'))}", "page card"),
            ("Ios26RadiusInner", f"{int(th.r('Ios26RadiusInner'))}", "inner tile"),
            ("Ios26RadiusTile", f"{int(th.r('Ios26RadiusTile'))}", "icon disc"),
            ("Ios26RadiusChip", f"{int(th.r('Ios26RadiusChip'))}", "chip · button"),
            ("Ios26RadiusSmall", f"{int(th.r('Ios26RadiusSmall'))}", "value pill")]
    for i, (k, v, what) in enumerate(rows):
        yy = y + i * 17
        text(cv, (x0 + 18, yy), k, Theme("dark"), "small", color=BOARD_TEXT, size=9.8)
        text(cv, (x0 + 160, yy), v + " px", Theme("dark"), "small", color=BOARD_ACCENT,
             size=9.8, weight="semibold")
        text(cv, (x0 + 210, yy), what, Theme("dark"), "label", color=BOARD_MUTED, size=9.2)
        # mini shape
        r = float(v)
        cv.paste(stroke_ring(int(46 * cv.s), int(14 * cv.s), min(r, 7) * cv.s, cv.s,
                             [{"rect": (0, 0, 1, 1), "start": (0, 0), "end": (0, 1),
                               "stops": [(0, (120, 130, 148, 255)), (1, (120, 130, 148, 255))]}]),
                 x1 - 76, yy)
    sp = [("SettingsCardPadding", "24,22"), ("SettingsGapCard", "22"),
          ("SettingsTilePadding", "12,10"), ("SettingsPagePadding", "18,14,18,18")]
    for i, (k, v) in enumerate(sp):
        yy = y + i * 17
        text(cv, (x0 + 280, yy), k, Theme("dark"), "small", color=BOARD_TEXT, size=9.8)
        text(cv, (x0 + 430, yy), v, Theme("dark"), "small", color=BOARD_ACCENT,
             size=9.8, weight="semibold")

    # ── 3 · type ladder ──
    y3 = y2 + 194
    y = _panel(cv, (x0, y3, x1, y3 + 208), "03 · TYPE LADDER",
               "AboutTypeScale.xaml — whole pixels only, three weights only.")
    ladder = [("Ios26FontSizeDisplay", "Display", "30"), ("Ios26FontSizeH1", "Page", "22"),
              ("Ios26FontSizeCardTitle", "Card title", "17"),
              ("Ios26FontSizeValue", "Field value", "14"),
              ("Ios26FontSizeBody", "Body", "13"), ("Ios26FontSizeSmall", "Small", "12"),
              ("Ios26FontSizeLabel", "Label", "11"), ("Ios26FontSizeCaption", "Caption", "10")]
    for i, (tok, name, px) in enumerate(ladder):
        yy = y + i * 19
        text(cv, (x0 + 18, yy), name, Theme("dark"), "small", color=BOARD_TEXT,
             size=th.fs(tok) * 0.72, weight="bold" if th.fs(tok) >= 16 else "semibold")
        text(cv, (x0 + 150, yy + 2), f"{px} px · " +
             ("Bold" if th.fs(tok) >= 16 else "SemiBold" if th.fs(tok) in (14, 11, 10) else "Regular"),
             Theme("dark"), "label", color=BOARD_MUTED, size=9.2)
        text(cv, (x1 - 18, yy + 2), tok.replace("Ios26FontSize", ""), Theme("dark"),
             "caption", color=(90, 98, 112, 255), anchor="rt", size=8.5)

    # ── 4 · controls ──
    y4 = y3 + 222
    y = _panel(cv, (x0, y4, x1, y4 + 240), "04 · CONTROL GRAMMAR",
               "Kept from the shipped module; only their containers changed.")
    lx = x0 + 18
    toggle(cv, (lx, y), True, th)
    toggle(cv, (lx + 74, y), False, th)
    text(cv, (lx + 140, y + 10), "ModernToggleSwitch", Theme("dark"), "label",
         color=BOARD_MUTED, size=9.2)
    y += 44
    segmented(cv, (lx, y, lx + 240, y + 30), ["Light", "Dark"], 1 if th.dark else 0, th)
    text(cv, (lx + 254, y + 8), "SettingsSegmentTrack", Theme("dark"), "label",
         color=BOARD_MUTED, size=9.2)
    y += 40
    slider(cv, (lx, y, lx + 240, y + 30), 0.62, th, label="62%", height=30)
    text(cv, (lx + 254, y + 8), "ModernSliderStyle", Theme("dark"), "label",
         color=BOARD_MUTED, size=9.2)
    y += 40
    xx = lx
    for lab, v in [("ONLINE", "green"), ("VALID", "green"), ("3 FILES", "muted"),
                   ("FAILED", "danger")]:
        ww, hh = chip(cv, (xx, y), lab, th, variant=v)
        xx += ww + 8
    y += 30
    xx = lx
    for lab, kind in [("Save", "primary"), ("Reset", "secondary")]:
        ww, hh = button(cv, (xx, y), lab, th, kind=kind)
        xx += ww + 10
    ww, hh = button(cv, (xx, y), "", th, kind="icon", icon_name="copy")
    xx += ww + 10
    value_pill(cv, (xx, y + 1), "12 px radius", th)

    # ── 5 · token map ──
    y5 = y4 + 254
    y = _panel(cv, (x0, y5, x1, y1), "05 · WHERE EVERY VALUE LIVES",
               "The sheets render FROM these files — change a token, re-run, the "
               "sheet follows.")
    maps = [("SettingsCardFaceBrush", "Themes/Light|DarkTheme.xaml"),
            ("SettingsCardEdgeBrush", "Themes/Light|DarkTheme.xaml"),
            ("SettingsInnerFace/Edge", "Themes/Light|DarkTheme.xaml"),
            ("SettingsCardShadow", "Themes/Light|DarkTheme.xaml"),
            ("Ios26Radius*", "Themes/Styles/AboutSurface.xaml"),
            ("Ios26FontSize*", "Themes/Styles/AboutTypeScale.xaml"),
            ("Settings* spacing", "Views/Settings/SettingsCardStyles.xaml"),
            ("stage + orbs", "Themes/Styles/AppStage.xaml"),
            ("accent palette", "Services/Theme/AccentPresets.cs"),
            ("rail rows", "SettingsViewModel.Navigation.cs"),
            ("every label", "Themes/Languages/en-US.xaml")]
    for i, (k, v) in enumerate(maps):
        yy = y + i * 16
        text(cv, (x0 + 18, yy), k, Theme("dark"), "small", color=BOARD_TEXT, size=9.6)
        text(cv, (x1 - 18, yy), v, Theme("dark"), "label", color=BOARD_MUTED,
             anchor="rt", size=9.0)


def _win_caption(cv, x, y, label):
    text(cv, (x, y), label, Theme("dark"), "caption", color=BOARD_MUTED,
         size=9.5, weight="semibold", tracking=0.6)


# ═══════════════════════════════════════════════════════════════════════════
# the ten sheets
# ═══════════════════════════════════════════════════════════════════════════
def sheet_01():
    s = Sheet("01", "Crystal kit & shell anatomy",
              "The Settings shell on the invariant About stage · the material, "
              "radii, spacing, type ladder and control grammar every page composes",
              2160, 100, badges=[])
    th = Theme("light")
    wh = measure_window_h(page_appearance_theme, th, True, 1440)
    s.size(2160, 100 + wh + 96,
           badges=[("LIGHT", "theme"), ("SHELL + APPEARANCE", "page"),
                   (f"2× · 4320×{(100 + wh + 96) * 2}", "scale")])
    s.window((40, 100, 1440, 100 + wh), th, "Appearance", rail=True,
             content=page_appearance_theme)
    _spec_column(s.cv, 1480, 100, 2120, 100 + wh, th)
    s.footer(["RENDERED FROM: SettingsCardFaceBrush / SettingsCardEdgeBrush / "
              "SettingsInnerFaceBrush / SettingsInnerEdgeBrush / SettingsCardShadow "
              "(Themes/LightTheme.xaml) · Ios26Radius* (AboutSurface.xaml) · "
              "Ios26FontSize* (AboutTypeScale.xaml) · Settings* spacing (SettingsCardStyles.xaml)",
              "SHELL: rail 264 px (SettingsView.xaml) · grab tab 34 · header strip "
              "Margin 18,14,18,8 Padding 14,12 · sections Margin 18,14,18,18 · card gap 22 · "
              "stage = AppBackgroundBrush (invariant, both themes)"])
    return s.save(os.path.join(OUT, "01-crystal-kit-shell.png"))


def _single(number, title, sub, page_key, theme, content, badges_extra=None,
            rail=True, fname=None):
    th = Theme(theme)
    wh = measure_window_h(content, th, rail, 1600)
    sh = 100 + wh + 96
    s = Sheet(number, title, sub, 1680, 100,
              badges=[(theme.upper(), "theme"), (page_key.upper(), "page"),
                      (f"2× · 3360×{sh * 2}", "scale")] + (badges_extra or []))
    s.size(1680, sh,
           badges=[(theme.upper(), "theme"), (page_key.upper(), "page"),
                   (f"2× · 3360×{sh * 2}", "scale")] + (badges_extra or []))
    s.window((40, 100, 1640, 100 + wh), th, page_key, rail=rail, content=content)
    return s


def sheet_02():
    s = _single("02", "Appearance · Theme & Layout",
                "Mode, visual family, card corner radius, density, accent palette, "
                "font and wallpapers — every control on the crystal grammar",
                "Appearance", "light", page_appearance_theme)
    s.footer(["PAGE CARDS: SettingsPageCardStyle — L2 frosted base + L3 rim + L4 sheen + "
              "L5 lip over the invariant stage · L1 tri-stop edge · L0 tinted shadow "
              "(blur 34 · depth 14 · #334155 @ 18%)",
              "ROWS: SettingsIconTileStyle disc 34/10 · SettingsRowTitle 14 SemiBold · "
              "SettingsRowMeta 11 · ModernToggleSwitch 62×36 · ModernSliderStyle 40 px "
              "fill-bar · segment = SettingsSegmentTrackStyle (ListBox, UniformGrid)"])
    return s.save(os.path.join(OUT, "02-appearance-theme-light.png"))


def sheet_03():
    s = _single("03", "Appearance · Typography, Density & Card Geometry",
                "The whole type ladder at its token sizes, the radius authority and "
                "the four density profiles — the two cards this sheet set owed",
                "Appearance", "light", page_appearance_type)
    s.footer(["TYPE: AboutTypeScale.xaml — 10…30 px whole pixels, three weights "
              "(Regular / SemiBold / Bold); NEVER Bold below 12 px, NEVER SemiBold on a "
              "17 px title (AboutModuleAuditTests locks both)",
              "GEOMETRY: radii publish from ThemeManager.ApplyCardCornerRadius "
              "(default 12) at ratios 26/12 · 12/12 · 10/12 · 12/12 · 6/12 — the "
              "Appearance slider moves Settings AND About together"])
    return s.save(os.path.join(OUT, "03-appearance-type-geometry-light.png"))


def sheet_04():
    s = _single("04", "Themes & Wallpaper",
                "Visual families as preset tiles, the six live Crystal Fluid axes and "
                "the wallpaper scene grid with its selection ring",
                "Themes", "light", page_themes_wallpaper)
    s.footer(["FAMILIES: VisualThemeEngine — Industrial (Studio) · Monet (Golden Hour) · "
              "Crystal Fluid (iPhone 17 Pro Max, light-only) · iOS 26; picking a family "
              "runs the full ThemePipeline rebuild",
              "SELECTION: chosen tile = SettingsInfoTileStyle wash + 2 px accent ring; "
              "scenes come from BuiltinWallpaperScenes + the per-module override list"])
    return s.save(os.path.join(OUT, "04-themes-wallpaper-light.png"))


def sheet_05():
    s = _single("05", "AI Assistant",
                "Master switches and tuning, the provider matrix with encrypted key "
                "pills, and the security card — the same grammar in Dark",
                "AI", "dark", page_ai)
    s.footer(["DARK MATERIAL: the five Settings surface keys are theme-owned "
              "(#CC1B1D24→#9E151720 face · edge 34/12/22% white · shadow #020617 @ 34% "
              "blur 40 depth 18) so Light/Dark repaints the whole module",
              "KEYS: read-only values are SettingsValuePillStyle (radius 6) with a copy "
              "icon button; state is a chip (green / muted / danger), never coloured text"])
    return s.save(os.path.join(OUT, "05-ai-dark.png"))


def _double(number, title, sub, a, b, fname):
    """Two stacked windows: (page_key, theme, content, caption) each."""
    (ka, ta, ca, lupa) = a
    (kb, tb, cb, lupb) = b
    ha = measure_window_h(ca, Theme(ta), False, 1600)
    hb = measure_window_h(cb, Theme(tb), False, 1600)
    sh = 104 + ha + 32 + hb + 96
    s = Sheet(number, title, sub, 1680, 100,
              badges=[(f"{ta.upper()} + {tb.upper()}", "theme"),
                      (f"{ka.upper()} · {kb.upper()}", "page"),
                      (f"2× · 3360×{sh * 2}", "scale")])
    s.size(1680, sh,
           badges=[(f"{ta.upper()} + {tb.upper()}", "theme"),
                   (f"{ka.upper()} · {kb.upper()}", "page"),
                   (f"2× · 3360×{sh * 2}", "scale")])
    _win_caption(s.cv, 40, 88, lupa)
    s.window((40, 104, 1640, 104 + ha), Theme(ta), ka, rail=False, content=ca)
    _win_caption(s.cv, 40, 104 + ha + 16, lupb)
    s.window((40, 104 + ha + 32, 1640, 104 + ha + 32 + hb), Theme(tb), kb,
             rail=False, content=cb)
    s.footer([f"SHEET {number} · {title} — rail collapsed (the 34 px grab tab stays), so each "
              "page shows at full module width exactly as the drawer behaviour leaves it",
              "Both windows sit on the SAME invariant stage (AppBackgroundBrush is "
              "byte-identical in LightTheme.xaml and DarkTheme.xaml — AppStageAuditTests); "
              "only the card material is theme-owned"])
    return s.save(os.path.join(OUT, fname))


def sheet_06():
    return _double("06", "Voice · AI & Agent",
                   "Whisper model picker with live install state, then the agent's "
                   "privacy, memory and capability grammar",
                   ("Voice", "light", page_voice, "VOICE — LIGHT · WhisperModelOptions from WhisperModelManager"),
                   ("Agent", "dark", page_agent, "AI & AGENT — DARK · privacy, memory & knowledge, capabilities"),
                   "06-voice-agent.png")


def sheet_07():
    return _double("07", "Data & Backup · Demo Center",
                   "Shared-folder data root, backup vault with storage bar and restore "
                   "points, database health; then the recording studio",
                   ("Data", "dark", page_data, "DATA & BACKUP — DARK · backup sets, vault, health probes"),
                   ("Demo", "dark", page_demo, "DEMO CENTER — DARK · capture, workflow, isolation, history"),
                   "07-data-demo-dark.png")


def sheet_08():
    return _double("08", "Updates · License",
                   "Running vs published release with channel and history; the license "
                   "status card as label/value rows with a state chip",
                   ("Updates", "light", page_updates, "UPDATES — LIGHT · overview metrics, release notes, history"),
                   ("License", "light", page_license, "LICENSE — LIGHT · status label/value card + activation"),
                   "08-updates-license-light.png")


def sheet_09():
    return _double("09", "Features · Sharing",
                   "The 119-border flag page on one grammar — per-module cards with "
                   "switch grids — then the sharing providers and templates",
                   ("Features", "light", page_features, "FEATURES — LIGHT · session + five module flag cards"),
                   ("Sharing", "light", page_sharing, "SHARING — LIGHT · providers, defaults, message templates"),
                   "09-features-sharing-light.png")


def sheet_10():
    return _double("10", "About & Help · Light / Dark parity",
                   "The module the grammar came from, beside the same Appearance page "
                   "in Dark — proof the material is theme-owned, not two designs",
                   ("About", "light", page_about, "ABOUT & HELP — LIGHT · the reference card language"),
                   ("Appearance", "dark", page_appearance_theme,
                    "APPEARANCE — DARK · same page, same tokens, theme-owned material"),
                   "10-about-parity.png")


PLAIN_PAGES = [
    ("01", "appearance", "light", "Appearance", page_appearance_theme),
    ("02", "appearance-type", "light", "Appearance", page_appearance_type),
    ("03", "themes-wallpaper", "light", "Themes", page_themes_wallpaper),
    ("04", "ai-assistant", "dark", "AI", page_ai),
    ("05", "ai-agent", "dark", "Agent", page_agent),
    ("06", "voice", "light", "Voice", page_voice),
    ("07", "updates", "light", "Updates", page_updates),
    ("08", "data-backup", "dark", "Data", page_data),
    ("09", "demo-center", "dark", "Demo", page_demo),
    ("10", "license", "light", "License", page_license),
    ("11", "features", "light", "Features", page_features),
    ("12", "sharing", "light", "Sharing", page_sharing),
    ("13", "about-help", "light", "About", page_about),
]


def sheet_plain(num: str, slug: str, theme: str, page_key: str, content) -> str:
    """One clean window, nothing around it: the page exactly as shipped."""
    th = Theme(theme)
    wh = measure_window_h(content, th, True, 1600)
    s = Sheet(num, "", "", 1680, wh + 80, plain=True)
    s.cv.img.paste(Image.new("RGBA", (int(1680 * 2), int((wh + 80) * 2)),
                             rgba((233, 236, 240, 255))), (0, 0))
    s.window((40, 40, 1640, 40 + wh), th, page_key, rail=True, content=content)
    out = os.path.join(OUT, "plain", f"{num}-{slug}-{theme}.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    return s.save(out)


SHEETS = [("01", sheet_01), ("02", sheet_02), ("03", sheet_03), ("04", sheet_04),
          ("05", sheet_05), ("06", sheet_06), ("07", sheet_07), ("08", sheet_08),
          ("09", sheet_09), ("10", sheet_10)]


def main(argv):
    want = [a for a in argv if a.strip()]
    os.makedirs(OUT, exist_ok=True)
    if "plain" in want:
        for num, slug, theme, key, content in PLAIN_PAGES:
            t0 = time.time()
            path = sheet_plain(num, slug, theme, key, content)
            print(f"  plain {num}  {os.path.basename(path):38} "
                  f"{os.path.getsize(path) // 1024:5} KB  {time.time() - t0:5.1f}s")
        print(f"\n  → {os.path.join(OUT, 'plain')}")
        return 0
    for num, fn in SHEETS:
        if want and num not in want:
            continue
        t0 = time.time()
        path = fn()
        print(f"  sheet {num}  {os.path.basename(path):42} "
              f"{os.path.getsize(path) // 1024:5} KB  {time.time() - t0:5.1f}s")
    print(f"\n  → {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

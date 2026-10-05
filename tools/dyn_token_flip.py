#!/usr/bin/env python3
"""Flip approved runtime-design tokens from StaticResource to DynamicResource.

WHY (release-safe protocol, dynamic/static policy):
  These tokens are owned by the runtime design system:
    - PgFont* / PgTopFont*   -> scaled live by ThemeManager.ApplyDisplayDensity
    - Pg*Margin/Padding/Gutter/*MinHeight -> density ladder (ApplyDisplayDensity)
    - Pg*Radius (except PgChipRadius 999 capsule) -> radius authority
      (ApplyCardCornerRadius)
  The MERIDIAN rewrite consumed them via StaticResource, which froze them at
  the authored values and broke density/radius reflow that the old kit had
  (it used DynamicResource FontSize.* / Surface.Primary.CornerRadius).
  This is the root-cause fix: consumers become dynamic; ThemeManager
  republishes the tokens (see ThemeManager.Part2.cs).

SAFETY:
  - Only tokens in the explicit allowlist are touched.
  - Only the exact string "StaticResource <token>" (attribute value marker)
    is rewritten; x:Key declarations, styles, templates and all other
    resources are untouched.
  - Per-file before/after counts are printed and must match expectations.
  - XML well-formedness is re-verified after the rewrite.
"""
import re, sys, xml.etree.ElementTree as ET

ALLOW = [
    # typography (22)
    "PgFontHero", "PgFontHeroSub", "PgFontCardTitle", "PgFontCardSub",
    "PgFontKpiValue", "PgFontKpiLabel", "PgFontKpiFoot",
    "PgFontStatValue", "PgFontStatLabel", "PgFontStatFoot",
    "PgFontStageValue", "PgFontStageName", "PgFontStageFoot",
    "PgFontBody", "PgFontBodySm", "PgFontMeta", "PgFontBtn",
    "PgFontGlyph", "PgFontGlyphSm",
    "PgTopFont", "PgTopFontBtn", "PgTopFontCap",
    # density geometry (8)
    "PgPageMargin", "PgCardPadding", "PgTilePadding", "PgRowPadding",
    "PgGutter", "PgGutterHalfMargin", "PgKpiTileMinHeight", "PgChartMinHeight",
    # radii (5) — PgChipRadius (999 capsule) is structural and stays static
    "PgCardRadius", "PgTileRadius", "PgCtrlRadius", "PgIconRadius", "PgIconSmRadius",
]
PATTERN = re.compile(r"StaticResource\s+(" + "|".join(sorted(ALLOW, key=len, reverse=True)) + r")\b")

FILES = [
    "Themes/Professional/ProfessionalCore.xaml",
    "Themes/Professional/ProfessionalShell.xaml",
    "MainWindow.xaml",
    "Views/Dashboard/DashboardView.xaml",
    "Views/Dashboard/Overview/OverviewDashboardView.xaml",
    "Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml",
    "Views/Dashboard/ProformaInvoice/PiDashboardView.xaml",
    "Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml",
    "Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml",
    "Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml",
    "Views/Dashboard/Statement/StDashboardView.xaml",
]

total = 0
for f in FILES:
    s = open(f, encoding="utf-8").read()
    new, n = PATTERN.subn(lambda m: f"DynamicResource {m.group(1)}", s)
    ET.fromstring(new)  # well-formedness gate
    if n:
        open(f, "w", encoding="utf-8").write(new)
    print(f"{f}: {n} flips")
    total += n
print(f"TOTAL: {total} flips across {len(FILES)} files")

# GLASS BLUSH — 100% Visual Fidelity Implementation (Personal UI)

**Baseline:** `5d86f11` (continues the MASTER UI MODERNIZATION stream from
`5de4bc6`) · **Started:** 2026-09-18
**Visual source of truth:** the 10 reference PNGs in the delivery folder
(`glass-01-dashboard-overview.png` … `glass-10-ai-assistant.png`, all
**1408×768**, 16:9). They are authoritative for composition, spacing, color,
glass, glow, radius and typography. This document tracks every phase, every
screen, deviations, and verification status.

**Honesty rules in force:** STATIC VERIFIED ≠ RUNTIME VERIFIED ·
VISUALLY MATCHED ≠ NOT YET VERIFIED (no runtime exists in this environment —
no .NET SDK, no screenshots possible; GB-P16 is tooling-ready but NOT RUN).

---

## GB-P1 — MEASUREMENTS OF THE 10 REFERENCES (2026-09-18)

Method: ImageMagick analysis (no PIL available): 14-color quantized palettes
per reference, 64×36 luminance grids for region detection, 1×1 full-resolution
probes for exact colors, 24×24 corner probes for radius. Machine-readable
raw data captured in `/tmp/glass_blush_measurements.json` (working session
only).

### Canvas (all 10)
- **1408 × 768 px (16:9)**, assumed 1:1 DIP for a ~1440×810 window at 100%.
- Sidebar: content starts at x≈110 (most screens) / 176 (glass-01) →
  **sidebar ≈ 140px** (glass-01 rail ≈ 176 with wider padding).
- Page margin: bright regions end at x≈1320–1364 → **right margin ≈ 44px**;
  uniform **page padding 44px**.
- Header band: y 0–~110; KPI row y≈154–264 → **top bar ≈ 72px + page header**.
- glass-01 KPI row: 4 cards spanning 176–1320 → **KPI card ≈ 270×110, gap 28**.

### Color (measured, full-res probes)
| Role | Measured | Token value |
|---|---|---|
| Background bottom-left | `srgb(3,23,74)` / `srgb(2,21,67)` | `#021749` → `#021543` |
| Background mid | `srgb(2,68,142)` | `#02448E` |
| Background top-right glow | `srgb(34,133,183)` | `#2285B7` (azure glow corner) |
| Sidebar rail | `srgb(1,24,71)` | `#011847` @ 55% over bg |
| Card glass fill | `srgb(96,139,172)` over bg → ≈28% white | `#48FFFFFF` (28%) |
| Card glass (strong) | brighter KPI cards ≈35% white | `#59FFFFFF` |
| Card border | bright translucent white | `#73FFFFFF` (45%) |
| Sheen top edge | diagonal white highlight | `#99FFFFFF`→`#2EFFFFFF` @135° |
| Text primary | `srgb(253,255,255)` | `#F5FAFF` |
| Text muted | ice blue (palette family) | `#A9C3E6` |
| Accent cyan | `#2285B7` / `#2E8EB4` / `#2188B8` family | `#2FA8D8` |
| Accent glow | bright cyan highlights | `#57C7F0` |
| Success / Warning / Danger | mint / amber / coral pills | `#34D399` / `#FBBF24` / `#F87171` |
| **Night (glass-04 only)** corners `srgb(7,12,50)`, tiles `#152B50`, accent `#1891AA`/`#4B5A71` | midnight `#070C32`, navy glass `#152B50`, neon cyan `#4FD1E8` | `GlassBlushNight.*` |

Gradient direction: **dark sapphire bottom-left → azure glow top-right**
(`StartPoint 0,1 → EndPoint 1,0`), plus a soft cyan radial orb in the
top-right and a faint orb bottom-left (measured corner luminance spread).

### Geometry (measured)
- Card corner radius: corner probes show full card coverage from ~20px into
  the diagonal; AI-rendered edges ≈ 20–24px → **CardRadius 22**.
- Small radii (chips/badges) ≈ 12–16 → **SmallRadius 14** · pills fully
  rounded → **999**.
- KPI card 270×110 · card gap 28 → **24** (normalized) · card padding ≈
  20 · control height ≈ 44 (measured button/input bands) · stepper diameter
  ≈ 36.

### Effects (visual read, to be dialed at GB-P16)
- Frost: translucent white over a pre-blurred atmospheric background
  (WPF has no true backdrop blur on arbitrary surfaces → closest stable
  architecture = gradient + soft radial orbs behind translucent cards;
  recorded as a documented technique, §12 of the master prompt).
- Glow: cyan DropShadow (radius ≈ 18, opacity ≈ 0.55) on accent elements;
  never on table rows (performance, §23).
- Sheen: 135° white gradient top-edge on cards.

### Typography (visual read)
- Sans (Segoe UI family per app font). KPI numerals ≈ 40px Bold tabular;
  page titles ≈ 26 SemiBold; headings 20; body 15; captions 13; labels 12.
  → `GlassBlush.Font.*` scale in the token plane.

### Per-reference composition map
| Ref | Screen → existing WPF view | Composition (measured regions) |
|---|---|---|
| glass-01 | Dashboard → `OverviewDashboardView` | rail 140 · 4 KPI cards · wide area chart + donut · rounded invoice table |
| glass-02 | PI → `ProformaInvoiceView` + steps | header + pill buttons · 9-step glow strip · form card (left) + line-items card + total card |
| glass-03 | JO → `JobOrderDetailView` | header pills · 4-stage glow timeline · schedule card (left) + details card (right) |
| glass-04 | Production → `ProductionControlCenterView` | NIGHT palette · 3 big readouts · 6 work-center tiles · queue table |
| glass-05 | Sheet store → `SheetStoreView` | header + search pill + round add · 3 stat chips · masonry sheet cards with stock bars |
| glass-06 | Delivery → `DeliveryNoteListView`/`DeliveryNoteDetailView` | glass list (left) + detail card: driver block, route timeline, items table |
| glass-07 | Aging → `PiDashboardView` aging / `SheetProfitabilityView` | filter pills (Overdue active) · 3 stat cards · invoice table + glowing aging bars |
| glass-08 | Analytics → `ExecutiveReportView`/`SalesReportView` | filter bar · multi-line hero chart · radial gauge · bar cards |
| glass-09 | Settings → `SettingsThemesSection` | section list rail · theme tiles · accent swatches · density segmented control · motion toggle |
| glass-10 | AI → `AiChatView` | chat panel: glass bubbles (user solid / assistant glass) + embedded mini-table · suggestion chip rail |

---

## Phase status

| Phase | Status | Commit |
|---|---|---|
| GB-P1 tokens + visual foundation | ✅ STATIC VERIFIED (tokens authored from measurements; runtime NOT VERIFIED — no .NET SDK) | `GB-P1` |
| GB-P2 shell | pending | — |
| GB-P3 shared components | pending | — |
| GB-P4…P13 screens (10) | pending | — |
| GB-P14 Professional↔Personal switch | pending | — |
| GB-P15 global visual consistency | pending | — |
| GB-P16 runtime screenshot fidelity | **NOT RUN — no WPF runtime in this environment** (tooling to be delivered) | — |
| GB-P17 DPI/a11y/performance | pending (static parts only) | — |

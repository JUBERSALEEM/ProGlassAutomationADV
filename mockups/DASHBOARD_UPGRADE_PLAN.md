# Dashboard Professional Redesign — Upgrade Plan (no rewrite)

**Companion to:** `mockups/dashboard-mockup.html` (open in any browser — Light/Dark toggle, token inspector)

**Claim:** the entire mockup can be implemented as **token swaps + one shared style file**,
with **zero view-structure edits, zero ViewModel edits, zero code changes**.

---

## 1. Why the dashboards currently read "not professional"

Verified from the source, not impression:

| # | Issue | Evidence in code |
|---|-------|------------------|
| 1 | **Three overlapping design languages.** `Dash*` compact system, `Soft*` reference system (radius 18–22, 28–30px type), and the `Industrial*` pass (extra shadows) coexist; modules pick different mixes. | `Styles/DashboardStyles.xaml` (3 style families in one file); `Statement` uses `SoftKpiCardTemplate` while the rest use `DbKpiTileTemplate` |
| 2 | **Colour-coding is decorative.** Every KPI tile carries a coloured icon chip (Green/Blue/Amber/Red), so red no longer means "alarm" — an overdue job and a healthy total look equally loud. | `OverviewDashboardViewModel` sets `Accent="Green"` on all 4 sales tiles, `"Blue"` on all production tiles, `"Amber"` on delivery tiles |
| 3 | **Warm cream background + glass surfaces** (`WindowBackgroundColor #FBF8F1`, `GlassSurface*`, CrystalFluid tints) suit the shell, not dense data screens. | `Themes/LightTheme.xaml`; `SoftTileStyle` uses `GlassUltraClearBrush` |
| 4 | **Type scale drift.** History: 14 sizes; today: `Dash*` 6-step (24…9) **plus** `Soft*` 8-step (30…11) plus `Db*` sizes in `DashboardKit.xaml` — 9px captions beside 30px heroes. | `DashboardStyles.xaml` header comment documents the original 14-size problem; `SoftFont*` tokens |
| 5 | **Inconsistent depth.** Shadows on some cards (`ShadowSoft`/`ShadowElevated`), none on others; also a known GPU cost the team already flagged. | `SoftCardStyle.Effect`, `IndustrialDashboardCardStyle.Effect`, the PERF-3 comment |
| 6 | **Two refresh indicators, duplicated hero text** — the command bar chip and the hero both show last-refresh; the subtitle sits in the bar AND the hero. | `DashboardView.xaml` row 0 + `OverviewDashboardView.xaml` hero strip |

None of this is a structure problem. It is a **vocabulary** problem — which is exactly the part WPF
makes trivially swappable.

## 2. Why this is 100% upgradeable (the key finding)

All seven module dashboards already consume the **same `Db*` design kit**:

```
Views/Dashboard/Overview/…            23× DbCardStyle, 4× DbKpiTileTemplate, DbPipelineStageTemplate
Views/Dashboard/ProformaInvoice/…     8× DbCardStyle, …
Views/Dashboard/SheetStore/…          7× DbCardStyle, …
Views/Dashboard/JobOrder/…            9× DbCardStyle, DbRowCardStyle, DbTileStyle …
Views/Dashboard/DeliveryNote/…        9× DbCardStyle, …
Views/Dashboard/TaxInvoice/…          12× DbCardStyle, …
Views/Dashboard/Statement/…           8× DbCardStyle, +2 legacy SoftKpiCardTemplate
```

Every colour in the kit resolves through `DynamicResource` (the codebase's own documented rule —
`StaticResource` would freeze a card across the runtime Light/Dark swap). So:

> **Rebuild the kit + tokens → all 7 dashboards restyle simultaneously, with no other file
> needing a structural change.**

The mockup's every element maps to a key that **already exists**:

| Mockup element | Existing WPF key | Change |
|---|---|---|
| Card | `DbCardStyle` (← `SurfacePrimaryStyle`) | token swap only |
| Inner tile | `DbTileStyle` (← `SurfaceDetailStyle`) | token swap only |
| Chart well | `DbChartWellStyle` | token swap only |
| KPI tile | `DbKpiTileTemplate` | recolour: neutral chip, rail only on `IsAlert` |
| Pipeline stage | `DbPipelineStageTemplate` | status ring instead of tint |
| Feed row | `DbFeedRowTemplate` | hairline dividers, neutral icon chip |
| Action buttons | `DbActionButtonStyle` / `DbActionButtonAccentStyle` | radius/height via tokens |
| Chips (Live, refresh, user) | `DbChipStyle` | token swap only |
| Radii / gutters | `CardCornerRadius`, `DbGutter`, `LayoutTokens.xaml` | token swap only |
| Type scale | `Db*` font tokens | trim to 6 steps |

## 3. Phased implementation (each phase = one commit, fully reversible)

### Phase A — Colour & geometry tokens (≈ 1 day, lowest risk)
**Files:** `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml`, `Themes/Styles/LayoutTokens.xaml`

1. Add one new page token used by dashboards only — do NOT touch the shell's warm background:
   - `DashboardPageBrush`: `#F4F6F9` (light) / `#0B111A` (dark)
2. Verify/rename targets the mockup uses (all already exist): `ElevatedSurfaceBrush`,
   `DetailSurfaceBrush`, `BorderBrush`, `PrimaryForegroundBrush`, `SecondaryForegroundBrush`,
   `MutedForegroundBrush`, `AccentBrush` `#0F6FA8` (kept — it is already a professional
   industrial blue), `Success/Warning/DangerBrush`, tint brushes.
3. Geometry: `CardCornerRadius` → 10, tile 8, keep `DbGutter` 14.
4. Shadow: drop `Effect` from `SoftCardStyle`/`IndustrialDashboardCardStyle` (perf win the
   team already documented) — depth now comes from hairline + surface step.

**Result:** every module's cards, wells, borders, radii and the page background change.
**Revert:** `git revert` of one commit. **Nothing else can break — no markup was touched.**

### Phase B — The `Db*` kit (≈ 1–2 days, the visible "redesign")
**File:** `Styles/DashboardKit.xaml` only.

1. **Type scale:** set `DbTitleStyle` 18/600, card titles 13/600, `DbKpiValueStyle` 26/700
   tabular (keep `Typography.NumeralAlignment=Tabular` — already there), labels 11/600
   uppercase, captions 11. Retire 9px.
2. **KPI tile (`DbKpiTileTemplate`):** icon chip → neutral (`SurfaceDetailStyle`, muted glyph)
   by default; the 3px rail and red glyph appear **only** when `IsAlert` (DataTrigger already
   exists in `DashAccentRailStyle` — copy the pattern).
3. **Pipeline (`DbPipelineStageTemplate`):** status state = tinted icon well + coloured foot
   text + connector colour; replace full-card tint.
4. **Feed rows (`DbFeedRowTemplate`):** hairline top border between rows, neutral icon chip,
   right-aligned timestamp.
5. **Buttons/chips:** 32px height, radius 8, pill for Live chip; single accent button per hero.
6. **De-dup hero:** keep the refresh chip in the command bar; remove the duplicated refresh
   text from the Overview hero (1-line view edit — the only structural change in Phases A–B).

**Result:** the mockup, on all seven modules at once.
**Revert:** one file. The old kit remains in git history; benchmark pins in
`Tests/Agent/*Dashboard*` (if any assert on style keys) are updated in the same commit.

### Phase C — Optional polish (per module, each independently shippable)
1. Statement: swap its 2 legacy `SoftKpiCardTemplate` references to `DbKpiTileTemplate`
   (same `KpiCardModel` data, template-only change) — removes the last off-kit surface.
2. Retire unused `Soft*`/`Industrial*` keys after a usage grep shows zero consumers
   (keeps the design system honest — one language per the kit's own header comment).
3. Delivery band: 5 tiles is unbalanced — merge "Delivered Today" into "Total Delivered"
   as a sub-line ("+120 today"). **One ViewModel line** (join two `KpiTileModel`s),
   the only data-model change in the whole plan, and it is optional.
4. Per-module grid tweaks (column fractions) if a module's content density wants it —
   cosmetic, per-file, reviewable one at a time.

## 4. What deliberately does NOT change

- **No ViewModel rework** — same `KpiTileModel` / `WorkflowStageModel` / `ActivityFeedItem`.
- **No navigation change** — segmented picker stays (it is already the right control).
- **No i18n change** — every label stays a `DynamicResource` string key (en-US + 9 languages).
- **No data change** — same queries, same refresh cadence, same commands.
- **Light/Dark runtime swap preserved** — every new value is a `DynamicResource` token.
- **Shell chrome untouched** — the app's navigation rail and window stay as they are;
  dashboards get their own cool-neutral page background (Phase A token), which is what
  separates "data" from "chrome".

## 5. Risk assessment

| Phase | Files touched | Blast radius | Rollback |
|---|---|---|---|
| A | 3 theme dictionaries | visual only | 1 commit |
| B | 1 style file (+1 hero line) | visual only, 7 modules | 1 commit |
| C | 1 template ref / 1 VM line each | per-module | per-commit |

No phase touches: parsing, commands, services, persistence, the optimization engine,
or any non-dashboard view (the Tax Invoice detail screens keep their own `CardStyle` —
that key is deliberately NOT redefined, per the existing scope note in `DashboardStyles.xaml`).

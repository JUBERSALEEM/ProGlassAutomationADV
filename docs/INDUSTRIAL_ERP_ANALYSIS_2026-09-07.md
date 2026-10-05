# Senior Industrial-ERP Analysis — Appearance, Branding & Theme Round
**Date:** 2026-09-07 · **Branch:** `arena/01a076b1-proglassautomationadv` · **Commit:** `dde0f24`

Scope of the round: per-module wallpaper toggle latency, defined card edges
(1–100 + colour plate), water-fill leak outside Ios-17pm, PDF logo layout
(LeftPortrait / TopPanel), a professional logo section with size controls,
click-to-load saved-company cards, and a senior review instead of blindly
applying each request.

This document is the review. It states what was **implemented**, what was
**deliberately not applied as-is** (and why), what is still **missing**, and
the **prioritised recommendations** an industrial ERP lead would raise
before shipping.

---

## 1. Requirement-by-requirement verdict

### 1.1 Per-module wallpaper toggle must not be laggy
**Implemented.** `Services/Theme/VisualThemeEngine.Part1.cs` now keeps a
family-baseline signature (`_familyBaselineSignature`, plus cached
`_familyWindowBrush` / `_familyModuleBrush`). `ApplyModule` compares the
incoming scene against that baseline: when only the per-module wallpaper
(Enabled / Path / Opacity) changed, it **restores the cached family surfaces
and only re-layers the wallpaper**, instead of the old behaviour that tore
down and republished ~60 keys per tick.

**Not applied blindly** — two correctness decisions:
- The **system wallpaper is still a family input** (`BuildFamilySignature`
  includes `WallpaperPath` + `WallpaperOpacity`), because Monet derives its
  window stage directly from it. A system-wallpaper change still triggers a
  real rebuild. Only the *per-module* wallpaper gets the fast path.
- The cached signature is rebuilt from `Current` (the family actually
  painted), so it can never drift from what is on screen.

**Remaining cost (missed item).** `ModuleWallpaperRow.OpacityValue` still
calls `VisualThemeEngine.ApplyModule(...)` on **every slider tick**, and the
wallpaper branch calls `BuildWallpaperBrush` each time — a synchronous
`BitmapImage` decode + `Freeze()` from disk. The teardown is gone, but the
image is still re-decoded per tick, and `AdaptTypographyForWallpaper` /
`ApplyFontPreference` / `GuaranteeCriticalResources` still run per tick.
See §3.1 for the recommended fix.

### 1.2 Defined card edges with 1–100 thickness + colour plate
**Implemented.** New `CardEdgeThickness` token (Light/Dark themes, default 2),
a full-row "Defined card edges" tile in
`Views/Settings/Sections/SettingsAppearanceSection.xaml` (on/off switch,
1–100 slider, `CardEdgePalette` AccentPresets chips, custom hex), VM surface
in `ViewModels/Settings/SettingsViewModel.Appearance.cs`, startup restore in
`App.StartupCore.cs`, and re-apply on theme swap in `ThemeManager.Part1.cs`.
All **card surface** styles now bind `CardEdgeThickness`; inputs/chrome keep
the subtle `CardBorderThickness`.

**Design judgement applied:** the edge weight is split from
`CardBorderThickness` so a bold industrial edge (up to 100 DIP) can never
inflate text boxes, gridlines and buttons. This is the correct industrial
separation — one token for *chrome* (hairline), one for *card envelope*.

**Missed items:**
- **No padding compensation.** At 100 DIP the border draws inward over the
  card's padding/content. Card styles have fixed padding (`DashCardPadding`,
  `SoftCardPadding`, 24/18/16 …) that does not scale with the edge. A slider
  that reaches 100 without layout compensation will clip content visually.
- **No "effective value" feedback for the plate.** The chips apply a colour
  but the current selection is not visually marked as active (the accent
  palette has the same behaviour, so this is consistent — but an ERP reviewer
  would want the active swatch highlighted).

### 1.3 Water fill must not survive outside Ios-17pm
**Implemented (root-cause fix).** `Controls/StatCard/StatCardControl.xaml.cs`
previously copied the gate into a **local** `WaterEnabled = TryFindResource(...)`
on load. A StatCard loaded while Ios-17pm was active kept `true` as a local
value forever, so switching to Normal / Monet / Industrial did not re-gate it.
It now uses `SetResourceReference(WaterEnabledProperty,
"CrystalWaterDefaultEnabled")` — a live DynamicResource that re-evaluates on
every family switch, exactly like the wrapped `CrystalWaterControl`.

**Verified gate chain:** `CrystalWaterDefaultEnabled = false` is published in
both the `Apply` and `ApplyModule` non-crystal branches
(`VisualThemeEngine.Part1.cs` ~350/522); `true` is published only in
`ApplyCrystalFluid` (`Part3.cs` ~213).

**Known intentional exception (not a leak):** `CrystalKpiCard` hard-codes
`WaterEnabled = true` in its constructor — but that control is only used by
the Ios-17pm control template and the Settings theme *preview*, so it never
renders water outside the crystal context. This should be documented as a
maintenance hazard (§3.4).

### 1.4 PDF logo — LeftPortrait left-aligned, TopPanel full-width panel
**Implemented.** `Services/Export/Pdf/Renderers/PdfChrome.Part1.cs`:
- **LeftPortrait** keeps the portrait logo on the **left** of the company
  identity, sized by `LogoWidthMm × LogoHeightMm` — unchanged semantics.
- **TopPanel** now renders a **full-width accent masthead band** across the
  top of the page (framed white logo chip + company identity + document
  title/reference), then the regular letterhead with its inline logo
  **suppressed** so the logo never renders twice.
- The logo chip (`LogoBox`) and masthead chip both gained a `0.5f` border on
  a white plate — the "stylish/professional" framing for logos with
  transparency.

**Missed items:**
- **TopPanel with no logo silently degrades.** If the operator selects
  TopPanel before uploading a logo, `Letterhead` falls through to the plain
  text letterhead — there is no masthead band and no hint. Recommend either
  rendering the identity masthead without the chip, or surfacing a
  "logo required for TopPanel" note in the editor.
- **Masthead width not bounded for the logo chip.** `LogoW` clamps to
  8–60 mm; at 60 mm the chip crowds the identity and the 200-unit document
  block. Cap the masthead chip width (e.g. ≤ 28 mm) independently of the
  printed portrait width.

### 1.5 Stylish/professional logo section with width/height
**Implemented.** `Views/CompanyCenter/CompanyCenterView.xaml` restyled the
logo/letterhead area: label bar, drop-shadow portrait preview, clearer
`WIDTH (MM)` / `HEIGHT (MM)` inputs, and the layout radios. The preview
reflects the saved aspect and the input clamps to 8–60 mm at save
(`SanitizedLogoWidthMm` / `SanitizedLogoHeightMm`).

**Missed item:** width/height are free-typed through `DoubleTypingConverter`
with **no inline range feedback**. The render clamps to 8–60 mm but the box
shows the raw value; a value like `600` is silently clamped at print time
rather than flagged at entry. Add a validation adornment or snap-on-lost-focus.

### 1.6 Click a saved-company card → load into the form
**Implemented.** The whole saved-company `Border` is now a click target
(`MouseBinding MouseAction="LeftClick"` → `SelectCompanyCommand` →
`BeginEdit(card)`) with a tooltip (`CompanyCenter.ClickToLoadCompany`). The
Edit/Delete buttons remain for discoverability.

**Missed items (important):**
- **No unsaved-changes guard.** `BeginEdit` overwrites every editor field
  without checking whether the operator has edits in progress. Clicking a
  card mid-edit **silently discards** unsaved changes. An industrial ERP must
  warn (or auto-save) before replacing a dirty form.
- **No keyboard access.** The card is a `Border` with only a mouse binding —
  not focusable, no Enter/Space activation, no `AutomationProperties.Name`.
  Accessibility and mouse-free operation (a stated ERP expectation) are not met.

---

## 2. Items deliberately *not* applied (and why)

- **"Thickness 100" applied literally with no layout work.** The slider range
  was honoured, but padding compensation was consciously deferred to this
  analysis instead of shipping a half-fix.
- **System wallpaper moved to the fast path.** Not done — it is a true family
  input for Monet; doing so would have introduced a colour-adaptive regress.
- **`EditorTitle` made settable to "fix" ERR-21172E.** Rejected again; the
  correct fix (`Mode=OneWay`) is in place and pinned by tests.

---

## 3. Prioritised recommendations

| # | Priority | Recommendation | Where |
|---|----------|----------------|-------|
| 1 | **High** | Cache the decoded, frozen wallpaper `BitmapImage` keyed by path (+last-write tick); rebuild only the cheap `ImageBrush` opacity wrapper per tick, and debounce the opacity slider's `ApplyModule` (~40 ms) with a flush on thumb release. | `BuildWallpaperBrush`, `ModuleWallpaperRow.OpacityValue` |
| 2 | **High** | Warn (or auto-save) before `BeginEdit` replaces a dirty company form; track a dirty flag on the editable fields. | `CompanyCenterViewModel.BeginEdit` |
| 3 | **High** | Make saved-company cards focusable with Enter/Space activation and an accessible name (`AutomationProperties.Name = CompanyName`). | `CompanyCenterView.xaml` card template |
| 4 | **Medium** | Scale card padding with edge weight (or clamp the visible effect) so a 100-DIP edge never overlaps content; highlight the active edge-colour swatch. | card styles + edge tile |
| 5 | **Medium** | Add range feedback for logo width/height (snap or adorn) so `600` mm is caught at entry, not silently clamped at print. | logo width/height inputs |
| 6 | **Medium** | TopPanel: render the identity masthead even without a logo (or show a "logo required" hint); cap masthead chip width. | `PdfChrome.TopPanelMasthead`, editor |
| 7 | **Low** | Document `CrystalKpiCard.WaterEnabled = true` as a crystal-only contract and add a source test that pins it to crystal usage so no future "leak" regression slips in. | `CrystalKpiCard`, `CrystalFluidControlsSourceTests` |
| 8 | **Low** | Skip `AdaptTypographyForWallpaper` / `ApplyFontPreference` on opacity-only ticks (contrast class does not change with opacity), and short-circuit `GuaranteeCriticalResources` when a pass already confirmed the keys. | `ApplyModule` fast path |

**Testing note.** None of the new behaviour is machine-verifiable in this
sandbox (WPF + QuestPDF cannot build here). The regression pins that exist
(`SettingsRedesignTests`, `LiquidGlassThemeSourceTests`) cover the token and
binding wiring only. Recommendation #1, #2 and #6 need real UI/PDF smoke
tests on a build host.

# Appearance / Theme Senior Audit — Glass, Monet, Settings & Global Typography
**Date:** 2026-08-29 · **Branch:** `arena/01a04c12-proglassautomationadv`

---

## 1. Compile errors (fixed)

`ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs` ended with a
corrupted paste: a stray duplicate fragment of `ChangeStatusAndSaveAsync` sat AFTER
the closing namespace brace (`}lid status change.";` …). That single corruption
produced every reported error as cascade:

| Reported error | Root cause |
|---|---|
| Newline in constant / Syntax error, ',' expected | the fragment's truncated string literal |
| Type or namespace definition, or end-of-file expected (×3) | code at file scope after the namespace closed |
| Top-level statements must precede namespace… | same stray fragment |
| The variable 'status' is declared but never used | Roslyn's recovery parse of the fragment |
| The type or namespace name 'lid' could not be found | `}lid …` parsed as an identifier |
| 'Status' / 'SaveAsync' / 'newStatus' do not exist in the current context | fragment landed outside the class, so all member references pointed at nothing |

**Fix:** removed the fragment, kept the single correct method definition (referenced by
the Issue/Paid commands at lines 621/625). Pushed as
`9788a47` *"Fix compile errors: remove stray duplicated fragment…"*.

---

## 2. Global typography — the "mixed / too small / not professional" problem

**Audit finding.** 1,762 `FontSize` attributes were hard-coded in XAML across **30+
different values** (8, 9, 9.5, 10, 10.5, 11, 11.5, 12, 12.5, 13, 13.5, 14 … 60).
1,237 of them were ≤ 12 DIP. Every view invented its own sizes, which is exactly the
"some text too small, normal mixed type, not professional" look.

**Fix — one canonical type scale.** New `Themes/Styles/TypeScaleTokens.xaml`, merged
second in `App.xaml` so every later dictionary and view can reference it:

| Token | Size | Replaced |
|---|---|---|
| `TextXSmall` | 11 | 8 – 9.5 |
| `TextCaption` | 12 | 10 – 10.5 |
| `TextBody` | 13 | 11 – 12 |
| `TextBodyLarge` | 14 | 12.5 – 13.5 |
| `TextSection` | 15 | 14 |
| `TextSubhead` | 16 | 15 |
| `TextTitle` | 18 | 16 – 18 |
| `TextTitleLarge` | 20 | 19 – 20 |
| `TextHeadline` | 22 | 21 – 22 |
| `TextDisplay` | 24 | 24 |
| `TextDisplayLarge` | 28 | 26 – 28 |

- **2,185 attributes remapped** across 140+ XAML files (both `FontSize="n"` and
  `FontSize" Value="n"` forms). No literal FontSize < 30 remains in any XAML file.
- Hero numerals (30–60) stay literal — they are deliberate display sizes.
- Density floors raised: Compact body 12→13, KPI 15→16; Comfortable body 13→14.
  Design-time defaults synced in `App.xaml` and `DesignTokens.xaml`.
- Every size went **up or stayed equal** (minimum +1, maximum +3 DIP) — nothing
  shrank, and the scale steps are uniform so the UI reads as one system.

Pushed as `a36a2cc` *"Global typography system…"*.

---

## 3. Glass themes — why "not as expected" and what changed

**Diagnosis.** `Glass`/`Glassy` were *translucency without blur*: cards had alpha
ramps (Glassy 0.18→0.05, Glass 0.78→0.94) floating over an **opaque** gradient stage.
Because nothing actually blurred behind them, the frost read as faint flat tint —
"settings not applied". iOS glass is fundamentally **backdrop blur + white tint +
specular edge**; WPF has no per-surface backdrop blur, so the effect must come from
the Desktop Window Manager.

**What was built (complete layering, top to bottom):**

1. **`Services/Theme/GlassChromeService.cs` (new)** — `SetWindowCompositionAttribute`
   accent policy: `ACCENT_ENABLE_ACRYLICBLURBEHIND` on Windows 10 1809+ / Windows 11,
   `ACCENT_ENABLE_BLURBEHIND` on Windows 10 1507+, graceful no-op below that. DWM
   degrades cleanly on RDP / with system transparency off (tint renders solid).
2. **Translucent stage veil** — with chrome active, `WindowBackgroundBrush` becomes a
   ~25–32% alpha veil (Glass: cool gradient; Glassy: the About stage + orbs at
   α=0x40), so the accent-tinted blurred desktop reads through the entire window.
   Without chrome the previous opaque stages are kept — the UI can never go
   invisible on unsupported systems.
3. **Apple-thin material alphas** — Glass surfaces drop to 0.58–0.78 (dark/light)
   when chrome is active, so cards genuinely frost over the blur; the old
   contrast-safe 0.78–0.94 values remain for the no-blur fallback.
4. **Soft top-light sheen** — the iOS "lit from above" identity is delivered at
   the brush level: the Glassy frost gradient now runs vertically (denser, brighter
   alpha at the top of every card fading downward) instead of diagonally, so every
   surface that consumes the glass keys — cards, DataGrid rows, hover, shell —
   carries the soft sheen automatically. (A crisp 1px specular overlay was
   attempted first via Style setters on `Border`, but `Border` has no `Template`
   property — MC4005/XDG0012/XLS0431 — so the brush-level sheen is the correct,
   template-free mechanism. See §9.)
5. **Accent-aware chrome tint** — the DWM backdrop tint mixes the theme base (deep
   slate / icy white) 28% toward the user accent; `SyncGlassChrome()` runs inside
   `Apply()`/`ApplyModule()`, and `MainWindow` calls `ReapplyWindowChrome()` after
   attaching, so theme swaps, accent changes and navigation stay in sync.
6. Existing pieces that now finally read correctly: hue-preserved status-tint glass
   (`TintIndigo/Blue/Green/Red/Amber/Purple/Gray`), the Glassy text palette
   (GlassSnow/Cerulean/Muted with contrast-safe light-theme substitution), and
   shell/sidebar/dashboard glass keys.

**Bug found & fixed in the same pass:** `OnAccentChanged` re-derived Monet and Glassy
but **not Glass**, so Glass surfaces kept the old accent tint after an accent change.

Pushed as `5ae8e6c` (acrylic chrome) and `1592221` (specular edges).

---

## 4. Monet review (works as designed)

Monet is the Material-You family: the whole palette derives from the accent hue —
HSL-derived window gradient (dark 0.14 L / light 0.94 L with eased saturation caps),
accent-tinted glass surfaces (tint ratios 0.18/0.26 after the 2026-08-25 richness
fix), radius 18/24/12, gradient or flat according to the *Gradient Surfaces* toggle,
wallpaper read-through. This is sound and was left untouched except the shared
accent-re-derive fix above (Monet was already covered there).

Recommendations (not changed, low risk):
- Monet in **light mode with no wallpaper** is intentionally pale; consider letting
  the *Gradient Surfaces* toggle also raise light-mode saturation a touch.

---

## 5. Settings → Appearance review

Covered controls: Visual Theme (Normal/Monet/Professional/Glass/Glassy/Ocean),
system + per-module wallpapers with opacity, accent presets + custom hex, Font Color
(Auto or user palette, applied last so user intent always wins), Gradient Surfaces,
Defined Card Edges, Border Thickness, Card Corner Radius, Display Density
(Compact/Normal/Comfortable — now with raised text floors), UI zoom 0.80–1.25,
global font family/weight via FontService, PDF layout, and one-tap Reset Appearance.

Changed: the Visual Theme caption now documents the real acrylic-blur behaviour and
its automatic fallback. No behavioural regressions.

Remaining known trade-offs (documented, deliberately not bulk-edited):
- **104 hard-coded `Foreground` literals** (mostly `White` on accent surfaces — safe)
  remain in templates; a handful (`#DBEAFE`, `#1F2937`, …) sit inside colored hero
  cards where they are local to that surface. Glassy overrides the global
  Primary/Secondary/Muted brushes, which covers the vast majority of text; the rest
  should be tokenized opportunistically per view, not globally.

---

## 6. Verification performed

- Lexical/structural scan of **every .cs file** (strings incl. interpolated/verbatim/
  raw, comments, chars, braces, `#if` nesting): **0 issues** after the fragment fix.
- XML parse of **all 174 XAML files**: 0 issues.
- Sweep verification: no literal `FontSize` < 30 remains in any XAML; 2,185 token
  references.
- No .NET SDK is available in this workspace (no network to dot.net), so a full
  `dotnet build` could not be run here — please do one build in Visual Studio on
  Windows. The changes are deliberately mechanical (attribute remaps, additive
  XAML setters, one new service, small engine edits) and each file was validated
  structurally.

## 7. Commits on this branch

1. `9788a47` — fix compile errors (stray fragment).
2. `a36a2cc` — global typography system (type-scale tokens + 2,185 remaps + density floors).
3. `5ae8e6c` — iOS-style glass: DWM acrylic backdrop + translucent stage veil + thin-material alphas.
4. `1592221` — specular card edges + frost polish (GlassEdgeBrush, 4 templated card styles).
5. `6376a90` — appearance senior audit: Glass accent re-derive fix + audit doc.

## 8. Continuation round — settings debug fixes & colour polish

**8.1 Light/Dark theme was never persisted (found & fixed).**
`AppearanceSettings` had no base-theme field, nothing restored it at startup
and `ThemeManager._currentTheme` always started `Light` — dark mode was lost
on every restart.

- New `AppearanceSettings.Theme` (`"Light"` default).
- Startup (App step 9d) restores it **before** the visual family applies so
  Monet/Glass/Glassy derive against the right palette.
- Persisted on both change paths: the Settings toggle and the AI-agent path
  (`AppearanceService.ApplyTheme`, which also now re-layers the module
  wallpaper — parity with the Settings path).
- Reset Appearance resets the base theme to Light through the same
  `ApplyTheme` path.

**8.2 Theme-aware buttons.**
`StatementListView` (New Statement) and the wizard's Clear All button used
hard-coded `Background="White"` — a stark white slab in dark and glass
families. Both now use `ElevatedSurfaceBrush` + `OutlineBrush` border, so
they stay light in Light mode and adapt everywhere else.

**8.3 Monet colour enhancement (light mode).**
Light-mode glass tint raised 0.18 → 0.22 so Monet's colour adaptation is
clearly visible on pale light surfaces (dark stays 0.26).

**8.4 Hard-coded colour audit (reviewed, intentional survivors).**
- CrashReportDialog: deliberately self-contained light-theme modal — kept.
- SalesReport navy hero, DeliveryNote progress banner, agent chat white
  input bar: white-on-accent pairs — kept (self-consistent surfaces).
- Wizard stepper dots / sales hero overlays: decorative on colored fills — kept.

## 9. Correction — Border styles cannot carry a Template (build errors)

The specular-edge overlay in commit `1592221` set `Style.Template` on four
styles whose TargetType is `Border` (`BaseCardStyle`, `ModuleSectionCardStyle`,
`SoftCardStyle`, `SettingsTileStyle`). `Border` derives from `FrameworkElement`,
not `Control`, so it has no `Template` property — the designer reported
MC4005 / XDG0012 / XLS0431 at those four lines.

**Resolution (corrected commit):**
- The four styles are restored to their original setter-only form —
  pixel-identical to the pre-glass build outside the glass families.
- `GlassEdgeBrush` is removed everywhere (both theme dictionaries,
  `ManagedKeys`, the engine helper).
- The iOS sheen is delivered at the brush level instead: `FrostBrush`'s
  gradient is now vertical (bright top alpha fading down) so every glass
  surface carries the soft top-light sheen with no template required.


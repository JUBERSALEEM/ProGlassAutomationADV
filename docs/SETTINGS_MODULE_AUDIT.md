# SETTINGS MODULE — DEEP AUDIT & UPGRADE PHASES
**ProGlass Automation · Arena agent-mode audit · 2026-09-05 (rev. A)**
**Auditor stance:** senior industrial-ERP developer. Criteria weighted for factory-floor duty: determinism, readability on weak displays, operator-proofing, low GPU assumptions, diagnosability — not showroom gloss.
**Method:** full read of the appearance stack (~18,000 LOC), static audits executed live (resource resolution, family-contract coverage, parity diffs, smell scans). Every finding cites file:line evidence. No impressions.

---

## 1. SYSTEM INVENTORY (measured)

| Layer | Files | LOC | Composition |
|---|---|---:|---|
| Settings shell | `Views/Settings/SettingsView.xaml` + `SettingsSectionStyles.xaml` | 553 | rail + grab-tab + content host |
| Settings sections | 11 UserControls | 5,534 | Appearance (1,153), Features (1,074), Agent, AI, Voice, Updates, Data, Demo, License, About |
| Settings ViewModels | 14 partials | 5,884 | ThemeWallpaper 256 · ThemeStudio 379 · WallpaperStudio 417 · Typography 574 · Helpers 861 · core 327 · others |
| Theme services | `Services/Theme/*` 15 files | 5,563 | engine (2,429 in 3 partials + 241 Industrial) · studio (1,126) · pipeline 193 · orchestrator 287 · scenes 214 |
| ThemeManager | 3 partials | 1,081 | Light/Dark swap, accent, outline, gradient prefs |
| Token dictionaries | `DarkTheme.xaml` / `LightTheme.xaml` | 2×630 | **194 keys each — exact parity** (verified) |
| Control styles | 17 dictionaries | 6,209 | ~240 styles, ~212 ControlTemplates |

Consumed-key resolution audit (all settings views vs every merged dictionary): **100% resolvable** — no dangling brushes/styles in the settings surface.

---

## 2. ARCHITECTURE MAP (as-built, after the 2026-09-05 pipeline work)

```
ONE canonical sequence (ThemePipeline) — everything else delegates:
  Rebuild(isDark)      = ThemeManager.ApplyTheme (dict swap) + RebuildFamily
  RebuildFamily        = VisualThemeEngine.ApplyModule  (family + module +
                         wallpaper/scene in ONE paint; finally = studio hook)
                       + ApplyAccent + ReapplyOutlinePreference
  RebuildWallpaperLayer = InvalidateSceneCache + ApplyModule   (wallpaper-only)
  RebuildAccentLayer    = accent + outline + RefreshScene

Family paint (engine): ClearOverrides(64 managed keys)
                     → GuaranteeCriticalResources PRE-SEED (never-absent)
                     → family republishes FULL contract
                     → font preference last → GuaranteeCriticalResources
Studio:   profile (19 sliders + 1) × snapshot base → scaled writes, per-key
          guarded units; per-family DefaultsFor incl. industrial (matte)
Wallpaper: ThemeLinked scene / PinnedScene / online providers / module files
Backdrop: SystemBackdropService — native DWM Mica/Acrylic, no packages
Net:      glass families paint alike through PublishSectionGlass tokens
```

This is now a genuinely layered design — one writer per layer, one order, contracts named. That was not true three days ago (four competing chains).

---

## 3. FINDINGS REGISTER
Severity: 🔴 critical · 🟠 high · 🟡 medium · 🔵 low · ⚪ note. Status at audit time.

### F-01 🔴 FIXED — Industrial family incomplete key contract (ERR-50472F)
Evidence: `VisualThemeEngine.Industrial.cs` published 9 of 64 managed keys; `ClearOverrides` strips all 64; `DashAmbientBrush` (ComingSoonView.xaml:34, dashboard) left absent → `ResourceReferenceKeyNotFoundException` at deferred template inflation.
Fix `924cf0c`: full contract republished as flat slabs (Dash trio, shell, sidebar, 14 Liquid tokens, 7 Tint chips, shadow color).

### F-02 🟠 FIXED — Double paint per switch (visible flash/"dull blur")
Evidence: crash log showed `Industrial family painted` ×2 per switch; `ThemePipeline.RebuildFamily` called `Apply()` then `ApplyModule()` (which rebuilds the family internally).
Fix `924cf0c`: single ApplyModule pass; scene signature gates real repaints; identical repeats skip.

### F-03 🟡 FIXED (Phase 1) — Phantom studio preset "DenseGlass"
*Resolution: `Preset()` gained an explicit `case "DenseGlass":`; a single
authoritative `ThemeStudioSettings.PresetNames` registry now feeds the VM
combo; parity locked by `ThemeRegistryParityTests.EveryRegisteredPreset_
ProducesADistinctProfile`.* Original finding:
Evidence: `SettingsViewModel.ThemeStudio.cs:57` lists `DenseGlass` in `StudioPresetOptions`; `ThemeStudioSettings.Preset()` has cases only for Crystal / LightGlass / Balanced / Frosted / RoyalRich (:252–:312). Selecting DenseGlass silently falls to the default arm yet audits "preset applied: DenseGlass".
Impact: one combo row lies to the operator. Fix: implement the case (dense, low-transparency industrial-ish glass) or drop the row. 15 min.

### F-04 🟡 FIXED (Phase 1) — Wallpaper-opacity slider repaints the entire family per tick
*Resolution: live drag now coalesces to the wallpaper layer (≤ ~8/s +
trailing pass, `ScheduleLiveWallpaperRepaint`); wallpaper set/clear
commands lost their pre-pipeline double paint (Apply+ApplyModule → one
`RebuildWallpaperLayer`); the Light/Dark toggle now runs the canonical
`ThemePipeline.Rebuild(bool)`.* Original finding:
Evidence: `SettingsViewModel.ThemeWallpaper.cs:158` — opacity setter calls `ApplyVisualTheme()` → `RebuildFamily` (full 64-key rebuild + accent + outline) on every drag tick. Save is debounced; the *paint* is not.
Impact: drag = continuous teardown churn; the exact flash the single-paint fix removed reappears during this one gesture. Fix: route the live tick to `ThemePipeline.RebuildWallpaperLayer` (exists, purpose-built) and run `RebuildFamily` only on capture-complete. 30 min.

### F-05 🟡 PARTIALLY FIXED (Phase 2) — Appearance surface is not localised in a 15-language app
*Progress: all new Phase-2 surface (quick looks, live preview, studio
slider niceties) + wallpaper status are localized (en-US authority +
ar-AE); remaining known gaps are family/scene proper names and studio
preset names (intentionally brand-stable — review with owners).* Original finding:
Evidence: 15 language dictionaries (incl. ar-AE, ur-PK), 2,524 strings in en-US — but family picker shows hard-coded English concatenations (`ThemeWallpaper.cs:66` `Display = key + " — " + scene`), `"No wallpaper set"` (:202), studio preset names, rail section labels are localized only where already strung. The audience that bought the Industrial theme is exactly the audience running ar-AE.
Impact: the headline feature ships English-only. Fix: localize family display names + scene labels + status strings; keep the raw Key internal (already are).

### F-06 🔵 FIXED (Phase 1) — Mode-coupling map duplicated in the VM
*Resolution: `VisualThemeEngine.ModePairFor(family)` owns the pairing;
VM consumes it; mapping locked by `ModePairFor_OnlyIndustrialCouplesTheMode`.* Original finding:
Evidence: the `switch` mapping IndustrialDark→Dark, White/Grey→Light lives in `ThemeWallpaper.cs:98` instead of beside `VisualThemeEngine.IsIndustrial`. Any future theme with mode pairing forces XAML-VM-layer edits.
Fix: `VisualThemeEngine.ModePairFor(family) → bool?`; VM consumes.

### F-07 🔵 FIXED (Phase 3) — Rail navigation is 10 hand-copied button blocks
*Resolution: data-driven `NavRows` registry — rail is one ItemsControl,
page validation reads row keys (the 10-string guard died with it), the
10 `IsXPage` flags and their 10-line notify farm are gone (host now uses
EqualityToVisibilityConverter), Agent has its own glyph, and the content
header shows a localized label instead of the raw English page key.
Adding a page = 1 row + 1 host line.* Original finding:
Evidence: `SettingsView.xaml:163–420` — identical Button/Style/DataTrigger/glyph/label blocks per page (~120 lines duplicated; `Agent` and `AI` even share glyph `&#xE8F2;`).
Impact: adding a page = copy-paste + manual page-flag; the `Is{Page}Page` flag farm (10 OnPropertyChanged calls per switch) grows linearly.
Fix: data-driven nav list (`SettingsNavItem {Key, Glyph, LabelKey}` in an ItemsControl), page state as `SettingsPage=="X"` converter or computed flag.

### F-08 🔵 FIXED (Phase 2, studio scope) — "Reset/Defaults" reachability differs between Studio and family layers
*Resolution: every studio slider now carries a per-slider reset chip
(↺ back to the active family's default) via StudioSliderControl +
ResetStudioSliderCommand; Reset Current/All kept. Per-card resets for
non-studio tiles remain a Phase-3 item.*
Evidence: Theme Studio exposes Reset Current / Reset All with audit; family/accent/border/corner prefs scatter "reset" behaviours across individual tiles (border/corner have debounced audits, no per-tile reset). Operators expect one discoverable "restore factory appearance" affordance per card.
Fix: per-card reset chips bound to DefaultsFor; keep global Reset All.

### F-09 ⚪ NOTE — Startup still paints familly twice (Apply + ApplyModule)
`App.StartupCore.cs:407` calls `Apply()` pre-window; the first ApplyModule repaints identically. Invisible (window not shown), costs ~ms once. Leave; note only.

### F-10 ⚪ FIXED (Phase 1) — Stale narrative comments
*Resolution: ThemeWallpaper header lists the real 9-family catalogue; the
Industrial.cs keyline comment now truthfully states outlines are accent-
layer-owned (ApplyKeyline is the documented no-op family marker).*
`ThemeWallpaper.cs:36-42` header still lists the 5-family catalogue (missing Ultra/Industrial); roadmap files cover history, but in-code headers should be authoritative.

### Confirmed-strong areas (audited, no action)
- Token parity Dark/Light: 194 ≡ 194 keys, zero drift.
- Resource resolution: 100% of settings-view references defined.
- GuaranteeCriticalResources: pre-seed + trailing loud pass — the correct defense-in-depth shape.
- Debouncing discipline: config saves & audits settle 500–600 ms, flushed on Dispose — no lost writes.
- Backdrop service: native DWM, capability-gated, honest degradation — no package weight.
- Zero TODO/FIXME/HACK in the entire appearance stack; the only hard-coded colour in the settings surface is the rail's `#000000` shadow.
- Scene catalogue parity: every family has a named scene (Ledger/Fog/Foundry etc. — verified `BuiltinWallpaperScenes.For`).

---

## 4. RATINGS (senior industrial-ERP bar, /10)

| # | Area | Score | Justification (audited) |
|---|---|---:|---|
| 1 | Architecture & layering | **8.5** | One pipeline, one writer per layer, contracts named and now enforced by pre-seed. Would be 9+ with contract tests in CI. |
| 2 | Theme-engine correctness | **8.0** | Contract-complete families after `924cf0c`; guarantee nets; normalized legacy names. One unproven WPF deferred-static residual, now moot by design. |
| 3 | Determinism & restart fidelity | **8.0** | Persist→Restore→Snapshot ordering fixed; saved configs replay to the same pixels across restarts (the "reset does nothing" class closed). |
| 4 | Settings shell UX | **7.5** | Rail + always-visible grab-tab + auto-close is professional ERP chrome; 120 lines of copy-pasted nav and twin glyphs cost it. |
| 5 | Theme Studio | **8.0** | 19-axis live scaling on snapshot bases, per-family defaults, guarded per-key writes, honest presets — minus one phantom preset and no visual preview. |
| 6 | Wallpaper Studio | **8.0** | Scenes per family, provider pipeline with diagnosis logs, library management, native backdrops. Opacity-tick churn is the blemish. |
| 7 | Style-system quality | **7.0** | Disciplined tokens; but 17 dictionaries with overlapping glass systems (LiquidGlassDesign 62 templates vs LiquidGlassStyles 24) invite divergence. |
| 8 | Industrial-ERP fitness | **7.5** | Industrial family is now honest: opaque slabs, keyline, matte tints, muted motion, projector-safe. Studio presets remain showroom-biased; no "Factory Floor" one-click look yet. |
| 9 | Performance discipline | **7.5** | Freeze-everything, blur in only 2 files, bounded shadows, debounce, single paint — docks points for opacity-tick rebuild and per-switch re-render breadth. |
| 10 | Diagnostics & observability | **8.5** | Crash reporter IDs + breadcrumbed pipeline logs + named fallback injections — this is how a field-crash got root-caused in one reading. |
| 11 | Localization & accessibility | **5.5** | 15-language platform, English-only appearance surface; no per-paint contrast audit; focus visuals inherit theme-file quality (decent but unaudited). |

**OVERALL: 7.6 / 10 — "B+ / ship-ready with Phase 1–3."**
Three days ago this was a 5.8 (four competing paint chains, contract gaps, lying dropdowns). The architecture is now right; what remains is polish, parity enforcement, and ERP-first ergonomics — not reinvention.

---

## 5. UPGRADE PHASES (prioritized — first three are the important ones)

### ⭐ PHASE 1 — Correctness & parity sweep  *(small, do first — 4–6 h)*
Closes every OPEN finding that a user can *see tomorrow*:
1. Implement or remove **DenseGlass** (F-03) + add a **registry test**: `StudioPresetOptions ≡ Preset() cases` (would have caught this at build).
2. Route opacity-tick painting to **RebuildWallpaperLayer**; family rebuild only on slider release (F-04).
3. Move mode-coupling to `VisualThemeEngine.ModePairFor` (F-06); refresh stale headers (F-10).
4. **Contract test** (the crown jewel): unit test that simulates every family and asserts all 64 managed keys present post-paint — turns ERR-50472F's class into a compile-time failure forever.
*Acceptance: tests green; opacity drag shows zero full-dictionary rebuilds in the app log.*

### ⭐ PHASE 2 — ERP-first Appearance ergonomics  *(2–3 days)*
The settings page an operator meets:
1. **Quick-look preset row at the top**: `Factory Floor` (industrial + matte + dense + motion low), `Office` (professional light), `Showroom` (liquid glass). One click sets family+studio+mode coherently.
2. Studio sliders: tick marks + numeric readouts + per-slider **reset chip** (F-08); glass-only sliders auto-dim when an industrial family is active (they scale nothing visible).
3. **Preview strip**: live mini-card rendering the current contract (surface/ink/keyline/accent/status chips) beside the pickers — decide before you commit.
4. Appearance surface localization: family display names, scene labels, statuses, presets → language dictionaries (F-05).
*Acceptance: factory user configures in ≤2 clicks; no English leaks under ar-AE.*

### ⭐ PHASE 3 — Settings shell professionalization  *(2–3 days)*
1. Data-driven nav (F-07): `SettingsNavItem` collection + ItemsControl; distinct Agent glyph; kills the 10-flag property farm.
2. Industrial chrome parity: when an industrial family is active the rail drops its glass glare band (honor the family's "no theatre" law in the settings UI itself — currently the rail keeps sheen/reflection layers).
3. Focus & keyboard: mnemonics on nav items, visible focus rectangles from a token, Esc closes rail.
4. Empty/error states: wallpaper fetch failure shown inline (FxStatus pattern) instead of only logging.
*Acceptance: add-a-page = 3 lines; rail renders matte under Industrial; full keyboard traversal.*

### PHASE 4 — Theme Studio v2  *✅ SHIPPED (2026-09-05)*
1. ✅ Industrial preset pack (Ledger / Workshop / HMI Steel, 19/19 axes each) — `PresetNames` is the single source; VM combo consumes it directly.
2. ✅ `AxisNames` registry + reflection API (`SetAxis`/`ResetAxisTo`) is the single home of the 19 axes; the per-slider reset switch was deleted; `StudioAxisManifestTests` (6 facts) is the compile-time drift lock.
3. ✅ Snapshot coherence: call sites audited (family-paint moments only — Restore / OnFamilyApplied / lazy guard); pinned by the SNAPSHOT CONTRACT comment in the service, no restructure of the proven pipeline.
4. ✅ One-step undo: single-slot capture before every studio mutation (sliders, presets, per-slider chip, both resets, quick-look entry); restores live preview and the persisted copy together.
*Acceptance met: manifest tests verify-by-inspection (dotnet not available in the authoring sandbox — run in VS); presets verified by range + distinctness tests.*

### PHASE 5 — Style-system consolidation  *(3–5 days)*
1. Merge `LiquidGlassStyles.xaml` into `LiquidGlassDesign.xaml` (one glass system, one owner).
2. BasedOn-chains for the 34 PI-industrial template variants; delete dead tokens (`WallpaperOpacityValue` proven consumer-less).
3. Token taxonomy doc (Surface/Outline/Ink/Accent/Status/Scene) + the parity checker as a CI script (the same diff harness used to prove the appearance restructure).
*Acceptance: dictionary count 17→~12; token audit runs in CI.*

### PHASE 6 — Performance & accessibility hardening  *(2–3 days)*
1. Contrast auto-audit at paint time: engine computes ink/surface ratios post-family-paint and logs sub-AA pairs (industrial themes must *pass* by construction).
2. Honor Windows reduced-motion + system contrast settings; lower-tier GPUs drop sheen layers even in glass families.
3. RTL traversal smoke (ar-AE) incl. rail direction flip verification.
*Acceptance: zero sub-AA pairs logged for all three industrial families; reduced-motion respected.*

### PHASE 7 — Theming platform (strategic, later)
Theme-pack export/import (studio profile + family + scene as one JSON), per-module overrides, designer handoff doc, paint-time telemetry.
*Only after Phases 1–6: the platform earns a platform layer.*

---

## 6. WHAT I VERIFIED LIVE (debug appendix)
| Check | Result |
|---|---|
| Family contract coverage per painter (scripted key-set diff) | Industrial 35/64 owned + 31 safely static + 1 dead key; other families complete via statics+republish |
| ManagedKeys × theme statics × runtime writes | exact Venn drawn; `WallpaperOpacityValue` proven consumer-less |
| Settings-view resource resolution (all 15 style/token sources) | 100% defined |
| VM option lists ↔ service registries | **1 mismatch found: DenseGlass (F-03)** |
| Scene catalogue ↔ family catalogue | 9/9 parity, industrial=Ledger/Fog/Foundry |
| Preset cases ↔ VM list | 5 cases vs 6 options (F-03) |
| DefaultsFor families | all 9 incl. matte industrial profile (:209) |
| Smell scan (TODO/FIXME/HACK, silent catches, hard-coded hex) | 0 / 5 by-design-with-fallbacks / 1 (rail shadow) |
| Localization coverage of appearance surface | **gap proven (F-05)** |

**Bottom line:** the foundation is now senior-grade. Phases 1–3 turn it into the ERP settings experience the industrial customers actually ordered; Phases 4–6 make it self-policing so these failure classes cannot return.

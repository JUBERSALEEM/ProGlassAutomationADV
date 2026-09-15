# About cards vs the engine, Settings navigation drift, and the Settings redesign

**Date:** 2026-09-16 · **Scope:** `Views/About/**`, `Views/Settings/**`,
`Views/Shared/QuickSettingsPanel.xaml`, `Services/Theme/**`,
`Services/ThemeManager.Part1/2.cs`, `Themes/Styles/AboutSurface.xaml`,
`Themes/Light|DarkTheme.xaml`, tests.
**Mockups:** `docs/mockups/settings-redesign/01…10` (10 PNG sheets, for approval).

---

## 1. Why the About cards still would not take a dynamic corner / border

Because **two About contracts existed and never met.** This is not a colour
bug; it is a wiring bug, and it is total in both directions.

### 1.1 Contract A — what the ENGINE publishes (live, cleaned up on switch)

Declared in `Services/Theme/VisualThemeEngine.cs` + `Ios26.cs`, republished on
every family/accent pass, and swept by the engine's managed-key list:

```
AboutCardBorderBrush   AboutDividerBrush        AboutInteractiveBrush
AboutInteractiveHoverBrush  AboutInteractiveBorderBrush
AboutInteractiveBorderStrongBrush
AboutGlassSlabBrush    AboutGlassPanelBrush     AboutGlassCapsuleBrush
AboutGlassSlabStroke   AboutGlassHairlineStroke AboutGlassCaptionBrush
AboutGlassGlowBrush    AboutCardCornerRadius    AboutPanelCornerRadius
AboutCardEffect        AboutWallpaperBrush      AboutGlassChromeVisibility
```

**Consumers of these keys across the whole repository: zero view files.**
Verified by scanning every `.xaml` and `.cs`: the only files that mention them
are the engines, `ThemeManager`, and one test. The engine was talking to an
empty room.

### 1.2 Contract B — what the ABOUT VIEWS read

| Key | Who writes it | Dynamic? |
|---|---|---|
| `Ios26CardBrush`, `Ios26CardBorderBrush`, 23 more palette keys | `Themes/LightTheme.xaml` ⇄ `DarkTheme.xaml` | Light/Dark only — a family/accent change does **not** move it |
| `Ios26RadiusCard/Inner/Tile/Chip/Small` | *a local constant in `Views/About/AboutIos26Theme.xaml`* | **never** |
| `Ios26CardShadow` | local constant | never |

So: the engine changed keys nobody read, and About read keys nobody changed.
The reported symptom — "cards still not accepting dynamic card corner border
color from Monet engine" — is exactly what that produces.

### 1.3 The radius authority the About cards ignored

`Services/ThemeManager.Part2.cs`:

```csharp
public static void ApplyCardCornerRadius(double radius)   // 0…28, default 12
{
    resources["CardCornerRadius"]       = cr;
    resources["ControlCornerRadius"]    = cr;
    resources["CardCornerRadiusLarge"]  = cr;
    ApplyDynamicRadiusPrimitives(resources, radius);   // Radius.XSmall/Small/Medium/Large/Hero
}
```

That is the **one** radius authority in the product: the Appearance "card corner
radius" preference, written at startup, on every slider move, on theme swap
(`ReapplyCardCornerRadius`) and by the family engines. Every other card in the
app follows it. The About cards followed a hard-coded `26`.

### 1.4 Why the obvious fix would have failed silently

A key declared in a dictionary merged **locally** by a view shadows
`Application.Resources` — the rule that froze the About palette in the first
place. Had the radii simply been republished at application scope while the
local `<CornerRadius x:Key="Ios26RadiusCard">26</CornerRadius>` stayed in
`Views/About/AboutIos26Theme.xaml`, **the local declaration would still win the
lookup and the republish would do nothing.** The fix therefore has to move the
declarations out *and* write the values at apply time. Both halves are done and
each is locked.

---

## 2. The fix

### 2.1 `Services/Theme/AboutSurfaceTokens.cs` (new)

Republishes the About scale from the live card radius, using **the mockup's own
ratios**, so the default configuration renders the approved design exactly and
anything else scales from it:

| About key | ratio | at default 12 px | at 18 px | at 28 px |
|---|---|---|---|---|
| `Ios26RadiusCard` | 26/12 | **26** | 39 | 48 (clamped) |
| `Ios26RadiusInner` | 12/12 | **12** | 18 | 28 |
| `Ios26RadiusTile` | 10/12 | **10** | 15 | 23 |
| `Ios26RadiusChip` | 12/12 | **12** | 18 | 28 |
| `Ios26RadiusSmall` | 6/12 | **6** | 9 | 14 |

Whole pixels, monotonic, clamped at 48 so a card never becomes a pill.
`Ios26RadiusBar` stays 999 in the module dictionary — bar-only full round, not a
card.

The card **edge** is aliased to the engine's own About edge token:
`Ios26CardBorderBrush ← AboutCardBorderBrush` (accent-derived, republished live
by `PublishAboutPalette`), falling back to the theme dictionary value when the
engine has not published one. That is what makes the outline follow the accent
and the family, not just Light/Dark.

### 2.2 Where the declarations live now

| Layer | File | Contains |
|---|---|---|
| Fallback (first frame) | `Themes/Styles/AboutSurface.xaml` (**new**, merged last in `App.xaml`) | 5 `CornerRadius` tokens, mockup values |
| Live | republished by `AboutSurfaceTokens.Publish` from `ApplyCardCornerRadius` (Part2) and `ApplyThemePostProcessing` (Part1) | same 5 keys + the edge |
| Module-owned | `Views/About/AboutIos26Theme.xaml` | `Ios26RadiusBar`, `Ios26CardShadow` only |

`AboutModuleTokenGuard` (from the previous pass) now also verifies these five
radii and the edge, and repairs them from `CardCornerRadius` if the dictionary
never reached the running build — so a card can never render square-and-edgeless
in silence.

### 2.3 Locks added

`Tests/Theme/AboutSurfaceAuditTests.cs`

| Lock | Catches |
|---|---|
| `About_radius_scale_reproduces_the_approved_mockup_at_the_default` | the design drifting at the default setting; the XAML fallback disagreeing with the table |
| `About_radius_scale_tracks_the_card_radius_monotonically` | a "dynamic" scale that is not monotonic / fractional / out of range, and the no-op case (radius 12 vs 18 must differ) |
| `About_surface_tokens_are_not_declared_inside_the_module` | the shadowing trap coming back |
| `About_surface_publisher_is_wired_to_the_radius_and_theme_paths` | a publisher nobody calls (the way the engine's About keys died) |
| `About_card_edge_follows_the_engine_token` | the edge alias going dead, or a white-on-white edge returning |

---

## 3. Settings top panel: the "10 buttons" and the overlap

### 3.1 What was actually in the file

`Views/Shared/QuickSettingsPanel.xaml` — the popup behind the top-bar gear — was
**ten hand-copied `<Button>` blocks** with their own `CommandParameter` strings,
their own glyphs and no spacing. The rail, by contrast, has been data-driven
since Phase 3 (`SettingsViewModel.NavRows`, documented in its own source as "the
single source of truth"). Measured against that registry:

| Symptom | Evidence |
|---|---|
| 10 buttons for 12 pages | `Themes` and `Sharing` had **no button at all** |
| two glyphs disagreed with the rail | Features drew `E762` (the rail's **License** glyph), License drew `E72E`, Agent drew `E99A` instead of the rail's `E9F5` — one page, two icons |
| buttons touching | no margin on the buttons; `Typography.Icon` sets **no** right margin, so every glyph touched its label. Exactly **one** button carried a hand-added `Margin="0,0,10,0"` — the gap was known and patched in place |

### 3.2 The fix

One grouped `ItemsControl` over `SettingsVM.NavRows` — the same collection the
rail renders — with a `CollectionViewSource` grouping on the row's **localized**
`GroupLabel` (so captions come from the localization service and the header
template stays a single binding). `SettingsNavRow` gained `GroupKey`/`GroupLabel`
and every row now declares its group (`Settings.GrpLook`, `Settings.GrpIntelligence`,
`Nav.GroupSystem`) — no new localization keys were invented.

Spacing is now explicit and uniform: **6 px between rows** (item margin) and
**10 px between glyph and label**, with the glyph in a fixed **20 px** column so
all twelve labels start on one vertical line. The popup chrome
(`ShellLiquidBrush`, `CardCornerRadiusLarge`, `LiquidShadowColor` shadow,
padding) is untouched — only the list inside was rebuilt.

`Tests/Settings/SettingsNavigationAuditTests.cs` locks it:

| Lock | Catches |
|---|---|
| `Settings_registry_is_the_only_place_pages_are_declared` | any settings navigation surface hard-coding a page key again |
| `Quick_panel_renders_the_registry_and_keeps_its_spacing` | the panel drifting off the registry, or the spacing being removed |
| `Every_registered_page_has_a_host_and_a_rail_row` | a registered page with no section host (unreachable), a host with no registry entry (invisible), duplicate glyphs, an ungrouped row |

> Note on case: the registry says `AI` and the host's `ConverterParameter` says
> `Ai`. That resolves, because `EqualityToVisibilityConverter` tries `Equals()`
> and then falls back to `OrdinalIgnoreCase`. The lock mirrors the converter's
> semantics rather than the other way round — a case-sensitive check would
> report a live page as dead.

---

## 4. Settings module audit (what the redesign has to work with)

| Page | Lines | `<Border>` count | Shape |
|---|---:|---:|---|
| Appearance | 1379 | 60 | presets, accent grid, sliders, per-surface radius, typography |
| Features | 1251 | 119 | deep flag lists ×6 modules |
| Demo | 520 | 30 | data seeding / destructive actions |
| Agent | 497 | 22 | provider + endpoint config |
| AI | 494 | 26 | provider cards, keys, model choice |
| Data | 416 | 19 | backup, storage, purge |
| Themes | 387 | 13 | family presets + scene grid |
| Voice | 368 | 7 | engine, rate/pitch, test |
| Updates | 349 | 11 | channel, check, history |
| Sharing | 236 | 6 | per-module sharing rows |
| About | 190 | 15 | version, credits, links |
| License | 137 | 3 | activation, status |

Container styles in use: `SurfacePrimaryStyle` (28 uses in Appearance alone),
`SurfaceDetailStyle` (26), plus `ModernSliderStyle`, `ComboBoxStyle`,
`ModernToggleSwitch`, `OutlinedPillButton`, `AccentChipStyle`. So Settings is
already surface-tokenised — what it does **not** have is About's card language
(26 px frosted card + hairline, 12 px inner tile, value pill, icon disc, chip
grammar). The redesign is therefore a *grammar swap on existing containers*, not
a rewrite of 6 000 lines: each `SurfacePrimaryStyle` card becomes the About card
style, each `SurfaceDetailStyle` inner block becomes the About inner tile.

---

## 5. Redesign specification (what the mockups show)

### 5.1 The card grammar — every element maps to an existing token

| Mockup element | Token / style | Notes |
|---|---|---|
| Page card | `Ios26CardStyle` (26 px, `#CCFFFFFF` frosted, `Ios26CardBorderBrush` hairline, `Ios26CardShadow`) | the About card, verbatim |
| Inner tile / row group | `Ios26InnerTileStyle` (12 px, solid white) | replaces `SurfaceDetailStyle` |
| Icon disc | `Ios26IconTileStyle` (10 px tile + 19 px MDL2 glyph) | one disc per card header |
| Card title | `Ios26CardTitleStyle` (17 px Bold, `Ios26TextTitleBrush`) | |
| Field label / value | `Ios26FieldLabelStyle` (13 px Regular, muted) · `Ios26FieldValueStyle` (14 px) | the License card's proven label/value role pair |
| Value pill | `Ios26ValuePillStyle` (`#F1F5F9` pill) | read-only values, paths, keys |
| Soft chip | `Ios26ChipSoftStyle` / `Ios26ChipGreenStyle` | status ("ONLINE", "VALID"), counts |
| Progress | `Ios26BarStyle` (full-round track + accent fill) | |
| Primary action | `AboutPillButtonStyle` (12 px radius — **not** a 999 pill) | Save / Apply / Activate |
| Capture / copy | existing `LicenseCopyButtonStyle` pattern | copy buttons on pills |
| Toggle | `ModernToggleSwitch` (keep) | unchanged, tokenised |
| Slider | `ModernSliderStyle` (keep) | accent-filled, already dynamic |

Grid: rail 264 px · content padding 18 px · card padding 22,20 · gap 14 px ·
inner tile padding 12,10. All values already exist as tokens; the mockups use
nothing that is not in the list above — that is what "adaptable 100% exactly"
means here.

### 5.2 Page → mockup sheet map

| Sheet | Covers | Highlight |
|---|---|---|
| 01 | **Shell + Appearance (hero)** | the anatomy: top bar, 12-row rail, page header, card grid |
| 02 | **Appearance — Theme & Look** | family preset tiles, accent swatch grid, Light/Dark segmented control |
| 03 | **Appearance — Density & Typography & Cards** | density segmented control, type preview tiles, per-surface radius |
| 04 | **Themes & Wallpaper** | family cards, wallpaper scene grid with selection ring |
| 05 | **AI** | provider cards, model dropdown, API-key pills + copy, test button |
| 06 | **Agent** | endpoint rows, capability toggles, status chips |
| 07 | **Voice** | engine choice, rate/pitch sliders, preview bar, mic test |
| 08 | **Data & Backup** | storage bar, backup rows, restore points, purge (danger card) |
| 09 | **Updates** | channel segmented control, check card, release history rows |
| 10 | **Features** | per-module flag grid (the 119-border page, now one grammar) |
| (spec) | Demo, Sharing, License, About | same grammar: Demo = Data's rows with a danger card; Sharing = Features' rows per module; License = the existing label/value card + status chips; About = the live module (already built) |

### 5.3 Implementation plan after approval

1. **Phase 1 — grammar**: add `Settings/SettingsCardStyles.xaml` that aliases the
   About styles under settings-facing names, and swap `SurfacePrimaryStyle` →
   `Ios26CardStyle`, `SurfaceDetailStyle` → `Ios26InnerTileStyle` mechanically
   (page by page, one commit per page, gate green each time).
2. **Phase 2 — headers**: card headers become icon disc + title + optional chip.
3. **Phase 3 — controls**: keep the tokenised controls (slider/toggle/combo);
   restyle only their containers.
4. **Phase 4 — the two heavy pages** (Features 119 borders, Appearance 60):
   collapse repeated borders into the tile grammar; expect real line reduction.
5. Every phase keeps the About locks, the Settings nav locks and the density /
   theme dynamic path green; the gate is the acceptance test.

---

## 6. Verification

* `scripts/layout-verify/verify_all.sh` — end-to-end green (277 XAML, 1479 C#).
* New locks mirrored and passing: radius scale (mockup parity at 12 px,
  monotonic 0→28, whole pixels, clamped, non-no-op), surface declarations, wiring
  greps, edge alias, registry↔hosts coverage, glyph uniqueness, panel spacing.
* `xaml-guard`, `xaml-resource-lint`, `xaml-xref-audit` clean; the
  `design-system-guard` findings are the two pre-existing ones (§7.2 of the
  2026-09-15 doc).
* No `dotnet` in this environment: the compile is confirmed by the owner's build.

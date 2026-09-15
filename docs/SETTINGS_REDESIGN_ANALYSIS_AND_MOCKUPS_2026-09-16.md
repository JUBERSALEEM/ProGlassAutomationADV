# About cards vs the engine, Settings navigation drift, and the Settings redesign

**Date:** 2026-09-16 · **Scope:** `Views/About/**`, `Views/Settings/**`,
`Views/Shared/QuickSettingsPanel.xaml`, `Services/Theme/**`,
`Services/ThemeManager.Part1/2.cs`, `Themes/Styles/AboutSurface.xaml`,
`Themes/Light|DarkTheme.xaml`, tests.
**Mockups:** ten approved PNG sheets. They are **not in the repository** by
owner directive ("Dont commit png ever in codes"): `docs/mockups/**/*.png` is
ignored, the files stay on disk as review artefacts, and the blob they were
briefly committed as has been scrubbed from this branch's history. What the
repository carries is the SPEC below — every value the sheets show maps to a
token that already exists.

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
| Icon disc | `SettingsIconTileStyle` (34 px disc, accent-soft) + `SettingsIconGlyphStyle` | on **rows** and on the rail's identity header. **No card heading carries a disc** — the sheets put icons on rows (sheet 02/03/06/08/10) and only the rail header (gear + "Settings" + subtitle) shows a disc beside a title. |
| Card title | `SettingsCardTitleStyle` (Bold, `Ios26TextTitleBrush`) + `SettingsCardHeaderStyle` / `SettingsCardHeaderNoteStyle` for the header block's bottom gap | the group heading: title, then the recap line as its helper |
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


---

## 7. Implementation log — 2026-09-16 (owner approved all ten sheets)

### 7.1 What shipped in this pass

| Item | Detail |
|---|---|
| **`Views/Settings/SettingsCardStyles.xaml`** (new) | The module design system: 53 keys — page card, flat/danger cards, inner tile, interactive/bare tiles, semantic washes (info / success / wash), icon tile + glyph, ten type roles, value pill, four chip pairs, separator, three buttons, icon button, segmented control + track, action bar, spacing scale. Built **entirely** on the live `Ios26*` tokens, so Settings now inherits Light/Dark, the accent, the visual family, the density profiles and the Appearance card-corner slider. Zero literals (locked). |
| **Shell** | Rail restyled to the About card grammar (frosted + hairline + the one shared `Ios26CardShadow`); its bespoke drop shadow and sheen stack removed, which is why the rail and the cards it framed did not read as the same material. Grammar merged ahead of the section styles. |
| **All 12 section pages** | Local page title + subtitle removed (the shell header strip is the single page header — every page printed its title twice); page gutters normalised to the 18/14 scale; `SurfacePrimaryStyle` → `SettingsPageCardStyle` (116 sites); `SurfaceDetailStyle` → `SettingsInnerTileStyle` (130 sites); **15 empty cards used as vertical gaps** → real separators; hand-built inline "cards" (their own radius/padding/wash tokens) → grammar tiles; `Typography.*` → grammar type roles; group bands → card captions. |
| **`Ios26CardShadow`** | Moved from `Views/About` to `Themes/Styles/AboutSurface.xaml` (app level). A shadow declared inside About is invisible to Settings, and an unresolved `{DynamicResource}` on `Effect` leaves a card with no shadow and no error. |
| **Two Appearance chip-slots** | A compact hex-entry strip and a second small border had been carrying the card style; both demoted to the wash tile after review (a 22,20-padded 26px card in a chip slot is a visible regression). |

### 7.2 Verification added for a 12-page rewrite

* **`scripts/settings-rewrite/verify_bindings.py`** — a page may be restyled, never defused. Compares each section against its committed version and fails if **any** binding, `CommandParameter`, `x:Name`, converter, `RelativeSource` or text key disappears. Intentional removals are recorded in a `JUSTIFIED` registry **with their reason**, so the decision is auditable and any *other* loss still fails. It caught the legitimately-removed duplicate titles and, importantly, would have caught a dropped binding while re-wrapping 246 containers.
* **`scripts/settings-rewrite/apply_grammar.py`** — the mechanical transformation, with two bugs found during review and fixed: it first re-emitted opening tags **without their `>`** (malformed XAML) and later injected a `Style` attribute next to a `<Border.Style>` property element (the compiler's "property set more than once"). Both were caught by diff review and by the duplicate-property gate stage respectively — the tool now skips the property-element form and reports what it did not touch.
* **New locks** in `Tests/Settings/SettingsNavigationAuditTests.cs`: no settings page may use a generic `Surface*` card style again, every page must merge the grammar, no local page-title block, no card used as a spacer, and the grammar itself must be token-only (no hex, no literal radius, no literal font size).

### 7.3 Superseded

The "next passes" this section listed (card headers, the two heavy pages) were
done in the second pass — see **section 8**, which states the rules that pass
settled, what it shipped and what is still open.


---

## 8. The rules this module now follows (2026-09-16, second + third pass)

Read the sheets for *detail* rather than layout and they say five things. Each
one is now a rule with a guard behind it, because each one had been got wrong at
least once by a mechanical pass.

### 8.1 Five rules

| # | Rule | Anchored in |
|---|---|---|
| 1 | **A group heading is its card's header**, never a caption floating above one: the band caption becomes the card title and the band's recap line becomes the card's helper line (`SettingsCardTitleStyle` + `SettingsCardHeaderNoteStyle`). | every sheet — "Visual family", "Storage", "Module flags" |
| 2 | **No disc on a card heading.** Discs belong to rows (`SettingsIconTileStyle` + `SettingsIconGlyphStyle` in a row, 26–34 px) and to the shell rail, including the rail's identity header (gear disc + "Settings" + subtitle + chevron). | 02/03/06/08/10 rows vs. the one disc-bearing heading in 01/02 |
| 3 | **A page card is top-level only.** Anything inside a card is an inner tile; two page cards nested paint the same fill, border and shadow twice and the hierarchy disappears. | sheet 06 "Capabilities", 08 "Backups", 10 "Module flags" |
| 4 | **A row sits directly in its card or tile** — disc, title, helper line, trailing control — with no tile wrapped around the row's title. | sheet 08 backup rows, 10 module rows |
| 5 | **The shell owns the page header** (hamburger + page name + the page's own controls), the page keeps its local title out of the way, and the page background stays global and outside the theme path. | sheet 01/02/03 headers |

The destructive case gets its own shape: a **danger card** — red edge, red
title, red warning, and only the action it performs. | sheet 08

### 8.2 What the second pass shipped

| Item | Detail |
|---|---|
| **Twelve pages on the card grammar** | 40 cards across the module after this pass (44 today: the third pass added Appearance's MODE and DISPLAY DENSITY cards, and Themes' Crystal-fluid card sits inside a conditional wrapper rather than a card); every band caption converted (Appearance 4, Agent 2, AI 3, Data 4, Demo 6 + 1 danger, Sharing 4, Themes 2, Updates 3, Features 6 module cards). Features keeps 8 bands on purpose: a card there is a *module* and its bands are the wizard-step sub-groups inside it (sheet 10's cap groups). |
| **Hierarchy fixed where it was lying** | 116 containers that were page cards inside page cards are inner tiles now (`card_depth_audit.py --demote`), and 51 tile-in-tile header boxes — a stray bordered panel around every row title — are gone (`flatten_tile_in_tile.py`). |
| **Grammar additions** | `SettingsCardHeaderStyle`, `SettingsCardHeaderNoteStyle` (the header block's air), `SettingsDangerCardTitleStyle`, `SettingsDangerCardNoteStyle` (the danger card's red roles). |
| **Demo reset is a danger card** | It was the third of three identical buttons, one slip away from "Enter demo mode". |
| **Indentation** | 1,034 lines re-indented from the real element depth (`reindent.py`), because several scripted restyles had left the trees unreadable — that is how the nesting errors stayed hidden. `diff -w` against the pre-run tree is empty, so the change is provably whitespace-only. |

### 8.3 Tooling (all under `scripts/settings-rewrite/`)

| Tool | Purpose |
|---|---|
| `groups_to_cards.py` | band caption + recap → card header. Parses the file into an element table first: extents are structural, not "up to the next caption" (that swallowed Themes' visibility wrapper) and not indentation-based (indentation lies here). Refuses to write a file whose tag fingerprint drifts or whose tree stops balancing. |
| `card_depth_audit.py` | reports (and with `--demote`, fixes) page cards nested in page cards. Walks a real tag stack with comments blanked space-for-space, so quoted markup cannot fake a parent. |
| `flatten_tile_in_tile.py` | removes the pure header box — nesting **and** no attribute but its `Style` **and** text-only content. The first draft checked nesting alone and deleted Data's amber status panel and Themes' crystal preview; the binding harness caught both, and those two panels are now permanently out of scope. |
| `reindent.py` | rewrites leading whitespace from the element tree; never touches a line that starts with text. |
| `verify_bindings.py` | every binding, command parameter, `x:Name`, converter, `RelativeSource` and text key that existed before is still there, with intentional removals recorded **per page, once**, with their reason (a duplicate entry is now a hard error: the later dict silently replaced the earlier one). |
| `grammar_usage.py` | reports which grammar styles the pages consume and which are still waiting (47 declared / 30 consumed / 17 waiting after the third pass); `--fail-unused` gates it. |

**Per-page gate before any commit:** `verify_bindings.py` (nothing defused) →
`scripts/xaml-guard.py` (well-formed) → `card_depth_audit.py` (0 nested) →
`scripts/layout-verify/verify_all.sh` (all 13 stages green).

`Tests/Settings/SettingsRedesignTests.cs` locks the rules that no binding check
can see: zero band captions on eleven pages (eight allowed on Features), no
nested page cards (walked on the parsed tree), the Demo danger card, and — new —
Sharing and Themes are in the parse list, which they never were.

### 8.4 Still open (honest list)

1. **The remaining status chips still have no severity source.** The shell chip
   is done (§8.5) and proves the mechanism: the VM supplies a *brush key* and the
   chip resolves it through `BrushKeyConverter`. What is still missing is the
   source, not the mechanism — sheet 08's backup rows ("VERIFIED" / "KEPT") come
   from `BackupSetInfo`, which carries no status field, and sheet 06's Agent
   status is a two-branch string (`KeyStatus`) with no severity. Give those two
   a real state (bool/enum on the model), then they can use the same chip.
2. ~~Segmented controls.~~ **Done** — sheet 02's Light/Dark and sheet 03's
   density are segmented controls now; see §8.5.
3. **Two literal English strings** now sit in card headers: Features' "STEP 9 ·
   TARGET PRICING" and Data's "Company LAN / Wi-Fi Database". Both are
   pre-existing; adding keys means 15 language files plus `scripts/i18n`, so they
   are recorded here rather than machine-translated.
4. **Unused sheet vocabulary** (17 styles today, deliberately kept as the
   design system's reserve — `grammar_usage.py` prints the list):
   `SettingsChipMuted`/`Danger` pairs plus `SettingsChipGreen*`'s siblings,
   `SettingsStatusDotStyle`, `SettingsMetricValue/LabelStyle`,
   `SettingsPrimary/SecondaryButtonStyle`, `SettingsActionBarStyle`,
   `SettingsInteractiveTileStyle`, `SettingsSuccessTileStyle`,
   `SettingsBareTileStyle`, `SettingsFlatCardStyle`, `SettingsFieldValueStyle`,
   `SettingsIconButtonStyle`, `SettingsPageSubtitleStyle`.
5. **Runtime verification is the owner's**: there is no `dotnet` in the agents'
   environment, so everything above is verified statically (well-formed XAML,
   zero lost references, zero nested cards, token-only grammar) and the visual
   result needs one build in Visual Studio.

### 8.5 Third pass — the two sheet elements that were still missing

| Item | Detail |
|---|---|
| **Segmented control, honestly** | The grammar had `SettingsSegmentStyle` (a `ToggleButton`) and `SettingsSegmentTrackStyle` (a `Border`) waiting as *design-system reserve*. They could not drive a control: WPF gives every `ToggleButton` its own `IsChecked`, so four densities would need four bools plus four commands and nothing would uncheck the others. Replaced with a `ListBox` pair — `SettingsSegmentItemStyle` (`ListBoxItem`: accent fill on `IsSelected`, hover, disabled) inside `SettingsSegmentTrackStyle` (the pill track, `UniformGrid Rows="1"` so every label gets the same width, and a template with no `ScrollViewer` so the wheel keeps reaching the page) plus `SettingsSegmentTrackFillStyle` for the full-width variant. One style, N options, keyboard navigation and selection from the selector itself. The `ToggleButton` version was deleted rather than left beside it. |
| **MODE card (sheet 02)** | Nothing in the module bound `IsDarkTheme` — Light/Dark was reachable only from the top bar. `SettingsViewModel.ThemeMode` holds the token the config already persists (`Appearance.Theme` = "Light"/"Dark"), and its setter routes through `IsDarkTheme` so there is still exactly **one** theme path (`ThemePipeline.Rebuild` → persist → audit); re-selecting the active mode is a no-op instead of a rebuild + audit entry. Labels come from the language files via `SelectedValuePath="Tag"`. |
| **DISPLAY DENSITY card (sheet 03)** | The bare `ComboBox` became four data-driven segments over `DisplayDensityOptions` / `DisplayDensity`, so the option list keeps one source. Sheet 03's four names are the VM's own tokens. |
| **Mode chip in the shell header (sheet 02)** | The page header now carries the live mode beside the page name, on all twelve pages. It reads `ThemeModeChipBackgroundKey` / `ThemeModeChipForegroundKey` from the VM and resolves them with `BrushKeyConverter`, so **no colour literal reaches the view** and a theme switch repaints it: neutral pill (`Ios26ValuePillBrush` + `Ios26TextMutedBrush`) in Light, accent-soft (`Ios26AccentSoftBrush` + `Ios26AccentSoftTextBrush`) in Dark. Both badges are language keys (`Settings.ModeBadgeLight` / `ModeBadgeDark`); visibility is `IsDarkTheme` through `BoolToVis` / `InverseBoolToVis`. |
| **Language coverage** | 6 new keys (`Settings.Mode`, `ModeHelp`, `ModeLight`, `ModeDark`, `ModeBadgeLight`, `ModeBadgeDark`) authored in all **15** dictionaries — not English fallbacks: `scripts/i18n/{code}.py` regenerated from `merge-keys.py` → `build.py`, coverage 95.7–98.7 %. |
| **The regression suite was lying** | `Tests/Settings/SettingsRedesignTests.cs` still asserted the deleted tile grammar: 23 of the 28 keys it required existed nowhere, its per-page lock demanded `SettingsTileStyle` / `SettingsGroupHeaderTextStyle` in 12 pages that have neither, it banned `WrapPanel` outright (the correct host for a chip palette, a button cluster and the Features tab strip), and its wallpaper test sliced between two words that had become a comment 73 characters apart. Replaced with card-grammar locks (§8.3), a burn-down lock that names Voice as the last page on the shims, and a tree-walked wallpaper test. |


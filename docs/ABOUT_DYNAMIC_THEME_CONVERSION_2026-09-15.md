# About module — making it fully theme-dynamic without moving a single pixel

**Date:** 2026-09-15
**Scope:** `Views/About/**`, `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml`,
new `Themes/Styles/AppStage.xaml`, new `Themes/Styles/AboutTypeScale.xaml`,
`Services/ThemeManager.Part1.cs`, `Services/ThemeManager.Part2.cs`,
`Services/Theme/VisualThemeEngine.Ios26.cs`, `MainWindow.xaml`,
`Tests/About/AboutModuleAuditTests.cs`.

---

## 1. Where we actually are

### 1.1 What the About page consumes today

`Views/About/AboutIos26Theme.xaml` holds **72 keys**. Measured against the rest
of the repository:

```
Unique About-local keys:                            72
Reachable from Themes/LightTheme.xaml:               0
Reachable from Themes/DarkTheme.xaml:                0
Reachable from Themes/Styles/GlobalDesignSystem.xaml:0
```

Every one of those 72 keys is declared **only** inside `Views/About`, and the
dictionary is merged *locally*:

```
AboutView.xaml:49   <ResourceDictionary Source="AboutStyles.xaml"/>
AboutStyles.xaml:24  <ResourceDictionary Source="AboutIos26Theme.xaml"/>
About/LicenseCard.xaml:55, CrashReportsCard.xaml:27, OverviewSection.xaml:30,
DeveloperSection.xaml:31, ModulesSection.xaml:29, DiagnosticsSection.xaml:25,
SystemSection.xaml:27   →  all merge ../AboutStyles.xaml
```

The only About keys that live in a theme dictionary are the six accent-derived
fallbacks in `SECTION 18` of `LightTheme.xaml` / `DarkTheme.xaml`
(`AboutInteractiveBrush`, `AboutInteractiveHoverBrush`,
`AboutInteractiveBorderBrush`, `AboutInteractiveBorderStrongBrush`,
`AboutCardBorderBrush`, `AboutDividerBrush`), and those six *are* republished
live by `ThemeManager.PublishAboutPalette()` (`Services/ThemeManager.Part1.cs`
L497–640).

**Therefore the module is exactly as dynamic as those six brushes — and nothing
else.** Cards, text, tiles, pills, hairlines, the accent family and the whole
15-step type scale are frozen.

### 1.2 Why it is frozen

Not an accident, and not a bug. The dictionary header records the decision:

> `2026-09-15 owner directive: "I want exactly liquid glass style and every card
> matching the mockups. Ignore the current theme if that's the problem." The
> About module is therefore rendered from THIS fixed palette […] Light / Dark /
> visual family switches in Settings no longer re-tint the About page
> (deliberate, approved).`

So the freeze was a deliberate trade: *mockup fidelity was bought with theme
adaptivity*. The new directive reverses the trade while keeping the mockups —
which is the whole engineering problem below.

### 1.3 The one WPF rule that decides the design

Resource lookup walks **element → parents → `Application.Resources`**. A
`{DynamicResource}` in a view resolves against *that* chain, and a definition
closer to the element **always wins**.

Two consequences that shape everything:

1. A key defined in a locally merged dictionary **shadows** the same key in
   `Application.Resources` — so re-publishing a frozen key from `ThemeManager`
   does nothing while the local definition exists. Relocation is mandatory; a
   runtime patch is not an option.
2. Once a key is declared only at application level, the existing
   `{DynamicResource Ios26…}` markup updates **by itself** on a dictionary swap.
   No `UpdateLayout`, no visual-tree walk, no code-behind.

### 1.4 The theme swap mechanism (verified)

`Services/ThemeManager.Part1.cs` L97 / L103 swap the *whole* theme dictionary
(`Source = .../DarkTheme.xaml` or `.../LightTheme.xaml`) inside
`Application.Current.Resources.MergedDictionaries`. `ThemeKeyParityTests`
already proves both dictionaries declare the same 225 keys, so a key that lives
in both themes is guaranteed to have a defined value after every swap.

---

## 2. The design

### 2.1 Principle: reachability, not re-mapping

The About keys stay **exactly as they are** — same names, same roles, same
markup (`{DynamicResource Ios26CardBrush}`, `{DynamicResource
Ios26FontSizeBody}`, …). What moves is the *values*: out of the locally merged
About dictionary and into the theme layer.

That single change is what converts the module, because:

* the consumer XAML does not change at all → the approved design can't drift
  through a rename or a restructure;
* the light column of the theme dictionaries keeps the **mockup literals
  verbatim** → default appearance is byte-identical;
* the dark column is new, so a Dark switch now restyles the module instead of
  leaving a light island;
* density already has a live publisher (`ThemeManager.Part2.cs` L170–215) which
  the type scale can join.

This is the same indirection the repo already uses for the About *glass* layer:
`VisualThemeEngine.Ios26.SeedNeutralAboutTokens()` (L352–366) copies
`Surface.*` values into `AboutGlass*` keys on every non-iOS26 apply. We are
applying an established pattern, not inventing one.

### 2.2 Layer map after the conversion

| Layer | File (merged from `App.xaml`) | Swapped by | Contents |
|---|---|---|---|
| Theme | `Themes/LightTheme.xaml` ⇄ `Themes/DarkTheme.xaml` | `ThemeManager` (whole-dictionary swap) | **25 About palette brushes** (light = mockup literals, dark = mapped) + the six accent fallbacks |
| Accent | `ThemeManager.PublishAboutPalette()` | accent / family apply | `Liquid*`, `AboutInteractive*`, `AboutCardBorderBrush`, `AboutDividerBrush` |
| Density | `Themes/Styles/AboutTypeScale.xaml` + `ThemeManager.PublishDensity()` | density profile | **15 whole-pixel `Ios26FontSize*` tokens** |
| Stage (deliberately **not** dynamic) | `Themes/Styles/AppStage.xaml` | nothing, ever | pastel canvas + 4 orb colours |
| Structure (never themed) | `Views/About/AboutIos26Theme.xaml` | — | radii, shadow, and every style/template |

### 2.3 What becomes dynamic, key by key

**Palette (25 brushes) — moved to the theme dictionaries.**
`Ios26CardBrush`, `Ios26CardBorderBrush`, `Ios26RailBrush`, `Ios26TileBrush`,
`Ios26TileBorderBrush`, `Ios26HairlineBrush`, `Ios26TrackBrush`,
`Ios26HoverBrush`, `Ios26TextTitleBrush`, `Ios26TextBodyBrush`,
`Ios26TextSecondaryBrush`, `Ios26TextMutedBrush`, `Ios26TextFaintBrush`,
`Ios26OnAccentBrush`, `Ios26OnAccentChipBrush`, `Ios26AccentBrush`,
`Ios26AccentDeepBrush`, `Ios26AccentSoftBrush`, `Ios26AccentSoftTextBrush`,
`Ios26SuccessBrush`, `Ios26SuccessSoftBrush`, `Ios26ValuePillBrush`,
`Ios26SearchBrush`, `Ios26NumberBrush`, `Ios26DangerDotBrush`.

The light column is the current literal, unchanged. The dark column is derived
by role from the dark theme's own semantics:

| About key (light) | light value | dark value | dark source role |
|---|---|---|---|
| `Ios26CardBrush` | `#CCFFFFFF` | `#CC17181D` | dark module surface (`#1B1C22` family) |
| `Ios26CardBorderBrush` | `#99FFFFFF` | `#4DFFFFFF` | dark specular edge |
| `Ios26RailBrush` | `#99FFFFFF` | `#B3141519` | dark rail |
| `Ios26TileBrush` | `#FFFFFFFF` | `#FF21222A` | solid tile inside a dark card |
| `Ios26TileBorderBrush` | `#E8EDF3` | `#FF2C2F3A` | tile edge |
| `Ios26HairlineBrush` | `#E2E8F0` | `#FF262A34` | dark `DividerBrush` |
| `Ios26TrackBrush` | `#E2E8F0` | `#FF262A34` | dark track |
| `Ios26HoverBrush` | `#E6ECF3` | `#FF23252E` | dark hover lift |
| `Ios26TextTitleBrush` | `#0F172A` | `#FFF5F5F7` | dark `TextPrimary` |
| `Ios26TextBodyBrush` | `#1E293B` | `#FFE6E8ED` | dark body ink |
| `Ios26TextSecondaryBrush` | `#475569` | `#FFA8B0BE` | dark `TextSecondary` |
| `Ios26TextMutedBrush` | `#64748B` | `#FF8B93A2` | dark muted ink |
| `Ios26TextFaintBrush` | `#94A3B8` | `#FF6E7687` | dark faint ink |
| `Ios26OnAccentBrush` | `#FFFFFFFF` | `#FFFFFFFF` | accent ink is white in both |
| `Ios26OnAccentChipBrush` | `#40FFFFFF` | `#33FFFFFF` | chip wash on accent |
| `Ios26AccentBrush` | `#0369A1` | `#FF38BDF8` | dark `AccentBrush` |
| `Ios26AccentDeepBrush` | `#075985` | `#FF0EA5E9` | dark `LiquidAccentDeep` |
| `Ios26AccentSoftBrush` | `#DBEAFE` | `#FF12324A` | dark `TintBlue` |
| `Ios26AccentSoftTextBrush` | `#1D4ED8` | `#FF7DD3FC` | accent ink on soft |
| `Ios26SuccessBrush` | `#16A34A` | `#FF34D399` | dark `SuccessBrush` |
| `Ios26SuccessSoftBrush` | `#DCFCE7` | `#FF123524` | dark `TintGreen` |
| `Ios26ValuePillBrush` | `#F1F5F9` | `#FF262A34` | pill inside a dark card |
| `Ios26SearchBrush` | `#EDF1F7` | `#FF20232B` | search field |
| `Ios26NumberBrush` | `#93C5FD` | `#FF7DD3FC` | metric ink on dark |
| `Ios26DangerDotBrush` | `#FB7185` | `#FFFB7185` | already the dark danger tone — identical on purpose |

**Type scale (15 tokens) — moved to `Themes/Styles/AboutTypeScale.xaml`.**
The scale is a *size* contract, not a colour contract, so it belongs to neither
theme file; it lives in one app-level dictionary that the density publisher
rewrites in place. Declaring the same 15 doubles twice (once per theme) would
have doubled the surface for drift and added 30 keys to the parity baseline for
no benefit.

Density rule (new, in `AboutDensityTypeScale`):

* **Compact (the default profile) → delta 0.** The scale is exactly the
  approved mockup scale, so the default appearance does not move.
* Delta is looked up per profile and applied **only to sizes ≥ 13 px**;
  micro labels (10/11/12) never grow, so the micro-text hierarchy is stable.
* Every result is rounded to a whole pixel. That is not cosmetic: under
  `TextOptions.TextFormattingMode="Display"` a fractional size is hinted on a
  half-pixel grid and the Bold/SemiBold cuts smear — the exact defect the
  2026-09-15 typography pass removed.

| profile | delta | `CardTitleSm` | `CardTitle` | `SectionTitle` | `H1` | `Metric` | `Stat` | `Display` |
|---|---|---|---|---|---|---|---|---|
| Compact (default) | 0 | 16 | 17 | 18 | 22 | 24 | 28 | 30 |
| Normal | +1 | 17 | 18 | 19 | 23 | 25 | 29 | 31 |
| Comfortable | +2 | 18 | 19 | 20 | 24 | 26 | 30 | 32 |
| Spacious | +2 | 18 | 19 | 20 | 24 | 26 | 30 | 32 |

(`Spacious` is clamped at +2 so the hero display never passes the global
`FontSize.Display` 32 px ceiling.)

### 2.4 What is deliberately **not** converted

| Thing | Why |
|---|---|
| **Page background** (`Ios26CanvasBrush`, `Ios26Orb*Brush`) | Owner directive: *"Background page dont make it dynamic."* It is the anchor of the design, so it stays invariant — and now also becomes the app-wide stage (§3). |
| **Corner radii** (`Ios26Radius*`, 6 keys) | Design geometry, not a theme signal. Global `CardCornerRadius` tokens are different numbers (the mockup calls for 26 px cards / 10 px tiles); mapping them would visibly re-shape every card for no adaptivity gain. |
| **Weights** | Already dynamic — `Weight.Regular/SemiBold/Bold` come from `GlobalDesignSystem.xaml` and are consumed via `{DynamicResource}`. |
| **`Ios26CardShadow`** | The only DropShadow in the module; a single fixed elevation, matching the mockups. Documented, not themed. |
| **Accent *hue* tracking** of the About palette | The mockup accent (`#0369A1`) is deliberately *not* the app accent (`#0F6FA8` light) — wiring About to the accent engine would re-colour the approved mockups. The hook point stays available in `PublishAboutPalette()` if the owner ever wants it; it is a one-line change **plus** a visual review. |

### 2.5 Risk register

| Risk | Mitigation |
|---|---|
| Design drifts on the default (light) appearance | Light column = existing literals, byte for byte. Verified by diff, not by eye. |
| Someone re-freezes the module by adding a local palette value | New *negative* lock: `Views/About/**` may not declare `Ios26` brush/size values (see §4). |
| Two themes drift (key in one, not the other) | Existing `ThemeKeyParityTests` + raised baseline constant. |
| Fractional size sneaks in via the density path | New pure-function test asserts whole pixels and strict ordering at **every** density profile. |
| Local definition shadows the theme key again | Negative lock scans every About XAML file for `x:Key="Ios26…"` declarations outside the allow-list (radii + shadow). |
| Dark-mode aesthetics | The dark column is *data*: it lives in `DarkTheme.xaml`, a tunable table, not in 9 view files. Tuning a colour after looking at the screen cannot regress any lock. |

---

## 3. The global background

Owner directive: *"it would be awesome if you add this background globally
everywhere the one about module is having."*

### 3.1 Finding: the "global" background layers were already there — and empty

* `MainWindow.xaml` L243 already paints the whole window root (chrome +
  modules) with `{DynamicResource ModuleWallpaperBrush}`, and that key's base
  value is **`Transparent`** (`DarkTheme.xaml` L92) — a no-op unless a wallpaper
  scene is active.
* `ModuleBackgroundBrush` is `#00FFFFFF` and has **zero consumers**.
* `AppBackgroundBrush` is the only page-background token actually in use, by
  four views: `LoginWindow.xaml`, `SplashView.xaml`,
  `CuttingWorkCenterView.xaml`, `ProcessWorkCenterView.xaml`. It was
  **theme-swapped at runtime** by three different publishers
  (`ThemeManager.Part1.cs` L232 and L271, plus the iOS 26 global tables), i.e.
  exactly the "dynamic page background" the directive rejects.

### 3.2 Change

1. New `Themes/Styles/AppStage.xaml` (merged before the theme dictionary) holds
   the five stage colours — `Ios26CanvasBrush` + the four orb brushes — moved
   verbatim out of `AboutIos26Theme.xaml`. About keeps consuming the same five
   keys, so the page renders identically; the values simply now have one
   home instead of being reachable only from About.
2. `AppBackgroundBrush` is re-pointed at the stage: a `DrawingBrush` that paints
   the pastel canvas plus the four ambient orbs in one vector brush. Its
   definition is **identical in `LightTheme.xaml` and `DarkTheme.xaml`** and the
   audit lock asserts that (WPF cannot alias a dictionary value, so the markup
   is repeated; the lock is what keeps the repetition honest).
3. The three runtime publishers of `AppBackgroundBrush` are removed, so a
   Light/Dark/family/accent switch cannot re-tint the page background.
   `VisualThemeEngine.Ios26` also drops the key from its global tables — if it
   kept it, selecting the iOS 26 family would repaint the stage pure white.
4. `MainWindow.xaml` gets a stage layer at the **very bottom** of the root grid
   (below the wallpaper layer), so every module sits on the About stage and an
   active user wallpaper still wins because it is painted above.

### 3.3 Consequence to be aware of

The stage is invariant by design, so in Dark theme the page stays the light
pastel stage while cards, tiles and type switch to their dark values — dark
glass over a light stage. That is the literal reading of the two directives
("don't make the background dynamic" + "use the About background everywhere"),
and it is one line to reverse per theme should the owner prefer a dark stage:
the `AppBackgroundBrush` block in `DarkTheme.xaml`, which is what the
invariance lock pins.

---

## 4. Regression protection

Existing locks stay green, and are extended rather than weakened:

| Lock | Change |
|---|---|
| `About_views_do_not_use_fixed_hex_colors_or_literal_corner_radii` | unchanged |
| `About_views_use_the_about_type_scale_and_the_global_weights` | unchanged |
| `About_weight_hierarchy_is_disciplined` | unchanged |
| `About_license_card_field_rows_follow_the_label_value_roles` | unchanged |
| `About_theme_defines_the_type_scale_and_whole_pixel_sizes` | now asserts the scale in **both** theme layers: keys + whole-pixel values in `Themes/Styles/AboutTypeScale.xaml`, and the **absence** of any `Ios26*` size/brush value in `Views/About/AboutIos26Theme.xaml` (the anti-refreeze clause) |
| `Themes_define_about_palette_fallback_keys` | extended from 6 to all 31 theme-owned About keys, in both theme files |
| `ThemeKeyParityTests.The_token_count_matches_the_baseline` | baseline raised by the new keys (the documented way for that number to move) |

New locks:

* `About_palette_and_type_scale_are_theme_owned_not_locally_frozen` —
  `Views/About/**` may not declare `x:Key="Ios26…"` except the six radii and
  the card shadow; an allow-list, so a new frozen value fails the build.
* `App_stage_is_theme_invariant` — the `AppBackgroundBrush` block must be
  byte-identical in both theme files, and the five stage colours must be
  declared exactly once, in `Themes/Styles/AppStage.xaml`.
* `About_type_scale_stays_whole_pixel_at_every_density` — pure function
  (`AboutDensityTypeScale`) over all four profiles: integral values, strict
  ordering, micro sizes untouched, Compact identical to the approved scale.

---

## 5. As-built inventory

| File | Change |
|---|---|
| `Themes/Styles/AppStage.xaml` | **new** — 5 invariant stage colours, merged at application level |
| `Themes/Styles/AboutTypeScale.xaml` | **new** — 15 whole-pixel size tokens, rewritten per density |
| `Services/Theme/AboutDensityTypeScale.cs` | **new** — pure density → ladder function + publish |
| `Themes/LightTheme.xaml` | +25 About palette brushes (mockup literals verbatim); `AppBackgroundBrush` re-pointed at the invariant stage drawing |
| `Themes/DarkTheme.xaml` | +25 About palette brushes (mapped dark column); identical `AppBackgroundBrush` drawing |
| `Views/About/AboutIos26Theme.xaml` | −30 value declarations; now structure only (6 radii, shadow, 12 styles/templates); header rewritten |
| `Views/About/AboutStyles.xaml`, `AboutView.xaml` | header notes corrected (the module is no longer "rendered from its own fixed palette") |
| `App.xaml` | merges the two new application-level dictionaries |
| `MainWindow.xaml` | new stage layer at the bottom of the root grid (below the wallpaper layer) |
| `Services/ThemeManager.Part2.cs` | publishes the About type scale per density profile |
| `Services/ThemeManager.Part1.cs` | removed both per-theme `AppBackgroundBrush` publishes |
| `Services/Theme/VisualThemeEngine.Ios26.cs` | removed `AppBackgroundBrush` from the key list + both global tables |
| `Tests/About/AboutModuleAuditTests.cs` | palette lock 6 → 31 keys; type-scale lock retargeted + vacuity guard; new anti-refreeze lock |
| `Tests/Theme/AppStageAuditTests.cs` | **new** — 6 locks (stage invariance, stage ownership, whole-pixel ladder, mockup parity, micro-label stability, XAML ↔ publisher agreement) |
| `Tests/Theme/ThemeKeyParityTests.cs` | token-count baseline corrected (see below) |

### 5.1 Measured before/after

```
Views/About/AboutIos26Theme.xaml keys (values it declared)   72  →  19  (all structural)
About palette keys reachable from a theme dictionary          0  →  25  (+6 accent = 31)
About value tokens referenced with {StaticResource}           0  →   0   (all live)
Theme dictionary keys (each)                                245  → 270  (25 added to both)
```

### 5.2 Two pre-existing defects surfaced by this work

1. **`ThemeKeyParityTests.BaselineTokenCount` was stale (225 vs 245).**
   The lock `The_token_count_matches_the_baseline` was *failing* on the
   repository before this change. It is invisible in the same way the PR #14
   compile defect was: the test project did not build, so no test in that
   assembly had executed. Corrected to the measured value and raised by the 25
   new keys (270), with the history recorded on the constant itself.
2. **`scripts/design-system-guard.py` reports 2 BLOCKING findings** —
   `Views/About/AboutIos26Theme.xaml` (`Ios26CardStyle`) and
   `Views/Reports/StatementList/StatementListView.xaml`
   (`StatementKpiCardStyle`) are flagged as "new duplicate card systems". Both
   are pre-existing: the About card style is the approved mockup style and is
   *not* among the changes here, and the StatementList file is untouched. The
   About one is worth a decision in a follow-up (the guard wants card chrome to
   come from `SurfacePrimaryStyle`/… via `DynamicResource`; the mockup calls for
   a 26 px radius and a firmer glass fill, which is why it is a separate style).
   Not fixed here because changing it would move the approved design.

---

## 6. Verification

`scripts/layout-verify/verify_all.sh` — end-to-end, 2026-09-15:

```
ok  property order rule / brace-raw-string / using heuristics (self-tests)
ok  well-formed        276 XAML file(s), 0 failing
ok  stray attributes   276 file(s), 0 issues
ok  property el. order 276 file(s), 0 issues
ok  duplicate props    276 file(s), 0 issues
ok  datacontext hijack
ok  brace balance      1475 file(s), 0 failing
ok  missing usings     1475 file(s), 0 missing
ok  C# syntax + xunit (tree-sitter + overload lint)
==  All checks passed
```

Also clean: `xaml-guard` (276 files), `xaml-resource-lint` (0 fatal),
`xaml-xref-audit` (forward 0, duplicate 0), `design-system-guard` (0 new
violations; see §5.2 for the two pre-existing ones).

**Not verifiable here:** there is no `dotnet`/`csc`/`mono` in the authoring
environment, so the C# side is verified by tree-sitter parsing, the repository's
own static linters and review. The compile and the runtime appearance are
confirmed by the owner's Visual Studio build — the two things to look at are
(a) Light theme / default density renders About pixel-identically and (b) Dark
theme now re-skins the cards and type while the stage stays pastel.

### 6.1 What to check by eye after the rebuild

1. **About on Light, Compact (default):** must be indistinguishable from before
   — card fill, hairlines, ink, accent chips, all 15 sizes.
2. **About on Dark:** cards, tiles, pills, hairlines and type switch to the dark
   column; the pastel stage stays (that is the directive, §3.3).
3. **Light ⇄ Dark with About open:** cards and type repaint live; the page
   background does not.
4. **Density Compact → Spacious with About open:** the sizes ≥ 13 px grow by 2 px
   whole pixels, micro labels stay; nothing blurs.
5. **Other pages:** Login, Splash, both WorkCenters and every transparent module
   now sit on the About stage; an active wallpaper still paints above it.

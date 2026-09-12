# Production Module — Senior UI Review (2026-09-12)

> Scope: the eight factory-stage pages (`Views/Production/*View.xaml`) and the
> five shared chrome controls (`Views/Production/Controls/*`). Method: static
> review of XAML resource usage, layout composition and duplication against the
> canonical design-system tokens in `Themes/`. No runtime tooling was available,
> so every finding is a static, file-and-line-referenced observation.

---

## Verdict

The Production module is **structurally sound** — eight genuinely separate pages,
a clean shared `ProductionStageViewModelBase`, correct stage ordering, correct
pane decomposition (OUTER/INNER), live DONE/AutoSum footers, and zero hard-coded
hex colours. It is **not** broken.

The owner's complaint — *"unprofessionally transparent, improper UI placement"* —
is accurate at the **surface-token level**, not the architecture level. A handful
of Production surfaces paint the **translucent/transparent** `ModuleBackgroundBrush`
(or the **fading** `DashAmbientBrush`) where the rest of the module — and the rest
of the product — paints **solid** surfaces. The result is a module that reads as
"glassy" and unfinished in exactly the places that should look like a factory
console: the two cut-list grids, the detail header, and the pipeline bar.

All findings below are token swaps, not redesigns. The module already follows the
`DynamicResource`-only rule; the fixes just point those resources at the
canonical surface tokens.

---

## 1. Transparency findings (HIGH — matches the owner's complaint)

### 1.1 The two cut-list grids are the only transparent cards in the detail overlay
**`Views/Production/Controls/ProductionUnitDetailTemplates.xaml`**

- `OuterGridTemplate` — `Border Background="{DynamicResource ModuleBackgroundBrush}"` (line 12)
- `InnerGridTemplate` — `Border Background="{DynamicResource ModuleBackgroundBrush}"` (line 125)

`ModuleBackgroundBrush` is the *translucent module surface*: `#00000000` /
`#00FFFFFF` in the raw theme dictionaries, and re-published as ≈0.78–0.80 alpha
(0.55 under an OS backdrop) by `VisualThemeEngine.Part1.cs` whenever a wallpaper
or scene is layered. It is designed to let the window scene "read through".

These are the **two most important surfaces on the screen** (the OUTER and INNER
cut lists the operator works in all day), yet they are the **only** transparent
cards in the overlay. Their siblings are solid:

- COMPLETED card → `TintGreenBrush` (solid)
- RECENT ENTRIES card → `SurfaceSecondaryStyle` (solid)
- DISPATCH card → `SurfaceSecondaryStyle` (solid)
- completed-KPI strip → `ModuleListKpiStripStyle` (solid)

**Fix:** change both templates' card background to a solid surface token —
`{StaticResource SurfacePrimaryStyle}` (replace the `Background`/`BorderBrush`/
`BorderThickness`/`CornerRadius` setters wholesale) or, if a slightly raised look
is wanted, `{DynamicResource ElevatedSurfaceBrush}` + `{DynamicResource OutlineBrush}`.
Keep `Padding="12,10"`.

### 1.2 Detail header bar is translucent
**`Views/Production/Controls/ProductionUnitDetailControl.xaml` line 40**

```xml
<Border Grid.Row="0" Background="{DynamicResource ModuleBackgroundBrush}"
        BorderBrush="{DynamicResource DividerBrush}" BorderThickness="0,0,0,1" ...>
```

The overlay's root grid is an **opaque** `WindowBackgroundBrush`, but the header
strip on top of it is the translucent module surface, so the page gradient bleeds
through the title bar. A header that owns a title + BACK button should read as a
solid rail.

**Fix:** `{StaticResource ModuleListShellStyle}` (or `SurfaceSecondaryStyle`) for
this border; keep the bottom hairline.

### 1.3 Pipeline bar paints the dashboard's fading ambient gradient
**`Views/Production/Controls/ProductionPipelineBar.xaml` lines 25–27**

```xml
<Border CornerRadius="{DynamicResource CardCornerRadius}"
        Background="{DynamicResource DashAmbientBrush}" ...>
```

`DashAmbientBrush` is the **dashboard ambient** gradient (`#4D38BDF8 → #0038BDF8`
dark / `#3393C5FD → #1A60A5FA` light) whose far stop is transparent. It exists to
tint the dashboard's background blobs, not to fill a content bar. Painted on the
pipeline bar it makes the bar **dissolve to nothing on the right edge** — the
"washed-out / see-through" look the owner is describing.

The stage chips inside (line 63) are also `ModuleBackgroundBrush` (translucent)
with only a hairline glass border — "ghost" chips.

**Fix:** bar → `{StaticResource ModuleListShellStyle}` or `SurfaceSecondaryStyle`;
chips → a solid tint surface (`{DynamicResource TintIndigoBrush}`-family tint, or
`SurfaceDetailStyle`) with `{DynamicResource CardBorderThickness}` and
`{DynamicResource OutlineBrush}` border. The current-stage `DataTrigger` already
promotes the chip to `ElevatedSurfaceBrush`, which will still read as "active".

### 1.4 Translucent hint + footer bands inside a solid shell
**`Views/Production/CuttingView.xaml` (identical in all 8 stage views)**

- double-click hint band — line 91: `Background="{DynamicResource ModuleBackgroundBrush}"`
- list footer band — line 216: `Background="{DynamicResource ModuleBackgroundBrush}"`

Both sit **inside** `ModuleListShellStyle` (solid). The translucent bands create
two faint stripes inside an otherwise solid panel — an accidental "glass" look.
(These bands are also redundant: the shell already separates the areas.)

**Fix:** drop the `Background` from the hint band (let the shell show through) and
use `{StaticResource SurfaceSecondaryStyle}` or no extra band for the footer strip.

---

## 2. Placement / alignment findings (MEDIUM)

### 2.1 Duplicated stage caption (8 files)
**`Views/Production/CuttingView.xaml` lines 50–67 (all stages)**

The identity header already renders a **bound** ordinal pill
(`{Binding StageOrdinalLabel}`) and then adds a second, **hard-coded** line:

```xml
<TextBlock Text="Stage 1 of 8 · first station — cut panes feed Holes &amp; Cutouts, …"/>
```

Two different sources of the stage number on one screen. The hard-coded copy
*will* drift when the pipeline is reordered. `ProductionStageViewModelBase`
already exposes `StageOrdinalLabel`, `Description` and `IndustryFocus` — the
static text should come from `IndustryFocus` (it is authored per-stage in the VM)
and the ordinal should appear exactly once.

**Fix:** replace the hard-coded "Stage N of 8 · …" string with
`{Binding IndustryFocus}` and drop either the pill or the hard-coded ordinal so
one source wins.

### 2.2 AutoSum footer duplicates the grid's column widths (2 places)
**`Views/Production/CuttingView.xaml` lines 223–240** — the footer `Grid` re-states
`44 / 130 / 1.5* / 80 / 80 / 90 / 150`, which must stay pixel-identical to the
`DataGrid` columns above (lines ~140–200). A change to one (e.g. widening
BALANCE) silently desynchronises the footer and misaligns the AutoSum under the
wrong columns.

**`Views/Production/Controls/ProductionUnitDetailTemplates.xaml`** — the same
8-column set (`28 / 1.4* / 0.62* / 0.62* / 0.66* / 0.62* / 0.78* / 0.72*`) is
hand-written **six times** (header, row, footer × OUTER and INNER).

**Fix:** define the column set once. Options, in increasing effort:
1. a `GridLengthCollection` (or a small `ColumnWidths` helper) referenced by a
   `SharedSizeGroup` on each column definition, or
2. a reusable `CutListColumnDefinitions` style/fragment in the template
   dictionary. Either removes the most likely class of future "misaligned footer"
   regressions.

### 2.3 Single-glass grid is a magic fixed width
**`Views/Production/Controls/ProductionUnitDetailControl.xaml` line 179**

```xml
<Border Grid.Column="0" Grid.ColumnSpan="3" Width="820" HorizontalAlignment="Center"
        Visibility="{Binding IsSingleGlass, ...}">
```

`Width="820"` ignores window width and the `MaxWidth="1280"` content column. On a
narrow host the single-glass grid can overflow the scroll viewport.

**Fix:** cap it relative to the container (`MaxWidth="820"` + `Width="Auto"` +
`HorizontalAlignment="Stretch"` inside the existing `MaxWidth="1280"` column), or
bind to a shared `SingleGlassMaxWidth` token.

### 2.4 Empty-state overlaps the footer row
**`Views/Production/CuttingView.xaml` lines 246–258**

The empty-state `StackPanel` is `Grid.Row="0"` of the row-3 grid (the same grid
that holds the DataGrid in row 0 and the footer in row 1), centred
`VerticalAlignment="Center"` over the **whole** grid. It also relies on
declaration order to stack above the DataGrid (no `Panel.ZIndex`), which is
fragile.

**Fix:** give the empty state `Grid.Row="0"` with an explicit
`Panel.ZIndex="1"` (or place it over the DataGrid's own cell), and centre it
against the DataGrid area rather than the grid that includes the footer.

---

## 3. Consistency / maintenance findings (LOW)

### 3.1 Hard-coded icon sizes bypass the type-scale tokens
- `CuttingView.xaml` line 43 (`FontSize="26"`) and line 249 (`FontSize="44"`) — ×8 stages
- `ProductionUnitDetailControl.xaml` line 64 (`FontSize="22"`)
- `ProductionPipelineBar.xaml` line 76 (`FontSize="15"`)

The design system ships `TextTitle` (18), `TextHeadline` (22), `TextDisplay` (24),
`TextDisplayLarge` (28) etc. and the rule in `DesignTokens.xaml` is "every
FontSize below 30 DIPs is a `{StaticResource Text…}` token". These four are the
only violations in the module.

**Fix:** map 15→`TextSection`, 22→`TextHeadline`, 26→`TextDisplay`, 44→a glyph
token (or `TextDisplayLarge`/`TextDisplay` as appropriate) so density/type-scale
presets keep driving the chrome.

### 3.2 Eight near-identical 265-line pages
Each stage page is a full copy of the header, pipeline bar, release bar, shell,
DataGrid and footer; only the title/description/ordinal/empty-text differ. The
"separate pages" requirement is satisfied either way, but the shared 90% could
live in a `ProductionStageShell` base control (exposing stage title/description/
columns/empty-text slots) with the eight pages remaining thin, distinct XAML
files. This is a refactor suggestion, not a defect.

### 3.3 Inner grid header labels are hard-coded, outer are stage-aware
`ProductionUnitDetailTemplates.xaml`: OUTER uses `{Binding OuterWHeader}` /
`{Binding OuterHHeader}`; INNER hard-codes `"W2"` / `"H2"` (lines ~150). This is
*correct today* (inner is always W2/H2) but asymmetric with the outer template;
if inner ever needs stage awareness the asymmetry will surprise the next editor.

---

## What is already good (do not regress)

- **No hard-coded colours.** Every `Background`/`Foreground`/`BorderBrush` in the
  module is a `DynamicResource` brush — fully light/dark + accent + wallpaper
  compliant.
- **Separate pages, shared plumbing only** — `ProductionStagePageBase` correctly
  isolates the async-load + scroll-feed plumbing without a shared "god screen".
- **Correct stage order and stage-specific copy** across all eight files
  (1 Cutting → 2 Holes & Cutouts → 3 Polish → 4 Edge Deletion → 5 Mitring →
  6 Tempering → 7 Pairing → 8 Doubling/Lamination).
- **Live DONE + AutoSum footers** with correct column-end totals; status pill
  trigger set (`WAITING UPSTREAM` / `IN PROGRESS` / `COMPLETED`) is clear and
  colour-coded through theme tokens.
- **KPI strip** (`ProductionKpiStrip.xaml`) is fully tokenised and correctly
  composed (`ModuleListKpi*Style`).

---

## Recommended fix order

1. **1.1 + 1.2 + 1.3** — swap the transparent/translucent/fading surfaces to
   solid tokens (grids, detail header, pipeline bar + chips). This is the
   visible "professional" fix and is ~10 line edits across 4 files.
2. **1.4** — remove/restyle the translucent hint + footer bands (8 files,
   mechanical).
3. **2.1** — route the stage caption through `IndustryFocus` (8 files).
4. **2.2** — single-source the column widths (1 templates file + 8 stage views).
5. **3.1** — tokenise the four icon sizes.
6. **2.3 / 2.4 / 3.2 / 3.3** — backlog polish.

Items 1–3 are token swaps and binding changes only; no layout re-architecture is
required, and the `DynamicResource`-only rule stays intact throughout.

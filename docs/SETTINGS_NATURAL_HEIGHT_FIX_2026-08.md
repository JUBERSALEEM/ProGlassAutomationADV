# Settings Natural-Height Fix — Global Wallpaper empty space (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "Why there is so much extra space in global wallpaper section in height?
> Are you blindly fixing"

You were right. I made the wallpaper group full-width, but I hosted the
two cards in a **`UniformGrid Columns="1"`** — which gave it a new bug.

---

## 2. Root cause

A **`UniformGrid` gives every cell the same size**. So when I stacked the
**short** Global Wallpaper card above the **tall** Per-Module Wallpapers
card (17 modules in 3 columns) inside a `UniformGrid Columns="1"`, the grid
forced the short Global card **up to the tall card's height**. That giant
empty region in the Global Wallpaper section was the result.

My previous fix was full-width (correct), but the container choice
introduced the height blow-up. That was not careful enough.

---

## 3. The fix — natural height + full width

- Changed every single-column card host in Settings from
  `UniformGrid Columns="1"` to a **`StackPanel`**.
- A `StackPanel` gives each child its **natural height**, so the short
  Global Wallpaper card is short and the tall Per-Module card is tall.
- Full width is preserved because the tiles already stretch
  (`SettingsFullRowTileStyle` + `HorizontalAlignment=Stretch`), and a
  StackPanel's children fill the available width.

This was applied to **all 10 Settings sections** so no other page has the
same hidden equal-height bug (e.g. a short card stacked over a list).

Multi-column tile grids (Columns=2/3/4/5) stay as `UniformGrid` — those
are rows of evenly-sized cards, where equal cell size is correct.

---

## 4. Verified (no `dotnet` build here)

- All 10 Settings AI/XAML sections parse; no element sets the same property
  twice.
- No section has a `UniformGrid Columns="1"` host anymore (would force equal
  heights); all single-column hosts are `StackPanel`.
- No leftover `WrapPanel` tile host (the earlier right-side empty-space bug).
- 0 model bindings lost.
- Tests updated/added:
  - `No_settings_section_forces_equal_card_heights` — forbids any
    `<UniformGrid Columns="1">`, forcing natural heights.
  - `Settings_tiles_stretch_to_fill_full_width` / `Rewritten_section_uses_the_new_tile_grid`
    now accept a StackPanel host for single-row sections and never a
    WrapPanel.

---

## 5. Notes for you (non-coder summary)

- **Settings → Appearance → Wallpapers** — Global Wallpaper is now a
  compact full-width card (just SET/CLEAR + the Visibility slider), and
  the Per-Module list sits below it in three columns. No more huge empty
  block inside Global Wallpaper.
- The same natural-height fix applied everywhere, so no Settings page
  stretches a short card to match a longer neighbour.

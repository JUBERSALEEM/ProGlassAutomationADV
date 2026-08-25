# Licensing + Appearance Wallpaper Full-Width Fix (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "Licensing still not fixed check that and appearance module wallpaper
> global wallpaper full width then after this per modules wallpaper in
> three section full width cover. Do u understand. Fix both screen"

Two things:
1. **Licensing screen** — still showed the same ugly empty space /
   left-aligned layout.
2. **Appearance → WALLPAPERS** — Global Wallpaper should span the full
   width, and Per-Module Wallpapers should span the full width in three
   columns.

---

## 2. Licensing screen — full width, no empty space

The shared `LicenseActivationView` (used by Settings → License, the
first-launch modal, and the "License required" page) was capped at
`MaxWidth=680`. Inside the ~920px settings content column that left ~240px
of empty space on the right, and its two action buttons were left-aligned.

Fixed:
- **Removed the `MaxWidth=680` cap** — the activation cards now stretch to
  fill the whole settings content column.
- **`APPLY PASTED LICENSE`** and **`BROWSE FOR .LIC FILE…`** now stretch
  across their card (was `Left`).
- Tightened the outer padding so the cards sit flush with the section edge.

The Settings → License page already hosts the activation view full-width.
The same benefit applies to the MainWindow "License required" page, which
drops the same control into a full-width grid row.

---

## 3. Appearance → WALLPAPERS — full width, then three columns

The WALLPAPERS group was a `UniformGrid Columns="2"`, so Global Wallpaper
(a wide tile) only got **half** the width and Per-Module Wallpapers (a
full-row tile) was squeezed beside it.

Fixed:
- The WALLPAPERS group is now **`UniformGrid Columns="1"`**.
- **Global Wallpaper** card uses `SettingsFullRowTileStyle` → spans the
  **full width** (SET / CLEAR on the left, Visibility slider on the right).
- **Per-Module Wallpapers** card also spans the **full width**, with its
  own inner **`UniformGrid Columns="3"`** so the 17 modules list in three
  columns covering the whole width.

So, top-to-bottom: Global Wallpaper (full width) → Per-Module Wallpapers
(full width, three columns).

---

## 4. Verified (no `dotnet` build here)

- All Settings + Licensing XAML parses; no element sets the same property
  twice (XLS0501/MC3024 guard).
- All wallpaper bindings preserved (`SetWallpaperCommand`,
  `ClearWallpaperCommand`, `WallpaperStatus`, `WallpaperOpacity`,
  `ModuleWallpaperRows` + nested `Enabled`/`Label`/`OpacityValue`/…), and
  the `DataContext.SetModuleWallpaperCommand` / `ClearModuleWallpaperCommand`
  handler wiring intact.
- LicenseActivationView code-behind handlers (`PasteApplyButton_Click`,
  `BrowseButton_Click`, `CloseButton_Click`) all still wired.
- Updated `Appearance_wallpapers_are_combined_and_three_column` to also
  lock the full-width group (`Columns="1"` + `SettingsFullRowTileStyle`).

---

## 5. Notes for you (non-coder summary)

- Open **Settings → License** — the activation cards now stretch across
  the full width, and its buttons are full-width; no empty right side.
- Open **Settings → Appearance → Wallpapers** — Global Wallpaper spans the
  whole width, and directly under it the Per-Module Wallpapers list spans
  the whole width in three tidy columns.

# Principles

Owner is an **estimator**. The product is a **real glass workflow** (quote → job → cut plan → station DONE → delivery), not a billion-dollar ERP demo.

## Do

- One UI. Same card, same pill, same type tokens, same accent/tints everywhere.
- Upgrade existing screens. Transparent **page skin** so wallpaper/scene shows; **cards** sit on that skin.
- Settings drive the look: blur 1–100, frost colour plate, opacity, radius, density, accent, font.
- Keep calculations, `{Binding}` paths, EF, and station workflow untouched in UI phases.
- Read the XAML before editing. No blind find-replace of business markup.

## Do not

- Clone industrial MES (racks, hold/scrap, furnace mixers, scan kiosks, pairing boards) unless asked.
- Put `BlurEffect` on a card that contains text (WPF blurs **children**, not the backdrop).
- Paint the whole module with opaque `SurfacePrimaryBrush` (white card on white page).
- Invent a fourth theme family. DS + Appearance is the live path.
- Hard-code `#FFFFFF`, `14`, `CornerRadius="8"` when a token exists.

## Hierarchy (every module)

1. **Skin** — wallpaper / scene (`ModuleWallpaperBrush`), optionally blurred.
2. **Page** — `Background="Transparent"` so the skin is visible.
3. **Main card** — `ModuleListShellStyle` / `SurfacePrimaryStyle` (frosted fill).
4. **Nested cards / KPIs** — `SurfaceSecondaryStyle`.
5. **Chips / facts** — `SurfaceDetailStyle` or tints (`TintBlueBrush` …).
6. **Actions** — `PrimaryPillButton` / `OutlinedPillButton` / `SuccessPillButton` / `DangerButton`.

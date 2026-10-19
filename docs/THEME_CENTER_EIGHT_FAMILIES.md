# ProGlass Eight-Family Theme Center

The selectable theme contract is exactly eight visual families, each supporting Light and Dark:

1. **Bento Grid** — modular panels, strong grouping, clear information hierarchy.
2. **Spatial / Immersive** — atmospheric depth and layered surfaces; no parallax dependency.
3. **Swiss International** — editorial typography, precise grid, restrained red accent, compact corners.
4. **Holographic / Iridescent** — prismatic accent edges on readable surfaces; no full-screen shader or continuous animation.
5. **Neumorphism** — shallow raised/inset relief with explicit focus and selection outlines.
6. **Claymorphism** — sculpted solid surfaces and soft shadows; no backdrop blur.
7. **Liquid Glass** — near-clear panes, luminous edges, and bounded translucency/blur.
8. **Glassmorphism** — frosted layered surfaces with stronger opacity than Liquid Glass.

## WPF

- The dedicated Settings → Themes page uses VisualThemeOptions as the canonical eight-item picker.
- ProGlassThemeEngine.ParseFamily / PersistFamily round-trip the eight persisted family identities.
- VisualThemeEngine.EightThemes.cs paints the material layer after the existing base painter; both full family rebuilds and module navigation apply it.
- Light/Dark remains independent for all eight selectable themes. The old Crystal Fluid identity is not a selectable ninth family; its material tuning controls are available for Liquid Glass.
- BuiltinWallpaperScenes.All mirrors the eight-item picker, and ThemeRegistryParityTests enforces registry parity.

## Next.js

- /[locale]/themes is the website Theme Center.
- appearanceFamilies is the selectable registry; the boot script and isAppearanceFamily validate persisted values.
- eight-theme-engine.css applies family-specific semantic tokens after the shared and legacy material stylesheets.
- Theme previews are CSS miniatures, not downloaded screenshots. The active family and Light/Dark/System mode update the existing global theme store.
- Legacy stored "crystal" values migrate to the Liquid Glass default; the retired alias is not displayed as an option.

## Performance and accessibility

- Backdrop blur is limited to selected Liquid Glass / Glassmorphism surfaces; Clay, Neumorphism, and Swiss use solid/soft-shadow materials.
- Transitions use the shared duration/easing tokens and are disabled by reduced-motion preferences.
- Theme choices remain keyboard-operable and selected states are not communicated by colour alone.
- Before release, run the website typecheck, tests and production build, plus WPF restore/build/test. Then manually verify family switching, Light/Dark, persistence across restart/navigation, settings focus visibility, and responsive layout at mobile widths.

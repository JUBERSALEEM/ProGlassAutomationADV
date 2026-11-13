# Phase 2 — Runtime integration and compatibility adapter

**Status:** In progress — runtime package slot, ERP token adapter, host pipeline hook, and Studio-to-host synchronization are implemented; Windows runtime verification remains open.

## Checklist
- [x] Trace actual theme contract: `ConfigService` -> Settings `SelectedVisualTheme` -> `ThemePipeline` -> `ThemeManager` -> `VisualThemeEngine` -> ProGlass semantic publisher.
- [x] Add Fluent to current `VisualThemeEngine`, the `ProGlassThemeFamily` parser/persister, Light/Dark engine palettes, Settings picker, wallpaper scene catalog, config migration allowlist, and parity tests.
- [x] Define one authoritative app-facing runtime owner: the existing ERP `ThemePipeline`; the imported service owns a single package slot and is synchronized through it.
- [x] Implement a compatibility adapter from imported theme tokens/materials to typed ERP semantic and legacy resource keys.
- [x] Use imported `ThemeService` candidate validation and transactional slot replacement; restore host theme/mode if activation fails.
- [x] Connect Settings selection and persistence; add Fluent migration/allowlist support and synchronize the imported customization snapshot.
- [ ] Confirm selected family and Light/Dark mode render correctly across app modules, settings, dialogs, menus, controls, and popups on Windows.
- [ ] Preserve wallpaper/material, density, contrast, and accessibility overlays where compatible; document any intentionally unsupported combination.
- [ ] Add tests for thread affinity, switch failures, missing keys/wrong token types, restoration, and existing legacy key parity.

## Guardrail
Do not call imported `ThemeService.Initialize` against the existing global dictionaries until resource precedence and semantic mapping are proven. Do not add the standalone remote `App.xaml`/`MainWindow.xaml` or replace the current startup.

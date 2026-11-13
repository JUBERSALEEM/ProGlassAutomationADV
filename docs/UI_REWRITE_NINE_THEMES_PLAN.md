# Nine-theme WPF UI rewrite

## Status
Implementation branch: `ui-rewrite/nine-themes`
Base: `v2.0.15`
Scope: presentation layer only. This document is the implementation contract for the branch.

## Owner requirements
- Rebuild the WPF user interface as a coherent application-wide experience; do not treat this as a sequence of isolated cosmetic patches.
- Provide nine selectable visual design systems, each with Light and Dark modes.
- Use Bento Grid / Modular as the proposed default because it suits dense ERP workflows; the other eight remain selectable.
- Preserve all business logic, calculations, ViewModels, services, repositories, SQLite schemas/data, permissions, licensing, integrations, and existing navigation/workflows.
- Keep all work on this branch. Do not modify `v2.0.15` or the separate theme-reference repository.

## Nine design systems
1. **Bento Grid / Modular** — structured workspace, modular cards, clear KPI hierarchy. Proposed default.
2. **Claymorphism** — soft dimensional surfaces, restrained depth, tactile controls.
3. **Fluent / Acrylic** — Windows-native hierarchy, acrylic used selectively, strong keyboard focus.
4. **Neumorphism** — low-contrast embossed controls with accessible contrast safeguards.
5. **Liquid Glass** — translucent layered surfaces and restrained specular highlights.
6. **Glassmorphism** — translucent cards over controlled backgrounds, legibility first.
7. **Spatial / Immersive** — depth and spatial grouping without compromising dense data entry.
8. **Swiss International** — editorial grid, typography-led hierarchy, minimal ornament.
9. **Holographic / Iridescent** — controlled iridescent accents; never apply effects that obscure tables or form fields.

Every design system must expose both Light and Dark modes. Mode is independent of design-system selection.

## Verified baseline observations
- The application is WPF on .NET 8 with nullable reference types, analyzers, and warnings-as-errors policy.
- `MainWindow.xaml` hosts 31 ViewModel-to-View DataTemplates. These are navigation/content contracts and must be preserved.
- `App.xaml` merges a large ordered set of dictionaries. Several dictionaries depend on earlier dictionaries through `StaticResource`; merge order is build/runtime-sensitive.
- `Themes/Styles/DesignTokens.xaml` already defines semantic surface primitives (Primary, Secondary, Detail) and shared styles.
- The current appearance pipeline includes `ThemePipeline`, `VisualThemeEngine`, `ThemeManager`, `ProGlassThemeEngine`, and `ThemeCompatibilityHub`.
- Legacy brush keys are still dual-written for existing consumers. The compatibility hub and legacy dictionaries are not safe to remove merely because a static scan reports low usage.
- Existing project documentation explicitly warns against removing `VisualThemeEngine` or CrystalFluid resources until reverse references have been proven zero.
- The repository has a large XAML surface (the existing census documents 264 XAML files). A full rewrite therefore requires staged migration and automated checks.

## Architecture decision
Do not introduce a competing theme publisher. The nine design systems must ultimately resolve through the existing semantic token publication path, preserving legacy aliases until a complete reference audit proves they can be retired.

The design-system choice and Light/Dark mode are separate settings:
- Design system: one of the nine stable IDs above.
- Appearance mode: Light or Dark.
- Accent, density, font, wallpaper, and material preferences remain orthogonal settings unless a specific view demonstrates a genuine conflict.

Theme-specific styling must be token-driven and use dynamic resource resolution where runtime switching is required. Do not duplicate business-facing views for each theme. One ViewModel and one view must render under all nine design systems.

## Migration sequence and exit criteria

### Phase 0 — baseline and dependency map
- Inventory shell regions, navigation contracts, resource dictionaries, theme settings persistence, and module XAML.
- Identify runtime-published keys and compatibility aliases.
- Record existing validation scripts/tests and the available CI build matrix.
- No production logic or schema changes.

### Phase 1 — theme contract and previewable foundation
- Define stable theme IDs, Light/Dark variants, semantic token requirements, and fallback behavior.
- Build a small representative shell + component showcase before migrating modules.
- Confirm the selected theme and mode survive restart without changing existing config compatibility.
- Add source-level tests for theme IDs, token completeness, fallback, and preservation of old config values.

### Phase 2 — shell rewrite
- Rebuild the application shell (header, navigation, module workspace, utility overlays) using the new shared components.
- Preserve the existing DataTemplates, commands, popup contracts, keyboard behavior, RTL support, and access visibility.
- Validate all nine systems in both modes.

### Phase 3 — shared component system
- Standardize page headers, toolbars, cards, buttons, inputs, tables, filters, dialogs, empty/loading/error states, and status indicators.
- Preserve print/PDF resources separately from interactive UI resources.
- Add accessibility checks for contrast, focus visibility, scaling, and keyboard navigation.

### Phase 4 — module migration
- Migrate modules in small groups, starting with low-risk read-only pages, then lists/forms, then complex PI and production workflows.
- Preserve bindings, commands, validation, calculations, and view-model ownership.
- Do not change formulas, pricing, VAT, document numbering, persistence, or permissions as part of visual work.

### Phase 5 — verification and release readiness
- Run the Windows .NET build, unit tests, XAML/resource validation, and existing regression scripts.
- Verify all nine themes × Light/Dark, language/RTL behavior, high-DPI scaling, dialogs, printing, and module navigation.
- Compare before/after workflows and record any known gaps.
- Do not merge to the production branch until build/test evidence and owner review are complete.

## Implementation log

### Phase 1 — nine-theme catalogue integration (source changes committed)
- The original application already contained eight selectable visual families; the ninth was missing.
- Added **Fluent / Acrylic** to the existing family enum, persisted identity parsing, normalization, material palette, fallback palette, settings picker, config migration allowlist, and built-in wallpaper scene catalogue.
- The Settings theme selector now presents nine cards in a 3×3 grid and labels the catalogue as nine visual systems.
- Updated theme-registry and Settings source tests to include Fluent / Acrylic.
- Static source checks confirm all nine IDs are present across normalization, painting, persistence, picker, and wallpaper catalogue.
- **Not yet verified:** Windows compilation, unit-test execution, actual rendered Light/Dark screenshots, and live theme switching. No GitHub Actions run was available for this branch at the time of this update.

### Architecture findings that shape the full rewrite
- The existing eight selectable families are currently painted through one shared `ApplyIndustrial` base plus `ApplyEightThemeMaterial`; Liquid Glass retains its separate crystal painter. This gives distinct palettes/material values, but it does **not yet** make each family a complete, independently authored control system.
- The newly registered Fluent / Acrylic family currently has its own palette and scene, but it does **not yet** have a Fluent-specific `FluentSurface` control/template or a true acrylic transient-chrome layer. Those belong in the shared component migration, not as an unsupported claim about the current palette.
- The current `MainWindow.xaml` contains 31 ViewModel DataTemplates, the shell navigation tree, seven Popup declarations, and named elements referenced by `MainWindow.xaml.cs`. These contracts must be carried into the shell rewrite.
- The existing shell currently uses one 260-DIP navigation drawer and a 56-DIP top bar. The module workspace is hosted through `ProfessionalContent`; preserve that content contract while redesigning shell composition.
- The reference V3 repository has separate theme component packages, including a FluentSurface material control. Treat those as design/implementation references only; do not transplant their separate theme engine or assume their app-specific resources exist in ADV.


### Default-theme decision
- Existing config default remains **Liquid Glass** for compatibility.
- **Bento Grid / Modular** remains the recommended new default, but it has not been switched in config because that would change the startup appearance for existing and new installations. Confirm before changing this persisted default.

## Non-negotiable safeguards
- No database migration or schema change for visual redesign.
- No edits to ViewModels/services/repositories unless a minimal presentation integration is essential and separately reviewed.
- No deletion of old theme resources, compatibility aliases, or theme engines without reverse-reference proof and tests.
- No rewriting of print/PDF document appearance as a side effect of screen theming.
- No claim of runtime verification unless a Windows build and application run were actually performed.
- Keep commits scoped and reversible; all commits target `ui-rewrite/nine-themes`.


### Phase 2 — first shell token migration (source changes committed)
- Replaced fixed corner radii on the shell reopen chip, suite badge, read-only banner, license overlay, AI agent surface, and More popup with the existing family-managed geometry tokens.
- Replaced the license activation overlay's hard-coded white fill with the semantic primary surface brush so it follows Light/Dark and the selected family.
- Added a source regression test pinning the shell's use of dynamic geometry and surface tokens.
- This is a deliberately small first shell migration: it preserves all named controls, popup anchors, DataTemplates, commands, and code-behind contracts.
- **Not yet verified:** Windows compilation, test execution, and rendered shell screenshots. The changes are source-level only until a Windows runner validates them.


### Phase 3 — shared rewrite component primitives (source changes committed)
- Added `Themes/Styles/RewriteComponentStyles.xaml` with shared workspace, card, raised-card, inset, toolbar, page-header, section-title, body, and caption primitives.
- Merged the dictionary after `MaterialTokens.xaml`, where its semantic surface and geometry dependencies are already available.
- The styles resolve through the existing runtime theme tokens; they do not create a second theme engine or duplicate module views.
- Added source regression coverage for dictionary registration, component keys, and semantic token usage.
- **Adoption status:** the primitives are available application-wide but have not yet been applied across module screens. The next migration step is to adopt them in a representative module without changing its ViewModel or workflow.
- **Not yet verified:** Windows compilation, test execution, and visual screenshots.


### Phase 4 — first ERP list-surface migration (source changes committed)
- Migrated the shared Studio list-card primitive from its fixed Studio face/edge/radius to `ProGlassRewriteCardStyle`, so every consumer of `Sl.Card` inherits family-managed surface, border, geometry, and elevation tokens.
- Migrated the Inquiry, Specifications, and Sheet Store workspace backgrounds to the active semantic workspace surface.
- Updated the rewrite primitives to use the unique semantic aliases (`SurfaceCardBrush`, `SurfaceElevatedBrush`, `RadiusCard`, `RadiusControl`, `RadiusLarge`) rather than legacy keys shadowed by the list-local Studio dictionary.
- Added regression checks for the shared card inheritance and the three workspace roots.
- Business bindings, commands, data templates, filters, and list workflows were not changed.
- **Not yet verified:** Windows compilation, test execution, and screenshots. The Studio accent/status palette and many inner control treatments remain to be migrated.


### Phase 4b — additional core workspace surfaces (source changes committed)
- Migrated the Proforma Invoice, Sales Report, Diagnostics, and User Center workspace roots from the fixed Indigo Studio canvas to `SurfaceWorkspaceBrush`.
- The Proforma Invoice list state now uses the same theme-managed workspace surface as its surrounding PI experience.
- This migration changes only view-level presentation properties; all module ViewModels, command bindings, pricing/calculation logic, and user-management workflows remain untouched.
- Added source regression coverage for the four workspace roots.
- **Not yet verified:** Windows compilation, test execution, and screenshots. Inner card, table, input, and accent treatments still require migration.


### Verification pipeline — branch-specific Windows CI
- Added `.github/workflows/nine-theme-ui-rewrite.yml` so pushes to this implementation branch run on `windows-latest`, restore the application and test projects, build the WPF app in Release, and execute the full regression suite.
- Pull requests targeting `v2.0.15` also run the same verification; `workflow_dispatch` is available for a manual rerun.
- The existing legacy theme workflow is scoped to a different branch/base and did not run for this rewrite branch. The new workflow closes that coverage gap without changing the production release workflow.


### Windows CI finding and correction
- The first branch-specific Windows run restored both projects successfully, then stopped at XAML compilation: `SettingsThemesSection.xaml` contained invalid `Hnine` property names on WPF elements.
- Corrected every `Hnine` attribute to the WPF `Height` property.
- Aligned source regression assertions with the actual semantic alias names used by the shared component dictionary (`SurfaceCardBrush`, `RadiusCard`, and `RadiusControl`) and narrowed the list-card assertion to the `Sl.Card` style itself.
- Added a regression pin to prevent the invalid `Hnine` attribute from returning.
- The next push triggers a fresh Windows build and full test run; the correction is not considered verified until that run completes.


### Second Windows CI markup correction
- The rerun confirmed the first `Hnine` correction was effective, and both restore stages again passed.
- Compilation then found three `FontWnine` typos in the same theme-picker specimen area. Corrected them to WPF `FontWeight`.
- Strengthened the picker regression guard to reject any remaining malformed `nine=` property suffix in that XAML file, rather than checking only the first typo spelling.
- This push triggers another Windows Release build and test attempt; verification remains pending until CI finishes.


### Shared shell controls — navigation, module tabs, and context menus
- Added family-aware navigation materials to the Professional shell and mapped the selected, hover, focus, border, and muted states to the active nine-theme palette.
- Migrated module tab strips, active/inactive tab states, God View headers, and pill TabItems to the runtime family material resources.
- Migrated shared context-menu surfaces, submenus, item hover/selection states, keylines, and separators to the same semantic family resources.
- Corrected two palette member references caught by the Windows compiler: the palette exposes `Ink` and `MutedInk`, not `Text` and `TextMuted`.
- Added source regression coverage for tab and context-menu theme-resource consumption.
- Windows verification of this correction is pending; the preceding run failed during WPF compilation before tests could execute.


### Shared dialog and tooltip chrome
- Updated the central dialog window surface, inset panels, borders, footer surfaces, and tooltip popup surface to consume the active nine-family card/control material brushes.
- This central migration applies to dialogs built on `ThemedDialogWindow` without editing their code-behind, commands, validation, or business workflows.
- Added source regression coverage for the shared dialog resource contract.
- Windows build and regression verification is pending for the preceding module-tab/context-menu commit; this dialog change is prepared as the next implementation slice.


### Optimization summary status colors
- Removed hard-coded white and pale-red status colors from the optimization summary.
- Optimal-state text now uses the semantic on-accent foreground; low-utilization state uses semantic danger tint and foreground resources.
- Added source regression checks to prevent these fixed colors from returning.

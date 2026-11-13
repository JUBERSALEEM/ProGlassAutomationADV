# Nine-theme engine port: WPF application cross-check

**Audit date:** 2026-11-13  
**Application branch:** `arena/f61d80ff-proglassautomationadv`  
**Application HEAD:** `f9da332bd998000f300abbff26dbf0229beea471` at audit start  
**Upstream source:** `JUBERSALEEM/ProGlassAutomation`, branch `arena/f1e0ac70-proglassautomation`, commit `2ce98567bbe0d0bb7e2ba937725a7ef05f447031`  
**Purpose:** distinguish what was imported, what is wired into the production ERP, and what still needs deliberate adaptation before this can be called a full engine port.

## Executive result

The upstream theme engine and Theme Studio source are present, and startup plus settings-driven theme changes reach the imported runtime. This is **not a complete production port**. The project has two overlapping theme systems: the established ERP appearance engine (ThemeManager/VisualThemeEngine and its resource dictionaries) and the imported nine-package engine. `ImportedThemeRuntime` currently acts as a bridge between them. It transfers a useful but bounded set of token values and adapts app-scoped explicit styles; it does not replace the ERP's styles/templates or give all ERP screens the source Theme Studio's family-specific composition.

**Accurate current classification:** source import complete; runtime entry points wired; cross-engine integration partial; all-screen visual adoption and Windows acceptance incomplete.

## 1. Source comparison: what was fetched

The linked upstream ref resolves to commit `2ce98567bbe0d0bb7e2ba937725a7ef05f447031`. A path-and-byte comparison of the upstream `Theme/` and `step1.ThemeStudio/` trees found:

- **241 source paths; 0 missing** in this clone.
- **6 differing paths:**
  - `Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs` — local runtime additions for package-change notifications, typed resource lookup, and safer package switching/loading.
  - `Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs` — local ERP adapter checks replace/extend the upstream checks.
  - `Theme/step1.d.tests/step1.d.3.verify_sources.py` and `Theme/step1.d.tests/step1.d.6.verify_foundation.py` — local source verification adaptations.
  - `Theme/step1.g.Customization/step1.g.1.CustomizationModel.cs` — whitespace-only difference.
  - `Theme/step1.g.Customization/step1.g.8.IconService.cs` — local semantic ERP icon additions.

The nine package directories each contain both Light and Dark resources, generally about 49–51 styles per family including shared component styles. The installed catalog contains nine identities, so the package/catalog shape represents 18 family/mode choices. The Studio source and package tests are in this checkout; source presence itself does not prove WPF build or runtime success.

## 2. Existing WPF application and theme architecture

| Concern | Existing application owner | Imported engine connection | Assessment |
|---|---|---|---|
| Runtime | Root SDK WPF app, `net8.0-windows`, `UseWPF=true` | Theme and Studio code compile into the root project via SDK default inclusion; theme tests are excluded. | Wired at project-source level; no local Windows build result. |
| Startup/config | `App.StartupCore.cs` loads `Config`, then calls `ImportedThemeRuntime.Initialize(Resources, VisualTheme, Dark)` before constructing the main window. | Initializes package from the persisted host family/mode, loads customization data, appends one package slot. | Wired. Initialization logs and degrades to the pre-existing theme on failure. |
| Existing theme pipeline | `ThemePipeline` calls ThemeManager/VisualThemeEngine, applies the app's semantic design system, then invokes `ImportedThemeRuntime.ApplyFromHost`. | Maps the host family/mode to the imported identity and publishes package values/styles. | Wired, but there are two writers with ordering/precedence to validate. |
| User family selection | Settings `SelectedVisualTheme` writes `App.Config.Appearance.VisualTheme`, saves config, then routes through the legacy appearance pipeline. | Family strings are mapped both directions in `ImportedThemeRuntime`. | Wired for the nine named families; must verify actual selection and restart behavior in WPF. |
| Theme mode | Settings mode uses the existing Light/Dark `AppearanceService` / `ThemePipeline`. | Imported engine receives the resulting mode in `ApplyFromHost`. | Wired in source. |
| Theme Studio | `ImportedThemeStudioWindow` hosts `ThemeStudioView` with `ThemeStudioViewModel(ImportedThemeRuntime.Engine)`. The Studio edits that engine directly. | The engine's `PackageChanged` event publishes back to host and synchronizes family/mode. | Wired, but this is a live shared engine, not an isolated preview engine. Its save/discard and rollback behavior needs end-to-end testing. |
| App-level resource scope | `App.xaml` merges a large legacy ERP resource graph (Light/Dark, ProGlass design system, shared styles, Crystal/other legacy resources, module dictionaries). | Package is appended to `Application.Resources`; bridge writes root overrides. Latest implementation snapshots app-level named styles and adapts those with package styles of the same target type. | Broad app-scope adapter, not a complete resource-scope solution. |

### Theme pipeline order currently present

The full `ThemePipeline.Rebuild` sequence swaps Light/Dark resources, rebuilds the legacy family/module/scene layers, reapplies accent and outline/surface tokens, then calls the imported runtime. Family-only and wallpaper/accent rebuild variants also call the imported runtime. This is a meaningful integration point, but legacy family visuals still execute first and may be visible where the imported bridge does not override the exact resource/style used by a control.

## 3. Imported engine features vs ERP reach

| Engine feature | Exists in imported source | Reaches production ERP | Evidence / remaining distinction |
|---|---:|---:|---|
| Nine family catalog and 18 resource packages | Yes | Yes, selected package is activated | `ThemeCatalog`, resource loaders, `ThemeService`; package load/contract source checks pass. Runtime execution not observed. |
| Atomic one-slot package replacement | Yes | Yes | Runtime switches through `ApplyFromHost`; require WPF test for dictionary precedence and failure rollback. |
| Semantic token catalog | Yes | Partial | `PublishToHost` maps selected color, type, geometry, spacing, status, chart and shell `Pg*` keys. Unmapped host resource names and local/static values remain on the legacy system. |
| Component styles | Yes | Partial | Imported dictionaries have implicit and keyed styles. App-scope explicit host styles with matching target types are now based on their original host style and receive allowlisted package setters. This preserves host templates/interactions but consequently does not apply the full imported component template. |
| Family-specific surfaces/materials | Yes | Mostly Studio/package scope | Clay, Fluent, Glass, Liquid Glass, Neumorphism, and Spatial include family-specific surface types/extras. ERP screens were not broadly converted to consume those family-specific controls; a palette/style-setter adapter is not the same as their surface renderer. |
| Bento layout and analytics | Yes | Primarily Studio/sample scope | `BentoGrid`, `BentoChart`, and `BentoKpi` are in imported source. A scan did not find these as general layouts/charts across production `Views/`; Theme Studio reference/sample views use them. Existing ERP workflows and chart controls remain host-owned. |
| Customization plates, geometry, fonts, presets | Yes | Partial | Studio services compose states into package resources; tokens are bridged. No actual ERP restart/persistence run was performed. Per-family availability differs intentionally (e.g. Fluent/Glass/Spatial do not expose all material controls). |
| Icons | Yes | Partial | Native icon catalog supports semantic kinds and 10 styles. `ImportedIconMark` appears in the Professional shell, Settings shared action styles, and Dashboard resources. Independent MDL2 glyphs, path/image resources and local icons elsewhere are not migrated. |
| Dialogs/popups/context menus | Package styles exist | Not established | Some app-global style types can receive safe setters; view/window-local dictionaries, popup inheritance, and control-specific templates were not runtime inspected. |
| DataGrid | Package implicit styles + validation | Partial | App-level explicit styles can receive presentation setters; business-specific editing/read-only/cell/row templates are intentionally retained. Full family DataGrid visuals are not established. |
| Accessibility/keyboard/focus/high DPI | Source has focus resources and style checks | Not established | No visual/accessibility or keyboard acceptance run. |

## 4. Quantified WPF adoption surface

A static scan across `Views/`, `Themes/`, `Styles/`, and `Controls/` counted **6,148 explicit `StaticResource` style references across 497 distinct keys in 340 XAML files**. These counts show why merging dictionaries alone was insufficient. They are usage counts, not proof that all keys resolve in the same runtime scope.

The current adapter captures named `Style` resources from the **application resource graph before** appending the imported package. For matching package target types it derives a new style from the original ERP style and copies only a limited set of presentation setters (background/foreground/border/font/padding/minimum size/alignment). The selected canonical primary/secondary button aliases retain the imported package style as before.

This strategy deliberately avoids replacing business templates and is safer than a blanket dictionary swap. Its coverage boundary is material:

- Only styles reachable from `Application.Resources` are captured; view/window-local styles are not.
- A host style receives nothing if a package has no implicit style for that exact `TargetType`.
- Properties outside the setter allowlist remain host-owned.
- Local values, direct XAML literals, frozen/static resources, and control templates can override or bypass style setters.
- Package-specific templates/material renderers are intentionally not copied into existing business controls.
- The audit also found four local section dictionaries shadowing `Surface.*.BackgroundBrush` with the old `Appearance.StudioFaceColor`. Those four now reference root-published `Imported.SurfacePrimaryColor` / `Imported.SurfaceSecondaryColor` aliases, and the runtime harness asserts those values through all 18 switches. Other view-local resources have not been comprehensively inventoried.
- No execution has verified style sealing, dynamic-resource expression behavior, resource precedence, startup cost, or rendering after this adaptation.

Consequently this is a stronger shared-style bridge, not proof that all 497 resource keys or all 6,148 use sites now express each theme.

## 5. Studio state, persistence, and customization pipeline

The store is `%LocalAppData%/ProGlassAutomation/bento-customization.json`, versioned JSON with size/type/state validation and atomic temporary-file replacement. It persists family/mode, Bento/Clay/Neumorphism/Liquid customization, shared geometry, fonts, colors/role overrides, icons and preset data. Initialization composes saved customization into a staged package; Studio apply edits the active shared `ThemeService`; save/discard/import/export commands are present in source.

Important integration risks to resolve in runtime testing:

1. Studio applies directly to the same engine used by the production app; confirm that Discard restores all package/host state and that closing the window does not persist unsaved state unexpectedly.
2. `PackageChanged` can synchronously call back through `ThemePipeline`, while Studio selection saves state after `ThemeService.Apply` returns. Exercise re-entrancy and ensure the host pipeline does not overwrite a newer draft/preset.
3. Host family/mode config and the imported customization file are two persistence sources. Verify restart, mismatched values, corrupt JSON, failed writes and rollback keep them consistent.
4. Verify every control advertised by Studio has an applicable family, a visible production effect, an accurate disabled state for unsupported families, and a durable saved value.
5. `IconService.Changed` invalidates loaded `ImportedIconMark` controls; this is event-driven for those controls only, not a migration of all independent ERP icon sources.

## 6. Verification status

**Passed as source/portable checks in this checkout:**

- Source/theme checker: 104 typed tokens × two modes, 1,514 contrast pairs, 125 XML/resource checks (per checker output).
- Nine-package isolation/contract checks and 47 style/target contracts in both modes.
- Fourteen checker mutation regressions.
- Project XML and all theme XAML XML parsing.
- `git diff --check` at the last implementation change.

**Not run / not proven:**

- `dotnet restore`, clean app build, or any C#/WPF/XAML compilation.
- Independent build/run of `Theme/step1.d.tests/step1.d.ThemeTests.csproj`.
- `RuntimeChecks` STA harness (including the 18 app-adapter switches).
- Actual ERP launch, startup error log, effective resource lookup on real controls, or screenshots.
- Studio save/discard/import/export/restart/corrupt-state end-to-end behavior.
- Representative business workflows, DataGrid editing, dialogs/popups, keyboard/accessibility, high-DPI checks.

No `dotnet` or MSBuild executable is present in this environment. Portable source checks must not be reported as runtime acceptance.

## 7. Port plan and definition of complete

The full port should proceed as a measured adapter, not by importing more files (the source inventory is already present) or replacing `App.xaml` dictionaries wholesale.

### Gate 1 — establish a compilable baseline

- On Windows, clean/restore/build the ERP app.
- Independently build and execute the STA theme test project and existing test suite.
- Fix actual compile/XAML/resource errors first. Capture output and preserve the business app startup path.

### Gate 2 — inventory actual resource reach

Generate an evaluated WPF resource/style inventory from the loaded application and a representative set of windows. For each used control, record: scope, key, target type, precedence, static/dynamic resolution, host template contract, imported target resource, and chosen adapter. Include visual tree controls created in code and popup/window resources, not only XAML text.

### Gate 3 — integrate by production surface

1. Shell/navigation and dashboard cards/charts.
2. Search/filter forms, editable ComboBoxes, date pickers, dialogs, context menus and popups.
3. Representative business DataGrids (read-only and editable), preserving validation, sorting, selection, commands and row templates.
4. Settings and reports; then the remaining module inventory.
5. App icon inventory and semantic migration, keeping labels/automation names and documenting intentional exemptions.
6. For every family, one or more real production components must demonstrate its signature material/layout, not merely an altered palette.

Each mapping should have a runtime test and owner-approved visual evidence. Keep the original ERP behavior as the base and add family presentation only when interaction contracts remain proven.

### Gate 4 — full acceptance

- Run all 18 family/mode combinations through the real app and assert the selected package, theme identity, effective mapped resources and stable single package slot.
- Exercise Studio changes, save/discard, restart, import/export, failure recovery and unsupported-family controls.
- Verify representative business workflows and data calculations are unchanged.
- Review normal/high DPI, focus/keyboard, contrast, disabled/selected/validation states, dialogs/popups and charts.
- Record screenshots/results and close all actual defects before calling the engine fully ported.

**Current completion statement:** upstream package/Studio source present = **yes**; ERP startup/settings/pipeline wired = **yes**; broad shared app-style bridge = **partial**; production-screen family-specific adoption = **partial/incomplete**; build/runtime/visual acceptance = **not run**; “100% implemented” = **no**.

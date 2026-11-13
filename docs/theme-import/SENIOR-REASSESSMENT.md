# Senior reassessment: nine-theme WPF integration

**Date:** 2026-11-13

**Scope:** Compare the imported theme engine with its actual reach into the ERP UI. This is a source-level audit, not a claim of successful WPF execution. No implementation changes are proposed or made in this report.

## Executive finding

The earlier completion narrative overstated what has been achieved. The nine source packages, Light/Dark dictionaries, engine, Studio, customization services, and a host adapter exist in the repository. That is not equivalent to delivering nine complete, visibly distinct application themes.

The integration currently has three different levels of coverage:

1. **Package coverage:** all nine package identities and both appearance dictionaries are present and the portable validators accept their contracts.
2. **Host bridge coverage:** selected token values are copied into a limited set of existing ERP resource keys; the package dictionaries are appended to `Application.Resources`.
3. **User-facing surface coverage:** imported icon rendering is wired into the professional shell's sidebar and several shared action-button templates. The ERP's other views still primarily reference existing, explicitly named host styles and resources. Most imported *keyed* component styles are not explicitly referenced by host XAML, and most standalone/application icons are not using the imported icon service.

Therefore, this should be treated as a **partial adapter/prototype pending real surface integration**, not as a completed full-engine import. The user's complaint is valid: the visible result can reasonably look like palette switching with a changed sidebar, while the family-specific control and composition language is not consistently applied throughout ERP screens.

## Evidence from the current tree

### 1. Resource packages are present, but presence does not prove app-wide adoption

- `Theme/Theme*/` contains the nine family packages and their Light/Dark XAML resource graphs.
- A source scan found roughly 49–51 `Style` declarations per family; across all package XAML there are 223 implicit style declarations and 33 distinct explicit style keys (some shared across families).
- The imported service initializes the engine against `Application.Resources`; `ThemeService.Initialize` appends the package as a merged dictionary. The pipeline calls `ApplyFromHost` after ERP theme operations.
- The app already has a large global/local style graph and many controls set `Style="{StaticResource ...}"` explicitly. Such explicit styles do not become imported family styles merely because another dictionary is merged. Implicit styles can affect eligible controls, but local styles, explicit styles, templates, and resource precedence can prevent the imported style from reaching a surface. The actual effective style must be verified at runtime, not inferred from dictionary inclusion.
- A text scan of `Views/**/*.xaml` and `Themes/**/*.xaml` found only seven imported explicit style identifiers referenced there (`Caption`, `Card`, `ChartCard`, `Plate`, `PrimaryButton`, `StatusBadge`, `StatusText`). This is a rough textual measure, not a complete binding/resource-resolution proof; it nevertheless demonstrates that package presence is not broad explicit adoption.

### 2. Host token bridge is intentionally narrow

`Services/Theme/ImportedThemeRuntime.cs::PublishToHost` translates a selected set of background/surface/text/accent/status/chart brushes and font/size/radius values. `CopyBrush` and `CopyTyped` only replace root resources when the key already exists and the existing value has a compatible type. This is safer than indiscriminately replacing dictionaries, but it also means:

- Imported semantics without an explicit host-key mapping remain confined to the imported dictionary.
- Existing controls bound to other host keys, local dictionaries, or static-resource values may not change.
- The bridge does not establish that every module surface (including dialogs, popup windows, reporting, grids, charts, and third-party controls) is themed consistently.
- Colors changing is evidence only that some mapped keys are live, not that the family style system is integrated.

### 3. Icon coverage is materially incomplete

`ImportedIconMark` uses `IconService` recipes and draws imported geometry. It is wired into the shell/sidebar and shared action-button templates in the Meridian shell, dashboard ghost actions, and Settings action buttons; semantic action names now resolve to native imported path marks (search/add/edit/delete/save/document/folder/close/menu/navigation/refresh/print/filter/more/check/warning/calendar/clock/export/import/help). The remaining app still contains many independent icon representations (MDL2 text glyphs, path geometries, packaged image/icon resources, and control-local visuals) that have not been migrated. Thus the accurate claim is **expanded shared-control plus sidebar integration**, not complete application-wide icon coverage.

Icon acceptance must also cover semantic mapping, unknown/missing icon fallback, scaling/stroke at high DPI, contrast/selected/disabled/focus states, and whether the sidebar's existing hard-coded chevron and other glyphs are supposed to remain host-specific.

### 4. Color plates and customization are not demonstrated across real ERP surfaces

The Studio has imported plate/customization controls and the runtime composes saved state into a package. The host bridge publishes only a subset of roles. The available source-level/runtime-check code intends to test a plate overlay, but no WPF runtime test has run. There is no evidence yet that a plate changes representative host cards, chart series, data grids, navigation, and dialogs together, or that custom role edits persist/reload without being overwritten by host rebuilds.

### 5. The earlier cross-comparison records implementation assertions as completed

`docs/theme-import/CROSS-COMPARISON.md` says package styles update app screens and icons update ERP sidebar marks. The latter is narrowly accurate; the former should not be treated as proof of full-screen adoption. It also describes multiple Studio commands and persistence behavior as wired, while the corresponding Windows interaction and restart tests remain unchecked. Its “Work now wired” column needs to be read as a source wiring claim, not as acceptance evidence. The phase checklists correctly still leave Windows build, smoke, persistence, keyboard, and failure testing open.

## Severity-ranked gaps

| Priority | Gap | Consequence | Required evidence before calling it complete |
|---|---|---|---|
| P0 | No verified Windows build; reported compiler errors show source audits have missed basic compile issues | Nothing can be considered shippable until the real WPF compiler/XAML compiler accepts the integration | Clean restore/build of the app and test harness; resolve all errors and review warnings |
| P0 | No actual app visual/runtime matrix for 9 × Light/Dark | Cannot tell whether a selected package becomes the effective ERP theme or whether it merely updates a handful of colors | Automated STA tests plus screenshot/manual inspection of representative real ERP modules for all 18 combinations |
| P1 | Imported controls/styles are not systematically mapped to existing ERP view vocabulary | Family-specific visual language is absent or inconsistent across major screens | Inventory host control/style keys; explicit compatibility layer or deliberate view adoption; coverage report and screenshots |
| P1 | Icon service reaches shell/sidebar and selected shared action templates, not all standalone glyphs/images | Unmigrated app icons do not respond to the imported icon preference | Inventory remaining icon sources and migrate supported semantic icons while preserving labels; add coverage tests |
| P1 | Token bridge only maps selected keys and only when root keys/types match | Some modules and nested/local resource scopes remain on legacy palettes/fonts/radii | Resource-key dependency inventory; module-by-module binding/visual test; bridge coverage and unmapped-token report |
| P1 | Family-specific extras/material effects may rely on custom controls and templates unavailable or unused in host views | Some families collapse to similar palette changes despite distinct package XAML | Per-family screenshots with identified signature components; ensure signature styles have real host consumers |
| P1 | No runtime evidence for plates/custom values, restart persistence, import/export, or rollback | Controls may appear present while changes fail to reach app or survive restart | Windows end-to-end tests for edit/apply/reset/save/restart/corrupt-input/failure recovery |
| P2 | Checker validates package contracts/contrast, not visual fidelity, effective resource lookup, or accessibility of the ERP | Passing portable checks can be mistaken for product acceptance | Separate runtime/UI/accessibility acceptance suite |

## Required engineering approach (before more broad edits)

### Gate A — freeze claims and establish a reproducible baseline

- Keep the existing shell, workflow, and ERP data untouched.
- On a Windows-capable environment, restore and build `ProGlassAutomation.csproj` and the intended theme test harness. Capture exact diagnostics and do not use source tests as a substitute.
- Run the existing application test suite. Confirm the test harness is actually buildable/invokable; the current `Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs` is source code, not evidence of a run.
- Baseline screenshot the current app and identify the selected theme/config, DPI, window size, and language.

### Gate B — define what “full engine” means for this ERP

Create a deliberate map from imported primitives to host surfaces, not a blanket global dictionary replacement:

- Semantic token -> ERP resource key(s), including nested dictionaries and per-window scopes.
- Imported style/template -> matching ERP control vocabulary and target views (cards, buttons, form fields, selection controls, menus, grids, charts, dialogs, navigation).
- Family signature -> at least one visible, real ERP surface where that family’s construction/material language is expressed; palette-only variants do not qualify.
- Icon kind/style -> application icon consumers, with a documented list of intentional exclusions.
- Plate roles -> real surfaces that consume each role, including chart/card treatments.

For every mapping, record ownership, precedence, dynamic update behavior, accessibility impact, and whether the old ERP design is intentionally retained.

### Gate C — implement and validate adapters incrementally

1. Pick representative existing screens and bridge them fully end-to-end first (shell/navigation, dashboard card/charts, complex form/data grid, modal/popup). Prove that template/style/material changes actually appear when switching packages.
2. Make style adoption explicit where host controls currently pin legacy styles; do not silently replace every host style or break local control behavior.
3. Expand icon integration from navigation only after icon inventory and semantic/accessibility behavior are specified.
4. Prove color plate/custom-role changes reach host surfaces and survive process restart.
5. Verify all 18 combinations and all switch/rollback/recovery paths after each adapter tranche.

### Gate D — acceptance and honest status

- Windows build and relevant tests pass.
- All nine families show distinct intended compositions (not only different colors) in representative production screens.
- Both modes, icons, plates, persistence, Studio controls, accessibility and recovery are exercised.
- Visual evidence is reviewed at normal and high DPI; regressions are compared against baseline.
- Update old comparison claims and phase checklist only from measured evidence.

## Current status / decision

**Not complete.** Package import and partial host wiring are present. Broad theme-style coverage, app-wide icon coverage, effective resource resolution, runtime behavior, visual distinction, Windows build, and production acceptance remain unproven or incomplete. No further implementation should be described as “complete” until Gate A establishes a buildable baseline and Gate B approves the explicit host-surface coverage plan.

## Follow-up work after this audit

A focused runtime adapter improvement was made in `Services/Theme/ImportedThemeRuntime.cs`: host-resource discovery uses `Application.TryFindResource` across merged dictionaries, the bridge covers additional color/typography/geometry/status/chart aliases, and selected canonical explicit button styles (`PrimaryButtonStyle`, `Button.Primary`, and secondary-button aliases) are routed to type-compatible styles from the active imported package. TextBox, editable ComboBox, DataGrid, and business-specific templates are deliberately not wholesale-replaced because their focus, validation, editing, and interaction contracts need dedicated compatibility work; their shared dynamic tokens are adapted instead. The Windows runtime harness now seeds merged host resources and asserts token/style publication across all 18 family/mode switches. Portable checks and XML parsing pass, but this harness has **not** run because the sandbox has no .NET/WPF toolchain. Shared button templates now also use semantic `ImportedIconMark` instances and additional native icon geometries; the runtime harness validates each icon kind/style combination. A later source review found the concrete “no visible impact” cause: the main Meridian shell/dashboard used its own `Pg*` palette and geometry resources, several shell references were `StaticResource`, and the bridge did not map imported tokens to those resource keys. The adapter now maps imported surface/text/accent/status colors, radii, padding, typography, and control sizing to the actual `Pg*` keys; shell references that must react at runtime use `DynamicResource`. The STA harness asserts the mapped shell accent/surface/radius/font across all 18 combinations and verifies that a custom plate role changes the real shell accent. This is source/test coverage only until the Windows harness runs; it still does not close family-specific adoption across all host surfaces or migrate every standalone icon representation.

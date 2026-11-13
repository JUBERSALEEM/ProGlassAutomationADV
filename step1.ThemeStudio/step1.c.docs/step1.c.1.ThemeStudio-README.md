> Current mosaic engine: [step1.c.11](step1.c.11.Bento-Grid-Engine.md). Analytics evidence: [step1.c.9](step1.c.9.Reusable-Analytics.md). This guide retains earlier feature history; later reports supersede old coverage claims.

# step1 — Shared theme engine and Theme Studio

**Phase 01 explicitly authorized on 2026-10-20; implementation is present. Windows build/runtime acceptance is blocked in this container, not claimed complete.**

Texts Studio and Colors Studio are shared overlays (`Theme/step1.g.Customization/step1.g.5.FontService.cs`, `step1.g.6.ColorPlateService.cs`) and **separate Settings pages**, not cards hidden inside Theme Studio. They run after the active material engine so Bento, Clay, Fluent and Neumorphism stay separate. Built-in typefaces plus imported TTF/OTF/TTC/OTC (8 MB) write `FontFamilyBody` / heading weight / `FontStyleBody` / `TextDecorationsBody`. Six named plates (Ocean, Forest, Sunset, Slate, Violet, Sand) with stops 50–800 plus role hex overrides write Accent, Focus and Chart tokens only. Contract coverage is 104 semantic roles including `FontStyleBody` and `TextDecorationsBody` in every Light/Dark package. Overflow Expand sizes the flyout to the window, not `PopupMaxHeight`. Read [AGENTS.md](../../AGENTS.md), [naming](../../docs/00.Project-Rules.md), [architecture](../../docs/01.Architecture-Principles.md), and [design policy](../../docs/02.UI-UX-Design-System.md) before editing. See the [current isolation/CI evidence](step1.c.3.Theme-Isolation-and-CI.md) and [earlier Phase 01 checkpoint](step1.c.2.Phase01-Verification.md).

## Scope and operation

Only Bento Grid / Modular is installed. Its Light/Dark selector drives the real application engine, not a sample-only palette. The installed-theme selector has exactly one functional registered choice; no future theme is represented as implemented. Theme identity changes will become visually meaningful only after another theme is separately authorized and implemented.

Studio offers overview/appearance modules, preview name with real validation, apply/reset/progress actions, checkbox/radio/selection samples, typography and status feedback, primary/secondary/elevated/accent surfaces, themed tooltips/context actions, a selected-row list and a live searchable resource reference. A modeless preview window uses the same view model and resources; leave it open during appearance changes. No persistence, theme editor, database or ERP workflow is included.

## Actual organization and file responsibilities

### Root integration (conventional-name exceptions)

| File | Responsibility |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | Application resources/startup; initialize the engine once and open Business Reference; Studio is separate; startup failures are explicitly reported. No SheetStore/database initialization. |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Native themed window containing separate Business Reference/Studio tabs; own/dispose view model and one modeless preview window. |
| `ProGlassAutomation.csproj` | Retained net8.0-windows7.0 WPF target; explicit Compile/Page items and one approved pinned WPF chart package; legacy graph remains removed. |
| `ProGlassAutomation.slnx` | Existing solution plus the Windows verification executable. |
| `app.manifest` | PerMonitorV2 DPI-awareness request; actual scaling still needs Windows inspection. |

AssemblyInfo is unchanged. No code from the unrelated module implementation was copied. Root starter integration was expressly authorized; its previous startup behavior is intentionally replaced, not preserved as a hidden workflow.

### Shared theme implementation — `Theme/step1.a.ThemeEngine/`

| File | Responsibility |
| --- | --- |
| `step1.a.1.ThemeContract.cs` | Immutable 102-token and 47-style/target contract; Brush roles allow supported future gradient treatments without changing screens. |
| `step1.a.2.ThemeService.cs` | Centralized loading, validated replacement of one application dictionary slot and active-state notification. Invalid resources/selection leave the previous dictionary installed. |
| `step1.a.3.ResourceValidation.cs` | Required-key/type checks and finite/nonnegative scalar/opacity constraints. |
| `step1.a.5.ThemeContract-README.md` | Documented meanings/types, resource precedence and extension procedure. |
| `step1.a.6.ThemeCatalog.cs` | Small installed-theme registry; Bento only, no placeholder entries. |

The shared engine is non-visual: metadata, lifecycle, resource replacement, validation and notifications only. It has no XAML, style implementation or default visual layer. The manifest defines required tokens and 46 necessary WPF style targets, not a palette/aesthetic.

### Complete individual theme — `Theme/ThemeBento/`

| File | Responsibility |
| --- | --- |
| `ThemeBento.Light.xaml` / `ThemeBento.Dark.xaml` | Bento’s complete 77-token appearance values/semantic mappings, independent of another theme. |
| `ThemeBento.Readme.md` | Research, visual specification and Bento validation expectations. |
| `step1.e.7.BentoResources.cs` | Bento-owned package composition; fresh dictionaries, own sealed style identities reused across modes, no cached brushes. |
| `step1.e.Components/step1.e.1.Components.xaml` | Compose Bento’s own visual files; not global infrastructure. |
| `step1.e.2.Foundation.xaml` | Bento window/text/card/plate/focus/separator/progress treatment. |
| `step1.e.3.Buttons.xaml` | Bento button templates and normal/hover/pressed/disabled/focus states. |
| `step1.e.4.Inputs.xaml` | Bento input/validation/caret/selection and ComboBox/popup treatment. |
| `step1.e.5.Selections.xaml` | Bento checkbox/radio/list selection templates and states. |
| `step1.e.6.PopupsAndScroll.xaml` | Bento tooltips, leaf context actions, thumb and two scrollbar orientations. |

The old engine a.4 visual branch was removed; step1.e is an internal branch of this feature, not a new theme/feature number. Stable appearance/readme filenames remain naming exceptions. No other theme folder exists. The orchestrator loads one complete package from this factory; future themes supply their own factory and visual implementation, never Bento fallback overrides. No globally installed visual styles remain when changing identity.

### Studio — `step1.ThemeStudio/step1.b.ThemeStudioScreen/`

| Numbered file | Responsibility |
| --- | --- |
| `step1.b.1.ThemeStudioView.xaml` + `.xaml.cs` | Screen composition and width-based module reflow; independent native card lanes: one below 980 content DIPs, two below 1500, three above; shortest-lane packing at breakpoint changes. |
| `step1.b.3.ThemeStudioViewModel.cs` | Actual selection, notifications, validation, preview commands and filtered live tokens; errors shown rather than silently swallowed. |
| `step1.b.4.DelegateCommand.cs` | Minimal native ICommand implementation with command requery. |
| `step1.b.5.TokenRow.cs` | Current resource name/type/value/brush projection, not an independent palette. |
| `step1.b.6.OverviewPanel.xaml` + `.xaml.cs` | Current design identity and contract count. |
| `step1.b.7.AppearancePanel.xaml` + `.xaml.cs` | Shared-resource preview explanation and preview-window action; actual selectors now live in the page header. |
| `step1.b.8.ControlsPanel.xaml` + `.xaml.cs` | Working input/validation/actions/helper toggle/context menu/progress samples. |
| `step1.b.9.TypeAndStatusPanel.xaml` + `.xaml.cs` | Text hierarchy and explicit status examples. |
| `step1.b.10.SurfacesPanel.xaml` + `.xaml.cs` | Surface layering and native list selection. |
| `step1.b.11.TokensPanel.xaml` + `.xaml.cs` | Filterable live contract values. |
| `step1.b.12.PreviewWindow.xaml` + `.xaml.cs` | Modeless shared-resource preview; close action only in code-behind. |

Additional focused files: `step1.b.13.MetricsPanel.xaml` (+ paired code-behind) demonstrates three reference metrics; `step1.b.14.WorklistPanel.xaml` (+ paired code-behind) demonstrates a searchable three-column list, metadata, selected rows, empty state and information/action plate. `step1.b.15.ReferenceSamples.cs` is the view-model partial for immutable local sample rows, filtering/selection/review/help/clear actions. No business services or persistence. `step1.d.6.verify_foundation.py` guards the exact cleanup inventory, project items, startup and reference sections.

Panel code-behind only calls InitializeComponent. Paired views share the same basename plus `.cs` for WPF tooling; b.2 is intentionally unused. Views contain layout and resource references, never concrete theme values.

### Verification — `Theme/step1.d.tests/`

- `step1.d.ThemeTests.csproj`: no-package Windows STA check executable, references the product project.
- `step1.d.1.RuntimeChecks.cs`: existing-window/control/dialog/popup switching, binding diagnostics, live tokens, commands/validation, selection failures and basic layout checks.
- `step1.d.2.ContractChecks.cs`: both appearances, required token/style/target contracts, contrast, missing/wrong/nonnumeric resources, duplicate merged keys and independently mutable candidate dictionaries.
- `step1.d.3.verify_sources.py`: portable XML/contract/resource/content/contrast/hardcoding checks; not compilation.
- `step1.d.5.verify_isolation.py`: portable ownership/composition/style-target/cross-theme/duplicate-key checks (also invoked by the main source checker).
- `step1.d.4.CheckerRegression.py`: ten destructive-regression simulations in temporary copies; original files remain untouched.

Run commands and real outcomes are in the verification record and root README. Windows harness must still be compiled/run; manual inspection is not replaced by assertions.

## Explicit styling/platform boundaries

- No screen-local theme palette, hardcoded theme-owned brushes/dimensions/type/radii, per-card theme conditionals or copied global styles are intentionally introduced. Live token swatches bind to actual resources, not data-authored colors.
- Grid proportions, row placement and the 980-DIP reflow threshold are screen layout behavior, not theme palette values. Normalized path geometry for the checkbox mark is template geometry; its brush/stroke thickness/padding consume tokens.
- The native OS title bar, IME/caret behavior outside defined WPF properties and startup-failure MessageBox remain Windows-owned surfaces. We do not claim to recolor OS chrome. The application client area and introduced WPF templates consume resources.
- Standard implicit Label/content presentation inherits window typography/resources; no custom business-specific control library is added. Editable ComboBox, submenu hierarchies, DataGrid (the reference uses a structured ListBox), indeterminate loading animation, high-contrast theme integration and later controls are **not implemented**; future introduction must extend each implemented theme’s own styles and verification, not bypass the contract.
- No runtime performance/DPI/accessibility conformance claims are made before Windows review. High contrast requires further validation/adaptation; no decorative animation is used.

## Adding an authorized future theme

Follow the [typed contract and concrete extension process](../../Theme/step1.a.ThemeEngine/step1.a.5.ThemeContract-README.md). Supply both complete variants, validate and register the stable directory in ThemeCatalog; do not touch Studio/business screens or duplicate controls. Theme names are not feature numbers. Claymorphism cannot start until Bento is reviewed, corrected as needed and explicitly approved. No further phase is authorized automatically.

## Windows CI and runtime boundaries

[windows-wpf.yml](../../cols/ci/windows-wpf.yml) uses windows-2022, official setup-dotnet 8.0.x, actual product/test paths, portable checks, compiled `--contracts-only` checks and a timed STA WPF smoke pass. It uses a CI-only SDK pin to avoid accidentally selecting a preinstalled newer SDK, without altering the repository TFM. No uploads/releases, no continue-on-error and no SDK installation attempts in this container. Native process failures/timeouts fail CI.

The harness uses actual WPF controls with software rendering and tests existing dialogs/popup, template identity, text/caret state, mode replacement, validation, bindings and layout. It is not a human rendered-screen review or physical DPI/accessibility/performance certification. The new workflow has not run: code is uncommitted/unpushed and Actions API listing returned 403. Do not claim Windows checks passed; no commit/push/publication was authorized.

Latest user instruction: no pull request without two separate confirmations. Windows Action YAML could not be published (missing `workflows` permission).

Latest layout specification and evidence: [full-width compact Studio](step1.c.6.Full-Width-Layout.md). Studio opens maximized without a width cap; token browsing has a theme-owned bounded viewport. Cards keep their instances and view-model binding across lane changes.

Latest product-depth assessment: [Bento richness audit](step1.c.7.Bento-Richness-Audit.md). Current implementation is a source-verified foundation, not a completed advanced desktop component system; see the explicit capability/verification gaps before reusing it as a finished visual standard.

## Current split-page revision (supersedes earlier layout/component counts)

See [step1.c.8](step1.c.8.Rich-Bento-Workspace.md) for current exact responsibilities/evidence. MainWindow now hosts native Theme Studio and Business Reference tabs sharing one view model/engine. Studio owns customization, compact preview and tokens; Business Reference owns the sample workflow. b.16 is the settings panel, b.17 typed settings/page-selection logic, b.18 the distinct sample view/reflow. Bento e.8 owns immutable customization and e.9 owns hero/metric/status/tab templates/private geometry. Shared engine remains nonvisual. Current contract82/35, source checks258 contrast/14 mutations;108 compiled settings cases and native UI remain unrun. Earlier paragraphs describe the preceding checkpoint, not current layout acceptance.

## Reusable analytics extension

The current Business Reference opens first and includes `AnalyticsOverview` (b.21), while Studio includes `ChartGallery` (b.20), both using deterministic b.19 datasets. `Theme/step1.f.Analytics/` owns the reusable immutable data, WPF host/adapter and KPI control; owning Bento e.10 supplies complete styles. f.6 documents every new file, dependency and usage; d.8 adds compiled/native assertions. Internal contract validation stays active, but the token dump is optional and collapsed. No sample business total is calculated in the chart renderer.

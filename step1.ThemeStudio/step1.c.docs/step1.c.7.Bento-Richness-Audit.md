# step1.c.7 — Bento richness and engine-depth audit

Date: **2026-10-20**. Audited product revision: **aff2c67**, pushed before this research as requested. This is a research/source audit, not a new visual implementation or acceptance certificate. The latest successful Windows screenshot is **not available**; the supplied Windows image showed a startup exception, not the rendered reference page. That Progress binding fix is in the pushed source but a successful native rerun is still unverified.

## Direct answer

**The current implementation is a functional, deliberately restrained foundation—not yet the full advanced/rich Bento reference the user wants.** That conclusion is supported by component/layout/template source. The exact perceived appearance cannot be judged without a running WPF view in both modes. Reducing whitespace and maximizing the window improve density, not engine maturity or visual richness.

The shared engine should remain small and nonvisual. A richer result requires a more expressive **Bento-owned visual system**, deeper realistic interactions and accessibility/state coverage, better diagnostics, and demonstrated Windows correctness. A larger engine class, more token names, random gradients, blurred glass or an ERP dashboard would not establish that.

## Research method and sources actually inspected

Retrieved source through GitHub API at pinned revisions; no dependency, copied implementation, reference screenshot or browser rendering was imported. Research text was cached outside the repository. Files were examined for the relevant spans, state roles and native platform APIs—not treated as a WPF component library.

| Reference | Inspected evidence | Relevant lesson / native boundary |
| --- | --- | --- |
| [R1 Magic UI Bento implementation](https://github.com/magicuidesign/magicui/blob/efe76fc5755eca1142dd4f74c1d6f0ace2887e3c/apps/www/registry/magicui/bento-grid.tsx) | 109-line component: three-column grid, configurable card spans/background content, icon/heading/description/action hierarchy, separate light/dark treatment, hover transitions. | Bento is a composition of meaningful modules, not only uniformly rounded rectangles. Adapt hierarchy and varied footprints using native WPF layout. Reject CSS, fixed web row heights, hover-only action discovery and blanket animation. |
| [R2 Actual Bento composition](https://github.com/magicuidesign/magicui/blob/efe76fc5755eca1142dd4f74c1d6f0ace2887e3c/apps/www/registry/example/bento-demo.tsx) | 117-line example: 1/2-width compositions, visual previews, calendar/list/file/network modules. | Meaningful visual content differentiates modules. A reference page can use local pattern previews, small sample trend/flow graphics and activity examples without pretending to implement business modules. Do not copy web marquee, blur, crypto/file demo content or animated marketing interactions. |
| [R3 Carbon grid](https://github.com/carbon-design-system/carbon/blob/42d0ecba41c5f4534a18253cb4a7744b5e17baf6/packages/grid/README.md) | 106-line README: grid/row/column responsibilities and breakpoint-specific column spans. | Responsive layout needs intentional hierarchy, not only more columns. Use WPF Grid/StackPanel and actual desired sizes; no Carbon package, Sass, CSS breakpoints or telemetry imported. |
| [R4 Native Windows WinUI button resources](https://github.com/microsoft/microsoft-ui-xaml/blob/09cfc8ff3fbf556166dd813ae9b81646716f54bf/controls/dev/CommonStyles/Button_themeresources.xaml) | 378-line resource file: separate accent/default/subtle normal/pointer-over/pressed/disabled foreground/background/border roles and high-contrast mappings. | Rich desktop quality includes state completeness and readable system accessibility modes. WinUI is **not WPF**: ThemeResource, ThemeDictionaries, WinUI controls/namespaces cannot be pasted into this application. |
| [R5 Native Windows severity feedback resources](https://github.com/microsoft/microsoft-ui-xaml/blob/09cfc8ff3fbf556166dd813ae9b81646716f54bf/controls/dev/InfoBar/InfoBar_themeresources.xaml) | 96-line file: severity-specific icon/background roles, title/message/action/close sizing, independent Light/Default/HighContrast resources. | Mature feedback is a structured interaction with severity, icon/text and optional action/dismissal, not just a colored sentence. Implement an appropriate small native WPF pattern if approved; this does not authorize importing WinUI InfoBar or fake notifications. |
| [R6 Fluent UI button state implementation](https://github.com/microsoft/fluentui/blob/e2642c72390b31645e5766683776199f0ab01ce2/packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts) | 606-line source: primary/secondary/subtle/outline appearances, hover/pressed/focus and forced-color behavior. | Confirms meaningful action hierarchy and complete interaction states. Web-source comparison only; no React/Griffel/Tabster dependency and no browser CSS in WPF. |
| [R7 Actual .NET 8 WPF system settings](https://github.com/dotnet/wpf/blob/v8.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/SystemParameters.cs) | Native public HighContrast (around line 173), ClientAreaAnimation (around line 1186), and settings resource infrastructure. | WPF has real OS accessibility/animation inputs. Use native APIs, notification lifetimes and Bento-owned mappings instead of inventing web media queries. The current product does not yet implement these behaviors. |

Research retrieval correction: Fluent UI uses **master**, not main; the first main revision lookup returned HTTP 422, then the repository default branch was queried and the successful master SHA pinned above. The previously guessed WinUI src/ path was not reused; the actual repository tree established controls/dev paths. No failed lookup is counted as evidence.

## Measured/source-observable visual findings

### 1. The compact layout is currently closer to masonry than expressive Bento

`b.1.ThemeStudioView.xaml/.cs` makes one/two/three equal-width StackPanel lanes. Every card occupies exactly one lane. Height-based placement reduces shared-row gaps but cannot produce a hero spanning two columns, a full-width metric band or a wide working-table module. The previous compacting change traded away asymmetric width hierarchy. R1–R3 show why varied footprints and purposeful visual content matter.

The code redistributes only when the **column count** changes. Width changes within a band, search-result height changes or other content expansion do not rebalance the lanes. Native layout avoids shared-row holes, but perfectly packed/balanced columns are not guaranteed. Root-source DataContext/focus preservation is designed and test-authored, not runtime-proven.

**Recommended direction:** retain full-width/maximized and compact controls; give the feature/identity area a deliberate wide footprint, metrics a compact band, and the working list appropriate width. Use the independent lanes for supporting cards rather than making every module equally prominent. Keep reading/tab order predictable; do not introduce a large speculative layout framework.

### 2. Most top-level surfaces are intentionally almost identical

Inspection of the eight panel roots found **seven Card** styles and **one ElevatedPlate**. Light uses white for both primary and elevated surfaces. Calculated surface-to-surface luminance ratios from the actual dictionaries:

| Relationship | Light | Dark |
| --- | --- | --- |
| Canvas ↔ primary | #F4F5F8 / #FFFFFF: **1.090:1** | #101218 / #1A1D27: **1.113:1** |
| Primary ↔ elevated | #FFFFFF / #FFFFFF: **1.000:1** | #1A1D27 / #2B3040: **1.281:1** |
| Primary ↔ secondary | #FFFFFF / #ECEEF5: **1.159:1** | #1A1D27 / #232735: **1.131:1** |

These are **not text contrast scores or automatic WCAG failures**. Text/state pair checks pass independently. They explain why the Light specification is likely to feel quiet/flat: elevation relies on an edge, not a distinct fill. The hero also uses the compact plate padding, so it is not inherently more spacious than ordinary cards.

**Recommended direction:** deliberate neutral canvas/surface separation, a richer theme-identity/feature treatment, meaningful accent/neutral/status hierarchy and restrained, testable depth. Preserve a genuinely light Light mode; do not answer “too light” by forcing Dark or turning every card into saturated decoration.

### 3. Content is predominantly text/control demonstration

The metrics show token/mode/sample counts, the worklist has four local rows, and supporting cards explain the system. These are legitimate samples, but there is no meaningful graphical pattern preview, icon-led activity grouping or richer interaction scenario. A token counter is evidence of a contract, not proof of a mature design system. R2 uses varied visual previews rather than only repeated text blocks.

**Recommended direction:** a compact local design-review scenario with information hierarchy, selected item details, validation, filter/empty state and actionable feedback. A small native sample trend/flow/structure visual may explain a pattern, clearly labeled illustrative. Do not resurrect SheetStore, fabricate business KPIs or add a database.

## Engine/component capability audit

| Area | Current evidence | Assessment / correction priority |
| --- | --- | --- |
| Visual isolation | Shared engine has no XAML; Bento owns both palettes, templates and composition factory. | **Keep.** Richness belongs inside Bento, not a giant shared visual engine. |
| Registration and typed contract | One real catalog registration, 77 typed roles and 29 exact required style targets. Candidate duplicate/missing/type/style checks. | Useful foundation, not evidence that all controls/states are mature. Add only genuinely consumed roles. |
| Replacement and mode lifecycle | Validate then replace one resource slot; styles sealed/reused inside Bento, fresh palette/key dictionaries. | Keep architecture; runtime preservation/leak/stress claims remain unproven. |
| Theme-private visual assets | BentoResources.Collect accepts **only Style** entries. Existing palette source check requires exactly the public token keys. | **Structural richness limitation.** Top-level Geometry/DrawingImage/effect helpers cannot simply be added to current component dictionaries. Before richer visuals, allow a deliberate Bento-private resource path/composition, separating immutable helpers from fresh appearance-sensitive values. The common factory already accepts a whole ResourceDictionary; no shared-engine redesign is inherently required. Never cache mutable brushes across themes. |
| Diagnostics / failure semantics | Theme/context errors and missing key/type messages; Apply notifies subscribers after activation. | **Robustness gap.** A subscriber throwing a caught exception can make the view model report “Appearance unchanged” even though activation already happened. Clarify activation vs notification failure and preserve both activation/rollback diagnostics. No fault-injection proof exists. |
| Buttons | Primary/secondary/subtle/destructive, hover/pressed/disabled/focus triggers. | Good starting coverage. Need a visible state matrix and actual keyboard/disabled/pressed review; some treatment is only an edge change. Rich does not mean every action filled with accent. |
| Inputs | Native TextBox validation, caret/selection, ComboBox and items; checkbox/radio/list states. | Functional basics, not a broad professional forms library. Read-only vs disabled and focus/invalid combinations need explicit review. Editable ComboBox is not implemented. |
| Data presentation | Searchable structured ListBox with metadata/status and selected rows. | It is **not** a sortable/resizable/virtualized DataGrid demonstration. A proper dense table pattern is a separate bounded enhancement, not something to falsely claim complete now. |
| Feedback | Colored status text/plates, inline validation/feedback, determinate progress and a preview window. | Missing a coherent actionable/dismissible severity pattern and loading/error/retry scenarios. Determinate progress template does not implement an indeterminate visual. |
| Menus/popups | Leaf context-menu actions, simple tooltip, noneditable selection popup. | No submenu/checkable/menu-icon/shortcut-column reference coverage. Do not claim a full desktop menu system. Preserve the working leaf use case. |
| Motion | No Storyboard/transition system in Bento component dictionaries. | Deliberately static. Short focus/hover/feedback motion could add polish **after correctness**, respecting OS animation preference. Avoid animated layouts, forever-running previews and changes to shared brushes. |
| High contrast / OS preferences | No product handling of HighContrast or ClientAreaAnimation. | **Accessibility gap**, distinct from adding another theme. OS diagnostics/observation may be nonvisual shared utilities; all actual visual mappings remain Bento-owned. |
| Keyboard, automation, DPI | Automation names, labels, focus styling and PerMonitorV2 manifest exist; authored STA checks. | Not accessibility certification. Lane reparenting/reading order, nested scroll wheel, menu focus, screen readers and real 100/150/200% scaling still require Windows testing. |
| Windows evidence | Portable checks pass; workflow intentionally inactive in cols/ci; no successful current Windows build/rendering result. | **P0 blocker.** Neither “rich” nor “minimal but correct” can be accepted without native execution. |

## Prioritized correction plan — recommendations, not implemented claims

### P0 — establish a working native baseline

1. Run the actual pushed root project on Windows and confirm the Progress binding fix resolves startup. Retain genuine Light/Dark screenshots, exact commit and logs.
2. Run compiled contracts and native STA smoke, including 720/1024/1440/1920/2560 widths, all eight cards, bindings/selection/focus and open popup/dialog switching. Repair any real failures before decoration.
3. The user intentionally placed workflow YAML in **cols/ci/windows-wpf.yml**. It will not run there. Do not silently activate it or create a PR to bypass the double-confirmation rule. Use a suitable Windows environment, or obtain explicit workflow activation/permission approval.

### P1 — rich Bento identity without losing compactness

1. Restore intentional wide/narrow module hierarchy while keeping full-width use; distinguish feature, working-content and supporting areas.
2. Specify both appearance treatments for hero/standard/elevated/inset/status surfaces, edge hierarchy and accent distribution before changing values. Keep measured readable text/focus contrast.
3. Add a small theme-owned icon/visual vocabulary and meaningful local sample content, not decorative stock art. Extend Bento composition to accept its private helpers safely; test private-resource isolation, duplicates and independent candidate mutation.
4. Demonstrate full action/input/status state combinations and honest capability labels, with real local interactions. Correct diagnostics and notification-failure semantics alongside visual upgrades.

### P2 — complete relevant reference behaviors, not a kitchen sink

1. A reusable native dense-table pattern with sorting/selection/empty state if approved; no hidden claim that ListBox already supplies it.
2. Consistent severity feedback with text/icon/action, determinate and optional indeterminate loading, and demonstrable success/error/retry behavior.
3. Native high-contrast and animation-preference handling, visible keyboard journeys, and real accessibility/DPI/performance evidence.
4. Small optional transitions only where they communicate state; retain static/reduced-motion behavior. No glass/blur/neon effects borrowed from another theme.

## Completion criteria for the richer revision

- Visibly distinct feature/working/supporting hierarchy in actual Light and Dark screenshots, not just different text labels on identical boxes.
- Compact/full-width layout with useful wide modules and no unintended clipping/overlap. Document ordering when reflowing.
- Every new visual role/theme-private helper belongs to Bento, with complete candidate validation and no hidden shared visual fallback.
- Interactive states, search/selection/validation/feedback and accessibility behavior are exercised, not only screenshot-ready.
- Correct startup and compiled/native regression evidence precede visual acceptance; no runtime/visual inference from Python checks.
- No future themes, legacy modules, external UI package, new application or speculative plugin architecture.

## What this turn actually delivered

The full-width/compact revision and permanent Git/PR safeguards were pushed first as **aff2c67**. This follow-up delivers the researched audit and a documented next-step proposal only; it does **not** claim to have upgraded all missing capabilities. Existing checks were rerun successfully before delivery: 77 tokens × 2, 76 contrast pairs, 29 style contracts, 11 mutation rejections, clean-foundation/project guards. No Windows render, screenshot, compiled test result, workflow run or PR was created. This audit is also committed and pushed under the standing delivery rule; see its commit in Git history rather than a self-referential embedded hash.

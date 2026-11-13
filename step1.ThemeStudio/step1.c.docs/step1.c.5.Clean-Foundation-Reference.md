# step1.c.5 — Clean V3.0 foundation and canonical reference page

Date: **2026-10-20**. Current local branch: `arena/bd154b4b-proglassautomation`; starting/unchanged HEAD: `57725f3`. Root: `/home/user/ProGlassAutomation`. Origin: `https://github.com/JUBERSALEEM/ProGlassAutomation.git`. Starting working tree was clean. **Source implementation and portable verification supplied; Windows compilation, runtime and rendered polish are not accepted or verified.** This report supersedes historical c.2/c.3 architecture/counts and workflow-placement status.

## Before / after cleanup

The [pre-deletion inventory](step1.c.4.Cleanup-Inventory.md) records every tracked file, its origin/classification, exact proposed deletion and SHA-256 before removal. It was written before deletion in a separate command.

| Category | Before | After |
| --- | --- | --- |
| SheetStore | 40 files: models/DTOs, SQLite schema/repository, cache/export/mediator/services, DI/navigation, view models and dialogs/view | All 40 removed; no persistence or old module required. |
| Old dashboard/shell | Shared/Styles/ShellStyles.xaml with sidebar/dashboard/SheetStore navigation plus empty companion | Both removed; V3 MainWindow/Studio retained. |
| Old converters | Four converters used only by old views or unused; ColorName references SheetStore models | All four removed; V3 does not use them. |
| Empty old MainViewModel | One unreferenced class, not the V3 view model | Removed. |
| Existing root/starter integration | App/MainWindow, AssemblyInfo, project/solution, repository configuration | Preserved. Startup remains V3-only; project comment reconciled. |
| Approved V3 documentation/engine/Bento/Studio/tests | All approved work in starting commit | Preserved/refined; no application recreation. |
| Windows verification workflow | Inactive docs/ci/windows-wpf.yml because of earlier push permission failure | Preserved and restored locally to cols/ci/windows-wpf.yml per latest instruction; not published/run. |

**47 legacy implementation files removed**, not merely excluded. The project had already removed old package references and startup integrations in approved Phase 01; cleanup now removes the dead source itself. No old-exclusive test project or on-disk database was found. Mixed root files were reviewed and retained, not blindly removed. No unresolved Review item remains. No V3 code was copied from the removed implementation. No remotes/history were changed and no commit/push occurred in this task.

## Startup and making Theme Studio visible

App initializes the actual ThemeService, constructs MainWindow and calls Show. MainWindow directly hosts ThemeStudioView; there is no business navigation, SQLite initialization, DI container or legacy dashboard. Bento Light is initial; only genuinely registered Bento is listed. Header controls switch the real application's appearance. The title, V3 reference designation, active mode and selectors are visible in the startup page source.

To see the **actual native WPF page**, use this branch/worktree on supported Windows, open the root project as the startup project or run:

```powershell
dotnet run --project ProGlassAutomation.csproj --configuration Release
```

These are instructions, not evidence that the UI ran here. This Linux browser cannot execute WPF; no remote native-Windows display is available. No browser/AI/static image substitute was created. The remote master branch is not changed by local work; this task expressly prohibits commit/push without fresh authorization.

## Reference-page responsibilities and coverage

| File / owner | Responsibility |
| --- | --- |
| App.xaml/App.xaml.cs | Minimal application resource scope, one theme service, visible startup/error handling. |
| MainWindow.xaml/.cs | Root host and view-model lifetime; single modeless preview lifetime. Existing approved code retained. |
| b.1.ThemeStudioView.xaml/.cs | Canonical title/subtitle/status, actual theme/appearance selectors, maximum content width and eight-module responsive composition. Layout only in code-behind. |
| b.3.ThemeStudioViewModel.cs | Theme selection/notifications/error feedback, live tokens, input validation, preview/progress commands and subscription disposal. |
| b.15.ReferenceSamples.cs | Focused partial view model: immutable local sample rows, search/filter/count/empty feedback, selection/review/help/clear actions. No business service/data store. |
| b.4.DelegateCommand.cs / b.5.TokenRow.cs | Minimal ICommand/requery and actual active-resource projection. |
| b.6.OverviewPanel | Emphasized primary feature, explanatory copy and working primary/secondary actions. |
| b.7.AppearancePanel | Supporting information/metadata about shared-resource preview and open-window action; selectors moved to header. |
| b.13.MetricsPanel | Three distinct reference metrics: live token count, appearance count and sample review-pattern count; clearly not business KPIs. |
| b.8.ControlsPanel | Labeled text/validation, apply/reset/disabled/subtle/destructive examples, checkbox, help, progress, feedback, tooltip and native context menu. Clear only affects preview text and triggers real validation. |
| b.9.TypeAndStatusPanel | Heading/body/caption hierarchy, explicit success/warning/error/information/pending text and semantic status surfaces. |
| b.14.WorklistPanel | Search input, aligned 3:2:2 header/list columns, row metadata/status, selection/hover/focus, no-results feedback, information plate and secondary review action. ListBox pattern, not a new DataGrid skin. |
| b.10.SurfacesPanel | Standard/elevated/accent/nested plate layering and native selection examples. Application background remains visible between cards. |
| b.11.TokensPanel | Searchable semantic names, active brush samples, types/values; no independent palette. |
| b.12.PreviewWindow | Native modeless window using the same active resources. |

Panel code-behind only initializes XAML. Screen layout uses auto/star sizing, wrapping, vertical scrolling, no Canvas coordinates. At 980 content DIPs it switches between 3:2 modular columns and eight stacked cards. Theme-owned PageMaxWidth caps content at 1440 DIPs; window minimum remains 720x600 and initial 1280x900. Different card spans, emphasis and inset surfaces establish hierarchy without heavy shadows. Actual clipping/resizing still requires Windows review.

## Theme architecture and shared components

The shared `Theme/step1.a.ThemeEngine/` remains strictly nonvisual: catalog/metadata, package loading/replacement, notifications, typed validation and clear errors. It has no XAML/template implementation. Each candidate is validated independently before replacing one slot; duplicate keys, missing/wrong types and missing/wrong style targets fail without knowingly installing a partial package.

All Bento visual values/styles/templates remain under `Theme/ThemeBento/`. Light/Dark each provide **77 identical semantic roles/types** with independent palettes. The added PageMaxWidth role is a genuine layout requirement; no speculative theme roles were introduced. The required style contract is **29 targets**, adding SubtleButton and DestructiveButton actually consumed by this page. Buttons reuse the existing native base template/states, not duplicated templates. StatusError/StatusErrorSurface provide destructive semantics; no view-local brushes were added.

BentoResources composes fresh appearance dictionaries and fresh component-key dictionaries referencing Bento-owned sealed style identities, avoiding unnecessary template recreation. No concrete brush/package cache and no global visual fallback. ResourceValidation owns contract/duplicate checks; catalog registers only Bento. Cross-theme/runtime extensibility remains specified and source-checked, not proven by inventing a future theme.

The [Bento specification](../../Theme/ThemeBento/ThemeBento.Readme.md) retains actual Light/Dark colors, type/spacing/radii/edges/state/DPI expectations and reference research, extended with Carbon grid and Fluent action-state source inspection and explicit native WPF adaptations. AGENTS and design-system documentation now make this page the permanent component/layout/reference standard. Future pages may arrange their workflows differently, never clone palettes or invent another visual engine.

## Verification — actual results

| Check | Result |
| --- | --- |
| Source/resource/contrast | **PASS:** 77 typed tokens per appearance, 76 contrast pairings (including pending text), 20 XML/XAML documents, content/resource/property/source checks. |
| Isolation | **PASS:** 29 style targets per complete mode, no engine visuals, duplicate package keys, cross-theme borrowing or sibling-order static dependency. |
| Validator mutations | **PASS:** 11 temporary-copy regressions rejected for intended reasons. Updated duplicate-property fixture for the newly emphasized overview. |
| Clean-foundation guard | **PASS:** all 47 inventoried removals absent, no legacy namespaces/dependencies in active sources, explicit project paths valid, 22 Compile/19 Page items without duplicates, all eight modules/header/startup/workflow present. |
| Workflow configuration | **PASS locally:** YAML syntax/unique keys, triggers, Windows runner, official SDK 8 setup, actual paths, foundation/contract commands, read-only permission/no publication or continue-on-error actions. Not exhaustive Actions/PowerShell execution. |
| Preservation/docs/whitespace | **PASS:** exact deletion set matches inventory plus workflow relocation; all 63 Preserve paths retained (workflow at restored path), 16 Markdown documents/89 local links valid; git diff --check clean; HEAD/branch unchanged. |
| Actual application build | **BLOCKED:** `dotnet build ProGlassAutomation.csproj --configuration Release` exited **127**, `dotnet: command not found`. No SDK/runtime/toolchain installation attempted. |
| Windows Actions access | **BLOCKED:** current `gh run list --branch arena/bd154b4b-proglassautomation --limit 1` returned **HTTP 403 Resource not accessible by integration**. No run ID/result exists for this work. |
| Windows workflow execution | **NOT RUN:** changes are local/uncommitted/unpushed under the user's instruction. Existing connection also previously rejected workflow updates without workflows permission. |
| Compiled tests / native runtime | **NOT RUN:** harness extended for search/empty/reset/review/clear behavior and constant dictionary count during repeated mode changes. Explicit startup/header visibility and module bounds/non-overlap assertions added; existing control/popup/dialog/template/caret/binding/reflow assertions retained. These are authored tests, not passed tests. |
| Screenshots / visual inspection | **UNAVAILABLE:** no genuine WPF rendering or screenshot captured. No mockup, web page or generated image is presented as proof. |

Portable commands actually run:

```sh
python3 Theme/step1.d.tests/step1.d.3.verify_sources.py
python3 Theme/step1.d.tests/step1.d.4.CheckerRegression.py
python3 Theme/step1.d.tests/step1.d.6.verify_foundation.py
```

Workflow: **[cols/ci/windows-wpf.yml](../../cols/ci/windows-wpf.yml)**. Windows 2022, official setup-dotnet@v4 8.0.x, CI-only resolver pin (TFM unchanged), official Python setup, portable guards/mutations, real csproj restore/build (SDK 8 does not read .slnx), separate compiled contracts and bounded STA actual-WPF smoke. Native command errors and timeouts fail the job; always-run status summary cannot hide failure. No release/upload/deployment step.

## Remaining acceptance work / next safe step

After separately authorized synchronization and repaired GitHub workflow/Actions permission, run this workflow for the exact resulting commit, retain real URL/logs and fix actual failures. A green build will not certify visual fidelity. On Windows inspect startup; both palettes/components; menus/open popup/dialog; input/editing/selection preservation; real mouse/keyboard hover/pressed/focus/disabled/validation; 720/1024/1440 widths; 100/150/200% display scale; clipping/overlap; screen reader/high contrast and performance/resource lifetime. Compare to the Bento spec, correct theme-owned components, rebuild and rerun regressions before user acceptance.

No future themes, placeholder directories, unrelated business modules, external UI packages, databases, commits or pushes were introduced. This remains Phase 01, not final visual acceptance.

## Exact current file structure

The following lists every non-Git working-tree file (generated build/cache outputs excluded). The pre-deletion inventory separately lists every removed path.

```text
.gitattributes
cols/ci/windows-wpf.yml
.gitignore
AGENTS.md
App.xaml
App.xaml.cs
AssemblyInfo.cs
MainWindow.xaml
MainWindow.xaml.cs
ProGlassAutomation.csproj
ProGlassAutomation.slnx
README.md
Theme/ThemeBento/ThemeBento.Dark.xaml
Theme/ThemeBento/ThemeBento.Light.xaml
Theme/ThemeBento/ThemeBento.Readme.md
Theme/ThemeBento/step1.e.7.BentoResources.cs
Theme/ThemeBento/step1.e.Components/step1.e.1.Components.xaml
Theme/ThemeBento/step1.e.Components/step1.e.2.Foundation.xaml
Theme/ThemeBento/step1.e.Components/step1.e.3.Buttons.xaml
Theme/ThemeBento/step1.e.Components/step1.e.4.Inputs.xaml
Theme/ThemeBento/step1.e.Components/step1.e.5.Selections.xaml
Theme/ThemeBento/step1.e.Components/step1.e.6.PopupsAndScroll.xaml
Theme/step1.a.ThemeEngine/step1.a.1.ThemeContract.cs
Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs
Theme/step1.a.ThemeEngine/step1.a.3.ResourceValidation.cs
Theme/step1.a.ThemeEngine/step1.a.5.ThemeContract-README.md
Theme/step1.a.ThemeEngine/step1.a.6.ThemeCatalog.cs
Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs
Theme/step1.d.tests/step1.d.2.ContractChecks.cs
Theme/step1.d.tests/step1.d.3.verify_sources.py
Theme/step1.d.tests/step1.d.4.CheckerRegression.py
Theme/step1.d.tests/step1.d.5.verify_isolation.py
Theme/step1.d.tests/step1.d.6.verify_foundation.py
Theme/step1.d.tests/step1.d.ThemeTests.csproj
app.manifest
docs/00.Project-Rules.md
docs/01.Architecture-Principles.md
docs/02.UI-UX-Design-System.md
docs/03.Development-and-Testing.md
docs/04.Project-Roadmap.md
docs/05.Decision-Log.md
docs/06.Feature-Registry.md
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.1.ThemeStudioView.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.1.ThemeStudioView.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.10.SurfacesPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.10.SurfacesPanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.11.TokensPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.11.TokensPanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.12.PreviewWindow.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.12.PreviewWindow.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.13.MetricsPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.13.MetricsPanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.14.WorklistPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.14.WorklistPanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.15.ReferenceSamples.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.3.ThemeStudioViewModel.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.4.DelegateCommand.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.5.TokenRow.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.6.OverviewPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.6.OverviewPanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.7.AppearancePanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.7.AppearancePanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.8.ControlsPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.8.ControlsPanel.xaml.cs
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.9.TypeAndStatusPanel.xaml
step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.9.TypeAndStatusPanel.xaml.cs
step1.ThemeStudio/step1.c.docs/step1.c.1.ThemeStudio-README.md
step1.ThemeStudio/step1.c.docs/step1.c.2.Phase01-Verification.md
step1.ThemeStudio/step1.c.docs/step1.c.3.Theme-Isolation-and-CI.md
step1.ThemeStudio/step1.c.docs/step1.c.4.Cleanup-Inventory.md
step1.ThemeStudio/step1.c.docs/step1.c.5.Clean-Foundation-Reference.md
```

## Startup binding correction — user-provided Windows failure

The user supplied a real Windows exception screenshot: TwoWay/OneWayToSource binding cannot write the read-only Progress property, thrown during MainWindow.Show. This proves the supplied startup failed on that Windows run; it is not a successful runtime verification. RangeBase.Value defaults to two-way binding. ControlsPanel now explicitly sets the progress bar to `Mode=OneWay`, preserving the private view-model setter and command-owned updates. The portable checker enforces this and an eleventh mutation removes the mode to prove rejection. The Windows harness additionally checks binding mode and command-to-displayed-progress updates. Local portable checks pass; corrected Windows startup/harness/rendering have not been rerun here. No commit or push.

Latest user instruction: workflow stored **inactive** at `cols/ci/windows-wpf.yml` so the code can be pushed without workflow-write permission. This supersedes earlier restoration/activation statements. No Windows CI run occurs until it is moved back to `.github/workflows/` with suitable permission; restoration commands are in the file header.

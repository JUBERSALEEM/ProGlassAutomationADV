# step1.c.8 — Rich Bento business style, customization Studio and separate sample

Date: **2026-10-20**. Branch: `arena/bd154b4b-proglassautomation`. Scope: the user explicitly requested web cross-checking and implementation, richer proper Light/Dark business Bento, smaller professional cards, a customization-focused Studio and a separate sample page. This report supersedes c.5–c.7 current-layout/capability descriptions, not their historical evidence. **Implemented in source; native Windows build/runtime/rendered acceptance remains unverified.**

## Web cross-check → concrete design decisions

The research compared actual previously pinned Magic UI Bento, Carbon grid, native WinUI and Fluent source from [c.7](step1.c.7.Bento-Richness-Audit.md) against fresh web-search results, prioritizing official guidance over trend articles:

- Bento variation should communicate priority rather than uniform masonry. The secondary guide's size/hierarchy discussion supports a wide feature plus smaller supporting modules [2](https://www.saasframe.io/blog/designing-bento-grids-that-actually-work-a-2026-practical-guide); actual pinned composition source in c.7 supplies the primary implementation comparison. **Applied:** business metric strip + wide feature/working list and narrower context/form; no equal-column auto-packing algorithm.
- Fluent's stored semantic styles avoid hardcoded per-screen values [2](https://fluent2.microsoft.design/design-tokens). **Applied:** all colors, shape, fonts, density and templates remain Bento-owned; no second preview palette in either page.
- Solid opaque materials are mode-aware and use color/elevation to distinguish regions [6](https://fluent2.microsoft.design/material). Fluent elevation guidance distinguishes surface contexts instead of using one indiscriminate shadow [1](https://fluent2.microsoft.design/elevation). **Applied:** differentiated light canvas/white cards/insets and dark navy steps, branded hero region, small metric/status surfaces; no blanket blur/shadow.
- Carbon explicitly distinguishes light layering from progressively lighter dark layers [10](https://carbondesignsystem.com/guidelines/color/overview/). **Applied:** independent palettes, not inversion. Rich color belongs to feature/action/status roles while working surfaces stay readable.
- Carbon's table guidance supports contextual compact density and matching related content/toolbar sizing [1](https://v10.carbondesignsystem.com/components/data-table/usage/). **Applied as a principle, not a copied component:** real compact/comfortable sizing, short working-list rows, readable metadata and search. The sample remains a native structured ListBox, not a falsely advertised complete DataGrid.

Fresh .NET 8 TabControl source was also inspected: [native selected-content host](https://github.com/dotnet/wpf/blob/v8.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/TabControl.cs) declares PART_SelectedContentHost as ContentPresenter. The Bento tab template preserves this native part and SelectedContent aliasing. No WinUI ThemeResource syntax, CSS grid, web component package, copied business module or generated screenshot was imported.

## Delivered page separation

One existing root WPF application, one ThemeService and one view model. MainWindow hosts a themed native TabControl; startup selects **Theme Studio**. The second tab is **Business Reference**. No second executable, preview engine, app directory or business database.

### Theme Studio — customization workbench

- Compact header and branded live-design summary; button opens the separate reference tab.
- A theme-owned300-DIP settings sidebar at wide widths, with full-width stacked reflow below1040 content DIPs. It does not grow into a huge settings card on a large monitor.
- Real Bento-only registered-theme selector and Light/Dark controls.
- Three curated accent choices: Ocean, Indigo, Teal.
- Compact/Comfortable control and card density.
- Crisp/Soft/Rounded card and control corners.
- Small/Standard/Large body typography, with readable caption floor.
- Reset design restores owner defaults; it does not clear sample text or reset business sample selection.
- Existing input/action/validation/progress/context-menu preview, compact state chips and a bounded searchable live token inspector.
- The controls act on actual application resources, not an isolated swatch. A modeless preview shares the same active package.
- Settings are **session-only**. No disk persistence, JSON settings store, custom arbitrary hex editor or fake advanced toggles.

### Business Reference — separate compact sample page

- Clear workspace title, active appearance and explicit sample-data designation. No customization rail, theme selector or token gallery.
- Three small, colored metric plates; sample counts/capacity/date are illustrative, not backend business facts.
- A wide brand feature with native theme-private trend geometry; the graphic is expressly illustrative, not a fabricated live chart.
- Supporting workspace/context card tied to selected sample item.
- Searchable local work queue with title/owner/date, semantic colored status badges, selection and empty-state feedback.
- Quick-review form with real validation/apply/reset/clear/progress/feedback interactions, shared view-model edits and commands.
- Full-width handoff/surface pattern below the main work area.
- Intentional2:1 wide/narrow layout, stacking below1100 content DIPs. No absolute layout coordinates, fixed-height overflowing cards or reparenting on resize. Native auto/star layout handles both pages.

The sample uses local Atrium/Lobby/Office/Entrance review rows only; no SheetStore, inventory, repository, SQLite or legacy dashboard code was restored.

## Bento Light/Dark and compact defaults

The exact full visual specification is [ThemeBento.Readme.md](../../Theme/ThemeBento/ThemeBento.Readme.md). Key changes:

| Role | Light | Dark |
| --- | --- | --- |
| Canvas | #E9EEF6 | #0D1422 |
| Main card | #FFFFFF | #172238 |
| Inset / elevated | #F1F4FA / #F8FAFF | #1E2D46 / #253650 |
| Text / secondary / metadata | #17243C / #465570 / #52617A | #EFF5FF / #C2CEE3 / #AFBED8 |
| Ocean accent / on accent | #254EDA / #FFFFFF | #9DB9FF / #101C36 |
| Hero region | #142B60 → #304BAC | #10203C → #243C78 |

Default display/title/section/body/caption:28/20/16/13/12 DIPs. Controls32 minimum, comfortable38. Card/plate/page padding12/10/12, card gutter8, item spacing6, section10. Default card/control radius12/6, alternatives4/2 and18/9. Body12/13/14, caption12 and section body+3 are actual typography choices. Foreground and semantic state contrast remains tested in every accent/mode combination. No meaningless shrinking to unreadable labels.

HeroCard, MetricCard, StatusBadge/StatusText and native TabControl/TabItem visuals are theme-owned. Severity is a reusable semantic Tag contract (Success/Warning/Information/Error/Neutral); the view model maps sample business status to severity. Status labels remain explicit. There are **82 semantic token roles** and **35 required style targets**. The private ActivityTrendGeometry is not a public contract role.

## Architecture and exact changed responsibilities

| Path/file | Responsibility |
| --- | --- |
| MainWindow.xaml | Two native themed tabs, selected page bound to actual state. Existing startup/preview lifetime remains unchanged. |
| b.1.ThemeStudioView.xaml/.cs | Studio header, compact fixed settings rail plus flexible previews/token inspector; width reflow only, no palette/reparenting engine. |
| b.16.CustomizationPanel.xaml/.cs | Labeled real settings controls, mode selector, reset and errors. Code-behind only initializes XAML. |
| b.17.Customization.cs | Focused view-model partial: typed choices, immutable pending preference record, page selection/open command, application/reset/error behavior. |
| b.18.BusinessReferenceView.xaml/.cs | Separate business sample composition and wide/narrow reflow. No theme-setting UI or duplicate model/service. |
| b.3.ThemeStudioViewModel.cs | Selection applies owner preferences; subscription refreshes live resources and selection; diagnostic distinction for activated-but-notified failure. |
| b.6/b.7/b.13 | Reworked feature/context/metrics modules; source-local sample copy, actual shared selections and native geometry. |
| b.8/b.9/b.10/b.14/b.15 | Compact form/state/handoff/worklist/typed sample-severity patterns; commands, validation and sample-only boundary retained. |
| ThemeBento.Light/Dark.xaml | Complete independent values,82 matching native types, richer layer/hero/compact settings roles. |
| step1.e.8.BentoCustomization.cs | All curated appearance-sensitive overrides. No mutable global preferences, business service or view-owned palette. |
| step1.e.9.Workspace.xaml | New hero/metric/severity/navigation treatments and private PathGeometry; included by Bento composition only. |
| step1.e.7.BentoResources.cs | Fresh palettes/key dictionaries, cached sealed Style and frozen Geometry only. Reject other component helper types explicitly. |
| ThemeContract.cs | Added actual hero, sidebar and component/navigation requirements. No concrete visual values in shared engine. |
| ThemeService.cs | Generic configure-before-validation callback; retain only the current identity's callback across mode changes; validate/replace complete package. No Bento options, colors or templates here. |
| step1.a.7.ThemeNotificationException.cs | Explicitly distinguish successful activation plus consumer refresh errors from rejected activation. Other subscribers still receive notifications; failures aggregate. |
| d.1.RuntimeChecks.cs | Updated Studio/popup/layout checks and separate-reference navigation/settings/input checks. Authored, not executed. |
| d.2.ContractChecks.cs / d.7.CustomizationChecks.cs |108 appearance/accent/density/shape/type combinations; immutable helper, preference retention, rejected candidate and notification-failure cases. Authored, not executed. |
| d.3/d.4/d.5/d.6 Python tools | All curated palette and hero endpoint contrast, new component targets, private helper regression, separate-page/customization/startup/source guards. Executed. |

Root project remains net8.0-windows7.0 with no product package additions; explicit globs include the new files. No application/project replacement. Shared theme infrastructure remains strictly nonvisual. Bento-specific customization is disabled for other theme identities; a future theme is not forced to use Bento preferences or helper composition. No future registration/folder was created.

## Safe resource/customization lifecycle

1. Start from a fresh complete palette and fresh component-key dictionary.
2. Apply the immutable theme-owned preference snapshot to that candidate, using the **candidate** appearance.
3. Independently validate types/roles/style targets/duplicate keys before replacing the active slot.
4. Only successful activation updates the active callback/identity/mode. Failed customization does not alter the prior package or callback.
5. Sealed styles and frozen Geometry identities can be reused; appearance brushes/candidate dictionaries cannot.
6. Retained callbacks capture preference records, not view models/controls; changing to another identity does not carry a Bento callback across.
7. Notify all subscribers; if consumers fail, report successful activation with a typed notification error instead of falsely saying “unchanged.”

## Actual verification

| Check | Actual result |
| --- | --- |
| Resource/source | **PASS:**82 roles ×2 appearances;23 XML/XAML documents; references/content/properties/class/handler pairs and no screen-local palettes. |
| Contrast | **PASS:**258 pairings across Ocean/Indigo/Teal Light/Dark, metric/status roles and both hero gradient endpoints. Not accessibility certification. |
| Ownership/contracts | **PASS:**35 required style targets, no engine XAML, cross-theme sources, duplicate package keys or sibling static-order dependency. |
| Mutation regressions | **PASS:**14 reason-checked temporary-copy failures, including missing hero, missing private geometry, bad curated-preset contrast and the exact prior Progress binding defect. |
| Foundation/project | **PASS:**47 legacy paths remain absent;27 Compile/22 Page items exist without duplicates; separate Studio/sample and real settings are guarded. |
| Actual application build | **BLOCKED:** `dotnet build ProGlassAutomation.csproj --configuration Release` returned127, `dotnet: command not found`. No SDK/VS/runtime installation attempted. |
|108 compiled customization cases | **NOT RUN:** authored Windows contract executable, not a Python simulation of runtime success. |
| Native tab/popup/dialog/layout tests | **NOT RUN:** authored tests cover defaults/reset, preference retention, existing controls/menus, pages and width changes. |
| Windows CI | **NOT RUN:** workflow intentionally remains inactive at [cols/ci/windows-wpf.yml](../../cols/ci/windows-wpf.yml). No activation, PR or unrelated run claimed. Prior Actions access403/workflow permission blocker remains unresolved. |
| Genuine screenshots / visual comparison | **UNAVAILABLE:** no suitable Windows rendering session. No browser/AI/source mockup is presented as WPF output. |

## Remaining native acceptance work

Run the exact pushed revision on Windows. Verify native tab/content selection and keyboard navigation, both modes and curated settings, settings reset versus preview reset, retained selection/edit/caret, open popup/dialog mode changes, nested token scroll behavior and actual non-overlap/clipping at720/1024/1440/1920/2560 widths. Inspect100/150/200% DPI, screen readers/high contrast, focus/disabled/validation combinations and resource/performance lifetimes. Compare genuine screenshots to the owning theme spec; fix real compiler/runtime/visual issues before final acceptance.

This upgrade does **not** claim a full DataGrid, editable ComboBox, hierarchical menu system, high-contrast adaptation, persisted settings or motion framework. No inappropriate global theme engine, future themes, unrelated module, new UI dependency or database was introduced. Standing user instruction authorizes immediate commit/push after checks; no PR is created without two separate explicit confirmations.

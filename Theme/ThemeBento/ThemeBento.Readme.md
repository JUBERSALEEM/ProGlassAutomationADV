# Bento — rich business design system and customization specification

Current implementation: 2026-10-21. Bento is the **layout** language: uneven mosaic tiles, one gutter, shared radius, hairline layered cards on a tinted canvas. It is not clay, not neumorph, not acrylic. Four themes are registered; Bento stays a flat `Border`. Native Windows appearance/build acceptance remains unverified.

## Research and cross-check

- Actual [Magic UI Bento source](https://github.com/magicuidesign/magicui/blob/efe76fc5755eca1142dd4f74c1d6f0ace2887e3c/apps/www/registry/magicui/bento-grid.tsx) and [composition](https://github.com/magicuidesign/magicui/blob/efe76fc5755eca1142dd4f74c1d6f0ace2887e3c/apps/www/registry/example/bento-demo.tsx): varied spans/priority, self-contained modules and meaningful visual previews. Adopt hierarchy, not CSS, fixed rows, hover-hidden actions or web animation packages.
- [Carbon grid source](https://github.com/carbon-design-system/carbon/blob/42d0ecba41c5f4534a18253cb4a7744b5e17baf6/packages/grid/README.md): intentional breakpoint sizing/spans; adapted with native Grid, not a web grid dependency.
- [Native Windows button resources](https://github.com/microsoft/microsoft-ui-xaml/blob/09cfc8ff3fbf556166dd813ae9b81646716f54bf/controls/dev/CommonStyles/Button_themeresources.xaml), [severity resources](https://github.com/microsoft/microsoft-ui-xaml/blob/09cfc8ff3fbf556166dd813ae9b81646716f54bf/controls/dev/InfoBar/InfoBar_themeresources.xaml), [Fluent button source](https://github.com/microsoft/fluentui/blob/e2642c72390b31645e5766683776199f0ab01ce2/packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts): action/state/severity hierarchy. WinUI/React are not WPF; no foreign XAML/API or packages imported.
- [Official Fluent tokens](https://fluent2.microsoft.design/design-tokens), [solid material](https://fluent2.microsoft.design/material), [elevation](https://fluent2.microsoft.design/elevation), [Carbon color layering](https://carbondesignsystem.com/guidelines/color/overview/) and [table guidance](https://carbondesignsystem.com/components/data-table/usage/) cross-checked through web search. Adapt semantic layers and contextual density, not pixel-for-pixel marketing layouts or blanket shadows.
- Previous [UI/UX Bento guidance](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill/blob/56ea9cf174ed86786187daa9de0e6247cb0747e4/.claude/skills/ui-ux-pro-max/data/styles.csv), [WPF guidance](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill/blob/56ea9cf174ed86786187daa9de0e6247cb0747e4/.claude/skills/ui-ux-pro-max/data/stacks/wpf.csv), [Ponytail workflow](https://github.com/DietrichGebert/ponytail/blob/09c434ab73ca43761c5d8b0c5d341926c2fba710/skills/ponytail/SKILL.md), and Microsoft [DynamicResource](https://github.com/dotnet/docs-desktop/blob/main/dotnet-desktop-guide/wpf/advanced/dynamicresource-markup-extension.md) remain relevant. No old application was copied.

See [current implementation/evidence](../../step1.ThemeStudio/step1.c.docs/step1.c.8.Rich-Bento-Workspace.md) and [preceding capability audit](../../step1.ThemeStudio/step1.c.docs/step1.c.7.Bento-Richness-Audit.md). Web/source research is not rendered WPF proof.

## Light and Dark are deliberately separate

| Role | Light / Ocean | Dark / Ocean |
| --- | --- | --- |
| Canvas | #E9EEF6 | #0D1422 |
| Working card | #FFFFFF | #172238 |
| Inset layer | #F1F4FA | #1E2D46 |
| Elevated layer | #F8FAFF | #253650 |
| Primary / secondary / metadata text | #17243C / #465570 / #52617A | #EFF5FF / #C2CEE3 / #AFBED8 |
| Primary accent / on accent | #254EDA / #FFFFFF | #9DB9FF / #101C36 |
| Accent hover / pressed / soft | #193EBC / #16319B / #E8EFFF | #B8CCFF / #8FAAF1 / #203253 |
| Secondary accent | #006E68 | #73DFD0 |
| Subtle / strong edge | #D3DDEC / #73839D | #33445F / #859BB9 |
| Hero gradient | #142B60 → #304BAC | #10203C → #243C78 |
| Hero text / secondary / graph | #F5F8FF / #D5E2FF / #7CEADB | #F5F8FF / #D5E2FF / #7CEADB |

Status foreground/surface pairs retain independent readable green/amber/red/blue palettes in each mode. Severity always includes text; no status relies only on color. The tinted canvas, contrasting solid cards, lighter Dark elevations, branded feature band and small metric/status plates create depth without expensive blur or heavy shadows. Hero background is a branded region, not the appearance of every card. Curated accent choices affect actions/focus/selection, not the entire neutral palette or fixed hero material.

## Compact business scale

Segoe UI; Consolas for token values; no font download. Default display/title/section/body/caption are 28/20/16/13/12 DIPs. Small/Standard/Large body choices are 12/13/14; section size follows body +3, caption remains12. Native font-metric line height, auto-sized rows, wrapped labels. Default controls32 DIPs minimum; comfortable38. Default card/plate/page padding12/10/12; section spacing10, item6, gutter8. Card/control radii12/6 by default; Crisp4/2 and Rounded18/9 are real alternatives. Borders1 DIP, focus2. Density increases padding as well as target height. No tiny fixed-coordinate dashboard.

## Real customization, owned by Bento

`step1.e.8.BentoCustomization.cs` owns typed immutable session preferences: Ocean/Indigo/Teal accent, Compact/Comfortable density, Crisp/Soft/Rounded shape and Small/Standard/Large typography. There are **108 combinations including both appearances**. Ocean comes from the appearance dictionaries; Indigo/Teal are curated complete accent/hover/pressed/soft/on-accent sets in the owner file. They are not arbitrary unvalidated hex input. Each candidate starts fresh, is customized before validation and replaces one active package. Reset restores default preferences, not sample input data. No file/settings/database persistence added.

A generic active-theme configuration callback in ThemeService retains those choices across appearance changes. No Bento rules live in that service and no callback crosses theme identity. Studio only enables these owner-specific settings for Bento; future themes are not forced to accept them.

## Visual composition and private helpers

`step1.e.Components/` owns Foundation, Buttons, Inputs, Selections, PopupsAndScroll and the new Workspace dictionary. Workspace contains HeroCard, MetricCard, semantic StatusBadge/StatusText, native TabControl/TabItem templates and a private PathGeometry for the explicitly illustrative trend. Geometry is frozen; styles sealed. Mutable brushes and candidate dictionaries are never shared/cached. The nonvisual engine has no visual dictionary or cross-theme fallback. The common contract now validates82 semantic roles and35 style targets, not a shared Bento implementation.

## Two pages, one live design system

- **Theme Studio:** compact300-DIP settings rail (stacks below1040 content DIPs), real appearance/accent/density/shape/type controls, reset, compact input/action/state preview and searchable bounded token inspector. This is the customization workspace.
- **Business Reference:** separate native tab, small3-metric band, wide2:1 feature/context composition, local work queue and quick review, plus a full-width handoff/surface example. Stacks below1100 content DIPs. No customization panels/token gallery. All data is clearly illustrative; it is not a SheetStore/dashboard module and writes no records.
- Both pages remain in the existing maximized root WPF window, use one view model/service and share the actual resource slot. No screen-local palette. No reparenting/masonry algorithm or duplicate engine. A modeless native preview uses the same resources.

## States, bounds and honest limitations

Primary/secondary/subtle/destructive actions retain normal/hover/pressed/disabled/focus behavior. Inputs retain labels, real validation, selection/caret and explicit one-way progress display. Native popup/menu/tooltip/scroll/list states remain. Tab selection has theme-owned hover/selected/focus; it preserves separate content instances while the view model retains edits/settings. Notification failures are now distinguished from rejected activation and other consumers are still notified.

Portable validation:82 typed roles ×2,258 contrast pairings (all accent choices, metric roles and both hero gradient endpoints),35 style contracts and14 reason-checked mutations. Compiled contracts author108 combinations plus helper immutability, preference retention, invalid candidate preservation and notification failure tests; native smoke authors tab/state/layout checks. **Neither compiled nor native test suite ran here.**

The page does not claim a complete DataGrid, editable ComboBox, submenus, high-contrast adaptation, animation framework, UI automation certification or persisted design presets. No motion was added without OS preference handling. Verify real startup, both modes/settings, menus, keyboard/tab navigation, wheel/nested scrolling,720–2560 widths,100/150/200% scale, high contrast/screen readers and performance on Windows before acceptance. The workflow remains intentionally inactive under cols/ci as instructed; no screenshots or green build are fabricated.

## Current analytics specification — supersedes the illustrative-trend checkpoint above

The old fixed Geometry trend is removed, not retained as chart evidence. Bento's factory now caches sealed Styles only. The current package is **102 typed roles / 47 required style targets**, including semantic plate, chart/KPI, BentoTile and native DataGrid targets. `step1.e.11.BentoGrid.cs` is the mosaic panel (spans, equal-height rows, gutter, compact packing). Screens compose with it; they do not stack equal cards. All palettes remain in this owning directory; the shared engine gained only contract entries, not visual implementation.

| Purpose / role | Light | Dark |
| --- | --- | --- |
| Positive / SurfacePositive | #DFF3E9 | #17392F |
| Feature / SurfaceFeature | #ECE7FA | #30294B |
| Information / SurfaceInformation | #E2EEFC | #1B3353 |
| Attention / SurfaceAttention | #FBECD9 | #41321E |
| ChartSeries1 | #254EDA | #9DB9FF |
| ChartSeries2 | #00766B | #73DFD0 |
| ChartSeries3 | #8755B5 | #C2A1F0 |
| ChartSeries4 | #AA6414 | #F4BC6D |
| ChartAxis | #465570 | #C2CEE3 |
| ChartGrid | #D3DDEC | #33445F |
| ChartTooltipSurface / ChartTooltipText | #17243C / #F5F8FF | #EFF5FF / #17243C |

ChartPlotHeight205 DIPs, ChartLineWidth2.5, ChartPointSize5, ChartDonutRadius56 and ChartAreaOpacity0.16 are native numeric roles in both variants. The compact KPI sparkline explicitly uses a90-DIP local content-height override; table height190 is a screen-specific bounded viewport, not a theme palette. Native plot bounds/ticks/resizing come from the library, not fixed path coordinates. Axis/name/tooltip size follows caption size; text typeface follows active FontFamily. Hover inspection is native, with keyboard legend controls and source values as alternatives. Animations are disabled; no shadows/blur or custom render loops added.

`step1.e.10.Analytics.xaml` owns ChartCard and Positive/Feature/Information/Attention plates, the BentoChart adapter style, the BentoKpi template and native DataGrid/row/cell/column-header styles. Header template preserves both native resize-gripper parts and sorting, adds keyboard focus and theme-driven sort/hover/focus visuals; no system-light sort-glyph brush is cached. Native cells use theme-selected/focus colors; existing scroll/tooltip/checkbox roles remain shared within Bento. Public plate names describe purpose, not a mandated color for future themes.

**Business Reference starts selected** and now combines a hero/context pair, four varied KPI tiles, 3:2 revenue/donut and trend/target rows, nested conversion sparkline, full-width compact table and smaller activity/attention plates. It retains the functional review list/form below. Analytics rows collapse below1000 available content DIPs; the containing sample module grid stacks below1100. No engine diagnostics/customization panel enters this sample.

**Studio stays separate**: current settings and control/state gallery plus live chart/scenario selectors and a gallery of area/stacked/horizontal/donut plots. Chart gallery stacks below1000 DIPs. The large Live Resource Contract card is replaced by an optional diagnostics checkbox; validation always remains active. See the [current implementation report](../../step1.ThemeStudio/step1.c.docs/step1.c.9.Reusable-Analytics.md) and [dependency/reuse guide](../step1.f.Analytics/step1.f.6.Analytics-README.md).

Current portable evidence: 102×2 typed roles, 47 style targets, measured text contrast combinations and 14 mutation rejections. See [c.11](../../step1.ThemeStudio/step1.c.docs/step1.c.11.Bento-Grid-Engine.md). Compiled analytics scenarios/runtime switching are authored but **unrun**. The earlier checkpoint's lack of DataGrid no longer describes source capability, but Windows restore/build/layout/DPI/accessibility/performance acceptance is still outstanding. Never equate this specification with inspected rendered output.

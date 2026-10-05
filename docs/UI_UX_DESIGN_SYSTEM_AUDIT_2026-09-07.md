# ProGlassAutomation — Industrial ERP UI/UX + WPF/XAML Design-System Audit

Date: 2026-09-07  
Branch: `arena/01a07b84-proglassautomationadv`  
Instruction: complete the master prompt without commit/push.

## A. Audit summary

### Repository/UI inventory

- Projects found:
  - `ProGlassAutomation.csproj`
  - `Licensing/ProGlass.Licensing.csproj`
  - `Tests/ProGlassAutomation.Tests.csproj`
- XAML inventory:
  - 225 `.xaml` files
  - 175 `.xaml.cs` files
- XAML module distribution highlights:
  - `Themes`: 43
  - `Views/ProformaInvoice`: 28
  - `Views/Reports`: 25
  - `Views/Settings`: 13
  - `Views/DeliveryNote`: 10
  - `Views/TaxInvoice`: 10
  - `Controls`: 9
  - `Views/JobOrder`: 9
  - `Views/Dashboard`: 8
  - `Views/Production`: 8
  - `Views/About`: 7
- Card/surface structural scan:
  - 1,188 card-like `Border` declarations with `Background`, `BorderBrush`, `CornerRadius`, or `BorderThickness`
  - 166 XAML files contain such visual surfaces
- Most surface-heavy files include:
  - `Themes/Styles/LiquidGlassDesign.xaml`
  - report review/list/preview/filter steps
  - `Views/SheetStore/SheetStoreView.xaml`
  - dashboard/list pages
  - About/Settings/Step9 profitability surfaces

### Architecture findings

1. The app already has a mature theme stack: Light/Dark dictionaries, Liquid Glass, Crystal Fluid, VisualThemeEngine, ThemePipeline, runtime accent/outline/font/wallpaper/density resources.
2. Existing Appearance settings already controlled global card radius, card border, defined outline, card-edge color, display density, gradients, font, wallpaper and PDF layout.
3. The main design-system gap was not “more styles”; it was the absence of a semantic ERP surface layer. Surfaces were named by page (`AboutCardStyle`, `SettingsTileStyle`, `DashCardStyle`, `TgtCardStyle`, `LicensePanelStyle`) rather than by hierarchy.
4. The About customization issue was a symptom: About no longer hard-coded all colors, but it still maintained local card/background/border decisions and data-template borders that bypassed central surface semantics.
5. ERP DataGrid usage is limited but important: God View grids and Inquiry BOQ grid need density, virtualization, keyboard focus and selection consistency. Existing God View grid had fixed row height and local chrome.
6. Print/preview XAML intentionally contains some fixed colors and should not be blindly tokenized because PDF output often needs deterministic colors.

## B. Implementation summary

Implemented a three-level semantic surface system that sits on the existing theme engine rather than replacing it.

Key implementation decisions:

- Additive and backward compatible.
- Existing visual families remain: Normal/Industrial, Monet, Crystal Fluid/Ios-17pm.
- Existing Appearance fields still work.
- No business/database/calculation logic changed.
- No new NuGet/package dependency added.
- No blind search/replace across all 1,188 surfaces.

Implemented:

1. Central semantic surface resources and styles in `Themes/Styles/DesignTokens.xaml`.
2. Runtime publishing of semantic surface brushes/thickness/radius/elevation from `ThemeManager`.
3. Backward-compatible settings schema extensions in `AppearanceSettings`.
4. Settings Appearance controls for material mode, opacity, radius, border thickness, rounded mode, primary overlay, and optional primary elevation.
5. Theme pipeline reapplication so semantic surfaces survive Light/Dark swaps, visual-family changes, accent changes and defined-edge changes.
6. Migration of shared card systems and high-impact local bypasses to the semantic surface layer.
7. ERP DataGrid style layer with density/virtualization/focus/selection states.
8. About page and About license card retargeted to global Primary/Secondary/Detail surfaces.

## C. Three-level card system

Exactly three semantic levels are now defined:

### Primary

For major business regions and module/page cards.

Resources/styles:

- `Surface.Primary.BackgroundBrush`
- `Surface.Primary.BorderBrush`
- `Surface.Primary.BorderThickness`
- `Surface.Primary.CornerRadius`
- `Surface.Primary.Padding`
- `Surface.Primary.Effect`
- `SurfacePrimaryStyle`
- `PrimarySurfaceCardStyle`

Adopted by:

- `ModuleSectionCardStyle`
- `BaseCardStyle`
- dashboard cards/soft cards
- dialog cards
- module list shells
- PI work surfaces
- shared specification group cards
- About main cards/hero
- Settings section cards
- Inquiry section cards
- statement wizard cards
- Tax Invoice detail card style

### Secondary

For nested groups, filters, table containers, KPI cells and subsection panels.

Resources/styles:

- `Surface.Secondary.BackgroundBrush`
- `Surface.Secondary.BorderBrush`
- `Surface.Secondary.BorderThickness`
- `Surface.Secondary.CornerRadius`
- `Surface.Secondary.Padding`
- `SurfaceSecondaryStyle`
- `SecondarySurfaceCardStyle`
- `ErpKpiSurfaceStyle`

Adopted by:

- Settings tiles/provider/model cards
- About section panels/templates
- module detail action bar
- PI KPI cells
- Step 9 target/sheet/KPI cards
- line-item table container
- Module list KPI/filter strips
- dashboard tiles
- About license panels

### Detail

For chips, badges, compact facts, field tiles and inline status elements.

Resources/styles:

- `Surface.Detail.BackgroundBrush`
- `Surface.Detail.BorderBrush`
- `Surface.Detail.BorderThickness`
- `Surface.Detail.CornerRadius`
- `Surface.Detail.Padding`
- `SurfaceDetailStyle`
- `DetailSurfaceCardStyle`
- `ErpInlineStatusSurfaceStyle`

Adopted by:

- About chips/header badges/template detail borders
- job-order field tiles
- compact shared stat tiles
- KPI tile base descendants
- detail badges and compact facts where safe

## D. Appearance system

Added Appearance settings fields:

- `SurfaceMaterialMode`: `Glass`, `Solid`, `Transparent`
- `SurfacePrimaryOpacity`
- `SurfaceSecondaryOpacity`
- `SurfaceDetailOpacity`
- `SurfacePrimaryRadius`
- `SurfaceSecondaryRadius`
- `SurfaceDetailRadius`
- `SurfacePrimaryBorderThickness`
- `SurfaceSecondaryBorderThickness`
- `SurfaceDetailBorderThickness`
- `UseRoundedSurfaces`
- `UsePrimarySurfaceElevation`
- `PrimarySurfaceOverlayOpacity`

Runtime behavior:

- `ThemeManager.ApplySemanticSurfaces(...)` derives the actual WPF resources.
- Missing/null radius and border overrides fall back to the existing global card settings, preserving older config files.
- Display density publishes `Surface.Primary.Padding`, `Surface.Secondary.Padding`, `Surface.Detail.Padding` from the existing density model.
- Theme changes, visual-family rebuilds, accent changes and card-edge toggles reapply semantic resources.
- Primary elevation is opt-in and off by default to protect WPF performance.
- Primary overlay is clamped and applied safely to solid-brush surfaces.

Settings UI now exposes a live Primary/Secondary/Detail control panel under Appearance.

## E. Module coverage

Touched/adopted central/high-impact surfaces in:

- App resource architecture: `App.xaml`
- Global design tokens: `Themes/Styles/DesignTokens.xaml`
- Global card styles: `Themes/Styles/CardStyles.xaml`
- Shared section styles: `Themes/Styles/SharedSectionStyles.xaml`
- List page styles: `Themes/Styles/ListPageStyles.xaml`
- DataGrid styles: `Themes/Styles/DataGridStyles.xaml`
- Dialog styles: `Themes/Styles/DialogStyles.xaml`
- Dashboard styles: `Styles/DashboardStyles.xaml`
- PI industrial styles: `Themes/Styles/PiIndustrialStyles.xaml`
- Module/God View tab/grid styles: `Themes/Styles/ModuleTabStyles.xaml`
- Settings styles and Appearance UI
- About styles and About license card
- Inquiry editor/BOQ grid
- JO/TI section styles
- Step 9 profitability cards

Audited but intentionally not blindly rewritten:

- print/PDF invoice preview XAML
- report-specific review/preview pages with output-specific color/layout constraints
- complex local templates with business-specific visual encodings

## F. Performance/accessibility/ERP usability

- No global implicit animation or shadow was added.
- Primary elevation remains off by default.
- Existing About no-shadow freeze fix is preserved.
- DataGrid virtualization and recycling are centralized for opt-in ERP grids.
- Density continues to drive rows, buttons, inputs, cards and now semantic surface padding.
- Numeric presentation conventions already in several KPI/report styles were preserved.
- Keyboard focus states were added to the ERP DataGrid cell style.
- Surface opacity is clamped to protect contrast and avoid unreadable fully transparent ERP panels.

## G. Build/validation result

Validation completed:

- XML parse of all 225 XAML files: PASS
- `git diff --check`: PASS
- Duplicate XAML `x:Key` scan per file: PASS
- C# brace-balance check on edited C# files: PASS

Build attempt:

```bash
dotnet build ProGlassAutomation.csproj --no-restore
```

Result:

```text
/bin/bash: line 1: dotnet: command not found
```

Build status: FAILED/UNAVAILABLE because the sandbox does not have the .NET SDK installed.

## H. Known limitations

- Full WPF compile/runtime validation is blocked by missing .NET SDK.
- Static analysis cannot guarantee every `DynamicResource` resolves at runtime, though merge order was adjusted so semantic surface styles load before dictionaries that use them.
- 1,188 card-like surfaces exist; this pass migrated the central systems and high-impact bypasses, not every individual Border in print/report templates.
- Some report/print surfaces keep deterministic output colors by design.
- No commit/push was performed.

## I. Files changed by this UI/UX design-system pass

- `App.xaml`
- `Services/AI/AIConfig/AppConfig.cs`
- `Services/Settings/AppearanceService.cs`
- `Services/Theme/ThemePipeline.cs`
- `Services/ThemeManager.Part1.cs`
- `Services/ThemeManager.Part2.cs`
- `Themes/Styles/DesignTokens.xaml`
- `Themes/Styles/CardStyles.xaml`
- `Themes/Styles/SharedSectionStyles.xaml`
- `Themes/Styles/ListPageStyles.xaml`
- `Themes/Styles/DataGridStyles.xaml`
- `Themes/Styles/DialogStyles.xaml`
- `Themes/Styles/PiIndustrialStyles.xaml`
- `Themes/Styles/ModuleTabStyles.xaml`
- `Styles/DashboardStyles.xaml`
- `Controls/StatCard/StatCardControl.xaml`
- `ViewModels/Settings/SettingsViewModel.Appearance.cs`
- `ViewModels/Settings/SettingsViewModel.Typography.cs`
- `Views/Settings/SettingsSectionStyles.xaml`
- `Views/Settings/Sections/SettingsAppearanceSection.xaml`
- `Views/About/AboutStyles.xaml`
- `Views/About/Sections/AboutLicenseCard.xaml`
- `Views/Inquiry/InquiryEditorWindow.xaml`
- `Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml`
- `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml`
- `Views/Reports/StatementGenerator/Styles/WizardStyles.xaml`
- `docs/UI_UX_DESIGN_SYSTEM_AUDIT_2026-09-07.md`

## Addendum — dynamic-resource completion pass after owner challenge

The owner correctly challenged whether all non-print XAML had been made dynamic. I reran the audit as a structural grep, not by style names, and completed a second pass focused on theme-sensitive visual literals.

### Non-print exclusions

Excluded from forced tokenization:

- `Printable*`
- `PrintPreview*`
- explicit `/Preview/` print/output preview paths
- PDF-related XAML/output surfaces
- theme dictionaries themselves where literal `Color` values are the source tokens
- third-party/tooling-only license generator XAML

### Completed dynamic conversion checks

For application non-print XAML outside the source theme dictionaries, the following now report zero remaining matches:

- Literal visual hex attributes:
  - `Background="#..."`
  - `Foreground="#..."`
  - `BorderBrush="#..."`
  - `Fill="#..."`
  - `Stroke="#..."`
  - `CaretBrush="#..."`
  - `SelectionBrush="#..."`
- Literal `Color="#..."` in non-theme application XAML
- Named non-dynamic visual colors such as `White`, `Black`, `Red`, etc.
- Hard-coded non-resource `CornerRadius="..."`
- Hard-coded non-resource `BorderThickness="..."`
- Static visual brush references on theme-sensitive properties; these were converted to `DynamicResource` where not excluded.

### Added support tokens

Added dynamic primitives so legacy XAML can be dynamic without destroying proportional shape:

- `Radius.None`
- `Radius.XSmall`
- `Radius.Small`
- `Radius.Medium`
- `Radius.Large`
- `Radius.Hero`
- `Radius.Top.Medium`
- `Radius.Bottom.Medium`
- `Radius.Left.Medium`
- `Radius.Right.Medium`
- `Radius.Asymmetric.Brand`
- `Border.NoneThickness`
- `Border.HairlineThickness`
- `Border.StrongThickness`
- `Border.TopHairlineThickness`
- `Border.BottomHairlineThickness`
- `Border.LeftHairlineThickness`
- `Border.RightHairlineThickness`
- `Border.VerticalHairlineThickness`
- `Border.HorizontalHairlineThickness`
- `Border.BoxNoLeftThickness`
- `Border.BoxNoTopThickness`
- `Border.RightBottomHairlineThickness`
- `LiquidOverlayColor`

These are seeded in `LightTheme.xaml` and `DarkTheme.xaml` so even early-loaded resource dictionaries can consume them safely, then refreshed at runtime by `ThemeManager` from Appearance settings.

### Revalidation after the completion pass

- XML parse of all 225 XAML files: PASS
- `git diff --check`: PASS
- Edited C# brace-balance checks: PASS
- Build attempt still blocked by missing SDK: `/bin/bash: dotnet: command not found`

## Addendum — balance phases applied

After the owner asked to apply the balance, the remaining non-runtime phases were completed as far as this sandbox allows.

### Phase 6 — full non-print dynamic/theme acceptance

Completed for non-print/non-output application XAML:

- All remaining non-print visual hex attributes were removed.
- All remaining non-print literal `Color="#..."` values outside source theme dictionaries were removed.
- All named visual colors such as `White`/`Black` were routed to dynamic resources.
- All hard-coded non-resource `CornerRadius` values were routed to dynamic radius tokens.
- All hard-coded non-resource `BorderThickness` values were routed to dynamic border tokens.
- All `StaticResource` references on theme-sensitive visual properties were converted to `DynamicResource` where not excluded.
- All non-foundation `Border` styles outside print/output/foundation theme files now inherit from one of the semantic surface levels or an accepted semantic descendant.

Final audit counts for non-print/non-output application XAML:

```text
visual_hex_attr: 0
color_hex_attr: 0
named_visual: 0
hard_corner: 0
hard_border: 0
visual_static: 0
hard_font: 0
remaining non-foundation Border styles without semantic BasedOn: 0
```

### Phase 7 — component-system completion

Completed safe component-system standardization by routing all non-foundation Border-based components to the semantic hierarchy:

- chat bubbles
- AI bubbles
- dashboard cards/tiles/wells/chips
- icon badges and status/chip primitives
- list shells/filter strips/table headers/rows
- PI workdeck/station/zone/cost/tile/rail/footer surfaces
- shared section/header/stat tiles
- shell submenu cards
- About crash/license/card rows
- inquiry pipeline/drop-zone/status surfaces
- Job Profitability cards/chips
- report wizard cards/steps/metrics
- Settings wide/full-row tiles and voice status badges
- Sheet Store rows/history entries
- Splash trust chips
- Tax Invoice family/meta row surfaces

The foundational material dictionaries (`LiquidGlassDesign`, `LiquidGlassStyles`, `CrystalFluid`, Light/Dark themes) remain source-token/foundation layers rather than consumers of the semantic surface styles.

### Remaining phase 8 blocker

Runtime/build validation is still blocked by missing `.NET SDK` in the sandbox. Static checks pass, but WPF runtime resource resolution and visual QA still require a machine that can build/run the app.

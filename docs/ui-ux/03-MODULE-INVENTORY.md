# Module inventory — every surface on the shared skin

MainWindow already hosts `ModuleWallpaperBrush` + `ModuleSkinBlurEffect` behind `CurrentViewModel`. Every module page root must be **Transparent**. Inner **cards** use Surface* styles.

## Shell

| Surface | File | Skin rule |
|---|---|---|
| Window | `MainWindow.xaml` | `WindowBackgroundBrush` |
| Wallpaper | same | `ModuleWallpaperBrush` + blur |
| Top bar | same | `GlassSurfaceElevated` |
| Drawer | same | `ShellLiquidBrush` |
| Sub-menus | `ShellStyles.xaml` | frost primary fill |
| Popups | Activity / Settings / User / Storage | existing popup surface; follow frost fills |

## Production (8)

`CuttingView`, `HolesCutoutsView`, `PolishView`, `EdgeDeletionView`, `MitringView`, `TemperingView`, `PairingView`, `DoublingLaminationView` + `ProductionStationBody`, `ProductionKpiStrip`, `ProductionFilterBar`, `ProductionUnitDetailControl`, pipeline/release bars.

Page Transparent. Body = `ModuleListShellStyle`. Detail 360 = `SurfacePrimaryStyle`. No extra MES chrome.

## Optimization

`OptimizationView` + `Tabs/*`. Page Transparent. `OptKpiCard` / `OptPanelCard` / `OptInnerCard` based on semantic surfaces.

## Sales / documents

Inquiry list, PI wizard + saved list, Job Order + list + detail, Tax Invoice + list + detail, Delivery Note + list + detail.

## Inventory / master

Sheet Store, Specifications, Company Center, User Center.

## Finance / reports

Job profitability, VAT, statement module, order report, PI/JO/DN/TI/sales/company/customer/executive/glass/profit/project/salesman reports.

## System

Dashboard (+ nested dashboards), Settings (pages Transparent; tiles stay cards), About, System Center, Diagnostics, AI chat overlay, Agent chat.

## Dialogs / login / splash

Keep window chrome; **cards inside** still Surface*. Print/PDF uses `PdfTheme` — not frost.

## Per-view leftover (later phases)

Any remaining inline `Background="{DynamicResource SurfacePrimaryBrush}"` on **inner** cards is OK if the parent page is Transparent. Remaining **UserControl.Background = SurfacePrimary** must become Transparent (Diagnostics / System Center done). Nested PI steps, report filters: migrate to `SurfacePrimaryStyle` when that view is redesigned — do not dump blur on them ad hoc.

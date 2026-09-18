# ProGlass UI — Complete XAML Inventory (Phase 1 of release-safe modernization)

**Generated:** 2026-09-18 · **Commit:** `97160501c4f1fbcf9eef0d4f0d99ee7ff59fb68a` · **Method:** ElementTree census; comments/PIs excluded; resource markers counted in attributes

## Totals (measured, not assumed)

| Metric | Count |
|---|---|
| .xaml files | **320** |
| .xaml.cs files | 244 |
| Windows | 34 |
| Pages | 0 |
| UserControls | 201 |
| ResourceDictionaries | 83 |
| Other roots | 2 |
| XML parse errors | 0 |
| MERIDIAN-rewritten views | 9 |
| {DynamicResource} markers (app-wide) | 17,318 |
| {StaticResource} markers (app-wide) | 8,711 |
| Hardcoded hex colour values | 1,068 (in 17 files) |
| Hardcoded numeric FontSize | 63 |

## By module

| Module | XAML | Views (Win/Pg/UC) | ResDicts | MERIDIAN | DynRes | StatRes | Hex | LitFSz | LocalStyles |
|---|---|---|---|---|---|---|---|---|---|
| Themes | 66 | 0 | 66 | 0 | 1,210 | 902 | 1041 | 1 | 647 |
| Settings | 53 | 50 | 3 | 0 | 1,209 | 1,628 | 0 | 7 | 90 |
| Reports | 32 | 31 | 1 | 0 | 5,187 | 1,474 | 0 | 0 | 145 |
| ProformaInvoice | 27 | 27 | 0 | 0 | 1,820 | 967 | 0 | 0 | 122 |
| Production | 18 | 17 | 1 | 0 | 651 | 479 | 0 | 29 | 6 |
| Shared | 16 | 16 | 0 | 0 | 498 | 104 | 0 | 3 | 2 |
| About | 10 | 8 | 2 | 0 | 704 | 143 | 0 | 0 | 32 |
| DeliveryNote | 10 | 9 | 1 | 0 | 724 | 250 | 0 | 0 | 48 |
| TaxInvoice | 10 | 9 | 1 | 0 | 595 | 208 | 0 | 0 | 35 |
| JobOrder | 9 | 8 | 1 | 0 | 263 | 170 | 0 | 0 | 15 |
| Optimization | 9 | 9 | 0 | 0 | 569 | 337 | 0 | 12 | 22 |
| Dashboard | 8 | 8 | 0 | 8 | 260 | 610 | 0 | 3 | 5 |
| SheetStore | 8 | 8 | 0 | 0 | 878 | 387 | 0 | 0 | 45 |
| JobProfitability | 6 | 6 | 0 | 0 | 614 | 208 | 0 | 0 | 37 |
| Login | 6 | 5 | 1 | 0 | 324 | 83 | 0 | 0 | 5 |
| Styles | 4 | 0 | 4 | 0 | 114 | 40 | 0 | 0 | 29 |
| Tools/LicenseGenerator | 3 | 1 | 1 | 0 | 28 | 73 | 15 | 0 | 15 |
| Inquiry | 3 | 3 | 0 | 0 | 386 | 181 | 0 | 0 | 16 |
| Shell | 2 | 1 | 0 | 1 | 125 | 102 | 0 | 2 | 9 |
| CrashReports | 2 | 1 | 1 | 0 | 30 | 14 | 0 | 0 | 2 |
| Agent | 1 | 1 | 0 | 0 | 38 | 21 | 3 | 0 | 5 |
| Controls/AutoCompleteTextBox | 1 | 1 | 0 | 0 | 21 | 3 | 0 | 0 | 1 |
| Controls/DonutChart | 1 | 1 | 0 | 0 | 4 | 4 | 0 | 0 | 0 |
| Controls/EmptyState | 1 | 1 | 0 | 0 | 15 | 5 | 0 | 0 | 1 |
| Controls/GlobalSearch | 1 | 1 | 0 | 0 | 46 | 19 | 0 | 0 | 8 |
| Controls/LoadingState | 1 | 1 | 0 | 0 | 3 | 1 | 0 | 0 | 0 |
| Controls/MiniBarChart | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Controls/Reporting | 1 | 1 | 0 | 0 | 12 | 8 | 0 | 0 | 1 |
| Controls/StatCard | 1 | 1 | 0 | 0 | 27 | 8 | 0 | 0 | 5 |
| Controls/TrendChart | 1 | 1 | 0 | 0 | 11 | 4 | 0 | 0 | 0 |
| AI | 1 | 1 | 0 | 0 | 112 | 22 | 0 | 0 | 17 |
| CompanyCenter | 1 | 1 | 0 | 0 | 109 | 35 | 0 | 3 | 4 |
| Diagnostics | 1 | 1 | 0 | 0 | 75 | 6 | 0 | 0 | 5 |
| Sharing | 1 | 1 | 0 | 0 | 92 | 14 | 0 | 0 | 2 |
| Specifications | 1 | 1 | 0 | 0 | 128 | 57 | 0 | 0 | 7 |
| Splash | 1 | 1 | 0 | 0 | 163 | 36 | 9 | 0 | 2 |
| SystemCenter | 1 | 1 | 0 | 0 | 168 | 62 | 0 | 0 | 22 |
| UserCenter | 1 | 1 | 0 | 0 | 105 | 46 | 0 | 3 | 2 |

## Full file table

kind: W=Window, P=Page, U=UserControl, R=ResourceDictionary, O=other. `RISK` = heuristic flags.

| File | Kind | DynRes | StatRes | Hex | LitFSz | LocalStyles | Templates | Hosts/parents (heuristic) | MERIDIAN | RISK |
|---|---|---|---|---|---|---|---|---|---|---|
| `Agent/UI/AgentChatView.xaml` | U | 38 | 21 | 3 | 0 | 5 | 3 | …/MainWindow.xaml |  | HARDCODED-HEX(3) |
| `App.xaml` | O | 13 | 0 | 0 | 0 | 1 | 1 | …/DarkTheme.xaml<br>…/ar-AE.xaml<br>…/bn-BD.xaml (+3) |  |  |
| `Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml` | U | 21 | 3 | 0 | 0 | 1 | 2 | …/CutListTabView.xaml<br>…/DataGridView.xaml |  |  |
| `Controls/DonutChart/DonutChartControl.xaml` | U | 4 | 4 | 0 | 0 | 0 | 0 | …/DnDashboardView.xaml<br>…/JoDashboardView.xaml<br>…/OverviewDashboardView.xaml (+3) |  |  |
| `Controls/EmptyState/EmptyStateControl.xaml` | U | 15 | 5 | 0 | 0 | 1 | 1 | …/DeliveryNoteListView.xaml<br>…/DeliveryNoteGodView.xaml<br>…/JobOrderGodView.xaml (+3) |  |  |
| `Controls/GlobalSearch/GlobalSearchBar.xaml` | U | 46 | 19 | 0 | 0 | 8 | 1 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Controls/LoadingState/LoadingStateControl.xaml` | U | 3 | 1 | 0 | 0 | 0 | 0 | …/DeliveryNoteDetailView.xaml<br>…/DeliveryNoteListView.xaml<br>…/JobOrderDetailView.xaml (+3) |  |  |
| `Controls/MiniBarChart/MiniBarChartControl.xaml` | U | 0 | 0 | 0 | 0 | 0 | 0 | …/OverviewDashboardView.xaml |  |  |
| `Controls/Reporting/MixPanel.xaml` | U | 12 | 8 | 0 | 0 | 1 | 1 | …/DeliveryReportView.xaml<br>…/JoReportView.xaml |  |  |
| `Controls/StatCard/StatCardControl.xaml` | U | 27 | 8 | 0 | 0 | 5 | 0 | …/DiagnosticsView.xaml |  |  |
| `Controls/TrendChart/TrendChartControl.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/DnDashboardView.xaml<br>…/JoDashboardView.xaml<br>…/OverviewDashboardView.xaml (+3) |  |  |
| `MainWindow.xaml` | W | 112 | 102 | 0 | 2 | 8 | 31 |  | **REWROTE** | LOCAL-STYLE-FAMILY(8) |
| `Styles/ButtonStyles.xaml` | R | 54 | 24 | 0 | 0 | 18 | 2 |  |  | LOCAL-STYLE-FAMILY(18) |
| `Styles/ContextMenuStyles.xaml` | R | 37 | 3 | 0 | 0 | 4 | 4 |  |  |  |
| `Styles/DashboardStyles.xaml` | R | 1 | 4 | 0 | 0 | 2 | 0 |  |  |  |
| `Styles/Step4SectionStyles.xaml` | R | 22 | 9 | 0 | 0 | 5 | 2 |  |  |  |
| `Themes/CrystalFluid/CrystalFluidBrushes.xaml` | R | 0 | 37 | 45 | 0 | 0 | 0 |  |  | HARDCODED-HEX(45) |
| `Themes/CrystalFluid/CrystalFluidColors.xaml` | R | 0 | 0 | 48 | 0 | 0 | 0 |  |  | HARDCODED-HEX(48) |
| `Themes/CrystalFluid/CrystalFluidControls.xaml` | R | 19 | 53 | 1 | 0 | 6 | 5 |  |  | HARDCODED-HEX(1) |
| `Themes/CrystalFluid/CrystalFluidDataGrid.xaml` | R | 0 | 15 | 0 | 0 | 4 | 1 |  |  |  |
| `Themes/CrystalFluid/CrystalFluidMetrics.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/CrystalFluid/CrystalFluidNavigation.xaml` | R | 0 | 11 | 0 | 0 | 2 | 1 |  |  |  |
| `Themes/CrystalFluid/CrystalFluidStyles.xaml` | R | 7 | 56 | 0 | 0 | 7 | 7 |  |  |  |
| `Themes/CrystalFluid/CrystalFluidTokens.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/DarkTheme.xaml` | R | 0 | 26 | 397 | 0 | 0 | 0 |  |  | HARDCODED-HEX(397) |
| `Themes/Languages/ar-AE.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/bn-BD.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/de-DE.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/en-US.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/es-ES.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/fr-FR.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/hi-IN.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/id-ID.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/ja-JP.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/ko-KR.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/pt-BR.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/ru-RU.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/tr-TR.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/ur-PK.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Languages/zh-CN.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/LightTheme.xaml` | R | 0 | 26 | 396 | 0 | 0 | 0 |  |  | HARDCODED-HEX(396) |
| `Themes/ProGlassDesignSystem/Compatibility/LegacyAliases.xaml` | R | 0 | 0 | 5 | 0 | 0 | 0 |  |  | HARDCODED-HEX(5) |
| `Themes/ProGlassDesignSystem/Materials/ProGlassMaterials.xaml` | R | 11 | 7 | 0 | 0 | 6 | 0 |  |  |  |
| `Themes/ProGlassDesignSystem/ProGlassDesignSystem.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/ProGlassDesignSystem/Tokens/SemanticColors.xaml` | R | 0 | 0 | 33 | 0 | 0 | 0 |  |  | HARDCODED-HEX(33) |
| `Themes/ProGlassDesignSystem/Tokens/SemanticDensity.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/ProGlassDesignSystem/Tokens/SemanticElevation.xaml` | R | 0 | 0 | 7 | 0 | 0 | 0 |  |  | HARDCODED-HEX(7) |
| `Themes/ProGlassDesignSystem/Tokens/SemanticGeometry.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/ProGlassDesignSystem/Tokens/SemanticMotion.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/ProGlassDesignSystem/Tokens/SemanticSpacing.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/ProGlassDesignSystem/Tokens/SemanticTypography.xaml` | R | 16 | 7 | 0 | 0 | 8 | 0 |  |  | LOCAL-STYLE-FAMILY(8) |
| `Themes/Professional/ProfessionalCore.xaml` | R | 70 | 115 | 0 | 0 | 58 | 10 |  |  | LOCAL-STYLE-FAMILY(58) |
| `Themes/Professional/ProfessionalShell.xaml` | R | 81 | 39 | 0 | 0 | 29 | 7 |  |  | LOCAL-STYLE-FAMILY(29) |
| `Themes/Styles/AboutSurface.xaml` | R | 0 | 0 | 1 | 0 | 0 | 0 |  |  | HARDCODED-HEX(1) |
| `Themes/Styles/AboutTypeScale.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Styles/AppStage.xaml` | R | 0 | 0 | 5 | 0 | 0 | 0 |  |  | HARDCODED-HEX(5) |
| `Themes/Styles/CardStyles.xaml` | R | 23 | 22 | 0 | 0 | 15 | 0 |  |  | LOCAL-STYLE-FAMILY(15) |
| `Themes/Styles/ComboTemplates.xaml` | R | 27 | 0 | 0 | 0 | 0 | 3 |  |  |  |
| `Themes/Styles/ComponentTokens.xaml` | R | 0 | 55 | 0 | 0 | 55 | 0 |  |  | LOCAL-STYLE-FAMILY(55) |
| `Themes/Styles/ConverterResources.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Styles/DataGridStyles.xaml` | R | 36 | 11 | 0 | 0 | 14 | 1 |  |  | LOCAL-STYLE-FAMILY(14) |
| `Themes/Styles/DesignTokens.xaml` | R | 40 | 21 | 7 | 0 | 24 | 2 |  |  | HARDCODED-HEX(7); LOCAL-STYLE-FAMILY(24) |
| `Themes/Styles/DialogStyles.xaml` | R | 38 | 19 | 0 | 0 | 13 | 3 |  |  | LOCAL-STYLE-FAMILY(13) |
| `Themes/Styles/GlobalDesignSystem.xaml` | R | 169 | 154 | 78 | 0 | 177 | 5 |  |  | HARDCODED-HEX(78); LOCAL-STYLE-FAMILY(177) |
| `Themes/Styles/IconBadgeStyles.xaml` | R | 8 | 10 | 0 | 0 | 10 | 0 |  |  | LOCAL-STYLE-FAMILY(10) |
| `Themes/Styles/InputStyles.xaml` | R | 50 | 10 | 0 | 0 | 9 | 5 |  |  | LOCAL-STYLE-FAMILY(9) |
| `Themes/Styles/LayoutTokens.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Styles/LiquidGlassDesign.xaml` | R | 200 | 24 | 0 | 0 | 24 | 20 |  |  | LOCAL-STYLE-FAMILY(24) |
| `Themes/Styles/LiquidGlassStyles.xaml` | R | 46 | 20 | 0 | 0 | 22 | 3 |  |  | LOCAL-STYLE-FAMILY(22) |
| `Themes/Styles/ListPageStyles.xaml` | R | 44 | 18 | 0 | 0 | 22 | 2 |  |  | LOCAL-STYLE-FAMILY(22) |
| `Themes/Styles/MaterialTokens.xaml` | R | 0 | 7 | 0 | 0 | 7 | 0 |  |  |  |
| `Themes/Styles/ModuleTabStyles.xaml` | R | 45 | 15 | 0 | 0 | 18 | 4 |  |  | LOCAL-STYLE-FAMILY(18) |
| `Themes/Styles/NavigationRailStyles.xaml` | R | 7 | 1 | 0 | 0 | 1 | 1 |  |  |  |
| `Themes/Styles/OptimizationStyles.xaml` | R | 48 | 12 | 0 | 1 | 15 | 8 |  |  | LOCAL-STYLE-FAMILY(15) |
| `Themes/Styles/PiIndustrialStyles.xaml` | R | 96 | 57 | 0 | 0 | 49 | 9 |  |  | LOCAL-STYLE-FAMILY(49) |
| `Themes/Styles/PillStyles.xaml` | R | 14 | 17 | 0 | 0 | 17 | 0 |  |  | LOCAL-STYLE-FAMILY(17) |
| `Themes/Styles/PrintTokens.xaml` | R | 2 | 1 | 6 | 0 | 2 | 0 |  |  | HARDCODED-HEX(6) |
| `Themes/Styles/SemanticTokens.xaml` | R | 0 | 0 | 12 | 0 | 0 | 0 |  |  | HARDCODED-HEX(12) |
| `Themes/Styles/SharedSectionStyles.xaml` | R | 8 | 10 | 0 | 0 | 10 | 2 |  |  | LOCAL-STYLE-FAMILY(10) |
| `Themes/Styles/ShellStyles.xaml` | R | 91 | 14 | 0 | 0 | 9 | 5 |  |  | LOCAL-STYLE-FAMILY(9) |
| `Themes/Styles/TypeScaleTokens.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Themes/Styles/TypographyStyles.xaml` | R | 14 | 12 | 0 | 0 | 14 | 0 |  |  | LOCAL-STYLE-FAMILY(14) |
| `Tools/LicenseGenerator/App.xaml` | O | 0 | 0 | 0 | 0 | 0 | 0 | …/App.xaml<br>…/DarkTheme.xaml<br>…/ar-AE.xaml (+3) |  |  |
| `Tools/LicenseGenerator/MainWindow.xaml` | W | 23 | 32 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Tools/LicenseGenerator/Theme.xaml` | R | 5 | 41 | 15 | 0 | 15 | 2 |  |  | HARDCODED-HEX(15); LOCAL-STYLE-FAMILY(15) |
| `Views/AI/AiChatView.xaml` | U | 112 | 22 | 0 | 0 | 17 | 6 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(17) |
| `Views/About/AboutIos26Theme.xaml` | R | 36 | 1 | 0 | 0 | 12 | 2 |  |  | LOCAL-STYLE-FAMILY(12) |
| `Views/About/AboutStyles.xaml` | R | 48 | 10 | 0 | 0 | 2 | 6 |  |  |  |
| `Views/About/AboutView.xaml` | U | 77 | 19 | 0 | 0 | 5 | 1 | …/MainWindow.xaml<br>…/AboutStyles.xaml |  |  |
| `Views/About/Sections/AboutCrashReportsCard.xaml` | U | 27 | 6 | 0 | 0 | 0 | 1 | …/DiagnosticsSection.xaml |  |  |
| `Views/About/Sections/AboutLicenseCard.xaml` | U | 84 | 38 | 0 | 0 | 5 | 1 | …/AboutView.xaml |  |  |
| `Views/About/Sections/AboutOverviewSection.xaml` | U | 125 | 14 | 0 | 0 | 1 | 1 | …/AboutView.xaml |  |  |
| `Views/About/Sections/DeveloperSection.xaml` | U | 106 | 20 | 0 | 0 | 2 | 0 | …/AboutView.xaml |  |  |
| `Views/About/Sections/DiagnosticsSection.xaml` | U | 22 | 6 | 0 | 0 | 1 | 1 | …/AboutView.xaml |  |  |
| `Views/About/Sections/ModulesSection.xaml` | U | 86 | 19 | 0 | 0 | 3 | 3 | …/AboutView.xaml |  |  |
| `Views/About/Sections/SystemSection.xaml` | U | 93 | 10 | 0 | 0 | 1 | 1 | …/AboutView.xaml |  |  |
| `Views/CompanyCenter/CompanyCenterView.xaml` | U | 109 | 35 | 0 | 3 | 4 | 1 | …/MainWindow.xaml |  |  |
| `Views/CrashReports/CrashReportDialog.xaml` | W | 23 | 12 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/CrashReports/CrashReportDialogStyles.xaml` | R | 7 | 2 | 0 | 0 | 2 | 2 |  |  |  |
| `Views/Dashboard/DashboardView.xaml` | U | 15 | 10 | 0 | 2 | 0 | 9 | …/MainWindow.xaml | **REWROTE** |  |
| `Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml` | U | 30 | 73 | 0 | 0 | 0 | 0 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml` | U | 30 | 73 | 0 | 0 | 0 | 0 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/Dashboard/Overview/OverviewDashboardView.xaml` | U | 39 | 125 | 0 | 1 | 2 | 0 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/Dashboard/ProformaInvoice/PiDashboardView.xaml` | U | 27 | 64 | 0 | 0 | 0 | 0 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml` | U | 19 | 55 | 0 | 0 | 0 | 0 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/Dashboard/Statement/StDashboardView.xaml` | U | 50 | 84 | 0 | 0 | 1 | 1 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml` | U | 50 | 126 | 0 | 0 | 2 | 3 | …/DashboardView.xaml | **REWROTE** |  |
| `Views/DeliveryNote/DeliveryNoteDetail/Attachments/Rename/RenameAttachmentDialog.xaml` | W | 29 | 6 | 0 | 0 | 1 | 1 |  |  |  |
| `Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml` | U | 62 | 30 | 0 | 0 | 3 | 0 | …/DeliveryNoteView.xaml |  |  |
| `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnAttachmentsSection.xaml` | U | 46 | 12 | 0 | 0 | 0 | 1 | …/DeliveryNoteDetailView.xaml |  |  |
| `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnHistorySection.xaml` | U | 108 | 30 | 0 | 0 | 1 | 1 | …/DeliveryNoteDetailView.xaml |  |  |
| `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnOverviewSection.xaml` | U | 236 | 70 | 0 | 0 | 5 | 2 | …/DeliveryNoteDetailView.xaml |  |  |
| `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml` | R | 22 | 4 | 0 | 0 | 9 | 2 |  |  | LOCAL-STYLE-FAMILY(9) |
| `Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml` | U | 155 | 67 | 0 | 0 | 16 | 2 | …/DeliveryNoteView.xaml |  | LOCAL-STYLE-FAMILY(16) |
| `Views/DeliveryNote/DeliveryNoteList/JobOrderPicker/JobOrderPickerDialog.xaml` | W | 18 | 3 | 0 | 0 | 1 | 0 |  |  |  |
| `Views/DeliveryNote/DeliveryNoteView.xaml` | U | 13 | 6 | 0 | 0 | 11 | 1 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/DeliveryNote/GodView/DeliveryNoteGodView.xaml` | U | 35 | 22 | 0 | 0 | 1 | 1 | …/DeliveryNoteView.xaml |  |  |
| `Views/Diagnostics/DiagnosticsView.xaml` | U | 75 | 6 | 0 | 0 | 5 | 4 | …/MainWindow.xaml |  |  |
| `Views/Inquiry/InquiryAttachmentPreviewWindow.xaml` | W | 39 | 4 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Inquiry/InquiryEditorWindow.xaml` | W | 118 | 86 | 0 | 0 | 5 | 12 |  |  |  |
| `Views/Inquiry/InquiryListView.xaml` | U | 229 | 91 | 0 | 0 | 11 | 5 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/JobOrder/GodView/JobOrderGodView.xaml` | U | 33 | 19 | 0 | 0 | 1 | 1 | …/JobOrderView.xaml |  |  |
| `Views/JobOrder/JobOrderDetail/JobOrderDetailView.xaml` | U | 22 | 16 | 0 | 0 | 0 | 0 | …/JobOrderView.xaml |  |  |
| `Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml` | U | 50 | 32 | 0 | 0 | 0 | 2 | …/JobOrderDetailView.xaml |  |  |
| `Views/JobOrder/JobOrderDetail/Sections/JoProductionSection.xaml` | U | 28 | 38 | 0 | 0 | 0 | 0 | …/JoOverviewSection.xaml |  |  |
| `Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml` | R | 0 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/JobOrder/JobOrderView.xaml` | U | 13 | 6 | 0 | 0 | 11 | 2 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/JobOrder/SavedJobOrdersList/JobOrderAttachmentWarningDialog.xaml` | W | 10 | 2 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/JobOrder/SavedJobOrdersList/ProformaPicker/JobOrderProformaPickerDialog.xaml` | W | 28 | 4 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml` | U | 79 | 53 | 0 | 0 | 3 | 1 | …/JobOrderView.xaml |  |  |
| `Views/JobProfitability/JobDetailPanel.xaml` | U | 161 | 63 | 0 | 0 | 9 | 2 | …/JobProfitabilityView.xaml |  | LOCAL-STYLE-FAMILY(9) |
| `Views/JobProfitability/JobFullDetailView.xaml` | U | 93 | 53 | 0 | 0 | 8 | 2 | …/JobProfitabilityView.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Views/JobProfitability/JobProfitabilityView.xaml` | U | 84 | 68 | 0 | 0 | 12 | 1 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(12) |
| `Views/JobProfitability/PortfolioGlassTypeComparisonView.xaml` | U | 94 | 14 | 0 | 0 | 3 | 1 | …/JobProfitabilityView.xaml |  |  |
| `Views/JobProfitability/PortfolioServiceProfitabilityView.xaml` | U | 91 | 6 | 0 | 0 | 3 | 1 | …/JobProfitabilityView.xaml |  |  |
| `Views/JobProfitability/PortfolioWastageAnalysisView.xaml` | U | 91 | 4 | 0 | 0 | 2 | 1 | …/JobProfitabilityView.xaml |  |  |
| `Views/Login/AuthSurfaceStyles.xaml` | R | 63 | 7 | 0 | 0 | 5 | 5 |  |  |  |
| `Views/Login/FirstRunSetupPanel.xaml` | U | 64 | 18 | 0 | 0 | 0 | 0 | …/SplashView.xaml |  |  |
| `Views/Login/ForcePasswordChangeWindow.xaml` | W | 62 | 20 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Login/LoginPanel.xaml` | U | 69 | 16 | 0 | 0 | 0 | 0 | …/LoginWindow.xaml<br>…/SplashView.xaml |  |  |
| `Views/Login/LoginWindow.xaml` | W | 3 | 0 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Login/ResetPasswordWindow.xaml` | W | 63 | 22 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Optimization/OptimizationView.xaml` | U | 105 | 52 | 0 | 8 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Optimization/Tabs/CutListTabView.xaml` | U | 59 | 91 | 0 | 0 | 8 | 3 | …/OptimizationView.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Views/Optimization/Tabs/JobsTabView.xaml` | U | 61 | 51 | 0 | 0 | 1 | 3 | …/OptimizationView.xaml |  |  |
| `Views/Optimization/Tabs/ReportTabView.xaml` | U | 152 | 59 | 0 | 0 | 3 | 8 | …/OptimizationView.xaml |  |  |
| `Views/Optimization/Tabs/SettingsTabView.xaml` | U | 6 | 19 | 0 | 0 | 1 | 0 | …/OptimizationView.xaml |  |  |
| `Views/Optimization/Tabs/SizesTabView.xaml` | U | 26 | 16 | 0 | 0 | 1 | 2 | …/OptimizationView.xaml |  |  |
| `Views/Optimization/Tabs/StocksTabView.xaml` | U | 17 | 19 | 0 | 0 | 1 | 1 | …/OptimizationView.xaml |  |  |
| `Views/Optimization/Tabs/SummaryTabView.xaml` | U | 78 | 14 | 0 | 4 | 2 | 4 | …/OptimizationView.xaml |  |  |
| `Views/Optimization/Tabs/TwoDViewTabView.xaml` | U | 65 | 16 | 0 | 0 | 5 | 2 | …/OptimizationView.xaml |  |  |
| `Views/Production/Controls/ProductionFilterBar.xaml` | U | 17 | 7 | 0 | 0 | 0 | 0 | …/ProductionStationBody.xaml |  |  |
| `Views/Production/Controls/ProductionKpiStrip.xaml` | U | 25 | 30 | 0 | 0 | 0 | 0 | …/ProductionStationBody.xaml |  |  |
| `Views/Production/Controls/ProductionPipelineBar.xaml` | U | 30 | 3 | 0 | 0 | 0 | 2 | …/CuttingView.xaml<br>…/DoublingLaminationView.xaml<br>…/EdgeDeletionView.xaml (+3) |  |  |
| `Views/Production/Controls/ProductionReleaseBar.xaml` | U | 14 | 6 | 0 | 0 | 1 | 0 | …/CuttingView.xaml<br>…/DoublingLaminationView.xaml<br>…/EdgeDeletionView.xaml (+3) |  |  |
| `Views/Production/Controls/ProductionStationBody.xaml` | U | 43 | 37 | 0 | 0 | 4 | 7 | …/CuttingView.xaml<br>…/DoublingLaminationView.xaml<br>…/EdgeDeletionView.xaml (+3) |  |  |
| `Views/Production/Controls/ProductionUnitDetailControl.xaml` | U | 129 | 60 | 0 | 0 | 0 | 2 | …/ProductionStationBody.xaml |  |  |
| `Views/Production/Controls/ProductionUnitDetailTemplates.xaml` | R | 58 | 46 | 0 | 0 | 0 | 4 |  |  |  |
| `Views/Production/CuttingView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/CuttingWorkCenterView.xaml` | U | 68 | 64 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/DoublingLaminationView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/EdgeDeletionView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/HolesCutoutsView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/MitringView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/PairingView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/PolishView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Production/ProcessWorkCenterView.xaml` | U | 102 | 71 | 0 | 15 | 0 | 0 | …/MainWindow.xaml |  | UNTOKENED-FONTSIZE(15) |
| `Views/Production/ProductionControlCenterView.xaml` | U | 77 | 123 | 0 | 14 | 1 | 0 | …/MainWindow.xaml |  | UNTOKENED-FONTSIZE(14) |
| `Views/Production/TemperingView.xaml` | U | 11 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/ProformaInvoice/ProformaInvoiceView.xaml` | U | 42 | 7 | 0 | 0 | 11 | 12 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml` | W | 36 | 10 | 0 | 0 | 2 | 2 |  |  |  |
| `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListDialog.xaml` | W | 12 | 2 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListView.xaml` | U | 77 | 46 | 0 | 0 | 1 | 1 | …/ProformaInvoiceView.xaml<br>…/SavedInvoicesListDialog.xaml |  |  |
| `Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml` | W | 27 | 4 | 0 | 0 | 0 | 1 |  |  |  |
| `Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml` | U | 70 | 33 | 0 | 0 | 12 | 1 | …/ProformaInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(12) |
| `Views/ProformaInvoice/Step2ClientProfile/ClientProfileView.xaml` | U | 45 | 20 | 0 | 0 | 10 | 1 | …/ProformaInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(10) |
| `Views/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsView.xaml` | U | 50 | 14 | 0 | 0 | 8 | 1 | …/ProformaInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Views/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersView.xaml` | U | 67 | 72 | 0 | 0 | 1 | 0 | …/PricingEstimatorView.xaml |  |  |
| `Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml` | U | 34 | 48 | 0 | 0 | 2 | 0 | …/PricingEstimatorView.xaml |  |  |
| `Views/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersView.xaml` | U | 36 | 63 | 0 | 0 | 0 | 0 | …/PricingEstimatorView.xaml |  |  |
| `Views/ProformaInvoice/Step4PricingEstimator/PricingEstimatorView.xaml` | U | 79 | 8 | 0 | 0 | 9 | 8 | …/ProformaInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(9) |
| `Views/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersView.xaml` | U | 18 | 30 | 0 | 0 | 0 | 0 | …/PricingEstimatorView.xaml |  |  |
| `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml` | U | 99 | 50 | 0 | 0 | 3 | 4 | …/ProformaInvoiceView.xaml |  |  |
| `Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryView.xaml` | U | 34 | 7 | 0 | 0 | 2 | 1 | …/DataGridView.xaml |  |  |
| `Views/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml` | U | 31 | 20 | 0 | 0 | 0 | 1 | …/OtherChargesView.xaml |  |  |
| `Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml` | U | 108 | 24 | 0 | 0 | 9 | 7 | …/ProformaInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(9) |
| `Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml` | U | 113 | 29 | 0 | 0 | 5 | 3 | …/OtherChargesView.xaml |  |  |
| `Views/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryView.xaml` | U | 50 | 26 | 0 | 0 | 1 | 1 | …/PrintPreviewView.xaml |  |  |
| `Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml` | U | 41 | 17 | 0 | 0 | 0 | 0 | …/ProformaInvoiceView.xaml |  |  |
| `Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml` | U | 269 | 104 | 0 | 0 | 10 | 7 | …/PrintPreviewView.xaml |  | LOCAL-STYLE-FAMILY(10) |
| `Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsView.xaml` | U | 34 | 12 | 0 | 0 | 3 | 2 | …/PrintPreviewView.xaml |  |  |
| `Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml` | U | 34 | 11 | 0 | 0 | 2 | 1 | …/ProformaInvoiceView.xaml |  |  |
| `Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml` | U | 43 | 31 | 0 | 0 | 6 | 1 | …/ProfitLossStepView.xaml |  |  |
| `Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml` | U | 160 | 193 | 0 | 0 | 11 | 6 | …/ProformaInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml` | U | 47 | 39 | 0 | 0 | 5 | 1 | …/ProfitLossStepView.xaml |  |  |
| `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml` | U | 164 | 47 | 0 | 0 | 9 | 3 | …/ProfitLossStepView.xaml |  | LOCAL-STYLE-FAMILY(9) |
| `Views/Reports/CompanyReport/CompanyReportView.xaml` | U | 158 | 62 | 0 | 0 | 1 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/CustomerReport/CustomerReportView.xaml` | U | 158 | 62 | 0 | 0 | 1 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/DeliveryReport/DeliveryReportView.xaml` | U | 277 | 89 | 0 | 0 | 2 | 3 | …/SalesReportView.xaml |  |  |
| `Views/Reports/ExecutiveReport/ExecutiveReportView.xaml` | U | 109 | 18 | 0 | 0 | 2 | 5 | …/SalesReportView.xaml |  |  |
| `Views/Reports/GlassSpecReport/GlassSpecReportView.xaml` | U | 117 | 62 | 0 | 0 | 4 | 2 | …/SalesReportView.xaml |  |  |
| `Views/Reports/JoReport/JoReportView.xaml` | U | 273 | 74 | 0 | 0 | 2 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/OrderReport/Filter/FilterStep.xaml` | U | 81 | 27 | 0 | 0 | 1 | 2 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/OrderReport/OrderReportGeneratorView.xaml` | U | 90 | 32 | 0 | 0 | 25 | 2 | …/OrderReportModuleView.xaml |  | LOCAL-STYLE-FAMILY(25) |
| `Views/Reports/OrderReport/OrderReportListView.xaml` | U | 216 | 30 | 0 | 0 | 8 | 10 | …/OrderReportModuleView.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Views/Reports/OrderReport/OrderReportModuleView.xaml` | U | 1 | 0 | 0 | 0 | 2 | 2 | …/MainWindow.xaml |  |  |
| `Views/Reports/OrderReport/Pipeline/ExecuteStep.xaml` | U | 20 | 1 | 0 | 0 | 2 | 0 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/OrderReport/Preview/PreviewStep.xaml` | U | 29 | 4 | 0 | 0 | 2 | 1 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/OrderReport/Review/ReviewStep.xaml` | U | 154 | 113 | 0 | 0 | 5 | 9 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/OrderReport/Specifications/SpecificationsStep.xaml` | U | 80 | 11 | 0 | 0 | 2 | 4 | …/OrderReportGeneratorView.xaml |  |  |
| `Views/Reports/PiReport/PiReportView.xaml` | U | 262 | 79 | 0 | 0 | 2 | 2 | …/SalesReportView.xaml |  |  |
| `Views/Reports/ProfitabilityReport/ProfitabilityReportView.xaml` | U | 93 | 60 | 0 | 0 | 3 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/ProjectReport/ProjectReportView.xaml` | U | 150 | 62 | 0 | 0 | 1 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/SalesReportView.xaml` | U | 344 | 56 | 0 | 0 | 8 | 25 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Views/Reports/SalesmanReport/SalesmanReportView.xaml` | U | 77 | 55 | 0 | 0 | 0 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/StatementGenerator/Filter/FilterStep.xaml` | U | 233 | 58 | 0 | 0 | 3 | 8 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/StatementGenerator/Pipeline/ExecuteStep.xaml` | U | 163 | 36 | 0 | 0 | 1 | 4 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/StatementGenerator/Preview/PreviewStep.xaml` | U | 341 | 20 | 0 | 0 | 4 | 7 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  |  |
| `Views/Reports/StatementGenerator/Review/ReviewStep.xaml` | U | 590 | 73 | 0 | 0 | 23 | 17 | …/OrderReportGeneratorView.xaml<br>…/StatementGeneratorView.xaml |  | LOCAL-STYLE-FAMILY(23) |
| `Views/Reports/StatementGenerator/Shell/StatementGeneratorView.xaml` | U | 90 | 26 | 0 | 0 | 20 | 2 | …/StatementModuleView.xaml |  | LOCAL-STYLE-FAMILY(20) |
| `Views/Reports/StatementGenerator/Styles/WizardStyles.xaml` | R | 61 | 8 | 0 | 0 | 6 | 5 |  |  |  |
| `Views/Reports/StatementList/StatementListView.xaml` | U | 155 | 48 | 0 | 0 | 9 | 3 | …/StatementModuleView.xaml |  | LOCAL-STYLE-FAMILY(9) |
| `Views/Reports/StatementModule/StatementModuleView.xaml` | U | 1 | 0 | 0 | 0 | 2 | 2 | …/MainWindow.xaml |  |  |
| `Views/Reports/StatementReport/StatementReportView.xaml` | U | 190 | 68 | 0 | 0 | 0 | 1 | …/SalesReportView.xaml |  |  |
| `Views/Reports/TaxInvoiceReport/TaxInvoiceReportView.xaml` | U | 270 | 86 | 0 | 0 | 2 | 2 | …/SalesReportView.xaml |  |  |
| `Views/Reports/VatSubmission/VatSubmissionView.xaml` | U | 231 | 118 | 0 | 0 | 2 | 8 | …/MainWindow.xaml |  |  |
| `Views/Reports/Workflow/WorkflowProgressDialog.xaml` | W | 69 | 23 | 0 | 0 | 0 | 4 |  |  |  |
| `Views/Reports/Workflow/WorkflowWarningDialog.xaml` | W | 104 | 13 | 0 | 0 | 0 | 4 |  |  |  |
| `Views/Settings/Sections/Cards/AgentCoreCard.xaml` | U | 40 | 88 | 0 | 0 | 1 | 1 | …/SettingsAgentSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AgentDataCard.xaml` | U | 11 | 20 | 0 | 0 | 0 | 0 | …/SettingsAgentSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AiAssistantCard.xaml` | U | 14 | 33 | 0 | 0 | 0 | 0 | …/SettingsAiSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AiProvidersCard.xaml` | U | 52 | 43 | 0 | 0 | 6 | 0 | …/SettingsAiSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AiSecurityCard.xaml` | U | 11 | 25 | 0 | 0 | 0 | 0 | …/SettingsAiSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceAccentFontCard.xaml` | U | 24 | 43 | 0 | 0 | 0 | 1 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceCardEdgesTile.xaml` | U | 14 | 22 | 0 | 0 | 0 | 1 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceCrystalTile.xaml` | U | 10 | 20 | 0 | 0 | 0 | 0 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceDensityCard.xaml` | U | 3 | 4 | 0 | 0 | 0 | 0 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceFontColorTile.xaml` | U | 7 | 17 | 0 | 0 | 0 | 1 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceFrostTile.xaml` | U | 7 | 18 | 0 | 0 | 0 | 1 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceGradientTile.xaml` | U | 3 | 7 | 0 | 0 | 0 | 0 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceLanguageCard.xaml` | U | 25 | 37 | 0 | 0 | 0 | 0 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceModeCard.xaml` | U | 5 | 6 | 0 | 0 | 0 | 0 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearancePreviewTile.xaml` | U | 23 | 7 | 0 | 3 | 0 | 0 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceRadiusTile.xaml` | U | 7 | 8 | 0 | 0 | 0 | 0 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceSplashCard.xaml` | U | 9 | 7 | 0 | 1 | 0 | 0 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceThemeFamilyTile.xaml` | U | 3 | 7 | 0 | 0 | 0 | 0 | …/AppearanceThemeLayoutCard.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceThemeLayoutCard.xaml` | U | 52 | 95 | 0 | 0 | 2 | 0 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/AppearanceWallpapersCard.xaml` | U | 69 | 59 | 0 | 0 | 1 | 3 | …/SettingsAppearanceSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DataBackupCard.xaml` | U | 23 | 31 | 0 | 0 | 0 | 1 | …/SettingsDataSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DataHealthCard.xaml` | U | 30 | 14 | 0 | 0 | 0 | 1 | …/SettingsDataSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DataRootCard.xaml` | U | 14 | 26 | 0 | 0 | 0 | 0 | …/SettingsDataSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DataSampleCard.xaml` | U | 9 | 15 | 0 | 0 | 0 | 0 | …/SettingsDataSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoFfmpegCard.xaml` | U | 23 | 22 | 0 | 0 | 0 | 0 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoHistoryCard.xaml` | U | 4 | 19 | 0 | 0 | 0 | 0 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoIsolationCard.xaml` | U | 5 | 14 | 0 | 0 | 0 | 0 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoOutputCard.xaml` | U | 8 | 8 | 0 | 0 | 0 | 1 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoQualityCard.xaml` | U | 14 | 35 | 0 | 0 | 0 | 0 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoRecordCard.xaml` | U | 6 | 18 | 0 | 0 | 0 | 0 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/DemoWorkflowCard.xaml` | U | 9 | 19 | 0 | 0 | 0 | 1 | …/SettingsDemoSection.xaml |  |  |
| `Views/Settings/Sections/Cards/FeaturesDeliveryNoteCard.xaml` | U | 23 | 51 | 0 | 0 | 0 | 0 | …/SettingsFeaturesSection.xaml |  |  |
| `Views/Settings/Sections/Cards/FeaturesInquiryCard.xaml` | U | 32 | 72 | 0 | 0 | 0 | 0 | …/SettingsFeaturesSection.xaml |  |  |
| `Views/Settings/Sections/Cards/FeaturesJobOrderCard.xaml` | U | 29 | 65 | 0 | 0 | 0 | 0 | …/SettingsFeaturesSection.xaml |  |  |
| `Views/Settings/Sections/Cards/FeaturesProformaCard.xaml` | U | 82 | 189 | 0 | 0 | 0 | 0 | …/SettingsFeaturesSection.xaml |  |  |
| `Views/Settings/Sections/Cards/FeaturesSessionCard.xaml` | U | 7 | 11 | 0 | 0 | 0 | 0 | …/SettingsFeaturesSection.xaml |  |  |
| `Views/Settings/Sections/Cards/FeaturesTaxInvoiceCard.xaml` | U | 23 | 51 | 0 | 0 | 0 | 0 | …/SettingsFeaturesSection.xaml |  |  |
| `Views/Settings/Sections/SettingsAboutSection.xaml` | U | 38 | 63 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsAgentSection.xaml` | U | 1 | 1 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsAiSection.xaml` | U | 1 | 1 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsAppearanceSection.xaml` | U | 1 | 1 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml<br>…/SettingsView.xaml |  |  |
| `Views/Settings/Sections/SettingsDataSection.xaml` | U | 1 | 1 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsDemoSection.xaml` | U | 4 | 7 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsFeaturesSection.xaml` | U | 16 | 17 | 0 | 0 | 5 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsLicenseSection.xaml` | U | 7 | 24 | 0 | 0 | 0 | 0 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsSharingSection.xaml` | U | 41 | 56 | 0 | 0 | 0 | 1 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsThemesSection.xaml` | U | 88 | 88 | 0 | 3 | 3 | 1 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsUpdatesSection.xaml` | U | 61 | 36 | 0 | 0 | 6 | 1 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/Sections/SettingsVoiceSection.xaml` | U | 24 | 55 | 0 | 0 | 5 | 1 | …/SettingsPageTemplates.xaml |  |  |
| `Views/Settings/SettingsCardStyles.xaml` | R | 107 | 25 | 0 | 0 | 47 | 4 |  |  | LOCAL-STYLE-FAMILY(47) |
| `Views/Settings/SettingsPageTemplates.xaml` | R | 0 | 0 | 0 | 0 | 0 | 12 |  |  |  |
| `Views/Settings/SettingsSectionStyles.xaml` | R | 70 | 4 | 0 | 0 | 13 | 11 |  |  | LOCAL-STYLE-FAMILY(13) |
| `Views/Settings/SettingsView.xaml` | U | 19 | 23 | 0 | 0 | 1 | 2 | …/MainWindow.xaml<br>…/SettingsAgentSection.xaml |  |  |
| `Views/Shared/ActivityPanel.xaml` | U | 13 | 2 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Shared/AuditReportDialog.xaml` | W | 69 | 24 | 0 | 0 | 1 | 1 |  |  |  |
| `Views/Shared/ChangesDialog.xaml` | W | 34 | 5 | 0 | 0 | 0 | 1 |  |  |  |
| `Views/Shared/ComingSoonView.xaml` | U | 25 | 1 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Shared/CountryPhoneControl.xaml` | U | 1 | 2 | 0 | 0 | 0 | 1 | …/CompanyCenterView.xaml<br>…/JoProductionSection.xaml<br>…/CompanySetupView.xaml (+1) |  |  |
| `Views/Shared/LanguageSuggestionDialog.xaml` | W | 18 | 2 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Shared/QuickSettingsPanel.xaml` | U | 23 | 8 | 0 | 3 | 0 | 2 | …/MainWindow.xaml |  |  |
| `Views/Shared/RecordOverlayWindow.xaml` | W | 30 | 6 | 0 | 0 | 1 | 1 |  |  |  |
| `Views/Shared/ScreenRecordPanel.xaml` | U | 76 | 13 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Shared/ScreenshotPanel.xaml` | U | 22 | 2 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Shared/SectionActivityView.xaml` | U | 60 | 15 | 0 | 0 | 0 | 1 | …/DeliveryNoteView.xaml<br>…/DiagnosticsView.xaml<br>…/JobOrderView.xaml (+1) |  |  |
| `Views/Shared/StorageDetailsPanel.xaml` | U | 43 | 9 | 0 | 0 | 0 | 1 | …/MainWindow.xaml |  |  |
| `Views/Shared/StorageSidebarCard.xaml` | U | 25 | 3 | 0 | 0 | 0 | 1 | …/MainWindow.xaml |  |  |
| `Views/Shared/UnsavedChangesDialog.xaml` | W | 8 | 3 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Shared/UserSwitchPanel.xaml` | U | 35 | 4 | 0 | 0 | 0 | 0 | …/MainWindow.xaml |  |  |
| `Views/Shared/VoidReasonDialog.xaml` | W | 16 | 5 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/Sharing/ShareComposerWindow.xaml` | W | 92 | 14 | 0 | 0 | 2 | 3 |  |  |  |
| `Views/SheetStore/AddEditSheetView.xaml` | W | 31 | 22 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/SheetStore/SheetDetailWindow.xaml` | W | 50 | 156 | 0 | 0 | 24 | 1 |  |  | LOCAL-STYLE-FAMILY(24) |
| `Views/SheetStore/SheetStoreBuy/BuyDialogView.xaml` | W | 96 | 12 | 0 | 0 | 1 | 0 |  |  |  |
| `Views/SheetStore/SheetStoreCsv/DuplicateResolutionView.xaml` | W | 47 | 17 | 0 | 0 | 3 | 0 |  |  |  |
| `Views/SheetStore/SheetStoreHistory/HistoryView.xaml` | W | 86 | 22 | 0 | 0 | 1 | 2 |  |  |  |
| `Views/SheetStore/SheetStoreItems/ItemMovementView.xaml` | W | 51 | 21 | 0 | 0 | 7 | 0 |  |  |  |
| `Views/SheetStore/SheetStoreUse/UseDialogView.xaml` | W | 127 | 14 | 0 | 0 | 1 | 0 |  |  |  |
| `Views/SheetStore/SheetStoreView.xaml` | U | 390 | 123 | 0 | 0 | 8 | 4 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(8) |
| `Views/Specifications/SpecificationsCenterView.xaml` | U | 128 | 57 | 0 | 0 | 7 | 4 | …/MainWindow.xaml |  |  |
| `Views/Splash/SplashView.xaml` | W | 163 | 36 | 9 | 0 | 2 | 3 |  |  | HARDCODED-HEX(9) |
| `Views/SystemCenter/SystemCenterView.xaml` | U | 168 | 62 | 0 | 0 | 22 | 3 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(22) |
| `Views/TaxInvoice/GodView/TaxInvoiceGodView.xaml` | U | 33 | 18 | 0 | 0 | 1 | 1 | …/TaxInvoiceView.xaml |  |  |
| `Views/TaxInvoice/TaxInvoiceDetail/IssueDraftsDialog.xaml` | W | 20 | 7 | 0 | 0 | 0 | 1 |  |  |  |
| `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiAttachmentsSection.xaml` | U | 45 | 6 | 0 | 0 | 2 | 1 | …/TaxInvoiceDetailView.xaml |  |  |
| `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiFamilySection.xaml` | U | 84 | 15 | 0 | 0 | 1 | 1 | …/TaxInvoiceDetailView.xaml |  |  |
| `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiOverviewSection.xaml` | U | 179 | 64 | 0 | 0 | 1 | 2 | …/TaxInvoiceDetailView.xaml |  |  |
| `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml` | R | 12 | 1 | 0 | 0 | 3 | 1 |  |  |  |
| `Views/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailView.xaml` | U | 83 | 26 | 0 | 0 | 5 | 1 | …/TaxInvoiceView.xaml |  |  |
| `Views/TaxInvoice/TaxInvoiceList/ProformaPickerDialog.xaml` | W | 11 | 3 | 0 | 0 | 0 | 0 |  |  |  |
| `Views/TaxInvoice/TaxInvoiceList/TaxInvoiceListView.xaml` | U | 115 | 62 | 0 | 0 | 11 | 1 | …/TaxInvoiceView.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/TaxInvoice/TaxInvoiceView.xaml` | U | 13 | 6 | 0 | 0 | 11 | 1 | …/MainWindow.xaml |  | LOCAL-STYLE-FAMILY(11) |
| `Views/UserCenter/UserCenterView.xaml` | U | 105 | 46 | 0 | 3 | 2 | 1 | …/MainWindow.xaml |  |  |

## Parse failures (must stay zero)

None — every .xaml file in the repository parses as well-formed XML.

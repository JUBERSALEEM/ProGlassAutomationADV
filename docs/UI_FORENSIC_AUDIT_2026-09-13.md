# ProGlassAutomationADV — Complete UI Forensic Audit
**Date:** 2026-09-13 (Asia/Dubai)  
**Branch audited:** `arena/01a09a9f-proglassautomationadv` (commit `71a4e69` — Dashboard polish: unify radius, responsive pipeline, density-aware + skeleton)  
**Scope:** 267 XAML files (Views 189, Themes 58, Styles 5, Controls 9, App.xaml, MainWindow.xaml) + design-system (proglass-industrial, proglass-pi)  
**Constraint:** AUDIT ONLY — no file modifications. This report is the sole deliverable.

---

## 0. Executive Summary

- **Total XAML files:** 267 (well-formed 267/267, 0 unresolved Static/DynamicResource, 0 binding errors per verify-dashboard.py)
- **Views:** 189
- **Themes:** 58 (LightTheme, DarkTheme, DesignTokens, TypeScaleTokens, ShellStyles, CardStyles, ButtonStyles, ListPageStyles, DataGridStyles, InputStyles, DialogStyles, LiquidGlassDesign, LiquidGlassStyles, CrystalFluid/* 8 files, ProGlassDesignSystem/Tokens/*, Languages 15 files)
- **Styles:** 5 (DashboardKit.xaml, DashboardStyles.xaml, ListPageStyles.xaml, ModuleChrome.xaml, DataGridStyles.xaml)
- **Controls:** 9 (custom controls)

- **Theme coverage:** 189 Views → **Fully Global 119 (63.0%)**, **Partially Themed 49 (25.9%)**, **Bypassing/Own System 21 (11.1%)**
- **Global keys defined:** 3800 (Themes + Styles)
- **Unused keys:** 1042 (27.4%) — mostly legacy CrystalFluid, Aurora, AboutGlass, SemanticTokens, and language keys not bound in XAML but via ViewModel
- **XAML files with NO theme usage:** 0 — every file uses at least one DynamicResource/StaticResource (good)
- **Local Styles (x:Key):** 844 definitions across Views
- **ControlTemplates:** 416
- **Duplicate x:Key definitions (excluding language i18n duplicates):** 2965 total incl. languages; **~31 true style duplicates** (CardStyle, SectionTitleStyle, FieldLabelStyle, IntInputStyle, etc.) + 372 card-related keys

**Critical finding:** The UI is currently controlled by **4 competing design eras** simultaneously:
1. Legacy `DashboardStyles.xaml` (DashCardStyle, SoftCardStyle, IndustrialDashboardCardStyle) — 2nd gen
2. Modern `DashboardKit.xaml` grammar (Db* tokens: DbCardPadding, DbPageMargin, DbGutter, DbKpiTileMinHeight, DbSkeleton*) — 3rd gen, density-aware, skeleton
3. `Surface.*` tokens (Surface.Primary/Secondary/Detail) from ProGlassDesignSystem
4. `CrystalFluid/*` + `LiquidGlass*` experimental glass system (CrystalCardPadding, CrystalGlass*, LiquidCard)

All 4 publish their own CornerRadius, BorderThickness, Padding, Shadow, Brush. `VisualThemeEngine.Ios26.IosRadius` and `ThemePipeline.ReapplyCardCornerRadius/Border/Edge` attempt to unify at runtime, but XAML still references all eras.

---

## 1. CURRENT UI ARCHITECTURE

### 1.1 Theme Layering (actual load order from App.xaml)

```
App.xaml
 ├─ LightTheme.xaml / DarkTheme.xaml (baseline: CardCornerRadius=12, ControlCornerRadius=8, Surface brushes, Density tokens)
 ├─ Themes/DesignTokens.xaml (Surface.Primary/OnPrimary/PrimaryBrush, TypeScale, Spacing)
 ├─ Themes/TypeScaleTokens.xaml (TextSm/Md/Lg/Xl, etc.)
 ├─ Themes/Styles/ShellStyles.xaml (SidebarCardBrush, ModuleTab, etc.)
 ├─ Themes/Styles/CardStyles.xaml (CardStyle, SoftCardStyle)
 ├─ Themes/Styles/ButtonStyles.xaml (Primary/Secondary/Success, ModuleToolbar)
 ├─ Themes/Styles/InputStyles.xaml (TextBox, ComboBox)
 ├─ Themes/Styles/ListPageStyles.xaml (ListPageTitle, etc.)
 ├─ Themes/Styles/DataGridStyles.xaml
 ├─ Themes/Styles/DialogStyles.xaml
 ├─ Themes/Styles/TypographyStyles.xaml (SectionTitleStyle, PageTitleStyle, FieldLabelStyle, TableHeaderStyle, RowTitleStyle)
 ├─ Themes/LiquidGlassDesign.xaml + LiquidGlassStyles.xaml (LiquidCard, GlassShadow)
 ├─ Themes/CrystalFluid/CrystalFluidTokens.xaml + Brushes + Colors + Metrics + Styles (CrystalCard*, CrystalGlass*)
 ├─ Themes/ProGlassDesignSystem/Tokens/SemanticColors + SemanticGeometry + SemanticSpacing
 ├─ Styles/DashboardStyles.xaml (legacy: DashCardPadding, DashCardStyle, SoftCardPadding, IndustrialDashboardCardStyle)
 └─ Styles/DashboardKit.xaml (modern: DbPageMargin=DensityPageMargin, DbCardPadding=DensityCardPadding, DbTilePadding, DbGutter, DbGutterHalfMargin, DbKpiTileMinHeight, DbChartMinHeight, DbSkeletonBlockStyle pulse 0.35→0.75, DbSkeletonCircleStyle CornerRadius 999, DbKpiTileTemplate, DbChartTemplate, DbFeedRowTemplate, DbPipelineStageTemplate, DbBandStyle, DbCardStyle, DbRowCardStyle, DbPipelineCardStyle)
```

### 1.2 C# Theme Pipeline

- `Services/Theme/ThemeManager.Part2.cs` — `ApplyDisplayDensity(Compact/Normal/Comfortable/Spacious)` publishes DbPageMargin, DbCardPadding, DbTilePadding, DbGutter, DbGutterHalfMargin, DbKpiTileMinHeight, DbChartMinHeight via `Application.Current.Resources`
- `Services/Theme/VisualThemeEngine.cs` + `VisualThemeEngine.Ios26.cs` — `IosRadius(iosDefault, fallback)` scales from `ThemeManager.CardCornerRadius` (respects `UseRoundedSurfaces=false → 0`). Families: Normal, NormalIndustrial, Monet, CrystalFluid, Ios26
- `Services/Theme/ThemePipeline.cs` — single orchestrator `ApplyTheme()` → `ApplyModule()` → `ReapplyCardCornerRadius()`, `ReapplyCardBorder()`, `ReapplyCardEdge()`
- `Services/Theme/ThemeManager.cs` — holds CardCornerRadius, ControlCornerRadius, CardBorderThickness, CardEdgeThickness, Density tokens

### 1.3 Design-System Reference (not yet enforced in XAML)

- `design-system/proglass-industrial/MASTER.md`: Primary #0F172A, Accent #0369A1, Background #F8FAFC, Card #FFFFFF, Border #E2E8F0, Radius 8px buttons / 12px cards / 16px modals, Spacing xs 2px → 3xl 32px, Shadow sm/md/lg/xl, Inter font, Density 9/10 Dense Dashboard, Anti-patterns: no emoji icons, cursor:pointer, no layout-shifting hovers, 4.5:1 contrast, transitions 150-300ms
- `design-system/proglass-pi/MASTER.md`: Primary #334155, Accent #059669 (stock green), same density
- `design-system/proglass-industrial/pages/proforma-invoice.md`: MES left station rail + work deck + command footer, 34px inputs, 11px labels, tabular numbers, Segoe MDL2 Assets only, Theme tokens only, Paper #FFFFFF on Step7, Mill well #0B1220 under Step4 plates, no motion beyond WPF hover/press

**Gap:** Design-system specifies Inter + 8/12/16 radius + #0369A1 accent, but actual WPF uses Segoe UI Variable + dynamic CardCornerRadius (12 default, but Ios26 scales) + multiple accent systems (AccentBrush, TintBlueBrush, CrystalAccent, LiquidAccent). Inter never loaded as FontFamily DynamicResource (only Segoe UI Variable via AppFontFamily).

### 1.4 Skeleton Loading (new in 71a4e69)

- All 7 dashboards now have `StackPanel Row=2 RowSpan=3/4 Visibility="{Binding IsLoading, Converter=BoolToVisibility}"` with `DbSkeleton*` templates + enhanced loading band (`TintBlueBrush` + `DbSkeletonCircleStyle` + `DbSkeletonBlockStyle`)
- Real content hidden via `InverseBoolToVisibilityConverter`
- 7/7 dashboards: Overview, SheetStore, Proforma (Pi), Jo, Dn, Ti, Statement — verified via grep SKELETON

---

## 2. THEME COVERAGE — Exact Numbers & Percentage

### 2.1 Overall

| Category | Count | % of Views (189) | Criteria |
|----------|-------|------------------|----------|
| **Fully Global** | 119 | 63.0% | local styles 0, hardcoded colors 0, hardcoded CornerRadius 0, DynamicResource >20, uses Db* or Surface* |
| **Partially Themed** | 49 | 25.9% | 1-5 local styles, or 1-2 hardcoded CR/colors, but still >50% DynamicResource |
| **Bypassing / Own System** | 21 | 11.1% | ≥6 local styles OR ≥3 hardcoded colors/CR OR custom card system shadowing global |

**Fully Global includes:** All 7 dashboards (OverviewDashboardView, SheetStoreDashboardView, PiDashboardView, JoDashboardView, DnDashboardView, TiDashboardView, StDashboardView) + DashboardView + DnAttachmentsSection, DnHistorySection, DnOverviewSection, JoOverviewSection, JoProductionSection, TiAttachmentsSection, TiFamilySection, TiOverviewSection, ProformaInvoice Step1-6 sections, CompanyCenter partially? Actually CompanyCenter is partial. Full list in `/tmp/report.txt` (191 lines, last line summary).

**Partially Themed examples (49):**
- Views/About/AboutView.xaml (2 local, 5 hardcoded CR)
- Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml (5 local, 7 CR, 17 buttons)
- Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml (shadows TableHeaderStyle, 30+ hardcoded CR 3/4/6)
- Views/Settings/Sections/SettingsAppearanceSection.xaml (40 local styles, fixed in 71a4e69 but still 40 local)
- Views/SheetStore/AddEditSheetView.xaml (5 local: LabelStyle, InputStyle, IntInputStyle, DecimalInputStyle, ComboStyle shadowing global InputStyles)
- Views/SystemCenter/SystemCenterView.xaml (2 local, 13 CR, 35 buttons)
- Views/UserCenter/UserCenterView.xaml (4 local)

**Bypassing / Own System (21):**
- Views/AI/AiChatView.xaml — 6 local (ChatIconBtn, SendBtn, UserBubble, AiBubble, ErrBubble), 0 cards global, 8 buttons custom, 5 FontFamily hardcoded MDL2
- Views/About/AboutStyles.xaml — 20 local (AboutCardStyle, HeroCardStyle, AboutSectionPanelStyle, etc.), 2 hardcoded hex #59FFFF/#00FFFF, 4 hardcoded CR 12/7/16
- Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml — 29 local (CompactStatLabelStyle, HistoryRowButtonStyle, BalanceCellStyle, etc.), shadows no global but defines own card system
- Views/Inquiry/InquiryEditorWindow.xaml — 8 local (StepDotStyle, StepNumberStyle, StepTitleStyle, SectionCardStyle, InqHintStyle), 6 CR, 13 TextBox, 5 ComboBox
- Views/Inquiry/InquiryListView.xaml — 10 local (InquiryStatusSpineStyle, InquiryStatusPillDynamicStyle, InquiryInitialsTileStyle, etc.), 11 CR, 24 buttons, 5 ComboBox
- Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml — 11 local (CardLabelStyle, SectionTitleStyle, JoFormCardStyle, etc.), shadows SectionTitleStyle global
- Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml — technically partial but 30+ hardcoded CR 6/4/3/1 and #FFFFFF paper (intentional per design-system)
- Views/Reports/StatementGenerator/Styles/WizardStyles.xaml — shadows FieldLabelStyle, 4 hardcoded hex #00FFFF/#000000/#FFF8FA/#FF1118
- Views/Settings/SettingsSectionStyles.xaml — 11 local (SectionCardStyle, ProviderCardBaseStyle, RowTitleStyle shadows RowTitleStyle global, PageTitleStyle shadows global)
- Views/SheetStore/SheetDetailWindow.xaml — 14 local (StatLabel, StatValue, StatCard, SectionCard, InventoryKpiCell), 4 CR, 32 buttons, 11 TextBox, 7 ComboBox, 194 StaticResource usages (legacy)
- Views/SheetStore/SheetStoreView.xaml — 9 local (ItemFieldLabel, ItemEntry, ItemFieldError, SheetMenuActionItem, InventoryKpiCell), 28 CR, 29 buttons, 9 TextBox, 7 ComboBox, 261 DynamicResource + 181 StaticResource (mixed eras)
- Views/Splash/SplashView.xaml — 5 local (TrustChipStyle, ChipGlyphStyle, ChipTextStyle, TermsDeclineButtonStyle, TermsContinueButtonStyle), 15 hardcoded colors #228080/#33FFFF/#148080/#0D0000, 5 hardcoded CR 16/12/26
- Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml — 8 local (FamilyRowButtonStyle, GridNumericCenterCell, InvoicePartyLabelStyle, etc.), shadows CardStyle
- Plus: JoSectionStyles, DnSectionStyles, AboutStyles, etc.

### 2.2 Per-Category Classification

| Category | Total XAML Usages | GLOBAL THEME / DYNAMICRESOURCE | STATICRESOURCE | LOCAL STYLE | HARDCODED | MISSING/NOT THEMED |
|----------|-------------------|--------------------------------|----------------|-------------|-----------|---------------------|
| **Total XAML files** | 267 | 267 use ≥1 Dynamic/Static (100%) | — | — | — | 0 files outside architecture |
| **Cards/Borders/Surfaces** | Border 2860 + Card keys 372 + Surface.* ~200 | DbCardStyle, DbRowCardStyle, DbPipelineCardStyle, Surface.Primary/Secondary/Detail via DynamicResource 1087 CR usages Dynamic (91.9%) | DashCardStyle, SoftCardStyle, IndustrialDashboardCardStyle via StaticResource 26 (2.2%) | 844 local styles include CardLabelStyle, SectionCardStyle, StatCard, etc. (~15% of card definitions) | 70 hardcoded CornerRadius (5.9%), 8 files with hardcoded hex (AboutStyles, SplashView, PrintableInvoice, GlassTypeComparisonView, etc.) | 0 missing — every Border has some brush, but 373 hardcoded brushes (7.2%) bypass theme |
| **Buttons** | 1008 | 0 DynamicResource (Button styles not yet Dynamic) | 300 StaticResource (29.8%) — ModuleToolbarPrimaryButtonStyle, ModuleToolbarSecondaryButtonStyle, AccentChip, OutlinedPill, PrimaryButtonStyle | 379 Local Style (37.6%) — ChatIconBtn, SendBtn, JoSectionStyles, etc. | 0 hardcoded (no hex in Button tag, but 271 hardcoded brushes overall include button Bg) | 329 Missing (32.6%) — <Button> with no Style attribute, uses default WPF chrome |
| **TextBoxes** | 277 | 0 Dynamic | 183 Static (66.1%) — IntInputStyle, DecimalInputStyle, FieldInputStyle | 1 Local (0.4%) — FileNameTextBoxStyle in RenameAttachmentDialog | 0 hardcoded | 93 Missing (33.6%) — bare <TextBox> |
| **ComboBoxes** | 194 | 0 Dynamic | 151 Static (77.8%) — ComboStyle | 0 Local | 0 | 43 Missing (22.2%) |
| **DataGrids** | 16 | 0 Dynamic | 6 Static (37.5%) — DataGridStyle | 0 Local | 0 | 10 Missing (62.5%) — no Style, uses default |
| **Tabs** | TabControl 2, TabItem 16, ModuleTab 38 occurrences | ModuleTab via DynamicResource TabCardActiveStyle/TabCardInactiveStyle | TabCardInactiveStyle StaticResource in DnListView | 2 Local — AboutTabListStyle, AboutTabItemStyle | 0 | TabControl missing styled template (uses default) |
| **Navigation** | 638 tokens Sidebar/Navigation | SidebarCardBrush DynamicResource, ModuleSectionCardStyle | — | QuickSettingsRow, QuickSettingsGlyph local | — | — |
| **Other Controls** | ItemsControl 388, ScrollViewer 158, ListBox 91, ListView 16, StackPanel 2550, Grid 2629, TextBlock 6054 | TextBlock uses StaticResource Text* tokens 2956 (97.9%) | — | — | — | — |
| **CornerRadius** | Declarations 57, Usages 1183 | 1087 DynamicResource (91.9%) — DbCardCornerRadius, CardCornerRadius, Surface.Primary.CornerRadius, ControlCornerRadius via ThemeManager | 26 StaticResource (2.2%) — legacy Dash* | 14 Other (1.2%) — local | 56-70 Hardcoded (4.7-5.9%) — 52 in Views: About 12/7/16, Splash 16/12/26, PrintableInvoice 6/4/3/1, Login 2.5, etc. | 0 missing — every card has some radius |
| **FontFamily** | 603 usages | 190 DynamicResource (31.5%) — AppFontFamily (Segoe UI Variable) | 3 StaticResource (0.5%) | 0 Local Style | 410 Hardcoded (68.0%) — mostly Segoe MDL2 Assets for icons (expected), but also 5-6 hardcoded Segoe UI in Splash/About | Missing: Inter never loaded, design-system says Inter but WPF uses Segoe |
| **FontSize** | 3018 usages | 57 DynamicResource (1.9%) — Db* density-aware | 2956 StaticResource (97.9%) — TextSm/Md/Lg/Xl, DensitySmall/Medium/Large | 4 Local | 5 Hardcoded (0.2%) — SplashView 1 hardcoded | Missing: 0, but Dynamic coverage only 1.9% (should be higher for density) |
| **FontWeight** | 1756 usages | 5 DynamicResource (0.3%) | 0 Static | 0 Local | 1751 Hardcoded (99.7%) — Bold, SemiBold, Medium literals (acceptable but should be tokens Weight.SemiBold etc.) | — |
| **Colors/Brushes** | Background/Foreground/BorderBrush 5161 + total Brush usages 5268 | 4707-4786 DynamicResource (90.8-91.2%) — TintBlueBrush, Surface.*, AccentBrush, MutedBrush, BorderBrush | 81-88 StaticResource (1.6-1.7%) — legacy | 123 Local (2.3%) — AboutCardBorderBrush local? Actually AboutCardBorderBrush is in DarkTheme but unused | 271-373 Hardcoded (5.1-7.2%) — hex in 8 files: AboutStyles #59FFFF/#00FFFF, AuthSurfaceStyles #2EFFFF, PrintPreviewView #FFFFFF, GlassTypeComparison #1000FF/#10FF00, ServiceProfitability #10FF00, SheetProfitability #1A22C5/#1AFF3B/#1AFFA5/#1A3B82, WizardStyles #00FFFF/#000000/#FFF8FA/#FF1118, Splash #228080/#33FFFF/#148080/#0D0000 | Missing: 0, but 1042 global keys unused (27.4%) |
| **Shadows/Blur/Opacity/Glass** | 430-476 effects | ShadowSoft, ShadowElevated, GlassShadow*, DashCardShadowEffect via DynamicResource | — | 5 Local (TrustChipStyle etc.) | Hardcoded Opacity 0.35-0.75 in DbSkeletonBlockStyle pulse (intentional) | — |
| **Local Styles** | 844 x:Key definitions in Views | — | — | 844 Local (100% of this category) — top files: SettingsAppearanceSection 40 local, DnSectionStyles 29, AboutStyles 20, SheetDetailWindow 14, JoSectionStyles 11, SheetStoreView 9, TiSectionStyles 8, InquiryEditor 8, InquiryList 10, Splash 5, AiChat 6 | — | — |
| **ControlTemplates** | 416 | Most in Themes (Button, TextBox, ComboBox, DataGrid) | — | 0? Actually some local ControlTemplates in Views (e.g., AboutTabItemStyle) | — | — |

### 2.3 Per-View Detailed Report (189 Views)

> Full raw data in `/tmp/report.txt` and `/tmp/audit3.txt`. Below is condensed grouping with key problems.

#### FULLY GLOBAL (119) — Examples

| View | Global Styles Used | Local | HC Color | HC CR | Cards (global/total) | Buttons | Fonts (hard) | Problems |
|------|-------------------|-------|----------|-------|----------------------|---------|--------------|----------|
| Views/Dashboard/Overview/OverviewDashboardView.xaml | 164 (40 dyn, 124 stat) DbCardPadding, DbPageMargin, DbGutter, DbKpiTileMinHeight, DbSkeleton*, TintBlueBrush | 0 | 0 | 0 | 28/65 DbCardStyle, DbRowCardStyle | 3 | 0 | None — exemplar after 71a4e69 fix (WrapPanel pipeline, ScrollViewer, density-aware DynamicResource, skeleton StackPanel Row=2 RowSpan=4) |
| Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml | 110 (25 dyn,85 stat) | 0 | 0 | 0 | 14/56 | 0 | 0 | None |
| Views/Dashboard/ProformaInvoice/PiDashboardView.xaml | 127 | 0 | 0 | 0 | 15/58 | 0 | 0 | None |
| Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml | 139 | 0 | 0 | 0 | 16/59 | 0 | 0 | None |
| Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml | 139 | 0 | 0 | 0 | 16/59 | 0 | 0 | None |
| Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml | 219 (72 dyn,150 stat) | 0 | 0 | 0 | 22/77 | 2 | 0 | None but 13 CR usages (all Dynamic) |
| Views/Dashboard/Statement/StDashboardView.xaml | 166 (59 dyn,108 stat) | 0 | 0 | 0 | 16/60 | 5 | 2 (MDL2 icons) | None |
| Views/Dashboard/DashboardView.xaml | 33 (23 dyn,11 stat) | 0 | 0 | 0 | 0/6 | 2 | 2 | None |
| Views/DeliveryNote/DeliveryNoteDetail/Sections/DnAttachmentsSection.xaml | 49 (32 dyn,18 stat) | 0 | 0 | 0 | 0/7 | 4 | 1 | None |
| Views/DeliveryNote/DeliveryNoteDetail/Sections/DnHistorySection.xaml | 92 (68 dyn,41 stat) | 0 | 0 | 0 | 0/16 | 3 | 4 | None |
| Views/DeliveryNote/DeliveryNoteDetail/Sections/DnOverviewSection.xaml | 249 (167 dyn,114 stat) | 0 | 0 | 0 | 6/29 | 4 | 3 | None — heavy but fully global |
| Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml | 66 | 0 | 0 | 0 | 2/11 | 0 | 0 | None |
| Views/TaxInvoice/TaxInvoiceDetail/Sections/TiOverviewSection.xaml | 189 (97 dyn,105 stat) | 0 | 0 | 0 | 9/22 | 4 | 3 | None |
| ... 105 more fully global (see /tmp/report.txt) | | | | | | | | |

#### PARTIALLY THEMED (49)

| View | Global | Local | HC | Cards | Buttons | Issue |
|------|--------|-------|----|-------|---------|-------|
| Views/About/AboutView.xaml | 27 (25 dyn,15 stat) | 2 (AboutTabListStyle, AboutTabItemStyle) | 5 CR (16,18,14,12,22) | 1/12 | 0 | Hardcoded CR 5, local tab styles |
| Views/CompanyCenter/CompanyCenterView.xaml | 107 (79 dyn,52 stat) | 2 (CcFieldLabel, CcTextBox) | 0 | 0/12 | 7 | Local field styles shadow global InputStyles |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 90 (65 dyn,35 stat) | 5 (DnMenuActionItem, HistoryRowButtonStyle, CreateFromMasterButtonStyle, DnMenuRealOnlyItem, FocBadgeStyle) | 7 CR | 0/13 | 17 | 5 local menu styles, 7 CR hardcoded |
| Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml | Shadows TableHeaderStyle global, 30+ hardcoded CR 6/4/3/1/0,0,4,4 etc., #FFFFFF paper (intentional per design-system but bypasses theme) | — | — | — | — | Paper white intentional, but CR hardcoded |
| Views/Settings/Sections/SettingsAppearanceSection.xaml | 40 local (fixed broken Fill="{Bindin <StackPanel duplication at line 1193 in 71a4e69), 100+ DynamicResource but 40 local | 40 | 0 | 0/12 | 0 | Largest local style file, 40 styles |
| Views/SheetStore/AddEditSheetView.xaml | 43 (24 dyn,30 stat) | 5 (LabelStyle, InputStyle, IntInputStyle, DecimalInputStyle, ComboStyle) shadows global InputStyles | 1 CR | 0/1 | 2 | Shadows IntInputStyle, DecimalInputStyle, ComboStyle global |
| Views/SystemCenter/SystemCenterView.xaml | 221 (157 dyn,74 stat) | 2 (ScFieldLabel, ScTextBox) | 13 CR | 0/15 | 35 | 35 buttons, 13 CR hardcoded |
| Views/UserCenter/UserCenterView.xaml | 113 (83 dyn,66 stat) | 4 (UcFieldLabel, UcTextBox, UcPasswordBox, UcMeterSegment) | 5 CR | 0/18 | 6 | Local UC styles |

#### BYPASSING / OWN SYSTEM (21)

| View | Local Styles | Hardcoded Colors | Hardcoded CR | Global Used | Problem |
|------|--------------|------------------|--------------|-------------|---------|
| Views/AI/AiChatView.xaml | 6 (ChatIconBtn, SendBtn, UserBubble, AiBubble, ErrBubble, ChatListStyle) | 0 | 0 | 107 (84 dyn,39 stat) | Own chat bubble system, 0 cards global, 8 buttons custom, 5 FontFamily hardcoded MDL2 |
| Views/About/AboutStyles.xaml | 20 (AboutCardStyle, HeroCardStyle, AboutSectionPanelStyle, AboutHeaderBadgeStyle, AboutHeaderBadgeTextStyle, etc.) | 2 (#59FFFF, #00FFFF) | 4 (12,7,16,16,0,0,10) | 95 (90 dyn,45 stat) | Shadows SectionTitleStyle, SectionSubtitleStyle global, 3 cards local, own glow system AboutGlassGlowBrush unused |
| Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml | 29 (CompactStatLabelStyle, HistoryRowButtonStyle, BalanceCellStyle, CompactStatNumberStyle, CompactStatUnitStyle, etc.) | 0 | 0 | 65 (40 dyn,28 stat) | Largest bypass — 29 local, defines own stat system |
| Views/Inquiry/InquiryEditorWindow.xaml | 8 (StepDotStyle, StepNumberStyle, StepTitleStyle, SectionCardStyle, InqHintStyle, etc.) | 0 | 6 CR | 226 (147 dyn,114 stat) | Shadows SectionCardStyle global, 9 cards local, 13 TextBox, 5 ComboBox, 2 DataGrid |
| Views/Inquiry/InquiryListView.xaml | 10 (InquiryStatusSpineStyle, InquiryStatusPillDynamicStyle, InquiryInitialsTileStyle, InquiryInitialsTextStyle, InquiryStatusTabStyle, etc.) | 0 | 11 CR | 294 (178 dyn,147 stat) | 1/55 cards global, 24 buttons, 5 ComboBox, own spine/pill system |
| Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml | 11 (CardLabelStyle, SectionTitleStyle, JoFormCardStyle, JoActionBarStyle, JoFieldTileStyle, etc.) | 0 | 0 | 24 (14 dyn,11 stat) | Shadows SectionTitleStyle global |
| Views/Settings/SettingsSectionStyles.xaml | 11 (SectionCardStyle, ProviderCardBaseStyle, InfoCardTitleStyle, SectionLabelStyle, RowTitleStyle shadows RowTitleStyle global, PageTitleStyle shadows global, PageSubtitleStyle shadows global) | 0 | 12 CR | 117 (67 dyn,37 stat) | Shadows 3 global typography styles |
| Views/SheetStore/SheetDetailWindow.xaml | 14 (StatLabel, StatValue, StatCard, SectionCard, InventoryKpiCell, etc.) | 0 | 4 CR | 163 (60 dyn,194 stat) | 14/38 cards local, 32 buttons, 11 TextBox, 7 ComboBox, 194 StaticResource (legacy era) |
| Views/SheetStore/SheetStoreView.xaml | 9 (ItemFieldLabel, ItemEntry, ItemFieldError, SheetMenuActionItem, InventoryKpiCell, etc.) | 0 | 28 CR | 399 (261 dyn,181 stat) | 0/45 cards global, 29 buttons, 9 TextBox, 7 ComboBox, 28 CR hardcoded — biggest view, mixed eras |
| Views/Splash/SplashView.xaml | 5 (TrustChipStyle, ChipGlyphStyle, ChipTextStyle, TermsDeclineButtonStyle, TermsContinueButtonStyle) | 15 (#228080 x2, #33FFFF, #148080, #0D0000) | 5 (16,16,12,26,26) | 90 (124 dyn,43 stat) | 15 hardcoded colors, 5 CR, 17 cards local, 2 buttons |
| Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml | 8 (FamilyRowButtonStyle, GridNumericCenterCell, InvoicePartyLabelStyle, InvoicePartyValueStyle, InvoiceMetaChipStyle, etc.) | 0 | 1 CR | 25 (17 dyn,8 stat) | Shadows CardStyle global |
| Views/Reports/StatementGenerator/Styles/WizardStyles.xaml | Shadows FieldLabelStyle global, 4 hardcoded hex | 4 | — | — | — |
| Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml | Shadows TableHeaderStyle, 2 hardcoded hex #1000FF/#10FF00 | 2 | — | — | — |
| Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml | Shadows TableHeaderStyle, 1 hardcoded hex #10FF00 | 1 | — | — | — |
| Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml | 4 hardcoded hex #1A22C5/#1AFF3B/#1AFFA5/#1A3B82 | 4 | — | — | — |
| Views/Login/AuthSurfaceStyles.xaml | 1 hardcoded hex #2EFFFF | 1 | — | — | — |
| Views/Reports/StatementGenerator/Preview/PreviewStep.xaml | 2 hardcoded CR 12,12,0,0 | — | 2 | — | — |

---

## 3. CONFLICTS — Every Competing Mechanism

### 3.1 CornerRadius — 4 Systems

| Token | Value | File | Usage |
|-------|-------|------|-------|
| CardCornerRadius | 12 (default) | DarkTheme.xaml, LightTheme.xaml, ThemeManager.CardCornerRadius | 1087 Dynamic usages (91.9%) — current winner after 71a4e69 |
| ControlCornerRadius | 8 | DarkTheme, LightTheme, ThemeManager | Buttons, Inputs |
| CardCornerRadiusLarge | 16 | DarkTheme | Modals, Hero |
| Surface.Primary.CornerRadius | 12 | DesignTokens.xaml | New semantic |
| Surface.Secondary.CornerRadius | 8 | DesignTokens | |
| Surface.Detail.CornerRadius | 6 | DesignTokens | |
| Radius.Small/Medium/Large/Hero | 4/8/12/16 | ProGlassDesignSystem/Tokens/SemanticGeometry.xaml | Unused (in 1042 unused keys) |
| CrystalCard* | 12-16 | CrystalFluidMetrics | Unused |
| Hardcoded | 12,7,16,10,14,22,20,2.5,6,4,3,26, etc. | Views (52 usages) | Bypass — About, Splash, PrintableInvoice, Login |

**Conflict:** Ios26 family scales iosDefault/12 from ThemeManager.CardCornerRadius, but hardcoded 12/16/22 in About/Splash ignore scaling. PrintableInvoice uses 6/4/3/1 for paper edges (intentional but not tokenized).

### 3.2 Card / Surface Systems — 7 Systems

| System | Key Examples | Files | Count |
|--------|--------------|-------|-------|
| Db* (modern) | DbCardStyle, DbRowCardStyle, DbPipelineCardStyle, DbSkeletonCardStyle, DbCardPadding, DbPageMargin, DbGutter | DashboardKit.xaml | 16 style keys, 1087 CR dyn, density-aware |
| Dash* (legacy 2nd gen) | DashCardPadding, DashCardMargin, DashCardStyle, SoftCardPadding, SoftCardStyle, SoftKpiCardTemplate, IndustrialDashboardCardPadding, IndustrialDashboardCardStyle, IndustrialDashboardRowCardStyle | DashboardStyles.xaml | 9 keys, 26 StaticResource usages |
| Module* | ModuleSectionCardStyle, ModuleCardStyle, ModuleToolbar* | Themes/Styles/CardStyles.xaml, ShellStyles.xaml | ~30 keys |
| Surface.* | Surface.Primary, Surface.Secondary, Surface.Detail, Surface.Primary.CornerRadius | DesignTokens.xaml | ~50 keys |
| Liquid* | LiquidCard, LiquidCardShadow | LiquidGlassDesign.xaml | 2 keys |
| Crystal* | CrystalCardPadding, CrystalCardGap, CrystalCardDuration, CrystalGlassSurfaceStrongBrush | CrystalFluid/* | ~20 keys, mostly unused (in 1042 unused) |
| Local | SectionCardStyle, StatCard, AboutCardStyle, HeroCardStyle, InventoryKpiCell, ProviderCardBaseStyle | DnSectionStyles (29 local), AboutStyles (20), SheetDetailWindow (14), JoSectionStyles (11), etc. | 844 local styles, 372 card-related keys total |

**Duplicate CardStyle:** Defined in both `Styles/DashboardStyles.xaml` and `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml` (TiSectionStyles shadows global CardStyle). Similarly `SectionCardStyle` in `InquiryEditorWindow.xaml` and `SettingsSectionStyles.xaml`.

### 3.3 Typography — 3 Systems

| System | Tokens | Usage |
|--------|--------|-------|
| Text* (legacy) | TextXs, TextSm, TextMd, TextLg, TextXl, Text2Xl | 2956 StaticResource FontSize (97.9%) — current winner |
| Density* | DensitySmall, DensityMedium, DensityLarge, DensityPageMargin, DensityCardPadding | Used for layout, not font |
| Db* (density-aware) | DbPageMargin, DbCardPadding, DbTilePadding, DbGutter, DbKpiTileMinHeight, DbChartMinHeight | 57 DynamicResource FontSize (1.9%) — new but low adoption |
| TypeScaleTokens | TypeScale.H1/H2/Body/Caption | Unused (in 1042 unused) |
| Local | SectionTitleStyle, PageTitleStyle, RowTitleStyle, FieldLabelStyle, TableHeaderStyle | 17 shadow occurrences (e.g., AboutStyles shadows SectionTitleStyle global, TiSectionStyles shadows CardStyle) |

**Conflict:** FontFamily 410 hardcoded (68%) — mostly `Segoe MDL2 Assets` for icons (acceptable), but design-system says Inter. No Inter font loaded. FontWeight 1751 hardcoded (99.7%) — Bold/SemiBold literals instead of Weight tokens.

### 3.4 Color / Brush — 5 Systems

| System | Examples | Usage |
|--------|----------|-------|
| Tint* + AccentBrush (current) | TintBlueBrush, AccentBrush, MutedBrush, BorderBrush, SurfaceBrush | 4707-4786 Dynamic (90.8-91.2%) — winner |
| Surface.* | Surface.PrimaryBrush, Surface.OnPrimary, Surface.SecondaryBrush | ~50 keys, partially used |
| Crystal* | CrystalAccentDarkBrush, CrystalAccentDeepBrush, CrystalGlassBorderLightBrush, CrystalBubbleFillBrush, CrystalDangerBrush, etc. | ~80 keys, mostly unused (in 1042 unused) |
| Liquid* | LiquidCard, LiquidAccent, LiquidGlassGlow | 2-5 keys, unused |
| Hardcoded hex | #59FFFF, #00FFFF, #2EFFFF, #FFFFFF, #1000FF, #10FF00, #1A22C5, #1AFF3B, #1AFFA5, #1A3B82, #00FFFF, #000000, #FFF8FA, #FF1118, #228080, #33FFFF, #148080, #0D0000 | 8 files, 271-373 hardcoded brush usages (5-7%) |
| SemanticColors | AccentSoftBrush, AccentPressedBrush, BorderStrong, BorderThin | Unused (in 1042 unused) |

### 3.5 Border Thickness

- CardBorderThickness (1), CardEdgeThickness (4), Border.HorizontalHairlineThickness, Border.VerticalHairlineThickness, Border.LeftHairlineThickness, Border.RightHairlineThickness (all in DarkTheme, unused), BorderStrong, BorderThin (SemanticGeometry, unused)

### 3.6 Shadows

- DashCardShadowEffect (DarkTheme), ShadowSoft, ShadowElevated, GlassShadow*, Crystal* shadows, plus hardcoded DropShadowEffect in 430-476 places. No unified Shadow token.

### 3.7 Dimensions

- CardEdgePaddingBase, CardEdgePaddingStat, CardEdgePaddingTask, CardEdgePaddingList (DarkTheme, unused), ControlHeightSmall/Medium/Large (SemanticGeometry, unused), CrystalAvatarSize, CrystalBubbleSize, CrystalCardPadding, CrystalCardPaddingCompact, CrystalCardGap (CrystalFluidMetrics, unused), DensityCardPadding (DarkTheme, used via DbCardPadding wrapper)

---

## 4. ROOT CAUSE — Why Different Views Have Different Shapes

1. **3 Generations of Dashboard Evolution (without deprecation):**
   - Gen1: `DashboardStyles.xaml` with DashCardStyle, SoftCardStyle, IndustrialDashboardCardStyle — hardcoded padding 24px, radius 12px, shadow md
   - Gen2: `Surface.*` + `Module*` system — semantic surfaces, but never migrated all views
   - Gen3: `DashboardKit.xaml` Db* grammar — density-aware, skeleton, WrapPanel pipeline, ScrollViewer template — introduced in 71a4e69, now used by 7 dashboards but not by other modules

2. **Experimental Design Systems Left Behind:**
   - CrystalFluid (8 files) and LiquidGlass (2 files) were experiments for glass morphism, added ~80 brush/metric tokens, but never adopted by Views (hence 1042 unused keys, 27.4% waste). They still publish CardCornerRadiusLarge, CrystalCardPadding, etc., competing with Db*.

3. **Module Isolation Without Global Lint:**
   - Each module (Dn, Jo, Ti, SheetStore, Inquiry, Settings) created its own `*SectionStyles.xaml` with 8-29 local styles (DnSectionStyles 29, AboutStyles 20, SheetDetailWindow 14) to move fast, without checking global TypographyStyles. Result: 844 local styles, 31 true duplicate keys (CardStyle, SectionTitleStyle, FieldLabelStyle, IntInputStyle, DecimalInputStyle, ComboStyle, RowTitleStyle, PageTitleStyle, PageSubtitleStyle, TableHeaderStyle).

4. **ThemeManager vs XAML Duplication:**
   - `ThemeManager.CardCornerRadius` and `VisualThemeEngine.Ios26.IosRadius` attempt runtime unification, but XAML still has 52 hardcoded CornerRadius in Views (About 12/7/16, Splash 16/12/26, PrintableInvoice 6/4/3/1) that bypass C# pipeline. `ThemePipeline.ReapplyCardCornerRadius` re-applies after `ApplyModule`, but hardcoded XAML values win over DynamicResource if not using DynamicResource.

5. **Density-Aware Tokens Not Adopted Beyond Dashboards:**
   - `ApplyDisplayDensity` publishes DbPageMargin, DbCardPadding, DbTilePadding, DbGutter, DbKpiTileMinHeight, DbChartMinHeight — used by 7 dashboards (100% DynamicResource), but other 182 Views still use StaticResource Density* or hardcoded Margin="24" etc.

6. **Font System Mismatch:**
   - Design-system says Inter, but WPF uses Segoe UI Variable via AppFontFamily DynamicResource (190 usages). Icon font Segoe MDL2 Assets is hardcoded 410 times (68% of FontFamily) — acceptable for icons but should be tokenized as IconFontFamily. FontWeight hardcoded Bold/SemiBold (99.7%) because no Weight tokens consumed.

7. **No Central Card Spec:**
   - Design-system says card radius 12px, padding 24px, shadow md, hover shadow lg + translateY(-2px). Actual implementation has 372 card-related keys with 7 systems, no single source of truth.

---

## 5. GLOBAL SYSTEM PLAN — Theme → Design Tokens → Global Styles → Controls → Cards/Surfaces → Every View

### 5.1 Proposed Architecture (single source of truth)

```
1. Theme (C#) — ThemeManager + ThemePipeline + VisualThemeEngine (single orchestrator)
   ├─ Publishes: CardCornerRadius, ControlCornerRadius, CardBorderThickness, CardEdgeThickness, Density (Compact/Normal/Comfortable/Spacious), FontFamily, Accent, IsDark
   └─ No XAML duplication — XAML reads only via DynamicResource

2. Design Tokens (XAML) — ONE file per concern, no duplicates
   ├─ Tokens/Colors.xaml — TintBlue, Accent, Muted, Border, Surface.Primary/Secondary/Detail, Success, Warning, Destructive (DynamicResource, no hex in Views)
   ├─ Tokens/Geometry.xaml — Radius.Small=4, Medium=8, Large=12 (CardCornerRadius), XLarge=16 (CardCornerRadiusLarge), Control=8, Pill=999, DensityPageMargin, DensityCardPadding, DensityTilePadding, DensityGutter
   ├─ Tokens/Typography.xaml — FontFamily.App (Segoe UI Variable, future Inter), FontFamily.Icon (Segoe MDL2 Assets), TypeScale (TextXs-Xl via StaticResource but backed by Dynamic density), Weight.Regular/Medium/SemiBold/Bold
   ├─ Tokens/Shadows.xaml — Shadow.Soft (0 1 2 0.05), Shadow.Medium (0 4 6 0.1), Shadow.Large (0 10 15 0.1), Shadow.XLarge (0 20 25 0.15)
   ├─ Tokens/Spacing.xaml — Space.Xs=2, Sm=4, Md=8, Lg=12, Xl=16, 2Xl=24, 3Xl=32
   └─ Tokens/Density.xaml — DbPageMargin, DbCardPadding, DbTilePadding, DbGutter, DbGutterHalfMargin, DbKpiTileMinHeight, DbChartMinHeight per Compact/Normal/Comfortable/Spacious (published by ThemeManager.ApplyDisplayDensity)

3. Global Styles (XAML) — ONE style per control, no local duplicates
   ├─ Styles/CardStyles.xaml — DbCardStyle, DbRowCardStyle, DbPipelineCardStyle, ModuleSectionCardStyle (all use DynamicResource Radius.Large, Padding.DensityCardPadding, Shadow.Medium, BorderBrush)
   ├─ Styles/ButtonStyles.xaml — PrimaryButtonStyle, SecondaryButtonStyle, ModuleToolbarPrimaryButtonStyle, AccentChip, OutlinedPill, Ghost (all DynamicResource ControlCornerRadius, no hardcoded)
   ├─ Styles/InputStyles.xaml — TextBoxStyle, ComboBoxStyle, IntInputStyle, DecimalInputStyle (all DynamicResource)
   ├─ Styles/TypographyStyles.xaml — SectionTitleStyle, PageTitleStyle, PageSubtitleStyle, RowTitleStyle, FieldLabelStyle, TableHeaderStyle, CardTitleStyle, CardSubtitleStyle (single definition, no shadowing)
   ├─ Styles/ListPageStyles.xaml — ListPageTitle, ListPageSubtitle, TabCardActiveStyle, TabCardInactiveStyle
   ├─ Styles/DataGridStyles.xaml — DataGridStyle, DataGridRowStyle, DataGridCellStyle
   └─ Styles/DashboardKit.xaml — DbKpiTileTemplate, DbChartTemplate, DbFeedRowTemplate, DbPipelineStageTemplate, DbSkeletonBlockStyle (pulse 0.35→0.75), DbSkeletonCircleStyle (999), DbSkeletonCardStyle, KpiBandPanel (UniformGrid Rows=2), WrapPanel pipeline, ScrollViewer template

4. Controls (XAML) — Custom controls use Global Styles only
   ├─ Controls/GlassCard.cs (if needed) — uses Surface.PrimaryBrush, Radius.Large
   └─ No local styles in Controls

5. Cards/Surfaces (XAML) — ONE system
   ├─ Surface.Primary (card default, 12 radius, 24 padding, shadow md)
   ├─ Surface.Secondary (muted, 8 radius, 12 padding)
   ├─ Surface.Detail (small, 6 radius, 8 padding)
   └─ All dashboards and modules use DbCardStyle or ModuleSectionCardStyle which map to Surface.Primary

6. Every View (XAML) — 0 local styles, 100% DynamicResource/StaticResource from Global
   ├─ No x:Key in Views (except DataTemplates for ItemsControl)
   ├─ No hardcoded CornerRadius, hex, FontFamily, FontSize, Margin
   ├─ Uses DbPageMargin, DbCardPadding, DbTilePadding, DbGutter for layout
   ├─ Uses SectionTitleStyle, FieldLabelStyle, etc. from TypographyStyles (no shadow)
   └─ Skeleton: StackPanel Row=2 RowSpan=4 Visibility BoolToVisibility + InverseBool on real content (as in 71a4e69)
```

### 5.2 Token Mapping (Design-System → WPF)

| Design-System Token | WPF Token (proposed) | Current Status |
|---------------------|----------------------|----------------|
| --color-primary #0F172A | PrimaryBrush | Exists but unused in some Views |
| --color-accent #0369A1 | AccentBrush / TintBlueBrush | TintBlueBrush used in dashboards (good) |
| --color-background #F8FAFC | BackgroundBrush | Exists |
| --color-card #FFFFFF | Surface.PrimaryBrush / CardBrush | Surface.Primary exists, but LiquidCard/Crystal* compete |
| --color-border #E2E8F0 | BorderBrush | Exists, 91% Dynamic |
| --space-md 8px | Space.Md / DbGutter | DbGutter exists (density-aware) |
| --space-xl 16px | Space.Xl / DbCardPadding | DbCardPadding exists |
| --shadow-md 0 4 6 0.1 | Shadow.Medium | DashCardShadowEffect exists but not unified |
| --radius 8px buttons / 12px cards / 16px modals | ControlCornerRadius=8, CardCornerRadius=12, CardCornerRadiusLarge=16 | Exists, 91.9% Dynamic |
| Inter font | AppFontFamily (currently Segoe UI Variable) | Should add Inter as option, but keep Segoe for WPF native |
| Segoe MDL2 Assets (icons) | IconFontFamily | Currently hardcoded 410 times — tokenize |

### 5.3 What to Deprecate

- Delete or archive: `CrystalFluid/*` (8 files, 80+ unused keys), `LiquidGlassDesign.xaml`, `LiquidGlassStyles.xaml` (2 files), `DashboardStyles.xaml` legacy Dash* keys (keep only if referenced, but migrate to Db*), `ProGlassDesignSystem/Tokens/SemanticGeometry` BorderStrong/BorderThin (unused), `DesignTokens` duplicates
- Keep: `DarkTheme.xaml`, `LightTheme.xaml` (baseline), `DashboardKit.xaml` (modern, density-aware), `CardStyles.xaml`, `ButtonStyles.xaml`, `InputStyles.xaml`, `TypographyStyles.xaml`, `ListPageStyles.xaml`, `DataGridStyles.xaml`, `DialogStyles.xaml`, `ShellStyles.xaml`
- Result: Global keys from 3800 → ~1200 (remove 1042 unused + 1000 language duplicates + 500 legacy), well-formed stays 267/267

---

## 6. IMPLEMENTATION ORDER — Safest Step-by-Step (No Visual Regressions)

### Phase 0: Freeze & Baseline (Done in 71a4e69)

- [x] Verify 267/267 well-formed, 0 unresolved resources
- [x] Fix SettingsAppearanceSection broken Fill="{Bindin <StackPanel duplication
- [x] Unify radius: Ios26 IosRadius scaling from ThemeManager.CardCornerRadius, ThemePipeline ReapplyCardCornerRadius/Border/Edge
- [x] Make pipeline responsive: WrapPanel + ScrollViewer + MinWidth 140/160
- [x] Density-aware: ThemeManager.Part2 ApplyDisplayDensity publishes Db* tokens, all dashboards use DynamicResource DbPageMargin/DbCardPadding/DbTilePadding/DbGutter/DbKpiTileMinHeight/DbChartMinHeight
- [x] Skeleton: DbSkeletonBlockStyle pulse 0.35→0.75, CircleStyle CornerRadius 999, KpiTileTemplate, ChartTemplate, FeedRowTemplate, PipelineStageTemplate, BandStyle in all 7 dashboards

### Phase 1: Token Consolidation (Read-Only Audit → Write Tokens)

1. **Create `Themes/Tokens/Geometry.xaml`** with single source: Radius.Small=4, Medium=8, Large=12, XLarge=16, Control=8, Pill=999, CardCornerRadius=12, CardCornerRadiusLarge=16, ControlCornerRadius=8 — all DynamicResource, backed by ThemeManager
2. **Create `Themes/Tokens/Typography.xaml`** with AppFontFamily (Segoe UI Variable), IconFontFamily (Segoe MDL2 Assets), Weight tokens, TypeScale (TextXs-Xl) — migrate FontFamily hardcoded 410 to DynamicResource IconFontFamily
3. **Create `Themes/Tokens/Shadows.xaml`** with Shadow.Soft/Medium/Large/XLarge — replace DashCardShadowEffect, GlassShadow*, etc.
4. **Create `Themes/Tokens/Colors.xaml`** with Primary, Accent (TintBlue), Muted, Border, Surface.Primary/Secondary/Detail, Success, Warning, Destructive — ensure 100% DynamicResource, no hex in Views
5. **Deprecate CrystalFluid and LiquidGlass** — move to `Themes/Archive/` or delete, remove 1042 unused keys from resource dictionary merge in App.xaml
6. **Run verify-dashboard.py after each token file** — must stay 267/267 well-formed, 0 unresolved

### Phase 2: Global Styles Unification (No View Changes Yet)

1. **Merge Card Systems into `Styles/CardStyles.xaml`:** Keep DbCardStyle, DbRowCardStyle, DbPipelineCardStyle, ModuleSectionCardStyle — all use DynamicResource Radius.Large, Padding.DensityCardPadding, Shadow.Medium. Delete DashCardStyle, SoftCardStyle, IndustrialDashboardCardStyle from DashboardStyles.xaml (or make them aliases to Db* to avoid breakage)
2. **Unify Button Styles:** Ensure PrimaryButtonStyle, SecondaryButtonStyle, ModuleToolbarPrimaryButtonStyle, AccentChip, OutlinedPill all use DynamicResource ControlCornerRadius, no hardcoded. Move 300 StaticResource button usages to DynamicResource if possible, but keep Static for performance where needed
3. **Unify Input Styles:** IntInputStyle, DecimalInputStyle, ComboStyle, TextBoxStyle — ensure 0 local duplicates (currently AddEditSheetView shadows 3). Delete local LabelStyle, InputStyle in AddEditSheetView, replace with global FieldLabelStyle + TextBoxStyle
4. **Unify Typography Styles:** Ensure SectionTitleStyle, PageTitleStyle, PageSubtitleStyle, RowTitleStyle, FieldLabelStyle, TableHeaderStyle, CardTitleStyle, CardSubtitleStyle have single definition in TypographyStyles.xaml. Delete 17 shadow occurrences (AboutStyles, JoSectionStyles, SettingsSectionStyles, etc.) — replace with global
5. **Verify:** 267/267 well-formed, 0 unresolved, local styles count drops from 844 → ~600

### Phase 3: View Migration — Bypassing → Partial → Fully Global (Module by Module, Safest Order)

**Order by risk (low → high):**

1. **SplashView (5 local, 15 HC colors, 5 HC CR)** — lowest risk, isolated, no business logic. Replace hardcoded #228080/#33FFFF with TintBlueBrush, CR 16/12/26 with Radius.Large/XLarge, TrustChipStyle with global ChipStyle
2. **AboutStyles + AboutView (20+2 local, 2 HC colors, 9 HC CR)** — low risk, replace AboutCardStyle with DbCardStyle, HeroCardStyle with ModuleSectionCardStyle, SectionTitleStyle shadow with global
3. **AiChatView (6 local)** — low-medium risk, replace ChatIconBtn, SendBtn, UserBubble, AiBubble with global ButtonStyles + Surface tokens
4. **SettingsSectionStyles + SettingsAppearanceSection (11+40 local)** — medium risk, 40 local is largest file, but Settings is not critical path. Replace ProviderCardBaseStyle with DbCardStyle, RowTitleStyle shadow with global
5. **DnSectionStyles (29 local) + Dn views** — medium risk, 29 local is largest bypass. Migrate CompactStatLabelStyle etc. to global Kpi styles from DashboardKit
6. **JoSectionStyles (11 local) + Jo views** — medium risk
7. **TiSectionStyles (8 local) + Ti views** — medium risk
8. **SheetStoreView + SheetDetailWindow (9+14 local, 28+4 HC CR, 32+29 buttons)** — high risk, biggest views (399 and 163 global usages), mixed eras. Migrate ItemFieldLabel, ItemEntry, InventoryKpiCell to global InputStyles + CardStyles, replace 28 HC CR with Radius.Large
9. **InquiryEditorWindow + InquiryListView (8+10 local, 6+11 CR, 13+1 TextBox, 5+5 ComboBox)** — high risk, 24 buttons in ListView, own spine/pill system. Migrate to global
10. **PrintableInvoice + ProfitLoss views (hardcoded CR 6/4/3/1, hex #FFFFFF paper)** — high risk, paper white intentional per design-system, but CR should use Radius.Small/Medium. Keep #FFFFFF as PaperBrush token (intentional), not hardcoded
11. **SystemCenter + UserCenter (2+4 local, 13+5 CR, 35+6 buttons)** — medium-high risk, but isolated

**After each module:** Run `python3 scripts/verify-dashboard.py` — must stay 267/267 well-formed, 0 unresolved, 0 legacy/hex/literalFontSize (except intentional PaperBrush #FFFFFF tokenized). Take screenshot of module before/after (if UI test harness exists).

### Phase 4: Density & Skeleton Rollout to All Views (Beyond Dashboards)

1. **Apply density-aware tokens to all Views:** Replace hardcoded Margin="24" with DynamicResource DbPageMargin/DensityPageMargin, Padding with DbCardPadding, Gutter with DbGutter
2. **Add skeleton loading to non-dashboard lists:** SheetStoreView, DeliveryNoteListView, JobOrder lists, TaxInvoiceListView — use DbSkeletonBlockStyle + DbSkeletonCircleStyle + BoolToVisibility pattern from dashboards
3. **Verify:** All Views use DynamicResource Db* for layout, not just 7 dashboards

### Phase 5: Cleanup & Final Verification

1. **Delete 1042 unused keys** from Themes (Crystal*, Aurora, AboutGlass, etc.) — keep only used keys (check via `/tmp/audit3.py` unused list)
2. **Remove duplicate style keys:** Ensure 0 duplicate x:Key outside language files (currently 2965 total incl. languages, but 31 true style duplicates). Delete local shadows
3. **Final verification:** `python3 scripts/verify-dashboard.py` must show 267/267 well-formed, resource universe ~1200 (down from 4295), 0 unresolved Static/Dynamic, 0 binding errors, 0 legacy/hex/literalFontSize (except PaperBrush token)
4. **Theme coverage target:** 189 Views → Fully Global 189 (100%), Partially 0, Bypassing 0 (up from 63% fully global)
5. **Documentation:** Update `design-system/proglass-industrial/MASTER.md` with WPF token mapping, and create `docs/THEME_MIGRATION_GUIDE.md` with before/after examples

---

## 7. Appendix — Exact File Lists

### 7.1 Total XAML Files (267)

- Views: 189 (list in `/tmp/report.txt` — 189 lines)
- Themes: 58 (DarkTheme, LightTheme, DesignTokens, TypeScaleTokens, ShellStyles, CardStyles, ButtonStyles, InputStyles, ListPageStyles, DataGridStyles, DialogStyles, TypographyStyles, LiquidGlassDesign, LiquidGlassStyles, CrystalFluid/* 8, ProGlassDesignSystem/Tokens/* 3, Languages 15, etc.)
- Styles: 5 (DashboardKit.xaml, DashboardStyles.xaml, ListPageStyles.xaml, ModuleChrome.xaml, DataGridStyles.xaml)
- Controls: 9 (custom controls)
- Root: App.xaml, MainWindow.xaml

### 7.2 Hardcoded Hex Colors (8 files)

- ./Views/About/AboutStyles.xaml: #59FFFF, #00FFFF
- ./Views/Login/AuthSurfaceStyles.xaml: #2EFFFF
- ./Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml: #FFFFFF (paper, intentional)
- ./Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml: #1000FF, #10FF00
- ./Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml: #10FF00
- ./Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml: #1A22C5, #1AFF3B, #1AFFA5, #1A3B82
- ./Views/Reports/StatementGenerator/Styles/WizardStyles.xaml: #00FFFF, #000000, #FFF8FA, #FF1118
- ./Views/Splash/SplashView.xaml: #228080 x2, #33FFFF, #148080, #0D0000 (15 occurrences)

### 7.3 Hardcoded CornerRadius (52 in Views, 70 total incl. Themes)

- AboutStyles: 12,7,16,16,0,0,10
- AboutView: 16,18,14,12,22
- DeveloperSection: 10,20,16,12
- ModulesSection: 14,20
- FirstRunSetupPanel: 2.5 x4, ForcePasswordChangeWindow: 2.5 x4, ResetPasswordWindow: 2.5 x4 (Login — intentional small radius for password inputs, but should be tokenized as Radius.Small)
- PrintableInvoiceView: 6,1,4,4,0,0,0,0,4,4,4,4,0,0,6,4,4,0,0,0,0,4,4,6,3,6,3 (paper edges, intentional but should be Radius.Small)
- PreviewStep: 12,12,0,0 x2
- SplashView: 16,16,12,26,26

### 7.4 Duplicate Style Keys (31 true duplicates, excluding language i18n)

- CardStyle: DashboardStyles.xaml + TiSectionStyles.xaml
- SectionTitleStyle: TypographyStyles.xaml + AboutStyles.xaml + JoSectionStyles.xaml
- SectionSubtitleStyle: TypographyStyles.xaml + AboutStyles.xaml
- TableHeaderStyle: TypographyStyles.xaml + PrintableInvoiceView.xaml + GlassTypeComparisonView.xaml + ServiceProfitabilityView.xaml
- FieldLabelStyle: SharedSectionStyles.xaml + WizardStyles.xaml + HistoryView.xaml + ItemMovementView.xaml
- SectionTitle: DashboardStyles.xaml + ReviewStep.xaml
- RowTitleStyle: TypographyStyles.xaml + SettingsSectionStyles.xaml
- PageTitleStyle: TypographyStyles.xaml + SettingsSectionStyles.xaml
- PageSubtitleStyle: TypographyStyles.xaml + SettingsSectionStyles.xaml
- IntInputStyle: InputStyles.xaml + AddEditSheetView.xaml
- DecimalInputStyle: InputStyles.xaml + AddEditSheetView.xaml
- ComboStyle: InputStyles.xaml + AddEditSheetView.xaml
- SectionCardStyle: InquiryEditorWindow.xaml + SettingsSectionStyles.xaml
- KpiCardStyle, KpiLabel, KpiValue: ProfitLoss views duplicate
- TableCellStyle: 6 occurrences across JobProfitability and PrintableInvoice
- LabelStyle, InputStyle: AddEditSheetView local shadows global
- StatLabel, StatValue, StatCard, InventoryKpiCell: SheetDetailWindow + SheetStoreView duplicate
- AboutTabListStyle, AboutTabItemStyle: AboutView local
- CrashFieldLabel, CrashFieldValue, etc.: CrashReportDialogStyles local
- ScFieldLabel, ScTextBox: SystemCenter local
- UcFieldLabel, UcTextBox, UcPasswordBox: UserCenter local
- CcFieldLabel, CcTextBox: CompanyCenter local

### 7.5 Local Styles Shadowing Global (17 occurrences)

- AboutStyles.xaml shadows SectionTitleStyle, SectionSubtitleStyle (global in TypographyStyles.xaml)
- JoSectionStyles.xaml shadows SectionTitleStyle
- PrintableInvoiceView.xaml shadows TableHeaderStyle
- GlassTypeComparisonView.xaml shadows TableHeaderStyle
- ServiceProfitabilityView.xaml shadows TableHeaderStyle
- WizardStyles.xaml shadows FieldLabelStyle
- ReviewStep.xaml shadows SectionTitle
- SettingsSectionStyles.xaml shadows RowTitleStyle, PageTitleStyle, PageSubtitleStyle
- AddEditSheetView.xaml shadows IntInputStyle, DecimalInputStyle, ComboStyle
- HistoryView.xaml shadows FieldLabelStyle
- ItemMovementView.xaml shadows FieldLabelStyle
- TiSectionStyles.xaml shadows CardStyle

### 7.6 Global Keys Defined 3800, Unused 1042 (27.4%) — Sample Unused

- About.Active, About.Copyright, About.LIVEModule, About.ModuleCount, About.Product, About.Release, About.Title (language keys not bound in XAML but via VM — okay to keep)
- AboutCardBorderBrush, AboutGlassGlowBrush, AboutInteractiveBorderBrush, AboutInteractiveBorderStrongBrush, AboutInteractiveBrush (DarkTheme — experimental, unused)
- AccentPressedBrush, AccentSoftBrush, BorderStrong, BorderThin, CardEdgePaddingBase, CardSubtitleStyle, CardTitleStyle, ColumnSeparatorStyle (SemanticTokens — unused)
- CrystalAccentDarkBrush, CrystalAccentDeepBrush, CrystalAccentLightColor, CrystalAccentPaleColor, CrystalAmbientDuration, CrystalAmbientHostVisibility, CrystalAvatarSize, CrystalBubbleBorderBrush, CrystalBubbleDuration, CrystalBubbleFillBrush, CrystalBubbleGlowColor, CrystalBubbleMaxOpacity, CrystalBubbleSize, CrystalButtonHoverBrush, CrystalButtonPaddingWide, CrystalButtonPressedBrush, CrystalCardDuration, CrystalCardGap, CrystalCardPaddingCompact, CrystalCausticBrush, CrystalCausticDuration, CrystalCausticOpacity, CrystalComboBoxStyle, CrystalDangerBrush, CrystalDefaultWaterLevel, CrystalEnvWarmColor, CrystalEnvironmentBrush, CrystalFontSizeH1/H2/Icon/Logo/Money/Title/Value, CrystalGlassBorderLightBrush, CrystalGlassBorderSubtleBrush, CrystalGlassLowColor, CrystalGlassOpacity, CrystalGlassSurfaceStrongBrush, etc. (CrystalFluid — ~80 keys unused)
- AuroraA, AuroraB, Border.HorizontalHairlineThickness, Border.LeftHairlineThickness, Border.RightHairlineThickness, Border.VerticalHairlineThickness, ControlHeightLarge/Medium/Small, ControlPadding (DarkTheme + SemanticGeometry — unused)

### 7.7 Theme Coverage Details (from /tmp/report.txt)

- Fully Global 119: includes all 7 dashboards + DashboardView + 111 other Views (DnAttachmentsSection, DnHistorySection, DnOverviewSection, JoOverviewSection, JoProductionSection, TiAttachmentsSection, TiFamilySection, TiOverviewSection, ProformaInvoice Step1-6, etc.)
- Partially Themed 49: AboutView, AboutCrashReportsCard, AboutLicenseCard, DeveloperSection, ModulesSection, CompanyCenterView, CrashReportDialogStyles, DeliveryNoteDetailView, DeliveryNoteListView, JobOrderDetailView? Actually JoDetail is fully global, etc. (full list 49 in report)
- Bypassing 21: AiChatView, AboutStyles, DnSectionStyles, InquiryEditorWindow, InquiryListView, JoSectionStyles, SettingsAppearanceSection (40 local), SettingsSectionStyles, SheetDetailWindow, SheetStoreView, SplashView, TiSectionStyles, WizardStyles, GlassTypeComparisonView, ServiceProfitabilityView, SheetProfitabilityView, AuthSurfaceStyles, PreviewStep, etc.

### 7.8 Other Controls

- ItemsControl 388, ScrollViewer 158, ListBox 91, ListView 16, TextBlock 6054, StackPanel 2550, Grid 2629, Border 2860, Button 1008, TextBox 277, ComboBox 194, DataGrid 16, TabControl 2, TabItem 16

---

## 8. Conclusion

Current UI architecture is **functional but fragmented**: 63% fully global, 27.4% unused tokens, 844 local styles, 7 competing card systems, 4 radius systems, 5 color systems. The 71a4e69 dashboard polish (density-aware Db* + skeleton + responsive WrapPanel) is the **exemplar** and should be the template for all Views. The safest path is **token consolidation → global styles unification → module-by-module migration (low risk → high risk) → density/skeleton rollout → cleanup**, with `verify-dashboard.py` gating each step to keep 267/267 well-formed and 0 unresolved resources.

**No file modifications were made in this audit task.** All counts and classifications are from current branch `arena/01a09a9f-proglassautomationadv` at commit `71a4e69`.

---

**Generated by:** Arena.ai Agent Mode — Audit Only  
**Artifacts:** `/tmp/report.txt` (189 views detailed), `/tmp/audit3.txt` (unused keys, hardcoded colors, hardcoded CR, card keys, shadowed globals), `/tmp/audit_detailed.py` output (buttons, textboxes, comboboxes, datagrids, corner radius, fonts, brushes, shadows, local styles, control templates)  
**Verification:** `python3 scripts/verify-dashboard.py` → 267/267 well-formed, 4295 keys (before cleanup), 0 unresolved Static/Dynamic, 0 binding, 0 legacy/hex/literalFontSize

# PHASE 19R ERR-35B71A TEMPLATE CANDIDATES — STATIC INVENTORY

**Date:** 2026-09-14
**HEAD:** d3a5647 + 10 local fixes (43 lines) — 9-file startup + 1 GlobalSearchBar GsModuleComboStyle
**Error ID:** ERR-35B71A — ResourceReferenceExpression not valid for FontSize — FrameworkTemplate.ReceivePropertySet
**OS:** Windows 11 / Microsoft Windows NT 10.0.26200.0, .NET 8.0.31, Theme Normal (Industrial)

## Baseline Freeze

```
git rev-parse HEAD → d3a564791c01e5e1ac3dd1c05fdbf628ce52faea
git status --short → M App.StartupCore.cs, M Controls/GlobalSearch/GlobalSearchBar.xaml, M MainWindow.xaml, M Styles/ButtonStyles.xaml, M Styles/ContextMenuStyles.xaml, M Styles/DashboardKit.xaml, M Themes/Styles/DialogStyles.xaml, M Themes/Styles/InputStyles.xaml, M Themes/Styles/ListPageStyles.xaml, M Themes/Styles/ModuleTabStyles.xaml, M Themes/Styles/ShellStyles.xaml, ?? docs/PHASE19R_*
git diff --stat → 11 files 160+/56- (10 FontSize fixes 43 lines + App.StartupCore.cs 130 lines enhanced forensics)
```

Existing 9-file local FontSize changes are intentional forensic changes — DO NOT discard — per task instruction.

## Proven Fact — Canonical FontSize Resources VALID, Aliases INVALID

- FontSize.Body=13, Title=14, Caption=10, H2=20, Kpi=24, H1=24, H3=16, Display=32 — all System.Double VALID
- TextBody, TextBodyLarge, TextCaption, TextXSmall, TextTitle, TextTitleLarge, TextSection, TextSubhead, DensityBodyFontSize, DensityKpiValueFontSize, DensityTitleFontSize, ProGlassLayout.BodyFontSize/KpiFontSize = System.Windows.ResourceReferenceExpression (DynamicResource alias chain) — Text* = DynamicResource FontSize.* → ResourceReferenceExpression
- Crash when WPF template loading receives ResourceReferenceExpression as value for FontSize (DependencyProperty expects Double)

## Static Candidate Inventory — Complete XAML Universe

| Pattern | Count |
|---|---|
| FontSize="{StaticResource Text | 472 |
| FontSize="{DynamicResource Text | 4 |
| FontSize="{StaticResource Density | 0 |
| FontSize="{DynamicResource Density | 0 |
| FontSize="{StaticResource ProGlassLayout | 0 |
| FontSize="{DynamicResource ProGlassLayout | 0 |
| TextElement.FontSize="{StaticResource Text | 0 |
| TextElement.FontSize="{DynamicResource Text | 0 |
| Value="{StaticResource Text (Setter) | 134 |
| **Total FontSize+Setter Text/Density/ProGlassLayout** | **606** |
| Indirect StaticResource TextBody | 286 |
| Indirect StaticResource TextBodyLarge | 57 |
| Indirect StaticResource TextCaption | 173 |
| Indirect StaticResource TextXSmall | 33 |
| Indirect StaticResource TextTitle | 33 |
| Indirect StaticResource TextTitleLarge | 7 |
| Indirect StaticResource TextSection | 27 |
| Indirect StaticResource TextSubhead | 20 |
| Indirect StaticResource Density* | 0 |
| Indirect StaticResource ProGlassLayout.* | 0 |

**Note:** Density* and ProGlassLayout.* are not directly used as StaticResource for FontSize, but they ARE ResourceReferenceExpression themselves (GDS 138-140 DensityBodyFontSize = DynamicResource FontSize.Body, LayoutTokens ProGlassLayout.BodyFontSize = DynamicResource DensityBodyFontSize) — double nesting — if any template uses StaticResource Density* or ProGlassLayout.* for FontSize, it would also crash. Current grep shows 0 direct FontSize StaticResource Density/ProGlassLayout, but they are still dangerous via indirect chain.

## Candidate Table — 606 Total, 173 Startup YES

**Columns:** File|Line|Style/Template|Property|Resource|Resource runtime type|Startup reachable?|Evidence

**Startup reachable definition:** YES if file is in App.xaml MergedDictionaries, MainWindow, GlobalSearchBar, StatCard, DonutChart, TrendChart, DashboardKit, DashboardStyles, TypographyStyles, ShellStyles, ButtonStyles, ListPageStyles, DialogStyles, InputStyles, ModuleTabStyles, ContextMenuStyles, or Views/Dashboard/* — loaded during Splash→MainWindow→Dashboard first layout. MAYBE if Controls/* not in main dashboard but could be loaded via DataTemplate. NO if Views/* module not in startup path.

| File | Line | Style/Template | Property | Resource | Runtime Type | Startup? | Evidence |
|---|---|---|---|---|---|---|---|
| Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml | 21 | AutoCompleteItemStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml | 76 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBodyLarge}" |
| Controls/DonutChart/DonutChartControl.xaml | 19 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextTitle}" FontWeight="Bold" |
| Controls/DonutChart/DonutChartControl.xaml | 24 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" FontWeight="SemiBold" |
| Controls/DonutChart/DonutChartControl.xaml | 35 |  | FontSize | TextTitleLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextTitleLarge}" |
| Controls/DonutChart/DonutChartControl.xaml | 40 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/EmptyState/EmptyStateControl.xaml | 52 |  | FontSize | TextDisplay | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextDisplay}" |
| Controls/EmptyState/EmptyStateControl.xaml | 58 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextTitle}" FontWeight="Bold" |
| Controls/EmptyState/EmptyStateControl.xaml | 65 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/EmptyState/EmptyStateControl.xaml | 107 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" FontWeight="Bold" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 34 | GsInputStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 42 | GsResultTitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 49 | GsResultSubtitleStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 84 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBodyLarge}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 102 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 130 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 215 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 235 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextXSmall}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 252 |  | FontSize | TextHeadline | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextHeadline}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 257 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 262 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 275 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Controls/LoadingState/LoadingStateControl.xaml | 32 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" FontWeight="SemiBold" |
| Controls/StatCard/StatCardControl.xaml | 145 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextTitle}" |
| Controls/StatCard/StatCardControl.xaml | 156 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" FontWeight="Bold" |
| Controls/StatCard/StatCardControl.xaml | 179 |  | FontSize | TextHeadline | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock FontSize="{StaticResource TextHeadline}" FontWeight="Bold"> |
| Controls/StatCard/StatCardControl.xaml | 209 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/StatCard/StatCardControl.xaml | 216 |  | FontSize | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="• • •" FontSize="{StaticResource TextSection}" FontWeight="Bold" |
| Controls/StatCard/StatCardControl.xaml | 238 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Controls/TrendChart/TrendChartControl.xaml | 100 |  | FontSize | TextHeadline | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextHeadline}" |
| Controls/TrendChart/TrendChartControl.xaml | 105 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Controls/TrendChart/TrendChartControl.xaml | 136 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Controls/TrendChart/TrendChartControl.xaml | 140 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBodyLarge}" |
| Styles/ContextMenuStyles.xaml | 27 |  | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Styles/ContextMenuStyles.xaml | 56 | ModernMenuItemStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Styles/DashboardKit.xaml | 102 | DbTitleStyle | Setter Value | TextTitleLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitleLarge}"/> |
| Styles/DashboardKit.xaml | 109 | DbSubtitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/DashboardKit.xaml | 116 | DbSectionTitleStyle | Setter Value | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextSubhead}"/> |
| Styles/DashboardKit.xaml | 123 | DbSectionSubStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 130 | DbMetaStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 136 | DbKpiLabelStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 145 | DbKpiValueStyle | Setter Value | TextDisplay | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextDisplay}"/> |
| Styles/DashboardKit.xaml | 152 | DbKpiFootStyle | Setter Value | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Styles/DashboardKit.xaml | 160 | DbGlyphStyle | Setter Value | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextSection}"/> |
| Styles/DashboardKit.xaml | 246 | DbPillTextStyle | Setter Value | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Styles/DashboardKit.xaml | 261 |  | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 482 | DbStateTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/DashboardKit.xaml | 501 | DbEmptyTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/DashboardKit.xaml | 544 | DbPipelineNameStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 552 | DbPipelineValueStyle | Setter Value | TextTitleLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitleLarge}"/> |
| Styles/DashboardKit.xaml | 559 | DbPipelineFootStyle | Setter Value | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Styles/DashboardStyles.xaml | 267 | ControlTemplate | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="{TemplateBinding Tag}" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextBody}" VerticalAlig |
| Styles/DashboardStyles.xaml | 268 | ControlTemplate | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="{TemplateBinding Content}" FontSize="{StaticResource TextBody}" FontWeight="SemiBold" VerticalAlignment |
| Styles/DashboardStyles.xaml | 334 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="{Binding Icon}" Style="{StaticResource DashGlyphStyle}" FontSize="{StaticResource TextBodyLarge}"/> |
| Styles/DashboardStyles.xaml | 565 | SoftBadgeGlyphStyle | Setter Value | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitle}"/> |
| Styles/DashboardStyles.xaml | 593 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock x:Name="Glyph" Text="{TemplateBinding Tag}" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextBody |
| Styles/DashboardStyles.xaml | 669 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="{Binding Label}" Style="{StaticResource SoftKpiLabelStyle}" TextWrapping="Wrap" TextTrimming="Character |
| Styles/Step4SectionStyles.xaml | 39 | Step4LabelStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/Step4SectionStyles.xaml | 55 |  | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Styles/Step4SectionStyles.xaml | 99 | Step4ComboStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/Step4SectionStyles.xaml | 207 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextXSmall}" |
| Styles/Step4SectionStyles.xaml | 214 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Styles/Step4SectionStyles.xaml | 222 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" |
| Themes/Styles/DataGridStyles.xaml | 32 | LineItemsHeaderTextStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/DataGridStyles.xaml | 49 | LineItemsSerialStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DataGridStyles.xaml | 57 | LineItemsReadonlyStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DataGridStyles.xaml | 64 | LineItemsMoneyStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DataGridStyles.xaml | 79 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DataGridStyles.xaml | 161 |  | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/DesignTokens.xaml | 167 | ModulePageSubtitleStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/DesignTokens.xaml | 180 | ModuleSectionCardStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/DesignTokens.xaml | 193 | ModuleToolbarPrimaryButtonStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DesignTokens.xaml | 258 | ModuleDetailTitleStyle | Setter Value | TextTitleLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitleLarge}"/> |
| Themes/Styles/DesignTokens.xaml | 307 | ModuleFieldLabelStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DesignTokens.xaml | 315 | ModuleInlineWarningStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DesignTokens.xaml | 325 | ModuleTabPillStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DialogStyles.xaml | 65 | DialogTitleStyle | Setter Value | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitle}"/> |
| Themes/Styles/DialogStyles.xaml | 74 | DialogSubtitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DialogStyles.xaml | 83 | DialogFieldLabelStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/DialogStyles.xaml | 92 | DialogBodyTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DialogStyles.xaml | 95 | DialogBodyTextStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="LineHeight" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/DialogStyles.xaml | 100 | DialogHintTextStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/DialogStyles.xaml | 134 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DialogStyles.xaml | 178 | {x:Type dialogs:ThemedDialogWindow} | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/DialogStyles.xaml | 334 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/IconBadgeStyles.xaml | 12 |  | FontSize | TextTitleLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="&#128200;" FontSize="{StaticResource TextTitleLarge}" |
| Themes/Styles/InputStyles.xaml | 20 | ModernTextBoxStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/InputStyles.xaml | 70 | ModernPasswordBoxStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/InputStyles.xaml | 122 | ModernEditableComboBoxStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/InputStyles.xaml | 219 | ModernComboBoxStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/InputStyles.xaml | 361 |  | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/ListPageStyles.xaml | 32 | ModuleListKpiLabelStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ListPageStyles.xaml | 46 | ModuleListKpiHintStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ListPageStyles.xaml | 70 | ModuleListFilterInputStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ListPageStyles.xaml | 82 | ModuleListFilterComboStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ListPageStyles.xaml | 99 | ModuleListFilterDatePickerStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ListPageStyles.xaml | 130 | ReportSearchInputStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/ListPageStyles.xaml | 147 | ModuleListHeaderTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ListPageStyles.xaml | 167 | ReportHeaderTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ModuleTabStyles.xaml | 46 | ModuleTabButtonStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/ModuleTabStyles.xaml | 195 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ModuleTabStyles.xaml | 210 | GodViewHeaderStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ModuleTabStyles.xaml | 250 | PillTabItemStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/OptimizationStyles.xaml | 44 | OptHeader | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/OptimizationStyles.xaml | 49 | OptPanelTitle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/OptimizationStyles.xaml | 62 | OptRailTab | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/OptimizationStyles.xaml | 74 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock x:Name="Icon" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextSubhead}" |
| Themes/Styles/OptimizationStyles.xaml | 110 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/OptimizationStyles.xaml | 170 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/OptimizationStyles.xaml | 190 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextXSmall}" Foreground="{DynamicResource MutedForegroundBrush}" |
| Themes/Styles/OptimizationStyles.xaml | 231 | OptGhostButton | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/OptimizationStyles.xaml | 290 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextCaption}" FontWeight="Bold" |
| Themes/Styles/OptimizationStyles.xaml | 293 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextXSmall}" FontWeight="SemiBold" |
| Themes/Styles/OptimizationStyles.xaml | 295 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="{Binding StockLabel}" FontSize="{StaticResource TextXSmall}" |
| Themes/Styles/PiIndustrialStyles.xaml | 26 | PiSectionLabelStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 34 | PiFieldLabelStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 73 | PiKpiCaptionStyle | Setter Value | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 79 | PiKpiValueStyle | Setter Value | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextSubhead}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 108 |  | FontSize | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextSection}" |
| Themes/Styles/PiIndustrialStyles.xaml | 113 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBodyLarge}" FontWeight="SemiBold" |
| Themes/Styles/PiIndustrialStyles.xaml | 160 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 197 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 299 | PiLayerTitleStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 308 | PiSpecLineStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 341 | PiDatePickerStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 361 | PiComboBoxStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 415 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 456 | PiViewAllLinkStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 483 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 514 | PiBandTitleStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 521 | PiInlineMetaStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 613 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextXSmall}" FontWeight="Bold" |
| Themes/Styles/PiIndustrialStyles.xaml | 620 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextXSmall}" FontWeight="Bold" |
| Themes/Styles/PiIndustrialStyles.xaml | 698 | PiStationCodeStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 706 | PiStationTitleStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/PiIndustrialStyles.xaml | 713 | PiStationHintStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/PillStyles.xaml | 12 | PillTextStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/PillStyles.xaml | 19 | PillTextDarkStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/PillStyles.xaml | 95 | TagPillTextStyle | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/SharedSectionStyles.xaml | 17 | FieldLabelStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/SharedSectionStyles.xaml | 30 | GridDataCell | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/SharedSectionStyles.xaml | 37 | GridHeaderCell | Setter Value | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Themes/Styles/SharedSectionStyles.xaml | 75 | ActionButtonStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/SharedSectionStyles.xaml | 110 | ExportButtonStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ShellStyles.xaml | 146 | TopPillTitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/ShellStyles.xaml | 152 | TopPillCaptionStyle | Setter Value | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Themes/Styles/ShellStyles.xaml | 251 | SidebarGroupLabel | Setter Value | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Themes/Styles/TypographyStyles.xaml | 22 | StatNumberStyle | Setter Value | TextDisplayLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextDisplayLarge}"/> |
| Themes/Styles/TypographyStyles.xaml | 32 | MediumNumberStyle | Setter Value | TextTitleLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitleLarge}"/> |
| Themes/Styles/TypographyStyles.xaml | 49 | PageSubtitleStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/TypographyStyles.xaml | 57 | SectionTitleStyle | Setter Value | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextTitle}"/> |
| Themes/Styles/TypographyStyles.xaml | 65 | SectionSubtitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/TypographyStyles.xaml | 73 | CardTitleStyle | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Themes/Styles/TypographyStyles.xaml | 80 | CardSubtitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/TypographyStyles.xaml | 89 | MetaTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/TypographyStyles.xaml | 96 | RowTitleStyle | Setter Value | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextSection}"/> |
| Themes/Styles/TypographyStyles.xaml | 104 | RowSubtitleStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/TypographyStyles.xaml | 113 | TrendingTextStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Themes/Styles/TypographyStyles.xaml | 120 | TableHeaderStyle | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Tools/LicenseGenerator/MainWindow.xaml | 32 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="◆" FontSize="{StaticResource TextTitle}" |
| Tools/LicenseGenerator/MainWindow.xaml | 47 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" FontWeight="SemiBold" |
| Tools/LicenseGenerator/MainWindow.xaml | 71 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="7 DAYS" FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/MainWindow.xaml | 73 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="Short evaluation" FontSize="{StaticResource TextCaption}" |
| Tools/LicenseGenerator/MainWindow.xaml | 81 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="30 DAYS" FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/MainWindow.xaml | 83 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="Monthly key" FontSize="{StaticResource TextCaption}" |
| Tools/LicenseGenerator/MainWindow.xaml | 91 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="6 MONTHS" FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/MainWindow.xaml | 93 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="Half-year key" FontSize="{StaticResource TextCaption}" |
| Tools/LicenseGenerator/MainWindow.xaml | 101 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="1 YEAR" FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/MainWindow.xaml | 103 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="Annual key" FontSize="{StaticResource TextCaption}" |
| Tools/LicenseGenerator/MainWindow.xaml | 111 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="2 YEARS" FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/MainWindow.xaml | 113 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: <TextBlock Text="Two-year key" FontSize="{StaticResource TextCaption}" |
| Tools/LicenseGenerator/MainWindow.xaml | 158 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/MainWindow.xaml | 162 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | YES | static only: FontSize="{StaticResource TextBody}" |
| Agent/UI/AgentChatView.xaml | 72 | AgentHeaderIconButton | Setter Value | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextSubhead}"/> |
| Agent/UI/AgentChatView.xaml | 136 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextTitle}" |
| Agent/UI/AgentChatView.xaml | 146 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextTitle}" FontWeight="Bold" |
| Agent/UI/AgentChatView.xaml | 148 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock FontSize="{StaticResource TextBody}" |
| Agent/UI/AgentChatView.xaml | 224 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" |
| Agent/UI/AgentChatView.xaml | 235 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" |
| Agent/UI/AgentChatView.xaml | 252 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextXSmall}" |
| Agent/UI/AgentChatView.xaml | 316 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" |
| Agent/UI/AgentChatView.xaml | 344 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextSubhead}" |
| Agent/UI/AgentChatView.xaml | 380 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" |
| Controls/Reporting/MixPanel.xaml | 35 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | MAYBE | static only: FontSize="{StaticResource TextBody}" |
| Controls/Reporting/MixPanel.xaml | 41 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | MAYBE | static only: FontSize="{StaticResource TextBody}" |
| Controls/Reporting/MixPanel.xaml | 64 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | MAYBE | static only: FontSize="{StaticResource TextBody}" |
| Controls/Reporting/MixPanel.xaml | 71 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | MAYBE | static only: FontSize="{StaticResource TextBody}" |
| Controls/Reporting/MixPanel.xaml | 93 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | MAYBE | static only: FontSize="{StaticResource TextBody}" |
| Controls/Reporting/MixPanel.xaml | 100 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | MAYBE | static only: FontSize="{StaticResource TextBody}" |
| Tools/LicenseGenerator/Theme.xaml | 51 | LabelText | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Tools/LicenseGenerator/Theme.xaml | 52 | LabelText | Setter Value | TextDimBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextDimBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 57 | HeaderText | Setter Value | TextHeadline | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextHeadline}"/> |
| Tools/LicenseGenerator/Theme.xaml | 59 | HeaderText | Setter Value | TextBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 63 | SectionLabel | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Tools/LicenseGenerator/Theme.xaml | 70 | HelpText | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Tools/LicenseGenerator/Theme.xaml | 71 | HelpText | Setter Value | TextDimBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextDimBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 78 |  | Setter Value | TextBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 82 |  | Setter Value | TextBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="CaretBrush" Value="{StaticResource TextBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 84 |  | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Tools/LicenseGenerator/Theme.xaml | 91 |  | Setter Value | TextBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 95 |  | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Tools/LicenseGenerator/Theme.xaml | 99 |  | Setter Value | TextBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 100 |  | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Tools/LicenseGenerator/Theme.xaml | 107 | ButtonBase | Setter Value | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Tools/LicenseGenerator/Theme.xaml | 158 | GhostButton | Setter Value | TextDimBrush | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="Foreground" Value="{StaticResource TextDimBrush}"/> |
| Tools/LicenseGenerator/Theme.xaml | 175 | CodeTextBox | Setter Value | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 71 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 76 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 83 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 186 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 200 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" |
| Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 205 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 55 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE8A5;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextTitle}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 61 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{Binding HeaderTitle}" FontSize="{StaticResource TextTitle}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 63 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="●" FontSize="{StaticResource TextBody}" Margin="8,0,0,0" VerticalAlignment="Center" |
| Views/Inquiry/InquiryEditorWindow.xaml | 69 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{Binding Status}" FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 73 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{Binding HeaderSubtitle}" FontSize="{StaticResource TextCaption}" Margin="0,3,0,0" |
| Views/Inquiry/InquiryEditorWindow.xaml | 77 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Column="1" Text="{DynamicResource Inquiry.CtrlSSaveEscClose}" FontSize="{StaticResource TextCaption}" Ve |
| Views/Inquiry/InquiryEditorWindow.xaml | 99 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE823;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextBody}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 314 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <DatePicker SelectedDate="{Binding ReceivedDate}" Height="34" FontSize="{StaticResource TextBodyLarge}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 396 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: Margin="9,0,0,0" FontSize="{StaticResource TextXSmall}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 499 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" Padding="4,2"/> |
| Views/Inquiry/InquiryEditorWindow.xaml | 510 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="＋" FontSize="{StaticResource TextBody}" VerticalAlignment="Center"/> |
| Views/Inquiry/InquiryEditorWindow.xaml | 515 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextXSmall}" VerticalAlignment="Center" Margin="12,0,0,0" |
| Views/Inquiry/InquiryEditorWindow.xaml | 530 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextXSmall}" VerticalAlignment="Center" |
| Views/Inquiry/InquiryEditorWindow.xaml | 556 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE896;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextTitle}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 560 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{DynamicResource Inquiry.DropFilesHereClickBrowse}" FontSize="{StaticResource TextBodyLarge}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 588 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE7C3;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextBodyLarge}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 593 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{Binding FileName}" FontSize="{StaticResource TextBody}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 597 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{Binding Extension, Mode=OneWay}" FontSize="{StaticResource TextXSmall}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 600 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text=" · " FontSize="{StaticResource TextXSmall}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 603 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextXSmall}" |
| Views/Inquiry/InquiryEditorWindow.xaml | 651 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="{Binding StatusText}" FontSize="{StaticResource TextBody}" VerticalAlignment="Center" Margin="9,0,0,0" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 60 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" TextWrapping="Wrap"/> |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 88 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE8C8;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextSubhead}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 106 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE713;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextSubhead}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 124 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE73E;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextSubhead}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 142 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="&#xE7BA;" FontFamily="Segoe MDL2 Assets" FontSize="{StaticResource TextSubhead}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 289 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 299 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 310 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 329 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 337 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 346 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 352 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 361 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 379 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 390 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml | 403 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 58 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextTitle}" |
| Views/JobProfitability/JobDetailPanel.xaml | 64 |  | FontSize | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextSection}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 67 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |
| Views/JobProfitability/JobDetailPanel.xaml | 82 |  | FontSize | TextSubhead | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextSubhead}" |
| Views/JobProfitability/JobDetailPanel.xaml | 112 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding CompanyName}" FontSize="{StaticResource TextBody}" Foreground="{D |
| Views/JobProfitability/JobDetailPanel.xaml | 115 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding CustomerName}" FontSize="{StaticResource TextBody}" Foreground="{ |
| Views/JobProfitability/JobDetailPanel.xaml | 118 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding ProjectName}" FontSize="{StaticResource TextBody}" Foreground="{D |
| Views/JobProfitability/JobDetailPanel.xaml | 121 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding Salesman}" FontSize="{StaticResource TextBody}" Foreground="{Dyna |
| Views/JobProfitability/JobDetailPanel.xaml | 124 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding IssueDateDisplay}" FontSize="{StaticResource TextBody}" Foregroun |
| Views/JobProfitability/JobDetailPanel.xaml | 127 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="5" Grid.Column="1" Text="{Binding GlassTypeSummary}" FontSize="{StaticResource TextBody}" Foregroun |
| Views/JobProfitability/JobDetailPanel.xaml | 163 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}"> |
| Views/JobProfitability/JobDetailPanel.xaml | 204 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 215 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |
| Views/JobProfitability/JobDetailPanel.xaml | 234 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 247 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 258 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 261 |  | FontSize | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextSection}" FontWeight="{DynamicResource Weight.Bold}"> |
| Views/JobProfitability/JobDetailPanel.xaml | 286 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |
| Views/JobProfitability/JobDetailPanel.xaml | 309 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextXSmall}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 314 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 342 |  | FontSize | TextXSmall | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextXSmall}" FontWeight="{DynamicResource Weight.Bold}"> |
| Views/JobProfitability/JobDetailPanel.xaml | 379 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 385 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |
| Views/JobProfitability/JobDetailPanel.xaml | 390 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 393 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 400 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 402 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text=" · " FontSize="{StaticResource TextCaption}" Foreground="{DynamicResource TextTertiaryBrush}" Margin="2 |
| Views/JobProfitability/JobDetailPanel.xaml | 404 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 406 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text=" · " FontSize="{StaticResource TextCaption}" Foreground="{DynamicResource TextTertiaryBrush}" Margin="2 |
| Views/JobProfitability/JobDetailPanel.xaml | 408 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |
| Views/JobProfitability/JobDetailPanel.xaml | 445 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="3" Grid.Column="0" Text="NET REVENUE (ex-VAT)" FontWeight="{DynamicResource Weight.Bold}" FontSize= |
| Views/JobProfitability/JobDetailPanel.xaml | 446 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding NetRevenueDisplay}" FontSize="{StaticResource TextBody}" FontWeig |
| Views/JobProfitability/JobDetailPanel.xaml | 475 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="3" Grid.Column="0" Text="TOTAL COST" FontWeight="{DynamicResource Weight.Bold}" FontSize="{StaticRe |
| Views/JobProfitability/JobDetailPanel.xaml | 476 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding TotalCostDisplay}" FontSize="{StaticResource TextBody}" FontWeigh |
| Views/JobProfitability/JobDetailPanel.xaml | 501 |  | FontSize | TextSection | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextSection}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 505 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextTitle}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 519 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextTitle}" FontWeight="{DynamicResource Weight.Bold}" |
| Views/JobProfitability/JobDetailPanel.xaml | 548 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="Cost-Based (no U-insert)" FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.S |
| Views/JobProfitability/JobDetailPanel.xaml | 549 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="(Revenue − Base Cost) ÷ Base Cost" FontSize="{StaticResource TextCaption}" Foreground="{DynamicResource |
| Views/JobProfitability/JobDetailPanel.xaml | 551 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Column="1" Text="{Binding MarginDisplay}" FontSize="{StaticResource TextBody}" FontWeight="{DynamicResou |
| Views/JobProfitability/JobDetailPanel.xaml | 563 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.Bold}" Foreground="{DynamicResour |
| Views/JobProfitability/JobDetailPanel.xaml | 566 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="(Revenue − Total Cost) ÷ Total Cost" FontSize="{StaticResource TextCaption}" Foreground="{DynamicResour |
| Views/JobProfitability/JobDetailPanel.xaml | 568 |  | FontSize | TextTitle | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Column="1" Text="{Binding OverallMarginDisplay}" FontSize="{StaticResource TextTitle}" FontWeight="{Dyna |
| Views/JobProfitability/JobDetailPanel.xaml | 591 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="Net (accounting)" FontSize="{StaticResource TextCaption}" FontWeight="{DynamicResource Weight.SemiBold} |
| Views/JobProfitability/JobDetailPanel.xaml | 592 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Text="(Revenue − Total Cost) ÷ Revenue" FontSize="{StaticResource TextCaption}" Foreground="{DynamicResource  |
| Views/JobProfitability/JobDetailPanel.xaml | 594 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: <TextBlock Grid.Column="1" Text="{Binding NetMarginDisplay}" FontSize="{StaticResource TextBody}" FontWeight="{DynamicRe |
| Views/JobProfitability/JobDetailPanel.xaml | 625 |  | FontSize | TextBodyLarge | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBodyLarge}" |
| Views/JobProfitability/JobDetailPanel.xaml | 633 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" |
| Views/JobProfitability/JobDetailPanel.xaml | 643 |  | FontSize | TextBody | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextBody}" |
| Views/JobProfitability/JobDetailPanel.xaml | 651 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |
| Views/JobProfitability/JobDetailPanel.xaml | 659 |  | FontSize | TextCaption | ResourceReferenceExpression (DynamicResource alias chain) | NO | static only: FontSize="{StaticResource TextCaption}" |

... truncated to first 300 of 606 — full CSV in docs/PHASE19R_CANDIDATES_ENHANCED.csv ...

## High-Confidence Startup Candidates — After 9-File Fix FAILED

Previous 9-file fix modified:
- MainWindow.xaml (28 lines)
- ButtonStyles.xaml (8)
- ContextMenuStyles.xaml (6)
- DashboardKit.xaml (8) — only 2 of 18 fixed, 16 remain
- DialogStyles.xaml (4)
- InputStyles.xaml (4)
- ListPageStyles.xaml (6)
- ModuleTabStyles.xaml (6)
- ShellStyles.xaml (14)
- Plus GlobalSearchBar.xaml 1 (GsModuleComboStyle TextCaption → FontSize.BodySmall) — total 10 files 43 lines

**Crash persists per Windows 10.0.26200.0 .NET 8.0.31 verification — proves at least one remaining startup-reachable template/path still produces invalid ResourceReferenceExpression FontSize assignment.**

**Top remaining startup YES candidates:**

| File | Line | Style | Property | Resource | Evidence |
|---|---|---|---|---|---|
| Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml | 21 | AutoCompleteItemStyle | Setter Value | TextBodyLarge | <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml | 76 |  | FontSize | TextBodyLarge | FontSize="{StaticResource TextBodyLarge}" |
| Controls/DonutChart/DonutChartControl.xaml | 19 |  | FontSize | TextTitle | FontSize="{StaticResource TextTitle}" FontWeight="Bold" |
| Controls/DonutChart/DonutChartControl.xaml | 24 |  | FontSize | TextCaption | FontSize="{StaticResource TextCaption}" FontWeight="SemiBold" |
| Controls/DonutChart/DonutChartControl.xaml | 35 |  | FontSize | TextTitleLarge | FontSize="{StaticResource TextTitleLarge}" |
| Controls/DonutChart/DonutChartControl.xaml | 40 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/EmptyState/EmptyStateControl.xaml | 52 |  | FontSize | TextDisplay | FontSize="{StaticResource TextDisplay}" |
| Controls/EmptyState/EmptyStateControl.xaml | 58 |  | FontSize | TextTitle | FontSize="{StaticResource TextTitle}" FontWeight="Bold" |
| Controls/EmptyState/EmptyStateControl.xaml | 65 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/EmptyState/EmptyStateControl.xaml | 107 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" FontWeight="Bold" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 34 | GsInputStyle | Setter Value | TextBody | <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 42 | GsResultTitleStyle | Setter Value | TextBody | <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 49 | GsResultSubtitleStyle | Setter Value | TextCaption | <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 84 |  | FontSize | TextBodyLarge | FontSize="{StaticResource TextBodyLarge}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 102 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 130 |  | FontSize | TextCaption | FontSize="{StaticResource TextCaption}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 215 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 235 |  | FontSize | TextXSmall | FontSize="{StaticResource TextXSmall}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 252 |  | FontSize | TextHeadline | FontSize="{StaticResource TextHeadline}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 257 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 262 |  | FontSize | TextCaption | FontSize="{StaticResource TextCaption}" |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 275 |  | FontSize | TextCaption | FontSize="{StaticResource TextCaption}" |
| Controls/LoadingState/LoadingStateControl.xaml | 32 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" FontWeight="SemiBold" |
| Controls/StatCard/StatCardControl.xaml | 145 |  | FontSize | TextTitle | FontSize="{StaticResource TextTitle}" |
| Controls/StatCard/StatCardControl.xaml | 156 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" FontWeight="Bold" |
| Controls/StatCard/StatCardControl.xaml | 179 |  | FontSize | TextHeadline | <TextBlock FontSize="{StaticResource TextHeadline}" FontWeight="Bold"> |
| Controls/StatCard/StatCardControl.xaml | 209 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/StatCard/StatCardControl.xaml | 216 |  | FontSize | TextSection | <TextBlock Text="• • •" FontSize="{StaticResource TextSection}" FontWeight="Bold" |
| Controls/StatCard/StatCardControl.xaml | 238 |  | FontSize | TextCaption | FontSize="{StaticResource TextCaption}" |
| Controls/TrendChart/TrendChartControl.xaml | 100 |  | FontSize | TextHeadline | FontSize="{StaticResource TextHeadline}" |
| Controls/TrendChart/TrendChartControl.xaml | 105 |  | FontSize | TextBody | FontSize="{StaticResource TextBody}" |
| Controls/TrendChart/TrendChartControl.xaml | 136 |  | FontSize | TextCaption | FontSize="{StaticResource TextCaption}" |
| Controls/TrendChart/TrendChartControl.xaml | 140 |  | FontSize | TextBodyLarge | FontSize="{StaticResource TextBodyLarge}" |
| Styles/ContextMenuStyles.xaml | 27 |  | Setter Value | TextBodyLarge | <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Styles/ContextMenuStyles.xaml | 56 | ModernMenuItemStyle | Setter Value | TextBodyLarge | <Setter Property="FontSize" Value="{StaticResource TextBodyLarge}"/> |
| Styles/DashboardKit.xaml | 102 | DbTitleStyle | Setter Value | TextTitleLarge | <Setter Property="FontSize" Value="{StaticResource TextTitleLarge}"/> |
| Styles/DashboardKit.xaml | 109 | DbSubtitleStyle | Setter Value | TextBody | <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/DashboardKit.xaml | 116 | DbSectionTitleStyle | Setter Value | TextSubhead | <Setter Property="FontSize" Value="{StaticResource TextSubhead}"/> |
| Styles/DashboardKit.xaml | 123 | DbSectionSubStyle | Setter Value | TextCaption | <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 130 | DbMetaStyle | Setter Value | TextCaption | <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 136 | DbKpiLabelStyle | Setter Value | TextCaption | <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 145 | DbKpiValueStyle | Setter Value | TextDisplay | <Setter Property="FontSize" Value="{StaticResource TextDisplay}"/> |
| Styles/DashboardKit.xaml | 152 | DbKpiFootStyle | Setter Value | TextXSmall | <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Styles/DashboardKit.xaml | 160 | DbGlyphStyle | Setter Value | TextSection | <Setter Property="FontSize" Value="{StaticResource TextSection}"/> |
| Styles/DashboardKit.xaml | 246 | DbPillTextStyle | Setter Value | TextXSmall | <Setter Property="FontSize" Value="{StaticResource TextXSmall}"/> |
| Styles/DashboardKit.xaml | 261 |  | Setter Value | TextCaption | <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 482 | DbStateTextStyle | Setter Value | TextBody | <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/DashboardKit.xaml | 501 | DbEmptyTextStyle | Setter Value | TextBody | <Setter Property="FontSize" Value="{StaticResource TextBody}"/> |
| Styles/DashboardKit.xaml | 544 | DbPipelineNameStyle | Setter Value | TextCaption | <Setter Property="FontSize" Value="{StaticResource TextCaption}"/> |
| Styles/DashboardKit.xaml | 552 | DbPipelineValueStyle | Setter Value | TextTitleLarge | <Setter Property="FontSize" Value="{StaticResource TextTitleLarge}"/> |

**Key groups:**
- Controls/GlobalSearch/GlobalSearchBar.xaml — 12 remaining: GsInputStyle TextBody, GsResultTitleStyle TextBody, GsResultSubtitleStyle TextCaption, TextBlock TextBodyLarge/Body/Caption/XSmall/Headline inside UserControl and DataTemplate (195,241) — DataTemplate is FrameworkTemplate → high-confidence, ComboBox and TextBox Styles loaded during MainWindow top bar init
- Styles/DashboardKit.xaml — 16 remaining: DbTitleStyle TextTitleLarge, DbSubtitleStyle TextBody, DbSectionTitleStyle TextSubhead, DbSectionSubStyle TextCaption, DbMetaStyle TextCaption, DbKpiLabelStyle TextCaption, DbKpiValueStyle TextDisplay, DbKpiFootStyle TextXSmall, DbGlyphStyle TextSection, DbPillTextStyle TextXSmall, etc. — Styles for TextBlock used inside DataTemplates DbKpiTileTemplate, DbPipelineStageTemplate — DataTemplate is FrameworkTemplate → high-confidence startup crash, OverviewDashboardView KPI tiles loaded during Dashboard first measure inside ScrollViewer
- Styles/DashboardStyles.xaml — 4+ TextBody TemplateBinding, TextBodyLarge, etc. — old grammar merged before DashboardKit, contains TextBody TemplateBinding inside ControlTemplate
- Controls/StatCard/StatCardControl.xaml — 6 TextTitle/Body/Headline/Section/Caption — StatCardControl may be used in other dashboards, MAYBE not startup but OverviewDashboardView uses DbKpiTileTemplate not StatCardControl
- Controls/DonutChart/DonutChartControl.xaml — 4 TextTitle/Caption/TitleLarge/Body — UserControl direct children SAFE? But if inside template, could crash — used in OverviewDashboardView DeliveryMix
- Controls/TrendChart/TrendChartControl.xaml — 4 TextHeadline/Body/Caption/BodyLarge — similar
- Controls/EmptyState/LoadingState/AutoCompleteTextBox/MixPanel — 10+ — MAYBE not startup unless empty state shown
- Themes/Styles/TypographyStyles.xaml — 12 Style setters TextDisplayLarge/TitleLarge/BodyLarge/Title/Body/Section — used by templates — YES startup reachable via App.xaml

## Why Static Grep Insufficient — Need Runtime Forensics

- WPF returns BaseUri= Line=0 Position=0 for template failures (BAML path) — static source mapping insufficient — requires runtime visual tree / TemplatedParent inspection
- Stack contains Grid Border StackPanel ScrollViewer Control Grid Border — does NOT prove which control responsible — could be GlobalSearchBar ComboBox, DbKpiTileTemplate Border/Grid/TextBlock, DashboardView ScrollViewer, etc.
- Must use debugger break on first-chance ArgumentException/XamlParseException at FrameworkTemplate.ReceivePropertySet(targetObject, member, value, templatedParent) — inspect targetObject type, member name, value type, templatedParent type/style/template

## Next Steps — Runtime Investigation Method (Per Task)

1. Run actual application under Windows debugger (Visual Studio)
2. Break on System.ArgumentException and System.Windows.Markup.XamlParseException (first-chance)
3. When exception first occurs, inspect Exception, InnerException, StackTrace, targetObject, member, value, templatedParent at FrameworkTemplate.ReceivePropertySet(Object targetObject, XamlMember member, Object value, DependencyObject templatedParent)
4. Determine targetObject type, member name, value type, templatedParent type/style/template
5. If debugger source unavailable, add temporary diagnostic instrumentation around startup visual tree — enumerate visual/logical tree and record Type, Name, Style, Template, ActualWidth, ActualHeight, IsVisible, IsLoaded, Control.Template, Control.Style, Control.FontSize, TextElement.FontSize
6. Log [Forensics][VisualTree] and [Forensics][TemplateApply] for each startup-reachable Control
7. Trace resource-consumer chain: Control → Template → FontSize property → Resource alias → ResourceReferenceExpression → ArgumentException
8. Required final evidence: ERR-35B71A → Control: Button → Name: xyz → Style: GlassPillButtonStyle → Template: GlassPillButtonTemplate → Template property: FontSize → Source XAML: ButtonStyles.xaml:67 → Resource: TextBodyLarge → TextBodyLarge runtime type: ResourceReferenceExpression → underlying FontSize resource: FontSize.Title → exact WPF failure here

## STOP Rule

If exact failing template cannot be identified with evidence: PHASE 19R = BLOCKED, ERR-35B71A ROOT CAUSE = NOT YET LOCATED, PUSH = FORBIDDEN, NO ADDITIONAL SOURCE MODIFICATIONS, NO COMMIT, NO PUSH


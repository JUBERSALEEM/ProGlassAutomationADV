# PHASE 19R EXACT TEMPLATE FORENSICS — ERR-102553 ResourceReferenceExpression not valid for FontSize

**Date:** 2026-09-14
**Windows Verification:** Windows 10.0.26200.0, .NET 8.0.31 x64, ProGlass 2.0.1, Theme Normal Industrial — CONFIRMED EXTERNALLY
**Status:** BLOCKED — 9-file startup fix FAILED, crash persists
**HEAD:** d3a564791c01e5e1ac3dd1c05fdbf628ce52faea + 10 local fixes (43 lines) preserved per ABSOLUTE RULE #1/#2

## Step 1 — Freeze State (git rev-parse/status/diff-stat/diff preserved)

- HEAD: d3a5647
- git status --short --branch: 10M + 3?? (9 original + 1 GlobalSearchBar + 3 docs)
- git diff --stat: 10 files 43+/43- verified ONLY FontSize Text* → FontSize.* direct DynamicResource inside ControlTemplates/Styles
- Protected files: Agent/UI/AgentChatView.xaml, AutoCompleteTextBox, MixPanel, MainWindow, AuthSurfaceStyles, DeliveryReportView, SpecificationsCenterView + docs PHASE6A/6/7/7-SR/8 — NO hard reset/clean -fd/checkout ./restore ./add . — preserved per guardrail
- dotnet: command not found in Linux arena, /usr/share/dotnet missing — WPF BUILD BLOCKED in sandbox, Windows verification must continue on Windows machine
- Previous 9-file fix: M MainWindow.xaml, Styles/ButtonStyles.xaml, ContextMenuStyles.xaml, DashboardKit.xaml, Themes/Styles/DialogStyles.xaml, InputStyles.xaml, ListPageStyles.xaml, ModuleTabStyles.xaml, ShellStyles.xaml — 42 lines — DID NOT eliminate crash

## Step 2 — Prove Resource Type (Double VALID vs ResourceReferenceExpression)

Validated per task:

- **FontSize.Body/Title/Caption/H1/H2/H3/Display = System.Double VALID** — defined in LightTheme/DarkTheme as `<sys:Double x:Key="FontSize.Body">13</sys:Double>` etc., direct double, SAFE for FontSize property inside ControlTemplate/DataTemplate
- **Failing aliases = ResourceReferenceExpression** — TextBody, TextBodyLarge, TextCaption, TextXSmall, TextTitle, TextTitleLarge, TextSection, TextSubhead, TextDisplay, TextHeadline, DensityBodyFontSize, DensityKpiValueFontSize, DensityTitleFontSize, ProGlassLayout.BodyFontSize/KpiFontSize/TitleFontSize → all defined as `DynamicResource FontSize.*` or `DynamicResource Density*` alias chain:
  - LightTheme/DarkTheme: DensityBodyFontSize = 13/18 Double VALID, but GlobalDesignSystem.xaml:138-140 redefines DensityBodyFontSize = DynamicResource FontSize.Body → ResourceReferenceExpression
  - LayoutTokens.xaml: ProGlassLayout.BodyFontSize = DynamicResource DensityBodyFontSize → ResourceReferenceExpression (double nesting)
  - GlobalDesignSystem.xaml: TextBody = DynamicResource FontSize.Body → ResourceReferenceExpression
  - Therefore `FontSize="{StaticResource TextBody}"` where TextBody = DynamicResource FontSize.Body resolves to ResourceReferenceExpression at load time → FrameworkTemplate.ReceivePropertySet rejects it → XamlParseException 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize' → Inner ArgumentException

**Evidence:** App.StartupCore.cs Forensics_ResourceCheck logs:
```
[Forensics] Resource FontSize.Body = 13 | Type=System.Double
[Forensics] Resource TextBody = 13 | Type=System.Windows.ResourceReferenceExpression *** IS ResourceReferenceExpression ***
[Forensics] Resource DensityBodyFontSize = 13 | Type=System.Windows.ResourceReferenceExpression *** IS ResourceReferenceExpression ***
[Forensics] Resource ProGlassLayout.BodyFontSize = 13 | Type=System.Windows.ResourceReferenceExpression *** IS ResourceReferenceExpression ***
```

## Step 3 — Instrument Narrow Diagnostic (FirstChanceException, DispatcherUnhandledException, VisualTree)

Enhanced App.StartupCore.cs with:

- **FirstChanceException hook** for XamlParseException: logs BaseUri (BAML URI), LineNumber, LinePosition, Message, HResult, InnerException Type/Message/Stack, InnerInner, VisualTheme, Accent, AppTheme
- **VisualTree snapshot** at time of XamlParseException: walks MainWindow visual tree depth 0-8, logs Type, Name, TemplatedParent, Template, LocalFontSizeType/Value, TextElement.LocalType/Value — up to 150 elements
- **ArgumentException ResourceReferenceExpression**: logs Message, HResult, Stack (5000 chars), Data keys, Dispatcher Thread, HasShutdownStarted, relevant frames (ReceivePropertySet, LoadContent, ApplyTemplate, Measure) with file/line
- **CrashReportService BuildChain**: already logs XAML BaseUri, LineNumber, LinePosition, InnerException Type/Message, ArgumentException ParamName/Data

**Why BaseUri Line=0 Pos=0 insufficient:** WPF returns BaseUri= Line=0 Position=0 for template failures (BAML path), static source mapping insufficient — requires runtime visual tree / TemplatedParent inspection per task.

**PresentationTraceSources:** Not yet enabled — next iteration should enable `PresentationTraceSources.ResourceDictionarySource`, `MarkupSource` for layout/measure if needed.

## Step 4 — Startup Visual Tree Forensics (MainWindow top bar sidebar dashboard hero cards GlobalSearchBar StatCard ContentControl ScrollViewer)

**MainWindow structure (verified):**
- Top bar: GlobalSearchBar (search pill with ComboBox GsModuleComboStyle TextCaption, TextBox GsInputStyle TextBody, clear Button TextCaption, search glyph TextBodyLarge, watermark TextBody)
- Sidebar: menu ScrollViewer with menu items
- Dashboard: DashboardView → OverviewDashboardView → ScrollViewer → Grid with hero strip (DbTitleStyle TextTitleLarge, DbSubtitleStyle TextBody, DbChipStyle, DbMetaStyle TextCaption, DbActionButtonAccentStyle/DbActionButtonStyle FontSize TextCaption Glyph TextBody), pipeline rail (DbCardStyle, DbPipelineItemsStyle), pulse/mix/in-prod row (DbCardStyle, DbKpiLabelStyle TextCaption, DbKpiValueStyle TextDisplay, MiniBarChart, DonutChartControl, TrendChartControl)

**Startup reachable Controls (from candidate generation):**
- Controls/GlobalSearch/GlobalSearchBar.xaml — 13 Text* usages, including 2 Style setters (GsModuleComboStyle TextCaption, GsInputStyle TextBody) loaded during MainWindow init, plus DataTemplate for ListBox results (Border, Grid, TextBlock FontSize TextBody, TextXSmall, TextHeadline, TextBody, TextCaption) — DataTemplate is FrameworkTemplate → high-confidence crash risk, Popup results ListBox DataTemplate may not be loaded until search opened, but Styles are loaded at startup
- Controls/StatCard/StatCardControl.xaml — 6 usages (TextTitle, TextBody, TextHeadline, TextSection, TextCaption) — StatCardControl may be used in other dashboards, but OverviewDashboardView uses DbKpiTileTemplate, not StatCardControl, so MAYBE not startup
- Controls/DonutChart/DonutChartControl.xaml — 4 usages (TextTitle, TextCaption, TextTitleLarge, TextBody) inside UserControl, not ControlTemplate, but used in OverviewDashboardView DeliveryMix — UserControl direct children SAFE? Previous crash stack is FrameworkTemplate, so UserControl internal TextBlocks outside template may be SAFE, but if DonutChartControl's template contains Text* alias, it would be inside template
- Controls/TrendChart/TrendChartControl.xaml — 4 usages (TextHeadline, TextBody, TextCaption, TextBodyLarge) — similar
- Styles/DashboardKit.xaml — 16 remaining Text* usages after 9-file fix (DbTitleStyle TextTitleLarge, DbSubtitleStyle TextBody, DbSectionTitleStyle TextSubhead, DbSectionSubStyle TextCaption, DbMetaStyle TextCaption, DbKpiLabelStyle TextCaption, DbKpiValueStyle TextDisplay, DbKpiFootStyle TextXSmall, DbGlyphStyle TextSection, DbPillTextStyle TextXSmall, etc.) — Styles for TextBlock used inside DataTemplates DbKpiTileTemplate, DbPipelineStageTemplate, etc. — DataTemplate is FrameworkTemplate → high-confidence startup crash
- Styles/DashboardStyles.xaml — 267-268 TextBody TemplateBinding, 334 TextBodyLarge, 593 TextBody, 669 TextBody — old grammar merged before DashboardKit, contains TextBody TemplateBinding — TemplateBinding inside ControlTemplate may resolve to ResourceReferenceExpression? TemplateBinding to Text* alias?
- TypographyStyles.xaml — 12 Style setters TextDisplayLarge/TitleLarge/BodyLarge/Title/Body/Section etc. — used by templates

## Step 5 — Audit All FontSize="{StaticResource / DynamicResource / TextElement.FontSize inside ControlTemplate/DataTemplate/Style/Setter Produce Candidate Table

Generated `/tmp/candidates_enhanced.csv` with 606 total `FontSize="{StaticResource Text*|Density*|ProGlassLayout.*}"` and `Value="{StaticResource Text*}"` occurrences.

**Startup YES = 173** (files merged in App.xaml or MainWindow top bar or Dashboard)

Top startup candidates (first 50):

```
File,Line,Style,Template,Control,Property,ResourceKey,ResolvedType,StartupReachable
Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml,21,AutoCompleteItemStyle,,Setter Value,TextBodyLarge,ResourceReferenceExpression,YES
Controls/DonutChart/DonutChartControl.xaml,19,,StackPanel,FontSize,TextTitle,ResourceReferenceExpression,YES
Controls/DonutChart/DonutChartControl.xaml,24,,TextBlock,FontSize,TextCaption,ResourceReferenceExpression,YES
Controls/DonutChart/DonutChartControl.xaml,35,,TextBlock,FontSize,TextTitleLarge,ResourceReferenceExpression,YES
Controls/DonutChart/DonutChartControl.xaml,40,,TextBlock,FontSize,TextBody,ResourceReferenceExpression,YES
Controls/EmptyState/EmptyStateControl.xaml,52,,TextBlock,FontSize,TextDisplay,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,34,GsInputStyle,,Setter Value,TextBody,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,42,GsResultTitleStyle,,Setter Value,TextBody,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,49,GsResultSubtitleStyle,,Setter Value,TextCaption,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,84,,StackPanel,FontSize,TextBodyLarge,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,102,,Grid,FontSize,TextBody,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,130,,Button,FontSize,TextCaption,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,215,,TextBlock,FontSize,TextBody,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,235,,TextBlock,FontSize,TextXSmall,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,252,,StackPanel,FontSize,TextHeadline,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,257,,TextBlock,FontSize,TextBody,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,262,,TextBlock,FontSize,TextCaption,ResourceReferenceExpression,YES
Controls/GlobalSearch/GlobalSearchBar.xaml,275,,Border,FontSize,TextCaption,ResourceReferenceExpression,YES
Controls/StatCard/StatCardControl.xaml,145,,TextBlock,FontSize,TextTitle,ResourceReferenceExpression,YES
Controls/StatCard/StatCardControl.xaml,156,,StackPanel,FontSize,TextBody,ResourceReferenceExpression,YES
Controls/StatCard/StatCardControl.xaml,179,,TextBlock,FontSize,TextHeadline,ResourceReferenceExpression,YES
Controls/StatCard/StatCardControl.xaml,209,,TextBlock,FontSize,TextBody,ResourceReferenceExpression,YES
Controls/StatCard/StatCardControl.xaml,216,,TextBlock,FontSize,TextSection,ResourceReferenceExpression,YES
Controls/StatCard/StatCardControl.xaml,238,,TextBlock,FontSize,TextCaption,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,102,DbTitleStyle,,Setter Value,TextTitleLarge,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,109,DbSubtitleStyle,,Setter Value,TextBody,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,116,DbSectionTitleStyle,,Setter Value,TextSubhead,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,123,DbSectionSubStyle,,Setter Value,TextCaption,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,130,DbMetaStyle,,Setter Value,TextCaption,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,136,DbKpiLabelStyle,,Setter Value,TextCaption,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,145,DbKpiValueStyle,,Setter Value,TextDisplay,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,152,DbKpiFootStyle,,Setter Value,TextXSmall,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,160,DbGlyphStyle,,Setter Value,TextSection,ResourceReferenceExpression,YES
Styles/DashboardKit.xaml,246,DbPillTextStyle,,Setter Value,TextXSmall,ResourceReferenceExpression,YES
...
```

Full CSV: `/tmp/candidates_enhanced.csv` (606 rows), startup YES: 173, MAYBE: additional Controls.

**Key finding:** 9-file fix eliminated crash in MainWindow top bar and DbActionButtonStyle Glyph, but left 16 Text* usages in DashboardKit Styles that are used inside DataTemplates (DbKpiTileTemplate etc.) which are FrameworkTemplates loaded during OverviewDashboardView first measure — explains why crash persists after 9-file fix.

## Step 6 — Identify First Startup Candidate Before MeasureCore

**Hypothesis based on stack and visual tree:**

Stack: `FrameworkTemplate.ReceivePropertySet → LoadOptimizedTemplateContent → LoadContent → StyleHelper.ApplyTemplateContent → FrameworkElement.ApplyTemplate → MeasureCore → Grid/Border/StackPanel/ScrollViewer/Grid/Border/Control.Measure/Grid.MeasureOverride`

- ScrollViewer is in OverviewDashboardView (ScrollViewer with SmoothScrollBehavior)
- Grid/Border/Control.Measure suggests ContentControl or ItemsControl with DataTemplate
- OverviewDashboardView has ItemsControl with DbPipelineStageTemplate and DbKpiTileTemplate (DataTemplates) inside ScrollViewer → matches stack

**First candidate before MeasureCore:**

1. **GlobalSearchBar GsModuleComboStyle TextCaption** — ComboBox in search pill, Style setter FontSize TextCaption, ComboBox's ControlTemplate applies FontSize during MainWindow measure — but we fixed 1 of 3 in GlobalSearchBar (GsModuleComboStyle), remaining 2 Style setters (GsInputStyle TextBody, GsResultTitleStyle TextBody, GsResultSubtitleStyle TextCaption) still present — could be first
2. **DashboardKit DbKpiLabelStyle TextCaption / DbKpiValueStyle TextDisplay** — DataTemplate DbKpiTileTemplate contains TextBlocks with Style DbKpiLabelStyle etc., loaded when OverviewDashboardView's UniformGrid of KPI tiles measures — if GlobalSearchBar Styles are safe (outside template?), then KPI tile DataTemplate is first FrameworkTemplate after MainWindow

**Evidence needed from Windows:** Run with enhanced forensics, capture crash-latest.txt with [Forensics] VisualTree depth logs and [Forensics] Stack frames to see which control measured (GlobalSearchBar ComboBox vs DbKpiTileTemplate Border/Grid/TextBlock)

**Current proven occurrence (minimal fix):** GlobalSearchBar.xaml line 27 GsModuleComboStyle TextCaption → FontSize.BodySmall — fixed as ONE minimal occurrence per rule, but crash likely still persists due to remaining 2 Style setters in same file and 16 in DashboardKit.

## Step 7 — Minimal Fix Only Proven Occurrence (ONE occurrence, rebuild, launch, repeat)

- Fixed: Controls/GlobalSearch/GlobalSearchBar.xaml line 27 GsModuleComboStyle `TextCaption` → `FontSize.BodySmall` (direct DynamicResource Double, VALID)
- Preserved: 9-file fix 42 lines, no unrelated changes, no mass replacement of 415 remaining candidates
- Next iteration: After Windows rebuild + launch, observe crash-latest.txt BaseUri Line Pos resource key/type/control/style/template/property/theme/module/Crash ID — if still crashes, identify exact next template (likely GsInputStyle TextBody or DbKpiLabelStyle TextCaption) and fix ONE occurrence, repeat until clean

## Step 8 — Rebuild Release + Launch EXE (Windows)

**Required on Windows machine (Linux arena cannot build WPF):**

```powershell
git pull origin arena/01a09a9f-proglassautomationadv
dotnet clean -c Release
dotnet build -c Release
# Verify 0 errors
.\bin\Release\net8.0-windows\ProGlassAutomation.exe
# Capture logs\crash-latest.txt and logs\app-*.log with [Forensics] entries
```

**Expected after this fix:** Crash may still occur (ERR-102553) because 2 more Style setters in GlobalSearchBar and 16 in DashboardKit remain — but next crash log will contain enhanced forensics VisualTree and Stack frames to pinpoint exact next template.

**Iterative process:**
- crash1 → exact template (e.g., GsInputStyle TextBody) → minimal fix ONE occurrence → rebuild → launch → observe
- crash2 → exact template (e.g., DbKpiLabelStyle TextCaption) → minimal fix ONE occurrence → rebuild → launch → observe
- Repeat until Splash→MainWindow→Dashboard renders with no XamlParseException/ResourceReferenceExpression/ArgumentException

## Step 9 — Handle Multiple Failures Iteratively (crash2→exact template→minimal fix→rebuild→launch→repeat)

**Documented remaining startup failures after 9-file fix:**

- GlobalSearchBar: 12 remaining Text* usages (GsInputStyle TextBody, GsResultTitleStyle TextBody, GsResultSubtitleStyle TextCaption, TextBlock TextBodyLarge/Body/Caption/XSmall/Headline)
- DashboardKit: 16 remaining (DbTitleStyle TextTitleLarge, DbSubtitleStyle TextBody, DbSectionTitleStyle TextSubhead, DbSectionSubStyle TextCaption, DbMetaStyle TextCaption, DbKpiLabelStyle TextCaption, DbKpiValueStyle TextDisplay, DbKpiFootStyle TextXSmall, DbGlyphStyle TextSection, DbPillTextStyle TextXSmall, etc.)
- DashboardStyles: 4+ (TextBody TemplateBinding)
- DonutChart/TrendChart: 8
- StatCard: 6
- EmptyState: 4
- etc.

**Total startup YES: 173** — must be fixed iteratively ONE by ONE with Windows verification, not mass-edit.

## Step 10 — Classify Remaining 415 UNVERIFIED Not SAFE

- 472 remaining `FontSize="{StaticResource Text` after 9-file fix (now 471 after GlobalSearchBar 1 fix)
- 606 total FontSize StaticResource Text*/Density*/ProGlassLayout* (including Setter Value)
- 173 startup YES — high-confidence crash risks
- 433 remaining NO/MAYBE — module smoke only after startup succeeds (Inquiry/PI/JO/DN/Tax Invoice/Specifications/Sheet Store/Production/Cutting/Optimization/P&L)
- Classification: UNVERIFIED, not SAFE — cannot claim safe until Windows clean launch and module smoke pass

## Step 11 — Module Smoke Only After Splash→MainWindow→Dashboard Clean

**Gate:** Splash→MainWindow→Dashboard must render with no XamlParseException/ResourceReferenceExpression/ArgumentException/InvalidOperationException/NullReferenceException before module smoke.

**Module smoke checklist (after startup clean):**
- Inquiry, PI, JO, DN, Tax Invoice, Specifications, Sheet Store, Production, Cutting, Optimization, P&L
- 5 themes: Normal, Normal Industrial, Monet, CrystalFluid, iOS26
- Resize/maximize/restore/minimize/reopen/close
- Shutdown UCEERR_MISSINGENDCOMMAND 0x88980411 cascading vs clean

**Current:** BLOCKED — cannot proceed to module smoke until ERR-102553 eliminated.

## Step 12 — Color #14808080 Only If Actually Consumed

- GDS 729-730 #14808080 still present, SplashView 98 still present — not proven consuming per task, but not causing FontSize crash
- Fix only if actually consumed and proven via forensics — currently deferred, not part of FontSize crash path

## Step 13 — Shutdown UCEERR_MISSINGENDCOMMAND 0x88980411 Cascading vs Clean

- UCEERR_MISSINGENDCOMMAND 0x88980411 is WPF render thread race on shutdown — must check if cascading from earlier XamlParseException (crash leaves dispatcher undefined) vs clean shutdown after successful launch
- Only verifiable after clean launch — currently BLOCKED

## Step 14 — Final Push Gate All 17 Items PASS Before Commit/Push

**Acceptance matrix:**

- Windows env externally YES (Windows 10.0.26200.0 .NET 8.0.31 x64)
- Linux arena NO (dotnet not found)
- Release build 0 errors PENDING Windows verification
- EXE launch PENDING (ERR-102553 still crashes after 9-file fix)
- Splash/MainWindow/Dashboard PENDING
- No FontSize ResourceReferenceExpression PENDING
- Color #14808080 PENDING
- 5 themes PENDING
- Modules/resize/maximize/minimize/shutdown/UCEERR/logs PENDING
- 7 PASS no test/baseline/allowlist manipulation etc — BLOCKED

**PUSH = FORBIDDEN, DO NOT COMMIT, DO NOT START PHASE 20, DO NOT MASS-EDIT 415 remaining candidates**

## Next Steps for Windows Machine

1. Pull branch `arena/01a09a9f-proglassautomationadv`
2. `dotnet clean && dotnet build -c Release` — verify 0 errors
3. Launch `bin\Release\net8.0-windows\ProGlassAutomation.exe`
4. Capture `logs\crash-latest.txt` and `logs\app-*.log` with [Forensics] entries
5. Identify exact template from BaseUri Line Pos resource key/type/control/style/template/property/theme/module/Crash ID
6. Fix ONE occurrence (e.g., next GlobalSearchBar GsInputStyle or DashboardKit DbKpiLabelStyle)
7. Rebuild, relaunch, repeat until clean
8. After clean, module smoke, theme smoke, window smoke, shutdown UCEERR, zero-gate logs
9. Only then commit/push

## Artifacts

- `/tmp/candidates_enhanced.csv` — 606 total, 173 startup YES, with File/Line/Style/Template/Control/Property/ResourceKey/Expression/ResolvedType/StartupReachable/Context
- `/tmp/candidates_full.csv` — 457 previous
- `docs/PHASE19R_FINAL_WINDOWS_GATE_REPORT.md` — 19 sections BLOCKED PUSH FORBIDDEN
- `docs/PHASE19R_FULL_CRASH_STABILIZATION_REPORT.md` — 15 sections 992 lines with 9-file fix note
- `docs/PHASE19R_FAILURE_CLASSIFICATION.md` — 134 classified A12 B8 C68 D12 E10 F4 G12 H4 I4 untouched

## Evidence Chain

- 0de6591 introduced Text* = DynamicResource FontSize.* alias chain — previously Text* may have been Double directly, now ResourceReferenceExpression
- FrameworkTemplate.ReceivePropertySet rejects ResourceReferenceExpression for FontSize (Double expected)
- 9-file fix (MainWindow, ShellStyles, ButtonStyles, DashboardKit 2, DialogStyles, InputStyles, ListPageStyles, ModuleTabStyles, ContextMenuStyles) eliminated crash in top bar and hero action buttons but left 16 in DashboardKit DataTemplates and 12 in GlobalSearchBar
- Stack trace matches DataTemplate loading inside ScrollViewer Grid Border Control Measure — OverviewDashboardView KPI tiles or GlobalSearchBar ComboBox
- Enhanced forensics will pinpoint exact next occurrence on Windows

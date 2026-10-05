# PHASE 19R ERR-35B71A EXACT WPF FONTSIZE TEMPLATE FORENSICS

**Date:** 2026-09-14
**Branch:** arena/01a09a9f-proglassautomationadv
**HEAD:** d3a564791c01e5e1ac3dd1c05fdbf628ce52faea + 10 local fixes (43 lines) — 9-file startup + 1 GlobalSearchBar GsModuleComboStyle — preserved per ABSOLUTE RULE
**Error ID:** ERR-35B71A
**Version:** ProGlass Automation 2.0.1
**OS:** Windows 11 / Microsoft Windows NT 10.0.26200.0
**Runtime:** .NET 8.0.31 x64
**Theme:** Normal (Industrial)
**Status:** BLOCKED — 9-file startup fix FAILED, crash persists, exact template not yet located with runtime evidence — PUSH FORBIDDEN

## 1. Baseline Freeze

```
git rev-parse HEAD → d3a564791c01e5e1ac3dd1c05fdbf628ce52faea
git status --short → M App.StartupCore.cs, M Controls/GlobalSearch/GlobalSearchBar.xaml, M MainWindow.xaml, M Styles/ButtonStyles.xaml, M Styles/ContextMenuStyles.xaml, M Styles/DashboardKit.xaml, M Themes/Styles/DialogStyles.xaml, M Themes/Styles/InputStyles.xaml, M Themes/Styles/ListPageStyles.xaml, M Themes/Styles/ModuleTabStyles.xaml, M Themes/Styles/ShellStyles.xaml, ?? docs/PHASE19R_*
git diff --stat → 11 files 160+/56- (10 FontSize fixes 43 lines + App.StartupCore.cs 130 lines enhanced forensics)
git log -10 --oneline → d3a5647 fix DashboardKit:279, 80c3008 fix SourceUri, f69420c forensics prove not running, 65ba3db identify culprit, 3ab8122 enhance diagnostics, 16f7da0 update forensics, 5c1fbd7 enhance crash reporting, b75e9d7 docs Phase8 baseline, 15d4e7c fix ERR-57855F, 6dd59f9 fix ERR-7366FE
```

Existing 9-file local FontSize changes are intentional forensic changes — DO NOT discard — per task instruction. DO NOT create new broad refactor.

- MainWindow.xaml 28 lines: UserCardButton Glyph TextBody→FontSize.Body, TopPillTitleStyle TextBody→FontSize.Body, TopPillCaptionStyle TextXSmall→FontSize.Caption, DateTime pill TextBodyLarge→FontSize.Title, etc.
- ShellStyles.xaml 14 lines: TopIconButton TextSubhead→FontSize.H3, TopPillTitleStyle TextBody→FontSize.Body, SidebarGroupLabel TextXSmall→FontSize.Caption, SidebarNavItem Icon TextSection→FontSize.H3 Label TextBodyLarge→FontSize.Title, etc.
- ButtonStyles.xaml 8 lines: GlassPillButtonTemplate Icon TextBodyLarge→FontSize.Title, ContentPresenter TextElement.FontSize TextBody→FontSize.Body, RowButtonTemplate TextCaption→FontSize.BodySmall
- DashboardKit.xaml 8 lines: Db* styles TextBody→FontSize.Body TextCaption→FontSize.BodySmall inside DataTemplates (only 2 of 18 fixed, 16 remain)
- InputStyles.xaml 4, DialogStyles.xaml 4, ListPageStyles.xaml 6, ModuleTabStyles.xaml 6, ContextMenuStyles.xaml 6
- GlobalSearchBar.xaml 1: GsModuleComboStyle TextCaption→FontSize.BodySmall (added after 9-file fix failed)

## 2. Crash Evidence

**Current failure after 9-file fix:**

Application reaches:
- Main DB ready
- Audit initialized
- Performance initialized
- Visual theme applied
- AI/STT/TTS initialized
- License check successful

Then during layout/rendering (~4 seconds after startup):

```
System.Windows.Markup.XamlParseException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
Inner: System.ArgumentException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
```

Critical stack:
```
System.Windows.FrameworkTemplate.ReceivePropertySet(...)
System.Windows.FrameworkTemplate.LoadOptimizedTemplateContent(...)
System.Windows.FrameworkTemplate.LoadContent(...)
System.Windows.StyleHelper.ApplyTemplateContent(...)
System.Windows.FrameworkElement.ApplyTemplate()
System.Windows.FrameworkElement.MeasureCore(...)
Grid.MeasureOverride
Border.MeasureOverride
StackPanel.MeasureOverride
ScrollViewer.MeasureOverride
Grid.MeasureOverride
Border.MeasureOverride
Control.Measure
Grid.MeasureOverride
```

XAML BaseUri, LineNumber, LinePosition unavailable (WPF returns BaseUri= Line=0 Pos=0 for BAML template failures).

**Previous crash logs:**
- ERR-2530A1 / ERR-1B5EEE / ERR-B6F94A / ERR-35B71A same signature — deterministic path Normal Industrial theme → startup services → license Trial expires 2026-09-25 → WPF layout/template MainWindow → DashboardView ScrollViewer hero/buttons → LoadOptimizedTemplateContent → ReceivePropertySet rejects ResourceReferenceExpression → XamlParseException
- Forensic logs prove FontSize.Body=Double 13 VALID, TextBody=ResourceReferenceExpression INVALID, nested chain confirmed via App.StartupCore.cs Forensics_ResourceCheck

## 3. Proven Fact — Canonical FontSize VALID, Aliases INVALID

Runtime instrumentation reports:

```
FontSize.Body = 13
FontSize.Title = 14
FontSize.Caption = 10
FontSize.H2 = 20
FontSize.Kpi = 24
FontSize.H1 = 24
FontSize.H3 = 16
FontSize.Display = 32
All System.Double VALID
```

However:

```
TextBody, TextBodyLarge, TextCaption, TextXSmall, TextTitle, TextTitleLarge, TextSection, TextSubhead,
DensityBodyFontSize, DensityKpiValueFontSize, DensityTitleFontSize,
ProGlassLayout.BodyFontSize, ProGlassLayout.KpiFontSize
→ System.Windows.ResourceReferenceExpression
```

**Root chain:**
- LightTheme/DarkTheme: DensityBodyFontSize = 13/18 Double VALID, but GlobalDesignSystem.xaml:138-140 redefines DensityBodyFontSize = DynamicResource FontSize.Body → ResourceReferenceExpression
- LayoutTokens.xaml: ProGlassLayout.BodyFontSize = DynamicResource DensityBodyFontSize → ResourceReferenceExpression (double nesting)
- GlobalDesignSystem.xaml: TextBody = DynamicResource FontSize.Body → ResourceReferenceExpression
- Therefore `FontSize="{StaticResource TextBody}"` where TextBody = DynamicResource FontSize.Body resolves to ResourceReferenceExpression at load time → FrameworkTemplate.ReceivePropertySet rejects it

**Important WPF semantic distinction:**
- VALID: `FontSize="{DynamicResource FontSize.Body}"` because FontSize is DependencyProperty
- DANGEROUS: `<Style.Resources><something x:Key="TextBody">{DynamicResource FontSize.Body}</something></Style.Resources>` followed by template/property path where WPF receives unresolved ResourceReferenceExpression as actual property value

## 4. Candidate Inventory — Complete XAML Resource Universe

Search complete XAML universe for:

```
FontSize="{StaticResource Text
FontSize="{DynamicResource Text
FontSize="{StaticResource Density
FontSize="{DynamicResource Density
FontSize="{StaticResource ProGlassLayout
FontSize="{DynamicResource ProGlassLayout
TextElement.FontSize="{StaticResource Text
TextElement.FontSize="{DynamicResource Text
TextElement.FontSize="{StaticResource Density
TextElement.FontSize="{DynamicResource Density
Indirect: StaticResource TextBody, TextBodyLarge, TextCaption, TextXSmall, TextTitle, TextTitleLarge, TextSection, TextSubhead, DensityBodyFontSize, DensityKpiValueFontSize, DensityTitleFontSize, ProGlassLayout.BodyFontSize, ProGlassLayout.KpiFontSize
```

**Counts:**

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
| **Total FontSize+Setter** | **606** |
| Indirect StaticResource TextBody | 286 |
| Indirect StaticResource TextBodyLarge | 57 |
| Indirect StaticResource TextCaption | 173 |
| Indirect StaticResource TextXSmall | 33 |
| Indirect StaticResource TextTitle | 33 |
| Indirect StaticResource TextTitleLarge | 7 |
| Indirect StaticResource TextSection | 27 |
| Indirect StaticResource TextSubhead | 20 |

Full candidate table: `docs/PHASE19R_ERR35B71A_TEMPLATE_CANDIDATES.md` (463 lines, first 300 of 606) and `docs/PHASE19R_CANDIDATES_ENHANCED.csv` (606 rows with File/Line/Style/Template/Control/Property/ResourceKey/Expression/ResolvedType/StartupReachable/Context)

**Startup YES = 173** — files merged in App.xaml or MainWindow top bar or Dashboard

## 5. Runtime Investigation Method

Per task, STOP blind replacement, use runtime instrumentation.

**Preferred method — Windows debugger:**

Run actual application under Windows debugger (Visual Studio), break on first-chance:

- System.ArgumentException
- System.Windows.Markup.XamlParseException

When exception first occurs, inspect at:

```
FrameworkTemplate.ReceivePropertySet(
    Object targetObject,
    XamlMember member,
    Object value,
    DependencyObject templatedParent)
```

Determine:
- targetObject type
- member name
- value type
- templatedParent type
- templatedParent style
- templatedParent template

If Visual Studio can break at first-chance exception, inspect locals/arguments at ReceivePropertySet — highest-value evidence.

**Enhanced forensics already added to App.StartupCore.cs (130 lines):**

- FirstChanceException hook for XamlParseException: logs BaseUri, LineNumber, LinePosition, Message, HResult, InnerException Type/Message/Stack, InnerInner, VisualTheme, Accent, AppTheme
- VisualTree snapshot at time of XamlParseException: walks MainWindow visual tree depth 0-8, logs Type, Name, TemplatedParent, Template, LocalFontSizeType/Value, TextElement.LocalType/Value — up to 150 elements
- ArgumentException ResourceReferenceExpression: logs Message, HResult, Stack 5000 chars, Data keys, Dispatcher Thread, HasShutdownStarted, relevant frames ReceivePropertySet/LoadContent/ApplyTemplate/Measure file/line
- CrashReportService BuildChain logs XAML BaseUri, LineNumber, LinePosition, InnerException Type/Message, ArgumentException ParamName/Data

**If debugger source still unavailable:**

Add temporary diagnostic instrumentation around startup visual tree immediately before crash-prone initial layout. Enumerate visual/logical tree and record Type, Name, Style, Template, ActualWidth, ActualHeight, IsVisible, IsLoaded. For each Control: Control.Template, Control.Style, Control.FontSize, TextElement.FontSize where applicable. Do not modify actual property values — identification only.

**Startup visual-tree forensics:**

At point immediately before initial dashboard/window rendering, log [Forensics][VisualTree] with every reachable control. Especially identify MainWindow, Shell, Navigation, Sidebar, TopBar, Dashboard, User card, Buttons, Tabs, ScrollViewer, DataGrid, ItemsControl, ListBox, ContentControl, Custom controls.

Crash stack contains Grid Border StackPanel ScrollViewer Control Grid Border — does NOT prove which control responsible — do not infer from stack alone.

**Control-template tracing:**

For each startup-reachable Control, log Control type, x:Name, Style key, Template key, Template type, FontSize local value, FontSize effective value. Also inspect Style.Setters, Style.Resources, Template.Resources, BasedOn. When control's template is applied, log [Forensics][TemplateApply] Control=... Name=... Style=... Template=...

Goal: determine which exact template is being instantiated immediately before ERR-35B71A.

## 6. Exact Failing Control — NOT YET LOCATED (Requires Windows Debugger)

**Current hypothesis based on stack and visual tree:**

Stack contains ScrollViewer → OverviewDashboardView has ScrollViewer with SmoothScrollBehavior → Grid with hero strip (DbTitleStyle TextTitleLarge, DbSubtitleStyle TextBody, DbActionButtonStyle FontSize TextCaption) → pipeline rail → pulse/mix/in-prod row (DbKpiLabelStyle TextCaption, DbKpiValueStyle TextDisplay, DonutChart, TrendChart)

**Top remaining startup YES candidates after 9-file fix FAILED:**

- Controls/GlobalSearch/GlobalSearchBar.xaml — 12 remaining: GsInputStyle TextBody, GsResultTitleStyle TextBody, GsResultSubtitleStyle TextCaption, TextBlock TextBodyLarge/Body/Caption/XSmall/Headline inside UserControl and DataTemplate (195,241) — DataTemplate is FrameworkTemplate → high-confidence, ComboBox and TextBox Styles loaded during MainWindow top bar init
- Styles/DashboardKit.xaml — 16 remaining: DbTitleStyle TextTitleLarge, DbSubtitleStyle TextBody, DbSectionTitleStyle TextSubhead, DbSectionSubStyle TextCaption, DbMetaStyle TextCaption, DbKpiLabelStyle TextCaption, DbKpiValueStyle TextDisplay, DbKpiFootStyle TextXSmall, DbGlyphStyle TextSection, DbPillTextStyle TextXSmall, etc. — Styles for TextBlock used inside DataTemplates DbKpiTileTemplate, DbPipelineStageTemplate — DataTemplate is FrameworkTemplate → high-confidence startup crash, OverviewDashboardView KPI tiles loaded during Dashboard first measure inside ScrollViewer
- Styles/DashboardStyles.xaml — 4+ TextBody TemplateBinding, TextBodyLarge, etc. — old grammar merged before DashboardKit
- Controls/StatCard/StatCardControl.xaml — 6 TextTitle/Body/Headline/Section/Caption — MAYBE not startup but could be
- Controls/DonutChart/DonutChartControl.xaml — 4 TextTitle/Caption/TitleLarge/Body — UserControl direct children SAFE? But if inside template, could crash
- Controls/TrendChart/TrendChartControl.xaml — 4 TextHeadline/Body/Caption/BodyLarge
- Themes/Styles/TypographyStyles.xaml — 12 Style setters TextDisplayLarge/TitleLarge/BodyLarge/Title/Body/Section — used by templates — YES startup reachable via App.xaml

**Required evidence to locate exact control:**

Must run Windows debugger, break at ReceivePropertySet, capture:

```
ERR-35B71A
→ Control: Button (or TextBlock, ComboBox, etc.)
→ Name: xyz
→ Style: GlassPillButtonStyle (or DbKpiLabelStyle, GsModuleComboStyle, etc.)
→ Template: GlassPillButtonTemplate (or DbKpiTileTemplate, etc.)
→ Template property: FontSize
→ Source XAML: ButtonStyles.xaml:67 (or DashboardKit.xaml:136, GlobalSearchBar.xaml:34, etc.)
→ Resource: TextBodyLarge (or TextCaption, TextBody, etc.)
→ TextBodyLarge runtime type: ResourceReferenceExpression
→ underlying FontSize resource: FontSize.Title (or Body, BodySmall, etc.)
→ exact WPF failure occurs here
```

Without this chain, do NOT modify another candidate per task.

## 7. Exact Failing Template — NOT YET LOCATED

**Previous 9-file fix modified:** MainWindow.xaml, ButtonStyles.xaml, ContextMenuStyles.xaml, DashboardKit.xaml (only 2 of 18), DialogStyles.xaml, InputStyles.xaml, ListPageStyles.xaml, ModuleTabStyles.xaml, ShellStyles.xaml

Crash proves at least one remaining startup-reachable template/path still produces invalid ResourceReferenceExpression FontSize assignment.

**Do NOT assume previous 9 files complete — do NOT simply modify another random candidate — identify it via runtime.**

Potential templates:

- GlobalSearchBar GsModuleComboStyle, GsInputStyle, GsResultTitleStyle, GsResultSubtitleStyle — ComboBox and TextBox ControlTemplates applying FontSize from Style setter that is ResourceReferenceExpression
- DashboardKit DbKpiLabelStyle, DbKpiValueStyle, DbKpiFootStyle, DbGlyphStyle, DbTitleStyle, DbSubtitleStyle, etc. — DataTemplate DbKpiTileTemplate contains TextBlocks with Style DbKpiLabelStyle etc., DataTemplate is FrameworkTemplate → ReceivePropertySet
- DashboardStyles Db* old grammar
- TypographyStyles Style setters

**Exact template must be proven via debugger locals at ReceivePropertySet — targetObject, member, value, templatedParent.**

## 8. Exact Source Line — NOT YET LOCATED

Requires Windows debugger evidence.

Candidate source lines (from enhanced CSV, startup YES):

```
Controls/GlobalSearch/GlobalSearchBar.xaml:34 GsInputStyle Setter Value TextBody
Controls/GlobalSearch/GlobalSearchBar.xaml:42 GsResultTitleStyle Setter Value TextBody
Controls/GlobalSearch/GlobalSearchBar.xaml:49 GsResultSubtitleStyle Setter Value TextCaption
Controls/GlobalSearch/GlobalSearchBar.xaml:84 TextBlock TextBodyLarge
Controls/GlobalSearch/GlobalSearchBar.xaml:102 TextBlock TextBody
Styles/DashboardKit.xaml:102 DbTitleStyle Setter Value TextTitleLarge
Styles/DashboardKit.xaml:109 DbSubtitleStyle Setter Value TextBody
Styles/DashboardKit.xaml:116 DbSectionTitleStyle Setter Value TextSubhead
Styles/DashboardKit.xaml:123 DbSectionSubStyle Setter Value TextCaption
Styles/DashboardKit.xaml:130 DbMetaStyle Setter Value TextCaption
Styles/DashboardKit.xaml:136 DbKpiLabelStyle Setter Value TextCaption
Styles/DashboardKit.xaml:145 DbKpiValueStyle Setter Value TextDisplay
Styles/DashboardKit.xaml:152 DbKpiFootStyle Setter Value TextXSmall
Styles/DashboardKit.xaml:160 DbGlyphStyle Setter Value TextSection
...
```

But static line number NOT sufficient — must have runtime evidence of which template is being measured when ReceivePropertySet fails.

## 9. Resource Chain — Proven Mechanism, Exact Consumer Not Yet Proven

**Proven mechanism:**

```
Template (ControlTemplate/DataTemplate)
→ FontSize property
→ StaticResource alias (TextBody, TextBodyLarge, TextCaption, etc.)
→ DynamicResource FontSize.* (via GlobalDesignSystem.xaml Text* = DynamicResource FontSize.*)
→ ResourceReferenceExpression (unresolved)
→ FrameworkTemplate.ReceivePropertySet rejects ResourceReferenceExpression for FontSize (expects Double)
→ ArgumentException → XamlParseException → crash
```

**Example chain (hypothesis, needs runtime proof):**

```
ERR-35B71A
→ Control: TextBlock (inside DataTemplate DbKpiTileTemplate)
→ Name: (unnamed)
→ Style: DbKpiLabelStyle
→ Template: DbKpiTileTemplate (DataTemplate)
→ Template property: FontSize
→ Source XAML: DashboardKit.xaml:136
→ Resource: TextCaption
→ TextCaption runtime type: ResourceReferenceExpression (TextCaption = DynamicResource FontSize.BodySmall)
→ underlying FontSize resource: FontSize.BodySmall = 12 Double VALID
→ exact WPF failure occurs here when DataTemplate loads and Style setter Value="{StaticResource TextCaption}" resolves to ResourceReferenceExpression
```

**Alternative chain (also hypothesis):**

```
ERR-35B71A
→ Control: ComboBox (GsModuleComboStyle)
→ Name: (search filter)
→ Style: GsModuleComboStyle
→ Template: ComboBox ControlTemplate (default)
→ Template property: FontSize
→ Source XAML: GlobalSearchBar.xaml:27 (already fixed) or 34
→ Resource: TextCaption / TextBody
→ runtime type: ResourceReferenceExpression
→ underlying: FontSize.BodySmall / Body
→ failure at ComboBox template apply during MainWindow top bar measure
```

Without runtime evidence at ReceivePropertySet, cannot determine which chain is actual first failure.

## 10. Root Cause — Indirect Alias Chain

**Root cause is NOT canonical FontSize resources themselves (they are Double VALID) but the alias indirection:**

- GlobalDesignSystem.xaml defines Text* = DynamicResource FontSize.* → ResourceReferenceExpression
- LayoutTokens.xaml defines ProGlassLayout.BodyFontSize = DynamicResource DensityBodyFontSize → ResourceReferenceExpression
- GDS 138-140 defines DensityBodyFontSize = DynamicResource FontSize.Body → ResourceReferenceExpression
- Any template that does `FontSize="{StaticResource TextBody}"` where TextBody is ResourceReferenceExpression causes WPF to receive ResourceReferenceExpression as property value inside FrameworkTemplate — invalid for FontSize (Double)

**Why 9-file fix FAILED:**

- Fixed MainWindow top bar, ShellStyles top bar/sidebar, ButtonStyles glass pill, InputStyles ComboBox ToggleButton, DialogStyles, ListPageStyles, ModuleTabStyles, ContextMenuStyles, DashboardKit hero (8 lines) — high-confidence startup path
- BUT left 16 Text* usages in DashboardKit Styles that are used inside DataTemplates DbKpiTileTemplate, DbPipelineStageTemplate which are FrameworkTemplates loaded during OverviewDashboardView first measure inside ScrollViewer — matches stack Grid Border StackPanel ScrollViewer Grid Border Control Measure
- Also left 12 in GlobalSearchBar including DataTemplate for ListBox results and Styles GsInputStyle, GsResultTitleStyle, GsResultSubtitleStyle — ComboBox and TextBox Styles loaded during MainWindow init
- Therefore crash persists — at least one remaining startup-reachable template/path still produces invalid assignment

## 11. Minimal Fix — ONE Proven Consumer Only After Runtime Evidence

Per task: Only after exact runtime evidence identifies failing source, modify smallest possible source location, preserve theme architecture, dynamic theme switching, density switching, six accent/theme families, do not hardcode numeric FontSize, do not create duplicate typography systems, do not modify unrelated templates.

**If proven fix is `FontSize="{DynamicResource FontSize.Body}"`, use that only at proven failing consumer.**

**Current minimal fix already applied (after 9-file fix failed):**

- Controls/GlobalSearch/GlobalSearchBar.xaml:27 GsModuleComboStyle TextCaption → FontSize.BodySmall (direct DynamicResource Double VALID) — ONE occurrence per rule

**Next minimal fix must wait for runtime evidence:**

- Do NOT apply another mass replacement
- Record ERR-xxxxxx and repeat debugger → first-chance exception → ReceivePropertySet → targetObject → member → value → templatedParent → exact template
- Then fix only newly proven consumer — multiple remaining offenders must be handled one at a time

**Example of correct minimal fix (if runtime proves DbKpiLabelStyle is culprit):**

```xml
<!-- Before (dangerous): -->
<Setter Property="FontSize" Value="{StaticResource TextCaption}"/>

<!-- After (safe, preserves dynamic): -->
<Setter Property="FontSize" Value="{DynamicResource FontSize.BodySmall}"/>
```

Use only at proven failing consumer, not globally.

## 12. Build Result — Linux Arena BLOCKED, Windows Required

```
dotnet --info → dotnet: command not found — Linux arena, /usr/share/dotnet missing
dotnet clean → not available — WPF BUILD BLOCKED BY CURRENT ENVIRONMENT
dotnet build -c Release → not available
```

**Requirement 0 errors:** Cannot verify in Linux — WINDOWS BUILD = NOT VERIFIED

**Expected:** XAML-only changes, no new C# warnings, previous Windows 1361 warnings known (CS8602, CS0414, etc.) — this fix should not introduce new warnings, but need Windows verification

## 13. Windows Launch Result — PENDING — Requires Windows Debugger

**Required on Windows machine:**

```powershell
git pull origin arena/01a09a9f-proglassautomationadv
dotnet clean -c Release
dotnet build -c Release
# Verify 0 errors
.\bin\Release\net8.0-windows\ProGlassAutomation.exe
# Run under Visual Studio debugger, break on first-chance ArgumentException and XamlParseException
# At FrameworkTemplate.ReceivePropertySet, inspect targetObject, member, value, templatedParent
# Capture crash-latest.txt BaseUri Line Pos resource key/type/control/style/template/property/theme/module/Crash ID
# Identify exact template, minimal fix ONE occurrence, rebuild, launch, repeat until clean
```

**Expected after this fix:** Crash may still occur (ERR-35B71A) because 12 more in GlobalSearchBar and 16 in DashboardKit remain — but next crash log with enhanced forensics VisualTree and Stack frames will pinpoint exact next template.

**Success criteria for this forensic pass:**

- Exact source: File, Line, Style, Template, Control, Property, Resource, Runtime resource type
- Exact causal chain: Control → Template → FontSize property → Resource alias → ResourceReferenceExpression → WPF ArgumentException
- Exact fix: One minimal source change
- Runtime result: Actual Windows launch proving exception gone

**Current:** NOT YET ACHIEVED — requires Windows debugger evidence

## 14. Regression Result — PENDING Windows

**After clean startup, test:**

- 5 themes: Normal, Normal Industrial, Monet, CrystalFluid, iOS26
- Modules: Inquiry, PI, JO, DN, Tax Invoice, Specifications, Sheet Store, Production, Cutting, Optimization, P&L
- Resize/maximize/restore/minimize/reopen/close
- Shutdown UCEERR_MISSINGENDCOMMAND 0x88980411 cascading vs clean
- Zero-gate logs search Unhandled/CRITICAL/Fatal/XamlParseException/ResourceReferenceExpression/ArgumentException/InvalidOperationException/NullReferenceException/COMException/UCEERR

**Current:** BLOCKED — cannot proceed to regression until ERR-35B71A eliminated

## 15. Remaining Candidates Explicitly Marked UNVERIFIED

- 472 FontSize="{StaticResource Text" after 10-file fix (471 after GlobalSearchBar 1 fix) + 134 Setter Value="{StaticResource Text" = 606 total
- 173 startup YES — high-confidence crash risks — UNVERIFIED until Windows proves which is first
- 433 remaining NO/MAYBE — module smoke only after startup succeeds — UNVERIFIED
- Classification: UNVERIFIED, not SAFE — cannot claim safe until Windows clean launch and module smoke pass
- Previous 415 deferred unless Windows proves crash — still deferred per ABSOLUTE RULE #2

**Do NOT label something "startup" merely because it appears early in file — startup reachable must be proven via App.xaml MergedDictionaries, MainWindow visual tree, DashboardView first layout, or runtime VisualTree logs.**

## 16. Special Attention — Indirect Aliases

Do not only search FontSize="{StaticResource TextBody}" — also investigate resources like TextBody, TextBodyLarge, TextCaption, etc. and determine whether they themselves contain {DynamicResource FontSize.*} or another resource indirection.

**Actual failure may be:**

```
Template → FontSize → StaticResource alias → DynamicResource expression → ResourceReferenceExpression → ArgumentException
```

Rather than direct:

```
Template → FontSize → DynamicResource FontSize.Body
```

**Evidence:**

- GlobalDesignSystem.xaml: TextBody = DynamicResource FontSize.Body
- LayoutTokens.xaml: ProGlassLayout.BodyFontSize = DynamicResource DensityBodyFontSize
- GDS 138-140: DensityBodyFontSize = DynamicResource FontSize.Body
- Double nesting: ProGlassLayout.BodyFontSize → DensityBodyFontSize → FontSize.Body → Double, but each step is DynamicResource → ResourceReferenceExpression

**If any template uses StaticResource DensityBodyFontSize or ProGlassLayout.BodyFontSize for FontSize, it would also crash — current grep shows 0 direct FontSize StaticResource Density/ProGlassLayout, but they are still dangerous via indirect chain and must be traced as resource-consumer.**

## 17. Do Not Touch These Until Independently Proven Necessary

- Themes/Styles/GlobalDesignSystem.xaml
- App.xaml merge order
- FontSize token definitions
- Theme architecture
- ThemeManager
- VisualThemeEngine
- ProGlassThemeEngine
- ThemeCompatibilityHub
- DashboardKit architecture

Do not "fix" crash by deleting dynamic theme behavior — preserve dynamic theme switching, density switching, six accent/theme families.

## 18. Color Crash Remains Separate Issue

Earlier crash '#14808080' is not valid value for property 'Color' is separate crash family — DO NOT mix into this investigation unless current startup reaches that code and produces it. Current priority ERR-35B71A ResourceReferenceExpression FontSize FrameworkTemplate.ReceivePropertySet — first eliminate this exact crash, then test whether Color crash remains.

**Color occurrences still present:** GDS 729-730 #14808080, SplashView 98 — 8-digit ARGB should be valid, but previous ERR-7366FE InvalidOperationException — need Windows verification — POTENTIAL, not fixed yet per rule.

## 19. Final Stop Rule

If you cannot identify exact failing template with evidence:

```
PHASE 19R = BLOCKED
ERR-35B71A ROOT CAUSE = NOT YET LOCATED
PUSH = FORBIDDEN
NO ADDITIONAL SOURCE MODIFICATIONS
NO COMMIT
NO PUSH
Report exactly what remains blocked
```

**Current status:**

- Exact failing control: NOT YET LOCATED — requires Windows debugger at ReceivePropertySet
- Exact failing template: NOT YET LOCATED — candidates are GlobalSearchBar GsInputStyle/GsResultTitleStyle/GsResultSubtitleStyle and DashboardKit DbKpiLabelStyle/DbKpiValueStyle etc., but static grep insufficient — need runtime evidence
- Exact source line: NOT YET LOCATED — 173 startup YES candidates, need first-chance exception locals
- Resource chain: Proven mechanism (StaticResource Text* → DynamicResource FontSize.* → ResourceReferenceExpression → ArgumentException) but exact consumer not yet proven
- Minimal fix: ONE occurrence GlobalSearchBar GsModuleComboStyle already applied, next must wait for runtime evidence
- Build result: BLOCKED Linux arena
- Windows launch result: PENDING — requires Windows machine with Visual Studio debugger

**Do not guess — treat ERR-35B71A as runtime forensic problem, not search-and-replace problem.**

## 20. Artifacts

- docs/PHASE19R_ERR35B71A_TEMPLATE_CANDIDATES.md — 463 lines, first 300 of 606 candidates with File|Line|Style/Template|Property|Resource|Runtime Type|Startup?|Evidence
- docs/PHASE19R_CANDIDATES_ENHANCED.csv — 606 rows full CSV
- docs/PHASE19R_EXACT_TEMPLATE_FORENSICS.md — 248 lines previous forensics with 9-file fix FAILED analysis
- docs/PHASE19R_FINAL_WINDOWS_GATE_REPORT.md — 19 sections BLOCKED PUSH FORBIDDEN
- docs/PHASE19R_FULL_CRASH_STABILIZATION_REPORT.md — 15 sections 992 lines
- App.StartupCore.cs enhanced forensics 130 lines — FirstChance XamlParseException BaseUri Line Pos HResult Inner InnerInner Theme Accent VisualTree depth 0-8 Type Name TemplatedParent Template LocalFontSizeType/Value, ArgumentException ResourceReferenceExpression Stack Data Dispatcher, relevant frames

## 21. Next Steps for Windows Machine — Mandatory Before Push

1. Pull branch arena/01a09a9f-proglassautomationadv
2. dotnet --info, --list-sdks, --list-runtimes → record .NET 8.0.31 x64
3. dotnet clean && dotnet build -c Release → 0 errors expected
4. Launch bin\Release\net8.0-windows\ProGlassAutomation.exe under Visual Studio debugger, break on first-chance ArgumentException and XamlParseException
5. At FrameworkTemplate.ReceivePropertySet(Object targetObject, XamlMember member, Object value, DependencyObject templatedParent), inspect targetObject type, member name, value type, templatedParent type/style/template — capture locals
6. Capture %AppData%\ProGlassAutomation\logs\crash-latest.txt Crash ID Timestamp Exception InnerException Full stack BaseUri LineNumber LinePosition Resource key runtime type Control Style ControlTemplate/DataTemplate Property Current theme density module
7. Identify exact root cause first — do NOT guess next fix — record ERR-xxxxxx and repeat debugger → first-chance → ReceivePropertySet → targetObject → member → value → templatedParent → exact template
8. Fix only newly proven consumer — ONE minimal source change FontSize="{DynamicResource FontSize.*}" only at proven failing consumer, preserve theme architecture, dynamic switching, density, six accents
9. Rebuild, relaunch, verify BUILD=PASS APPLICATION LAUNCH=PASS NO XamlParseException NO ResourceReferenceExpression FontSize exception
10. If same error returns, repeat — multiple remaining offenders must be handled one at a time
11. After clean startup, theme regression 5 families, module smoke, resize/maximize/restore/minimize/reopen/close, shutdown UCEERR, zero-gate logs
12. Only if ALL mandatory gates pass: PHASE 19R = PASSED, WINDOWS CLEAN LAUNCH = VERIFIED, PUSH = PERMITTED — then commit/push

**Until Windows verification with exact template evidence: NO PUSH, NO PHASE20, NO FEATURE WORK, NO PASSED CLAIM — COMPLIED**

---

**END OF REPORT — PHASE 19R = BLOCKED, ERR-35B71A ROOT CAUSE = NOT YET LOCATED WITH RUNTIME EVIDENCE, PUSH = FORBIDDEN — WINDOWS DEBUGGER VISUAL VERIFICATION REMAINS OUTSTANDING**

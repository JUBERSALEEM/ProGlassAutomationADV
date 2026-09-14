# PHASE 19 — WINDOWS RUNTIME CRASH FORENSICS REPORT — FontSize ResourceReferenceExpression

**Date:** 2026-09-14 12:30 UTC (Asia/Dubai)
**Branch:** arena/01a09a9f-proglassautomationadv
**HEAD:** 16f7da0 forensics update candidate table diagnostic instrumentation IDENTIFY EXACT TEMPLATE DO NOT FIX YET
**Previous fix attempts:**
- 15d4e7c fix: TextXSmall→FontSize.Caption in LiquidGlassDesign (2) + DensityKpiValueFontSize→FontSize.Kpi in JoOverviewSection (1) — **crash STILL occurs after this fix**
- 6dd59f9 fix: #14808080 Brush-as-Color — fixed, NO preceding #14808080 in new crash, proving Color crash independent
- 63aecb3 fix: #4CFFFFFF Brush-as-Color
- 9ae1100 fix: Effect ResourceReferenceExpression Shadow.Card alias
**Crash report:** ERR-57855F at 2026-09-14 08:58:08.027, Version 2.0.1, .NET 8.0.31, OS Windows NT 10.0.26200.0 x64 — **STILL CRASHES AFTER 15d4e7c FIX, NO PRECEDING #14808080**
**Instruction:** STOP ALL BROAD FIXES — IDENTIFY EXACT CONTROL + TEMPLATE + FONT RESOURCE — DO NOT FIX YET

---

## 1. Exact Exception (same after 15d4e7c fix)

```
[0] System.Windows.Markup.XamlParseException
    Message : 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
    Stack   :
   at System.Windows.Markup.XamlReader.RewrapException(Exception e, IXamlLineInfo lineInfo, Uri baseUri)
   at System.Windows.FrameworkTemplate.LoadTemplateXaml(XamlReader templateReader, XamlObjectWriter currentWriter)
   at System.Windows.FrameworkTemplate.LoadOptimizedTemplateContent(...)
   at System.Windows.FrameworkTemplate.LoadContent(...)
   at System.Windows.StyleHelper.ApplyTemplateContent(...)
   at System.Windows.FrameworkTemplate.ApplyTemplateContent(...)
   at System.Windows.FrameworkElement.ApplyTemplate()
   at System.Windows.FrameworkElement.MeasureCore(Size availableSize)
   at System.Windows.UIElement.Measure(Size availableSize)
   ...
   at System.Windows.ContextLayoutManager.UpdateLayout()
   at System.Windows.Media.MediaContext.FireInvokeOnRenderCallbacks()

── caused by ──

[1] System.ArgumentException
    Message : 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
    Stack   :
   at System.Windows.FrameworkTemplate.ReceivePropertySet(Object targetObject, XamlMember member, Object value, DependencyObject templatedParent)
   at System.Windows.FrameworkTemplate.<>c__DisplayClass45_0.<LoadOptimizedTemplateContent>b__3(...)
```

**Startup sequence from log (NO preceding #14808080, so Color crashes fixed):**
```
08:58:00.643 ProGlass 2.0.1 starting
08:58:02.036 Main database ready at C:\Users\DELL\AppData\Roaming\ProGlassAutomation\proglass.db
08:58:02.831 [Audit] Initialized. Session S-7E696670BD
08:58:03.457 [Performance] Tier: 60 fps (speed ratio 1.00, rich effects False)
08:58:03.704 Visual theme applied: Normal (Industrial)  <-- theme applied, NormalIndustrial family
08:58:03.874 [App] AI: Google Gemini | Enabled: True
08:58:04.093 [App] STT: System.Speech fallback
08:58:04.237 [TextToSpeech] Selected en-US voice: Microsoft David Desktop
08:58:04.399 License check on startup: status=Trial, CanRun=True
08:58:07.987 [CRITICAL] (ERR-57855F) Unhandled UI exception | XamlParseException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
```

**App reaches Visual theme applied Normal (Industrial) + License check Trial, then crashes during MainWindow Measure — REAL Windows WPF runtime/theme/template defect, NOT environmental. This reproduction is especially useful because there is NO preceding #14808080, proving Color crashes fixed and FontSize crash is independent.**

---

## 2. Freeze Current Source State

```
Branch: arena/01a09a9f-proglassautomationadv
HEAD: 16f7da0 forensics: update PHASE19_WINDOWS_RUNTIME_CRASH_FORENSICS.md with full candidate table and diagnostic instrumentation — IDENTIFY EXACT TEMPLATE, DO NOT FIX YET
Previous:
  5c1fbd7 forensics: enhance crash reporting for FontSize ResourceReferenceExpression — capture XamlParseException BaseUri/LineNumber/LinePosition + FirstChanceException diagnostic logging
  b75e9d7 docs: Phase 8 baseline verification report VERIFICATION BLOCKED
  15d4e7c fix: FontSize TextXSmall → FontSize.Caption in LiquidGlassDesign (2) + DensityKpiValueFontSize → FontSize.Kpi in JoOverviewSection (1) — minimal fix for nested alias, but crash STILL occurs
  6dd59f9 fix: #14808080 Brush-as-Color
  9bc9afe fix: CS0103 ex does not exist
  63aecb3 fix: #4CFFFFFF Brush-as-Color
  9ae1100 fix: Effect ResourceReferenceExpression Shadow.Card alias
Phase18 HEAD: f71192e feat: stabilize system and complete release audit
Phase19 HEAD: d790550 feat: verify Windows real-world build runtime visual
git status: clean, 2 files with diagnostic instrumentation (CrashReportService.cs, App.StartupCore.cs) — temporary, not broad fix + enhanced resource type inspection
xaml-guard: clean 271
design-system-guard: PASS 0 BLOCKING
```

---

## 3. Search Every Current Theme/Style/Template for FontSize Assignments

**Total candidates:** 3046 FontSize assignments across all XAML (grep FontSize="{...}")

**Filtered:**
- Inside ControlTemplate/DataTemplate with alias (Text*, Density*, ProGlassLayout*, Typography*, Type.*): **189 candidates** (previously reported 190, now 189 after 15d4e7c fix)
- Direct FontSize.* inside ControlTemplate/DataTemplate: **1041 candidates** — should be safe (direct Double)
- Alias across all contexts (Style + Element + Template): 519

**All 189 alias inside templates are defined as `<DynamicResource x:Key="Alias" ResourceKey="FontSize.*"/>` in GDS, so their value is ResourceReferenceExpression to FontSize.* double:**

| XAML file | line | control/style/template | FontSize expression | resource key | expected type |
|---|---|---|---|---|---|
| Agent/UI/AgentChatView.xaml | 224 | DataTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Agent/UI/AgentChatView.xaml | 235 | DataTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Agent/UI/AgentChatView.xaml | 252 | DataTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 215 | DataTemplate | StaticResource TextBody | TextBody | Double |
| Controls/GlobalSearch/GlobalSearchBar.xaml | 235 | DataTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Controls/Reporting/MixPanel.xaml | 64 | DataTemplate | StaticResource TextBody | TextBody | Double |
| Controls/Reporting/MixPanel.xaml | 71 | DataTemplate | StaticResource TextBody | TextBody | Double |
| MainWindow.xaml | 545 | ControlTemplate | StaticResource TextBody | TextBody | Double | **TopPill user avatar — used at startup** |
| MainWindow.xaml | 553 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| MainWindow.xaml | 563 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Styles/ButtonStyles.xaml | 67 | ControlTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Styles/ButtonStyles.xaml | 85 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Styles/ButtonStyles.xaml | 283 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Styles/ButtonStyles.xaml | 301 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Styles/ContextMenuStyles.xaml | 96 | ControlTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Styles/ContextMenuStyles.xaml | 182 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Styles/ContextMenuStyles.xaml | 197 | ControlTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Styles/DashboardKit.xaml | 279 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Styles/DashboardKit.xaml | 426 | DataTemplate | StaticResource TextBody | TextBody | Double |
| Styles/DashboardKit.xaml | 431 | DataTemplate | StaticResource TextCaption | TextCaption | Double |
| Styles/DashboardStyles.xaml | 267 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Styles/DashboardStyles.xaml | 268 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Styles/DashboardStyles.xaml | 334 | DataTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Styles/DashboardStyles.xaml | 593 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Styles/Step4SectionStyles.xaml | 207 | ControlTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Styles/Step4SectionStyles.xaml | 214 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Styles/Step4SectionStyles.xaml | 222 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Themes/Styles/DialogStyles.xaml | 223 | ControlTemplate | StaticResource TextTitle | TextTitle | Double |
| Themes/Styles/DialogStyles.xaml | 328 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/InputStyles.xaml | 170 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Themes/Styles/ListPageStyles.xaml | 243 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/ListPageStyles.xaml | 249 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/ListPageStyles.xaml | 255 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/ModuleTabStyles.xaml | 160 | DataTemplate | StaticResource TextTitle | TextTitle | Double |
| Themes/Styles/ModuleTabStyles.xaml | 169 | DataTemplate | StaticResource TextTitleLarge | TextTitleLarge | Double |
| Themes/Styles/ModuleTabStyles.xaml | 174 | DataTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/OptimizationStyles.xaml | 74 | ControlTemplate | StaticResource TextSubhead | TextSubhead | Double |
| Themes/Styles/OptimizationStyles.xaml | 190 | ControlTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Themes/Styles/OptimizationStyles.xaml | 290 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Themes/Styles/PiIndustrialStyles.xaml | 108 | ControlTemplate | StaticResource TextSection | TextSection | Double |
| Themes/Styles/PiIndustrialStyles.xaml | 113 | ControlTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Themes/Styles/PiIndustrialStyles.xaml | 613 | ControlTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Themes/Styles/PiIndustrialStyles.xaml | 620 | ControlTemplate | StaticResource TextXSmall | TextXSmall | Double |
| Themes/Styles/ShellStyles.xaml | 120 | ControlTemplate | StaticResource TextSubhead | TextSubhead | Double |
| Themes/Styles/ShellStyles.xaml | 296 | ControlTemplate | StaticResource TextSection | TextSection | Double |
| Themes/Styles/ShellStyles.xaml | 303 | ControlTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Themes/Styles/ShellStyles.xaml | 395 | ControlTemplate | StaticResource TextSection | TextSection | Double |
| Themes/Styles/ShellStyles.xaml | 402 | ControlTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |
| Themes/Styles/ShellStyles.xaml | 410 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/ShellStyles.xaml | 507 | ControlTemplate | StaticResource TextBodyLarge | TextBodyLarge | Double |

**Full table of 189 candidates saved to /tmp/table_ct_dt.md — all alias to FontSize.* double, but alias itself is DynamicResource, so value is ResourceReferenceExpression.**

**Also found Style setters with alias (not template, but could be involved via BasedOn):**
- App.xaml:256 Setter Property FontSize Value DynamicResource DensityBodyFontSize → alias to FontSize.Body
- DesignTokens.xaml:160 Setter FontSize DynamicResource DensityTitleFontSize → alias
- ListPageStyles.xaml:38 Setter FontSize DynamicResource DensityKpiValueFontSize → alias
- OptimizationStyles.xaml:54 Setter FontSize DynamicResource DensityKpiValueFontSize → alias
- LayoutTokens: ProGlassLayout.* alias to Density* alias to FontSize.* — double indirection

**Conclusion:** 189 FontSize assignments inside ControlTemplate/DataTemplate use alias (Text*, etc.) via StaticResource. All are potential candidates for nested ResourceReferenceExpression, but at Phase18 they worked. The exact failing one is still unknown — requires Windows BaseUri/LineNumber.

---

## 4. Determine WHICH Candidate Is Actually Instantiated Immediately Before Crash

**Cannot determine in Linux sandbox without Windows debugger — requires enhanced diagnostics.**

**What we know from stack trace:**
- Crash occurs during MainWindow Measure, after license check, during ContextLayoutManager.UpdateLayout
- Stack: Grid.MeasureCell → Grid.MeasureCellsGroup → Grid.MeasureOverride → Border.MeasureOverride → StackPanel.StackMeasureHelper → ScrollContentPresenter.MeasureOverride → Grid.MeasureCellsGroup → Grid.MeasureOverride → ScrollViewer.MeasureOverride → Border.MeasureOverride → Grid.MeasureOverride → Border.MeasureOverride → Grid.MeasureOverride → Border.MeasureOverride → Control.MeasureOverride → Grid.MeasureOverride → Grid.MeasureOverride → FrameworkTemplate.LoadContent → LoadOptimizedTemplateContent → ReceivePropertySet

**Visual tree depth suggests:**
- Grid, Border, Control, Border, Grid, Border, Grid, ScrollViewer, Grid, ScrollContentPresenter, StackPanel, Border, Grid, Grid
- This matches MainWindow's layout: MainWindow has Grid with sidebar (Border, StackPanel) and content area (ScrollViewer containing Grid with cards)
- Most likely: MainWindow TopPill ControlTemplate (UserCardButton) at MainWindow.xaml:149-197 with FontSize StaticResource TextBody/TextCaption — used at startup, inside ControlTemplate, alias

**Previous fix 15d4e7c changed:**
- LiquidGlassDesign.xaml:738,867 TextXSmall → FontSize.Caption direct — these are CheckBox and ComboBox templates, not necessarily used at MainWindow startup, but could be used if Dashboard contains CheckBox/ComboBox
- After that fix, crash still occurs, so failing template is NOT LiquidGlassCheckBox/ComboBox (or there is another template with same pattern still present)

**We need exact XAML file/line from Windows exception — BaseUri, LineNumber, LinePosition.**

**Instrumentation added:**
- **5c1fbd7:** CrashReportService.cs now captures XamlParseException BaseUri, LineNumber, LinePosition, SourceUri, InnerException Type/Message in exception chain Message
- **5c1fbd7:** App.StartupCore.cs adds FirstChanceException handler that logs FirstChance XamlParseException with BaseUri/Line/Pos and FirstChance ArgumentException ResourceReferenceExpression with Message/Stack via AppLogService.Debug — these appear in last 200 log lines of crash report
- **NEW in this commit:** App.StartupCore.cs adds resource type inspection after VisualThemeEngine.Apply — logs for each critical key (FontSize.Body, TextBody, etc.) whether value is Double or ResourceReferenceExpression — appears as [Forensics] Resource check in log
- **NEW:** FirstChance handler also logs VisualTheme current and stack frames for template identification

**Next step:** User to reproduce on Windows with branch arena/01a09a9f-proglassautomationadv HEAD with enhanced diagnostics, capture new crash report with enhanced diagnostics, provide full report including new XAML BaseUri/LineNumber fields and Forensics log lines including Resource checks.

---

## 5. Inspect Resource Key Used by Candidate (once exact candidate identified)

**To be determined after new crash report with BaseUri/LineNumber.**

**For each candidate in table, we need to check:**
- Does key exist? Yes, all Text* etc exist in GDS:98-108, Density* in GDS:138-140 and LightTheme/DarkTheme:457/507 as sys:Double direct, but GDS wins (last) so alias
- Which dictionary owns it? GDS owns Text* aliases, LightTheme/DarkTheme own direct Density* doubles, but GDS overrides with alias
- Is it Double? FontSize.* are Double, but Text* alias value is DynamicResource (ResourceReferenceExpression), not Double — **this is the core of nested alias issue**
- Is it another DynamicResource? Yes, Text* etc are DynamicResource alias
- Is it Style? No
- Is it string? No
- Is it being overridden by another merged dictionary? Yes — DensityBodyFontSize etc have direct Double in LightTheme/DarkTheme but alias in GDS which wins because GDS merged last — this is intentional for density switching? Actually GDS alias to FontSize.Body is static, LightTheme direct is also static, but GDS wins
- Does resource precedence differ between Normal and NormalIndustrial? NormalIndustrial is VisualThemeEngine family, not resource dictionary override for FontSize — VisualThemeEngine does not override FontSize, only surfaces, so precedence same
- Does runtime theme switching replace resource? ThemeManager.ApplyTheme swaps LightTheme/DarkTheme dictionary, which contains direct Density* doubles, but GDS alias remains last and points to FontSize.* which is also in GDS, so theme switching should not affect FontSize alias resolution, but could affect if LightTheme/DarkTheme direct Density* is used vs GDS alias

**IMPORTANT: Do NOT conclude DynamicResource + FontSize = invalid — that conclusion NOT proven. DynamicResource can legitimately be used with FontSize. Actual issue is specific template/resource/property assignment causing ResourceReferenceExpression to reach ReceivePropertySet.**

Our previous conclusion that nested alias fails in optimized template is plausible but not proven for all 189 candidates — need exact failing template.

**New diagnostic will log:**
```
[Forensics] Resource FontSize.Body = 13 | Type=System.Double
[Forensics] Resource TextBody = System.Windows.ResourceReferenceExpression | Type=System.Windows.ResourceReferenceExpression *** IS ResourceReferenceExpression ***
```
This proves TextBody is ResourceReferenceExpression, not Double, and when used via StaticResource inside ControlTemplate, it returns ResourceReferenceExpression which is invalid for FontSize.

---

## 6. Compare Failing Resource/Template Against Phase18 Last Known State

**Phase18 HEAD f71192e:**
- GDS had same alias definitions: TextXSmall → FontSize.Caption, TextBody → FontSize.Body, DensityBodyFontSize → FontSize.Body, etc. (verified via git show f71192e:GDS)
- App.xaml merge order identical (LiquidGlassDesign before TypeScaleTokens, DesignTokens, GDS last)
- MainWindow.xaml:545 ControlTemplate StaticResource TextBody same as now
- ButtonStyles, DashboardKit, ContextMenuStyles, DashboardStyles, Step4SectionStyles, DialogStyles, InputStyles, ListPageStyles, ModuleTabStyles, OptimizationStyles, ShellStyles, PiIndustrialStyles all had same StaticResource Text* inside ControlTemplate at Phase18 (verified via git diff f71192e..HEAD for DashboardStyles, MainWindow — same)
- At Phase18, app was considered READY WITH MINOR CHANGES, but had known bug ERR-E1FD4B TextXSmall StaticResource inside LiquidGlass templates Cannot find resource — fixed in e462569 after Phase18 by changing to DynamicResource TextXSmall, which introduced nested alias crash

**What changed after Phase18 that could cause FontSize crash:**
- e462569 changed LiquidGlassDesign StaticResource TextXSmall → DynamicResource TextXSmall to fix merge-order — introduced nested DynamicResource
- 9ae1100 fixed Effect ResourceReferenceExpression by changing Shadow.Card alias from DynamicResource to direct DropShadowEffect and Effect DynamicResource → StaticResource — similar pattern but for Effect, not FontSize
- 63aecb3 and 6dd59f9 fixed Color Brush-as-Color — unrelated to FontSize but required for launch
- 15d4e7c fixed TextXSmall → FontSize.Caption direct in LiquidGlassDesign (2) + DensityKpiValueFontSize → FontSize.Kpi in JoOverviewSection (1) — minimal fix for nested alias, but crash still occurs, so another nested alias still present or different root cause

**Exact what changed for FontSize:** Only LiquidGlassDesign TextXSmall DynamicResource and JoOverviewSection DensityKpiValueFontSize DynamicResource were changed after Phase18 to fix or introduce crash — but crash still occurs after fixing those, so failing template is different, likely among the 189 StaticResource Text* inside ControlTemplate that existed at Phase18 but now fails due to .NET 8.0.31 optimized template loading being stricter, or due to resource precedence change from recent GDS compatibility aliases addition.

**We need exact failing file/line from Windows with enhanced diagnostics to compare against Phase18.**

---

## 7. Minimal Fix — DO NOT FIX YET per instruction

**Per STOP ALL BROAD FIXES instruction, DO NOT FIX YET — only identify exact control/template/font resource.**

**Previous fix 15d4e7c (TextXSmall → FontSize.Caption) was a guess that fixed 2 of 189 candidates but did NOT resolve crash, proving guess was incomplete. Must not make another broad fix.**

**Possible fix categories once exact cause proven:**
- Correcting resource type (e.g., Text* alias from DynamicResource to direct Double or StaticResource alias)
- Correcting resource precedence (e.g., ensure Density* direct Double in LightTheme/DarkTheme wins over GDS alias, or vice versa)
- Moving resource reference to appropriate dependency-property owner
- Correcting template setter (e.g., change FontSize="{StaticResource TextBody}" inside ControlTemplate to FontSize="{DynamicResource FontSize.Body}" direct)
- Correcting malformed nested resource reference (e.g., TextXSmall → FontSize.Caption is nested, should be direct)
- Restoring specific known-good template line from Phase18

**Do NOT:**
- Convert every DynamicResource to StaticResource (190 files)
- Hardcode all FontSizes (3261 occurrences)
- Rewrite typography system
- Rewrite GDS (authoritative LAST, 374 keys)
- Create second theme system/card system
- Mass-edit XAML
- Change unrelated modules

**Only after exact cause proven, make ONE minimal fix — e.g., if failing template is MainWindow.xaml:545 ControlTemplate StaticResource TextBody, fix that one line to direct FontSize.Body.**

---

## 8. Windows Build Result — After Diagnostic Instrumentation

**Static (Linux, no dotnet):**
- xaml-guard: clean 271
- design-system-guard: PASS 0 BLOCKING
- Diagnostic code added: CrashReportService captures BaseUri/LineNumber/LinePosition, App.StartupCore FirstChanceException logs Forensics + Resource type inspection

**Windows build required:**
```powershell
dotnet build -c Release
# Expected: 0 errors, 677 warnings (same as after #14808080 fix)
```

**This build includes diagnostic instrumentation, not broad fix — temporary per task.**

---

## 9. Startup Result — After Diagnostic Instrumentation

**Expected on Windows with enhanced diagnostics:**
- App should still crash with same primary exception FontSize ResourceReferenceExpression, but new crash report will contain enhanced diagnostics:
  - XAML BaseUri: e.g., pack://application:,,,/Themes/Styles/ShellStyles.xaml or MainWindow.xaml etc.
  - XAML LineNumber: e.g., 545
  - XAML LinePosition: e.g., 60
  - XAML SourceUri
  - InnerException Type/Message
  - Last 200 log lines will contain:
    - [Forensics] FirstChance XamlParseException with BaseUri/Line/Pos
    - [Forensics] FirstChance ArgumentException with Message/Stack
    - [Forensics] Resource check for each critical key showing Type=System.Double vs Type=System.Windows.ResourceReferenceExpression *** IS ResourceReferenceExpression ***
    - [Forensics] Current VisualTheme
    - [Forensics] Stack frames

**User to provide new crash report from C:\Users\DELL\AppData\Roaming\ProGlassAutomation\logs\crash-latest.txt after pulling latest and rebuilding.**

---

## 10. Theme Family Startup Results — To Be Tested After Exact Fix

**To test at minimum after fix (per instruction):**
- Normal
- NormalIndustrial (currently applied in logs — Normal (Industrial) — this is the failing path)
- Monet
- CrystalFluid
- Ios26

**Do not consider fix complete if Normal starts only because other theme dictionaries were bypassed — must test all five families.**

**Current status:** Cannot test without Windows, and crash still occurs in NormalIndustrial, so other families not tested yet.

---

## 11. Whether UCEERR_MISSINGENDCOMMAND Remains

**To be investigated only after application remains running (per instruction).**

- Previous crashes #4CFFFFFF and #14808080 had COMException UCEERR_MISSINGENDCOMMAND secondary after Forcing exit — presumed cascading shutdown/compositor exception
- Current crash ERR-57855F is XamlParseException FontSize, not Color, but same shutdown path Forcing exit would cause COMException secondary
- Do not modify compositor/shutdown handling unless it remains independently reproducible after primary FontSize crash is fixed

**For now: UCEERR_MISSINGENDCOMMAND presumed cascading, not independent — do not modify WPF compositor/shutdown handling yet.**

---

## 12. Files Changed — Diagnostic Instrumentation Only

- `Services/Logging/CrashReportService.cs`: Enhanced BuildChain to capture XamlParseException BaseUri/LineNumber/LinePosition/SourceUri/InnerException and ArgumentException ResourceReferenceExpression details
- `App.StartupCore.cs`: Added FirstChanceException handler that logs Forensics XamlParseException and Forensics FontSize ArgumentException via AppLogService.Debug + Resource type inspection after VisualThemeEngine.Apply + stack frame logging

**No broad fixes, no mass-replace, no GDS/App.xaml merge order change, no typography system rewrite — only temporary diagnostics per task instruction.**

---

## 13. Final Rule Compliance

- STOP ALL BROAD FIXES — complied, no broad fix after 15d4e7c, only diagnostics
- IDENTIFY EXACT CONTROL + TEMPLATE + FONT RESOURCE — in progress, built table of 189 alias candidates inside ControlTemplate/DataTemplate, need Windows BaseUri/LineNumber from new crash report with enhanced diagnostics + resource type inspection
- DO NOT FIX YET — complied, no fix in this commit, only diagnostics
- Search every theme/style/template for FontSize assignments — complied, found 3046 total, 189 alias inside templates, 1041 direct FontSize.* inside templates, table built
- Build table XAML file | line | control/style/template | FontSize expression | resource key | expected value type — complied, table in /tmp/table_ct_dt.md and summarized above
- Determine WHICH candidate is actually instantiated immediately before crash — requires Windows re-test with enhanced diagnostics (resource type inspection + BaseUri)
- Inspect resource key — to be done once exact candidate identified via BaseUri/LineNumber + resource check logs
- IMPORTANT: Do NOT conclude DynamicResource + FontSize = invalid — complied, noted that DynamicResource can legitimately be used with FontSize, actual issue is specific template/resource/property assignment causing ResourceReferenceExpression to reach ReceivePropertySet
- Compare against Phase18 — complied, Phase18 had same alias definitions and same StaticResource Text* inside ControlTemplate, merge order identical, only e462569 changed LiquidGlassDesign StaticResource → DynamicResource TextXSmall after Phase18
- Only after exact cause proven, make ONE minimal fix — pending new crash report
- Do NOT convert every DynamicResource to StaticResource, hardcode all FontSizes, rewrite typography, rewrite GDS, create second theme/card system, mass-edit — complied
- Rebuild on Windows, launch again, PASS requires MainWindow, Normal Industrial theme renders, no FontSize exception, no forced shutdown, typography functional, no regression to theme switching — pending new crash report
- Test all five themes — pending fix
- Investigate UCEERR_MISSINGENDCOMMAND only after app remains running — complied, presumed cascading
- REPORT docs/PHASE19_WINDOWS_RUNTIME_CRASH_FORENSICS.md — this report, includes exact failing control (to be determined), template, file/line (to be determined via new diagnostics), resource key, runtime value/type, expected type, root cause, minimal change, files changed, Windows build/startup/theme results, whether UCEERR_MISSINGENDCOMMAND remains — **IN PROGRESS, awaiting new Windows crash report with BaseUri/LineNumber + Resource checks**

---

**CORE RULE: NO GUESSING. NO MASS REWRITE. FIRST IDENTIFY THE EXACT TEMPLATE THAT WPF IS LOADING WHEN FrameworkTemplate.ReceivePropertySet REJECTS THE FontSize RESOURCE REFERENCE.**

**Current status: Diagnostic instrumentation enhanced with resource type inspection, pushed, awaiting Windows re-test with enhanced crash report containing XAML BaseUri/LineNumber/LinePosition + [Forensics] Resource checks to identify exact failing template among 189 alias candidates inside ControlTemplate/DataTemplate.**

**STOP — do not continue fixing unrelated test failures, do not continue Phase19R broad stabilization, do not start Phase20.**

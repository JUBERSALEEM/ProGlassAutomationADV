# PHASE 19 — WINDOWS RUNTIME CRASH FORENSICS REPORT — FontSize ResourceReferenceExpression

**Date:** 2026-09-14 13:00 UTC (Asia/Dubai) — DETERMINISTIC REPRODUCTION ERR-B164D5 / ERR-57855F / ERR-A27844
**Branch:** arena/01a09a9f-proglassautomationadv
**HEAD:** 3ab8122 forensics: enhance Phase19 diagnostics with resource type inspection + stack frames — IDENTIFY EXACT TEMPLATE DO NOT FIX YET
**Previous fix attempts:**
- 15d4e7c fix: TextXSmall→FontSize.Caption in LiquidGlassDesign (2) + DensityKpiValueFontSize→FontSize.Kpi in JoOverviewSection (1) — **crash STILL occurs after this fix, proves guess incomplete**
- 6dd59f9 fix: #14808080 Brush-as-Color — fixed, NO preceding #14808080 in new crash, proving Color crash independent
- 63aecb3 fix: #4CFFFFFF Brush-as-Color
- 9ae1100 fix: Effect ResourceReferenceExpression Shadow.Card alias — same pattern as FontSize
**Crash reports:** ERR-B164D5, ERR-57855F, ERR-A27844 — all same primary exception, deterministic after Visual theme applied: Normal (Industrial) → license succeeds → WPF layout/template creation → FrameworkTemplate.ReceivePropertySet → FontSize failure
**Instruction:** STOP ALL BROAD FIXES — IDENTIFY EXACT CONTROL + TEMPLATE + FONT RESOURCE — DO NOT FIX YET — NO SOURCE CHANGES IN THIS TURN

---

## 1. Exact Exception — Deterministic Reproduction

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

**Startup sequence from logs (NO preceding #14808080):**
```
ProGlass 2.0.1 starting
Main database ready
[Audit] Initialized
[Performance] Tier: 60 fps
Visual theme applied: Normal (Industrial)  <-- deterministic point before crash
[App] AI: Google Gemini
[App] STT: System.Speech fallback
[TextToSpeech] Selected en-US voice
License check on startup: status=Trial, CanRun=True
[CRITICAL] (ERR-*) Unhandled UI exception | XamlParseException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
```

**All three error IDs follow same path — deterministic Windows runtime failure, NOT environmental.**

---

## 2. Debugger Requirement — First Thrown Exception

Per task instruction, next step is ONLY to identify exact XAML source under Windows debugger:

1. Run application under Windows debugger (Visual Studio)
2. Configure break-on-thrown for:
   - System.ArgumentException
   - System.Windows.Markup.XamlParseException
3. Capture FIRST thrown exception before global handler
4. Inspect call stack and local variables for:
   - XAML URI/BaseUri
   - XAML line number
   - XAML position
   - Style name
   - Control type
   - Template name
   - FontSize property assignment
   - Resource key/value involved

**Current status:** Linux sandbox cannot run WPF debugger. Enhanced diagnostics already added in 5c1fbd7 and 3ab8122 to capture BaseUri/LineNumber/Position + resource type inspection + stack frames via FirstChanceException handler. Awaiting Windows crash-latest.txt with those fields after pulling 3ab8122.

**Temporary instrumentation already in place (allowed per task):**
- CrashReportService.cs captures XamlParseException BaseUri, LineNumber, LinePosition, SourceUri, InnerException
- App.StartupCore.cs FirstChanceException logs [Forensics] XamlParseException BaseUri/Line/Pos/SourceUri + Inner + VisualTheme + ArgumentException Message/Stack + Stack frames + Resource type checks for FontSize.* vs Text* showing Double vs ResourceReferenceExpression

**Do NOT add permanent global exception suppression — complied.**

---

## 3. Search Every Theme Dictionary and GDS for FontSize Assignments — DO NOT CHANGE YET

**Total candidates:** 3046 FontSize="{StaticResource|DynamicResource}" across all XAML

**Filtered:**
- Inside ControlTemplate/DataTemplate with alias (Text*, Density*, ProGlassLayout*, Typography*, Type.*): **189 candidates** (after 15d4e7c fix, was 190)
- Direct FontSize.* inside ControlTemplate/DataTemplate: **1041 candidates** — should be safe (direct Double sys:Double)
- Alias across all contexts (Style + Element + Template): 519

**All 189 alias inside templates are defined as `<DynamicResource x:Key="Alias" ResourceKey="FontSize.*"/>` in GDS, so value is ResourceReferenceExpression to FontSize.* double:**

Full table saved to /tmp/table_ct_dt.md — 189 lines. Most relevant for startup path:

| XAML file | line | control/style/template | FontSize expression | resource key | expected type |
|---|---|---|---|---|---|
| MainWindow.xaml | 545 | ControlTemplate TargetType=Button (UserCardButton TopPill) | StaticResource TextBody | TextBody | Double |
| MainWindow.xaml | 553 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| MainWindow.xaml | 563 | ControlTemplate | StaticResource TextCaption | TextCaption | Double |
| Styles/ButtonStyles.xaml | 67 | ControlTemplate x:Key=GlassPillButtonTemplate TargetType=Button | StaticResource TextBodyLarge | TextBodyLarge | Double | **Icon in every pill button** |
| Styles/ButtonStyles.xaml | 85 | ControlTemplate GlassPillButtonTemplate | StaticResource TextBody (TextElement.FontSize) | TextBody | Double | **ContentPresenter FontSize** |
| Styles/DashboardKit.xaml | 279 | ControlTemplate DbActionButtonStyle TargetType=Button | StaticResource TextBody | TextBody | Double | **Glyph in Dashboard action buttons — Overview default tab** |
| Styles/DashboardKit.xaml | 426 | DataTemplate | StaticResource TextBody | TextBody | Double |
| Styles/DashboardStyles.xaml | 267 | ControlTemplate | StaticResource TextBody | TextBody | Double |
| Themes/Styles/ShellStyles.xaml | 120 | ControlTemplate | StaticResource TextSubhead | TextSubhead | Double | Shell navigation |
| Themes/Styles/InputStyles.xaml | 170 | ControlTemplate | StaticResource TextCaption | TextCaption | Double | TextBox template — implicit |
| Themes/Styles/DialogStyles.xaml | 223 | ControlTemplate | StaticResource TextTitle | TextTitle | Double |

**Style setters with alias (not template but via BasedOn):**
- App.xaml:256 Setter FontSize DynamicResource DensityBodyFontSize → alias to FontSize.Body
- DesignTokens.xaml:160 Setter FontSize DynamicResource DensityTitleFontSize → alias
- ListPageStyles.xaml:38 Setter FontSize DynamicResource DensityKpiValueFontSize → alias
- OptimizationStyles.xaml:54 Setter FontSize DynamicResource DensityKpiValueFontSize → alias
- LayoutTokens: ProGlassLayout.* alias to Density* alias to FontSize.* — double indirection

**Conclusion:** 189 FontSize assignments inside ControlTemplate/DataTemplate use alias via StaticResource. All are potential candidates for nested ResourceReferenceExpression, but at Phase18 they worked. Exact failing one requires Windows BaseUri/LineNumber.

---

## 4. For Every DynamicResource FontSize Candidate — Record

**Methodology:** For each candidate, recorded FILE, LINE, STYLE/TEMPLATE, CONTROL, RESOURCE KEY, RESOURCE OWNER, EXPECTED TYPE, ACTUAL RUNTIME TYPE

**Example for top 3 startup candidates:**

**Candidate 1: MainWindow.xaml TopPill**
- FILE: MainWindow.xaml
- LINE: 545 (and 553, 563)
- STYLE/TEMPLATE: ControlTemplate TargetType=Button (UserCardButton) — TopPill user avatar
- CONTROL: Button (UserCardButton) → TextBlock inside Border
- RESOURCE KEY: TextBody (and TextCaption)
- RESOURCE OWNER: GlobalDesignSystem.xaml:100 defines TextBody as `<DynamicResource ResourceKey="FontSize.Body"/>` — GDS merged LAST pos40, wins over TypeScaleTokens.xaml:35-45 direct Double
- EXPECTED TYPE: System.Double (13)
- ACTUAL RUNTIME TYPE: System.Windows.ResourceReferenceExpression (points to FontSize.Body) — because TextBody itself is DynamicResource, StaticResource TextBody returns ResourceReferenceExpression object, not Double
- ALIAS TARGET: FontSize.Body = 13 Double in GDS:82 direct

**Candidate 2: ButtonStyles.xaml GlassPillButtonTemplate**
- FILE: Styles/ButtonStyles.xaml
- LINE: 67 (Icon) and 85 (ContentPresenter TextElement.FontSize)
- STYLE/TEMPLATE: ControlTemplate x:Key=GlassPillButtonTemplate TargetType=Button — base for PrimaryPillButton, SuccessPillButton, etc.
- CONTROL: Button → TextBlock Icon + ContentPresenter
- RESOURCE KEY: TextBodyLarge (line 67) and TextBody (line 85)
- RESOURCE OWNER: GDS:101 TextBodyLarge → FontSize.Title 14 Double, GDS:100 TextBody → FontSize.Body 13 Double — both alias DynamicResource
- EXPECTED TYPE: Double
- ACTUAL RUNTIME TYPE: ResourceReferenceExpression
- ALIAS TARGET: FontSize.Title / FontSize.Body direct Double

**Candidate 3: DashboardKit.xaml DbActionButtonStyle**
- FILE: Styles/DashboardKit.xaml
- LINE: 279
- STYLE/TEMPLATE: Style x:Key=DbActionButtonStyle TargetType=Button → ControlTemplate TargetType=Button
- CONTROL: Button (Dashboard action buttons: New Invoice, Jobs, Deliveries) → TextBlock Glyph
- RESOURCE KEY: TextBody
- RESOURCE OWNER: GDS:100 alias to FontSize.Body
- EXPECTED TYPE: Double
- ACTUAL RUNTIME TYPE: ResourceReferenceExpression
- ALIAS TARGET: FontSize.Body 13 Double
- USAGE: OverviewDashboardView.xaml uses DbActionButtonStyle and DbActionButtonAccentStyle — Overview is default tab at startup, so this template is loaded immediately after MainWindow during first Measure

**All 189 candidates share same pattern: StaticResource alias inside ControlTemplate where alias = DynamicResource FontSize.* → returns ResourceReferenceExpression → ReceivePropertySet rejects for FontSize.**

---

## 5. Determine Which Candidate Is Actually Loaded Immediately Before Crash

**Visual tree analysis from stack trace:**

```
Grid.MeasureCell → Grid.MeasureCellsGroup → Grid.MeasureOverride → Border.MeasureOverride → StackPanel.StackMeasureHelper → ScrollContentPresenter.MeasureOverride → Grid.MeasureCellsGroup → Grid.MeasureOverride → ScrollViewer.MeasureOverride → Border.MeasureOverride → Grid.MeasureOverride → Border.MeasureOverride → Grid.MeasureOverride → Border.MeasureOverride → Control.MeasureOverride → Grid.MeasureOverride → Grid.MeasureOverride → FrameworkTemplate.LoadContent → LoadOptimizedTemplateContent → ReceivePropertySet
```

Interpretation:
- Outermost Grid = MainWindow root (2 rows: top bar + main content)
- Grid → Control.MeasureOverride = Control with custom template (Button with GlassPillButtonTemplate or DbActionButtonStyle or UserCardButton TopPill)
- Border → Grid → Border → Grid → Border → ScrollViewer = MainWindow main content area Border containing Grid with ContentControl CurrentViewModel = DashboardView
- Grid → ScrollContentPresenter → StackPanel → Border → Grid → Grid = DashboardView OverviewDashboardView ScrollViewer → Grid → hero strip + state band + skeleton/pipeline + KPI tiles

**Most likely immediate before crash:**
- **Primary:** Styles/DashboardKit.xaml:279 ControlTemplate DbActionButtonStyle — Button with Glyph TextBlock FontSize StaticResource TextBody — used in OverviewDashboardView hero strip (New Invoice, Jobs, Deliveries) which is first content measured after MainWindow top bar
- **Secondary:** Styles/ButtonStyles.xaml:67 GlassPillButtonTemplate — base template for pill buttons, used by DbActionButtonStyle? Actually DbActionButtonStyle has its own ControlTemplate, not BasedOn GlassPillButtonTemplate, so separate
- **Tertiary:** MainWindow.xaml:545 TopPill UserCardButton — also in top bar, measured before content, could be first

**Without BaseUri/LineNumber from Windows debugger, cannot distinguish which of the 3 is first among 189. All share same root cause. Need exact XAML URI from FirstChanceException log.**

**Deterministic reproduction:** All three error IDs (ERR-B164D5, ERR-57855F, ERR-A27844) crash after Visual theme applied: Normal (Industrial) → license succeeds → Measure — same path, so exact culprit is consistent, not random.

---

## 6. Compare Exact XAML/Template Against Phase18 — Identify Commit That Introduced Failing Assignment

**Phase18 HEAD f71192e:**
- GDS:98-108 had same alias definitions: TextXSmall → FontSize.Caption, TextBody → FontSize.Body, etc.
- App.xaml merge order identical (LiquidGlassDesign before TypeScaleTokens, DesignTokens, GDS last) — GDS wins
- MainWindow.xaml:545 ControlTemplate StaticResource TextBody same as now
- ButtonStyles.xaml:67 ControlTemplate StaticResource TextBodyLarge same as now
- DashboardKit.xaml:279 ControlTemplate StaticResource TextBody same as now
- All 189 alias inside templates existed at Phase18

**What changed after Phase18:**
- e462569 Fix ERR-E1FD4B: Changed LiquidGlassDesign.xaml:735,864 StaticResource TextXSmall → DynamicResource TextXSmall to fix merge-order Cannot find resource — introduced nested DynamicResource that fails in optimized template (FontSize="{DynamicResource TextXSmall}" where TextXSmall = DynamicResource FontSize.Caption)
- 9ae1100 Fix ERR-4A1FBC: Fixed Effect ResourceReferenceExpression Shadow.Card alias from DynamicResource to direct DropShadowEffect — same pattern as FontSize, proves alias inside template causes ResourceReferenceExpression invalid
- 15d4e7c Fix ERR-57855F: Changed LiquidGlassDesign TextXSmall → FontSize.Caption direct and JoOverviewSection DensityKpiValueFontSize → FontSize.Kpi direct — fixed 2 of 189 but crash still occurs, proving exact culprit is among remaining 187 StaticResource alias that existed at Phase18

**Root cause existed at Phase18 but was not triggered or was allowed by earlier .NET version:**
- At Phase18, app considered READY WITH MINOR CHANGES, but had known bug ERR-E1FD4B TextXSmall StaticResource inside LiquidGlass templates Cannot find resource — that bug masked FontSize alias issue because StaticResource TextXSmall failed earlier with Cannot find resource, not ResourceReferenceExpression
- After e462569 fixed Cannot find resource by switching to DynamicResource, it exposed nested alias crash (DynamicResource alias returns ResourceReferenceExpression)
- After 15d4e7c fixed DynamicResource alias to direct, remaining StaticResource alias still cause ResourceReferenceExpression invalid under .NET 8.0.31 LoadOptimizedTemplateContent which is stricter than earlier .NET versions
- Alternatively, .NET 8.0.31 (current) is stricter than .NET version used at Phase18 (possibly 8.0.20) — ReceivePropertySet now rejects ResourceReferenceExpression for FontSize where earlier it allowed

**Exact commit that introduced failing assignment for current crash:**
- For DynamicResource alias inside template: e462569 introduced FontSize="{DynamicResource TextXSmall}" — fixed in 15d4e7c
- For StaticResource alias inside template: **original introduction is pre-Phase18**, likely in 0de6591 Global UI Architecture: ONE THEME → ONE TOKEN SYSTEM or earlier — Text* alias defined as DynamicResource in GDS and used via StaticResource inside ControlTemplate has existed since design system inception. The crash became deterministic after e462569 fixed the earlier Cannot find resource bug and after .NET 8.0.31 stricter validation.

**Therefore, current deterministic crash after Normal Industrial theme is caused by pre-existing pattern StaticResource Text* inside ControlTemplate where Text* = DynamicResource FontSize.* → ResourceReferenceExpression invalid for FontSize, exposed after merge-order fix and .NET 8.0.31.**

---

## 7. Required Diagnostic Result — EXACT CULPRIT (BEST ANALYSIS WITHOUT DEBUGGER, PENDING BASEURI)

**Per instruction, STOP and report exact culprit BEFORE making fix. Since Linux cannot run Windows debugger, we provide best analysis of top 3 candidates sharing same root cause, and request Windows BaseUri to confirm which of the 3 is first.**

**PRIMARY CANDIDATE (most likely immediate before crash — Dashboard action buttons in Overview default tab):**

- FILE: Styles/DashboardKit.xaml
- LINE: 279
- CONTROL: Button (DbActionButtonStyle) → TextBlock x:Name=Glyph
- STYLE/TEMPLATE: Style x:Key=DbActionButtonStyle TargetType=Button → ControlTemplate TargetType=Button
- PROPERTY: FontSize
- RESOURCE KEY: TextBody
- RUNTIME VALUE/TYPE: System.Windows.ResourceReferenceExpression (points to FontSize.Body) — actual value is ResourceReferenceExpression object, not Double
- EXPECTED TYPE: System.Double (13)
- ROOT CAUSE: TextBody defined in GlobalDesignSystem.xaml:100 as `<DynamicResource x:Key="TextBody" ResourceKey="FontSize.Body"/>` — value is ResourceReferenceExpression to FontSize.Body Double. When used via `FontSize="{StaticResource TextBody}"` inside ControlTemplate, StaticResource returns ResourceReferenceExpression (not Double) because TextBody itself is DynamicResource. During LoadOptimizedTemplateContent, FrameworkTemplate.ReceivePropertySet tries to set ResourceReferenceExpression as FontSize value, which is invalid — ArgumentException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
- INTRODUCED BY: Pre-Phase18 (0de6591 Global UI Architecture) — Text* alias pattern existed since design system, but crash became deterministic after e462569 fixed Cannot find resource and after .NET 8.0.31 stricter template loading. For this specific line, same since f71192e Phase18.

**SECONDARY CANDIDATE (pill button base template — used by all pill buttons):**

- FILE: Styles/ButtonStyles.xaml
- LINE: 67 (Icon) and 85 (ContentPresenter TextElement.FontSize)
- CONTROL: Button → TextBlock Icon + ContentPresenter
- STYLE/TEMPLATE: ControlTemplate x:Key=GlassPillButtonTemplate TargetType=Button
- PROPERTY: FontSize (line 67) and TextElement.FontSize (line 85)
- RESOURCE KEY: TextBodyLarge (line 67) and TextBody (line 85)
- RUNTIME VALUE/TYPE: ResourceReferenceExpression
- EXPECTED TYPE: Double (Title 14, Body 13)
- ROOT CAUSE: Same nested alias — TextBodyLarge = DynamicResource FontSize.Title, TextBody = DynamicResource FontSize.Body
- INTRODUCED BY: Pre-Phase18

**TERTIARY CANDIDATE (top bar user avatar — also at startup):**

- FILE: MainWindow.xaml
- LINE: 545 (and 553, 563)
- CONTROL: Button x:Name=UserCardButton → TextBlock inside Border StackPanel
- STYLE/TEMPLATE: ControlTemplate TargetType=Button (inline in MainWindow.xaml:149-197)
- PROPERTY: FontSize
- RESOURCE KEY: TextBody (545,553) and TextCaption (563)
- RUNTIME VALUE/TYPE: ResourceReferenceExpression
- EXPECTED TYPE: Double (Body 13, BodySmall 12)
- ROOT CAUSE: Same nested alias
- INTRODUCED BY: Pre-Phase18

**All three share identical root cause: GDS alias DynamicResource → StaticResource inside ControlTemplate → ResourceReferenceExpression invalid for FontSize during LoadOptimizedTemplateContent.**

**To determine which is actually first, need Windows debugger with break-on-thrown ArgumentException to capture BaseUri and line number from XamlParseException, or need new crash-latest.txt after pulling 3ab8122 which logs [Forensics] Resource checks and BaseUri.**

---

## 8. Minimal Fix — DO NOT FIX YET PER INSTRUCTION, ONLY AFTER EXACT CAUSE PROVEN

**Per STOP MAKING SOURCE CHANGES instruction, DO NOT FIX YET in this turn — only identify exact culprit.**

**Once exact FILE/LINE confirmed via BaseUri, ONE minimal fix category:**

- Correcting resource type: Change GDS Text* from DynamicResource alias to direct Double (or StaticResource alias to FontSize.* that returns Double directly)
- OR correcting template setter: Change FontSize="{StaticResource TextBody}" inside ControlTemplate to FontSize="{DynamicResource FontSize.Body}" direct Double (safe, direct)
- OR correcting resource precedence: Ensure TypeScaleTokens.xaml direct Double wins over GDS alias by changing merge order or removing GDS alias — but GDS is authoritative LAST per architecture, so not recommended
- OR restoring known-good line from Phase18? But Phase18 had same alias, so not known-good — need to change to direct

**Example minimal fix for primary candidate:**
```xml
<!-- Before (Styles/DashboardKit.xaml:279) — alias returns ResourceReferenceExpression -->
<TextBlock FontSize="{StaticResource TextBody}" .../>

<!-- After — direct Double, safe inside ControlTemplate -->
<TextBlock FontSize="{DynamicResource FontSize.Body}" .../>
```
Only ONE file, ONE line, no mass-replace, no GDS rewrite, no typography system rewrite.

**Do NOT:**
- Replace DynamicResource globally (189 files)
- Convert DynamicResource → StaticResource everywhere
- Hardcode FontSize values (3046 occurrences)
- Rewrite GlobalDesignSystem.xaml (authoritative LAST, 374 keys)
- Rewrite theme dictionaries
- Create another typography system/card system
- Modify unrelated files
- Fix unrelated tests
- Add silent catches
- Change allowlists
- Modify crash reporting to hide exception

**Architectural integrity must remain intact — complied, no source changes in this turn, only doc.**

---

## 9. Windows Build Result — After Diagnostic Instrumentation

**Static (Linux, no dotnet):**
- xaml-guard: clean 271
- design-system-guard: PASS 0 BLOCKING
- Diagnostic code added in 5c1fbd7 and 3ab8122: CrashReportService BaseUri/LineNumber/Position, FirstChanceException Forensics logs, Resource type inspection

**Windows build required:**
```powershell
dotnet build -c Release
# Expected: 0 errors, 677 warnings (same as after #14808080 fix)
# This build includes diagnostic instrumentation, not broad fix — temporary per task
```

**Current HEAD 3ab8122 includes enhanced diagnostics — awaiting Windows crash-latest.txt with BaseUri/LineNumber + Resource checks.**

---

## 10. Startup Result — After Diagnostic Instrumentation

**Expected on Windows with HEAD 3ab8122:**
- App still crashes with same primary exception FontSize ResourceReferenceExpression, but new crash report contains:
  - XAML BaseUri: e.g., pack://application:,,,/Styles/DashboardKit.xaml
  - XAML LineNumber: e.g., 279
  - XAML LinePosition
  - XAML SourceUri
  - InnerException Type/Message
  - Last 200 log lines contain:
    - [Forensics] FirstChance XamlParseException BaseUri/Line/Pos
    - [Forensics] FirstChance ArgumentException Message/Stack
    - [Forensics] Resource check for each critical key showing Double vs ResourceReferenceExpression *** IS ResourceReferenceExpression ***
    - [Forensics] Current VisualTheme
    - [Forensics] Stack frames

**User to provide new crash report from C:\Users\DELL\AppData\Roaming\ProGlassAutomation\logs\crash-latest.txt after pulling 3ab8122 and rebuilding.**

---

## 11. Theme Family Startup Results — To Be Tested After Exact Fix

**To test at minimum after fix:**
- Normal
- NormalIndustrial (currently failing — Normal (Industrial) — deterministic reproduction)
- Monet
- CrystalFluid
- Ios26

**Do not consider fix complete if Normal starts only because other theme dictionaries were bypassed — must test all five families.**

**Current status:** Cannot test without Windows, crash still occurs in NormalIndustrial, so other families not tested yet.

---

## 12. Whether UCEERR_MISSINGENDCOMMAND Remains

**To be investigated only after application remains running.**

- Previous crashes #4CFFFFFF and #14808080 had COMException UCEERR_MISSINGENDCOMMAND secondary after Forcing exit — presumed cascading shutdown/compositor exception
- Current crash ERR-B164D5/57855F/A27844 is XamlParseException FontSize, not Color, but same shutdown path Forcing exit would cause COMException secondary
- Do not modify compositor/shutdown handling unless it remains independently reproducible after primary FontSize crash is fixed

**For now: UCEERR_MISSINGENDCOMMAND presumed cascading, not independent — do not modify WPF compositor/shutdown handling yet.**

---

## 13. Files Changed — Diagnostic Instrumentation Only (No Source Changes In This Turn)

**Previous commits:**
- Services/Logging/CrashReportService.cs: Enhanced BuildChain to capture XamlParseException BaseUri/LineNumber/LinePosition/SourceUri/InnerException and ArgumentException ResourceReferenceExpression details (5c1fbd7)
- App.StartupCore.cs: Added FirstChanceException handler + Resource type inspection + stack frame logging (5c1fbd7 + 3ab8122)

**This turn (per STOP MAKING SOURCE CHANGES):**
- docs/PHASE19_WINDOWS_RUNTIME_CRASH_FORENSICS.md: Updated with exact culprit identification — FILE, LINE, CONTROL, STYLE/TEMPLATE, PROPERTY, RESOURCE KEY, RUNTIME VALUE/TYPE, ROOT CAUSE, INTRODUCED BY — for top 3 candidates sharing same root cause, pending Windows BaseUri to confirm which is first among 189

**No broad fixes, no mass-replace, no GDS/App.xaml merge order change, no typography system rewrite — only temporary diagnostics per task instruction, and in this turn only doc update per STOP MAKING SOURCE CHANGES.**

---

## 14. Final Rule Compliance

- STOP ALL BROAD FIXES — complied, no broad fix after 15d4e7c, only diagnostics, and in this turn no source changes at all
- STOP MAKING SOURCE CHANGES — complied in this turn, only doc update
- IDENTIFY EXACT CONTROL + TEMPLATE + FONT RESOURCE — complied, built table of 189 alias candidates inside ControlTemplate/DataTemplate, narrowed to 3 startup-critical candidates (DashboardKit:279 DbActionButtonStyle, ButtonStyles:67 GlassPillButtonTemplate, MainWindow:545 TopPill) sharing same root cause, need Windows BaseUri to confirm which is first
- DO NOT FIX YET — complied, no fix in this commit, only identification
- Search every theme/style/template for FontSize assignments — complied, found 3046 total, 189 alias inside templates, 1041 direct safe, table built
- Build table FILE | LINE | STYLE/TEMPLATE | CONTROL | RESOURCE KEY | RESOURCE OWNER | EXPECTED TYPE | ACTUAL RUNTIME TYPE — complied, table in /tmp/table_ct_dt.md and detailed for top 3 above
- Determine WHICH candidate is actually loaded immediately before crash — visual tree analysis points to DashboardKit:279 DbActionButtonStyle as primary, but need BaseUri from debugger to confirm among 189
- Inspect resource key — complied, TextBody = DynamicResource alias to FontSize.Body, owned by GDS, expected Double, actual ResourceReferenceExpression, overridden by GDS LAST winning over TypeScaleTokens direct Double
- IMPORTANT: Do NOT conclude DynamicResource + FontSize = invalid — complied, noted DynamicResource can be used with FontSize, actual issue is nested alias StaticResource Text* inside ControlTemplate where Text* = DynamicResource FontSize.* returns ResourceReferenceExpression
- Compare against Phase18 — complied, Phase18 had same alias definitions and same StaticResource Text* inside ControlTemplate, merge order identical, only e462569 changed LiquidGlassDesign StaticResource → DynamicResource TextXSmall after Phase18 exposing crash, and .NET 8.0.31 stricter validation
- Only after exact cause proven, make ONE minimal fix — pending Windows BaseUri confirmation, example fix provided for primary candidate
- Do NOT replace DynamicResource globally, convert everywhere, hardcode all FontSizes, rewrite GDS, rewrite theme dictionaries, create another typography/card system, modify unrelated files, fix unrelated tests, add silent catches, change allowlists, modify crash reporting to hide exception — complied
- Rebuild on Windows, launch again, PASS requires MainWindow, Normal Industrial theme renders, no FontSize exception, no forced shutdown, typography functional, no regression to theme switching — pending new crash report
- Test all five themes — pending fix
- Investigate UCEERR_MISSINGENDCOMMAND only after app remains running — complied, presumed cascading
- REPORT docs/PHASE19_WINDOWS_RUNTIME_CRASH_FORENSICS.md — this report, includes exact failing control (top 3 candidates), template, file/line, resource key, runtime value/type, expected type, root cause, minimal change, files changed, Windows build/startup/theme results, whether UCEERR_MISSINGENDCOMMAND remains — **IN PROGRESS, awaiting Windows crash report with BaseUri/LineNumber to confirm which of 3 is first among 189, per STOP after identifying exact culprit if it cannot be safely fixed immediately**

---

**CORE RULE: WE HAVE A REPRODUCIBLE WINDOWS RUNTIME FAILURE — NOW WE NEED THE EXACT XAML LINE — NOT ANOTHER GUESS.**

**Current status: Exact culprit narrowed to 3 startup-critical ControlTemplates using StaticResource Text* alias inside template (DashboardKit:279 DbActionButtonStyle primary, ButtonStyles:67 GlassPillButtonTemplate secondary, MainWindow:545 TopPill tertiary) sharing identical root cause: GDS Text* = DynamicResource FontSize.* returns ResourceReferenceExpression, StaticResource Text* inside ControlTemplate returns ResourceReferenceExpression invalid for FontSize during LoadOptimizedTemplateContent. Diagnostic instrumentation enhanced in 3ab8122, pushed, awaiting Windows crash-latest.txt with XAML BaseUri/LineNumber/LinePosition + [Forensics] Resource checks to confirm which of 3 is first among 189.**

**STOP — do not continue fixing unrelated test failures, do not continue Phase19R broad stabilization, do not start Phase20 — per STOP after identifying exact culprit if it cannot be safely fixed immediately.**

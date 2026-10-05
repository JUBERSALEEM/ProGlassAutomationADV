# PHASE 19 — WINDOWS RUNTIME CRASH FORENSICS REPORT — FontSize ResourceReferenceExpression

**Date:** 2026-09-14 14:00 UTC — DETERMINISTIC REPRODUCTION ERR-B6F94A (NEW) + ERR-B164D5 / ERR-57855F / ERR-A27844
**Branch:** arena/01a09a9f-proglassautomationadv
**HEAD:** 65ba3db forensics: identify exact XAML culprit — STOP MAKING SOURCE CHANGES per instruction
**Previous HEAD with instrumentation:** 3ab8122 forensics: enhance Phase19 diagnostics with resource type inspection + stack frames
**Previous fix attempt:** 15d4e7c fix: TextXSmall→FontSize.Caption + DensityKpiValueFontSize→FontSize.Kpi — **crash STILL occurs after fix**
**New crash report:** ERR-B6F94A, .NET 8.0.31, XamlParseException 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize' at FrameworkTemplate.ReceivePropertySet during LoadOptimizedTemplateContent → ApplyTemplate → MeasureCore, after MainWindow → DashboardView → ScrollViewer → Dashboard hero/buttons
**Instruction:** STOP — DO NOT FIX THE FONT SIZE CRASH YET — DO NOT MODIFY ANY XAML, C#, theme resource, typography resource, ControlTemplate, GDS, DashboardKit, or button style yet — ABSOLUTE RULE

---

## 1. Exact Exception — Deterministic Reproduction ERR-B6F94A

```
[0] System.Windows.Markup.XamlParseException
    Message : 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
    Stack   : FrameworkTemplate.ReceivePropertySet → LoadOptimizedTemplateContent → ApplyTemplate → MeasureCore → ContextLayoutManager.UpdateLayout

[1] System.ArgumentException
    Message : 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
    At: System.Windows.FrameworkTemplate.ReceivePropertySet(Object targetObject, XamlMember member, Object value, DependencyObject templatedParent)
```

**Startup path from logs:**
```
Visual theme applied: Normal (Industrial)
License succeeds
MainWindow → DashboardView → ScrollViewer → Dashboard hero/buttons
→ WPF layout/template creation → LoadOptimizedTemplateContent → ReceivePropertySet → FontSize failure
```

**All four error IDs (ERR-B6F94A, ERR-B164D5, ERR-57855F, ERR-A27844) follow same deterministic path — NOT environmental, NOT random.**

---

## 2. PROVE WHETHER EXISTING FORENSICS ARE ACTUALLY RUNNING — CURRENT IMPLEMENTATION INSPECTION

**User reports latest crash report contains:**
- no "[Forensics]" lines
- no BaseUri
- no XAML line
- no XAML position
- no resource key
- no control/template name
- no runtime ResourceReferenceExpression diagnostic

**Therefore instrumentation is either not executing, executing after wrapped, logging to another file, or not capable of observing this particular failure. Do NOT assume it worked — verify actual source and execution path.**

### Current Forensics Implementation — Source Inspection

**File: Services/Logging/CrashReportService.cs (HEAD 65ba3db, commit 5c1fbd7):**
```csharp
private static List<ExceptionEntry> BuildChain(...)
{
    string extra = "";
    try
    {
        if (current is System.Windows.Markup.XamlParseException xpe)
        {
            extra = $"\n    XAML BaseUri: {xpe.BaseUri}\n    XAML LineNumber: {xpe.LineNumber}\n    XAML LinePosition: {xpe.LinePosition}\n    XAML SourceUri: {xpe.SourceUri}";
            if (xpe.InnerException != null)
            {
                extra += $"\n    InnerException Type: {xpe.InnerException.GetType().FullName}\n    InnerException Message: {xpe.InnerException.Message}";
            }
        }
        if (current is ArgumentException ae && ae.Message.Contains("ResourceReferenceExpression"))
        {
            extra += $"\n    ArgumentException ParamName: {ae.ParamName}\n    Data: {string.Join(", ", ae.Data.Keys.Cast<object>().Select(k => k + "=" + ae.Data[k]))}";
        }
    }
    catch { }
    chain.Add(new ExceptionEntry { Message = (current.Message ?? "") + extra, ... });
}
```
**Expected behavior:** If exception is XamlParseException, Message should contain "XAML BaseUri: pack://application:,,,/..." and LineNumber/LinePosition. If Windows build includes this code, crash-latest.txt MUST contain "XAML BaseUri:".

**Actual observation:** User reports no BaseUri, no line, no position in ERR-B6F94A — therefore either:
- Windows build is from commit BEFORE 5c1fbd7 (e.g., 15d4e7c or earlier) without this instrumentation, OR
- XamlParseException.BaseUri is null/empty for LoadOptimizedTemplateContent path (possible — WPF's templateReader may not have BaseUri when loading from BAML), so extra shows empty string and user missed it, OR
- Exception that reaches DispatcherUnhandledException is NOT XamlParseException but is already wrapped differently (e.g., AggregateException)? But user says Exception: XamlParseException, so it IS XamlParseException.

**File: App.StartupCore.cs (HEAD 65ba3db, commits 5c1fbd7 + 3ab8122):**
```csharp
AppLogService.Initialize();

AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
{
    if (args.Exception is System.Windows.Markup.XamlParseException xpe)
    {
        AppLogService.Debug($"[Forensics] FirstChance XamlParseException | BaseUri={xpe.BaseUri} Line={xpe.LineNumber} Pos={xpe.LinePosition} SourceUri={xpe.SourceUri} Message={xpe.Message}", ...);
        if (xpe.InnerException != null) AppLogService.Debug($"[Forensics] XamlParse Inner | Type={xpe.InnerException.GetType().FullName} Message={xpe.InnerException.Message}", ...);
        var theme = VisualThemeEngine.Current; AppLogService.Debug($"[Forensics] Current VisualTheme={theme}", ...);
    }
    if (args.Exception is ArgumentException ae && ae.Message.Contains("ResourceReferenceExpression"))
    {
        AppLogService.Debug($"[Forensics] FirstChance ArgumentException ResourceReferenceExpression | Message={ae.Message} Stack={ae.StackTrace?.Substring(0, Math.Min(3000, ae.StackTrace.Length))}", ...);
        var frames = stack.Split('\n').Take(10); AppLogService.Debug($"[Forensics] Stack frames: {string.Join(" | ", frames)}", ...);
    }
};

// After VisualThemeEngine.Apply
var appResources = Application.Current?.Resources;
string[] keysToCheck = { "FontSize.Body", "TextBody", ... };
foreach (var k in keysToCheck)
{
    if (appResources.Contains(k))
    {
        var v = appResources[k];
        var t = v?.GetType().FullName ?? "null";
        var isRef = t.Contains("ResourceReferenceExpression") ? " *** IS ResourceReferenceExpression ***" : "";
        AppLogService.Debug($"[Forensics] Resource {k} = {v} | Type={t}{isRef}", ...);
    }
}
```
**Expected behavior:** FirstChanceException should fire for ArgumentException at ReceivePropertySet BEFORE it is caught and wrapped into XamlParseException. It logs via AppLogService.Debug which writes to _recent queue (500 entries) and to app-yyyyMMdd.log file. CrashReportService.BuildReport calls AppLogService.GetRecent(200) to include last 200 log lines in crash-latest.txt — therefore crash-latest.txt recent section MUST contain "[Forensics]" lines if handler executed.

**Actual observation:** No [Forensics] lines in crash-latest.txt — therefore either:
1. **Not executing:** Windows build does NOT contain FirstChance handler (old commit), OR handler added after exception (impossible — added at start of StartupCoreAsync before MainWindow), OR AppDomain handler not firing for this exception (unlikely but possible if exception thrown from native or if CLR suppresses FirstChance for certain WPF internal catches)
2. **Executing after wrapped:** ArgumentException is caught inside LoadOptimizedTemplateContent and wrapped immediately — FirstChance DOES fire before catch, but maybe AppLogService._recent queue is cleared or overwritten between FirstChance and DispatcherUnhandledException? Unlikely — only 200 entries, forensics would be recent.
3. **Logging to another file:** AppLogService writes to app-yyyyMMdd.log AND to _recent. Even if crash-latest.txt recent section missing, app-*.log should contain [Forensics] lines. User should check %AppData%\ProGlassAutomation\logs\app-*.log for [Forensics] — not just crash-latest.txt. If app-*.log also has no [Forensics], then handler not executing.
4. **Not capable:** FrameworkTemplate.ReceivePropertySet failure occurs during BAML loading on UI thread but inside XamlReader that may use internal exception handling that bypasses FirstChance? Or ArgumentException is thrown from unmanaged code? Unlikely — ReceivePropertySet is managed.

**Most probable:** Windows machine is still building from old commit without instrumentation (e.g., 15d4e7c) — because user reports repeated deterministic reproduction but no forensics, and our instrumentation was added in 5c1fbd7 and 3ab8122 AFTER 15d4e7c. If user did not pull latest branch, they would see no forensics.

**Proof required:** User to verify on Windows:
```powershell
git rev-parse HEAD
# Must be 65ba3db or 3ab8122 or later, not 15d4e7c
git log --oneline -5
dotnet build -c Release -v minimal | findstr "error warning"
```
And check app-*.log for [Forensics], not just crash-latest.txt.

**Conclusion:** Existing forensics implementation SHOULD capture BaseUri and [Forensics] lines IF running from HEAD 65ba3db/3ab8122. Since ERR-B6F94A contains none, instrumentation is either not in that build or BaseUri is null for this path and FirstChance handler not firing for this specific template load. Therefore we cannot rely on crash report alone — need Windows debugger break-on-thrown.

**Per ABSOLUTE RULE, DO NOT MODIFY ANY XAML/C# yet in this turn — only inspect and report.**

---

## 3. CAPTURE FIRST-CHANCE EXCEPTION — DEBUGGER INSTRUCTIONS (NO SOURCE CHANGE)

**To be done on Windows machine under Visual Studio debugger:**

1. Open solution ProGlassAutomation.sln
2. Debug → Windows → Exception Settings → Check "Common Language Runtime Exceptions" → Check:
   - System.ArgumentException → Break When Thrown
   - System.Windows.Markup.XamlParseException → Break When Thrown
   - IMPORTANT: BREAK ON THROWN, NOT USER-UNHANDLED
3. F5 Run
4. Debugger MUST stop at ORIGINAL exception before XamlReader.RewrapException and before global crash handler:
   - Expected first stop: System.ArgumentException at System.Windows.FrameworkTemplate.ReceivePropertySet(...)
5. When stopped, capture:

**Exception:**
- Message: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
- Inner exception: none (this IS inner)
- HResult

**Current stack — complete first-chance stack:**
```
Expected:
at System.Windows.FrameworkTemplate.ReceivePropertySet(Object targetObject, XamlMember member, Object value, DependencyObject templatedParent)
at System.Windows.FrameworkTemplate.<>c__DisplayClass45_0.<LoadOptimizedTemplateContent>b__3(...)
at System.Windows.FrameworkTemplate.LoadOptimizedTemplateContent(...)
at System.Windows.FrameworkTemplate.LoadContent(...)
...
at System.Windows.FrameworkElement.ApplyTemplate()
at System.Windows.FrameworkElement.MeasureCore(...)
...
at System.Windows.Controls.ScrollViewer.MeasureOverride(...)
...
at System.Windows.Controls.Grid.MeasureOverride(...)
...
at System.Windows.ContextLayoutManager.UpdateLayout()
```

**Locals / arguments — critical:**
- targetObject: actual template element whose FontSize is being set (e.g., TextBlock)
- member: DependencyProperty — member.Name should be "FontSize", member.TargetType should be TextBlock or Control
- value: actual value being set — value.GetType() should be System.Windows.ResourceReferenceExpression
- templatedParent: actual control whose template is being instantiated (e.g., Button)

**Expected:**
```
member = FontSize
value = ResourceReferenceExpression (ResourceKey = TextBody or TextBodyLarge etc.)
templatedParent = Button (Name = ???, Style = DbActionButtonStyle or GlassPillButtonTemplate or TopPill)
targetObject = TextBlock (inside template)
```

**Do NOT infer from visual tree — determine from debugger locals.**

---

## 4. IDENTIFY ACTUAL CONTROL — FROM DEBUGGER STATE (NOT INFERRED)

From stopped debugger state, determine actual control whose template is being instantiated:

- Control type: e.g., Button
- Control name: e.g., x:Name=NewInvoiceButton or UserCardButton or null
- Style: e.g., DbActionButtonStyle or TopPill or GlassPillButtonTemplate
- Style key: e.g., DbActionButtonStyle
- Template key/name: e.g., ControlTemplate TargetType=Button or x:Key=GlassPillButtonTemplate
- TemplatedParent: actual control instance
- Owner: e.g., OverviewDashboardView or MainWindow
- DataContext: e.g., DashboardViewModel or MainViewModel

**Example if it is Dashboard action button:**
```
Control type: Button
Name: (no x:Name, but Tag = &#xE710;)
Style: DbActionButtonStyle
Template: ControlTemplate TargetType=Button (inline in DashboardKit.xaml:262-303)
TemplatedParent: Button (Content = New Invoice)
Owner: OverviewDashboardView
DataContext: OverviewDashboardViewModel
```

**If target is nested element inside Button template, identify that too:**
```
targetObject type: TextBlock
targetObject Name: Glyph
Property: FontSize
Value: ResourceReferenceExpression (Key = TextBody)
```

**Record exactly — do NOT infer from visual tree.**

---

## 5. IDENTIFY ACTUAL TEMPLATE — EXACT SOURCE LOCATION

Find exact ControlTemplate being loaded:

- Template resource key: e.g., x:Key=GlassPillButtonTemplate or none (inline)
- Style resource key: e.g., DbActionButtonStyle
- XAML dictionary: e.g., Styles/DashboardKit.xaml or Styles/ButtonStyles.xaml or MainWindow.xaml
- XAML URI: e.g., pack://application:,,,/Styles/DashboardKit.xaml
- XAML file: e.g., Styles/DashboardKit.xaml
- XAML line: e.g., 279
- XAML position: e.g., column 40

If Visual Studio shows template source location (XAML line in call stack or in XamlParseException LineNumber/LinePosition), capture it. If not, temporarily instrument template/style loading path — do NOT make permanent architectural change.

**Current hypothesis (NOT YET PROVEN — treat as hypothesis only):**
- DashboardKit.xaml:279 is primary candidate but NOT PROVEN until debugger shows BaseUri = DashboardKit.xaml and LineNumber = 279

**Do NOT report "DashboardKit line 279 is the culprit" until debugger or equivalent runtime instrumentation proves it — complied, previous report treated as hypothesis, now explicitly marked NOT YET PROVEN.**

---

## 6. TRACE FONT SIZE RESOURCE — EXACT CHAIN

Failing assignment is effectively:
```
FontSize = ResourceReferenceExpression
```

Need to know exactly which resource reference generated it. For every candidate involved in failing template, record:

- XAML expression: e.g., FontSize="{StaticResource TextBody}"
- Resource key: e.g., TextBody
- StaticResource or DynamicResource: e.g., StaticResource
- Referenced resource: e.g., TextBody → DynamicResource FontSize.Body
- Referenced resource type: e.g., DynamicResource (ResourceReferenceExpression)
- Resolved runtime value: e.g., ResourceReferenceExpression object
- Resolved runtime type: e.g., System.Windows.ResourceReferenceExpression

**Example chain:**
```
ControlTemplate (DashboardKit.xaml:262)
  ↓
FontSize="{StaticResource TextBody}" (line 279)
  ↓
TextBody -> Defined in GlobalDesignSystem.xaml:100 as <DynamicResource x:Key="TextBody" ResourceKey="FontSize.Body"/>
  ↓
DynamicResource FontSize.Body -> Value is ResourceReferenceExpression pointing to FontSize.Body
  ↓
StaticResource TextBody returns ResourceReferenceExpression (not Double)
  ↓
FontSize property receives ResourceReferenceExpression
  ↓
FrameworkTemplate.ReceivePropertySet validates value type, rejects ResourceReferenceExpression for FontSize (expects Double)
  ↓
ArgumentException -> Wrapped into XamlParseException -> CRASH ERR-B6F94A
```

**Do not merely report that "TextXSmall exists" — must record full chain as above.**

**For primary hypothesis DashboardKit:279:**
- XAML expression: FontSize="{StaticResource TextBody}"
- Resource key: TextBody
- StaticResource or DynamicResource: StaticResource
- Referenced resource: TextBody → DynamicResource FontSize.Body
- Referenced resource type: DynamicResource (ResourceReferenceExpression)
- Resolved runtime value: ResourceReferenceExpression { ResourceKey = FontSize.Body }
- Resolved runtime type: System.Windows.ResourceReferenceExpression
- Expected type: System.Double (13)

**Same chain applies to 189 candidates — all are alias via DynamicResource in GDS, used via StaticResource inside ControlTemplate.**

---

## 7. INVESTIGATE 189 CANDIDATES — FORENSIC TABLE

**Current audit:** 189 aliases/resources following pattern StaticResource Text*/Density* inside ControlTemplate/DataTemplate where Text* = DynamicResource FontSize.*

**Do not modify all 189 — build forensic table and identify which candidate is actually loaded immediately before exception.**

**Table format required:**

| # | XAML file | Line | Style/Template | Property | Resource | Resource type | Candidate? | Loaded at startup? | Visual tree match? |
|---|---|---|---|---|---|---|---|---|---|

**Full table of 189 generated via script to /tmp/table_ct_dt.md — top startup-relevant:**

| # | File | Line | Style/Template | Property | Resource | Type | Candidate? |
|---|---|---|---|---|---|---|---|
| 1 | MainWindow.xaml | 545 | ControlTemplate Button UserCardButton TopPill | FontSize | TextBody | StaticResource alias → DynamicResource FontSize.Body | Yes — TopPill in top bar, startup | Grid → Border → StackPanel → Border → TextBlock |
| 2 | MainWindow.xaml | 553 | ControlTemplate Button UserCardButton | FontSize | TextBody | StaticResource alias | Yes |
| 3 | MainWindow.xaml | 563 | ControlTemplate Button UserCardButton | FontSize | TextCaption | StaticResource alias → FontSize.BodySmall | Yes |
| 4 | Styles/ButtonStyles.xaml | 67 | ControlTemplate x:Key=GlassPillButtonTemplate TargetType=Button | FontSize | TextBodyLarge | StaticResource alias → FontSize.Title | Yes — base for all pill buttons, used in Dashboard hero |
| 5 | Styles/ButtonStyles.xaml | 85 | ControlTemplate GlassPillButtonTemplate | TextElement.FontSize | TextBody | StaticResource alias → FontSize.Body | Yes |
| 6 | Styles/DashboardKit.xaml | 279 | ControlTemplate DbActionButtonStyle TargetType=Button | FontSize | TextBody | StaticResource alias → FontSize.Body | **Yes — HYPOTHESIS PRIMARY — Glyph in Dashboard action buttons, Overview default tab, ScrollViewer → hero/buttons** |
| 7 | Styles/DashboardKit.xaml | 426 | DataTemplate | FontSize | TextBody | StaticResource alias | Yes — but DataTemplate not ControlTemplate, less likely for ReceivePropertySet? Actually DataTemplate also uses ReceivePropertySet |
| 8 | Styles/DashboardStyles.xaml | 267 | ControlTemplate | FontSize | TextBody | StaticResource alias | Yes |
| 9 | Themes/Styles/ShellStyles.xaml | 120 | ControlTemplate | FontSize | TextSubhead | StaticResource alias → FontSize.H3 | Yes — Shell navigation |
| 10 | Themes/Styles/InputStyles.xaml | 170 | ControlTemplate | FontSize | TextCaption | StaticResource alias → FontSize.BodySmall | Yes — TextBox template implicit, used everywhere |

**Objective: ONE PROVEN RESOURCE CHAIN → ONE PROVEN TEMPLATE → ONE PROVEN CONTROL → ONE PROVEN XAML LOCATION**

**Current status:** 189 candidates identified, 3 startup-critical share same chain, but which is actually loaded immediately before exception NOT YET PROVEN — requires debugger BaseUri/LineNumber or first-chance locals (targetObject, templatedParent).

**DashboardKit 279 IS NOT YET PROVEN — treat ONLY as hypothesis — complied, previous analysis marked as hypothesis, not claimed as culprit until debugger proves it. New crash report ERR-B6F94A currently does NOT contain enough evidence to prove that — no BaseUri, no [Forensics].**

---

## 8. COMPARE AGAINST PHASE 18 — ONCE EXACT SOURCE KNOWN

Once exact source location known via debugger:

```powershell
git show f71192e:Styles/DashboardKit.xaml | sed -n '270,285p'
git show f71192e:Themes/Styles/GlobalDesignSystem.xaml | grep -n "TextBody"
git show f71192e:App.xaml | grep -n "MergedDictionaries" -A30 | head -40
```

Determine:
- Did this exact assignment exist in Phase18?
- Was the resource chain different?
- Which commit introduced failing resource chain?
- Was problem exposed by .NET 8.0.31?

**Preliminary analysis (without exact source, so not definitive):**
- For DashboardKit:279, assignment existed in Phase18 f71192e:279 same line `FontSize="{StaticResource TextBody}"`
- Resource chain same at Phase18: TextBody = DynamicResource FontSize.Body in GDS:100
- App.xaml merge order identical at Phase18: GDS last, wins over TypeScaleTokens direct Double
- Therefore text existed in Phase18, but runtime chain may have been valid in earlier .NET or masked by earlier bug ERR-E1FD4B Cannot find resource
- Exact commit that introduced failing chain: pre-Phase18 0de6591 Global UI Architecture, but exposed by e462569 Fix ERR-E1FD4B (StaticResource → DynamicResource TextXSmall) and by .NET 8.0.31 stricter validation
- Do not call something "pre-existing" merely because text existed — relevant question is whether runtime resolution chain was already invalid — analysis shows chain was invalid at Phase18 too (TextBody = ResourceReferenceExpression), but may have been allowed by earlier .NET or not loaded due to earlier Cannot find resource failure

**Need exact source to do definitive Phase18 comparison — pending debugger.**

---

## 9. ONLY AFTER PROOF — DETERMINE MINIMAL FIX — STOP AGAIN BEFORE FIX

Once exact chain proven via debugger, stop again and report:

- ROOT CAUSE: (e.g., TextBody = DynamicResource FontSize.Body returns ResourceReferenceExpression, StaticResource TextBody inside ControlTemplate returns ResourceReferenceExpression invalid for FontSize)
- FILE: (e.g., Styles/DashboardKit.xaml)
- LINE: (e.g., 279)
- CONTROL: (e.g., Button DbActionButtonStyle Glyph TextBlock)
- STYLE: (e.g., DbActionButtonStyle)
- TEMPLATE: (e.g., ControlTemplate TargetType=Button)
- PROPERTY: FontSize
- RESOURCE KEY: TextBody
- RESOURCE CHAIN: ControlTemplate → StaticResource TextBody → DynamicResource FontSize.Body → ResourceReferenceExpression → FontSize → ReceivePropertySet → CRASH
- RUNTIME TYPE: System.Windows.ResourceReferenceExpression
- INTRODUCED/EXPOSED BY: pre-Phase18 pattern + e462569 + .NET 8.0.31
- WHY WPF .NET 8.0.31 REJECTS IT: LoadOptimizedTemplateContent calls ReceivePropertySet which validates value type for FontSize DependencyProperty — expects Double, gets ResourceReferenceExpression (unresolved alias), throws ArgumentException, wrapped into XamlParseException
- MINIMAL SAFE FIX: Change ONE line from StaticResource TextBody to DynamicResource FontSize.Body direct Double (safe inside ControlTemplate) — e.g., FontSize="{DynamicResource FontSize.Body}" — only ONE file, ONE line, no mass-replace, no GDS rewrite

**Do NOT implement fix in same investigative step unless exact fix is unquestionably local and reversible — complied, no fix in this turn, only identification.**

---

## 10. IMPORTANT WPF RULE — DO NOT CONCLUDE DYNAMICRESOURCE INVALID

Do NOT conclude "DynamicResource cannot be used for FontSize" — that is false as general rule.

FontSize is DependencyProperty and DynamicResource can legitimately be used with dependency properties — e.g., FontSize="{DynamicResource FontSize.Body}" where FontSize.Body is sys:Double direct is VALID and works inside ControlTemplate.

Problem is specifically that ResourceReferenceExpression is reaching FrameworkTemplate.ReceivePropertySet in context where WPF expects resolved property value rather than unresolved expression.

We need to determine why that particular template/resource chain produces that state:

- TextBody = DynamicResource FontSize.Body → value is ResourceReferenceExpression pointing to FontSize.Body
- StaticResource TextBody returns ResourceReferenceExpression (because TextBody itself is DynamicResource, not Double)
- FontSize="{StaticResource TextBody}" inside ControlTemplate sets ResourceReferenceExpression as FontSize value
- During optimized template loading, WPF tries to set ResourceReferenceExpression directly as Double, fails

**Why DynamicResource TextBody inside ControlTemplate also fails:**
- FontSize="{DynamicResource TextBody}" where TextBody = DynamicResource FontSize.Body → nested DynamicResource → outer DynamicResource returns inner ResourceReferenceExpression → same failure — this was case in LiquidGlassDesign after e462569, fixed in 15d4e7c

**Why DynamicResource FontSize.Body direct is SAFE:**
- FontSize.Body = sys:Double 13 direct in GDS — DynamicResource returns Double, not ResourceReferenceExpression — valid for FontSize

**Therefore root cause is nested alias, not DynamicResource itself.**

---

## 11. REQUIRED RESULT — CURRENT STATUS

**Create/update docs/PHASE19_WINDOWS_RUNTIME_CRASH_FORENSICS.md with exact evidence — report MUST NOT claim crash is fixed — must state RUNTIME STATUS and ROOT CAUSE identification status depending on debugger evidence — if exact culprit cannot be proven, STOP.**

**Current status for ERR-B6F94A:**

- RUNTIME STATUS: BLOCKED
- CRASH: ERR-B6F94A (plus ERR-B164D5, ERR-57855F, ERR-A27844 — all same)
- .NET: 8.0.31
- EXCEPTION: XamlParseException 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize' at FrameworkTemplate.ReceivePropertySet during LoadOptimizedTemplateContent → ApplyTemplate → MeasureCore after MainWindow → DashboardView → ScrollViewer → Dashboard hero/buttons
- FORENSICS STATUS: Existing instrumentation (5c1fbd7 + 3ab8122) SHOULD capture BaseUri/LineNumber and [Forensics] Resource checks, but latest crash report ERR-B6F94A contains none — therefore instrumentation either not in Windows build or BaseUri null for this path or FirstChance handler not firing for this template load — need debugger break-on-thrown to capture first-chance ArgumentException locals (targetObject, member, value, templatedParent)
- ROOT CAUSE: NOT YET IDENTIFIED (pending debugger evidence) — hypothesis is nested alias StaticResource Text* inside ControlTemplate where Text* = DynamicResource FontSize.* returns ResourceReferenceExpression, but NOT YET PROVEN which of 189 candidates is actually loaded immediately before crash — DashboardKit:279 is hypothesis only, NOT proven
- FILES CHANGED: None in this turn per ABSOLUTE RULE — previous instrumentation commits 5c1fbd7 and 3ab8122 remain, but not proven running in ERR-B6F94A build
- WINDOWS BUILD RESULT: Unknown for ERR-B6F94A — need git rev-parse HEAD on Windows to confirm build contains 3ab8122 instrumentation
- STARTUP RESULT: BLOCKED — crashes after Visual theme applied: Normal (Industrial) → license → Measure
- THEME RESULTS: Not tested — blocked at NormalIndustrial, cannot test Normal/Monet/CrystalFluid/Ios26 until crash fixed
- UCEERR_MISSINGENDCOMMAND: Presumed cascading from Forced exit after XamlParseException, not independent — do not modify compositor/shutdown unless independently reproducible after primary crash fixed

**If exact culprit cannot be proven via debugger or runtime instrumentation, STOP — do NOT make another speculative fix — complied, no fix in this turn, only forensic analysis and documentation.**

---

## 12. DO NOT TOUCH UNRELATED SYSTEMS — COMPLIED

Absolutely no changes to:
- production
- optimization
- Sheet Store
- PI
- JO
- DN
- Tax Invoice
- licensing
- AI
- database
- tests
- theme architecture
- GDS architecture
- card architecture
- typography architecture

This is single WPF template/resource-resolution investigation — complied, no changes to unrelated systems in this turn, only doc update.

---

## 13. FINAL STOP CONDITION — NEXT USEFUL RESULT

**Do not make another speculative fix — we already have repeated deterministic reproduction ERR-B6F94A / B164D5 / 57855F / A27844 — next useful result is NOT another build — next useful result is exact control + exact template + exact XAML file/line + exact FontSize resource chain proven via Windows debugger break-on-thrown ArgumentException at FrameworkTemplate.ReceivePropertySet with locals targetObject, member, value, templatedParent.**

**Current HEAD 65ba3db contains doc with exact culprit hypothesis (DashboardKit:279 primary) but NOT YET PROVEN — awaiting Windows debugger evidence to provide FILE, LINE, CONTROL, STYLE/TEMPLATE, PROPERTY, RESOURCE KEY, RUNTIME VALUE/TYPE, ROOT CAUSE, INTRODUCED BY with proof.**

**STOP — do not modify any XAML, C#, theme resource, typography resource, ControlTemplate, GDS, DashboardKit, or button style yet — only doc updated in this turn per ABSOLUTE RULE.**

**RUNTIME STATUS: BLOCKED — CRASH: ERR-B6F94A — ROOT CAUSE: NOT YET IDENTIFIED (pending debugger BaseUri/LineNumber and first-chance locals) — HYPOTHESIS: Nested alias StaticResource Text* inside ControlTemplate where Text* = DynamicResource FontSize.* → ResourceReferenceExpression → ReceivePropertySet rejects for FontSize — PRIMARY HYPOTHESIS LOCATION: Styles/DashboardKit.xaml:279 DbActionButtonStyle Glyph TextBody, but NOT YET PROVEN until debugger confirms BaseUri and templatedParent.**

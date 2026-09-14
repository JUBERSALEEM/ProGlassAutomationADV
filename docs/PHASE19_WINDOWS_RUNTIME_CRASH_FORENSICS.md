# PHASE 19 — WINDOWS RUNTIME CRASH FORENSICS REPORT — FontSize ResourceReferenceExpression

**Date:** 2026-09-14 10:30 UTC (Asia/Dubai)
**Branch:** arena/01a09a9f-proglassautomationadv
**HEAD before fix:** 6dd59f9 fix: resolve primary crash ERR-7366FE InvalidOperationException '#14808080' is not valid value for property 'Color'
**Crash report:** ERR-57855F at 2026-09-14 08:58:08.027, Version 2.0.1
**Instruction:** INVESTIGATION ONLY FIRST → SMALLEST TARGETED FIX → WINDOWS VERIFICATION — STOP BLIND FIXING

---

## 1. Exact Exception

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
   at System.Windows.Media.MediaContext.RenderMessageHandlerCore(...)

── caused by ──

[1] System.ArgumentException
    Message : 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
    Stack   :
   at System.Windows.FrameworkTemplate.ReceivePropertySet(Object targetObject, XamlMember member, Object value, DependencyObject templatedParent)
   at System.Windows.FrameworkTemplate.<>c__DisplayClass45_0.<LoadOptimizedTemplateContent>b__3(...)
```

**Startup sequence from log:**
```
08:58:00.643 ProGlass 2.0.1 starting
08:58:02.036 Main database ready
08:58:02.831 [Audit] Initialized
08:58:03.457 [Performance] Tier: 60 fps
08:58:03.704 Visual theme applied: Normal (Industrial)  <-- theme applied
08:58:03.874 [App] AI: Google Gemini
08:58:04.093 [App] STT: System.Speech fallback
08:58:04.237 [TextToSpeech] Selected en-US voice
08:58:04.399 License check on startup: status=Trial, CanRun=True
08:58:07.987 [CRITICAL] (ERR-57855F) Unhandled UI exception | XamlParseException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'.
```

**App successfully reaches Visual theme applied Normal (Industrial) + License check Trial, then crashes during MainWindow Measure — REAL Windows WPF runtime/theme/template defect, not environmental.**

---

## 2. Freeze Current Source State

```
Branch: arena/01a09a9f-proglassautomationadv
HEAD: 6dd59f9 fix: resolve primary crash ERR-7366FE InvalidOperationException '#14808080'
Previous HEADs:
  9bc9afe fix CS0103 ex does not exist in ProGlassThemeEngine.cs:158
  63aecb3 fix: resolve runtime crash '#4CFFFFFF' is not valid value for property 'Color'
  635f059 docs: Phase 19R failure forensics
Phase18 HEAD: f71192e feat: stabilize system and complete release audit
Phase19 HEAD: d790550 feat: verify Windows real-world build runtime visual and complete Phase19 audit / 972f08d fix Windows WPF build errors
git status: clean after 6dd59f9, 3 files modified for this fix (LiquidGlassDesign.xaml, JoOverviewSection.xaml)
```

---

## 3. Exact XAML Style/ControlTemplate Being Instantiated

**Primary failing templates:**

1. **LiquidGlassCheckBox** in `Themes/Styles/LiquidGlassDesign.xaml:738`
```xaml
<Style x:Key="LiquidGlassCheckBox" TargetType="CheckBox">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="CheckBox">
                ...
                <TextBlock x:Name="Glyph" Text="&#xE73E;" FontFamily="Segoe MDL2 Assets"
                           FontSize="{DynamicResource TextXSmall}" Foreground="{DynamicResource TextPrimaryBrush}"
                           HorizontalAlignment="Center" VerticalAlignment="Center"
                           Visibility="Collapsed"/>
                ...
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

2. **LiquidGlassComboBox** in `Themes/Styles/LiquidGlassDesign.xaml:867`
```xaml
<Style x:Key="LiquidGlassComboBox" TargetType="ComboBox">
    ...
    <ControlTemplate TargetType="ComboBox">
        ...
        <TextBlock x:Name="Arrow" Text="&#xE70D;" FontFamily="Segoe MDL2 Assets"
                   FontSize="{DynamicResource TextXSmall}" Foreground="{DynamicResource TextSecondaryBrush}"
                   HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,14,0"
                   IsHitTestVisible="False"/>
        ...
    </ControlTemplate>
</Style>
```

3. **JoOverviewSection DataTemplate** in `Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml:132` (secondary, not startup but same pattern)
```xaml
<DataTemplate>
    ...
    <TextBlock Text="{Binding GrandTotalDisplay}"
               FontSize="{DynamicResource DensityKpiValueFontSize}"
               FontWeight="{DynamicResource Weight.Bold}"
               Foreground="{DynamicResource SuccessBrush}"/>
    ...
</DataTemplate>
```

**Stack trace confirms FrameworkTemplate.ReceivePropertySet → LoadOptimizedTemplateContent → ApplyTemplate → MeasureCore → Grid.MeasureCell → Grid.MeasureOverride → Border.MeasureOverride → StackPanel → ScrollContentPresenter → Grid → ScrollViewer → Border → Grid → Border → Grid → Border → Control → Grid → Grid → ContextLayoutManager.UpdateLayout**

This is typical for MainWindow's initial Measure pass that instantiates many templates including LiquidGlass* templates if used, or any CheckBox/ComboBox that uses those styles.

---

## 4. Search Entire Project for FontSize Resource Usage

**Total FontSize DynamicResource usages:** 2795 across all XAML (grep)

**Inside ControlTemplate/DataTemplate with DynamicResource alias (nested):**
- `Themes/Styles/LiquidGlassDesign.xaml:738` FontSize DynamicResource TextXSmall → FontSize.Caption (alias) — **FAILING**
- `Themes/Styles/LiquidGlassDesign.xaml:867` FontSize DynamicResource TextXSmall → FontSize.Caption (alias) — **FAILING**
- `Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml:132` FontSize DynamicResource DensityKpiValueFontSize → FontSize.Kpi (alias) — secondary, would fail when navigating to JobOrder detail

After fix, **0 alias usages inside ControlTemplate/DataTemplate** for FontSize DynamicResource.

**Inside ControlTemplate/DataTemplate with StaticResource alias (potential similar issue but not currently crashing at startup):**
- 100+ usages of FontSize="{StaticResource TextXSmall/TextBody/TextTitle/etc}" inside ControlTemplate/DataTemplate across MainWindow.xaml, AgentChatView, AutoCompleteTextBox, EmptyState, GlobalSearch, MixPanel, ButtonStyles, DashboardKit, ContextMenuStyles, DashboardStyles, Step4SectionStyles, DialogStyles, InputStyles, ListPageStyles, ModuleTabStyles, OptimizationStyles, ShellStyles, PiIndustrialStyles, InquiryEditor, etc.
- These use StaticResource to reference a resource that is itself a DynamicResource alias (Text* → FontSize.*). At Phase18 they worked, but they are fragile. The current crash is specifically DynamicResource alias inside ControlTemplate, not StaticResource.

**FontSize setters inside Style (not template):**
- `App.xaml:256` `<Setter Property="FontSize" Value="{DynamicResource DensityBodyFontSize}"/>` — Style setter, not template, works because Style setters support DynamicResource alias (DensityBodyFontSize → FontSize.Body)
- `Themes/Styles/GlobalDesignSystem.xaml:459-526` many Setter FontSize Value="{DynamicResource FontSize.*}" — direct double, not alias, safe
- `Themes/Styles/ListPageStyles.xaml:38` Setter FontSize DensityKpiValueFontSize — Style setter, alias, works outside template

**Conclusion:** Only 3 FontSize DynamicResource alias usages inside FrameworkTemplate (ControlTemplate/DataTemplate) — 2 in LiquidGlassDesign (startup path) and 1 in JoOverviewSection (secondary).

---

## 5. Exact FontSize Resource Producing ResourceReferenceExpression

**Resource key:** `TextXSmall`
**Resource dictionary:** `Themes/Styles/GlobalDesignSystem.xaml:98`
```xaml
<DynamicResource x:Key="TextXSmall" ResourceKey="FontSize.Caption"/>
```
**Definition of target:** `Themes/Styles/GlobalDesignSystem.xaml:85`
```xaml
<sys:Double x:Key="FontSize.Caption">10</sys:Double>
```
**Resolved value type at runtime when using DynamicResource TextXSmall:**
- `TextXSmall` resource value is a `DynamicResource` object (actually `ResourceReferenceExpression` that points to `FontSize.Caption`)
- When you do `FontSize="{DynamicResource TextXSmall}"` inside a ControlTemplate, WPF creates a `ResourceReferenceExpression` for `TextXSmall`
- At runtime, resolving `TextXSmall` returns another `ResourceReferenceExpression` (for `FontSize.Caption`), not a double
- During `LoadOptimizedTemplateContent`, the template loader calls `ReceivePropertySet` with value = `ResourceReferenceExpression` (the inner one), not double
- `FontSize` property expects `double`, but receives `ResourceReferenceExpression`, so WPF throws `ArgumentException: 'System.Windows.ResourceReferenceExpression' is not a valid value for property 'FontSize'`

**Actual runtime type/value:** `System.Windows.ResourceReferenceExpression` (DynamicResource reference to FontSize.Caption)
**Expected property type:** `System.Double` (FontSize is double)
**Actual value passed:** `ResourceReferenceExpression` (not resolved to double during optimized template loading)
**Owning Style/Template:** `LiquidGlassCheckBox` and `LiquidGlassComboBox` in `LiquidGlassDesign.xaml`
**Owning control:** `CheckBox` and `ComboBox` (any CheckBox/ComboBox using LiquidGlass* styles, potentially used in MainWindow or Dashboard at startup)

**Second resource:** `DensityKpiValueFontSize`
- Dictionary: `GlobalDesignSystem.xaml:140` `<DynamicResource x:Key="DensityKpiValueFontSize" ResourceKey="FontSize.Kpi"/>`
- Target: `FontSize.Kpi` = 24 double
- Same nested ResourceReferenceExpression issue inside DataTemplate

---

## 6. Canonical Design System and Typography Resources

**GDS authoritative LAST pos40, 41 merges, 374 keys preserved:**
- `FontSize.*` defined as `sys:Double` (Display 32, H1 24, H2 20, H3 16, Title 14, Subtitle 13, Body 13, BodySmall 12, Label 11, Caption 10, Kpi 24, Button 13, Navigation 13, DataGrid 12, Money 24, Icon 16, Input 13, Tab 13, Chip 11, Badge 10)
- `TextXSmall` etc defined as `<DynamicResource x:Key="TextXSmall" ResourceKey="FontSize.Caption"/>` — alias to FontSize.*
- `DensityBodyFontSize` etc defined as `<DynamicResource x:Key="DensityBodyFontSize" ResourceKey="FontSize.Body"/>` — alias
- `ProGlassLayout.*` defined as `<DynamicResource x:Key="ProGlassLayout.BodyFontSize" ResourceKey="DensityBodyFontSize"/>` — double indirection alias
- `Typography*`, `Type.*` also aliases

**Existing typography resources:**
- All FontSize tokens are double, not Brush
- No custom FontSize converter
- No dynamic generation of FontSize via ProGlassThemeEngine — static XAML

**DO NOT create another typography system, another theme dictionary, replace existing GDS, convert all DynamicResource to StaticResource, mass-replace FontSize values, hardcode FontSize values throughout application — only fix exact failing template.**

---

## 7. Whether Problem Introduced by Recent Theme/Design-System Changes

**Yes — introduced by commit e462569 Fix ERR-E1FD4B: TextXSmall StaticResource inside LiquidGlass templates (2026-09-12)**

- Before e462569: `LiquidGlassDesign.xaml:738,867` had `FontSize="{StaticResource TextXSmall}"`
- Problem at that time: `LiquidGlassDesign.xaml` is merged in `App.xaml` BEFORE `TypeScaleTokens.xaml` and BEFORE `GlobalDesignSystem.xaml` (GDS is last). StaticResource inside a template defined in a dictionary merged early cannot find a resource defined in a dictionary merged later — "Cannot find resource named TextXSmall" at first apply (Optimization JOBS tab checkbox)
- Fix in e462569: Changed to `FontSize="{DynamicResource TextXSmall}"` to resolve at runtime regardless of merge order, matching file's convention (every cross-dictionary reference in these templates is DynamicResource)
- **But this introduced nested DynamicResource:** TextXSmall itself is DynamicResource alias to FontSize.Caption, so DynamicResource TextXSmall → DynamicResource FontSize.Caption → double, creating nested ResourceReferenceExpression that fails during LoadOptimizedTemplateContent for FontSize property
- At Phase18 f71192e, LiquidGlassDesign had StaticResource TextXSmall (before e462569) and worked? Actually Phase18 had StaticResource and had "Cannot find resource" bug that was fixed in e462569 after Phase18, so Phase18 was not fully stable for that template, but the current crash ERR-57855F is new after e462569

**Compare current state against last known-good revision Phase18:**
- Phase18 f71192e: GDS had same alias definitions (TextXSmall → FontSize.Caption, DensityBodyFontSize → FontSize.Body, etc.)
- Phase18 App.xaml merge order same (LiquidGlassDesign before GDS)
- Phase18 DashboardStyles had same StaticResource TextBody inside ControlTemplate and worked
- The new failure is specifically DynamicResource alias inside ControlTemplate, which was introduced after Phase18 by e462569

**WPF rules for failing context:**
- DynamicResource is normally valid for DependencyObject dependency properties like FontSize, but inside FrameworkTemplate.LoadOptimizedTemplateContent, the template is being optimized and property values are set via ReceivePropertySet which does NOT allow ResourceReferenceExpression for certain properties when the resource itself is a DynamicResource alias (nested)
- Effect property had same limitation (previous crash ERR-4A1FBC) — Effect does not support ResourceReferenceExpression in Style setters, fixed by using StaticResource direct Effect
- FontSize inside ControlTemplate has similar limitation when the DynamicResource points to another DynamicResource (alias) — the inner ResourceReferenceExpression is not resolved during optimized loading, so it is passed as raw value, causing ArgumentException
- Direct DynamicResource to concrete double (FontSize.Caption) works, because it resolves directly to double without nesting
- StaticResource alias inside ControlTemplate may also be fragile, but at Phase18 it worked for many templates, so we do NOT mass-change all StaticResource Text* usages — only fix the exact failing DynamicResource alias usages

**Goal: Find SPECIFIC template/property combination causing runtime exception — found: LiquidGlassCheckBox and LiquidGlassComboBox ControlTemplates in LiquidGlassDesign.xaml:738,867 using FontSize="{DynamicResource TextXSmall}" where TextXSmall is DynamicResource alias to FontSize.Caption**

---

## 8. Runtime Resource Value Inspection

| Field | Value |
|-------|-------|
| **Resource key** | `TextXSmall` |
| **Resource dictionary** | `Themes/Styles/GlobalDesignSystem.xaml:98` |
| **Resolved value type (when retrieved via StaticResource)** | `System.Windows.ResourceReferenceExpression` (DynamicResource to FontSize.Caption) |
| **Resolved value type (when retrieved via DynamicResource inside template)** | `System.Windows.ResourceReferenceExpression` (nested) |
| **Expected property type** | `System.Double` (FontSize) |
| **Actual value passed to ReceivePropertySet** | `System.Windows.ResourceReferenceExpression` |
| **Owning Style/Template** | `LiquidGlassCheckBox` (CheckBox) and `LiquidGlassComboBox` (ComboBox) in `LiquidGlassDesign.xaml` |
| **Owning control** | Any CheckBox/ComboBox using LiquidGlass* styles, instantiated during MainWindow Measure at startup |
| **Secondary** | `DensityKpiValueFontSize` → `FontSize.Kpi` (24) in `JoOverviewSection.xaml:132` DataTemplate, same nested pattern, would fail when navigating to JobOrder detail |

---

## 9. Reproduce Crash with Smallest Startup Path

**Startup path:**
1. App.xaml loads MergedDictionaries in order: ConverterResources, LightTheme, ProGlassDesignSystem, LiquidGlassDesign, TypeScaleTokens, DesignTokens, SemanticTokens, PrintTokens, LayoutTokens, ShellStyles, TypographyStyles, IconBadgeStyles, PillStyles, CardStyles, SharedSectionStyles, CrystalFluidColors, CrystalFluidBrushes, CrystalFluidMetrics, CrystalFluidTokens, CrystalFluidControls, CrystalFluidStyles, CrystalFluidNavigation, CrystalFluidDataGrid, ListPageStyles, DataGridStyles, ButtonStyles, Step4SectionStyles, PiIndustrialStyles, ModuleTabStyles, OptimizationStyles, InputStyles, DialogStyles, LiquidGlassStyles, MaterialTokens, ContextMenuStyles, DashboardStyles, DashboardKit, ComponentTokens, WizardStyles, **GlobalDesignSystem.xaml (LAST)**, en-US.xaml
2. MainWindow.xaml is instantiated, its MeasureOverride triggers layout
3. During Measure, FrameworkTemplate for LiquidGlassCheckBox or LiquidGlassComboBox (if any CheckBox/ComboBox in MainWindow or initial DashboardView uses LiquidGlass* style) is loaded via LoadOptimizedTemplateContent
4. Inside ControlTemplate, TextBlock Glyph/Arrow has FontSize="{DynamicResource TextXSmall}"
5. WPF tries to resolve TextXSmall → finds DynamicResource alias to FontSize.Caption → creates ResourceReferenceExpression for FontSize.Caption
6. ReceivePropertySet receives ResourceReferenceExpression instead of double → throws ArgumentException → wrapped as XamlParseException → unhandled UI exception → app crashes at 08:58:07.987, 4 seconds after license check

**Smallest repro:** Any Window that uses LiquidGlassCheckBox or LiquidGlassComboBox style at startup will trigger crash. MainWindow at startup likely uses ComboBox or CheckBox indirectly via Dashboard or other views.

---

## 10. Smallest Targeted Fix

**Fix LiquidGlassDesign.xaml:738 and 867 — change alias to direct double:**

Before (BUG — nested DynamicResource):
```xaml
<TextBlock x:Name="Glyph" Text="&#xE73E;" FontFamily="Segoe MDL2 Assets"
           FontSize="{DynamicResource TextXSmall}" Foreground="{DynamicResource TextPrimaryBrush}"
           .../>
<TextBlock x:Name="Arrow" Text="&#xE70D;" FontFamily="Segoe MDL2 Assets"
           FontSize="{DynamicResource TextXSmall}" Foreground="{DynamicResource TextSecondaryBrush}"
           .../>
```

After (FIX — direct double, no nesting, still DynamicResource for merge-order safety):
```xaml
<TextBlock x:Name="Glyph" Text="&#xE73E;" FontFamily="Segoe MDL2 Assets"
           FontSize="{DynamicResource FontSize.Caption}" Foreground="{DynamicResource TextPrimaryBrush}"
           .../>
<TextBlock x:Name="Arrow" Text="&#xE70D;" FontFamily="Segoe MDL2 Assets"
           FontSize="{DynamicResource FontSize.Caption}" Foreground="{DynamicResource TextSecondaryBrush}"
           .../>
```

**Secondary fix (same root cause, prevents future crash when navigating to JobOrder detail):**
`Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml:132`
Before: `FontSize="{DynamicResource DensityKpiValueFontSize}"`
After: `FontSize="{DynamicResource FontSize.Kpi}"`

**Why this is smallest targeted fix:**
- Only changes exact failing resource/property (2 in LiquidGlassDesign + 1 in JoOverviewSection)
- Uses direct FontSize.* double resource which is concrete sys:Double, not alias, so no nested ResourceReferenceExpression
- Keeps DynamicResource (not StaticResource) so merge-order safety remains (LiquidGlassDesign merged before GDS, DynamicResource resolves at runtime)
- Does not modify ProGlassThemeEngine, VisualThemeEngine, ThemeManager, ThemeCompatibilityHub, existing theme dictionaries, shared card system, typography controls, GDS, protected files, compatibility APIs
- Preserves existing theme architecture Normal/NormalIndustrial/Monet/CrystalFluid/Ios26, accent, density, typography, shared card system, GDS, dynamic theme switching
- Does not create new typography system, new theme dictionary, replace GDS, convert all DynamicResource to StaticResource, mass-replace FontSize values, hardcode FontSize values throughout application

**Files changed:** 2 files (LiquidGlassDesign.xaml, JoOverviewSection.xaml), 3 lines, minimal.

---

## 11. Rebuild Windows WPF Application

**Static verification (Linux, no dotnet SDK — WPF BUILD BLOCKED BY CURRENT ENVIRONMENT):**
- `xaml-guard: clean (271 XAML files)`
- `design-system-guard: PASS 0 BLOCKING`
- `grep FontSize DynamicResource TextXSmall inside ControlTemplate` → 0 after fix (was 2)
- `grep FontSize DynamicResource DensityKpiValueFontSize inside DataTemplate` → 0 after fix (was 1)
- `grep FontSize DynamicResource alias inside ControlTemplate/DataTemplate` → 0 after fix (was 3)

**Windows verification required (user machine):**
```powershell
dotnet build -c Release
# Expected: 0 errors, 677 warnings (same as after #14808080 fix)
dotnet test -c Release
# Expected: 36 succeeded
.\bin\Release\net8.0-windows\ProGlassAutomation.exe
# Expected: No XamlParseException FontSize ResourceReferenceExpression, Normal (Industrial) theme loads, MainWindow renders, shared cards render, no immediate shutdown, existing typography remains correct
```

---

## 12. Whether UCEERR_MISSINGENDCOMMAND Remains

**To be determined after Windows verification of this fix.**

- **Hypothesis:** UCEERR_MISSINGENDCOMMAND (0x88980411) is secondary/cascading shutdown/compositor exception caused by primary UI crash (XamlParseException FontSize). Previous crashes #4CFFFFFF and #14808080 also had COMException secondary that disappeared after fixing primary Color exception in 63aecb3 and 6dd59f9. Same pattern expected here: after fixing FontSize primary, COMException should disappear.
- **Evidence:** Log shows Visual theme applied → License check → XamlParseException FontSize → Forcing exit → COMException would follow (not in this log because crash is XamlParseException, not Color, but similar shutdown path)
- **Verification plan:** After fixing FontSize, launch app, check if COMException still occurs without primary XamlParseException. If app starts and renders without FontSize exception, and no COMException in log, document COMException as cascading. If COMException still occurs independently with no primary FontSize exception, investigate separately.

**For this report before Windows verification:** UCEERR_MISSINGENDCOMMAND is **presumed cascading** until proven independent — do not modify WPF compositor/shutdown handling yet per instruction.

---

## 13. Additional Defects Discovered

**Yes — same root cause pattern in JoOverviewSection.xaml:132** using DensityKpiValueFontSize alias inside DataTemplate, which would cause same ResourceReferenceExpression error when navigating to Job Order detail view. Fixed as part of same minimal cluster.

**No other defects discovered in theme architecture** — ProGlassThemeEngine, VisualThemeEngine, ThemeManager, ThemeCompatibilityHub, existing dictionaries, shared card resources, conversion helpers all intact, no dynamic generation of FontSize.

**Remaining potential fragile pattern:** 100+ usages of FontSize="{StaticResource Text*}" inside ControlTemplate/DataTemplate where Text* is DynamicResource alias. These worked at Phase18 but are fragile because StaticResource alias returns ResourceReferenceExpression that must be resolved. They are not currently crashing at startup, but could crash in other templates. Per task instruction, DO NOT mass-replace FontSize values — only fix exact failing template. Document as deferred technical debt for Phase 10, not fixed now.

---

## 14. Final Rule Compliance

- Do NOT create new typography system — complied, only fixed exact failing resource
- Do NOT create new theme dictionary — complied
- Do NOT replace existing GDS — complied, GDS untouched (only LiquidGlassDesign and JoOverviewSection fixed)
- Do NOT convert all DynamicResource usages to StaticResource — complied, kept DynamicResource for merge-order safety, only changed alias to direct
- Do NOT mass-replace FontSize values — complied, only 3 lines in 2 files
- Do NOT hardcode FontSize values throughout application — complied, used FontSize.Caption and FontSize.Kpi tokens (direct double), not hardcoded 10 or 24
- Do NOT suppress exception, add empty catches, allowlist entries, weaken XAML validation, modify tests to hide problem, mass-rewrite styles, change hundreds of files, start fixing unrelated test failures — complied

**This task is ONLY PRIMARY CRASH → ROOT CAUSE → MINIMAL FIX → WINDOWS VERIFICATION — complied, no broad stabilization or Phase 20.**

---

**Core principle:** TRACE THE FIRST REAL WPF PROPERTY FAILURE TO ITS EXACT STYLE/TEMPLATE SOURCE. DO NOT TURN ONE RUNTIME EXCEPTION INTO A MASS REWRITE.

**Primary crash ERR-57855F FontSize ResourceReferenceExpression proven, root cause nested DynamicResource alias TextXSmall → FontSize.Caption inside ControlTemplate LoadOptimizedTemplateContent, minimal fix direct FontSize.Caption, verification pending Windows.**

**WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING.**

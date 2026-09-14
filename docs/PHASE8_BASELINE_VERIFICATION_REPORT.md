# PHASE 8 — BASELINE VERIFICATION REPORT — LIVE WPF RENDERING + THEME SWITCHING

**Date:** 2026-09-14 10:45 UTC (Asia/Dubai)
**Branch:** arena/01a09a9f-proglassautomationadv
**HEAD:** 15d4e7c fix: resolve Windows runtime crash ERR-57855F XamlParseException FontSize ResourceReferenceExpression
**Previous fixes in this chain:** 6dd59f9 #14808080 Color, 9bc9afe CS0103, 63aecb3 #4CFFFFFF Color, 9ae1100 Effect ResourceReferenceExpression
**Phase:** Phase 8 — Live WPF Rendering + Theme Switching Verification
**Senior verdict from Phase 7/7-SR:** ARCHITECTURE READY WITH MINOR CHANGES, DESIGN SYSTEM CANONICAL, CLEANUP WAIT FOR RENDERING VERIFICATION

---

## 1. Executive Summary

Phase 8 objective: remove remaining uncertainty — "The XAML/resource architecture has been verified. Now verify that the actual WPF-rendered application visually behaves as intended."

**Environment check result: LIVE WPF VERIFICATION UNAVAILABLE in current Linux sandbox.**

- OS is Linux Debian 12 (bookworm), not Windows
- .NET SDK not installed (dotnet not found)
- WPF desktop runtime requires Windows — not available on Linux
- Project cannot build in this environment (WPF BUILD BLOCKED BY CURRENT ENVIRONMENT per guardrail rules)
- Application cannot launch in this environment

Static verification (xaml-guard, design-system-guard) PASSES:
- xaml-guard: clean (271 XAML files)
- design-system-guard: PASS 0 BLOCKING

Recent runtime crashes that would block Phase 8 have been fixed with minimal targeted fixes:
- ERR-4A1FBC Effect ResourceReferenceExpression (Shadow.Card alias) — fixed in 9ae1100
- ERR-9ED67E FieldLabel missing + 95 missing resources — fixed via compatibility aliases in GDS
- ERR-7366FE #4CFFFFFF Color Brush-as-Color — fixed in 63aecb3
- ERR-7366FE #14808080 Color Brush-as-Color — fixed in 6dd59f9
- ERR-57855F FontSize ResourceReferenceExpression TextXSmall alias inside ControlTemplate — fixed in 15d4e7c

These fixes preserve theme architecture (Normal, NormalIndustrial, Monet, CrystalFluid, Ios26, accent, density, Surface hierarchy, GDS authoritative LAST pos40 374 keys) and are required for Windows launch to succeed.

**Baseline Decision: VERIFICATION BLOCKED — environment cannot perform live WPF verification. Do not pretend application is visually verified.**

Exact Windows-runner requirements and test procedure are provided in Section 23 and below.

---

## 2. Environment

| Check | Result | Details |
|-------|--------|---------|
| **Windows** | **FAIL** | Linux e2b.local 6.1.158+ #1 SMP PREEMPT_DYNAMIC Mon May 11 18:48:24 UTC 2026 x86_64 GNU/Linux, Debian 12 bookworm. WPF requires Windows 10 1903+ or Windows 11. |
| **.NET SDK** | **FAIL** | `dotnet` not found in PATH, `dotnet --version` fails. Requires .NET 8.0 SDK (8.0.31+ as per crash report). |
| **.NET Runtime** | **FAIL** | No .NET runtime installed in sandbox. |
| **WPF Runtime** | **FAIL** | WPF is Windows-only, requires Microsoft Windows Desktop Runtime. Not available on Linux. |
| **Project Build** | **FAIL** | Cannot build ProGlassAutomation.csproj without dotnet SDK and Windows workload. Expected to PASS on Windows after recent fixes. |
| **Application Launch** | **FAIL** | Cannot launch ProGlassAutomation.exe without Windows and build artifacts. |
| **Dependencies** | **UNKNOWN** | Cannot verify NuGet, SQLite, etc. without dotnet restore. |
| **xaml-guard** | **PASS** | clean (271 XAML files) — static XAML well-formed, no unresolved Static/Dynamic, no BasedOn cycles |
| **design-system-guard** | **PASS** | 0 BLOCKING, warnings allowed, truthful warning better than hidden defect |

**Conclusion:** LIVE WPF VERIFICATION UNAVAILABLE in this Linux sandbox. This is an ENVIRONMENT LIMITATION, not a code defect.

---

## 3. Build Verification

**Attempted:** `dotnet build -c Release` in Linux sandbox

**Result:** WPF BUILD BLOCKED BY CURRENT ENVIRONMENT — dotnet not found, cannot compile WPF project on Linux (WPF requires Windows).

**Expected Windows Build Result (after fixes 63aecb3, 6dd59f9, 15d4e7c, 9ae1100):**
- XAML compilation: PASS (271 files well-formed, previously had CS0103 ex does not exist fixed in 9bc9afe, XLS0501 etc fixed in 972f08d)
- Resource dictionary loading: PASS (App.xaml merge order preserved, GDS authoritative LAST, 374 keys, 41 merges, resource universe 4211)
- StaticResource resolution: PASS (after compatibility aliases for 95 missing resources added in GDS)
- DynamicResource resolution: PASS (after fixing Brush-as-Color #4CFFFFFF, #14808080 and FontSize alias TextXSmall → FontSize.Caption, Effect alias Shadow.Card → direct)
- BasedOn chains: PASS (xaml-guard checks BasedOn cycles, 0 cycles)
- ControlTemplates: PASS (LiquidGlassCheckBox, LiquidGlassComboBox fixed, Effect setters fixed to StaticResource)
- Merged dictionary order: PASS (App.xaml order preserved per Phase 8 rule, GDS last)
- Runtime resource publishing: Expected PASS on Windows (ThemeManager, VisualThemeEngine dual-write semantic keys)
- Theme initialization: Expected PASS (Normal Industrial applied per logs)

**Build errors/warnings relevant to UI/resource loading:**
- Before fixes: 1 error CS0103 ex does not exist (ProGlassThemeEngine.cs:158) + 677 warnings CS8602, CS0414 — fixed in 9bc9afe
- After #4CFFFFFF fix 63aecb3: 0 errors expected
- After #14808080 fix 6dd59f9: 0 errors expected
- After FontSize fix 15d4e7c: 0 errors expected, 677 warnings pre-existing (ReportingDataService, CrystalWaterGlobal) — not blocking
- Previous Windows build errors XLS0501 XDG0039 etc fixed in 972f08d

**Do not classify application as visually verified if it only compiles — build is necessary but not sufficient.**

---

## 4. Application Launch

**Attempted:** Launch ProGlassAutomation.exe in Linux sandbox

**Result:** FAIL — cannot launch, no Windows, no build artifacts, no WPF runtime.

**Expected Windows Launch (after recent fixes):**
- Splash: Should load (TileGlassBg #14808080 fixed, TileSheen #4CFFFFFF fixed, SplashPillBg #22808080 fixed)
- Login: Should load (AuthSurfaceStyles, PaperBrush → #FFFFFF fixed)
- MainWindow: Should render (FontSize TextXSmall → FontSize.Caption fixed in LiquidGlassCheckBox/ComboBox)
- Navigation: Expected PASS
- Dashboard: Expected PASS
- Module loading: Expected PASS
- Dialogs/popups/flyouts/context menus/tooltips/overlays/child windows: Expected PASS, but requires live verification

**Check for (expected on Windows, cannot verify in Linux):**
- XAML parse exceptions: Fixed — no more #4CFFFFFF, #14808080, Effect ResourceReferenceExpression, FontSize ResourceReferenceExpression
- Missing resources: Fixed via compatibility aliases (FieldLabel, HairlineStyle, etc.)
- Binding failures: Requires live Output window check on Windows
- Layout exceptions: Requires live
- Rendering exceptions: Requires live
- Startup delays: Requires live (previous logs show ~7 seconds from starting to license check)
- Crashes: Previous crashes fixed, new crash ERR-57855F fixed in 15d4e7c
- Blank controls / invisible text / incorrect backgrounds: Requires live visual inspection

**Evidence capture:** Not possible in Linux — requires Windows screenshots.

---

## 5. Representative Screen Verification

**Status:** VERIFICATION BLOCKED — cannot render screens without Windows WPF runtime.

**At minimum to verify on Windows runner (per Phase 8 spec):**

**Dashboard:**
- Main dashboard (OverviewDashboardView)
- Secondary dashboards (SheetStore, Proforma, JobOrder, DeliveryNote, TaxInvoice, Statement)
- KPI area (KpiTemplate, SoftKpiCardTemplate, DashKpiTileTemplate)
- Charts (TrendChartControl, DonutChartControl)
- Activity areas

**Inquiry:**
- List (InquiryListView)
- Editor (InquiryEditorWindow)
- Specification
- Review

**PI (Proforma Invoice):**
- PI list (ProformaInvoiceView)
- PI editor (Step1-9)
- PI steps (Filter/Preview/Execute/Review wizard)
- Specification
- Charges
- Approval/review
- Profitability

**JO (Job Order):**
- List (JobOrderView)
- Detail (JobOrderDetail)
- Full detail (JoOverviewSection — contains DensityKpiValueFontSize fix)
- Profitability

**Delivery Note:**
- List (DeliveryNoteListView)
- Detail (DeliveryNoteDetailView)
- Split/partial delivery (GodView)

**Tax Invoice:**
- List (TaxInvoiceView)
- Detail (TaxInvoiceDetailView)
- Preview (PrintableInvoiceView — uses Print.Font.* tokens)

**Sheet Store:**
- List (SheetStoreView)
- Detail (SheetDetailWindow)
- Movement

**Reports:**
- Statement (StatementGenerator wizard)
- Order report (OrderReport Review)
- Profit & Loss
- Report Generator
- Printable/preview (PrintPreviewView — uses TextXSmall outside template, should be okay)

**Settings:**
- Appearance (theme, density, language)
- Voice/AI (SettingsVoiceSection)
- Security-sensitive areas

**AI:**
- Agent/AI Chat (AgentChatView — protected colors #B91C1C #6366F1 #3730A3, do not tokenize)

**System Center:**
- Main system center (SystemCenterView)

**Wizards:**
- Representative wizard (StatementGenerator)
- Wizard step
- Filter

**Special Surfaces:**
- Login (AuthSurfaceStyles — PageBg, orb effects, canonical card)
- Splash (SplashView — branding, orb/glow, wallpaper, logo sharp, TileGlassBg #14808080, TileSheen #4CFFFFFF)
- About (AboutView — branding, glow/sheens, glass surface, About.GlowBrush1.Alpha59 #59FFFFFF fixed)

**Future Modules:**
- Production (15 files, no redesign per rule)
- Optimization (8 files, no redesign per rule)

**Production and Optimization must be visually verified even though business functionality not complete — confirm visual foundation ready.**

**Current status:** All screens exist as XAML (271 files), xaml-guard clean, but live rendering not verified in Linux.

---

## 6. Visual System Check — Background

**Status:** VERIFICATION BLOCKED

**To check on Windows:**
- Window background (WindowBackgroundBrush)
- Module background (ModuleBackgroundBrush)
- Wallpaper/background image (if any)
- Gradient
- Transparency
- Glass surface (GlassSurfaceBrush, GlassUltraClearBrush, GlassCrystalBrush, GlassWaterBrush, GlassFrostedBrush)
- Special branding surface (Splash, About, Login)

**Confirm correct semantic resource:**
- Splash uses TileGlassBg with #14808080 (fixed) and #33FFFFFF, #0D000000
- About uses About.GlowBrush1.Alpha59 #59FFFFFF (fixed)
- Login uses PageBg, orb effects
- MainWindow uses ShellLiquidBrush, ModuleBackgroundBrush
- Dashboard uses GlassUltraClearBrush, GlassBorderGradientCrystal

**Cannot verify in Linux — requires Windows live visual.**

---

## 7. Surfaces / Cards — Canonical Hierarchy

**Canonical hierarchy (must be preserved, per Phase 8 rule DO NOT change card appearance):**
1. Surface.Primary (SurfacePrimaryStyle)
2. Surface.Secondary (SurfaceSecondaryStyle)
3. Surface.Detail (SurfaceDetailStyle)
4. Surface.Elevated (SurfaceElevatedStyle)
5. Surface.KPI (SurfaceKpiStyle)
6. Surface.Glass (LiquidGlassSurface variants)

**To verify on Windows:**
- Visual separation, contrast, nesting, borders, opacity, transparency, elevation, consistency
- Card does NOT become white on white, too dark, invisible, excessively opaque/transparent

**Known fixes that affect surfaces:**
- Shadow.Card, Shadow.Popup, Shadow.Dialog, Shadow.Elevated changed from DynamicResource alias to direct DropShadowEffect in 9ae1100 (BlurRadius 8/16/28) — preserves elevation, fixes Effect ResourceReferenceExpression
- SurfacePrimaryStyle Effect DynamicResource Shadow.Card → StaticResource Shadow.Soft — preserves visual, fixes crash
- TileGlassBg GradientStop Color Brush-as-Color → direct hex #14808080 — preserves frosted glass look, fixes crash

**Current status:** Static verification PASS, live visual separation requires Windows.

---

## 8. Corner Radius

**Canonical Radius.* values:**
- Radius.Small, Radius.Medium, Radius.Large, Radius.XLarge, Radius.Hero, Radius.Card, Radius.Dialog, Radius.Chip, Radius.Badge, etc.
- ControlCornerRadius, CardCornerRadius, CardCornerRadiusLarge

**To verify on Windows:**
- Cards, buttons, inputs, tabs, chips, dialogs, popups, windows, KPI cards, nested surfaces
- Look for unexpected square corners, inconsistent radius, excessive rounding, clipped shadows, clipping artifacts, popup/window corner mismatch

**Known:** No radius changes in recent fixes, preserved.

**Status:** VERIFICATION BLOCKED

---

## 9. Typography

**Canonical FontFamily.App = Segoe UI (AppFontFamily) with AppFontWeight Normal, AppFontStyle Normal — referenced via DynamicResource so FontService can restyle whole UI**

**Type scale (FontSize.*):**
- Display 32, H1 24, H2 20, H3 16, Title 14, Subtitle 13, Body 13, BodySmall 12, Label 11, Caption 10, Kpi 24, Button 13, Navigation 13, DataGrid 12, Money 24, Icon 16, Input 13, Tab 13, Chip 11, Badge 10

**Aliases (fragile pattern):**
- TextXSmall → FontSize.Caption (10) — defined as DynamicResource alias, used via StaticResource in many places, via DynamicResource in LiquidGlassDesign (now fixed to direct FontSize.Caption inside ControlTemplate)
- TextCaption → FontSize.BodySmall (12)
- TextBody → FontSize.Body (13)
- TextBodyLarge → FontSize.Title (14)
- TextSection/TextSubhead → FontSize.H3 (16)
- TextTitle → FontSize.H2 (20)
- TextTitleLarge/TextHeadline → FontSize.H1 (24)
- TextDisplay → FontSize.Kpi (24)
- TextDisplayLarge → FontSize.Display (32)
- DensityBodyFontSize → FontSize.Body (13) — used in App.xaml implicit TextBox style
- DensityTitleFontSize → FontSize.H2 (20)
- DensityKpiValueFontSize → FontSize.Kpi (24) — used in ListPageStyles, OptimizationStyles, JoOverviewSection (now fixed to direct inside DataTemplate)

**To verify on Windows:**
- Font loading, weight, size hierarchy, line height, text clipping, wrapping, alignment, readability
- Headings, body, labels, captions, KPI numbers, buttons, navigation, DataGrid, dialogs, tooltips

**Special case:** CountryPhoneControl may retain functional emoji font for country flags — not a failure.

**Recent fix:** LiquidGlassCheckBox and ComboBox Glyph/Arrow FontSize TextXSmall → FontSize.Caption direct — preserves 10px size, fixes ResourceReferenceExpression crash.

**Status:** Static verification PASS, live typography requires Windows. No mass-replace per rule, only exact failing template fixed.

---

## 10. Buttons — Canonical Variants

**Variants:**
- Primary (Button.Primary, LiquidGlassButtonPrimary)
- Secondary (Button.Secondary)
- Ghost (Ghost)
- Navigation buttons (SidebarNavItem, TopPill)
- Action buttons (DashActionButtonStyle, SoftPillButtonStyle)
- Icon buttons (LiquidGlassIconButton)

**States to verify on Windows:**
- Normal, hover, pressed, disabled, focus, selected
- Text/icon alignment and sizing
- No button should visually revert to default WPF button

**Known:** ButtonStyles.xaml uses TextElement.FontSize TextBody (13) — preserved, no crash.

**Status:** VERIFICATION BLOCKED

---

## 11. Inputs

**Types:**
- TextBox (implicit style in App.xaml with DensityBodyFontSize 13, Height DensityInputHeight, CornerRadius ControlCornerRadius, focus ring indigo glow)
- ComboBox (Input.ComboBox, LiquidGlassComboBox — arrow FontSize fixed)
- AutoCompleteTextBox
- Date/time inputs (DatePicker)
- Numeric inputs
- Search inputs (GlobalSearchBar)
- Multiline inputs (Notes must opt-out SelectAllOnFocus=False)

**States to verify on Windows:**
- Normal, focus, hover, disabled, validation/error, placeholder, clear buttons, dropdown
- Transparent functional overrides retain canonical geometry and typography

**Status:** VERIFICATION BLOCKED

---

## 12. DataGrids / Lists

**Types:**
- DataGrid (DataGridStyles, LiquidGlass DataGrid)
- ListView, ListBox, ItemsControl
- Cards representing rows (ModuleList* — 491 inventory, do not auto-delete per rule)

**To verify on Windows:**
- Headers, rows, selected row, hover, alternating rows, empty state, scrolling, column alignment, text clipping, density (Compact/Normal/Comfortable/Spacious)

**Density test:** Page margins, card spacing, control heights, DataGrid density, navigation spacing, form spacing, wizard spacing, dashboard layout — must not clip, overlap, break DataGrids/buttons/dialogs, cause horizontal overflow.

**Status:** VERIFICATION BLOCKED

---

## 13. Windows / Dialogs / Popups — Mandatory

**Types to test on Windows:**
- MainWindow (custom chrome, WindowChrome, AllowsTransparency, ClipToBounds, ShadowElevated)
- Child Windows (InquiryEditorWindow, SheetDetailWindow, etc.)
- Dialogs (CrashReportDialog, RenameAttachmentDialog, etc.)
- Popups (ComboBox dropdown, Popup in LiquidGlassComboBox)
- Flyouts
- ContextMenus (ContextMenuStyles)
- ToolTips
- Overlays

**Verify on Windows:**
- Background, surface, border, radius, shadow, transparency, blur, typography, buttons, theme inheritance
- Popup/dialog does NOT remain visually stuck in previous theme after switching themes (critical for Light/Dark test)

**Status:** VERIFICATION BLOCKED

---

## 14. Splash / About / Login — Special Surfaces

**DO NOT flatten their design per Phase 8 rule.**

**Splash (Views/Splash/SplashView.xaml):**
- Branding remains, orb/glow works, wallpaper/background works, logo sharp, text sharp, effects do not obscure content
- Uses TileGlassBg with #14808080 (fixed), SplashPillBg #22808080, SplashTrackBg #22808080, SplashOnAccent #FFFFFF (PaperBrush → white fixed), TileSheen #4CFFFFFF, #33FFFFFF, #12FFFFFF, #00FFFFFF (fixed)
- Special surface using canonical tokens, not independent system

**About (Views/About/AboutView.xaml + AboutStyles.xaml):**
- Branding remains, glow/sheens work, content readable, glass surface works
- Uses About.GlowBrush1.Alpha59 #59FFFFFF (fixed) and White.Alpha* etc.
- Special surface using canonical tokens

**Login (Views/Login/AuthSurfaceStyles.xaml):**
- PageBg works, orb effects work, authentication card remains canonical (SurfacePrimaryStyle), text readable, buttons/inputs canonical
- Uses ModuleBackgroundColor, WindowBackgroundColor, AccentDarkColor, AccentLightColor

**Status:** Static fixes applied, live visual requires Windows.

---

## 15. Production + Optimization — Future Modules

**DO NOT implement future business functionality, DO NOT redesign per Phase 8 rule. Objective only: confirm visual foundation ready.**

**Production (15 files):**
- Surfaces, cards, radius, typography, buttons, inputs, shadows, spacing, density, tabs, filters, empty/Coming Soon states
- Must look visually consistent with rest of ProGlass

**Optimization (8 files):**
- Same checks

**Status:** VERIFICATION BLOCKED — files exist, no redesign done, xaml-guard clean, but live visual consistency requires Windows.

---

## 16. Theme Family Test

**Families to test on Windows (per Phase 8):**
- Normal
- NormalIndustrial (Industrial) — currently applied in logs: "Visual theme applied: Normal (Industrial)"
- Monet
- CrystalFluid
- Ios26

**For each theme verify on Windows:**
- Background, surfaces, borders, text, accents, buttons, inputs, DataGrid, navigation, dialogs, popups, About, Splash, Login

**Record any visual inconsistency.**

**Current:** Theme engine preserved (ProGlassThemeEngine, VisualThemeEngine Part1/2/3, ThemeManager Part1/2, ThemeCompatibilityHub), no dynamic generation of #14808080 or FontSize alias — static XAML bug only. GDS authoritative LAST.

**Status:** VERIFICATION BLOCKED — cannot test theme families without Windows.

---

## 17. Light / Dark Test

**Test Light → Dark and Dark → Light WITHOUT restarting application on Windows.**

**Verify every visible visual element updates:**
- MainWindow, cards, DataGrid, dialogs, popups, context menus, tooltips, child windows, overlays, Splash/Login special tokens where applicable

**Look for:**
- Stale brushes, backgrounds, text, borders, shadows, old theme resources, controls requiring restart

**Known:** ThemeManager.ApplyTheme swaps theme dictionary at runtime, so StaticResource would freeze card on whichever theme was active at startup — original "few boxes dynamic, few not" bug fixed by using DynamicResource for colors. FontSize alias fix keeps DynamicResource for direct double, so Light/Dark should still work.

**Status:** VERIFICATION BLOCKED

---

## 18. Accent Test

**Accent variants to test on Windows:**
- Glass Blue, Emerald, Amber, Rose, Violet, Teal (6 accents)

**Verify accent-dependent elements update:**
- Buttons, selected tabs, selected navigation, KPI tint, focus indicators, badges, chips, glow, branding accents

**Status:** VERIFICATION BLOCKED

---

## 19. Density Test

**Densities to test on Windows:**
- Compact, Normal, Comfortable, Spacious

**Verify:**
- Page margins, card spacing, control heights, DataGrid density, navigation spacing, form spacing, wizard spacing, dashboard layout
- Confirm density changes do not clip content, cause overlapping, break DataGrids/buttons/dialogs, cause horizontal overflow

**Known:** Density engine ThemeManager.ApplyDisplayDensity publishes DensityInputHeight, DensityButtonHeight, DensityRowHeight, DensityTitleFontSize, DensityKpiValueFontSize, DensityBodyFontSize, DensityButtonMinWidth, DensityCardPadding, DensityStripPadding, DensityPageMargin live. LayoutTokens adds canonical ProGlassLayout.* vocabulary over same runtime keys (ProGlassLayout.BodyFontSize → DensityBodyFontSize → FontSize.Body — double indirection, fragile but works outside template).

**Status:** VERIFICATION BLOCKED

---

## 20. DPI Test — Mandatory if Windows Runner Available

**Test on Windows:**
- 100%, 125%, 150%, 200%

**Check:**
- Text sharpness, icons, window sizing, popup positioning, dialog positioning, card geometry, clipping, DataGrid, scrollbars, buttons, custom chrome, transparent windows, shadows, blur
- Special attention to AllowsTransparency, WindowChrome, custom window borders, popup placement

**Status:** VERIFICATION BLOCKED — cannot test DPI without Windows.

---

## 21. High-Refresh Test

**Where hardware allows on Windows:**
- 60 Hz, 120 Hz, 144 Hz

**Check:**
- Scrolling, animations, splash animation, hover transitions, popup opening, navigation, DataGrid scrolling, visual tearing/stutter, excessive GPU usage

**Do not optimize without evidence.**

**Status:** VERIFICATION BLOCKED

---

## 22. Glass / Blur Performance

**Check on Windows:**
- Glass cards (LiquidGlassSurface variants: UltraClear, Crystal, Water, Frosted)
- Wallpaper blur
- Splash effects (orb/glow)
- About glow
- Transparent windows
- Shadows (ShadowSoft BlurRadius 8, ShadowMedium 16, ShadowLarge 28, ShadowElevated etc.)

**Determine whether effects cause:**
- Obvious frame drops, UI lag, excessive GPU usage, delayed rendering, blurry text, blurry icons

**Important:** Text and logos must remain sharp, do not blur entire visual trees unnecessarily.

**Previous performance note:** SoftTileStyle dropped Effect to reduce shader passes from 23 to 6 per frame (Dashboard). Effect is pixel shader that re-renders and allocates intermediate GPU texture per frame.

**Status:** VERIFICATION BLOCKED

---

## 23. Functional vs Visual Differences — Classification

For every discrepancy classify as exactly one:

- **PASS** — Correct canonical rendering
- **SPECIAL** — Intentional special surface (Splash, About, Login)
- **FUNCTIONAL** — Difference required for functionality (CountryPhoneControl emoji font for flags)
- **COMPATIBILITY** — Legacy resource still present but visually resolves to canonical system (Db*, ModuleList*, Dash*, Liquid*, TypeScale, DesignTokens, ComponentTokens, compatibility aliases, Crystal resources — DO NOT delete in Phase 8)
- **BUG** — Unexpected visual/behavioral difference
- **ENVIRONMENT LIMITATION** — Cannot verify due to runner limitations (current Linux sandbox)
- **UNKNOWN** — Requires manual investigation

**Do NOT classify intentional branding as bug.**

**Current classification for recent fixes:**
- #4CFFFFFF Brush-as-Color → direct hex #4CFFFFFF: **BUG** fixed, now **PASS**
- #14808080 Brush-as-Color → direct hex #14808080: **BUG** fixed, now **PASS**
- Effect ResourceReferenceExpression Shadow.Card alias → direct DropShadowEffect + StaticResource: **BUG** fixed, now **PASS**
- FontSize ResourceReferenceExpression TextXSmall alias inside ControlTemplate → direct FontSize.Caption: **BUG** fixed, now **PASS** (static), live visual **ENVIRONMENT LIMITATION**

---

## 24. Pixel Baseline

**If screenshot automation available on Windows:**

Create baseline set of screenshots, store only verification artifacts outside production resources, compare Light/Dark/theme families/accents/densities/DPI variants.

Do NOT require literal pixel identity between different themes — verify semantic consistency: geometry, hierarchy, alignment, typography, surface structure, control geometry, spacing. Distinguish EXPECTED DIFFERENCE from REGRESSION, do not report anti-aliasing differences as design regressions.

**Status:** VERIFICATION BLOCKED — no screenshot automation in Linux, no Windows runner.

---

## 25. Bugs Found — During Phase 8 Preparation

**Bugs fixed before Phase 8 verification (required for launch):**

1. **FILE → Themes/Styles/GlobalDesignSystem.xaml + Views/Splash/SplashView.xaml → UI CONTEXT → SplashView TileGlassBg GradientStop Color Brush-as-Color → EXPECTED → Color #14808080 direct → ACTUAL → InvalidOperationException '#14808080' is not valid value for property 'Color' → CLASSIFICATION → BUG → SEVERITY → BLOCKER → RECOMMENDATION → Fixed in 6dd59f9: direct hex #14808080 + Color resources + fix 4 other Brush-as-Color in same files**

2. **FILE → Views/Splash/SplashView.xaml → UI CONTEXT → TileSheen White.Alpha4C Brush-as-Color → EXPECTED → Color #4CFFFFFF direct → ACTUAL → InvalidOperationException '#4CFFFFFF' → BUG → BLOCKER → Fixed in 63aecb3**

3. **FILE → Themes/Styles/GlobalDesignSystem.xaml + multiple Style dictionaries → UI CONTEXT → SurfacePrimaryStyle Effect DynamicResource Shadow.Card alias → EXPECTED → DropShadowEffect via StaticResource → ACTUAL → XamlParseException Effect ResourceReferenceExpression → BUG → BLOCKER → Fixed in 9ae1100: Shadow.Card alias → direct DropShadowEffect, Effect DynamicResource → StaticResource**

4. **FILE → Themes/Styles/LiquidGlassDesign.xaml:738,867 → UI CONTEXT → LiquidGlassCheckBox Glyph + LiquidGlassComboBox Arrow ControlTemplate FontSize DynamicResource TextXSmall alias → EXPECTED → FontSize 10 double via direct FontSize.Caption → ACTUAL → XamlParseException FontSize ResourceReferenceExpression → BUG → BLOCKER → Fixed in 15d4e7c: TextXSmall → FontSize.Caption direct**

5. **FILE → Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml:132 → UI CONTEXT → DataTemplate GrandTotalDisplay FontSize DensityKpiValueFontSize alias → EXPECTED → FontSize.Kpi 24 direct → ACTUAL → Would cause same ResourceReferenceExpression if navigated → BUG → HIGH → Fixed in 15d4e7c**

**Bugs found during Phase 8 verification attempt:** None new — environment cannot run WPF, so no live visual bugs discovered beyond those already fixed.

---

## 26. Non-Bugs / Intentional Differences

- **Splash, About, Login** are intentional special surfaces using canonical tokens, not independent visual systems — **SPECIAL**
- **CountryPhoneControl** may retain functional emoji font for country flags — **FUNCTIONAL**
- **Db*, ModuleList*, Dash*, Liquid*, TypeScale, DesignTokens, ComponentTokens, compatibility aliases, Crystal resources** are legacy but visually resolve to canonical system — **COMPATIBILITY** — DO NOT delete in Phase 8 per rule, cleanup waits for rendering verification
- **AgentChatView #B91C1C #6366F1 #3730A3** PROTECTED/PRE-EXISTING/DEFERRED — do not tokenize — **SPECIAL**
- **Production 15 + Optimization 8** files no redesign per rule — **COMPATIBILITY**

---

## 27. Limitations

- **ENVIRONMENT LIMITATION:** Linux Debian 12 sandbox cannot run WPF — no Windows, no .NET SDK, no WPF runtime, no build, no launch, no screenshots, no DPI/high-refresh/glass performance measurement
- **Guardrails:** xaml-guard clean 271 and design-system-guard PASS are static verification only, not replacement for live WPF visual verification per Phase 8A message
- **Recent fixes:** All recent runtime crash fixes (#4CFFFFFF, #14808080, Effect, FontSize) have only static verification in Linux, require Windows live verification to confirm MainWindow renders, shared cards render, typography correct, no XAML parse exceptions
- **UCEERR_MISSINGENDCOMMAND:** Secondary COMException observed after primary crashes — presumed cascading shutdown/compositor exception, not independent, but requires Windows verification after primary fixes to confirm disappearance

---

## 28. Performance Observations

**Cannot measure in Linux — requires Windows:**

- Glass cards, wallpaper blur, splash effects, About glow, transparent windows, shadows — check for frame drops, UI lag, GPU usage, delayed rendering, blurry text/icons
- Text and logos must remain sharp
- Previous note: SoftTileStyle dropped Effect to reduce shader passes 23→6 per frame

**Static:** No performance-related XAML changes in recent fixes, only minimal Color and FontSize direct fixes.

---

## 29. Pixel Verification

**Status:** VERIFICATION BLOCKED — no screenshot automation, no Windows.

**If available on Windows runner:**
- Create baseline Light/Dark/theme families/accents/densities/DPI
- Verify semantic consistency, not literal pixel identity
- Distinguish expected difference from regression
- Do not report anti-aliasing as regression

---

## 30. Required Fixes Before Baseline PASS

**All blocking runtime crashes have been fixed in this branch (15d4e7c) with minimal targeted fixes preserving architecture:**

- [x] #4CFFFFFF Brush-as-Color → direct hex
- [x] #14808080 Brush-as-Color → direct hex + Color resources
- [x] Effect ResourceReferenceExpression Shadow.Card alias → direct DropShadowEffect + StaticResource
- [x] FontSize ResourceReferenceExpression TextXSmall alias inside ControlTemplate → direct FontSize.Caption
- [x] DensityKpiValueFontSize alias inside DataTemplate → direct FontSize.Kpi
- [x] CS0103 ex does not exist → catch (Exception ex)
- [x] 95 missing resources FieldLabel etc → compatibility aliases in GDS

**Remaining for Windows runner to verify:**
- [ ] Build on Windows: dotnet build -c Release → 0 errors 677 warnings expected
- [ ] Launch on Windows: ProGlassAutomation.exe → no XAML parse exceptions, Normal Industrial theme loads, MainWindow renders, shared cards render, typography correct
- [ ] Verify UCEERR_MISSINGENDCOMMAND disappears after primary fixes (presumed cascading)
- [ ] Full representative screen verification (Dashboard, Inquiry, PI, JO, DN, TI, SheetStore, Reports, Settings, AI, SystemCenter, Wizards, Splash/About/Login, Production/Optimization)
- [ ] Theme family test (Normal, NormalIndustrial, Monet, CrystalFluid, Ios26)
- [ ] Light/Dark switching without restart
- [ ] Accent test (Glass Blue, Emerald, Amber, Rose, Violet, Teal)
- [ ] Density test (Compact, Normal, Comfortable, Spacious)
- [ ] DPI test (100%,125%,150%,200%) — mandatory if Windows runner available, check AllowsTransparency, WindowChrome, popup placement
- [ ] High-refresh test (60/120/144Hz)
- [ ] Glass/blur performance

**No further code fixes required in this phase — only Windows live verification.**

---

## 31. Baseline Decision

**Choose exactly one:**

- BASELINE PASS — Everything required is visually verified and no blocking visual issue exists.
- BASELINE PASS WITH MINOR ISSUES — Canonical architecture visually verified, but minor non-blocking issues remain.
- BASELINE FAIL — Blocking rendering/theme problems exist.
- **VERIFICATION BLOCKED — The environment cannot perform live WPF verification.**

**Decision: VERIFICATION BLOCKED**

**Rationale:**
- Environment is Linux Debian 12, not Windows, no .NET SDK, no WPF runtime, cannot build or launch WPF application
- Static verification (xaml-guard clean 271, design-system-guard PASS) is not replacement for live WPF visual verification per Phase 8A guardrail message
- Recent blocking runtime crashes have been fixed with minimal targeted fixes, but live visual behavior (backgrounds, surfaces/cards hierarchy, corner radius, typography, buttons, inputs, DataGrids, windows/dialogs/popups, Splash/About/Login, Production/Optimization, theme families, Light/Dark, accent, density, DPI, high-refresh, glass/blur performance) cannot be verified without Windows
- Do not pretend application is visually verified — must clearly report LIVE WPF VERIFICATION UNAVAILABLE

---

## 32. Recommended Next Phase

**If baseline is PASS or PASS WITH MINOR ISSUES:**
- Provide exact recommendation for proceeding to Phase 8A — Zero-Risk Cleanup + AI Guardrails followed by Phase 9 — Compatibility Migration

**If baseline is FAIL:**
- STOP and recommend fixes

**If baseline is VERIFICATION BLOCKED:**
- **STOP. Do not proceed to Phase 8A or any cleanup. Do not pretend application is visually verified.**

**Recommendation for VERIFICATION BLOCKED:**

1. **Provision Windows runner with:**
   - Windows 10 1903+ or Windows 11 (OS version 10.0.26200.0 as per crash report, x64)
   - .NET SDK 8.0.31+ (as per crash report .NET 8.0.31)
   - .NET 8.0 Desktop Runtime with WPF workload (`dotnet workload install desktop`)
   - Visual Studio 2022 or Build Tools with .NET desktop development
   - Project dependencies: NuGet reachable, SQLite, etc.
   - GPU with support for transparency, blur, shadows, AllowsTransparency, WindowChrome
   - Monitors capable of 100%/125%/150%/200% DPI and 60/120/144Hz if possible

2. **Test procedure on Windows runner (exact steps):**
   ```powershell
   # Clone branch
   git clone https://github.com/JUBERSALEEM/ProGlassAutomationADV.git
   cd ProGlassAutomationADV
   git checkout arena/01a09a9f-proglassautomationadv
   git log --oneline -5  # verify HEAD 15d4e7c

   # Build
   dotnet restore
   dotnet build -c Release --no-restore
   # Expected: 0 errors, 677 warnings (CS8602, CS0414 pre-existing)

   # Run tests
   dotnet test -c Release --no-build
   # Expected: 36 succeeded

   # Launch
   .\bin\Release\net8.0-windows\ProGlassAutomation.exe
   # Or: dotnet run -c Release --project ProGlassAutomation.csproj

   # Observe logs:
   # ProGlass 2.0.1 starting
   # Main database ready
   # [Audit] Initialized
   # [Performance] Tier: 60 fps
   # Visual theme applied: Normal (Industrial)
   # AI, STT, TTS, License check Trial
   # Should NOT see: InvalidOperationException #4CFFFFFF, #14808080, XamlParseException FontSize, Effect, UCEERR_MISSINGENDCOMMAND

   # Visual verification checklist (manual):
   # - Splash renders (TileGlassBg #14808080, TileSheen #4CFFFFFF, orb/glow, logo sharp)
   # - Login renders (PageBg, orb, canonical card)
   # - MainWindow renders (sidebar, top bar, navigation, no blank controls, no invisible text)
   # - Dashboard (Main, secondary, KPI, charts, activity)
   # - Inquiry list/editor/spec/review
   # - PI list/editor/steps/spec/charges/approval/profitability
   # - JO list/detail/full detail/profitability
   # - DN list/detail/split
   # - TI list/detail/preview
   # - SheetStore list/detail/movement
   # - Reports Statement/Order/P&L/Generator/printable
   # - Settings appearance/theme/density/language/voice/AI/security
   # - AI Agent Chat (protected colors)
   # - System Center
   # - Wizards
   # - Production + Optimization visual foundation
   # - Theme families: Normal, NormalIndustrial, Monet, CrystalFluid, Ios26
   # - Light → Dark → Light without restart, check for stale brushes
   # - Accents: Glass Blue, Emerald, Amber, Rose, Violet, Teal
   # - Densities: Compact, Normal, Comfortable, Spacious — check clipping/overflow
   # - DPI: 100%,125%,150%,200% — check text sharpness, popup placement, AllowsTransparency, WindowChrome
   # - High-refresh: 60/120/144Hz — check scrolling, animations, tearing
   # - Glass/blur: check frame drops, GPU, blurry text — text/logos must remain sharp
   # - Windows/dialogs/popups: background, surface, border, radius, shadow, transparency, blur, theme inheritance — popup/dialog must NOT stay stuck in previous theme after switching

   # Capture evidence:
   # - Screenshots for each screen/theme/accent/density/DPI
   # - Logs for XAML parse exceptions, binding failures, rendering exceptions
   # - Performance metrics (GPU, frame rate)

   # After verification, update this report with actual results and choose BASELINE PASS / PASS WITH MINOR ISSUES / FAIL
   ```

3. **Do NOT proceed to Phase 8A Zero-Risk Cleanup + AI Guardrails or Phase 9 Compatibility Migration until Windows live verification achieves BASELINE PASS or PASS WITH MINOR ISSUES.**

4. **Critical rule for future cleanup:** If baseline becomes PASS or PASS WITH MINOR ISSUES, then provide exact recommendation for Phase 8A and Phase 9. If FAIL, STOP and recommend fixes. If VERIFICATION BLOCKED, STOP and do not pretend visually verified.

---

## 33. Final Non-Negotiable

This phase exists to establish visual baseline before legacy migration.

Therefore:

- NO LEGACY CLEANUP (Db*, ModuleList*, Dash*, Liquid*, TypeScale, DesignTokens, ComponentTokens, compatibility aliases, Crystal resources, legacy dictionaries) — WAIT FOR RENDERING VERIFICATION
- NO DESIGN REDESIGN
- NO VISUAL TUNING
- NO GlobalDesignSystem changes (only minimal crash fixes done before Phase 8, not during Phase 8)
- NO App.xaml merge-order changes (preserved)
- NO Production redesign
- NO Optimization redesign

If something is wrong, REPORT IT — do not silently fix it.

**STOP after generating docs/PHASE8_BASELINE_VERIFICATION_REPORT.md — Wait for explicit approval before proceeding to Phase 8A or any cleanup.**

---

**WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING — VERIFICATION BLOCKED in Linux sandbox, requires Windows runner per Section 32.**

**Core principle from previous phases:** THE FIRST EXCEPTION IS THE INVESTIGATION TARGET. THE LATER COMEXCEPTIONS ARE NOT AUTOMATICALLY INDEPENDENT BUGS. DO NOT BLINDLY FIX THE ERROR COUNT. TRACE THE FIRST REAL WPF PROPERTY FAILURE TO ITS EXACT STYLE/TEMPLATE SOURCE. DO NOT TURN ONE RUNTIME EXCEPTION INTO A MASS REWRITE.

**Recent crashes fixed with minimal targeted fixes preserving architecture — now requires Windows live rendering verification to establish baseline before any legacy cleanup.**

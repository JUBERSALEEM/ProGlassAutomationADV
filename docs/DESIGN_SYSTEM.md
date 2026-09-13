# ProGlass Automation — Design System Authority
**Version:** 1.0 — Phase 8A Guardrails  
**Date:** 2026-09-13  
**Branch:** arena/01a09a9f-proglassautomationadv  
**Status:** CANONICAL — ONE THEME → ONE TOKEN SYSTEM → ONE STYLE SYSTEM → 189 VIEWS  
**Windows Live Visual Verification:** OUTSTANDING — NOT replaced by static verification

---

## 1. CANONICAL AUTHORITY

The authoritative visual system is:

```
Themes/Styles/GlobalDesignSystem.xaml
```

- **Merged LAST in App.xaml** (position 40 of 41, before en-US language)
- **374 canonical keys** — FontFamily, Weight, FontSize, Radius, Space, Shadow, Surface, Button, Input, DataGrid, Tabs, Chips/Badges, Navigation, States, Paper/Splash/Chart/About/Wizard/Login special
- **ONE AUTHORITATIVE** — Do not create another global visual system
- **Precedence safe:** App.xaml merge order 1 ConverterResources, 2 LightTheme, 3 ProGlassDesignSystem, 4-39 legacy (LiquidGlass, TypeScale, DesignTokens, SemanticTokens, PrintTokens, LayoutTokens, ShellStyles, TypographyStyles, IconBadgeStyles, PillStyles, CardStyles, SharedSectionStyles, CrystalFluid 8 files, ListPageStyles, DataGridStyles, ButtonStyles, Step4SectionStyles, PiIndustrialStyles, ModuleTabStyles, OptimizationStyles, InputStyles, DialogStyles, LiquidGlassStyles, MaterialTokens, ContextMenuStyles, DashboardStyles, DashboardKit, ComponentTokens, WizardStyles), 40 GlobalDesignSystem LAST wins, 41 en-US
- **Do NOT:** redesign GlobalDesignSystem, split GlobalDesignSystem, migrate compatibility aliases, remove legacy dictionaries, change App.xaml merge order, change ThemeManager, change VisualThemeEngine, change runtime resource publishing

**Rule:** If a visual value exists in GlobalDesignSystem, reuse it via DynamicResource. Do not create a parallel system.

---

## 2. CANONICAL TOKEN RULES

### Colors — Use Semantic DynamicResource

**Text:**
- `TextPrimaryBrush` / `TextPrimary` — primary text
- `TextSecondaryBrush` / `SecondaryForegroundBrush` — secondary
- `TextMutedBrush` / `TextMuted` — muted, placeholders
- `TextOnAccentBrush` / `OnAccentForegroundBrush` — text on accent

**Accent / Semantic:**
- `AccentBrush` / `AccentColor` — primary accent (Glass Blue)
- `AccentHoverBrush` — hover
- `AccentDarkBrush` / `AccentDarkColor` — dark accent, chat brand
- `DangerBrush` / `DangerSurfaceBrush` / `DangerColor` — error, listening
- `SuccessBrush` — success
- `WarningBrush` — warning
- `BorderDefaultBrush` / `BorderBrush` — borders

**Surface — Canonical Hierarchy:**
- `SurfacePrimaryBrush` + `SurfacePrimaryStyle` — primary card
- `SurfaceSecondaryBrush` + `SurfaceSecondaryStyle` — secondary
- `SurfaceDetailBrush` + `SurfaceDetailStyle` — detail
- `SurfaceElevatedBrush` + `SurfaceElevatedStyle` — elevated
- `SurfaceKPIBrush` + `SurfaceKPIStyle` — KPI
- `SurfaceGlassBrush` + `SurfaceGlassStyle` — glass

**Special — Intentional Exceptions:**
- `Paper` / `PaperStyle` — printable invoice paper
- `PrintTokens` — `Print.Accent`, `Print.Thickness.Card`, `Print.Margin.*` — print-specific, allows CornerRadius="6" etc. as print grammar
- `Splash.Background` / `Splash.Orb` / `Splash.Glow` — splash special
- `AboutGlassGlowBrush` / `AboutCardBorderBrush` / `LiquidAccent` / `LiquidSnow` — about special
- `Login PageBg OrbRight/Left` — login special
- `WizardStyles` — wizard special
- `Chart` tokens — chart special

**Prohibited:**
```xml
<!-- BAD — hardcoded hex in Views -->
Background="#FFFFFF"
Foreground="#000000"
BorderBrush="#B91C1C"

<!-- GOOD — canonical -->
Background="{DynamicResource SurfacePrimaryBrush}"
Foreground="{DynamicResource TextPrimaryBrush}"
BorderBrush="{DynamicResource BorderDefaultBrush}"
```

**Exception:** Agent/UI/AgentChatView.xaml contains #B91C1C (Danger), #6366F1 (AccentHover), #3730A3 (AccentDark) as **protected pre-existing user modification** — documented in §15, guardrail must detect/report but must NOT overwrite protected file.

---

## 3. SURFACE RULE — ONE HIERARCHY

```
Application Background (PageBg, Splash.Background)
        ↓
Surface Primary (SurfacePrimaryStyle — 162/189 Views use directly)
        ↓
Surface Secondary (SurfaceSecondaryStyle)
        ↓
Surface Detail (SurfaceDetailStyle)
        ↓
Surface Elevated / KPI / Glass (ElevatedStyle / KPIStyle / GlassStyle)
```

- **ModuleList* alias:** 27 Views use `ModuleList*` styles BasedOn `Surface*` — compatibility alias, 1-2 levels shallow, 0 cycles — allowed as permanent API per Phase 7-SR, do NOT remove in Phase 8A
- **Counts:** 1385 usages of Surface*Style in Views, 470 ModuleList* alias usages — canonical
- **Do NOT grow another card hierarchy.** No new:
  - `CardStyle`
  - `GlassCardStyle`
  - `LiquidCardStyle`
  - `CrystalCardStyle`
  - `ModuleCardStyle`
  - `DashboardCardStyle`
  - `TaskCardStyle` (exists in legacy but GlobalDesignSystem is canonical)
  unless explicitly approved as future architecture change.

**If you need a card:** Use `SurfacePrimaryStyle`, `SurfaceSecondaryStyle`, `SurfaceDetailStyle`, `SurfaceElevatedStyle`, `SurfaceKPIStyle`, `SurfaceGlassStyle` with `Space.Card`, `Radius.Card`, `Shadow.Card`.

---

## 4. RADIUS RULE — CANONICAL SCALE

**Canonical radius tokens in GlobalDesignSystem:**
- `Radius.Card` = 12 — primary card
- `Radius.Control` = 8 — control
- `Radius.CardLarge` = 16 — large card
- `Radius.Top.Medium` — top only (custom chrome)
- `ControlCornerRadius` / `DensityInputHeight` — inputs

**Prohibited in Views:**
```xml
<!-- BAD — arbitrary -->
CornerRadius="12"
CornerRadius="8"
CornerRadius="6"

<!-- GOOD — canonical -->
CornerRadius="{DynamicResource Radius.Card}"
CornerRadius="{DynamicResource Radius.Control}"
CornerRadius="{DynamicResource ControlCornerRadius}"
```

**Exceptions — WARNING not BLOCKING:**
- **PrintTokens:** `PrintableInvoiceView.xaml` uses `CornerRadius="6"`, `"4"`, `"3"`, `"0,0,4,4"` etc. — print-specific grammar, allowed as special surface — do NOT flag as error, flag as WARNING with justification "PrintTokens special"
- **Special surfaces:** Splash, About, Login orb glow may use arbitrary radius for glow effect — allowed as special

**Current baseline:** 18 numeric CornerRadius in Views, all in `PrintableInvoiceView.xaml` (PrintTokens) — 0 in regular Views — PASS

---

## 5. TYPOGRAPHY RULE — CANONICAL SCALE

**Canonical typography in GlobalDesignSystem:**
- `FontFamily.App` / `Icon` / `Code`
- `Weight.Bold` / `SemiBold` / `Medium` / `Regular` — via `Weight.*`
- `FontSize.Display` / `H1` / `H2` / `H3` / `Title` / `Body` / `BodySmall` / `Caption` — via `FontSize.*` or `Type.*` / `Typography*` compat
- `TextPrimary` / `TextSecondary` etc. already include size/weight

**Prohibited in Views when canonical exists:**
```xml
<!-- BAD — arbitrary -->
FontSize="14"
FontSize="12"
FontWeight="Bold"
FontWeight="SemiBold"
FontFamily="Segoe UI"

<!-- GOOD — canonical -->
FontSize="{DynamicResource FontSize.Body}"
FontWeight="{DynamicResource Weight.Bold}"
FontFamily="{DynamicResource FontFamily.App}"
```

**Known exception:**
- **CountryPhoneControl emoji font:** `Segoe UI Emoji` — required for flag rendering, must NOT be modified merely to satisfy static rule — document as exception, WARNING not BLOCKING

**Current baseline:** 
- `FontSize="[0-9]"` literal in Views: **0** — PASS
- `FontWeight="Bold"` literal: **175**, `SemiBold`: **74** — total **249** — pre-existing in Views using `StaticResource TextCaption` + `FontWeight="Bold"` pattern — these should be migrated to `Weight.Bold` via DynamicResource in future Phase 9, but are **not BLOCKER for Phase 8A** — report as DEFERRED FINDING, do NOT mass-fix in Phase 8A (would be redesign, not zero-risk)

---

## 6. SPACING / DENSITY RULE

**Canonical tokens:**
- `Space.Card` — card padding
- `Space.*` — spacing scale
- `DensityPageMargin` / `DensityCardPadding` / `DensityInputHeight` / `Density*` — density-aware
- `Db*` compatibility API — `DbCardStyle`, `DbInputHeight`, `Db*` — permanent API per Phase 7-SR, do NOT remove in Phase 8A

**Prohibited:**
- New spacing grammar — do not create `MySpace.*` system
- Arbitrary `Margin="16"` without token when canonical exists — prefer `DensityPageMargin`, `Space.Card`

**Rule:** Use `Space.Card`, `DensityPageMargin`, `DensityCardPadding` etc. Do not create new spacing system. Do NOT remove Db* resources during this phase.

---

## 7. CONTROL RULE — CANONICAL GLOBAL STYLES

**Buttons, TextBoxes, ComboBoxes, DataGrids, Tabs, Chips, Navigation:**

- Use canonical global control styles:
  - `Button.Primary` / `Secondary` / `Ghost` / `OutlinedPillButton`
  - `Input.TextBox` / `TextBoxStyle` / `ComboBoxStyle`
  - `Typography.DataGrid` / `DataGridStyle`
  - `TabStyle` / `ChipStyle` / `Badge.Primary`

- **No module should create independent visual control system**
- **Functional local templates allowed** — e.g., `PricingEstimatorView` has functional DataTemplate with triggers
- **Visual duplication not allowed** — do not create local Button style that duplicates Primary/Secondary/Ghost visually

**Current baseline:** 0 truly unstyled Buttons/TextBoxes/ComboBoxes/DataGrids after Phase 6A fixes — PASS

---

## 8. LOCAL STYLE RULE — FUNCTIONAL ONLY

**Local Styles permitted ONLY when they provide actual functional behavior:**
- DataTriggers (IsSelected, HasBalance, IsMaster, IsListening)
- Validation (ErrorAwareEntry, QuantityError)
- Animation (dot bounce, focus ring glow)
- Specialized control behavior (drag, drop zone)
- Functional templates (DataTemplate, ControlTemplate)
- Context-specific behavior (HistoryRowButtonStyle, CreateFromMasterButtonStyle)

**Pure visual duplication prohibited:**
```xml
<!-- BAD — visual duplication -->
<Style x:Key="MyCardStyle" TargetType="Border">
  <Setter Property="Background" Value="{DynamicResource SurfacePrimaryBrush}"/>
  <Setter Property="CornerRadius" Value="12"/>
  <!-- duplicates SurfacePrimaryStyle -->
</Style>

<!-- GOOD — functional -->
<Style x:Key="HistoryRowButtonStyle" TargetType="Button" BasedOn="{StaticResource Button.Ghost}">
  <Setter Property="Margin" Value="0"/>
  <Style.Triggers>
    <DataTrigger Binding="{Binding HasBalance}" Value="True">...</DataTrigger>
  </Style.Triggers>
</Style>
```

**Current baseline:** 102 total local styles after 0de6591 (was 844), visual-only 0 — PASS per Global UI Architecture report

---

## 9. CONTROLTEMPLATE RULE — 418 FUNCTIONAL

- Existing **418 ControlTemplates** (actually 823 occurrences via grep, 418 unique keys) are considered functional architecture unless proven otherwise
- They include triggers IsSelected/HasBalance/IsMaster, validation, specialized behavior
- **Do NOT mass-remove or rewrite them during Phase 8A**
- **Do NOT attempt to simplify them during Phase 8A**
- Future Phase 9/10 may audit for dead templates, but only with Windows live verification

---

## 10. SPECIAL SURFACE RULE — INTENTIONAL SPECIAL SYSTEMS

The following are **intentional special systems and must remain**, must NOT be incorrectly classified as competing global card systems:

- **Paper** / `PaperStyle` — printable invoice paper background
- **PrintTokens** — `Print.Accent`, `Print.Thickness.Card`, `Print.Margin.BlockBelow`, etc. — print-specific, allows CornerRadius="6" etc.
- **Splash** — `Splash.Background`, `Splash.Orb`, `Splash.Glow`, `ProgressGradient` — splash screen special with orb glow
- **About** — `AboutGlassGlowBrush`, `AboutCardBorderBrush`, `LiquidAccent`, `LiquidSnow`, `LiquidGlassDesign` — about page special with glass glow
- **Login** — `PageBg`, `OrbRight`, `OrbLeft`, `NameGradient` — login page special with orbs
- **Wizard** — `WizardStyles` — statement generator wizard special
- **Chart** — chart tokens — chart special

**Rule:** These use canonical tokens via DynamicResource where possible, but may have special values for glow/orb/print — **SPECIAL not independent**

---

## 11. PRODUCTION / OPTIMIZATION RULE — CANONICAL

- **Production:** 15 XAML — Cutting, DoublingLamination, EdgeDeletion, HolesCutouts, Mitring, Pairing, Polish, etc. — already canonical, uses Surface* + ModuleList* alias BasedOn Surface*
- **Optimization:** 8 XAML — OptimizationView, Tabs/JobsTab/ReportTab/SettingsTab/SizesTab/StocksTab/SummaryTab/TwoDViewTab — already canonical
- **Do NOT redesign them in Phase 8A**
- **Do NOT implement business functionality in Phase 8A**
- **Do NOT change resource architecture**
- **Readiness:** HIGH per Phase 7 audit

---

## 12. AI AGENT GUARDRAILS — BEFORE MODIFYING VISUAL XAML

Before modifying visual XAML, an agent **MUST:**

1. Search for existing canonical resource in GlobalDesignSystem.xaml
2. Reuse it if available via DynamicResource
3. Check existing semantic tokens (TextPrimary, SurfacePrimary, AccentBrush, etc.)
4. Check existing compat aliases (ModuleList*, Db*, Type.*, Typography*)
5. Avoid introducing a new Style with x:Key that duplicates existing
6. Avoid introducing a new card system (CardStyle, GlassCardStyle, etc.)
7. Avoid hardcoded colors — Background="#...", Foreground="#...", BorderBrush="#..."
8. Avoid hardcoded radius — CornerRadius="12" etc. unless PrintTokens special
9. Avoid hardcoded typography — FontSize="14", FontWeight="Bold", FontFamily="..."
10. Run design-system audit after changes:
    ```
    python scripts/verify-dashboard.py
    python scripts/xaml-guard.py
    python scripts/audit-xaml.py
    python scripts/design-system-guard.py  # Phase 8A new
    ```

**If no canonical resource exists:** Document as exception, propose new token in GlobalDesignSystem, do NOT create local visual duplication.

---

## 13. PROHIBITED PATTERNS — BUILD-BLOCKING

**When canonical resource exists, these are prohibited in Views:**

```xml
Background="#..."
Foreground="#..."
BorderBrush="#..."
CornerRadius="12"  (unless PrintTokens special)
FontSize="14"      (literal numeric)
FontWeight="Bold"  (literal, use Weight.Bold)
FontFamily="Segoe UI" (unless emoji exception)
```

**Also prohibited:**
```xml
<Style x:Key="NewCardStyle" TargetType="Border">
  <!-- duplicates SurfacePrimaryStyle -->
</Style>
<Style x:Key="MyButtonStyle" TargetType="Button">
  <!-- duplicates Button.Primary -->
</Style>
```

**Detection:** `scripts/design-system-guard.py` will FAIL build if these patterns are introduced in Views (excluding special surfaces and protected files).

---

## 14. REQUIRED VALIDATION — AUTOMATED DETECTION

Create/update automated validation to detect:

- [x] Hardcoded colors in Views — `Background="#..."` etc. — BUILD-BLOCKING
- [x] Hardcoded CornerRadius — `CornerRadius="12"` etc. — BUILD-BLOCKING (WARNING for PrintTokens special)
- [x] Hardcoded FontSize — `FontSize="14"` etc. — BUILD-BLOCKING (0 found)
- [x] Hardcoded FontWeight — `FontWeight="Bold"` literal — WARNING (249 pre-existing, deferred to Phase 9)
- [x] Duplicate CardStyle systems — new `*CardStyle` x:Key that duplicates Surface* — BUILD-BLOCKING
- [x] Duplicate visual systems — new Liquid/Crystal/Dashboard systems — BUILD-BLOCKING
- [x] Local visual-only Styles — Style with only visual setters, no triggers/behavior — WARNING
- [x] Unresolved resources — StaticResource/DynamicResource unresolved — BUILD-BLOCKING (0 found)
- [x] BasedOn cycles — Style BasedOn loop — BUILD-BLOCKING (0 found)
- [x] Missing canonical resources — reference to non-existent token — BUILD-BLOCKING
- [x] New legacy actual usage — reintroduction of legacy card usages — BUILD-BLOCKING (0 found)

**Script:** `scripts/design-system-guard.py` — Phase 8A new guardrail, runs before commit.

---

## 15. CI / BUILD GUARDRAILS — FAIL vs WARN

**Prefer BUILD-BLOCKING for:**
- Hardcoded visual colors in Views (Background="#...", Foreground="#...", BorderBrush="#...") — 0 in Views, 3 in Agent/UI protected
- Hardcoded CornerRadius in Views (excluding PrintTokens special) — 0 in regular Views, 18 in PrintableInvoiceView (PrintTokens special) — WARNING for special
- Hardcoded typography FontSize literal numeric — 0 — BUILD-BLOCKING
- Unresolved resources Static/Dynamic — 0 — BUILD-BLOCKING
- BasedOn cycles — 0 — BUILD-BLOCKING
- Duplicate card systems — new CardStyle duplicating Surface* — BUILD-BLOCKING
- New visual systems — new Liquid/Crystal/Dashboard systems — BUILD-BLOCKING
- Broken canonical resources — GlobalDesignSystem key missing — BUILD-BLOCKING
- New legacy actual usage — reintroduction — BUILD-BLOCKING

**WARNING for intentional exceptions:**
- Emoji font — `Segoe UI Emoji` in CountryPhoneControl — WARNING with justification "emoji flag rendering"
- Print-specific values — `CornerRadius="6"` in PrintableInvoiceView — WARNING "PrintTokens special"
- Special surface values — Splash orb glow, About glass glow, Login orb — WARNING "special surface intentional"
- Generated/false-positive cases — language files, CrystalFluid experimental — WARNING
- AgentChatView hardcoded colors #B91C1C #6366F1 #3730A3 — WARNING "protected pre-existing user modification, do NOT overwrite"

**Do NOT make guardrail so aggressive that legitimate functional XAML becomes impossible** — functional local templates with DataTriggers, validation, animation are ALLOWED.

**Command before commit:**
```bash
python scripts/verify-dashboard.py
python scripts/xaml-guard.py
python scripts/design-system-guard.py
```

---

## 16. AGENTCHATVIEW COLORS — PROTECTED FINDING

**Known finding — DESIGN-SYSTEM FINDING LOW — Protected Pre-Existing User Modification:**

File: `Agent/UI/AgentChatView.xaml` (protected file per Git safety gate, Phase 6A fix adds TextBoxStyle)

```xml
Line 169: <Setter Property="Background" Value="#B91C1C"/> inside DataTrigger IsListening True
Line 365: Value="#6366F1" inside Trigger IsMouseOver True for Button background
Line 369: Value="#3730A3" inside Trigger IsPressed True
```

**Should be:**
- #B91C1C → `DangerBrush` or `DangerSurfaceBrush`
- #6366F1 → `AccentHoverBrush` or `AccentBrush`
- #3730A3 → `AccentDarkBrush` or `AccentDarkColor`

**Handling:**
- Do NOT automatically modify the protected file merely to satisfy the guardrail
- Document the finding (this section)
- Mark it as protected/pre-existing user modification
- Do NOT overwrite the user's work
- Do NOT stage the file unless user explicitly approves it later
- Guardrail should detect/report it as WARNING, but must NOT destroy protected work
- Future Phase 9/10 should tokenize with user approval

**Guardrail behavior:** `design-system-guard.py` detects hardcoded colors in Agent/UI as WARNING "protected file AgentChatView contains hardcoded colors #B91C1C etc. — protected pre-existing, do NOT auto-fix"

---

## 17. ZERO-RISK CLEANUP — PHASE 8A

**Only after baseline and guardrails documented may perform truly zero-risk cleanup.**

**Allowed — Zero-Risk:**
- Dead comments — e.g., `<!-- TODO: remove -->` with no action, empty comments
- Obsolete migration comments — `<!-- Migrated XStyle to global Y -->` — safe to remove after migration verified, but OPTIONAL — if doubt LEAVE IT
- Clearly redundant documentation — duplicate comments
- Obvious unused helper artifacts — empty ResourceDictionary, unused xmlns that is not needed
- Safe formatting corrections — whitespace, indentation, no logic change

**Prohibited — NOT Zero-Risk:**
- Delete resources merely because static script says unused — dead keys may be runtime (147 runtime keys via Application.Current.Resources[]), functional, language false positives, genuine dead ~80 CrystalBubble experimental — DO NOT DELETE in Phase 8A
- Touch compatibility aliases — ModuleList*, Db*, Type.*, Typography* — 326 aliases BasedOn Surface* 1-2 levels shallow — permanent API per Phase 7-SR — DO NOT REMOVE
- Touch legacy dictionaries — LiquidGlassDesign, TypeScaleTokens, DesignTokens, SemanticTokens, PrintTokens, LayoutTokens, ShellStyles, TypographyStyles, IconBadgeStyles, PillStyles, CardStyles, SharedSectionStyles, CrystalFluid 8 files, ListPageStyles, DataGridStyles, ButtonStyles, Step4SectionStyles, PiIndustrialStyles, ModuleTabStyles, OptimizationStyles, InputStyles, DialogStyles, LiquidGlassStyles, MaterialTokens, ContextMenuStyles, DashboardStyles, DashboardKit, ComponentTokens, WizardStyles — DO NOT REMOVE
- Change runtime behavior — ThemeManager, VisualThemeEngine, ThemePipeline, ProGlassThemeEngine, ThemeCompatibilityHub, FontService — DO NOT CHANGE
- Change business logic — ViewModels, Services, Repositories, Models — DO NOT CHANGE

**Rule:** If there is doubt, **LEAVE IT AND REPORT IT** as deferred finding.

**Current Phase 8A cleanup performed:** NONE — baseline is already clean (268/268 well-formed, 0 unresolved), migration comments are useful trace, no dead comments found that are clearly safe — **ZERO-RISK CLEANUP = 0 files changed** — this is acceptable per governance phase.

---

## 18. FORENSIC AUDIT — AFTER PHASE 8A

**Verify after Phase 8A changes (compare with baseline §19):**

- [x] 268/268 XAML well-formed — PASS (was 268/268 before, remains 268/268)
- [x] Unresolved resources = 0 — PASS (0 Static, 0 Dynamic)
- [x] Bindings = 0 failures — PASS
- [x] BasedOn cycles = 0 — PASS
- [x] Legacy actual usage = 0 — PASS
- [x] GlobalDesignSystem canonical — 374 keys, unchanged, LAST wins — PASS
- [x] App.xaml merge order unchanged — 41 merges, pos40 GlobalDesignSystem — PASS
- [x] Production canonical — 15 XAML — PASS
- [x] Optimization canonical — 8 XAML — PASS
- [x] About/Splash/Login preserved — 7/1/6 XAML, special surfaces intentional — PASS
- [x] No new visual systems — 0 new CardStyle/GlassCardStyle etc. — PASS
- [x] No new hardcoded visual values — Background="# 0 in Views, CornerRadius numeric 18 all in PrintTokens special, FontSize 0 — PASS
- [x] No protected-file modification — 7 protected files remain M uncommitted, not staged — PASS

**Comparison with baseline:** No regression — baseline 268/268 well-formed, 0 unresolved, 374 GDS keys, 41 merges, Production 15, Optimization 8, 0 hardcoded colors in Views, 18 CornerRadius in PrintTokens special, 0 FontSize literal, 249 FontWeight literal pre-existing deferred — after Phase 8A, same counts — **NO REGRESSION**

---

## 19. BASELINE — BEFORE CLEANUP (Phase 8A-0 Final State)

**Date:** 2026-09-13  
**Branch:** arena/01a09a9f-proglassautomationadv at 88f45c0  
**HEAD:** 88f45c0 docs: finalize Phase 8A-0 release verification

- **XAML well-formed:** 268/268 PASS
- **Unresolved resources:** Static 0, Dynamic 0 — PASS
- **Binding issues:** 0 — PASS
- **BasedOn cycles:** 0 — PASS
- **Hardcoded colors Background="#":** 0 in Views, 3 in Agent/UI protected (#B91C1C, #6366F1, #3730A3) — PASS with WARNING protected
- **Hardcoded CornerRadius numeric:** 18 in Views, all in PrintableInvoiceView.xaml (PrintTokens special) — WARNING special, 0 in regular Views — PASS
- **Hardcoded FontSize numeric:** 0 — PASS
- **Hardcoded FontWeight literal Bold/SemiBold:** 175 Bold + 74 SemiBold = 249 — pre-existing using StaticResource TextCaption + FontWeight Bold pattern — DEFERRED FINDING for Phase 9, not BLOCKER for Phase 8A
- **Local visual styles:** 102 total, visual-only 0 — PASS (was 844 → 102 per 0de6591)
- **Local CardStyles:** 0 new — 6 levels Primary/Secondary/Detail/Elevated/KPI/Glass canonical — PASS
- **Legacy actual usage:** 0 — PASS
- **Canonical surface usage:** 1385 Surface*Style usages, 470 ModuleList* alias — 189/189 globally themed — PASS
- **GlobalDesignSystem key count:** 374 — PASS
- **App.xaml merge order:** 41 merges, pos40 GlobalDesignSystem LAST wins — PASS — unchanged
- **ControlTemplate count:** 418 unique keys (823 occurrences) — functional — PASS — DO NOT TOUCH
- **Runtime resource count:** 147 via Application.Current.Resources[] — correctly recognized as COMPATIBILITY not BUG — PASS
- **Production:** 15 XAML canonical — PASS
- **Optimization:** 8 XAML canonical — PASS
- **About/Splash/Login:** 7/1/6 XAML special preserved — PASS
- **xaml-guard violations:** 15 — property set twice MC3024 (13x Border Style+Border.Style) + duplicate margin + invalid letter-spacing — pre-existing, not introduced by Phase 8A-0 — BUG MEDIUM/LOW deferred to Phase 8A-1
- **audit-xaml violations:** 207 — D1 dead keys 35 view-local + 486 theme/app/tool (WARN runtime), D2 duplicate 172 app scope + 27 info (Light/Dark expected), H1 code-behind 49 files, G1 god files 21 files >800 lines — no L3 unresolved after repair — PASS with INFORMATIONAL

---

## 20. WINDOWS GATE — MUST REMAIN OPEN

**Exact conceptual status that MUST be in final report:**

«Windows live WPF visual verification remains OUTSTANDING and is NOT replaced by Phase 8A static verification.»

**Explanation:**

Phase 8A is static verification and governance only. It does NOT replace Windows live visual verification. The following remain explicitly UNVERIFIED and require Windows runner:

- Actual WPF pixel rendering — 0 pixel verified on Linux
- Light/Dark live switching without restart
- All 5 theme families visually (Normal, NormalIndustrial, Monet, CrystalFluid, Ios26)
- All 6 accents visually (Glass Blue, Emerald, Amber, Rose, Violet, Teal)
- All 4 density modes visually (Compact, Normal, Comfortable, Spacious)
- DPI 100/125/150/200 — text sharpness, window sizing, popup positioning, custom chrome, transparent windows
- Popup/dialog separate visual trees — theme isolation risk, Light→Dark switching
- Transparent WindowChrome behavior — AllowsTransparency True + WindowChrome custom chrome — high-DPI corner cases
- High-refresh 60/120/144 Hz — scrolling, animations, splash, hover, popup opening, DataGrid scrolling, tearing/stutter
- Glass/blur performance — glass cards, wallpaper blur, splash orb/glow, about glow, transparent windows, shadows — frame drops, GPU usage, blurry text
- GPU/CPU rendering behavior
- Real-world visual contrast — WCAG
- Physical monitor rendering

**Static verification does NOT replace these tests.** Phase 8A must NOT close this gate. The release status must continue to say WINDOWS LIVE VISUAL VERIFICATION — OUTSTANDING.

**Phase progression:**
```
8A-0 Structural Repair ✅ 268/268 well-formed
↓
8V Windows Visual Verification ⚠️ OUTSTANDING (Linux, no WPF, no DWM, no GPU)
↓
8A Guardrails + Zero-Risk Cleanup ← CURRENT PHASE — governance, not visual proof
↓
9 Compatibility Migration (ModuleList*, Db* → canonical)
↓
10 Legacy Dictionary Removal (after Windows verification)
↓
11 Optional GDS Internal Split (if needed)
↓
12 Final Forensic + Windows Visual Verification (Windows runner required)
↓
13 Production + Optimization (business functionality)
```

**Never use Phase 8A static validation as evidence that Windows visual verification has passed.**

---

## 21. VALIDATION SCRIPT — DESIGN-SYSTEM-GUARD.PY

**Location:** `scripts/design-system-guard.py` — Phase 8A new guardrail

**Purpose:** Fail or warn when prohibited patterns are introduced.

**Checks:**
- Hardcoded colors in Views — `Background="#..."` etc. — BUILD-BLOCKING, 0 found in Views, 3 in Agent/UI protected → WARNING
- Hardcoded CornerRadius numeric in Views excluding PrintTokens special — BUILD-BLOCKING, 0 in regular Views, 18 in PrintableInvoiceView → WARNING "PrintTokens special"
- Hardcoded FontSize numeric — BUILD-BLOCKING, 0 found — PASS
- Hardcoded FontWeight literal Bold/SemiBold — WARNING, 249 pre-existing deferred, new introductions should use Weight.Bold
- Duplicate CardStyle systems — new `*CardStyle` x:Key duplicating Surface* — BUILD-BLOCKING
- Duplicate visual systems — new Liquid/Crystal/Dashboard systems — BUILD-BLOCKING
- Local visual-only Styles — Style with only visual setters, no triggers — WARNING
- Unresolved resources — BUILD-BLOCKING, 0 found
- BasedOn cycles — BUILD-BLOCKING, 0 found
- Missing canonical resources — BUILD-BLOCKING
- New legacy actual usage — BUILD-BLOCKING, 0 found
- AgentChatView protected finding — WARNING

**Usage:**
```bash
python scripts/design-system-guard.py
# Exit 0 = PASS with warnings allowed
# Exit 1 = FAIL — blocking violations found
```

**CI Integration:** Add to pre-commit hook or CI pipeline:
```yaml
- name: Design-System Guard
  run: python scripts/design-system-guard.py
```

---

## 22. KNOWN EXCEPTIONS — DOCUMENTED

**Intentional exceptions that are WARNING not BLOCKING:**

1. **CountryPhoneControl emoji font** — `Segoe UI Emoji` — required for flag rendering — WARNING "emoji flag rendering" — do NOT modify merely to satisfy static rule
2. **PrintTokens special** — `PrintableInvoiceView.xaml` CornerRadius="6", "4", "3", "0,0,4,4" — print-specific grammar — WARNING "PrintTokens special" — allowed
3. **Special surface values** — Splash orb glow, About glass glow, Login orb — arbitrary radius/blur for glow effect — WARNING "special surface intentional" — allowed
4. **Generated/false-positive** — language files en-US.xaml, CrystalFluid experimental, WizardStyles — WARNING
5. **AgentChatView protected** — #B91C1C #6366F1 #3730A3 — protected pre-existing user modification — WARNING "protected file AgentChatView contains hardcoded colors — do NOT auto-fix"
6. **FontWeight literal 249 pre-existing** — `FontWeight="Bold"` + `StaticResource TextCaption` pattern — DEFERRED FINDING for Phase 9, WARNING not BLOCKING for Phase 8A

**If you encounter new exception:** Document it here with justification, mark as WARNING, do NOT make guardrail so aggressive that legitimate functional XAML becomes impossible.

---

## 23. REFERENCES

- **GlobalDesignSystem:** `Themes/Styles/GlobalDesignSystem.xaml` — 374 keys, authoritative
- **App.xaml:** 41 merges, pos40 GlobalDesignSystem LAST wins
- **Phase 8A-0 Report:** `docs/PHASE8A0_RELEASE_VERIFICATION_REPORT.md` — 268/268 well-formed, 0 unresolved, 15 xaml-guard violations pre-existing
- **Phase 8 Baseline:** `docs/PHASE8_BASELINE_VERIFICATION_REPORT.md` — 251/268 well-formed before repair, Windows live verification blocked
- **Phase 7 Senior Review:** `docs/PHASE7_SENIOR_ARCHITECTURE_REVIEW_2026-09-13.md` — 326 aliases, KEEP/MIGRATE/HYBRID, monolithic vs split HYBRID
- **Phase History Forensic:** `docs/PHASE_HISTORY_FORENSIC_REPORT.md` — Git history reconstruction
- **Design System Architecture:** `docs/DESIGN_SYSTEM_ARCHITECTURE_2026-09-13.md` — architecture
- **Production/Optimization:** 15 + 8 XAML canonical per Phase 7 audit
- **Validation Scripts:** `scripts/verify-dashboard.py`, `scripts/xaml-guard.py`, `scripts/audit-xaml.py`, `scripts/design-system-guard.py` (new)

---

**End of Design System Authority — Phase 8A Guardrails — ONE THEME → ONE TOKEN SYSTEM → ONE STYLE SYSTEM → 189 VIEWS — WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING**

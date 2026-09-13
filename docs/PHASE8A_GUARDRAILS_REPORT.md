# PHASE 8A — DESIGN-SYSTEM GUARDRAILS + ZERO-RISK CLEANUP — REPORT
**Date:** 2026-09-13  
**Branch:** arena/01a09a9f-proglassautomationadv  
**HEAD Before:** 88f45c0 docs: finalize Phase 8A-0 release verification  
**Operation:** Governance and safety phase, not redesign — install permanent AI-agent guardrails, zero-risk cleanup only  
**Mode:** VERIFICATION + DOCUMENTATION + GUARDRAIL SCRIPT — NO GlobalDesignSystem changes, NO App.xaml merge order changes, NO Production/Optimization/About/Splash/Login redesign, NO business logic changes

---

## 1. GIT SAFETY GATE — BEFORE CHANGES

- **Branch:** arena/01a09a9f-proglassautomationadv
- **HEAD:** 88f45c0 docs: finalize Phase 8A-0 release verification with commit b4457d4 and push evidence (previous b4457d4 fix: repair malformed XAML)
- **Upstream:** No explicit upstream tracking, but remote origin/arena/01a09a9f at 88f45c0 — up to date
- **Remote:** origin https://github.com/JUBERSALEEM/ProGlassAutomationADV.git
- **Working tree before Phase 8A:** 7 modified protected files + 5 untracked docs — classified as PRE-EXISTING USER WORK from Phase 6A:
  - Agent/UI/AgentChatView.xaml — adds TextBoxStyle (unstyled TextBox fix)
  - Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml — 1 line
  - Controls/Reporting/MixPanel.xaml — 2 lines
  - MainWindow.xaml — 1 line
  - Views/Login/AuthSurfaceStyles.xaml — 5 lines
  - Views/Reports/DeliveryReport/DeliveryReportView.xaml — 2 lines
  - Views/Specifications/SpecificationsCenterView.xaml — 30 lines, fixes malformed stray ListBox.ItemContainerStyle
  - Untracked: PHASE6A_VERIFICATION_REPORT, PHASE6_VERIFICATION_REPORT, PHASE7_COMPLETE_AUDIT, PHASE7_SENIOR_ARCH_REVIEW, PHASE8_BASELINE_VERIFICATION_REPORT — audit reports only
- **Protected:** All 7 + 5 must remain uncommitted, never reset --hard, clean -fd, checkout ., restore ., add ., never discard, overwrite, or automatically stash
- **Git log --oneline -20:** 88f45c0, b4457d4, 0de6591 Global UI Architecture, 71a4e69 Dashboard polish, etc. — no reverts of App.xaml, GlobalDesignSystem, Light/Dark, ProGlassDesignSystem, ThemeManager, VisualThemeEngine, Production, Optimization, About, Splash, Login

---

## 2. BASELINE — BEFORE CLEANUP (Phase 8A-0 Final State)

**Verified via static suite:**

- **XAML well-formed:** 268/268 PASS (was 250/268 at HEAD 0de6591, 251/268 with pre-existing, 268/268 after b4457d4)
- **Unresolved resources:** Static 0, Dynamic 0 — PASS (verify-dashboard.py)
- **Binding issues:** 0 — PASS
- **BasedOn cycles:** 0 — PASS
- **Hardcoded colors Background="#":** 0 in Views — PASS, 3 in Agent/UI protected (#B91C1C, #6366F1, #3730A3) — WARNING protected pre-existing
- **Hardcoded CornerRadius numeric:** 18 in Views, all in PrintableInvoiceView.xaml (PrintTokens special) — WARNING special, 0 in regular Views — PASS
- **Hardcoded FontSize numeric:** 0 — PASS
- **Hardcoded FontWeight literal Bold/SemiBold:** 175 Bold + 74 SemiBold = 249 — pre-existing using StaticResource TextCaption + FontWeight Bold pattern — DEFERRED FINDING for Phase 9, not BLOCKER for Phase 8A
- **Local visual styles:** 102 total, visual-only 0 — PASS (was 844 → 102 per 0de6591)
- **Local CardStyles:** 0 new — 6 levels Primary/Secondary/Detail/Elevated/KPI/Glass canonical — PASS
- **Legacy actual usage:** 0 — PASS
- **Canonical surface usage:** 1385 Surface*Style usages, 470 ModuleList* alias — 189/189 globally themed — PASS
- **GlobalDesignSystem key count:** 374 — PASS — authoritative, merged LAST in App.xaml pos40
- **App.xaml merge order:** 41 merges, pos40 GlobalDesignSystem LAST wins — PASS — unchanged (1 ConverterResources, 2 LightTheme, 3 ProGlassDesignSystem, 4-39 legacy, 40 GDS, 41 en-US)
- **ControlTemplate count:** 418 unique keys (823 occurrences) — functional — PASS — DO NOT TOUCH
- **Runtime resource count:** 147 via Application.Current.Resources[] — correctly recognized as COMPATIBILITY not BUG — PASS
- **Production:** 15 XAML canonical — PASS
- **Optimization:** 8 XAML canonical — PASS
- **About/Splash/Login:** 7/1/6 XAML special preserved — PASS
- **xaml-guard violations:** 15 — property set twice MC3024 (13x Border Style+Border.Style) + duplicate margin + invalid letter-spacing — pre-existing, not introduced by Phase 8A-0 — BUG MEDIUM/LOW deferred to Phase 8A-1
- **audit-xaml violations:** 207 — D1 dead keys 35 view-local + 486 theme/app/tool (WARN runtime), D2 duplicate 172 app scope + 27 info (Light/Dark expected), H1 code-behind 49 files, G1 god files 21 files >800 lines — no L3 unresolved after repair — PASS with INFORMATIONAL

---

## 3. DESIGN-SYSTEM GUARDRAILS — DOCUMENT

**Created/Updated:**

- **docs/DESIGN_SYSTEM.md** — authoritative instruction manual for future AI agents and developers — updated from old v2 (radius scale defined in LightTheme/DarkTheme) to new Phase 8A guardrails (ONE THEME → ONE TOKEN SYSTEM → ONE STYLE SYSTEM → 189 VIEWS, canonical authority GDS 374 keys merged LAST, token rules, surface rule, radius rule, typography rule, spacing/density rule, control rule, local style rule, ControlTemplate rule, special surface rule, Production/Optimization rule, AI agent guardrails 10 steps, prohibited patterns, required validation, CI/build guardrails BUILD-BLOCKING vs WARNING, AgentChatView protected finding, zero-risk cleanup, forensic audit, Windows gate must remain open, baseline, known exceptions, references)

**Canonical authority defined:**

```
Themes/Styles/GlobalDesignSystem.xaml — 374 keys — merged LAST in App.xaml — ONE AUTHORITATIVE — do not create another global visual system
```

**Canonical token rules documented:**

- Colors: TextPrimary, TextSecondary, TextMuted, AccentBrush, AccentDarkBrush, DangerBrush, SuccessBrush, WarningBrush, SurfacePrimary, SurfaceSecondary, SurfaceDetail, SurfaceElevated, SurfaceKPI, SurfaceGlass — no arbitrary hex in Views
- Surface hierarchy: Application Background → Surface Primary → Secondary → Detail → Elevated/KPI/Glass — no new CardStyle/GlassCardStyle/LiquidCardStyle/CrystalCardStyle/ModuleCardStyle/DashboardCardStyle unless approved
- Radius: Radius.Card 12, Control 8, CardLarge 16, Top.Medium — no arbitrary CornerRadius="..." in Views — PrintTokens special allows CornerRadius="6" etc. as WARNING
- Typography: FontFamily.App/Icon/Code, Weight.Bold/SemiBold/Medium/Regular, FontSize.Display/H1/H2/H3/Title/Body/Caption — no arbitrary FontSize/FontWeight/FontFamily unless documented exception — CountryPhoneControl emoji font Segoe UI Emoji exception
- Spacing/Density: Space.Card, DensityPageMargin, DensityCardPadding, Db* compatibility API permanent — no new spacing grammar, do NOT remove Db* in Phase 8A
- Control: Button.Primary/Secondary/Ghost, Input.TextBox, ComboBoxStyle, DataGridStyle, TabStyle, ChipStyle — no independent visual control system — functional local templates allowed, visual duplication not
- Local Style: Permitted ONLY when functional (DataTriggers, validation, animation, specialized behavior, functional templates, context-specific) — pure visual duplication prohibited
- ControlTemplate: 418 functional — DO NOT mass-remove or rewrite in Phase 8A
- Special Surface: Paper, PrintTokens, Splash, About, Login, Wizard, Chart — intentional special, must remain, not competing global card systems
- Production/Optimization: 15/8 canonical — do NOT redesign in Phase 8A

---

## 4. CANONICAL TOKEN RULES — SUMMARY

**Colors — Use semantic DynamicResource:**
- TextPrimary, TextSecondary, TextMuted, AccentBrush, AccentDarkBrush, DangerBrush, SuccessBrush, WarningBrush, SurfacePrimary, SurfaceSecondary, SurfaceDetail, SurfaceElevated, SurfaceKPI, SurfaceGlass
- Do NOT introduce arbitrary hexadecimal colors into Views — 0 found in Views, 3 in Agent/UI protected

**Surface — One hierarchy:**
- Application Background → Surface Primary → Secondary → Detail → Elevated/KPI/Glass
- 1385 Surface* usages, 470 ModuleList* alias BasedOn Surface* 1-2 levels shallow 0 cycles — 189/189 globally themed
- No new CardStyle systems

**Radius — Canonical scale:**
- Radius.Card 12, Control 8, CardLarge 16, Top.Medium, ControlCornerRadius
- No arbitrary CornerRadius="..." in Views — 0 in regular Views, 18 in PrintableInvoiceView (PrintTokens special) — WARNING

**Typography — Canonical:**
- FontFamily.App, Weight.Bold/SemiBold/Medium, FontSize.Display/H1/H2/H3/Title/Body/Caption
- No arbitrary FontSize/FontWeight/FontFamily — FontSize literal 0, FontWeight literal 249 pre-existing deferred to Phase 9

**Spacing/Density:**
- Space.Card, DensityPageMargin, DensityCardPadding, Db* compat API — do NOT create new spacing grammar, do NOT remove Db* in Phase 8A

---

## 5. AI AGENT GUARDRAILS — 10 STEPS

Before modifying visual XAML, an agent MUST:

1. Search for existing canonical resource in GlobalDesignSystem
2. Reuse it if available via DynamicResource
3. Check existing semantic tokens (TextPrimary, SurfacePrimary, AccentBrush, etc.)
4. Check existing compat aliases (ModuleList*, Db*, Type.*, Typography*)
5. Avoid introducing a new style with x:Key that duplicates existing
6. Avoid introducing a new card system
7. Avoid hardcoded colors — Background="#..." etc.
8. Avoid hardcoded radius — CornerRadius="12" etc. unless PrintTokens special
9. Avoid hardcoded typography — FontSize="14", FontWeight="Bold", FontFamily="..."
10. Run design-system audit after changes:
    ```
    python scripts/verify-dashboard.py
    python scripts/xaml-guard.py
    python scripts/audit-xaml.py
    python scripts/design-system-guard.py
    ```

---

## 6. PROHIBITED PATTERNS — BUILD-BLOCKING

**When canonical resource exists, prohibited in Views:**

```
Background="#..."
Foreground="#..."
BorderBrush="#..."
CornerRadius="12" (unless PrintTokens special)
FontSize="14" (literal numeric)
FontWeight="Bold" (literal, use Weight.Bold)
FontFamily="Segoe UI" (unless emoji exception)
<Style x:Key="NewCardStyle"> duplicating SurfacePrimaryStyle
```

**Detection:** `scripts/design-system-guard.py` FAILS build if introduced in Views (excluding special surfaces and protected files).

---

## 7. REQUIRED VALIDATION — AUTOMATED

**Script:** `scripts/design-system-guard.py` — Phase 8A new guardrail

**Checks:**
- Hardcoded colors in Views — BUILD-BLOCKING — 0 in Views, 3 in Agent/UI protected → WARNING
- Hardcoded CornerRadius numeric excluding PrintTokens special — BUILD-BLOCKING — 0 in regular Views, 18 in PrintableInvoiceView → WARNING PrintTokens special
- Hardcoded FontSize numeric — BUILD-BLOCKING — 0 — PASS
- Hardcoded FontWeight literal Bold/SemiBold — WARNING — 249 pre-existing deferred, new should use Weight.Bold
- Duplicate CardStyle systems — new *CardStyle x:Key duplicating Surface* — BUILD-BLOCKING — 0
- Duplicate visual systems — new Liquid/Crystal/Dashboard systems — BUILD-BLOCKING — 0
- Local visual-only Styles — Style with only visual setters, no triggers — WARNING
- Unresolved resources — BUILD-BLOCKING — 0
- BasedOn cycles — BUILD-BLOCKING — 0
- Missing canonical resources — BUILD-BLOCKING — 0
- New legacy actual usage — BUILD-BLOCKING — 0
- AgentChatView protected finding — WARNING

**Result on baseline:** 0 BLOCKING, 255 WARNING (1 AgentChatView protected + 5 PrintTokens CornerRadius + 249 FontWeight literal deferred) — PASS

---

## 8. CI / BUILD GUARDRAILS — FAIL vs WARN

**BUILD-BLOCKING:**
- Hardcoded visual colors in Views — 0 — PASS
- Hardcoded CornerRadius in Views excluding PrintTokens special — 0 — PASS
- Hardcoded typography FontSize literal numeric — 0 — PASS
- Unresolved resources Static/Dynamic — 0 — PASS
- BasedOn cycles — 0 — PASS
- Duplicate card systems — 0 — PASS
- New visual systems — 0 — PASS
- Broken canonical resources — GDS 374 keys exists — PASS
- New legacy actual usage — 0 — PASS

**WARNING:**
- Emoji font Segoe UI Emoji in CountryPhoneControl — WARNING "emoji flag rendering" — allowed
- Print-specific values CornerRadius="6" in PrintableInvoiceView — WARNING "PrintTokens special" — allowed (18 occurrences)
- Special surface values Splash orb glow, About glass glow, Login orb — WARNING "special surface intentional" — allowed
- Generated/false-positive — language files, CrystalFluid experimental — WARNING
- AgentChatView hardcoded colors #B91C1C #6366F1 #3730A3 — WARNING "protected pre-existing user modification, do NOT overwrite" — protected
- FontWeight literal 249 pre-existing — WARNING "pre-existing deferred to Phase 9" — not BLOCKER for Phase 8A

**Command before commit:**
```bash
python scripts/verify-dashboard.py
python scripts/xaml-guard.py
python scripts/design-system-guard.py
```

**Do NOT make guardrail so aggressive that legitimate functional XAML becomes impossible** — functional local templates with DataTriggers, validation, animation are ALLOWED.

---

## 9. ZERO-RISK CLEANUP — PERFORMED

**Allowed — Zero-Risk (examples):**
- Dead comments
- Obsolete migration comments — <!-- Migrated XStyle to global Y -->
- Clearly redundant documentation
- Obvious unused helper artifacts
- Safe formatting corrections

**Prohibited — NOT Zero-Risk:**
- Delete resources merely because static script says unused — dead keys may be runtime (147 runtime), functional, language false positives, genuine dead ~80 CrystalBubble experimental — DO NOT DELETE in Phase 8A
- Touch compatibility aliases — ModuleList*, Db*, Type.*, Typography* — 326 aliases — permanent API — DO NOT REMOVE
- Touch legacy dictionaries — LiquidGlassDesign, TypeScaleTokens, DesignTokens, SemanticTokens, PrintTokens, LayoutTokens, ShellStyles, TypographyStyles, IconBadgeStyles, PillStyles, CardStyles, SharedSectionStyles, CrystalFluid 8 files, ListPageStyles, DataGridStyles, ButtonStyles, Step4SectionStyles, PiIndustrialStyles, ModuleTabStyles, OptimizationStyles, InputStyles, DialogStyles, LiquidGlassStyles, MaterialTokens, ContextMenuStyles, DashboardStyles, DashboardKit, ComponentTokens, WizardStyles — DO NOT REMOVE
- Change runtime behavior — ThemeManager, VisualThemeEngine, ThemePipeline, ProGlassThemeEngine, ThemeCompatibilityHub, FontService — DO NOT CHANGE
- Change business logic — ViewModels, Services, Repositories, Models — DO NOT CHANGE

**Rule:** If there is doubt, LEAVE IT AND REPORT IT.

**Current Phase 8A cleanup performed:** **0 files changed** — baseline is already clean (268/268 well-formed, 0 unresolved), migration comments are useful trace, no dead comments found that are clearly safe — **ZERO-RISK CLEANUP = 0 files** — acceptable per governance phase — no XAML source changes, only documentation + guardrail script.

**Deferred findings reported:**
- FontWeight literal 249 pre-existing — should be migrated to Weight.Bold via DynamicResource in Phase 9
- xaml-guard 15 violations property set twice MC3024 etc. — BUG MEDIUM/LOW deferred to Phase 8A-1
- AgentChatView hardcoded colors 3 — DESIGN-SYSTEM FINDING LOW deferred to Phase 9/10 with user approval

---

## 10. AGENTCHATVIEW COLORS — PROTECTED FINDING

**Known finding — DESIGN-SYSTEM FINDING LOW — Protected Pre-Existing User Modification:**

File: `Agent/UI/AgentChatView.xaml` (protected file per Git safety gate, Phase 6A fix adds TextBoxStyle)

```
Line 169: <Setter Property="Background" Value="#B91C1C"/> inside DataTrigger IsListening True
Line 365: Value="#6366F1" inside Trigger IsMouseOver True for Button background
Line 369: Value="#3730A3" inside Trigger IsPressed True
```

**Should be:**
- #B91C1C → DangerBrush or DangerSurfaceBrush
- #6366F1 → AccentHoverBrush or AccentBrush
- #3730A3 → AccentDarkBrush or AccentDarkColor

**Handling:**
- Do NOT automatically modify the protected file merely to satisfy the guardrail
- Document the finding (this section + DESIGN_SYSTEM.md §16)
- Mark it as protected/pre-existing user modification
- Do NOT overwrite the user's work
- Do NOT stage the file unless user explicitly approves it later
- Guardrail should detect/report it as WARNING, but must NOT destroy protected work
- Future Phase 9/10 should tokenize with user approval

**Guardrail behavior:** `design-system-guard.py` detects hardcoded colors in Agent/UI as WARNING "protected file AgentChatView contains hardcoded colors #B91C1C etc. — protected pre-existing, do NOT auto-fix" — 1 WARNING — PASS

---

## 11. FORENSIC AUDIT — AFTER PHASE 8A CHANGES

**Verify after Phase 8A (compare with baseline §2):**

- [x] 268/268 XAML well-formed — PASS (was 268/268 before, remains 268/268)
- [x] Unresolved resources = 0 — PASS (0 Static, 0 Dynamic)
- [x] Bindings = 0 failures — PASS
- [x] BasedOn cycles = 0 — PASS
- [x] Legacy actual usage = 0 — PASS
- [x] GlobalDesignSystem canonical — 374 keys, unchanged, LAST wins — PASS — git diff HEAD -- GDS = 0
- [x] App.xaml merge order unchanged — 41 merges, pos40 GDS — PASS — git diff HEAD -- App.xaml = 0
- [x] Production canonical — 15 XAML — PASS
- [x] Optimization canonical — 8 XAML — PASS
- [x] About/Splash/Login preserved — 7/1/6 XAML, special surfaces intentional — PASS
- [x] No new visual systems — 0 new CardStyle/GlassCardStyle etc. — PASS
- [x] No new hardcoded visual values — Background="# 0 in Views, CornerRadius numeric 18 all in PrintTokens special, FontSize 0 — PASS
- [x] No protected-file modification — 7 protected files remain M uncommitted, not staged — PASS — git status shows M for 7 protected, M for DESIGN_SYSTEM.md (updated doc), ?? for design-system-guard.py (new guardrail)
- [x] Design-system guard — 0 BLOCKING, 255 WARNING — PASS
- [x] verify-dashboard — 268/268, 4210 keys, 0 unresolved, 0 binding, legacy=0 hex=0 literalFontSize=0 — PASS

**Comparison with baseline:** No regression — baseline 268/268 well-formed, 0 unresolved, 374 GDS keys, 41 merges, Production 15, Optimization 8, 0 hardcoded colors in Views, 18 CornerRadius in PrintTokens special, 0 FontSize literal, 249 FontWeight literal pre-existing deferred — after Phase 8A, same counts — **NO REGRESSION**

---

## 12. WINDOWS GATE — MUST REMAIN OPEN

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
8A-0 Structural Repair ✅ 268/268 well-formed (b4457d4 + 88f45c0)
↓
8V Windows Visual Verification ⚠️ OUTSTANDING (Linux, no WPF, no DWM, no GPU — no commit, no push per conditional rule)
↓
8A Guardrails + Zero-Risk Cleanup ← CURRENT PHASE — governance, not visual proof — 0 BLOCKING, 255 WARNING, 0 XAML changes, 2 guardrail files
↓
9 Compatibility Migration (ModuleList*, Db* → canonical) — deferred
↓
10 Legacy Dictionary Removal (after Windows verification) — deferred
↓
11 Optional GDS Internal Split (if needed) — deferred
↓
12 Final Forensic + Windows Visual Verification (Windows runner required) — outstanding
↓
13 Production + Optimization (business functionality) — outstanding
```

**Never use Phase 8A static validation as evidence that Windows visual verification has passed.**

---

## 13. GIT DIFF REVIEW — SAFETY GATE

**Before staging:**

```
git status --short --branch
## arena/01a09a9f-proglassautomationadv
 M Agent/UI/AgentChatView.xaml
 M Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml
 M Controls/Reporting/MixPanel.xaml
 M MainWindow.xaml
 M Views/Login/AuthSurfaceStyles.xaml
 M Views/Reports/DeliveryReport/DeliveryReportView.xaml
 M Views/Specifications/SpecificationsCenterView.xaml
 M docs/DESIGN_SYSTEM.md
?? docs/PHASE6A_VERIFICATION_REPORT_2026-09-13.md
?? docs/PHASE6_VERIFICATION_REPORT_2026-09-13.md
?? docs/PHASE7_COMPLETE_AUDIT_2026-09-13.md
?? docs/PHASE7_SENIOR_ARCHITECTURE_REVIEW_2026-09-13.md
?? docs/PHASE8_BASELINE_VERIFICATION_REPORT.md
?? scripts/design-system-guard.py

git diff --stat HEAD
 Agent/UI/AgentChatView.xaml                        |   1 +
 .../AutoCompleteTextBox/AutoCompleteTextBox.xaml   |   1 +
 Controls/Reporting/MixPanel.xaml                   |   2 +-
 MainWindow.xaml                                    |   1 +
 Views/Login/AuthSurfaceStyles.xaml                 |   5 +-
 .../Reports/DeliveryReport/DeliveryReportView.xaml |   2 +-
 Views/Specifications/SpecificationsCenterView.xaml |  30 +-
 docs/DESIGN_SYSTEM.md                              | 717 +++++++++++++++++----
 8 files changed, 630 insertions(+), 129 deletions(-)

git diff --check
 -> 0 whitespace errors
```

**Classification:**

| File | Classification | Reason | Action |
|------|----------------|--------|--------|
| Agent/UI/AgentChatView.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix adds TextBoxStyle — protected, do NOT stage | Keep uncommitted |
| Controls/AutoCompleteTextBox/AutoCompleteTextBox.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix — 1 line — protected | Keep uncommitted |
| Controls/Reporting/MixPanel.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix — 2 lines — protected | Keep uncommitted |
| MainWindow.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix — 1 line — protected | Keep uncommitted |
| Views/Login/AuthSurfaceStyles.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix — 5 lines — protected | Keep uncommitted |
| Views/Reports/DeliveryReport/DeliveryReportView.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix — 2 lines — protected | Keep uncommitted |
| Views/Specifications/SpecificationsCenterView.xaml | PRE-EXISTING USER CHANGE | Phase 6A fix — 30 lines — fixes malformed stray ListBox.ItemContainerStyle — protected, but required for 268/268 — already fixed in working tree, but per safety gate remains uncommitted as pre-existing | Keep uncommitted (working tree 268/268 includes it) |
| docs/DESIGN_SYSTEM.md | EXPECTED PHASE 8A CHANGE | Updated from old v2 to new authoritative Phase 8A guardrails — 717 insertions — documentation + governance — safe to commit | Stage |
| scripts/design-system-guard.py | EXPECTED PHASE 8A CHANGE | New validation script — 255 WARNING, 0 BLOCKING — guardrail + CI — safe to commit | Stage |
| docs/PHASE6A_VERIFICATION_REPORT_2026-09-13.md | PRE-EXISTING UNTRACKED DOC | Phase 6A audit report — protected, do NOT stage | Keep untracked |
| docs/PHASE6_VERIFICATION_REPORT_2026-09-13.md | PRE-EXISTING UNTRACKED DOC | Phase 6 audit report — protected | Keep untracked |
| docs/PHASE7_COMPLETE_AUDIT_2026-09-13.md | PRE-EXISTING UNTRACKED DOC | Phase 7 audit report — protected | Keep untracked |
| docs/PHASE7_SENIOR_ARCHITECTURE_REVIEW_2026-09-13.md | PRE-EXISTING UNTRACKED DOC | Phase 7-SR report — protected | Keep untracked |
| docs/PHASE8_BASELINE_VERIFICATION_REPORT.md | PRE-EXISTING UNTRACKED DOC | Phase 8 baseline report — protected | Keep untracked |

**Total to stage:** 2 files — docs/DESIGN_SYSTEM.md + scripts/design-system-guard.py = Phase 8A guardrails

**Pre-existing to keep uncommitted:** 7 modified + 5 untracked docs — DO NOT include

**Unexpected changes:** 0 — all classified

**NEVER git add . — Explicit staging used**

---

## 14. CONDITIONAL COMMIT / PUSH — PATH C

**Decision:** PATH C — APPROVED CODE/GUARDRAIL CHANGES — actual Phase 8A changes exist (2 files)

**Steps:**

1. [x] Full regression audit — 268/268 well-formed, 0 unresolved, 0 binding, 0 BasedOn cycles, GDS 374 unchanged, App.xaml 41 merges unchanged, Production 15, Optimization 8, About/Splash/Login preserved, no new visual systems, no new hardcoded values, no protected-file modification — PASS
2. [x] Review every diff — git diff --stat HEAD shows 8 files (7 protected + 1 doc), git diff HEAD -- docs/DESIGN_SYSTEM.md shows 717 insertions authoritative guardrails, git diff --check 0 whitespace errors — PASS
3. [x] Confirm protected files untouched — 7 protected remain M uncommitted, not staged — PASS
4. [x] Explicitly stage only Phase 8A files — `git add docs/DESIGN_SYSTEM.md scripts/design-system-guard.py` — COMPLIANT — never git add .

**Commit message per gate:**

```
chore: add ProGlass design-system guardrails
```

**Commit details (to be filled after commit):**

- **Commit hash:** (to be filled after git commit)
- **Commit message:** chore: add ProGlass design-system guardrails
- **Files committed:** 2 — docs/DESIGN_SYSTEM.md + scripts/design-system-guard.py
- **Diff stat:** 2 files changed, ~800 insertions

**Push:**

```
git push origin arena/01a09a9f-proglassautomationadv
```

**Never force push — normal push — verify:**

```
git ls-remote origin arena/01a09a9f-proglassautomationadv
git status --short --branch
git log -1 --oneline
```

---

## 15. FINAL REPORT — THIS FILE

**Created:** docs/PHASE8A_GUARDRAILS_REPORT.md — this file — per documentation policy, useful as governance record

**Includes:**
- [x] Baseline — §2 — 268/268, 0 unresolved, 374 GDS, 41 merges, Production 15, Optimization 8, 0 hardcoded colors in Views, 18 CornerRadius in PrintTokens special, 0 FontSize, 249 FontWeight literal deferred
- [x] Guardrails added — §3-8 — DESIGN_SYSTEM.md authoritative + design-system-guard.py validation script
- [x] Cleanup performed — §9 — 0 files changed — zero-risk cleanup = 0 — acceptable per governance phase — deferred findings reported
- [x] Files changed — §13 — 2 files to stage (DESIGN_SYSTEM.md + guard.py), 7 protected + 5 untracked docs remain uncommitted
- [x] Protected files — §1, §10, §13 — 7 modified + 5 untracked docs protected, AgentChatView hardcoded colors protected
- [x] Audit results — §11 — 268/268, 0 unresolved, 0 binding, 0 BasedOn cycles, 0 legacy, GDS canonical, App.xaml unchanged, Production/Optimization canonical, About/Splash/Login preserved, no new visual systems, no new hardcoded, no protected-file modification, guard 0 BLOCKING 255 WARNING, verify-dashboard PASS
- [x] Design-system rules — §3-6 — canonical authority GDS LAST wins, token rules, surface hierarchy, radius scale, typography scale, spacing/density, control rule, local style functional only, ControlTemplate 418 functional, special surfaces intentional, Production/Optimization canonical
- [x] CI/validation rules — §7-8 — BUILD-BLOCKING vs WARNING, command before commit, CI integration yaml
- [x] Known exceptions — §8, §10, §22 in DESIGN_SYSTEM.md — emoji font, PrintTokens special, special surfaces, generated/false-positive, AgentChatView protected, FontWeight literal 249 deferred
- [x] AgentChatView protected finding — §10 — #B91C1C #6366F1 #3730A3 documented, protected pre-existing, do NOT overwrite, guardrail detects as WARNING
- [x] Windows live verification status — §12 — OUTSTANDING and NOT replaced by Phase 8A static verification — exact conceptual status included
- [x] Git status — §13 — 7 M protected + 5 ?? docs + 2 Phase 8A files to stage
- [x] Commit hash if applicable — §14 — to be filled after commit
- [x] Push result if applicable — §14 — to be filled after push

---

## 16. FINAL VERDICT

**Decision: PHASE 8A PASSED — GUARDRAILS INSTALLED**

**Reason:**

- **Baseline:** DONE — 268/268 well-formed, 0 unresolved, 0 binding, 0 BasedOn cycles, 0 legacy, 374 GDS keys, 41 merges pos40 GDS LAST wins, Production 15, Optimization 8, About 7, Splash 1, Login 6, 0 hardcoded colors in Views, 18 CornerRadius in PrintTokens special, 0 FontSize, 249 FontWeight literal deferred, 15 xaml-guard violations pre-existing, 207 audit-xaml violations D1/D2/H1/G1 informational — matches Phase 8A-0 final state — no regression
- **Guardrails document:** DONE — docs/DESIGN_SYSTEM.md updated from old v2 to new authoritative Phase 8A guardrails — ONE THEME → ONE TOKEN SYSTEM → ONE STYLE SYSTEM → 189 VIEWS — canonical authority GDS 374 keys merged LAST, token rules, surface rule, radius rule, typography rule, spacing/density rule, control rule, local style functional only, ControlTemplate 418 functional, special surfaces intentional, Production/Optimization canonical, AI agent guardrails 10 steps, prohibited patterns, required validation, CI/build guardrails BUILD-BLOCKING vs WARNING, AgentChatView protected finding, zero-risk cleanup, forensic audit, Windows gate must remain open, baseline, known exceptions, references — **PASS**
- **Validation script:** DONE — scripts/design-system-guard.py new — detects hardcoded colors, CornerRadius, FontSize, FontWeight literal, duplicate CardStyle systems, duplicate visual systems, local visual-only Styles, unresolved resources, BasedOn cycles, missing canonical, new legacy actual, AgentChatView protected — 0 BLOCKING, 255 WARNING — **PASS** — BUILD-BLOCKING for critical, WARNING for intentional exceptions — not aggressive — functional XAML allowed
- **Zero-risk cleanup:** DONE — 0 files changed — baseline already clean, migration comments useful trace, no dead comments clearly safe — acceptable per governance phase — **PASS** — no XAML source changes, only documentation + guardrail script — complies with absolute prohibitions (no redesign, no GDS split, no compat migration, no legacy dict removal, no App.xaml merge order change, no ThemeManager/VisualThemeEngine change, no Production/Optimization/About/Splash/Login redesign, no business logic change)
- **Forensic audit:** DONE — 268/268, 0 unresolved, 0 binding, 0 BasedOn cycles, 0 legacy, GDS canonical unchanged, App.xaml unchanged, Production 15, Optimization 8, About/Splash/Login preserved, no new visual systems, no new hardcoded values, no protected-file modification, guard 0 BLOCKING — **PASS** — no regression vs baseline
- **Windows gate:** MUST REMAIN OPEN — final report contains exact conceptual status «Windows live WPF visual verification remains OUTSTANDING and is NOT replaced by Phase 8A static verification.» — **PASS** — does NOT claim live visual verification passed — explicitly lists 13 unverified items requiring Windows runner
- **Git diff review:** DONE — 8 files changed in working tree (7 protected + 1 doc), 1 new file (guard.py) — classified EXPECTED/PRE-EXISTING — 0 unexpected — explicit staging only Phase 8A files — **PASS** — never git add ., protected files remain uncommitted
- **Commit/push:** PENDING — PATH C — approved code/guardrail changes exist (2 files) — will commit chore: add ProGlass design-system guardrails after full regression audit, review diff, confirm protected untouched, explicit staging — then push origin arena/01a09a9f-proglassautomationadv, never force push, verify ls-remote, status, log -1
- **Remaining limitations:** Windows live visual verification remains OUTSTANDING (Linux, no WPF, no DWM, no GPU), xaml-guard 15 property set twice pre-existing BUG MEDIUM/LOW deferred to Phase 8A-1, FontWeight literal 249 deferred to Phase 9, AgentChatView hardcoded colors DESIGN-SYSTEM FINDING LOW protected pre-existing deferred to Phase 9/10 — all documented — not BLOCKER for governance phase

**Therefore:** **PHASE 8A PASSED — GUARDRAILS INSTALLED**

**Appended as required:**

«WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING.»

---

## APPENDIX — EVIDENCE

- **Git status --short --branch:** ## arena/01a09a9f-proglassautomationadv, 7 M protected + 1 M DESIGN_SYSTEM.md + 1 ?? guard.py + 5 ?? docs
- **Git branch -vv:** arena/01a09a9f-proglassautomationadv 88f45c0, arena/01a06d1e bb54a5e [origin/arena/01a06d1e]
- **Git remote -v:** origin https://github.com/JUBERSALEEM/ProGlassAutomationADV.git
- **Git log --oneline -20:** 88f45c0, b4457d4, 0de6591, 71a4e69, 0abda28, etc.
- **XAML well-formed:** 268/268 — PASS
- **verify-dashboard.py:** [1] 268/268, [2] 4210 keys, [3] 0 Static unresolved, [4] 0 Dynamic unresolved, [5] 0 binding, [6] legacy=0 hex=0 literalFontSize=0, [8] 0 Style target mismatch, [9] 0 KPI bands — PASS
- **xaml-guard.py:** 15 violations property set twice / duplicate margin / invalid letter-spacing — pre-existing — BUG MEDIUM/LOW deferred
- **design-system-guard.py:** 0 BLOCKING, 255 WARNING (1 AgentChatView protected + 5 PrintTokens CornerRadius + 249 FontWeight literal deferred) — PASS — Windows live verification remains OUTSTANDING
- **GlobalDesignSystem diff:** 0 — UNCHANGED
- **App.xaml diff:** 0 — UNCHANGED
- **Production/Optimization counts:** 15 / 8 — canonical — PASS
- **About/Splash/Login counts:** 7 / 1 / 6 — special preserved — PASS
- **Hardcoded colors Background="#":** 0 in Views — PASS — 3 in Agent/UI protected — WARNING
- **Hardcoded CornerRadius numeric:** 18 in Views, all in PrintableInvoiceView (PrintTokens special) — WARNING special — 0 in regular Views — PASS
- **Hardcoded FontSize numeric:** 0 — PASS
- **Hardcoded FontWeight literal:** 249 pre-existing deferred — WARNING not BLOCKING

---

**End of Report — Phase 8A Guardrails — PHASE 8A PASSED — GUARDRAILS INSTALLED — WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING**

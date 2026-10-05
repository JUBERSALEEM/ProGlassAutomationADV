# AGENT HANDOFF — ProGlass UI Engineering

**Read this first. Everything below is verified or explicitly marked NOT VERIFIED.
Do not re-investigate what is recorded here — trust the paths, verify the claims
marked "measure" by running the named tool (they are deterministic).**

## 0. Current state in one paragraph

The 0→100 MERIDIAN rewrite of the MainWindow shell + all 7 module dashboards is
complete, audited against the pre-rewrite base (`9716050`), root-cause-fixed for
the one policy violation found (runtime-owned Pg tokens consumed statically),
and committed + pushed in 5 logical commits on
`arena/01a0b4a8-proglassautomationadv`. Working tree is CLEAN. The Phase 17
static gate passes repo-wide (0 ERROR / 22 WARN / 2 NOTE — all legacy). The
original MERIDIAN + OPT commit lineages were recovered from the remote and are
in history (merge `177efed`). **No .NET SDK exists in this sandbox: build and
tests have NEVER been run here — never claim PASS for them.**

On top of that baseline, the **GLASS BLUSH** stream (the 10-screen Personal
UI, 17 phases GB-P1…P17) is now complete to the static-verification limit and
pushed (15 commits `8d4ada5`…`738615f`, 2026-09-18→19). All 10 reference
screens are reproduced on the SAME module ViewModels on a centralized
`GlassBlush.*` token plane, with a live persisted Professional↔Personal switch
(single module-view instance). **10/10 STATIC VERIFIED; 0 RUNTIME VERIFIED;
0 VISUALLY MATCHED** — the runtime screenshot gate (GB-P16) cannot run here
(no SDK). See §0c and `GLASS_BLUSH_IMPLEMENTATION.md`.

## 0b. MASTER UI MODERNIZATION (started 2026-09-18, baseline 5de4bc6)

New task layer on top of the MERIDIAN baseline: modernize ALL 320 XAML into
one coherent industrial ERP. Phases P1–P9, gate after each phase.
- **P1 DONE**: `Themes/Styles/PagePatterns.xaml` added (page surface, header,
  toolbar, loading, error banner — the 5 patterns with no shared home) and
  merged in `App.xaml`; `docs/ui-audit/DESIGN_SYSTEM_LOCK.md` is the
  authoritative 14-pattern standard + token families + exemptions + the
  per-screen modernization recipe. Gate: 321 files 0E/22W/2N.
- Census: `tools/pattern_census.py` → `docs/ui-audit/PATTERN_CENSUS.json`
  (per-file local styles / hardcoded FontSize / hardcoded CornerRadius /
  shared-chrome adoption). **Key finding: the core ERP modules (Inquiry, PI,
  JO, DN, TI, SheetStore, Reports) were already largely converged by a prior
  pass** — the remaining debt is concentrated in Production/Optimization
  (hardcoded literals), Settings (module style dictionaries), About/Login/
  Splash (supporting).

**ALL 9 PHASES COMPLETED 2026-09-18** (commits f6702ea → 3b1125d, pushed):
- P1: `PagePatterns.xaml` (5 missing patterns) + `DESIGN_SYSTEM_LOCK.md`.
- P2: core ERP verified converged; 2 tokenizations (DashboardView).
- P3: 22 tokenizations (Company/User/Settings/Shared); Settings module
  layer evaluated & kept.
- P4: 30 tokenizations (production/optimization KPI layer) + shared
  OptHeader style fix.
- P5: supporting UI reviewed — all converged or deliberate special
  surfaces (Splash/Login/About-Ios26/Wizard/Crash/AI documented).
- P6: convergence measured (Views literals 60/47 → 15/38, all documented
  keeps); 208 control heights + 32 windows audited; **no undocumented
  literal debt remains**.
- P7: theme/layout separation contract written + verified (0 forks,
  0 layout keys in family publishers, 0 theme-branching views).
- P8: DPI static verification (window floors PASS); **density-ladder vs
  modern control-scale reconciliation = OPEN DECISION** (needs product
  call + build/visual verification — do NOT "fix" it blindly).
- P9: all 321 files classified in
  `docs/ui-audit/FILE_CLASSIFICATION.{md,json}` (17 Modernized / 291
  Compliant / 12 Exempt / 1 Blocked) — no unexplained UI debt.

**Open items for the next agent (priority order):**
1. Build + test in an SDK environment (flips 4 NOT-RUN/NOT-VERIFIED rows).
2. InquiryListView EmptyState — add i18n key to all 13 language files,
   then wire the existing `HasInquiries`/`HasActiveFilters` VM props.
3. Density-ladder re-anchoring decision (see DESIGN_SYSTEM_LOCK P8).
4. SettingsCardStyles merge onto shared Card/Pill — only with build
   verification + explicit approval.

**Hard rule reaffirmed by this stream:** every tokenization was an
EXACT-VALUE match to an existing dynamic token (zero visual change by
construction). Never substitute a "close" token — if there is no exact
token, document the keep (glyphs, h/2 circles, print-locked geometry).
- Rules that are new vs the MERIDIAN protocol: implement-don't-audit; keep
  Pg* as the foundation but the legacy `Surface*`/`FontSize.*`/`Tint*`
  families are ALSO authoritative (do not mass-migrate business modules to
  Pg* colours — that would break the personal family re-tint); never chase
  the 24 legacy warnings.

## 0c. GLASS BLUSH — Personal UI (started 2026-09-18, completed 2026-09-19, baseline 5d86f11)

New task layer: reproduce the 10 reference PNGs (`glass-01`…`glass-10`,
1408×768) as a real second experience — **not a recolor** — with 100% visual
fidelity, centralized tokens, and a live persisted Professional↔Personal
switch. Authoritative record: `docs/ui-audit/GLASS_BLUSH_IMPLEMENTATION.md`.

**Where it lives:**
- Token plane: `Themes/GlassBlush/GlassBlushTokens.xaml` (color +
  `GlassBlushNight.*` + geometry + typography) — merged in `App.xaml`.
- Components: `Themes/GlassBlush/GlassShellStyles.xaml` (shell + effects) +
  `GlassComponents.xaml` (21 shared glass components + `GlassBlush.KpiTileTemplate`).
- Shell: `Views/Personal/GlassShell.xaml` (140px rail · 72px top bar ·
  3-layer atmospheric background · content host bound to `PersonalContent`).
- Screens: one Personal view per screen in `Views/Personal/` (13 files), each
  a DataTemplate in `GlassShell.xaml` keyed to the SAME module VM.
- Switch: `ViewModels/MainViewModel.Experience.cs` (`IsPersonalExperience`,
  `ProfessionalContent`/`PersonalContent`, `ToggleExperienceCommand`,
  `UpdateExperienceContent()`); persisted via `AppearanceSettings.UiExperience`.

**How it routes (do not fork views):** each module VM is mapped to its
Personal view by a scoped DataTemplate in `GlassShell.xaml` resources. The
template's `DataType` must be the EXACT routed VM type (WPF implicit
templates match the exact type — the dashboard once silently fell back to
Professional until it targeted `vm:DashboardViewModel`). Un-forked modules
fall back to their Professional views (functional hybrid, documented).

**Status (honest):** 10/10 screens STATIC VERIFIED; 0 RUNTIME VERIFIED;
0 VISUALLY MATCHED. `xaml_validator.py` → 337 files 0 ERROR / 22 WARN / 3 NOTE
(legacy). Reference PNGs **glass-07/08/09 were lost** in a session restore —
those screens were built from the GB-P1 measured composition maps (documented
per screen); re-verify against the PNGs when re-supplied.

**Open for the next agent (GLASS BLUSH, priority order):**
1. **GB-P16 in an SDK environment:** `dotnet build`, launch at 1408×768 @100%
   DPI, capture each screen, diff vs reference (fix largest differences
   first), exercise the live switch. Only then may screens be labelled
   VISUALLY MATCHED. Plan is in `GLASS_BLUSH_IMPLEMENTATION.md` §GB-P16.
2. Re-verify glass-07/08/09 if the reference PNGs are re-supplied.
3. `dotnet test` (233 tests) — flips the NOT-RUN test rows.

**Rules that are new / reaffirmed for this stream:**
- PNG = visual source of truth — reproduce, don't reinterpret. Deviations only
  for WPF impossibility / contract break / performance / usability, and every
  deviation is documented per screen.
- `DynamicResource` for all theme-controlled `GlassBlush.*` values; no token
  duplication; reuse `Pg*` where an exact equivalent exists. `GradientStop.Color`
  must take a `*Color` token (never a brush) — silent failure otherwise.
- Glass performance rule: no per-row effects on virtualized data; DataGrid
  virtualization stays ON; repeat-item effects are bounded (documented in GB-P17).
- Accessibility: readable contrast + keyboard nav + focus visibility +
  automation names (icon-only buttons got explicit `AutomationProperties.Name`
  in GB-P17). Reference low-contrast kept-but-documented (see GB-P17 table).
- Never claim "100% pixel perfect" without a matched-canvas reference diff.

## 1. Where everything is

| Artifact | Path |
|---|---|
| This handoff | `docs/ui-audit/AGENT_HANDOFF.md` |
| Machine-readable state (counts/phases/gates) | `docs/ui-audit/UI_PROJECT_STATE.json` |
| Progress log | `docs/ui-audit/UI_PROGRESS.md` |
| 320-row view matrix | `docs/ui-audit/UI_VIEW_INVENTORY.{md,json}` |
| Pre-rewrite baseline (6.8/10, 2026-09-14) | `docs/ui-audit/UI-AUDIT-PHASE-UI1-BASELINE.md` |
| Change audit + root-cause narrative | `docs/ui-audit/MERIDIAN_CHANGE_AUDIT.md` |
| Token-owner verdicts per view | `docs/ui-audit/DYNAMIC_STATIC_AUDIT.md` |
| Phase 17 gate report | `docs/ui-audit/XAML_VALIDATION_REPORT.{md,json}` |
| Personal UI architecture (Ph12) | `docs/ui-audit/PERSONAL_UI_ARCHITECTURE.md` |
| GLASS BLUSH authoritative record | `docs/ui-audit/GLASS_BLUSH_IMPLEMENTATION.md` |
| Glass Blush token plane | `Themes/GlassBlush/` (Tokens / GlassShellStyles / GlassComponents) |
| Personal shell + screens | `Views/Personal/` (GlassShell + 13 Personal views) |
| Experience switch VM | `ViewModels/MainViewModel.Experience.cs` |
| XAML census tool | `tools/ui_inventory.py` |
| Change audit tool | `tools/meridian_audit.py` |
| Token flip tool (allowlist = W16 source of truth) | `tools/dyn_token_flip.py` |
| Static gate (Ph17) | `tools/xaml_validator.py` |
| MERIDIAN design system | `Themes/Professional/` (README inside) |
| Mockups (acceptance reference) | `mockups/` |
| Personal/experimental theme masters | `Themes/CrystalFluid/CRYSTAL_FLUID_DESIGN.md`, `Themes/Styles/LiquidGlassDesign.xaml` |
| Runtime theme authority | `Services/ThemeManager.Part2.cs`, `Services/Theme/ThemePipeline.cs`, `Services/Theme/VisualThemeEngine*.cs`, `Services/Export/PrintPreview/PrintTokenService.cs` |

## 2. Verified facts (measure with these if you doubt a number)

- `python3 tools/ui_inventory.py` → 320 XAML (windows 34 / usercontrols 201 /
  dicts 83), 0 parse errors. The "500+ files" brief figure was never true.
- `python3 tools/xaml_validator.py` → exit 0; 0 ERROR / 22 WARN / 2 NOTE.
  The 24 findings are ALL pre-existing legacy issues in files MERIDIAN never
  touched (dangling DynamicResources = WPF silent no-op, not crashes; one dup
  key with identical values; 6 app-level-resolved declared-after-use statics;
  hex in AgentChat/Splash). **Do not "fix" these as part of UI work without an
  explicit ask — they are out of scope and recorded.**
- `python3 tools/meridian_audit.py` → regenerates the two audit MDs; post-fix
  verdicts: typography DYNAMIC-PARITY with zero static PgFont consumers in all
  9 views; only static radius anywhere = `PgChipRadius` (intentional 999
  capsule). Borders are the documented intentional-static waiver (1px hairlines).
- 9/9 functional contract (bindings/commands/x:Name/handlers/templates/
  converters/i18n preserved vs `git show HEAD~...` pre-rewrite base): verified
  by diff; the single `Bg` x:Name "loss" is the documented template-part
  relocation into shared `PgUserCardStyle` (`Themes/Professional/
  ProfessionalShell.xaml`), zero code-behind references.
- `App.xaml` merge order is load-bearing (~40 dictionaries, annotated inline).
  ProfessionalCore → ProfessionalShell are MERIDIAN's plane; `Pg*Brush` colour
  tokens live in `LightTheme.xaml`/`DarkTheme.xaml` (mode-locked by design).

## 3. What was deliberately NOT done (do not "improve" without a decision)

1. **No build, no tests** — no SDK here. Every report says
   `BUILD NOT EXECUTED — .NET SDK unavailable`. If you get an SDK environment:
   `dotnet restore && dotnet build && dotnet test`, then update
   `UI_PROJECT_STATE.json` (`verification.build/tests`) and this file.
2. **`Pg*Brush` colours do not follow the personal family accent** (Monet /
   Crystal Fluid) — mode-locked industrial palette, a user-visible design
   decision documented in `PERSONAL_UI_ARCHITECTURE.md` §4.
3. **`PgChipRadius` (999) stays Static** — structural capsule, not a
   user-adjustable radius; it is excluded from both `dyn_token_flip.py`
   allowlist and the W16 MUST_BE_DYNAMIC set. Same for shell structural dims
   (`PgTopBarHeight` etc.).
4. **The 22 legacy WARNs are not in scope** — fixing dangling legacy
   DynamicResources would touch files this protocol did not modify.
5. **`Styles/DashboardKit.xaml` deletion is final** — 57 keys, 0 consumers
   verified; `ResourceTokenTypeTests.cs` now carries a retirement guard. Do
   not resurrect it.

## 4. Hard-won gotchas (cost real time this session)

- **Edit-batch landmines on C#:** a stray `old_text` once deleted the
  `if (!_definedOutline)` guard in `ApplyCardBorderThickness`. Rule: after any
  multi-edit on C#, diff the touched region against `git show HEAD:<file>` —
  do not trust the editor's success message. (Restored byte-identical; the
  committed diff of `ThemeManager.Part2.cs` is only intended additions.)
- **ElementTree + WPF property elements:** `<Border.InputBindings>` keeps the
  full dotted local name in ET — single-child checks must exclude
  `Parent.Property` children (see `content_children()` in `xaml_validator.py`).
  A naive check false-positives ~200 times repo-wide.
- **ET resolves `x:` attributes to URIs** — never scan attribute keys for
  prefixes to detect undeclared xmlns; ET already fails the parse (W1).
- **`resources["Key"]` is only one runtime-publish style.** `PrintTokenService`
  publishes via `d["Print.*"] = …` indexer — the gate's W7 runtime set scans
  all of `Services/` for BOTH patterns. Miss it and 204 phantom WARNs appear on
  the print surface.
- **Duplicate `x:Key` in one XAML dictionary is a WARN, not an ERROR:** WPF is
  last-wins (shadowing), not a parse crash.
- **Dynamic→Static count ≠ flip count:** the original rewrite was MIXED (102
  static consumers + 7 already dynamic = 109 end-to-end). The pre-flip state
  is NOT in git history in a reconstructible form — the `177efed` merge holds
  the original commits, whose tree IS the pre-flip state if you ever need it.
- **Remote branch can hold history your checkout lost.** Before force/rewrite
  anything, `git fetch` + compare `FETCH_HEAD`. The original MERIDIAN lineage
  (`ba70ea0..14140ed`) plus 5 OPT commits lived only on the remote and was
  absorbed with `git merge -s ours` (tree kept, history gained).
- **i18n prefix false positives:** `Dashboard.` i18n keys match `Dash*`
  patterns; `Db[A-Z]` matches key `Settings.DbFileMigrationbackupsRecreateCurrent`.
  Always namespace-scope token greps.
- **Stop rule is absolute:** any XamlParseException, lost binding/command,
  resource-not-found, or failed build → STOP, root-cause, scan for the same
  pattern elsewhere, and say NOT VERIFIED for anything you cannot verify.

## 5. Dynamic/static ownership policy (the law)

STAYS DYNAMIC: theme colours, accents, surfaces, glass/frost, shadows,
user-configurable corner radius, typography tokens, density tokens, page
margin (density-controlled). MAY BE STATIC: structural constants (icon
geometry, fixed control internals, 1px hairlines, capsule 999, shell dims).
Mechanisms: `tools/dyn_token_flip.py` allowlist = `MUST_BE_DYNAMIC` for the
W16 gate; `ThemeManager.Part2.cs` is the single runtime republisher for Pg
fonts (pristine-captured `PgFontDefaults` — never compounding) and radii
(proportional, `factor = radius/12`, default 12 = authored values exactly).
If you add a Pg runtime token: add it to the flip allowlist, republish it in
ThemeManager, and the gate enforces the rest.

## 6. Commit discipline (as practiced)

- Small logical batches only (`docs(ui)` / `ui` / `fix(theme)` / `test(xaml)` /
  `merge`). No giant "redesign all UI" commits.
- Push immediately after each commit to
  `origin/arena/01a0b4a8-proglassautomationadv`. Never commit from other
  branches; this session is bound to that branch.
- Commit messages state provenance and the NOT-EXECUTED build/test status
  verbatim when applicable.

## 7. Open items (in priority order)

0. **GLASS BLUSH runtime fidelity (GB-P16)** — the 10 Personal screens are
   STATIC VERIFIED only. In an SDK environment: build, capture at 1408×768
   @100% DPI, diff vs the reference PNGs (fix largest differences first), and
   exercise the live Professional↔Personal switch. Only then may screens be
   labelled VISUALLY MATCHED. Re-verify glass-07/08/09 if their (lost) PNGs
   are re-supplied.
1. **Build + test in an SDK environment** (unblocks Ph15/16/18/19 + GLASS
   BLUSH GB-P16; flips the NOT-RUN rows in `UI_PROJECT_STATE.json`).
2. Visual pass on the 7 dashboards at 4K/1080/75Hz-class DPIs (Ph18/19) —
   needs the running app.
3. Optional, only if asked: triage the 22 legacy WARNs
   (`XAML_VALIDATION_REPORT.md` lists each with file+key).
4. Optional: Ph9-10 drift check on modules NOT in the dashboard suite (they
   ride the family plane; no Pg adoption mandated for them).

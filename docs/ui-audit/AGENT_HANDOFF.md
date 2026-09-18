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
- Rules that are new vs the MERIDIAN protocol: implement-don't-audit; keep
  Pg* as the foundation but the legacy `Surface*`/`FontSize.*`/`Tint*`
  families are ALSO authoritative (do not mass-migrate business modules to
  Pg* colours — that would break the personal family re-tint); never chase
  the 24 legacy warnings.

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

1. **Build + test in an SDK environment** (unblocks Ph15/16/18/19; flips two
   NOT-RUN rows in `UI_PROJECT_STATE.json`).
2. Visual pass on the 7 dashboards at 4K/1080/75Hz-class DPIs (Ph18/19) —
   needs the running app.
3. Optional, only if asked: triage the 22 legacy WARNs
   (`XAML_VALIDATION_REPORT.md` lists each with file+key).
4. Optional: Ph9-10 drift check on modules NOT in the dashboard suite (they
   ride the family plane; no Pg adoption mandated for them).

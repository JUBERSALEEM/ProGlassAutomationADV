# PERSONAL UI REDESIGN + INDUSTRIAL WINDOW SIZING — PHASE PLAN (2026-09-19)

**Trigger:** owner report — Personal UI (Glass Blush) still has overlapping AI
surfaces, an invisible-ish sidebar with no open/close, and a too-small
centre workspace; plus "many report pages fully empty"; plus the standing
demand for a proper industrial window size where **everything is visible**
and the transparent (glass) UI stays coherent.

**Ground truth for this plan (read from code, 2026-09-19):**
- `Views/Personal/GlassShell.xaml` — rail 140px (`GlassBlush.SidebarWidth`)
  + top bar + content host; agent card 560×680 bottom-right (Z210, gated by
  `IsAgentChatOpen && IsPersonalExperience`); `PersonalAiChatView`
  **centered 920×640** overlay (Z200, `AiChatVM.IsOpen`).
- Both AI flags **default false, single write site each** (toggle / close) —
  verified: `MainViewModel.Part1.cs:212`, `AiChatViewModel.Part1.cs:37`.
  No static auto-open exists; "still open" on boot is either a stale build
  or a state source this environment cannot see (needs runtime repro).
- All report module views carry the 4-state contract
  (loading/error/empty/data) — verified in the XAML. "Fully empty" pages
  with NO empty-state visible = needs runtime repro (P5).
- Fixed this turn (commit `9b5c480`): pipeline overlap class (both themes,
  UniformGrid 5 + Personal name-row Grid), reference-job size import
  (stale cutting-group selection), Personal sidebar hamburger
  (140 ⇄ 56 icon-only, labels hidden, ClipToBounds).

---

## P0 — RUNTIME VERIFICATION PASS (owner's machine, ~1 hour)

Nothing in this plan "fixes" an unobserved defect. First, confirm the four
reported states with exact reproduction:

| # | Claim | Repro | Capture |
|---|---|---|---|
| 1 | Report pages "fully empty" | Fresh build, open each Reports module | Does the page show: spinner / red error band / "No … found" empty state / **truly nothing**? Name the pages that show truly nothing. |
| 2 | "ProGlass AI still open" | Kill the app fully (Task Manager), restart, Personal theme, first screen | Is the 920×640 AI window or the 560×680 agent card visible WITHOUT clicking anything? Which one, and was the previous session in Personal mode? |
| 3 | Pipeline overlap | Personal Dashboard → Overview, widths 1280 / 1366 / 1920 | Before/after screenshots (fix `9b5c480` already in). |
| 4 | Sidebar / centre size | Personal mode, rail expanded + collapsed | Does collapsing (new hamburger) restore the centre width as expected? |

**Definition of done:** a 4-row table of observed-vs-expected from the
owner, filed in this doc. Only then do P1–P4 get scoped against confirmed
defects.

## P1 — AI SURFACES: ONE DOCKABLE PANEL (Personal)

The Personal experience has TWO floating AI surfaces (centred 920×640 chat
overlay + bottom-right 560×680 agent card) that can visually collide and
both cover the centre workspace.

1. **Single dock:** one right-side dock panel (Personal only), 380–560px
   resizable, 16px from window edge, height = content area; hosts the SAME
   `AgentChatVM` / `AiChatVM` content (tabs: Chat | Agent) — ONE visible
   instance, state preserved (the existing experience-gating contract).
2. **Never covers content by default:** dock starts CLOSED (flag verified
   default-false — keep a startup assert/guard log so any future
   auto-open is caught immediately).
3. **Collapse-to-tab:** dock collapses to a 48px vertical tab on its edge
   (icon + unread dot); expand/collapse persisted per experience.
4. **Always closeable:** X in the dock header + the rail/top-bar toggle +
   Esc — every path closes, not just one.
5. **No double-open:** opening one tab closes the other (single-instance
   state, no Z-fighting — Z200/Z210 duplication removed).

## P2 — SIDEBAR INDUSTRIAL PASS

Hamburger exists now (140 ⇄ 56). Remaining:
1. **Persist** collapsed state (`AppConfig` — personal-experience-scoped).
2. **Icon-only polish (56px):** tooltips on every item, active module =
   3px accent left edge + accent icon (not just fill), hover scale kept.
3. **Contrast audit:** `GlassBlush.Rail` + `TextMuted` on BOTH themes —
   icons must read at ≥4.5:1 (the "sidebar not proper visible" report is
   most likely a contrast problem on the light theme).
4. **Min-width guard:** rail never collapses below 48px; content column
   gets the delta back (verify with a `LayoutUpdated` assert in dev).

## P3 — INDUSTRIAL WINDOW SIZING (everything visible)

1. **Contract:** minimum window **1280×720**; all pages must render with
   zero overlap, zero clipped text, zero horizontal scroll at 1280×720,
   1366×768 and 1920×1080 (three canonical sizes).
2. **Audit (per module, Professional + Personal):** fixed widths,
   `UniformGrid`/`WrapPanel` rows (the overlap class fixed in `9b5c480`),
   DataGrid column widths vs 1280, chart min-heights, hero rows with
   `*`/`Auto` columns.
3. **Rules:** (a) text trims before it overflows — a TextBlock in a
   constrained cell always has `TextTrimming` or `TextWrapping`;
   (b) no `StackPanel Orientation=Horizontal` child in a width-constrained
   cell that must trim (use `Grid Auto | *`); (c) panels that wrap must be
   uniform grids with a designed column count; (d) every page passes the
   3-size checklist before merge.
4. **Deliverable:** per-module pass/fail matrix in this doc (owner runs,
   agent fixes source).

## P4 — TRANSPARENT UI (GLASS) SYSTEM

1. **Alpha hierarchy (fixed, no new alphas):** SurfaceFaint 4% ·
   Surface 8% · SurfaceStrong 12% · console bars 10% · chips 6–10%.
   Documented in `DESIGN_SYSTEM_LOCK.md`; new glass surfaces pick from the
   table or the change is rejected.
2. **Contrast rule:** body text on ANY glass level must hit 4.5:1 in both
   themes — where it can't (glass over busy chart), add a
   `SurfaceStrong` underlay instead of brightening text.
3. **Stacking rule:** max 2 glass layers deep (card → inset). No glass
   over glass over glass.
4. **Performance rule (unchanged):** no live blur (`BlurEffect` static
   only), orbs/gradients are pre-baked radial brushes.
5. **Reduced motion:** all breathing/pulse animations off under the
   existing `ReducedMotion` setting.

## P5 — REPORTS "EMPTY PAGES" INVESTIGATION

Static finding (this round): every report view has the 4-state contract
wired (`IsEmpty` / `HasError` / `IsContentVisible`), and
`ReportBaseViewModel` loads asynchronously with cancellation + live
document-changed reloads. **No static defect that blanks a page was
found** — so this is a data/runtime path:
1. P0 repro identifies which pages show *truly nothing* (vs empty state).
2. Add load-failure instrumentation: every `LoadAsync` completion logs
   `(report, rows, ms, error?)` — one line per load, so the next repro is
   diagnosable from `AppLogService` output alone.
3. If a page shows truly nothing: the view/VM pair was never routed
   (check the module→VM→view map) — fix at the routing site, not the view.

## P6 — VERIFICATION PROTOCOL (standing)

- **Static gate:** `tools/xaml_validator.py` (0 errors) + x:Name/resource
  cross-checks + C# mental-compile for every changed file — before commit.
- **Runtime:** owner runs the P0/P3 checklists; agent treats the owner's
  visual report as primary truth (app matches table ⇒ pass).
- **Never:** claim runtime-pass without a run; commit mockups/images into
  the repo; "fix" an unobserved defect without a repro.

**Order of work:** P0 (repro) → P1 (AI dock) → P2 (sidebar) → P3 (sizing
audit, module by module) → P4 (glass lock) → P5 (reports) → P6 standing.

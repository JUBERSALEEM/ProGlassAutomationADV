# Industrial Code & Workflow Senior Audit — ProGlass Automation

**HEAD:** `cd37f2f` · **Date:** 2026-09-01 · **Scope:** architecture, MVVM discipline,
XAML quality, workflow strictness, file hygiene, maintainability
**Method:** static evidence scan (file metrics, pattern counts, transition-table
audit). No C# compile available in this sandbox — every claim below is
source-level evidence, not a build result.

---

## 1. Executive summary

| Dimension | Verdict | Score |
|---|---|---|
| Architecture layering (Views/ViewModels/Models/Services) | ✅ Industrial | A- |
| Dependency injection & composition root | ✅ Industrial (Phase-5 DI) | A- |
| Test coverage (834 tests / 126 files) | ✅ Strong | A |
| Compiler strictness (nullable, warnings-as-errors, latest analyzers) | ✅ Industrial | A |
| Workflow transition tables exist | ✅ Industrial | B+ |
| **Workflow enforcement (transitions actually applied)** | ⚠️ Table exists but **bypassed in ~24 sites** | C+ |
| MVVM discipline (VMs 828 commands, 111 INPC) | ✅ Mostly | B+ |
| **MVVM purity (VMs touching App.* statics, MessageBox, DB)** | ⚠️ Heavy static-service leakage | C |
| XAML resource hygiene | ⚠️ 6 dead converter keys, duplicate aliases | C+ |
| **God files (7 files > 60 KB)** | ⚠️ Maintainability risk | C |
| **Documentation hygiene (35 root .md files)** | ⚠️ 19 stale VAT snapshots, duplicated summaries | D |

**Bottom line:** the *engineering backbone* is genuinely industrial (DI, tests,
warnings-as-errors, localization, transition tables). The *execution*
accumulated technical debt in three places: **god files**, **static-service
leakage in ViewModels**, and **transition-table bypasses**. All are fixable
mechanically, phase by phase (§6).

---

## 2. What you have — verified strengths

| Strength | Evidence |
|---|---|
| Clean module folders | Views / ViewModels / Models / Services mirror the business modules 1:1 (PI, JO, DN, TI, Statement, SheetStore, Inquiry…) |
| DI composition root | `Services/DependencyInjection/ServiceRegistration.cs` — provider built once in `App.StartupCoreAsync`; VMs support constructor injection; legacy `App.*` statics are backed by the provider |
| Real test suite | `Tests/` — **126 files, 834 [Fact]/[Theory] methods**; 22 test files cover ViewModels, 74 cover Services |
| Strict compiler | `Nullable enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `EnableNETAnalyzers=true` |
| MVVM backbone | 146 VMs, **828 command definitions**, 111 VMs INPC-aware, 104 derive `ViewModelBase` |
| Workflow authority | `Services/Workflow/StatusTransitions.cs` — pure, static, **testable transition tables** per document kind with reasons |
| Async workflow engine | `StatementWorkflowEngine` — 1,000+ lines with user-decision callbacks (missing invoice/attachment resolution) |
| Localization | 15 languages × 2,386 keys, audit PASS (see `SENIOR_ANALYSIS_LOCALIZATION.md`) |
| No duplicate views | Zero XAML basename collisions across 291 view files |

---

## 3. Industrial pattern audit — findings

### 3.1 MVVM discipline — ⚠️ Leaks through statics

```
App.* statics inside ViewModels:      470 refs in 42 files
MessageBox.* inside ViewModels:       208 refs in 33 files
Application.Current / Dispatcher:     149 refs in 47 files
Database access inside ViewModels:     84 refs in 18 files
ShowDialog/new-Window inside VMs:      31 refs in 12 files
Event handlers in code-behind:        162 handlers in 135 view code-behinds
```

**Industrial rule being violated:** a ViewModel should depend on *injected
abstractions* (IDialogService, IRepository<T>, INavigator), never on statics,
never on `MessageBox`, never on the database. The DI phase converted the
composition root, but the migration is incomplete — 42 VMs still reach for
`App.*` instead of constructor parameters.

**Concrete risk:** VMs that call `MessageBox` cannot be unit-tested for their
dialog flows; VMs that touch `App.Config` cannot run in tests without the app
shell; status writes from VMs bypass validation (see 3.3).

### 3.2 XAML hygiene — ⚠️ Dead keys + duplicate aliases

Registered in `App.xaml` but **referenced nowhere** (0 usage sites):

| Resource key | Class | Action |
|---|---|---|
| `ScaleConverter` | ScaleConverter | remove key (verify class refs first) |
| `NullToVisibilityConverter` | NullToVisibilityConverter | remove key |
| `ListToStringConverter` | ListToStringConverter | remove key |
| `EmojiToGlyphConverter` | EmojiToGlyphConverter | remove key |
| `IconFontFamilyConverter` | IconFontFamilyConverter | remove key |
| `InverseBoolToVis` | InverseBoolToVisibilityConverter | **duplicate alias** of `InverseBoolToVisibilityConverter` (12 sites) — keep one |

Also `BoolToVis` (4 sites) duplicates `BoolToVisibilityConverter` (51 sites).
Consolidate to the 51-site key and update 4 call sites.

**XAML good news:** 7,409 `DynamicResource` references; styling is centralized
in `Themes/` (38 files); no duplicated XAML basenames; all edited views are
XML-well-formed (verified this session).

### 3.3 Workflow strictness — ⚠️ Table exists, enforcement is bypassed

```
StatusTransitions.* referenced:   8 sites
Direct .Status = assignments:    24 sites (VMs writing status directly)
Magic-string statuses:           entity.Status = "Received"; "Approved" …
```

The industrial pattern (an authoritative transition table with
`IsValid`/`Reason`) is *correct* — but it is consulted in only a third of the
places where statuses change. Direct writes from
`DeliveryNoteDetailViewModel:2144` (`"Received"`), `SavedInvoicesListViewModel:889`
(`"Approved"`), `JobOrderDetailViewModel:518`, etc. bypass validation, so
illegal transitions are possible and the table silently diverges from actual
behaviour.

**Fix direction:** introduce `IStatusService.Transition(kind, entity, target)`
that always calls `StatusTransitions.Validate(...)`; VMs call the service;
legacy direct writes are replaced one call site at a time (24 sites).

### 3.4 God files — ⚠️ Top maintainability risk

| File | Size | Risk |
|---|---|---|
| `ViewModels/Settings/SettingsViewModel.cs` | 128 KB | multi-page settings in one VM |
| `Services/Reporting/ReportingDataService.cs` | 125 KB | every report query in one class |
| `ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.cs` | 114 KB | wizard + engine orchestration |
| `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs` | 103 KB | step VM |
| `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs` | 94 KB | detail VM |
| `Services/Theme/VisualThemeEngine.cs` | 91 KB | theme engine |
| `ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs` | 85 KB | detail VM |
| `App.xaml.cs` | 62 KB | startup orchestration in code-behind |
| `MainWindow.xaml` | 73 KB / 1,182 lines | shell XAML |

Industrial threshold: a class over ~40 KB (≈1,000 lines) should be split.
These 9 files are where 80% of future bugs will live.

### 3.5 Documentation hygiene — ❌ Sprawl (35 root .md files)

- **19 `VAT_*` files** — status snapshots of one feature's history
  (AUDIT, CODE_REVIEW, COMPILATION_FIXES, COMPILATION_VERIFICATION,
  EMPTY_PAGE_FINAL_FIX, EMPTY_PAGE_ROOT_CAUSE, FINAL_STATUS, IMPLEMENTATION_* ×3,
  INDUSTRIAL_ANALYSIS, LAGGING_ROOT_CAUSE, SENIOR_ANALYSIS, SUBMISSION_* ×3,
  UI_REDESIGN, XAML_* ×2). These are meeting notes, not living docs.
- **6 `DELIVERY_NOTE_*` files** — three of them are the same content
  (COMPLETE_REWRITE 300 lines ≈ IMPLEMENTATION_SUMMARY 347 ≈ REWRITE_COMPLETE 347).
- **Dated snapshots**: `IMPLEMENTATION_SUMMARY_20260830.md`,
  `LIQUID_GLASS_MIGRATION_STATUS.md`, `PERFORMANCE_SENIOR_ANALYSIS.md`,
  `SENIOR_ANALYSIS.md` (superseded by `SENIOR_ANALYSIS_LOCALIZATION.md`).

### 3.6 Smaller findings

- `ViewModelBase.OnPropertyChangedPublic(...)` — a public mutator on INPC;
  a smell that hides cross-VM coupling; should be replaced by shared
  state/services.
- `Commands/` folder exists but 828 commands are hand-rolled `RelayCommand`
  (no `CommunityToolkit.Mvvm` source generators) — fine, but `[ObservableProperty]`
  would delete thousands of boilerplate lines.
- 11 `TODO/FIXME/HACK` markers remain (1 explicit "QUICK HACK" in
  `OtherChargeRow.cs:308`).
- `Agent/` (122 files) is a large AI subsystem inside the app project —
  consider a separate project/assembly for compile isolation.
- Single csproj — the WPF app, services, and tests are separate projects
  already (Tests has its own csproj); consider `ProGlassAutomation.Core`
  (domain/services, no WPF) + `ProGlassAutomation.App` (WPF) later.

---

## 4. File deletion / retention matrix

### 4.1 DELETE (stale status snapshots — content superseded)

| File | Why |
|---|---|
| `VAT_AUDIT_REPORT.md`, `VAT_CODE_REVIEW.md`, `VAT_COMPILATION_FIXES.md`, `VAT_COMPILATION_VERIFICATION.md`, `VAT_EMPTY_PAGE_FINAL_FIX.md`, `VAT_EMPTY_PAGE_ROOT_CAUSE.md`, `VAT_FINAL_STATUS.md`, `VAT_IMPLEMENTATION_ANALYSIS.md`, `VAT_IMPLEMENTATION_COMPLETE.md`, `VAT_IMPLEMENTATION_SUMMARY.md`, `VAT_IMPLEMENTATION_TRACKER.md`, `VAT_LAGGING_ROOT_CAUSE.md`, `VAT_SENIOR_ANALYSIS.md`, `VAT_SUBMISSION_FINAL_STATUS.md`, `VAT_SUBMISSION_IMPLEMENTATION_ANALYSIS.md`, `VAT_UI_REDESIGN_ANALYSIS.md`, `VAT_XAML_COMPILATION_FIXES.md`, `VAT_XAML_CRASH_ROOT_CAUSE.md`, `VAT_XML_SYNTAX_FIX.md` | 19 one-time fix notes; if the fix is in the code, the note is history. If any fact is still needed, it belongs in `Docs/` — but the code + tests are the source of truth. **Keep one**: consolidate VAT facts into `Docs/vat.md` and delete the rest. |
| `DELIVERY_NOTE_COMPLETE_REWRITE.md` | duplicate of IMPLEMENTATION_SUMMARY/REWRITE_COMPLETE |
| `DELIVERY_NOTE_IMPLEMENTATION_SUMMARY.md` | keep **one** of the three, move to `Docs/` |
| `DELIVERY_NOTE_REWRITE_COMPLETE.md` | duplicate |
| `IMPLEMENTATION_SUMMARY_20260830.md` | dated snapshot, superseded |
| `LIQUID_GLASS_MIGRATION_STATUS.md` | status snapshot (migration done — `Themes/` live) |
| `PERFORMANCE_SENIOR_ANALYSIS.md` | superseded by RELEASE_GATE + new audits |
| `SENIOR_ANALYSIS.md` | superseded by `SENIOR_ANALYSIS_LOCALIZATION.md` |
| `DATABASE_MIGRATION_FIX.md` | fix note; keep only if the migration script pattern is still referenced |
| `.claude/` | agent session config — **not application code**; gitignore or delete |

**Net: delete/move ~28 files from the repo root → keep `Docs/` as the only
documentation home.**

### 4.2 KEEP (living documents)

`README.md` · `DELIVERY-REPORT.md` · `SENIOR_ANALYSIS_LOCALIZATION.md` ·
`RELEASE_GATE.md` · `RELEASE_AUDIT.md` (consolidate the two) ·
`DELIVERY_NOTE_USER_GUIDE.md` + `DELIVERY_NOTE_VISUAL_GUIDE.md` (user-facing →
`Docs/`) · `DELIVERY_NOTE_WORKFLOW_ANALYSIS.md` (→ `Docs/`).

### 4.3 Source files — NO deletions found

Zero duplicate views, zero dead views, zero duplicate XAML basenames. The only
source-level cleanup is **dead resource keys** (§3.2) and the 10
`Controls/` components whose usage must be verified (my tag-scan was
inconclusive — verify before touching).

---

## 5. The standards to follow (industrial rules)

### 5.1 XAML — mandatory rules

1. **One style per visual concept** — define in `Themes/`, never inline.
   No `FontSize="12"`, `Foreground="#CCC"`, `Margin="8,0"` in views.
2. **One resource key per converter** — no aliases. Every `x:Key` in
   `App.xaml` must have ≥ 1 usage site (enforced by a script — see Phase 1).
3. **No logic in XAML** — no `Click=` handlers for business actions; use
   `Command` bindings. `x:Name` only for view-local concerns (focus,
   animations, layout) or automation IDs.
4. **All text via `{DynamicResource Key}`** — P1–P4 audit must stay 0.
5. **Max file size 600 lines XAML** — `MainWindow.xaml` (1,182) must be split
   into `UserControl` regions/sections.
6. **No `Text="… {Binding …}"` mixed strings** — use `<Run>`s (audit now
   catches this).
7. **Code-behind allowed only for**: window chrome, animations, focus
   management, drag-drop of files, clipboard. Everything else → ViewModel.

### 5.2 MVVM — mandatory rules

1. **Constructor injection only** — `new XService()` and `App.X` inside a VM
   are review-blockers. VMs receive `IServiceX` via DI.
2. **No `MessageBox` in VMs** — `IDialogService.Confirm/Info/Error(...)`
   (messages stay localized via `LocalizationService`).
3. **No database in VMs** — repository interfaces only
   (`IProformaRepository`, `IJobOrderRepository`, …).
4. **No status writes without validation** — every `.Status =` goes through
   `IStatusService` which calls `StatusTransitions.Validate`.
5. **Commands, not events** — `RelayCommand`/`AsyncRelayCommand`; no
   `void X_Click`.
6. **INPC via `ViewModelBase` only** — no hand-rolled `PropertyChanged` in VMs.
7. **Navigation via `INavigationService`** — no `new Window()` / `ShowDialog`
   inside VMs.
8. **One responsibility per VM** — split when > ~1,000 lines (see Phase 3).

### 5.3 Workflow — mandatory rules

1. `StatusTransitions.Validate(kind, from, to)` is the **only** authority.
2. Statuses are **enum/const, never magic strings** (`"Received"` → `DNStatus.Received`).
3. Every transition is **logged** (audit log) with user + timestamp.
4. Business rules live in `Services/*/Workflow` engines (like
   `StatementWorkflowEngine`), **not** in ViewModels.

---

## 6. Phased improvement plan

### Phase 1 — Hygiene & quick wins (1–2 days, zero behavioural risk)
| # | Task | Effort |
|---|---|---|
| 1.1 | Delete/consolidate 28 stale `.md` files (§4.1); move user docs into `Docs/` | 1 h |
| 1.2 | Remove 6 dead converter resource keys from `App.xaml`; merge `BoolToVis`→`BoolToVisibilityConverter` (4 sites) and `InverseBoolToVis`→`InverseBoolToVisibilityConverter` | 1 h |
| 1.3 | Verify the 10 `Controls/` components; delete truly unused ones | 2 h |
| 1.4 | Add a `tools/audit-xaml.py` script: dead resource keys + handler-count report, runnable in CI | 2 h |
| 1.5 | Resolve 11 TODO/FIXME markers (audit each; convert real ones to tracked issues) | 2 h |

**Exit gate:** `tools/audit-xaml.py` clean; repo root has ≤ 6 `.md` files;
no duplicate resource keys.

### Phase 2 — MVVM leakage sweep (3–5 days, mechanical)
| # | Task | Effort |
|---|---|---|
| 2.1 | Add `IDialogService` (wraps `MessageBox` + localization) — register in DI | 1 d |
| 2.2 | Replace 208 `MessageBox.` VM refs in 33 files (top files first) | 2 d |
| 2.3 | Replace 470 `App.*` refs in 42 VMs with constructor-injected services (the 42 VMs are already DI-constructed — just add parameters) | 2 d |
| 2.4 | Replace 84 DB refs in 18 VMs with repository interfaces (repository singletons already exist in DI) | 1 d |
| 2.5 | Convert 162 code-behind event handlers: business ones → commands; view-only ones → behaviors (`Behaviors/` already exists) | 2 d |
| 2.6 | Replace `new Window()`/`ShowDialog` in VMs with `INavigationService` (31 sites, 12 files) | 1 d |

**Exit gate:** grep counts → `App.` in VMs ≈ 0, `MessageBox.` in VMs ≈ 0,
`.Status =` only inside `IStatusService`.

### Phase 3 — God-file decomposition (1–2 weeks, highest value)
| # | Task | Effort |
|---|---|---|
| 3.1 | `SettingsViewModel` (128 KB) → `partial class` per settings tab (Appearance/Language/AI/Voice/…) or sub-VMs | 3 d |
| 3.2 | `ReportingDataService` (125 KB) → one partial/class per report family (PI/JO/DN/TI/Statement/VAT) | 2 d |
| 3.3 | `StatementGeneratorViewModel` (114 KB) → step sub-VMs (already has step views) + engine stays in Services | 2 d |
| 3.4 | `ProfitLossStepViewModel` (103 KB) & `TaxInvoiceDetailViewModel` (94 KB) → partials per section | 2 d |
| 3.5 | `App.xaml.cs` (62 KB) → `AppStartupPipeline` service: (1) config, (2) DI, (3) services init, (4) windows | 1 d |
| 3.6 | `MainWindow.xaml` (1,182 lines) → region `UserControl`s | 1 d |

**Rule going forward:** PRs cannot add > 1,000 lines to a file (enforce in CI).

### Phase 4 — Workflow enforcement (2–3 days)
| # | Task | Effort |
|---|---|---|
| 4.1 | `IStatusService.Transition(kind, entity, target)` wrapping `StatusTransitions.Validate` + audit-log | 1 d |
| 4.2 | Replace 24 direct `.Status =` sites; magic strings → typed enums (`DNStatus.Received`, `PIStatus.Approved`) | 1 d |
| 4.3 | Unit tests: every legal/illegal transition per kind (transition table is pure — fast tests) | 1 d |
| 4.4 | Add `WorkflowAuditTrail` table/service so every status change is traceable | 1 d |

**Exit gate:** `StatusTransitions.*` refs == all status writes; zero magic
strings; transition tests green.

### Phase 5 — MVVM modernization (optional, 1–2 weeks)
| # | Task | Effort |
|---|---|---|
| 5.1 | Adopt `CommunityToolkit.Mvvm` — `[ObservableProperty]`, `[RelayCommand]` replace hand-rolled boilerplate (partial-class compatible with Phase 3) | 3 d |
| 5.2 | Split `ProGlassAutomation.Core` (Services/Models/Workflow — no WPF) so Core is unit-testable headless + `ProGlassAutomation.App` (Views/VMs/Controls) | 3 d |
| 5.3 | `Agent/` (122 files) → separate project | 1 d |
| 5.4 | Replace `OnPropertyChangedPublic` with shared state services | 1 d |

### Phase 6 — CI & regression gates (2–3 days)
| # | Task | Effort |
|---|---|---|
| 6.1 | GitHub Actions: `dotnet build -warnaserror` + `dotnet test` on push (repo has `.github/`) | 1 d |
| 6.2 | Add `tools/i18n-audit.py` + `tools/audit-xaml.py` to CI | 0.5 d |
| 6.3 | Code-coverage gate on the workflow/transition + pricing calculators | 0.5 d |
| 6.4 | PR checklist template: MVVM rules §5.2, XAML rules §5.1, no new god files | 0.5 d |

---

## 7. Suggested priority order

1. **Phase 1 now** (1–2 days) — instant hygiene, zero risk.
2. **Phase 4** (2–3 days) — workflow integrity is a business-risk fix
   (illegal statuses today = wrong reports tomorrow).
3. **Phase 2** (3–5 days) — testability unlocks everything else.
4. **Phase 3** (1–2 weeks) — the big maintainability win.
5. **Phase 5 + 6** — modernization + guard rails.

## 8. KPI dashboard (track after each phase)

| Metric | Today | Target | Phase-1 result |
|---|---|---|---|
| Root `.md` files | 35 | ≤ 6 | ✅ **5** |
| Dead converter keys in `App.xaml` | 6 | 0 | ✅ **0** |
| Dead view-local XAML keys | 38 | 0 | ✅ **0** (40 registrations removed) |
| App-scope duplicate keys | 3 | 0 | ✅ **0** (BrushKeyConverter, StringEqualsMultiConverter, TintedCardStyle ×2) |
| Theme-level dead keys (WARN) | — | verify | ⚠️ **155** — flagged, needs runtime verification (Phase 1.7) |
| `App.*` refs in ViewModels | 470 (42 files) | 0 | Phase 2 |
| `MessageBox.` refs in ViewModels | 208 (33 files) | 0 | Phase 2 |
| Direct `.Status =` writes outside workflow service | 24 | 0 | Phase 4 |
| Code-behind event handlers (business) | ~100 | 0 | Phase 2 (MainWindow.xaml.cs = 15, worst) |
| Files > 60 KB | 9 | 0 | Phase 3 |
| XAML god files > 800 lines | 14 | 0 | Phase 3 |
| Tests | 834 | ≥ 1,000 | Phase 6 |
| `tools/audit-xaml.py` | — | PASS in CI | ✅ **PASS** (exit 0, runnable in CI) |
| `tools/i18n-audit.py` | PASS | PASS | ✅ PASS |

---

## 9. Phase 1 — completed (2026-09-01, commits `7dbefc3`→`943f6ee`)

| # | Task | Status |
|---|---|---|
| 1.1 | Delete/consolidate 26 stale root `.md` files; move 5 living docs into `Docs/` | ✅ root 35 → 5 |
| 1.2 | Remove 6 dead converter resource keys; merge `BoolToVis` alias (14 sites) | ✅ + 3 dead converter classes deleted |
| 1.3 | Verify 10 `Controls/` components | ✅ 8 used / 2 dead deleted (`GradientHeroCard`, `Sparkline`) |
| 1.4 | `tools/audit-xaml.py` — dead keys, duplicates, handlers, god files | ✅ PASS; scoping rules documented in header |
| 1.5 | Resolve TODO/FIXME markers | ✅ 1 real marker audited → business item (§9.1) |

### 9.1 Tracked follow-ups from Phase 1 (NOT deleted — require runtime verification)

1. **155 theme/app/tool-level dead resource keys** (list via
   `python3 tools/audit-xaml.py --quiet`) — `VisualThemeEngine` may key some
   dynamically; verify on a running build, then delete.
2. **`OtherChargeRow.MetricQty` delivery-row hack** — documented business
   rule (delivery = per-invoice = 1 trip, matches PDF/Excel export); proper
   fix (PER_ROUND source + editable qty) is a product decision → schedule in
   a product backlog, do not change silently.
3. **`MainWindow.xaml.cs` 15 event handlers** — Phase-2 command migration
   target.
4. **14 XAML god files > 800 lines** (incl. `InquiryListView` 1429,
   `ReviewStep` 1399, `MainWindow` 1182) — Phase-3 decomposition targets.

---

## 10. Phase 4 — completed (2026-09-01)

| # | Task | Status |
|---|---|---|
| 4.1 | `IStatusService.Transition/Validate` wrapping `StatusTransitions` + audit | ✅ `Services/Workflow/IStatusService.cs` + `StatusService.cs` (in-memory 200-entry ring + log line + `Transitioned` event); singleton in DI; `App.Status` accessor |
| 4.2 | Replace direct `.Status =` user-transition sites; magic strings → typed constants | ✅ 3 user-transition gaps migrated (DN Detail `Received`, JO Detail save, PI SavedList `Approved`); magic strings → `DeliveryNoteStatusValues` / new `ProformaInvoiceStatus` consts; 2 sites verified already-validated (DN List, JO SavedList, PI SavedList:763); lifecycle-forced writes documented by design (PI Repo `MarkCancelledAsync`, snapshot restore, save-only normalize) |
| 4.3 | Transition tests | ✅ existing `Tests/Agent/StatusTransitionsTests.cs` (valid/invalid/idempotent/empty per kind) + NEW `Tests/Workflow/StatusServiceTests.cs` (9 tests: record, reject-never-record, newest-first, event, case-insensitivity, actor/note, unknown kind) |
| 4.4 | Workflow audit trail | ✅ log-based + in-memory ring (bounded); DB `WorkflowAuditTrail` table deferred — needs EF migration + runtime verification (tracked) |

**Behaviour changes (intended):** illegal transitions are now blocked with a
localized warning instead of silently persisted — e.g. a JO can no longer
move `Delivered → Draft`, a PI can no longer become `Approved` after
`Cancelled`. First-save of a new JO (no prior status) is exempt.
**Excluded by design** (documented in `IStatusService` header): snapshot
replay, mapper sync, sample data, report filters, lifecycle-forced
cascade writes.

---

## 11. Phase 2 — in progress (2026-09-01, foundation shipped)

| # | Task | Status |
|---|---|---|
| 2.1 | `IDialogService` (wraps `MessageBox` + conveniences) | ✅ `Services/Dialogs/` — singleton in DI, `App.Dialogs` accessor |
| 2.2 | Replace `MessageBox.` refs in ViewModels | ✅ **209/209 sites, 33 files → `App.Dialogs.Show(...)`**; VM `MessageBox.Show` = 0 (target 0). Passthrough = byte-identical behaviour; single interception point for theming/audit/fakes |
| 2.3 | Replace `App.*` refs in ViewModels with constructor injection | ✅ 470 refs → **0 in VM bodies** across 59 files. DI ctor (services) + parameterless chain ctor resolving `App.ServiceProvider` (fallbacks: `DialogService`/`App.Config`/`App.Presence`/`App.Connectivity`). Parents pass services to constructed children (PI wizard steps, PrintPreview→PrintableInvoice, StatementModule→List/Generator, SalesReport→6 report VMs, MainViewModel→GlobalSearch). `ModuleWallpaperRow` takes `AppConfig`. **Remaining 112 `App.*` refs = chain-ctor boundary lines only** + documented keeps (Ai/`RaiseAiProviderChanged` events, `SpecGroup` row model deferred to Phase 3, `App.xaml` comment). Row models `SpecGroup` still use `App.Specs` by design (service-owned construction sites). |
| 2.4 | Replace DB refs in VMs with repository interfaces | ✅ `IDatabaseServices` facade (`Services/Database/`) wrapping the three static helpers VMs actually used — `DependencyGuard`, `DocumentLifecycle`, `AppDataRoot` — injected into 12 VMs (list/detail VMs, report base + 6 report VMs, StatementGenerator/List, Settings, SalesReport). 17 real call sites migrated (`CheckDependencyAsync`, `Subscribe/UnsubscribeDocumentsChanged`, `IsDemo`/folders). Remaining: `DependencyGuard.Kind` enum type refs only (argument to the facade); `DatabaseBackup.BackupService`/`SchemaHealthService`/DB-diagnostics in SettingsViewModel deferred to the Phase-3 god-file split (Settings-only maintenance features). Note: the audit's "84 refs" census was textual (incl. usings/comments); real call sites ≈ 17. |
| 2.5 | Code-behind event handlers → commands/behaviors | 🔄 **2.5a done** (MainWindow, worst file): 19 handlers → 12 removed (7 accordion → `Behaviors/AccordionBehavior.Group`, 5 dropdown + logo + dim overlay → `TogglePopupCommand`/`LogoClickCommand`/`CloseAllPopupsCommand` on `MainViewModel` + `Behaviors/ClickToCommandBehavior`; 5 empty popup handler pairs deleted). 4 kept (closing + view-lifecycle refreshes, documented). Real census: 174 XAML event attrs in 44 files (audit's 162 ≈ correct) — remaining 155 across 43 files for 2.5b+. |
| 2.6 | `new Window()`/`ShowDialog` in VMs → `INavigationService` | ✅ `IDialogService` gained `Create<TWindow>`/`ShowDialog<TWindow>`/`Show<TWindow>` (generic, Owner = main window, Activator factory). **16 app-window sites → 0 `new …View/Dialog(` in VMs** (JobOrderPicker, JobOrderProformaPicker, ApprovalUploadChoice, InvoiceApproval, SavedInvoicesList, ProformaPicker, RenameAttachment ×4, WorkflowWarning, JobOrderAttachmentWarning, DuplicateResolution, AddEditSheet ×2, Buy/Use/History). Result properties read from the created window (type-safe). **Excluded by design:** OS file dialogs (`OpenFileDialog`/`SaveFileDialog`, 11 sites) and `PrintDialog` (2 sites) — not app navigation; wrapping deferred to Phase 5.2 Core split (low testability gain vs 11 call-shape rewrites). |

**Blocking note for 2.3:** the 5 list/detail VMs are constructed BOTH via DI
(transient) AND manually (`new DeliveryNoteListViewModel()` in parent VMs,
`new SavedInvoicesListViewModel { … }` in a dialog code-behind) — constructor
injection must convert the manual sites in the same commit. Work item for the
next Phase-2 pass.

**Toolchain note:** `msg-wire.py` / `msg-extract.py` now recognize
`App.Dialogs.Show(` as the same call-site shape; `msg-spec.json` stays frozen
as the Phase-D historical artifact (its line drift predates Phase 2).

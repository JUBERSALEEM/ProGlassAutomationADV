# RC-009 SECOND-PASS FORENSIC AUDIT — JOBS → SIZES cutting-size list empty

**Module:** Optimization (JOBS tab → SIZES tab)
**Symptom:** Tick a JOB on the left → SPECIFICATIONS panel and the middle pane
checklist populate → the SIZES / cutting-size list stays **EMPTY**.
**Audit date:** 2026-09-20
**Auditor scope:** second pass. The first pass (commit `e32f0caf`, "RC-009 FIX")
is treated as **unverified and not defended**. Nothing below assumes it was right.

---

## 0. THE MANDATED QUESTION, ANSWERED FIRST

> *At the exact moment the user selects a JOB, what object contains the cutting
> sizes, what collection contains them, what collection does SIZES bind to, and
> where between those two points are the values lost?*

| # | Question | Answer (file · class · member) |
|---|---|---|
| 1 | **What object contains the cutting sizes at the moment of selection?** | `OptimizationViewModel.BuildUpLayerVm` — one instance per pane box. Its `Pieces` property (`List<(string Glass, string Label, int W, int H, int Qty)>`) holds the pane's cutting rows. `ViewModels/Optimization/OptimizationViewModel.Jobs.cs:128` (class), `:154` (`Pieces`). |
| 2 | **What collection contains them?** | `OptimizationViewModel.JobSpecBreakdown` (`ObservableCollection<SpecOptionVm>`) → each `SpecOptionVm.Layers` (`List<BuildUpLayerVm>`) → each `BuildUpLayerVm.Pieces`. The readable subset is `PaneChecklist` (`ObservableCollection<BuildUpLayerVm>`, panes only). |
| 3 | **What collection does SIZES bind to?** | **Not `Pieces`.** `Views/Optimization/Tabs/SizesTabView.xaml` binds four things: `SizeGroups` (the merged cards), `CutPlanRows` (the expanded plan), `CuttingGroups` + `SelectedCuttingGroup` (the dropdown), `PieceSummary` / `HasPieces` (the empty-state hint). All four are **derived** collections rebuilt by `RefreshDerived()` (`OptimizationViewModel.Parts.cs:153`) from the one authoritative source: `Pieces` (`ObservableCollection<PieceEntryVm>`, `OptimizationViewModel.Parts.cs:87`). |
| 4 | **Where are the values lost?** | At **exactly one hop**, and it is not a "lost mid-flight" bug in the current source. `BuildUpLayerVm.Pieces` → `Pieces` is performed by `AutoRebuildPiecesFromSelectedJobs()` (`OptimizationViewModel.Jobs.cs:851`), reached **only** from `RebuildSelectedJobBreakdown()` (`:755`). That call site was added by commit `6c7224f1` (2026-09-19 11:57). **In any build earlier than `6c7224f1` the call does not exist**, so the rebuild fills `JobSpecBreakdown`, `PaneChecklist` and `Stocks` and *nothing at all* fills `Pieces` — specifications and panes appear, SIZES stays empty. That is the reported symptom, verbatim. See Phase 20 for the proof that such a build is what is running, and §7 for the three genuine defects found in the current source that produce *partial* SIZES loss. |

**One authoritative data path (now documented in code):**

```
ProductionDistributionEngine.DistributeGroup          Q0  job Step 5 rows
  → LoadJobsInternalAsync                             Q1  BuildUpLayerVm.Pieces
    → [tick] OnJobSelectionChanged                    (Jobs.cs:596)
      → ResetPaneTicksToDefault(job)                  (Jobs.cs:633)  ← RC-013
      → RebuildSelectedJobBreakdown                   (Jobs.cs:755)
        → JobSpecBreakdown / PaneChecklist            (middle pane UI)
        → SyncStocksFromSelectedJobs                  (STOCKS tab)
        → AutoRebuildPiecesFromSelectedJobs           Q2→Q3→Q4  Pieces
          → RefreshDerived                            Q5  SizeGroups / CutPlanRows
            → RebuildCuttingGroups                    Q6  CuttingGroups dropdown
              → TryBuildInputs (run)                      engine
```

---

## 1. METHOD — why this pass could reach a verdict at all

The first pass reasoned statically and then declared success. Static reasoning
over this chain produces *conservation* (nothing is lost), which is why it could
not explain an empty SIZES tab and reached for a fallback.

This pass built a **line-by-line executable port** of the chain and measured the
quantity at every hop:

* **Artifact:** `scripts/forensics/opt_jobs_sizes_trace.py`
* **Run:** `python3 scripts/forensics/opt_jobs_sizes_trace.py`
* **Result:** **31 PASS / 0 FAIL**
* It ports `DistributeGroup`, `LoadJobsInternalAsync`, `OnJobSelectionChanged`,
  `ResetPaneTicksToDefault`, `RebuildSelectedJobBreakdown`,
  `SyncStocksFromSelectedJobs`, `AutoRebuildPiecesFromSelectedJobs`,
  `RefreshDerived`, `RebuildCuttingGroups`, `RebuildCutPlan` and
  `TryBuildInputs`, and it measures **Q0…Q6** (defined in §0's diagram) plus the
  engine's own part count.
* Every defect reproduced below is reproduced **twice**: once with the pre-fix
  behaviour switched on (must fail in the documented way) and once with the
  shipped behaviour (must conserve). The harness defaults now mirror the shipped
  C#; each pre-fix reproduction passes its old flags **explicitly**, so flipping a
  default can never silently turn a defect demonstration into a no-op.

Benchmark fixture: the owner's own calibration job — 2 specs, DGU, 4 pane layers,
**152 pieces** (6mm clear ft = 64, 6mm ClimaGuard HP Neutral 70 low-e ft = 76,
6mm obscure clear ramly ft = 12).

**Environment limitation (disclosed, not worked around):** this workspace has no
.NET SDK and no network. `net8.0-windows` + `UseWPF` cannot be compiled or run
here. Every statement below is therefore either (a) measured by the Python port,
(b) proven from git/GitHub metadata, or (c) explicitly labelled *not yet
verified*. No claim of runtime success in the real WPF app is made anywhere in
this document.

---

## 2. THE 23-PHASE AUDIT

| # | Phase | Status | Evidence |
|---|---|---|---|
| 1 | Identify the real runtime view | **CONFIRMED** | `MainViewModel.Part1.cs:102` `OptimizationVM => _lazyOptimizationVM ??= ResolveOrNew<OptimizationViewModel>()`; `MainViewModel.Experience.cs:119` `ProfessionalContent = _currentViewModel`. The SIZES tab is `Views/Optimization/Tabs/SizesTabView.xaml`. No second view, no design-time-only twin. |
| 2 | Prove the job-selection path | **CONFIRMED** | CheckBox `IsChecked` → `JobOptionVm.IsSelected` setter (`Jobs.cs:84`, early-returns when unchanged) → `PropertyChanged` → `OnJobSelectionChanged` (`Jobs.cs:596`), attached at `Jobs.cs:542` via `AttachJobOption`. Measured: ticking one job moves Q1 152 → Q4 152. |
| 3 | Verify the logs actually execute | **CONFIRMED — and corrected** | The first pass added six `[OPT-JOBS]` Info lines around the same three methods. They executed but said nothing about *quantity*, which is the only thing that distinguishes "correctly empty" from "silently lost". Replaced by **one** line per rebuild carrying the build stamp and Q1…Q6 (`LogImportConservation`, `Jobs.cs:1026`). |
| 4 | Trace runtime values | **CONFIRMED** | Harness, benchmark job: `layers=152 sel=152 rows=78 qty=152 SG=152 CP=152 ENG=152 HasPieces=True`. 78 rows merge into 3 cutting groups totalling 152. |
| 5 | Trace the SIZES UI backward | **CONFIRMED** | `SizesTabView.xaml` → `SizeGroups` / `CutPlanRows` / `CuttingGroups` / `PieceSummary` → `RefreshDerived` (`Parts.cs:153`) → `Pieces` → `AutoRebuildPiecesFromSelectedJobs`. |
| 6 | Collection mismatch | **ROOT CAUSE FOUND (2 of 3)** | SIZES does **not** bind `Pieces`; it binds derived collections. Two mismatches existed inside the derivation: the dropdown keyed on `Trim()` with `Length>0`, the cards on the raw `Trim()` (`""`), the cut plan on `"—"`. Three different keys for one concept. Fixed by `CompositionLabel` (`Sizes.cs:126`) + `UnlabelledGlass` (`Sizes.cs:106`). See §7 RC-010. |
| 7 | `PropertyChanged` / `ObservableCollection` | **ROOT CAUSE FOUND (3 of 3)** | `SelectedCuttingGroup`'s setter raised `PropertyChanged` and **never re-derived** the filtered collections, while `Run()` already honoured the selection. Screen and engine disagreed. Fixed at `Sizes.cs:72`. See §7 RC-011. |
| 8 | Find all `Pieces.Clear()` | **CONFIRMED — 4 sites, all legitimate** | `ClearPiecesCommand` (`OptimizationViewModel.cs:92`), `AutoRebuildPiecesFromSelectedJobs` ×2 (empty-selection branch + pre-rebuild), RC-009's fallback (now **removed**). Plus `RestoreSessionCore` and `RunFromStep5`. No unowned clear. |
| 9 | Secondary clear / reset operations | **CONFIRMED** | `SetAllPanesSelected(bool)` (`Jobs.cs:819`) unticks panes but does **not** touch `JobOptionVm.IsSelected` — this is why "Clear" leaves the job ticked with every pane unticked, and why re-ticking the job rebuilt the same still-unticked layer objects. See RC-013. |
| 10 | ViewModel instance identity | **CONFIRMED** | Single lazily-cached instance; navigation does not reconstruct it (harness TEST F). The headless Step 5 host is a *different* instance with `IsHeadlessHost = true`, guarded at the top of both `RebuildSelectedJobBreakdown` and `AutoRebuildPiecesFromSelectedJobs` (RC-007, intact). |
| 11 | Headless / proforma host | **CONFIRMED, NOT REGRESSED** | `IsHeadlessHost` short-circuits before any clear. `RunFromStep5` still owns that instance's `Pieces`. My changes do not touch `LiveRun.cs`. |
| 12 | Job data source | **CONFIRMED** | `LoadJobsInternalAsync` reads confirmed Job Orders → `ProductionDistributionEngine.DistributeGroup` → `layer.Pieces`. **Not re-read** at rebuild time: a pane with zero rows means the *job* has no cutting rows. The new `noPaneRows` branch says exactly that on screen instead of rendering nothing. |
| 13 | Dimension source | **CONFIRMED** | Harness TEST D: `1234 × 2345` identical at Step5 row → `ProductionWorkItem` → `PieceEntryVm` → cut-plan row → `TryBuildInputs` part. No rounding, no swap, no unit change. |
| 14 | Zero-filters | **CONFIRMED, NOT BYPASSED** | `w<=0 \|\| h<=0 \|\| qty<=0` is kept exactly as-is. Rows it rejects are now **counted** (`rejectedRows` / `rejectedQty`) so the conservation check can tell "filtered" from "lost", and the operator gets `"every Step 5 row was rejected (W≤0, H≤0 or Qty≤0)"`. No filter was weakened, and no `IsSelected` is forced past one. |
| 15 | Grouping key | **CONFIRMED** | `(CompositionKey.Normalize(glass), W, H, Position.ToLowerInvariant())`. Position is load-bearing: an equal-size DGU has identical outer and inner rows, and without position the inner pane merged away. Harness TEST B pins 4+4 → 8. |
| 16 | Quantity + dimension conservation | **CONFIRMED, now enforced in product code** | Q0=Q1=Q2=Q4=Q5=Q6=**152** at every hop. `LogImportConservation` now evaluates the invariant at runtime and emits a **Warning** plus an on-screen `RunMessage` (`"Quantity not conserved: panes N pcs → merged N pcs → cutting list N pcs"`) on any mismatch. A future regression announces itself. |
| 17 | UI binding | **CONFIRMED** | `SizesTabView.xaml` bindings match the property names; no converter, no `Visibility` gate, no `ItemsSource` swap. `HasPieces=false` renders the empty hint — which is what the operator saw. |
| 18 | No log-only verification | **SATISFIED** | The verdict rests on measured collection contents and counts (Q0…Q6, `HasPieces`, `PieceSummary`, cut-plan rows, engine part count), not on log lines. Logs are now a *by-product* of the measurement, not the evidence. |
| 19 | Confirm the new build is running | **ROOT CAUSE FOUND (1 of 3) — see §3** | `<Version>` frozen at 2.0.1 since `d6fb00e7`; the fix commits came after; OTA can never offer them. |
| 20 | No new fallbacks yet | **SATISFIED — and the old one removed** | RC-009's fallback re-read the identical `layer.Pieces` through the identical filter that had just produced 0 rows. Harness REPRO1: **0 rows produced in 4 of 4 zero-dimension cases.** Provably dead. Removed. |
| 21 | Minimum correct fix | **SATISFIED** | 7 targeted changes, §7. No workaround stacked on RC-009; RC-009's two mechanisms are both gone (one deleted, one replaced by the correct semantic). |
| 22 | Build + test A–J | **WRITTEN, NOT EXECUTED — see §8** | `Tests/Optimization/OptimizationJobsToSizesConservationTests.cs` (19 tests). Cannot be compiled or run in this workspace. |
| 23 | Report | **THIS DOCUMENT** | |

---

## 3. PHASE 19/20 IN FULL — the user is not running the fixed build

This is the primary root cause and it is proven from metadata, not inference.

| Fact | Evidence |
|---|---|
| `<Version>` frozen at **2.0.1** since **2026-09-10 11:30** | `git log -L '/<Version>/,+1:ProGlassAutomation.csproj'` → `d6fb00e7` *"…v2.0.1 everywhere…"*. No later commit touched it. |
| The auto-import fix landed **2026-09-19 11:57** | `6c7224f1` *"fix: senior deep-debug AI toggle + jobs auto-import merge"* — adds the `AutoRebuildPiecesFromSelectedJobs()` call to `RebuildSelectedJobBreakdown`. |
| RC-009 landed **2026-09-19 13:05** | `e32f0caf` *"RC-009 FIX: job selection auto-imports cutting sizes — was only specs"* |
| Both are **after** the version freeze | ⇒ every one of those builds reports itself as **2.0.1**, identical to the build that predates them. |
| OTA can therefore never offer them | `Services/Updates/UpdateService.cs:713` `IsNewer` is a **strict** `a > b` compare. `2.0.1` vs `2.0.1` ⇒ no update offered, ever. |
| The only published release predates the module | `gh release list` → **`ProGlass Automation v1.0.14` (2026-08-07)**, Latest. Nothing at 2.0.x is published. |
| The pre-`6c7224f1` code reproduces the symptom exactly | Harness AB1 (`auto_import_on_job_tick=False`): `PaneChecklist rows = 4`, pane labels `['64 pcs','64 pcs','12 pcs','12 pcs']`, `STOCKS rows = 3`, **`Pieces = 0`, `SizeGroups = 0`, `HasPieces = False`, `PieceSummary = 'No pieces yet'`**. Pressing "Add selected panes → cutting list" was the only way to get `Q4 = 152`. |

**Conclusion.** A machine on a build older than `6c7224f1` shows *precisely* the
reported symptom, and no in-app mechanism could ever have told the operator — or
the previous audit — that an old binary was running. RC-009 was written against
source that already contained the real fix, which is why it "fixed" nothing
observable: **the fix was never in the running binary.**

Fixed as **RC-014**: `<Version>` / `<AssemblyVersion>` / `<FileVersion>` → **2.0.2**,
`AppInfo.FallbackVersion` → 2.0.2 (now `public`, so `ProGlassLicense.AppVersion`
no longer carries a third private copy of the number), a new
`AppInfo.BuildStamp` = `"2.0.2 (build 2026.09.20 14:03:11 UTC)"`, stamped into the
startup log, the Optimization module's opening breadcrumb, and every
`[OPT-JOBS]` import line.

*License-safety check performed before bumping:* `LicenseValidator.CheckVersionPolicy`
only enforces `MinimumApplicationVersion` (default `"1.0.0"`) and
`MaximumApplicationVersion` (default **null** ⇒ not enforced). `AllowedMajorVersions`
is unset. A 2.0.1 → 2.0.2 patch bump cannot invalidate an issued license.
Side effect, intended: `AppConfig.SkipLoginBoundVersion` no longer matches, so the
splash asks for login once after the update.

---

## 4. WHY RC-009 WAS WRONG (not merely unverified)

RC-009 added two mechanisms. Both are now removed.

**(a) `if (layer.IsPane) layer.IsSelected = true;` inside `RebuildSelectedJobBreakdown`** —
a blanket force over **every pane of every selected job**, on **every** selection
change.

* It *was* load-bearing for exactly one scenario. Harness AB2, Clear → untick job →
  re-tick job: `force=False` → **Q4 = 0, SIZES EMPTY**; `force=True` → **Q4 = 6**.
  Pane tick state lives on `BuildUpLayerVm`, which the `JobOptionVm` **owns**, not
  `JobSpecBreakdown`; `SetAllPanesSelected(false)` unticks them permanently, and
  re-ticking the job rebuilt the same still-unticked objects.
* It was **not** load-bearing for a first tick: layers are constructed
  `IsSelected = true` (`Jobs.cs:426`), so a first tick already worked from
  `6c7224f1` onward.
* It **regressed operator intent**. Harness REPRO4b: job R1 + R2 ticked (Q4 = 8),
  operator unticks R1's pane (Q4 = 3, correct), then unticks and re-ticks the
  *unrelated* job R2 → **Q4 = 8 again**. Glass the operator deliberately removed
  was silently re-added to the cutting list — and would have been cut.

**(b) The `merged.Count == 0` "fallback"** — dead code. It re-read the identical
`selectedLayers[*].Pieces` through the identical `w<=0 || h<=0 || qty<=0` filter
that had just produced zero rows. Harness REPRO1: **0 rows in 4 of 4**
zero-dimension cases. Its only observable effect was to relabel a *correct* empty
outcome as a bug and print `"BUG: … layer.Pieces may be empty or filtered"`.

**Replacement — RC-013, per-job pane reset.** `OnJobSelectionChanged` now calls
`ResetPaneTicksToDefault(job)` for the job that **just became selected**, before
the rebuild. Measured:

| Scenario | RC-009 blanket force | RC-013 per-job reset |
|---|---|---|
| First tick of a job | 152 ✓ | 152 ✓ |
| Clear → untick job → re-tick job | 6 ✓ | **6 ✓** |
| Re-ticking job P2 after removing P1's pane | **8 ✗ (re-added)** | **3 ✓ (stays removed)** |
| Re-selecting P1 itself | 8 ✓ | **8 ✓** |

Re-entrancy was checked, not assumed: a job only now becoming selected is not yet
in `JobSpecBreakdown`, so its layers carry no `OnLayerSelectionChanged`
subscription (the previous rebuild detached it) and the reset writes raise nothing.

---

## 5. WHAT WAS *NOT* THE CAUSE (eliminated with evidence)

Tab visibility · `ProfessionalContent` identity · all tab code-behind ·
`Feed`/`Phase14`/`Phase15` · CutList send · a swallowed exception ·
`LoadSavedSessionsAsync` · csproj globbing (`<Compile Remove="Tests/**"/>`,
`Licensing/**`, `Tools/**` confirmed) · `SelectedCuttingGroup` null write-back ·
value converters · `CompositionKey.Normalize` idempotence · dimension parsing ·
the merge key's position component · `IsHeadlessHost` · silent catch blocks.

---

## 6. DEFECTS FOUND AND **NOT** PATCHED (deliberately)

* **RC-G — `ComposeForSlot` `FirstOrDefault`.** `Jobs.cs:470` tags the inner rack
  with only the *first* Pane2 occurrence, so a DGU+LAM spec would leave "Lam Inner"
  untagged. **Unreachable today:** `DguLamPricingPanelSnapshot` carries no sheet-size
  fields and `ApplyStep4PaneAndSheetMetadata` writes `0` for DGU+LAM, so
  `group.InnerSheetWidthMm > 0` is false and `ComposeForSlot` is never called.
  Patching unreachable code on an unbuildable box is how workarounds get stacked.
  Recorded, not patched.
* **RC-H — the JOBS tab cannot run a DGU+LAM job.** For the same reason its sheet
  sizes are 0, `SyncStocksFromSelectedJobs` adds **no racks**, and `TryBuildInputs`
  refuses with *"Add at least one stock size and tick it for use (STOCKS tab)."*
  `LiveRun.cs` has a Sheet-Store cross-check fallback for compositions Step 4 left
  without a sheet (`wantedByGroup` vs `rackedKeys`); the JOBS path has no
  equivalent. This is a **feature gap**, not a data-loss bug, and it is unrelated to
  the reported symptom. Recorded, not patched.
* **A residual edge in RC-012.** `CompositionKey.Normalize` maps a composition of
  literally `" glass"` to `""`, which `BuildCuttingGroups` then labels
  `(unlabelled glass)` while the SIZES dropdown offers the raw `" glass"`. Picking
  that entry still refuses the run. This is pre-existing (the old raw compare
  mismatched it too), it needs a hand-typed piece whose entire glass ref is the
  word "glass", and closing it properly means deciding whether `CompositionLabel`
  should normalize — a semantic change to every consumer at once, not something to
  slip into a bug fix that cannot be compiled here. Recorded, not patched.
* **`Tools/LicenseGenerator/LicenseGeneratorCore.cs:133`** still says
  `appVersion: "2.0.1"`. It is a *separate* csproj excluded from the main assembly,
  so it cannot see `AppInfo` without a new `ProjectReference`; and the value is
  inert (no version policy is enforced). Left alone.

---

## 7. THE FIX SET APPLIED (minimum correct, no stacking)

| ID | File · member | Change |
|---|---|---|
| **RC-010** | `Sizes.cs:106,126` · `Parts.cs:193` · `Sizes.cs:150,188` | One `UnlabelledGlass` const + one `CompositionLabel(glassRef)` helper used by the dropdown, the merged cards, the cut plan, `Run.BuildCuttingGroups` and `RebuildIndustrialReport`. Replaces four spellings of the same concept (`Length>0`, `""`, `"—"`, the literal). The cards' `GroupBy` also became case-insensitive to match the dropdown. |
| **RC-011** | `Sizes.cs:72` | `SelectedCuttingGroup`'s setter now calls `RefreshDerived()`. Proven non-re-entrant: `RebuildCuttingGroups` writes the **field**, never the setter, and re-resolves `previous` — which is the value just set and is present in the rebuilt list; the ComboBox write-back then early-returns on equality. |
| **RC-012** | `Run.cs:313` | The run's group filter compares `CompositionKey.Normalize` of both sides — the same canonical key the groups were built with. Removes the *"selected cutting group has no valid pieces"* refusal when a hand-typed list holds two spellings of one glass. |
| **RC-013** | `Jobs.cs:596,633` · `Jobs.cs:755` | Per-job pane reset on unselected→selected; RC-009's blanket force deleted from the rebuild loop. |
| **RC-009 (removal)** | `Jobs.cs:851` | Dead fallback deleted. Replaced by counted rejections + an explicit conservation invariant + honest on-screen messages (`"…carry no Step 5 cutting rows…"`, `"…every Step 5 row was rejected (W≤0, H≤0 or Qty≤0)…"`, `"Quantity not conserved: …"`). |
| **RC-014** | `ProGlassAutomation.csproj:37` · `AppInfo.cs:30,58,66,104` · `ProGlassLicense.cs:39` · `App.StartupCore.cs:265` · `OptimizationViewModel.cs:83` | Version 2.0.1 → **2.0.2** (all three assembly fields), `FallbackVersion` public + bumped, new `BuildTimeUtc` / `BuildStamp`, stamped into the startup log, the module breadcrumb and every import log line. |
| **Seam** | `Jobs.cs:653` · `:542` | `AttachJobOption(job)` — the picker's wiring extracted into one idempotent method, used by `LoadJobsInternalAsync` **and** by the tests. Not a test-only back door: `LoadJobs()` applies its results solely through `Application.Current.Dispatcher`, which is null under xUnit, so without this seam the chain is untestable at all. |
| **Logging** | `Jobs.cs:1026` | Six verbose `[OPT-JOBS]` Info lines → **one** consolidated line carrying the build stamp and Q1…Q6, at Warning level with `"— QUANTITY NOT CONSERVED"` when the invariant breaks. |

Diff: **10 files, +383 / −153**, plus one new test file. No XAML changed. No
filter weakened. No UI layout touched.

---

## 8. TESTS WRITTEN — AND THEIR VERIFICATION STATUS

`Tests/Optimization/OptimizationJobsToSizesConservationTests.cs` — 19 tests
covering the required A–J matrix plus a pin for each defect:

| | Scenario | Assertion highlights |
|---|---|---|
| **A** | single job / pane / size | 1 row, qty 4, `HasPieces`, 1 group, 4 cut-plan rows |
| **B** | multiple panes, equal-size DGU | outer **and** inner both survive (position in the merge key), 10+10 = 20 |
| **C** | multiple jobs | two jobs merge into **one** row of 76; `SelectedJobs.Count()==2` |
| **D** | different W/H, one composition | 2 rows, 1 group, `SizeRows=="2"`, 12 pieces |
| **E** | qty > 1 (`[Theory]` 2 / 17 / 152) | Q1 == Q4 == Q5 == qty; cut plan expands |
| **F** | navigate away / return | fresh VM over the same job data ⇒ identical Q4, group count, plan count |
| **G** | clear + reselect | `ClearPiecesCommand` → untick → re-tick ⇒ 20 restored (**RC-013 pin**); plus per-pane untick/re-tick |
| **H** | run optimization after import | `Stocks` non-empty, all pieces parse, `RunQuickCommand` ⇒ `RunMessage` is none of the four refusals |
| **I** | change selection after populated | dropdown re-filters **immediately** (**RC-011 pin**: 140 → 76 → 140) while `Pieces` still holds 140; unticking a whole job removes exactly its quantity |
| **J** | app reload | fresh VM over the **same** job objects ⇒ identical result |
| **RC-010** | unlabelled composition | gets its own dropdown entry; selecting it shows exactly its 13 pieces in cards **and** cut plan; the other 64 are still in `Pieces` |
| **RC-013** | anti-regression | ticking job 2 must **not** re-tick the pane the operator removed from job 1 (42, not 72) |
| **Conservation** | 2-spec job, 6 pane rows | Q1 = Q4 = Q5 = Q6 = **152**, 6 pane rows → **3** merged rows, per-group counts 64/76/12 |
| **Honest empties** | 3 tests | no pane rows / all rows uncuttable / no job selected ⇒ the right message, never a silent empty |
| **RC-014** | build identity | `BuildStamp` non-blank, contains version + "build" + "UTC"; `FallbackVersion == "2.0.2"` |

**These tests have NOT been compiled or executed.** There is no .NET SDK in this
workspace. They are written against the public surface only
(`AvailableJobs`, `AttachJobOption`, `IsSelected`, `Pieces`, `SizeGroups`,
`CutPlanRows`, `CuttingGroups`, `SelectedCuttingGroup`, `Stocks`, `PaneChecklist`,
`SelectedPaneCount`, `JobSpecBreakdown`, `HasPieces`, `PieceSummary`, `RunMessage`,
`ClearPiecesCommand`, `RunQuickCommand`), and the test project has
`TreatWarningsAsErrors=false` and a `ProjectReference` to the app.

Run them with:

```
dotnet test Tests/ProGlassAutomation.Tests.csproj --filter "FullyQualifiedName~OptimizationJobsToSizesConservationTests"
```

---

## 9. WHAT YOU MUST DO TO CLOSE THIS OUT

1. **Build 2.0.2 on Windows** and confirm the first log line reads
   `ProGlass 2.0.2 (build 2026.09.20 HH:MM:SS UTC) starting`. If it still says
   **2.0.1**, you are looking at the old binary and nothing below matters.
2. **Run the tests** in §8. All 19 must pass.
3. **Reproduce the original symptom** on the 2.0.2 build: JOBS → tick a job →
   SIZES. Then grep the log for `[OPT-JOBS] import build=`. That single line now
   states `paneQty`, `mergedQty`, `piecesQty`, `sizesQty`, `sizeGroups`,
   `hasPieces` — it will tell you which hop is empty, or that none is.
4. **Publish the release.** Until a 2.0.2 GitHub release exists, OTA has nothing
   to offer; the last published release is still `v1.0.14` (2026-08-07).
5. From now on, **bump the PATCH digit on every behavioural fix.** That is what
   makes the update reachable and the build identifiable; nine days of fixes were
   invisible because the number never moved.

---

## 10. STATUS

# ROOT CAUSE CONFIRMED + FIX NOT YET VERIFIED

* **Root cause — CONFIRMED**, with measured and metadata evidence, at three
  independent points:
  1. **The running binary predates `6c7224f1`** and is unidentifiable because
     `<Version>` was frozen at 2.0.1 while `IsNewer` is a strict compare and the
     only published release is v1.0.14. In that build nothing populates `Pieces`,
     so specifications and panes appear while SIZES stays empty — the symptom
     verbatim (harness AB1).
  2. **RC-010/RC-011** — three different composition keys and a dropdown setter
     that never re-derived, which made glass vanish from SIZES *after* a group was
     picked (harness REPRO2: 9 of 13 pieces; REPRO3: the tab did not move).
  3. **RC-013** — RC-009's blanket force re-added glass the operator had removed
     (harness REPRO4b: 3 → 8 without consent).
* **Fix — applied to source, NOT YET VERIFIED at runtime.** `net8.0-windows` WPF
  cannot be built or executed in this workspace, so no runtime proof exists.
  Verification achieved here: the Python port of the shipped control flow measures
  **31 PASS / 0 FAIL** with conservation at every hop; 19 xUnit tests are written
  and awaiting execution on a machine with the SDK.
* The words *fixed*, *working* and *resolved* are deliberately **not** used
  anywhere in this document to describe runtime behaviour.

---
---

# ADDENDUM — same day, second session: the upstream chain (RC-015 / RC-016)

**Trigger:** the operator reported four symptoms together and confirmed they are
**running the 2.0.2 build** — so RC-010…RC-014 are in the binary and the cutting
list is *still* empty. That rules out the SIZES-tab derivation as the cause of
what they see and moves the defect upstream, into the Proforma Invoice wizard.
The first-pass audit stopped at the Optimization module boundary; this addendum
crosses it.

**Operator-reported symptoms**
1. Step 4 spec **colors get reset**.
2. Step 7 shows a **negative profit margin** — the invoice prints as a loss.
3. The Optimization source lock **shows jobs but no sizes** in the cutting list.
4. A **confirmed PI is not read as a Job Order**.

**Operator-supplied facts:** running 2.0.2 · Step 5 grid **does** show real W×H
and Qty · Step 4 **goes empty as soon as steps are switched** (corrected from an
earlier "still filled after restart").

## A. The causal chain, proven at code level

```
GoToStep(4)  ProformaInvoiceViewModel.cs:815-822
  └─ if (!Step4VM.HasRestoredSnapshotState) Step4VM.RefreshSheetStoreData();
        HasRestoredSnapshotState is set ONLY by RestoreFromSnapshot
        (PricingEstimatorViewModel.Part1.cs:858/867) ⇒ FALSE for every
        newly created invoice ⇒ the refresh fires on EVERY entry to Step 4.
     └─ LoadSheetStoreDataAsync → RefreshOuterFromStoreAsync / RefreshInnerFromStoreAsync
          ├─ ReplaceSizes(...)  →  target.Clear()  UNCONDITIONALLY,
          │                        even when the store returns 0 sizes
          │                        (DguParametersViewModel.SheetStore.cs:302, 330, 422-433)
          │                        ⇒ the sheet-size dropdown is EMPTIED   ← symptom "Step 4 goes empty"
          └─ RepriceOuterAsync / RepriceInnerAsync
               else branch:  OuterBasePrice = 0m;  InnerBasePrice = 0m;
               ("never keep a stale previous pane price")
               (DguParametersViewModel.SheetStore.cs:376-381, 411-416)
               ⇒ the QUOTED SELLING RATE IS DESTROYED on any store miss
GoToStep crossing 4→5  (:827-828)
  └─ PrepareStep5FromStep4 → SyncStep5SelectionsFromStep4 (:672)
       └─ ApplyStep4PricingSnapshot (SpecGroup.Part1.cs:839)
            clears HasManualStep5SpecOverride / HasManualStep5GlassTypeOverride
            and overwrites type, spec name, colours and BaseRate from the
            now-zeroed Step 4                        ← symptom 1 "colours reset"
            (and RefreshGlassColorsFromSpecName, :1084, sets OuterGlassColor=""
             AND InnerGlassColor="" whenever the spec name is blank)
GoToStep (:839-840)  silent draft save built AFTER the handoff
  └─ the degraded state is PERSISTED with no Save press
GoToStep(9) (:836-837)  Step9VM.RecalculateAsync
  └─ selling rate 0 against a live cost side        ← symptom 2 "negative margin / loss"
     and Step 7 simply prints Step 9's numbers
```

**Why symptom 3 survives the 2.0.2 build — the frozen snapshot.**
`JobOrderSnapshot.EmbeddedInvoice` is assigned in **exactly one place in the
codebase**: `JobOrderMapper.cs:88`, at conversion. Every other reference
(`DeliveryNoteMapper`, the CSV/PDF/Excel layouts, `BuildEntityFromSnapshot`) is a
**read**. The mapper deep-clones deliberately — *"so subsequent PI edits don't
mutate the historical record preserved in the job order"*. So the Optimization
picker does not read the PI the operator is looking at; it reads the clone frozen
at the moment of approval. **Fixing Step 4 today does not heal a Job Order that
was converted while Step 4 was degraded.** That is why "Step 5 shows real sizes"
and "the cutting list is empty" are both true at once.

**Symptom 4 is an independent, separately proven bug.**
`JobOrderStatus.Released` is written in **exactly one place in the codebase**:
`JobOrderMapper.cs:92`, and only when the PI is *already* `"Approved"` at that
instant. Nothing ever upgrades an existing Draft. Meanwhile
`ConvertToJobOrderAsync` (`SavedInvoicesListViewModel.Part2.cs:129`) **returned
early** when any Job Order already existed for the PI. So a job created before
approval — the Job Orders → New picker even has a `ProceededWithoutApproval`
path, audited as *"RELEASED WITHOUT CONFIRMED APPROVAL"*
(`SavedJobOrdersListViewModel.Part1.cs:826`) — stayed **Draft forever**, and the
picker's `!Is(j.Status, Draft)` filter (`Jobs.cs:330`) hid it, while the status
line still read *"Loading confirmed job orders…"*.

## B. Fixes applied in this addendum

| ID | File · member | Change |
|---|---|---|
| **RC-015** | `SavedInvoicesListViewModel.Part2.cs` · `ConvertToJobOrderAsync` | Approval now **releases** an existing Draft job instead of returning early. Follows the list-side status-change pattern exactly: `StatusTransitions.Validate(Kind.JobOrder, Draft → Released)` (a legal transition, `StatusTransitions.cs:82`), update the indexed `Status` column, keep `FullSnapshotJson`'s `Production.Status` in sync (the detail view, its save path and the production-sheet exports read the status from the snapshot), audit-log it. Voided jobs and non-Draft jobs are skipped; an illegal transition logs a Warning and is skipped rather than forced. |
| **RC-016** | `PricingEstimatorViewModel.Part1.cs:349` · `ProformaInvoiceViewModel.cs:815` | New `HasPricedStep4Data` (any section with `EstimatedRatePerSqm > 0`) gates the navigation-triggered `RefreshSheetStoreData()`. A Step 4 that has produced a rate is no longer wiped on entry; a fresh one still picks up current stock. Nothing is lost by gating — genuine stock changes still reach Step 4 via `SheetStoreVM.StockLedgerChanged → RefreshSheetStoreData` (`MainViewModel.Part2.cs:254`), and restores via `RestoreFromSnapshot` (`Part1.cs:840`). |

**Deliberately NOT changed:** the `OuterBasePrice = 0m` store-miss behaviour
itself. It is a documented decision (*"never keep a stale previous pane price
such as Clear 25 on a Low-E/Ramly pane"*) and the correct replacement is an
operator-ownership flag like Step 5's `HasManualStep5BaseRateOverride` — which
does not exist in any of the SGU/DGU/LAM panels and would mean new state plus
XAML bindings across three sub-view-models in the pricing heart of the app,
written blind with no compiler. RC-016 removes the *trigger* (an unrequested
refresh on navigation) without changing how a price is computed when the operator
genuinely changes a colour. Adding the ownership flag is the recommended
follow-up.

**Also NOT changed:** the frozen `EmbeddedInvoice` contract. Auto re-syncing it
on every PI save would violate the mapper's explicit historical-record guarantee.
Healing it needs an operator-initiated "re-sync from source PI" action — a
feature decision, not a bug fix.

## C. How to get sizes for the job that is broken RIGHT NOW

The existing Job Order still carries the degraded clone, and no code path
refreshes it. Two ways forward, no rebuild needed for the first:

1. **Re-convert (immediate).** Job Orders → New → pick the approved PI → accept
   the *"already has N job(s)"* prompt. `CreateJobOrderAsync` mints the next free
   `JOB-…` id and embeds **today's** PI. Because the PI is Approved, the new job
   is born **Released** and the Optimization picker will show it with sizes.
   Void the stale job afterwards so the two are not both cut.
2. **RC-015 (after this build).** Approving the PI again releases a Draft job —
   but it does **not** re-embed the snapshot, so a job whose *data* was degraded
   still needs route 1.

## D. Status of this addendum

# ROOT CAUSE CONFIRMED + FIX NOT YET VERIFIED

Every mechanism above is read from source with file:line evidence, and the
two destructive writes (`ReplaceSizes` → `Clear()`, `Reprice*Async` → `= 0m`)
are unconditional in the code as written. But this workspace still has no .NET
SDK: RC-015 and RC-016 are **not compiled and not run**. Verification requires
the operator's Windows machine:

1. Build. Confirm About / first log line reads **2.0.3-or-later** (bump the
   patch digit again for this change set — see RC-014's rationale).
2. Create a NEW invoice, fill Step 4, switch to Step 5, return to Step 4.
   **Before RC-016:** sizes and prices blank. **After:** unchanged.
3. Approve a PI whose job was created as Draft beforehand. **Before RC-015:**
   job stays Draft and never appears in Optimization. **After:** status becomes
   Released, an audit entry *"Draft job released on invoice approval"* appears,
   and the job is listed in the Optimization JOBS tab.
4. Step 7 margin on a quote that previously printed a loss.
5. For the job that is already broken: route C.1, then confirm the cutting list
   populates and `[OPT-JOBS] import build=…` reports `paneQty == piecesQty ==
   sizesQty`.

---
---

# ADDENDUM 2 — WORKFLOW CONFORMANCE (RC-017): does the code follow the intended pattern?

**The operator stated the intended workflow explicitly:**

> Step 4 create specs → APPLY (or continue) → Step 5 must carry the selected
> glass type + specs → Step 6 other charges → Step 7 SAVE saves the PI →
> Step 8 attach the document and MARK AS APPROVED, which must behave like the
> Proforma list's approve → approving auto-creates the Job Order → opening
> Optimization and selecting that job must import the PI's specs **and sizes** →
> the optimization result feeds Step 6, or can be run directly from Step 5.

**Verdict per step, read from source:**

| # | Intended | What the code does | Verdict |
|---|---|---|---|
| 1 | Step 4 → APPLY → Step 5 carries glass type + specs | `GoToStep` crossing 4→5 → `PrepareStep5FromStep4` → `SyncStep5SelectionsFromStep4` (`:672`) → `ApplyStep4PricingSnapshot` (`SpecGroup.Part1.cs:839`). The handoff exists and is the single path. | **FOLLOWS** — but was being corrupted before the handoff by the destructive Step 4 refresh (**RC-016**) |
| 2 | Step 7 SAVE saves the PI | `ExecuteSaveInvoiceAsync` (`PrintableInvoiceViewModel.Part1.cs:468`): body-lock check, cross-step validation, insert/update, `StampSaveArtifactsAsync`, success dialog. | **FOLLOWS** |
| 3 | Step 8 MARK AS APPROVED behaves like the list's approve | `AttachmentsStepViewModel.ApproveInvoiceAsync` (`:187`) only guards duplicate attachments and opens a file picker. The real approval is `SavedInvoicesListViewModel.CompleteApprovalAsync` — documented as *"runs after the user uploads the signed PI / LPO in Step 8"* — reached via `OnStep8AttachmentUploaded` (`ProformaInvoiceViewModel.cs:987`). That method resolved the invoice id from `_pendingApprovalInvoiceId ?? _loadedInvoiceId`, and **`_loadedInvoiceId` is assigned in exactly ONE place (`:964`, inside `LoadInvoiceIntoWizardAsync`)**. Step 7's save never returns the new row id. So for an invoice **created in the wizard**, both were null → `if (id == null) return;` → attachment stored, **no approval, no Job Order, no message.** | **BROKEN — fixed as RC-017** |
| 4 | Approving auto-creates the Job Order | `CompleteApprovalAsync` → `ConvertToJobOrderAsync`. Only reachable from the list, or from Step 8 *when the invoice was opened from the list*. A pre-existing Draft job was never upgraded (**RC-015**). | **PARTIAL — RC-015 + RC-017 restore it** |
| 5 | Optimization: select job → PI's specs **and sizes** import | The picker reads `JobOrderMapper.DeserializeSnapshot(jo).EmbeddedInvoice` — a clone **frozen at conversion** (`JobOrderMapper.cs:88` is the only assignment in the codebase; every other reference is a read). Later PI edits never reach it. Draft jobs are filtered out (`Jobs.cs:330`). | **FOLLOWS in design; broken by the frozen clone + the Draft filter** |
| 6 | Optimization result → Step 6 | `Step6VM.BindOptimizationFeed(...)` (`:252`) reads the static `OptimizationResultFeed`, guarded by `IsForDocument(CurrentDocumentKey)`, into `WastagesSection.BindOptimizationFeedProvider`. The headless host publishes via `PublishLiveFeed` (`Feed.cs:74`). | **FOLLOWS** |
| 7 | Run optimization directly from Step 5 | `RunOptimizationFromStep5` (`:476`) → `_optimizationHost ??= CreateOptimizationHost()` → **`new OptimizationViewModel()`** with `IsHeadlessHost = true` (`:502-506`). Only `IsRunning` + two progress properties are mirrored back; the code states *"this quote never displays the host's own tab surface."* | **FOLLOWS — on an instance that is never displayed** |

## The answer to "it shows a result but SIZES has no sizes — how is it optimizing?"

**Because there are two different `OptimizationViewModel` objects, and the SIZES
tab you are looking at does not belong to the one that ran.**

* `_optimizationHost` — private to the Proforma wizard, `IsHeadlessHost = true`,
  **never displayed**. `RunFromStep5` fills *its* `Pieces` and `Stocks`, runs the
  real engine, publishes to `OptimizationResultFeed`. The optimization is genuine.
* `MainViewModel.OptimizationVM` — the lazy-cached module instance
  (`MainViewModel.Part1.cs:102`). Its SIZES tab renders *its own* `Pieces`, which
  are filled **only** by job selection on the JOBS tab.

Nothing carries a Step 5 run into the module instance. So the module's SIZES tab
is empty **not because the optimization failed** but because it is a different
object that never received that data — and it cannot fill itself from the job
either, when that job is Draft (RC-015) or its frozen clone has no cutting rows.

This is why several days of patching the SIZES derivation changed nothing
observable: **the SIZES tab was never the fault. Instance identity, the frozen
`EmbeddedInvoice`, and the dead Step 8 approval were.**

## RC-017 — the fix applied

`OnStep8AttachmentUploaded` (`ProformaInvoiceViewModel.cs:987`) no longer gives up
when both ids are null. The wizard always knows the document number (Step 7
refuses to save without one), so it resolves the saved row through the existing
`IProformaInvoiceRepository.GetByProformaIdAsync`, adopts it as
`_loadedInvoiceId`, and calls the **same single** `CompleteApprovalAsync`
pipeline. No second approval implementation was created. When the row cannot be
found, or no ProformaId exists, it now writes an explicit Warning stating that
the attachment was stored but **the invoice was not approved and no Job Order was
created** — previously that case was completely silent.

## Remaining gap — needs a product decision, not a patch

The two-instance split is deliberate (RC-007: the module's job-selection rebuild
was wiping the Step 5 run's cutlist, so they were separated). Making "run from
Step 5" and "open Optimization → SIZES" show the same data has two honest
options, and choosing between them is a design decision:

* **(A) Adopt the last run.** Keep two instances; add an explicit
  *"load latest run for this PI"* that copies the headless host's
  `Pieces`/`Stocks`/results into the module instance. Small, additive, no race.
* **(B) One instance.** Share a single `OptimizationViewModel` and give job
  selection and Step 5 runs an explicit owner token so they cannot fight over
  `Pieces`. Correct long-term, larger, and it re-opens exactly the race RC-007
  closed — not something to attempt blind without a compiler.

**Also still open:** the frozen `EmbeddedInvoice`. Healing an already-converted
job needs an operator-initiated *"re-sync from source PI"*; auto-syncing on every
PI save would break the mapper's explicit historical-record guarantee. Until
then, route **C.1** in the previous addendum (re-convert from the Job Orders
picker) is the way to get today's data into a job.

## Status of Addendum 2

# ROOT CAUSE CONFIRMED + FIX NOT YET VERIFIED

RC-017 is read from source and is a one-method change using only existing
repository and approval APIs, but it is **not compiled and not run** — this
workspace has no .NET SDK. Verify on Windows: create a NEW invoice (do not open
one from the list), fill Steps 1–7, SAVE on Step 7, go to Step 8, press APPROVE
INVOICE, attach a document. **Before RC-017:** the attachment is stored and
nothing else happens. **After:** the Signed-PI/LPO dialog appears, the PI becomes
Approved, a Job Order is created Released, an audit entry is written, and the job
appears in the Optimization JOBS tab with its specs and sizes.

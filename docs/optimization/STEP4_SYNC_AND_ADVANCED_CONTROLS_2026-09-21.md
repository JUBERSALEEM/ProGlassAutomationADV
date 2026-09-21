# Step 4/5 synchronization and advanced controls repair — 2.0.8

## Findings

The engine-preservation statement did not mean all advanced **controls** had survived the UI port. Comparing `4e1049c` with pre-port `024a1148` confirmed these regressions:

1. **Full spec notifications were disconnected.** Step 4 still emits `CurrentSpecFingerprint` after capturing its updated type/name/pane/sheet/rate snapshot. The imported wizard coordinator listened only to `SelectedGlassType` and `EstimatedRatePerSqm`. Same-price colour/thickness/sheet changes therefore did not take the live full-snapshot route. The type notification also arrives before the snapshot update completes.
2. **Rate-only sync marked itself as manual.** It assigned `group.BaseRate` without `_isSynchronizingFromStep4`. The setter classified that write as a typed override, blocking subsequent automatic rate updates.
3. **Advanced UI was reduced.** The port replaced the named five-stage selector with a numeric effort selector and removed the 50/40 breakout selector, dynamic margin hints and architecture explanation. That was a feature loss even though the advanced algorithms remained.
4. **Thickness was not following imported inputs.** The settings thickness could stay at 6mm after loading thick-glass panes. Per-group runtime substitution existed, but the displayed settings were misleading and did not track group selection.
5. **The shared thick-glass table had the opposite orientation.** It specified TM 50/BM 0. The latest instruction explicitly requires **TM 0/BM 50**.

## Implemented changes

### Step 4 → Step 5 and saving

- Listen to `CurrentSpecFingerprint` and section-count changes while at Step 4 or later; ensure the corresponding Step 5 groups exist before applying snapshots.
- Ignore intermediate events during pricing-snapshot restoration. The coordinator no longer consumes the early glass-type setter notification as a completed snapshot.
- Retain the existing ownership checks: genuine manual Step 5 type/spec/rate choices are not overwritten by unrelated Step 4 edits.
- Add `ApplyStep4RateSnapshot`, which wraps programmatic rate changes in the synchronization guard and leaves type/spec/pane metadata alone.
- Continue and direct forward jumps still flush the full handoff **before** the silent draft save. Backward exits from Step 4 and Return to List from Step 4 now flush it too.
- Existing saved type/spec/provenance/manual-override fields remain in the mapper. No database schema migration is required for the added JSON fields.

### SGU panel extra

- Add predefined fabrication notes, a **Combine panel extras** switch and custom text in the SGU Step 4 panel.
- Append the effective extra to the generated SGU specification, without duplicating an identical edge-polish suffix.
- Persist the selected option, custom-mode flag and custom text through section capture, primary invoice snapshot, JSON, restore and new-invoice reset.
- Include these fields in the content fingerprint. The spec-name/catalogue handoff therefore receives the extra too.
- Do not offer argon/spacer options for SGU: it has no sealed cavity. This is fabrication metadata, not an invented extra price.

### Step 5 tooltips

- Hover the per-spec **Outer / Inner / Single pane sheet summary** or specification heading for the Step 4 estimated selling rate and pane sheet-material rates.
- Distinguish the overall selling estimate from outer/inner raw sheet rates; do not label raw pane prices as separate selling estimates.
- Keep inline sheet dimensions; move the inline material prices into the tooltip.
- Store the estimate independently of editable `BaseRate`, so manual pricing does not falsify the estimate. Persist/restore that estimate and clear it when manual selection breaks its provenance. Older snapshots without an estimate show “not available” until synchronized, not a fabricated value.

### Advanced optimization settings

Restored the original named selector and its two-way mapping to persisted effort:

1. QUICK — Deterministic Shelf
2. Effort 2 — Multi-Order Grid
3. Effort 3 — Stratified Smart Search
4. Effort 4 — Windowed Best-Split & Sheet-Fill
5. Effort 5 — Deep Smart Placement & Consolidation

These are selectable effort/strategy levels, **not five sequential passes run for every job**. Actual engine routing still depends on effort, group size and finite inventory. Progress again exposes the architecture explanation, with the finite-inventory global-search exception stated.

- Homogeneous imported input and selected glass groups update settings thickness automatically. Explicit pane thickness metadata takes precedence over label parsing.
- Untouched house trims follow thickness changes; genuine manual trims survive. LOAD HOUSE DEFAULTS remains the explicit reset.
- Mixed thicknesses are identified in the UI; each runtime group receives its own house profile unless custom margins were deliberately supplied.
- Restore the thick-glass 50/40 breakout control. Choosing 40 is a sanctioned house alternative, **not** a reason to freeze thick margins onto thin groups.

## House table

| Thickness | LM | RM | TM | BM | Breakout | Kerf |
|---|---:|---:|---:|---:|---:|---:|
| Up to 6mm | 15 | 15 | 15 | 15 | 15 | 3 |
| 7–8mm | 30 | 30 | 30 | 30 | 30 | 3 |
| 10 & 12mm (existing thick bucket, 9mm+) | **50** | **50** | **0** | **50** | **50 default; 40 optional** | 4 |

The top/bottom correction is in shared `MarginProfile.DefaultFor`, so the engine input and display agree. Existing saved plans/custom margin values are not silently rewritten; load house defaults and rerun when the new table should apply.

## Engine integrity evidence

`git diff 024a1148 -- Services/Optimization Models/Optimization` contains **only** the requested thick-glass table correction and explanatory comments in `OptimizationModels.cs` (6 additions / 7 deletions). No packer, pattern, candidate-index, governor, partitioning, model-entity or other protected service file changed.

The manifest retains all original baseline hashes and records **one explicit approved exception** for the new margin-table hash. The verification script permits only that named exception. This is not a wholesale engine re-import or a silent re-baseline.

QUICK, advanced global OPTIMIZE and partitioned OPTIMIZE remain wired. Finite inventory retains the global-cap route. The 2,000,000 expanded-piece admission guard remains. No new throughput benchmark has been performed.

## Side-by-side assessment

| Area | Pre-port baseline | Previous port | 2.0.8 implementation |
|---|---|---|---|
| Full-spec live notification | Present | Listener omitted | Restored, after completed snapshot |
| Automatic price ownership | Guarded full handoff | Rate-only path incorrectly set manual flag | Guarded rate-only update |
| SGU panel extra | Not a separate persisted panel-extra control | Same gap | Added across UI/name/snapshot/restore |
| Five named stages | Present | Reduced to numeric effort | Restored |
| Breakout / dynamic margin hints | Present | Removed | Restored |
| Actual advanced packer | Advanced | Preserved | Preserved |
| 10/12mm top/bottom defaults | TM50/BM0 | TM50/BM0 | **TM0/BM50**, latest owner instruction |
| Windows end-to-end status | Not run here | User-reported failures | **Build/runtime acceptance still pending** |

Code-review completeness only: Step 4/5 synchronization **4/10 before this repair → 7/10 provisional**; advanced settings reachability **4/10 → 8/10 provisional**. These are not runtime reliability or performance ratings. No “verified working” score is assigned.

## Validation

Executed here:

- `python3 scripts/verify-optimization-port.py`: **14/14 portable source/contract checks pass**.
- New full-spec-sync, controls and SGU/tooltip guards fail against previous `HEAD` (`4e1049c`) as expected (negative controls).
- Tree-sitter parsing of changed/new C#: no syntax errors. This is not compiler/type checking.
- XAML resource lint: clean across 337 files, 116 warnings.
- XAML validation: zero fatal issues; existing duplicate `SettingsCardMargin` in the unrelated Settings view remains.
- `git diff --check`: clean.

Added C# behavioral coverage for repeated rate updates/manual-rate preservation, same-rate SGU↔DGU handoff, SGU extras capture/JSON/restore, all five stage mappings, imported/group thickness, custom-trim preservation and thick/alternate-breakout engine profiles. Updated house-table tests for the new orientation.

**No .NET SDK is installed here. C# compilation, xUnit execution and live WPF checks have NOT run.**

## Required Windows checks before release

```powershell
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release
```

1. SGU/DGU/LAM/DGU+LAM: change glass type, thickness, colour and sheet size, including equal-price changes. Continue, Back, direct-jump and return to the list; reopen the draft and verify each spec's identity and dimensions.
2. Change Step 4 rates repeatedly; Step 5 follows until its BaseRate is genuinely typed. Verify manual type/spec overrides are not reclaimed by a price-only edit.
3. Configure different SGU extras in two sections. Switch sections/steps, save/reopen and confirm exact option/custom text and generated names.
4. Hover each spec's sheet summary. Check its estimate and pane rates against its own Step 4 section, including a manually overridden BaseRate.
5. Import 6/10/12mm panes; change the cutting-group selector and check visible thickness/trim. Run all five effort levels on a small valid job. Verify TM0/BM50 and 50/40 breakout in actual layouts.
6. Exercise mixed-thickness jobs, manual trims, finite inventory, saved sessions, Step 5 live runs/feed, cancellation and the existing large-job acceptance checklist. Preservation of engine files is not a benchmark.

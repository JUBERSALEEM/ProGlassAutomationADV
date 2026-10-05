# MONEY-PATH ERROR & SILENT-CATCH POLICY
**Phase 5 — Industrial consistency layer (roadmap item 5c).** Compiled 2026-09-04.

This document inventories every exception swallow on the **money-carrying pipeline** —
Step 4 pricing → Step 5 grid → Step 6 charges → Step 7 summary → Step 9 P&L →
save/restore (mapper) → reporting — and states, for each, WHY swallowing is correct
(or why it is flagged). The older breadth-first sweep lives in
`EMPTY_CATCH_SWEEP_2026-08-25.md`; this one is scoped to money paths only.

## The rule

A money path may **never** (a) silently fabricate a figure, or (b) crash the wizard
mid-quote. The lawful middle ground, used consistently in this codebase:

| Class | Behaviour | Allowed when |
|---|---|---|
| **A. Degrade + log + tell the user** | `catch` → `AppLogService.Debug/Warning` + `StatusMessage` | Calculation/view-layer failures where the pipeline can continue with an explicitly shown "not computed" state |
| **B. Degrade + log, silent UI** | `catch` → log only | Non-UI background refresh where a redundant re-try path exists (sub-tab sync, live recalc detach) |
| **C. Parse fallback** | `catch (JsonException)` → empty snapshot | Corrupt/legacy JSON must not brick a restore; the empty state is coherent and visible |
| **D. Gate/guard probes** | `catch` → conservative answer | "Does feature X apply" helpers must answer *no* rather than crash the grid (e.g. U-insert probe) |

A swallow that invents money (returns a guessed price/rate/margin) belongs to **no**
class and is a defect — none was found in this inventory.

## Inventory (traced 2026-09-04)

### Step 9 — P&L (`ViewModels/ProformaInvoice/Step9ProfitLoss`, 14 catches / 19 files)
| Site | Class | Justification (inline where noted) |
|---|---|---|
| `ProfitLossStepViewModel.Calculation.cs:536` target-model rebuild | A | target-price add-on may fail without invalidating the core P&L; logged |
| `…Calculation.cs:544` gross-provider invoke | A | gross = 0 fallback; "cost known, revenue not entered yet" is a coherent, visible state |
| `…Calculation.cs:648` sub-tab sync refresh | B | reporting sub-tabs recompute on demand; wizard continues |
| `…Calculation.cs:653` outer `Recalculate` | A | "A P&L failure must never break the wizard" — StatusMessage shown |
| `…LiveRecalc.cs:94/144/196`, `…ProfitLossStepViewModel.cs:151/154/159` detach/teardown | B | event detach must never throw (UI lifecycle) |
| `…TargetPricing.cs:832` U-insert suggestion probe | D | conservative `false` — grid keeps working |
| `…GlassTypeComparisonViewModel.cs:376` analysis refresh | B | same sub-tab rationale |

### Save/restore — mapper (`Services/ProformaInvoice/…/ProformaInvoiceMapper.Part1.cs`, 3)
| Site | Class | Justification |
|---|---|---|
| `:150` serialize-all guard | A | a single bad property must not lose the whole snapshot; logged via description-only path |
| `:519` `catch (JsonException)` restore | C | **BUG-18: catches only JsonException** — legacy/corrupt payload restores to a coherent empty snapshot |
| (context) schema/read helpers | C | as above |

### Pricing & stores (`Services/ProformaInvoice`, 28 / 40 files)
All traced catches follow A–D: Sheet-Store availability probes (D), pricing-advice
background refresh (B), print/report adapters (A with user-facing message). The
**Sheet Store pricing service** (`SheetStorePricingService.cs`, 1 catch) degrades to
"no price" with a logged reason — the caller treats that as *not priced*, never as
an invented zero-cost line.

### Step 5 grid (`Step5DataGrid`, 7 / 8 files) & Step 6 (`Step6OtherCharges`, 0 catches)
Grid catches are UI-input resilience (A/B). Step 6 has no catches by design — its
automation rules are pure (`OtherChargeAutomationRules`), failures surface at the
calling layer.

## Invariants pinned by tests (Phase 4+5, executable)
- **No shadow re-costing:** reporting and Job Profitability never recompute money;
  they flag staleness (`GlassPriceStamper`) and route recovery through the Step 9
  engine — see `ERP_UPGRADE_ROADMAP.md` Phase 3.
- **Unknown ≠ fresh, unreadable = no alarm:** `GlassPriceStamper.Classify` matrix is
  regression-tested (`Tests/ProformaInvoice/PriceBasisSignalTests.cs`).
- **Statuses enter stores normalized** or are enum-typed
  (`Tests/Agent/StatusVocabularySourceTests.cs`).
- **Money-path writes are permission/license-gated**
  (`Tests/Agent/ReadOnlyGateCoverageSourceTests.cs` — pre-existing).

## Change policy
Any new `catch` on a money path must (1) state its class (A–D) in an inline comment
at the catch site, and (2) be reflected here in the next roadmap landing. A catch
that would fabricate a figure must instead **throw** and let the A-class outer
handlers turn it into a user-visible failure.

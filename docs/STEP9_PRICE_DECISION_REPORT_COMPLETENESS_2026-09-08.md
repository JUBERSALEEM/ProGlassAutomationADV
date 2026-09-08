# Step 9 Price-Decision Report Completeness — 2026-09-08

## Decision

Step 9’s export projection now treats each **Step 5 specification group** as an independent commercial decision. It does not compress all target data into one summary record and it does not reuse the word **base** for two different concepts without qualification.

This is a reporting-only enhancement. `SpecificationTargetCalculator`, Step 5 rate sourcing, target aggregation, U-insert treatment and actual P&L costing are unchanged.

## Terminology contract

| Report label | Canonical source | Meaning |
|---|---|---|
| **Step 5 given / quoted price** | `GivenPricePerSqm` | The customer rate held in the Step 5 grid. The source UI calls its field `Base Price`; reports describe it as a quote to avoid confusing it with the formula base. |
| **Normal formula base — no profit** | `BasePricePerSqm` | The pre-uplift DGU/SGU/LAM formula result. It is never derived from the Step 5 quote. |
| **Given judged for margin** | `GivenPriceJudgedPerSqm` | The rate used by the existing BASE and TARGET margins. It is reduced only when a U-insert is included inside the Step 5 quote. |
| **BASE result** | `ExtraProfitPerSqm`, `ExtraProfitMarginPercent` | Balance and margin against the normal formula base. |
| **Required target** | `FinalRequiredTargetPerSqm` | Rounded normal formula base plus the configured profit uplift. |
| **TARGET result** | `DifferencePerSqm`, `DifferenceValue`, `MarginAgainstTargetPercent`, `Status` | Quote balance plus the existing target margin/status. The report explicitly identifies raw quote balances versus judged-price margin/status where U-insert treatment makes them differ. |

## Export surfaces covered

### Direct Step 9 Overview — CSV, Excel and PDF

`Step9ReportExport.BuildOverviewRows()` now emits:

1. an explicit **JOB PRICE DECISION SUMMARY** mirroring Overview card 3’s area-weighted normal base, Step 5 quote, judged given, BASE balance/margin, required target, TARGET balance/margin and U-insert treatment; and
2. one `SPEC n · PRICE DECISION` section for every `TargetSpecsView` row.

Each specification section contains identity/area, formula inputs, normal formula base, Step 5 quote, BASE result, required target, TARGET result, status and formula/U-insert/warning audit text. The shared section-aware reporting exporter creates a distinct block for every unique specification section in CSV, Excel and PDF.

### Direct Step 9 Specification Price Decision report — CSV, Excel and PDF

The target report’s CSV and Excel columns are now explicitly headed:

- Formula inputs
- Normal formula base (no profit)
- Step 5 given / quoted price
- BASE result
- Required target (+ profit)
- TARGET result / status
- Audit / warnings

Their first mapper field is `SECTION` (`SPEC n · PRICE DECISION`), so two specification groups produce two report blocks and N groups produce N blocks. The PDF uses the same eight discrete decision lines per specification rather than a nine-column table, preventing the full formula and judgment explanation from being truncated on the page.

### Saved Order Report Generator — CSV, Excel and PDF

The saved-report projection now persists the additive target audit fields needed to render the same decision trail: yield, uplift, specification target, U-insert treatment/rate, judged given, BASE balance/margin/value, required target value and TARGET difference value.

CSV/Excel create one `STEP 9 · PI … · SPEC n PRICE DECISION` section per specification. The custom Order Report PDF renders one full pricing-decision card per specification and no longer caps the target-card list at twelve rows.

Older saved Order Report snapshots lack these additive fields. They render `not saved`/unavailable markers rather than falsely presenting default zero as a money amount or profit uplift. Regenerate the report from its Proforma to populate the richer audit fields.

## Formula reconciliation vector

For a DGU with:

- outer glass rate `48`
- inner glass rate `25`
- ASP rate `45`
- yield `0.85`
- Step 5 quote `160`
- target uplift `15%`

the reports show the calculator’s rounded sequence:

```text
Normal formula base (no profit)
(48.00 + 25.00) ÷ 0.85 + 45.00 = AED 130.88/SQM

Required target
AED 130.88/SQM × 1.15 (15% profit) = AED 150.51/SQM

BASE result
AED 160.00/SQM judged − AED 130.88/SQM = AED 29.12/SQM
BASE margin = +22.25%
```

No report recalculates the formula base, required target or either margin; it displays the corresponding canonical Step 9 result fields. The saved Order Report's displayed BASE balance value is the only presentation-only derivation: saved per-SQM BASE balance × saved area, rounded to two decimals, matching the existing Step 9 row display.

## Validation

- Added direct projection tests for the vector above, separate BASE/TARGET export fields, multi-spec section layout and included-U-insert handling.
- Added saved Order Report target-row/export tests for the same terminology/data separation, per-spec section count and old-snapshot no-fake-zero behavior.
- `git diff --check`: pass.
- C# brace-balance and conflict-marker scans: pass.
- Full `dotnet` build/test execution remains unavailable in this environment because no .NET SDK/compiler is installed.

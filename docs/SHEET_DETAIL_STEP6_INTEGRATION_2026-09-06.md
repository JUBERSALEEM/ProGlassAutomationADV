# Sheet Detail — Step 6 Wastage Integration (2026-09-06)

## Problem

The Step 9 **SHEET DETAIL** report was not connected to the **Step 6
wastage charge setup**. Its "wastage" figure was derived from a free-hand
sheet-count estimate instead of the operator's actual Step 6 wastage rows,
so every report surface (KPIs, the breakdown table and the detail screen)
showed a wrong wastage charge. It also surfaced an **offcut** concept that
does not exist in the business model.

The business model is strictly three-way:

| Term        | Meaning                                                            |
|-------------|--------------------------------------------------------------------|
| BUY SHEET   | Sheets bought, priced at the authoritative Step 9 glass row amount |
| USED SHEET  | Whole sheets required to cover cut SQM + wastage %                 |
| WASTAGE SHEET | BUY − USED sheets; the monetary charge is the Step 6 wastage charge |

There is deliberately **no offcut / scrap / remnant value**. Glass that is
bought but not used is wastage, full stop.

## Single source of truth

- **BUY SHEET** — `ProfitLossRow.Quantity` / `.Amount` from Step 9
  (`BuildGlassRowAsync`). When the operator set a SHEET QTY on the matching
  Step 6 wastage row, that quantity is already baked into the row.
- **USED SHEET** — `PnLMeasurement.AreaWithWastage(cutSqm, wastage%)` →
  `WholeSheetsRequired(...)`.
- **WASTAGE SHEETS** — `max(0, buy − used)`.
- **WASTAGE CHARGE** — `WastageRow.ResultAmount` from
  `OtherChargesViewModel.WastagesSection.WastageRows`, matched to the exact
  pane side, split across the row's checked `AvailableSources` by SQM share
  (`src.SqmValue / row.TotalSqm`) — byte-for-byte the same allocation
  `AppendWastageCostRows` uses in
  `ProfitLossStepViewModel.CalculationSupport.cs`, so the two screens
  reconcile to the penny.

## Formulas

```
requiredSqm    = AreaWithWastage(cutSqm, wastagePercent)
usedSheets     = WholeSheetsRequired(requiredSqm, sheetSqm)
buySheets      = priced-by-area ? usedSheets : glassRow.Quantity
wastageSheets  = max(0, buySheets - usedSheets)
buyPrice       = glassRow.Amount                      // Step 9 glass amount
usedPrice      = requiredSqm * glassRow.PricePerSqm   // AED
wastagePrice   = Σ Step 6 charge shares for this pane
```

The "priced by required area" basis is read from the glass row description
(the phrase `priced by required area`) — never guessed — so SGU panes
without a Step 6 SHEET QTY, whole-sheet DGU/LAM panes, and Step 6 SHEET-QTY
overrides are each handled exactly as Step 9 already priced them.

## Pane-side mapping (Step 9 → Step 6 source label)

| Step 9 side | Step 6 source label   |
|-------------|-----------------------|
| Outer       | `Outer (W1/H1)`       |
| Inner       | `Inner (W2/H2)`       |
| Lam Outer   | `Lam Outer (W2/H2)`   |
| Lam Inner   | `Lam Inner (W2/H2)`   |
| Middle / Middle N | `{side} (W2/H2)` |
| any other   | (none — no charge)    |

Only active wastage rows (`IsActive`) contribute a charge, mirroring
`AppendWastageCostRows`; a disabled row still owns its SHEET QTY in Step 9,
but carries no wastage charge.

## Changed files

- `ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs`
  — rewritten: three-way buy/used/wastage model, per-pane Step 6 charge
  resolution, offcut concept removed.
- `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml`
  — rewritten: KPI strip (right-aligned label/value rows, no far-left/far-right),
  GLASS TYPE column shows only SGU / DGU / LAM / DGU+LAM, three-way price
  reconciliation + overall report, no offcut surface.
- `Tests/Agent/Step9SheetDetailTests.cs` — pins the three-way model, the
  Step 6 source-of-truth wiring, and the absence of any offcut surface.

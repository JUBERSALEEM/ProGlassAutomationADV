# Sheet Detail — Step 6 Wastage Integration (2026-09-06)

## Model (proforma-based, NOT stock-based)

A job buys whole glass sheets and cuts them. The bought sheets split into
exactly two parts:

```
BUY SHEET  =  USED SHEET  +  WASTAGE (leftover)
```

| Term        | Meaning                                                            |
|-------------|--------------------------------------------------------------------|
| BUY SHEET   | Sheets bought, priced at the authoritative Step 9 glass row amount |
| USED SHEET  | Whole sheets required to cover cut SQM + wastage %                 |
| WASTAGE     | The LEFTOVER of cutting = BUY − USED (sheets, area, material value)|

Wastage is **derived**, never purchased. There is no separate "wastage
sheet" line and no scrap/remnant value.

## Step 6 wastage charge — a separate, cross-checked fee

The Step 6 WASTAGE CHARGE (`WastageRow.ResultAmount`) is a monetary fee the
operator set. It is read from the matching Step 6 wastage row's **SOURCE**
(outer / inner / lam outer / lam inner / middle), its **SQM**, its **SHEET
QTY** and its **rate**, then cross-checked against the derived usage:

| Condition                        | Badge      | Meaning                                   |
|----------------------------------|------------|-------------------------------------------|
| bought sheets < used sheets      | SHORTFALL  | Step 6 SHEET QTY is below what is needed  |
| charge > 0 but no leftover       | REVIEW     | charged for wastage that does not exist   |
| leftover > 0 but no charge       | NO CHARGE  | leftover glass has no Step 6 charge       |
| otherwise                        | OK         | wastage charge matches the leftover       |

A shortfall can only be produced by the Step 6 SHEET QTY override, because
every other buy path (SGU priced by required area, DGU/LAM whole-sheet
rounding) buys exactly what is needed.

## Single source of truth

- **BUY SHEET** — `ProfitLossRow.Quantity` / `.Amount` from Step 9
  (`BuildGlassRowAsync`). The Step 6 SHEET QTY is already applied there.
- **USED SHEET** — `PnLMeasurement.AreaWithWastage(cutSqm, wastage%)` →
  `WholeSheetsRequired(...)`.
- **WASTAGE** — `buy − used` (raw; negative ⇒ shortfall).
- **WASTAGE CHARGE** — `WastageRow.ResultAmount`, split across the row's
  checked `AvailableSources` by SQM share (`src.SqmValue / row.TotalSqm`) —
  the same allocation `AppendWastageCostRows` uses, so the two screens
  reconcile to the penny. The source's `DisplayLabel` (pane + colour +
  W1/H1) is surfaced so the operator sees exactly which outer/inner pane
  was charged.

## Formulas

```
requiredSqm    = AreaWithWastage(cutSqm, wastagePercent)
usedSheets     = WholeSheetsRequired(requiredSqm, sheetSqm)
buySheets      = priced-by-area ? usedSheets : glassRow.Quantity
wastageSheets  = buySheets − usedSheets
buyPrice       = glassRow.Amount
usedPrice      = requiredSqm × glassRow.PricePerSqm
wastageValue   = max(0, buyPrice − usedPrice)
step6Charge    = Σ Step 6 source shares for this pane
```

The "priced by required area" basis is read from the glass row description
(the phrase `priced by required area`) — never guessed.

## Pane-side mapping (Step 9 → Step 6 source label)

| Step 9 side | Step 6 source label   |
|-------------|-----------------------|
| Outer       | `Outer (W1/H1)`       |
| Inner       | `Inner (W2/H2)`       |
| Lam Outer   | `Lam Outer (W2/H2)`   |
| Lam Inner   | `Lam Inner (W2/H2)`   |
| Middle / Middle N | `{side} (W2/H2)` |
| any other   | (none — no charge)    |

Only active wastage rows contribute a charge; a disabled row still owns its
SHEET QTY in Step 9 but carries no charge.

## Changed files

- `ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs`
  — buy/used/wastage(leftover) model, per-pane Step 6 source readout, and
  the shortfall / review / no-charge cross-check states.
- `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml`
  — KPI strip, SHORTFALL + REVIEW banners, breakdown table with a STATUS
  badge column, full-screen detail with a STEP 6 WASTAGE CROSS-CHECK card
  and an OVERALL REPORT.
- `Tests/Agent/Step9SheetDetailTests.cs` — pins the three-way model, the
  Step 6 source-of-truth wiring, and the cross-check warnings.

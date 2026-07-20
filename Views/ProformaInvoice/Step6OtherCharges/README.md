# Step 6 - Other Charges

Supplementary charges section of the Proforma Invoice wizard.
Captures fabrication, processing, and delivery charges that get
added on top of the DataGrid (Step 5) glass material subtotal.

## Purpose

DataGrid (Step 5) only prices the raw glass panels. Real proforma
invoices include additional service charges like:

- Argon gas fill inside DGU cavity
- Silicon bed sealant for double glazing
- Overlap edge stepping
- Structural shutter bonding
- Beveled mitring cuts
- Polish, cutouts, holes (added in later phases)
- Glass color wastage allowances (added in later phases)

Each charge is calculated as: Quantity from DataGrid x Rate = Amount

## Phase Rollout

- Phase 7.11a  Metric Discovery Hub + basic charges (Argon, Silicon, Overlap, Shutter Bonding, Mitring)
- Phase 7.11b  Polish sub-types + Cutout/Holes with DGU/LAM auto-doubling
- Phase 7.11c  Wastages with color-based auto-aggregation across spec groups
- Phase 7.11d  Custom manual charges + full integration into Invoice Main Summary

This README documents Phase 7.11a.

## What This Is NOT

This is NOT the final invoice total. It intentionally excludes:

- Discount    (Phase 7.12)
- VAT 5%      (Phase 7.12)
- Grand Total (Phase 7.12)

The Total Other Charges shown here feeds INTO the Invoice Main
Summary module which is built after all Other Charges phases are done.

## Files in This Folder

| File                          | Purpose                                    |
|-------------------------------|--------------------------------------------|
| OtherChargesView.xaml         | Main UI - Metric Hub + charges table       |
| OtherChargesView.xaml.cs      | Code-behind (partial class only)           |
| README.md                     | This documentation                         |

Related files live at:

- ViewModels/ProformaInvoice/Step6OtherCharges/OtherChargesViewModel.cs
- ViewModels/ProformaInvoice/Step6OtherCharges/MetricDiscoveryHubViewModel.cs
- Models/ProformaInvoice/Step6OtherCharges/OtherChargeUnit.cs
- Models/ProformaInvoice/Step6OtherCharges/GridSourceOption.cs
- Models/ProformaInvoice/Step6OtherCharges/OtherChargeRow.cs
- Models/ProformaInvoice/Step6OtherCharges/PredefinedChargeTemplates.cs

## Architecture

Step 6 is fully driven by live data from Step 5 (DataGrid).
No standalone database or service layer needed for this phase.

    ProformaInvoiceViewModel
    |
    +-- Step5VM (DataGridViewModel)   ---> owns SpecGroups
    |
    +-- Step6VM (OtherChargesViewModel)
           |
           +-- takes Step5VM reference in constructor
           |
           +-- MetricDiscoveryHubViewModel
           |     subscribes to Step5VM.SpecGroups
           |     aggregates: LM1, LM2, LM1+LM2, SQM1, SQM2, SQM1+SQM2,
           |                 Total Pieces, Double Glass, DGU Cavity Area
           |
           +-- AllSources (pool of GridSourceOption)
           |     kept in sync with MetricHub via PropertyChanged
           |
           +-- ChargeRows (predefined templates)
                 each row has AvailableSources filtered by Unit type
                 each row calculates ResultAmount = MetricQty x Rate
                 Total = sum of active rows' ResultAmount

## Metric Discovery Hub

Top section of Step 6 shows live stats aggregated from all Spec Groups:

| Metric                 | Source                             | Unit |
|------------------------|------------------------------------|------|
| Total Glass 1 (LM1)    | Sum of Lm1 across all groups       | lm   |
| Total Glass 2 (LM2)    | Sum of Lm2 across all groups       | lm   |
| Combined Glass (LM)    | LM1 + LM2                          | lm   |
| Total Pieces           | Sum of Qty across all groups       | pcs  |
| Double Glass (1+1)     | Qty x 2 for DGU/LAM, x1 for SGU    | pcs  |
| Area Glass 1 (SQM1)    | Sum of TotalSqm across all groups  | sqm  |
| Area Glass 2 (SQM2)    | Sum of TotalSqm2 across all groups | sqm  |
| Combined Area (SQM)    | SQM1 + SQM2                        | sqm  |
| DGU Cavity Area        | SQM1 for DGU groups only           | sqm  |
| Primary Glass Refs     | Unique spec names from all groups  | list |

All values auto-refresh whenever DataGrid changes (add row, edit
dimension, change spec, add/remove spec group).

## Charge Rows (Phase 7.11a)

Five predefined templates ship in this phase:

| Charge          | Unit | Default Rate | Default Source     |
|-----------------|------|--------------|--------------------|
| Argon Gas       | Sqm  | 45.00        | SQM1               |
| Silicon bed     | Lm   | 6.00         | LM1                |
| Overlap         | Lm   | 6.00         | LM1                |
| Shutter Bonding | Lm   | 25.00        | LM1 + LM2          |
| Mitring         | Lm   | 30.00        | LM1                |

Each row has:

- Active checkbox (uncheck = row grayed out, excluded from total)
- Charge name + description
- Target Grid Source dropdown (filtered by Unit type)
- Metric Qty (auto-populated from selected source, read-only)
- Rate (editable per invoice)
- Formula & Result display

## Grid Source Filtering

Dropdown options are filtered by the charge's Unit type so users
cannot pick incompatible sources.

- Sqm charges see: SQM1, SQM2, SQM1+SQM2, DGU Cavity Area
- Lm  charges see: LM1, LM2, LM1+LM2
- Qty charges see: Total Pieces, Double Glass (1+1)

## Formula Display

Format: {MetricQty} {UNIT} x {Rate} AED = {Total} AED ({HINT})

Example:

    303.33 LM x 6.00 AED = 1,819.98 AED (W1 H1 PERIMETER (LM1))

When a row is unchecked, display shows: DISABLED

## LM Calculation Rules

Handled in DataGrid (Step 5) - already implemented:

- SGU: LM = (W + H) / 1000 x 2 x Qty  (perimeter with factor 2)
- DGU: LM = (W + H) / 1000 x 2 x Qty  (perimeter with factor 2)
- LAM: LM = (W + H) / 1000 x 4 x Qty  (all-side polish with factor 4)

Step 6 just consumes the pre-computed Lm1 / Lm2 values from each row.

## Change Tracking

The Hub subscribes to Spec Group property changes. Recalculation is
triggered only for these properties (performance guard):

- FooterTotalQty
- FooterTotalSqm
- FooterTotalSqm2
- FooterTotalLm1
- FooterTotalLm2
- SelectedGlassType

Other property changes (GroupName rename, scroll, focus) do NOT
trigger recalculation.

Batched paste operations (150+ rows) refresh the Hub only once
after the batch completes, thanks to SpecGroup's _isBulkAdding flag
from Phase 7.10b/c.

## Testing Checklist

- [ ] Metric Hub shows correct totals matching DataGrid footers
- [ ] Adding a spec group updates all metric cards
- [ ] Editing a row dimension updates metrics live
- [ ] LM1 dropdown value matches Hub LM1 value
- [ ] Checking Argon Gas shows formula and result
- [ ] Unchecking a row shows DISABLED and excludes it from Total
- [ ] Rate edit updates result live
- [ ] Total Other Charges = sum of active rows only
- [ ] DGU-only spec group shows correct DGU Cavity Area
- [ ] SGU-only spec group shows DGU Cavity Area = 0
- [ ] Double Glass count = Qty for SGU, Qty x 2 for DGU/LAM

## Future Phases

- Phase 7.11b - Polish sub-type dropdown (All Side / Both Height Side / As Per DWG)
- Phase 7.11b - Cutout/Holes with SGU=1 vs DGU/LAM=2 doubling
- Phase 7.11c - Wastages with color-based auto-aggregation
- Phase 7.11d - Custom manual charges section
- Phase 7.12  - Invoice Main Summary (Discount + VAT + Grand Total)
- Phase 7.13  - Print Preview / PDF layout
- Phase 7.14  - Save PI to database (Other Charges become part of PI record)
# Senior ERP Runtime Fix Notes — 2026-09-07

## Root cause analysis

### Step 6 wastage prices
The wastage row was still able to derive a displayed formula price from multiple selected pane sources by weighted averaging their rates. In real PI scenarios this allowed a Low-E pane price to be blended with a Clear pane price, producing rates such as 50.4486 instead of the exact Step 4 Low-E price 52. The Step 9 P&L path also preferred Sheet Store lookup before the Step 4 pane box price, so a colour name containing `Clear` could resolve to the generic Clear catalogue price instead of the exact Step 4 pane price for `Obscure Clear Ramly`.

### Step 9 Sheet Details
Sheet Details recalculated used sheets from a global Step 9 wastage percentage. That made Step 9 invent its own used-sheet quantity instead of reflecting the Step 6 wastage setup. The correct industrial ledger behaviour is: if Step 6 sets 17 sheets for a pane, Sheet Details must show 17 bought and 17 used, then use Step 6 optimization/utilization percentage to split the bought area into utilized and wastage area/value.

### Step 9 target/profitability source of truth
Target pricing was still reading live Step 4 tab values in several paths. That is unsafe when the invoice has multiple specs or when Step 5/Step 6 already hold the authoritative handoff metadata. Target calculations must prefer the per-spec Step 5 metadata that came from Step 4.

### Defined card-edge OFF
The OFF state was stopping custom outline application but retaining the last custom card-edge colour/thickness in configuration/resources. OFF now explicitly resets the custom edge state to defaults before applying the theme outline.

## Applied changes

1. **Step 6 exact pane-rate hardening**
   - `WastageRow.RefreshSelectedSourceDefaults()` no longer weighted-averages selected source prices.
   - If the row has a colour identity, only exact colour-matching checked sources can drive the formula price and sheet default display.
   - Mixed selected prices now show no automatic formula price instead of silently fabricating a blended price.

2. **Step 9 glass cost source priority**
   - `BuildGlassRowAsync()` now treats the Step 4 pane box rate handed through the spec group as authoritative.
   - Sheet Store lookup is only a legacy fallback when no Step 4 pane price exists.
   - This prevents `Obscure Clear Ramly` from falling to generic Clear pricing.

3. **Step 9 Sheet Details linkage to Step 6**
   - Sheet Details now resolves Step 6 setup before computing sheet counts.
   - When Step 6 sheet quantity exists, bought sheets and used sheets both equal that Step 6 quantity.
   - Step 6 optimization percent drives utilization/wastage area and value.
   - Step 6 setup is read even when its row is inactive, so setup diagnostics remain visible without forcing a charge import.

4. **Target pricing source cleanup**
   - Per-spec target rate cards now prefer Step 5/Step 4 handoff prices and spacer/processing metadata instead of blindly reading the live Step 4 panel.
   - When Step 6 sheet setup exists, target glass rates are converted from bought sheet cost to actual AED/m² over the finished Step 5 area, and the target yield factor remains 1.00 to avoid importing wastage twice.

5. **DGU+LAM metadata completion**
   - Step 5 now captures DGU+LAM outer/inner pane rates into the same per-spec fields used by Step 6/Step 9.

6. **Defined card-edge OFF reset**
   - Turning defined outline OFF clears the custom edge colour, resets thickness to default, applies default card-edge resources, and then reapplies the theme outline.

## Verification target

For the user-provided scenario, Step 9 should now price pane sheets from the exact Step 4 pane boxes:

- Low-E sheet: 3210 × 2250 = 7.2225 m² × 17 × AED 52 = AED 6,384.69
- Clear sheet: 3210 × 2250 = 7.2225 m² × 16 × AED 25 = AED 2,889.00
- Ramly sheet: 2500 × 1875 = 4.6875 m² × 1 × AED 32 = AED 150.00
- Production: 45 × 104.43 = AED 4,699.35
- Total production cost: AED 14,123.04
- Actual cost per finished DGU SQM: AED 135.24/m²

## Build note

The sandbox still does not contain the .NET SDK, so `dotnet build` cannot be executed here. Final compile validation must be run in Visual Studio or another machine with the project SDK installed.


## Follow-up correction — Step 9 SQM and Sheet Details

User correction: Step 9 must not use raw `width × height / 1,000,000` for chargeable glass SQM when a panel is below 0.5 m². Step 5 already applies the UAE glass minimum billable area (`SqmCalculator.CalculatePanel`), so Step 9 measurement now uses that same Step 5 billable SQM source for glass/spacer/production rows.

Sheet Details has also been changed to build rows directly from the Step 6 Wastage Charge Setup first. For every checked Step 6 wastage source with a sheet quantity:

- Sheet size is read from the Step 6 source.
- Price per SQM is read from the Step 6/Step 4 source price.
- Bought sheets = Step 6 sheet quantity.
- Used sheets = Step 6 sheet quantity.
- Utilization/wastage area is derived from the Step 6 optimization percentage.
- Step 9 P&L glass rows are used only as a fallback for panes that have no Step 6 setup source.


## Follow-up correction — shared Step 6 wastage rows across multiple specs

When one Step 6 wastage setup row has multiple checked sources, it means the operator intentionally mixed matching glass across specifications (for example Low-E outer panes from Spec 1 and Spec 2). Step 9 must not duplicate the full Step 6 sheet quantity on every spec.

A shared allocator now splits the Step 6 total sheet quantity back across the checked sources by each source's Step 5 SQM demand and the Step 6 optimization percentage. Example: at 72% utilization, a 7.2225 m² sheet contributes 5.2002 useful m². The allocator uses that useful sheet area to apportion the shared total, then rounds by largest remainder so the per-spec sheet quantities always sum back to the exact Step 6 setup quantity.

This allocator is used by both Sheet Details and Target Profitability so a shared 17-sheet Low-E setup can display as an intelligent split such as 11 sheets for one spec and 6 for another, instead of 17 + 17.


## Follow-up correction — actual-cost target rows also use shared sheet allocation

The first shared-sheet fix updated Sheet Details and the target rate helper, but the actual-cost rows shown inside Step 9 Target Profitability still called the older `ResolveStep6WastageSheetQuantity` method. That method returned the full Step 6 row sheet quantity for every matching spec source. It is now changed to use the same `Step6SheetAllocation` allocator, so a shared Low-E setup with 17 total sheets is split per related spec in the actual-cost target rows as well.

## Follow-up correction — Sheet Details utilization and pane identity

Sheet Details now keeps the Step 6 setup utilization visible at two-decimal precision and no longer defaults a shared setup to 100% when the setup has no explicit optimization percentage; in that fallback case it infers actual utilization from selected source SQM divided by total bought sheet area. Sheet breakdown rows also now show the pane identity beside the full specification: `Spec name — Outer/Inner · Glass name`, so a clicked row clearly identifies whether it belongs to Low-E, Clear, Ramly, etc.

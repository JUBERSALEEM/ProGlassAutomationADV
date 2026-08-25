# Dashboard layout revision — senior analysis (2026-08-25)

## What went wrong the first pass
The request was to widen the **data lists inside the cards**. The first
pass instead promoted the list cards to **full-width rows** (Job Orders,
Deliveries, Tax Invoices). That broke the bento rhythm: charts lost their
pair, rows stacked into long corridors, and the 7 dashboards stopped
reading as one system.

## Design rules restored (applied to all 7 dashboards)

1. **Bento pairing, never corridors.** A chart always pairs with its data
   list on the same row — chart is the visual, list is the evidence.
2. **Wider evidence column.** List columns widened from `1*` to `1.2*`
   (charts `1.6–1.8*`), so row data (icon · title/subtitle · meta) gets
   real width while the composition stays balanced.
3. **Rows breathe vertically.** Chart/list rows are 300–320 px
   (was 220) — more rows visible per card, no horizontal squeeze.
4. **One spacing system.** 14 px column gutters, 16 px row gutters,
   uniform card padding, KPI stacks on the same rhythm. No card mixes
   8/10/12 margins anymore.
5. **List rows fill the card width.** Feed rows are `Auto | * | Auto`
   grids (badge · text · meta) that stretch to the card's inner width;
   titles use ellipsis, never wrap-displacement.
6. **Consistent card anatomy.** Title left + meta right on every card
   header, so scanning any of the 7 dashboards costs zero relearning.
7. **Theme-driven colour only.** Every surface, keyline and text colour
   is a DynamicResource — Monet/gradient/dark switches recolor all 7
   dashboards without a second source of truth.

## Per-dashboard composition (final)
| Dashboard | Row A (top) | Row B (bottom) |
|---|---|---|
| Overview | pulse hero · delivery mix · 3 KPIs | revenue trend 1.8* + recent documents 1.2* (320 px tall), then sales/production/sheet-store strips |
| Job Orders | pulse tiles · status mix · priority | workload 1.6* + recent job orders 1.2* |
| Deliveries | pulse tiles · delivery mix · balance | status distribution 1.6* + recent deliveries 1.2* |
| Tax Invoices | collections pulse · ageing mix · overdue KPIs | revenue trend 1.6* + recent invoices 1.2*, then top customers * + ageing brackets * |
| Proforma | pulse tiles · pipeline mix · KPIs | top 5 customers (full-width chart) |
| Sheet Store | stock pulse · category mix · KPIs | available SQM by category (full-width chart) |
| Statements | balance pulse · status mix · KPIs | recent statements (full-width list) |

## Rule for future changes
If a data list feels narrow, widen the **column ratio** and the **row
height** — never orphan a card into its own corridor row.

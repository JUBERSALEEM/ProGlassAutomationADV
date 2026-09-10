# KPI Live Updates + Overview Delivery Count + Per-SQM + Profitability Layout

## 1. Overview KPI cards were not live
`OverviewDashboardViewModel` never subscribed to `DocumentLifecycle` — the
report pages reload on every document write, the dashboard did not, so the
top KPI tiles stayed stale until re-navigation. Now subscribed (debounced
400 ms, marshalled to the UI thread, reentrancy-safe via the page base).

## 2. Overview "delivery count showing wrong" — voided documents counted
The dashboard metrics service had **zero** `IsVoid` filtering: soft-deleted
proformas, job orders and delivery notes (invisible on every list screen)
were counted in Total Sales, Invoices, In Production, Total Jobs,
Preparing, Dispatched, Total Delivered pcs — and the recent-activity feed.
Every metrics query now excludes void documents, matching what the module
lists show. This is also why Overview disagreed with the Reports hub.

## 3. "Per SQM price" wrong in the Proforma Invoice hub
`CurrencyDisplayConverter`'s `sqm` branch renders a **price per m²**
(`AED 45.70 / m²`), but all four call sites bind it to `TotalSqm` — the
AREA. Every PI row, the PI and JO KPI hints, and the TI detail showed the
area mislabeled as a rate. New `area` branch (`45.70 m²`) added; all four
sites switched. The `sqm` rate branch stays for genuine rate values.

## 4. Delivery report KPIs ignored the Status filter
`GetDeliverySummaryAsync` (the KPI cards) never applied `filter.Status` —
the rows grid did, so a status-filtered grid showed unfiltered totals.
The same three bucket predicates now narrow the summary, written as plain
SQL-translatable branches (Eq() is not EF-translatable).

## 5. Profitability layout — breathing space + own Qty/Sqm column
Qty & Sqm were crammed into a tiny chip inside the PROFORMA column,
crowding both the name and the adjacent P&L numbers. The grid is now
7 columns — PROFORMA 1.6* | QTY/SQM 96px (own column, pcs over m²) |
REVENUE / COST / PROFIT 1.05* each | MARGIN 76px | COST BASIS 1.25* —
header and rows aligned, 16px numeric gutters kept.

## Verification
Static (no SDK in sandbox): 238-file XAML semantic sweep, changed XAML
XML-parsed, changed C# brace/paren balanced, zero `ConverterParameter=sqm`
left in views, asserted state battery (void sites ×4, delivery status
predicates, Overview subscription + debounce + marshal, 7-column defs ×2,
QTYSQM key present in 15/15 language files).

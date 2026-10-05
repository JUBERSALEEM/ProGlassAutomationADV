# User-Reported Fixes — Round 2 (2026-09-10)

## 1. Overview "Delivered (pcs)" showed an item's SR NO instead of qty
The Overview piece tiles summed the note header `TotalDeliveredQty` — a
denormalized copy that only some save paths refresh, so a stale/mis-seeded
header displayed an item's serial (34) as delivered pieces. The dashboard
now reconciles pieces from `DeliveryNoteItems` directly (live, non-master,
sent-out parents; today = [00:00, 24:00)) — the same source of truth the
Delivery report uses. Header drift can no longer reach the tiles.

## 2. Proforma hub rate/sqm — the real spec price from the PI
The old display put the AREA behind a "/m²" mask (user: "gave 170, shows
98" — 98 was m²). Rows now show `N pcs · X m²` on line one and the ACTUAL
spec price stamped from the PI's priced rows on line two: "AED 170 / m²"
(single rate) or "AED 150–190 / m²" (multi-rate) — `PiReportRow.RateDisplay`,
never derived from totals.

## 3. PI grid column — PROJECT only
Company removed from the grid column (header + cells); the column is now
PROJECT. Company remains in the detail expander and filters.

## 4. Company details — Delivery Notes in DOCUMENT ACTIVITY
The expander listed Proformas / Job Orders / Tax Invoices / Invoiced /
Outstanding but no delivery row (the model field `DeliveryCount` existed
and was never populated). Now hydrated: live non-master DNs mapped to the
company through its proformas; shown as a "Delivery Notes" row.

## 5. Tax Invoice hub — invoicable pipeline KPIs
New strip: TAX INVOICES | **INVOICABLE** (confirmed quotations value under
the filter) | **INVOICED** (billed total) | **BALANCE INVOICABLE**
(invoicable − invoiced, clamped ≥ 0) | OUTSTANDING | OVERDUE. Pending
count remains in the pending banner.

## 6. Tax Invoice rows — delivered & balance next to Quantity & Area
Each TI row's QUANTITY & AREA panel now also shows "Delivered D pcs ·
Balance B pcs", reconciled per quotation from delivery-note items
(row-key MAX ordered so split notes never inflate the balance).

## Verification
Static (no SDK in sandbox): 238-file XAML semantic sweep, changed XAML
XML-parsed, changed C# brace/paren balanced, asserted state battery
(item-metric rewrite, RateDisplay class placement, TI strip column
uniqueness 0–10, company chain, delivery reconciliation), export mapping
kept in sync.

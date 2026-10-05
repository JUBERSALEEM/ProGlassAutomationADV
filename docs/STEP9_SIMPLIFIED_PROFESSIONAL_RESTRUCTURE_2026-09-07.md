# Step 9 Professional Simplification / Restructure — 2026-09-07

## User problem

Step 9 was advanced but visually too complicated. The calculation engine is strong, but the screen mixed executive results, formulas, row audit trails, and extra analysis too closely. This made it harder for users to understand the answer quickly.

## Senior UI/UX decision

Keep the calculation logic intact and restructure presentation only:

1. **Main decision first** — show target result and overall decision before formulas.
2. **Money summary second** — show revenue/cost/margin after the decision.
3. **Spec-level detail third** — show one row per Step 5 specification separately.
4. **Extra audit detail after** — formulas, actual cost rows, and strategy analysis stay available but do not dominate the first view.

## Implemented changes

### Step 9 main tabs

Renamed the four sections into a simpler workflow:

- `1 · OVERVIEW`
- `2 · SERVICES`
- `3 · SHEETS`
- `4 · SPECS`

### Overview tab

- Added a simple reading map: Decision → Money → Spec Check → Extra Detail.
- Added a clear `MAIN DECISION` label before the KPI cards.
- Collapsed formula audit into `EXTRA DETAIL · calculation summary and formula audit`.
- Collapsed actual P&L line audit into `EXTRA DETAIL · actual glass cost rows and rate check`.
- Kept all existing bindings/calculations; only simplified hierarchy and readability.

### Services tab

- Updated wording to tell users the order: totals first, service rows second, recommendation/status last.
- Added visible `MAIN RESULT — SERVICE TOTALS` heading above KPIs.

### Sheets tab

- Updated wording to tell users the order: alerts first, totals second, details after.
- Added visible `MAIN RESULT — SHEET QUANTITY, WASTAGE AND EFFICIENCY` heading.
- Added visible `SECONDARY CHECK — MONEY RECONCILIATION` heading.

### Specs tab

- Main spec table is now clearly labelled `MAIN SPEC CHECK — ONE ROW PER STEP 5 SPEC`.
- Glass-type strategy analysis is moved into a collapsed `EXTRA DETAIL` expander, so the user first sees each specification result instead of a second advanced analysis panel.

## Why this is safer

The calculation engine remains untouched. Users now see the correct business answer first and only expand deeper evidence when needed. This reduces misunderstanding while preserving auditability.

## Validation

- Edited XAML parses successfully.
- Full XAML parse should be run before final push.
- Full .NET build is expected to remain unavailable in the sandbox because `dotnet` is not installed.

# Step 9 Detailed Export / Report Generator Audit — 2026-09-07

## Cross-check result

Step 9 already had export buttons for the advanced analysis screens, but the exported datasets were too shallow for a serious report generator. The screen could show rich details, while CSV/Excel/PDF only exported condensed columns. Also the Overview tab exported only the actual P&L cost breakdown, not the full executive/target/formula/cost story.

## What was missed

1. **Overview section needed its own full report.** The first tab is the main decision screen, so its CSV/Excel/PDF should include rating, target decision, revenue/cost/margin, target aggregate, warnings, per-spec summaries, formulas and actual cost rows.
2. **Spec export needed the six-section detail, not only the list-table columns.** The previous target export did not include formula text, pane rates, yield, uplift, U-insert treatment detail, base-vs-target balances, dimensions summary and warning detail.
3. **Sheet export needed Step 6 traceability.** The old export had the basic sheet math but not enough source details for audit: Step 6 source label, source SQM, sheet quantity, rate, efficiency, diagnostic and share percentages.
4. **Service export needed status.** Service rows now carry profit/loss status so the report can be filtered and tracked.
5. **Generic PDF export was capped.** The central generic PDF layout exported only the first 200 rows. That is not acceptable for a report generator that must carry the complete audit trail.

## Implemented upgrades

### Section 1 — Overview

Added new commands:

- `ExportOverviewCsvCommand`
- `ExportOverviewExcelCommand`
- `ExportOverviewPdfCommand`

The top Overview toolbar now exports a full `Step 9 Overview Detailed Report` containing:

- Step 9 rating and improvement guidance
- Target status and margin against target
- Required target price and expected profit margin
- Calculation status/time
- Gross, net, glass cost, production cost, total cost, profit and margins
- Given/required/difference aggregate target values
- Base margin and target margin
- U-insert treatment summary/detail
- All spec formula audit text
- All warnings
- Per-spec summaries
- Actual cost rows with quantity/rate/sheet/source detail

The collapsed actual-cost area keeps separate P&L-only CSV/Excel/PDF buttons.

### Section 2 — Services

Service export now includes:

- Section marker
- Spec
- Service
- Description
- Area
- Charge
- Cost
- Profit
- Margin
- Profit/Loss status

### Section 3 — Sheets

Sheet export now includes:

- Section marker
- Spec
- Glass type
- Pane/glass name
- Sheet size and sheet SQM
- Buy/used/waste sheets
- Buy/used/waste area
- Rate/SQM
- Buy price, used price, wastage value, Step 6 charge
- Step 6 source label
- Step 6 source SQM
- Step 6 sheet quantity
- Step 6 rate
- Efficiency percent
- Used/wastage/Step 6 share percentages
- Status and diagnostic message

### Section 4 — Specs

Spec/target export now includes the full detail set:

- Section marker
- Spec number and full specification name
- Build type
- Step 5 dimensions summary
- Area, given rate and given value
- Outer/inner/third pane rates
- ASP and processing rates
- Yield and target uplift
- Full formula text
- Specification target
- U-insert treatment, treatment detail, source, rate and applied result
- Base price, base difference, base value difference and base margin
- Final required target, difference, required target value and total difference value
- Margin against target
- Status and warning summary

### Central report generator layout

Updated `ReportingExportService` generic exports:

- Excel now wraps text, uses wider columns, and highlights section/total rows.
- PDF now exports all rows instead of silently truncating at 200.
- PDF uses a larger text allowance for small-column detailed reports so Overview details remain readable.

## Safety

Calculation logic was not changed in this pass. This was a reporting/export and traceability upgrade only.

## Validation

- XAML parse: PASS.
- Conflict marker scan: PASS.
- Edited C# brace balance: PASS.
- `git diff --check`: PASS.
- Full build: unavailable in sandbox because `dotnet` is not installed.

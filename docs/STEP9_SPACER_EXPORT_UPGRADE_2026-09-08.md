# Step 9 Spacer / Production Service Export Upgrade — 2026-09-08

## Senior analysis
The Step 9 cost exports already contained glass, spacer and production rows, but the exported report did not make the spacer cost obvious enough for ERP review. A user reading the PDF/CSV/Excel could see a normal specification name and an amount, but could miss that the line was not another glass pane — it was the DGU/ASP production service charge.

## Risk before upgrade
- Spacer rows could be read like normal specification/glass rows.
- The report did not explicitly show `SQM × rate = amount` for spacer service.
- Overall spacer production cost was not called out as its own subtotal.
- Service profitability did not show an explicit service class or rate-per-SQM basis.

## Upgrade applied
### Profit & Loss Cost Breakdown export
CSV, Excel and PDF now include additional columns:

- Cost Class
- Extended Rate
- Calculation Basis
- Extended Calculation
- Production / Service Note

Spacer rows are labelled as:

`PRODUCTION SERVICE · SPACER / ASP`

and their basis prints as:

`Spacer service quantity X SQM × AED Y/SQM`

The P&L export also appends:

- `SPACER TOTAL`
- `PRODUCTION TOTAL`
- final `TOTAL`

### Overview Detailed Report export
The overview export now adds an invoice-wide spacer/ASP production total and a spacer-by-spec breakdown. This makes it clear, for example, how much spacer production cost belongs to Spec 1 separately from the PI total.

### Service Profitability export
CSV, Excel and PDF now include:

- Service Class
- Service Qty (SQM)
- Rate / SQM
- Calculation Basis

Spacer/ASP services are explicitly classified as production/service work rather than normal glass material.

## Result
The exported artifacts now answer the operational question:

> How much spacer / ASP production cost is included in this PI overall, and how much belongs to each specification?

without forcing the user to manually interpret a generic specification line.

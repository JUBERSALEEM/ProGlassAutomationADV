# Step 9 Categorized ERP Report Upgrade — 2026-09-08

## Senior audit conclusion
The Step 9 exports had become technically detailed but too dense for a production/finance user. The report needed to separate the user's four mental sections:

1. **Overview / executive summary** — what is the PI result?
2. **Service / production work** — spacer, argon, cutting, tempering, lamination, U-insert and Step 6 add-ons.
3. **Sheet / material price** — sheet size, sheet SQM, sheet quantity, rate and wastage/efficiency.
4. **Specification price target** — normal spec price, rate components, target and margin.

## Problems corrected
- Argon Gas was not visible in Step 9 exports because core P&L intentionally excluded some commercial Step 6 add-ons from actual-cost rows.
- Spacer/ASP service was visible but could still be mistaken for a normal glass/spec row.
- Sheet report had too many narrow columns for PDF readability.
- Target/spec report exposed too many atomic columns instead of a readable ERP summary.
- Generic PDF export typography was too small for Step 9's wide reports.

## Upgrade applied
### Overview report
Renamed to **Step 9 Executive Cost Summary** and reorganized into readable sections:

- Executive Summary
- Sheet Price / Material
- Production Services
- Spacer / ASP by Spec
- Argon Gas / Step 6 Add-ons
- Profit Result
- Spec Price Target
- Formula Audit
- Spec Summary
- Actual Cost Ledger

### Service report
Renamed to **Production Services and Add-On Report**. It now surfaces active Step 6 add-ons such as **Argon Gas (Sqm)** with formula details, without pretending an internal production cost exists when no cost model is allocated.

### Sheet report
Renamed to **Sheet Price and Wastage Report**. Columns are consolidated around readable ERP concepts instead of dozens of narrow atomic fields:

- material category
- pane/glass
- sheet size
- sheet qty/area
- rate
- price calculation
- Step 6/wastage
- efficiency/status

### Specification target report
Renamed to **Specification Price Target Report**. It now shows normal spec price, rate components, target result and formula/status in grouped columns.

### PDF readability
Generic report PDF typography was increased and text truncation limits were relaxed. Fewer wider columns mean Step 9 PDF exports are easier to read and less crowded.

## Business result
A user can now clearly distinguish:

- **Glass sheet material price** — sheet SQM × sheet qty × glass rate.
- **Spacer/ASP production service** — spacer SQM × ASP rate, e.g. 12mm ASP at AED 45/SQM.
- **Argon Gas add-on** — active Step 6 DGU cavity gas filling charge.
- **Other production services** — cutting, tempering, doubling/lamination, U-insert and Step 6 add-ons.

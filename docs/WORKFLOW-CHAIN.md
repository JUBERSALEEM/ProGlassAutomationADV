# Workflow chain

Commercial spine is **Inquiry → PI → JO → Production → DN → TI → money**.  
Runtime of this chain: **NOT EXECUTED** (no SDK). Tree below is **code authority**, not a live run.

Legend: `ok` = in-scope GDS + status table exists · `split` = known vocabulary mismatch · `ui-open` = not restyled · `unproven` = no build/test/runtime here

```
Splash → Login / first-run
              │
              ├─ Master data ── Company · User · Specs · Sheet store
              │
              ▼
         Inquiry
         Received → Prepared → Sent → Confirmed
              │                    ↘ Hold / Cancelled
              ▼
    Proforma Invoice (wizard)
    1 Company → 2 Client → 3 Project → 4 Pricing (SGU/DGU/Lam)
    → 5 Grid + totals → 6 Charges / wastage → 7 Print preview
    → 8 Attachments → 9 P&L
              │
         Draft → Sent → Approved → (JO)
              ↘ Rejected / Expired / Cancelled ⇄ Draft
              │
              ▼
         Job Order
         Draft → Released → InProduction → Completed → Delivered
              │
              ▼
         Production
         PCC → Cutting → Edge → Holes → Mitre → Polish
             → Temper → Pair → DGU/Lam
              │
              ▼
         Delivery Note
         Draft|Dispatching → Dispatched → Delivered → Signed → Received
              │
              ▼
         Tax Invoice
         Draft → Issued → PartiallyPaid → Paid
                       ↘ Overdue
              │
              ├─ Credit note  Draft → Issued → Applied
              │
              └─ Statement / VAT / Order report
                    Filter → Execute → Preview → Review
```

## Status machines (code)

```
PI     Draft → Sent → Approved
                 ↘ Rejected | Expired | Cancelled ⇄ Draft

JO     Draft → Released → InProduction → Completed → Delivered
                 ↘ Rejected ⇄ Draft

DN     Draft|Dispatching → Dispatched → Delivered → Signed → Received
                 ↘ Cancelled ⇄ Draft     Received = terminal

TI     Draft → Issued → PartiallyPaid → Paid     Paid = terminal
                 ↘ Overdue / Cancelled / Void     Cancelled|Void = terminal

CN     Draft → Issued → Applied     Applied|Cancelled = terminal
```

## Side chains

```
PI ── Optimization (sizes/stocks/cut) ── Sheet store use/buy
JO ── Job profitability / portfolio
DN/TI ── Attachments, family, history
App ── Dashboard · Reports hub · Sharing · AI chat
```

## Separate leaves (not on the healthy spine)

```
UNPROVEN
  build · tests (~1727 facts unrun) · live WPF
  this clone HEAD c919443  ≠  remote 2a5e104

VOCABULARY SPLIT
  JO enum has no Rejected — transitions table does
  DN enum has no Dispatching|Cancelled — strings + mapper do
  TI display "Partially Paid" vs stored PartiallyPaid

UI NOT ON GDS
  Settings / Ios26
  About / Ios26
  PrintableInvoiceView
  Personal (GbNight)

LEFT ON PURPOSE
  AiChat 38×38 avatar
  Inquiry 38×38 glyph
  Generator KPI Typography.Label BasedOn
  Nested production controls: no ErpPageRoot
```

**Read:** spine is designed and restyled in-scope. Do not treat any leaf as production-green until SDK build + tests + a click-through of Inquiry→PI→JO→DN→TI.

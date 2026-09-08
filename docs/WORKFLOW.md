# Application Workflow

The end-to-end business chain of ProGlass Automation, with the real code
path for each handoff.

Read this if you want to understand **what the application does** rather
than how a folder is arranged. For the document map, see
[`README.md`](README.md). For architecture, see
[`ARCHITECTURE.md`](ARCHITECTURE.md).

---

## The chain

```
INQUIRY ──convert──▶ PROFORMA INVOICE ──confirm──▶ JOB ORDER ──dispatch──▶ DELIVERY NOTE
  + BOQ                9-step wizard                (factory floor)          (signed/received)
                              │                                                   │
                              │                                                   ▼
                              └────────── Step 9 P&L snapshot ──────────▶ TAX INVOICE (5% VAT)
                                                                                  │
                                                                                  ▼
                                                                        STATEMENT + REPORTS
```

Every arrow is a **real conversion with a real audit record**, not a
re-keying. Nothing downstream invents data the upstream did not carry.

| Stage | Status vocabulary | Owner |
|---|---|---|
| Inquiry | `Received` · `Prepared` · `Sent` · `Confirmed` · `Hold` · `Cancelled` | `Services/Inquiry/` |
| Proforma Invoice | `Draft` · `Sent` · `Approved` · `Rejected` · `Expired` · `Cancelled` | `Services/ProformaInvoice/` |
| Job Order | `InProduction` · `Completed` · `Delivered` · `Approved` | `Services/JobOrder/` |
| Delivery Note | `Draft` · `Dispatching` · `Dispatched` · `Signed` · `Received` · `Delivered` | `Services/DeliveryNote/` |
| Tax Invoice | `Issued` · `Paid` · `Overdue` | `Services/TaxInvoice/` |
| Statement | balances, not a lifecycle | `Services/Statement/` |

These vocabularies are the contract. They enter their stores through a
single normalising home (`ProformaInvoiceStatus`, `InquiryStatuses`), and
**an unknown status passes through untouched — it is never remapped to
something plausible.** A status you do not recognise becoming a status you
do is the single worst failure mode an ERP can have.

---

## Handoff 1 — Inquiry → Proforma Invoice

**Code:** `Services/ProformaInvoice/InquiryBoqConversion.cs`,
`Services/ProformaInvoice/InquiryIntegrationService.cs`

An inquiry carries a **scope of supply (BOQ)**: build type, spec, colours,
W×H, quantity, expected rate. Conversion copies those rows into the
proforma's Step 5 spec grid.

Guarantees:

- The copy is **atomic** — you do not get half a BOQ on a money document.
- A row that cannot be imported is **left on the inquiry as a note**, not
  silently dropped and not silently guessed at.
- Conversion is **permission-gated and logged**.

---

## Handoff 2 — Proforma Invoice (the 9-step wizard)

**Code:** `Views/ProformaInvoice/Step1…Step9`, `Services/ProformaInvoice/`

| Step | What it does |
|---|---|
| 1 Company | Copies all 8 legal-entity fields from Company Center. Edits **enrich the master at save with a preserve-blank contract** — a missed invoice field can never erase the company record. |
| 2 Client | Customer, TRN, billing profile; dedup and auto-registration. |
| 3 Project | Project identifier, contract reference, site mapping. |
| 4 Pricing | Rates per build type (SGU / DGU / LAM / DGU+LAM), sheet selection, air-spacer, processing. **Per-group production provenance**: a multi-section quote never costs every group with section 1's panel. |
| 5 Spec Grid | Multi-spec grid (W1/H1/W2/H2/qty → SQM + value), BOQ imports, manual overrides carrying provenance. |
| 6 Charges | Predefined + wastage + custom. **Auto-imports sheet colours from Step 5** — 7 colours produce 7 rows, never hardcoded. |
| 7 Summary | VAT 5%, round-safe; discount; totals. |
| 8 Attachments | Evidence chain per invoice. |
| 9 Profit & Loss | **The single pricing engine.** Sheet cost from Bin Store buy-prices, production rows from the spec's own provenance, target-price KPIs, buy-vs-sell cost basis, wastage. |

**Step 9 is the only place money is computed.** Its snapshot is what
everything downstream reads.

---

## Handoff 3 — Proforma → Job Order

**Code:** `Services/JobOrder/Mapper/JobOrderMapper.cs`

Real grid data crosses — not a re-typed summary. The honest geometry
parser carries sheet selections and spacer gaps so Step 4/5 stay
consistent with what the floor actually builds.

---

## Handoff 4 — Job Order → Delivery Note

**Code:** `Services/DeliveryNote/DeliveryNoteCreateService.cs`
(`CreateFirstFromJobOrderAsync`, `CreateOrOpenDispatchingAsync`)

A job can be delivered in several movements. `CreateOrOpenDispatchingAsync`
either starts a new dispatch or reopens the in-flight one, so partial
deliveries accumulate against the job instead of overwriting each other.
**Pending pieces are computed double-count-safe** — the same rule lives in
one place (`DashboardMetricsService`) rather than being re-derived per
screen.

---

## Handoff 5 — Proforma / Delivery → Tax Invoice

VAT-compliant conversion from the approved proforma, with its own status
workflow (`Issued` → `Paid`, or `Overdue` when terms lapse) and version
history.

---

## Handoff 6 — Tax Invoice → Statement → Reports

Statements group a customer's invoices into balances. Reports read the
persisted figures.

---

## The honesty contract

**A professional ERP must never silently invent money.** These rules are
enforced, not aspirational:

| Rule | Where |
|---|---|
| Reporting **reads** money from the persisted Step-9 snapshot; it never recomputes it | `JobProfitabilityViewModel`, `ReportingDataService` |
| A stale price basis is **flagged `⚠`**, never silently rescored | `GlassPriceStamper` |
| Unknown statuses pass through, never remapped | `ProformaInvoiceStatus`, `InquiryStatuses` |
| Every money-path `catch` is classed A–D and documented | [`MONEY_PATH_ERROR_POLICY.md`](MONEY_PATH_ERROR_POLICY.md) |
| Company profile: preserve-blank on invoice save, field-diff trail on every change | `MasterDataRepository` |

---

## Where the workflow is observable in the app

- **Dashboard → Overview** carries the **order-to-cash pipeline rail**:
  Quote → Production → Delivery → Invoice → Payment, each stage counting
  the live work sitting in that handoff. See
  [`../Views/Dashboard/README.md`](../Views/Dashboard/README.md).
- Each module dashboard shows that module's own stage breakdown.
- All pipeline figures come from `Services/Dashboard/Core/DashboardMetricsService.cs`
  through aggregate DTOs — a dashboard ViewModel never receives an EF
  entity, so it cannot hold a table in memory.

---

## Cross-cutting properties

- 🔐 **Operation-level authorization at the service boundary**
  (`OperationGuard`) — the UI hides buttons, the repository enforces rules.
  Hiding a button is not a permission check.
- 📝 **Audit on every meaningful write** — before/after diff payloads with
  session, actor and module.
- 💾 **SQLite, WAL, busy-timeouts, additive-only migrations with backups.**
  A migration never destroys a column.
- 🌍 **15 locales** as pure XAML dictionaries (`Themes/Languages/*.xaml`) —
  adding a locale needs no C# change.
- 🧪 **Workflow regression net** (`Tests/`) pins company semantics,
  inquiry→PI conversion, price-basis determinism and status-vocabulary
  funnels. See [`../Tests/README.md`](../Tests/README.md).

---

## Reading further

| To understand… | Read |
|---|---|
| how the code is organised | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| the design language | [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md) |
| what may and may not happen on a money path | [`MONEY_PATH_ERROR_POLICY.md`](MONEY_PATH_ERROR_POLICY.md) |
| a specific module's history | the *Business workflow* section of [`README.md`](README.md) |
| how to install and run it | [`INSTALL.md`](INSTALL.md) → [`FIRST_RUN.md`](FIRST_RUN.md) |

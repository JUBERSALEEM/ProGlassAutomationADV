# Senior Analysis — Advanced Reporting

**Date:** 2026-09-02
**Status:** Design proposal — no code written yet, awaiting your decision
**Author's note:** You said you're not sure how to implement this and asked me to bring the industry experience. This document is that answer. Read §3 and §7; the rest is supporting detail.

---

## 1. What you asked for, restated

Three things, in your words:

1. **Proforma Invoice** — one report containing *everything*: the invoice, the specs datagrid, other charges, **and** profit & loss.
2. **Job Order** — "how many released, which specs most used, which colour mostly used", with filters.
3. **Delivery** — "how many gone, how many pending".

And underneath: *"how a normal report generates in industrial fields — which useful details for the company to track everything."*

---

## 2. What already exists (do not rebuild this)

Before designing anything I audited the reporting layer. It is much further along than the request assumes:

| Asset | What it already does |
|---|---|
| `ReportingFilter` | 17 filter fields — date range, company, customer, salesman, project, all 4 document IDs, status, **glass type, specification, colour, thickness, glass ref**, free text, paging |
| `ReportingDataService.*` | 11 partial classes: Pi, Jo, Delivery, TaxInvoice, Statement, Vat, Profitability, GlassSpecs, MasterData, DrillDown |
| `JoReportSummary` | TotalJo, FromPi, AwaitingJo, **ByStatus dict**, **ByPriority dict**, TotalQty, TotalSqm, TotalValue |
| `DeliveryReportSummary` | TotalRows, NotDelivered, Partial, FullyDelivered, ordered/delivered/remaining qty, **CompletionPct** |
| `SpecificationIntelligenceService` | Parses a spec into a **canonical fingerprint** — outer/inner thickness, colour, treatment, coating, spacer, gas, PVB |
| `ReportingExportService` | Generic CSV/Excel/PDF export over any row type |
| `RowTraceabilityService` | Row-level PI → JO → DN → Invoice linkage |

**The important finding:** your Job Order and Delivery asks are ~80% already computed. `DeliveryReportSummary` literally has "how many gone, how many pending" today. What's missing is *aggregation by spec/colour* and a place to see it.

This changes the recommendation completely. This is **not** a new reporting engine. It is two new aggregates plus one new document type.

---

## 3. How industrial reports are actually structured

This is the part you asked me to bring. In manufacturing ERP, a report that people actually use has **four layers, in this order**:

```
┌─ 1. HEADER ─────────────────────────────────────────┐
│ Who, what period, what filters, when generated,     │
│ who generated it. Non-negotiable — a report without │
│ its filter criteria printed on it is unauditable.   │
├─ 2. KPI BAND ───────────────────────────────────────┤
│ 4–6 numbers answering "is this good or bad?"        │
│ Read in 3 seconds by a manager who reads nothing    │
│ else.                                               │
├─ 3. ANALYSIS ───────────────────────────────────────┤
│ Breakdowns that explain the KPIs. Pareto tables:    │
│ top specs, top colours, status mix, ageing buckets. │
│ This is where "which colour was mostly used" lives. │
├─ 4. DETAIL ─────────────────────────────────────────┤
│ The rows. Sorted, filtered, and reconciling exactly │
│ to the KPI band above.                              │
└─────────────────────────────────────────────────────┘
```

**The rule most in-house reports break:** layer 4 must sum to layer 2. If the KPI says 47 job orders and the detail lists 45, trust in the whole document is gone. Every aggregate below is computed from *the same filtered row set* as the detail, never from a separate query.

### The four question types a factory actually asks

1. **Volume** — how many, how much area, what value. *(You have this.)*
2. **Status/flow** — where is work stuck? *(You have this: `ByStatus`, `CompletionPct`.)*
3. **Mix / Pareto** — which specs and colours dominate? *(**Missing** — this is your ask.)*
4. **Money** — margin per job, per spec, per customer. *(Partially: `Profitability` exists, not surfaced per-spec.)*

Mix analysis is the one that changes purchasing behaviour. "6mm Clear FT is 63% of volume" tells you what to hold in stock. That's why your instinct to ask for it is right.

---

## 4. Proposal A — Proforma Invoice "Full Dossier"

**One PDF/Excel per invoice, containing every section.** Industry name: a *job dossier* or *order pack*.

| Sheet / Section | Source (all already exists) |
|---|---|
| 1. Cover + KPI band | Invoice header, grand total, margin % |
| 2. Client & project | Steps 1–3 |
| 3. Specifications | Step 5 spec groups, per-pane glass |
| 4. Cut list / datagrid | Step 5 item rows, W×H×qty, SQM |
| 5. Other charges | Step 6 — custom charges, wastages |
| 6. Terms | Step 7 |
| 7. **Profit & loss** | Step 9 — cost basis, sell, margin |
| 8. Traceability | `RowTraceabilityService` — linked JO / DN / invoice |

**Two variants, and this matters commercially:**

- **Internal dossier** — includes §7 P&L and cost basis.
- **Customer pack** — identical minus §7.

> ⚠️ **A single "export everything" button that includes P&L will eventually be emailed to a customer.** Cost and margin must be a deliberate, separately-labelled action. I would make the customer pack the default and the internal one explicitly named "Internal — includes P&L".

---

## 5. Proposal B — Job Order Analytics

New aggregate `JoAnalyticsSummary`, computed from the same filtered rows as the JO report:

| Section | Content |
|---|---|
| KPI band | Total JO · Released · In production · Completed · Total SQM · Total value |
| **Top specifications** | Rank, spec name, JO count, SQM, % of total *(Pareto)* |
| **Top colours** | Rank, colour, SQM, % of total |
| Thickness mix | 6mm / 8mm / 10mm / 12mm share |
| Status flow | Existing `ByStatus`, rendered as a funnel |
| Priority mix | Existing `ByPriority` |
| Throughput | JOs released per week over the range |

Spec and colour come from `SpecificationIntelligenceService`'s **canonical fingerprint** — not raw strings. That matters: `"6mm Clear FT"` and `"6 mm clear ft glass"` are the same product and must aggregate together, or the Pareto is meaningless.

---

## 6. Proposal C — Delivery Analytics

| Section | Content |
|---|---|
| KPI band | Ordered · Delivered · Pending · **Completion %** *(all exist today)* |
| **Ageing buckets** | Pending 0–7 / 8–14 / 15–30 / 30+ days |
| By customer | Ordered vs delivered vs pending, sorted by pending |
| By spec/colour | What's sitting undelivered, by product |
| On-time analysis | Delivered before vs after promised date |

**Ageing is the highest-value addition here.** "47 pending" is inert. "12 pending over 30 days" is a call list. Ageing buckets are the single most-used report in any dispatch office.

---

## 7. Recommended sequencing

I am deliberately **not** writing ~2,000 lines of report code in one push. That is the exact shape of the change that broke this app before, and there is still no compiler in my environment.

| Slice | Content | Risk | Value |
|---|---|---|---|
| **1** | `JoAnalyticsSummary` + `DeliveryAnalyticsSummary` aggregates in the data service, with unit-testable pure functions. No UI. | Low | Unlocks 2 & 3 |
| **2** | Analytics sections rendered in the existing JO and Delivery report views | Low | **Answers your Job Order + Delivery asks** |
| **3** | PI Full Dossier — Excel (multi-sheet) | Medium | Answers your PI ask |
| **4** | PI Full Dossier — PDF, customer + internal variants | Medium | Print/email ready |
| **5** | Cross-module executive summary (all modules, one page) | Medium | Management view |

**Start with slice 1+2.** They deliver two of your three asks, reuse aggregates that mostly exist, and add no new document plumbing.

---

## 8. Two decisions I need from you

1. **P&L exposure.** Should the PI dossier default to *excluding* P&L (customer-safe), with the internal version as a separate explicitly-labelled button? My strong recommendation is yes.
2. **Pareto depth.** Top 10 specs/colours, or all with a "% of total" column? Top 10 + "other" is the industry norm and keeps a printed page readable.

---

## 9. What I am NOT proposing

- **A new reporting engine.** `ReportingExportService` already does generic CSV/Excel/PDF. Adding a second path would guarantee drift.
- **A new filter model.** `ReportingFilter` has all 17 fields these reports need.
- **Duplicate aggregates.** `ByStatus`, `ByPriority`, `CompletionPct` already exist and will be reused, not recomputed.

The honest summary: **you already own most of this.** The gap is Pareto/mix analysis and a multi-section document type — not the foundations.

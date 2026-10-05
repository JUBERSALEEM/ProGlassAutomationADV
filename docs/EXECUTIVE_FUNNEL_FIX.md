# Executive Report Hub — Order-to-Cash Funnel Fix (2026-09-10)

## Why the tunnel % was wrong

The funnel displayed *Share %* and *step conversion %* between stages whose
counts were measured in **different units**:

| Stage | Old count source | Unit |
|---|---|---|
| Quoted | `pi.TotalPi` | proforma **documents** in period |
| In production | `jo.TotalJo` | job-order documents — but the JO date filter is `DeliveryDate == null || in range`, so **every undelivered JO ever** was counted against this period's quotes |
| Delivered | `dn.FullyDelivered` | **PI spec rows** (`JobOrderId + SpecGroupNumber + SerialNumber`), not documents — a quote has many rows |
| Invoiced | `ti.TotalInvoices` | tax-invoice documents |
| Paid | `ti.Paid` | tax invoices with status Paid |

A percentage computed between two different units is not an approximation —
it is meaningless. Typical corruption: Delivered (rows) > Quoted (documents)
→ Share % above 100%, step conversion > 100% or wildly deflated.

## The fix — a true cohort milestone funnel

Every stage now counts **distinct quotes from the same period cohort** that
reached the milestone:

```
Quoted → became a job order → fully delivered → invoiced → paid
```

- **Cohort** = quotes under `ApplyPiFilters` (the PI summary's own filter),
  materialised once as PI ids; every later stage is restricted to those ids.
- **In production** = live JOs whose `SourceProformaId` is in the cohort.
- **Delivered (full order)** = every spec row of the quote fully delivered.
  Row predicates mirror `BuildDeliveryRowQuery` (non-void parents, no phantom
  rows, real quantities) so the funnel can never disagree with the Delivery
  report about what a completed row is. Downstream stages are intentionally
  *ever-happened* (delivery dates ignored): the question is what became of
  this period's quotes, not when each event happened.
- **Invoiced / Paid** = live TIs with `ProformaId` in the cohort; Paid = at
  least one cohort TI with status `Paid`.
- Stage rows now read **"x of N"** (`CountOfDisplay`) so the cohort context
  is visible without doing mental arithmetic.

Monotonicity is now structural (distinct-subset counts of one cohort), the
bottleneck flag (step < 60%) is meaningful, and each stage reconciles with
its module report.

## Bugs fixed alongside

1. **`TaxInvoiceReportSummary.PendingInvoice` was never assigned** — the
   TaxInvoice report "PENDING INVOICE" KPI and the executive "UNINVOICED"
   health card permanently read 0. Now computed: confirmed quotes
   (`Approved / Confirmed / Issued` — the same statuses the PI report maps
   to Confirmed) with no live tax invoice, under the shared PI filter.
2. **Last-day exclusion in every date-filtered report.** Entity dates are
   stamped `DateTime.Now` (PI `IssueDate`, DN `DeliveryDate`), so
   `field <= DateTo` (midnight) dropped the **entire last day** of every
   filtered range. All 14 DateTo comparisons across PI / TI / Delivery /
   JO / GlassSpec / Profitability / Statement / VAT now compare against
   `DayEnd(filter.DateTo)` (23:59:59.9999999).
3. **Filter drift.** The PI and TI filter chains existed as 2 identical
   inline copies each; they are now `ApplyPiFilters` / `ApplyTiFilters`,
   shared by summaries, rows, the executive funnel and the pending-invoice
   count.
4. **`ExecutiveReportViewModel.LoadAsync` race** — a superseded load's
   `finally` cleared `IsLoading` while the newer load still ran, and a stale
   result could land after a fresh one. Fixed with a monotonic run-id guard.

## Files touched

- `Services/Reporting/ReportingDataService.cs` — `DayEnd` helper
- `Services/Reporting/ReportingDataService.Pi.cs` — `ApplyPiFilters` (2 call sites rewired)
- `Services/Reporting/ReportingDataService.TaxInvoice.cs` — `ApplyTiFilters` (2 call sites), `CountPendingInvoicesAsync`
- `Services/Reporting/ReportingDataService.Executive.cs` — `BuildCohortFunnelAsync` replaces `BuildFunnel`
- `Services/Reporting/ReportingDataService.{Delivery,Jo,GlassSpecs,Profitability,Statement,Vat}.cs` — DayEnd DateTo sites
- `Services/Reporting/ExecutiveSummary.cs` — `FirstStageCount`, `CountOfDisplay`, header note
- `ViewModels/Reports/ExecutiveReport/ExecutiveReportViewModel.cs` — run-id guard
- `Views/Reports/ExecutiveReport/ExecutiveReportView.xaml` — funnel rows bind `CountOfDisplay` + tooltip

## Verification (no .NET SDK in the sandbox — static, disclosed honestly)

Asserted edit battery (anchor counts exact), brace/paren balance on all
changed C# files, 238-file XAML semantic check, zero remaining
`<= filter.DateTo.Value` in the reporting services, binding-target
existence for `CountOfDisplay` / `FirstStageCount`. Build locally before
merge; report any compiler errors back.

# Phase 8 — Finish Pagination

**Severity: Major · Estimate: 4-5 days · Prerequisite: green build**

This is the remainder of the original P1 item. As of 2026-09-09 there are
**13 unfiltered `GetAllAsync()` call sites across 11 ViewModels**, down from
25 / 18, locked by `Tests/Architecture/PaginationRatchetTests.cs`.

## What is already done

- **Category B — single-row lookups:** complete. Five converted to targeted
  repository methods; two reclassified as non-defects after reading them.
- **Category C — pickers:** the proforma picker now uses the slim
  `GetListRowsAsync` projection instead of reading `FullSnapshotJson` per row.
- **Category D — full-text search:** delivery notes and tax invoices push the
  filter into SQL via `SearchAsync`. The sheet-store search is deliberately left
  on the full table because `GlassSheet` is master data.
- **Dashboards:** all seven read aggregates from `IDashboardMetricsService`.

## What remains, and why it is last

Ten sites across eight files are **real list screens**. Each needs four
coordinated changes:

1. a paged repository method returning `PagedResult<T>`
2. page state and commands in the ViewModel
3. a pager in the view's XAML
4. **KPI figures moved off the page**

Item 4 is the trap. `OrderReportListViewModel.UpdateKpis(rows)` computes its
cards from the full set. Paginate without a separate aggregate query and the
KPIs silently describe one page while looking like they describe the whole
table. That is a wrong number on a dashboard, not a crash — invisible in code
review, obvious to a customer.

**Do these one screen at a time with a build between each.**

## The infrastructure already exists

- `PagedResult<T>` — `Services/Reporting/ReportingFilter.cs:99`
- `GlobalQueryEngine.ExecutePagedQueryAsync` / `ExecuteCountAsync` /
  `ExecuteSumAsync` — `Services/Database/GlobalQueryEngine.cs`
- Reference implementation — `Services/Inquiry/InquiryRepository.cs`, including
  `GetStatisticsAsync` for page-independent KPIs
- Pager chrome to copy — `Views/Inquiry/InquiryListView.xaml:1403-1430`

There is **no shared pager control**, so it is one XAML edit per screen in a
different root grid each time. Consider extracting a `PagerControl` first — that
turns six risky XAML edits into six one-line insertions.

## Remaining sites

| File | Sites | Note |
|---|---|---|
| `DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.Part1.cs` | 3 | Main list at :845; two picker loads at :719 and :797 |
| `Reports/OrderReport/OrderReportListViewModel.cs` | 1 | KPI separation needed |
| `Reports/StatementList/StatementListViewModel.cs` | 1 | |
| `SheetStore/SheetStoreHistory/HistoryViewModel.cs` | 1 | Filters applied in memory after load |
| `SheetStore/SheetStoreViewModel.Consumables.cs` | 1 | |
| `SheetStore/SheetStoreViewModel.Part1.cs` | 1 | Main sheet list — master data, so lower urgency |
| `Specifications/SpecificationsCenterViewModel.cs` | 1 | Master data |
| `TaxInvoice/GodView/TaxInvoiceGodViewViewModel.cs` | 1 | |

Deliberately **not** migrated, with reasons recorded at each call site:
`Shared/GlobalSearchViewModel.cs` (sheet-store master data),
`SheetStore/AddEditSheetViewModel.cs` (master data),
`Reports/StatementGenerator/StatementGeneratorViewModel.Helpers.cs`
(six-field dedupe scan that does not translate to SQL).

## Also open from earlier work

`ProformaInvoiceRepository.SearchAsync` and `JobOrderRepository.SearchAsync`
escape LIKE wildcards using SQL Server's `[%]` / `[_]` bracket syntax, which
SQLite does not implement — `[` is an ordinary character there and the `%`
inside the brackets stays a wildcard. A search term containing `_`, which is
common in this app's own identifiers, matches any single character. The newer
searches use `EF.Functions.Like` with an explicit escape character via
`Services/Database/SearchPattern`. Convert the two older ones and delete the
bracket escaping.

## Acceptance criteria

- [ ] Every remaining list screen paginates, with total count from
      `ExecuteCountAsync`.
- [ ] No KPI or total on any screen is computed from the current page.
- [ ] A shared pager control exists, or the reason for not having one is
      recorded.
- [ ] `PaginationRatchetTests` allow-list is empty and the test is retargeted to
      assert zero rather than maintain a list.

## Must not change

- The three deliberate exclusions above.
- `DeliveryNoteRepository.GetAllAsync`'s `!IsMasterDn` filter, now pinned by
  `Tests/Architecture/MasterDnFilterParityTests.cs`.

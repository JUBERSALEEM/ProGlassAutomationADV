# Phase 8 — Finish Pagination

**Severity: Major · Estimate: 4-5 days · Prerequisite: green build**

This is the remainder of the original P1 item. As of 2026-09-09 there are
**13 unfiltered `GetAllAsync()` call sites across 11 ViewModels**, down from
25 / 18, locked by `Tests/Architecture/PaginationRatchetTests.cs`.

## Status — remaining sites classified 2026-09-09; nothing paginated

| | |
|---|---|
| Ratchet verified accurate | **done** — 11 offenders, 11 allow-list entries, no drift |
| Every remaining site classified by what it would break | **done** |
| Actual pagination applied | **NOT done — see below** |

### Most of these must NOT be paginated

This phase's premise — "20 call sites load whole tables; add pagination" — was
already corrected once when the ratchet was narrowed from "any `GetAllAsync`" to
"`GetAllAsync()` with no arguments". Read individually, the remaining 11 split
into two groups, and the larger one is a trap.

**Six are lookups or complete-set consumers. Paginating them is a regression:**

| site | what the result feeds | what pagination would do |
|---|---|---|
| `SheetStore/AddEditSheetViewModel.cs:293` | `_stocked = all.Where(...)`, a progressively-narrowed stock match | report "no matching sheet" for a sheet that exists |
| `Specifications/SpecificationsCenterViewModel.cs:286` | `_all`, then `RebuildFilterOptions()` | silently drop filter choices |
| `SheetStore/SheetStoreViewModel.Part1.cs:359` | `_allSheets`, then `RebuildFilterOptions()` | silently drop filter choices |
| `SheetStore/SheetStoreViewModel.Consumables.cs:186` | `_allItems`, then `ApplyItemFilters()` | silently drop filter choices |
| `Reports/StatementGenerator/StatementGeneratorViewModel.Helpers.cs:103` | `foreach (var item in existing)` with `SameText` duplicate detection | create duplicate statements |
| `Shared/GlobalSearchViewModel.cs:429` | full-text search across every sheet | stop finding sheets beyond page one |

None of these fails loudly. A missing filter option, a phantom "no match", a
duplicate statement — all of them are silent, and three of the four filter cases
look identical in the code to a list screen until you read what the variable is
used for.

`GlobalSearchViewModel` already carries a comment saying exactly this: a sheet
stops appearing in search with no error, and that is not worth the risk for a
table this size.

**Four are genuine list screens** — `OrderReportListViewModel.cs:149`,
`StatementListViewModel.cs:224`, `TaxInvoiceGodViewViewModel.cs:100`,
`SheetStoreHistory/HistoryViewModel.cs:255` — each mapping rows into a grid.
These are the real work.

**One is both.** `DeliveryNoteListViewModel.Part1.cs` has two calls: line 719
loads `allJobOrders` as a picker source, which needs the complete set, and line
797 loads `dns` for the list, which does not.

### Why the four genuine ones are not done

Each needs a pager added to its XAML view — next/previous controls, a row
counter, and the current filter carried across pages. That is a UI change across
four screens with no compiler and no running app to look at it, on code that
works today. Adding a control that cannot be seen, to a screen that cannot be
opened, is exactly the kind of edit that turns a working product into a broken
one with nothing to show which change did it.

The ratchet holds at 11 and fails on growth, so none of these can be quietly
multiplied while the four are waiting.


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

# Inquiry Module Rebuild — Phase 0/1 Discovery

## Repository finding

The current branch does **not** contain an existing Inquiry domain implementation. The only Inquiry code is the recently added UI shell:

- `Views/Inquiry/InquiryListView.xaml(.cs)`
- `ViewModels/Inquiry/InquiryListViewModel.cs`
- navigation/DataTemplate/DI registration

There is no Inquiry entity, DbContext, repository, query service, attachment entity, communication entity, audit adapter, export/import service, PI foreign key, or JO relationship. The current ViewModel explicitly says persistence is not implemented and every action command is a status-message placeholder.

Therefore there is no hidden Inquiry business logic to preserve. Treating the current shell as a complete module would create fake controls and binding debt. The rebuild must add a real domain/application layer while preserving the surrounding ERP services.

## Existing reusable application infrastructure

- Multi-database SQLite architecture rooted at `AppDataRoot.CurrentFolder`, including Demo isolation.
- EF Core contexts/repositories for PI, JO, DN, TI, Statements, Master Data and Sheet Store.
- Additive backup-first schema migration framework: `SchemaMigrationRunner` / `AppSchemaMigrations`.
- `GlobalQueryEngine` and `PagedResult<T>` for projected, no-tracking, database-side paging.
- Master Data clients, projects and salesmen.
- Audit users and `AuditService`; `VersionHistoryService` for append-only snapshots.
- Existing PI attachment service (disk/manifest) and DN/TI attachment implementations. These are document-specific and cannot safely be reused as an Inquiry entity, but their validation/security conventions should be reused.
- Existing PI→JO→DN→TI stable links and document-chain Agent query.
- Existing DI composition root and `RelayCommand`.

## Existing functionality checklist

| Capability | Current reality |
|---|---|
| Inquiry creation/edit/delete/status | Not implemented |
| Received/Sent/Confirmed/Hold/Cancelled | Strings only in filter dropdown; no data source |
| Customer/Salesman/Prepared By relationships | Not implemented |
| Attachments/open | Preview POCO bindings only; no persistence/open command |
| Communication history | String collection only; no model/storage |
| Search/filter/sort/paging/lazy load | Controls/placeholders only |
| Export/import | Placeholder commands only |
| Audit/permissions/notifications | Not implemented for Inquiry |
| PI/JO connection | Display strings only; no relationship |
| Navigation | Implemented |
| New approved list XAML | Implemented, but not data-backed |

## Target dependency map

```text
InquiryListView
  -> InquiryListViewModel
      -> IInquiryQueryService (debounced filter, sort, page, cancellation)
      -> IInquiryWorkflowService (create/edit/status/PI eligibility)
      -> IInquiryAttachmentService (metadata first; open on demand)
      -> IInquiryExportService
      -> IInquiryRepository
          -> InquiryDbContext -> inquiries.db
      -> AuditService / VersionHistoryService
      -> MasterDataRepository (Customer, Project, Salesman)
      -> optional PI stable link
```

## New normalized Inquiry aggregate required

- `InquiryRecord`: stable integer key, unique number/reference, CustomerId/display snapshot, ProjectId/display snapshot, SalesmanId/name, PreparedByUserId/name, status, received/prepared/sent/confirmed timestamps, value, notes, PI link, audit/version timestamps.
- `InquiryAttachment`: metadata and storage path/key; no list-query file bytes.
- `InquiryCommunication`: chronological workflow event with title, description, occurred-at and actor.
- Controlled status mapping for existing display strings: Received, Prepared, Sent, Confirmed, Hold, Cancelled. Database strings remain backward-compatible.

## UI architecture decision

The approved Inquiry design remains the source of truth. It must be split into module-local resources/templates to keep XAML maintainable:

- `Resources/InquiryStyles.xaml`
- `Resources/InquiryTemplates.xaml`
- `InquiryListView.xaml` as structural composition only
- right drawer driven by selection state and a 200ms view-only animation

The register will use a virtualized DataGrid with explicit `ScrollViewer.HorizontalScrollBarVisibility=Auto`, independent vertical scroll, recycling, deferred scrolling and fixed minimum column widths. The table must include the required Prepared By and Last Updated date/time columns; the current approved-shell table does not yet include them.

## Data safety

A new `inquiries.db` is appropriate because no Inquiry schema exists and the app intentionally separates module databases. It will use the existing additive migration runner. No existing database/table is renamed or deleted. PI/JO schema changes are not required for the first list-only release; downstream links are nullable and added only in a separately reviewed phase.

## Implementation sequence

1. Models/status mapping and `InquiryDbContext`.
2. Repository/query service with projected paging/counts and cancellation.
3. Attachment metadata/open service and communication timeline.
4. Replace placeholder ViewModel with real async commands/filtering/paging/selection.
5. Split and finish approved visual resources: required columns, empty/error/loading states, responsive horizontal scrolling, drawer animation.
6. Wire audit, permissions, export/import and stable PI eligibility.
7. Build/tests/runtime/binding diagnostics and screenshot QA.

## Critical correction to prior work

The current module is only a visual shell. It must not be reported as a functioning Inquiry module until the repository, persistence, query, attachment and workflow services above exist and pass runtime validation.

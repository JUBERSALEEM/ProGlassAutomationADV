# Document Register — architecture and source review

**Scope:** one ProGlass view with Proforma Invoice, Job Order, and Delivery Note tabs. This is a static source review; it does not claim that a build, test run, live database read, deployed Supabase inspection, or production-workbook inspection occurred.

## 1. Architecture decision

Implement the register as a read model over the existing ProGlass document repositories, joined by their persisted business identifiers. Keep those repositories and their normal create/edit workflows as the only writers. Do not add a spreadsheet-shaped mirror table, copy full document snapshots into the register, or have the view update snapshot-backed entities itself.

This keeps one authoritative PI/JO/DN record per existing workflow, avoids divergence between indexed list columns and `FullSnapshotJson`, and lets reports continue to read the same operational data they already use.

## 2. ProGlass data graph and efficient list inputs

| Tab | Existing projection | Link used by the register | Important lifecycle rule |
| --- | --- | --- | --- |
| Proforma Invoices | `IProformaInvoiceRepository.GetListRowsAsync()` → `ProformaListRow` (`Id`, `ProformaId`, company/customer/project, status, VAT-inclusive total, issue/created/updated dates, void flag) | `ProformaId` | Do not fetch `FullSnapshotJson` just to paint the list. |
| Job Orders | `IJobOrderRepository.GetListRowsAsync()` → `JobOrderListRow` (`Id`, `JobOrderId`, `SourceProformaId`, customer/project, total, status, priority, delivery/created/updated dates, void flag) | `SourceProformaId` → PI `ProformaId` | Job creation is from an eligible PI; do not fabricate a JO detached from its source PI. |
| Delivery Notes | `IDeliveryNoteRepository.GetAllAsync()` → `DeliveryNoteListItem` (DN/JO/PI identifiers, status, date, quantities, tax-invoice link, void flag) | `JobOrderId` → JO `JobOrderId`; `ProformaId` is also available | `GetAllAsync()` projects scalar list fields and excludes internal Master DNs. Real DNs must continue through the remaining-balance workflow. |

`SavedProformaInvoice` and `SavedJobOrder` store complete snapshots in `FullSnapshotJson`; `SavedDeliveryNote` likewise has detail data beyond its list projection. The register should use the narrow list inputs above and leave snapshot loading to the source editor.

Voided rows are retained by the source repositories for audit/history. The register therefore hides voided PI/JO/DN rows by default and offers a “Show voided” filter. Master DNs remain excluded from the regular DN tab.

## 3. Windowfilehost / ProGlassExcelSync reference findings

The readable reference is Windowfilehost branch `arena/01a10053-windowfilehost`, commit `137835425c74150b764d132395641be740119535`. Its `ProGlass.ExcelSync.Core` / `ProGlass.ExcelSync.Service` projects implement a **PI-only workbook synchronizer**, not a JO/DN register. The selected workbook contract in `docs/EXCEL_FORMAT.md` lists these 12 headings:

`DATE`, `SALESMAN`, `COMPANY`, `PI NUMBER`, `TYPE OF WORK`, `CUSTOMER REF`, `QTY`, `SQM`, `PREPARED BY`, `STATUS`, `PI STATUS`, `ZOHO STATUS`.

`ExcelReaderService.KnownHeaders` and `ProGlassSyncProfile` confirm the same headings. The profile configures them as text, treats `PI NUMBER` as the required identity, trims its outer whitespace, and preserves case/punctuation/internal whitespace. `NormalizedExcelRecord` normalizes only the known missing sentinels for `QTY`/`SQM`; the sync payload remains string-valued. `SupabaseService.ListRecordsAsync()` selects the mapped PI fields plus `source_file`, `source_worksheet`, and `updated_at`.

The checked-in `supabase/schema.sql` is explicitly a software-side template, not proof of the deployed schema. `docs/EXCEL_FORMAT.md` also says the production workbook was not present in that source workspace, so its real worksheet names, formulas, formats, and validation conventions remain unverified. No claim about those live artifacts is made here.

Some workbook concepts overlap PI data in ProGlass: issue date, company, PI number, customer reference, representative, and prepared-by values exist in the PI entity/snapshot. `QTY` and `SQM` are derived from specification rows in ProGlass reporting. The exact workbook meaning of `TYPE OF WORK`, `STATUS` versus `PI STATUS`, and which prepared-by value is equivalent has not been validated. `ZOHO STATUS` has no corresponding ProGlass PI/JO/DN field in the inspected models. The register should not invent or duplicate those fields; full PI data entry stays in the PI workflow until a separate mapping is approved.

## 4. Existing reports and workflow consumers

The operational reports read ProGlass’ existing document databases, not Windowfilehost’s Supabase table:

- `ReportingDataService.Pi.cs` filters live PI rows from `ProformaInvoicesDbContext`, uses indexed fields for summaries, and derives specification quantity/area from PI snapshots / report rows.
- `ReportingDataService.Jo.cs` reads saved JO data; the PI relationship is `SourceProformaId`.
- `ReportingDataService.Delivery.cs` builds row-level delivery quantities from the Delivery Note item records and their JO/specification/serial relationships.
- `OrderReportEngine.BuildForProformaAsync()` reads the PI, calls `GetBySourceProformaIdAsync(proformaId)` for its JOs, and retrieves DNs by each linked `JobOrderId`; internal Master/Dummy DNs are excluded from prepared-document output.
- Operational report helpers apply the source modules’ `IsVoid` exclusion (with the documented statutory VAT exception).

Therefore, new documents and changes saved through the existing ProGlass workflows are the path that makes the data visible to those reports. A separate register store would not.

The existing create/open routes are reusable: `ProformaInvoiceViewModel.TriggerNewInvoice()` / `OpenSavedInvoiceAsync(int)`, `SavedJobOrdersListViewModel.CreateJobOrderCommand` / `JobOrderViewModel.OpenJobOrderAsync(int)`, and `DeliveryNoteListViewModel.CreateDnCommand` / `DeliveryNoteViewModel.OpenDeliveryNoteAsync(int, bool)`.

## 5. Safe spreadsheet-like interaction

A blanket editable grid that writes directly into repository rows is unsafe: it could update an indexed scalar while leaving its snapshot stale, skip optimistic-concurrency/lifecycle checks, or bypass side effects (for example, PI approval’s evidence step, status transitions, JO status/priority snapshot synchronization, and DN dispatch rules).

The register provides spreadsheet-style tabular browsing, sorting, filtering, keyboard selection, and explicit `Open / Edit` and `New` actions. For quick manual updates, only existing controlled fields with a source workflow are exposed in-cell: PI/JO/DN status and JO priority. Their change callbacks reuse the existing list-workflow methods and transition validation; they do not issue direct database writes. Full PI, JO, and DN editing and creation stays in each native editor. External-only `ZOHO STATUS` is not represented or written.

This is a deliberate safe boundary, not a claim that every PI/JO/DN field is inline-editable. Adding additional editable columns requires an approved field map and a writer path that preserves the corresponding snapshot and workflow invariants.

## 6. Smallest useful view

The initial page is a separate **Document Register** module with three tabs, shared in-memory search, refresh, default-hidden voided records, linked PI/JO/DN identifiers, each document’s existing report-useful scalar fields, status controls (plus JO priority), and row-level `Open / Edit` actions. Creation buttons route to the current workflow, including PI selection for JO creation and JO selection/balance logic for DN creation.

It does not port Windowfilehost’s workbook selection, file monitoring, sync worker, Supabase settings, or its complete UI.

## 7. Evidence and verification boundary

Evidence is from static inspection of the repository contracts/models and report/workflow code named above, plus the Windowfilehost reference files `docs/EXCEL_FORMAT.md`, `Models/NormalizedExcelRecord.cs`, `Services/ProGlassSyncProfile.cs`, `Services/SupabaseService.cs`, `Models/SupabaseRecordPayload.cs`, and `supabase/schema.sql`. The production workbook and deployed Supabase schema were not inspected. Build/test execution is separately reported in the implementation summary.

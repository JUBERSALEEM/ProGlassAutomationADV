# Operations exports — design and follow-up analysis

## Export contract

The Reports/Proforma, Job Orders and Deliveries pages are backed by paginated
server APIs. The export endpoint is separate from the visible table: it carries
the page's current filters and sort, deliberately omits the UI page number, and
exports every matching row rather than only the 50/100/250 rows currently on
screen.

| Page | Export grain | CSV | Excel workbook |
|---|---|---|---|
| Proforma / Reports | One source record per row; all statuses currently matched by the Reports filters | Proforma record rows | Proforma record rows + `Export Info` |
| Job Orders | One PI per row, grouped from its active source lines; the server keeps the strict `Confirmed` gate | Confirmed PI queue | Job Order queue + `Export Info` |
| Delivery Notes | One row per movement; PIs with no movements retain a blank movement row | Flattened movement ledger with the PI balance repeated for reconciliation | `PI Summary`, `Delivery Movements`, and `Export Info` |

CSV is UTF-8 with a BOM for spreadsheet compatibility, fully quoted, and guards
cells that could be interpreted as formulas. XLSX is generated server-side as
OOXML, preserves numeric and boolean cells, freezes the heading row, enables
filters, and records the generation time and active filters in `Export Info`.

PDF is rendered server-side by `@react-pdf/renderer` in the Next.js Node runtime.
It includes report title, generation time, applied filters, summary figures,
page numbers, and report-specific tables. Delivery PDFs include both PI
obligations and movement rows. No browser-only PDF runtime or separate service
is needed.

Exports are bounded at 10,000 result rows. Oversized or already-truncated source
reads fail explicitly rather than producing a file that looks complete but is
not. Proforma reads detect concurrent count changes and duplicate/missing IDs
while paging; refresh and retry if the data changed during export. Files are
private/no-store, downloads require the same operator token as the dashboards,
and the Supabase service-role key is never sent to the browser.

## What the three pages should continue to improve

1. **Keep document identity explicit.** PI number is the hand-off key today.
   If imported workbooks reuse a PI number, the export and the UI must retain
   `source_file` so rows from different workbooks are not silently merged.
2. **Keep the status gates different on purpose.** Job Orders accepts only
   `Confirmed`; Deliveries also retains a PI that already has ledger movements
   so a later record status does not erase its dispatch history. Exports call
   those same server data layers instead of reconstructing eligibility in the
   browser.
3. **Move large exports to a queued job if the data grows.** A 10,000-row bound
   is a safe synchronous ceiling for the current Vercel/Node request model. If
   normal exports approach it, generate an asynchronous job, show progress, and
   provide a short-lived download URL rather than raising the ceiling blindly.
4. **Add consistent-snapshot support for very busy syncs.** Current export reads
   verify row counts and IDs, but PostgREST offset paging is not a database
   transaction. A future export RPC should return a snapshot/version or a
   keyset cursor so every page is guaranteed to come from one source version.
5. **Add automated fixture tests.** Cover formula-like customer text, Unicode,
   commas/quotes/newlines, zero-result filters, duplicate PI lines, multiple
   workbooks with the same PI, part-load/void/draft movements, and movement-only
   carry-over PIs. Validate generated XLSX ZIP/XML and PDF page totals in CI.
6. **Add export audit events without logging customer rows.** Capture operator
   ID, report type, format, row count, filter names, duration, and failure code;
   do not write the exported values or bearer token to logs.

## Why the website PDF engine differs from the desktop app

The Windows desktop application already uses QuestPDF Community. The website is
Next.js/Node.js on Vercel, however; it cannot load the WPF application's .NET
implementation in a browser or Node function. The website export uses
`@react-pdf/renderer` so PDF files can be generated within the existing Node
server, without introducing a second service, deployment target, or secret.

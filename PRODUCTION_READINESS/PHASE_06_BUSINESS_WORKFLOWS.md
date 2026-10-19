# Phase 6 — Business workflow correctness

**Status:** One targeted defect fixed in this phase; full end-to-end workflow acceptance remains open.

## Implemented in this phase

- `Services/Inquiry/InquiryImportExportService.cs` now parses CSV by logical records rather than physical lines.
- Quoted fields containing commas, doubled quotes, CR/LF, or embedded newlines no longer split one inquiry across multiple rows.
- An unterminated quoted field fails explicitly with `FormatException` rather than silently shifting columns.
- Import remains validation-first: records missing customer or project are skipped and counted; duplicate inquiry numbers within the imported file are blanked for repository-side regeneration.

## Required business acceptance suite

Run on a disposable database with deterministic fixtures. Capture test output and source-document IDs.

- Inquiry → PI: source inquiry identity, customer/project, attachments, dimensions and descriptions survive conversion; cancelled/converted inquiries cannot create unintended duplicates.
- PI: line arithmetic, SQM/LM, discount, VAT, rounding, save/reopen, revision chain, PDF, CSV and Excel agree.
- Sheet Store: opening stock + receipts − consumption ± adjustments = balance; concurrent movements cannot lose updates.
- Job Order: PI link is explicit; duplicate creation, cancellation and status transitions are guarded at the persistence boundary.
- Delivery Note: delivered quantity cannot exceed ordered quantity, including two concurrent delivery attempts.
- Tax Invoice / Credit Note: tax basis, partial invoice, credit linkage, payment state and document totals reconcile with source documents.
- Statement and Reports: balances reconcile to source ledgers; filters include/exclude exact date boundaries; exported totals match on-screen totals.

## Exit criteria

- [ ] Round-trip CSV tests cover comma, quote, CRLF, embedded newline, blank rows, malformed quote and Unicode.
- [ ] Business golden-value tests pass for all money paths.
- [ ] Concurrency tests prove duplicate/over-delivery/over-invoicing is rejected at the database transaction boundary.
- [ ] Windows Release build and the full test suite pass on the candidate SHA.

This phase is **not complete** until the acceptance suite runs. A parser fix alone is not evidence that the ERP workflow is production-safe.

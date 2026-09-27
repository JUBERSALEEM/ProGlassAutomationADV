-- 0019_fiscal_print_snapshot.sql
-- Phase 17 (fiscal completion slice): the three deferred fiscal items from
-- mapping §1.11 — bank/TRN print fields as columns, the print snapshot
-- (snapshot_json) population contract, and the wider statement filter axes
-- (the statements.filter_* columns have existed since 0003; the axes are
-- exposed by FiscalService in this phase).
--
-- WPF source of truth: Models/TaxInvoice/SavedTaxInvoice/SavedTaxInvoice.cs
-- (BankName, BankAccountNumber, Iban, SwiftCode, CompanyTrn, CompanyName,
-- PurchaseOrderNumber) + Services/TaxInvoice/Mapper/TaxInvoiceMapper.cs
-- (BuildSnapshot → FullSnapshotJson, PascalCase JSON, SchemaVersion 1).
--
-- The migration tool (Phase 16) already preserves the WPF FullSnapshotJson
-- verbatim in snapshot_json; from this phase on it also fills these columns
-- from the WPF entity, and the API populates snapshot_json at create.
--
-- Print-surface data: free text, no vocabularies to CHECK. Existing rows
-- default to '' (empty print header) — identical to the WPF's defaults for
-- a wizard state that never set them.

ALTER TABLE saas.tax_invoices ADD COLUMN company_name text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN company_trn text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN purchase_order_number text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN bank_name text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN bank_account_number text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN iban text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN swift_code text NOT NULL DEFAULT '';

-- The WPF persists its whole StatementFilter POCO to SavedStatement.FilterJson
-- (Models/Statement/StatementFilter.cs); the SaaS statements table already
-- carries the company/customer/project/salesman/date axes as columns (0003)
-- and Phase 17 persists the status multi-select the same way — as a jsonb
-- column, NULL = "all statuses" (the WPF's empty list).
ALTER TABLE saas.statements ADD COLUMN filter_statuses jsonb;

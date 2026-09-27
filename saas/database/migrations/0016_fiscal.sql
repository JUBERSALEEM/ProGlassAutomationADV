-- ═══════════════════════════════════════════════════════════════════════
--  0016 — Phase 12 Fiscal core (WPF-faithful upgrade)
--
--  WPF ground truth (read 2026-09-27):
--    • Models/TaxInvoice/SavedTaxInvoice/SavedTaxInvoice.cs — status
--      vocab Draft/Issued/Paid/PartiallyPaid/Overdue/Cancelled/Void; a
--      Tax Invoice is a FISCAL document: once Issued it is never
--      edited, only voided or credited; payment terms + due date
--      (IssueDate + PaymentTermsDays).
--    • Services/Workflow/StatusTransitions.cs — the TaxInvoice table
--      (Draft→{Draft,Issued,Cancelled,Void}; Issued→{…,Paid,
--      PartiallyPaid,Overdue,Cancelled,Void}; Paid terminal; …) and the
--      CreditNote table (Draft→{Draft,Issued,Cancelled};
--      Issued→{Issued,Applied,Cancelled}; Applied/Cancelled terminal).
--    • Services/TaxInvoice/PaymentTermsResolver.cs — Immediate 0, Net
--      7/15/30/45/60/90, unknown → 30.
--    • Services/TaxInvoice/InvoiceTotalsCalculator.cs —
--      DiscountAmount = round(Subtotal × %/100), VatAmount =
--      round(AfterDiscount × 5%), GrandTotal = AfterDiscount + Vat
--      (MoneyRounding, midpoint away from zero).
--
--  The Phase 3 first-pass tables were guesses (tax_invoices status
--  Issued/Paid/Overdue only + a free-text status_text; credit_notes
--  lowercase draft/issued/void). Both are greenfield — vocabularies are
--  replaced outright and the missing WPF columns added. ALTER only
--  (standing rule from 0012–0015).
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE saas.tax_invoices DROP CONSTRAINT ck_tax_invoices_status;
ALTER TABLE saas.tax_invoices
  ADD CONSTRAINT ck_tax_invoices_status
  CHECK (status IN ('Draft', 'Issued', 'Paid', 'PartiallyPaid', 'Overdue',
                    'Cancelled', 'Void'));
ALTER TABLE saas.tax_invoices DROP COLUMN status_text;

ALTER TABLE saas.tax_invoices ADD COLUMN customer_name text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN project_name text NOT NULL DEFAULT '';
ALTER TABLE saas.tax_invoices ADD COLUMN payment_terms text NOT NULL DEFAULT 'Net 30';
ALTER TABLE saas.tax_invoices ADD COLUMN discount_percent numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.tax_invoices ADD COLUMN is_void boolean NOT NULL DEFAULT false;
ALTER TABLE saas.tax_invoices ADD COLUMN voided_at timestamptz;
ALTER TABLE saas.tax_invoices ADD COLUMN void_reason text NOT NULL DEFAULT '';

ALTER TABLE saas.credit_notes DROP CONSTRAINT ck_credit_notes_status;
ALTER TABLE saas.credit_notes
  ADD CONSTRAINT ck_credit_notes_status
  CHECK (status IN ('Draft', 'Issued', 'Applied', 'Cancelled'));

-- Statement lines are a LEDGER with a defined line order (the WPF
-- statement grid order: invoice, its payment, its credit notes, …).
-- The Phase 3 table has no ordinal, so ordering by id (random uuid) or
-- created_at (identical per batch) is not deterministic — add one.
ALTER TABLE saas.statement_lines ADD COLUMN line_no integer NOT NULL DEFAULT 0;
CREATE INDEX ix_statement_lines_statement_line_no
  ON saas.statement_lines (tenant_id, statement_id, line_no);

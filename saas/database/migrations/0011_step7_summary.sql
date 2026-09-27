-- ═══════════════════════════════════════════════════════════════════════
-- 0011 — STEP 7: the print-preview financial summary.
--
-- WPF source of truth (read 2026-09-27):
--   ViewModels/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/
--     InvoiceMainSummaryViewModel.Recalculate — BUG-56 (a fixed discount
--     can never exceed the net), BUG-57 (batch restore), BUG-58 (UAE VAT
--     law: Article 34 discounts come off the value of supply BEFORE VAT).
--   Services/ProformaInvoice/ProformaInvoiceMapper — persists
--     DiscountType (string), DiscountValue, IsVatApplicable, VatPercent
--     per invoice, alongside the computed amounts.
--
-- 0003 already carries the computed amounts (discount_amount, vat_amount,
-- grand_total_vat_inclusive); this migration adds the four OPERATOR INPUT
-- columns the mapper persists, so the summary is reproducible from the
-- stored document alone (the WPF recalculated live from the wizard VM).
--
-- VAT percent is 0–100 (the WPF setter clamps 0–100); the per-tenant
-- RATE (fraction, 0–1) stays in tenant_vat_config (0003) and only seeds
-- the first save's default.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE saas.proforma_invoices
  ADD COLUMN discount_type text NOT NULL DEFAULT 'Percentage';

ALTER TABLE saas.proforma_invoices
  ADD COLUMN discount_value numeric(18,2) NOT NULL DEFAULT 0;

ALTER TABLE saas.proforma_invoices
  ADD COLUMN is_vat_applicable boolean NOT NULL DEFAULT true;

ALTER TABLE saas.proforma_invoices
  ADD COLUMN vat_percent numeric(5,2) NOT NULL DEFAULT 5.00;

ALTER TABLE saas.proforma_invoices
  ADD CONSTRAINT ck_proforma_discount_type
    CHECK (discount_type IN ('Percentage', 'FixedAmount'));

ALTER TABLE saas.proforma_invoices
  ADD CONSTRAINT ck_proforma_discount_value
    CHECK (discount_value >= 0);

ALTER TABLE saas.proforma_invoices
  ADD CONSTRAINT ck_proforma_vat_percent
    CHECK (vat_percent >= 0 AND vat_percent <= 100);

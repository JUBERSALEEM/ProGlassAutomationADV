-- 0020_tax_invoice_items.sql
-- Phase 19 (reporting core, slice 1): tax invoice LINE ITEMS — the WPF
-- TaxInvoiceItems table (Models/TaxInvoice/SavedTaxInvoice/SavedTaxInvoice.cs
-- TaxInvoiceItem) that order reconciliation
-- (Models/Statement/Reconciliation/OrderReconciliationService.cs) reads as
-- its invoiced-qty axis. Phase 12 shipped totals-only invoices (documented);
-- this closes that gap so reconciliation is fully portable.
--
-- WPF columns → SaaS columns (same shapes as delivery_note_items' 0013
-- parity columns): the spec identity (group number/name, glass type, serial,
-- glass ref, W1/H1/W2/H2), qty, sqm (0.5 m² floor), total_sqm, rate, value,
-- notes — plus the SaaS-native spec_group_item_id link (SET NULL) that
-- delivery_note_items also carry.

CREATE TABLE saas.tax_invoice_items (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  tax_invoice_id uuid NOT NULL,
  spec_group_item_id uuid,
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  spec_group_name text NOT NULL DEFAULT '',
  glass_type text NOT NULL DEFAULT '',
  glass_ref text NOT NULL DEFAULT '',
  w1_mm integer NOT NULL DEFAULT 0,
  h1_mm integer NOT NULL DEFAULT 0,
  w2_mm integer NOT NULL DEFAULT 0,
  h2_mm integer NOT NULL DEFAULT 0,
  qty integer NOT NULL DEFAULT 1,
  sqm numeric(12,3) NOT NULL DEFAULT 0,
  total_sqm numeric(12,3) NOT NULL DEFAULT 0,
  rate numeric(18,2) NOT NULL DEFAULT 0,
  value numeric(18,2) NOT NULL DEFAULT 0,
  notes text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_tax_invoice_items PRIMARY KEY (id),
  CONSTRAINT fk_tax_invoice_items_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_tax_invoice_items_invoice FOREIGN KEY (tax_invoice_id) REFERENCES saas.tax_invoices (id) ON DELETE CASCADE,
  CONSTRAINT fk_tax_invoice_items_item FOREIGN KEY (spec_group_item_id) REFERENCES saas.spec_group_items (id) ON DELETE SET NULL,
  CONSTRAINT ck_tax_invoice_items_qty CHECK (qty >= 0)
);
-- FK-leading indexes (the 0018 hardening convention — the audit checks
-- that every FK's first column leads an index; cascades and joins filter
-- on it, and tenant scoping rides the RLS clause).
CREATE INDEX ix_tax_invoice_items_tenant_invoice ON saas.tax_invoice_items (tenant_id, tax_invoice_id);
CREATE INDEX ix_tax_invoice_items_tax_invoice_id ON saas.tax_invoice_items (tax_invoice_id);
CREATE INDEX ix_tax_invoice_items_spec_group_item_id ON saas.tax_invoice_items (spec_group_item_id);

CREATE TRIGGER trg_tax_invoice_items_touch BEFORE UPDATE ON saas.tax_invoice_items
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

-- RLS (0005 pattern — DEFAULT PRIVILEGES already grants saas_app).
ALTER TABLE saas.tax_invoice_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.tax_invoice_items FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON saas.tax_invoice_items
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

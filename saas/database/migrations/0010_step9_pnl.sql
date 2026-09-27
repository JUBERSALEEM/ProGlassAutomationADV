-- ═══════════════════════════════════════════════════════════════════════
-- 0010_step9_pnl — Phase 7b Step 9 (Profit & Loss) schema.
-- Applied 2026-09-27.
--
-- Ground truth (WPF, read 2026-09-27 — progress facts 85–95):
--   • ViewModels/ProformaInvoice/Step9ProfitLoss/
--     ProfitLossStepViewModel.{Calculation,QuoteBasis,CalculationSupport,
--     CostBasis,Persistence}.cs, PnLMeasurement.cs, Step6SheetAllocation.cs,
--     ProfitLossRow.cs
--   • Services/ProformaInvoice/SheetStorePricingService.cs (lookup contract)
--
-- Design:
--   • Step 9 is a LIVE ANALYSIS rebuilt from the saved document; the
--     persisted artifact is the ProfitLossSnapshot (KPIs + rows). Here it
--     normalizes: pnl_rows (replace-per-recalc, like the Step 5/6 sections)
--     + KPI/operator-input columns on the invoice.
--   • spec_groups gains the PHASE 1-AUDIT Step 4 production provenance:
--     the rates THIS group was quoted with (spacer, interlayer BUY cost,
--     cutting, tempering, U-insert seed). Nullable on purpose — "groups on
--     invoices saved before these fields existed deserialize to null, and
--     Step 9 falls back" — so a legacy group reads unknown, not zero-priced.
--   • Target-pricing engine (SpecTargetSnapshot) is a deferred 7b sub-item;
--     no columns for it yet.
-- Single-action ALTERs (the EF generator parses this form).
-- ═══════════════════════════════════════════════════════════════════════

BEGIN;

-- ── spec_groups: Step 4 production-cost provenance (Phase 1-AUDIT) ────
ALTER TABLE saas.spec_groups ADD COLUMN step4_spacer_price_per_sqm numeric(18,4);
ALTER TABLE saas.spec_groups ADD COLUMN step4_interlayer_cost_per_sqm numeric(18,4);
ALTER TABLE saas.spec_groups ADD COLUMN step4_cutting_cost_per_sqm numeric(18,4);
ALTER TABLE saas.spec_groups ADD COLUMN step4_tempering_cost_per_sqm numeric(18,4);
ALTER TABLE saas.spec_groups ADD COLUMN u_insert_price_per_sqm numeric(18,4);

-- ── proforma_invoices: Step 9 operator inputs + KPI outputs ───────────
-- Operator inputs (CostBasis.cs): wastage default 15, clamped 0–95;
-- cost basis at selling price default true.
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_wastage_percent numeric(5,2) NOT NULL DEFAULT 15;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_cost_at_selling_price boolean NOT NULL DEFAULT true;
ALTER TABLE saas.proforma_invoices ADD CONSTRAINT ck_proforma_pnl_wastage
  CHECK (pnl_wastage_percent >= 0 AND pnl_wastage_percent <= 95);
-- KPI outputs (ProfitLossSnapshot field-for-field).
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_gross_vat_inclusive numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_net_ex_vat numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_total_glass_cost numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_total_production_cost numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_profit numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_margin_percent numeric(9,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_overall_margin_percent numeric(9,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_net_margin_percent numeric(9,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_u_insert_cost numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_basis_cost numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE saas.proforma_invoices ADD COLUMN pnl_calculated_at timestamptz;

-- ── pnl_rows: the ProfitLossRow snapshot, normalized ─────────────────
CREATE TABLE saas.pnl_rows (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  proforma_id uuid NOT NULL,
  kind text NOT NULL DEFAULT '',
  spec_number integer NOT NULL DEFAULT 0,
  spec_name text NOT NULL DEFAULT '',
  description text NOT NULL DEFAULT '',
  sheet_width_mm integer NOT NULL DEFAULT 0,
  sheet_height_mm integer NOT NULL DEFAULT 0,
  sheet_sqm numeric(12,4) NOT NULL DEFAULT 0,
  quantity numeric(18,3) NOT NULL DEFAULT 0,
  price_per_unit numeric(18,4) NOT NULL DEFAULT 0,
  unit_label text NOT NULL DEFAULT 'SQM',
  amount numeric(18,2) NOT NULL DEFAULT 0,
  sheet_source text NOT NULL DEFAULT '',
  sort_order integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_pnl_rows PRIMARY KEY (id),
  CONSTRAINT fk_pnl_rows_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_pnl_rows_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE CASCADE
);
CREATE INDEX ix_pnl_rows_tenant_proforma ON saas.pnl_rows (tenant_id, proforma_id);

-- RLS (0005 pattern — DEFAULT PRIVILEGES already grants saas_app).
ALTER TABLE saas.pnl_rows ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.pnl_rows FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON saas.pnl_rows
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

COMMIT;

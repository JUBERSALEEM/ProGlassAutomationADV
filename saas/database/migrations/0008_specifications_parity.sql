-- ═══════════════════════════════════════════════════════════════════════
-- 0008_specifications_parity.sql — ProGlass SaaS (Phase 7b)
--
-- Restores the WPF specifications model the Phase 3 schema missed,
-- found by porting against the WPF source (same discipline as 0007):
--
--   • Specifications Center catalogue columns (WPF 2026-09-02, additive
--     and NULLable/defaulted there too — pre-existing rows read blank):
--     category, color, thickness_mm, sheet_width_mm/height_mm, sheet_id,
--     panes_json (SpecPane list — a DGU's panes can come from different
--     stock sizes), last_proforma_no (ledger-derived provenance).
--   • The natural key the WPF dedups on: (tenant_id, glass_type,
--     spec_name) — Step 4 APPLY upserts by exactly this pair.
--   • specification_usages: the LEDGER-DERIVED usage trail
--     (SpecificationLedgerService). Ground truth, stated once: the
--     LIBRARY is written by Step 4 (rate + catalogue details, never
--     counts); COUNTS/HISTORY/PROVENANCE are derived from SAVED
--     invoices' Step 5 groups ONLY. One row per (spec, proforma) — the
--     WPF's "2 PIs × 2 specs = 4 verified uses" — which makes the
--     reconcile set-based and idempotent.
--   • usage_count default 0: the WPF starts a new spec at ZERO ("being
--     built in Step 4 is not being used"; Phase 3's default of 1 was an
--     ungrounded guess).
--
-- RLS follows the 0005 pattern for the new table (0005's DEFAULT
-- PRIVILEGES already grant saas_app DML on it).
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE saas.specifications
  ADD COLUMN category text;

ALTER TABLE saas.specifications
  ADD COLUMN color text;

ALTER TABLE saas.specifications
  ADD COLUMN thickness_mm integer NOT NULL DEFAULT 0;

ALTER TABLE saas.specifications
  ADD COLUMN sheet_width_mm integer NOT NULL DEFAULT 0;

ALTER TABLE saas.specifications
  ADD COLUMN sheet_height_mm integer NOT NULL DEFAULT 0;

ALTER TABLE saas.specifications
  ADD COLUMN sheet_id uuid;

ALTER TABLE saas.specifications
  ADD COLUMN panes_json jsonb;

ALTER TABLE saas.specifications
  ADD COLUMN last_proforma_no text;

ALTER TABLE saas.specifications
  ADD CONSTRAINT fk_specifications_sheet
  FOREIGN KEY (sheet_id) REFERENCES saas.glass_sheets (id) ON DELETE SET NULL;

-- The WPF dedups a save by GlassType + SpecName (SpecificationsRepository
-- SaveOrUpdateAsync); the SaaS enforces it per tenant.
ALTER TABLE saas.specifications
  ADD CONSTRAINT uq_specifications_tenant_type_name
  UNIQUE (tenant_id, glass_type, spec_name);

ALTER TABLE saas.specifications
  ALTER COLUMN usage_count SET DEFAULT 0;

-- ── the ledger-derived usage trail ────────────────────────────────────
CREATE TABLE saas.specification_usages (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  spec_id uuid NOT NULL,
  proforma_id uuid NOT NULL,
  spec_name text NOT NULL,
  glass_type text NOT NULL,
  company_name text NOT NULL DEFAULT '',
  customer_name text NOT NULL DEFAULT '',
  project_name text NOT NULL DEFAULT '',
  price_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  sheet_width_mm integer NOT NULL DEFAULT 0,
  sheet_height_mm integer NOT NULL DEFAULT 0,
  used_at timestamptz NOT NULL DEFAULT now(),
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_specification_usages PRIMARY KEY (id),
  CONSTRAINT fk_specification_usages_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_specification_usages_spec FOREIGN KEY (spec_id) REFERENCES saas.specifications (id) ON DELETE CASCADE,
  CONSTRAINT fk_specification_usages_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE CASCADE,
  CONSTRAINT ck_specification_usages_glass_type CHECK (glass_type IN ('SGU', 'DGU', 'LAM', 'DGU+LAM')),
  CONSTRAINT uq_specification_usages_pair UNIQUE (spec_id, proforma_id)
);

CREATE INDEX ix_specification_usages_spec
  ON saas.specification_usages (spec_id, used_at DESC);

CREATE INDEX ix_specification_usages_proforma
  ON saas.specification_usages (proforma_id);

CREATE INDEX ix_specification_usages_tenant
  ON saas.specification_usages (tenant_id, used_at DESC);

-- ── RLS for the new table (0005 pattern) ──────────────────────────────
ALTER TABLE saas.specification_usages ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.specification_usages FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON saas.specification_usages
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

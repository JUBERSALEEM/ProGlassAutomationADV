-- ═══════════════════════════════════════════════════════════════════════
-- 0009_other_charges_parity — Phase 7b Step 6 (Other Charges + Wastages)
-- Applied 2026-09-27.
--
-- Ground truth (WPF, read 2026-09-27):
--   • Models/ProformaInvoice/Step6OtherCharges/{OtherChargeRow,SpecTargetSelection,
--     SpecCheckOption,MetricOption,PolishSubType,PredefinedChargeTemplates,
--     CustomCharges/CustomChargeRow,Wastages/WastageRow,Wastages/SpecGroupColorSide}
--   • ViewModels/ProformaInvoice/Step6OtherCharges/{OtherChargesViewModel
--     (RecalculateTotal/RecalculatePolishRowsImmediate),MetricDiscoveryHubViewModel,
--     Wastages/WastagesSectionViewModel (SyncSourcesForRow)}
--   • Services/ProformaInvoice/Export/ProformaChargeMetricsCalculator
--     (ComputePolishLm / ComputeWastageQty / ComputeSpecMetrics)
--   • Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1
--     (the Step 6 save contract: PredefinedChargeSnapshot / WastageRowSnapshot /
--     CustomChargeSnapshot)
--
-- What Phase 3 (0003) guessed wrong and this migration fixes:
--   • other_charges.metric_key CHECK allowed 'SQM'/'LM'/'Qty'/'Fixed' — the
--     real WPF metric keys are LM1/LM2/LM_COMBINED/SQM1/SQM2/SQM_COMBINED/
--     DGU_CAVITY/QTY/DOUBLE_GLASS (MetricOption.All).
--   • other_charges lacked the template identity, unit, polish state and the
--     computed metric qty the WPF snapshot persists per row.
--   • wastage_rows carried stock_sqm/glass_sqm/spec_group_id — Phase-3
--     guesses with no WPF counterpart (a wastage row targets a SET of
--     spec-group pane sides by "{spec}-{SideLabel}" keys, not one group).
--     Dropped and replaced with the real contract: total_sqm,
--     use_optimization_formula, is_manually_added, checked_source_keys.
--   • custom charges lived only inside the WPF JSON snapshot — normalized
--     here as their own table.
--   • spec_groups lacked the Step 4 provenance the wastage sources read
--     (pane colours + per-side stock-sheet geometry/price, SpecGroupSnapshot
--     Phase G fields) and spec_group_items lost the per-row perimeter factor
--     (default 2), so LM recomputation on read could not round-trip.
-- ═══════════════════════════════════════════════════════════════════════

BEGIN;

-- ── spec_group_items: per-row perimeter factor (WPF GlassItemRow, default 2) ──
ALTER TABLE saas.spec_group_items
  ADD COLUMN perimeter_factor integer NOT NULL DEFAULT 2;
ALTER TABLE saas.spec_group_items
  ADD CONSTRAINT ck_spec_group_items_perimeter_factor
  CHECK (perimeter_factor BETWEEN 1 AND 10);

-- ── spec_groups: Step 4 provenance parity (SpecGroupSnapshot) ───────────
-- Single-action ALTERs (the EF generator parses this form).
ALTER TABLE saas.spec_groups ADD COLUMN outer_glass_color text NOT NULL DEFAULT '';
ALTER TABLE saas.spec_groups ADD COLUMN inner_glass_color text NOT NULL DEFAULT '';
ALTER TABLE saas.spec_groups ADD COLUMN third_glass_color text NOT NULL DEFAULT '';
ALTER TABLE saas.spec_groups ADD COLUMN outer_sheet_width_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN outer_sheet_height_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN inner_sheet_width_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN inner_sheet_height_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN third_sheet_width_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN third_sheet_height_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN outer_sheet_price_per_sqm numeric(18,4) NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN inner_sheet_price_per_sqm numeric(18,4) NOT NULL DEFAULT 0;
ALTER TABLE saas.spec_groups ADD COLUMN third_sheet_price_per_sqm numeric(18,4) NOT NULL DEFAULT 0;

-- ── other_charges: the real Step 6 row contract ─────────────────────────
-- Single-action ALTERs (the EF generator parses this form).
ALTER TABLE saas.other_charges ADD COLUMN template_key text NOT NULL DEFAULT '';
ALTER TABLE saas.other_charges ADD COLUMN description text NOT NULL DEFAULT '';
ALTER TABLE saas.other_charges ADD COLUMN unit text NOT NULL DEFAULT 'Qty';
ALTER TABLE saas.other_charges ADD COLUMN metric_qty numeric(18,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.other_charges ADD COLUMN polish_sub_type text NOT NULL DEFAULT '';
ALTER TABLE saas.other_charges ADD COLUMN custom_polish_description text NOT NULL DEFAULT '';
ALTER TABLE saas.other_charges ADD COLUMN custom_polish_qty numeric(18,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.other_charges ALTER COLUMN metric_key SET DEFAULT '';

ALTER TABLE saas.other_charges
  DROP CONSTRAINT ck_other_charges_metric;
ALTER TABLE saas.other_charges
  ADD CONSTRAINT ck_other_charges_metric
  CHECK (metric_key IN ('LM1', 'LM2', 'LM_COMBINED', 'SQM1', 'SQM2',
                        'SQM_COMBINED', 'DGU_CAVITY', 'QTY', 'DOUBLE_GLASS'));
ALTER TABLE saas.other_charges
  ADD CONSTRAINT ck_other_charges_unit CHECK (unit IN ('Sqm', 'Lm', 'Qty'));
ALTER TABLE saas.other_charges
  ADD CONSTRAINT ck_other_charges_polish_sub_type
  CHECK (polish_sub_type IN ('', 'AllSidePolish', 'BothHeightSide',
                             'BothWidthSide', 'AllSidePolishAsPerDwg',
                             'BothHeightSideAsPerDwg', 'BothWidthSideAsPerDwg',
                             'BothHeightOneWidth', 'BothHeightOneWidthAsPerDwg',
                             'OneHeightOneWidth', 'OneHeightOneWidthAsPerDwg',
                             'OneHeightSide', 'OneWidthSide', 'Custom'));

-- ── wastage_rows: the real wastage contract ─────────────────────────────
ALTER TABLE saas.wastage_rows DROP CONSTRAINT fk_wastage_rows_spec_group;
DROP INDEX IF EXISTS saas.ix_wastage_rows_spec_group;
ALTER TABLE saas.wastage_rows DROP COLUMN spec_group_id;
ALTER TABLE saas.wastage_rows DROP COLUMN stock_sqm;
ALTER TABLE saas.wastage_rows DROP COLUMN glass_sqm;
ALTER TABLE saas.wastage_rows ADD COLUMN total_sqm numeric(12,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.wastage_rows ADD COLUMN is_manually_added boolean NOT NULL DEFAULT false;
ALTER TABLE saas.wastage_rows ADD COLUMN use_optimization_formula boolean NOT NULL DEFAULT false;
ALTER TABLE saas.wastage_rows ADD COLUMN checked_source_keys jsonb NOT NULL DEFAULT '[]';
ALTER TABLE saas.wastage_rows ADD COLUMN sort_order integer NOT NULL DEFAULT 0;
-- The optimization mode DERIVES the rate at 6 dp (price ÷ % gap) and the
-- formula price carries sheet prices at 4 dp — Phase 3's 2 dp truncated
-- both on save.
ALTER TABLE saas.wastage_rows ALTER COLUMN rate TYPE numeric(18,6);
ALTER TABLE saas.wastage_rows ALTER COLUMN formula_price_per_sqm TYPE numeric(18,4);

-- ── custom_charges: the Custom Charges section (CustomChargeRow) ────────
CREATE TABLE saas.custom_charges (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  proforma_id uuid NOT NULL,
  charge_name text NOT NULL DEFAULT '',
  qty numeric(18,3) NOT NULL DEFAULT 1,
  unit text NOT NULL DEFAULT 'Qty',
  rate numeric(18,2) NOT NULL DEFAULT 0,
  amount numeric(18,2) NOT NULL DEFAULT 0,
  is_active boolean NOT NULL DEFAULT true,
  sort_order integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_custom_charges PRIMARY KEY (id),
  CONSTRAINT fk_custom_charges_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_custom_charges_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE CASCADE,
  CONSTRAINT ck_custom_charges_unit CHECK (unit IN ('Sqm', 'Lm', 'Qty')),
  CONSTRAINT ck_custom_charges_qty CHECK (qty >= 0)
);
CREATE INDEX ix_custom_charges_tenant_proforma ON saas.custom_charges (tenant_id, proforma_id);

-- RLS for the new table (0005 pattern — DEFAULT PRIVILEGES already
-- granted SELECT/INSERT/UPDATE/DELETE to saas_app for future tables).
ALTER TABLE saas.custom_charges ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.custom_charges FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON saas.custom_charges
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

COMMIT;

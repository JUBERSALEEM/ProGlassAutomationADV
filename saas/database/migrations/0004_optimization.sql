-- ═══════════════════════════════════════════════════════════════════════
-- 0004_optimization.sql — ProGlass SaaS (Phase 3)
-- Optimization sessions and their queryable projections (sheets,
-- placements, cuts, leftovers). Column semantics come verbatim from the
-- WPF engine models (Services/Optimization/OptimizationModels.cs):
--   SheetLayout.UsableX/Y/W/H, CutStep (Seq, Axis, AtMm, Stage, IsTrim),
--   PlacedPiece (integer mm coords + Rotated), RemnantRegion leftovers.
-- A persisted session that fails either oracle (conservation_verified /
-- geometry_verified = false) is quarantined: never exported, never fed
-- to production (WPF rule).
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE saas.optimization_sessions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  document_type text NOT NULL DEFAULT '',
  document_id uuid,
  composition_key text NOT NULL DEFAULT '',
  params_json jsonb,
  status text NOT NULL DEFAULT 'queued',
  strategy text NOT NULL DEFAULT '',
  sheets_used integer NOT NULL DEFAULT 0,
  unplaced_count integer NOT NULL DEFAULT 0,
  total_used_sqm numeric(14,4) NOT NULL DEFAULT 0,
  total_stock_sqm numeric(14,4) NOT NULL DEFAULT 0,
  utilization_pct numeric(6,2) NOT NULL DEFAULT 0,
  conservation_verified boolean NOT NULL DEFAULT false,
  geometry_verified boolean NOT NULL DEFAULT false,
  validation_problems jsonb NOT NULL DEFAULT '[]',
  scale_notes jsonb NOT NULL DEFAULT '[]',
  sheets_lower_bound integer NOT NULL DEFAULT 0,
  created_by uuid,
  started_at timestamptz,
  completed_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  CONSTRAINT pk_optimization_sessions PRIMARY KEY (id),
  CONSTRAINT fk_optimization_sessions_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT ck_optimization_sessions_status CHECK (status IN ('queued', 'running', 'completed', 'failed'))
);
CREATE INDEX ix_optimization_sessions_tenant_document ON saas.optimization_sessions (tenant_id, document_type, document_id);
CREATE INDEX ix_optimization_sessions_tenant_status ON saas.optimization_sessions (tenant_id, status);

CREATE TABLE saas.optimization_sheets (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  session_id uuid NOT NULL,
  sheet_no integer NOT NULL,
  stock_w_mm integer NOT NULL,
  stock_h_mm integer NOT NULL,
  thickness_mm integer NOT NULL DEFAULT 0,
  color text NOT NULL DEFAULT '',
  material_key text NOT NULL DEFAULT '',
  usable_x integer NOT NULL,
  usable_y integer NOT NULL,
  usable_w integer NOT NULL,
  usable_h integer NOT NULL,
  used_sqm numeric(12,4) NOT NULL DEFAULT 0,
  stock_sqm numeric(12,4) NOT NULL DEFAULT 0,
  utilization_pct numeric(6,2) NOT NULL DEFAULT 0,
  trim_cut_count integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_optimization_sheets PRIMARY KEY (id),
  CONSTRAINT fk_optimization_sheets_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_optimization_sheets_session FOREIGN KEY (session_id) REFERENCES saas.optimization_sessions (id) ON DELETE CASCADE
);
CREATE INDEX ix_optimization_sheets_tenant_session ON saas.optimization_sheets (tenant_id, session_id);

CREATE TABLE saas.optimization_placements (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  sheet_id uuid NOT NULL,
  piece_ref text NOT NULL DEFAULT '',
  job_ref text NOT NULL DEFAULT '',
  glass_ref text NOT NULL DEFAULT '',
  spec_label text NOT NULL DEFAULT '',
  position text NOT NULL DEFAULT '',
  color text NOT NULL DEFAULT '',
  thickness_mm integer NOT NULL DEFAULT 0,
  x_mm integer NOT NULL,
  y_mm integer NOT NULL,
  w_mm integer NOT NULL,
  h_mm integer NOT NULL,
  rotated boolean NOT NULL DEFAULT false,
  qty integer NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_optimization_placements PRIMARY KEY (id),
  CONSTRAINT fk_optimization_placements_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_optimization_placements_sheet FOREIGN KEY (sheet_id) REFERENCES saas.optimization_sheets (id) ON DELETE CASCADE,
  CONSTRAINT ck_optimization_placements_dims CHECK (x_mm >= 0 AND y_mm >= 0 AND w_mm > 0 AND h_mm > 0)
);
CREATE INDEX ix_optimization_placements_tenant_sheet ON saas.optimization_placements (tenant_id, sheet_id);

CREATE TABLE saas.optimization_cuts (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  sheet_id uuid NOT NULL,
  seq integer NOT NULL,
  axis text NOT NULL,
  at_mm integer NOT NULL,
  from_x integer NOT NULL,
  from_y integer NOT NULL,
  from_w integer NOT NULL,
  from_h integer NOT NULL,
  stage integer NOT NULL DEFAULT 1,
  is_trim boolean NOT NULL DEFAULT false,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_optimization_cuts PRIMARY KEY (id),
  CONSTRAINT fk_optimization_cuts_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_optimization_cuts_sheet FOREIGN KEY (sheet_id) REFERENCES saas.optimization_sheets (id) ON DELETE CASCADE,
  CONSTRAINT ck_optimization_cuts_axis CHECK (axis IN ('H', 'V'))
);
CREATE INDEX ix_optimization_cuts_tenant_sheet ON saas.optimization_cuts (tenant_id, sheet_id, seq);

CREATE TABLE saas.optimization_leftovers (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  sheet_id uuid NOT NULL,
  x_mm integer NOT NULL,
  y_mm integer NOT NULL,
  w_mm integer NOT NULL,
  h_mm integer NOT NULL,
  is_reusable boolean NOT NULL DEFAULT false,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_optimization_leftovers PRIMARY KEY (id),
  CONSTRAINT fk_optimization_leftovers_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_optimization_leftovers_sheet FOREIGN KEY (sheet_id) REFERENCES saas.optimization_sheets (id) ON DELETE CASCADE,
  CONSTRAINT ck_optimization_leftovers_dims CHECK (w_mm >= 0 AND h_mm >= 0)
);
CREATE INDEX ix_optimization_leftovers_tenant_sheet ON saas.optimization_leftovers (tenant_id, sheet_id);

-- updated_at triggers for every table in this file.
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['optimization_sessions', 'optimization_sheets',
                           'optimization_placements', 'optimization_cuts',
                           'optimization_leftovers'] LOOP
    EXECUTE format('CREATE TRIGGER trg_%I_touch BEFORE UPDATE ON saas.%I '
      'FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at()', t, t);
  END LOOP;
END $$;

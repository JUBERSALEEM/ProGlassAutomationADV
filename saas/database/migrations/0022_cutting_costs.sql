-- ═══════════════════════════════════════════════════════════════════════
--  0022 — Phase 26b Cutting workflow, allocation ledger, actual costs,
--         replacements/FOC
--
--  WPF ground truth (read 2026-09-27):
--    • Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
--      — SavedMaterialAllocation (AllocationId = ReservationId|S{sheetNo}|
--      {placedPieceLabel}; status Available/Reserved/Allocated/Consumed/
--      Released/Cancelled; Consumption cascades to the reservation when
--      every allocation of that reservation is consumed),
--      SavedActualCost (CostId = JobRef|Spec|Serial|{int type}(|alloc)(|FOC);
--      Estimated/ActualMaterial/Production/Wastage/Processing/TotalActual;
--      FOC keeps cost, zeroes selling), SavedProductionReplacement
--      (ReplacementId = JobRef|Spec|Serial|Position|{stage}|RPL:{n};
--      replacement work item UniqueKey = original|RPL:{n}).
--    • Models/Production/SavedProduction/SavedProductionPhase15Entities.cs
--      — SavedCuttingSession (CUT-JobRef-…-yyyyMMddHHmmss; Draft/Prepared/
--      Optimized/Verified/Allocated/Cutting/Completed/Cancelled/Failed),
--      SavedCuttingSheet (session|S{n}, usable rect + used/stock sqm),
--      SavedCuttingPlacedPiece (sheet|{pieceId}; Pending/Reserved/
--      Allocated/Cutting/Completed/QcPending/QcPassed/QcFailed/Rework/
--      Breakage/Foc/Cancelled).
--    • Services/Production/MaterialAllocationService.cs — AllocateAsync
--      (idempotent, live reservation required: Cancelled/Released/Consumed
--      refused; Reserved→Allocated), ConsumeAsync (idempotent; the
--      reservation flips to Consumed only when ALL its allocations are
--      consumed, never overwriting Released/Cancelled).
--    • Services/Production/ActualCostService.cs — RecordCostAsync
--      (idempotent update, never overwrites the PI estimate),
--      GetSummaryByJobAsync (TotalActual falls back to the sum of the
--      components; FocImpact = cost incurred with selling zero),
--      DeriveFromAllocationAsync (per allocation: piece sqm × the sheet's
--      purchase price; unknown price → 0 with a warning, allocation
--      preserved).
--    • Services/Production/ProductionBreakageService.cs — RecordBreakage/
--      Rework/Foc: the original row is NEVER silently modified; breakage
--      event + replacement piece + replacement row.
--    • Services/Production/Cutting/* — requirement build (pane-instance
--      identity, material identity from the spec grammar, thickness
--      single-distinct-or-0), grouping FAMILY|COLOR|THICK|COATING|
--      TREATMENT, stock selection (Balance − other active holds, waste
--      score, 80% fill estimate), workflow (reserve → pack → verify →
--      persist → allocate, compensation releases every reservation of the
--      run on failure), completion (piece → work item QtyDone+1 clamped →
--      allocation Consumed → cascade; breakage during cutting → Phase 14
--      replacement).
--
--  SaaS adaptations (documented, mapping §1.7d): GUID PKs + tenant RLS on
--  every table (the WPF's int Ids are SQLite-local); the WPF's int enums
--  become TEXT vocabularies (allocation status reuses the 0015
--  material_reservations CHECK words; the WPF int codes stay in the
--  deterministic string ids exactly as built by the WPF); the WPF's
--  cross-DB GlassSheet int id becomes glass_sheets uuid (SET NULL — the
--  unlimited fallback sheet has no rack); reservation links ride the
--  0015 reservation_key; the WPF's WorkItemId int becomes
--  production_pieces uuid. Deterministic string identities (allocation_id,
--  cost_id, replacement_id, cutting_session_id, cutting_sheet_id,
--  placed_piece_id) are UNIQUE per tenant — idempotency rides on them
--  exactly as in the WPF.
-- ═══════════════════════════════════════════════════════════════════════

-- ── material_allocations → the WPF MaterialAllocations ledger ────────

CREATE TABLE saas.material_allocations (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  allocation_id text NOT NULL,
  reservation_id uuid,
  reservation_key text NOT NULL DEFAULT '',
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  optimization_session_id uuid,
  sheet_no integer NOT NULL DEFAULT 0,
  placed_piece_label text NOT NULL DEFAULT '',
  glass_ref text NOT NULL DEFAULT '',
  width_mm integer NOT NULL DEFAULT 0,
  height_mm integer NOT NULL DEFAULT 0,
  quantity integer NOT NULL DEFAULT 1,
  utilization_pct numeric(18, 2) NOT NULL DEFAULT 0,
  wastage_pct numeric(18, 2) NOT NULL DEFAULT 0,
  status text NOT NULL DEFAULT 'Allocated',
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  consumed_at timestamptz,
  notes text NOT NULL DEFAULT '',
  CONSTRAINT pk_material_allocations PRIMARY KEY (id),
  CONSTRAINT fk_material_allocations_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_material_allocations_reservation FOREIGN KEY (reservation_id) REFERENCES saas.material_reservations (id) ON DELETE SET NULL,
  CONSTRAINT fk_material_allocations_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_material_allocations_opt_session FOREIGN KEY (optimization_session_id) REFERENCES saas.optimization_sessions (id) ON DELETE SET NULL,
  CONSTRAINT ck_material_allocations_status CHECK (status IN ('Available', 'Reserved', 'Allocated', 'Consumed', 'Released', 'Cancelled')),
  CONSTRAINT ck_material_allocations_qty CHECK (quantity > 0),
  CONSTRAINT ck_material_allocations_dims CHECK (width_mm >= 0 AND height_mm >= 0)
);
-- Deterministic identity — repeated allocations return the existing row
-- (WPF IX on AllocationId).
CREATE UNIQUE INDEX ix_material_allocations_tenant_allocation_id
  ON saas.material_allocations (tenant_id, allocation_id);
CREATE INDEX ix_material_allocations_tenant_job
  ON saas.material_allocations (tenant_id, job_order_id, sheet_no, placed_piece_label);
CREATE INDEX ix_material_allocations_reservation_key
  ON saas.material_allocations (tenant_id, reservation_key);

-- ── production_actual_costs → the WPF ProductionActualCosts ─────────

CREATE TABLE saas.production_actual_costs (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  cost_id text NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  source_proforma_id text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  glass_ref text NOT NULL DEFAULT '',
  spec_name text NOT NULL DEFAULT '',
  cost_type text NOT NULL DEFAULT 'ActualMaterial',
  stock_sheet_id uuid,
  material_allocation_id text NOT NULL DEFAULT '',
  quantity numeric(18, 4) NOT NULL DEFAULT 0,
  unit_cost numeric(18, 4) NOT NULL DEFAULT 0,
  total_cost numeric(18, 4) NOT NULL DEFAULT 0,
  selling_value numeric(18, 4) NOT NULL DEFAULT 0,
  is_foc boolean NOT NULL DEFAULT false,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  notes text NOT NULL DEFAULT '',
  CONSTRAINT pk_production_actual_costs PRIMARY KEY (id),
  CONSTRAINT fk_production_actual_costs_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_actual_costs_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_actual_costs_stock_sheet FOREIGN KEY (stock_sheet_id) REFERENCES saas.glass_sheets (id) ON DELETE SET NULL,
  CONSTRAINT ck_production_actual_costs_type CHECK (cost_type IN ('Estimated', 'ActualMaterial', 'Production', 'Wastage', 'Processing', 'TotalActual')),
  CONSTRAINT ck_production_actual_costs_amounts CHECK (quantity >= 0 AND unit_cost >= 0 AND total_cost >= 0 AND selling_value >= 0)
);
CREATE UNIQUE INDEX ix_production_actual_costs_tenant_cost_id
  ON saas.production_actual_costs (tenant_id, cost_id);
CREATE INDEX ix_production_actual_costs_tenant_job
  ON saas.production_actual_costs (tenant_id, job_order_id, spec_group_number, serial_number, cost_type);

-- ── production_replacements → the WPF ProductionReplacements ────────

CREATE TABLE saas.production_replacements (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  replacement_id text NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  source_proforma_id text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  glass_ref text NOT NULL DEFAULT '',
  spec_name text NOT NULL DEFAULT '',
  position text NOT NULL DEFAULT '',
  stage text NOT NULL DEFAULT '',
  original_piece_id uuid,
  original_event_id uuid,
  replacement_piece_id uuid,
  entry_type text NOT NULL DEFAULT 'Rework',
  reason text NOT NULL DEFAULT '',
  quantity integer NOT NULL DEFAULT 1,
  is_foc boolean NOT NULL DEFAULT false,
  replacement_sequence integer NOT NULL DEFAULT 1,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  status text NOT NULL DEFAULT 'Pending',
  CONSTRAINT pk_production_replacements PRIMARY KEY (id),
  CONSTRAINT fk_production_replacements_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_replacements_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_replacements_original_piece FOREIGN KEY (original_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT fk_production_replacements_original_event FOREIGN KEY (original_event_id) REFERENCES saas.production_events (id) ON DELETE SET NULL,
  CONSTRAINT fk_production_replacements_piece FOREIGN KEY (replacement_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT ck_production_replacements_entry_type CHECK (entry_type IN ('Rework', 'Foc')),
  CONSTRAINT ck_production_replacements_status CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Cancelled')),
  CONSTRAINT ck_production_replacements_qty CHECK (quantity > 0),
  CONSTRAINT ck_production_replacements_seq CHECK (replacement_sequence > 0)
);
CREATE UNIQUE INDEX ix_production_replacements_tenant_replacement_id
  ON saas.production_replacements (tenant_id, replacement_id);
CREATE INDEX ix_production_replacements_tenant_job
  ON saas.production_replacements (tenant_id, job_order_id);
CREATE INDEX ix_production_replacements_original_piece
  ON saas.production_replacements (original_piece_id);

-- ── cutting_sessions → the WPF CuttingSessions ──────────────────────

CREATE TABLE saas.cutting_sessions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  cutting_session_id text NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  source_proforma_id text NOT NULL DEFAULT '',
  optimization_session_id uuid,
  material_group_id text NOT NULL DEFAULT '',
  total_pieces integer NOT NULL DEFAULT 0,
  sheets_used integer NOT NULL DEFAULT 0,
  utilization_pct numeric(18, 2) NOT NULL DEFAULT 0,
  wastage_pct numeric(18, 2) NOT NULL DEFAULT 0,
  status text NOT NULL DEFAULT 'Draft',
  rotation_policy text NOT NULL DEFAULT 'Free90',
  split_mode text NOT NULL DEFAULT 'HorizontalFirst',
  effort_level integer NOT NULL DEFAULT 3,
  margin_profile_json text NOT NULL DEFAULT '',
  is_verified boolean NOT NULL DEFAULT false,
  verification_result text NOT NULL DEFAULT '',
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  completed_at timestamptz,
  notes text NOT NULL DEFAULT '',
  CONSTRAINT pk_cutting_sessions PRIMARY KEY (id),
  CONSTRAINT fk_cutting_sessions_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_sessions_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_sessions_opt_session FOREIGN KEY (optimization_session_id) REFERENCES saas.optimization_sessions (id) ON DELETE SET NULL,
  CONSTRAINT ck_cutting_sessions_status CHECK (status IN ('Draft', 'Prepared', 'Optimized', 'Verified', 'Allocated', 'Cutting', 'Completed', 'Cancelled', 'Failed')),
  CONSTRAINT ck_cutting_sessions_rotation CHECK (rotation_policy IN ('Free90', 'Locked')),
  CONSTRAINT ck_cutting_sessions_split CHECK (split_mode IN ('HorizontalFirst', 'VerticalFirst', 'LongerLeftover')),
  CONSTRAINT ck_cutting_sessions_effort CHECK (effort_level BETWEEN 1 AND 5)
);
CREATE UNIQUE INDEX ix_cutting_sessions_tenant_session_id
  ON saas.cutting_sessions (tenant_id, cutting_session_id);
CREATE INDEX ix_cutting_sessions_tenant_job
  ON saas.cutting_sessions (tenant_id, job_order_id, created_at DESC);

-- ── cutting_sheets → the WPF CuttingSheets ──────────────────────────

CREATE TABLE saas.cutting_sheets (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  cutting_sheet_id text NOT NULL,
  cutting_session_id uuid NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  sheet_no integer NOT NULL DEFAULT 1,
  stock_sheet_id uuid,
  stock_size_label text NOT NULL DEFAULT '',
  stock_width_mm integer NOT NULL DEFAULT 0,
  stock_height_mm integer NOT NULL DEFAULT 0,
  thickness_mm integer NOT NULL DEFAULT 0,
  color text NOT NULL DEFAULT '',
  usable_x integer NOT NULL DEFAULT 0,
  usable_y integer NOT NULL DEFAULT 0,
  usable_width_mm integer NOT NULL DEFAULT 0,
  usable_height_mm integer NOT NULL DEFAULT 0,
  used_sqm numeric(18, 4) NOT NULL DEFAULT 0,
  stock_sqm numeric(18, 4) NOT NULL DEFAULT 0,
  utilization_pct numeric(18, 2) NOT NULL DEFAULT 0,
  wastage_pct numeric(18, 2) NOT NULL DEFAULT 0,
  is_verified boolean NOT NULL DEFAULT false,
  verification_result text NOT NULL DEFAULT '',
  placed_piece_count integer NOT NULL DEFAULT 0,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_cutting_sheets PRIMARY KEY (id),
  CONSTRAINT fk_cutting_sheets_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_sheets_session FOREIGN KEY (cutting_session_id) REFERENCES saas.cutting_sessions (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_sheets_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_sheets_stock_sheet FOREIGN KEY (stock_sheet_id) REFERENCES saas.glass_sheets (id) ON DELETE SET NULL,
  CONSTRAINT ck_cutting_sheets_sheet_no CHECK (sheet_no > 0)
);
CREATE UNIQUE INDEX ix_cutting_sheets_tenant_sheet_id
  ON saas.cutting_sheets (tenant_id, cutting_sheet_id);
CREATE INDEX ix_cutting_sheets_session
  ON saas.cutting_sheets (cutting_session_id, sheet_no);

-- ── cutting_placed_pieces → the WPF CuttingPlacedPieces ─────────────

CREATE TABLE saas.cutting_placed_pieces (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  placed_piece_id text NOT NULL,
  cutting_sheet_id uuid NOT NULL,
  cutting_session_id uuid NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  glass_ref text NOT NULL DEFAULT '',
  spec_name text NOT NULL DEFAULT '',
  position text NOT NULL DEFAULT '',
  piece_id text NOT NULL DEFAULT '',
  piece_label text NOT NULL DEFAULT '',
  width_mm integer NOT NULL DEFAULT 0,
  height_mm integer NOT NULL DEFAULT 0,
  x integer NOT NULL DEFAULT 0,
  y integer NOT NULL DEFAULT 0,
  is_rotated boolean NOT NULL DEFAULT false,
  cut_sequence integer NOT NULL DEFAULT 0,
  status text NOT NULL DEFAULT 'Pending',
  source_piece_id uuid,
  reservation_key text NOT NULL DEFAULT '',
  allocation_id text NOT NULL DEFAULT '',
  is_foc boolean NOT NULL DEFAULT false,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  notes text NOT NULL DEFAULT '',
  CONSTRAINT pk_cutting_placed_pieces PRIMARY KEY (id),
  CONSTRAINT fk_cutting_placed_pieces_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_placed_pieces_sheet FOREIGN KEY (cutting_sheet_id) REFERENCES saas.cutting_sheets (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_placed_pieces_session FOREIGN KEY (cutting_session_id) REFERENCES saas.cutting_sessions (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_placed_pieces_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_cutting_placed_pieces_source_piece FOREIGN KEY (source_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT ck_cutting_placed_pieces_status CHECK (status IN ('Pending', 'Reserved', 'Allocated', 'Cutting', 'Completed', 'QcPending', 'QcPassed', 'QcFailed', 'Rework', 'Breakage', 'Foc', 'Cancelled')),
  CONSTRAINT ck_cutting_placed_pieces_cut_sequence CHECK (cut_sequence >= 0)
);
CREATE UNIQUE INDEX ix_cutting_placed_pieces_tenant_placed_piece_id
  ON saas.cutting_placed_pieces (tenant_id, placed_piece_id);
CREATE INDEX ix_cutting_placed_pieces_session
  ON saas.cutting_placed_pieces (cutting_session_id, cut_sequence);
CREATE INDEX ix_cutting_placed_pieces_sheet
  ON saas.cutting_placed_pieces (cutting_sheet_id);
CREATE INDEX ix_cutting_placed_pieces_source_piece
  ON saas.cutting_placed_pieces (source_piece_id);
CREATE INDEX ix_cutting_placed_pieces_tenant_job_status
  ON saas.cutting_placed_pieces (tenant_id, job_order_id, status);

-- ── tenant isolation (every new table, 0005 pattern) ────────────────

ALTER TABLE saas.material_allocations ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.material_allocations FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.material_allocations
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.production_actual_costs ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.production_actual_costs FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.production_actual_costs
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.production_replacements ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.production_replacements FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.production_replacements
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.cutting_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.cutting_sessions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.cutting_sessions
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.cutting_sheets ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.cutting_sheets FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.cutting_sheets
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.cutting_placed_pieces ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.cutting_placed_pieces FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.cutting_placed_pieces
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

-- ═══════════════════════════════════════════════════════════════════════
--  0021 — Phase 26a Production extras: process execution + control
--
--  WPF ground truth (read 2026-09-27):
--    • Models/Production/SavedProduction/SavedProductionPhase16Entities.cs
--      — SavedProcessExecution / SavedProcessPairing /
--      SavedProcessAssembly (deterministic identities
--      JobRef|Spec|Serial|Stage|ExecutionSequence, Pair:seq, DGU/LAM:seq;
--      rework lineage ParentExecutionId + ReworkSequence; FOC flag).
--    • Models/Production/SavedProduction/SavedProductionPhase17Entities.cs
--      — SavedWorkstation / SavedProductionControlHold /
--      SavedProductionControlAssignment / SavedProductionControlOverride
--      (hold history preserved when released, assignments deactivated
--      never deleted, priority 0/1/2 influences ordering only).
--    • Services/Production/Process/* — routing (canonical stage order,
--      gating), execution (idempotent start, piece-flow completion,
--      breakage → replacement execution), projection (release → one
--      Pending execution per live work item), pairing (deterministic
--      Outer+Inner by identity, never by dimension match), assembly
--      (component lineage preserved).
--    • Services/Production/Control/* — readiness engine (READY/BLOCKED/
--      …/ON_HOLD with explainable block reasons), controlled transitions
--      (Start/Pause/Resume/Hold/Release), workstation master, queue with
--      deterministic ordering, dashboard KPIs/pressure, progress math.
--
--  SaaS adaptations (documented, mapping §1.7c): GUID PKs + tenant RLS on
--  every table (the WPF's int Ids are SQLite-local); the WPF's
--  WorkItemId int becomes production_piece_id uuid (FK, SET NULL); the
--  WPF's int enums become the SaaS's existing TEXT vocabularies
--  (production_pieces status CHECK from 0014, production_events
--  event_type CHECK, QcResults) so no second status system exists;
--  operator names stay free text (the WPF stores display names — the
--  SaaS records the acting user id in *_by columns alongside).
--  The deterministic string identities (execution_id, pairing_id,
--  assembly_id, hold_id, assignment_id, control_id, workstation_id,
--  piece_id lineage strings) are UNIQUE per tenant — idempotency rides
--  on them exactly as in the WPF.
-- ═══════════════════════════════════════════════════════════════════════

-- ── workstations → the WPF Workstations master (ERP-level only) ──────

CREATE TABLE saas.workstations (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  workstation_id text NOT NULL,
  code text NOT NULL,
  name text NOT NULL,
  process_type integer NOT NULL DEFAULT 1,
  process_name text NOT NULL DEFAULT '',
  is_active boolean NOT NULL DEFAULT true,
  description text NOT NULL DEFAULT '',
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_workstations PRIMARY KEY (id),
  CONSTRAINT fk_workstations_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT ck_workstations_process_type CHECK (process_type BETWEEN 1 AND 8)
);
CREATE UNIQUE INDEX ix_workstations_tenant_workstation_id
  ON saas.workstations (tenant_id, workstation_id);
CREATE INDEX ix_workstations_tenant_process
  ON saas.workstations (tenant_id, process_type, is_active);

-- ── process_executions → the WPF ProcessExecutions ───────────────────

CREATE TABLE saas.process_executions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  execution_id text NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  source_proforma_id text NOT NULL DEFAULT '',
  production_piece_id uuid,
  input_production_piece_id uuid,
  spec_group_number integer NOT NULL DEFAULT 0,
  spec_name text NOT NULL DEFAULT '',
  serial_number integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  piece_id text NOT NULL DEFAULT '',
  input_piece_id text NOT NULL DEFAULT '',
  output_piece_id text NOT NULL DEFAULT '',
  process_type integer NOT NULL DEFAULT 1,
  sequence integer NOT NULL DEFAULT 0,
  status text NOT NULL DEFAULT 'Pending',
  started_by uuid,
  started_at timestamptz,
  completed_by uuid,
  completed_at timestamptz,
  qty_required integer NOT NULL DEFAULT 0,
  qty_done integer NOT NULL DEFAULT 0,
  entry_type text NOT NULL DEFAULT 'Normal',
  reason text NOT NULL DEFAULT '',
  notes text NOT NULL DEFAULT '',
  qc_status text NOT NULL DEFAULT 'Pending',
  qc_id text NOT NULL DEFAULT '',
  parent_execution_id text NOT NULL DEFAULT '',
  rework_sequence integer NOT NULL DEFAULT 0,
  is_foc boolean NOT NULL DEFAULT false,
  previous_stage text NOT NULL DEFAULT '',
  next_stage text NOT NULL DEFAULT '',
  process_detail_json text NOT NULL DEFAULT '',
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_process_executions PRIMARY KEY (id),
  CONSTRAINT fk_process_executions_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_process_executions_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_process_executions_piece FOREIGN KEY (production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT fk_process_executions_input_piece FOREIGN KEY (input_production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT ck_process_executions_process_type CHECK (process_type BETWEEN 1 AND 8),
  CONSTRAINT ck_process_executions_status CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Paused', 'Rework', 'Rejected', 'Cancelled', 'QcPending', 'QcFailed')),
  CONSTRAINT ck_process_executions_entry_type CHECK (entry_type IN ('Normal', 'Breakage', 'Rework', 'Foc', 'QcPassed', 'QcFailed', 'Cancelled')),
  CONSTRAINT ck_process_executions_qc_status CHECK (qc_status IN ('Pending', 'Passed', 'Failed', 'Rework')),
  CONSTRAINT ck_process_executions_qty CHECK (qty_required >= 0 AND qty_done >= 0)
);
-- Deterministic identity — idempotent Start/re-release (WPF UNIQUE
-- ExecutionId convention).
CREATE UNIQUE INDEX ix_process_executions_tenant_execution_id
  ON saas.process_executions (tenant_id, execution_id);
CREATE INDEX ix_process_executions_tenant_job
  ON saas.process_executions (tenant_id, job_ref, spec_group_number, serial_number, process_type);
CREATE INDEX ix_process_executions_piece
  ON saas.process_executions (production_piece_id);
CREATE INDEX ix_process_executions_tenant_process_type
  ON saas.process_executions (tenant_id, process_type, created_at DESC);

-- ── process_pairings → the WPF ProcessPairings (DGU lineage) ─────────

CREATE TABLE saas.process_pairings (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  pairing_id text NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  source_proforma_id text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  pair_id text NOT NULL DEFAULT '',
  outer_piece_id text NOT NULL DEFAULT '',
  inner_piece_id text NOT NULL DEFAULT '',
  outer_glass_ref text NOT NULL DEFAULT '',
  inner_glass_ref text NOT NULL DEFAULT '',
  outer_production_piece_id uuid,
  inner_production_piece_id uuid,
  outer_execution_id text NOT NULL DEFAULT '',
  inner_execution_id text NOT NULL DEFAULT '',
  status text NOT NULL DEFAULT 'Completed',
  notes text NOT NULL DEFAULT '',
  is_foc boolean NOT NULL DEFAULT false,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_process_pairings PRIMARY KEY (id),
  CONSTRAINT fk_process_pairings_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_process_pairings_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_process_pairings_outer_piece FOREIGN KEY (outer_production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT fk_process_pairings_inner_piece FOREIGN KEY (inner_production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE SET NULL,
  CONSTRAINT ck_process_pairings_status CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Paused', 'Rework', 'Rejected', 'Cancelled', 'QcPending', 'QcFailed'))
);
CREATE UNIQUE INDEX ix_process_pairings_tenant_pairing_id
  ON saas.process_pairings (tenant_id, pairing_id);
CREATE INDEX ix_process_pairings_tenant_job
  ON saas.process_pairings (tenant_id, job_ref, spec_group_number, serial_number);

-- ── process_assemblies → the WPF ProcessAssemblies (lineage kept) ────

CREATE TABLE saas.process_assemblies (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  assembly_id text NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  source_proforma_id text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  assembly_label text NOT NULL DEFAULT '',
  assembly_type text NOT NULL DEFAULT 'DGU',
  components_json text NOT NULL DEFAULT '[]',
  spacer_info text NOT NULL DEFAULT '',
  interlayer_info text NOT NULL DEFAULT '',
  sealant_info text NOT NULL DEFAULT '',
  pairing_id text NOT NULL DEFAULT '',
  status text NOT NULL DEFAULT 'Completed',
  qty_required integer NOT NULL DEFAULT 1,
  qty_done integer NOT NULL DEFAULT 1,
  is_foc boolean NOT NULL DEFAULT false,
  qc_id text NOT NULL DEFAULT '',
  notes text NOT NULL DEFAULT '',
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  completed_at timestamptz,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_process_assemblies PRIMARY KEY (id),
  CONSTRAINT fk_process_assemblies_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_process_assemblies_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT ck_process_assemblies_status CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Paused', 'Rework', 'Rejected', 'Cancelled', 'QcPending', 'QcFailed')),
  CONSTRAINT ck_process_assemblies_qty CHECK (qty_required >= 0 AND qty_done >= 0)
);
CREATE UNIQUE INDEX ix_process_assemblies_tenant_assembly_id
  ON saas.process_assemblies (tenant_id, assembly_id);
CREATE INDEX ix_process_assemblies_tenant_job
  ON saas.process_assemblies (tenant_id, job_ref, spec_group_number, serial_number);

-- ── production_control_holds → the WPF ProductionControlHolds ────────

CREATE TABLE saas.production_control_holds (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  hold_id text NOT NULL,
  production_piece_id uuid NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  stage text NOT NULL DEFAULT '',
  hold_reason text NOT NULL DEFAULT '',
  held_by uuid,
  held_at timestamptz NOT NULL DEFAULT now(),
  released_by uuid,
  released_at timestamptz,
  is_released boolean NOT NULL DEFAULT false,
  notes text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_production_control_holds PRIMARY KEY (id),
  CONSTRAINT fk_production_control_holds_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_control_holds_piece FOREIGN KEY (production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_control_holds_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX ix_production_control_holds_tenant_hold_id
  ON saas.production_control_holds (tenant_id, hold_id);
-- Active holds are the hot read (Start refuses while one exists).
CREATE INDEX ix_production_control_holds_piece_active
  ON saas.production_control_holds (production_piece_id, is_released);

-- ── production_control_assignments → WPF ProductionControlAssignments ─

CREATE TABLE saas.production_control_assignments (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  assignment_id text NOT NULL,
  production_piece_id uuid NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  stage text NOT NULL DEFAULT '',
  workstation_id text NOT NULL DEFAULT '',
  operator_name text NOT NULL DEFAULT '',
  assigned_by uuid,
  assigned_at timestamptz NOT NULL DEFAULT now(),
  unassigned_by uuid,
  unassigned_at timestamptz,
  is_active boolean NOT NULL DEFAULT true,
  notes text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_production_control_assignments PRIMARY KEY (id),
  CONSTRAINT fk_production_control_assignments_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_control_assignments_piece FOREIGN KEY (production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_control_assignments_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX ix_production_control_assignments_tenant_assignment_id
  ON saas.production_control_assignments (tenant_id, assignment_id);
CREATE INDEX ix_production_control_assignments_piece_active
  ON saas.production_control_assignments (production_piece_id, is_active);
CREATE INDEX ix_production_control_assignments_tenant_workstation
  ON saas.production_control_assignments (tenant_id, workstation_id, is_active);

-- ── production_control_overrides → WPF ProductionControlOverrides ────

CREATE TABLE saas.production_control_overrides (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  control_id text NOT NULL,
  production_piece_id uuid NOT NULL,
  job_order_id uuid,
  job_ref text NOT NULL DEFAULT '',
  spec_group_number integer NOT NULL DEFAULT 0,
  serial_number integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  stage text NOT NULL DEFAULT '',
  priority integer NOT NULL DEFAULT 0,
  due_date timestamptz,
  priority_set_by uuid,
  priority_set_at timestamptz NOT NULL DEFAULT now(),
  notes text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_production_control_overrides PRIMARY KEY (id),
  CONSTRAINT fk_production_control_overrides_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_control_overrides_piece FOREIGN KEY (production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_control_overrides_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT ck_production_control_overrides_priority CHECK (priority BETWEEN 0 AND 2)
);
CREATE UNIQUE INDEX ix_production_control_overrides_tenant_control_id
  ON saas.production_control_overrides (tenant_id, control_id);
CREATE INDEX ix_production_control_overrides_piece
  ON saas.production_control_overrides (production_piece_id);

-- ── production_pieces: the WPF WorkItem completion/rework columns ────
-- SavedProductionWorkItem carries CompletedAt/CompletedBy/ReplacementSequence
-- (0014 predated the control phase); the timing service, the CompletedToday
-- KPI and the pane-instance replacement lineage (|RPL:n) read them.

ALTER TABLE saas.production_pieces ADD COLUMN completed_at timestamptz;
ALTER TABLE saas.production_pieces ADD COLUMN completed_by uuid;
ALTER TABLE saas.production_pieces ADD COLUMN replacement_sequence integer NOT NULL DEFAULT 0;

CREATE INDEX ix_production_pieces_completed_at
  ON saas.production_pieces (completed_at);

-- ── touch triggers (0005 convention) ─────────────────────────────────

CREATE TRIGGER trg_workstations_touch BEFORE UPDATE ON saas.workstations
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();
CREATE TRIGGER trg_process_executions_touch BEFORE UPDATE ON saas.process_executions
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();
CREATE TRIGGER trg_process_pairings_touch BEFORE UPDATE ON saas.process_pairings
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();
CREATE TRIGGER trg_process_assemblies_touch BEFORE UPDATE ON saas.process_assemblies
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();
CREATE TRIGGER trg_production_control_holds_touch BEFORE UPDATE ON saas.production_control_holds
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();
CREATE TRIGGER trg_production_control_assignments_touch BEFORE UPDATE ON saas.production_control_assignments
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();
CREATE TRIGGER trg_production_control_overrides_touch BEFORE UPDATE ON saas.production_control_overrides
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

-- ── RLS (0005 pattern — DEFAULT PRIVILEGES already grants saas_app) ──

ALTER TABLE saas.workstations ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.workstations FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.workstations
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.process_executions ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.process_executions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.process_executions
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.process_pairings ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.process_pairings FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.process_pairings
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.process_assemblies ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.process_assemblies FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.process_assemblies
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.production_control_holds ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.production_control_holds FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.production_control_holds
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.production_control_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.production_control_assignments FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.production_control_assignments
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE saas.production_control_overrides ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.production_control_overrides FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.production_control_overrides
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

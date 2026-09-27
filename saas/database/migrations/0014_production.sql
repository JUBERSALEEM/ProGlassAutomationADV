-- ═══════════════════════════════════════════════════════════════════════
--  0014 — Phase 9b Production core (WPF-faithful upgrade)
--
--  WPF ground truth (read 2026-09-27):
--    • Models/Production/ProductionModels.cs — ProductionStage
--      (Cutting=1, HolesCutouts=2, Polish=3, EdgeDeletion=4, Mitring=5,
--      Tempering=6, Pairing=7, DoublingLamination=8) and
--      WorkItemStatus (Pending=0, InProgress=1, Completed=2, Paused=3,
--      Rework=4, Rejected=5, Cancelled=6, QcPending=7, QcFailed=8 —
--      append-only, 0..2 frozen).
--    • Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
--      — ProductionEntryType (Normal/Breakage/Rework/Foc/QcPassed/
--      QcFailed/Cancelled) and QcResult (Pending/Passed/Failed/Rework).
--    • Services/Production/ProductionDbContext.cs — the UNIQUE
--      UniqueKey index (a re-release produces the SAME keys, so
--      statuses survive) and the JobRef/Stage/Status/SpecGroupNumber
--      indexes.
--    • Services/Production/ProductionDistributionEngine.cs — the work
--      item shape the plan produces (spec/serial/position/build type,
--      dims, qty, unit/total sqm, total lm, treatment, predecessor
--      stage, charge identity).
--
--  The Phase 3 first-pass tables were guesses with no WPF counterpart
--  (stage 'cutting'/'lamination'/…/​'done', status
--  'pending'/'in_progress'/'qc_pass'/'qc_fail'/'broken'/'released').
--  Both tables are still greenfield in the SaaS (no rows have ever been
--  written — the module did not exist), so the vocabularies are replaced
--  outright and the WPF work-item columns are added. ALTER only — the
--  standing rule from 0012/0013 (never CREATE a Phase 3 table).
--
--  DEFERRED (documented in WPF_TO_SAAS_MAPPING §1.7b): process
--  executions/pairings/assemblies, workstations, control holds/
--  assignments/overrides, actual costs, replacement pieces, the
--  inventory consistency scanner and the optimization bridge (the
--  optimization slice owns cutting sessions).
-- ═══════════════════════════════════════════════════════════════════════

-- ── production_pieces → the WPF ProductionWorkItem ────────────────────

ALTER TABLE saas.production_pieces DROP CONSTRAINT ck_production_pieces_stage;
ALTER TABLE saas.production_pieces DROP CONSTRAINT ck_production_pieces_status;
ALTER TABLE saas.production_pieces
  ADD CONSTRAINT ck_production_pieces_stage
  CHECK (stage IN ('Cutting', 'HolesCutouts', 'Polish', 'EdgeDeletion',
                   'Mitring', 'Tempering', 'Pairing', 'DoublingLamination'));
ALTER TABLE saas.production_pieces
  ADD CONSTRAINT ck_production_pieces_status
  CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Paused', 'Rework',
                    'Rejected', 'Cancelled', 'QcPending', 'QcFailed'));

ALTER TABLE saas.production_pieces ADD COLUMN unique_key text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN spec_group_number integer NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN serial_number integer NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN position text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN build_type text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN spec_name text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN glass_ref text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN unit_sqm numeric(12,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN total_sqm numeric(12,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN total_lm numeric(12,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN treatment text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN predecessor_stage text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN qty_done integer NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN charge_template_key text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN charge_metric_qty numeric(18,3) NOT NULL DEFAULT 0;
ALTER TABLE saas.production_pieces ADD COLUMN charge_metric_unit text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN polish_sub_type_label text NOT NULL DEFAULT '';
ALTER TABLE saas.production_pieces ADD COLUMN is_foc boolean NOT NULL DEFAULT false;
ALTER TABLE saas.production_pieces ADD COLUMN is_cancelled boolean NOT NULL DEFAULT false;
ALTER TABLE saas.production_pieces ADD COLUMN cancelled_at timestamptz;
ALTER TABLE saas.production_pieces ADD COLUMN cancelled_by uuid;
ALTER TABLE saas.production_pieces ADD COLUMN cancel_reason text NOT NULL DEFAULT '';

-- Deterministic identity — a re-release produces the SAME keys, so
-- statuses survive (WPF IX_ProductionWorkItem_UniqueKey).
CREATE UNIQUE INDEX ix_production_pieces_tenant_unique_key
  ON saas.production_pieces (tenant_id, unique_key)
  WHERE unique_key <> '';
CREATE INDEX ix_production_pieces_tenant_status
  ON saas.production_pieces (tenant_id, status);
CREATE INDEX ix_production_pieces_tenant_spec
  ON saas.production_pieces (tenant_id, spec_group_number);
CREATE INDEX ix_production_pieces_tenant_cancelled
  ON saas.production_pieces (tenant_id, is_cancelled);

-- ── production_events → the WPF ProductionEntry ───────────────────────

ALTER TABLE saas.production_events DROP CONSTRAINT ck_production_events_type;
ALTER TABLE saas.production_events
  ADD CONSTRAINT ck_production_events_type
  CHECK (event_type IN ('Normal', 'Breakage', 'Rework', 'Foc', 'QcPassed',
                        'QcFailed', 'Cancelled', 'Stage'));

ALTER TABLE saas.production_events ADD COLUMN job_order_id uuid;
ALTER TABLE saas.production_events ADD COLUMN stage text NOT NULL DEFAULT '';
ALTER TABLE saas.production_events ADD COLUMN spec_group_number integer NOT NULL DEFAULT 0;
ALTER TABLE saas.production_events ADD COLUMN serial_number integer NOT NULL DEFAULT 0;
ALTER TABLE saas.production_events ADD COLUMN position text NOT NULL DEFAULT '';
ALTER TABLE saas.production_events ADD COLUMN spec_name text NOT NULL DEFAULT '';
ALTER TABLE saas.production_events ADD COLUMN glass_ref text NOT NULL DEFAULT '';
ALTER TABLE saas.production_events ADD COLUMN qty integer NOT NULL DEFAULT 0;
ALTER TABLE saas.production_events ADD COLUMN done_by uuid;
ALTER TABLE saas.production_events ADD COLUMN reason text NOT NULL DEFAULT '';
ALTER TABLE saas.production_events ADD COLUMN related_qc_id text NOT NULL DEFAULT '';

CREATE INDEX ix_production_events_tenant_job
  ON saas.production_events (tenant_id, job_order_id, created_at);
CREATE INDEX ix_production_events_tenant_type
  ON saas.production_events (tenant_id, event_type);

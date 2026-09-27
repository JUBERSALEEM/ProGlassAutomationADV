-- ═══════════════════════════════════════════════════════════════════════
-- 0012_job_orders — Phase 9 (Job Order core): upgrade the Phase 3
-- first-pass saas.job_orders to the WPF-faithful shape. Applied 2026-09-27.
--
-- Ground truth (WPF, read 2026-09-27):
--   • Models/JobOrder/SavedJobOrder/SavedJobOrder.cs (entity),
--     JobOrderSnapshot.cs (root JSON snapshot),
--     Common/JobOrderStatus/* (status values + the enum),
--     JobPriority/JobPriority.cs
--   • Services/JobOrder/Mapper/JobOrderMapper.cs (PI → JO conversion)
--   • Services/JobOrder/Repository/JobOrderRepository.cs (insert retry,
--     optimistic RowVersion, soft-close void → Rejected)
--   • Services/Workflow/StatusTransitions.cs (the JobOrder table)
--   • ViewModels/JobOrder/SavedJobsList/*.Part1.cs (convert guards:
--     voided PI refused, existing JOs warn-not-block, Approved → Released)
--
-- What changed from the Phase 3 shape (empty table, safe to reshape):
--   • Status vocabulary: the real machine is Draft→Released→InProduction
--     →Completed→Delivered with Rejected as the void target (the Phase 3
--     guess 'Approved' was not a job-order status at all) — new CHECK +
--     default Draft.
--   • The Phase 3 status_text column is DROPPED — a free-text status
--     column is exactly the spelling-split bug the WPF's
--     JobOrderStatusValues exists to prevent ("InProduction" vs
--     "In Production" under-counted dashboards).
--   • Added the WPF's indexed/searchable columns (customer/project name,
--     grand total copied at conversion) + the ProductionDetails fields
--     (priority, delivery date, site contact, assigned to, cutting
--     instructions, drawing reference, internal notes) + the void block
--     + the conversion-time snapshot summary.
--   • proforma_id becomes NOT NULL (every job order comes from a PI).
-- Single-action statements (the EF generator parses this form).
--
-- The Master Delivery Note the WPF creates with the job order
-- (MasterDeliveryNoteService.EnsureForJobOrderAsync) arrives with the
-- Phase 10 delivery module (documented deferral).
-- ═══════════════════════════════════════════════════════════════════════

BEGIN;

-- ── status: the real machine ──────────────────────────────────────────
ALTER TABLE saas.job_orders DROP CONSTRAINT ck_job_orders_status;

ALTER TABLE saas.job_orders ADD CONSTRAINT ck_job_orders_status
  CHECK (status IN ('Draft','Released','InProduction','Completed','Delivered','Rejected'));

ALTER TABLE saas.job_orders ALTER COLUMN status SET DEFAULT 'Draft';

ALTER TABLE saas.job_orders DROP COLUMN status_text;

-- ── source PI is mandatory (WPF SourceProformaId) ─────────────────────
ALTER TABLE saas.job_orders ALTER COLUMN proforma_id SET NOT NULL;

-- ── WPF indexed/searchable columns ────────────────────────────────────
ALTER TABLE saas.job_orders ADD COLUMN customer_name text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN project_name text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN grand_total_vat_inclusive numeric(18,2) NOT NULL DEFAULT 0;

-- ── WPF ProductionDetails ─────────────────────────────────────────────
ALTER TABLE saas.job_orders ADD COLUMN priority text NOT NULL DEFAULT 'Medium';

ALTER TABLE saas.job_orders ADD CONSTRAINT ck_job_orders_priority
  CHECK (priority IN ('Low','Medium','High'));

ALTER TABLE saas.job_orders ADD COLUMN site_delivery_contact text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN site_delivery_phone text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN site_delivery_address text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN assigned_to text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN cutting_instructions text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN drawing_reference text NOT NULL DEFAULT '';

ALTER TABLE saas.job_orders ADD COLUMN internal_notes text NOT NULL DEFAULT '';

-- ── soft-close void (ERP hardening: keep the row, kill the job) ───────
ALTER TABLE saas.job_orders ADD COLUMN is_void boolean NOT NULL DEFAULT false;

ALTER TABLE saas.job_orders ADD COLUMN voided_at timestamptz;

ALTER TABLE saas.job_orders ADD COLUMN voided_by uuid;

ALTER TABLE saas.job_orders ADD COLUMN void_reason text NOT NULL DEFAULT '';

-- ── conversion-time summary (the WPF embeds the full PI snapshot;
--    the normalized source PI stays addressable — documented) ──────────
ALTER TABLE saas.job_orders ADD COLUMN snapshot_json jsonb;

ALTER TABLE saas.job_orders ADD CONSTRAINT ck_job_orders_row_version
  CHECK (row_version >= 0);

COMMIT;

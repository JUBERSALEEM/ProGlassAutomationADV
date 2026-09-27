-- ═══════════════════════════════════════════════════════════════════════
-- 0013_delivery_notes — Phase 10 (Delivery core): upgrade the Phase 3
-- first-pass delivery tables to the WPF-faithful shape. Applied
-- 2026-09-27.
--
-- Ground truth (WPF, read 2026-09-27):
--   • Models/DeliveryNote/SavedDeliveryNote/{SavedDeliveryNote.cs,
--     DeliveryNoteItem.cs}, Common/DeliveryNoteStatus/*,
--     Common/DeliveryType/*
--   • Services/DeliveryNote/DeliveryNoteWorkflow.cs (IsOpenForDispatch /
--     IsConfirmedDelivery / LineKey)
--   • Services/DeliveryNote/MasterDeliveryNoteService.cs (the balance
--     tracker), DeliveryNoteCreateService.cs (one path for every entry
--     point), Mapper/DeliveryNoteMapper.cs (FromJobOrder path A,
--     BUG-22/23/24 comments)
--   • Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs
--     (BuildMasterDnIdAsync = MDN-{core}, BuildDeliveryNoteIdAsync =
--     DN-{core}-{seq:D2}, GetNextSequenceAsync)
--   • Services/Workflow/StatusTransitions.cs (the DeliveryNote table)
--
-- What changed from the Phase 3 shape (empty tables, safe to reshape):
--   • status_text DROPPED (same rationale as 0012 — a free-text status
--     column is the spelling-split bug class).
--   • Added the master-DN concept (is_master_dn + sequence numbering),
--     the delivery type vocabulary, FOC, the confirm block (driver/
--     vehicle/receiver/date/time), the void block, the qty totals and
--     the WPF line identity (spec group + continuous serial) with the
--     balance columns.
--   • job_order_id becomes NOT NULL (every DN — master or dispatch —
--     belongs to a job order).
-- Deferred to later Phase 10 slices (documented): the JSON snapshot for
-- PDF/export, attachments (files), the history/analytics services, the
-- Tax Invoice link, the weight calculator (needs the Step 4 glass
-- composition port).
-- Single-action statements (the EF generator parses this form).
-- ═══════════════════════════════════════════════════════════════════════

BEGIN;

-- ── delivery_notes ─────────────────────────────────────────────────────
ALTER TABLE saas.delivery_notes DROP COLUMN status_text;

ALTER TABLE saas.delivery_notes ALTER COLUMN job_order_id SET NOT NULL;

ALTER TABLE saas.delivery_notes ADD COLUMN sequence_number integer NOT NULL DEFAULT 1;

ALTER TABLE saas.delivery_notes ADD COLUMN customer_name text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_notes ADD COLUMN project_name text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_notes ADD COLUMN delivery_type text NOT NULL DEFAULT 'Original';

ALTER TABLE saas.delivery_notes ADD CONSTRAINT ck_delivery_notes_type
  CHECK (delivery_type IN ('Original','Dummy','Rework','Return'));

ALTER TABLE saas.delivery_notes ADD COLUMN is_foc boolean NOT NULL DEFAULT false;

ALTER TABLE saas.delivery_notes ADD COLUMN is_master_dn boolean NOT NULL DEFAULT false;

ALTER TABLE saas.delivery_notes ADD COLUMN receiver_name text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_notes ADD COLUMN delivery_time text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_notes ADD COLUMN total_ordered_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_notes ADD COLUMN total_delivered_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_notes ADD COLUMN total_balance_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_notes ADD COLUMN is_void boolean NOT NULL DEFAULT false;

ALTER TABLE saas.delivery_notes ADD COLUMN voided_at timestamptz;

ALTER TABLE saas.delivery_notes ADD COLUMN voided_by uuid;

ALTER TABLE saas.delivery_notes ADD COLUMN void_reason text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_notes ADD COLUMN snapshot_json jsonb;

-- ── delivery_note_items: the WPF line identity + balance columns ──────
ALTER TABLE saas.delivery_note_items ADD COLUMN spec_group_number integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN serial_number integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN spec_group_name text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_note_items ADD COLUMN glass_ref text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_note_items ADD COLUMN w2_mm integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN h2_mm integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN ordered_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN previously_delivered_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN balance_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN rework_qty integer NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN weight_per_piece numeric(12,3) NOT NULL DEFAULT 0;

ALTER TABLE saas.delivery_note_items ADD COLUMN line_notes text NOT NULL DEFAULT '';

ALTER TABLE saas.delivery_note_items ADD COLUMN job_order_id uuid;

COMMIT;

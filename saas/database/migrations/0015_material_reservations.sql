-- ═══════════════════════════════════════════════════════════════════════
--  0015 — Phase 9c Material reservations (WPF-faithful upgrade)
--
--  WPF ground truth (read 2026-09-27):
--    • Services/Production/MaterialReservationService.cs — ReserveAsync
--      (idempotent on the deterministic ReservationId; cancelled/released
--      rows are REACTIVATED after a fresh availability check; available =
--      sheet Balance (Stock − Used) − other active holds), ReleaseAsync
--      (consumed material is never released; idempotent), CancelAsync
--      (consumed never cancelled; idempotent; cascades to allocations),
--      "Not enough stock. Available: {n}, Requested: {m}".
--    • Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
--      — ReservationStatus Available/Reserved/Allocated/Consumed/
--      Released/Cancelled.
--    • BuildReservationId: JobRef|Spec|Serial|GlassSheetId|OptSession|
--      SheetNo.
--
--  The Phase 3 first-pass table (status reserved/consumed/released) was a
--  guess; the table is greenfield (the module did not exist), so the
--  vocabulary is replaced outright and the WPF columns added. ALTER only
--  (standing rule from 0012–0014).
--
--  The allocation ledger (SavedMaterialAllocation) is deferred to the
--  optimization slice — documented in the mapping.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE saas.material_reservations DROP CONSTRAINT ck_material_reservations_status;
ALTER TABLE saas.material_reservations
  ADD CONSTRAINT ck_material_reservations_status
  CHECK (status IN ('Available', 'Reserved', 'Allocated', 'Consumed',
                    'Released', 'Cancelled'));

ALTER TABLE saas.material_reservations ADD COLUMN reservation_key text NOT NULL DEFAULT '';
ALTER TABLE saas.material_reservations ADD COLUMN spec_group_number integer NOT NULL DEFAULT 0;
ALTER TABLE saas.material_reservations ADD COLUMN serial_number integer NOT NULL DEFAULT 0;
ALTER TABLE saas.material_reservations ADD COLUMN glass_ref text NOT NULL DEFAULT '';
ALTER TABLE saas.material_reservations ADD COLUMN spec_name text NOT NULL DEFAULT '';
ALTER TABLE saas.material_reservations ADD COLUMN optimization_session_id uuid;
ALTER TABLE saas.material_reservations ADD COLUMN stock_size_label text NOT NULL DEFAULT '';
ALTER TABLE saas.material_reservations ADD COLUMN width_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.material_reservations ADD COLUMN height_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.material_reservations ADD COLUMN thickness_mm integer NOT NULL DEFAULT 0;
ALTER TABLE saas.material_reservations ADD COLUMN color text NOT NULL DEFAULT '';
ALTER TABLE saas.material_reservations ADD COLUMN notes text NOT NULL DEFAULT '';
ALTER TABLE saas.material_reservations ADD COLUMN released_at timestamptz;
ALTER TABLE saas.material_reservations ADD COLUMN release_reason text NOT NULL DEFAULT '';

-- Deterministic identity — repeated reserve calls with the same key
-- return the existing row (WPF IX on ReservationId).
CREATE UNIQUE INDEX ix_material_reservations_tenant_key
  ON saas.material_reservations (tenant_id, reservation_key)
  WHERE reservation_key <> '';
CREATE INDEX ix_material_reservations_tenant_status
  ON saas.material_reservations (tenant_id, status);

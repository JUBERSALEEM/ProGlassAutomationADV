-- ═══════════════════════════════════════════════════════════════════════
-- 0007_masterdata_parity.sql — ProGlass SaaS (Phase 5)
--
-- Restores three WPF master-data fields the Phase 3 schema missed
-- (found while porting the master-data module against the WPF source of
-- truth — Models/MasterData/SavedCompany.cs, SavedClient.cs):
--
--   • companies.logo_layout / logo_width_mm / logo_height_mm
--     WPF Company Center stores the operator's branding intent
--     (CompanyLogoLayout enum: None / LeftPortrait / TopPanel, sizes in
--     mm with industrial defaults 22 × 26; the PDF letterhead renderer
--     clamps to 8–60 mm at render time, so the stored value is intent,
--     not a validated dimension — no CHECK here, matching WPF).
--   • clients.document_prepared_by
--     WPF saves "Person who prepared the document" on the client profile
--     and prefills it on later documents (auto-saved from invoice Step 3).
--
-- Inline NOT NULL + DEFAULT is safe here (existing rows backfill from the
-- DEFAULT); unlike 0006 there is no cross-column expression involved.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE saas.companies
  ADD COLUMN logo_layout text NOT NULL DEFAULT 'None';

ALTER TABLE saas.companies
  ADD CONSTRAINT ck_companies_logo_layout
  CHECK (logo_layout IN ('None', 'LeftPortrait', 'TopPanel'));

ALTER TABLE saas.companies
  ADD COLUMN logo_width_mm numeric(6,2) NOT NULL DEFAULT 22;

ALTER TABLE saas.companies
  ADD COLUMN logo_height_mm numeric(6,2) NOT NULL DEFAULT 26;

ALTER TABLE saas.clients
  ADD COLUMN document_prepared_by text NOT NULL DEFAULT '';

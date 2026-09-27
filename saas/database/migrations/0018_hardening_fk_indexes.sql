-- ═══════════════════════════════════════════════════════════════════════
--  0018 — Phase 15 hardening: FK index coverage
--
--  The hardening audit (database/verify/hardening_audit.py) found 49
--  foreign-key columns with no leading index. Tenant CASCADE deletes,
--  per-document child reads (spec groups by proforma, placements by
--  sheet, statement lines by statement, …) and join plans all filter
--  on the FK column — unindexed FKs mean sequential scans that grow
--  with the table.
--
--  Indexes only (CREATE INDEX IF NOT EXISTS); no table is altered.
--  Generated from the audit's findings 2026-09-27.
-- ═══════════════════════════════════════════════════════════════════════

CREATE INDEX IF NOT EXISTS ix_plan_features_feature_id ON saas.plan_features (feature_id);
CREATE INDEX IF NOT EXISTS ix_subscriptions_plan_id ON saas.subscriptions (plan_id);
CREATE INDEX IF NOT EXISTS ix_tenant_features_feature_id ON saas.tenant_features (feature_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id ON saas.audit_logs (user_id);
CREATE INDEX IF NOT EXISTS ix_projects_customer_id ON saas.projects (customer_id);
CREATE INDEX IF NOT EXISTS ix_job_orders_proforma_id ON saas.job_orders (proforma_id);
CREATE INDEX IF NOT EXISTS ix_stock_transactions_glass_sheet_id ON saas.stock_transactions (glass_sheet_id);
CREATE INDEX IF NOT EXISTS ix_stock_transactions_inventory_item_id ON saas.stock_transactions (inventory_item_id);
CREATE INDEX IF NOT EXISTS ix_stock_transactions_performed_by ON saas.stock_transactions (performed_by);
CREATE INDEX IF NOT EXISTS ix_job_orders_customer_id ON saas.job_orders (customer_id);
CREATE INDEX IF NOT EXISTS ix_job_orders_project_id ON saas.job_orders (project_id);
CREATE INDEX IF NOT EXISTS ix_job_orders_salesman_id ON saas.job_orders (salesman_id);
CREATE INDEX IF NOT EXISTS ix_inquiries_customer_id ON saas.inquiries (customer_id);
CREATE INDEX IF NOT EXISTS ix_attachments_uploaded_by ON saas.attachments (uploaded_by);
CREATE INDEX IF NOT EXISTS ix_material_reservations_job_order_id ON saas.material_reservations (job_order_id);
CREATE INDEX IF NOT EXISTS ix_proforma_invoices_root_proforma_id ON saas.proforma_invoices (root_proforma_id);
CREATE INDEX IF NOT EXISTS ix_proforma_invoices_inquiry_id ON saas.proforma_invoices (inquiry_id);
CREATE INDEX IF NOT EXISTS ix_proforma_invoices_company_id ON saas.proforma_invoices (company_id);
CREATE INDEX IF NOT EXISTS ix_proforma_invoices_client_id ON saas.proforma_invoices (client_id);
CREATE INDEX IF NOT EXISTS ix_proforma_invoices_project_id ON saas.proforma_invoices (project_id);
CREATE INDEX IF NOT EXISTS ix_proforma_invoices_salesman_id ON saas.proforma_invoices (salesman_id);
CREATE INDEX IF NOT EXISTS ix_spec_groups_proforma_id ON saas.spec_groups (proforma_id);
CREATE INDEX IF NOT EXISTS ix_spec_groups_spec_id ON saas.spec_groups (spec_id);
CREATE INDEX IF NOT EXISTS ix_spec_group_items_spec_group_id ON saas.spec_group_items (spec_group_id);
CREATE INDEX IF NOT EXISTS ix_other_charges_proforma_id ON saas.other_charges (proforma_id);
CREATE INDEX IF NOT EXISTS ix_wastage_rows_proforma_id ON saas.wastage_rows (proforma_id);
CREATE INDEX IF NOT EXISTS ix_production_pieces_job_order_id ON saas.production_pieces (job_order_id);
CREATE INDEX IF NOT EXISTS ix_production_pieces_spec_group_item_id ON saas.production_pieces (spec_group_item_id);
CREATE INDEX IF NOT EXISTS ix_production_events_production_piece_id ON saas.production_events (production_piece_id);
CREATE INDEX IF NOT EXISTS ix_material_reservations_glass_sheet_id ON saas.material_reservations (glass_sheet_id);
CREATE INDEX IF NOT EXISTS ix_material_reservations_reserved_by ON saas.material_reservations (reserved_by);
CREATE INDEX IF NOT EXISTS ix_delivery_notes_job_order_id ON saas.delivery_notes (job_order_id);
CREATE INDEX IF NOT EXISTS ix_delivery_notes_customer_id ON saas.delivery_notes (customer_id);
CREATE INDEX IF NOT EXISTS ix_delivery_notes_project_id ON saas.delivery_notes (project_id);
CREATE INDEX IF NOT EXISTS ix_delivery_note_items_delivery_note_id ON saas.delivery_note_items (delivery_note_id);
CREATE INDEX IF NOT EXISTS ix_delivery_note_items_spec_group_item_id ON saas.delivery_note_items (spec_group_item_id);
CREATE INDEX IF NOT EXISTS ix_tax_invoices_proforma_id ON saas.tax_invoices (proforma_id);
CREATE INDEX IF NOT EXISTS ix_tax_invoices_job_order_id ON saas.tax_invoices (job_order_id);
CREATE INDEX IF NOT EXISTS ix_credit_notes_tax_invoice_id ON saas.credit_notes (tax_invoice_id);
CREATE INDEX IF NOT EXISTS ix_credit_notes_voided_by ON saas.credit_notes (voided_by);
CREATE INDEX IF NOT EXISTS ix_statement_lines_statement_id ON saas.statement_lines (statement_id);
CREATE INDEX IF NOT EXISTS ix_optimization_sheets_session_id ON saas.optimization_sheets (session_id);
CREATE INDEX IF NOT EXISTS ix_optimization_placements_sheet_id ON saas.optimization_placements (sheet_id);
CREATE INDEX IF NOT EXISTS ix_optimization_cuts_sheet_id ON saas.optimization_cuts (sheet_id);
CREATE INDEX IF NOT EXISTS ix_optimization_leftovers_sheet_id ON saas.optimization_leftovers (sheet_id);
CREATE INDEX IF NOT EXISTS ix_specifications_sheet_id ON saas.specifications (sheet_id);
CREATE INDEX IF NOT EXISTS ix_custom_charges_proforma_id ON saas.custom_charges (proforma_id);
CREATE INDEX IF NOT EXISTS ix_pnl_rows_proforma_id ON saas.pnl_rows (proforma_id);
CREATE INDEX IF NOT EXISTS ix_inquiries_project_id ON saas.inquiries (project_id);

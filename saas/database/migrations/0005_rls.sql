-- ═══════════════════════════════════════════════════════════════════════
-- 0005_rls.sql — ProGlass SaaS (Phase 3)
-- DATABASE-LAYER TENANT ISOLATION (ADR-009, master prompt §13).
--
-- Every tenant-owned table gets:
--   • ENABLE + FORCE ROW LEVEL SECURITY (FORCE = even the table owner is
--     subject to the policy when connected as a non-superuser)
--   • one policy: rows are visible/modifiable only when
--       tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
--     USING filters reads/updates/deletes; WITH CHECK blocks INSERT/UPDATE
--     that would write a row into another tenant.
--   • NULLIF treats BOTH "unset" and "reset-to-empty" contexts as NULL →
--     the comparison is NULL → NO ROWS. An unset or cleared context sees
--     NOTHING (fails closed). A malformed non-UUID value raises a loud
--     cast error rather than silently exposing data.
--
-- The application connects as role saas_app (DML only, no DDL). Migrations
-- run as saas_owner. Npgsql issues DISCARD ALL when a pooled connection is
-- returned, so a leaked app.tenant_id cannot survive on a reused
-- connection; the EF interceptor additionally sets it on every open.
--
-- Platform tables (tenants, plans, features) are NOT row-secured: the API
-- gates them by authorization, not tenancy.
-- ═══════════════════════════════════════════════════════════════════════

DO $$
DECLARE
  t text;
  tenant_tables text[] := ARRAY[
    'subscriptions', 'tenant_features', 'users', 'refresh_tokens', 'audit_logs',
    'companies', 'clients', 'projects', 'salesmen',
    'glass_sheets', 'inventory_items', 'stock_transactions',
    'inquiries', 'attachments', 'specifications',
    'tenant_pricing_config', 'tenant_vat_config',
    'proforma_invoices', 'spec_groups', 'spec_group_items', 'other_charges', 'wastage_rows',
    'job_orders', 'production_pieces', 'production_events', 'material_reservations',
    'delivery_notes', 'delivery_note_items',
    'tax_invoices', 'credit_notes', 'statements', 'statement_lines',
    'optimization_sessions', 'optimization_sheets', 'optimization_placements',
    'optimization_cuts', 'optimization_leftovers'
  ];
BEGIN
  FOREACH t IN ARRAY tenant_tables LOOP
    EXECUTE format('ALTER TABLE saas.%I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE saas.%I FORCE ROW LEVEL SECURITY', t);
    EXECUTE format(
      'CREATE POLICY tenant_isolation ON saas.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
      t);
  END LOOP;
END $$;

-- ── roles & grants ────────────────────────────────────────────────────
-- saas_owner: runs migrations (DDL). saas_app: application runtime (DML).
-- (CREATE ROLE is idempotent-guarded; roles survive re-runs.)
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_owner') THEN
    CREATE ROLE saas_owner NOLOGIN;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app') THEN
    CREATE ROLE saas_app NOLOGIN;
  END IF;
END $$;

GRANT USAGE ON SCHEMA saas TO saas_owner, saas_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA saas TO saas_owner;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA saas TO saas_owner;

-- Runtime role: SELECT/INSERT/UPDATE/DELETE only — no TRUNCATE, no DDL.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA saas TO saas_app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA saas TO saas_app;

-- Platform tables are read-only for the runtime role (tenancy does not
-- apply to them; authorization gates them).
REVOKE INSERT, UPDATE, DELETE ON saas.tenants FROM saas_app;
REVOKE INSERT, UPDATE, DELETE ON saas.plans FROM saas_app;
REVOKE INSERT, UPDATE, DELETE ON saas.features FROM saas_app;
REVOKE INSERT, UPDATE, DELETE ON saas.plan_features FROM saas_app;

-- Migration bookkeeping table is migration-runner-only (if it exists yet).
REVOKE ALL ON saas._saas_migrations FROM saas_app;

-- Tables the app must never mutate get read-only for saas_app.
REVOKE INSERT, UPDATE, DELETE ON saas.audit_logs FROM saas_app;
GRANT INSERT ON saas.audit_logs TO saas_app;
REVOKE UPDATE, DELETE ON saas.stock_transactions FROM saas_app;
REVOKE UPDATE, DELETE ON saas.production_events FROM saas_app;
REVOKE UPDATE, DELETE ON saas.optimization_placements FROM saas_app;
REVOKE UPDATE, DELETE ON saas.optimization_cuts FROM saas_app;
REVOKE UPDATE, DELETE ON saas.optimization_leftovers FROM saas_app;

-- Future objects created by saas_owner inherit the same grants.
ALTER DEFAULT PRIVILEGES FOR ROLE saas_owner IN SCHEMA saas
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO saas_app;
ALTER DEFAULT PRIVILEGES FOR ROLE saas_owner IN SCHEMA saas
  GRANT USAGE ON SEQUENCES TO saas_app;

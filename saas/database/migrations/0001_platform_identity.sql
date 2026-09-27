-- ═══════════════════════════════════════════════════════════════════════
-- 0001_platform_identity.sql — ProGlass SaaS (Phase 3)
-- Platform tables (tenants, plans, features, subscriptions) + identity
-- (users, refresh tokens, audit logs) + shared triggers.
-- See saas/docs/SAAS_DATABASE_DESIGN.md — this DDL is the source of truth.
-- NOTE: the `saas` schema itself is created by the migration runner's
-- bootstrap (SqlMigrationRunner / apply_and_test.py), not here.
-- ═══════════════════════════════════════════════════════════════════════

-- ── updated_at maintenance ────────────────────────────────────────────
-- Every table with an updated_at column gets this trigger so the value is
-- correct no matter which path writes the row (EF, SQL, migration seeds).
CREATE OR REPLACE FUNCTION saas.touch_updated_at() RETURNS trigger AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ── tenants (platform — NOT tenant-owned) ─────────────────────────────
CREATE TABLE saas.tenants (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  name text NOT NULL,
  slug text NOT NULL,
  status text NOT NULL DEFAULT 'active',
  timezone text NOT NULL DEFAULT 'Asia/Dubai',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  deleted_at timestamptz,
  CONSTRAINT pk_tenants PRIMARY KEY (id),
  CONSTRAINT uq_tenants_slug UNIQUE (slug),
  CONSTRAINT ck_tenants_status CHECK (status IN ('active', 'suspended', 'offboarded'))
);
CREATE TRIGGER trg_tenants_touch BEFORE UPDATE ON saas.tenants
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

-- ── plans / features (platform catalog) ───────────────────────────────
CREATE TABLE saas.plans (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  code text NOT NULL,
  name text NOT NULL,
  price numeric(18,2) NOT NULL DEFAULT 0,
  billing_period text NOT NULL DEFAULT 'monthly',
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_plans PRIMARY KEY (id),
  CONSTRAINT uq_plans_code UNIQUE (code),
  CONSTRAINT ck_plans_billing_period CHECK (billing_period IN ('monthly', 'yearly', 'lifetime'))
);
CREATE TRIGGER trg_plans_touch BEFORE UPDATE ON saas.plans
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

CREATE TABLE saas.features (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  key text NOT NULL,
  description text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_features PRIMARY KEY (id),
  CONSTRAINT uq_features_key UNIQUE (key)
);
CREATE TRIGGER trg_features_touch BEFORE UPDATE ON saas.features
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

CREATE TABLE saas.plan_features (
  plan_id uuid NOT NULL,
  feature_id uuid NOT NULL,
  limit_value integer,
  CONSTRAINT pk_plan_features PRIMARY KEY (plan_id, feature_id),
  CONSTRAINT fk_plan_features_plan FOREIGN KEY (plan_id) REFERENCES saas.plans (id) ON DELETE CASCADE,
  CONSTRAINT fk_plan_features_feature FOREIGN KEY (feature_id) REFERENCES saas.features (id) ON DELETE CASCADE
);

-- ── subscriptions (tenant-owned) ──────────────────────────────────────
CREATE TABLE saas.subscriptions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  plan_id uuid NOT NULL,
  status text NOT NULL DEFAULT 'trialing',
  period_start date NOT NULL DEFAULT CURRENT_DATE,
  period_end date NOT NULL,
  trial_ends_at timestamptz,
  cancel_reason text,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_subscriptions PRIMARY KEY (id),
  CONSTRAINT fk_subscriptions_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_subscriptions_plan FOREIGN KEY (plan_id) REFERENCES saas.plans (id),
  CONSTRAINT ck_subscriptions_status CHECK (status IN ('trialing', 'active', 'past_due', 'suspended', 'cancelled'))
);
CREATE INDEX ix_subscriptions_tenant ON saas.subscriptions (tenant_id);
CREATE TRIGGER trg_subscriptions_touch BEFORE UPDATE ON saas.subscriptions
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

CREATE TABLE saas.tenant_features (
  tenant_id uuid NOT NULL,
  feature_id uuid NOT NULL,
  is_enabled boolean NOT NULL DEFAULT true,
  limit_value integer,
  CONSTRAINT pk_tenant_features PRIMARY KEY (tenant_id, feature_id),
  CONSTRAINT fk_tenant_features_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_tenant_features_feature FOREIGN KEY (feature_id) REFERENCES saas.features (id) ON DELETE CASCADE
);

-- ── users (tenant-owned) ──────────────────────────────────────────────
-- Password hashes are Argon2id (ADR-006); lockout columns mirror the WPF
-- AppUser semantics (persisted across restarts). Role catalog is the WPF
-- catalog ported verbatim: see ProGlass.Saas.Domain.Authorization.AppRoles.
CREATE TABLE saas.users (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  username text NOT NULL,
  display_name text NOT NULL,
  email text,
  mobile text NOT NULL DEFAULT '',
  password_hash text NOT NULL,
  role text NOT NULL DEFAULT 'Viewer',
  is_active boolean NOT NULL DEFAULT true,
  failed_attempt_count integer NOT NULL DEFAULT 0,
  locked_until_utc timestamptz,
  mfa_secret_encrypted text,
  email_confirmed_at timestamptz,
  must_reset_password boolean NOT NULL DEFAULT false,
  last_login_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_users PRIMARY KEY (id),
  CONSTRAINT fk_users_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT uq_users_tenant_username UNIQUE (tenant_id, username),
  CONSTRAINT uq_users_tenant_email UNIQUE (tenant_id, email),
  CONSTRAINT ck_users_role CHECK (role IN ('Administrator', 'Manager', 'Estimator', 'Sales', 'Production', 'Store', 'Accountant', 'Viewer', 'Operator'))
);
CREATE INDEX ix_users_tenant ON saas.users (tenant_id);
CREATE TRIGGER trg_users_touch BEFORE UPDATE ON saas.users
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

-- ── refresh tokens (tenant-owned, per user) ───────────────────────────
CREATE TABLE saas.refresh_tokens (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  user_id uuid NOT NULL,
  token_hash text NOT NULL,
  expires_at timestamptz NOT NULL,
  revoked_at timestamptz,
  replaced_by_token_hash text,
  created_ip text,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
  CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id) REFERENCES saas.users (id) ON DELETE CASCADE,
  CONSTRAINT fk_refresh_tokens_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE
);
CREATE INDEX ix_refresh_tokens_user ON saas.refresh_tokens (user_id);
CREATE INDEX ix_refresh_tokens_tenant ON saas.refresh_tokens (tenant_id);
CREATE TRIGGER trg_refresh_tokens_touch BEFORE UPDATE ON saas.refresh_tokens
  FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at();

-- ── audit logs (tenant-owned, immutable) ──────────────────────────────
-- Parity with WPF audit.db ActivityLog + before/after payload.
CREATE TABLE saas.audit_logs (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  user_id uuid,
  action text NOT NULL,
  entity_type text NOT NULL DEFAULT '',
  entity_id uuid,
  before_json jsonb,
  after_json jsonb,
  ip text,
  correlation_id text,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_audit_logs PRIMARY KEY (id),
  CONSTRAINT fk_audit_logs_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_audit_logs_user FOREIGN KEY (user_id) REFERENCES saas.users (id) ON DELETE SET NULL
);
CREATE INDEX ix_audit_logs_tenant_created ON saas.audit_logs (tenant_id, created_at);
CREATE INDEX ix_audit_logs_tenant_entity ON saas.audit_logs (tenant_id, entity_type, entity_id);

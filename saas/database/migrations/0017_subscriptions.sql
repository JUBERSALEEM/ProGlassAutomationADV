-- ═══════════════════════════════════════════════════════════════════════
--  0017 — Phase 13 Subscriptions (WPF Licensing → tenant subscriptions)
--
--  WPF ground truth (read 2026-09-27):
--    • LicensePayload.LicenseEdition tiers (doc comments verbatim):
--      Trial "30-day, single-activation, all features"; Basic "single
--      user, inquiry + proforma only"; Business "up to 5 users, full
--      ERP without production optimisation"; Factory "unlimited users,
--      includes cutting-optimisation engine"; Enterprise "unlimited +
--      multi-site + custom integrations".
--    • LicenseService.AutoTrialDays = 30; LicenseVerifier
--      .ExpiryWarningDays = 14; GracePeriod = 7 days read-only.
--
--  The plans/features/plan_features/subscriptions/tenant_features
--  tables exist since 0001 — this migration only SEEDS the platform
--  catalog (idempotent, ON CONFLICT DO NOTHING). No table is created
--  or altered. Registration now also inserts a 30-day trialing
--  subscription (the AutoTrialManager port) — code change, no DDL.
-- ═══════════════════════════════════════════════════════════════════════

-- ── features (module keys; limit_value lives on plan_features) ──────
INSERT INTO saas.features (key, description) VALUES
  ('users',             'Active user seats (null limit = unlimited)'),
  ('inquiries',         'Inquiry / BOQ module'),
  ('proformas',         'Proforma invoice module'),
  ('job_orders',        'Job orders'),
  ('delivery',          'Delivery notes'),
  ('tax_invoices',      'Tax invoices / credit notes / statements / VAT'),
  ('sheet_store',       'Sheet Store (glass stock)'),
  ('production',        'Production pieces + reservations'),
  ('optimization',      'Cutting-optimisation engine'),
  ('reports',           'Pricing config + reports'),
  ('multi_site',        'Multi-site support'),
  ('custom_integrations', 'Custom integrations')
ON CONFLICT (key) DO NOTHING;

-- ── plans (the WPF edition tiers; price/billing = platform decision,
--           seeded 0/monthly as placeholders of record) ──────────────
INSERT INTO saas.plans (code, name, price, billing_period, is_active) VALUES
  ('trial',      'Trial — 30-day evaluation, all features', 0, 'monthly',  true),
  ('basic',      'Basic — single user, inquiry + proforma only', 0, 'monthly', true),
  ('business',   'Business — up to 5 users, full ERP without production optimisation', 0, 'monthly', true),
  ('factory',    'Factory — unlimited users, includes cutting-optimisation engine', 0, 'monthly', true),
  ('enterprise', 'Enterprise — unlimited + multi-site + custom integrations', 0, 'yearly',  true)
ON CONFLICT (code) DO NOTHING;

-- ── plan_features (edition tier doc comments, made concrete) ────────
-- Trial: ALL features on (users unlimited — the WPF trial limits the
-- machine, not the seats; 30 days is the limit).
INSERT INTO saas.plan_features (plan_id, feature_id, limit_value)
SELECT p.id, f.id, NULL
FROM saas.plans p CROSS JOIN saas.features f
WHERE p.code = 'trial'
ON CONFLICT (plan_id, feature_id) DO NOTHING;

-- Basic: single user; inquiry + proforma ONLY.
INSERT INTO saas.plan_features (plan_id, feature_id, limit_value)
SELECT p.id, f.id, v.lim
FROM saas.plans p
JOIN (VALUES
  ('users', 1), ('inquiries', NULL), ('proformas', NULL)
) AS v(fkey, lim) ON TRUE
JOIN saas.features f ON f.key = v.fkey
WHERE p.code = 'basic'
ON CONFLICT (plan_id, feature_id) DO NOTHING;

-- Business: up to 5 users; full ERP WITHOUT production optimisation.
INSERT INTO saas.plan_features (plan_id, feature_id, limit_value)
SELECT p.id, f.id, v.lim
FROM saas.plans p
JOIN (VALUES
  ('users', 5), ('inquiries', NULL), ('proformas', NULL),
  ('job_orders', NULL), ('delivery', NULL), ('tax_invoices', NULL),
  ('sheet_store', NULL), ('production', NULL), ('reports', NULL)
) AS v(fkey, lim) ON TRUE
JOIN saas.features f ON f.key = v.fkey
WHERE p.code = 'business'
ON CONFLICT (plan_id, feature_id) DO NOTHING;

-- Factory: unlimited users + the optimisation engine.
INSERT INTO saas.plan_features (plan_id, feature_id, limit_value)
SELECT p.id, f.id, NULL
FROM saas.plans p CROSS JOIN saas.features f
WHERE p.code = 'factory' AND f.key <> 'multi_site'
ON CONFLICT (plan_id, feature_id) DO NOTHING;

-- Enterprise: everything.
INSERT INTO saas.plan_features (plan_id, feature_id, limit_value)
SELECT p.id, f.id, NULL
FROM saas.plans p CROSS JOIN saas.features f
WHERE p.code = 'enterprise'
ON CONFLICT (plan_id, feature_id) DO NOTHING;

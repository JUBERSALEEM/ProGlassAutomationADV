-- =====================================================================
-- PROGLASS AUTOMATION — MULTI-TENANCY FOUNDATION (Phase 2, DORMANT)
-- Project: afpbdyamedpanwijxghk
-- =====================================================================
--
-- PURPOSE
--   Prepare the single-operator schema to serve many glass companies
--   ("ProGlass Cloud", docs/MONETIZATION_PLAN.md Phase 2) WITHOUT
--   changing any current behaviour. Today every table implicitly
--   belongs to one business; this file introduces the tenant objects
--   and nullable tenant columns so activation later is a backfill +
--   policy flip, not a migration under pressure.
--
-- SAFETY PROPERTIES (verify before running — the cross-check did)
--   * Additive only: CREATE TABLE IF NOT EXISTS, ADD COLUMN IF NOT
--     EXISTS, new indexes. No DROP, no DELETE, no UPDATE, no NOT NULL
--     tightening, no change to any existing policy.
--   * New tenant columns are NULLABLE and unread by current code.
--   * New tables ship with RLS on and read-own-rows policies only.
--   * Run ONLY after Phase 0 verification (supabase-records.sql bottom)
--     so live policies are known before tenant policies join them.
--
-- ACTIVATION (later, in order — templates at the bottom of this file)
--   1. Insert the default company + operator memberships.
--   2. Backfill company_id on existing rows.
--   3. Add tenant RLS policies per table; test as anon + 2 companies.
--   4. Wire src/lib/tenant.ts into the APIs; flip
--      NEXT_PUBLIC_CLOUD_TENANCY=on in Preview, then Production.
--   5. Only then consider SET NOT NULL on company_id columns.
-- =====================================================================

-- ── Tenants ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.companies (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name       text NOT NULL,
  slug       text UNIQUE,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.company_members (
  company_id uuid NOT NULL REFERENCES public.companies(id) ON DELETE CASCADE,
  user_id    uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
  role       text NOT NULL DEFAULT 'operator'
             CHECK (role IN ('owner','admin','operator','viewer')),
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (company_id, user_id)
);

CREATE TABLE IF NOT EXISTS public.subscriptions (
  id                        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  company_id                uuid NOT NULL REFERENCES public.companies(id) ON DELETE CASCADE,
  plan                      text NOT NULL DEFAULT 'cloud',
  status                    text NOT NULL DEFAULT 'trialing'
                            CHECK (status IN ('trialing','active','past_due','cancelled')),
  current_period_end        timestamptz,
  provider                  text,
  provider_subscription_id  text UNIQUE,
  created_at                timestamptz NOT NULL DEFAULT now(),
  updated_at                timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_company_members_user
  ON public.company_members (user_id);
CREATE INDEX IF NOT EXISTS idx_subscriptions_company
  ON public.subscriptions (company_id, status);

-- ── RLS: read-own only (writes via dashboard/future RPCs) ──────────
-- company_members' own policy must not reference other tenant tables,
-- or policies evaluating it recurse. user_id = auth.uid() is the base.
ALTER TABLE public.companies       ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.company_members ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.subscriptions   ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS companies_read_own ON public.companies;
CREATE POLICY companies_read_own ON public.companies
  FOR SELECT TO authenticated
  USING (id IN (SELECT company_id FROM public.company_members
                 WHERE user_id = auth.uid()));

DROP POLICY IF EXISTS company_members_read_own ON public.company_members;
CREATE POLICY company_members_read_own ON public.company_members
  FOR SELECT TO authenticated
  USING (user_id = auth.uid());

DROP POLICY IF EXISTS subscriptions_read_own ON public.subscriptions;
CREATE POLICY subscriptions_read_own ON public.subscriptions
  FOR SELECT TO authenticated
  USING (company_id IN (SELECT company_id FROM public.company_members
                         WHERE user_id = auth.uid()));

-- The caller's companies, for future table policies. SECURITY DEFINER
-- so a policy on (say) records can ask "is this row one of mine?"
-- without re-entering company_members' RLS per row.
CREATE OR REPLACE FUNCTION public.current_company_ids()
RETURNS SETOF uuid
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT company_id FROM public.company_members WHERE user_id = auth.uid()
$$;

GRANT EXECUTE ON FUNCTION public.current_company_ids() TO authenticated, service_role;

-- ── Tenant columns on the shared tables (nullable, unread today) ───
ALTER TABLE public.records
  ADD COLUMN IF NOT EXISTS company_id uuid REFERENCES public.companies(id);
ALTER TABLE public.inquiries
  ADD COLUMN IF NOT EXISTS company_id uuid REFERENCES public.companies(id);
ALTER TABLE public.inquiry_customer_submissions
  ADD COLUMN IF NOT EXISTS company_id uuid REFERENCES public.companies(id);
ALTER TABLE public.production_job_orders
  ADD COLUMN IF NOT EXISTS company_id uuid REFERENCES public.companies(id);

CREATE INDEX IF NOT EXISTS idx_records_company
  ON public.records (company_id) WHERE company_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_inquiries_company
  ON public.inquiries (company_id) WHERE company_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_customer_submissions_company
  ON public.inquiry_customer_submissions (company_id) WHERE company_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_job_orders_company
  ON public.production_job_orders (company_id) WHERE company_id IS NOT NULL;

-- =====================================================================
-- ACTIVATION TEMPLATES — commented on purpose. Uncomment per step,
-- in order, during a supervised activation (MONETIZATION_PLAN.md).
-- =====================================================================
--
-- Step 1 · default company + memberships:
--   INSERT INTO public.companies (name, slug) VALUES ('<Business Name>', '<slug>')
--   RETURNING id;  -- note the id as :cid
--   INSERT INTO public.company_members (company_id, user_id, role)
--   SELECT '<cid>', id, 'owner' FROM auth.users WHERE email = '<operator email>';
--
-- Step 2 · backfill (repeat per table):
--   UPDATE public.records SET company_id = '<cid>' WHERE company_id IS NULL;
--
-- Step 3 · tenant policy per table (example: records). Existing live
-- policies stay; this ADDS a tenant-scoped read for members. Remove or
-- tighten any broader legacy SELECT policy found in Phase 0 export:
--   CREATE POLICY records_tenant_read ON public.records
--     FOR SELECT TO authenticated
--     USING (company_id IN (SELECT public.current_company_ids()));
--
-- Step 5 · only after backfill is proven complete:
--   ALTER TABLE public.records ALTER COLUMN company_id SET NOT NULL;

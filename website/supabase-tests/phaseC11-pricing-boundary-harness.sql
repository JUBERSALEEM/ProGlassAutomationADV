-- PHASE C11 PRICING-BOUNDARY SCRATCH HARNESS
--
-- THROWAWAY DATABASE ONLY. Run in order:
--   psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phaseC11-pricing-boundary-harness.sql
--   psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-inquiry-proforma-phaseC11-customer-pricing-boundary.sql
--   psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phaseC11-pricing-boundary-assertions.sql
--
-- The fixture recreates the relevant Phase 15/C7 access shape with customer,
-- inquiry admin, auditor and no-role accounts. It does not connect to Supabase.

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF; END $$;
GRANT USAGE ON SCHEMA public TO authenticated, anon;

CREATE SCHEMA auth;
GRANT USAGE ON SCHEMA auth TO authenticated;
CREATE TABLE auth.users (id uuid PRIMARY KEY, email text);
CREATE OR REPLACE FUNCTION auth.uid() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT nullif(nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'sub', '')::uuid;
$$;
GRANT EXECUTE ON FUNCTION auth.uid() TO authenticated;

CREATE TABLE public.inquiry_operator_roles (
  user_id uuid PRIMARY KEY,
  role text NOT NULL,
  enabled boolean NOT NULL DEFAULT true
);
CREATE OR REPLACE FUNCTION public.has_inquiry_role(p_roles text[])
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
  SELECT auth.uid() IS NOT NULL AND EXISTS (
    SELECT 1 FROM public.inquiry_operator_roles r
     WHERE r.user_id = auth.uid() AND r.enabled AND r.role = ANY(p_roles));
$$;
REVOKE ALL ON FUNCTION public.has_inquiry_role(text[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.has_inquiry_role(text[]) TO authenticated;

CREATE TABLE public.inquiry_proformas (
  id uuid PRIMARY KEY,
  inquiry_id uuid NOT NULL,
  proforma_number text NOT NULL,
  status text NOT NULL,
  revision integer NOT NULL,
  source_system text NOT NULL DEFAULT 'website',
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE public.inquiry_proforma_lines (
  proforma_id uuid NOT NULL REFERENCES public.inquiry_proformas(id),
  line_no integer NOT NULL,
  approved_rate numeric NOT NULL,
  line_value numeric NOT NULL
);
CREATE TABLE public.inquiry_proforma_saves (
  proforma_id uuid NOT NULL REFERENCES public.inquiry_proformas(id),
  revision integer NOT NULL,
  total_value numeric NOT NULL
);
CREATE TABLE public.inquiry_proforma_groups (
  proforma_id uuid NOT NULL REFERENCES public.inquiry_proformas(id),
  group_no integer NOT NULL,
  base_rate numeric NOT NULL
);
CREATE TABLE public.inquiry_proforma_shares (
  proforma_id uuid PRIMARY KEY REFERENCES public.inquiry_proformas(id),
  customer_user_id uuid NOT NULL REFERENCES auth.users(id),
  revision integer NOT NULL,
  shared_by uuid NOT NULL REFERENCES auth.users(id),
  shared_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE public.inquiry_proformas ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_saves ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_groups ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_shares ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON public.inquiry_proformas, public.inquiry_proforma_lines,
  public.inquiry_proforma_saves, public.inquiry_proforma_groups,
  public.inquiry_proforma_shares TO authenticated;

-- Phase 15 and C7 policies before the boundary migration.
CREATE POLICY "Operators read proformas" ON public.inquiry_proformas
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));
CREATE POLICY "Customers read their shared proforma" ON public.inquiry_proformas
  FOR SELECT TO authenticated USING (
    EXISTS (SELECT 1 FROM public.inquiry_proforma_shares s
      WHERE s.proforma_id = inquiry_proformas.id AND s.customer_user_id = auth.uid())
  );
CREATE POLICY "Operators read proforma lines" ON public.inquiry_proforma_lines
  FOR SELECT TO authenticated USING (
    EXISTS (SELECT 1 FROM public.inquiry_proformas p WHERE p.id = proforma_id)
  );
CREATE POLICY "Operators read proforma saves" ON public.inquiry_proforma_saves
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));
CREATE POLICY "Operators read proforma groups" ON public.inquiry_proforma_groups
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));
CREATE POLICY "Owners and operators read proforma shares" ON public.inquiry_proforma_shares
  FOR SELECT TO authenticated USING (
    customer_user_id = auth.uid() OR public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor'])
  );

-- Preserve the staff save RPC permission shape; the real function body is
-- covered by the existing Phase 15/C5/C8 contract tests.
CREATE OR REPLACE FUNCTION public.save_inquiry_proforma_v2(
  p_inquiry_id uuid, p_idempotency_key text, p_expected_revision integer,
  p_currency text, p_lines jsonb, p_groups jsonb
) RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'An enabled inquiry administrator role is required.' USING ERRCODE='42501';
  END IF;
  RETURN p_inquiry_id;
END;
$$;
REVOKE ALL ON FUNCTION public.save_inquiry_proforma_v2(uuid,text,integer,text,jsonb,jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.save_inquiry_proforma_v2(uuid,text,integer,text,jsonb,jsonb) TO authenticated;

INSERT INTO auth.users (id, email) VALUES
  ('aaaaaaaa-0000-4000-8000-000000000001', 'customer@example.test'),
  ('aaaaaaaa-0000-4000-8000-000000000002', 'admin@example.test'),
  ('aaaaaaaa-0000-4000-8000-000000000003', 'auditor@example.test'),
  ('aaaaaaaa-0000-4000-8000-000000000004', 'visitor@example.test');
INSERT INTO public.inquiry_operator_roles (user_id, role, enabled) VALUES
  ('aaaaaaaa-0000-4000-8000-000000000002', 'inquiry_admin', true),
  ('aaaaaaaa-0000-4000-8000-000000000003', 'inquiry_auditor', true);
INSERT INTO public.inquiry_proformas (id, inquiry_id, proforma_number, status, revision)
VALUES ('bbbbbbbb-0000-4000-8000-000000000001', 'cccccccc-0000-4000-8000-000000000001', 'PI-WEB-TEST-000001', 'Sent', 4);
INSERT INTO public.inquiry_proforma_lines (proforma_id, line_no, approved_rate, line_value)
VALUES ('bbbbbbbb-0000-4000-8000-000000000001', 1, 125.50, 500.25);
INSERT INTO public.inquiry_proforma_saves (proforma_id, revision, total_value)
VALUES ('bbbbbbbb-0000-4000-8000-000000000001', 4, 500.25);
INSERT INTO public.inquiry_proforma_groups (proforma_id, group_no, base_rate)
VALUES ('bbbbbbbb-0000-4000-8000-000000000001', 1, 100.00);
INSERT INTO public.inquiry_proforma_shares (proforma_id, customer_user_id, revision, shared_by)
VALUES ('bbbbbbbb-0000-4000-8000-000000000001', 'aaaaaaaa-0000-4000-8000-000000000001', 4, 'aaaaaaaa-0000-4000-8000-000000000002');

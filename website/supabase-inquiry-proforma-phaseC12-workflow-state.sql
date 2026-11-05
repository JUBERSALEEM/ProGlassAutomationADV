-- Phase C12: revision-scoped staff PI Step 4 and Step 6 state.
-- Apply after Phase C8 and C11, before deploying the matching website build.
-- This is a local migration artifact only; it has NOT been applied to Production.
BEGIN;

CREATE TABLE IF NOT EXISTS public.inquiry_proforma_workflow_states (
  proforma_id uuid NOT NULL REFERENCES public.inquiry_proformas(id) ON DELETE CASCADE,
  revision integer NOT NULL CHECK (revision >= 1),
  snapshot jsonb NOT NULL CHECK (jsonb_typeof(snapshot) = 'object'),
  saved_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (proforma_id, revision)
);

ALTER TABLE public.inquiry_proforma_workflow_states ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.inquiry_proforma_workflow_states FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.inquiry_proforma_workflow_states TO authenticated;
DROP POLICY IF EXISTS "Operators read proforma workflow states" ON public.inquiry_proforma_workflow_states;
CREATE POLICY "Operators read proforma workflow states" ON public.inquiry_proforma_workflow_states
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

-- Retain v2's existing line, rate, authorization, revision, idempotency and
-- validation behavior; store the additional staff-only snapshot atomically.
CREATE OR REPLACE FUNCTION public.save_inquiry_proforma_v3(
  p_inquiry_id uuid,
  p_idempotency_key text,
  p_expected_revision integer,
  p_currency text,
  p_lines jsonb,
  p_groups jsonb,
  p_workflow_snapshot jsonb
) RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_id uuid;
  v_revision integer;
  v_existing_revision integer;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'An enabled inquiry administrator role is required to save a proforma.'
      USING ERRCODE = '42501';
  END IF;
  IF p_idempotency_key IS NULL OR p_idempotency_key !~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
    RAISE EXCEPTION 'A canonical idempotency key is required.' USING ERRCODE = '22023';
  END IF;
  IF p_workflow_snapshot IS NULL OR jsonb_typeof(p_workflow_snapshot) <> 'object'
     OR octet_length(p_workflow_snapshot::text) > 262144 THEN
    RAISE EXCEPTION 'A bounded Step 4/6 workflow snapshot is required.'
      USING ERRCODE = '22023';
  END IF;

  -- Serialize retries using the same idempotency key. A retry returns the
  -- original revision and never replaces its snapshot with stale client state.
  PERFORM pg_advisory_xact_lock(hashtextextended(p_idempotency_key, 0));
  SELECT s.revision INTO v_existing_revision
    FROM public.inquiry_proforma_saves s
   WHERE s.idempotency_key = p_idempotency_key::uuid;

  v_id := public.save_inquiry_proforma_v2(
    p_inquiry_id, p_idempotency_key, p_expected_revision, p_currency, p_lines, p_groups
  );

  IF v_existing_revision IS NULL THEN
    SELECT s.revision INTO v_revision
      FROM public.inquiry_proforma_saves s
     WHERE s.proforma_id = v_id AND s.idempotency_key = p_idempotency_key::uuid;
    IF v_revision IS NULL THEN
      RAISE EXCEPTION 'The saved proforma revision could not be located.' USING ERRCODE = 'P0002';
    END IF;
    INSERT INTO public.inquiry_proforma_workflow_states (proforma_id, revision, snapshot)
      VALUES (v_id, v_revision, p_workflow_snapshot)
      ON CONFLICT (proforma_id, revision) DO NOTHING;
  END IF;
  RETURN v_id;
END;
$$;
REVOKE ALL ON FUNCTION public.save_inquiry_proforma_v3(uuid, text, integer, text, jsonb, jsonb, jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.save_inquiry_proforma_v3(uuid, text, integer, text, jsonb, jsonb, jsonb) TO authenticated;

COMMIT;

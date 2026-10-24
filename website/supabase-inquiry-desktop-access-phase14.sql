-- PHASE 14: THE ADMIN ACCOUNT MAY RUN THE DESKTOP DELIVERY LOOP
--
-- Why this exists: the desktop inbox signs in with a real Supabase account
-- and then calls plan_customer_inquiry_deliveries_v2 first. Phase 13 left
-- that function gated to inquiry_bridge alone, so the owner's own account
-- (inquiry_admin) signed in successfully and was then refused with 42501,
-- which the desktop correctly renders as "Signed in, this account is not
-- authorized". That is a permissions wall, not a broken sign-in.
--
-- This migration lets inquiry_admin run the same three delivery calls as the
-- machine bridge, so one account can drive the shop PC. Nothing else moves:
-- the estimator, auditor and viewer boundaries are untouched, the bridge gate
-- is only widened, and every function keeps its signature, its definer flag,
-- its pinned search_path and its grants.
--
-- Trade-off, stated plainly: if an admin and the machine bridge both run the
-- delivery loop, both can plan the same accepted inquiry. The ledger still
-- allows exactly one acknowledgement, so the second one fails closed with a
-- conflict instead of duplicating a delivery. Run one or the other per shop.
--
-- Applied on project afpbdyamedpanwijxghk. See
-- docs/PHASE14_DESKTOP_OPERATOR_ACCESS.md for the diagnosis, the evidence,
-- the verification and the rollback.

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. The self-access report must tell the truth about the wider gate
-- ---------------------------------------------------------------------------
--
-- inquiry_capabilities_for_roles gains two rows for inquiry_admin. The name
-- inbox.plan.bridge is historical: it means "may plan and acknowledge desktop
-- deliveries", which the admin now may. The bridge's own capability list is
-- unchanged, so the desktop can test one capability name for either account.

CREATE OR REPLACE FUNCTION public.inquiry_capabilities_for_roles(p_roles text[])
RETURNS text[]
LANGUAGE sql
IMMUTABLE
SET search_path = public, pg_temp
AS $function$
  SELECT coalesce(array_agg(DISTINCT capability ORDER BY capability), ARRAY[]::text[])
  FROM unnest(coalesce(p_roles, ARRAY[]::text[])) AS role_name
  CROSS JOIN LATERAL (
    SELECT capability
    FROM (VALUES
      ('inquiry_admin',     'audit.read'),
      ('inquiry_admin',     'chat.read'),
      ('inquiry_admin',     'chat.write'),
      ('inquiry_admin',     'estimator.manage'),
      ('inquiry_admin',     'inbox.acknowledge'),
      ('inquiry_admin',     'inbox.download'),
      ('inquiry_admin',     'inbox.plan.bridge'),
      ('inquiry_admin',     'inbox.read.all'),
      ('inquiry_admin',     'inbox.review'),
      ('inquiry_admin',     'pipeline.write'),
      ('inquiry_auditor',   'audit.read'),
      ('inquiry_auditor',   'chat.read'),
      ('inquiry_auditor',   'inbox.download'),
      ('inquiry_auditor',   'inbox.read.all'),
      ('inquiry_estimator', 'chat.read'),
      ('inquiry_estimator', 'chat.write'),
      ('inquiry_estimator', 'inbox.download'),
      ('inquiry_estimator', 'inbox.read.assigned'),
      ('inquiry_estimator', 'pipeline.write'),
      ('inquiry_bridge',    'chat.read'),
      ('inquiry_bridge',    'inbox.acknowledge'),
      ('inquiry_bridge',    'inbox.download'),
      ('inquiry_bridge',    'inbox.plan.bridge'),
      ('inquiry_bridge',    'pipeline.write')
    ) AS mapping(role, capability)
    WHERE mapping.role = role_name
  ) matched;
$function$;

-- ---------------------------------------------------------------------------
-- 2. Plan the deliveries: bridge as before, plus an enabled admin
-- ---------------------------------------------------------------------------

-- Dropped and recreated on purpose: this keeps the migration correct even if
-- a scratch database stubbed a narrower RETURN TABLE shape. The transaction
-- makes the gap invisible to callers, and the grants are restated below.
DROP FUNCTION IF EXISTS public.plan_customer_inquiry_deliveries_v2(integer);

CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_deliveries_v2(p_limit integer DEFAULT 25)
RETURNS TABLE(submission_id uuid, submission_reference text, submitted_at timestamp with time zone, status text, contact_name text, company_name text, phone text, customer_reference text, attention_name text, vat_trn_number text, salesman_reference text, handover_estimator_name text, project_name text, project_location text, inquiry_type text, description text, document_count bigint, primary_estimator_code text, assignment_snapshot jsonb, routing_key text)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'public', 'pg_temp'
AS $function$
BEGIN
  -- The machine bridge and an enabled admin may both run this desk. The
  -- refusal keeps SQLSTATE 42501 so the gateway still answers 403.
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge', 'inquiry_admin']) THEN
    RAISE EXCEPTION 'Inquiry bridge or admin role required.' USING ERRCODE='42501';
  END IF;
  RETURN QUERY SELECT s.id,s.submission_reference,s.submitted_at,s.status,s.contact_name,s.company_name,
    s.phone,s.customer_reference,s.attention_name,s.vat_trn_number,s.salesman_reference,
    s.handover_estimator_name,s.project_name,s.project_location,s.inquiry_type,s.description,
    count(d.id)::bigint,p.estimator_code,public.customer_inquiry_assignment_snapshot(s.id),
    CASE WHEN p.estimator_code IS NULL THEN 'unassigned' ELSE 'assigned/'||p.estimator_code END
  FROM public.inquiry_customer_submissions s
  LEFT JOIN public.inquiry_customer_documents d ON d.submission_id=s.id AND d.availability='available'
  LEFT JOIN public.inquiry_customer_deliveries x ON x.submission_id=s.id
  LEFT JOIN LATERAL (
    SELECT e.estimator_code FROM public.inquiry_estimator_assignments a
    JOIN public.inquiry_estimators e ON e.user_id=a.estimator_user_id
    WHERE a.submission_id=s.id AND a.active AND a.assignment_role='primary' AND e.active LIMIT 1
  ) p ON true
  WHERE s.upload_state='complete' AND s.status='accepted' AND x.submission_id IS NULL
  GROUP BY s.id,p.estimator_code ORDER BY s.submitted_at,s.id LIMIT greatest(1,least(coalesce(p_limit,25),100));
END $function$;

-- ---------------------------------------------------------------------------
-- 3. Plan the documents: same widening, same accepted-only guard
-- ---------------------------------------------------------------------------

DROP FUNCTION IF EXISTS public.plan_customer_inquiry_delivery_documents(uuid);

CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_delivery_documents(p_submission_id uuid)
RETURNS TABLE(document_id uuid, original_filename text, detected_mime text, byte_size bigint, content_sha256 text, storage_bucket text, storage_object_key text)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'public', 'pg_temp'
AS $function$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge', 'inquiry_admin']) THEN
    RAISE EXCEPTION 'Inquiry bridge or admin role required.' USING ERRCODE='42501'; END IF;
  IF NOT EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=p_submission_id
    AND s.upload_state='complete' AND s.status='accepted') THEN
    RAISE EXCEPTION 'Accepted submission not found.' USING ERRCODE='P0002'; END IF;
  RETURN QUERY SELECT d.id,d.original_filename,d.detected_mime,d.byte_size,d.content_sha256,
    d.storage_bucket,d.storage_object_key FROM public.inquiry_customer_documents d
    WHERE d.submission_id=p_submission_id AND d.availability='available' ORDER BY d.uploaded_at,d.id;
END $function$;

-- ---------------------------------------------------------------------------
-- 4. Acknowledge the delivery: bridge as before, plus an enabled admin
-- ---------------------------------------------------------------------------
--
-- Every check after the gate is unchanged: the replay check runs before the
-- queue so a retry returns quietly, the document count must still match, the
-- assignment snapshot must still match, and the routing key is still derived
-- from the database rather than trusted from the caller.

CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery_v2(p_submission_id uuid, p_manifest_sha256 text, p_document_count integer, p_routing_key text, p_assignment_snapshot jsonb)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'public', 'pg_temp'
AS $function$
DECLARE
  v_expected integer;
  v_current  jsonb;
  v_primary  text;
  v_route    text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge', 'inquiry_admin']) THEN
    RAISE EXCEPTION 'Inquiry bridge or admin role required.' USING ERRCODE = '42501';
  END IF;
  IF p_manifest_sha256 !~ '^[0-9a-f]{64}$'
     OR p_document_count NOT BETWEEN 0 AND 15
     OR jsonb_typeof(p_assignment_snapshot) <> 'array' THEN
    RAISE EXCEPTION 'Invalid acknowledgement.' USING ERRCODE = '22023';
  END IF;

  -- Idempotency first: a replay of an accepted delivery is not a new delivery.
  IF EXISTS (SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id = p_submission_id) THEN
    IF EXISTS (
      SELECT 1 FROM public.inquiry_customer_deliveries
       WHERE submission_id = p_submission_id
         AND manifest_sha256 = p_manifest_sha256
         AND document_count = p_document_count
         AND routing_key = p_routing_key
         AND assignment_snapshot = p_assignment_snapshot
    ) THEN
      RETURN;
    END IF;
    RAISE EXCEPTION 'Conflicting acknowledgement.' USING ERRCODE = '23505';
  END IF;

  SELECT count(*) INTO v_expected
    FROM public.inquiry_customer_documents d
    JOIN public.inquiry_customer_submissions s ON s.id = d.submission_id
   WHERE s.id = p_submission_id
     AND s.upload_state = 'complete'
     AND s.status = 'accepted'
     AND d.availability = 'available';
  IF v_expected <> p_document_count THEN
    RAISE EXCEPTION 'Document count changed.' USING ERRCODE = '40001';
  END IF;

  v_current := public.customer_inquiry_assignment_snapshot(p_submission_id);
  IF v_current <> p_assignment_snapshot THEN
    RAISE EXCEPTION 'Estimator assignment changed.' USING ERRCODE = '40001';
  END IF;
  SELECT value ->> 'estimatorCode' INTO v_primary
    FROM jsonb_array_elements(v_current) value
   WHERE value ->> 'role' = 'primary'
   LIMIT 1;
  v_route := CASE WHEN v_primary IS NULL THEN 'unassigned' ELSE 'assigned/' || v_primary END;
  IF p_routing_key IS DISTINCT FROM v_route THEN
    RAISE EXCEPTION 'Invalid routing key.' USING ERRCODE = '22023';
  END IF;

  INSERT INTO public.inquiry_customer_deliveries(
    submission_id, bridge_user_id, manifest_sha256, document_count,
    routing_key, primary_estimator_code, assignment_snapshot)
  VALUES (p_submission_id, auth.uid(), p_manifest_sha256, p_document_count,
    p_routing_key, v_primary, p_assignment_snapshot);
  UPDATE public.inquiry_customer_submissions
     SET status = 'downloaded',
         downloaded_at = coalesce(downloaded_at, now()),
         status_updated_at = now()
   WHERE id = p_submission_id AND status = 'accepted';
  INSERT INTO public.inquiry_customer_events(submission_id, event_type, details, customer_visible)
  VALUES (p_submission_id, 'inbox_delivered',
          jsonb_build_object(
            'document_count', p_document_count,
            'routing_key', p_routing_key,
            'primary_estimator_code', v_primary),
          true);
END
$function$;

-- ---------------------------------------------------------------------------
-- 5. Privileges, restated so a re-run cannot widen them
-- ---------------------------------------------------------------------------

REVOKE ALL ON FUNCTION public.inquiry_capabilities_for_roles(text[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.inquiry_capabilities_for_roles(text[]) TO authenticated;

REVOKE ALL ON FUNCTION public.plan_customer_inquiry_deliveries_v2(integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.plan_customer_inquiry_deliveries_v2(integer) TO authenticated;

REVOKE ALL ON FUNCTION public.plan_customer_inquiry_delivery_documents(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.plan_customer_inquiry_delivery_documents(uuid) TO authenticated;

REVOKE ALL ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2(uuid, text, integer, text, jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2(uuid, text, integer, text, jsonb) TO authenticated;

COMMIT;

-- ---------------------------------------------------------------------------
-- Verification after apply (read-only)
-- ---------------------------------------------------------------------------
--
--   select p.proname, has_function_privilege('anon', p.oid, 'EXECUTE') as anon_exec,
--          has_function_privilege('authenticated', p.oid, 'EXECUTE') as auth_exec
--     from pg_proc p where p.pronamespace = 'public'::regnamespace
--      and p.proname in ('plan_customer_inquiry_deliveries_v2',
--                        'plan_customer_inquiry_delivery_documents',
--                        'acknowledge_customer_inquiry_delivery_v2');
--
--   -- as an admin (expect zero errors and the pending row count)
--   select count(*) from public.plan_customer_inquiry_deliveries_v2(25);
--
--   -- as an auditor, a viewer and an estimator (expect 42501 each)
--   select count(*) from public.plan_customer_inquiry_deliveries_v2(25);
--
--   -- the self-report must now list the two delivery capabilities
--   select capabilities from public.get_my_inquiry_access_v1();
--
-- The storage side needs no change: the existing SELECT policy
-- "Customers and operators download inquiry intake documents" already lets an
-- enabled admin read private objects for every complete submission.

-- ---------------------------------------------------------------------------
-- Rollback (run as a separate transaction)
-- ---------------------------------------------------------------------------
--
-- Re-run these files after the block below to restore the bridge-only gate and
-- the smaller capability list. No table, row, delivery or event is dropped.
--
--   website/supabase-inquiry-estimator-phase9e-handoff.sql   (plan and ack)
--   website/supabase-inquiry-intake-phase8c-inbox.sql        (documents plan)
--   website/supabase-inquiry-role-matrix-phase13.sql         (capabilities)

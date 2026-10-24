-- PHASE 13: INQUIRY ROLE MATRIX, SELF-ACCESS, AND INBOX SCOPE
--
-- Approved by the owner and applied on project afpbdyamedpanwijxghk on
-- 2026-10-24 in four ordered steps, because the file is too large for one
-- payload. See docs/PHASE13_INQUIRY_ROLE_MATRIX.md for the matrix, the evidence
-- behind it, the verification after apply, the apply order and the rollback.
--
--   sections 1 to 4  -> inquiry_role_matrix_phase13_access  (20261024080329)
--   sections 5 to 9  -> inquiry_role_matrix_phase13_scope   (20261024080348)
--   section 10       -> inquiry_role_matrix_phase13_audit   (20261024080355)
--   the whole file   -> inquiry_role_matrix_phase13_body_comments (20261024080424)
--
-- The body comments matter: md5(prosrc) only matches this file once they are
-- included in the function bodies.
--
-- Additive only, with four deliberate behaviour changes that were approved
-- before apply and are called out here so nobody has to discover them from a
-- diff:
--
--   1. Three intake SELECT policies are recreated without inquiry_viewer, and
--      authorize_customer_inquiry_document_download stops accepting that role.
--      The matrix gives inquiry_viewer no inbox access unless it is approved
--      separately. No live account held that role, so the blast radius was nil
--      on the day of apply.
--   2. update_customer_inquiry_status_v1 and the chat pull widen from
--      inquiry_bridge to bridge + admin + an actively assigned estimator. The
--      bridge branch is unchanged.
--   3. acknowledge_customer_inquiry_delivery_v2 is reordered so a retry of a
--      successful acknowledgement returns quietly instead of raising
--      "Document count changed." The signature and the stored columns are
--      unchanged.
--   4. A trigger now writes an audit row for every role grant, change and
--      revocation.
--
-- Everything else is new: one capabilities helper, one self-access RPC, one
-- read predicate, one scoped inbox listing, one audit table.
--
-- Kept exactly as it is: the submission status CHECK constraint (no desktop
-- state enters the customer vocabulary), every customer-facing read, the bridge
-- plan RPCs, and the global community room.

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Capabilities, derived from enabled roles only
-- ---------------------------------------------------------------------------

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
      ('inquiry_admin',     'inbox.read.all'),
      ('inquiry_admin',     'inbox.download'),
      ('inquiry_admin',     'inbox.review'),
      ('inquiry_admin',     'pipeline.write'),
      ('inquiry_admin',     'chat.read'),
      ('inquiry_admin',     'chat.write'),
      ('inquiry_admin',     'audit.read'),
      ('inquiry_admin',     'estimator.manage'),
      ('inquiry_auditor',   'inbox.read.all'),
      ('inquiry_auditor',   'inbox.download'),
      ('inquiry_auditor',   'chat.read'),
      ('inquiry_auditor',   'audit.read'),
      ('inquiry_estimator', 'inbox.read.assigned'),
      ('inquiry_estimator', 'inbox.download'),
      ('inquiry_estimator', 'pipeline.write'),
      ('inquiry_estimator', 'chat.read'),
      ('inquiry_estimator', 'chat.write'),
      ('inquiry_bridge',    'inbox.plan.bridge'),
      ('inquiry_bridge',    'inbox.download'),
      ('inquiry_bridge',    'inbox.acknowledge'),
      ('inquiry_bridge',    'pipeline.write'),
      ('inquiry_bridge',    'chat.read')
    ) AS mapping(role, capability)
    WHERE mapping.role = role_name
  ) matched;
$function$;

REVOKE ALL ON FUNCTION public.inquiry_capabilities_for_roles(text[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.inquiry_capabilities_for_roles(text[]) TO authenticated;

-- ---------------------------------------------------------------------------
-- 2. Self-service access report
--
-- Identity comes from auth.uid() and nowhere else. The function returns the
-- caller's own row and nothing about any other account, so no user directory
-- can be built from it. An unsigned caller gets authenticated = false with a
-- reason, which is what a desktop needs to tell "no session" apart from
-- "session but no role".
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.get_my_inquiry_access_v1()
RETURNS TABLE (
  authenticated boolean,
  user_id uuid,
  email text,
  display_name text,
  roles text[],
  capabilities text[],
  denial_reason text
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public, auth, pg_temp
AS $function$
DECLARE
  v_uid        uuid := auth.uid();
  v_roles      text[] := ARRAY[]::text[];
  v_capabilities text[];
  v_email      text;
  v_name       text;
BEGIN
  IF v_uid IS NULL THEN
    RETURN QUERY SELECT
      false,
      NULL::uuid,
      NULL::text,
      NULL::text,
      ARRAY[]::text[],
      ARRAY[]::text[],
      'No Supabase Auth session reached the database. Sign in and send that session access token with the request.'::text;
    RETURN;
  END IF;

  SELECT coalesce(array_agg(r.role ORDER BY r.role), ARRAY[]::text[])
    INTO v_roles
    FROM public.inquiry_operator_roles r
   WHERE r.user_id = v_uid AND r.enabled;

  SELECT u.email INTO v_email FROM auth.users u WHERE u.id = v_uid;
  SELECT coalesce(p.display_name, '') INTO v_name FROM public.user_profiles p WHERE p.user_id = v_uid;

  v_capabilities := public.inquiry_capabilities_for_roles(v_roles);

  RETURN QUERY SELECT
    true,
    v_uid,
    coalesce(v_email, ''),
    coalesce(v_name, ''),
    v_roles,
    v_capabilities,
    CASE
      WHEN cardinality(v_roles) = 0 THEN
        'Signed in as ' || coalesce(v_email, 'this account') ||
        ', but the account has no enabled inquiry role.'
      WHEN cardinality(v_capabilities) = 0 THEN
        'Signed in as ' || coalesce(v_email, 'this account') ||
        ', but the enabled inquiry role grants no inbox capability.'
      ELSE NULL
    END;
END
$function$;

REVOKE ALL ON FUNCTION public.get_my_inquiry_access_v1() FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_my_inquiry_access_v1() TO authenticated;

-- ---------------------------------------------------------------------------
-- 3. One read predicate, derived from server state
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.inquiry_can_read_submission(p_submission_id uuid)
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
  SELECT auth.uid() IS NOT NULL AND EXISTS (
    SELECT 1
      FROM public.inquiry_customer_submissions s
     WHERE s.id = p_submission_id
       AND s.upload_state = 'complete'
       AND (
         s.customer_user_id = auth.uid()
         OR public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor'])
         OR public.is_active_inquiry_estimator_assignment(s.id)
       )
  );
$function$;

REVOKE ALL ON FUNCTION public.inquiry_can_read_submission(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.inquiry_can_read_submission(uuid) TO authenticated;

-- ---------------------------------------------------------------------------
-- 4. Scoped inbox listing for people, not machines
--
-- This is the operator inbox. The bridge keeps its own plan RPCs untouched.
-- Scope is decided by the database from enabled roles and trusted assignment
-- rows; there is no scope parameter a caller could forge.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.list_customer_inquiry_inbox_v1(p_limit integer DEFAULT 100)
RETURNS TABLE (
  submission_id uuid,
  submission_reference text,
  submitted_at timestamptz,
  status text,
  contact_name text,
  company_name text,
  project_name text,
  project_location text,
  inquiry_type text,
  document_count bigint,
  primary_estimator_code text,
  assignment_snapshot jsonb,
  routing_key text,
  desktop_delivered boolean,
  delivered_at timestamptz,
  pipeline_status text,
  pipeline_note text,
  pipeline_updated_at timestamptz,
  inbox_scope text
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
DECLARE
  v_scope text;
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;

  IF public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']) THEN
    v_scope := 'all';
  ELSIF public.has_inquiry_role(ARRAY['inquiry_estimator']) THEN
    v_scope := 'assigned';
  ELSE
    RAISE EXCEPTION 'Inquiry inbox access is not enabled for this account.' USING ERRCODE = '42501';
  END IF;

  RETURN QUERY
    SELECT s.id,
           s.submission_reference,
           s.submitted_at,
           s.status,
           s.contact_name,
           s.company_name,
           s.project_name,
           s.project_location,
           s.inquiry_type,
           count(d.id)::bigint,
           p.estimator_code,
           public.customer_inquiry_assignment_snapshot(s.id),
           CASE WHEN p.estimator_code IS NULL THEN 'unassigned' ELSE 'assigned/' || p.estimator_code END,
           x.submission_id IS NOT NULL,
           x.acknowledged_at,
           s.pipeline_status,
           s.pipeline_note,
           s.pipeline_updated_at,
           v_scope
      FROM public.inquiry_customer_submissions s
      LEFT JOIN public.inquiry_customer_documents d
             ON d.submission_id = s.id AND d.availability = 'available'
      LEFT JOIN public.inquiry_customer_deliveries x
             ON x.submission_id = s.id
      LEFT JOIN LATERAL (
        SELECT e.estimator_code
          FROM public.inquiry_estimator_assignments a
          JOIN public.inquiry_estimators e ON e.user_id = a.estimator_user_id
         WHERE a.submission_id = s.id
           AND a.active
           AND a.assignment_role = 'primary'
           AND e.active
         LIMIT 1
      ) p ON true
     WHERE s.upload_state = 'complete'
       AND (v_scope = 'all' OR public.is_active_inquiry_estimator_assignment(s.id))
     GROUP BY s.id, p.estimator_code, x.submission_id, x.acknowledged_at
     ORDER BY s.submitted_at DESC, s.id
     LIMIT greatest(1, least(coalesce(p_limit, 100), 200));
END
$function$;

REVOKE ALL ON FUNCTION public.list_customer_inquiry_inbox_v1(integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.list_customer_inquiry_inbox_v1(integer) TO authenticated;

-- ---------------------------------------------------------------------------
-- 5. inquiry_viewer leaves the intake surfaces
--
-- The matrix gives a viewer no inbox access unless it is approved separately.
-- The customer branch of every policy is untouched, so a customer still reads
-- their own submission and their own documents exactly as before.
-- ---------------------------------------------------------------------------

DROP POLICY IF EXISTS "Customers read own inquiry submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Customers read own inquiry submissions" ON public.inquiry_customer_submissions
  FOR SELECT TO authenticated USING (
    customer_user_id = auth.uid()
    OR (upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_auditor', 'inquiry_admin']))
  );

DROP POLICY IF EXISTS "Customers read own inquiry documents" ON public.inquiry_customer_documents;
CREATE POLICY "Customers read own inquiry documents" ON public.inquiry_customer_documents
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s
     WHERE s.id = submission_id
       AND (s.customer_user_id = auth.uid()
         OR (s.upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_auditor', 'inquiry_admin'])))
  ));

DROP POLICY IF EXISTS "Customers and operators download inquiry intake documents" ON storage.objects;
CREATE POLICY "Customers and operators download inquiry intake documents" ON storage.objects
  FOR SELECT TO authenticated USING (bucket_id = 'inquiry-intake-documents' AND EXISTS (
    SELECT 1 FROM public.inquiry_customer_documents d
      JOIN public.inquiry_customer_submissions s ON s.id = d.submission_id
     WHERE d.storage_object_key = name
       AND (s.customer_user_id = auth.uid()
         OR (s.upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_auditor', 'inquiry_admin'])))
  ));

-- The bridge and estimator policies from Phase 8C and 9A/9B are left exactly as
-- they are. The customer-visible event policy is untouched.

-- ---------------------------------------------------------------------------
-- 6. Download authorization without the viewer branch
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.authorize_customer_inquiry_document_download(
  p_submission_id uuid,
  p_document_id uuid
)
RETURNS TABLE (
  storage_bucket text,
  storage_object_key text,
  original_filename text,
  detected_mime text,
  byte_size bigint,
  content_sha256 text
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
DECLARE
  v_submission public.inquiry_customer_submissions%ROWTYPE;
  v_document   public.inquiry_customer_documents%ROWTYPE;
  v_operator   boolean;
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;
  SELECT * INTO v_submission FROM public.inquiry_customer_submissions WHERE id = p_submission_id;
  IF v_submission.id IS NULL THEN
    RAISE EXCEPTION 'Document not found.' USING ERRCODE = 'P0002';
  END IF;
  v_operator := public.has_inquiry_role(ARRAY['inquiry_auditor', 'inquiry_admin'])
             OR public.is_active_inquiry_estimator_assignment(p_submission_id);
  -- An operator only ever sees a finished handoff; a customer may still fetch
  -- their own files while the submission is a draft.
  IF v_submission.customer_user_id <> auth.uid()
     AND NOT (v_operator AND v_submission.upload_state = 'complete') THEN
    RAISE EXCEPTION 'Document not found.' USING ERRCODE = 'P0002';
  END IF;
  SELECT * INTO v_document
    FROM public.inquiry_customer_documents
   WHERE id = p_document_id
     AND submission_id = p_submission_id
     AND availability = 'available';
  IF v_document.id IS NULL THEN
    RAISE EXCEPTION 'Document not found.' USING ERRCODE = 'P0002';
  END IF;
  INSERT INTO public.inquiry_customer_events(submission_id, document_id, event_type, customer_visible)
  VALUES (p_submission_id, p_document_id, 'download_authorized', false);
  RETURN QUERY SELECT v_document.storage_bucket,
                      v_document.storage_object_key,
                      v_document.original_filename,
                      v_document.detected_mime,
                      v_document.byte_size,
                      v_document.content_sha256;
END
$function$;

REVOKE ALL ON FUNCTION public.authorize_customer_inquiry_document_download(uuid, uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.authorize_customer_inquiry_document_download(uuid, uuid) TO authenticated;

-- ---------------------------------------------------------------------------
-- 7. Pipeline write-back: bridge as before, plus admin and an assigned
--    estimator. The bridge branch keeps the same behaviour and the same row
--    shape, and pipeline_updated_by still records auth.uid() for attribution.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.update_customer_inquiry_status_v1(
  p_submission_id uuid,
  p_status text,
  p_note text
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
DECLARE
  v_old text;
  v_allowed boolean;
BEGIN
  v_allowed := public.has_inquiry_role(ARRAY['inquiry_bridge', 'inquiry_admin'])
            OR public.is_active_inquiry_estimator_assignment(p_submission_id);
  IF NOT v_allowed THEN
    RAISE EXCEPTION 'Inquiry bridge role, inquiry admin role, or an active assignment is required.'
      USING ERRCODE = '42501';
  END IF;
  IF p_status NOT IN ('prepared', 'sent', 'confirmed', 'hold', 'cancelled') THEN
    RAISE EXCEPTION 'Unknown pipeline status: %', p_status USING ERRCODE = '22023';
  END IF;
  SELECT pipeline_status INTO v_old
    FROM public.inquiry_customer_submissions
   WHERE id = p_submission_id FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'Submission not found.' USING ERRCODE = 'P0002';
  END IF;
  UPDATE public.inquiry_customer_submissions
     SET pipeline_status     = p_status,
         pipeline_note       = coalesce(p_note, ''),
         pipeline_updated_at = now(),
         pipeline_updated_by = auth.uid()::text
   WHERE id = p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id, event_type, details, customer_visible)
  VALUES (p_submission_id, 'status_changed',
          jsonb_build_object(
            'source', CASE WHEN public.has_inquiry_role(ARRAY['inquiry_bridge'])
                           THEN 'desktop_inbox' ELSE 'website_operator' END,
            'from', v_old,
            'to', p_status,
            'note', coalesce(p_note, '')),
          false);
END
$function$;

REVOKE ALL ON FUNCTION public.update_customer_inquiry_status_v1(uuid, text, text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.update_customer_inquiry_status_v1(uuid, text, text) TO authenticated;

-- ---------------------------------------------------------------------------
-- 8. Chat pull: same columns, same read-only promise, wider caller set
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.list_customer_inquiry_communications_v1(p_submission_id uuid)
RETURNS TABLE (
  id uuid,
  visibility text,
  message_kind text,
  request_status text,
  body text,
  author_display_name text,
  created_at timestamptz
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
BEGIN
  IF NOT (
    public.has_inquiry_role(ARRAY['inquiry_bridge', 'inquiry_admin', 'inquiry_auditor'])
    OR public.is_active_inquiry_estimator_assignment(p_submission_id)
  ) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY
    SELECT c.id, c.visibility, c.message_kind, c.request_status, c.body,
           coalesce(p.display_name, 'Website user'), c.created_at
      FROM public.inquiry_communications c
      LEFT JOIN public.user_profiles p ON p.user_id = c.author_user_id
     WHERE c.submission_id = p_submission_id
     ORDER BY c.created_at, c.id;
END
$function$;

REVOKE ALL ON FUNCTION public.list_customer_inquiry_communications_v1(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.list_customer_inquiry_communications_v1(uuid) TO authenticated;

-- ---------------------------------------------------------------------------
-- 9. Acknowledgement retry
--
-- Before this change an identical retry raised "Document count changed." from
-- the expected-count query, because the first acknowledgement had already moved
-- the submission out of status 'accepted'. The ledger check now runs first, so
-- a retry of the same manifest, count, routing key and snapshot returns quietly
-- and anything else still fails closed. Signature and stored columns unchanged.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery_v2(
  p_submission_id uuid,
  p_manifest_sha256 text,
  p_document_count integer,
  p_routing_key text,
  p_assignment_snapshot jsonb
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
DECLARE
  v_expected integer;
  v_current  jsonb;
  v_primary  text;
  v_route    text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE = '42501';
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

REVOKE ALL ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2(uuid, text, integer, text, jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2(uuid, text, integer, text, jsonb) TO authenticated;

-- ---------------------------------------------------------------------------
-- 10. Role grant and revocation audit
--
-- Role rows are managed from the dashboard rather than an RPC, so the audit has
-- to sit on the table. The trigger records the change, the account it affects,
-- the actor when there is a session, and the SQL role otherwise, so a dashboard
-- edit is still attributable to whoever had dashboard access.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.inquiry_operator_role_audit (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id uuid NOT NULL,
  role text NOT NULL,
  enabled boolean NOT NULL,
  action text NOT NULL CHECK (action IN ('granted', 'updated', 'revoked')),
  actor_user_id uuid,
  actor_role text NOT NULL DEFAULT current_user,
  changed_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_inquiry_operator_role_audit_user
  ON public.inquiry_operator_role_audit(user_id, changed_at DESC);

ALTER TABLE public.inquiry_operator_role_audit ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.inquiry_operator_role_audit FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.inquiry_operator_role_audit TO authenticated;

DROP POLICY IF EXISTS "Admins and auditors read role audit" ON public.inquiry_operator_role_audit;
CREATE POLICY "Admins and auditors read role audit" ON public.inquiry_operator_role_audit
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

CREATE OR REPLACE FUNCTION public.log_inquiry_operator_role_change()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $function$
BEGIN
  IF TG_OP = 'DELETE' THEN
    INSERT INTO public.inquiry_operator_role_audit(user_id, role, enabled, action, actor_user_id, actor_role)
    VALUES (OLD.user_id, OLD.role, OLD.enabled, 'revoked', auth.uid(), current_user);
    RETURN OLD;
  END IF;
  INSERT INTO public.inquiry_operator_role_audit(user_id, role, enabled, action, actor_user_id, actor_role)
  VALUES (
    NEW.user_id,
    NEW.role,
    NEW.enabled,
    CASE
      WHEN TG_OP = 'INSERT' THEN 'granted'
      WHEN OLD.enabled AND NOT NEW.enabled THEN 'revoked'
      ELSE 'updated'
    END,
    auth.uid(),
    current_user);
  RETURN NEW;
END
$function$;

REVOKE ALL ON FUNCTION public.log_inquiry_operator_role_change() FROM PUBLIC, anon, authenticated;

DROP TRIGGER IF EXISTS trg_inquiry_operator_role_audit ON public.inquiry_operator_roles;
CREATE TRIGGER trg_inquiry_operator_role_audit
  AFTER INSERT OR UPDATE OR DELETE ON public.inquiry_operator_roles
  FOR EACH ROW EXECUTE FUNCTION public.log_inquiry_operator_role_change();

NOTIFY pgrst, 'reload schema';
COMMIT;

-- ---------------------------------------------------------------------------
-- Rollback (run as a separate transaction, in this order)
--
-- DROP TRIGGER IF EXISTS trg_inquiry_operator_role_audit ON public.inquiry_operator_roles;
-- DROP FUNCTION IF EXISTS public.log_inquiry_operator_role_change();
-- DROP TABLE IF EXISTS public.inquiry_operator_role_audit;
-- DROP FUNCTION IF EXISTS public.list_customer_inquiry_inbox_v1(integer);
-- DROP FUNCTION IF EXISTS public.get_my_inquiry_access_v1();
-- DROP FUNCTION IF EXISTS public.inquiry_can_read_submission(uuid);
-- DROP FUNCTION IF EXISTS public.inquiry_capabilities_for_roles(text[]);
-- then re-run supabase-inquiry-intake-phase8a-staged-uploads.sql,
-- supabase-inquiry-estimator-phase9b-access.sql and
-- supabase-inquiry-inbox-bridge-phase12.sql to restore the earlier function
-- bodies and the three intake policies. No data is destroyed by this rollback:
-- every table in this migration is either new or untouched.
-- ---------------------------------------------------------------------------

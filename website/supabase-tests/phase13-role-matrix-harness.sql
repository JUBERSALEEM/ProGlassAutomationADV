-- PHASE 13 ROLE MATRIX: SCRATCH-DATABASE HARNESS
--
-- Runs only against a throwaway Postgres database, never against a project.
-- This file rebuilds the parts of the live intake contract that Phase 13
-- touches. Run the three files in this order:
--
--   psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase13-role-matrix-harness.sql
--   psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-inquiry-role-matrix-phase13.sql
--   psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase13-role-matrix-assertions.sql
--
-- The fixtures cover two customers in different companies, one accepted and
-- assigned submission, one submitted and unassigned submission, one draft, one
-- estimator with an assignment and one without, an admin, an auditor, a viewer,
-- an enabled bridge and a disabled bridge.

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF; END $$;
GRANT USAGE ON SCHEMA public TO anon, authenticated;

CREATE SCHEMA auth;
GRANT USAGE ON SCHEMA auth TO authenticated;
CREATE TABLE auth.users (id uuid PRIMARY KEY, email text);
CREATE OR REPLACE FUNCTION auth.uid() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT nullif(nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'sub', '')::uuid;
$$;
GRANT EXECUTE ON FUNCTION auth.uid() TO authenticated;

CREATE SCHEMA storage;
GRANT USAGE ON SCHEMA storage TO authenticated;
CREATE TABLE storage.objects (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bucket_id text, name text);
ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON storage.objects TO authenticated;

CREATE TABLE public.user_profiles (user_id uuid PRIMARY KEY, display_name text NOT NULL DEFAULT '');

CREATE TABLE public.inquiry_operator_roles (
  user_id uuid PRIMARY KEY,
  role text NOT NULL,
  enabled boolean NOT NULL DEFAULT true,
  assigned_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE public.inquiry_operator_roles ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON public.inquiry_operator_roles TO authenticated;

CREATE OR REPLACE FUNCTION public.has_inquiry_role(p_roles text[])
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
  SELECT auth.uid() IS NOT NULL AND EXISTS (
    SELECT 1 FROM public.inquiry_operator_roles r
     WHERE r.user_id = auth.uid() AND r.enabled AND r.role = ANY(p_roles));
$$;
REVOKE ALL ON FUNCTION public.has_inquiry_role(text[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.has_inquiry_role(text[]) TO authenticated;

CREATE TABLE public.inquiry_customer_submissions (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  submission_reference text NOT NULL UNIQUE,
  customer_user_id uuid NOT NULL,
  contact_name text NOT NULL DEFAULT 'Contact',
  company_name text NOT NULL DEFAULT 'Company',
  project_name text NOT NULL DEFAULT 'Project',
  project_location text NOT NULL DEFAULT '',
  phone text NOT NULL DEFAULT '',
  customer_reference text NOT NULL DEFAULT '',
  attention_name text NOT NULL DEFAULT '',
  vat_trn_number text NOT NULL DEFAULT '',
  salesman_reference text NOT NULL DEFAULT '',
  handover_estimator_name text NOT NULL DEFAULT '',
  inquiry_type text NOT NULL DEFAULT 'quotation',
  description text NOT NULL DEFAULT 'A description of the work.',
  status text NOT NULL DEFAULT 'submitted'
    CHECK (status IN ('submitted','under_review','accepted','rejected','downloaded','closed')),
  submitted_at timestamptz NOT NULL DEFAULT now(),
  status_updated_at timestamptz NOT NULL DEFAULT now(),
  downloaded_at timestamptz,
  upload_state text NOT NULL DEFAULT 'complete' CHECK (upload_state IN ('draft','complete','failed')),
  expected_document_count integer NOT NULL DEFAULT 0,
  finalized_at timestamptz,
  pipeline_status text,
  pipeline_note text,
  pipeline_updated_at timestamptz,
  pipeline_updated_by text
);
ALTER TABLE public.inquiry_customer_submissions ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON public.inquiry_customer_submissions TO authenticated;

CREATE TABLE public.inquiry_customer_documents (
  id uuid PRIMARY KEY,
  submission_id uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id),
  original_filename text NOT NULL,
  detected_mime text NOT NULL DEFAULT 'application/pdf',
  byte_size bigint NOT NULL DEFAULT 10,
  content_sha256 text NOT NULL,
  storage_bucket text NOT NULL DEFAULT 'inquiry-intake-documents',
  storage_object_key text NOT NULL UNIQUE,
  availability text NOT NULL DEFAULT 'available' CHECK (availability IN ('available','quarantined')),
  uploaded_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE public.inquiry_customer_documents ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON public.inquiry_customer_documents TO authenticated;

CREATE TABLE public.inquiry_customer_deliveries (
  submission_id uuid PRIMARY KEY REFERENCES public.inquiry_customer_submissions(id),
  bridge_user_id uuid NOT NULL,
  manifest_sha256 text NOT NULL,
  document_count integer NOT NULL,
  acknowledged_at timestamptz NOT NULL DEFAULT now(),
  routing_key text,
  primary_estimator_code text,
  assignment_snapshot jsonb
);
ALTER TABLE public.inquiry_customer_deliveries ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.inquiry_customer_deliveries FROM PUBLIC, anon, authenticated;

CREATE TABLE public.inquiry_customer_events (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  submission_id uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id),
  document_id uuid,
  actor_id uuid DEFAULT auth.uid(),
  event_type text NOT NULL,
  details jsonb NOT NULL DEFAULT '{}'::jsonb,
  customer_visible boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE public.inquiry_customer_events ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON public.inquiry_customer_events TO authenticated;

CREATE TABLE public.inquiry_communications (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  submission_id uuid NOT NULL,
  author_user_id uuid NOT NULL,
  visibility text NOT NULL DEFAULT 'customer',
  message_kind text NOT NULL DEFAULT 'message',
  request_status text,
  body text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE public.inquiry_communications ENABLE ROW LEVEL SECURITY;

CREATE TABLE public.inquiry_estimators (
  user_id uuid PRIMARY KEY,
  estimator_code text NOT NULL UNIQUE,
  display_name text NOT NULL,
  active boolean NOT NULL DEFAULT true
);
CREATE TABLE public.inquiry_estimator_assignments (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  submission_id uuid NOT NULL,
  estimator_user_id uuid NOT NULL,
  assignment_role text NOT NULL CHECK (assignment_role IN ('primary','collaborator')),
  active boolean NOT NULL DEFAULT true,
  assigned_at timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.is_active_inquiry_estimator_assignment(p_submission_id uuid, p_user_id uuid DEFAULT auth.uid())
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
  SELECT p_user_id IS NOT NULL AND public.has_inquiry_role(ARRAY['inquiry_estimator']) AND EXISTS (
    SELECT 1 FROM public.inquiry_estimator_assignments a
    JOIN public.inquiry_estimators e ON e.user_id = a.estimator_user_id
    WHERE a.submission_id = p_submission_id AND a.estimator_user_id = p_user_id AND a.active AND e.active);
$$;
GRANT EXECUTE ON FUNCTION public.is_active_inquiry_estimator_assignment(uuid,uuid) TO authenticated;
ALTER TABLE public.inquiry_estimators ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_estimator_assignments ENABLE ROW LEVEL SECURITY;
GRANT SELECT ON public.inquiry_estimators, public.inquiry_estimator_assignments TO authenticated;
CREATE POLICY "Approved operators read estimator directory" ON public.inquiry_estimators FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor','inquiry_estimator']));
CREATE POLICY "Admins auditors and assigned estimators read assignments" ON public.inquiry_estimator_assignments FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor']) OR public.is_active_inquiry_estimator_assignment(submission_id));

CREATE OR REPLACE FUNCTION public.customer_inquiry_assignment_snapshot(p_submission_id uuid)
RETURNS jsonb LANGUAGE sql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
  SELECT coalesce(jsonb_agg(jsonb_build_object(
    'role',a.assignment_role,'estimatorCode',e.estimator_code,'displayName',e.display_name,'assignedAt',a.assigned_at
  ) ORDER BY CASE a.assignment_role WHEN 'primary' THEN 0 ELSE 1 END, e.estimator_code),'[]'::jsonb)
  FROM public.inquiry_estimator_assignments a
  JOIN public.inquiry_estimators e ON e.user_id = a.estimator_user_id
  WHERE a.submission_id = p_submission_id AND a.active AND e.active;
$$;
REVOKE ALL ON FUNCTION public.customer_inquiry_assignment_snapshot(uuid) FROM PUBLIC, anon, authenticated;

-- Current intake policies, as they exist live today (before Phase 13).
CREATE POLICY "Customers read own inquiry submissions" ON public.inquiry_customer_submissions
  FOR SELECT TO authenticated USING (
    customer_user_id = auth.uid()
    OR (upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])));
CREATE POLICY "Inquiry bridge reads accepted submissions" ON public.inquiry_customer_submissions
  FOR SELECT TO authenticated USING (
    upload_state = 'complete' AND status = 'accepted' AND public.has_inquiry_role(ARRAY['inquiry_bridge']));
CREATE POLICY "Assigned estimators read complete submissions" ON public.inquiry_customer_submissions
  FOR SELECT TO authenticated USING (
    upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_estimator'])
    AND EXISTS (SELECT 1 FROM public.inquiry_estimators e JOIN public.inquiry_estimator_assignments a ON a.estimator_user_id = e.user_id
      WHERE a.submission_id = public.inquiry_customer_submissions.id AND a.estimator_user_id = auth.uid() AND a.active AND e.active));

CREATE POLICY "Customers read own inquiry documents" ON public.inquiry_customer_documents
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id = submission_id
      AND (s.customer_user_id = auth.uid()
        OR (s.upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])))));
CREATE POLICY "Inquiry bridge reads accepted documents" ON public.inquiry_customer_documents
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_bridge']) AND EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id = submission_id AND s.upload_state='complete' AND s.status='accepted'));
CREATE POLICY "Assigned estimators read submission documents" ON public.inquiry_customer_documents
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_estimator']) AND EXISTS (
    SELECT 1 FROM public.inquiry_estimators e JOIN public.inquiry_estimator_assignments a ON a.estimator_user_id = e.user_id
    WHERE a.submission_id = inquiry_customer_documents.submission_id AND a.estimator_user_id = auth.uid() AND a.active AND e.active));

CREATE POLICY "Customers read visible own inquiry events" ON public.inquiry_customer_events
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id = submission_id
      AND ((s.customer_user_id = auth.uid() AND customer_visible)
        OR (s.upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_auditor','inquiry_admin'])))));

CREATE POLICY "Customers and operators download inquiry intake documents" ON storage.objects
  FOR SELECT TO authenticated USING (bucket_id = 'inquiry-intake-documents' AND EXISTS (
    SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id = d.submission_id
    WHERE d.storage_object_key = name
      AND (s.customer_user_id = auth.uid()
        OR (s.upload_state = 'complete' AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])))));

CREATE POLICY "Operators read own inquiry role" ON public.inquiry_operator_roles
  FOR SELECT TO authenticated USING (user_id = auth.uid() OR public.has_inquiry_role(ARRAY['inquiry_admin']));

-- Current download authorization, before Phase 13.
CREATE OR REPLACE FUNCTION public.authorize_customer_inquiry_document_download(p_submission_id uuid, p_document_id uuid)
RETURNS TABLE (storage_bucket text, storage_object_key text, original_filename text, detected_mime text, byte_size bigint, content_sha256 text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_submission public.inquiry_customer_submissions%ROWTYPE; v_document public.inquiry_customer_documents%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  SELECT * INTO v_submission FROM public.inquiry_customer_submissions WHERE id = p_submission_id;
  IF v_submission.id IS NULL OR NOT (v_submission.customer_user_id = auth.uid()
    OR public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']))
  THEN RAISE EXCEPTION 'Document not found.' USING ERRCODE='P0002'; END IF;
  SELECT * INTO v_document FROM public.inquiry_customer_documents
    WHERE id = p_document_id AND submission_id = p_submission_id AND availability='available';
  IF v_document.id IS NULL THEN RAISE EXCEPTION 'Document not found.' USING ERRCODE='P0002'; END IF;
  INSERT INTO public.inquiry_customer_events(submission_id, document_id, event_type, customer_visible)
    VALUES (p_submission_id, p_document_id, 'download_authorized', false);
  RETURN QUERY SELECT v_document.storage_bucket, v_document.storage_object_key, v_document.original_filename,
    v_document.detected_mime, v_document.byte_size, v_document.content_sha256;
END $$;
REVOKE ALL ON FUNCTION public.authorize_customer_inquiry_document_download(uuid,uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.authorize_customer_inquiry_document_download(uuid,uuid) TO authenticated;

-- Current acknowledgement (the version with the retry bug).
CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery_v2(
  p_submission_id uuid, p_manifest_sha256 text, p_document_count integer, p_routing_key text, p_assignment_snapshot jsonb)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_expected integer; v_current jsonb; v_primary text; v_route text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  IF p_manifest_sha256 !~ '^[0-9a-f]{64}$' OR p_document_count NOT BETWEEN 0 AND 15 OR jsonb_typeof(p_assignment_snapshot) <> 'array'
  THEN RAISE EXCEPTION 'Invalid acknowledgement.' USING ERRCODE='22023'; END IF;
  SELECT count(*) INTO v_expected FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
    WHERE s.id=p_submission_id AND s.upload_state='complete' AND s.status='accepted' AND d.availability='available';
  IF v_expected <> p_document_count THEN RAISE EXCEPTION 'Document count changed.' USING ERRCODE='40001'; END IF;
  v_current := public.customer_inquiry_assignment_snapshot(p_submission_id);
  IF v_current <> p_assignment_snapshot THEN RAISE EXCEPTION 'Estimator assignment changed.' USING ERRCODE='40001'; END IF;
  SELECT value->>'estimatorCode' INTO v_primary FROM jsonb_array_elements(v_current) value WHERE value->>'role'='primary' LIMIT 1;
  v_route := CASE WHEN v_primary IS NULL THEN 'unassigned' ELSE 'assigned/'||v_primary END;
  IF p_routing_key IS DISTINCT FROM v_route THEN RAISE EXCEPTION 'Invalid routing key.' USING ERRCODE='22023'; END IF;
  IF EXISTS (SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id=p_submission_id) THEN
    IF EXISTS (SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id=p_submission_id
      AND manifest_sha256=p_manifest_sha256 AND document_count=p_document_count AND routing_key=p_routing_key AND assignment_snapshot=p_assignment_snapshot)
    THEN RETURN; END IF;
    RAISE EXCEPTION 'Conflicting acknowledgement.' USING ERRCODE='23505';
  END IF;
  INSERT INTO public.inquiry_customer_deliveries(submission_id, bridge_user_id, manifest_sha256, document_count, routing_key, primary_estimator_code, assignment_snapshot)
    VALUES (p_submission_id, auth.uid(), p_manifest_sha256, p_document_count, p_routing_key, v_primary, p_assignment_snapshot);
  UPDATE public.inquiry_customer_submissions SET status='downloaded', downloaded_at=coalesce(downloaded_at,now()), status_updated_at=now()
    WHERE id=p_submission_id AND status='accepted';
  INSERT INTO public.inquiry_customer_events(submission_id, event_type, details, customer_visible)
    VALUES (p_submission_id, 'inbox_delivered', jsonb_build_object('document_count',p_document_count,'routing_key',p_routing_key,'primary_estimator_code',v_primary), true);
END $$;
REVOKE ALL ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2(uuid,text,integer,text,jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2(uuid,text,integer,text,jsonb) TO authenticated;

CREATE OR REPLACE FUNCTION public.update_customer_inquiry_status_v1(p_submission_id uuid, p_status text, p_note text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_old text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  IF p_status NOT IN ('prepared','sent','confirmed','hold','cancelled') THEN RAISE EXCEPTION 'Unknown pipeline status: %', p_status USING ERRCODE='22023'; END IF;
  SELECT pipeline_status INTO v_old FROM public.inquiry_customer_submissions WHERE id=p_submission_id FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Submission not found.' USING ERRCODE='P0002'; END IF;
  UPDATE public.inquiry_customer_submissions SET pipeline_status=p_status, pipeline_note=coalesce(p_note,''),
    pipeline_updated_at=now(), pipeline_updated_by=auth.uid()::text WHERE id=p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id, event_type, details, customer_visible)
    VALUES (p_submission_id,'status_changed', jsonb_build_object('source','desktop_inbox','from',v_old,'to',p_status,'note',coalesce(p_note,'')), false);
END $$;
GRANT EXECUTE ON FUNCTION public.update_customer_inquiry_status_v1(uuid,text,text) TO authenticated;

CREATE OR REPLACE FUNCTION public.list_customer_inquiry_communications_v1(p_submission_id uuid)
RETURNS TABLE (id uuid, visibility text, message_kind text, request_status text, body text, author_display_name text, created_at timestamptz)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  RETURN QUERY SELECT c.id,c.visibility,c.message_kind,c.request_status,c.body,coalesce(p.display_name,'Website user'),c.created_at
    FROM public.inquiry_communications c LEFT JOIN public.user_profiles p ON p.user_id=c.author_user_id
    WHERE c.submission_id=p_submission_id ORDER BY c.created_at,c.id;
END $$;
GRANT EXECUTE ON FUNCTION public.list_customer_inquiry_communications_v1(uuid) TO authenticated;

-- The bridge plan RPC, as it exists live today (bridge-only, undelivered only,
-- full 20-column row shape after Phase 13).
CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_deliveries_v2(p_limit integer DEFAULT 25)
RETURNS TABLE (submission_id uuid, submission_reference text, submitted_at timestamptz, status text,
  contact_name text, company_name text, phone text, customer_reference text, attention_name text,
  vat_trn_number text, salesman_reference text, handover_estimator_name text, project_name text,
  project_location text, inquiry_type text, description text, document_count bigint,
  primary_estimator_code text, assignment_snapshot jsonb, routing_key text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
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
END $$;
REVOKE ALL ON FUNCTION public.plan_customer_inquiry_deliveries_v2(integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.plan_customer_inquiry_deliveries_v2(integer) TO authenticated;

-- Fixtures: two customers, two submissions in different companies, one
-- assigned to the estimator, one unassigned, plus one draft.
INSERT INTO auth.users(id,email) VALUES
 ('11111111-1111-4111-8111-111111111111','customer.a@example.test'),
 ('22222222-2222-4222-8222-222222222222','customer.b@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000001','bridge@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000002','admin@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000003','auditor@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000004','estimator@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000005','other.estimator@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000006','viewer@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000007','norole@example.test'),
 ('aaaaaaaa-0000-4000-8000-000000000008','disabled@example.test');

INSERT INTO public.user_profiles(user_id,display_name) VALUES
 ('aaaaaaaa-0000-4000-8000-000000000004','Assigned Estimator'),
 ('aaaaaaaa-0000-4000-8000-000000000005','Other Estimator'),
 ('aaaaaaaa-0000-4000-8000-000000000002','Inquiry Admin'),
 ('aaaaaaaa-0000-4000-8000-000000000006','Read Only Viewer');

INSERT INTO public.inquiry_operator_roles(user_id,role,enabled) VALUES
 ('aaaaaaaa-0000-4000-8000-000000000001','inquiry_bridge',true),
 ('aaaaaaaa-0000-4000-8000-000000000002','inquiry_admin',true),
 ('aaaaaaaa-0000-4000-8000-000000000003','inquiry_auditor',true),
 ('aaaaaaaa-0000-4000-8000-000000000004','inquiry_estimator',true),
 ('aaaaaaaa-0000-4000-8000-000000000005','inquiry_estimator',true),
 ('aaaaaaaa-0000-4000-8000-000000000006','inquiry_viewer',true),
 ('aaaaaaaa-0000-4000-8000-000000000008','inquiry_bridge',false);

INSERT INTO public.inquiry_estimators(user_id,estimator_code,display_name,active) VALUES
 ('aaaaaaaa-0000-4000-8000-000000000004','EST-A','Assigned Estimator',true),
 ('aaaaaaaa-0000-4000-8000-000000000005','EST-B','Other Estimator',true);

INSERT INTO public.inquiry_customer_submissions(id,submission_reference,customer_user_id,company_name,project_name,status,upload_state,submitted_at) VALUES
 ('ffffffff-0000-4000-8000-000000000001','WEB-2026-000101','11111111-1111-4111-8111-111111111111','Alpha Glass','Alpha Tower','accepted','complete', now() - interval '3 days'),
 ('ffffffff-0000-4000-8000-000000000002','WEB-2026-000102','22222222-2222-4222-8222-222222222222','Beta Interiors','Beta Mall','submitted','complete', now() - interval '2 days'),
 ('ffffffff-0000-4000-8000-000000000003','WEB-2026-000103','11111111-1111-4111-8111-111111111111','Alpha Glass','Alpha Annexe','submitted','draft', now() - interval '1 day');

INSERT INTO public.inquiry_estimator_assignments(submission_id,estimator_user_id,assignment_role,active) VALUES
 ('ffffffff-0000-4000-8000-000000000001','aaaaaaaa-0000-4000-8000-000000000004','primary',true);

INSERT INTO public.inquiry_customer_documents(id,submission_id,original_filename,content_sha256,storage_object_key) VALUES
 ('dddddddd-0000-4000-8000-000000000001','ffffffff-0000-4000-8000-000000000001','alpha.pdf', repeat('a',64), '11111111-1111-4111-8111-111111111111/ffffffff-0000-4000-8000-000000000001/dddddddd-0000-4000-8000-000000000001'),
 ('dddddddd-0000-4000-8000-000000000002','ffffffff-0000-4000-8000-000000000002','beta.pdf',  repeat('b',64), '22222222-2222-4222-8222-222222222222/ffffffff-0000-4000-8000-000000000002/dddddddd-0000-4000-8000-000000000002');

INSERT INTO public.inquiry_communications(submission_id,author_user_id,visibility,body) VALUES
 ('ffffffff-0000-4000-8000-000000000001','11111111-1111-4111-8111-111111111111','customer','Please review the glass schedule.');

INSERT INTO storage.objects(bucket_id,name) VALUES
 ('inquiry-intake-documents','11111111-1111-4111-8111-111111111111/ffffffff-0000-4000-8000-000000000001/dddddddd-0000-4000-8000-000000000001'),
 ('inquiry-intake-documents','22222222-2222-4222-8222-222222222222/ffffffff-0000-4000-8000-000000000002/dddddddd-0000-4000-8000-000000000002');

-- Harness fixture: the assignment snapshot the bridge is expected to echo back,
-- captured as the owner because the helper is not granted to authenticated.
CREATE TABLE public.zz_test_snapshot (submission_id uuid PRIMARY KEY, snap jsonb NOT NULL);
GRANT SELECT ON public.zz_test_snapshot TO authenticated;
INSERT INTO public.zz_test_snapshot(submission_id, snap)
SELECT id, public.customer_inquiry_assignment_snapshot(id) FROM public.inquiry_customer_submissions;

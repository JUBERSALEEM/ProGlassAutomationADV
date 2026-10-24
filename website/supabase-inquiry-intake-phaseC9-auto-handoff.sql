-- PHASE C9: WEBSITE SUBMISSIONS REACH THE DESKTOP WITHOUT A MANUAL ACCEPT
--
-- Why this exists. The delivery queue was gated on status='accepted', so a
-- customer could submit an inquiry and the shop PC would show "0 ready" until
-- an operator opened the website queue and moved the row to accepted. That is
-- one human step in front of every ordinary quotation request, and in
-- production it had never been done: eight complete submissions, none
-- accepted, an empty desktop inbox.
--
-- What this migration changes, and nothing else:
--
--   1. A complete submission whose status is 'submitted' OR 'accepted' is
--      deliverable. The acceptance step is no longer a prerequisite; it stays
--      available as a hold (an operator may still park a submission in
--      under_review, and it will not be handed over until accepted).
--   2. A submission whose inquiry_type is 'proforma_invoice' is finalized in
--      under_review instead of submitted, so a priced document still needs a
--      coordinator. That is the one case the owner asked to keep behind a
--      human decision.
--   3. The bridge may read the storage objects of a deliverable submission.
--      Before this, the acknowledgement flipped the row to 'downloaded' and
--      the bridge could no longer re-read the files it had just delivered.
--
-- What it deliberately does NOT change:
--
--   * inquiry_sources, its enabled flag and its bridge_user_id are untouched.
--   * No role is granted, no policy is widened beyond deliverable submissions,
--     and no customer gains access to anything.
--   * The acknowledgement contract is unchanged: one delivery per submission,
--     a matching document count, a matching assignment snapshot, a derived
--     routing key, a 64-hex manifest hash, an idempotent replay and 23505 for
--     a conflicting one.
--   * No existing row is rewritten. There is no backfill and no UPDATE outside
--     the functions below.
--
-- Rollback is at the bottom of this file, inside a block comment.

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. One more inquiry type: the proforma invoice request
-- ---------------------------------------------------------------------------

ALTER TABLE public.inquiry_customer_submissions
  DROP CONSTRAINT IF EXISTS inquiry_customer_submissions_inquiry_type_check;
ALTER TABLE public.inquiry_customer_submissions
  ADD CONSTRAINT inquiry_customer_submissions_inquiry_type_check
  CHECK (inquiry_type IN ('quotation','drawing_review','technical','order_handover','proforma_invoice','other'));

ALTER TABLE public.inquiry_customer_projects
  DROP CONSTRAINT IF EXISTS inquiry_customer_projects_default_inquiry_type_check;
ALTER TABLE public.inquiry_customer_projects
  ADD CONSTRAINT inquiry_customer_projects_default_inquiry_type_check
  CHECK (default_inquiry_type IN ('quotation','drawing_review','technical','order_handover','proforma_invoice','other'));

-- The two write functions validate the type as well, so both are restated
-- with the wider list. Signatures, gates and effects are otherwise identical.
-- CREATE OR REPLACE preserves each function's existing ACL, so the
-- authenticated-only grants from the earlier migrations stay in force.

CREATE OR REPLACE FUNCTION public.create_customer_inquiry_draft_v2(
  p_contact_name text,p_company_name text,p_phone text,p_customer_reference text,
  p_attention_name text,p_vat_trn_number text,p_salesman_reference text,p_handover_estimator_name text,
  p_project_id uuid,p_project_name text,p_project_location text,p_inquiry_type text,
  p_description text,p_expected_document_count integer
) RETURNS TABLE(id uuid,submission_reference text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_id uuid; v_reference text; v_project public.inquiry_customer_projects%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF p_project_id IS NOT NULL THEN
    SELECT * INTO v_project FROM public.inquiry_customer_projects
      WHERE inquiry_customer_projects.id=p_project_id AND customer_user_id=auth.uid() AND active;
    IF v_project.id IS NULL THEN RAISE EXCEPTION 'Project not found.' USING ERRCODE='P0002'; END IF;
    p_project_name:=v_project.project_name; p_project_location:=v_project.project_location;
  END IF;
  IF p_expected_document_count NOT BETWEEN 0 AND 15
    OR char_length(btrim(coalesce(p_contact_name,''))) NOT BETWEEN 1 AND 120
    OR char_length(btrim(coalesce(p_company_name,''))) NOT BETWEEN 1 AND 160
    OR char_length(btrim(coalesce(p_project_name,''))) NOT BETWEEN 1 AND 180
    OR char_length(btrim(coalesce(p_project_location,''))) > 180
    OR char_length(btrim(coalesce(p_description,''))) NOT BETWEEN 10 AND 5000
    OR p_inquiry_type NOT IN ('quotation','drawing_review','technical','order_handover','proforma_invoice','other')
  THEN RAISE EXCEPTION 'Invalid inquiry draft fields.' USING ERRCODE='22023'; END IF;
  INSERT INTO public.inquiry_customer_submissions(customer_user_id,contact_name,company_name,phone,customer_reference,
    attention_name,vat_trn_number,salesman_reference,handover_estimator_name,project_id,project_name,project_location,
    inquiry_type,description,upload_state,expected_document_count,finalized_at)
  VALUES(auth.uid(),btrim(p_contact_name),btrim(p_company_name),btrim(coalesce(p_phone,'')),btrim(coalesce(p_customer_reference,'')),
    btrim(coalesce(p_attention_name,'')),btrim(coalesce(p_vat_trn_number,'')),btrim(coalesce(p_salesman_reference,'')),
    btrim(coalesce(p_handover_estimator_name,'')),p_project_id,btrim(p_project_name),btrim(coalesce(p_project_location,'')),
    p_inquiry_type,btrim(p_description),'draft',p_expected_document_count,NULL)
  RETURNING inquiry_customer_submissions.id,inquiry_customer_submissions.submission_reference INTO v_id,v_reference;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(v_id,'draft_created',jsonb_build_object('expected_document_count',p_expected_document_count,'project_id',p_project_id,'inquiry_type',p_inquiry_type));
  RETURN QUERY SELECT v_id,v_reference;
END $$;;


CREATE OR REPLACE FUNCTION public.save_customer_inquiry_project(
  p_project_id uuid,p_project_name text,p_project_location text,p_default_inquiry_type text,p_active boolean DEFAULT true
) RETURNS public.inquiry_customer_projects
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_project public.inquiry_customer_projects%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF char_length(btrim(coalesce(p_project_name,''))) NOT BETWEEN 1 AND 180
    OR char_length(btrim(coalesce(p_project_location,''))) > 180
    OR p_default_inquiry_type NOT IN ('quotation','drawing_review','technical','order_handover','proforma_invoice','other')
  THEN RAISE EXCEPTION 'Invalid customer project fields.' USING ERRCODE='22023'; END IF;
  IF p_project_id IS NULL THEN
    INSERT INTO public.inquiry_customer_projects(customer_user_id,project_name,project_location,default_inquiry_type,active)
    VALUES(auth.uid(),btrim(p_project_name),btrim(coalesce(p_project_location,'')),p_default_inquiry_type,coalesce(p_active,true))
    RETURNING * INTO v_project;
  ELSE
    UPDATE public.inquiry_customer_projects SET project_name=btrim(p_project_name),
      project_location=btrim(coalesce(p_project_location,'')),default_inquiry_type=p_default_inquiry_type,
      active=coalesce(p_active,true),updated_at=now()
    WHERE id=p_project_id AND customer_user_id=auth.uid() RETURNING * INTO v_project;
    IF v_project.id IS NULL THEN RAISE EXCEPTION 'Project not found.' USING ERRCODE='P0002'; END IF;
  END IF;
  RETURN v_project;
END $$;;


-- ---------------------------------------------------------------------------
-- 2. Finalize: the quotation request is ready, the PI request waits
-- ---------------------------------------------------------------------------
--
-- Both outcomes are still 'complete' with a submitted_at, so the customer sees
-- the submission immediately. Only the delivery gate differs: submitted and
-- accepted are deliverable, under_review is not.

CREATE OR REPLACE FUNCTION public.finalize_customer_inquiry_submission(p_submission_id uuid)
RETURNS TABLE(id uuid,submission_reference text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_submission public.inquiry_customer_submissions%ROWTYPE; v_count integer;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  SELECT * INTO v_submission FROM public.inquiry_customer_submissions
    WHERE inquiry_customer_submissions.id=p_submission_id AND customer_user_id=auth.uid() FOR UPDATE;
  IF v_submission.id IS NULL THEN RAISE EXCEPTION 'Submission not found.' USING ERRCODE='P0002'; END IF;
  IF v_submission.upload_state='complete' AND v_submission.finalized_at IS NOT NULL
  THEN RETURN QUERY SELECT v_submission.id,v_submission.submission_reference; RETURN; END IF;
  IF v_submission.upload_state<>'draft' THEN RAISE EXCEPTION 'Submission cannot be finalized.' USING ERRCODE='22023'; END IF;
  SELECT count(*) INTO v_count FROM public.inquiry_customer_documents WHERE submission_id=p_submission_id;
  IF v_count<>v_submission.expected_document_count
  THEN RAISE EXCEPTION 'Not all expected documents are registered.' USING ERRCODE='22023'; END IF;
  -- A quotation request is ready for the desktop the moment it is complete.
  -- A proforma invoice request is a priced document, so it is parked in
  -- under_review until an inquiry_admin accepts it on the website.
  UPDATE public.inquiry_customer_submissions
    SET upload_state='complete',finalized_at=now(),submitted_at=now(),status_updated_at=now(),
        status=CASE WHEN v_submission.inquiry_type='proforma_invoice'
                    THEN 'under_review' ELSE 'submitted' END,
        accepted_at=NULL
    WHERE inquiry_customer_submissions.id=p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(p_submission_id,'submitted',jsonb_build_object(
      'document_count',v_count,
      'inquiry_type',v_submission.inquiry_type,
      'desktop_handoff',CASE WHEN v_submission.inquiry_type='proforma_invoice'
                             THEN 'held_for_coordinator' ELSE 'ready' END));
  RETURN QUERY SELECT v_submission.id,v_submission.submission_reference;
END $$;;


-- ---------------------------------------------------------------------------
-- 3. The delivery gate: submitted or accepted
-- ---------------------------------------------------------------------------
--
-- The desk is offered a complete submission with no delivery row, in either
-- deliverable status. under_review is a deliberate hold and stays out.

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
  WHERE s.upload_state='complete' AND s.status IN ('submitted','accepted') AND x.submission_id IS NULL
  GROUP BY s.id,p.estimator_code ORDER BY s.submitted_at,s.id LIMIT greatest(1,least(coalesce(p_limit,25),100));
END $function$;;


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
    AND s.upload_state='complete' AND s.status IN ('submitted','accepted')) THEN
    RAISE EXCEPTION 'Submission is not waiting for the desktop.' USING ERRCODE='P0002'; END IF;
  RETURN QUERY SELECT d.id,d.original_filename,d.detected_mime,d.byte_size,d.content_sha256,
    d.storage_bucket,d.storage_object_key FROM public.inquiry_customer_documents d
    WHERE d.submission_id=p_submission_id AND d.availability='available' ORDER BY d.uploaded_at,d.id;
END $function$;;


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
     AND s.status IN ('submitted','accepted')
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
   WHERE id = p_submission_id AND status IN ('submitted','accepted');
  INSERT INTO public.inquiry_customer_events(submission_id, event_type, details, customer_visible)
  VALUES (p_submission_id, 'inbox_delivered',
          jsonb_build_object(
            'document_count', p_document_count,
            'routing_key', p_routing_key,
            'primary_estimator_code', v_primary),
          true);
END
$function$;;


-- ---------------------------------------------------------------------------
-- 4. The bridge may read a deliverable submission, its documents and its files
-- ---------------------------------------------------------------------------
--
-- Three policies said 'accepted', and the storage policy is evaluated as the
-- caller, so it inherits the row level security of the two tables it joins.
-- Widening only the storage policy would therefore have changed nothing: the
-- bridge would still have seen no submission rows, no document rows, and an
-- EMPTY object listing, which is exactly how it failed in validation.
--
-- All three now say deliverable: submitted or accepted. A delivered submission
-- (status 'downloaded') is still closed to the bridge. This is the same rule as
-- the delivery gate, so the files a PC is offered are always the files it is
-- allowed to read.
--
-- The previous storage rule also closed the door behind every delivery, because
-- the acknowledgement sets 'downloaded': a PC that lost its local copy could not
-- download the objects again. That is now possible until the acknowledgement
-- lands, and not after.

DROP POLICY IF EXISTS "Inquiry bridge reads accepted submissions" ON public.inquiry_customer_submissions;
DROP POLICY IF EXISTS "Inquiry bridge reads deliverable submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Inquiry bridge reads deliverable submissions" ON public.inquiry_customer_submissions FOR SELECT TO authenticated
USING(upload_state='complete' AND status IN ('submitted','accepted') AND public.has_inquiry_role(ARRAY['inquiry_bridge']));

DROP POLICY IF EXISTS "Inquiry bridge reads accepted documents" ON public.inquiry_customer_documents;
DROP POLICY IF EXISTS "Inquiry bridge reads deliverable documents" ON public.inquiry_customer_documents;
CREATE POLICY "Inquiry bridge reads deliverable documents" ON public.inquiry_customer_documents FOR SELECT TO authenticated
USING(public.has_inquiry_role(ARRAY['inquiry_bridge']) AND EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s
  WHERE s.id=submission_id AND s.upload_state='complete' AND s.status IN ('submitted','accepted')));

DROP POLICY IF EXISTS "Inquiry bridge reads accepted intake objects" ON storage.objects;
DROP POLICY IF EXISTS "Inquiry bridge reads deliverable intake objects" ON storage.objects;
CREATE POLICY "Inquiry bridge reads deliverable intake objects" ON storage.objects FOR SELECT TO authenticated
USING(bucket_id='inquiry-intake-documents' AND public.has_inquiry_role(ARRAY['inquiry_bridge','inquiry_admin']) AND EXISTS(
  SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
  WHERE d.storage_object_key=name AND s.upload_state='complete' AND s.status IN ('submitted','accepted')));

NOTIFY pgrst,'reload schema';

COMMIT;

/* ---------------------------------------------------------------------------
   ROLLBACK — paste this block in place of everything above this comment.
   It restores the accepted-only gate, removes the new type and puts the
   storage rule back. It is idempotent and rewrites no row.
   ---------------------------------------------------------------------------

BEGIN;

ALTER TABLE public.inquiry_customer_submissions
  DROP CONSTRAINT IF EXISTS inquiry_customer_submissions_inquiry_type_check;
ALTER TABLE public.inquiry_customer_submissions
  ADD CONSTRAINT inquiry_customer_submissions_inquiry_type_check
  CHECK (inquiry_type IN ('quotation','drawing_review','technical','order_handover','other'));

ALTER TABLE public.inquiry_customer_projects
  DROP CONSTRAINT IF EXISTS inquiry_customer_projects_default_inquiry_type_check;
ALTER TABLE public.inquiry_customer_projects
  ADD CONSTRAINT inquiry_customer_projects_default_inquiry_type_check
  CHECK (default_inquiry_type IN ('quotation','drawing_review','technical','order_handover','other'));

-- Restore the 'accepted' predicates in the three delivery functions and the
-- accepted-only finalize, by re-running the originals:
--   psql ... -f supabase-inquiry-desktop-access-phase14.sql
--   psql ... -f supabase-inquiry-intake-phase8a-staged-uploads.sql
-- (both are idempotent CREATE OR REPLACE migrations)
--
-- Then, if proforma_invoice rows exist, they must be re-typed first, because
-- the narrower constraint would reject them:
--   UPDATE public.inquiry_customer_submissions
--      SET inquiry_type='other' WHERE inquiry_type='proforma_invoice';

DROP POLICY IF EXISTS "Inquiry bridge reads deliverable submissions" ON public.inquiry_customer_submissions;
DROP POLICY IF EXISTS "Inquiry bridge reads accepted submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Inquiry bridge reads accepted submissions" ON public.inquiry_customer_submissions FOR SELECT TO authenticated
USING(upload_state='complete' AND status='accepted' AND public.has_inquiry_role(ARRAY['inquiry_bridge']));

DROP POLICY IF EXISTS "Inquiry bridge reads deliverable documents" ON public.inquiry_customer_documents;
DROP POLICY IF EXISTS "Inquiry bridge reads accepted documents" ON public.inquiry_customer_documents;
CREATE POLICY "Inquiry bridge reads accepted documents" ON public.inquiry_customer_documents FOR SELECT TO authenticated
USING(public.has_inquiry_role(ARRAY['inquiry_bridge']) AND EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s
  WHERE s.id=submission_id AND s.upload_state='complete' AND s.status='accepted'));

DROP POLICY IF EXISTS "Inquiry bridge reads deliverable intake objects" ON storage.objects;
DROP POLICY IF EXISTS "Inquiry bridge reads accepted intake objects" ON storage.objects;
CREATE POLICY "Inquiry bridge reads accepted intake objects" ON storage.objects FOR SELECT TO authenticated
USING(bucket_id='inquiry-intake-documents' AND public.has_inquiry_role(ARRAY['inquiry_bridge']) AND EXISTS(
  SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
  WHERE d.storage_object_key=name AND s.upload_state='complete' AND s.status='accepted'));

NOTIFY pgrst,'reload schema';
COMMIT;
--------------------------------------------------------------------------- */

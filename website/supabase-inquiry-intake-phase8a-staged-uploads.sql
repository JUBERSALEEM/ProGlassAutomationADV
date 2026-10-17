-- PHASE 8A FOLLOW-UP — STAGED CUSTOMER DOCUMENT UPLOADS
-- Additive migration. Apply only after the base, fields, and limits migrations.
-- Existing completed submissions and immutable documents remain unchanged.
BEGIN;

ALTER TABLE public.inquiry_customer_submissions
  ADD COLUMN IF NOT EXISTS upload_state text NOT NULL DEFAULT 'complete',
  ADD COLUMN IF NOT EXISTS expected_document_count integer NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS finalized_at timestamptz;

ALTER TABLE public.inquiry_customer_submissions
  DROP CONSTRAINT IF EXISTS inquiry_customer_submissions_upload_state_check,
  DROP CONSTRAINT IF EXISTS inquiry_customer_submissions_expected_document_count_check;
ALTER TABLE public.inquiry_customer_submissions
  ADD CONSTRAINT inquiry_customer_submissions_upload_state_check
    CHECK (upload_state IN ('draft','complete','failed')),
  ADD CONSTRAINT inquiry_customer_submissions_expected_document_count_check
    CHECK (expected_document_count BETWEEN 0 AND 15);

-- Every row created by the earlier atomic endpoint is already complete.
UPDATE public.inquiry_customer_submissions
SET upload_state='complete',
    expected_document_count=(SELECT count(*) FROM public.inquiry_customer_documents d WHERE d.submission_id=inquiry_customer_submissions.id),
    finalized_at=coalesce(finalized_at,submitted_at)
WHERE finalized_at IS NULL AND upload_state='complete';

ALTER TABLE public.inquiry_customer_events
  DROP CONSTRAINT IF EXISTS inquiry_customer_events_event_type_check;
ALTER TABLE public.inquiry_customer_events
  ADD CONSTRAINT inquiry_customer_events_event_type_check
  CHECK (event_type IN ('draft_created','submitted','document_registered','upload_failed','status_changed','download_authorized'));

CREATE OR REPLACE FUNCTION public.create_customer_inquiry_draft(
  p_contact_name text, p_company_name text, p_phone text, p_customer_reference text,
  p_attention_name text, p_vat_trn_number text, p_salesman_reference text, p_handover_estimator_name text,
  p_project_name text, p_project_location text, p_description text, p_expected_document_count integer
) RETURNS TABLE(id uuid, submission_reference text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_id uuid; v_reference text;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF p_expected_document_count NOT BETWEEN 0 AND 15
    OR char_length(btrim(coalesce(p_contact_name,''))) NOT BETWEEN 1 AND 120
    OR char_length(btrim(coalesce(p_company_name,''))) NOT BETWEEN 1 AND 160
    OR char_length(btrim(coalesce(p_project_name,''))) NOT BETWEEN 1 AND 180
    OR char_length(btrim(coalesce(p_description,''))) NOT BETWEEN 10 AND 5000
    OR char_length(btrim(coalesce(p_phone,''))) > 60
    OR char_length(btrim(coalesce(p_customer_reference,''))) > 100
    OR char_length(btrim(coalesce(p_attention_name,''))) > 120
    OR char_length(btrim(coalesce(p_vat_trn_number,''))) > 50
    OR char_length(btrim(coalesce(p_salesman_reference,''))) > 120
    OR char_length(btrim(coalesce(p_handover_estimator_name,''))) > 120
    OR char_length(btrim(coalesce(p_project_location,''))) > 180
  THEN RAISE EXCEPTION 'Invalid inquiry draft fields.' USING ERRCODE='22023'; END IF;

  INSERT INTO public.inquiry_customer_submissions(customer_user_id,contact_name,company_name,phone,
    customer_reference,attention_name,vat_trn_number,salesman_reference,handover_estimator_name,
    project_name,project_location,description,upload_state,expected_document_count,finalized_at)
  VALUES(auth.uid(),btrim(p_contact_name),btrim(p_company_name),btrim(coalesce(p_phone,'')),
    btrim(coalesce(p_customer_reference,'')),btrim(coalesce(p_attention_name,'')),btrim(coalesce(p_vat_trn_number,'')),
    btrim(coalesce(p_salesman_reference,'')),btrim(coalesce(p_handover_estimator_name,'')),
    btrim(p_project_name),btrim(coalesce(p_project_location,'')),btrim(p_description),'draft',p_expected_document_count,NULL)
  RETURNING inquiry_customer_submissions.id,inquiry_customer_submissions.submission_reference INTO v_id,v_reference;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(v_id,'draft_created',jsonb_build_object('expected_document_count',p_expected_document_count));
  RETURN QUERY SELECT v_id,v_reference;
END $$;
REVOKE ALL ON FUNCTION public.create_customer_inquiry_draft(text,text,text,text,text,text,text,text,text,text,text,integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.create_customer_inquiry_draft(text,text,text,text,text,text,text,text,text,text,text,integer) TO authenticated;

CREATE OR REPLACE FUNCTION public.register_customer_inquiry_document(
  p_submission_id uuid, p_document_id uuid, p_original_filename text, p_extension text,
  p_detected_mime text, p_byte_size bigint, p_content_sha256 text, p_storage_object_key text
) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,storage,pg_temp AS $$
DECLARE v_submission public.inquiry_customer_submissions%ROWTYPE; v_expected text; v_count integer;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  SELECT * INTO v_submission FROM public.inquiry_customer_submissions
    WHERE id=p_submission_id AND customer_user_id=auth.uid() AND status='submitted' FOR UPDATE;
  IF v_submission.id IS NULL OR v_submission.upload_state NOT IN ('draft','complete')
  THEN RAISE EXCEPTION 'Submission not found or no longer editable.' USING ERRCODE='42501'; END IF;
  IF v_submission.upload_state='complete' AND v_submission.finalized_at IS NOT NULL
  THEN RAISE EXCEPTION 'Submission is finalized.' USING ERRCODE='42501'; END IF;
  SELECT count(*) INTO v_count FROM public.inquiry_customer_documents WHERE submission_id=p_submission_id;
  -- During the migration-first deployment window, the previous atomic API can
  -- still create a complete row with expected_document_count=0. Keep that
  -- bounded path working until the staged API is deployed.
  IF v_count >= CASE WHEN v_submission.upload_state='draft' THEN v_submission.expected_document_count ELSE 15 END
    OR v_count >= 15
  THEN RAISE EXCEPTION 'Document count reached.' USING ERRCODE='22023'; END IF;
  IF EXISTS (SELECT 1 FROM public.inquiry_customer_documents
    WHERE submission_id=p_submission_id AND lower(original_filename)=lower(btrim(p_original_filename)))
  THEN RAISE EXCEPTION 'Duplicate document filename.' USING ERRCODE='22023'; END IF;
  v_expected := auth.uid()::text || '/' || p_submission_id::text || '/' || p_document_id::text;
  IF p_storage_object_key<>v_expected OR p_byte_size NOT BETWEEN 1 AND 31457280
    OR p_content_sha256 !~ '^[a-f0-9]{64}$'
  THEN RAISE EXCEPTION 'Invalid document metadata.' USING ERRCODE='22023'; END IF;
  IF NOT EXISTS (SELECT 1 FROM storage.objects WHERE bucket_id='inquiry-intake-documents' AND name=p_storage_object_key)
  THEN RAISE EXCEPTION 'Uploaded object not found.' USING ERRCODE='P0002'; END IF;
  INSERT INTO public.inquiry_customer_documents(id,submission_id,original_filename,extension,detected_mime,
    byte_size,content_sha256,storage_object_key)
  VALUES(p_document_id,p_submission_id,btrim(p_original_filename),lower(p_extension),lower(p_detected_mime),
    p_byte_size,lower(p_content_sha256),p_storage_object_key);
  INSERT INTO public.inquiry_customer_events(submission_id,document_id,event_type)
    VALUES(p_submission_id,p_document_id,'document_registered');
  RETURN p_document_id;
END $$;
REVOKE ALL ON FUNCTION public.register_customer_inquiry_document(uuid,uuid,text,text,text,bigint,text,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.register_customer_inquiry_document(uuid,uuid,text,text,text,bigint,text,text) TO authenticated;

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
  UPDATE public.inquiry_customer_submissions
    SET upload_state='complete',finalized_at=now(),submitted_at=now(),status_updated_at=now()
    WHERE inquiry_customer_submissions.id=p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(p_submission_id,'submitted',jsonb_build_object('document_count',v_count));
  RETURN QUERY SELECT v_submission.id,v_submission.submission_reference;
END $$;
REVOKE ALL ON FUNCTION public.finalize_customer_inquiry_submission(uuid) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.finalize_customer_inquiry_submission(uuid) TO authenticated;

-- Drafts remain owner-readable for retry, but operators must not see an
-- incomplete customer handoff through direct PostgREST/table access.
DROP POLICY IF EXISTS "Customers read own inquiry submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Customers read own inquiry submissions" ON public.inquiry_customer_submissions
  FOR SELECT TO authenticated USING (
    customer_user_id=auth.uid() OR (upload_state='complete'
      AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']))
  );
DROP POLICY IF EXISTS "Customers read own inquiry documents" ON public.inquiry_customer_documents;
CREATE POLICY "Customers read own inquiry documents" ON public.inquiry_customer_documents
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=submission_id
      AND (s.customer_user_id=auth.uid() OR (s.upload_state='complete'
        AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])))
  ));
DROP POLICY IF EXISTS "Customers read visible own inquiry events" ON public.inquiry_customer_events;
CREATE POLICY "Customers read visible own inquiry events" ON public.inquiry_customer_events
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=submission_id
      AND ((s.customer_user_id=auth.uid() AND customer_visible)
        OR (s.upload_state='complete' AND public.has_inquiry_role(ARRAY['inquiry_auditor','inquiry_admin'])))
  ));

DROP POLICY IF EXISTS "Customers upload own inquiry intake documents" ON storage.objects;
CREATE POLICY "Customers upload own inquiry intake documents" ON storage.objects FOR INSERT TO authenticated
WITH CHECK (bucket_id='inquiry-intake-documents' AND (storage.foldername(name))[1]=auth.uid()::text
  AND EXISTS (SELECT 1 FROM public.inquiry_customer_submissions s
    WHERE s.id::text=(storage.foldername(name))[2] AND s.customer_user_id=auth.uid()
      AND s.status='submitted' AND (s.upload_state='draft'
        OR (s.upload_state='complete' AND s.finalized_at IS NULL))));
DROP POLICY IF EXISTS "Customers and operators download inquiry intake documents" ON storage.objects;
CREATE POLICY "Customers and operators download inquiry intake documents" ON storage.objects FOR SELECT TO authenticated
USING (bucket_id='inquiry-intake-documents' AND EXISTS (
  SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
  WHERE d.storage_object_key=name AND (s.customer_user_id=auth.uid()
    OR (s.upload_state='complete' AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])))
));

NOTIFY pgrst,'reload schema';
COMMIT;

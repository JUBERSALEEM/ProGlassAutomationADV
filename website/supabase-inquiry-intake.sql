-- PROGLASS AUTOMATION — CUSTOMER WEBSITE INQUIRY INTAKE (PHASE 8A)
-- Additive migration. Apply only after review in a disposable/non-production project.
-- Keeps website submissions separate from desktop-published public.inquiries.
BEGIN;

CREATE SEQUENCE IF NOT EXISTS public.inquiry_submission_reference_seq;

CREATE TABLE IF NOT EXISTS public.inquiry_customer_submissions (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  submission_reference  text NOT NULL UNIQUE DEFAULT (
    'WEB-' || to_char(current_date, 'YYYY') || '-' ||
    lpad(nextval('public.inquiry_submission_reference_seq')::text, 6, '0')
  ),
  customer_user_id      uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  contact_name          text NOT NULL CHECK (char_length(contact_name) BETWEEN 1 AND 120),
  company_name          text NOT NULL CHECK (char_length(company_name) BETWEEN 1 AND 160),
  phone                 text NOT NULL DEFAULT '' CHECK (char_length(phone) <= 60),
  customer_reference    text NOT NULL DEFAULT '' CHECK (char_length(customer_reference) <= 100),
  attention_name        text NOT NULL DEFAULT '' CHECK (char_length(attention_name) <= 120),
  vat_trn_number        text NOT NULL DEFAULT '' CHECK (char_length(vat_trn_number) <= 50),
  salesman_reference    text NOT NULL DEFAULT '' CHECK (char_length(salesman_reference) <= 120),
  handover_estimator_name text NOT NULL DEFAULT '' CHECK (char_length(handover_estimator_name) <= 120),
  project_name          text NOT NULL CHECK (char_length(project_name) BETWEEN 1 AND 180),
  project_location      text NOT NULL DEFAULT '' CHECK (char_length(project_location) <= 180),
  description           text NOT NULL CHECK (char_length(description) BETWEEN 10 AND 5000),
  status                text NOT NULL DEFAULT 'submitted' CHECK (status IN ('submitted','under_review','accepted','rejected','downloaded','closed')),
  submitted_at          timestamptz NOT NULL DEFAULT now(),
  status_updated_at     timestamptz NOT NULL DEFAULT now(),
  accepted_at           timestamptz,
  downloaded_at         timestamptz,
  closed_at             timestamptz
);

CREATE INDEX IF NOT EXISTS idx_inquiry_customer_submissions_owner
  ON public.inquiry_customer_submissions(customer_user_id, submitted_at DESC);
CREATE INDEX IF NOT EXISTS idx_inquiry_customer_submissions_status
  ON public.inquiry_customer_submissions(status, submitted_at);

CREATE TABLE IF NOT EXISTS public.inquiry_customer_documents (
  id                    uuid PRIMARY KEY,
  submission_id         uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  original_filename     text NOT NULL CHECK (char_length(original_filename) BETWEEN 1 AND 260),
  extension             text NOT NULL CHECK (extension ~ '^\.[a-z0-9]{1,10}$'),
  detected_mime         text NOT NULL CHECK (char_length(detected_mime) BETWEEN 3 AND 150),
  byte_size             bigint NOT NULL CHECK (byte_size BETWEEN 1 AND 31457280),
  content_sha256        text NOT NULL CHECK (content_sha256 ~ '^[a-f0-9]{64}$'),
  storage_bucket        text NOT NULL DEFAULT 'inquiry-intake-documents' CHECK (storage_bucket='inquiry-intake-documents'),
  storage_object_key    text NOT NULL UNIQUE,
  availability          text NOT NULL DEFAULT 'available' CHECK (availability IN ('available','quarantined')),
  uploaded_at           timestamptz NOT NULL DEFAULT now(),
  UNIQUE(submission_id, content_sha256, original_filename)
);
CREATE INDEX IF NOT EXISTS idx_inquiry_customer_documents_submission
  ON public.inquiry_customer_documents(submission_id, uploaded_at);

CREATE TABLE IF NOT EXISTS public.inquiry_customer_events (
  id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  submission_id   uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  document_id     uuid REFERENCES public.inquiry_customer_documents(id) ON DELETE RESTRICT,
  actor_id        uuid NOT NULL DEFAULT auth.uid() REFERENCES auth.users(id) ON DELETE RESTRICT,
  event_type      text NOT NULL CHECK (event_type IN ('submitted','document_registered','status_changed','download_authorized')),
  details         jsonb NOT NULL DEFAULT '{}'::jsonb,
  customer_visible boolean NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_inquiry_customer_events_submission
  ON public.inquiry_customer_events(submission_id, created_at);

ALTER TABLE public.inquiry_customer_submissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_customer_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_customer_events ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.inquiry_customer_submissions, public.inquiry_customer_documents,
  public.inquiry_customer_events FROM PUBLIC, anon;
REVOKE INSERT, UPDATE, DELETE ON TABLE public.inquiry_customer_submissions,
  public.inquiry_customer_documents, public.inquiry_customer_events FROM authenticated;
GRANT SELECT ON TABLE public.inquiry_customer_submissions, public.inquiry_customer_documents,
  public.inquiry_customer_events TO authenticated;

DROP POLICY IF EXISTS "Customers read own inquiry submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Customers read own inquiry submissions" ON public.inquiry_customer_submissions
  FOR SELECT TO authenticated USING (
    customer_user_id=auth.uid() OR public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])
  );
DROP POLICY IF EXISTS "Customers read own inquiry documents" ON public.inquiry_customer_documents;
CREATE POLICY "Customers read own inquiry documents" ON public.inquiry_customer_documents
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=submission_id
      AND (s.customer_user_id=auth.uid() OR public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']))
  ));
DROP POLICY IF EXISTS "Customers read visible own inquiry events" ON public.inquiry_customer_events;
CREATE POLICY "Customers read visible own inquiry events" ON public.inquiry_customer_events
  FOR SELECT TO authenticated USING (EXISTS (
    SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=submission_id
      AND ((s.customer_user_id=auth.uid() AND customer_visible)
        OR public.has_inquiry_role(ARRAY['inquiry_auditor','inquiry_admin']))
  ));

CREATE OR REPLACE FUNCTION public.create_customer_inquiry_submission(
  p_contact_name text, p_company_name text, p_phone text, p_customer_reference text,
  p_attention_name text, p_vat_trn_number text, p_salesman_reference text, p_handover_estimator_name text,
  p_project_name text, p_project_location text, p_description text
) RETURNS TABLE(id uuid, submission_reference text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_id uuid; v_reference text;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF char_length(btrim(coalesce(p_contact_name,''))) NOT BETWEEN 1 AND 120
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
  THEN RAISE EXCEPTION 'Invalid inquiry submission fields.' USING ERRCODE='22023'; END IF;
  INSERT INTO public.inquiry_customer_submissions(customer_user_id,contact_name,company_name,phone,
    customer_reference,attention_name,vat_trn_number,salesman_reference,handover_estimator_name,project_name,project_location,description)
  VALUES(auth.uid(),btrim(p_contact_name),btrim(p_company_name),btrim(coalesce(p_phone,'')),
    btrim(coalesce(p_customer_reference,'')),btrim(coalesce(p_attention_name,'')),btrim(coalesce(p_vat_trn_number,'')),
    btrim(coalesce(p_salesman_reference,'')),btrim(coalesce(p_handover_estimator_name,'')),
    btrim(p_project_name),btrim(coalesce(p_project_location,'')),btrim(p_description))
  RETURNING inquiry_customer_submissions.id,inquiry_customer_submissions.submission_reference INTO v_id,v_reference;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type) VALUES(v_id,'submitted');
  RETURN QUERY SELECT v_id,v_reference;
END $$;
REVOKE ALL ON FUNCTION public.create_customer_inquiry_submission(text,text,text,text,text,text,text,text,text,text,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.create_customer_inquiry_submission(text,text,text,text,text,text,text,text,text,text,text) TO authenticated;

CREATE OR REPLACE FUNCTION public.register_customer_inquiry_document(
  p_submission_id uuid, p_document_id uuid, p_original_filename text, p_extension text,
  p_detected_mime text, p_byte_size bigint, p_content_sha256 text, p_storage_object_key text
) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,storage,pg_temp AS $$
DECLARE v_owner uuid; v_expected text;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  SELECT customer_user_id INTO v_owner FROM public.inquiry_customer_submissions
    WHERE id=p_submission_id AND customer_user_id=auth.uid() AND status='submitted' FOR UPDATE;
  IF v_owner IS NULL THEN RAISE EXCEPTION 'Submission not found or no longer editable.' USING ERRCODE='42501'; END IF;
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

CREATE OR REPLACE FUNCTION public.set_customer_inquiry_status(p_submission_id uuid,p_status text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_old text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'Inquiry admin role required.' USING ERRCODE='42501'; END IF;
  IF p_status NOT IN ('under_review','accepted','rejected','closed') THEN
    RAISE EXCEPTION 'Invalid review status.' USING ERRCODE='22023'; END IF;
  SELECT status INTO v_old FROM public.inquiry_customer_submissions WHERE id=p_submission_id FOR UPDATE;
  IF v_old IS NULL THEN RAISE EXCEPTION 'Submission not found.' USING ERRCODE='P0002'; END IF;
  UPDATE public.inquiry_customer_submissions SET status=p_status,status_updated_at=now(),
    accepted_at=CASE WHEN p_status='accepted' THEN coalesce(accepted_at,now()) ELSE accepted_at END,
    closed_at=CASE WHEN p_status='closed' THEN coalesce(closed_at,now()) ELSE closed_at END
  WHERE id=p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(p_submission_id,'status_changed',jsonb_build_object('from',v_old,'to',p_status));
END $$;
REVOKE ALL ON FUNCTION public.set_customer_inquiry_status(uuid,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.set_customer_inquiry_status(uuid,text) TO authenticated;

CREATE OR REPLACE FUNCTION public.authorize_customer_inquiry_document_download(p_submission_id uuid,p_document_id uuid)
RETURNS TABLE(storage_bucket text,storage_object_key text,original_filename text,detected_mime text,byte_size bigint,content_sha256 text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_submission public.inquiry_customer_submissions%ROWTYPE; v_document public.inquiry_customer_documents%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  SELECT * INTO v_submission FROM public.inquiry_customer_submissions WHERE id=p_submission_id;
  IF v_submission.id IS NULL OR NOT (v_submission.customer_user_id=auth.uid()
    OR public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']))
  THEN RAISE EXCEPTION 'Document not found.' USING ERRCODE='P0002'; END IF;
  SELECT * INTO v_document FROM public.inquiry_customer_documents
    WHERE id=p_document_id AND submission_id=p_submission_id AND availability='available';
  IF v_document.id IS NULL THEN RAISE EXCEPTION 'Document not found.' USING ERRCODE='P0002'; END IF;
  INSERT INTO public.inquiry_customer_events(submission_id,document_id,event_type,customer_visible)
    VALUES(p_submission_id,p_document_id,'download_authorized',false);
  RETURN QUERY SELECT v_document.storage_bucket,v_document.storage_object_key,v_document.original_filename,
    v_document.detected_mime,v_document.byte_size,v_document.content_sha256;
END $$;
REVOKE ALL ON FUNCTION public.authorize_customer_inquiry_document_download(uuid,uuid) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.authorize_customer_inquiry_document_download(uuid,uuid) TO authenticated;

INSERT INTO storage.buckets(id,name,public,file_size_limit,allowed_mime_types)
VALUES('inquiry-intake-documents','inquiry-intake-documents',false,31457280,
  ARRAY['application/pdf','image/jpeg','image/png','image/gif','image/webp','image/bmp','image/x-ms-bmp','text/csv','text/plain','application/csv','application/vnd.openxmlformats-officedocument.spreadsheetml.sheet','application/zip'])
ON CONFLICT(id) DO UPDATE SET public=false,file_size_limit=31457280,allowed_mime_types=EXCLUDED.allowed_mime_types;

DROP POLICY IF EXISTS "Customers upload own inquiry intake documents" ON storage.objects;
CREATE POLICY "Customers upload own inquiry intake documents" ON storage.objects FOR INSERT TO authenticated
WITH CHECK (bucket_id='inquiry-intake-documents' AND (storage.foldername(name))[1]=auth.uid()::text
  AND EXISTS (SELECT 1 FROM public.inquiry_customer_submissions s
    WHERE s.id::text=(storage.foldername(name))[2] AND s.customer_user_id=auth.uid() AND s.status='submitted'));
DROP POLICY IF EXISTS "Customers and operators download inquiry intake documents" ON storage.objects;
CREATE POLICY "Customers and operators download inquiry intake documents" ON storage.objects FOR SELECT TO authenticated
USING (bucket_id='inquiry-intake-documents' AND EXISTS (
  SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
  WHERE d.storage_object_key=name AND (s.customer_user_id=auth.uid()
    OR public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])))
);
-- Deliberately no UPDATE or DELETE policy: submitted evidence is immutable.
NOTIFY pgrst,'reload schema';
COMMIT;

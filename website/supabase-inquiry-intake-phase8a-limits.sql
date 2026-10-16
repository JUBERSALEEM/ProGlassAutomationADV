-- PHASE 8A FOLLOW-UP — CUSTOMER UPLOAD LIMITS
-- Apply only after supabase-inquiry-intake.sql in disposable/non-production.
BEGIN;
ALTER TABLE public.inquiry_customer_documents
  DROP CONSTRAINT IF EXISTS inquiry_customer_documents_byte_size_check;
ALTER TABLE public.inquiry_customer_documents
  ADD CONSTRAINT inquiry_customer_documents_byte_size_check
  CHECK (byte_size BETWEEN 1 AND 31457280);

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

UPDATE storage.buckets SET public=false,file_size_limit=31457280
WHERE id='inquiry-intake-documents';
NOTIFY pgrst,'reload schema';
COMMIT;

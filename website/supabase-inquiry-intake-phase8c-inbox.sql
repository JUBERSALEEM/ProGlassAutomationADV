-- PHASE 8C — CUSTOMER INQUIRY CLOUD-TO-WINDOWS INBOX
-- Additive migration. Apply only after all Phase 8A/8B migrations.
BEGIN;

CREATE TABLE IF NOT EXISTS public.inquiry_customer_deliveries (
  submission_id uuid PRIMARY KEY REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  bridge_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  manifest_sha256 text NOT NULL CHECK (manifest_sha256 ~ '^[0-9a-f]{64}$'),
  document_count integer NOT NULL CHECK (document_count BETWEEN 0 AND 15),
  acknowledged_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE public.inquiry_customer_deliveries ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.inquiry_customer_deliveries FROM PUBLIC,anon,authenticated;

CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_deliveries(p_limit integer DEFAULT 25)
RETURNS TABLE(submission_id uuid,submission_reference text,submitted_at timestamptz,status text,
  contact_name text,company_name text,phone text,customer_reference text,attention_name text,
  vat_trn_number text,salesman_reference text,handover_estimator_name text,project_name text,
  project_location text,inquiry_type text,description text,document_count bigint)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  RETURN QUERY SELECT s.id,s.submission_reference,s.submitted_at,s.status,s.contact_name,s.company_name,
    s.phone,s.customer_reference,s.attention_name,s.vat_trn_number,s.salesman_reference,
    s.handover_estimator_name,s.project_name,s.project_location,s.inquiry_type,s.description,
    count(d.id)::bigint
  FROM public.inquiry_customer_submissions s
  LEFT JOIN public.inquiry_customer_documents d ON d.submission_id=s.id AND d.availability='available'
  LEFT JOIN public.inquiry_customer_deliveries x ON x.submission_id=s.id
  WHERE s.upload_state='complete' AND s.status='accepted' AND x.submission_id IS NULL
  GROUP BY s.id ORDER BY s.submitted_at,s.id LIMIT greatest(1,least(coalesce(p_limit,25),100));
END $$;

CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_delivery_documents(p_submission_id uuid)
RETURNS TABLE(document_id uuid,original_filename text,detected_mime text,byte_size bigint,
  content_sha256 text,storage_bucket text,storage_object_key text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  IF NOT EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=p_submission_id
    AND s.upload_state='complete' AND s.status='accepted') THEN
    RAISE EXCEPTION 'Accepted submission not found.' USING ERRCODE='P0002'; END IF;
  RETURN QUERY SELECT d.id,d.original_filename,d.detected_mime,d.byte_size,d.content_sha256,
    d.storage_bucket,d.storage_object_key FROM public.inquiry_customer_documents d
    WHERE d.submission_id=p_submission_id AND d.availability='available' ORDER BY d.uploaded_at,d.id;
END $$;

CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery(
  p_submission_id uuid,p_manifest_sha256 text,p_document_count integer)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_expected integer;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  IF p_manifest_sha256 !~ '^[0-9a-f]{64}$' OR p_document_count NOT BETWEEN 0 AND 15 THEN
    RAISE EXCEPTION 'Invalid acknowledgement.' USING ERRCODE='22023'; END IF;
  SELECT count(*) INTO v_expected FROM public.inquiry_customer_documents d
    JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
    WHERE s.id=p_submission_id AND s.upload_state='complete' AND s.status='accepted'
      AND d.availability='available';
  IF v_expected<>p_document_count THEN RAISE EXCEPTION 'Document count changed.' USING ERRCODE='40001'; END IF;
  IF EXISTS(SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id=p_submission_id) THEN
    IF EXISTS(SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id=p_submission_id
      AND manifest_sha256=p_manifest_sha256 AND document_count=p_document_count) THEN RETURN; END IF;
    RAISE EXCEPTION 'Conflicting acknowledgement.' USING ERRCODE='23505';
  END IF;
  INSERT INTO public.inquiry_customer_deliveries(submission_id,bridge_user_id,manifest_sha256,document_count)
    VALUES(p_submission_id,auth.uid(),p_manifest_sha256,p_document_count);
  UPDATE public.inquiry_customer_submissions SET status='downloaded',downloaded_at=coalesce(downloaded_at,now()),status_updated_at=now()
    WHERE id=p_submission_id AND status='accepted';
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details,customer_visible)
    VALUES(p_submission_id,'inbox_delivered',jsonb_build_object('document_count',p_document_count),true);
END $$;

REVOKE ALL ON FUNCTION public.plan_customer_inquiry_deliveries(integer),public.plan_customer_inquiry_delivery_documents(uuid),public.acknowledge_customer_inquiry_delivery(uuid,text,integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.plan_customer_inquiry_deliveries(integer),public.plan_customer_inquiry_delivery_documents(uuid),public.acknowledge_customer_inquiry_delivery(uuid,text,integer) TO authenticated;

DROP POLICY IF EXISTS "Inquiry bridge reads accepted submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Inquiry bridge reads accepted submissions" ON public.inquiry_customer_submissions FOR SELECT TO authenticated
USING(upload_state='complete' AND status='accepted' AND public.has_inquiry_role(ARRAY['inquiry_bridge']));
DROP POLICY IF EXISTS "Inquiry bridge reads accepted documents" ON public.inquiry_customer_documents;
CREATE POLICY "Inquiry bridge reads accepted documents" ON public.inquiry_customer_documents FOR SELECT TO authenticated
USING(public.has_inquiry_role(ARRAY['inquiry_bridge']) AND EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s
  WHERE s.id=submission_id AND s.upload_state='complete' AND s.status='accepted'));

DROP POLICY IF EXISTS "Inquiry bridge reads accepted intake objects" ON storage.objects;
CREATE POLICY "Inquiry bridge reads accepted intake objects" ON storage.objects FOR SELECT TO authenticated
USING(bucket_id='inquiry-intake-documents' AND public.has_inquiry_role(ARRAY['inquiry_bridge']) AND EXISTS(
  SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
  WHERE d.storage_object_key=name AND s.upload_state='complete' AND s.status='accepted'));

NOTIFY pgrst,'reload schema';
COMMIT;

-- PHASE 9B — ESTIMATOR TEAM VISIBILITY AND INTEGRITY-CHECKED DOWNLOAD AUTHORIZATION
BEGIN;
CREATE OR REPLACE FUNCTION public.is_active_inquiry_estimator_assignment(p_submission_id uuid,p_user_id uuid DEFAULT auth.uid())
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
 SELECT p_user_id IS NOT NULL AND public.has_inquiry_role(ARRAY['inquiry_estimator']) AND EXISTS(
  SELECT 1 FROM public.inquiry_estimator_assignments a JOIN public.inquiry_estimators e ON e.user_id=a.estimator_user_id
  WHERE a.submission_id=p_submission_id AND a.estimator_user_id=p_user_id AND a.active AND e.active);
$$;
REVOKE ALL ON FUNCTION public.is_active_inquiry_estimator_assignment(uuid,uuid) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.is_active_inquiry_estimator_assignment(uuid,uuid) TO authenticated;
DROP POLICY IF EXISTS "Admins auditors and assigned estimators read assignments" ON public.inquiry_estimator_assignments;
CREATE POLICY "Admins auditors and assigned estimators read assignments" ON public.inquiry_estimator_assignments FOR SELECT TO authenticated
USING(public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor']) OR public.is_active_inquiry_estimator_assignment(submission_id));

CREATE OR REPLACE FUNCTION public.authorize_customer_inquiry_document_download(p_submission_id uuid,p_document_id uuid)
RETURNS TABLE(storage_bucket text,storage_object_key text,original_filename text,detected_mime text,byte_size bigint,content_sha256 text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_submission public.inquiry_customer_submissions%ROWTYPE;v_document public.inquiry_customer_documents%ROWTYPE;
BEGIN
 IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501';END IF;
 SELECT * INTO v_submission FROM public.inquiry_customer_submissions WHERE id=p_submission_id;
 IF v_submission.id IS NULL OR NOT(v_submission.customer_user_id=auth.uid()
  OR public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])
  OR public.is_active_inquiry_estimator_assignment(p_submission_id))
 THEN RAISE EXCEPTION 'Document not found.' USING ERRCODE='P0002';END IF;
 SELECT * INTO v_document FROM public.inquiry_customer_documents WHERE id=p_document_id AND submission_id=p_submission_id AND availability='available';
 IF v_document.id IS NULL THEN RAISE EXCEPTION 'Document not found.' USING ERRCODE='P0002';END IF;
 INSERT INTO public.inquiry_customer_events(submission_id,document_id,event_type,customer_visible) VALUES(p_submission_id,p_document_id,'download_authorized',false);
 RETURN QUERY SELECT v_document.storage_bucket,v_document.storage_object_key,v_document.original_filename,v_document.detected_mime,v_document.byte_size,v_document.content_sha256;
END $$;
REVOKE ALL ON FUNCTION public.authorize_customer_inquiry_document_download(uuid,uuid) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.authorize_customer_inquiry_document_download(uuid,uuid) TO authenticated;
NOTIFY pgrst,'reload schema';
COMMIT;

-- PHASE 8A FOLLOW-UP — SALESMAN/HANDOVER FIELDS
-- Additive patch for a non-production project where supabase-inquiry-intake.sql
-- was already applied. Do not apply to production without explicit approval.
BEGIN;
ALTER TABLE public.inquiry_customer_submissions
  ADD COLUMN IF NOT EXISTS salesman_reference text NOT NULL DEFAULT '' CHECK (char_length(salesman_reference) <= 120),
  ADD COLUMN IF NOT EXISTS handover_estimator_name text NOT NULL DEFAULT '' CHECK (char_length(handover_estimator_name) <= 120);

REVOKE ALL ON FUNCTION public.create_customer_inquiry_submission(text,text,text,text,text,text,text) FROM PUBLIC,anon,authenticated;
DROP FUNCTION public.create_customer_inquiry_submission(text,text,text,text,text,text,text);

CREATE FUNCTION public.create_customer_inquiry_submission(
  p_contact_name text, p_company_name text, p_phone text, p_customer_reference text,
  p_salesman_reference text, p_handover_estimator_name text,
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
    OR char_length(btrim(coalesce(p_salesman_reference,''))) > 120
    OR char_length(btrim(coalesce(p_handover_estimator_name,''))) > 120
    OR char_length(btrim(coalesce(p_project_location,''))) > 180
  THEN RAISE EXCEPTION 'Invalid inquiry submission fields.' USING ERRCODE='22023'; END IF;
  INSERT INTO public.inquiry_customer_submissions(customer_user_id,contact_name,company_name,phone,
    customer_reference,salesman_reference,handover_estimator_name,project_name,project_location,description)
  VALUES(auth.uid(),btrim(p_contact_name),btrim(p_company_name),btrim(coalesce(p_phone,'')),
    btrim(coalesce(p_customer_reference,'')),btrim(coalesce(p_salesman_reference,'')),
    btrim(coalesce(p_handover_estimator_name,'')),btrim(p_project_name),btrim(coalesce(p_project_location,'')),btrim(p_description))
  RETURNING inquiry_customer_submissions.id,inquiry_customer_submissions.submission_reference INTO v_id,v_reference;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type) VALUES(v_id,'submitted');
  RETURN QUERY SELECT v_id,v_reference;
END $$;
REVOKE ALL ON FUNCTION public.create_customer_inquiry_submission(text,text,text,text,text,text,text,text,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.create_customer_inquiry_submission(text,text,text,text,text,text,text,text,text) TO authenticated;
NOTIFY pgrst,'reload schema';
COMMIT;

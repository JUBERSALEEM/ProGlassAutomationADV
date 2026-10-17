-- PHASE 9D — CONTROLLED CUSTOMER / ESTIMATOR COMMUNICATION
-- Additive only. Apply after the Phase 9A, repair, and 9B migrations.
BEGIN;

CREATE TABLE IF NOT EXISTS public.inquiry_communications (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  submission_id uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  author_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  visibility text NOT NULL CHECK (visibility IN ('customer','internal')),
  message_kind text NOT NULL DEFAULT 'message' CHECK (message_kind IN ('message','missing_information_request')),
  request_status text CHECK (request_status IS NULL OR request_status IN ('open','resolved')),
  body text NOT NULL CHECK (char_length(btrim(body)) BETWEEN 1 AND 4000),
  created_at timestamptz NOT NULL DEFAULT now(),
  resolved_at timestamptz,
  resolved_by uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  CHECK ((message_kind='missing_information_request' AND request_status IS NOT NULL) OR (message_kind='message' AND request_status IS NULL)),
  CHECK ((request_status='resolved' AND resolved_at IS NOT NULL AND resolved_by IS NOT NULL) OR (request_status IS DISTINCT FROM 'resolved' AND resolved_at IS NULL AND resolved_by IS NULL))
);
CREATE INDEX IF NOT EXISTS ix_inquiry_communications_submission_created ON public.inquiry_communications(submission_id,created_at,id);

CREATE TABLE IF NOT EXISTS public.inquiry_communication_events (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  submission_id uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  communication_id uuid REFERENCES public.inquiry_communications(id) ON DELETE RESTRICT,
  actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  event_type text NOT NULL CHECK (event_type IN ('message_created','request_resolved')),
  visibility text NOT NULL CHECK (visibility IN ('customer','internal')),
  occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_inquiry_communication_events_submission ON public.inquiry_communication_events(submission_id,occurred_at,id);

ALTER TABLE public.inquiry_communications ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_communication_events ENABLE ROW LEVEL SECURITY;
REVOKE INSERT,UPDATE,DELETE ON public.inquiry_communications FROM authenticated;
REVOKE INSERT,UPDATE,DELETE ON public.inquiry_communication_events FROM authenticated;

DROP POLICY IF EXISTS "Authorized participants read inquiry communications" ON public.inquiry_communications;
CREATE POLICY "Authorized participants read inquiry communications" ON public.inquiry_communications FOR SELECT TO authenticated USING (
  public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor'])
  OR public.is_active_inquiry_estimator_assignment(submission_id)
  OR (visibility='customer' AND EXISTS (SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=public.inquiry_communications.submission_id AND s.customer_user_id=auth.uid()))
);
DROP POLICY IF EXISTS "Admins and auditors read communication audit" ON public.inquiry_communication_events;
CREATE POLICY "Admins and auditors read communication audit" ON public.inquiry_communication_events FOR SELECT TO authenticated USING (
  public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor'])
);

CREATE OR REPLACE FUNCTION public.post_inquiry_communication(p_submission_id uuid,p_visibility text,p_message_kind text,p_body text)
RETURNS public.inquiry_communications LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_customer boolean;v_internal boolean;v_row public.inquiry_communications;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501';END IF;
  IF p_visibility NOT IN ('customer','internal') OR p_message_kind NOT IN ('message','missing_information_request') OR char_length(btrim(coalesce(p_body,''))) NOT BETWEEN 1 AND 4000 THEN RAISE EXCEPTION 'Invalid communication.' USING ERRCODE='22023';END IF;
  SELECT EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=p_submission_id AND s.customer_user_id=auth.uid() AND s.upload_state='complete') INTO v_customer;
  v_internal:=public.has_inquiry_role(ARRAY['inquiry_admin']) OR public.is_active_inquiry_estimator_assignment(p_submission_id);
  IF NOT v_customer AND NOT v_internal THEN RAISE EXCEPTION 'Inquiry not found.' USING ERRCODE='P0002';END IF;
  IF v_customer AND (p_visibility<>'customer' OR p_message_kind<>'message') THEN RAISE EXCEPTION 'Customers may only send customer-visible messages.' USING ERRCODE='42501';END IF;
  IF p_message_kind='missing_information_request' AND p_visibility<>'customer' THEN RAISE EXCEPTION 'Information requests must be customer visible.' USING ERRCODE='22023';END IF;
  INSERT INTO public.inquiry_communications(submission_id,author_user_id,visibility,message_kind,request_status,body)
  VALUES(p_submission_id,auth.uid(),p_visibility,p_message_kind,CASE WHEN p_message_kind='missing_information_request' THEN 'open' END,btrim(p_body)) RETURNING * INTO v_row;
  INSERT INTO public.inquiry_communication_events(submission_id,communication_id,actor_user_id,event_type,visibility) VALUES(p_submission_id,v_row.id,auth.uid(),'message_created',p_visibility);
  RETURN v_row;
END $$;
REVOKE ALL ON FUNCTION public.post_inquiry_communication(uuid,text,text,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.post_inquiry_communication(uuid,text,text,text) TO authenticated;

CREATE OR REPLACE FUNCTION public.resolve_inquiry_information_request(p_communication_id uuid)
RETURNS public.inquiry_communications LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_row public.inquiry_communications;v_allowed boolean;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501';END IF;
  SELECT * INTO v_row FROM public.inquiry_communications WHERE id=p_communication_id AND message_kind='missing_information_request' FOR UPDATE;
  IF v_row.id IS NULL THEN RAISE EXCEPTION 'Request not found.' USING ERRCODE='P0002';END IF;
  v_allowed:=public.has_inquiry_role(ARRAY['inquiry_admin']) OR public.is_active_inquiry_estimator_assignment(v_row.submission_id)
    OR EXISTS(SELECT 1 FROM public.inquiry_customer_submissions s WHERE s.id=v_row.submission_id AND s.customer_user_id=auth.uid() AND v_row.visibility='customer');
  IF NOT v_allowed THEN RAISE EXCEPTION 'Request not found.' USING ERRCODE='P0002';END IF;
  IF v_row.request_status='open' THEN
    UPDATE public.inquiry_communications SET request_status='resolved',resolved_at=now(),resolved_by=auth.uid() WHERE id=v_row.id RETURNING * INTO v_row;
    INSERT INTO public.inquiry_communication_events(submission_id,communication_id,actor_user_id,event_type,visibility) VALUES(v_row.submission_id,v_row.id,auth.uid(),'request_resolved',v_row.visibility);
  END IF;
  RETURN v_row;
END $$;
REVOKE ALL ON FUNCTION public.resolve_inquiry_information_request(uuid) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.resolve_inquiry_information_request(uuid) TO authenticated;

NOTIFY pgrst,'reload schema';
COMMIT;

-- PHASE 8B — INTERNAL REVIEW OF WEBSITE CUSTOMER INQUIRIES
-- Additive migration. Apply only after all Phase 8A migrations.
BEGIN;

ALTER TABLE public.inquiry_customer_submissions
  ADD COLUMN IF NOT EXISTS estimated_response_at timestamptz,
  ADD COLUMN IF NOT EXISTS operator_note text NOT NULL DEFAULT '';
ALTER TABLE public.inquiry_customer_submissions
  DROP CONSTRAINT IF EXISTS inquiry_customer_submissions_operator_note_check;
ALTER TABLE public.inquiry_customer_submissions
  ADD CONSTRAINT inquiry_customer_submissions_operator_note_check
  CHECK (char_length(operator_note) <= 2000);

CREATE INDEX IF NOT EXISTS idx_inquiry_customer_submissions_review_queue
  ON public.inquiry_customer_submissions(upload_state,status,submitted_at)
  WHERE upload_state='complete';

CREATE OR REPLACE FUNCTION public.review_customer_inquiry_submission(
  p_submission_id uuid,p_status text,p_estimated_response_at timestamptz,p_operator_note text
) RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_old text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'Inquiry admin role required.' USING ERRCODE='42501'; END IF;
  IF p_status NOT IN ('under_review','accepted','rejected','closed')
    OR char_length(btrim(coalesce(p_operator_note,''))) > 2000
  THEN RAISE EXCEPTION 'Invalid review fields.' USING ERRCODE='22023'; END IF;
  SELECT status INTO v_old FROM public.inquiry_customer_submissions
    WHERE id=p_submission_id AND upload_state='complete' FOR UPDATE;
  IF v_old IS NULL THEN RAISE EXCEPTION 'Submission not found.' USING ERRCODE='P0002'; END IF;
  IF v_old IN ('rejected','closed') AND p_status<>v_old
  THEN RAISE EXCEPTION 'Closed review state cannot be reopened.' USING ERRCODE='22023'; END IF;
  UPDATE public.inquiry_customer_submissions SET
    status=p_status,status_updated_at=now(),estimated_response_at=p_estimated_response_at,
    operator_note=btrim(coalesce(p_operator_note,'')),
    accepted_at=CASE WHEN p_status='accepted' THEN coalesce(accepted_at,now()) ELSE accepted_at END,
    closed_at=CASE WHEN p_status='closed' THEN coalesce(closed_at,now()) ELSE closed_at END
  WHERE id=p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details,customer_visible)
  VALUES(p_submission_id,'status_changed',jsonb_build_object(
    'from',v_old,'to',p_status,'estimated_response_at',p_estimated_response_at,
    'operator_note',btrim(coalesce(p_operator_note,''))),true);
END $$;
REVOKE ALL ON FUNCTION public.review_customer_inquiry_submission(uuid,text,timestamptz,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.review_customer_inquiry_submission(uuid,text,timestamptz,text) TO authenticated;

NOTIFY pgrst,'reload schema';
COMMIT;

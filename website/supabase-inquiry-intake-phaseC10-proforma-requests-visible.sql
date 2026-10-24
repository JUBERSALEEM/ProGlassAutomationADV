-- PHASE C10: A PROFORMA INVOICE REQUEST REACHES THE DESK, MARKED FOR APPROVAL
--
-- C9 parked a proforma invoice request in 'under_review', which meant the desk
-- could not see it at all until a coordinator accepted it. The decision is the
-- opposite: the request and its files arrive like any other inquiry, and the
-- approval simply records that a coordinator has looked at the priced document.
--
-- What changes, and nothing else:
--
--   1. finalize_customer_inquiry_submission marks every submission 'submitted'.
--      A proforma invoice request is no longer diverted to 'under_review'.
--   2. The 'submitted' event still records the type, and now records
--      'approval_required': true for a proforma invoice request, false for
--      everything else. That is the flag the desk and the website read.
--   3. Nothing else moves. The delivery gate still accepts 'submitted' or
--      'accepted'; 'under_review' is still a deliberate operator hold and is
--      still not delivered; the bridge's three read policies are unchanged; the
--      acknowledgement contract is unchanged.
--
-- What "approved" means now: review_customer_inquiry_submission sets
-- status='accepted' and accepted_at, so a request is awaiting approval while
-- inquiry_type='proforma_invoice' and status is not accepted/rejected/closed.
-- That is derived, never stored twice, and it survives the desk downloading the
-- submission (status 'downloaded') without anybody approving it.
--
-- Rollback is at the bottom, inside a block comment. It restores the C9 branch,
-- which parks a proforma invoice request in 'under_review'.

BEGIN;

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
  -- Every complete submission is ready for the desktop. A proforma invoice
  -- request is ready too; the flag below is what asks a coordinator to approve
  -- the priced document afterwards.
  UPDATE public.inquiry_customer_submissions
    SET upload_state='complete',finalized_at=now(),submitted_at=now(),status_updated_at=now(),
        status='submitted',
        accepted_at=NULL
    WHERE inquiry_customer_submissions.id=p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(p_submission_id,'submitted',jsonb_build_object(
      'document_count',v_count,
      'inquiry_type',v_submission.inquiry_type,
      'desktop_handoff','ready',
      'approval_required',v_submission.inquiry_type='proforma_invoice'));
  RETURN QUERY SELECT v_submission.id,v_submission.submission_reference;
END $$;;

NOTIFY pgrst,'reload schema';

COMMIT;

/* ---------------------------------------------------------------------------
   ROLLBACK — paste this block in place of everything above this comment.
   It restores the C9 behaviour: a proforma invoice request is finalized in
   under_review and is not handed to the desktop until a coordinator accepts it.
   It is idempotent and rewrites no row; rows finalized after this migration are
   not moved by it.
   ---------------------------------------------------------------------------

BEGIN;

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

COMMIT;
*/

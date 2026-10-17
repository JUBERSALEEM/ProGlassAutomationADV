-- PHASE 9A REPAIR — QUALIFY OUTER SUBMISSION ID IN ESTIMATOR RLS
-- Required only where the original Phase 9A migration was applied before this repair.
BEGIN;

DROP POLICY IF EXISTS "Assigned estimators read complete submissions" ON public.inquiry_customer_submissions;
CREATE POLICY "Assigned estimators read complete submissions"
ON public.inquiry_customer_submissions FOR SELECT TO authenticated
USING (
  upload_state='complete'
  AND public.has_inquiry_role(ARRAY['inquiry_estimator'])
  AND EXISTS (
    SELECT 1
    FROM public.inquiry_estimators e
    JOIN public.inquiry_estimator_assignments a ON a.estimator_user_id=e.user_id
    WHERE a.submission_id=public.inquiry_customer_submissions.id
      AND a.estimator_user_id=auth.uid()
      AND a.active
      AND e.active
  )
);

NOTIFY pgrst,'reload schema';
COMMIT;

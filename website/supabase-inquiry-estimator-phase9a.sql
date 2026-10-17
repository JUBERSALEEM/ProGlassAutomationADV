-- PHASE 9A — ESTIMATOR IDENTITY, ASSIGNMENT, AND OWNERSHIP
-- Additive migration. Apply after all Phase 8 migrations.
BEGIN;

ALTER TABLE public.inquiry_operator_roles
  DROP CONSTRAINT IF EXISTS inquiry_operator_roles_role_check;
ALTER TABLE public.inquiry_operator_roles
  ADD CONSTRAINT inquiry_operator_roles_role_check
  CHECK (role IN ('inquiry_viewer','inquiry_auditor','inquiry_admin','inquiry_bridge','inquiry_estimator'));

CREATE TABLE IF NOT EXISTS public.inquiry_estimators (
  user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE RESTRICT,
  estimator_code text NOT NULL UNIQUE CHECK (estimator_code ~ '^[A-Z0-9][A-Z0-9_-]{1,29}$'),
  display_name text NOT NULL CHECK (char_length(display_name) BETWEEN 1 AND 120),
  active boolean NOT NULL DEFAULT true,
  created_by uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.inquiry_estimator_assignments (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  assignment_group_id uuid NOT NULL,
  submission_id uuid NOT NULL REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  estimator_user_id uuid NOT NULL REFERENCES public.inquiry_estimators(user_id) ON DELETE RESTRICT,
  assignment_role text NOT NULL CHECK (assignment_role IN ('primary','collaborator')),
  active boolean NOT NULL DEFAULT true,
  reason text NOT NULL DEFAULT '' CHECK (char_length(reason) <= 500),
  assigned_by uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  assigned_at timestamptz NOT NULL DEFAULT now(),
  ended_by uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  ended_at timestamptz,
  CHECK ((active AND ended_by IS NULL AND ended_at IS NULL) OR
         (NOT active AND ended_by IS NOT NULL AND ended_at IS NOT NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_inquiry_estimator_active_member
  ON public.inquiry_estimator_assignments(submission_id,estimator_user_id) WHERE active;
CREATE UNIQUE INDEX IF NOT EXISTS uq_inquiry_estimator_active_primary
  ON public.inquiry_estimator_assignments(submission_id) WHERE active AND assignment_role='primary';
CREATE INDEX IF NOT EXISTS idx_inquiry_estimator_active_queue
  ON public.inquiry_estimator_assignments(estimator_user_id,assigned_at DESC) WHERE active;

CREATE TABLE IF NOT EXISTS public.inquiry_estimator_events (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  submission_id uuid REFERENCES public.inquiry_customer_submissions(id) ON DELETE RESTRICT,
  estimator_user_id uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  event_type text NOT NULL CHECK (event_type IN ('estimator_registered','estimator_updated','assignment_set','assignment_ended')),
  details jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details)='object'),
  occurred_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE public.inquiry_estimators ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_estimator_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_estimator_events ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.inquiry_estimators,public.inquiry_estimator_assignments,public.inquiry_estimator_events FROM PUBLIC,anon;
REVOKE INSERT,UPDATE,DELETE ON public.inquiry_estimators,public.inquiry_estimator_assignments,public.inquiry_estimator_events FROM authenticated;
GRANT SELECT ON public.inquiry_estimators,public.inquiry_estimator_assignments,public.inquiry_estimator_events TO authenticated;

CREATE POLICY "Approved operators read estimator directory" ON public.inquiry_estimators FOR SELECT TO authenticated
USING (public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor','inquiry_estimator']));
CREATE POLICY "Admins auditors and assigned estimators read assignments" ON public.inquiry_estimator_assignments FOR SELECT TO authenticated
USING (public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor']) OR
  (estimator_user_id=auth.uid() AND public.has_inquiry_role(ARRAY['inquiry_estimator']) AND
   EXISTS(SELECT 1 FROM public.inquiry_estimators e WHERE e.user_id=auth.uid() AND e.active)));
CREATE POLICY "Admins auditors and relevant estimators read assignment events" ON public.inquiry_estimator_events FOR SELECT TO authenticated
USING (public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor']) OR
  (estimator_user_id=auth.uid() AND public.has_inquiry_role(ARRAY['inquiry_estimator']) AND
   EXISTS(SELECT 1 FROM public.inquiry_estimators e WHERE e.user_id=auth.uid() AND e.active)));

CREATE OR REPLACE FUNCTION public.register_inquiry_estimator(
  p_email text,p_estimator_code text,p_display_name text,p_active boolean DEFAULT true)
RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,auth,pg_temp AS $$
DECLARE v_user_id uuid; v_role text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN RAISE EXCEPTION 'Inquiry admin role required.' USING ERRCODE='42501'; END IF;
  SELECT id INTO v_user_id FROM auth.users WHERE lower(email)=lower(btrim(p_email));
  IF v_user_id IS NULL THEN RAISE EXCEPTION 'Auth user not found.' USING ERRCODE='P0002'; END IF;
  IF (SELECT count(*) FROM auth.users WHERE lower(email)=lower(btrim(p_email)))<>1 THEN RAISE EXCEPTION 'Email is not unique.' USING ERRCODE='21000'; END IF;
  IF upper(btrim(p_estimator_code)) !~ '^[A-Z0-9][A-Z0-9_-]{1,29}$' OR char_length(btrim(p_display_name)) NOT BETWEEN 1 AND 120
    THEN RAISE EXCEPTION 'Invalid estimator profile.' USING ERRCODE='22023'; END IF;
  SELECT role INTO v_role FROM public.inquiry_operator_roles WHERE user_id=v_user_id FOR UPDATE;
  IF v_role IS NOT NULL AND v_role<>'inquiry_estimator' THEN RAISE EXCEPTION 'User already has a different operator role.' USING ERRCODE='23505'; END IF;
  INSERT INTO public.inquiry_operator_roles(user_id,role,enabled,assigned_by,updated_at)
    VALUES(v_user_id,'inquiry_estimator',p_active,auth.uid(),now())
    ON CONFLICT(user_id) DO UPDATE SET enabled=excluded.enabled,updated_at=now();
  INSERT INTO public.inquiry_estimators(user_id,estimator_code,display_name,active,created_by,updated_by)
    VALUES(v_user_id,upper(btrim(p_estimator_code)),btrim(p_display_name),p_active,auth.uid(),auth.uid())
    ON CONFLICT(user_id) DO UPDATE SET estimator_code=excluded.estimator_code,display_name=excluded.display_name,
      active=excluded.active,updated_by=auth.uid(),updated_at=now();
  INSERT INTO public.inquiry_estimator_events(estimator_user_id,actor_user_id,event_type,details)
    VALUES(v_user_id,auth.uid(),CASE WHEN v_role IS NULL THEN 'estimator_registered' ELSE 'estimator_updated' END,
      jsonb_build_object('estimator_code',upper(btrim(p_estimator_code)),'display_name',btrim(p_display_name),'active',p_active));
  RETURN v_user_id;
END $$;

CREATE OR REPLACE FUNCTION public.set_customer_inquiry_estimators(
  p_submission_id uuid,p_primary_estimator uuid,p_collaborators uuid[] DEFAULT '{}'::uuid[],p_reason text DEFAULT '')
RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_group uuid:=gen_random_uuid(); v_estimator uuid; v_collaborators uuid[]:=coalesce(p_collaborators,'{}'::uuid[]);
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN RAISE EXCEPTION 'Inquiry admin role required.' USING ERRCODE='42501'; END IF;
  IF char_length(btrim(coalesce(p_reason,'')))>500 THEN RAISE EXCEPTION 'Assignment reason is too long.' USING ERRCODE='22023'; END IF;
  IF NOT EXISTS(SELECT 1 FROM public.inquiry_customer_submissions WHERE id=p_submission_id AND upload_state='complete')
    THEN RAISE EXCEPTION 'Complete submission not found.' USING ERRCODE='P0002'; END IF;
  IF p_primary_estimator=ANY(v_collaborators) OR cardinality(v_collaborators)<>cardinality(ARRAY(SELECT DISTINCT unnest(v_collaborators)))
    THEN RAISE EXCEPTION 'Estimator assignment contains duplicates.' USING ERRCODE='22023'; END IF;
  IF NOT EXISTS(SELECT 1 FROM public.inquiry_estimators e JOIN public.inquiry_operator_roles r ON r.user_id=e.user_id
    WHERE e.user_id=p_primary_estimator AND e.active AND r.role='inquiry_estimator' AND r.enabled)
    THEN RAISE EXCEPTION 'Primary estimator is not active.' USING ERRCODE='22023'; END IF;
  FOREACH v_estimator IN ARRAY v_collaborators LOOP
    IF NOT EXISTS(SELECT 1 FROM public.inquiry_estimators e JOIN public.inquiry_operator_roles r ON r.user_id=e.user_id
      WHERE e.user_id=v_estimator AND e.active AND r.role='inquiry_estimator' AND r.enabled)
      THEN RAISE EXCEPTION 'Collaborating estimator is not active.' USING ERRCODE='22023'; END IF;
  END LOOP;
  UPDATE public.inquiry_estimator_assignments SET active=false,ended_by=auth.uid(),ended_at=now()
    WHERE submission_id=p_submission_id AND active;
  INSERT INTO public.inquiry_estimator_assignments(assignment_group_id,submission_id,estimator_user_id,assignment_role,reason,assigned_by)
    VALUES(v_group,p_submission_id,p_primary_estimator,'primary',btrim(coalesce(p_reason,'')),auth.uid());
  FOREACH v_estimator IN ARRAY v_collaborators LOOP
    INSERT INTO public.inquiry_estimator_assignments(assignment_group_id,submission_id,estimator_user_id,assignment_role,reason,assigned_by)
      VALUES(v_group,p_submission_id,v_estimator,'collaborator',btrim(coalesce(p_reason,'')),auth.uid());
  END LOOP;
  INSERT INTO public.inquiry_estimator_events(submission_id,actor_user_id,event_type,details)
    VALUES(p_submission_id,auth.uid(),'assignment_set',jsonb_build_object('assignment_group_id',v_group,'primary_estimator',p_primary_estimator,'collaborators',v_collaborators,'reason',btrim(coalesce(p_reason,''))));
  RETURN v_group;
END $$;

CREATE OR REPLACE FUNCTION public.end_customer_inquiry_estimator_assignment(p_submission_id uuid,p_reason text DEFAULT '')
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_count integer;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN RAISE EXCEPTION 'Inquiry admin role required.' USING ERRCODE='42501'; END IF;
  IF char_length(btrim(coalesce(p_reason,'')))>500 THEN RAISE EXCEPTION 'Assignment reason is too long.' USING ERRCODE='22023'; END IF;
  UPDATE public.inquiry_estimator_assignments SET active=false,ended_by=auth.uid(),ended_at=now()
    WHERE submission_id=p_submission_id AND active;
  GET DIAGNOSTICS v_count=ROW_COUNT;
  IF v_count=0 THEN RAISE EXCEPTION 'Active assignment not found.' USING ERRCODE='P0002'; END IF;
  INSERT INTO public.inquiry_estimator_events(submission_id,actor_user_id,event_type,details)
    VALUES(p_submission_id,auth.uid(),'assignment_ended',jsonb_build_object('reason',btrim(coalesce(p_reason,'')),'members_ended',v_count));
END $$;

REVOKE ALL ON FUNCTION public.register_inquiry_estimator(text,text,text,boolean),public.set_customer_inquiry_estimators(uuid,uuid,uuid[],text),public.end_customer_inquiry_estimator_assignment(uuid,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.register_inquiry_estimator(text,text,text,boolean),public.set_customer_inquiry_estimators(uuid,uuid,uuid[],text),public.end_customer_inquiry_estimator_assignment(uuid,text) TO authenticated;

CREATE POLICY "Assigned estimators read complete submissions" ON public.inquiry_customer_submissions FOR SELECT TO authenticated
USING(upload_state='complete' AND public.has_inquiry_role(ARRAY['inquiry_estimator']) AND
  EXISTS(SELECT 1 FROM public.inquiry_estimators e JOIN public.inquiry_estimator_assignments a ON a.estimator_user_id=e.user_id
    WHERE a.submission_id=id AND a.estimator_user_id=auth.uid() AND a.active AND e.active));
CREATE POLICY "Assigned estimators read submission documents" ON public.inquiry_customer_documents FOR SELECT TO authenticated
USING(public.has_inquiry_role(ARRAY['inquiry_estimator']) AND EXISTS(SELECT 1 FROM public.inquiry_estimators e
  JOIN public.inquiry_estimator_assignments a ON a.estimator_user_id=e.user_id
  WHERE a.submission_id=inquiry_customer_documents.submission_id AND a.estimator_user_id=auth.uid() AND a.active AND e.active));
CREATE POLICY "Assigned estimators read intake objects" ON storage.objects FOR SELECT TO authenticated
USING(bucket_id='inquiry-intake-documents' AND public.has_inquiry_role(ARRAY['inquiry_estimator']) AND EXISTS(
  SELECT 1 FROM public.inquiry_customer_documents d JOIN public.inquiry_estimator_assignments a ON a.submission_id=d.submission_id
  JOIN public.inquiry_estimators e ON e.user_id=a.estimator_user_id
  WHERE d.storage_object_key=name AND a.estimator_user_id=auth.uid() AND a.active AND e.active));

NOTIFY pgrst,'reload schema';
COMMIT;

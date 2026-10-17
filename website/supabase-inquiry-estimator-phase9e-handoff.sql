-- PHASE 9E — ESTIMATOR-AWARE WINDOWS INBOX HANDOFF
-- Additive only. Apply after Phase 8C and Phase 9A/9B.
BEGIN;

ALTER TABLE public.inquiry_customer_deliveries
  ADD COLUMN IF NOT EXISTS routing_key text,
  ADD COLUMN IF NOT EXISTS primary_estimator_code text,
  ADD COLUMN IF NOT EXISTS assignment_snapshot jsonb;
ALTER TABLE public.inquiry_customer_deliveries
  DROP CONSTRAINT IF EXISTS inquiry_customer_deliveries_routing_key_check;
ALTER TABLE public.inquiry_customer_deliveries
  ADD CONSTRAINT inquiry_customer_deliveries_routing_key_check
  CHECK (routing_key IS NULL OR routing_key='unassigned' OR routing_key ~ '^assigned/[A-Z0-9][A-Z0-9_-]{1,29}$');
ALTER TABLE public.inquiry_customer_deliveries
  DROP CONSTRAINT IF EXISTS inquiry_customer_deliveries_assignment_snapshot_check;
ALTER TABLE public.inquiry_customer_deliveries
  ADD CONSTRAINT inquiry_customer_deliveries_assignment_snapshot_check
  CHECK (assignment_snapshot IS NULL OR jsonb_typeof(assignment_snapshot)='array');

CREATE OR REPLACE FUNCTION public.customer_inquiry_assignment_snapshot(p_submission_id uuid)
RETURNS jsonb LANGUAGE sql STABLE SECURITY DEFINER SET search_path=public,pg_temp AS $$
  SELECT coalesce(jsonb_agg(jsonb_build_object(
    'role',a.assignment_role,'estimatorCode',e.estimator_code,'displayName',e.display_name,'assignedAt',a.assigned_at
  ) ORDER BY CASE a.assignment_role WHEN 'primary' THEN 0 ELSE 1 END,e.estimator_code),'[]'::jsonb)
  FROM public.inquiry_estimator_assignments a
  JOIN public.inquiry_estimators e ON e.user_id=a.estimator_user_id
  WHERE a.submission_id=p_submission_id AND a.active AND e.active;
$$;
REVOKE ALL ON FUNCTION public.customer_inquiry_assignment_snapshot(uuid) FROM PUBLIC,anon,authenticated;

CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_deliveries_v2(p_limit integer DEFAULT 25)
RETURNS TABLE(submission_id uuid,submission_reference text,submitted_at timestamptz,status text,
  contact_name text,company_name text,phone text,customer_reference text,attention_name text,
  vat_trn_number text,salesman_reference text,handover_estimator_name text,project_name text,
  project_location text,inquiry_type text,description text,document_count bigint,
  primary_estimator_code text,assignment_snapshot jsonb,routing_key text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501';END IF;
  RETURN QUERY SELECT s.id,s.submission_reference,s.submitted_at,s.status,s.contact_name,s.company_name,
    s.phone,s.customer_reference,s.attention_name,s.vat_trn_number,s.salesman_reference,
    s.handover_estimator_name,s.project_name,s.project_location,s.inquiry_type,s.description,
    count(d.id)::bigint,p.estimator_code,public.customer_inquiry_assignment_snapshot(s.id),
    CASE WHEN p.estimator_code IS NULL THEN 'unassigned' ELSE 'assigned/'||p.estimator_code END
  FROM public.inquiry_customer_submissions s
  LEFT JOIN public.inquiry_customer_documents d ON d.submission_id=s.id AND d.availability='available'
  LEFT JOIN public.inquiry_customer_deliveries x ON x.submission_id=s.id
  LEFT JOIN LATERAL (
    SELECT e.estimator_code FROM public.inquiry_estimator_assignments a
    JOIN public.inquiry_estimators e ON e.user_id=a.estimator_user_id
    WHERE a.submission_id=s.id AND a.active AND a.assignment_role='primary' AND e.active LIMIT 1
  ) p ON true
  WHERE s.upload_state='complete' AND s.status='accepted' AND x.submission_id IS NULL
  GROUP BY s.id,p.estimator_code ORDER BY s.submitted_at,s.id LIMIT greatest(1,least(coalesce(p_limit,25),100));
END $$;

CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery_v2(
  p_submission_id uuid,p_manifest_sha256 text,p_document_count integer,p_routing_key text,p_assignment_snapshot jsonb)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_expected integer;v_current jsonb;v_primary text;v_route text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501';END IF;
  IF p_manifest_sha256 !~ '^[0-9a-f]{64}$' OR p_document_count NOT BETWEEN 0 AND 15 OR jsonb_typeof(p_assignment_snapshot)<>'array' THEN RAISE EXCEPTION 'Invalid acknowledgement.' USING ERRCODE='22023';END IF;
  SELECT count(*) INTO v_expected FROM public.inquiry_customer_documents d JOIN public.inquiry_customer_submissions s ON s.id=d.submission_id
    WHERE s.id=p_submission_id AND s.upload_state='complete' AND s.status='accepted' AND d.availability='available';
  IF v_expected<>p_document_count THEN RAISE EXCEPTION 'Document count changed.' USING ERRCODE='40001';END IF;
  v_current:=public.customer_inquiry_assignment_snapshot(p_submission_id);
  IF v_current<>p_assignment_snapshot THEN RAISE EXCEPTION 'Estimator assignment changed.' USING ERRCODE='40001';END IF;
  SELECT value->>'estimatorCode' INTO v_primary FROM jsonb_array_elements(v_current) value WHERE value->>'role'='primary' LIMIT 1;
  v_route:=CASE WHEN v_primary IS NULL THEN 'unassigned' ELSE 'assigned/'||v_primary END;
  IF p_routing_key IS DISTINCT FROM v_route THEN RAISE EXCEPTION 'Invalid routing key.' USING ERRCODE='22023';END IF;
  IF EXISTS(SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id=p_submission_id) THEN
    IF EXISTS(SELECT 1 FROM public.inquiry_customer_deliveries WHERE submission_id=p_submission_id AND manifest_sha256=p_manifest_sha256 AND document_count=p_document_count AND routing_key=p_routing_key AND assignment_snapshot=p_assignment_snapshot) THEN RETURN;END IF;
    RAISE EXCEPTION 'Conflicting acknowledgement.' USING ERRCODE='23505';
  END IF;
  INSERT INTO public.inquiry_customer_deliveries(submission_id,bridge_user_id,manifest_sha256,document_count,routing_key,primary_estimator_code,assignment_snapshot)
    VALUES(p_submission_id,auth.uid(),p_manifest_sha256,p_document_count,p_routing_key,v_primary,p_assignment_snapshot);
  UPDATE public.inquiry_customer_submissions SET status='downloaded',downloaded_at=coalesce(downloaded_at,now()),status_updated_at=now() WHERE id=p_submission_id AND status='accepted';
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details,customer_visible)
    VALUES(p_submission_id,'inbox_delivered',jsonb_build_object('document_count',p_document_count,'routing_key',p_routing_key,'primary_estimator_code',v_primary),true);
END $$;

REVOKE ALL ON FUNCTION public.plan_customer_inquiry_deliveries_v2(integer),public.acknowledge_customer_inquiry_delivery_v2(uuid,text,integer,text,jsonb) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.plan_customer_inquiry_deliveries_v2(integer),public.acknowledge_customer_inquiry_delivery_v2(uuid,text,integer,text,jsonb) TO authenticated;
NOTIFY pgrst,'reload schema';
COMMIT;

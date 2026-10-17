-- PHASE 8A FOLLOW-UP — CUSTOMER PROFILE AND PROJECTS
-- Additive migration. Apply only after staged-uploads migration verification.
-- Existing inquiry snapshots and documents remain unchanged.
BEGIN;

CREATE TABLE IF NOT EXISTS public.inquiry_customer_profiles (
  customer_user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE RESTRICT,
  contact_name text NOT NULL DEFAULT '' CHECK (char_length(contact_name) <= 120),
  company_name text NOT NULL DEFAULT '' CHECK (char_length(company_name) <= 160),
  phone text NOT NULL DEFAULT '' CHECK (char_length(phone) <= 60),
  vat_trn_number text NOT NULL DEFAULT '' CHECK (char_length(vat_trn_number) <= 50),
  attention_name text NOT NULL DEFAULT '' CHECK (char_length(attention_name) <= 120),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.inquiry_customer_projects (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  project_name text NOT NULL CHECK (char_length(project_name) BETWEEN 1 AND 180),
  project_location text NOT NULL DEFAULT '' CHECK (char_length(project_location) <= 180),
  default_inquiry_type text NOT NULL DEFAULT 'quotation'
    CHECK (default_inquiry_type IN ('quotation','drawing_review','technical','order_handover','other')),
  active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE(customer_user_id,project_name,project_location)
);
CREATE INDEX IF NOT EXISTS idx_inquiry_customer_projects_owner
  ON public.inquiry_customer_projects(customer_user_id,active,project_name);

ALTER TABLE public.inquiry_customer_submissions
  ADD COLUMN IF NOT EXISTS project_id uuid REFERENCES public.inquiry_customer_projects(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS inquiry_type text NOT NULL DEFAULT 'other';
ALTER TABLE public.inquiry_customer_submissions
  DROP CONSTRAINT IF EXISTS inquiry_customer_submissions_inquiry_type_check;
ALTER TABLE public.inquiry_customer_submissions
  ADD CONSTRAINT inquiry_customer_submissions_inquiry_type_check
  CHECK (inquiry_type IN ('quotation','drawing_review','technical','order_handover','other'));
CREATE INDEX IF NOT EXISTS idx_inquiry_customer_submissions_project
  ON public.inquiry_customer_submissions(customer_user_id,project_id,submitted_at DESC);

ALTER TABLE public.inquiry_customer_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_customer_projects ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.inquiry_customer_profiles,public.inquiry_customer_projects FROM PUBLIC,anon;
REVOKE INSERT,UPDATE,DELETE ON TABLE public.inquiry_customer_profiles,public.inquiry_customer_projects FROM authenticated;
GRANT SELECT ON TABLE public.inquiry_customer_profiles,public.inquiry_customer_projects TO authenticated;

DROP POLICY IF EXISTS "Customers read own inquiry profile" ON public.inquiry_customer_profiles;
CREATE POLICY "Customers read own inquiry profile" ON public.inquiry_customer_profiles
  FOR SELECT TO authenticated USING (customer_user_id=auth.uid());
DROP POLICY IF EXISTS "Customers read own inquiry projects" ON public.inquiry_customer_projects;
CREATE POLICY "Customers read own inquiry projects" ON public.inquiry_customer_projects
  FOR SELECT TO authenticated USING (customer_user_id=auth.uid());

CREATE OR REPLACE FUNCTION public.save_customer_inquiry_profile(
  p_contact_name text,p_company_name text,p_phone text,p_vat_trn_number text,p_attention_name text
) RETURNS public.inquiry_customer_profiles
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_profile public.inquiry_customer_profiles%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF char_length(btrim(coalesce(p_contact_name,''))) > 120
    OR char_length(btrim(coalesce(p_company_name,''))) > 160
    OR char_length(btrim(coalesce(p_phone,''))) > 60
    OR char_length(btrim(coalesce(p_vat_trn_number,''))) > 50
    OR char_length(btrim(coalesce(p_attention_name,''))) > 120
  THEN RAISE EXCEPTION 'Invalid customer profile fields.' USING ERRCODE='22023'; END IF;
  INSERT INTO public.inquiry_customer_profiles(customer_user_id,contact_name,company_name,phone,vat_trn_number,attention_name)
  VALUES(auth.uid(),btrim(coalesce(p_contact_name,'')),btrim(coalesce(p_company_name,'')),btrim(coalesce(p_phone,'')),
    btrim(coalesce(p_vat_trn_number,'')),btrim(coalesce(p_attention_name,'')))
  ON CONFLICT(customer_user_id) DO UPDATE SET
    contact_name=excluded.contact_name,company_name=excluded.company_name,phone=excluded.phone,
    vat_trn_number=excluded.vat_trn_number,attention_name=excluded.attention_name,updated_at=now()
  RETURNING * INTO v_profile;
  RETURN v_profile;
END $$;
REVOKE ALL ON FUNCTION public.save_customer_inquiry_profile(text,text,text,text,text) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.save_customer_inquiry_profile(text,text,text,text,text) TO authenticated;

CREATE OR REPLACE FUNCTION public.save_customer_inquiry_project(
  p_project_id uuid,p_project_name text,p_project_location text,p_default_inquiry_type text,p_active boolean DEFAULT true
) RETURNS public.inquiry_customer_projects
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_project public.inquiry_customer_projects%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF char_length(btrim(coalesce(p_project_name,''))) NOT BETWEEN 1 AND 180
    OR char_length(btrim(coalesce(p_project_location,''))) > 180
    OR p_default_inquiry_type NOT IN ('quotation','drawing_review','technical','order_handover','other')
  THEN RAISE EXCEPTION 'Invalid customer project fields.' USING ERRCODE='22023'; END IF;
  IF p_project_id IS NULL THEN
    INSERT INTO public.inquiry_customer_projects(customer_user_id,project_name,project_location,default_inquiry_type,active)
    VALUES(auth.uid(),btrim(p_project_name),btrim(coalesce(p_project_location,'')),p_default_inquiry_type,coalesce(p_active,true))
    RETURNING * INTO v_project;
  ELSE
    UPDATE public.inquiry_customer_projects SET project_name=btrim(p_project_name),
      project_location=btrim(coalesce(p_project_location,'')),default_inquiry_type=p_default_inquiry_type,
      active=coalesce(p_active,true),updated_at=now()
    WHERE id=p_project_id AND customer_user_id=auth.uid() RETURNING * INTO v_project;
    IF v_project.id IS NULL THEN RAISE EXCEPTION 'Project not found.' USING ERRCODE='P0002'; END IF;
  END IF;
  RETURN v_project;
END $$;
REVOKE ALL ON FUNCTION public.save_customer_inquiry_project(uuid,text,text,text,boolean) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.save_customer_inquiry_project(uuid,text,text,text,boolean) TO authenticated;

CREATE OR REPLACE FUNCTION public.create_customer_inquiry_draft_v2(
  p_contact_name text,p_company_name text,p_phone text,p_customer_reference text,
  p_attention_name text,p_vat_trn_number text,p_salesman_reference text,p_handover_estimator_name text,
  p_project_id uuid,p_project_name text,p_project_location text,p_inquiry_type text,
  p_description text,p_expected_document_count integer
) RETURNS TABLE(id uuid,submission_reference text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=public,pg_temp AS $$
DECLARE v_id uuid; v_reference text; v_project public.inquiry_customer_projects%ROWTYPE;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF p_project_id IS NOT NULL THEN
    SELECT * INTO v_project FROM public.inquiry_customer_projects
      WHERE inquiry_customer_projects.id=p_project_id AND customer_user_id=auth.uid() AND active;
    IF v_project.id IS NULL THEN RAISE EXCEPTION 'Project not found.' USING ERRCODE='P0002'; END IF;
    p_project_name:=v_project.project_name; p_project_location:=v_project.project_location;
  END IF;
  IF p_expected_document_count NOT BETWEEN 0 AND 15
    OR char_length(btrim(coalesce(p_contact_name,''))) NOT BETWEEN 1 AND 120
    OR char_length(btrim(coalesce(p_company_name,''))) NOT BETWEEN 1 AND 160
    OR char_length(btrim(coalesce(p_project_name,''))) NOT BETWEEN 1 AND 180
    OR char_length(btrim(coalesce(p_project_location,''))) > 180
    OR char_length(btrim(coalesce(p_description,''))) NOT BETWEEN 10 AND 5000
    OR p_inquiry_type NOT IN ('quotation','drawing_review','technical','order_handover','other')
  THEN RAISE EXCEPTION 'Invalid inquiry draft fields.' USING ERRCODE='22023'; END IF;
  INSERT INTO public.inquiry_customer_submissions(customer_user_id,contact_name,company_name,phone,customer_reference,
    attention_name,vat_trn_number,salesman_reference,handover_estimator_name,project_id,project_name,project_location,
    inquiry_type,description,upload_state,expected_document_count,finalized_at)
  VALUES(auth.uid(),btrim(p_contact_name),btrim(p_company_name),btrim(coalesce(p_phone,'')),btrim(coalesce(p_customer_reference,'')),
    btrim(coalesce(p_attention_name,'')),btrim(coalesce(p_vat_trn_number,'')),btrim(coalesce(p_salesman_reference,'')),
    btrim(coalesce(p_handover_estimator_name,'')),p_project_id,btrim(p_project_name),btrim(coalesce(p_project_location,'')),
    p_inquiry_type,btrim(p_description),'draft',p_expected_document_count,NULL)
  RETURNING inquiry_customer_submissions.id,inquiry_customer_submissions.submission_reference INTO v_id,v_reference;
  INSERT INTO public.inquiry_customer_events(submission_id,event_type,details)
    VALUES(v_id,'draft_created',jsonb_build_object('expected_document_count',p_expected_document_count,'project_id',p_project_id,'inquiry_type',p_inquiry_type));
  RETURN QUERY SELECT v_id,v_reference;
END $$;
REVOKE ALL ON FUNCTION public.create_customer_inquiry_draft_v2(text,text,text,text,text,text,text,text,uuid,text,text,text,text,integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.create_customer_inquiry_draft_v2(text,text,text,text,text,text,text,text,uuid,text,text,text,text,integer) TO authenticated;

NOTIFY pgrst,'reload schema';
COMMIT;

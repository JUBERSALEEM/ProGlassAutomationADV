-- =====================================================================
-- PROGLASS AUTOMATION — PRIVATE INQUIRY DOCUMENT PROJECTION (PHASE 3)
-- Draft additive migration. Review in a non-production Supabase project
-- before applying. This file is NOT run automatically by the website.
--
-- V1 boundary: read-only website, inquiry attachments only. Desktop/local
-- data remains authoritative. No customer access and no browser uploads.
-- =====================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Internal roles are database-owned, not browser-editable auth metadata.
CREATE TABLE IF NOT EXISTS public.inquiry_operator_roles (
  user_id       uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  role          text NOT NULL CHECK (role IN ('inquiry_viewer', 'inquiry_auditor', 'inquiry_admin', 'inquiry_bridge')),
  enabled       boolean NOT NULL DEFAULT true,
  assigned_by   uuid REFERENCES auth.users(id) ON DELETE SET NULL,
  assigned_at   timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.has_inquiry_role(p_roles text[])
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
  SELECT auth.uid() IS NOT NULL AND EXISTS (
    SELECT 1 FROM public.inquiry_operator_roles r
    WHERE r.user_id = auth.uid() AND r.enabled AND r.role = ANY(p_roles)
  );
$$;

REVOKE ALL ON FUNCTION public.has_inquiry_role(text[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.has_inquiry_role(text[]) TO authenticated;

CREATE TABLE IF NOT EXISTS public.inquiry_sources (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source_dataset_id     uuid NOT NULL UNIQUE,
  display_name          text NOT NULL DEFAULT '',
  bridge_user_id        uuid NOT NULL UNIQUE REFERENCES auth.users(id) ON DELETE RESTRICT,
  enabled               boolean NOT NULL DEFAULT true,
  last_complete_scan_at timestamptz,
  last_success_at       timestamptz,
  last_source_revision  bigint NOT NULL DEFAULT 0 CHECK (last_source_revision >= 0),
  created_at            timestamptz NOT NULL DEFAULT now(),
  updated_at            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.inquiry_sync_runs (
  id                uuid PRIMARY KEY,
  source_id         uuid NOT NULL REFERENCES public.inquiry_sources(id) ON DELETE RESTRICT,
  status            text NOT NULL CHECK (status IN ('running', 'completed', 'failed', 'partial')),
  source_revision   bigint NOT NULL CHECK (source_revision >= 0),
  complete_scan     boolean NOT NULL DEFAULT false,
  inquiries_seen    integer NOT NULL DEFAULT 0 CHECK (inquiries_seen >= 0),
  documents_seen    integer NOT NULL DEFAULT 0 CHECK (documents_seen >= 0),
  warnings_count    integer NOT NULL DEFAULT 0 CHECK (warnings_count >= 0),
  error_code        text,
  started_at        timestamptz NOT NULL DEFAULT now(),
  finished_at       timestamptz,
  actor_id          uuid NOT NULL DEFAULT auth.uid() REFERENCES auth.users(id) ON DELETE RESTRICT,
  UNIQUE (source_id, source_revision)
);

CREATE TABLE IF NOT EXISTS public.inquiries (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source_id             uuid NOT NULL REFERENCES public.inquiry_sources(id) ON DELETE RESTRICT,
  source_inquiry_id     bigint NOT NULL CHECK (source_inquiry_id > 0),
  inquiry_number        text NOT NULL CHECK (inquiry_number ~ '^INQ-[0-9]{4}-[0-9]{4,}$'),
  inquiry_reference     text NOT NULL DEFAULT '',
  customer_name         text NOT NULL DEFAULT '',
  customer_location     text NOT NULL DEFAULT '',
  project_name          text NOT NULL DEFAULT '',
  project_description   text NOT NULL DEFAULT '',
  salesman_name         text NOT NULL DEFAULT '',
  prepared_by_name      text NOT NULL DEFAULT '',
  status                text NOT NULL CHECK (status IN ('Received', 'Prepared', 'Sent', 'Confirmed', 'Hold', 'Cancelled')),
  priority              text NOT NULL DEFAULT 'Normal' CHECK (priority IN ('Low', 'Normal', 'High', 'Urgent')),
  inquiry_value         numeric(18,2) NOT NULL DEFAULT 0 CHECK (inquiry_value >= 0),
  received_at           timestamptz NOT NULL,
  prepared_at           timestamptz,
  sent_at               timestamptz,
  confirmed_at          timestamptz,
  source_updated_at     timestamptz NOT NULL,
  proforma_number       text NOT NULL DEFAULT '',
  job_order_number      text NOT NULL DEFAULT '',
  source_revision       bigint NOT NULL CHECK (source_revision >= 0),
  source_fingerprint    text NOT NULL CHECK (source_fingerprint ~ '^[a-f0-9]{64}$'),
  availability          text NOT NULL DEFAULT 'available' CHECK (availability IN ('available', 'source_missing', 'tombstoned')),
  last_observed_at      timestamptz NOT NULL DEFAULT now(),
  published_at          timestamptz NOT NULL DEFAULT now(),
  updated_at            timestamptz NOT NULL DEFAULT now(),
  UNIQUE (source_id, source_inquiry_id),
  UNIQUE (source_id, inquiry_number)
);

CREATE INDEX IF NOT EXISTS idx_inquiries_number ON public.inquiries (inquiry_number);
CREATE INDEX IF NOT EXISTS idx_inquiries_status_updated ON public.inquiries (status, source_updated_at DESC);
CREATE INDEX IF NOT EXISTS idx_inquiries_customer_search ON public.inquiries (lower(customer_name));
CREATE INDEX IF NOT EXISTS idx_inquiries_project_search ON public.inquiries (lower(project_name));

CREATE TABLE IF NOT EXISTS public.inquiry_documents (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  inquiry_id            uuid NOT NULL REFERENCES public.inquiries(id) ON DELETE RESTRICT,
  source_attachment_id  bigint NOT NULL CHECK (source_attachment_id > 0),
  category              text NOT NULL DEFAULT 'inquiry_attachment' CHECK (category = 'inquiry_attachment'),
  original_filename     text NOT NULL CHECK (char_length(original_filename) BETWEEN 1 AND 260),
  extension             text NOT NULL CHECK (extension ~ '^\.[a-z0-9]{1,10}$'),
  availability          text NOT NULL DEFAULT 'available' CHECK (availability IN ('available', 'source_missing', 'quarantined')),
  current_version_id    uuid,
  source_revision       bigint NOT NULL CHECK (source_revision >= 0),
  first_published_at    timestamptz NOT NULL DEFAULT now(),
  last_observed_at      timestamptz NOT NULL DEFAULT now(),
  updated_at            timestamptz NOT NULL DEFAULT now(),
  UNIQUE (inquiry_id, source_attachment_id)
);

CREATE INDEX IF NOT EXISTS idx_inquiry_documents_inquiry ON public.inquiry_documents (inquiry_id, availability);

CREATE TABLE IF NOT EXISTS public.inquiry_document_versions (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  document_id           uuid NOT NULL REFERENCES public.inquiry_documents(id) ON DELETE RESTRICT,
  content_sha256        text NOT NULL CHECK (content_sha256 ~ '^[a-f0-9]{64}$'),
  byte_size             bigint NOT NULL CHECK (byte_size BETWEEN 1 AND 10485760),
  detected_mime         text NOT NULL,
  storage_bucket        text NOT NULL DEFAULT 'inquiry-documents' CHECK (storage_bucket = 'inquiry-documents'),
  storage_object_key    text NOT NULL UNIQUE CHECK (
    storage_object_key ~ '^inquiry-documents/[0-9a-f-]{36}/[0-9a-f-]{36}/[0-9a-f-]{36}/[0-9a-f-]{36}$'
  ),
  source_uploaded_at    timestamptz,
  source_modified_at    timestamptz,
  source_revision       bigint NOT NULL CHECK (source_revision >= 0),
  state                 text NOT NULL DEFAULT 'available' CHECK (state IN ('available', 'superseded', 'quarantined')),
  published_at          timestamptz NOT NULL DEFAULT now(),
  UNIQUE (document_id, content_sha256)
);

ALTER TABLE public.inquiry_documents
  DROP CONSTRAINT IF EXISTS inquiry_documents_current_version_fk;
ALTER TABLE public.inquiry_documents
  ADD CONSTRAINT inquiry_documents_current_version_fk
  FOREIGN KEY (current_version_id) REFERENCES public.inquiry_document_versions(id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS idx_inquiry_document_versions_document ON public.inquiry_document_versions (document_id, published_at DESC);

CREATE TABLE IF NOT EXISTS public.inquiry_document_events (
  id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  source_id       uuid REFERENCES public.inquiry_sources(id) ON DELETE RESTRICT,
  inquiry_id      uuid REFERENCES public.inquiries(id) ON DELETE RESTRICT,
  document_id     uuid REFERENCES public.inquiry_documents(id) ON DELETE RESTRICT,
  version_id      uuid REFERENCES public.inquiry_document_versions(id) ON DELETE RESTRICT,
  sync_run_id     uuid REFERENCES public.inquiry_sync_runs(id) ON DELETE RESTRICT,
  event_type      text NOT NULL CHECK (event_type IN (
                    'inquiry_published', 'inquiry_updated', 'inquiry_tombstoned',
                    'document_published', 'document_superseded', 'document_source_missing',
                    'document_quarantined', 'document_access_authorized', 'sync_completed', 'sync_failed'
                  )),
  actor_id        uuid REFERENCES auth.users(id) ON DELETE SET NULL,
  details         jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
  created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_inquiry_document_events_inquiry_created ON public.inquiry_document_events (inquiry_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_inquiry_document_events_document_created ON public.inquiry_document_events (document_id, created_at DESC);

-- All tables default-deny. Reads are explicit; writes go through reviewed RPCs.
ALTER TABLE public.inquiry_operator_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_sources ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_sync_runs ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiries ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_document_versions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_document_events ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.inquiry_operator_roles, public.inquiry_sources,
  public.inquiry_sync_runs, public.inquiries, public.inquiry_documents,
  public.inquiry_document_versions, public.inquiry_document_events FROM PUBLIC, anon;
REVOKE INSERT, UPDATE, DELETE ON TABLE public.inquiry_operator_roles, public.inquiry_sources,
  public.inquiry_sync_runs, public.inquiries, public.inquiry_documents,
  public.inquiry_document_versions, public.inquiry_document_events FROM authenticated;

GRANT SELECT ON TABLE public.inquiry_operator_roles, public.inquiry_sources,
  public.inquiry_sync_runs, public.inquiries, public.inquiry_documents,
  public.inquiry_document_versions, public.inquiry_document_events TO authenticated;

CREATE POLICY "Operators read own inquiry role" ON public.inquiry_operator_roles
  FOR SELECT TO authenticated USING (user_id = auth.uid() OR public.has_inquiry_role(ARRAY['inquiry_admin']));
CREATE POLICY "Internal roles read inquiry sources" ON public.inquiry_sources
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin','inquiry_bridge']));
CREATE POLICY "Audit roles read sync runs" ON public.inquiry_sync_runs
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_auditor','inquiry_admin','inquiry_bridge']));
CREATE POLICY "Internal viewers read inquiries" ON public.inquiries
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']));
CREATE POLICY "Internal viewers read documents" ON public.inquiry_documents
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']));
CREATE POLICY "Internal viewers read current versions" ON public.inquiry_document_versions
  FOR SELECT TO authenticated USING (
    public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin'])
    AND (state = 'available' OR public.has_inquiry_role(ARRAY['inquiry_auditor','inquiry_admin']))
  );
CREATE POLICY "Audit roles read document events" ON public.inquiry_document_events
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_auditor','inquiry_admin']));

-- A bridge can only register runs for the source assigned to its auth user.
CREATE OR REPLACE FUNCTION public.begin_inquiry_sync_run(
  p_run_id uuid, p_source_dataset_id uuid, p_source_revision bigint
) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_source public.inquiry_sources%ROWTYPE;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE = '42501';
  END IF;
  SELECT * INTO v_source FROM public.inquiry_sources
    WHERE source_dataset_id = p_source_dataset_id AND bridge_user_id = auth.uid() AND enabled FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Enabled inquiry source not assigned to this bridge.' USING ERRCODE = '42501'; END IF;
  IF p_source_revision <= v_source.last_source_revision THEN
    RAISE EXCEPTION 'Source revision must advance beyond %.', v_source.last_source_revision USING ERRCODE = '40001';
  END IF;
  INSERT INTO public.inquiry_sync_runs(id, source_id, status, source_revision)
    VALUES (p_run_id, v_source.id, 'running', p_source_revision);
  RETURN v_source.id;
END; $$;

REVOKE ALL ON FUNCTION public.begin_inquiry_sync_run(uuid, uuid, bigint) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.begin_inquiry_sync_run(uuid, uuid, bigint) TO authenticated;

-- Metadata upsert is monotonic and excludes desktop Notes/BOQ/communications.
CREATE OR REPLACE FUNCTION public.publish_inquiry_snapshot(
  p_run_id uuid, p_source_inquiry_id bigint, p_inquiry_number text,
  p_inquiry_reference text, p_customer_name text, p_customer_location text,
  p_project_name text, p_project_description text, p_salesman_name text,
  p_prepared_by_name text, p_status text, p_priority text, p_inquiry_value numeric,
  p_received_at timestamptz, p_prepared_at timestamptz, p_sent_at timestamptz,
  p_confirmed_at timestamptz, p_source_updated_at timestamptz,
  p_proforma_number text, p_job_order_number text, p_source_fingerprint text
) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_run public.inquiry_sync_runs%ROWTYPE; v_id uuid; v_created boolean := false;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  SELECT r.* INTO v_run FROM public.inquiry_sync_runs r JOIN public.inquiry_sources s ON s.id=r.source_id
    WHERE r.id=p_run_id AND r.status='running' AND r.actor_id=auth.uid() AND s.bridge_user_id=auth.uid() FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active sync run not found.' USING ERRCODE='42501'; END IF;
  IF p_source_inquiry_id <= 0 OR p_inquiry_number !~ '^INQ-[0-9]{4}-[0-9]{4,}$'
     OR p_source_fingerprint !~ '^[a-f0-9]{64}$' THEN
    RAISE EXCEPTION 'Invalid inquiry source identity or fingerprint.' USING ERRCODE='22023';
  END IF;
  INSERT INTO public.inquiries(source_id,source_inquiry_id,inquiry_number,inquiry_reference,customer_name,
    customer_location,project_name,project_description,salesman_name,prepared_by_name,status,priority,
    inquiry_value,received_at,prepared_at,sent_at,confirmed_at,source_updated_at,proforma_number,
    job_order_number,source_revision,source_fingerprint)
  VALUES(v_run.source_id,p_source_inquiry_id,btrim(p_inquiry_number),left(coalesce(p_inquiry_reference,''),100),
    left(coalesce(p_customer_name,''),200),left(coalesce(p_customer_location,''),250),left(coalesce(p_project_name,''),200),
    left(coalesce(p_project_description,''),500),left(coalesce(p_salesman_name,''),200),left(coalesce(p_prepared_by_name,''),200),
    p_status,p_priority,coalesce(p_inquiry_value,0),p_received_at,p_prepared_at,p_sent_at,p_confirmed_at,p_source_updated_at,
    left(coalesce(p_proforma_number,''),50),left(coalesce(p_job_order_number,''),50),v_run.source_revision,p_source_fingerprint)
  ON CONFLICT(source_id,source_inquiry_id) DO UPDATE SET
    inquiry_number=excluded.inquiry_number,inquiry_reference=excluded.inquiry_reference,customer_name=excluded.customer_name,
    customer_location=excluded.customer_location,project_name=excluded.project_name,project_description=excluded.project_description,
    salesman_name=excluded.salesman_name,prepared_by_name=excluded.prepared_by_name,status=excluded.status,priority=excluded.priority,
    inquiry_value=excluded.inquiry_value,received_at=excluded.received_at,prepared_at=excluded.prepared_at,sent_at=excluded.sent_at,
    confirmed_at=excluded.confirmed_at,source_updated_at=excluded.source_updated_at,proforma_number=excluded.proforma_number,
    job_order_number=excluded.job_order_number,source_revision=excluded.source_revision,source_fingerprint=excluded.source_fingerprint,
    availability='available',last_observed_at=now(),updated_at=now()
    WHERE public.inquiries.source_revision < excluded.source_revision
  RETURNING id, (xmax = 0) INTO v_id, v_created;
  IF v_id IS NULL THEN
    SELECT id INTO v_id FROM public.inquiries WHERE source_id=v_run.source_id AND source_inquiry_id=p_source_inquiry_id;
  ELSE
    INSERT INTO public.inquiry_document_events(source_id,inquiry_id,sync_run_id,event_type,actor_id)
      VALUES(v_run.source_id,v_id,p_run_id,CASE WHEN v_created THEN 'inquiry_published' ELSE 'inquiry_updated' END,auth.uid());
  END IF;
  RETURN v_id;
END; $$;

REVOKE ALL ON FUNCTION public.publish_inquiry_snapshot(uuid,bigint,text,text,text,text,text,text,text,text,text,text,numeric,timestamptz,timestamptz,timestamptz,timestamptz,timestamptz,text,text,text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.publish_inquiry_snapshot(uuid,bigint,text,text,text,text,text,text,text,text,text,text,numeric,timestamptz,timestamptz,timestamptz,timestamptz,timestamptz,text,text,text) TO authenticated;

-- Register metadata only after the immutable object upload exists. The bridge
-- supplies UUIDs so it can calculate the opaque key before upload and retry.
CREATE OR REPLACE FUNCTION public.publish_inquiry_document(
  p_run_id uuid, p_inquiry_id uuid, p_document_id uuid, p_version_id uuid,
  p_source_attachment_id bigint, p_original_filename text, p_extension text,
  p_content_sha256 text, p_byte_size bigint, p_detected_mime text,
  p_storage_object_key text, p_source_uploaded_at timestamptz, p_source_modified_at timestamptz
) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, storage, pg_temp AS $$
DECLARE
  v_run public.inquiry_sync_runs%ROWTYPE;
  v_inquiry public.inquiries%ROWTYPE;
  v_document_id uuid;
  v_current_version uuid;
  v_extension text := lower(btrim(coalesce(p_extension,'')));
  v_mime text := lower(btrim(coalesce(p_detected_mime,'')));
  v_expected_key text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  SELECT r.* INTO v_run FROM public.inquiry_sync_runs r JOIN public.inquiry_sources s ON s.id=r.source_id
    WHERE r.id=p_run_id AND r.status='running' AND r.actor_id=auth.uid() AND s.bridge_user_id=auth.uid() FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active sync run not found.' USING ERRCODE='42501'; END IF;
  SELECT * INTO v_inquiry FROM public.inquiries
    WHERE id=p_inquiry_id AND source_id=v_run.source_id AND source_revision=v_run.source_revision FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Inquiry was not published in this sync revision.' USING ERRCODE='40001'; END IF;
  IF p_source_attachment_id <= 0 OR p_byte_size NOT BETWEEN 1 AND 10485760
     OR p_content_sha256 !~ '^[a-f0-9]{64}$'
     OR char_length(btrim(coalesce(p_original_filename,''))) NOT BETWEEN 1 AND 260
     OR p_original_filename ~ '[\\/[:cntrl:]]' THEN
    RAISE EXCEPTION 'Invalid inquiry document metadata.' USING ERRCODE='22023';
  END IF;
  IF NOT (
    (v_extension='.pdf' AND v_mime='application/pdf') OR
    (v_extension IN ('.jpg','.jpeg') AND v_mime='image/jpeg') OR
    (v_extension='.png' AND v_mime='image/png') OR
    (v_extension='.gif' AND v_mime='image/gif') OR
    (v_extension='.webp' AND v_mime='image/webp') OR
    (v_extension='.bmp' AND v_mime IN ('image/bmp','image/x-ms-bmp')) OR
    (v_extension='.csv' AND v_mime IN ('text/csv','text/plain','application/csv')) OR
    (v_extension='.xlsx' AND v_mime IN ('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet','application/zip'))
  ) THEN RAISE EXCEPTION 'Document extension/MIME is not approved.' USING ERRCODE='22023'; END IF;
  v_expected_key := 'inquiry-documents/' || (SELECT source_dataset_id::text FROM public.inquiry_sources WHERE id=v_run.source_id)
    || '/' || p_inquiry_id::text || '/' || p_document_id::text || '/' || p_version_id::text;
  IF p_storage_object_key IS DISTINCT FROM v_expected_key THEN
    RAISE EXCEPTION 'Storage object key does not match document identity.' USING ERRCODE='22023';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM storage.objects WHERE bucket_id='inquiry-documents' AND name=p_storage_object_key) THEN
    RAISE EXCEPTION 'Uploaded private object is not available for registration.' USING ERRCODE='P0002';
  END IF;

  SELECT id,current_version_id INTO v_document_id,v_current_version FROM public.inquiry_documents
    WHERE inquiry_id=p_inquiry_id AND source_attachment_id=p_source_attachment_id FOR UPDATE;
  IF v_document_id IS NULL THEN
    v_document_id := p_document_id;
    INSERT INTO public.inquiry_documents(id,inquiry_id,source_attachment_id,original_filename,extension,source_revision)
      VALUES(v_document_id,p_inquiry_id,p_source_attachment_id,btrim(p_original_filename),v_extension,v_run.source_revision);
  ELSIF v_document_id <> p_document_id THEN
    RAISE EXCEPTION 'Document UUID does not match the existing source attachment.' USING ERRCODE='23505';
  END IF;

  INSERT INTO public.inquiry_document_versions(id,document_id,content_sha256,byte_size,detected_mime,
    storage_object_key,source_uploaded_at,source_modified_at,source_revision)
  VALUES(p_version_id,v_document_id,p_content_sha256,p_byte_size,v_mime,p_storage_object_key,
    p_source_uploaded_at,p_source_modified_at,v_run.source_revision)
  ON CONFLICT(document_id,content_sha256) DO NOTHING;

  IF NOT EXISTS (SELECT 1 FROM public.inquiry_document_versions WHERE id=p_version_id AND document_id=v_document_id) THEN
    SELECT id INTO p_version_id FROM public.inquiry_document_versions
      WHERE document_id=v_document_id AND content_sha256=p_content_sha256;
  END IF;
  IF v_current_version IS NOT NULL AND v_current_version <> p_version_id THEN
    UPDATE public.inquiry_document_versions SET state='superseded' WHERE id=v_current_version AND state='available';
    INSERT INTO public.inquiry_document_events(source_id,inquiry_id,document_id,version_id,sync_run_id,event_type,actor_id)
      VALUES(v_run.source_id,p_inquiry_id,v_document_id,v_current_version,p_run_id,'document_superseded',auth.uid());
  END IF;
  UPDATE public.inquiry_documents SET original_filename=btrim(p_original_filename),extension=v_extension,
    availability='available',current_version_id=p_version_id,source_revision=v_run.source_revision,
    last_observed_at=now(),updated_at=now() WHERE id=v_document_id;
  INSERT INTO public.inquiry_document_events(source_id,inquiry_id,document_id,version_id,sync_run_id,event_type,actor_id)
    SELECT v_run.source_id,p_inquiry_id,v_document_id,p_version_id,p_run_id,'document_published',auth.uid()
    WHERE NOT EXISTS (SELECT 1 FROM public.inquiry_document_events WHERE version_id=p_version_id AND event_type='document_published');
  RETURN p_version_id;
END; $$;

REVOKE ALL ON FUNCTION public.publish_inquiry_document(uuid,uuid,uuid,uuid,bigint,text,text,text,bigint,text,text,timestamptz,timestamptz) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.publish_inquiry_document(uuid,uuid,uuid,uuid,bigint,text,text,text,bigint,text,text,timestamptz,timestamptz) TO authenticated;

-- Finish advances the source high-water mark. Missing/tombstone reconciliation
-- is intentionally deferred until Phase 5 can submit and test a complete set.
CREATE OR REPLACE FUNCTION public.complete_inquiry_sync_run(
  p_run_id uuid, p_inquiries_seen integer, p_documents_seen integer, p_warnings_count integer,
  p_observed_inquiry_ids bigint[], p_observed_attachment_keys text[]
) RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_run public.inquiry_sync_runs%ROWTYPE;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE='42501'; END IF;
  SELECT r.* INTO v_run FROM public.inquiry_sync_runs r JOIN public.inquiry_sources s ON s.id=r.source_id
    WHERE r.id=p_run_id AND r.status='running' AND r.actor_id=auth.uid() AND s.bridge_user_id=auth.uid() FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active sync run not found.' USING ERRCODE='42501'; END IF;
  IF least(p_inquiries_seen,p_documents_seen,p_warnings_count) < 0 THEN RAISE EXCEPTION 'Sync counts cannot be negative.' USING ERRCODE='22023'; END IF;
  IF coalesce(array_length(p_observed_inquiry_ids,1),0) <> p_inquiries_seen
     OR coalesce(array_length(p_observed_attachment_keys,1),0) <> p_documents_seen THEN
    RAISE EXCEPTION 'Observed identity sets do not match sync counts.' USING ERRCODE='22023';
  END IF;
  -- Reconcile only a warning-free complete scan. Any unreadable manifest,
  -- missing source file, or validation warning preserves the prior cloud state.
  IF p_warnings_count = 0 THEN
    INSERT INTO public.inquiry_document_events(source_id,inquiry_id,sync_run_id,event_type,actor_id)
      SELECT v_run.source_id,i.id,p_run_id,'inquiry_tombstoned',auth.uid()
      FROM public.inquiries i WHERE i.source_id=v_run.source_id AND i.availability<>'tombstoned'
        AND NOT (i.source_inquiry_id=ANY(p_observed_inquiry_ids));
    UPDATE public.inquiries SET availability='tombstoned',updated_at=now()
      WHERE source_id=v_run.source_id AND NOT (source_inquiry_id=ANY(p_observed_inquiry_ids));

    INSERT INTO public.inquiry_document_events(source_id,inquiry_id,document_id,sync_run_id,event_type,actor_id)
      SELECT v_run.source_id,d.inquiry_id,d.id,p_run_id,'document_source_missing',auth.uid()
      FROM public.inquiry_documents d JOIN public.inquiries i ON i.id=d.inquiry_id
      WHERE i.source_id=v_run.source_id AND d.availability='available'
        AND NOT ((i.source_inquiry_id::text || ':' || d.source_attachment_id::text)=ANY(p_observed_attachment_keys));
    UPDATE public.inquiry_documents d SET availability='source_missing',updated_at=now()
      FROM public.inquiries i WHERE i.id=d.inquiry_id AND i.source_id=v_run.source_id
        AND NOT ((i.source_inquiry_id::text || ':' || d.source_attachment_id::text)=ANY(p_observed_attachment_keys));
  END IF;
  UPDATE public.inquiry_sync_runs SET status=CASE WHEN p_warnings_count=0 THEN 'completed' ELSE 'partial' END,
    complete_scan=(p_warnings_count=0),inquiries_seen=p_inquiries_seen,documents_seen=p_documents_seen,
    warnings_count=p_warnings_count,finished_at=now() WHERE id=p_run_id;
  UPDATE public.inquiry_sources SET last_complete_scan_at=CASE WHEN p_warnings_count=0 THEN now() ELSE last_complete_scan_at END,
    last_success_at=now(),last_source_revision=v_run.source_revision,updated_at=now() WHERE id=v_run.source_id;
  INSERT INTO public.inquiry_document_events(source_id,sync_run_id,event_type,actor_id,details)
    VALUES(v_run.source_id,p_run_id,'sync_completed',auth.uid(),jsonb_build_object(
      'complete_scan',p_warnings_count=0,'inquiries_seen',p_inquiries_seen,'documents_seen',p_documents_seen,'warnings_count',p_warnings_count));
END; $$;

REVOKE ALL ON FUNCTION public.complete_inquiry_sync_run(uuid,integer,integer,integer,bigint[],text[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.complete_inquiry_sync_run(uuid,integer,integer,integer,bigint[],text[]) TO authenticated;

-- Resolve a download and append an audit event in one authorized database call.
-- The object remains private; the API downloads it with this same operator JWT.
CREATE OR REPLACE FUNCTION public.authorize_inquiry_document_download(p_document_id uuid)
RETURNS TABLE(storage_bucket text, storage_object_key text, original_filename text, detected_mime text, byte_size bigint, content_sha256 text)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_document public.inquiry_documents%ROWTYPE; v_version public.inquiry_document_versions%ROWTYPE;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']) THEN
    RAISE EXCEPTION 'Inquiry viewer role required.' USING ERRCODE='42501';
  END IF;
  SELECT * INTO v_document FROM public.inquiry_documents
    WHERE id=p_document_id AND availability='available' FOR SHARE;
  IF NOT FOUND OR v_document.current_version_id IS NULL THEN
    RAISE EXCEPTION 'Current inquiry document is not available.' USING ERRCODE='P0002';
  END IF;
  SELECT * INTO v_version FROM public.inquiry_document_versions
    WHERE id=v_document.current_version_id AND document_id=v_document.id AND state='available';
  IF NOT FOUND THEN RAISE EXCEPTION 'Current inquiry document version is not available.' USING ERRCODE='P0002'; END IF;
  INSERT INTO public.inquiry_document_events(inquiry_id,document_id,version_id,event_type,actor_id)
    VALUES(v_document.inquiry_id,v_document.id,v_version.id,'document_access_authorized',auth.uid());
  RETURN QUERY SELECT v_version.storage_bucket,v_version.storage_object_key,v_document.original_filename,
    v_version.detected_mime,v_version.byte_size,v_version.content_sha256;
END; $$;

REVOKE ALL ON FUNCTION public.authorize_inquiry_document_download(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.authorize_inquiry_document_download(uuid) TO authenticated;

-- Bucket is private. Creating it here is additive and idempotent.
INSERT INTO storage.buckets (id, name, public, file_size_limit)
VALUES ('inquiry-documents', 'inquiry-documents', false, 10485760)
ON CONFLICT (id) DO UPDATE SET public = false, file_size_limit = 10485760;

-- No direct browser listing. SELECT is constrained to authenticated internal roles.
DROP POLICY IF EXISTS "Internal operators download inquiry documents" ON storage.objects;
CREATE POLICY "Internal operators download inquiry documents" ON storage.objects
  FOR SELECT TO authenticated
  USING (bucket_id='inquiry-documents' AND public.has_inquiry_role(ARRAY['inquiry_viewer','inquiry_auditor','inquiry_admin']));

-- Bridge can create immutable objects only within its assigned dataset prefix.
DROP POLICY IF EXISTS "Assigned bridge uploads immutable inquiry objects" ON storage.objects;
CREATE POLICY "Assigned bridge uploads immutable inquiry objects" ON storage.objects
  FOR INSERT TO authenticated
  WITH CHECK (
    bucket_id='inquiry-documents'
    AND public.has_inquiry_role(ARRAY['inquiry_bridge'])
    AND (storage.foldername(name))[1]='inquiry-documents'
    AND EXISTS (
      SELECT 1 FROM public.inquiry_sources s
      WHERE s.bridge_user_id=auth.uid() AND s.enabled
        AND s.source_dataset_id::text=(storage.foldername(name))[2]
    )
  );
-- Deliberately no storage UPDATE or DELETE policy: objects are immutable in v1.

-- Admin bootstrap/role changes are intentionally SQL/deployment operations in v1.
-- Do not add a browser-writable role policy. Example (replace UUIDs explicitly):
-- INSERT INTO public.inquiry_operator_roles(user_id, role, assigned_by)
-- VALUES ('operator-auth-uuid', 'inquiry_viewer', 'admin-auth-uuid');

NOTIFY pgrst, 'reload schema';
COMMIT;

-- Deployment gate after applying to a disposable/non-production project:
-- 1. Inspect pg_policies for all public inquiry_* tables and storage.objects.
-- 2. Prove anon gets no rows/functions/objects.
-- 3. Prove viewer cannot insert/update/delete or read audit events.
-- 4. Prove auditor reads events but cannot mutate.
-- 5. Prove bridge cannot read private documents or publish another source.
-- 6. Prove bucket public=false and immutable objects cannot update/delete.

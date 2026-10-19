-- PHASE 11A — PRODUCTION COORDINATION ROOM FOUNDATION
-- Additive only. Apply after the Phase 9 migrations and only after the
-- Phase 9E Windows bridge verification gate has passed.
-- This file is NOT run automatically by the website.
BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.production_room_members (
  user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE RESTRICT,
  display_name text NOT NULL CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 120),
  department text NOT NULL CHECK (char_length(btrim(department)) BETWEEN 1 AND 80),
  is_room_admin boolean NOT NULL DEFAULT false,
  active boolean NOT NULL DEFAULT true,
  created_by uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_production_room_members_active_name
  ON public.production_room_members(active,display_name,user_id);

CREATE TABLE IF NOT EXISTS public.production_room_messages (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  parent_id uuid REFERENCES public.production_room_messages(id) ON DELETE RESTRICT,
  author_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  category text NOT NULL CHECK (category IN (
    'general_production',
    'urgent_blocker',
    'material_shortage',
    'machine_issue',
    'quality_issue',
    'schedule_change',
    'dispatch_dependency',
    'decision',
    'action_item'
  )),
  body text NOT NULL CHECK (char_length(btrim(body)) BETWEEN 1 AND 4000),
  is_pinned boolean NOT NULL DEFAULT false,
  pinned_by uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  pinned_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  edited_at timestamptz,
  edit_count integer NOT NULL DEFAULT 0 CHECK (edit_count >= 0),
  deleted_by uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  deleted_at timestamptz,
  CHECK (parent_id IS NULL OR parent_id <> id),
  CHECK ((deleted_at IS NULL AND deleted_by IS NULL) OR (deleted_at IS NOT NULL AND deleted_by IS NOT NULL)),
  CHECK (
    (is_pinned AND parent_id IS NULL AND category='decision' AND deleted_at IS NULL AND pinned_by IS NOT NULL AND pinned_at IS NOT NULL)
    OR (NOT is_pinned AND pinned_by IS NULL AND pinned_at IS NULL)
  )
);
CREATE INDEX IF NOT EXISTS ix_production_room_messages_created
  ON public.production_room_messages(created_at DESC,id DESC);
CREATE INDEX IF NOT EXISTS ix_production_room_messages_parent_created
  ON public.production_room_messages(parent_id,created_at,id);
CREATE INDEX IF NOT EXISTS ix_production_room_messages_category_created
  ON public.production_room_messages(category,created_at DESC,id DESC);
CREATE INDEX IF NOT EXISTS ix_production_room_messages_author_created
  ON public.production_room_messages(author_user_id,created_at DESC,id DESC);
CREATE INDEX IF NOT EXISTS ix_production_room_messages_pinned
  ON public.production_room_messages(pinned_at DESC,id DESC) WHERE is_pinned;

CREATE TABLE IF NOT EXISTS public.production_room_reads (
  user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE RESTRICT,
  last_read_at timestamptz NOT NULL,
  last_read_message_id uuid REFERENCES public.production_room_messages(id) ON DELETE RESTRICT,
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.production_room_events (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  message_id uuid REFERENCES public.production_room_messages(id) ON DELETE RESTRICT,
  member_user_id uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  event_type text NOT NULL CHECK (event_type IN (
    'member_registered',
    'member_updated',
    'member_active_changed',
    'message_created',
    'message_edited',
    'message_deleted',
    'message_pinned',
    'message_unpinned'
  )),
  details jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details)='object'),
  occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_production_room_events_message_time
  ON public.production_room_events(message_id,occurred_at,id);
CREATE INDEX IF NOT EXISTS ix_production_room_events_member_time
  ON public.production_room_events(member_user_id,occurred_at,id);

CREATE OR REPLACE FUNCTION public.is_production_room_member()
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
  SELECT auth.uid() IS NOT NULL AND EXISTS (
    SELECT 1 FROM public.production_room_members m
    WHERE m.user_id=auth.uid() AND m.active
  );
$$;

CREATE OR REPLACE FUNCTION public.is_production_room_admin()
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
  SELECT auth.uid() IS NOT NULL AND EXISTS (
    SELECT 1 FROM public.production_room_members m
    WHERE m.user_id=auth.uid() AND m.active AND m.is_room_admin
  );
$$;

REVOKE ALL ON FUNCTION public.is_production_room_member(),public.is_production_room_admin() FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.is_production_room_member(),public.is_production_room_admin() TO authenticated;

ALTER TABLE public.production_room_members ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.production_room_messages ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.production_room_reads ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.production_room_events ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON public.production_room_members,public.production_room_messages,public.production_room_reads,public.production_room_events FROM PUBLIC,anon;
REVOKE INSERT,UPDATE,DELETE ON public.production_room_members FROM authenticated;
REVOKE INSERT,UPDATE,DELETE ON public.production_room_messages FROM authenticated;
REVOKE INSERT,UPDATE,DELETE ON public.production_room_reads FROM authenticated;
REVOKE INSERT,UPDATE,DELETE ON public.production_room_events FROM authenticated;
GRANT SELECT ON public.production_room_members,public.production_room_messages,public.production_room_reads,public.production_room_events TO authenticated;

DROP POLICY IF EXISTS "Production room directory is member and manager read" ON public.production_room_members;
CREATE POLICY "Production room directory is member and manager read"
ON public.production_room_members FOR SELECT TO authenticated USING (
  user_id=auth.uid()
  OR public.has_inquiry_role(ARRAY['inquiry_admin'])
  OR (public.is_production_room_member() AND (active OR public.is_production_room_admin()))
);

DROP POLICY IF EXISTS "Production room members read messages" ON public.production_room_messages;
CREATE POLICY "Production room members read messages"
ON public.production_room_messages FOR SELECT TO authenticated USING (
  public.is_production_room_member()
);

DROP POLICY IF EXISTS "Production room members read own read position" ON public.production_room_reads;
CREATE POLICY "Production room members read own read position"
ON public.production_room_reads FOR SELECT TO authenticated USING (
  user_id=auth.uid() AND public.is_production_room_member()
);

DROP POLICY IF EXISTS "Production room admins read audit events" ON public.production_room_events;
CREATE POLICY "Production room admins read audit events"
ON public.production_room_events FOR SELECT TO authenticated USING (
  public.is_production_room_admin()
);

CREATE OR REPLACE FUNCTION public.register_production_room_member(
  p_email text,
  p_display_name text,
  p_department text,
  p_is_room_admin boolean DEFAULT false,
  p_active boolean DEFAULT true
)
RETURNS public.production_room_members
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,auth,pg_temp
AS $$
DECLARE
  v_user_id uuid;
  v_existing public.production_room_members;
  v_row public.production_room_members;
  v_email text:=lower(btrim(coalesce(p_email,'')));
  v_display_name text:=btrim(coalesce(p_display_name,''));
  v_department text:=btrim(coalesce(p_department,''));
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT (public.has_inquiry_role(ARRAY['inquiry_admin']) OR public.is_production_room_admin()) THEN
    RAISE EXCEPTION 'Production room member management requires an inquiry admin or room admin.' USING ERRCODE='42501';
  END IF;
  IF char_length(v_email) NOT BETWEEN 3 AND 254 OR position('@' IN v_email) < 2
     OR char_length(v_display_name) NOT BETWEEN 1 AND 120
     OR char_length(v_department) NOT BETWEEN 1 AND 80
     OR p_is_room_admin IS NULL OR p_active IS NULL THEN
    RAISE EXCEPTION 'Invalid production room member profile.' USING ERRCODE='22023';
  END IF;
  IF (SELECT count(*) FROM auth.users WHERE lower(email)=v_email) <> 1 THEN
    RAISE EXCEPTION 'Auth user not found or email is not unique.' USING ERRCODE='P0002';
  END IF;
  SELECT id INTO v_user_id FROM auth.users WHERE lower(email)=v_email;

  SELECT * INTO v_existing FROM public.production_room_members WHERE user_id=v_user_id FOR UPDATE;
  IF v_existing.user_id IS NOT NULL AND v_existing.active AND v_existing.is_room_admin
     AND (NOT p_active OR NOT p_is_room_admin) THEN
    IF (SELECT count(*) FROM public.production_room_members WHERE active AND is_room_admin) <= 1 THEN
      RAISE EXCEPTION 'At least one active production room admin is required.' USING ERRCODE='22023';
    END IF;
  END IF;

  INSERT INTO public.production_room_members(
    user_id,display_name,department,is_room_admin,active,created_by,updated_by
  ) VALUES (
    v_user_id,v_display_name,v_department,p_is_room_admin,p_active,auth.uid(),auth.uid()
  )
  ON CONFLICT(user_id) DO UPDATE SET
    display_name=excluded.display_name,
    department=excluded.department,
    is_room_admin=excluded.is_room_admin,
    active=excluded.active,
    updated_by=auth.uid(),
    updated_at=now()
  RETURNING * INTO v_row;

  INSERT INTO public.production_room_events(member_user_id,actor_user_id,event_type,details)
  VALUES(
    v_user_id,
    auth.uid(),
    CASE WHEN v_existing.user_id IS NULL THEN 'member_registered' ELSE 'member_updated' END,
    jsonb_build_object(
      'display_name',v_row.display_name,
      'department',v_row.department,
      'is_room_admin',v_row.is_room_admin,
      'active',v_row.active
    )
  );
  RETURN v_row;
END $$;

CREATE OR REPLACE FUNCTION public.set_production_room_member_active(
  p_user_id uuid,
  p_active boolean
)
RETURNS public.production_room_members
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
DECLARE v_existing public.production_room_members; v_row public.production_room_members;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT (public.has_inquiry_role(ARRAY['inquiry_admin']) OR public.is_production_room_admin()) THEN
    RAISE EXCEPTION 'Production room member management requires an inquiry admin or room admin.' USING ERRCODE='42501';
  END IF;
  IF p_active IS NULL THEN RAISE EXCEPTION 'Invalid member state.' USING ERRCODE='22023'; END IF;
  SELECT * INTO v_existing FROM public.production_room_members WHERE user_id=p_user_id FOR UPDATE;
  IF v_existing.user_id IS NULL THEN RAISE EXCEPTION 'Production room member not found.' USING ERRCODE='P0002'; END IF;
  IF v_existing.active AND v_existing.is_room_admin AND NOT p_active THEN
    IF (SELECT count(*) FROM public.production_room_members WHERE active AND is_room_admin) <= 1 THEN
      RAISE EXCEPTION 'At least one active production room admin is required.' USING ERRCODE='22023';
    END IF;
  END IF;
  UPDATE public.production_room_members
    SET active=p_active,updated_by=auth.uid(),updated_at=now()
    WHERE user_id=p_user_id
    RETURNING * INTO v_row;
  INSERT INTO public.production_room_events(member_user_id,actor_user_id,event_type,details)
  VALUES(p_user_id,auth.uid(),'member_active_changed',jsonb_build_object('active',p_active));
  RETURN v_row;
END $$;

CREATE OR REPLACE FUNCTION public.post_production_room_message(
  p_body text,
  p_category text DEFAULT 'general_production',
  p_parent_id uuid DEFAULT NULL
)
RETURNS public.production_room_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
DECLARE
  v_parent public.production_room_messages;
  v_category text;
  v_row public.production_room_messages;
  v_body text:=btrim(coalesce(p_body,''));
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT public.is_production_room_member() THEN
    RAISE EXCEPTION 'Active production room membership is required.' USING ERRCODE='42501';
  END IF;
  IF char_length(v_body) NOT BETWEEN 1 AND 4000 THEN
    RAISE EXCEPTION 'Message must contain 1 to 4000 characters.' USING ERRCODE='22023';
  END IF;

  IF p_parent_id IS NOT NULL THEN
    SELECT * INTO v_parent FROM public.production_room_messages WHERE id=p_parent_id FOR UPDATE;
    IF v_parent.id IS NULL OR v_parent.parent_id IS NOT NULL OR v_parent.deleted_at IS NOT NULL THEN
      RAISE EXCEPTION 'Production room thread not found.' USING ERRCODE='P0002';
    END IF;
    v_category:=v_parent.category;
  ELSE
    IF p_category IS NULL OR p_category NOT IN (
      'general_production','urgent_blocker','material_shortage','machine_issue',
      'quality_issue','schedule_change','dispatch_dependency','decision','action_item'
    ) THEN
      RAISE EXCEPTION 'Invalid production room category.' USING ERRCODE='22023';
    END IF;
    v_category:=p_category;
  END IF;

  INSERT INTO public.production_room_messages(parent_id,author_user_id,category,body)
  VALUES(p_parent_id,auth.uid(),v_category,v_body)
  RETURNING * INTO v_row;

  INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
  VALUES(v_row.id,auth.uid(),'message_created',jsonb_build_object('category',v_category,'parent_id',p_parent_id));
  RETURN v_row;
END $$;

CREATE OR REPLACE FUNCTION public.edit_production_room_message(
  p_message_id uuid,
  p_body text
)
RETURNS public.production_room_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
DECLARE v_existing public.production_room_messages; v_row public.production_room_messages; v_body text:=btrim(coalesce(p_body,'')); v_previous_length integer;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT public.is_production_room_member() THEN
    RAISE EXCEPTION 'Active production room membership is required.' USING ERRCODE='42501';
  END IF;
  IF char_length(v_body) NOT BETWEEN 1 AND 4000 THEN
    RAISE EXCEPTION 'Message must contain 1 to 4000 characters.' USING ERRCODE='22023';
  END IF;
  SELECT * INTO v_existing FROM public.production_room_messages WHERE id=p_message_id FOR UPDATE;
  IF v_existing.id IS NULL OR v_existing.author_user_id<>auth.uid() THEN
    RAISE EXCEPTION 'Production room message not found.' USING ERRCODE='P0002';
  END IF;
  IF v_existing.deleted_at IS NOT NULL THEN
    RAISE EXCEPTION 'Deleted messages cannot be edited.' USING ERRCODE='22023';
  END IF;
  IF v_existing.created_at <= now() - interval '30 minutes' THEN
    RAISE EXCEPTION 'The 30-minute edit window has closed.' USING ERRCODE='22023';
  END IF;
  v_previous_length:=char_length(v_existing.body);
  UPDATE public.production_room_messages
    SET body=v_body,edited_at=now(),edit_count=edit_count+1
    WHERE id=p_message_id
    RETURNING * INTO v_row;
  INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
  VALUES(p_message_id,auth.uid(),'message_edited',jsonb_build_object(
    'previous_length',v_previous_length,
    'new_length',char_length(v_row.body),
    'edit_count',v_row.edit_count
  ));
  RETURN v_row;
END $$;

CREATE OR REPLACE FUNCTION public.delete_production_room_message(p_message_id uuid)
RETURNS public.production_room_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
DECLARE v_existing public.production_room_messages; v_row public.production_room_messages; v_is_author boolean; v_allowed boolean;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT public.is_production_room_member() THEN
    RAISE EXCEPTION 'Active production room membership is required.' USING ERRCODE='42501';
  END IF;
  SELECT * INTO v_existing FROM public.production_room_messages WHERE id=p_message_id FOR UPDATE;
  IF v_existing.id IS NULL THEN RAISE EXCEPTION 'Production room message not found.' USING ERRCODE='P0002'; END IF;
  IF v_existing.deleted_at IS NOT NULL THEN RETURN v_existing; END IF;
  v_is_author:=v_existing.author_user_id=auth.uid();
  v_allowed:=public.is_production_room_admin()
    OR (v_is_author AND v_existing.created_at > now() - interval '30 minutes');
  IF NOT v_allowed THEN
    IF v_is_author THEN
      RAISE EXCEPTION 'The 30-minute author deletion window has closed.' USING ERRCODE='22023';
    END IF;
    RAISE EXCEPTION 'Production room message not found.' USING ERRCODE='P0002';
  END IF;
  UPDATE public.production_room_messages
    SET body='This message was deleted.',is_pinned=false,pinned_by=NULL,pinned_at=NULL,
        deleted_by=auth.uid(),deleted_at=now()
    WHERE id=p_message_id
    RETURNING * INTO v_row;
  INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
  VALUES(p_message_id,auth.uid(),'message_deleted',jsonb_build_object('actor_is_author',v_is_author));
  RETURN v_row;
END $$;

CREATE OR REPLACE FUNCTION public.set_production_room_message_pinned(
  p_message_id uuid,
  p_pinned boolean
)
RETURNS public.production_room_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
DECLARE v_existing public.production_room_messages; v_row public.production_room_messages;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT public.is_production_room_admin() THEN
    RAISE EXCEPTION 'Production room admin membership is required.' USING ERRCODE='42501';
  END IF;
  IF p_pinned IS NULL THEN RAISE EXCEPTION 'Invalid pin state.' USING ERRCODE='22023'; END IF;
  SELECT * INTO v_existing FROM public.production_room_messages WHERE id=p_message_id FOR UPDATE;
  IF v_existing.id IS NULL THEN RAISE EXCEPTION 'Production room message not found.' USING ERRCODE='P0002'; END IF;
  IF p_pinned AND (v_existing.parent_id IS NOT NULL OR v_existing.category<>'decision' OR v_existing.deleted_at IS NOT NULL) THEN
    RAISE EXCEPTION 'Only a top-level decision can be pinned.' USING ERRCODE='22023';
  END IF;
  IF v_existing.is_pinned=p_pinned THEN RETURN v_existing; END IF;
  UPDATE public.production_room_messages
    SET is_pinned=p_pinned,
        pinned_by=CASE WHEN p_pinned THEN auth.uid() ELSE NULL END,
        pinned_at=CASE WHEN p_pinned THEN now() ELSE NULL END
    WHERE id=p_message_id
    RETURNING * INTO v_row;
  INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
  VALUES(p_message_id,auth.uid(),CASE WHEN p_pinned THEN 'message_pinned' ELSE 'message_unpinned' END,'{}'::jsonb);
  RETURN v_row;
END $$;

CREATE OR REPLACE FUNCTION public.mark_production_room_read(
  p_through timestamptz DEFAULT now()
)
RETURNS public.production_room_reads
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=public,pg_temp
AS $$
DECLARE v_through timestamptz:=least(coalesce(p_through,now()),now()); v_message_id uuid; v_row public.production_room_reads;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT public.is_production_room_member() THEN
    RAISE EXCEPTION 'Active production room membership is required.' USING ERRCODE='42501';
  END IF;
  SELECT id INTO v_message_id
    FROM public.production_room_messages
    WHERE created_at <= v_through
    ORDER BY created_at DESC,id DESC
    LIMIT 1;
  INSERT INTO public.production_room_reads(user_id,last_read_at,last_read_message_id)
  VALUES(auth.uid(),v_through,v_message_id)
  ON CONFLICT(user_id) DO UPDATE SET
    last_read_at=greatest(public.production_room_reads.last_read_at,excluded.last_read_at),
    last_read_message_id=CASE
      WHEN excluded.last_read_at >= public.production_room_reads.last_read_at THEN excluded.last_read_message_id
      ELSE public.production_room_reads.last_read_message_id
    END,
    updated_at=now()
  RETURNING * INTO v_row;
  RETURN v_row;
END $$;

REVOKE ALL ON FUNCTION public.register_production_room_member(text,text,text,boolean,boolean) FROM PUBLIC,anon;
REVOKE ALL ON FUNCTION public.set_production_room_member_active(uuid,boolean) FROM PUBLIC,anon;
REVOKE ALL ON FUNCTION public.post_production_room_message(text,text,uuid) FROM PUBLIC,anon;
REVOKE ALL ON FUNCTION public.edit_production_room_message(uuid,text) FROM PUBLIC,anon;
REVOKE ALL ON FUNCTION public.delete_production_room_message(uuid) FROM PUBLIC,anon;
REVOKE ALL ON FUNCTION public.set_production_room_message_pinned(uuid,boolean) FROM PUBLIC,anon;
REVOKE ALL ON FUNCTION public.mark_production_room_read(timestamptz) FROM PUBLIC,anon;

GRANT EXECUTE ON FUNCTION public.register_production_room_member(text,text,text,boolean,boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.set_production_room_member_active(uuid,boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.post_production_room_message(text,text,uuid) TO authenticated;
GRANT EXECUTE ON FUNCTION public.edit_production_room_message(uuid,text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_production_room_message(uuid) TO authenticated;
GRANT EXECUTE ON FUNCTION public.set_production_room_message_pinned(uuid,boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.mark_production_room_read(timestamptz) TO authenticated;

NOTIFY pgrst,'reload schema';
COMMIT;

-- Read-only structural verification after authorized application:
-- select 'tables' check_name,count(*)::text result from information_schema.tables
-- where table_schema='public' and table_name in (
--   'production_room_members','production_room_messages','production_room_reads','production_room_events')
-- union all select 'rls_tables',count(*)::text from pg_tables where schemaname='public'
--   and tablename in (
--   'production_room_members','production_room_messages','production_room_reads','production_room_events')
--   and rowsecurity
-- union all select 'policies',count(*)::text from pg_policies where schemaname='public'
--   and tablename in (
--   'production_room_members','production_room_messages','production_room_reads','production_room_events')
-- union all select 'functions',count(*)::text from information_schema.routines where routine_schema='public'
--   and routine_name in (
--   'is_production_room_member','is_production_room_admin','register_production_room_member',
--   'set_production_room_member_active','post_production_room_message','edit_production_room_message',
--   'delete_production_room_message','set_production_room_message_pinned','mark_production_room_read')
-- union all select 'members',count(*)::text from public.production_room_members
-- union all select 'messages',count(*)::text from public.production_room_messages
-- union all select 'reads',count(*)::text from public.production_room_reads
-- union all select 'events',count(*)::text from public.production_room_events;
--
-- Expected immediately after application: 4 tables, 4 RLS tables, 4 policies,
-- 9 functions, and zero rows in all four new tables.

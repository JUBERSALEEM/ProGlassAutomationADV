-- GLOBAL COMMUNITY CHAT — one signed-in group room
-- Additive only. Apply as a single Supabase migration after the inquiry role
-- migrations that define public.inquiry_operator_roles and public.has_inquiry_role.
-- Signed-in users may read the room; all writes go through bounded RPCs.

CREATE TABLE IF NOT EXISTS public.global_chat_messages (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  author_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  author_display_name text NOT NULL CHECK (char_length(btrim(author_display_name)) BETWEEN 1 AND 60),
  body text NOT NULL DEFAULT '' CHECK (char_length(body) <= 1000),
  deleted_at timestamptz,
  deleted_by uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  created_at timestamptz NOT NULL DEFAULT now(),
  CHECK (
    (deleted_at IS NULL AND deleted_by IS NULL AND char_length(btrim(body)) BETWEEN 1 AND 1000)
    OR (deleted_at IS NOT NULL AND deleted_by IS NOT NULL AND body = '')
  )
);
CREATE INDEX IF NOT EXISTS ix_global_chat_messages_created ON public.global_chat_messages(created_at, id);
CREATE INDEX IF NOT EXISTS ix_global_chat_messages_author_created ON public.global_chat_messages(author_user_id, created_at DESC);

CREATE TABLE IF NOT EXISTS public.global_chat_reads (
  user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  last_read_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.global_chat_events (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  message_id uuid REFERENCES public.global_chat_messages(id) ON DELETE RESTRICT,
  actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  event_type text NOT NULL CHECK (event_type IN ('message_created','message_deleted')),
  occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_global_chat_events_message ON public.global_chat_events(message_id, occurred_at, id);

ALTER TABLE public.global_chat_messages ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.global_chat_reads ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.global_chat_events ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.global_chat_messages, public.global_chat_reads, public.global_chat_events FROM anon;
GRANT SELECT ON TABLE public.global_chat_messages, public.global_chat_reads, public.global_chat_events TO authenticated;
REVOKE INSERT, UPDATE, DELETE ON TABLE public.global_chat_messages, public.global_chat_reads, public.global_chat_events FROM authenticated;

DROP POLICY IF EXISTS "Signed-in users read global chat" ON public.global_chat_messages;
CREATE POLICY "Signed-in users read global chat" ON public.global_chat_messages
  FOR SELECT TO authenticated USING (auth.uid() IS NOT NULL);

DROP POLICY IF EXISTS "Users read own global chat read state" ON public.global_chat_reads;
CREATE POLICY "Users read own global chat read state" ON public.global_chat_reads
  FOR SELECT TO authenticated USING (user_id = auth.uid());

DROP POLICY IF EXISTS "Inquiry admins and auditors read global chat audit" ON public.global_chat_events;
CREATE POLICY "Inquiry admins and auditors read global chat audit" ON public.global_chat_events
  FOR SELECT TO authenticated USING (public.has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor']));

CREATE OR REPLACE FUNCTION public.global_chat_display_name()
RETURNS text
LANGUAGE sql
STABLE
AS $$
  SELECT left(
    regexp_replace(
      btrim(coalesce(
        nullif(btrim(auth.jwt()->'user_metadata'->>'full_name'), ''),
        nullif(btrim(auth.jwt()->'user_metadata'->>'name'), ''),
        nullif(split_part(coalesce(auth.jwt()->>'email', ''), '@', 1), ''),
        'ProGlass user'
      )),
      '\s+',
      ' ',
      'g'
    ),
    60
  );
$$;
REVOKE ALL ON FUNCTION public.global_chat_display_name() FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.global_chat_display_name() TO authenticated;

CREATE OR REPLACE FUNCTION public.post_global_chat_message(p_body text)
RETURNS public.global_chat_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_body text;
  v_last_at timestamptz;
  v_recent_count integer;
  v_row public.global_chat_messages;
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;

  v_body := btrim(coalesce(p_body, ''));
  IF char_length(v_body) NOT BETWEEN 1 AND 1000 THEN
    RAISE EXCEPTION 'Message must contain 1 to 1000 characters.' USING ERRCODE = '22023';
  END IF;

  SELECT max(created_at), count(*) FILTER (WHERE created_at >= now() - interval '1 minute')
    INTO v_last_at, v_recent_count
    FROM public.global_chat_messages
   WHERE author_user_id = auth.uid();

  IF v_last_at IS NOT NULL AND v_last_at > now() - interval '2 seconds' THEN
    RAISE EXCEPTION 'You are sending messages too quickly. Please wait a moment.';
  END IF;
  IF coalesce(v_recent_count, 0) >= 20 THEN
    RAISE EXCEPTION 'Message limit reached. Please wait a minute before sending more.';
  END IF;

  INSERT INTO public.global_chat_messages(author_user_id, author_display_name, body)
  VALUES(auth.uid(), public.global_chat_display_name(), v_body)
  RETURNING * INTO v_row;

  INSERT INTO public.global_chat_events(message_id, actor_user_id, event_type)
  VALUES(v_row.id, auth.uid(), 'message_created');

  INSERT INTO public.global_chat_reads(user_id, last_read_at, updated_at)
  VALUES(auth.uid(), v_row.created_at, now())
  ON CONFLICT (user_id) DO UPDATE
    SET last_read_at = GREATEST(public.global_chat_reads.last_read_at, EXCLUDED.last_read_at),
        updated_at = now();

  RETURN v_row;
END
$$;
REVOKE ALL ON FUNCTION public.post_global_chat_message(text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.post_global_chat_message(text) TO authenticated;

CREATE OR REPLACE FUNCTION public.delete_global_chat_message(p_message_id uuid)
RETURNS public.global_chat_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_row public.global_chat_messages;
  v_moderator boolean;
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;

  SELECT * INTO v_row
    FROM public.global_chat_messages
   WHERE id = p_message_id
   FOR UPDATE;

  IF v_row.id IS NULL THEN
    RAISE EXCEPTION 'Message not found.' USING ERRCODE = 'P0002';
  END IF;

  IF v_row.deleted_at IS NOT NULL THEN
    RETURN v_row;
  END IF;

  v_moderator := public.has_inquiry_role(ARRAY['inquiry_admin']);
  IF v_row.author_user_id <> auth.uid() AND NOT v_moderator THEN
    RAISE EXCEPTION 'Message not found.' USING ERRCODE = 'P0002';
  END IF;

  UPDATE public.global_chat_messages
     SET body = '', deleted_at = now(), deleted_by = auth.uid()
   WHERE id = v_row.id
  RETURNING * INTO v_row;

  INSERT INTO public.global_chat_events(message_id, actor_user_id, event_type)
  VALUES(v_row.id, auth.uid(), 'message_deleted');

  RETURN v_row;
END
$$;
REVOKE ALL ON FUNCTION public.delete_global_chat_message(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.delete_global_chat_message(uuid) TO authenticated;

CREATE OR REPLACE FUNCTION public.mark_global_chat_read(p_last_read_at timestamptz DEFAULT now())
RETURNS public.global_chat_reads
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_at timestamptz := least(coalesce(p_last_read_at, now()), now());
  v_row public.global_chat_reads;
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;

  INSERT INTO public.global_chat_reads(user_id, last_read_at, updated_at)
  VALUES(auth.uid(), v_at, now())
  ON CONFLICT (user_id) DO UPDATE
    SET last_read_at = GREATEST(public.global_chat_reads.last_read_at, EXCLUDED.last_read_at),
        updated_at = now()
  RETURNING * INTO v_row;

  RETURN v_row;
END
$$;
REVOKE ALL ON FUNCTION public.mark_global_chat_read(timestamptz) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.mark_global_chat_read(timestamptz) TO authenticated;

ALTER TABLE public.global_chat_messages REPLICA IDENTITY FULL;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime')
     AND NOT EXISTS (
       SELECT 1
         FROM pg_publication_tables
        WHERE pubname = 'supabase_realtime'
          AND schemaname = 'public'
          AND tablename = 'global_chat_messages'
     ) THEN
    ALTER PUBLICATION supabase_realtime ADD TABLE public.global_chat_messages;
  END IF;
END
$$;

NOTIFY pgrst, 'reload schema';

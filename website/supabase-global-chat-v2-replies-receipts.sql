-- GLOBAL COMMUNITY CHAT V2 — WhatsApp-style replies + delivery/read receipts
-- Additive. Apply as a single Supabase migration after supabase-global-chat.sql.
-- Replies reference the original message (quotes never snapshot deleted text).
-- Receipts are per-message so "who read this and when" stays exact, and are
-- visible to signed-in room participants, like WhatsApp group receipts.
-- All writes still go through bounded security-definer RPCs.

ALTER TABLE public.global_chat_messages
  ADD COLUMN IF NOT EXISTS reply_to_message_id uuid REFERENCES public.global_chat_messages(id) ON DELETE RESTRICT;
CREATE INDEX IF NOT EXISTS ix_global_chat_messages_reply_to ON public.global_chat_messages(reply_to_message_id);

DROP FUNCTION IF EXISTS public.post_global_chat_message(text);

CREATE OR REPLACE FUNCTION public.post_global_chat_message(p_body text, p_reply_to_message_id uuid DEFAULT NULL)
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

  IF p_reply_to_message_id IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM public.global_chat_messages
     WHERE id = p_reply_to_message_id AND deleted_at IS NULL
  ) THEN
    RAISE EXCEPTION 'The message you are replying to is no longer available.' USING ERRCODE = 'P0002';
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

  INSERT INTO public.global_chat_messages(author_user_id, author_display_name, body, reply_to_message_id)
  VALUES(auth.uid(), public.global_chat_display_name(), v_body, p_reply_to_message_id)
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
REVOKE ALL ON FUNCTION public.post_global_chat_message(text, uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.post_global_chat_message(text, uuid) TO authenticated;

CREATE TABLE IF NOT EXISTS public.global_chat_receipts (
  message_id uuid NOT NULL REFERENCES public.global_chat_messages(id) ON DELETE CASCADE,
  user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
  display_name text NOT NULL CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 60),
  delivered_at timestamptz,
  read_at timestamptz,
  PRIMARY KEY (message_id, user_id),
  CHECK (delivered_at IS NOT NULL OR read_at IS NOT NULL),
  CHECK (read_at IS NULL OR delivered_at IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS ix_global_chat_receipts_message_read ON public.global_chat_receipts(message_id, read_at);
CREATE INDEX IF NOT EXISTS ix_global_chat_receipts_user ON public.global_chat_receipts(user_id, message_id);

ALTER TABLE public.global_chat_receipts ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.global_chat_receipts FROM anon;
GRANT SELECT ON TABLE public.global_chat_receipts TO authenticated;
REVOKE INSERT, UPDATE, DELETE ON TABLE public.global_chat_receipts FROM authenticated;

DROP POLICY IF EXISTS "Signed-in users read global chat receipts" ON public.global_chat_receipts;
CREATE POLICY "Signed-in users read global chat receipts" ON public.global_chat_receipts
  FOR SELECT TO authenticated USING (auth.uid() IS NOT NULL);

CREATE OR REPLACE FUNCTION public.mark_global_chat_receipts_delivered(p_message_ids uuid[])
RETURNS SETOF public.global_chat_receipts
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;
  IF p_message_ids IS NULL OR cardinality(p_message_ids) = 0 THEN
    RETURN;
  END IF;
  IF cardinality(p_message_ids) > 200 THEN
    RAISE EXCEPTION 'Too many messages in one receipt update.' USING ERRCODE = '22023';
  END IF;

  RETURN QUERY
  INSERT INTO public.global_chat_receipts(message_id, user_id, display_name, delivered_at)
  SELECT m.id, auth.uid(), public.global_chat_display_name(), now()
    FROM public.global_chat_messages m
   WHERE m.id = ANY(p_message_ids)
     AND m.author_user_id <> auth.uid()
     AND m.deleted_at IS NULL
  ON CONFLICT (message_id, user_id) DO UPDATE
    SET display_name = EXCLUDED.display_name,
        delivered_at = COALESCE(public.global_chat_receipts.delivered_at, EXCLUDED.delivered_at)
  WHERE public.global_chat_receipts.delivered_at IS NULL
     OR public.global_chat_receipts.display_name <> EXCLUDED.display_name
  RETURNING *;
END
$$;
REVOKE ALL ON FUNCTION public.mark_global_chat_receipts_delivered(uuid[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.mark_global_chat_receipts_delivered(uuid[]) TO authenticated;

CREATE OR REPLACE FUNCTION public.mark_global_chat_receipts_read(p_message_ids uuid[])
RETURNS SETOF public.global_chat_receipts
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Authentication required.' USING ERRCODE = '42501';
  END IF;
  IF p_message_ids IS NULL OR cardinality(p_message_ids) = 0 THEN
    RETURN;
  END IF;
  IF cardinality(p_message_ids) > 200 THEN
    RAISE EXCEPTION 'Too many messages in one receipt update.' USING ERRCODE = '22023';
  END IF;

  RETURN QUERY
  INSERT INTO public.global_chat_receipts(message_id, user_id, display_name, delivered_at, read_at)
  SELECT m.id, auth.uid(), public.global_chat_display_name(), now(), now()
    FROM public.global_chat_messages m
   WHERE m.id = ANY(p_message_ids)
     AND m.author_user_id <> auth.uid()
     AND m.deleted_at IS NULL
  ON CONFLICT (message_id, user_id) DO UPDATE
    SET display_name = EXCLUDED.display_name,
        delivered_at = COALESCE(public.global_chat_receipts.delivered_at, EXCLUDED.delivered_at),
        read_at = COALESCE(public.global_chat_receipts.read_at, EXCLUDED.read_at)
  WHERE public.global_chat_receipts.read_at IS NULL
     OR public.global_chat_receipts.display_name <> EXCLUDED.display_name
  RETURNING *;
END
$$;
REVOKE ALL ON FUNCTION public.mark_global_chat_receipts_read(uuid[]) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.mark_global_chat_receipts_read(uuid[]) TO authenticated;

ALTER TABLE public.global_chat_receipts REPLICA IDENTITY FULL;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime')
     AND NOT EXISTS (
       SELECT 1
         FROM pg_publication_tables
        WHERE pubname = 'supabase_realtime'
          AND schemaname = 'public'
          AND tablename = 'global_chat_receipts'
     ) THEN
    ALTER PUBLICATION supabase_realtime ADD TABLE public.global_chat_receipts;
  END IF;
END
$$;

NOTIFY pgrst, 'reload schema';

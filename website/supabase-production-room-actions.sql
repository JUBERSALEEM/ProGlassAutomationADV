-- Phase 11 — structured action items in the production room.
--
-- Why: the room has had an `action_item` category since phase 11a, but the
-- owner and the due moment only ever existed in the prose. A production room
-- where "who owns this and is it late" cannot be answered by a filter is a
-- categorised chat, not coordination.
--
-- Shape, deliberately narrow:
--   * Only a top-level `action_item` may carry an owner, a due moment or a
--     status. A reply, or any other category, must leave all three null.
--   * An owner must be an active room member. That is enforced in the writer
--     because a CHECK constraint cannot read another table.
--   * Status is null until someone acts, then `open`, `done` or `cancelled`.
--     Null and `open` mean the same thing to a reader; the column is nullable
--     so existing rows and non-action categories stay valid without a backfill.
--   * Assigning is a room admin or the author. Closing or cancelling an item is
--     additionally allowed for its owner, but an owner may not reassign.
--
-- Additive only: three columns, one check constraint, one partial index, two
-- functions, and a widening of the event allowlist. No existing column changes.

BEGIN;

ALTER TABLE public.production_room_messages
  ADD COLUMN IF NOT EXISTS action_owner_user_id uuid REFERENCES auth.users(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS action_due_at timestamptz,
  ADD COLUMN IF NOT EXISTS action_status text;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conrelid = 'public.production_room_messages'::regclass
      AND conname = 'production_room_messages_action_status_check'
  ) THEN
    ALTER TABLE public.production_room_messages
      ADD CONSTRAINT production_room_messages_action_status_check
      CHECK (action_status IS NULL OR action_status IN ('open','done','cancelled'));
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conrelid = 'public.production_room_messages'::regclass
      AND conname = 'production_room_messages_action_shape_check'
  ) THEN
    ALTER TABLE public.production_room_messages
      ADD CONSTRAINT production_room_messages_action_shape_check
      CHECK (
        (action_owner_user_id IS NULL AND action_due_at IS NULL AND action_status IS NULL)
        OR (category = 'action_item' AND parent_id IS NULL)
      );
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_production_room_messages_action_due
  ON public.production_room_messages(action_due_at, id)
  WHERE category = 'action_item' AND (action_status IS NULL OR action_status <> 'done');

-- The audit allowlist has to learn the three new events, or a legitimate action
-- write would fail on the events insert and roll the whole change back.
DO $$
BEGIN
  ALTER TABLE public.production_room_events DROP CONSTRAINT production_room_events_event_type_check;
  ALTER TABLE public.production_room_events
    ADD CONSTRAINT production_room_events_event_type_check
    CHECK (event_type IN (
      'member_registered','member_updated','member_active_changed',
      'message_created','message_edited','message_deleted','message_pinned','message_unpinned',
      'action_assigned','action_updated','action_completed','action_reopened'
    ));
END $$;

CREATE OR REPLACE FUNCTION public.set_production_room_action(
  p_message_id uuid,
  p_owner_user_id uuid DEFAULT NULL,
  p_due_at timestamptz DEFAULT NULL,
  p_status text DEFAULT NULL
)
RETURNS public.production_room_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'public', 'pg_temp'
AS $function$
DECLARE
  v_existing public.production_room_messages;
  v_row public.production_room_messages;
  v_is_author boolean;
  v_is_admin boolean;
  v_is_owner boolean;
  v_owner_changed boolean;
  v_due_changed boolean;
  v_status text:=nullif(btrim(coalesce(p_status,'')),'');
  v_event text;
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Authentication required.' USING ERRCODE='42501'; END IF;
  IF NOT public.is_production_room_member() THEN
    RAISE EXCEPTION 'Active production room membership is required.' USING ERRCODE='42501';
  END IF;

  IF v_status IS NOT NULL AND v_status NOT IN ('open','done','cancelled') THEN
    RAISE EXCEPTION 'Invalid action status.' USING ERRCODE='22023';
  END IF;

  IF p_due_at IS NOT NULL AND (p_due_at < now() - interval '5 years' OR p_due_at > now() + interval '5 years') THEN
    RAISE EXCEPTION 'Due date is outside the accepted range.' USING ERRCODE='22023';
  END IF;

  SELECT * INTO v_existing FROM public.production_room_messages WHERE id=p_message_id FOR UPDATE;
  IF v_existing.id IS NULL OR v_existing.deleted_at IS NOT NULL THEN
    RAISE EXCEPTION 'Production room message not found.' USING ERRCODE='P0002';
  END IF;
  IF v_existing.category <> 'action_item' OR v_existing.parent_id IS NOT NULL THEN
    RAISE EXCEPTION 'Only a top-level action item can carry an owner, a due time or a status.' USING ERRCODE='22023';
  END IF;

  IF p_owner_user_id IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM public.production_room_members m
    WHERE m.user_id = p_owner_user_id AND m.active
  ) THEN
    RAISE EXCEPTION 'The owner must be an active production room member.' USING ERRCODE='22023';
  END IF;

  v_is_author := v_existing.author_user_id = auth.uid();
  v_is_admin := public.is_production_room_admin();
  v_is_owner := v_existing.action_owner_user_id = auth.uid();
  v_owner_changed := v_existing.action_owner_user_id IS DISTINCT FROM p_owner_user_id;
  v_due_changed := v_existing.action_due_at IS DISTINCT FROM p_due_at;

  IF NOT (v_is_admin OR v_is_author OR v_is_owner) THEN
    RAISE EXCEPTION 'Production room message not found.' USING ERRCODE='P0002';
  END IF;

  -- The owner may close or reopen what they own; they may not hand it to
  -- somebody else or move the date, which is the author's or an admin's call.
  IF v_is_owner AND NOT v_is_admin AND NOT v_is_author THEN
    IF v_owner_changed OR v_due_changed THEN
      RAISE EXCEPTION 'Only a room admin or the author can reassign an action item.' USING ERRCODE='42501';
    END IF;
  END IF;

  UPDATE public.production_room_messages
    SET action_owner_user_id = p_owner_user_id,
        action_due_at = p_due_at,
        action_status = v_status
    WHERE id = p_message_id
    RETURNING * INTO v_row;

  v_event := CASE
    WHEN v_row.action_status = 'done' AND coalesce(v_existing.action_status,'open') <> 'done' THEN 'action_completed'
    WHEN coalesce(v_row.action_status,'open') <> 'done' AND v_existing.action_status = 'done' THEN 'action_reopened'
    WHEN v_owner_changed THEN 'action_assigned'
    ELSE 'action_updated'
  END;

  INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
  VALUES(p_message_id,auth.uid(),v_event,jsonb_build_object(
    'owner_user_id',v_row.action_owner_user_id,
    'due_at',v_row.action_due_at,
    'status',v_row.action_status,
    'previous_owner_user_id',v_existing.action_owner_user_id,
    'previous_status',v_existing.action_status
  ));

  RETURN v_row;
END $function$;

-- Posting an action item with its owner and date in one write. Deliberately a
-- separate function rather than a wider post signature: adding defaulted
-- parameters to post_production_room_message would create an ambiguous overload
-- that PostgREST resolves unpredictably.
CREATE OR REPLACE FUNCTION public.post_production_room_action_item(
  p_body text,
  p_owner_user_id uuid DEFAULT NULL,
  p_due_at timestamptz DEFAULT NULL
)
RETURNS public.production_room_messages
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'public', 'pg_temp'
AS $function$
DECLARE
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
  IF p_due_at IS NOT NULL AND (p_due_at < now() - interval '5 years' OR p_due_at > now() + interval '5 years') THEN
    RAISE EXCEPTION 'Due date is outside the accepted range.' USING ERRCODE='22023';
  END IF;
  IF p_owner_user_id IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM public.production_room_members m
    WHERE m.user_id = p_owner_user_id AND m.active
  ) THEN
    RAISE EXCEPTION 'The owner must be an active production room member.' USING ERRCODE='22023';
  END IF;

  INSERT INTO public.production_room_messages(
    parent_id,author_user_id,category,body,action_owner_user_id,action_due_at,action_status
  ) VALUES (
    NULL,auth.uid(),'action_item',v_body,p_owner_user_id,p_due_at,'open'
  )
  RETURNING * INTO v_row;

  INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
  VALUES(v_row.id,auth.uid(),'message_created',jsonb_build_object('category','action_item','parent_id',NULL));
  IF p_owner_user_id IS NOT NULL THEN
    INSERT INTO public.production_room_events(message_id,actor_user_id,event_type,details)
    VALUES(v_row.id,auth.uid(),'action_assigned',jsonb_build_object(
      'owner_user_id',p_owner_user_id,'due_at',p_due_at,'status','open'
    ));
  END IF;

  RETURN v_row;
END $function$;

REVOKE ALL ON FUNCTION public.set_production_room_action(uuid,uuid,timestamptz,text) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.post_production_room_action_item(text,uuid,timestamptz) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.set_production_room_action(uuid,uuid,timestamptz,text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.post_production_room_action_item(text,uuid,timestamptz) TO authenticated;

NOTIFY pgrst, 'reload schema';

COMMIT;

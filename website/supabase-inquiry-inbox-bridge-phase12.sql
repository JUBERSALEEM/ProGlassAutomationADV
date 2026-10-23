-- DESKTOP INBOX BRIDGE (ProglassAutomation3.0 docs/07)
--
-- Two additive contracts for the desktop inbox, applied after Phase 8A, 8C,
-- 9D and 9E:
--
--   1. A pipeline status write-back, so the shop PC can report where an
--      inquiry has reached (prepared, sent, confirmed, hold, cancelled)
--      without ever touching a customer-facing column.
--   2. A read-only per-inquiry chat pull, so the shop PC can show the same
--      thread the customer sees, and can never post into it.
--
-- Neither contract changes an existing CHECK constraint, an existing policy,
-- an inbox RPC, the global community room, or any customer-facing behaviour.
-- Both are gated inside the function body by has_inquiry_role, which requires a
-- signed-in user holding an enabled inquiry_bridge row. EXECUTE is revoked from
-- PUBLIC and anon and granted to authenticated only, so an anonymous request
-- reaches nothing at all.
--
-- Applied as migrations `inquiry_inbox_bridge_status_write_back` and
-- `inquiry_inbox_bridge_chat_pull` on project afpbdyamedpanwijxghk.

-- ---------------------------------------------------------------------------
-- 1. Pipeline status write-back
-- ---------------------------------------------------------------------------

BEGIN;

ALTER TABLE public.inquiry_customer_submissions
  ADD COLUMN IF NOT EXISTS pipeline_status     text,
  ADD COLUMN IF NOT EXISTS pipeline_note       text,
  ADD COLUMN IF NOT EXISTS pipeline_updated_at timestamptz,
  ADD COLUMN IF NOT EXISTS pipeline_updated_by text;

CREATE OR REPLACE FUNCTION public.update_customer_inquiry_status_v1(
  p_submission_id uuid,
  p_status text,
  p_note text
)
RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_old text;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE = '42501';
  END IF;
  IF p_status NOT IN ('prepared', 'sent', 'confirmed', 'hold', 'cancelled') THEN
    RAISE EXCEPTION 'Unknown pipeline status: %', p_status USING ERRCODE = '22023';
  END IF;
  SELECT pipeline_status INTO v_old
    FROM public.inquiry_customer_submissions
   WHERE id = p_submission_id FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'Submission not found.' USING ERRCODE = 'P0002';
  END IF;
  UPDATE public.inquiry_customer_submissions
     SET pipeline_status     = p_status,
         pipeline_note       = coalesce(p_note, ''),
         pipeline_updated_at = now(),
         pipeline_updated_by = auth.uid()::text
   WHERE id = p_submission_id;
  INSERT INTO public.inquiry_customer_events(submission_id, event_type, details, customer_visible)
  VALUES (p_submission_id, 'status_changed',
          jsonb_build_object('source', 'desktop_inbox', 'from', v_old, 'to', p_status, 'note', coalesce(p_note, '')),
          false);
END $$;
REVOKE ALL ON FUNCTION public.update_customer_inquiry_status_v1(uuid, text, text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.update_customer_inquiry_status_v1(uuid, text, text) TO authenticated;

NOTIFY pgrst, 'reload schema';
COMMIT;

-- ---------------------------------------------------------------------------
-- 2. Read-only per-inquiry chat pull
--
-- There is deliberately no companion write function. The desktop pulls the
-- thread and renders it; posting stays with the people who are allowed to post.
-- ---------------------------------------------------------------------------

BEGIN;

CREATE OR REPLACE FUNCTION public.list_customer_inquiry_communications_v1(p_submission_id uuid)
RETURNS TABLE (
  id uuid,
  visibility text,
  message_kind text,
  request_status text,
  body text,
  author_display_name text,
  created_at timestamptz
)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_bridge']) THEN
    RAISE EXCEPTION 'Inquiry bridge role required.' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY
    SELECT c.id, c.visibility, c.message_kind, c.request_status, c.body,
           coalesce(p.display_name, 'Website user'), c.created_at
      FROM public.inquiry_communications c
      LEFT JOIN public.user_profiles p ON p.user_id = c.author_user_id
     WHERE c.submission_id = p_submission_id
     ORDER BY c.created_at, c.id;
END $$;
REVOKE ALL ON FUNCTION public.list_customer_inquiry_communications_v1(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.list_customer_inquiry_communications_v1(uuid) TO authenticated;

NOTIFY pgrst, 'reload schema';
COMMIT;

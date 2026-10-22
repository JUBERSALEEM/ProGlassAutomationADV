-- Phase 11 privilege tightening for the chat pipeline and the production room.
--
-- Why this exists: Supabase grants ALL on every new table in `public` to
-- `anon`, `authenticated` and `service_role` by default. The Phase 11
-- migrations revoked ALL from `anon` and revoked INSERT/UPDATE/DELETE from
-- `authenticated` on the chat tables, which left three privileges behind:
--
--   TRUNCATE   ignores Row Level Security entirely, so it must never be
--              reachable by a signed-in browser session. PostgREST does not
--              expose it today, which is luck, not a control.
--   TRIGGER    allows a session to attach a trigger to the table.
--   REFERENCES allows it to point a foreign key at the table.
--
-- The account page was also left with DELETE on `user_profiles`, which it never
-- calls. The app has no `.delete()` on any table; message deletion is an RPC.
--
-- The room tables carry the same default grants for the same reason.
--
-- Verified afterwards: `authenticated` holds SELECT on the seven read-only
-- tables, SELECT/INSERT/UPDATE on `user_profiles`, and nothing on `anon`.
-- Every write path is a SECURITY DEFINER function with a pinned search_path,
-- so it runs as the owner and is unaffected by these revokes.

BEGIN;

REVOKE ALL ON TABLE
  public.global_chat_messages,
  public.global_chat_reads,
  public.global_chat_events,
  public.global_chat_receipts,
  public.user_profiles,
  public.production_room_messages,
  public.production_room_members,
  public.production_room_reads,
  public.production_room_events
FROM anon, authenticated;

GRANT SELECT ON TABLE
  public.global_chat_messages,
  public.global_chat_reads,
  public.global_chat_events,
  public.global_chat_receipts,
  public.production_room_messages,
  public.production_room_members,
  public.production_room_reads,
  public.production_room_events
TO authenticated;

-- The account page writes its own profile row directly, so those three stay.
GRANT SELECT, INSERT, UPDATE ON TABLE public.user_profiles TO authenticated;

NOTIFY pgrst, 'reload schema';

COMMIT;

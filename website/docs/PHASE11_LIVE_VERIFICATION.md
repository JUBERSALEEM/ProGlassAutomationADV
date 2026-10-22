# Phase 11 live verification runbook

**Scope:** the Phase 11 migrations and the room behaviour they support.

Status on 2026-10-22: sections 2 and 3 are **done on project
`afpbdyamedpanwijxghk`** and recorded in `PHASE11_CHAT_PIPELINE.md`. What remains
is section 4 onwards, which needs two signed-in accounts and a moderator. Only
one project exists in this organisation, so the browser matrix runs against that
project; the data it creates is three chat messages and their receipts.

## 1. Prerequisites

- [ ] A disposable Supabase project, matching the one used for Phase 8A to 8C.
- [ ] The Phase 8 and Phase 9 migrations already applied, because
      `global_chat_events` reuses `public.inquiry_operator_roles` and
      `public.has_inquiry_role` in its audit policy.
- [ ] Two customer accounts (A and B) and one admin account.
- [ ] A local `.env.local` holding the project URL and anon key. The anon key is
      never pasted into a document, a commit or a chat.

## 2. Application order

Apply as three separate migrations, in this order, stopping after each one to
run its verification block. A failure at any step is a stop, not a retry.

| Order | File | Adds |
|---|---|---|
| 1 | `supabase-global-chat.sql` | `global_chat_messages`, `global_chat_reads`, `global_chat_events`, 3 policies, 4 RPCs, Realtime publication |
| 2 | `supabase-global-chat-v2-replies-receipts.sql` | `global_chat_receipts`, `reply_to_message_id`, the two-argument post RPC, 1 policy, 2 receipt RPCs |
| 3 | `supabase-user-profiles.sql` | `user_profiles`, 3 policies, `touch_user_profiles_updated_at` |
| 4 | `supabase-phase11-grant-tightening.sql` | revokes the unused default privileges; leaves `SELECT` on the seven read-only tables and `SELECT, INSERT, UPDATE` on `user_profiles` |

Existing inquiry, intake, estimator, assignment and delivery counts are captured
before step 1 and compared after step 3. Any change is a failure.

## 3. Structural verification

Run after all three migrations. Read-only.

```sql
select 'tables' check_name, count(*)::text result from information_schema.tables
where table_schema = 'public' and table_name in
  ('global_chat_messages','global_chat_reads','global_chat_events','global_chat_receipts','user_profiles')
union all select 'rls_tables', count(*)::text from pg_tables where schemaname = 'public'
  and tablename in ('global_chat_messages','global_chat_reads','global_chat_events','global_chat_receipts','user_profiles')
  and rowsecurity
union all select 'policies', count(*)::text from pg_policies where schemaname = 'public'
  and tablename in ('global_chat_messages','global_chat_reads','global_chat_events','global_chat_receipts','user_profiles')
union all select 'rpcs', count(*)::text from information_schema.routines where routine_schema = 'public'
  and routine_name in ('post_global_chat_message','delete_global_chat_message','mark_global_chat_read',
    'mark_global_chat_receipts_delivered','mark_global_chat_receipts_read','global_chat_display_name')
union all select 'publication', count(*)::text from pg_publication_tables where pubname = 'supabase_realtime'
  and tablename in ('global_chat_messages','global_chat_receipts','user_profiles')
union all select 'anon_grants', count(*)::text from information_schema.role_table_grants
where table_schema = 'public' and grantee = 'anon'
  and table_name in ('global_chat_messages','global_chat_reads','global_chat_events','global_chat_receipts','user_profiles')
union all select 'write_grants', count(*)::text from information_schema.role_table_grants
where table_schema = 'public' and grantee = 'authenticated' and privilege_type in ('INSERT','UPDATE','DELETE')
  and table_name in ('global_chat_messages','global_chat_reads','global_chat_events','global_chat_receipts');
```

Expected: 5 tables, 5 RLS tables, 7 policies, **6** chat routines (`post` exists
only with its two-argument signature), 3 published tables, **0** anon grants,
**0** write grants on the four chat tables. Verified on 2026-10-22.

## 4. Functional matrix

Run in a browser against the running site with two private windows.

| # | Actor | Action | Expected |
|---|---|---|---|
| 1 | signed out | open `/{locale}/chat` | sign-in gate, no message content, no Realtime subscription attempt |
| 2 | signed out | open `/{locale}/inquiries/chat` | routed to the account page with a same-origin return path |
| 3 | A | send `Hello from A` | message appears without refresh; composer clears; focus stays in the composer |
| 4 | B | watch the room | message arrives live, one unread badge, unread divider above it |
| 5 | B | open Message info on A's message | delivered becomes read, and A sees the tick change on the next reconcile |
| 6 | A | reply to B's message | quoted block shows the original; "Jump to the original message" scrolls and flashes it |
| 7 | A | search a word that only exists in an old, unloaded message | result row appears, selecting it loads older pages until the message is shown |
| 8 | A | search `50%` | no wildcard behaviour: only messages containing the literal text are returned |
| 9 | A | type `@B`'s display name, send | B sees the mention marked in the bubble and the screen-reader text "mentions you"; C sees plain text |
| 10 | A | delete own message | body replaced for everyone, audit row added, receipts retained |
| 11 | B | attempt to delete A's message | refused: the delete button is absent for a non-author, non-moderator |
| 12 | admin | delete any message | allowed, audit row recorded |
| 13 | A | open `/{locale}/account`, change avatar icon and colour | the room avatar changes after reload; other users cannot change it |
| 14 | A | reload after a successful sign-in | stays signed in, no login bounce |
| 15 | A | sign in while the project is unreachable | "Saved sign-in could not be confirmed yet" with Retry, not a password prompt |

Record the migration result, the HTTP status of `/chat`, `/account` and
`/inquiries/chat`, and any console or network error per row.

## 5. Negative checks

- Direct `INSERT`, `UPDATE` or `DELETE` against the four chat tables as
  `authenticated` is refused; writes exist only through RPCs.
- A message body of 1,001 characters is refused by the RPC.
- A receipt with `read_at` set and `delivered_at` null is refused by the
  constraint.
- Deleting a message leaves an empty body plus the deleting actor, never a
  half-deleted row.
- `anon` cannot read messages even with a known project URL.

## 6. Failure behaviour

- Realtime unavailable: the room stays readable, shows "Auto-refresh on", and
  converges within three seconds.
- Search unavailable: an inline error appears inside the search panel; the room
  keeps working.
- Session refresh refused: one forced refresh is attempted, then the sign-in
  gate; a transport failure never signs the user out.

## 7. Rollback

The migrations are additive. Rollback drops, in reverse order:
`global_chat_receipts`, `global_chat_events`, `global_chat_reads`,
`global_chat_messages`, `user_profiles`, and the seven functions. No inquiry,
intake, estimator or delivery table is touched, so the rest of the site keeps
working with chat links removed from navigation.

## 8. Evidence to record

Append one block per run to `PHASE11_CHAT_PIPELINE.md`: date, project
identifier (never the key), migration files applied, the structural result
line, the matrix rows that passed, and anything that did not.

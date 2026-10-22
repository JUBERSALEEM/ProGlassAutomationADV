# Phase 11, inquiry chat pipeline and community room

**Scope:** `website/` only. No WPF source, migration, production data or secret
was touched.
**Branch:** `arena/2611f8ff-proglassautomationadv`
**Status:** implemented in source, local gates green, all four migrations applied
and structurally verified on project `afpbdyamedpanwijxghk` on 2026-10-22, live
browser matrix still open.

## Why this is Phase 11

Phase 9 delivered the estimator workflow (9A to 9E). Phase 10 was taken by the
responsive and theme redesign, so the next operational phase kept the number 11.
`PHASE9_ESTIMATOR_AND_PRODUCTION_COORDINATION.md` records the production
coordination room as Phase 11 scope. This document records the part of that
phase that is actually built: the shared room and the customer chat channel the
room runs on.

The coordination room's own categories (general, urgent blocker, material
shortage, machine issue, quality issue, schedule change, dispatch dependency,
decision, action item), action owner/due/status, pinned decisions and private
attachments are **not** built and are not claimed here.

## What was built

| Surface | Route | Component |
|---|---|---|
| Community room | `/[locale]/chat` | `src/components/chat/GlobalChatWorkspace.tsx` |
| Customer inquiries and their chat | `/[locale]/inquiries/chat` | `src/components/inquiries/CustomerInquiryChatWorkspace.tsx` |
| Account, profile and sign-in | `/[locale]/account` | `src/components/account/CustomerAccountWorkspace.tsx` |

Supporting client modules: `lib/global-chat.ts` (normalisation, unread
arithmetic, excerpting), `lib/customer-inquiry-chat.ts`,
`lib/useCustomerInquiries.ts`, `lib/user-profile.ts`, `lib/website-session.ts`
(persisted session with one forced refresh on a refused token),
`types/global-chat.ts`, `types/user-profile.ts`.

### Room behaviour

- [x] Signed-in only. Signed-out visitors are routed through `account` with a
      safe same-origin return path before any room or private channel loads.
- [x] Realtime subscription plus a 3 second reconcile tick, so a dropped socket
      converges instead of freezing the room.
- [x] Replies with quoted context and jump to the original message.
- [x] Per-message delivered and read receipts, with a "who read when" panel.
- [x] Database unread count, an unread divider in the stream and a
      jump-to-latest control.
- [x] Day dividers, older-message paging that holds scroll position, and a
      dense one-line bubble layout that keeps sixteen short messages readable.
- [x] Delete for everyone, bounded to the author or an inquiry admin, with an
      immutable audit event.
- [x] Profile display name, avatar icon and avatar colour, shared by the room
      and the account page.
- [x] Bounded composer, 1,000 characters, Enter to send, Shift plus Enter for a
      new line, character warning near the limit.
- [x] Honest gates for "chat is not configured", "we could not confirm your
      saved sign-in" and "sign in to join", never a fake empty room.
- [x] Search across the whole room. The query runs against the table with a
      quoted, escaped `ilike`, so a match in an unloaded page is found and a
      term containing `%`, `*`, `_` or `"` cannot change the query. The result
      list is capped at 30, newest first, and says when the cap was reached.
      Selecting a result loads older pages until the message is on screen, then
      highlights it.
- [x] Mentions. `@` plus a member's display name, or the first word of it, is
      marked in the bubble for the member it names and announced as "mentions
      you"; another member's name stays plain text.

### Wiring

- [x] Navigation, footer and the account hub link to `account`,
      `inquiries/chat` and `chat`, with icons and localized labels.
- [x] Storage of a signing-in redirect is validated as same-origin before use.
- [x] CSP allows the project's Realtime WebSocket origin
      (`wss://afpbdyamedpanwijxghk.supabase.co`) in production, because some
      browsers do not treat an `https` `connect-src` source as permission for
      `wss`.
- [x] Transport failures are distinguished from refused tokens, so an
      unreachable Supabase project cannot be mistaken for an expired session and
      bounce a signed-in user back to the login form.

## Applied on the project (2026-10-22)

Migration history from the project, in application order:

| Version | Name |
|---|---|
| `20261019043256` | `production_room_phase11a` |
| `20261019072133` | `global_community_chat` |
| `20261019074255` | `global_community_chat_replies_receipts` |
| `20261019084503` | `customer_user_profiles_avatar` |
| `20261022` | `phase11_revoke_unused_table_privileges` |

Read-only verification returned, for the five chat and profile tables: **5
tables, 5 with RLS, 7 policies, 6 chat routines, 3 Realtime-published tables, 3
tables with `REPLICA IDENTITY FULL`, 10 indexes, 0 anon grants**. The
one-argument `post_global_chat_message(text)` no longer exists because the v2
migration replaced it with the two-argument signature, which is what the browser
calls.

Live content at the time of verification: 3 messages, 1 receipt, 3 audit
events, 2 profiles. Untouched by this work: 7 customer submissions, 23
documents, 2 production room members.

### Privilege drift found and fixed

Supabase grants `ALL` on every new table in `public` by default. The Phase 11
migrations revoked `ALL` from `anon` and revoked `INSERT`, `UPDATE`, `DELETE`
from `authenticated` on the chat tables, which left three privileges behind, and
left `DELETE` on `user_profiles`:

| Table group | Was | Is now |
|---|---|---|
| four chat tables | `SELECT, REFERENCES, TRIGGER, TRUNCATE` | `SELECT` |
| `user_profiles` | `DELETE, INSERT, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE` | `SELECT, INSERT, UPDATE` |
| four production room tables | `SELECT, REFERENCES, TRIGGER, TRUNCATE` | `SELECT` |
| `anon` | 0 | 0 |

`TRUNCATE` was the one that mattered: it ignores Row Level Security entirely, so
it must not be reachable by a signed-in browser session even though PostgREST
does not expose it. Every write path is a `SECURITY DEFINER` function with a
pinned `search_path`, and no application code calls `.delete()` on any table, so
the revoke removes nothing the site uses. `supabase-phase11-grant-tightening.sql`
carries the change so the repository matches the project.

## Database contract

Three additive migrations, applied in this order. None of them alters an
existing inquiry, intake, estimator or delivery table.

| File | Objects |
|---|---|
| `supabase-global-chat.sql` | `global_chat_messages`, `global_chat_reads`, `global_chat_events`, 3 policies, 4 RPCs |
| `supabase-global-chat-v2-replies-receipts.sql` | `global_chat_receipts`, reply column and RPC overload, 1 policy, 2 RPCs |
| `supabase-user-profiles.sql` | `user_profiles`, 3 policies, 1 trigger function |

Totals: 5 tables, 7 policies, 7 RPC signatures, Realtime publication for
messages, receipts and profiles, and `REPLICA IDENTITY FULL` on the three
published tables.

Writes are RPC only. `authenticated` holds `SELECT` on the chat tables and
`SELECT, INSERT, UPDATE` on `user_profiles` own rows, and `INSERT`, `UPDATE`,
`DELETE` are revoked from `authenticated` on the chat tables. `anon` holds
nothing.

Bounded values: body 1 to 1,000 characters, display name 1 to 60, avatar icon
and colour from allowlists, a receipt must carry `delivered_at` before
`read_at`, a deleted message must hold an empty body and the deleting actor.

### Read-only verification

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
union all select 'messages', count(*)::text from public.global_chat_messages
union all select 'receipts', count(*)::text from public.global_chat_receipts
union all select 'profiles', count(*)::text from public.user_profiles;
```

Expected after a clean application: 5 tables, 5 RLS tables, 7 policies, 7 RPC
rows (`post_global_chat_message` counts twice, once per signature), 3 published
tables, and zero rows in the three content tables. Existing inquiry, intake,
estimator, assignment and delivery counts must be unchanged.

## Tests

| File | Covers |
|---|---|
| `tests/global-chat-workspace.test.tsx` | unread rendering, no realtime restart on duplicate auth events, sixteen-message density, room surviving a Realtime setup throw |
| `tests/customer-account-workspace.test.tsx` | saved session, icon save, redirect continuation, signed-out copy |
| `tests/website-session.test.tsx` | invalid first read refresh, unreachable auth does not sign out, one refresh after a 401 |
| `tests/global-chat-search.test.tsx` | search term normalisation, the query-safe pattern, excerpt around a mid-message match, mention matching and splitting |
| `scripts/inquiry-global-chat.test.mjs` | 18 static contracts: signed-in-only SQL, RLS and RPC-only writes, realtime publication, reply and receipt safety, browser-direct room, reply and tick UI, account profile scope, whole-table search, mention scope |

## Gates (2026-10-22)

- `npx tsc --noEmit` clean.
- `npm run lint` clean.
- `npx vitest run` 16 files, 160 tests passed.
- `node --test scripts/*.test.mjs` 562 tests, 562 passed.
- `npm run build` succeeded, 533 prerendered routes, build cache recorded.
- Chat security script alone: 18 of 18 passed.

## Not done

- [x] The four migrations are applied and recorded on the project, with the
      structural verification above.
- [ ] The browser matrix has not been run. The structural side is proven above;
      what remains is two signed-in accounts plus a moderator walking
      `PHASE11_LIVE_VERIFICATION.md` section 4.
- [ ] Repository changes for this phase are uncommitted at the time of writing.
- [ ] Room features still absent: attachments, pinned decisions, message
      editing (delete only), presence and typing.
- [ ] The production coordination room has a complete database layer
      (`production_room_phase11a`: 4 tables, 9 RPCs, 4 policies, membership,
      read position, categories, pinning, edit and delete) and **no website
      surface at all**. Nothing in `src/` references it. This is the largest
      remaining piece of Phase 11 as originally scoped.
- [ ] Locale translations for the new copy; the pages currently fall back to
      their English source strings.

## Must not change

- The Phase 8 intake contract, the 15 file and 30 MB limits, and the customer
  submission tables.
- The estimator RLS and assignment invariants from Phase 9A to 9D.
- The Phase 8C pull bridge and its isolated inbox boundary. The chat pipeline
  never writes desktop SQLite and is never a second upload path.
- The read-only desktop publication projection.

# Phase 11, inquiry chat pipeline and community room

**Scope:** `website/` only. No WPF source, migration, production data or secret
was touched.
**Branch:** `arena/2611f8ff-proglassautomationadv`
**Status:** implemented in source, local gates green, migrations written but not
recorded as applied, live matrix open.

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

- [ ] The three migrations are written but nowhere recorded as applied. No
      structural verification result exists for them.
- [ ] No live matrix has been run against a disposable Supabase project.
      `PHASE11_LIVE_VERIFICATION.md` holds the exact procedure, the structural
      query with expected counts, the 15-row functional matrix, the negative
      checks, the failure behaviour and the rollback order.
- [ ] Repository changes for this phase are uncommitted at the time of writing.
- [ ] Room features still absent: attachments, pinned decisions, message
      editing (delete only), presence and typing.
- [ ] Production coordination categories and action items.
- [ ] Locale translations for the new copy; the pages currently fall back to
      their English source strings.

## Must not change

- The Phase 8 intake contract, the 15 file and 30 MB limits, and the customer
  submission tables.
- The estimator RLS and assignment invariants from Phase 9A to 9D.
- The Phase 8C pull bridge and its isolated inbox boundary. The chat pipeline
  never writes desktop SQLite and is never a second upload path.
- The read-only desktop publication projection.

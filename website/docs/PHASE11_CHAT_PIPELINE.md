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
- [x] Repository changes committed and pushed (`2304adc`).
- [ ] Room features still absent: attachments, pinned decisions, message
      editing (delete only), presence and typing.
- [x] The production coordination room now has a website surface at
      `/[locale]/production`, built on the nine applied RPCs. See the section
      below.
- [ ] Locale translations for the new copy. The three new pages write their
      copy directly rather than through `t()`, so they render English inside
      otherwise translated chrome. The dictionaries stand at `en-US` 100
      percent, six locales at 75 to 76 percent and six at 10 to 11 percent, so
      parity for the whole site is a separate, mechanical programme rather than
      a Phase 11 close-out item.

## The production coordination room

Route: `/[locale]/production`, component
`src/components/production/ProductionRoomWorkspace.tsx`, contract
`src/lib/production-room.ts` and `src/types/production-room.ts`.

The database layer predates the UI: migration `production_room_phase11a`
created four tables, nine functions and four policies, and the browser never
used them. The original migration file was applied through the dashboard and
never committed, so `supabase-production-room-phase11a.sql` reconstructs it from
the live project and is idempotent; applying it to a project that already has
the objects changes nothing. A contract test asserts the file and the component
agree.

### What the UI does

- [x] Membership gate. A signed-in visitor without an active membership row is
      told the room is empty on purpose and shown nothing; RLS would have
      returned no rows anyway, and pretending that is an empty room would be a
      lie.
- [x] Nine categories, each with a sentence describing what belongs in it, and
      filter chips carrying live counts.
- [x] Composer with category selection, 4,000 character bound, Enter to post,
      Shift plus Enter for a new line, Escape to cancel a reply.
- [x] One level of threading. A reply inherits the root category, because the
      insert function does.
- [x] Pinned decisions strip. Pin and unpin are offered only on a live,
      top-level decision, which is exactly what the check constraint allows.
- [x] Author edit and delete inside thirty minutes; a room admin can delete at
      any time. Buttons are hidden when the write would be refused, rather than
      failing after the click.
- [x] Search, unread-only filter, unread divider and jump to the first unread.
- [x] Member administration for room admins and inquiry admins: register by
      email, deactivate, reactivate. The database refuses to remove the last
      active room admin.
- [x] Realtime on `production_room_messages` plus a twelve second reconcile, so
      a dropped socket degrades to slower rather than stuck.
- [x] Read position written through `mark_production_room_read`, coalesced by a
      1.2 second timer.
- [x] Honest states: not configured, saved sign-in unconfirmed, signed out, not
      a member, loading, no posts, nothing matches the filter.
- [x] `robots: noindex, nofollow`, and the route is absent from navigation,
      the footer and the sitemap. A room that names customers, machines and
      dates has no business in an index.

### Action items

An `action_item` is the only post that carries structured work: an owner, a due
time and a status. The composer shows the owner and due fields the moment the
category is `action_item`, and hides them again for every other category, so the
shape cannot drift by accident. The database enforces what the camera roll
cannot see:

| Field | Rule |
|---|---|
| `action_owner_user_id` | must be an active room member, `ON DELETE RESTRICT` |
| `action_due_at` | within five years either side of now |
| `action_status` | `open`, `done` or `cancelled`, `open` on creation |

Constraint `production_room_messages_action_shape_check` allows three empty
fields or three fields on a live, top-level `action_item`; a reply can never
carry them. A partial index on `(action_due_at, id)` keeps the overdue filter
cheap.

Rights mirror the rest of the room. A room admin or the author can assign and
re-date; the current owner can close or reopen what they own but cannot hand it
to somebody else. A member who is neither author, owner nor admin sees no action
control at all rather than a button that would be refused.

- `post_production_room_action_item(p_body, p_owner_user_id, p_due_at)` inserts
  the post as `action_item` with status `open` and audits `message_created` plus
  `action_assigned` when an owner is named.
- `set_production_room_action(p_message_id, p_owner_user_id, p_due_at, p_status)`
  is the only way to change the three fields and writes exactly one event:
  `action_assigned`, `action_updated`, `action_completed` or `action_reopened`.

The screen adds three filter chips (`open actions`, `mine`, `overdue`) and three
stat rows (open, overdue, mine), all derived from the same loaded thread, so a
count and its list can never disagree.

### Database change made with the UI

The room tables had never been added to the Realtime publication, so a browser
subscription could not fire and "Live" would have been a false claim. Migration
`production_room_realtime` adds `production_room_messages` to
`supabase_realtime`, and sets `REPLICA IDENTITY FULL` on the messages and
members tables. Published tables in the project are now four: the three chat and
profile tables plus the room's message table.

### Verification

- `tests/production-room.test.tsx`: 32 assertions over normalisation,
  threading, unread arithmetic, the three permission mirrors (edit window,
  delete window, pin shape) and the action helpers (status labels, overdue and
  due today, reassignment rights).
- `tests/production-room-workspace.test.tsx`: 9 DOM tests: the non-member gate
  renders no composer, a member posts with the selected category, a reply
  inherits the root category, a pin is offered only on a decision, edit and
  delete disappear after thirty minutes, an action item posts with its owner and
  due time in one call, an owner closes their own item, and a member who neither
  wrote nor owns an item is offered no control at all.
- `scripts/inquiry-production-room.test.mjs`: 7 static contracts: RLS and
  RPC-only writes, the nine categories matching the check constraint, the 30
  minute and 4,000 character bounds matching the SQL, noindex and no navigation,
  no fake data path, the action columns with their shape constraint, and the two
  action functions with their grants.
- Applied and verified on the project: 4 tables, 4 policies, 11 functions, 1
  published table, `SELECT` only for `authenticated`, nothing for `anon`.

### Still open in the room

- [ ] Attachments. The table has no document column and no storage policy, so
      this is a schema change plus a bucket policy, not a UI addition.
- [ ] Optional stable links to PI, job order, delivery or inquiry.
- [ ] Mentions and typing indicators.
- [ ] Locale translations, as with the chat pages.

## Distance to a WPF handoff

The WPF boundary in `PHASE9_ESTIMATOR_AND_PRODUCTION_COORDINATION.md` gates
desktop work on the Phase 9 assignment and handoff contracts, not on website
polish. Those contracts are applied and verified except for the Windows live
run. Nothing above blocks the desktop, and nothing below is required before it.

| Gate | State | Owner |
|---|---|---|
| Phase 9A to 9D RLS, assignment and communication | applied and live-tested | closed |
| Phase 9E handoff schema and v2 manifest | applied, 3 delivery columns and 3 RPCs verified | closed |
| Phase 9E dry-run, apply and recovery on Windows | open | needs the Windows PC |
| WPF compiles at all | open: no .NET SDK has ever built this tree | needs the Windows PC |
| An inbox-consumption destination in WPF | open: zero references to an inbox in C# | design, then implementation |
| Website chat pipeline | implemented, schema verified | independent of WPF |
| Production room | UI, schema and action items implemented, realtime published | independent of WPF |

## Must not change

- The Phase 8 intake contract, the 15 file and 30 MB limits, and the customer
  submission tables.
- The estimator RLS and assignment invariants from Phase 9A to 9D.
- The Phase 8C pull bridge and its isolated inbox boundary. The chat pipeline
  never writes desktop SQLite and is never a second upload path.
- The read-only desktop publication projection.

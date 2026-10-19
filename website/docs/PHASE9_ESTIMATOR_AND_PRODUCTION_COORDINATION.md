# Phase 9 estimator workflow and Phase 10 production coordination

Status: Phases 9A–9E are implemented and live-verified. The owner confirmed on 19 October 2026 that the Phase 9E Windows bridge dry-run/apply/recovery and assigned/unassigned/reassignment verification passed. Phase 11A–11B are implemented in the repo, and the Phase 11A migration was applied to live Supabase project `afpbdyamedpanwijxghk` on 19 October 2026 as migration version `20261019043256` (`production_room_phase11a`); structural verification passed. Two owner-designated Management accounts have since been registered as active room admins. The owner decided not to merge this branch into another branch and to use it as the main branch; production use still requires that branch to be the deployed main branch and the live identity matrix to pass. Phase 10 responsive UI redesign is implemented except for the final browser visual review.

## Verified reports-page pattern to reuse

The real `/reports` implementation reads `public.records` and supplies six filtered summary cards (entries, quantity, SQM, average area, companies, salesmen), clickable Top-6 breakdowns (prepared by, salesman, company, work type), URL-synchronized search/date/source/company/salesman/preparer/status/PI/work/Zoho filters, sortable/paginated compact or comfortable records, detail inspection, exports, and confirmed-PI links to Job Orders. Phase 9 will reuse these interaction patterns but will calculate only genuine inquiry/assignment timestamps and counts.

## Phase 9A — estimator identity and assignment foundation

- [x] Add `inquiry_estimator` to the database-owned role allowlist.
- [x] Add bounded estimator directory, active/inactive state, immutable assignment history, primary/collaborator roles, and audit events.
- [x] Enforce one active primary and no duplicate active member per inquiry.
- [x] Add admin-only constrained registration, assignment/reassignment, and assignment-ending RPCs.
- [x] Reject missing users, conflicting existing roles, incomplete submissions, inactive estimators, duplicate members, and overlong reasons.
- [x] Add estimator RLS for actively assigned complete submissions, document metadata, and private objects only.
- [x] Add TypeScript contracts and static security tests.
- [x] Apply the authorized migration and one-time qualified-ID RLS repair to disposable Supabase; verify 3 tables, 3 RPCs, 3 RLS tables, 6 policies, role constraint, zero initial rows, and preserved 7 submissions/23 documents/1 delivery.
- [x] Run `npm run inquiries:estimators:live-test`: two distinct identities, unassigned denial, primary/collaborator isolation, assigned private download, unassigned record/document denial, disabled-role denial, re-enable, reassignment access loss, one-primary invariant, and preserved/audited history all passed. Final state is one active primary (`EST-COLLAB-TEST`) on `WEB-2026-000007`, 5 history rows, and 4 assignment events.

Migration: `supabase-inquiry-estimator-phase9a.sql`. Deployments that applied its original revision must also apply the one-time `supabase-inquiry-estimator-phase9a-rls-repair.sql`, which qualifies the outer submission ID in the estimator policy; it changes no assignment data.

After authorized application, structural verification is read-only:

```sql
select 'tables' check_name,count(*)::text result from information_schema.tables
where table_schema='public' and table_name in ('inquiry_estimators','inquiry_estimator_assignments','inquiry_estimator_events')
union all select 'rpcs',count(*)::text from information_schema.routines where routine_schema='public'
  and routine_name in ('register_inquiry_estimator','set_customer_inquiry_estimators','end_customer_inquiry_estimator_assignment')
union all select 'rls',count(*)::text from pg_tables where schemaname='public'
  and tablename in ('inquiry_estimators','inquiry_estimator_assignments','inquiry_estimator_events') and rowsecurity
union all select 'estimator_policies',count(*)::text from pg_policies where schemaname='public'
  and policyname like '%estimator%'
union all select 'estimator_rows',count(*)::text from public.inquiry_estimators
union all select 'assignment_rows',count(*)::text from public.inquiry_estimator_assignments
union all select 'event_rows',count(*)::text from public.inquiry_estimator_events;
```

Expected immediately after migration: 3 tables, 3 RPC overloads, 3 RLS tables, 6 estimator-named policies, and zero rows in all three new tables. Existing inquiry/document/delivery counts must be checked separately and remain unchanged.

## Phase 9B — assignment administration and estimator queue

Planned as one complete batch after 9A passes:

- [x] Admin estimator registration/directory and active-estimator assignment controls.
- [x] Unassigned queue plus primary/collaborator assignment, reassignment, and ending controls.
- [x] Estimator route showing only RLS-assigned complete work, with search and status filters.
- [x] Assigned detail and private document delivery through the existing server-side byte/hash integrity check.
- [x] Primary/team presentation by controlled display name rather than customer identity.
- [x] Separate token/role checks, same-origin mutation checks, bounded queue, and static security assertions.
- [x] Apply and structurally verify `supabase-inquiry-estimator-phase9b-access.sql`; live build 504/504, admin queue/search/filter/profile update, primary+collaborator assignment, both estimator queue/detail/download paths, customer denial, final-primary restoration, former-primary queue/detail/document denial, and admin regression all passed.

## Phase 9C — statistics and short reports

Use the reports-page visual/interaction grammar with inquiry-specific facts:

- [x] Six role-scoped cards: visible inquiries, assigned, unassigned, due today, overdue, completed this month.
- [x] Reports-style Top-6 breakdowns: primary estimator, inquiry type, company, and status.
- [x] Short CSV reports: authorized queue, estimator workload, and inquiry aging/due state.
- [x] Use persisted submitted/status-updated/response-estimate timestamps only; explicitly avoid fabricated production quantities and response values.
- [x] Add URL-synchronized role-scoped search, status, estimator, inquiry-type, company, project, location, received-date, and due-state filters; cards and exports recalculate from the filtered queue.
- [x] Add the sortable operational table with Received, Age, Inquiry Ref, Customer Ref, Company, Project, Location, Inquiry Type, Primary, Collaborators, Documents, Response Due, Last Activity, Status, and Action columns.
- [x] Add detail navigation from each report row.
- [x] Add server-side batched full authorized-queue loading with a fail-closed 10,000-row safety boundary instead of silently reporting from the first page.
- [x] Live-test the six cards, four breakdowns, ten filters, sortable table, detail links, three exports, and role-scoped totals; user confirmed the full Phase 9C matrix passed.

First-opened, waiting-for-customer, first-response, and review-duration metrics remain intentionally excluded until a later additive phase persists those events. “Last Activity” is currently the genuine persisted `status_updated_at` fact.

No metric may be shown until its source timestamp/state is persisted and defined.

## Phase 9D — controlled customer/estimator communication

- [x] Customer-visible messages and requests for missing information.
- [x] Separate private internal notes hidden from customers.
- [x] Immutable audit events, explicit visibility, 4,000-character bounds, and controlled open/resolved request transitions.
- [x] Assigned-estimator/admin access only while customer ownership remains unchanged.
- [x] Same-origin mutation API and one shared conversation panel on customer and estimator inquiry details.
- [x] Apply `supabase-inquiry-estimator-phase9d-communication.sql` after explicit authorization; read-only verification passed with 2 tables, 2 RLS tables, 2 RPCs, 2 policies, 0 messages, and 0 events.
- [x] Live-test customer/primary/collaborator/former-estimator/admin visibility and mutation matrix; user confirmed all Phase 9D checks passed.

Existing inquiry documents remain the protected file exchange for this phase; messages do not create a second upload path or weaken the established 30 MB/15-file controls.

Read-only verification after application:

```sql
select 'tables' check_name,count(*)::text result from information_schema.tables
where table_schema='public' and table_name in ('inquiry_communications','inquiry_communication_events')
union all select 'rls_tables',count(*)::text from pg_tables where schemaname='public'
  and tablename in ('inquiry_communications','inquiry_communication_events') and rowsecurity
union all select 'rpcs',count(*)::text from information_schema.routines where routine_schema='public'
  and routine_name in ('post_inquiry_communication','resolve_inquiry_information_request')
union all select 'policies',count(*)::text from pg_policies where schemaname='public'
  and tablename in ('inquiry_communications','inquiry_communication_events')
union all select 'messages',count(*)::text from public.inquiry_communications
union all select 'events',count(*)::text from public.inquiry_communication_events;
```

Expected immediately after application: 2 tables, 2 RLS tables, 2 RPCs, 2 policies, 0 messages, and 0 events. Existing submission, document, delivery, estimator, and assignment counts must remain unchanged.

## Phase 9E — estimator-aware Windows handoff

- [x] Add manifest v2 with stable primary estimator code and an ordered active primary/collaborator assignment snapshot.
- [x] Route final deliveries through controlled `assigned/<ESTIMATOR_CODE>/` or `unassigned/` folders without customer email in paths.
- [x] Fail closed if assignment or routing changes between plan and acknowledgement.
- [x] Preserve global administrative recovery, byte/hash checks, atomic completion, conflicting-folder detection, and idempotent acknowledgement.
- [x] Preserve the isolated inbox boundary and do not write desktop SQLite.
- [x] Apply `supabase-inquiry-estimator-phase9e-handoff.sql` after explicit authorization; read-only verification passed with 3 delivery columns, 3 v2/helper RPCs, 1 preserved delivery, 0 invalid routes, and 0 invalid snapshots.
- [x] Run dry-run/apply/recovery and assigned/unassigned/reassignment live verification on the Windows bridge environment — owner confirmed completion on 19 October 2026.

Read-only verification after application:

```sql
select 'delivery_columns' check_name,count(*)::text result from information_schema.columns
where table_schema='public' and table_name='inquiry_customer_deliveries'
  and column_name in ('routing_key','primary_estimator_code','assignment_snapshot')
union all select 'v2_rpcs',count(*)::text from information_schema.routines where routine_schema='public'
  and routine_name in ('plan_customer_inquiry_deliveries_v2','acknowledge_customer_inquiry_delivery_v2','customer_inquiry_assignment_snapshot')
union all select 'existing_deliveries',count(*)::text from public.inquiry_customer_deliveries
union all select 'invalid_routes',count(*)::text from public.inquiry_customer_deliveries
  where routing_key is not null and routing_key<>'unassigned' and routing_key!~'^assigned/[A-Z0-9][A-Z0-9_-]{1,29}$'
union all select 'invalid_snapshots',count(*)::text from public.inquiry_customer_deliveries
  where assignment_snapshot is not null and jsonb_typeof(assignment_snapshot)<>'array';
```

Expected structure: 3 delivery columns and 3 v2/helper RPCs. Existing delivery count must be preserved; invalid routes and snapshots must both be 0.

## Phase 10 — responsive UI redesign pattern

Repository-only visual redesign work undertaken while Phase 9E awaited the Windows PC. The website currently presents too many full-width single-card rows and must use the available desktop width more professionally. The user selected a whole-site pattern of four normal content cards and up to six compact statistic cards on desktop, with two on tablet and one on mobile where appropriate.

- [x] Audit all 19 top-level pages and their delegated card components. After screenshot review exposed that the first pass waited until the 1024px breakpoint, move normal collections to 2 cards on narrow phones and 4 cards from 640px; compact statistic collections use 3/5–6 at those tiers.
- [x] Preserve wider two-track layouts, forms, tables, and content-heavy paired panels instead of blindly forcing them into quarter-width cards.
- [x] Preserve existing dashboard four-card breakdowns and six-card operational-stat patterns.
- [x] Add a permanent structural grid test to the density suite.
- [x] Decouple top-panel chrome padding from the global radius-safe card floor, cap navigation/dropdown corners, and restore proper dropdown clipping without changing content-card geometry.
- [x] Give Clay’s neutral Black plate a dedicated genuinely near-black midnight material recipe instead of pastel/gray derived surfaces.
- [ ] Complete browser visual review at representative mobile, tablet, desktop, and wide-desktop widths and correct any content-specific overflow or imbalance before closing Phase 10.

## Phase 11 — production coordination room (separate scope)

One production-only room is preferred over separate meeting/stage chats. Membership is explicit for approved department heads and admins; no access follows automatically from customer, estimator, viewer, auditor, or bridge roles. Even inquiry admins must hold an active room-membership row before they can read or post messages; inquiry-admin authority is used only to bootstrap and manage membership.

Live database status (19 October 2026): Phase 11A and Phase 11B are implemented in the repo. After the owner confirmed Phase 9E completion, `supabase-production-room-phase11a.sql` (SHA-256 `44b0ae7d986f02fb851a2a842c38b7b30cdb86d5d5795b9b79f32142eb93b40a`) was applied to live Supabase project `afpbdyamedpanwijxghk` as migration version `20261019043256` (`production_room_phase11a`). Read-only verification returned 4 tables, 4 RLS tables, 4 policies, 9 functions, and zero rows in all four new tables. Existing counts immediately before and after application were unchanged: 67 records, 1 job order, 7 customer submissions, 23 customer documents, 1 delivery, 2 estimators, 10 estimator assignments, and 6 operator-role rows. After that initial zero-row verification, two owner-designated Management accounts were registered through the reviewed RPC as active room admins; current room counts are 2 members, 2 registration events, 0 messages, and 0 read positions. The owner decided not to merge `arena/f2590052-proglassautomationadv` into another branch and to use it as the main branch. The room is not operationally live until that branch is the deployed main branch and the live identity matrix passes.

Categories are fixed identically in SQL and TypeScript: `general_production`, `urgent_blocker`, `material_shortage`, `machine_issue`, `quality_issue`, `schedule_change`, `dispatch_dependency`, `decision`, and `action_item`.

### Phase 11A — schema, RLS, membership, and RPC controls

- [x] Add the additive migration `supabase-production-room-phase11a.sql` with members, messages, read positions, and immutable audit events.
- [x] Add an explicit production-room member register with display name, department label, room-admin flag, and active state, independent of the one-row `inquiry_operator_roles` contract.
- [x] Add one-room messages with one-level threads/replies; replies inherit the parent category.
- [x] Enable RLS on all four new tables, grant authenticated users `SELECT` only, revoke direct `INSERT`/`UPDATE`/`DELETE`, and route every write through reviewed RPCs.
- [x] Add `is_production_room_member()` and `is_production_room_admin()` helpers for policies and RPC checks.
- [x] Add RPCs to register/update members, set member active state, post, edit, delete, pin/unpin, and mark the room read.
- [x] Bound message bodies to 1–4,000 trimmed characters. Authors may edit/delete only within 30 minutes; room admins may delete at any time. Deletion overwrites the body with a tombstone and unpins the message.
- [x] Allow only top-level `decision` messages to be pinned, and only by active room admins.
- [x] Protect against deactivating or demoting the last active room admin.
- [x] Add TypeScript contracts and static security tests (`npm run test:production-room`).
- [x] Apply the migration after Phase 9E passed and the owner authorized continuation (19 October 2026; migration version `20261019043256`).
- [x] Run the read-only structural verification below after application (4 tables, 4 RLS tables, 4 policies, 9 functions, and zero initial rows).
- [x] Register the first two owner-designated Management accounts as active room admins.
- [ ] Run the live member/non-member/admin matrix after this branch is deployed as the main branch.

After authorized application, structural verification is read-only:

```sql
select 'tables' check_name,count(*)::text result from information_schema.tables
where table_schema='public' and table_name in ('production_room_members','production_room_messages','production_room_reads','production_room_events')
union all select 'rls_tables',count(*)::text from pg_tables where schemaname='public'
  and tablename in ('production_room_members','production_room_messages','production_room_reads','production_room_events') and rowsecurity
union all select 'policies',count(*)::text from pg_policies where schemaname='public'
  and tablename in ('production_room_members','production_room_messages','production_room_reads','production_room_events')
union all select 'functions',count(*)::text from information_schema.routines where routine_schema='public'
  and routine_name in ('is_production_room_member','is_production_room_admin','register_production_room_member','set_production_room_member_active','post_production_room_message','edit_production_room_message','delete_production_room_message','set_production_room_message_pinned','mark_production_room_read')
union all select 'members',count(*)::text from public.production_room_members
union all select 'messages',count(*)::text from public.production_room_messages
union all select 'reads',count(*)::text from public.production_room_reads
union all select 'events',count(*)::text from public.production_room_events;
```

Expected immediately after application: 4 tables, 4 RLS tables, 4 policies, 9 functions, and zero rows in all four new tables. Existing inquiry, estimator, document, delivery, record, and job-order counts must be checked separately and remain unchanged.

### Phase 11B — basic room UI

- [x] Add the protected `/production-room` operator route.
- [x] Add same-origin mutation API routes that forward the signed-in user's token, keeping Supabase RLS as the authority.
- [x] Add stat cards for active members, unread messages, pinned decisions, and visible threads.
- [x] Add URL-synchronized search, category, date-range, and pinned-only filters, expanding matched replies into their threads.
- [x] Add timeline, categorized composer, one-level replies, bounded edit/delete controls, room-admin decision pinning, unread badges, and mark-room-read.
- [x] Add the pinned-decisions strip.
- [x] Add member directory plus manager-only registration/update and deactivate/reactivate controls.
- [x] Add graceful sign-in, non-member/inactive-member, and schema-not-installed states so unconfigured deployments fail safely without exposing room data.
- [ ] Live-test after migration with room-admin, department-head, inactive-member, non-member, and inquiry-admin-without-room-membership identities.

### Phase 11C — deferred scope

- [ ] Mentions.
- [ ] Action owner, due date, and status workflow beyond the `action_item` category label.
- [ ] Optional stable links to PI, Job Order, delivery, and inquiry records.
- [ ] Private bounded attachments, reusing the established staged-upload and byte/hash controls.
- [ ] Decide whether Supabase Realtime is needed; Phase 11B deliberately uses refresh plus unread state, consistent with the rest of the site.

Voice/video, public employee chat, customer chat, and WPF chat synchronization remain excluded initially.

## WPF boundary

No WPF change begins until Phase 9 assignment and handoff contracts pass. The desktop application currently has no customer inquiry workflow; any later work must inspect and define a real inbox-consumption destination rather than inventing one or writing directly to unrelated SQLite tables.

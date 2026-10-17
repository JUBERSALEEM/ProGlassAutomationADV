# Phase 9 estimator workflow and Phase 10 production coordination

Status: Phase 9A prepared locally; migration unapplied. Phase 9B–10 are blocked until 9A is applied and live role isolation passes.

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
- [ ] Apply migration to disposable Supabase only after explicit authorization.
- [ ] Live-test customer, unassigned estimator, assigned estimator, collaborator, auditor, admin, disabled estimator, reassignment, and document isolation.

Migration: `supabase-inquiry-estimator-phase9a.sql`.

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

- Admin estimator directory and activation controls.
- Unassigned queue and assignment/reassignment form with required reason.
- Estimator route showing only assigned work.
- Detail/document access through existing integrity-checked delivery.
- Primary and collaborator presentation without exposing customer auth identity.
- Assignment timeline and access-loss verification after unassignment/disable.

## Phase 9C — statistics and short reports

Use the reports-page visual/interaction grammar with inquiry-specific facts:

- Summary: assigned, new/unopened, due today, overdue, waiting for customer, completed this month.
- Timing: first response, review duration, due compliance, last activity, aging buckets.
- Breakdowns: estimator, inquiry type, company, project location, status, age bucket.
- Table: received, age, reference, customer reference, company, project/location, type, primary/collaborators, documents, response due, last activity, status, action.
- Filters: date, estimator, assigned/unassigned, type, status, company, project, location, documents, due/overdue/waiting/completed.
- Short exports: workload, aging, response performance, source/type, assignment history, customer follow-up, conversion.

No metric may be shown until its source timestamp/state is persisted and defined.

## Phase 9D — controlled customer/estimator communication

- Customer-visible messages and requests for missing information.
- Separate private internal notes.
- Audited message visibility, bounded content, private files, and status transitions.
- Assigned estimator access only; customer ownership remains unchanged.

## Phase 9E — estimator-aware Windows handoff

- Add stable estimator code and assignment snapshot to the inbox manifest.
- Controlled assigned/unassigned subfolders without customer email in paths.
- Preserve global administrative recovery, byte/hash checks, atomic completion, and idempotent acknowledgement.
- Do not write desktop SQLite.

## Phase 10 — production coordination room (separate scope)

One production-only room is preferred over separate meeting/stage chats. Membership is explicit for approved department heads and admins; no access follows automatically from customer, estimator, viewer, or bridge roles.

Planned categories: general production, urgent blocker, material shortage, machine issue, quality issue, schedule change, dispatch dependency, decision, action item.

Planned controls: RLS membership, threads/replies, mentions, unread/read position, pinned decisions, bounded edits/deletion policy, search/date/category filters, action owner/due/status, optional stable links to PI/Job Order/delivery/inquiry, private bounded attachments, and immutable audit events. Voice/video, public employee chat, customer chat, and WPF chat synchronization are excluded initially.

## WPF boundary

No WPF change begins until Phase 9 assignment and handoff contracts pass. The desktop application currently has no customer inquiry workflow; any later work must inspect and define a real inbox-consumption destination rather than inventing one or writing directly to unrelated SQLite tables.

# Phase 9 estimator workflow and Phase 10 production coordination

Status: Phases 9A, 9B, and 9C are implemented and live-verified in disposable Supabase. Phase 9D is next, followed by Phase 9E. Phase 10 remains blocked until all five Phase 9 stages are complete and verified.

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

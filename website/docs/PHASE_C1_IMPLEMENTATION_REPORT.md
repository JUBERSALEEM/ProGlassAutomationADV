# Phase C1 implementation report

PI identity contract and additive database schema. No production database was
touched, nothing was deployed, and the WPF repositories were not modified.

Validated on 2026-10-24 against a disposable PostgreSQL 17.9 instance rebuilt from
this repository's migrations. Sections 1 to 15 are the implementation and the
static and automated evidence; sections 16 to 18 are the executed database
validation, the risks it exposed, and the verdict.

Labels: **Verified fact**, **Implemented**, **Not applied**, **Requires business
approval**, **Limitation**.

---

## 1. Verified repository, branch and commit

| Item | Value |
| --- | --- |
| Repository | `JUBERSALEEM/ProGlassAutomationADV` |
| Branch | `arena/2611f8ff-proglassautomationadv` |
| Starting commit | `10f56b926430eaf784b33cfb917f31a25de81975` |
| Remote | `git ls-remote` returned the same SHA, so local and origin matched before the work |
| Working tree at start | clean apart from the two untracked Phase B documents |
| Ending commit | committed and pushed to `arena/2611f8ff-proglassautomationadv` with the Phase C2 to C5 work, per the later instruction to stop keeping local-only commits |
| Revalidated | 2026-10-24, migration applied twice and all 27 probes re-run on the disposable PostgreSQL 17.9 used for C5 |
| Validation run | 2026-10-24, disposable PostgreSQL 17.9, section 16 |

**Verified fact** before writing anything:

- `website/supabase-inquiry-proforma-phase15.sql` defines `inquiry_proformas`
  with `proforma_number ~ '^PI-(WEB|DSK)-[0-9]{4}-[0-9]{6}$'`, the constraint
  `inquiry_proformas_source_identity` requiring `inquiry_id IS NULL` for
  `source_system = 'desktop'`, the unique index
  `inquiry_proformas_source_number` on `(source_system, source_proforma_number)`,
  and the partial unique index `inquiry_proformas_open_draft`.
- `website/supabase-inquiry-intake.sql` defines
  `inquiry_customer_submissions.id uuid PRIMARY KEY` with the human reference
  `submission_reference` (`WEB-YYYY-NNNNNN`), and states that website
  submissions are kept separate from desktop-published `public.inquiries`.
- `website/supabase-inquiries.sql` defines `public.inquiries` as the published
  mirror, `id uuid`, `inquiry_number ~ '^INQ-[0-9]{4}-[0-9]{4,}$'`, unique
  `(source_id, source_inquiry_id)`, `availability` in
  `('available', 'source_missing', 'tombstoned')`, and the publication path
  `begin_inquiry_sync_run` / `publish_inquiry_snapshot` /
  `complete_inquiry_sync_run` gated on `has_inquiry_role(ARRAY['inquiry_bridge'])`
  with `inquiry_sources.bridge_user_id = auth.uid()`.
- `public.inquiries` grants `SELECT` to `authenticated` and reads are limited by
  the policy `Internal viewers read inquiries` to
  `inquiry_viewer`, `inquiry_auditor`, `inquiry_admin`.
- The WPF V3.0 inquiry record carries `WebSubmissionId`, `WebSubmissionReference`,
  `ProformaInvoiceId`, `ProformaNumber` and `RowVersion`, and the local inbox
  ledger keys on `SubmissionId` with `LocalInquiryId`, so one submission becomes
  one local inquiry. **Verified fact** at 3.0 commit `812cf31` (read only).
- The ADV proforma id is `[Required, MaxLength(50)]`, user facing and unique in
  local SQLite, with no format validation, which is why the desktop pattern in
  the migration is bounded at 50 characters and accepts letters and hyphens.

The repository matched both Phase B documents on every point that C1 depends on.
No discrepancy was material enough to stop.

---

## 2. C1 scope implemented

| In scope | Delivered |
| --- | --- |
| Canonical desktop PI identity that can be minted offline | `inquiry_proformas.client_public_id uuid` |
| PI to published inquiry linkage for desktop origin | the `inquiry_id IS NULL` prohibition removed from the origin check |
| Submission to published inquiry linkage | `inquiries.web_submission_id uuid` with a restricted foreign key |
| One customer-facing number, stored verbatim, no `PI-DSK` | origin-aware `proforma_number` rule |
| Duplicate protection | unique partial indexes on both new identifiers |
| Documentation of every change | this report |

Explicitly not done, per the scope boundary: no publishing function, no number
allocation, no status or lifecycle change, no pricing or rounding change, no
issuance or document record, no website UI or API change, no policy, grant,
revoke or role change, no backfill, no production migration, no deployment, no
WPF change, no C2 to C5 work.

**Files added, and nothing else:**

- `website/supabase-inquiry-proforma-phaseC1.sql`
- `website/scripts/inquiry-proforma-identity-c1.test.mjs`
- `website/docs/PHASE_C1_IMPLEMENTATION_REPORT.md` (this file)

No existing file was modified. The two Phase B documents are untouched.

---

## 3. Final identity contract

Nine identifiers exist, and they are not interchangeable.

| # | Identity | Type and shape | Owner | Meaning |
| --- | --- | --- | --- | --- |
| 1 | `inquiry_customer_submissions.id` | uuid | Website | The original customer submission |
| 2 | `inquiry_customer_submissions.submission_reference` | `WEB-YYYY-NNNNNN` | Website | Human reference for that submission |
| 3 | `inquiries.id` | uuid | Server | The published desktop inquiry mirror |
| 4 | `inquiries.inquiry_number` | `INQ-YYYY-NNNN` | Desktop | The desktop number of that inquiry |
| 5 | `inquiries.web_submission_id` | uuid, nullable | Publisher | Proof that 3 came from 1 |
| 6 | `inquiry_proformas.id` | uuid | Server | The canonical proforma row |
| 7 | `inquiry_proformas.client_public_id` | uuid, nullable, unique | Desktop | The canonical identity of a desktop-authored PI, minted offline |
| 8 | `inquiry_proformas.proforma_number` | text, one per origin | Authoring side | The single customer-facing number |
| 9 | `inquiry_proformas.source_proforma_number` | text | Publisher | Origin proof for the existing source-number index |

Relationships:

- Submission to published inquiry: at most one **live** published inquiry per
  submission. A tombstoned inquiry (the publisher's record of a source row that
  disappeared) does not block a later re-ingest.
- Published inquiry to proforma: `inquiry_id`. A desktop-origin row may now carry
  it; a website-origin row must still carry it.
- Proforma to desktop document: `client_public_id`. It is the anchor a later
  publication uses for idempotency, and it exists before any network call.
- Number: `proforma_number` is the display and uniqueness key. For desktop origin
  it holds the desktop number verbatim, so there is exactly one customer-facing
  number and no `PI-DSK`. `source_proforma_number` repeats that string because
  the Phase 15 unique index on `(source_system, source_proforma_number)` needs it
  and because it records origin separately from display.

**Matching rules, unchanged and enforced by design.** No relationship may be
created from a customer name, a project name, a phone number or a matching PI
number. C1 adds the columns; it does not infer or backfill a single link.

---

## 4. Schema changes and justification

All changes are in `public`, inside one transaction, and idempotent.

### 4.1 `public.inquiries.web_submission_id`

| Property | Value |
| --- | --- |
| Purpose | Preserve which website submission produced the published inquiry, which nothing in the database could express before |
| Type | `uuid` |
| Nullable | Yes. Historical rows, desktop-only inquiries and unproven relationships stay null |
| Default | None |
| Foreign key | `inquiries_web_submission_id_fkey` to `public.inquiry_customer_submissions(id)`, `ON DELETE RESTRICT` |
| Uniqueness | `inquiries_web_submission_live`, unique on `(web_submission_id)` where `web_submission_id IS NOT NULL AND availability <> 'tombstoned'` |
| Index | The unique partial index is the lookup index; queries filter on the same predicate |
| Constraint interaction | None. `inquiries` has no other rule touching this column. The existing unique keys remain `(source_id, source_inquiry_id)` and `(source_id, inquiry_number)` |
| Compatibility | Old application versions never select this column and are unaffected. New versions see null until a publisher writes it |
| Rollback | Drop the constraint, the index and the column. Safe while no row has been linked; destructive afterwards |

Why `ON DELETE RESTRICT`: the schema already uses `RESTRICT` for every other
reference into the intake tables, submissions are closed rather than deleted, and
losing the link silently would be worse than refusing the delete.

Why nullable and why no backfill: a link that cannot be proven must not be
invented. The desktop ledger knows the pairing; SQL does not. Historical
reconciliation is a separate, explicitly approved exercise.

### 4.2 `public.inquiry_proformas.client_public_id`

| Property | Value |
| --- | --- |
| Purpose | The canonical identity of a desktop-authored PI, created offline, stable across edits and revisions |
| Type | `uuid` |
| Nullable | Yes. Website-origin drafts identify themselves by `id`; a desktop row gets one at publication (C4 enforces its presence) |
| Default | None |
| Foreign key | None. It is an identity, not a reference |
| Uniqueness | `inquiry_proformas_client_public_id`, unique where not null, across both origins |
| Index | The unique partial index is sufficient |
| Constraint interaction | `inquiry_proformas_client_identity` requires `client_public_id IS NULL OR source_system = 'desktop'`, so a website draft cannot claim a desktop identity |
| Compatibility | Additive and ignored by the current website code |
| Rollback | Drop the check, the index and the column |

### 4.3 `inquiry_proformas_source_identity`, replaced

Old, from Phase 15:

```sql
(source_system = 'website' AND inquiry_id IS NOT NULL AND source_proforma_number = '')
OR (source_system = 'desktop' AND inquiry_id IS NULL AND source_proforma_number <> '')
```

New:

```sql
(source_system = 'website' AND inquiry_id IS NOT NULL AND source_proforma_number = '')
OR (source_system = 'desktop' AND source_proforma_number <> '')
```

The website branch is byte for byte the same. The desktop branch loses only the
prohibition, so a published desktop invoice can point at its published inquiry.
Nothing else about desktop rows changes here.

### 4.4 `inquiry_proformas_proforma_number_origin`, new

| Property | Value |
| --- | --- |
| Purpose | Store the desktop number verbatim while keeping the server format for website origin |
| Website branch | `proforma_number ~ '^PI-WEB-[0-9]{4}-[0-9]{6}$'` |
| Desktop branch | `proforma_number ~ '^[A-Za-z0-9][A-Za-z0-9._-]{2,49}$'` |
| Constraint interaction | `proforma_number` remains `NOT NULL` and globally `UNIQUE`, so a desktop number that duplicates any existing number is still rejected. The source-number index still keys on `(source_system, source_proforma_number)` |
| Compatibility | Existing website rows all satisfy the new rule unchanged. No row is rewritten |
| Rollback | Restore the Phase 15 pattern (see section 10); only safe before a desktop row with a verbatim number exists |

The Phase 15 format check was declared inline, so its name is whatever PostgreSQL
generated. The migration finds it by definition (`pg_get_constraintdef` matches a
`proforma_number ~` comparison), drops it by discovered name, and **raises** if any
such check survives, so a wrong assumption fails loudly instead of leaving the old
rule in force. A fresh constraint name is then added, so a second run is a no-op.

---

## 5. Existing constraints preserved

| Object | State |
| --- | --- |
| `inquiry_proformas.proforma_number NOT NULL UNIQUE` | Unchanged |
| `inquiry_proformas_source_number` unique partial index | Unchanged |
| `inquiry_proformas_open_draft` unique partial index | Unchanged |
| `status` check (`Draft`, `Issued`, `Void`) | Unchanged |
| `currency`, `revision`, `line_count`, `total_sqm`, `total_value` checks | Unchanged |
| `created_by` foreign key to `auth.users` | Unchanged |
| `inquiry_proforma_lines` and `inquiry_proforma_saves` | Not referenced by the migration at all |
| `inquiries` unique keys, `inquiry_number` pattern, `availability` check | Unchanged |
| `save_inquiry_proforma_v1` | Not referenced, not replaced |
| Publication functions (`begin_inquiry_sync_run`, `publish_inquiry_snapshot`, `publish_inquiry_document`, `complete_inquiry_sync_run`) | Not referenced, not replaced |

The migration does not mention `inquiry_proforma_lines`, `inquiry_proforma_saves`,
`approved_rate`, `total_value` or `status`, which the test asserts.

---

## 6. Security implications

- **No policy, grant, revoke or role statement is added or changed.**
  `auth.uid()`, `has_inquiry_role`, the `inquiry_bridge` source scope and every
  existing RLS policy behave exactly as before. The test asserts that the
  executable SQL contains no `CREATE POLICY`, `GRANT`, `REVOKE`, `ROW LEVEL
  SECURITY` or role statement.
- **No new column is readable by anon.** New columns inherit the table grants:
  `inquiry_proformas` is `GRANT SELECT ... TO authenticated` with reads limited by
  `Operators read proformas` (`inquiry_admin`, `inquiry_auditor`); `inquiries` is
  limited by `Internal viewers read inquiries` (`inquiry_viewer`,
  `inquiry_auditor`, `inquiry_admin`). Anon holds nothing.
- **No capability leak through the new identifiers.** `web_submission_id` and
  `client_public_id` are opaque UUIDs. Reading one does not grant access to the
  referenced row: the submission's own RLS policy still decides that, so a viewer
  who cannot read submissions learns nothing from the reference.
- **No privileged credential anywhere.** C1 adds no function and no key. The
  bridge continues to authenticate with the anon key plus an operator session, and
  the database continues to authorise with `inquiry_bridge` scoped through
  `inquiry_sources.bridge_user_id`.
- **No document exposure.** The intake bucket and its policies are untouched.
- **Deletion is refused, not cascaded.** `ON DELETE RESTRICT` means a submission
  cannot be deleted while a published inquiry points at it.
- **No weakening for employees either.** Estimators read submissions through their
  own policy and cannot read `inquiries`, so the new column does not widen their
  view; viewers who read `inquiries` cannot resolve the submission.

---

## 7. Backward-compatibility analysis

| Concern | Finding |
| --- | --- |
| Existing rows | The migration changes no row. Every new column is nullable, so historical rows stay valid and readable |
| Existing website code | `proforma-api.ts` selects `inquiry_proformas` with `select("*")` after filtering, so new columns appear in its payload but no code reads them. `recordRow` ignores unknown fields. No API or UI change is needed |
| Existing save function | `save_inquiry_proforma_v1` inserts named columns only, so a nullable `client_public_id` does not affect it. Its behaviour and error codes are unchanged |
| Existing publication | `publish_inquiry_snapshot` does not write `web_submission_id`, so published inquiries keep null until C4 extends it |
| Existing number rules | Every website number still satisfies the new rule. A desktop row that previously could not exist at all is now storable, which is the point |
| Old application versions | An older website build that inserts a proforma still passes both new checks: website rows carry an inquiry and no client identity |
| Local desktop files | Untouched. No WPF source, schema or data was modified |
| Rollback before use | Fully reversible, since no row uses a new column |
| Rollback after use | Dropping `client_public_id` or `web_submission_id` would discard linkage, and restoring the Phase 15 number check would fail if a desktop row already stores a verbatim number. Rollback is therefore a C1/C2 window operation, not a post-publication one |

---

## 8. Tests actually executed and results

Run from `website/`.

| Command | Result |
| --- | --- |
| `node --test scripts/inquiry-proforma-identity-c1.test.mjs` | 12 tests, 12 pass, 0 fail |
| `node --test scripts/*.test.mjs` | 627 tests, 627 pass, 0 fail (baseline 615, plus the 12 new tests) |
| `npx vitest run` | 27 files passed, 282 tests passed, 0 failed (unchanged from the pre-C1 baseline) |
| `npm run lint` (`eslint src scripts`) | clean, no warnings or errors |
| `npx tsc --noEmit` | clean, no type errors |
| Migration and probe run against a disposable PostgreSQL server | section 16 |

The five commands above the last row were executed on 2026-10-24, after the last
edit to the test file and to this report. They read source text, or run the website,
and none of them executes SQL. The last row is the separate proof: the migration run
itself, recorded in section 16.

One intermediate run failed and is recorded for honesty. The data-statement guard
first used `/\b(DELETE|INSERT|UPDATE|TRUNCATE)\b/i`, which matched the text of
`ON DELETE RESTRICT` in the two foreign keys and produced one false failure (11
pass, 1 fail). The assertion was rewritten to match statement shapes (`DELETE
FROM`, `INSERT INTO`, `UPDATE ... SET`, `TRUNCATE`). The migration was not
changed: `ON DELETE RESTRICT` is a referential action, and relaxing it would have
been a real regression.

### 8.1 What the new contract test asserts

`scripts/inquiry-proforma-identity-c1.test.mjs` reads the migration, the Phase 15
schema, the intake schema, the published-inquiry schema and this report, strips SQL
comments so only executable statements are judged, and checks:

1. One transaction, starting at `BEGIN;` and ending at `COMMIT;`.
2. No data statement (`DELETE FROM`, `INSERT INTO`, `UPDATE ... SET`,
   `TRUNCATE`), and no `DROP` of a table, column, schema, index, policy,
   function, trigger, type, sequence or view. Only `DROP CONSTRAINT` appears,
   for constraints this file replaces.
3. No policy, grant, revoke, RLS or role statement.
4. `web_submission_id` is an optional `uuid` with a `RESTRICT` foreign key to
   `inquiry_customer_submissions(id)` and a unique partial index limited to live
   inquiries.
5. Both sides of that key are `uuid` in their own schema files, so the foreign key
   is type correct.
6. `client_public_id` is an optional unique `uuid` bound to desktop origin.
7. The website branch of the origin check is unchanged, the desktop branch no
   longer forbids `inquiry_id`, and the Phase 15 file really did forbid it.
8. Exactly two number patterns exist. Fixtures prove the website format still
   rejects `PI-2026-001` and malformed web numbers, and that `PI-2026-001`,
   `INV-42`, `ABC-001`, `PI-2026-AB12CD` and `sales-2026-7` are storable verbatim
   while empty, spaced, slashed, too short and 51 character values are rejected.
9. The inline check is found by definition, dropped by discovered name, and a
   surviving check raises an exception.
10. No `PI-DSK` appears, and no later-phase column or table appears
    (`source_revision`, `source_content_hash`, `source_status`, `pricing_version`,
    `summary`, `issued_at`, `document_sha256`, `superseded_by`, revision and event
    tables).
11. Only two columns are added, and neither `inquiry_proforma_lines` nor
    `inquiry_proforma_saves` is mentioned.
12. This report states the rollback and the production position.

### 8.2 What these tests do not prove

They are static checks over SQL text and patterns, and one suite of ordinary
runtime tests over the website. Neither class executes the migration. That gap is
closed separately, in section 16, by running the same file against a PostgreSQL
server.

Two limits remain even after that run. The number-pattern fixtures in this test
evaluate the extracted patterns with JavaScript regular expressions, which is an
approximation of PostgreSQL ARE, and the runtime suite exercises the website only,
not the database. Section 16 governs what the database run does and does not
prove.

---

## 9. Production migration status

**Not applied to production.** No data definition and no data change was issued
against the Supabase project, and nothing was deployed.

Read-only inspection was performed as part of this validation. Both statements are
`SELECT`s; neither creates, alters or drops anything, and neither writes a row:

| Query | Result |
| --- | --- |
| counts of `inquiry_proformas`, `inquiries`, `inquiry_customer_submissions` with `current_setting('server_version')` | 0, 0, 8, PostgreSQL 17.11 |
| `conname`, `contype` and `pg_get_constraintdef` for the three tables | 45 constraints, identical in name and definition to the reconstruction in section 16 |

That second result is the reason the section 16 run means something: the disposable
schema is not an approximation written by hand, it is a rebuild that reproduces the
production constraint set exactly for the tables C1 touches.

Validation itself ran in a disposable PostgreSQL instance (section 16), not in the
project. Before the migration is applied for real:

1. Repeat the section 16 rebuild on a Supabase database branch, so the first real
   run happens on the platform rather than on stock PostgreSQL.
2. Confirm `\d public.inquiry_proformas` shows `inquiry_proformas_source_identity`
   and `inquiry_proformas_proforma_number_origin`, that
   `inquiry_proformas_proforma_number_check` is gone, and that both new indexes
   exist.
3. Run the probes in section 11 on that copy.
4. Apply it to the project as its own migration record
   (`inquiry_proforma_phaseC1_identity`).

---

## 10. Rollback

Only valid before any row uses the new columns. Restoring the Phase 15 number
check fails if a desktop row already stores a verbatim number, which is the
intended signal that the rollback window has closed.

```sql
BEGIN;

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_proforma_number_origin;
ALTER TABLE public.inquiry_proformas
  ADD CONSTRAINT inquiry_proformas_proforma_number_check
  CHECK (proforma_number ~ '^PI-(WEB|DSK)-[0-9]{4}-[0-9]{6}$');

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_source_identity;
ALTER TABLE public.inquiry_proformas
  ADD CONSTRAINT inquiry_proformas_source_identity CHECK (
    (source_system = 'website' AND inquiry_id IS NOT NULL AND source_proforma_number = '')
    OR (source_system = 'desktop' AND inquiry_id IS NULL AND source_proforma_number <> '')
  );

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_client_identity;
DROP INDEX IF EXISTS public.inquiry_proformas_client_public_id;
ALTER TABLE public.inquiry_proformas
  DROP COLUMN IF EXISTS client_public_id;

DROP INDEX IF EXISTS public.inquiries_web_submission_live;
ALTER TABLE public.inquiries
  DROP CONSTRAINT IF EXISTS inquiries_web_submission_id_fkey;
ALTER TABLE public.inquiries
  DROP COLUMN IF EXISTS web_submission_id;

COMMIT;
```

Column comments disappear with their columns. No comment needs a separate undo.

This block was extracted verbatim from this section and executed against the
disposable database of section 16, after the migration had been applied twice and
the probe suite had run. It exited 0, and the resulting schema was identical to the
pre-migration baseline snapshot, constraint for constraint. Re-applying the
migration afterwards restored the C1 schema and all 27 probes passed again. The
object at risk is only the number check: restoring
`inquiry_proformas_proforma_number_check` fails if any desktop row already stores a
verbatim number, which is the intended signal that the rollback window has closed.

---

## 11. Constraint probes, executed

Every probe below ran against the disposable database of section 16, after the
migration, inside a transaction that was rolled back. 27 of 27 passed.

| Probe | Expected | Observed |
| --- | --- | --- |
| P01 inquiry with an unknown `web_submission_id` | rejected, 23503 | rejected by `inquiries_web_submission_id_fkey` |
| P02 inquiry with a real submission | accepted | accepted |
| P03 second live inquiry for the same submission | rejected, 23505 | rejected by `inquiries_web_submission_live` |
| P04 delete a submission that a live inquiry references | rejected, 23503 | rejected by the foreign key |
| P05 desktop row with a `client_public_id` | accepted | accepted |
| P05b second row with the same `client_public_id` | rejected, 23505 | rejected by `inquiry_proformas_client_public_id` |
| P06 website row with a `client_public_id` | rejected, 23514 | rejected by `inquiry_proformas_client_identity` |
| P07 desktop row with a verbatim `PI-2026-001` | accepted | accepted, which is the point of C1 |
| P08 desktop row with `PI-DSK-2026-000101` | accepted | accepted, legacy shape preserved |
| P09 desktop row linked to its published inquiry | accepted | accepted, a new capability |
| P10 website row carrying a desktop number | rejected, 23514 | rejected by the origin check |
| P11 website row without `inquiry_id` | rejected, 23514 | rejected by `source_identity` |
| P12 desktop row without a source number | rejected, 23514 | rejected by `source_identity` |
| P13 desktop number with a space | rejected, 23514 | rejected by the origin check |
| P14 desktop number of two characters | rejected, 23514 | rejected by the origin check |
| P15 desktop number of 50 characters | accepted | accepted |
| P16 desktop number of 51 characters | rejected, 23514 | rejected by the origin check |
| P17 desktop number with a slash | rejected, 23514 | rejected by the origin check |
| P18 desktop custom id `sales-2026-7` | accepted | accepted |
| P19 desktop number shaped like `PI-WEB-2026-000999` | accepted by the table, documented gap | accepted, see section 17 |
| P20 duplicate `proforma_number` across origins | rejected, 23505 | rejected by the global unique constraint |
| P21 duplicate desktop source number | rejected, 23505 | rejected by `inquiry_proformas_source_number` |
| P22 desktop row without `client_public_id` | accepted | accepted |
| P23 malformed `client_public_id` | rejected, 22P02 | rejected |
| P24 unknown `source_system` | rejected | rejected, 23514; PostgreSQL reported the origin check first |
| P25 new inquiry for the submission of a tombstoned inquiry | accepted | accepted |
| P26 delete a submission whose only inquiry is tombstoned | rejected, 23503 | rejected, see section 17 |

The same database before C1, for contrast:

| Probe | Result before C1 |
| --- | --- |
| desktop row with a verbatim `PI-2026-001` | rejected, 23514 `inquiry_proformas_proforma_number_check` |
| desktop row linked to a published inquiry | rejected, 23514 `inquiry_proformas_source_identity` |
| desktop row in the Phase 15 shape | accepted |
| duplicate `proforma_number` | rejected, 23505 |
| write `inquiries.web_submission_id` | 42703, column does not exist |

Also executed in the same run, because a constraint is only half the story:

| Check | Result |
| --- | --- |
| Apply the migration twice | second run exit 0, identical schema, no error |
| Row fingerprint and `xmin` of the seeded rows, before and after the second run | unchanged: `7c9218f8526b283ca0661583277f5597`, `xmin 786` |
| Add a differently named `proforma_number` check, then re-apply | found by definition and dropped, origin check present, 27 probes still pass |
| Extract the rollback from section 10 and execute it, then diff against the pre-C1 snapshot | exit 0, no diff, schema identical to the baseline; re-applying C1 restores all 27 passing probes |
| `save_inquiry_proforma_v1`, create then revise a website PI | `PI-WEB-2026-000002`, revision 2, 1 line, 6.0000 m2, 900.00 AED, `client_public_id` null |
| `publish_inquiry_snapshot` through a bridge sync run | `INQ-2026-0003` created, `web_submission_id` null |
| Admin, customer and anon reading proformas | 3 rows, 0 rows, permission denied |

---

## 12. Known limitations

- The run used stock PostgreSQL, not the Supabase platform. `auth.users`,
  `auth.uid()`, `auth.jwt()` and the `storage` tables are local shims, and RLS was
  exercised by switching database roles rather than by sending a signed JWT
  through PostgREST. The policies themselves were not modified and the policy and
  grant lists are byte-identical before and after, but the platform path is not
  proven.
- Version parity is close, not exact: the disposable server was PostgreSQL 17.9,
  production reports 17.11. No feature C1 uses changed between those releases.
- The production tables are empty (0 proformas, 0 inquiries, 8 submissions), so the
  run proves the migration against representative structure and representative
  synthetic rows, not against real historical data. The two seeded proformas and
  two seeded inquiries are fixtures, not customer records.
- No concurrency was tested: two publishers writing the same submission at once,
  or two proforma saves racing on one inquiry. The uniqueness rules are the
  defence, and they were tested one statement at a time.
- No performance testing. `inquiry_proformas.inquiry_id` still has no index for
  desktop-origin lookups; C4 adds one if its query needs it.
- No backfill. Every existing inquiry keeps `web_submission_id = null` and every
  existing proforma keeps `client_public_id = null`. Historical reconciliation
  needs its own approval and its own per-row proof.
- Duplication by design: for desktop origin, `proforma_number` and
  `source_proforma_number` hold the same string. C4 must write both, and a test
  there should assert they match.
- The database does not require `client_public_id` on a desktop row, and it does
  not stop a desktop number shaped like a website number. Both belong to the C4
  publication function, because enforcing them in the table could refuse a legacy
  custom identifier. Section 17 states the consequence.
- Nothing reads the new columns yet. The website payload carries them, inert.

---

## 13. Remaining decisions

| Decision | State after C1 |
| --- | --- |
| A. Numbering display | Resolved for implementation by this phase's instruction: one customer-facing number, the desktop number verbatim, no `PI-DSK`. **Requires business approval** to confirm that a portal user sees exactly the number printed on the desktop document |
| B. Sent versus Issued, canonical lifecycle | Open. Not needed by C1. C4 needs it |
| C. Meaning of Approved | Established by source as customer acceptance (signed PI or LPO). No schema change in C1. C4 needs the canonical label |
| D. Portal content, VAT and charges | Open. The `summary` shape is deliberately not added in C1 |
| E. Operator attribution | Open. No event table in C1 |
| F. 4 m2 threshold and 20 percent | Open. No pricing change in C1 |
| G. Historical submission to inquiry links | Open. Deliberately not backfilled |

---

## 14. Exact prerequisites for C2

C2 is the single pricing core plus the parity fixture. It needs:

1. Approval that the canonical number rule is final as implemented, or a corrected
   instruction before any desktop publication writes a number.
2. A decision on decision F: whether the 4 m2 threshold and the default percentage
   are configuration with an effective date, or code constants. C2 cannot encode
   the rule twice.
3. Agreement that the parity fixture is the shared, versioned artefact both
   implementations must satisfy, and where it lives (`website/tests/fixtures/` or a
   neutral file both repositories read).
4. Agreement that C2 may change no stored totals and no UI, and that existing PIs
   keep their own arithmetic through a version stamp added later, not in C2.
5. This migration reviewed and applied to a disposable environment, with the probes
   in section 11 recorded. That condition is now met (section 16); repeating it on a
   Supabase branch remains the last step before the project itself is changed.

C2 does not need decisions B, D or E.

---

## 15. C1 acceptance criteria, self-assessed

| Criterion | Result |
| --- | --- |
| Existing inquiry identity constraints remain valid | Yes. Untouched, asserted by test, and confirmed byte-identical in the production constraint list and in the post-migration diff |
| The new submission link can represent a verified relationship | Yes. Nullable uuid, restricted foreign key, live-only uniqueness. P01 to P04 |
| Historical inquiries without a known submission remain valid | Yes. The column is nullable, nothing is backfilled, and every pre-existing row kept its values |
| Duplicate or invalid relationships are rejected | Yes, and executed, not inferred: P01, P03, P05b, P06, P10 to P17, P20, P21, P23, P24 |
| Existing PI identity and number fields remain unchanged | Columns, types and the global uniqueness are unchanged. The format rule becomes origin aware, which is the authorised change |
| Existing PI save behaviour is not altered | Yes. `save_inquiry_proforma_v1` created and revised a website PI with unchanged semantics (section 11) |
| Existing RLS and grants are not weakened | Yes. No policy, grant or role statement exists in the migration, the before and after policy and grant lists are identical, and anon is refused while admin and customer see 3 and 0 rows |
| The migration applies to a representative schema | Yes, on a schema rebuilt from 18 repository migrations on PostgreSQL 17.9, with the production constraint set reproduced exactly. It has not been applied to Supabase itself |
| No existing records are rewritten | Yes. No data statement exists, and the seeded rows kept both their fingerprint and their `xmin` across two applications |
| Rollback documented | Yes, section 10, and executed: the documented block restored the exact pre-migration schema |

**Verdict.** C1 is implemented, statically tested, and now executed against a
disposable database that reproduces the production constraint set for the three
tables it changes. The migration applies cleanly, applies twice, changes nothing
but the six intended objects, rejects every invalid combination it should reject,
and leaves the existing write paths and the policy set alone. No production data
or schema was modified, no deployment occurred, and no WPF source was changed.

---

## 16. Disposable database: environment, reconstruction and commands

### 16.1 Environment

| Item | Value |
| --- | --- |
| Server | PostgreSQL 17.9, `initdb` from the `@embedded-postgres/linux-x64` npm package |
| Production for comparison | PostgreSQL 17.11, project `afpbdyamedpanwijxghk` |
| Cluster | `/tmp/pg17/data`, socket only on `/tmp/pg17/sock`, port 55433, `listen_addresses=''`, trust auth on the socket |
| Reachability | No TCP listener, so nothing outside the sandbox could connect. The cluster and the databases were discarded after the run |
| Extensions | `pgcrypto` and `pg_trgm` ship with this build, so the `CREATE EXTENSION pgcrypto` in `supabase-inquiries.sql` ran unmodified instead of being stubbed |
| Supabase parts | `auth` and `storage` schemas created locally as shims (roles `anon`, `authenticated`, `service_role`; `auth.users`; `auth.uid()`, `auth.jwt()`, `auth.role()` reading `request.jwt.claims`; `storage.buckets`, `storage.objects`, `storage.foldername`) |

An earlier attempt used PostgreSQL 16.2 from a PyPI package, where `pgcrypto` is
not bundled and had to be stubbed with an empty extension. Every result below was
reproduced on 17.9, and the numbers quoted are from the 17.9 run.

### 16.2 Reconstruction

Two databases were built from the repository's own migrations, applied in this
order, each with `psql -v ON_ERROR_STOP=1`:

1. `supabase-inquiries.sql`
2. `supabase-inquiry-intake.sql`
3. `supabase-inquiry-intake-phase8a-limits.sql`
4. `supabase-inquiry-intake-phase8a-staged-uploads.sql`
5. `supabase-inquiry-intake-phase8a-customer-projects.sql`
6. `supabase-inquiry-intake-phase8b-review.sql`
7. `supabase-inquiry-intake-phase8c-inbox.sql`
8. `supabase-inquiry-intake-phase8c-event-repair.sql`
9. `supabase-inquiry-estimator-phase9a.sql`
10. `supabase-inquiry-estimator-phase9a-rls-repair.sql`
11. `supabase-inquiry-estimator-phase9b-access.sql`
12. `supabase-inquiry-estimator-phase9d-communication.sql`
13. `supabase-inquiry-estimator-phase9e-handoff.sql`
14. `supabase-inquiry-inbox-bridge-phase12.sql`
15. `supabase-inquiry-role-matrix-phase13.sql`
16. `supabase-inquiry-desktop-access-phase14.sql`
17. `supabase-inquiry-proforma-phase15.sql`
18. `supabase-inquiry-proforma-phase15-write.sql`

`c1baseline` stopped there. `c1validation` then received
`supabase-inquiry-proforma-phaseC1.sql`, unmodified, sha256
`52d42d622c7163f09dc57158085b7be7bb9602c82fd37f821beaef4ed9343fab`.

Two deliberate exclusions, both recorded rather than hidden:

- `supabase-inquiry-intake-phase8a-fields.sql` was not applied. Its columns
  (`attention_name`, `vat_trn_number`, `salesman_reference`,
  `handover_estimator_name`) already exist in the consolidated
  `supabase-inquiry-intake.sql`, and the file additionally drops a seven argument
  overload of `create_customer_inquiry_submission` that the consolidated file no
  longer creates, so it fails with 42883 by construction. Applying it would add
  nothing and would report an error.
- `supabase-dashboard.sql`, `supabase-deliveries.sql`, `supabase-global-chat*.sql`,
  `supabase-job-orders.sql`, `supabase-production-room*.sql`,
  `supabase-user-profiles.sql` and `supabase-phase11-grant-tightening.sql` were not
  applied. They are outside the inquiry and PI identity closure, and the last of
  them contains no reference to any inquiry table.

Fidelity was then checked against production, read only: the 45 constraints on
`inquiries`, `inquiry_customer_submissions` and `inquiry_proformas` in the
reconstruction match those in the project by name, type and definition.

Seed data, inserted before the migration ran: three `auth.users` rows, an
`inquiry_admin` and an `inquiry_bridge` operator, one `inquiry_sources` row, two
website submissions (`WEB-2026-000001` submitted, `WEB-2026-000002` accepted), two
published inquiries (`INQ-2026-0001`, `INQ-2026-0002`), one website proforma
(`PI-WEB-2026-000001`, linked to `INQ-2026-0001`) and one desktop proforma in the
Phase 15 shape (`PI-DSK-2026-000001`, no inquiry link, source number
`PI-2026-000001`).

One seed detail worth recording: inserting a proforma number as a literal does not
advance `inquiry_proforma_number_seq`, so the first RPC call collided with
`PI-WEB-2026-000001` until the sequence was set past it. That is a property of
fixture loading, not of the migration, and it is the kind of collision a manual
backfill would also have to respect.

### 16.3 Commands and results

| Step | Command | Result |
| --- | --- | --- |
| 1 | `initdb -D /tmp/pg17/data -U postgres --auth=trust --no-sync -E UTF8` | cluster created |
| 2 | `pg_ctl -D /tmp/pg17/data -o "-k /tmp/pg17/sock -p 55433 -c listen_addresses=''" start` | started, `select version()` returns PostgreSQL 17.9 |
| 3 | `CREATE DATABASE c1baseline; CREATE DATABASE c1validation;` | two databases |
| 4 | `psql -d <db> -f 00_supabase_shim.sql` | roles, `auth` and `storage` shims created |
| 5 | the 18 files above, `psql -d <db> -v ON_ERROR_STOP=1 -f <file>` | all 36 runs (18 per database) exit 0 |
| 6 | `psql -d <db> -f 10_seed.sql` then `setval('public.inquiry_proforma_number_seq', 1, true)` | fixture rows created |
| 7 | `psql -d c1baseline -At -f introspect.sql` | 114 line baseline snapshot of constraints, indexes, policies, grants, function bodies, counts |
| 8 | `psql -d c1validation -v ON_ERROR_STOP=1 -f supabase-inquiry-proforma-phaseC1.sql` | exit 0, ended in `COMMIT`, no error |
| 9 | the same command again | exit 0, schema identical to step 8, row fingerprint and `xmin` unchanged |
| 10 | `psql -d c1validation -At -f introspect.sql` then `diff` against the baseline | 8 diff lines: exactly the six intended object changes |
| 11 | `psql -d c1validation -f 30_probes.sql` | 27 probes, 27 pass, 0 fail |
| 12 | `psql -d c1validation -f 40_compat.sql` | website PI save, bridge publish, admin and customer reads, 0 errors |
| 13 | `BEGIN; SET LOCAL ROLE anon; SELECT count(*) FROM public.inquiry_proformas; ROLLBACK;` | `permission denied for table inquiry_proformas` |
| 14 | add `legacy_number_check`, then re-apply the migration | legacy check discovered and dropped, origin check present, probes still 27 of 27 |
| 15 | extract the rollback block from section 10, `psql -f` it, then `diff` the new snapshot against the baseline | exit 0, empty diff; re-applying the migration afterwards restored 27 of 27 probes |

The diff in step 10, in full:

- added `inquiries_web_submission_id_fkey` (23503 on an unknown submission)
- added `inquiry_proformas_client_identity`
- removed `inquiry_proformas_proforma_number_check` (the Phase 15 inline check)
- added `inquiry_proformas_proforma_number_origin`
- replaced `inquiry_proformas_source_identity` (desktop branch only)
- added the indexes `inquiries_web_submission_live` and
  `inquiry_proformas_client_public_id`

No policy, grant, role, function body, table, column outside the two new ones, or
row changed.

### 16.4 What this run does not prove

It does not prove the Supabase platform path: no PostgREST, no GoTrue, no signed
JWT, no storage service. It does not prove behaviour at 17.11 exactly, in
concurrency, at production data volume, or with real historical rows. Section 12
lists these as limitations rather than footnotes.

---

## 17. Compatibility risks and unresolved issues found by the run

1. **A desktop row can hold a website shaped number.** P19 inserted
   `PI-WEB-2026-000999` as a desktop number and the table accepted it. Because
   `proforma_number` is globally unique, the damage is bounded: a later website
   allocation of that exact number fails with 23505 instead of producing two
   documents with one number. It is still an availability and trust defect, and the
   publisher (C4) must reject `^PI-WEB-` for desktop origin.
2. **A published submission can never be deleted.** `ON DELETE RESTRICT` refuses
   P04 and P26, including when the only linked inquiry is tombstoned. That is the
   correct default for an audit trail, but it means retention or erasure requests
   cannot be satisfied by deleting a submission row once it has been published.
   The decision belongs to the owner, not to C1.
3. **The number-check guard is definition based and will drop any match.** The
   guard experiment in section 11 shows that a differently named check on
   `proforma_number` is discovered and dropped. That is what makes the migration safe to re-run
   against the Phase 15 inline check, but it also means re-running it after C2 or
   C4 adds another `proforma_number ~ ...` check would silently remove that check.
   The migration is a one-time migration and should be treated as one.
4. **`client_public_id` is unique across both origins**, while only desktop rows may
   set it. Harmless today; worth remembering if the website ever mints one.
5. **Numbers that differ only in case or punctuation are different documents.**
   `INV-42` and `inv-42` both fit the pattern and both fit the unique index. If the
   desktop treats them as one invoice, C4 needs a normalisation rule.
6. **No index on `inquiry_proformas.inquiry_id`.** The new desktop link makes that
   column more interesting than before. Add an index when a query needs it, not
   before.
7. **`inquiries.web_submission_id` stays null until the publisher writes it.** C4
   must set it in the same transaction that publishes the inquiry, or the column
   will be decoration.

---

## 18. Validation verdict

| Question | Answer |
| --- | --- |
| Was the migration executed anywhere? | Yes. On a disposable PostgreSQL 17.9 instance rebuilt from 18 repository migrations, then discarded |
| Was it executed against production? | No. Only two read-only `SELECT`s touched the project |
| Does it apply cleanly? | Yes, first attempt, exit 0, `COMMIT` |
| Is it idempotent? | Yes, second application produced an identical schema and touched no row |
| Does it change anything beyond its stated scope? | No. Six object changes, nothing else |
| Do the new rules reject what they should? | Yes, 27 of 27 probes |
| Do the old rules and write paths still work? | Yes: website PI save, bridge publish, admin read, customer read, anon refusal |
| Is it ready to apply to the project? | The schema is ready. Repeating the rebuild on a Supabase branch is the remaining step, and applying it requires explicit authorisation |

C1 is validated as far as this environment allows. Nothing was committed, pushed,
deployed or applied to the project, and no application or WPF source was changed.

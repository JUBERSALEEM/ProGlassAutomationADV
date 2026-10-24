# Phase C5 implementation report

Persisted specification pricing settings, the approved PI status vocabulary, and
number allocation that follows the desktop allocator. No production database was
touched, nothing was deployed, and the WPF repositories were not modified.

Validated on 2026-10-24 against a disposable PostgreSQL 17.9 instance rebuilt from
this repository's migrations (the same rebuild described in
`PHASE_C1_IMPLEMENTATION_REPORT.md` section 16), with the C5 file applied on top
twice.

Labels: **Verified fact**, **Implemented**, **Not applied**, **Limitation**.

---

## 1. What this phase changed

| Area | Change | Status |
| --- | --- | --- |
| Specification rates | `public.inquiry_proforma_groups` stores BASE rate and SURCHARGE % per specification per revision | **Implemented** |
| Save path | `public.save_inquiry_proforma_v2` writes lines and settings in one revision and one transaction; v1 is left in place | **Implemented** |
| Status vocabulary | `Draft`, `Sent`, `Approved`, `Issued`, `Cancelled`; legacy `Void` rows converted to `Cancelled` | **Implemented** |
| Numbering | `public.inquiry_next_proforma_number` allocates the first free number above the highest used under the prefix, case-insensitively, like the desktop allocator | **Implemented** |
| Uniqueness | `inquiry_proformas_number_ci`, a unique index on `upper(proforma_number)` | **Implemented** |
| Sent / Approved / Issued operations | No operation exists to send, accept or issue, so no transition is claimed | **Not implemented, deliberately** |
| Production | Nothing applied, nothing deployed | **Not applied** |

Files:

- `website/supabase-inquiry-proforma-phaseC5-rates-and-status.sql` (new, 426 lines,
  sha256 `526e8f6c062556701292c50025333efdb7da74a00d31d06a8f1905a4aa538425`)
- `website/src/lib/proforma-status.ts` (new)
- `website/src/lib/proforma-rates.ts` (settings load/send helpers)
- `website/src/lib/proforma-api.ts` (v2 save, groups read, status normalisation)
- `website/src/types/proforma.ts`, `website/src/app/api/inquiries/[id]/proforma/route.ts`
- `website/src/components/inquiries/ProformaWorkspace.tsx`,
  `.../proforma/SpecificationRateBar.tsx`, `.../proforma/ProformaDocument.tsx`,
  `website/src/lib/proforma-document.ts`
- `website/tests/proforma-status.test.tsx` (new, 14 tests), `website/tests/proforma-rates.test.tsx`
  (3 new tests), `website/tests/proforma-document.test.tsx`

---

## 2. WPF numbering, verified in the desktop source

**Verified fact**, read from
`Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs`:

- `_idPattern` (line 61) is `^(?<prefix>.*?)(?<num>\d+)$`. The prefix is everything
  before the trailing run of digits; the padding width is the length of that run.
- `SuggestNextIdAsync` (line 120) takes the id it is given, not the table maximum.
  `InsertAsync` seeds it with `$"PI-{DateTime.Now.Year}-000"` (line 207), so the
  desktop family is `PI-2026-001`: a year prefix and three digits.
- Empty input becomes `PI-000`. Input with no trailing digit run (`ABC`) becomes
  prefix `ABC-`, start 0, width 3, giving `ABC-001`.
- `int.TryParse` failure (a digit run too long for `int`) degrades that run to 0
  instead of throwing (AUDIT V-1), so numbering restarts from that prefix and the
  duplicate search keeps it correct.
- It loads every id starting with the prefix in one query (BUG-08), compares them
  case-insensitively in a `HashSet`, and returns the first candidate not present,
  walking `current + 1` upward to `current + 100000`. Gaps below the current number
  are not reused.
- After 100000 attempts it returns `prefix + Guid`, shortened to fit 50 characters
  (BUG-09).
- `InsertAsync` retries the insert up to five times when a concurrent writer took
  the candidate first (lines 211 to 258), which is how the desktop tolerates the
  race between "find a free id" and "insert it".

Documented examples in the source, and true of this implementation: `PI-2026-001`
to `PI-2026-002` (or `003` if `002` exists), `INVOICE-42` to `INVOICE-43`, `ABC` to
`ABC-001`.

**Verified fact:** there is no stored `Sr No` generator anywhere in the WPF tree.
`SrNo` appears only in reporting headers (`ReportingExportService.cs:577`,
`ReportingDataService.cs:57`, `ReportingModels.cs:136`). Row numbering is display
order, not identity.

---

## 3. Number allocation in the website

**Before C5** the website allocated `PI-WEB-YYYY-` + `nextval(inquiry_proforma_number_seq)`
padded to six digits. The sequence is monotonic and blind: it never looks at the
numbers that exist, so a number a desktop record already holds would collide with
the unique constraint and fail the whole save.

**After C5**, `save_inquiry_proforma_v2` calls
`public.inquiry_next_proforma_number('PI-WEB-' || year || '-', 6)`, which:

1. takes an advisory lock on the prefix, so two concurrent saves cannot be handed
   the same candidate;
2. reads the highest numeric suffix already used under that prefix, ignoring digit
   runs longer than nine digits rather than overflowing (the SQL counterpart of
   AUDIT V-1);
3. treats the old sequence's `last_value` as a floor, so a number already printed
   is never reissued;
4. walks upward and returns the first candidate that no row holds, comparing
   case-insensitively;
5. raises `55000` after 100000 attempts instead of inventing a GUID, because the
   website column check admits only the six digit website shape.

| Probe | Result |
| --- | --- |
| Highest existing website number is 000003 | passed |
| Next number skips a gap at 000002 (max + 1, desktop rule) | `PI-WEB-2026-000004` |
| A requested width of 3 produces three digits | `PI-WEB-2026-004` |
| A desktop row holding `PI-WEB-2026-000002` is skipped, not collided with | `PI-WEB-2026-000003` |
| A lower case `pi-web-2026-000002` is skipped | `PI-WEB-2026-000003` |
| A website insert differing only by case is rejected | 23505 on `inquiry_proformas_number_ci` |
| A number burnt by the old `nextval` allocator is never reissued | floor 9 gives `000010` |
| Empty prefix, width 0 | rejected `22023` |
| A thirteen digit run does not overflow | `PI-WEB-2026-000002` |
| A different prefix has its own sequence | `PI-DSK-2026-000002` |
| Anon cannot call the allocator | 42501 |
| Authenticated can call it | allowed |
| The same unspent candidate is returned until it is stored | stable |

14 of 14 passed.

### How the two origins coexist without duplicates

- Website numbers stay `PI-WEB-YYYY-NNNNNN`; desktop numbers are stored verbatim
  from the desktop (`PI-2026-001`, `INV-42`, `ABC-001`, the GUID fallback), which
  is what C1 established.
- `proforma_number` was already `UNIQUE`; C5 adds `UNIQUE (upper(proforma_number))`
  so `pi-web-2026-000001` and `PI-WEB-2026-000001` cannot both exist, matching the
  desktop's case-insensitive comparison.
- The allocator skips any number a desktop row already holds. That is the WPF
  rule, and it is what makes sharing one number space safe.
- The unique index is what turns a lost race into a failed insert rather than two
  documents sharing a number.

### Deliberate differences from the desktop

| Desktop | Website | Why |
| --- | --- | --- |
| Prefix comes from the id on screen | Prefix is the current year, fixed by the C1 column check | A year-stamped public reference was already the website contract |
| Width comes from the existing digit run | Fixed at six digits | The same column check |
| Reuses the number of a deleted row | Never reissues a printed number | A customer document number must not reappear elsewhere |
| GUID fallback after 100000 attempts | Raises `55000` | A GUID cannot satisfy the website number check |
| Up to five insert retries in C# | One advisory lock per prefix | The lock removes the race the retries work around |

**Limitation:** a desktop row whose number happens to look like
`PI-WEB-2026-000999` is accepted by the table (probe P19 in the C1 report). It
cannot duplicate a website number, but it can occupy one, which would turn that
website save into a retry. Publishing desktop numbers that avoid the `PI-WEB-`
prefix remains a desktop publishing contract item, not something the website can
enforce without rejecting legitimate desktop ids.

---

## 4. Status vocabulary and transitions

`CHECK (status IN ('Draft','Sent','Approved','Issued','Cancelled'))`.

| Status | Meaning | How a row reaches it today |
| --- | --- | --- |
| Draft | Being prepared | Default on creation, and the only status the save function writes |
| Sent | Sent to the customer | Not reachable: no send operation exists |
| Approved | Customer accepted through the business process | Not reachable: no acceptance record exists |
| Issued | Went through the verified issuance workflow | Not reachable: no issuance operation exists |
| Cancelled | No longer live | Written only by the migration for legacy `Void` rows; there is no cancel button yet |

`website/src/lib/proforma-status.ts` holds the vocabulary, a
`normaliseProformaStatus` that maps a stored `Void` to `Cancelled` and an
unrecognised value to `Draft`, an `isLegacyStatus` helper, and two transition
lists:

- `IMPLEMENTED_TRANSITIONS` is **empty on purpose**. Nothing may move a document
  into Sent, Approved or Issued until the operation that performs it exists.
- `PENDING_TRANSITIONS` records what is missing: `Draft to Sent` (send),
  `Sent to Approved` (acceptance), `Sent to Issued` (issuance), and
  `Draft or Sent to Cancelled` (cancel).

**Legacy handling:** the migration updates `Void` to `Cancelled` before it adds the
check, so no row is rejected or deleted, and the read path normalises any `Void`
still arriving from a database that has not been migrated. The proforma read no
longer filters `status <> 'Void'`, so a cancelled proforma opens and says it is
cancelled instead of silently showing an older draft in its place.

**Verified fact** on the disposable database: a row inserted as `Void` before the
migration became `Cancelled`, the table still held three rows, no `Void` row was
left, and `Void` is now rejected with 23514.

---

## 5. Persisted specification settings

`public.inquiry_proforma_groups`:

| Column | Type and rule |
| --- | --- |
| `proforma_id` | `uuid` FK to `inquiry_proformas` `ON DELETE CASCADE` |
| `revision` | `integer`, at least 1 |
| `group_no` | `integer`, 1 to 50 (the same 50 specification limit the grid enforces) |
| `base_rate` | `numeric(18,2)`, 0 to 1,000,000 |
| `surcharge_percent` | `numeric(9,2)`, 0 to 1000 |
| `created_at` | `timestamptz` |

Primary key `(proforma_id, revision, group_no)`.

- **Per revision.** A new revision never inherits a row it did not write. Revision
  1 kept `100 / 20` and `80 / 0` while revision 2 stored `105 / 20`.
- **Per proforma and per specification.** The key carries both, so two PIs never
  share a setting.
- **Duplicate, reorder and remove** are safe because the key is the specification
  number, not a position: the write replaces the set of rows for that revision, so
  a removed specification leaves no orphan and a reordered one keeps its figures.
- **Database authority.** The page loads the settings from the database on open and
  stores what is on screen when the operator saves. There is no browser storage.
- **RLS.** Enabled. `SELECT` is granted to operators gated on the proforma row;
  insert, update and delete are revoked from `authenticated`, so only the
  `SECURITY DEFINER` write function can change them. Anon has nothing.

**Verified fact, RLS:** with a customer JWT the table returns 0 rows; with an admin
JWT it returns the row.

**Limitation, stated plainly:** a proforma saved before C5 has no settings rows, so
reopening it shows empty rate boxes until an operator saves. No backfill was
invented, because the two figures were never stored and a guess would become a
price.

**Deployment without the migration:** the API falls back to
`save_inquiry_proforma_v1` when v2 is absent, and the workspace says the settings
were not saved instead of pretending they were.

---

## 6. The migration

`website/supabase-inquiry-proforma-phaseC5-rates-and-status.sql`, one transaction,
idempotent:

1. `CREATE TABLE IF NOT EXISTS inquiry_proforma_groups` with constraints, RLS and
   policies.
2. `UPDATE ... SET status = 'Cancelled' WHERE status = 'Void'`, then drop every
   `status` check on the table (found by definition, not by assumed name) and add
   `inquiry_proformas_status_check`.
3. `inquiry_next_proforma_number(text, integer)`, the case-insensitive unique
   index (refusing to build over pre-existing case duplicates), and the grants.
4. `CREATE OR REPLACE FUNCTION save_inquiry_proforma_v2(...)`, identical to v1 apart
   from the `p_groups` argument, the allocation call and the settings write.

Apply order: after `supabase-inquiry-proforma-phase15.sql`,
`supabase-inquiry-proforma-phase15-write.sql` and
`supabase-inquiry-proforma-phaseC1.sql`. Nothing else references it.

---

## 7. Executed database validation

Disposable PostgreSQL 17.9 (same major version as the project, which reports
17.11), rebuilt from 19 repository migrations plus a seed of representative data,
with the C5 file applied twice.

| Group | Result |
| --- | --- |
| Apply twice | second run exit 0, no error, no duplicate object |
| Status probes (5) | `Sent`, `Approved`, `Issued`, `Cancelled` accepted; `Void` rejected 23514 |
| Group constraint probes (8) | group 0 and 51, negative base, percent 1001, unknown proforma FK, revision 0, duplicate key; all rejected as expected, a valid row accepted |
| Allocation probes (14) | all passed, section 3 |
| Save path through v2 | revision 1 wrote two specifications, allocated `PI-WEB-2026-000002`; revision 2 changed one and kept revision 1 intact; the number did not change |
| Idempotency replay | same proforma id returned, 3 group rows and 2 save rows, nothing written twice |
| Legacy database | `Void` row became `Cancelled`, 3 rows before and after, allocation continued at `PI-WEB-2026-000008` |
| RLS | customer 0 rows, admin 1 row |
| Anon | `permission denied for table inquiry_proforma_groups` |

---

## 8. Automated gates

| Gate | Result |
| --- | --- |
| `npx vitest run` | 31 files, 336 tests, all passed |
| `node --test scripts/inquiry-proforma-identity-c1.test.mjs` | 12 tests, all passed |
| `npm run test:inquiry-security` | 142 tests, all passed |
| `npm run lint` | clean |
| `npx tsc --noEmit` | clean |
| `npm run build` | production build succeeded |

New coverage: status vocabulary and legacy mapping, the empty transition list, the
pending list, settings load/send round trip, payload filtering by specification
number and by the stored maximum, percentage and comma input, and three cases in
the rate bar for restored and empty settings.

---

## 9. Production migration status

**Not applied.** No DDL and no row change was issued against the Supabase project,
and nothing was deployed. Before applying:

1. Apply it on a Supabase database branch, after C1.
2. Confirm the group table, `inquiry_proformas_status_check`, the case-insensitive
   index and `save_inquiry_proforma_v2` exist, and that no `Void` row remains.
3. Run the probes in section 7 on that copy.

---

## 10. Remaining genuine dependencies

1. **Issuance and acceptance contracts.** `Sent`, `Approved` and `Issued` are words
   with no operation behind them. They need the desktop's acceptance and issuance
   behaviour before any transition can be implemented; `IMPLEMENTED_TRANSITIONS`
   stays empty until then.
2. **Desktop publishing contract.** The desktop must not publish numbers in the
   `PI-WEB-` family (section 3, Limitation). Everything else about coexistence is
   already enforced by the case-insensitive unique index.
3. **Step 6 charges, discounts and VAT** are untouched, as instructed. The rate
   settings are Step 5 inputs, not a second place where money is stored.
4. **PDF and publication** of the saved document remain unimplemented.

---

## 11. Files changed

| File | Change |
| --- | --- |
| `website/supabase-inquiry-proforma-phaseC5-rates-and-status.sql` | new migration |
| `website/src/lib/proforma-status.ts` | new vocabulary module |
| `website/src/lib/proforma-rates.ts` | `settingsFromServer`, `settingsPayload`, `%` tolerance |
| `website/src/lib/proforma-api.ts` | v2 save with v1 fallback, groups read, status normalisation, cancelled proformas readable |
| `website/src/lib/proforma-document.ts` | cancelled wording |
| `website/src/types/proforma.ts` | `ProformaGroupSetting`, workspace `groups` and `groups_persisted`, save `groups` |
| `website/src/app/api/inquiries/[id]/proforma/route.ts` | validate and forward `groups` |
| `website/src/components/inquiries/ProformaWorkspace.tsx` | hydrate settings from the server, send them on save, honest notice |
| `website/src/components/inquiries/proforma/SpecificationRateBar.tsx` | copy no longer calls the settings a working aid |
| `website/src/components/inquiries/proforma/ProformaDocument.tsx` | five status tones |
| `website/tests/proforma-status.test.tsx` | new, 14 tests |
| `website/tests/proforma-rates.test.tsx` | 3 restored-settings tests |
| `website/tests/proforma-document.test.tsx` | `Void` case became `Cancelled` |

# Phase C4: Step 5 grid actions and validation parity

Implementation checkpoint. This phase implements the two pieces of Step 5 work that
were still unblocked after C3: the row actions an operator expects in a
specification grid, and client validation that mirrors the checks
`save_inquiry_proforma_v1` already performs.

No migration, no change to the write function, no change to any WPF file, and
nothing applied or deployed.

---

## 1. Source basis

The limits are not invented. They are the parameter checks in
`website/supabase-inquiry-proforma-phase15-write.sql`, which is the authority that
accepts or rejects a save:

| Field | Range the function enforces |
| --- | --- |
| Lines in one save | 1 to 200 |
| `group_no` | 1 to 50 |
| `line_no` | 1 to 500 |
| `w1`, `h1` | 1 to 20000 mm |
| `w2`, `h2` | 0 to 20000 mm |
| `qty` | 1 to 100000 |
| `rate` | 0 to 1000000 |

The grid previously checked only the first two dimension fields, the quantity
floor and that a rate was a number. A draft could therefore pass the client check
and still be rejected by the server, which is exactly the round trip this phase
removes.

The duplicate and move behaviour is the ordinary editing behaviour of the desktop
grid. Duplicating a row repeats a specification line with the same glass, size,
quantity and rate; moving a row reorders it inside its own specification. Nothing
here changes pricing: the area and the value follow the row's own dimensions, and
the database still computes the stored money.

---

## 2. What was implemented

### Validation parity

`draftIssues` now reports the same limits the function enforces, in the same order
the rows appear:

- more than 200 lines in the draft, or more than 500 in one specification;
- more than 50 specifications, or a specification number outside 1 to 50;
- a panel side above 20000 mm, or an inner size above 20000 mm;
- a quantity above 100000, or below 1;
- a rate that is not a number, is negative, or exceeds 1000000.

Messages name the specification and the line, so a large draft does not produce an
anonymous list.

### Row actions

Each row now carries four controls: `Up`, `Down`, `Copy`, `Remove`.

- `Copy` inserts an identical row directly after its source, with a fresh key and
  the same specification, glass reference, dimensions, quantity and rate. Line
  numbers are derived from the order, so the copy becomes a real second line and
  every later line of that specification renumbers.
- `Up` and `Down` swap the row with its neighbour inside the same specification. A
  row never crosses into another specification, because the specification is what
  the line belongs to. The buttons are disabled at the top and bottom of a
  specification, so an impossible action is not offered.
- `Remove` is unchanged.

Both actions clear any pending validation list and, for a copy, say what happened
in the workspace notice.

---

## 3. Files added or changed

Added:

- `website/tests/proforma-grid-actions.test.tsx` — 15 tests.
- `website/docs/PHASE_C4_GRID_ACTIONS_AND_LIMITS.md` — this report.

Changed:

- `website/src/lib/proforma-draft.ts` — `duplicateLine`, `moveLine`, and the
  extended `draftIssues`.
- `website/src/components/inquiries/proforma/ProformaSpecification.tsx` — the row
  controls and two new callbacks.
- `website/src/components/inquiries/ProformaWorkspace.tsx` — `duplicateRow` and
  `moveRow`, wired to the grid.
- `website/tests/inquiry-inbox-cards.test.tsx` — the existing surface test supplies
  the two new callbacks. No assertion changed.

Unchanged: the Phase 15 schema and function, the API routes, `pricingRules.ts`, the
Phase C1 migration, every WPF file.

---

## 4. Migrations

None. C1 remains unchanged (`sha256 52d42d62...`) and unapplied.

---

## 5. Tests run and their results

Run from `website/` on 2026-10-24.

| Command | Result |
| --- | --- |
| `npx vitest run tests/proforma-grid-actions.test.tsx` | 15 tests, 15 pass |
| `npx vitest run` | 30 files, 319 tests, all pass (was 29 / 304) |
| `node --test scripts/*.test.mjs` | 627 tests, 627 pass (unchanged) |
| `npm run lint` | clean |
| `npx tsc --noEmit` | clean |
| `npm run build` | succeeded |

The new tests cover: the copy landing immediately after its source with every
figure preserved and a new key; renumbering of the specification after a copy; an
unknown key being a no-op; a move swapping two rows and renumbering them; a move
refusing to cross into another specification; the ends of a specification being
no-ops; each of the six new validation limits; and, at the component level, that
`Copy` and `Up` report the right row and direction and that the end buttons are
disabled.

Not tested: the flow against a live Supabase project, because no operator session
is available in this sandbox, and pointer interaction in a real browser.

---

## 6. Decisions still blocking the rest of the workflow

Unchanged by this phase:

1. The 4 m2 threshold and the 20 percent standard: configuration with an effective
   date, or constants.
2. Whether the portal presents VAT, other charges and terms at all.
3. PI numbering across origins.
4. Status meanings, in particular `Sent` versus `Issued`, which leaves `Issued` and
   `Void` unreachable in the schema.
5. Whether a specification's base rate and percentage must survive a reload, which
   needs an approved additive table and a write path.

---

## 7. Complete versus unfinished

Complete: Step 5 grid editing for a website proforma, with four row actions and
client validation that matches the database's own limits. Covered by 15 tests, with
the whole suite and the production build green.

Unfinished: Step 4 estimation (stays on the desktop), Step 6 charges, discount and
VAT, issuance and acceptance, desktop publication, and a server rendered PDF. All
of them are blocked on the decisions above, not on missing code.

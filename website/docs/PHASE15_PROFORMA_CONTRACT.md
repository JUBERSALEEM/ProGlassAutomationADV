# Website proforma contract, version 1

Status: implemented on the website, not consumed by the desktop yet.
Owner surface: `website/`, tables `public.inquiry_proformas`,
`public.inquiry_proforma_lines`, `public.inquiry_proforma_saves`.
Function: `public.save_inquiry_proforma_v1(uuid, text, integer, text, jsonb)`.

This document is the contract a desktop consumer would implement. It records
what exists today and, in the last section, exactly what does not.

## 1. Identity

| Field | Meaning |
| --- | --- |
| `proforma_number` | The document number. Website originals are `PI-WEB-YYYY-NNNNNN`, allocated by a sequence. Desktop originals would be `PI-DSK-...` under this contract. |
| `source_system` | `website` or `desktop`. It is the origin of the document, not the last writer. |
| `source_proforma_number` | The desktop's own number for a desktop origin. Empty for a website original. |
| unique `(source_system, source_proforma_number)` | One document per origin and number. A desktop publication that reuses a website number collides here instead of creating a second record. |
| unique open draft per `inquiry_id` where `source_system = 'website'` | One website draft per inquiry. |

`public.inquiries.proforma_number` remains the desktop's reference, copied from
the publication. It is not a key into these tables and the two are never merged
implicitly.

## 2. Read

`GET /api/inquiries/{inquiryId}/proforma`

- `Authorization: Bearer <supabase access token>` (required).
- The token is verified with `auth.getUser`, then the caller's enabled role is
  read from `inquiry_operator_roles`. Read roles: `inquiry_admin`,
  `inquiry_auditor`.
- Response, HTTP 200:

```json
{
  "ok": true,
  "data": {
    "inquiry": { "id": "...", "inquiry_number": "INQ-2026-0042", "customer_name": "...", "project_name": "...", "status": "Sent", "proforma_number": "", "inquiry_value": 184500 },
    "proforma": { "id": "...", "proforma_number": "PI-WEB-2026-000001", "source_system": "website", "status": "Draft", "currency": "AED", "revision": 2, "line_count": 3, "total_sqm": 22.14, "total_value": 3665.99, "...": "..." },
    "lines": [{ "group_no": 1, "line_no": 1, "glass_ref": "6mm Clear FT", "w1": 1200, "h1": 900, "w2": 0, "h2": 0, "qty": 8, "approved_rate": 150, "sqm": 1.08, "total_sqm": 8.64, "line_value": 1296 }],
    "saves": [{ "revision": 2, "line_count": 3, "total_value": 3665.99, "saved_at": "..." }],
    "installed": true
  }
}
```

`installed` is false when the Phase 15 tables are absent, which is a statement
about the deployment, not an empty draft.

## 3. Write

`POST /api/inquiries/{inquiryId}/proforma`

Headers: `Authorization: Bearer ...`, `Content-Type: application/json`,
`Idempotency-Key: <uuid v4>`.

```json
{
  "expected_revision": 2,
  "currency": "AED",
  "lines": [
    { "group_no": 1, "line_no": 1, "glass_ref": "6mm Clear FT", "w1": 1200, "h1": 900, "w2": 0, "h2": 0, "qty": 8, "rate": 150 }
  ]
}
```

Rules the database enforces:

- Role `inquiry_admin` inside `save_inquiry_proforma_v1`, checked again after
  the route has checked it. Authorization never lives in a page.
- `expected_revision = 0` creates the draft. On an existing draft the value must
  equal the current `revision`, otherwise `40001` and HTTP 409.
- `idempotency_key` is unique in `inquiry_proforma_saves`. Repeating a save with
  the same key returns the same proforma and does not create a revision.
- Between 1 and 200 lines, groups 1 to 50, lines 1 to 500, panel sides 1 to
  20000 mm, quantity at least one, rate 0 to 1,000,000.
- `sqm` is the billable panel area, floored at 0.5 m², rounded to four places.
  `total_sqm` is `sqm * qty`. `line_value` is `round(total_sqm * rate, 2)`. All
  three are computed in the function. A client value is never stored.
- Each accepted save appends a row to `inquiry_proforma_saves` with the new
  revision, the key, the line count, the totals and the operator.

Status codes: 200, 400 (`INVALID`), 401, 403, 404, 409 (`CONFLICT`), 503.

## 4. Access

- RLS is on for all three tables. `anon` holds no grant and no policy.
- `authenticated` holds `SELECT` only; insert, update and delete are revoked.
  The only write path is the `SECURITY DEFINER` function, which revokes execute
  from `PUBLIC` and `anon` and grants it to `authenticated`.
- Read policies require `has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor'])`.
- No service role key is used by any route or client. No policy grants the
  website tables to the bridge or the estimator role.

## 5. What the desktop must do, and what it cannot do yet

Missing desktop dependencies, stated plainly:

1. **No desktop consumer exists.** Nothing in the Windows application reads
   `inquiry_proformas`, `inquiry_proforma_lines` or `inquiry_proforma_saves`.
   A website draft is not visible in the desktop today.
2. **No desktop publisher exists.** A proforma created in the desktop is not
   written to these tables, so a desktop PI number in
   `inquiries.proforma_number` is a reference only. The workspace says so.
3. **No conflict rule is implemented.** The unique index on
   `(source_system, source_proforma_number)` prevents a silent duplicate, but
   nothing reconciles a desktop document against a website draft for the same
   inquiry. Until that rule exists, a website draft and a desktop PI for one
   inquiry remain two documents, and the workspace states that in the UI.
4. **No revision cursor is consumed.** `inquiry_proforma_saves` is the intended
   cursor for an incremental reader. It is populated and unused.

When a desktop consumer is written it must:

- authenticate as an operator with an enabled `inquiry_admin` role and never
  with `service_role`, and never call the write function from a browser;
- send an idempotency key and the revision it read, and treat HTTP 409 as
  "reload, then reapply", never as "overwrite";
- never allocate a number that the website already holds for the same origin;
- never treat `inquiries.proforma_number` as this contract's identity.

## 6. Evidence recorded at implementation time

- Migrations applied: `inquiry_proforma_phase15_tables`,
  `inquiry_proforma_phase15_write`.
- Rolled-back probe as an enabled admin: `PI-WEB-2026-000001` allocated,
  1000 x 1000 at qty 2 and rate 100 stored `sqm 1.0000`, `total_sqm 2.0000`,
  `line_value 200.00`; a retry with the same key returned the same id and left
  `revision` at 1; a stale `expected_revision` raised `40001`; a three line save
  produced revision 2 with `22.1400` sqm and `3665.99`, including a 300 x 300
  panel floored to `0.5` sqm and priced at `149.99`; a zero quantity raised
  `22023`.
- Estimator token: `42501`. Anon: `permission denied for table
  inquiry_proformas`.

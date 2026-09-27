# SAAS API DESIGN — /api/v1

> **Document status:** Phase 2 deliverable (STEP 6 of the SaaS master plan).
> REST over HTTPS, JSON. Versioned path prefix `/api/v1` (master prompt §41) — breaking
> changes require `/api/v2` or additive fields only. OpenAPI generated at build time.

## 0. Cross-cutting conventions

- **Auth:** `Authorization: Bearer <access JWT>`; refresh via `POST /auth/refresh`
  (HttpOnly cookie or token rotation). Tenant, user, role and permissions travel in claims;
  **the server never accepts a tenant id from the request body/query** (§24).
- **Errors:** consistent envelope on every non-2xx:
  ```json
  { "code": "proforma.invalid_transition", "message": "Cannot move from Sent to PendingApproval.",
    "correlationId": "c1a2…", "details": { … } }
  ```
  400 validation · 401 unauthenticated · 403 forbidden · 404 not-found-in-tenant (never
  reveals cross-tenant existence) · 409 conflict (state/concurrency — includes current
  `rowVersion`) · 422 business-rule violation · 429 rate/limit · 402 subscription limit.
  No stack traces (§33).
- **Pagination:** `?page=1&pageSize=50` (max 200) → `{ items, page, pageSize, totalCount }`.
  **Sorting/filtering server-side** (`?status=`, `?dateFrom=`, `?dateTo=`, `?customerId=`,
  `?search=` full-text) — never fetch-all-and-filter-in-browser (§54).
- **Idempotency (§53):** `Idempotency-Key` header honored on financial POSTs (invoices,
  stock transactions, imports, optimization runs).
- **Concurrency (§52):** optimistic — clients send `rowVersion` on PUT/PATCH; stale → 409
  with current entity.
- **Money:** returned as strings with 2 decimals (`"1234.50"`); areas as 3/4-decimal
  strings; the backend recomputes all totals — client math is preview-only (§35).

## 1. Authentication & account

```
POST   /api/v1/auth/register            # tenant + Owner user + trial subscription
POST   /api/v1/auth/login               # → { accessToken, expiresIn } + refresh cookie
POST   /api/v1/auth/refresh             # rotation + reuse detection
POST   /api/v1/auth/logout
POST   /api/v1/auth/forgot-password
POST   /api/v1/auth/reset-password
POST   /api/v1/auth/verify-email
POST   /api/v1/auth/mfa/setup | /auth/mfa/verify | /auth/mfa/disable
GET    /api/v1/auth/sessions            # active sessions
DELETE /api/v1/auth/sessions/{id}       # revoke
```
Lockout mirrors WPF (`failed_attempt_count`, `locked_until`) — response 423 with retry-after.

## 2. Users, roles, permissions (tenant-scoped)

```
GET/POST          /api/v1/users
GET/PUT/DELETE    /api/v1/users/{id}          # DELETE = deactivate (WPF rule: never orphan audit)
GET               /api/v1/roles               # catalog (Administrator, Manager, Estimator,
                                              #  Sales, Production, Store, Accountant, Viewer)
GET               /api/v1/permissions         # Module.Capability catalog
```

## 3. Master data

```
GET/POST          /api/v1/companies          GET/PUT/DELETE /api/v1/companies/{id}
GET/POST          /api/v1/clients            GET/PUT/DELETE /api/v1/clients/{id}
GET/POST          /api/v1/projects           GET/PUT/DELETE /api/v1/projects/{id}
GET/POST          /api/v1/salesmen           GET/PUT/DELETE /api/v1/salesmen/{id}
```
All list endpoints support `?search=` (recency + usage_count ordering for autocomplete parity).

## 4. Inquiries

```
GET    /api/v1/inquiries?status=&search=&page=
POST   /api/v1/inquiries
GET    /api/v1/inquiries/{id}
PUT    /api/v1/inquiries/{id}
DELETE /api/v1/inquiries/{id}                    # Inquiry.Delete permission
POST   /api/v1/inquiries/{id}/convert-to-proforma   # BOQ conversion → returns proforma id
```
(Inquiry attachments land on the entity-generic `/api/v1/attachments`
surface below — Phase 24; `inquiry` is one of the six entity types.)

## 4a. Attachments (Phase 24)

```
POST   /api/v1/attachments/{entityType}/{entityId}   # multipart upload (≤ 10 MB, image/PDF)
GET    /api/v1/attachments/{entityType}/{entityId}   # metadata list
GET    /api/v1/attachments/{id}/content              # bytes (original filename + content type)
DELETE /api/v1/attachments/{id}                      # row authoritative, file best-effort
GET    /api/v1/attachments/usage                     # tenant storage counter (Reports.View)
```
`entityType` ∈ {delivery_note, proforma_invoice, tax_invoice,
credit_note, job_order, inquiry} — each gated by that entity's own
permission pair: view for list/download, edit for upload/delete.
Validation order is the WPF's own: extension first, then the 10 MB
size limit. Byte storage is the filesystem under `SAAS_ATTACHMENT_ROOT`
(`{tenant}/{entity-type}/{entity-id}/{unique-name}`), with
`attachments.storage_key` the indirection so an object-storage backend
can replace it later without a schema change. See mapping §1.13.

## 5. Specifications & pricing

```
GET    /api/v1/specifications?glassType=&search=
POST   /api/v1/specifications                     # = WPF Step 4 APPLY
GET/PUT/DELETE /api/v1/specifications/{id}
POST   /api/v1/pricing/preview                    # SGU|DGU|LAM|DGU+LAM params →
                                                   # { ratePerSqm, costBasisPerSqm, warnings[] }
GET    /api/v1/pricing/config                     # tenant catalogs (thickness/treatment/ASP/…)
PUT    /api/v1/pricing/config                     # Settings.Manage
POST   /api/v1/pricing/apply                      # stamp rate onto existing spec groups
```
`preview` returns the SAME warnings the WPF surfaces (yield ≤ 0 fallback; unpriced
interlayer) in a `warnings[]` array — parity with BUG-40/41 behavior.

## 6. Proforma invoices

```
GET    /api/v1/proforma?status=&customerId=&projectId=&dateFrom=&dateTo=&search=&page=
POST   /api/v1/proforma                           # draft (allocates PI-{year}-NNN)
GET    /api/v1/proforma/{id}                      # includes snapshot + steps completion
PUT    /api/v1/proforma/{id}                      # rowVersion required
DELETE /api/v1/proforma/{id}                      # soft (void) for posted, hard for empty drafts
POST   /api/v1/proforma/{id}/duplicate            # snapshot scrub parity
POST   /api/v1/proforma/{id}/revisions            # create -R{n} revision (chain via root id)
GET    /api/v1/proforma/{id}/revisions
POST   /api/v1/proforma/{id}/transition           # { to } — 409 on invalid state machine move
POST   /api/v1/proforma/{id}/void                 # { reason }
GET    /api/v1/proforma/{id}/pdf                  # server-rendered (QuestPDF)
GET    /api/v1/proforma/{id}/export?format=excel|csv
POST   /api/v1/proforma/{id}/items/import         # upload → parse → validate
GET    /api/v1/proforma/{id}/items/import/{batchId}/preview    # errors + diff
POST   /api/v1/proforma/{id}/items/import/{batchId}/commit     # explicit confirmation
GET    /api/v1/proforma/{id}/profit-loss          # Step 9 P&L (sheets, services, glass-type comparison)
GET    /api/v1/proforma/suggest-next-number
```

## 7. Sheet Store

```
GET    /api/v1/stock/sheets?category=&color=&thickness=&lowStock=&search=&page=
POST   /api/v1/stock/sheets                       # new sheet type (unique identity rule)
GET/PUT/DELETE /api/v1/stock/sheets/{id}
POST   /api/v1/stock/purchases                    # { sheetId, qty, pricePerSqm, supplier, date } → transaction
POST   /api/v1/stock/issues                       # consumption { sheetId, qty, referenceType, referenceId }
POST   /api/v1/stock/adjustments                  # { sheetId, qtyDelta, reason } — audited
GET    /api/v1/stock/sheets/{id}/history?page=    # movement ledger
GET    /api/v1/stock/inventory                    # consumables (spacer LM etc.)
POST   /api/v1/stock/inventory/{id}/movements
POST   /api/v1/stock/import/preview | /commit     # CSV/Excel
GET    /api/v1/stock/export?format=csv|excel
```

## 8. Job orders & production

```
GET/POST /api/v1/job-orders     GET/PUT /api/v1/job-orders/{id}
POST /api/v1/job-orders/{id}/transition          # InProduction|Completed|Delivered|Approved
GET  /api/v1/job-orders/{id}/delivery-summary    # per-JO SQM + delivered/balance/returned
GET/POST /api/v1/production/pieces?jobOrderId=&stage=&status=
POST /api/v1/production/pieces/{id}/stage        # advance stage
POST /api/v1/production/pieces/{id}/qc           # pass/fail
POST /api/v1/production/pieces/{id}/breakage
POST /api/v1/production/pieces/{id}/release | /cancel
GET  /api/v1/production/stages                   # stage work-center summaries
POST /api/v1/production/material-reservations    # reserve glass before cutting
GET  /api/v1/production/consistency-scan         # latest scan report (scheduled job)
```

## 9. Delivery notes

```
GET/POST /api/v1/delivery-notes   GET/PUT/DELETE /api/v1/delivery-notes/{id}
POST /api/v1/delivery-notes/{id}/dispatch       # Draft→Dispatching→Dispatched
POST /api/v1/delivery-notes/{id}/sign           # Signed
POST /api/v1/delivery-notes/{id}/receive        # Received (partial: per-item qty + returns)
POST /api/v1/delivery-notes/{id}/items/{itemId}/return
GET  /api/v1/delivery-notes/{id}/pdf
```

## 10. Tax invoices, credit notes, statements, VAT

```
GET/POST /api/v1/tax-invoices    GET/PUT /api/v1/tax-invoices/{id}
POST /api/v1/tax-invoices/{id}/mark-paid | /mark-overdue | /void
GET  /api/v1/tax-invoices/{id}/pdf
GET/POST /api/v1/credit-notes    POST /api/v1/credit-notes/{id}/issue | /void
POST /api/v1/statements/generate                 # filters → draft + preview lines
GET  /api/v1/statements?customerId=&page=        GET /api/v1/statements/{id}
GET  /api/v1/statements/{id}/pdf
GET  /api/v1/reports/vat?dateFrom=&dateTo=&status=    # VAT submission data + export
```

## 11. Optimization

```
POST /api/v1/optimization/run              # { jobId? , pieces?, stocks?, rotation, split,
                                           #   minBreakoutMm?, effortLevel?, platePattern? }
                                           # effortLevel 1..5 (null → 3, the WPF OPTIMIZE default;
                                           # 1 = the QUICK pass); platePattern = Yield |
                                           # FillFirstPlate | FewestCuts | MinimiseStockChanges |
                                           # RemnantFirst | BalancedSpread (Phase 25a)
POST /api/v1/optimization/validate         # server re-validation of any submitted layout
GET  /api/v1/optimization/jobs/{jobId}     # status + progress { stage, completed, total }
GET  /api/v1/optimization/jobs/{jobId}/result           # sheets, placements, cuts, leftovers, metrics
GET  /api/v1/optimization/jobs/{jobId}/layout.svg       # rendered visualization (parity requirement §28)
GET  /api/v1/optimization/jobs/{jobId}/cut-list         # stage-ordered cut list
GET  /api/v1/optimization/jobs/{jobId}/export?format=cnc|excel|csv
GET  /api/v1/optimization/sessions?documentId=&page=
GET  /api/v1/optimization/config            # tenant margin house table (ThicknessBucket defaults)
PUT  /api/v1/optimization/config            # Settings.Manage; IsCustomized explicit
```
Request payload mirrors WPF inputs: `stockSizes[]` (with `maxSheets`, sentinels 0=∞-explicit
/ 99999999=default), `pieces[]` (with full provenance), `rotationPolicy`, `splitMode`,
`platePattern`, `cutComplexity`, `minTrimWidthMm`, per-group `marginProfile`, `effort`,
`seed`. The server **never trusts client-computed layouts**; persisted results must pass
both oracles (§7 audit) and are quarantined otherwise.

## 12. Reports & dashboard

```
GET /api/v1/reports/{reportKey}?filters…&page=      # company|customer|delivery|executive|glass-spec|
                                                    # job-order|order|pi|profitability|project|
                                                    # salesman|statement|tax-invoice|vat|
                                                    # cutting|production-control|production-process
GET /api/v1/reports/{reportKey}/export?format=pdf|excel|csv
GET /api/v1/dashboard                              # server-aggregated metrics + widgets
```

## 13. Files, imports, audit, subscriptions (admin)

```
POST /api/v1/files                    # validated upload → { storageKey }
GET  /api/v1/files/{key}/download     # signed URL
GET  /api/v1/audit-logs?action=&entityType=&dateFrom=&dateTo=&page=   # Audit.View
GET  /api/v1/subscription             # current plan, status, usage vs limits
GET  /api/v1/subscription/usage
```
Platform-admin API (separate host/prefix, super-admin role): tenants, plans, features,
subscription overrides — v1 internal.

## 14. Rate limits (initial, per tenant)

Login 10/min · writes 120/min · optimization runs 10 concurrent · imports 5 concurrent ·
exports 10/min · reports 60/min. 429 + `Retry-After`.

## 15. Versioning & compatibility

- Additive changes only within v1 (new optional fields, new endpoints).
- Breaking changes → `/api/v2` with overlap period; deprecation headers
  `Sunset` + `Deprecation` per RFC 8594.
- OpenAPI spec is the Android-client contract (§57) — published as CI artifact.

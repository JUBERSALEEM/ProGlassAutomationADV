# SAAS DATABASE DESIGN — PostgreSQL 16

> **Document status:** Phase 2 deliverable (STEP 5 of the SaaS master plan).
> Conventions: every tenant-owned table has `id UUID PK DEFAULT gen_random_uuid()`,
> `tenant_id UUID NOT NULL FK → tenants(id)`, `created_at / updated_at TIMESTAMPTZ NOT NULL DEFAULT now()`,
> `created_by / updated_by UUID NULL FK → users(id)`. Money = `NUMERIC(18,2)`; areas =
> `NUMERIC(12,4)`; rates per SQM = `NUMERIC(18,4)` (intermediate precision before Money
> rounding — mirrors WPF MoneyRounding.Round(value, decimals) overload). Every index on
> tenant-owned tables **leads with tenant_id**. Soft-void over delete for fiscal docs.

## 1. Platform (not tenant-scoped)

```sql
tenants(id, name, slug UNIQUE, status, timezone, created_at, deleted_at)  -- deleted_at = soft offboarding
plans(id, code, name, price NUMERIC(18,2), billing_period, is_active)
features(id, key UNIQUE)                       -- e.g. 'optimization', 'reports.export'
plan_features(plan_id FK, feature_id FK, limit_value INT NULL)   -- NULL = unlimited
subscriptions(id, tenant_id, plan_id, status /*Trialing|Active|PastDue|Suspended|Cancelled*/,
              period_start, period_end, trial_ends_at, cancel_reason)
tenant_features(tenant_id, feature_id, is_enabled, limit_value)
```

## 2. Identity & authorization

```sql
users(id, tenant_id, username, display_name, email, mobile,
      password_hash /*Argon2id*/, role /*catalog string*/, is_active,
      failed_attempt_count, locked_until_utc,
      mfa_secret_encrypted, email_confirmed_at, last_login_at)
      UNIQUE(tenant_id, username), UNIQUE(tenant_id, email)
refresh_tokens(id, user_id, token_hash, expires_at, absolute_expires_at_utc, revoked_at, replaced_by_token_hash, created_ip)
audit_logs(id, tenant_id, user_id, action /*e.g. 'invoice.created'*/, entity_type, entity_id,
           before_json JSONB, after_json JSONB, ip, correlation_id, created_at)
      INDEX(tenant_id, created_at), INDEX(tenant_id, entity_type, entity_id)
```
Roles/permissions catalog lives in code (ported verbatim from
`Services/Audit/Permissions.cs`) — not in join tables — so permission changes ship as
versioned code with tests, matching WPF behavior. A `role` column + claims transformation.

## 3. Master data

```sql
companies(id, tenant_id, name, trn, location, representative, representative_mobile,
          email, account_number, bank_name, logo_storage_key, is_default, usage_count, last_used_at)
clients(id, tenant_id, customer_name, customer_trn, reference_code, sales_representative,
        billing_address, attention_executive_name, phone, usage_count, last_used_at)
projects(id, tenant_id, identifier_name, contract_no, reference, site_delivery_location,
         lpo_number, customer_id FK→clients, usage_count, last_used_at)
salesmen(id, tenant_id, name, department, usage_count, last_used_at)
```
(Columns map 1:1 to WPF `SavedCompany/SavedClient/SavedProject/SavedSalesman`.)

## 4. Inquiry

```sql
inquiries(id, tenant_id, inquiry_no, status /*Received|Prepared|Sent|Confirmed|Hold|Cancelled
          — CHECK constraint, unknown passes through via status_text*/, priority,
          customer_name, project_name, salesman, notes, received_at,
          boq_json JSONB, converted_proforma_id UUID NULL, is_active)
attachments(id, tenant_id, entity_type, entity_id, storage_key, original_name,
            mime_type, size_bytes, uploaded_by, created_at)   -- shared by all doc types
```

## 5. Specifications & pricing config

```sql
specifications(id, tenant_id, spec_name, glass_type /*SGU|DGU|LAM|DGU+LAM*/,
               params_json JSONB, rate_per_sqm NUMERIC(18,2), cost_basis_per_sqm NUMERIC(18,2),
               usage_count, last_used_at, is_active)
tenant_pricing_config(id, tenant_id,
    yield_factor_default NUMERIC(8,4), margin_default NUMERIC(5,2),
    thickness_options TEXT[], treatment_options TEXT[], asp_widths TEXT[],
    interlayer_types TEXT[], interlayer_thicknesses TEXT[], polish_options TEXT[],
    panel_extra_options TEXT[])
tenant_vat_config(id, tenant_id, vat_rate NUMERIC(5,4) DEFAULT 0.0500, currency CHAR(3) DEFAULT 'AED')
```

## 6. Proforma Invoice (normalized core + snapshot fidelity)

```sql
proforma_invoices(id, tenant_id, proforma_no /*PI-{year}-NNN, UNIQUE(tenant_id, proforma_no)*/,
    root_proforma_id UUID NULL, revision_number INT DEFAULT 0,
    inquiry_id UUID NULL, company_id, client_id, project_id, salesman_id,
    status /*Draft|Sent|Approved|Rejected|Expired|Cancelled*/,
    lifecycle_state /*Draft|PendingApproval|Approved|Sent|Confirmed|Cancelled|Rejected*/,
    issue_date DATE, valid_until DATE,
    glass_materials_subtotal, other_charges_subtotal, discount_amount,
    vat_amount, grand_total_vat_inclusive,          -- all NUMERIC(18,2)
    is_void, voided_at, voided_by, void_reason,
    furthest_step INT, snapshot_json JSONB,          -- wizard-state fidelity (WPF parity)
    row_version,                                     -- EF concurrency (xmin)
    sent_at, approved_at, confirmed_at, job_order_id UUID NULL)
    INDEX(tenant_id, status), INDEX(tenant_id, issue_date), INDEX(tenant_id, customer... via client_id)

spec_groups(id, tenant_id, proforma_id FK, group_number, glass_type, spec_name,
            spec_id UUID NULL, rate_per_sqm NUMERIC(18,2), price_source /*formula|manual|target*/)
spec_group_items(id, tenant_id, spec_group_id FK, row_serial, position, width_mm INT,
    height_mm INT, qty INT, w1 INT, h1 INT, w2 INT, h2 INT, sqm NUMERIC(12,3),
    total_sqm NUMERIC(12,3), unit_price NUMERIC(18,2), total NUMERIC(18,2), notes)
other_charges(id, tenant_id, proforma_id FK, charge_name, metric_key /*SQM|LM|Qty|Fixed*/,
    checked_spec_group_numbers INT[], rate NUMERIC(18,2), amount NUMERIC(18,2), is_active)
wastage_rows(id, tenant_id, proforma_id FK, spec_group_id, color_name, stock_sqm NUMERIC(12,4),
    glass_sqm NUMERIC(12,4), optimization_percent NUMERIC(6,3), sheet_quantity INT,
    formula_price_per_sqm NUMERIC(18,2), rate NUMERIC(18,2), result_amount NUMERIC(18,2),
    is_active)
```

## 7. Sheet Store & inventory

```sql
glass_sheets(id, tenant_id, category, thickness_mm INT, color, width_mm INT, height_mm INT,
    supplier, purchase_price_per_sqm NUMERIC(18,2), sell_price_per_sqm NUMERIC(18,2),
    stock INT, used INT, last_purchase_date,
    UNIQUE(tenant_id, category, thickness_mm, color, width_mm, height_mm, supplier))
inventory_items(id, tenant_id, category /*spacer|sealant|desiccant|gas|hardware*/,
    name, supplier, stock NUMERIC(14,2) /*LM for spacer*/, used NUMERIC(14,2),
    reorder_level NUMERIC(14,2), purchase_price_per_unit, sell_price_per_unit, unit)
stock_transactions(id, tenant_id, item_type /*sheet|inventory*/, glass_sheet_id NULL,
    inventory_item_id NULL, transaction_type /*purchase|issue|return|adjustment|consume*/,
    quantity_delta NUMERIC(14,2), balance_after NUMERIC(14,2), unit_cost NUMERIC(18,2),
    reference_type, reference_id, notes, performed_by, created_at)
    INDEX(tenant_id, glass_sheet_id, created_at)   -- WPF SheetHistory parity: movement auditable
```

## 8. Job Order & Production

```sql
job_orders(id, tenant_id, job_no, proforma_id FK, status /*InProduction|Completed|Delivered|Approved*/,
    customer_id, project_id, salesman_id, due_date, notes, row_version)
production_pieces(id, tenant_id, job_order_id FK, piece_label /*'G-101 · 3/24'*/,
    spec_group_item_id NULL, stage /*cutting|lamination|edge_deletion|holes|mitring|pairing|polish|done*/,
    status /*pending|in_progress|qc_pass|qc_fail|broken|released*/,
    width_mm, height_mm, qty, optimization_sheet_id NULL, notes)
production_events(id, tenant_id, production_piece_id, event_type /*stage|qc|breakage|cancel|release*/,
    payload JSONB, user_id, created_at)          -- breakage/QC/release/cancellation history
material_reservations(id, tenant_id, job_order_id, glass_sheet_id, reserved_qty INT,
    status /*reserved|consumed|released*/, reserved_by, created_at)
```

## 9. Delivery

```sql
delivery_notes(id, tenant_id, delivery_no, job_order_id FK, status
    /*Draft|Dispatching|Dispatched|Signed|Received|Delivered — CHECK + state machine in domain*/,
    customer_id, project_id, delivery_date, driver, vehicle, notes, row_version)
delivery_note_items(id, tenant_id, delivery_note_id FK, spec_group_item_id NULL,
    description, width_mm, height_mm, qty INT, sqm NUMERIC(12,3),
    delivered_qty INT, returned_qty INT)          -- balance = qty − delivered + returned
```

## 10. Tax Invoice / Credit Note / Statement

```sql
tax_invoices(id, tenant_id, invoice_no UNIQUE(tenant_id, invoice_no), proforma_id NULL,
    delivery_note_reference, job_order_id NULL, status /*Issued|Paid|Overdue*/,
    issue_date, due_date, subtotal, discount_amount, vat_amount, grand_total,
    paid_amount, balance_amount, snapshot_json JSONB, row_version)
credit_notes(id, tenant_id, credit_note_no, tax_invoice_id FK, reason,
    subtotal, vat_amount, grand_total, status /*draft|issued|void*/, issued_at, void_*)
statements(id, tenant_id, statement_no, title, filter_company, filter_customer,
    filter_project, filter_salesman, filter_date_from, filter_date_to,
    generated_at, totals_json JSONB, storage_key /*rendered PDF*/)
statement_lines(id, tenant_id, statement_id, line_type, document_no, document_date,
    description, debit NUMERIC(18,2), credit NUMERIC(18,2), running_balance NUMERIC(18,2))
```

## 11. Optimization

```sql
optimization_sessions(id, tenant_id, document_type, document_id, composition_key,
    params_json JSONB /*rotation, split, platePattern, cutComplexity, margins per group,
    seed, effort*/, status /*queued|running|completed|failed*/, created_by, started_at,
    completed_at, scale_notes TEXT[], strategy, sheets_used INT, unplaced_count INT,
    total_used_sqm NUMERIC(14,4), total_stock_sqm NUMERIC(14,4), utilization_pct NUMERIC(6,2),
    conservation_verified BOOL, geometry_verified BOOL, validation_problems TEXT[],
    sheets_lower_bound INT)
optimization_sheets(id, tenant_id, session_id FK, sheet_no, stock_w_mm, stock_h_mm,
    thickness_mm, color, material_key, usable_x, usable_y, usable_w, usable_h,
    used_sqm NUMERIC(12,4), stock_sqm NUMERIC(12,4), utilization_pct NUMERIC(6,2),
    trim_cut_count INT)
optimization_placements(id, tenant_id, sheet_id FK, piece_ref, job_ref, glass_ref, spec_label,
    position, color, thickness_mm, x_mm, y_mm, w_mm, h_mm, rotated BOOL, qty)
optimization_cuts(id, tenant_id, sheet_id FK, seq INT, axis /*H|V*/, at_mm INT,
    from_x, from_y, from_w, from_h, stage INT, is_trim BOOL)
optimization_leftovers(id, tenant_id, sheet_id FK, x_mm, y_mm, w_mm, h_mm, is_reusable BOOL)
```
Result JSON is additionally stored as one document per session (WPF parity for reload),
with the tables above as the queryable projection. Every persisted result **must** carry
`conservation_verified = geometry_verified = true` or be quarantined (never exported,
never fed to production — WPF oracle rule).

## 12. Constraints & rules (examples enforced at DB + domain)

- All status columns: `CHECK (status IN (...))` + domain state machine; unknown legacy values
  from WPF migrate into a `status_text` overflow column that **passes through** unchanged.
- Fiscal documents: no hard delete — `is_void` + void audit columns; delete permission maps
  to void for posted docs (WPF G-6 rule).
- `stock_transactions` is the ONLY writer of `glass_sheets.stock/used` (enforced in service
  layer within one transaction; trigger optional).
- Optimistic concurrency: EF Core `row_version` (xmin) on documents; 409 on conflict (§52).

## 13. Index & performance strategy

- Composite indexes lead with `tenant_id`, then the module's hot filter
  (status, issue_date, customer_id, proforma_no…).
- Full-text search: PostgreSQL `tsvector` on inquiry/proforma/job/delivery/tax-invoice
  number + customer + project (§54 server-side search); trigram index for partial numbers.
- JSONB GIN indexes on `params_json` only where queried (optimization session lookup).
- Pagination mandatory on list endpoints; server-side aggregation for dashboards/reports.
- Partition-ready: all large tables (`audit_logs`, `stock_transactions`, `optimization_*`)
  are natural range/partition candidates by created_at or tenant_id hash at 1k+ tenants.

## 14. Migration mapping (SQLite → PostgreSQL) summary

| WPF source | SaaS target | Notes |
|---|---|---|
| `proglass.db` GlassSheets/SheetHistories/InventoryItems | glass_sheets + stock_transactions | history rows → transactions; balances recomputed & compared |
| `proformainvoices.db` (metadata + FullSnapshotJson) | proforma_invoices + spec_groups/items/other_charges/wastage_rows + snapshot_json | totals re-verified by recomputation; 1-fil tolerance |
| `specifications.db` | specifications | |
| `inquiries.db` | inquiries | |
| `joborders.db` / `production.db` | job_orders, production_pieces/events, material_reservations | |
| `deliverynotes.db` | delivery_notes/items | |
| `taxinvoices.db` / `creditnotes.db` | tax_invoices / credit_notes | |
| `statements.db` | statements/statement_lines | |
| `masterdata.db` | companies/clients/projects/salesmen | usage_count preserved |
| `optimization.db` | optimization_* | sessions re-validated by oracles during import |
| `audit.db` Users/ActivityLog | users (hash reset) + audit_logs | local DateTimes → UTC |
| attachments on disk | object storage | keys generated; original names kept in DB |

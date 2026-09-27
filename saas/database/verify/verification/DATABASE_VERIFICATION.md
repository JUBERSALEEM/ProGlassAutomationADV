# SAAS DATABASE VERIFICATION REPORT (Phases 3–4)

**Generated:** 2026-09-27T08:56:25.635546+00:00  
**PostgreSQL:** 16 (pgserver, live instance)  
**Migrations applied:** 0001_platform_identity.sql, 0002_masterdata_stock.sql, 0003_documents.sql, 0004_optimization.sql, 0005_rls.sql, 0006_refresh_token_absolute_expiry.sql, 0007_masterdata_parity.sql, 0008_specifications_parity.sql, 0009_other_charges_parity.sql, 0010_step9_pnl.sql, 0011_step7_summary.sql  
**Result: 59/59 checks passed, 0 failed**

Run it yourself: `python3 saas/database/verify/apply_and_test.py`
(requires `pip install pgserver psycopg2-binary`; or pass `--db-uri`).

| # | Check | Result | Detail |
|---|---|---|---|
| 1 | RLS visibility — all 36 tenant tables scoped to context tenant | ✅ PASS | 12 seeded with 1 row each, rest empty — none leak from tenant B |
| 2 | empty-string tenant context sees 0 rows (fails closed) | ✅ PASS | — |
| 3 | cross-tenant SELECT by id returns 0 rows | ✅ PASS | — |
| 4 | cross-tenant UPDATE affects 0 rows | ✅ PASS | — |
| 5 | cross-tenant DELETE affects 0 rows | ✅ PASS | — |
| 6 | cross-tenant INSERT rejected (WITH CHECK) | ✅ PASS | InsufficientPrivilege |
| 7 | own-tenant INSERT/DELETE works | ✅ PASS | — |
| 8 | unset tenant context sees 0 rows (fails closed) | ✅ PASS | — |
| 9 | tenant B sees only its own audit + optimization rows | ✅ PASS | — |
| 10 | duplicate proforma_no within tenant rejected | ✅ PASS | — |
| 11 | same proforma_no allowed in another tenant | ✅ PASS | — |
| 12 | invalid status rejected (CHECK) | ✅ PASS | — |
| 13 | FK to nonexistent proforma rejected | ✅ PASS | — |
| 14 | cascade delete removes spec groups + items | ✅ PASS | — |
| 15 | updated_at trigger advances on UPDATE | ✅ PASS | 2026-09-27 08:56:25.442094+00:00 → 2026-09-27 08:56:25.493106+00:00 |
| 16 | audit_logs UPDATE denied for app role | ✅ PASS | — |
| 17 | stock_transactions DELETE denied for app role | ✅ PASS | — |
| 18 | numeric money arithmetic exact | ✅ PASS | 1050.00 |
| 19 | re-run is a no-op (idempotent) | ✅ PASS | applied 0 |
| 20 | all 11 migrations recorded with checksums | ✅ PASS | 0001_platform_identity.sql, 0002_masterdata_stock.sql, 0003_documents.sql, 0004_optimization.sql, 0005_rls.sql, 0006_refresh_token_absolute_expiry.sql, 0007_masterdata_parity.sql, 0008_specifications_parity.sql, 0009_other_charges_parity.sql, 0010_step9_pnl.sql, 0011_step7_summary.sql |
| 21 | tampered migration detected | ✅ PASS | — |
| 22 | runtime role cannot INSERT tenants | ✅ PASS | — |
| 23 | runtime role can READ tenants | ✅ PASS | — |
| 24 | 0006: absolute_expires_at_utc exists NOT NULL | ✅ PASS | ('NO', None) |
| 25 | 0006: ix_refresh_tokens_token_hash exists | ✅ PASS | — |
| 26 | duplicate username within a tenant rejected | ✅ PASS | — |
| 27 | same username in another tenant allowed | ✅ PASS | — |
| 28 | refresh token insert without absolute deadline rejected (NOT NULL) | ✅ PASS | — |
| 29 | refresh token rows require the absolute deadline (app always sets it) | ✅ PASS | — |
| 30 | token_hash lookups use ix_refresh_tokens_token_hash | ✅ PASS | Bitmap Heap Scan on refresh_tokens  (cost=4.16..9.53 rows=1 width=16)   Recheck  |
| 31 | refresh token cross-tenant INSERT rejected (WITH CHECK) | ✅ PASS | — |
| 32 | 0007: companies logo columns exist NOT NULL with WPF defaults | ✅ PASS | {'logo_height_mm': ('26', 'NO'), 'logo_layout': ("'None'::text", 'NO'), 'logo_width_mm': ('22', 'NO')} |
| 33 | 0007: clients.document_prepared_by exists NOT NULL | ✅ PASS | ("''::text", 'NO') |
| 34 | company insert fills WPF branding defaults | ✅ PASS | None/22.00/26.00 |
| 35 | companies.logo_layout CHECK rejects unknown layouts | ✅ PASS | — |
| 36 | 0008: specifications catalogue columns exist (WPF 2026-09-02 set) | ✅ PASS | ['category', 'color', 'last_proforma_no', 'panes_json', 'sheet_height_mm', 'sheet_id', 'sheet_width_mm', 'thickness_mm'] |
| 37 | 0008: usage_count default is 0 (a new spec has never been used) | ✅ PASS | ('0',) |
| 38 | 0008: natural key (tenant_id, glass_type, spec_name) enforced | ✅ PASS | — |
| 39 | 0008: specification_usages exists with RLS enabled | ✅ PASS | — |
| 40 | 0008: new spec starts at usage_count 0 (not 1) | ✅ PASS | 0 |
| 41 | 0008: duplicate (tenant, glass_type, spec_name) rejected | ✅ PASS | — |
| 42 | 0008: same spec name allowed in another tenant | ✅ PASS | — |
| 43 | 0008: usage pair UNIQUE (spec, proforma) — one verified use per pair | ✅ PASS | — |
| 44 | 0009: other_charges row contract columns exist | ✅ PASS | ['custom_polish_description', 'custom_polish_qty', 'description', 'metric_qty', 'polish_sub_type', 'template_key', 'unit'] |
| 45 | 0009: wastage_rows contract columns (Phase-3 guesses dropped) | ✅ PASS | ['checked_source_keys', 'is_manually_added', 'total_sqm', 'use_optimization_formula'] |
| 46 | 0009: custom_charges table exists (CustomChargeRow) | ✅ PASS | ['amount', 'charge_name', 'is_active', 'qty', 'rate', 'sort_order', 'unit'] |
| 47 | 0009: spec_group_items.perimeter_factor exists (default 2, WPF GlassItemRow) | ✅ PASS | — |
| 48 | 0009: spec_groups Step 4 provenance columns exist (SpecGroupSnapshot Phase G) | ✅ PASS | — |
| 49 | 0009: real metric key LM1 accepted | ✅ PASS | — |
| 50 | 0009: Phase-3 metric key 'SQM' rejected (real registry is LM1/SQM1/...) | ✅ PASS | — |
| 51 | 0009: custom_charges readable under tenant A RLS | ✅ PASS | — |
| 52 | 0010: spec_groups Step 4 production provenance columns (nullable — Phase 1-AUDIT contract) | ✅ PASS | ['step4_cutting_cost_per_sqm', 'step4_interlayer_cost_per_sqm', 'step4_spacer_price_per_sqm', 'step4_tempering_cost_per_sqm', 'u_insert_price_per_sqm'] |
| 53 | 0010: provenance columns are NULLABLE (legacy groups read unknown, not zero-priced) | ✅ PASS | — |
| 54 | 0010: invoice P&L operator inputs (15% wastage, cost-at-selling default) + KPI columns exist | ✅ PASS | — |
| 55 | 0010: pnl_rows exists with RLS tenant isolation | ✅ PASS | — |
| 56 | 0011: the four mapper-persisted operator inputs exist | ✅ PASS | ['discount_type', 'discount_value', 'is_vat_applicable', 'vat_percent'] |
| 57 | 0011: WPF defaults (Percentage / 0 / VAT on / 5%) | ✅ PASS | {'discount_type': "'Percentage'::text", 'discount_value': '0', 'is_vat_applicable': 'true', 'vat_percent': '5.00'} |
| 58 | 0011: discount type vocabulary + non-negative value + 0-100 VAT percent enforced | ✅ PASS | ['ck_proforma_discount_type', 'ck_proforma_discount_value', 'ck_proforma_vat_percent'] |
| 59 | 0011: a negative discount value is rejected | ✅ PASS | CheckViolation |

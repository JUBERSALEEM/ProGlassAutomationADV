# PHASE 3 DATABASE VERIFICATION REPORT

**Generated:** 2026-09-27T05:35:45.778761+00:00  
**PostgreSQL:** 16 (pgserver, live instance)  
**Migrations applied:** 0001_platform_identity.sql, 0002_masterdata_stock.sql, 0003_documents.sql, 0004_optimization.sql, 0005_rls.sql  
**Result: 23/23 checks passed, 0 failed**

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
| 15 | updated_at trigger advances on UPDATE | ✅ PASS | 2026-09-27 05:35:45.709868+00:00 → 2026-09-27 05:35:45.760573+00:00 |
| 16 | audit_logs UPDATE denied for app role | ✅ PASS | — |
| 17 | stock_transactions DELETE denied for app role | ✅ PASS | — |
| 18 | numeric money arithmetic exact | ✅ PASS | 1050.00 |
| 19 | re-run is a no-op (idempotent) | ✅ PASS | applied 0 |
| 20 | all 5 migrations recorded with checksums | ✅ PASS | 0001_platform_identity.sql, 0002_masterdata_stock.sql, 0003_documents.sql, 0004_optimization.sql, 0005_rls.sql |
| 21 | tampered migration detected | ✅ PASS | — |
| 22 | runtime role cannot INSERT tenants | ✅ PASS | — |
| 23 | runtime role can READ tenants | ✅ PASS | — |

# ProGlass SaaS — hardening audit

Generated: 2026-09-27T15:04:42.022052
Result: **14/14 passed**

| Check | Result | Detail |
|---|---|---|
| every tenant table has RLS enabled | ✅ PASS | 41 tables |
| FORCE ROW LEVEL SECURITY on tenant tables | ✅ PASS | all forced |
| tenant_isolation policy exists per tenant table | ✅ PASS | 41 policies |
| runtime role cannot write platform tables | ✅ PASS | tenants/plans/features read-only |
| runtime role is not privileged | ✅ PASS | superuser/createdb/createrole = (False, False, False) |
| tenant_id FK → saas.tenants on every tenant table | ✅ PASS | 41 FKs |
| no orphaned tenant rows | ✅ PASS | clean |
| fiscal document numbers unique per tenant | ✅ PASS | credit_notes:UNIQUE (tenant_id, credit_note_no); statements:UNIQUE (tenant_id, statement_no); tax_invoices:UNIQUE (tenant_id, invoice_no) |
| CHECK constraints present (vocabularies DB-enforced) | ✅ PASS | 53 CHECK constraints |
| every FK's leading column is indexed | ✅ PASS | all covered |
| all password hashes are Argon2id PHC strings | ✅ PASS | ok |
| statement line ordinals unique per statement | ✅ PASS | 0 duplicate(s) |
| no zero-delta or negative ledger rows | ✅ PASS | 0 bad row(s) |
| ledger tail matches stock−used | ✅ PASS | all match |

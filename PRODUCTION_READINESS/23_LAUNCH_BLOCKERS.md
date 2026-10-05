# Launch blockers

| ID | Severity | Status | Evidence |
|---|---|---|---|
| LB-1 | P0 | BLOCKED | Release build not run. No .NET SDK in this environment. Microsoft CDN TLS failed. |
| LB-2 | P0 | UNVERIFIED | No automated test run. Test projects exist; they were not executed. |
| LB-3 | P0 | UNVERIFIED | No runtime pass of create/save/reopen for PI, job, delivery, tax invoice, sheet store, or payments. |
| LB-4 | P1 | PARTIAL | Print preview `Print.*` keys were missing and are now defined. Visual result on Windows is UNVERIFIED. |
| LB-5 | P1 | UNVERIFIED | Repository `Initialize` methods ignore `SchemaMigrationRunner.Upgrade() == false`. Failure is logged. Customer-visible recovery is not proven. |
| LB-6 | P1 | UNVERIFIED | Optimizer conservation checks exist in `GuillotinePacker`. Large-job runs (1k–50k) were not executed. |
| LB-7 | P2 | FIXED-STATIC | Blank `Settings.Reset` label, missing `CardBorderBrush`, missing About warning/badge brushes, broken chart tint keys. Runtime look UNVERIFIED. |

Public release remains blocked by LB-1, LB-2, and LB-3 regardless of static fixes.

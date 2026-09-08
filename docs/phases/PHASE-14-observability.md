# Phase 14 — Observability and Audit Completeness

**Severity: Moderate · Estimate: 3 days · Prerequisite: green build**

## What already exists, and is good

Four exception sinks, each with a documented reason for existing:

- `DispatcherUnhandledException` — UI thread, with `PermissionDeniedException`
  correctly excluded so an authorization denial is not reported as a crash
- `AppDomain.CurrentDomain.UnhandledException` — background threads
- `SessionEnding` — Windows shutdown, which `MainWindow.Closing` never sees
- `TaskScheduler.UnobservedTaskException` — fire-and-forget failures

Plus `WriteCrashReport`, `ForceExitAfterCrash` with a watchdog, correlation IDs
via `CorrelationContext`, and `AsyncUtility.SafeFireAndForget` as the single
sanctioned `async void`.

## Gaps

### 14.1 — Silent degradation is not observable

Three places log a problem and continue:

- DI container build failure → `Warning`, app continues with a partial
  composition root (Phase 7)
- `SqliteResilience` pragma failure → `Warning`, connection used unhardened,
  which means **WAL and foreign-key enforcement may be silently off**
- `TaxInvoiceDnReconciler` repairs → `Info`, divergence healed with no signal
  that it happened

Each is individually defensible. Together they mean an installation can run for
months with foreign keys unenforced and nobody knows. **Surface these in the
About / health page**, not only in the log. A desktop ERP has no monitoring
stack — the UI is the monitoring stack.

### 14.2 — No health summary

`Services/Database/SchemaHealthService.cs` exists. Wire it, plus the pragma
state, plus the reconciler's repair count, plus the license status, into one
health panel. An operator or a support engineer should be able to answer "is
this installation healthy?" in one screen.

### 14.3 — Audit completeness

`AuditService` is thorough. Two things to verify rather than assume:

- Does **every** write path audit? Enumerate the repository write methods and
  cross-check against audit call sites. A write that does not audit is invisible
  after the fact.
- Are **read** paths for sensitive data audited? For a system holding customer
  pricing, "who looked at this quote" is often as important as who changed it.

### 14.4 — Log volume and retention

`AppLogService` writes to `logs\`. Confirm rotation and a retention cap exist.
An ERP left running for months on a small disk will fill it, and the failure
mode is that logging itself starts throwing — which several catch blocks here
already anticipate.

## Acceptance criteria

- [ ] A single health panel reports database hardening state, schema health,
      reconciliation repairs, license status and container state.
- [ ] Every silent-degradation path is visible somewhere other than the log.
- [ ] Write-path audit coverage enumerated and gaps closed.
- [ ] Log rotation and retention confirmed and documented.

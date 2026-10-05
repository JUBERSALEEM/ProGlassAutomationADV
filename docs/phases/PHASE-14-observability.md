# Phase 14 — Observability and Audit Completeness

**Severity: Moderate · Estimate: 3 days · Prerequisite: green build**

## Status — 14.1 APPLIED 2026-09-09, 14.2-14.4 open

| | |
|---|---|
| 14.1 DI container failure logged at Warning | **fixed** → `Error`, with the exception and an ErrorId |
| 14.1 pragma failure logged at Warning | **fixed** → `Error`, with the exception |
| 14.1 reconciler repair logged at Info | **fixed** → `Warning` |
| 14.1 surfaced in the About / health page | **not started** |
| 14.2 health summary panel | **not started** |
| 14.3 audit completeness | **not started** |
| 14.4 log volume and retention | **not started** |

### A correction this phase had to make first

Phase 7 recorded a claim that `AppLogService` has no Error level — only `Debug`,
`Info` and `Warning`. **That was false.** `Error` is at line 75 and `Critical` at
line 84. Both return a `string` ErrorId rather than `void`, and the grep that
produced the claim matched only `public static void`, so it missed them and
reported an absence that did not exist.

Usage across the codebase, counted properly:

| | call sites |
|---|---|
| `Debug` | 573 |
| `Warning` | 363 |
| `Error` | 103 |
| `Info` | 64 |
| `Critical` | 5 |

The lesson is the same one this repository keeps teaching: a grep pattern that
encodes an assumption (`void`) silently filters out the answer. The Phase 7 note
has been corrected in place.

### What changed, and why these three levels

**Pragma failure → `Error`.** This is the one silent degradation in 14.1 that
changes *data integrity* rather than performance. If `foreign_keys` did not take,
referential integrity is unenforced for every write on that connection, and
nothing else in the application will ever say so. `Warning` is what this codebase
reserves for things that recovered; this one continued unhardened. The exception
object is now passed as well, so the ErrorId carries a stack rather than only
`ex.Message`.

**DI container failure → `Error`.** A container that will not build means the
application is running in a composition it was never tested in. The message also
stops interpolating the whole exception into the text now that the exception
object is attached properly.

**Reconciler repair → `Warning`.** Reaching that line means tax-invoice and
delivery-note records had already diverged and something healed them — normally a
crash or a failed write. `Info` reads as routine housekeeping and gets filtered
out of every report. The sweep *summary* line stays at `Info`, which is right: it
fires on a clean startup too, when nothing was repaired.

### What remains

The log-level changes make these events findable. They do not make them
*visible*. 14.1's actual ask — surface the degraded state in the About / health
page — and 14.2's health panel are both UI work. A desktop ERP has no monitoring
stack; the UI is the monitoring stack, and that is not something to build blind.
`SchemaHealthService` already exists and is the right thing to wire it to.


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

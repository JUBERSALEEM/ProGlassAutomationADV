# Phase 7 — Logging, diagnostics and incident response

**Status:** Operational control requirements documented. Existing logging/crash-report infrastructure still needs an end-to-end fault-injection review.

## Current implementation areas to verify

- `Services/Logging/AppLogService.cs`: durable log levels, rotation, failure behavior and structured context.
- `App.xaml.cs`: fatal startup handling and crash-report output.
- `Services/Common/CorrelationContext.cs`: correlation IDs propagated through multi-step operations.
- `Services/Audit`: business/security audit events, separate from diagnostic logs.
- `Services/Updates` and `Services/Database/DatabaseBackup`: updater and backup failure telemetry.

## Required controls

1. Every failed money-path operation must produce a durable diagnostic with module, operation, correlation ID and exception details, while the user receives a safe actionable message.
2. Authorization denials, login/session boundaries, licensing decisions, update signature failures, backup/restore outcomes and database migration failures must be auditable.
3. Logs must not contain passwords, license secrets, bearer tokens, private keys, full payment credentials or unnecessary customer data. Exception text and remote payloads must be reviewed before logging.
4. Log write failure must not hide the original error; it should fall back safely and never falsely report a successful operation.
5. Log and crash-report storage needs bounded retention, restrictive local permissions and documented export/removal steps.
6. Support bundles must be opt-in, previewable, redacted and written to a user-selected location. Never upload logs automatically without explicit consent.
7. Correlation IDs must survive a complete business operation: Inquiry → PI → Job Order → Delivery → Tax Invoice → Statement.

## Fault-injection matrix

- Database file locked, read-only, missing, corrupt, or inaccessible.
- Migration failure and schema mismatch.
- Disk full / access denied while writing logs, crash reports or backups.
- Network loss during update check/download and shared-root operations.
- Invalid or untrusted installer signature.
- Permission denial and license expiry.
- Unhandled UI-thread and background-task exceptions.

For each case verify: user-facing message, durable log event, correlation ID, no secret leakage, no partial business write, and a documented recovery action.

## Exit criteria

- [ ] Automated tests cover critical logging paths and log redaction.
- [ ] Release-build fault-injection results are recorded.
- [ ] A non-developer can locate and export diagnostics without exposing secrets.
- [ ] Retention and local file permissions are verified on a clean installed machine.

**Do not claim this phase complete from code inspection alone.**

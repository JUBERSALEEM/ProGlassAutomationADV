# Professional Audit Trail Upgrade — 2026-09-07

## Senior audit findings
The application already had an `ActivityLog` table, before/after JSON support, session ids, record metadata and version history. The weak point was presentation and context completeness:

1. Module Activity tabs did not read like a professional audit report.
2. Rows focused on time/action/details but did not clearly answer **who, when, what, why, which record, which session/workstation**.
3. Old/simple events without field diffs were not clearly distinguished from structured before/after audit events.
4. A reviewer could not quickly tell whether an entry came from the current user or another user/session.
5. Popup activity feed was too lightweight for management/audit review.

## Implementation
### Persisted audit context
New audit columns are added through an idempotent migration and populated automatically by `AuditService`:

- `UserDisplayName`
- `UserRole`
- `MachineName`
- `AppVersion`
- `LocalTimeZone`
- `OperationId`

Indexes were added for entity lookup and operation lookup.

### Professional Activity row model
`ActivityRowViewModel` now exposes a complete audit narrative:

- Local and UTC time
- Display user, login and role
- Current-user vs other-user/session notice
- Module/section
- Entity type and entity id
- Action and details
- Human reason / WHY
- Before/after change count
- Session, operation id, workstation, app version and timezone
- Copy-ready `AuditReportText`

### Professional UI
`SectionActivityView` was redesigned into a proper audit trail table/card hybrid:

- WHEN column: local and UTC timestamps
- WHO column: display name, login, role and other-user warning
- WHAT/RECORD column: action badge and record id
- DETAILS/WHY/BEFORE-AFTER column: narrative, reason and change summary
- REPORT column: complete report dialog and diff button when available

### Complete audit report dialog
Added `AuditReportDialog`:

- WHO / WHEN / WHAT / WHY executive cards
- Complete copy-ready audit narrative
- Before/after field comparison list when structured changes exist
- Explicit message when a legacy/simple event has no field-level diff
- Copy report button for management/support review

### Activity popup
The top activity popup now shows actor + role, action, details, record id and session id instead of only a lightweight section/action line.

## Compatibility
- Existing rows remain readable; new fields default to empty on old databases.
- Existing `Log(...)` and `LogWithChanges(...)` callers continue to work.
- New context is automatic; modules do not need to manually pass workstation/version/session data.
- Structured before/after remains available where modules already call `LogWithChanges(...)`; simple legacy rows are clearly labelled as not having field-level diff.

## Validation
- XAML parse was run for all views.
- C# brace-balance and whitespace checks were run for changed files.
- Full .NET build could not be executed in the sandbox because `dotnet` is not installed.

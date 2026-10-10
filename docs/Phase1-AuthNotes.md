# Phase 1 — Authentication and authorization notes

**Prepared:** 2026-10-10<br>
**Scope:** AUTH-001 and the service-layer authorization part of AUTHZ-001.<br>
**Verification:** source reviewed; tests added but not run because `dotnet` is unavailable in this environment. No Windows/WPF run was performed.

## AUTH-001 implementation

### Canonical roles

- `Administrator` is the canonical administrator role. `AppRoles.Normalize` maps legacy `Admin` (in any casing) to `Administrator`; permission checks and last-administrator checks use that canonical meaning.
- The audit-database migration rewrites persisted `Admin` spellings to `Administrator`. The compatibility alias remains in role resolution so a legacy backup still fails safely into the expected administrator permissions.
- Role assignment normalizes known names before saving. Unknown roles do not gain permissions; the role map returns an empty set for them.
- The legacy `Operator` role continues to map to the Store permission set.

### First-user bootstrap fails closed

- The first-run UI calls `BootstrapFirstAdministratorAsync`, not the normal account-creation method. Normal account creation requires an already authenticated Administrator and refuses to create the first account.
- Bootstrap first requires a ready migrated user database. Its insert is conditional on `NOT EXISTS (SELECT 1 FROM AppUsers)`, so concurrent first-run windows cannot both create the initial account. The created role is always canonical `Administrator`.
- `HasAnyUsersAsync` returns “users exist” if the database cannot be read or migrations are not ready. It therefore does not expose the bootstrap UI on an uncertain/error state.
- Migration failure clears readiness and leaves sign-in/bootstrap disabled. A normalization collision is reported and stops readiness; no account is silently chosen or renamed.

### Last active Administrator protection

- Disabling an account and changing a role are performed in a SQLite serializable write transaction. The service computes the proposed state from the transaction’s user rows and refuses any change that would leave no active canonical Administrator.
- The check treats legacy `Admin` as Administrator. It permits a demotion/deactivation only if another active Administrator remains.
- `DeleteUserAsync` retains account rows for audit identity/history and refuses physical deletion. It also specifically reports that the last active Administrator cannot be removed. Deactivation is the supported offboarding operation.

### Username identity and database uniqueness

`UsernameNormalizer.Normalize` is the shared identity-key function used at account creation and login:

1. trim leading/trailing whitespace;
2. normalize Unicode to Form KC;
3. apply invariant uppercase.

The original username casing remains available for display. `AppUsers.UsernameNormalized` stores the canonical key and has a unique SQLite index (`IX_AppUsers_UsernameNormalized`). The migration backfills that field in .NET using the same normalizer, detects duplicate canonical keys before index creation, and aborts readiness with the conflicting usernames if it finds a collision. Application-side duplicate checks are for a clearer message; the unique index is the concurrency-safe enforcement boundary.

Sign-in, password changes/resets, lockout lookups, and default-password checks query the normalized key. This avoids paths disagreeing about whether `Admin`, `admin`, whitespace variants, or compatibility Unicode forms identify the same account.

## AUTHZ-001 implementation

- `OperationGuard.Demand` is the headless repository/service boundary. It requires a non-null active `AuditService.CurrentUser` and the role’s required permission; denial is audited and throws `PermissionDeniedException`.
- Mutating document, Inquiry, and Sheet Store repository methods now include the guard. `ReadOnlyGate.Demand` remains a separate writable-license check at mutation boundaries.
- Production writes use `ProductionWriteGuard` (Production.Edit plus ReadOnlyGate) at public workflow entry points and again in `ProductionDbContext.SaveChanges`/`SaveChangesAsync`, preventing direct context callers from skipping authorization.
- TI-owned Delivery Note invoice-link maintenance has its own `DeliveryNote.InvoiceLink` permission. It is not the broader DeliveryNote body-edit permission: Accountant can manage finance links without gaining DN body-edit rights, while Production can edit permitted DN fields but cannot write invoice links. Manager and Administrator also have the link permission.
- The reconciler is scheduled after a signed-in identity is established, not during pre-login startup. Any link repair still passes through the repository guard; an account without the finance-link permission gets an explicit failed reconciliation result/log rather than an internal bypass.

## Tests added or expanded (not run)

- `Tests/Audit/AuthNormalizationTests.cs`: normalizer examples, canonical Admin role, last-admin policy, and source-level checks for normalized-key DB uniqueness, conditional bootstrap, login normalization, and retained account deletion.
- `Tests/Agent/OperationGuardTests.cs`: no-user denial, unknown-role denial, and role mapping.
- `Tests/Audit/OperationGuardCoverageTests.cs`: source scan that pins service-layer `OperationGuard.Demand` coverage for configured mutating repositories.
- `Tests/Agent/ProductionWriteGuardTests.cs`: production guard and DbContext commit overload coverage, plus public workflow entry-point checks.
- `Tests/Audit/TaxInvoiceDnLinkPermissionTests.cs`: role-level tests pinning the narrow TI-owned DN-link permission.

## Open decision / operational note

The active-user object is held in process memory. Deactivation or role change in a different running client prevents later sign-ins but does not currently push an immediate revocation event into every already-running client. The next phase should decide whether to add cross-client session revocation or revalidate account activity/role on write. Current write guards still require a signed-in active session and check its current role snapshot.

A migration collision intentionally blocks authentication rather than guessing. Operators will need a documented/manual account-resolution procedure before retrying migration on such a database.

## Key files touched directly for account identity/auth

This focused list covers account identity and authorization infrastructure; the broader repository/UI inventory is in `docs/Phase1-Report.md`.

- `Models/Audit/AppUser.cs`
- `Services/Audit/AuditDbContext.cs`
- `Services/Audit/AuditDbMigrations.cs`
- `Services/Audit/AuditService.Part1.cs`
- `Services/Audit/AuditService.Part2.cs`
- `Services/Audit/Permissions.cs`
- `Services/Audit/UsernameNormalizer.cs`
- `Services/Audit/UserAdministrationPolicy.cs`
- `Services/Production/ProductionWriteGuard.cs`
- `Services/Production/ProductionDbContext.cs`
- `Services/Production/ProductionReleaseService.cs`
- `Services/Production/ProductionRepository.cs`
- `Services/SheetStore/InventoryItemRepository.cs`
- `Services/SheetStore/SheetStoreRepository.cs`
- `Services/SheetStore/SheetStoreHistory/SheetHistoryRepository.cs`
- `Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs`
- `Tests/Audit/AuthNormalizationTests.cs`
- `Tests/Audit/OperationGuardCoverageTests.cs`
- `Tests/Audit/TaxInvoiceDnLinkPermissionTests.cs`
- `Tests/Agent/OperationGuardTests.cs`
- `Tests/Agent/ProductionWriteGuardTests.cs`
- `Views/Login/FirstRunSetupPanel.xaml.cs`
- `ViewModels/UserCenter/UserCenterViewModel.cs`
- `App.AuthHandlers.cs`
- `App.StartupCore.cs`

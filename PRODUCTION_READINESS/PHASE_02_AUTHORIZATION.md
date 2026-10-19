# Phase 2 — Identity, authorization and licensing hardening

**Status:** Control map and release gates recorded; end-to-end enforcement still requires Windows test evidence.

## Existing controls observed in the audited branch

- `Services/Audit/Permissions.cs` defines role names and granular permission names.
- `Services/Audit/OperationGuard.cs` provides a headless service/repository permission gate and audits denials.
- `Tests/Audit/OperationGuardCoverageTests.cs` uses a source-level guard to require permission checks in configured mutating repository methods.
- `Services/Security/AuthSessionService.cs` clears saved auto-login credentials on sign-out and writes a session boundary.
- `Services/Security/SecretVault.cs` uses Windows DPAPI with CurrentUser scope for persisted secret values.
- `Services/Licensing/LicenseGate.cs` gates navigation by feature. Its own comments state that it does not block opening an already loaded view or gate every per-command operation.

These are useful controls, but their presence alone does not prove complete coverage.

## Required remediation and verification

| ID | Required action | Acceptance evidence |
|---|---|---|
| AUTH-01 | Enumerate every mutating service, repository, background task and AI/tool entry point; map each to its exact permission | Generated/maintained inventory and reviewer sign-off |
| AUTH-02 | Verify all mutating methods deny no-session, unknown-role and insufficient-permission calls | Negative tests for every permission family; no UI-only enforcement |
| AUTH-03 | Review `OperationGuard.Enabled` and all test-only bypasses; prove no production configuration or runtime input can disable authorization | Search results + Release test proving enforcement cannot be disabled by user-controlled state |
| AUTH-04 | Audit all navigation and command paths for license feature checks; ensure an already loaded view cannot perform a gated action | Per-command denial tests, not navigation-only tests |
| AUTH-05 | Verify password hashing parameters, salts, migration behavior, lockout/rate limits, and credential reset flows | Security tests plus documented policy |
| AUTH-06 | Verify DPAPI vault ACLs, atomic writes, corruption recovery, backup handling and log redaction | Tests under a non-admin Windows user; no secret values in logs |
| AUTH-07 | Review session handoff on shared terminals, saved auto-login revocation, sign-out cleanup and stale view-model state | Multi-user session-switch tests |
| LIC-01 | Test malformed, altered, expired, wrong-machine and wrong-edition license payloads, including clock rollback and offline cases | Licensing contract suite and explicit policy for each case |

## Updater publisher identity is a release blocker

The classic updater calls `UpdateSignatureVerifier.Verify(path)` without a publisher pin. The verifier accepts any trusted code-signing certificate when no pin is supplied. The publisher identity/thumbprint must come from the real release certificate; do not guess it or copy an arbitrary certificate identity.

Before enabling a signed release:
1. Obtain the real publisher certificate and approved thumbprint(s).
2. Define certificate rotation/overlap and revocation procedures.
3. Store signing private keys only in a secure signing service or protected CI secret, never in source control.
4. Pin the intended publisher in every updater verification path.
5. Verify a correctly signed package is accepted and unsigned, tampered, wrong-publisher and expired-signature packages are rejected.

## Phase exit criteria

- [ ] Complete permission inventory, including service methods outside the configured repository test list.
- [ ] Negative authorization tests pass in Release configuration.
- [ ] No production bypass for authorization or licensing remains.
- [ ] Shared-terminal sign-out and credential clearing tests pass.
- [ ] Publisher identity is configured and signed update verification is proven end-to-end.

**Do not mark Phase 2 complete until every unchecked criterion has evidence.**

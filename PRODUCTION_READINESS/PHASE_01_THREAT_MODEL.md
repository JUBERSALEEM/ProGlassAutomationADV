# Phase 1 — Security baseline and threat model

**Status:** Baseline documented; technical verification incomplete.  
**Audited base:** `e510d760fefa7cb0b6d65febd10cc2d502f6fe82`  
**Working branch:** `fix/production-readiness-2026-10-19`  
**Application:** ProGlass Automation ERP, WPF / .NET 8 / SQLite.

This document is a control register, not a security certification. Static source inspection cannot establish that the shipped executable is secure. All items marked **Verify** require a test result or owner evidence before release.

## Assets to protect

1. Financial records: proforma invoices, tax invoices, credit notes, payments, statements, tax values and document links.
2. Operational records: sheet stock, job orders, delivery notes, production and inquiry data.
3. Local database files, SQLite WAL/SHM sidecars, backup sets, attachments and exported files.
4. User identities, role assignments, license payloads, API credentials and DPAPI-protected secrets.
5. Application binaries, updater packages, installer metadata, signing identity and release credentials.
6. Audit records, diagnostic logs and crash reports, including any customer or commercial data they may contain.

## Trust boundaries and threats

| Boundary | Threat | Required control / evidence |
|---|---|---|
| Windows user → application | Unauthorized access on a shared shop-floor terminal | Session lock/sign-out, idle policy decision, role checks at service boundary, tests for no-session and role denial |
| ViewModel → service/repository | UI-only permission checks bypassed by another caller | Central authorization checks on every mutation; inventory and negative tests |
| App → SQLite | Corrupt schema, interrupted writes, concurrency, tampering or partial document updates | Transactions, constraints, fail-closed migrations, concurrent-write tests, integrity checks |
| App → local files | Path traversal, unsafe attachment type, overwrite, unintended disclosure | Canonical path validation, allowlisted roots, attachment size/type rules, least-privilege ACLs |
| App → updater/release | Compromised release asset or untrusted signer | Valid Authenticode signature, pinned publisher identity, secure key storage, signed-package CI gate |
| Build → dependencies | Vulnerable or substituted NuGet package / compromised build | Restore lock strategy, dependency vulnerability scan, minimal CI permissions, immutable release provenance |
| App → logs/export | Secret, credential, or customer data leakage | Redaction, log review, export tests, restricted log/backup access |
| Backup → restore | Inconsistent snapshot or untested recovery | SQLite-consistent backup mechanism and isolated restore drills with evidence |

## Current known blockers from source review

- Release workflow documentation states that generated packages are **unsigned**. Production releases must remain blocked until signing is configured with the real publisher certificate and tested end-to-end.
- The classic updater calls `UpdateSignatureVerifier.Verify(path)` without publisher pins. The verifier documents that empty pins accept any otherwise trusted code-signing certificate. A real publisher pin cannot be invented; configure it after the publisher identity is confirmed.
- Main database migration failure is logged and startup continues. Decide and implement an explicit fail-closed startup policy, with a recoverable operator message and tests.
- Backup code copies SQLite files and sidecars as separate files. A consistent snapshot under concurrent writes has not been proven; add an SQLite-supported snapshot/backup mechanism and concurrency tests.
- The restore drill must be verified to operate only on an isolated scratch data root and must not create a backup set under or mutate the live data root.
- CI files are present under `.github/workflows/`, but a green Windows build/test run has not been evidenced in this audit.
- UI interaction, persistence, clean-machine installation and upgrade behavior remain unverified.

## Phase 1 acceptance checklist

- [x] Audited base commit and working branch recorded.
- [x] Critical data assets and trust boundaries listed.
- [x] Known release blockers recorded without implying they are fixed.
- [ ] Complete source inventory with every mutation entry point and permission mapping.
- [ ] Threat review approved by project owner.
- [ ] Windows Release build and complete test results captured for the exact candidate commit.
- [ ] No unresolved Critical/High security finding before production release.

## Release rule

Do not describe the application as fully secure or production-approved until all Critical/High findings are closed or formally accepted, the required CI gates pass on the exact candidate commit, and installation/update/recovery tests have evidence. Never place signing certificates, private keys, tokens or production customer data in the repository.

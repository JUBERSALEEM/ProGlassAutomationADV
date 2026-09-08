# Phase 9 — Security Hardening

**Severity: Moderate · Estimate: 2 days · Prerequisite: green build**

## What is already right

`Services/Security/PasswordHasher.cs` is genuinely good work: PBKDF2-HMAC-SHA256
at 210,000 iterations (the OWASP figure), 16-byte random salt, a self-describing
stored format `PBKDF2$iterations$salt$hash` so the count can be raised later
without resetting passwords, transparent migration from the legacy
salted-SHA-256 scheme on next successful sign-in, and
`CryptographicOperations.FixedTimeEquals` for comparison.

Do not rewrite it. Tighten around it.

## Findings

### 9.1 — Password policy is thin

`PasswordHasher.MinLength = 8`. No complexity rule, no breached-password check.
For a system holding customer money and stock, eight characters is below current
guidance — OWASP recommends 12+ for user-chosen secrets, and specifically
recommends screening against a breached-password list over imposing composition
rules.

**Fix:** raise `MinLength` to 12 for new and changed passwords. Existing hashes
are unaffected by design, so this raises the bar only going forward. Add an
optional offline k-anonymity lookup (HaveIBeenPwned ranged API) as a Settings
toggle for sites with internet access — do not make it mandatory, because the
app is explicitly offline-first.

Also: the first-run wizard is the only path that creates the first admin. Make
sure it enforces the same policy, and that no default password is seeded
anywhere. Verify `AuditService.InitializeAsync` no longer seeds one — the
comment in `App.StartupCore.cs` says the `admin`/`admin` seed was removed.
Confirm it, and add a test asserting no seeded credential exists.

### 9.2 — Legacy comparison has a non-constant-time branch

`VerifyLegacySha256` returns:

```csharp
return CryptographicOperations.FixedTimeEquals(hash, storedBytes)
    || string.Equals(hex, storedHashHex, StringComparison.OrdinalIgnoreCase);
```

The second operand is an ordinary string comparison. The comment above it claims
the byte comparison "preserves that behaviour without the timing leak of a
string compare" — which the `||` then reintroduces.

Impact on a local desktop app is negligible, but the comment over-claims. Either
drop the string branch (the fixed-time one already covers case differences,
because it compares decoded bytes) or correct the comment.

### 9.3 — Audit database readability

The `PasswordHasher` header notes `audit.db` is "an ordinary file in %AppData%,
readable by any process running as that user, and it is included in every
backup set the app writes."

That is an accurate statement of the threat model and the hashing is the correct
defence. Two follow-ups worth doing:

- Ensure **backup sets** are not written to a world-readable location, and say
  where they go in the installer documentation.
- `Services/Security/SecretVault.cs` holds API keys for AI providers. Confirm it
  uses DPAPI at `CurrentUser` scope and that no key is ever written to the log —
  `AppLogService` is thorough, and thorough logging plus an unmasked secret is a
  common leak.

### 9.4 — No anti-debug or integrity checks

Recorded previously as acceptable for a desktop ERP.
`Services/Licensing/AssemblyIntegrity.cs` exists — read it and confirm what it
actually covers, then state the residual risk honestly in
`docs/CODE_RATING_REPORT.md` rather than leaving it implied.

## Acceptance criteria

- [ ] Minimum password length 12 for new and changed passwords.
- [ ] No seeded credential anywhere; a test asserts it.
- [ ] `VerifyLegacySha256` has no non-constant-time branch, or the comment is
      corrected.
- [ ] No secret reaches the log — verified by a test that greps the logging
      surface for known key names.
- [ ] Backup location and permissions documented.

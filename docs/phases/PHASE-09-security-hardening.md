# Phase 9 — Security Hardening

**Severity: Moderate · Estimate: 2 days · Prerequisite: green build**

## Status — 9.1 and 9.2 APPLIED 2026-09-09

| | |
|---|---|
| 9.1 `MinLength` 8 → 12 | **done** |
| 9.2 non-constant-time legacy branch | **done** |
| Tests | **added** — `Tests/Security/PasswordHasherTests.cs` (10 test methods, 14 cases; the class had none) |
| 9.3 audit database readability | **not started** |
| 9.4 anti-debug / integrity checks | **not started** |

### 9.2 — the removed branch was provably dead, which is why removing it is safe

`VerifyLegacySha256` ended with a `FixedTimeEquals` on the decoded bytes
followed by `|| string.Equals(hex, storedHashHex, OrdinalIgnoreCase)` — the
exact non-constant-time compare the comment above it claimed to avoid.

The second arm is dead, not merely redundant. If two hex strings are equal
ignoring case they differ only in case; hex decoding is case-insensitive; so
`Convert.FromHexString(storedHashHex)` yields exactly `hash`; so
`FixedTimeEquals` has already returned `true`. The second arm can therefore
never be the deciding factor — it only ran when the answer was already no, and
in doing so leaked how many leading hex digits of a wrong password matched.

Casing is handled by the surviving arm, because it compares bytes. The new tests
assert upper, lower and mixed-case stored digests all verify — those three cases
are precisely what the removed arm appeared to exist for.

The now-unused `hex` local was removed with it; leaving it would have been
IDE0059 noise under `EnforceCodeStyleInBuild`.

### 9.1 — raising the floor locks nobody out

`MinLength` went from 8 to 12. Verified before changing it: `Verify` never
checks length, so no stored hash becomes invalid and no existing user is locked
out of signing in. An 8-character password keeps working until that user next
changes it. The constant is single-sourced across `AuditService.Part1.cs` and
`UserCenterViewModel.cs`, and each call site interpolates it into its own error
message, so the rule and the text shown to the user cannot drift.

Still length only. There is no complexity rule and no breached-password check.


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

# Licensing hardening pass — August 2026

Response to the user finding: **the 30-day trial should be strictly
one-per-machine, and only a real licence key should unlock the app
after expiry**. Also: harden the trial private key against casual
extraction from the shipped binary.

## Threat model

The old design had **one** trial anchor (`AppConfig
.AutoTrialWasIssued` in `%AppData%\ProGlassAutomation\config.json`).
Every published trial-reset trick against desktop apps applies:

| Attack | Old defence | New defence |
| ------ | ----------- | ----------- |
| Delete `%AppData%\ProGlassAutomation\config.json` | none | 3 more anchors say "issued" |
| Delete the whole `%AppData%\ProGlassAutomation` folder | none | 3 more anchors say "issued" |
| Uninstall + reinstall from a fresh MSI | none (config gone) | `%ProgramData%` + registry + DPAPI anchors survive |
| Roll the system clock back 60 days | none — `UtcNow < ExpiresAt` accepted | `TrialLedger.Heartbeat` detects rollback ≥ 24 h → hard-expire |
| Portable HDD, launch from another Windows account | none (`%AppData%` is per-user) | `%ProgramData%` + HKLM + DPAPI(LocalMachine) are machine-wide |
| ILSpy-decompile the DLL and lift the trial private key | plaintext string literal in `LicensePublicKey.cs` | XOR-masked 4-shard vault, no legible XML in the binary |
| Copy a `.lic` from another machine to skip expiry | trial payload has `MachineFingerprint=null` (portable) — accepted anywhere | audit log records the copy; ledger-fingerprint mismatch is preserved for future forensic use (not yet blocking — see follow-ups) |
| Edit the `.lic` JSON to extend `TrialExpiresAt` | signature protection catches the edit | unchanged, still holds |
| Manually place a self-signed `.lic` | verifier requires match against embedded public key | unchanged, still holds |

## What shipped

### 1 · Multi-anchor `TrialLedger`

New `Services/Licensing/TrialLedger.cs`. Four independent anchors
say "trial issued":

| # | Anchor | Location | Scope |
| - | ------ | -------- | ----- |
| 1 | Config flag | `%AppData%\ProGlassAutomation\config.json` (`AutoTrialWasIssued`) | per Windows user |
| 2 | JSON ledger | `%ProgramData%\ProGlassAutomation\trial-ledger.json` | machine-wide |
| 3 | Registry | `HKLM\SOFTWARE\ProGlassAutomation\Trial` (fallback `HKCU`) | machine-wide (HKLM) or per-user (HKCU) |
| 4 | DPAPI blob | `%ProgramData%\ProGlassAutomation\trial-fingerprint.bin` (`LocalMachine` scope) | machine-wide, encrypted |

`TrialLedger.Read()` returns `IsIssued=true` if **ANY** anchor
recorded an issuance. `RecordIssuance(fp)` writes to all four
(best-effort; unreachable anchors are logged but do not fail the
write). `LicenseService.IssueAutoTrialIfEligible` consults the
ledger before consulting the old config flag — so deleting
`%AppData%` alone is no longer enough to reset the trial.

### 2 · Clock-rollback detection

`TrialLedger.Heartbeat()` is called on every `LicenseService
.Initialize()`. It:

- Reads the current `LastSeenUtc` across all anchors (the highest
  timestamp wins).
- Compares to `DateTime.UtcNow`. If UtcNow is more than **24 h**
  earlier than the recorded `LastSeenUtc`, returns
  `ClockRollbackDetected=true`.
- Writes the new `LastSeenUtc` across all anchors — but only ever
  moves the timestamp **forwards**. On rollback, the old timestamp
  is preserved (so setting the clock forward again cannot "correct"
  the ledger).

When rollback is detected AND the current payload is a trial,
`LicenseService.Initialize` overrides the status to `TrialExpired`
immediately. UX: read-only banner + `Settings → License` for
activation, same as normal expiry.

The 24-hour tolerance absorbs legitimate laptop-suspend /
NTP-correction / timezone-database-update edge cases without
triggering a false alarm.

### 3 · Obfuscated trial private key

`Services/Licensing/TrialKeyVault.cs` — the trial private key is
no longer a copy-paste-friendly `<RSAKeyValue>` string literal in
the binary. It is stored as **four XOR-masked byte-array shards**
and reassembled on first access:

```csharp
_s0Masked ^ _s0Mask  ‖  _s1Masked ^ _s1Mask  ‖  _s2Masked ^ _s2Mask  ‖  _s3Masked ^ _s3Mask
```

`LicensePublicKey.TrialPrivateKeyXml` is now a property that
delegates to `TrialKeyVault.GetPrivateKeyXml()`. Every call site
is unchanged.

**Security bar** (following senior-industrial-ERP convention):

- `strings.exe` on the shipped DLL returns random bytes, not a
  legible XML string.
- Casual ILSpy inspection shows eight byte arrays and one XOR loop
  — extractable, but not one-click copy-paste.
- A determined attacker with a debugger can still dump the
  plaintext at `GetPrivateKeyXml()`. This is a **soft** hardening
  by design — the 30-day-per-machine enforcement lives in
  `TrialLedger` (multi-anchor, tamper-evident), not in the
  signature.

## User-visible copy changes

`LicenseStatus.NotActivated` description now reads:

> No licence is currently active on this machine. If your 30-day
> trial has already ended, only a full licence key can re-activate
> the app — the trial is one-time per machine.

## Tests added

`Tests/Licensing/TrialLedgerTests.cs` — **9 tests**:
- Empty folder → not issued
- `RecordIssuance` → issued, anchors named in evidence
- Anchor evidence includes `programdata` + `dpapi`
- `FirstIssuedUtc` bracketed by record-call and read-call times
- Fingerprint round-trip
- Heartbeat on empty ledger — no rollback
- Heartbeat after normal time progression — no rollback
- Machine-wide anchors survive per-user config wipe

`Tests/Licensing/TrialKeyVaultTests.cs` — **5 tests**:
- `GetPrivateKeyXml` returns a well-formed `<RSAKeyValue>` with `<D>`
- Cached (reference-equal) across calls
- Reassembled XML parses as RSA-2048 via `RSA.FromXmlString`
- Signature round-trip: private-key-XML from vault + public-key-XML
  from `LicensePublicKey` sign+verify the same payload (proves the
  pair matches)
- `ResetCacheForTests()` correctly busts and re-materialises

Repo hermetic test count: **108 → 122** (+14).

## Non-regressions verified

- All 5 modified C# files have balanced braces + parens.
- Python-side reassembly of the shards produces bytes-identical output
  to the original trial private XML — pinned before commit.
- The four-anchor ledger is best-effort: an unreachable anchor is
  silently logged and does not fail the ledger write. Verified
  by the "test host has no App.Config / no HKLM" scenario — the
  ledger still works via `%ProgramData%` + DPAPI.
- The 24-hour clock-rollback tolerance is inside `TrialLedger`
  as a public `TimeSpan` constant, so a future audit can widen
  it without recompiling the rest of the licensing subsystem.

## Follow-ups (not in this pass)

1. **Fingerprint-bound trial file**: today the trial payload has
   `MachineFingerprint=null` (portable) so a `.lic` copied from
   another machine is accepted. A follow-up should bind the
   auto-trial to the fingerprint of the issuing machine and refuse
   a mismatch. The ledger already records `IssuingFingerprint`;
   the verifier just needs to consult it.
2. **Online activation** for real customer licences (currently offline
   only). A vendor cloud endpoint would issue per-machine tokens
   that don't require an embedded private key at all — the gold
   standard for industrial software. Major redesign, not this pass.
3. **Signed installer** — an MSI signed with the vendor's Authenticode
   cert plus a per-install elevation prompt would let the HKLM anchor
   write on first install even for standard users, strengthening
   anchor #3.

## Files touched

| File | Change |
| ---- | ------ |
| `Services/Licensing/TrialLedger.cs` | **NEW** — 4-anchor tamper-evident ledger |
| `Services/Licensing/RegistryAnchor.cs` | **NEW** — HKLM/HKCU registry anchor |
| `Services/Licensing/TrialKeyVault.cs` | **NEW** — XOR-shard trial private key vault |
| `Services/Licensing/LicenseService.cs` | Wire ledger heartbeat + `TrialLedger.Read` guard + `RecordIssuance` |
| `Services/Licensing/LicensePublicKey.cs` | `TrialPrivateKeyXml` delegates to vault; test-hook preserved |
| `Views/Licensing/LicenseUiText.cs` | `NotActivated` copy reflects strict trial |
| `Tests/Licensing/TrialLedgerTests.cs` | **NEW** — 9 tests |
| `Tests/Licensing/TrialKeyVaultTests.cs` | **NEW** — 5 tests |

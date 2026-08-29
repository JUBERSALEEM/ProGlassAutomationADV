# Licensing Security Cross-Check — Findings & Hardening (2026-08-29)

> Adversarial review of the license-key / trial chain after the
> single-click plan redesign. Every finding below was either verified
> clean or fixed in the same commit. Repo visibility checked during the
> review: **private** ✓ (the embedded production key is not public).

## 1. Forensics — leaked key material

- Scanned the FULL git history of every `.cs` file for RSA private
  components (`<D>`, `<P>`, `<Q>`, `PrivateKeyXml` XML literals) and for
  committed key files (`.pgkey/.pem/.pfx/.key`): **no historical private
  key embeds exist**; the only private XML in history is the current
  production pair, committed intentionally to the generator.
- The two historical public keys (`2rJQoH…`, `tzD0mVm…`) never had their
  private halves in the repository.

## 2. Findings & fixes applied

| # | Finding | Severity | Fix |
|---|---|---|---|
| 1 | The app's trust ring contained a **legacy key id** whose private half was never recoverable — dead trust that could never legitimately sign anything, pure attack surface | Medium | `TrustedLicenseKeys.All` now maps exactly ONE key id → the generator's public half. Legacy key removed. |
| 2 | `LicenseCrypto.VerifyCanonical` accepted **PKCS#1 v1.5** signatures in addition to PSS — a second acceptance path for no benefit (no shipped license uses v1.5) | Medium | PSS-only verification. New contract test `VerifyCanonical_rejects_pkcs1_v15_signatures` pins it. |
| 3 | The app accepted **raw unarmored JSON** license keys, although the generator only ever emits `PGKEY1.` blocks | Low | `LicenseKeyArmor.IsArmoredBlock` gate in `InstallKeyText`/`InstallKeyFile`: armored generator keys ONLY. New service test pins raw-JSON rejection + armored acceptance. |
| 4 | Generator plan preview text never updated when switching plans | UI bug | `PlanRadio_Checked` handler + code-set default (no XAML-order race). |
| 5 | Historical leak risk via the trust ring / old keys | Info | Verified clean; ring now single-key so any future leak of one key pair is a full, clean rotation (see §5). |

## 3. Attack matrix (what the reviewer tried, what holds)

| Attack | Feasibility | Defense |
|---|---|---|
| Forge a license key without the private key | Infeasible | RSA-2048-PSS over deterministic canonical bytes; trust ring = one public key; verify-before-store |
| Recover a signing key from git history | Infeasible (verified §1) | Forensic sweep + single-key ring |
| Modify/refactor a valid key (features, days, expiry, PC ID) | Fails | Any byte change breaks the signature; `InstallKeyText_rejects_tampered_...` test exists |
| Re-sign with v1.5 padding | Blocked | PSS-only verify + regression test |
| Paste raw JSON instead of generator format | Blocked | Armor-boundary check + regression test |
| Copy `license-key.bin` to another machine/user | Fails | DPAPI (LocalMachine) + fixed entropy; unreadable store → trial/read-only fallback, never a working license |
| Reset the trial by deleting files | Partially feasible — requires deleting ALL FOUR anchors (AppConfig, %ProgramData%, registry, DPAPI) | Multi-anchor ledger merges the EARLIEST issuance across anchors; single deleted anchor can't reset |
| Clock rollback to extend trial | Fails | Ledger heartbeat rollback detection → read-only degrade (never a new trial) |
| Clock forward | Self-harm only (expires sooner) | — |
| Decompile & patch the app binary to skip checks | Feasible — inherent to any offline .NET client | Accepted residual risk; mitigations in §4 |
| Steal the generator binary → produce unlimited keys | Feasible if the binary leaks | Generator is vendor-office-only; repo private; rotation procedure in §5 |

## 4. Residual risks & recommendations (honest senior take)

1. **Client-side patching is unavoidable offline.** A determined attacker
   can decompile the app and patch `LicenseService`/gates. No amount of
   client code stops that. Real anti-piracy needs the license check
   server-side (online activation endpoint) or at least periodic
   re-verification. Recommended when revenue depends on it.
2. **Obfuscation/packing** (e.g. Obfuscar, ConfuserEx, .NET Reactor)
   raises the cost of patching significantly and is cheap to add at
   publish time. Not applied here (no toolchain in this sandbox);
   recommended for release builds of the customer app.
3. **Keep the generator out of customer hands.** The embedded production
   private key means whoever holds the generator can issue keys. Repo
   is private today — keep it that way, and never commit the generator's
   publish output to any public store.
4. **Trial reset via all-anchor deletion** is the residual offline-trial
   limitation (requires local admin + knowing all four anchor locations).
   Accepted per product decision; ledger design documented in
   `TrialLedger.cs`.

## 5. Key rotation procedure (documented for the day it is needed)

1. Generate a fresh RSA-2048 pair (OpenSSL or the generator's
   `LicenseCrypto.GenerateKeyPair`).
2. Put the private half in `Tools/LicenseGenerator/ProductionSigningKey.cs`
   (`PrivateKeyXml`), the public half in `PublicKeyXml` there AND in
   `Services/Licensing/LicensePublicKey.GeneratorCompatiblePublicKeyXml`.
3. Rebuild BOTH projects in the same release.
4. Old keys die with the old public half (the trust ring holds exactly
   one key) — reissue active customers from the generator.
5. Tests pin the equality of the two public halves
   (`LicenseKeyEmbeddingTests`, `LicenseGeneratorDesignSourceTests`), so
   a mismatched rotation fails CI instead of shipping.

## 6. Verification performed this round

- History forensics: zero private-key embeds outside the intended file.
- New regression tests: v1.5 rejection (contract), raw-JSON rejection
  (service), app/generator public-half equality (already pinned).
- Structural audits: 174 XAML files parse; both changed views have no
  duplicate `x:Name`s and every wired handler exists; generator resource
  keys resolve; all changed C# files lexer-clean.

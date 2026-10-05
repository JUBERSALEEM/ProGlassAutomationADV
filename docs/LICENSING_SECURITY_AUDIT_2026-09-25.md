# PGKEY1 licensing security audit

Date: 2026-09-25. Scope: the existing RSA-PSS-SHA256 license contract, the generator that signs it, and the app path that accepts a pasted PGKEY1 key. This is not a claim that the system is uncrackable.

## What already held

- Signatures cover `LicenseCanonical.SerializeUtf8(document)` only. Pretty JSON and feature order do not change the signed bytes.
- Verification is PSS-SHA256 only. The old PKCS#1 v1.5 acceptance path is gone.
- The customer app embeds the production public key, not the production private key. `TrialKeyVault` returns a placeholder, not an RSA private key.
- The generator self-verifies a key before it reports success: create, canonical serialize, sign, read back, verify, validate.
- Offline activation already required an exact `keyId` and an exact algorithm. The simple-key verifier did not.
- There is no online revocation list. A stolen, unexpired, unbound key remains usable until expiry or until it is device-bound. That is an offline-license limit, not something a local check can fully close.

## Findings and what changed

| Severity | Finding | Change |
| --- | --- | --- |
| Critical | `VerifyAndValidate` used the only trusted public key when `keyId` was missing or unknown. The app trust ring has exactly one key, so rewriting `keyId` did not fail. The signature does not cover `keyId`. | Exact lookup only. Unknown `keyId` returns `UnknownKey`. No single-key fallback. |
| High | The algorithm string is outside the signed document. The verifier ignored it and always used PSS, so a rewritten algorithm was still accepted. | Allow-list is `RSA-PSS-SHA256` only. Anything else is `InvalidAlgorithm`, before the signature is trusted. |
| High | Installed-key startup verified the signature and then ignored `LastValidatedUtc`. Rolling the clock back made an expired key look current. Re-pasting the same key also reset that timestamp. | Runtime and install both consult a DPAPI clock anchor that is not deleted when a key is replaced. Rollback beyond 24 hours is rejected. The anchor moves forward only. A tampered clock file is not treated as "no clock". |
| Medium | The reader had no size cap, accepted comments and trailing commas, and matched property names case-insensitively. | 64 KB JSON cap, 96 KB armor cap, depth 16, exact names, no comments, no trailing commas. |
| Medium | Keys smaller than 2048 bits were not rejected by the verifier. | `VerifyCanonical` rejects a public key under 2048 bits and a signature whose length does not match the key. Existing 2048-bit licenses still verify. A future 3072-bit key can be added beside the current public key; this pass does not rotate the production key. |
| Medium | A signed document with an unknown type, expiry before issue, a missing issue time, or an unknown device binding could pass business rules if the signature was valid. | Those now fail closed after the signature check. Trial, Subscription, and Enterprise must have an expiry. |
| Medium | A signed activation policy could set a huge clock-rollback tolerance and widen the check. | The signed value may tighten the window. It cannot widen it past 24 hours. |
| Low | Verification exceptions were mostly caught, but a throw from the read path could escape to a caller. | `VerifyAndValidate` catches and returns `InvalidPayload`. It does not return success. |

PGKEY1 is unchanged. Already-signed licenses whose `keyId` is `production-key-2026` and whose algorithm is `RSA-PSS-SHA256` still verify. No PGKEY2 migration is required.

## What this does not do

- It does not stop someone who has the generator, or a copy of the private key already in git history, from signing a new license. The private key remains in the generator project only. It is not in the customer EXE. Removing it from git history would be a history rewrite, and rotating it without keeping the current public key would invalidate every issued license. Neither was done in this pass.
- It does not stop a person who can patch the client binary, or who owns the machine and deletes every local anchor, from bypassing a local check. No anti-debug or obfuscation was added. Those do not hold against a determined local attacker.
- It does not add online revocation. Until a revocation list exists, replay of a still-valid stolen key is limited by expiry and device binding, not by a server.
- UI visibility is not authorization. Feature checks for an installed key go through the verified payload. A future screen that enables a command without calling that check would be a product bug, not a signature bypass.
- `LicenseVerifier.Verify` is the legacy envelope path. Customer paste and startup do not call it. It was left in place because existing signer tests still use it.

## Attack coverage

`Licensing/ProGlass.Licensing.Tests/LicenseTamperTests.cs` covers a genuine key, JSON edits, armor edits, expiry, type, features, product, customer, device binding, version policy, signature bytes, unknown `keyId` with a one-key trust ring, algorithm downgrade, canonical feature reorder, expired replay, expiry-before-issue, clock rollback versus small skew, malformed input, empty trust, oversized input, a 1024-bit key, and comment or trailing-comma documents.

This sandbox has no .NET SDK, so those tests were not executed here. Run `dotnet test Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj` on a Windows build machine before shipping.

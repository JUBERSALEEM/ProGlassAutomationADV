# ProGlassAutomation Offline Max-Security Licensing — Phase 0 Audit (2026-08-29)

## Scope

Audited areas:

- `Services/Licensing/*`
- `Licensing/Contracts/*`
- `Tools/LicenseGenerator/*`
- `Views/Licensing/*`
- `ViewModels/Licensing/*`
- `App.xaml.cs`
- `MainWindow.xaml`
- `ProGlassAutomation.csproj`
- `installer/velopack.json`
- `Tests/Licensing/*`
- `Licensing/ProGlass.Licensing.Tests/*`

This report is the required **Phase 0 audit before the redesign**. It is intentionally concise, implementation-focused, and aligned to the user's offline-only constraint.

---

## Executive conclusion

The current repository is **not yet a secure offline commercial licensing system**.

It currently contains:

1. **Two parallel licensing stacks**
   - legacy runtime stack in `Services/Licensing/*`
   - separate contract stack in `Licensing/Contracts/*`

2. **Private key material in source / distributed tooling paths**
   - generator-side embedded production-compatible private key
   - client-side embedded trial private key path

3. **Client-side signing and trial issuance**
   - the shipped app can mint trial licenses locally

4. **Base64-wrapped license transport still used as an activation flow**
   - base64 is only encoding, not security
   - the current activation UX still normalizes it as a primary path

5. **Locally trusted activation state**
   - portable activation counting is file-based and machine-local
   - no cryptographically protected signed activation response contract exists yet in the active runtime path

6. **No single offline protocol authority**
   - request/response activation is not yet the only path
   - legacy `.lic` trust remains a broad bypass surface

Because the user explicitly confirmed **no server, no cloud, fully offline**, the correct target is **not** server-authoritative activation. The correct target is:

- a **single authoritative offline license contract**
- **vendor-side signing only**
- **customer app = verifier only**
- **manual offline challenge/response activation**
- **protected local activation state**
- **machine binding + anti-rollback + feature entitlement validation**
- **strict migration off the legacy base64-centered `.lic` workflow**

---

## High-severity findings

### H1 — Private signing key is embedded in the vendor tool source tree

Observed:

- `Tools/LicenseGenerator/AppCompatibleIssuerKeys.cs`
  - contains `PrivateKeyXml`

Risk:

- violates basic commercial key-custody rules
- anyone with repository or binary access can recover production-compatible signing material
- key rotation becomes cosmetic because the old compromised key remains in Git history

Required action:

- remove embedded production private key from repo
- move signing to external operator-held key material outside Git and outside customer machines

---

### H2 — Client-side trial signing path exists in the shipped application

Observed:

- `Services/Licensing/LicensePublicKey.cs`
- `Services/Licensing/TrialKeyVault.cs`
- `Services/Licensing/LicenseService.cs`
  - `IssueAutoTrialIfEligible(...)`
  - `InstallTrial(...)`

Risk:

- the client can create trusted signed artifacts locally for at least one trust path
- trial logic becomes extractable and forgeable after reverse engineering
- this directly conflicts with the requested hardened offline model

Required action:

- remove client-side signing for all production/runtime trust paths
- replace auto-trial issuance with vendor-issued offline activation material only

---

### H3 — Runtime app still trusts the legacy `.lic` flow instead of the stronger contract library

Observed:

- active runtime behavior still goes through `Services/Licensing/LicenseService.cs`
- `Services/Licensing/ProGlassLicense.cs` exists but is not the sole runtime authority

Risk:

- two verification/trust paths mean one hardened path can be bypassed through the other
- migration remains ambiguous

Required action:

- choose one authority: `ProGlass.Licensing`
- treat legacy `.lic` as migration-only, not a permanent equal path

---

### H4 — Base64 is still treated like a product activation channel

Observed:

- `Views/Licensing/LicenseActivationView.xaml.cs`
- `Tools/LicenseGenerator/LicenseGeneratorCore.cs`

Risk:

- whole-license base64 wrapping adds no security
- it encourages clipboard-based opaque blobs rather than structured signed offline artifacts
- it keeps the current UX coupled to the legacy format

Required action:

- retire base64-wrapped whole-license activation as the primary format
- use signed JSON files / structured activation response artifacts instead
- keep base64 only where unavoidable for binary signature fields inside JSON

---

### H5 — Local activation counting is not a secure activation protocol

Observed:

- `Services/Licensing/LicenseService.cs`
  - `activations.json`
  - local file counting

Risk:

- practical for convenience, not strong as a trust boundary
- copying/resetting local state remains viable for a determined attacker
- no nonce-bound activation response currently protects the activation lifecycle end-to-end

Required action:

- define signed offline activation response protocol
- protect local installed activation state with machine/user scoped Windows protection
- bind installed activation state to license ID + activation ID + installation ID + machine fingerprint

---

## Medium-severity findings

### M1 — Multiple silent/best-effort catches remain around licensing flow

Observed in:

- `Services/Licensing/LicenseService.cs`
- `Services/Licensing/LicenseVerifier.cs`
- `Views/Licensing/LicenseActivationView.xaml.cs`
- `Services/Licensing/TrialLedger.cs`

Security note:

Some catches are legitimate resilience boundaries, but current code mixes:

- telemetry resilience
- trust-decision resilience
- silent fallback behavior

Required action:

- trust failures must fail closed with structured results
- only telemetry/audit UI refresh hooks should remain best-effort

---

### M2 — Machine fingerprinting is useful but insufficient alone

Observed:

- `Services/Licensing/MachineFingerprint.cs`

Security note:

- fingerprinting is still useful for offline copy resistance
- it is not sufficient protection by itself
- it must be only one factor in a signed activation response and protected local state model

---

### M3 — Test seams remain visible in production assemblies

Observed:

- `LicensePublicKey.SetForTest(...)`
- `LicensePublicKey.SetTrialKeysForTest(...)`
- `LicenseService.TestDataFolderOverride`
- `TrialLedger.SkipRegistryAnchorForTests`

Required action:

- preserve only where necessary for testability
- tighten release separation over time

---

## Current trust points

The current runtime trusts licensing state at:

- `LicenseService.Current`
- `LicenseService.FeatureAllowed(...)`
- `LicenseGate.CanNavigate(...)`
- `ReadOnlyGate.Demand(...)`
- `LicenseStatusViewModel`
- `LicenseActivationView`
- `App.xaml.cs` startup path
- `MainWindow.xaml` overlay path

Implication:

The redesign must replace the authority feeding those surfaces, not just add a second secure-looking layer beside them.

---

## Offline-only redesign decision

Given the explicit user constraint:

- **no licensing server**
- **no cloud**
- **fully offline**

The redesign target is:

1. **Single authoritative contract** = `ProGlass.Licensing`
2. **Modern asymmetric signature** = RSA-PSS-SHA256 for now (`net8.0` compatibility)
3. **Vendor-side signing only**
4. **Manual offline activation request / response**
5. **Signed activation response bound to**:
   - product ID
   - license ID
   - installation ID
   - machine fingerprint
   - request nonce
   - activation ID
   - entitlements
   - validity window
6. **Protected local activation state** via Windows DPAPI / integrity-bound local cache
7. **Strict legacy cutover**
   - legacy `.lic` path becomes migration-only and then removable
8. **No client-side auto-trial self-signing**
9. **No whole-document base64 activation path as a primary workflow**

---

## What is being started now

This redesign starts with:

- updated Phase 0 audit
- new offline activation protocol foundation inside `ProGlass.Licensing`
- canonical request/response model for signed offline activation
- explicit migration direction away from legacy base64 `.lic` activation

This is **not yet final completion** of the full offline hardening program.

---

## Honest boundary

This sandbox currently has **no `dotnet` SDK available**, so I cannot truthfully claim:

- full solution build success
- runtime execution validation
- passing automated test suite execution

What I can do here safely:

- senior source audit
- protocol/contract redesign
- code-level hardening changes
- static validation (`git diff --check`, XML parse, brace balance, grep cross-checks)
- documentation of the exact secure operator workflow

That is the correct truthful boundary for this repo state today.

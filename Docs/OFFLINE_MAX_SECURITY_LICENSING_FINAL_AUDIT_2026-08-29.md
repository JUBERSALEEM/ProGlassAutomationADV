# ProGlassAutomation Offline Max-Security Licensing — Final Audit Snapshot (2026-08-29)

This is the post-implementation audit snapshot for the current offline redesign pass.

It is intentionally honest.

- It describes what is now materially improved.
- It describes what still remains theoretically attackable.
- It does **not** falsely claim full production completion where the sandbox cannot prove it.

---

## A. Existing vulnerabilities discovered

Primary vulnerabilities identified in the original licensing direction:

1. embedded production-compatible private key in generator source
2. embedded client-side trial private key path
3. legacy direct `.lic` runtime trust path
4. whole-license base64 activation flow
5. plaintext/local-only activation-trust patterns
6. mixed legacy/runtime/new-contract authority
7. silent or weakly classified failure handling in several licensing paths

---

## B. New security architecture

Current hardened direction now in repo:

- authoritative contract = `ProGlass.Licensing`
- activation model = offline manual request/response
- customer app = verifier only
- vendor tool = external-key-file signer
- local installed state = DPAPI-protected
- entitlements = cryptographically signed in activation response
- old `.lic` = migration/inspection path only, not trusted runtime authority

---

## C. Cryptographic design

Current design uses:

- RSA-PSS with SHA-256
- deterministic canonical serialization for signed payloads
- signed activation response envelope carrying:
  - protocol version
  - keyId
  - signed payload
  - signature

Security-sensitive bindings in payload:

- product ID
- license ID
- activation ID
- installation ID
- request nonce
- machine fingerprint
- dates
- feature entitlements
- offline policy
- signed status

Any protected payload modification invalidates verification.

---

## D. Key-management design

Current intended operator workflow:

- public keys embedded in app trust ring
- private signing key stored in external `.pgkey` file
- `.pgkey` is operator-held and outside Git
- generator loads `.pgkey` explicitly
- generator source no longer embeds production private key literal

Rotation model:

- trust ring supports multiple public keys
- responses carry `keyId`
- new keys can be introduced without breaking older issued responses during controlled transition

---

## E. Machine-binding design

Current offline binding uses:

- app-computed machine fingerprint
- request stores current machine fingerprint
- vendor-signed response binds back to that fingerprint
- runtime verifies machine fingerprint before accepting response

The fingerprint is still not magic DRM.

It is a copy-resistance layer, not an unspoofable identity.

---

## F. Activation workflow

Current secure workflow:

1. user enters vendor license reference in app
2. app generates activation request (`.pgrequest`)
3. request stores installation ID + machine fingerprint + nonce
4. vendor imports request into generator
5. generator loads external `.pgkey`
6. generator signs activation response (`.lic` JSON)
7. user imports signed response in app
8. app verifies:
   - protocol version
   - keyId
   - signature
   - product
   - installation ID
   - machine fingerprint
   - request nonce
   - dates
   - signed status
9. app stores protected activation state in DPAPI-protected local storage

---

## G. Files/classes changed

Major hardening surfaces now involved:

- `Licensing/Contracts/OfflineActivationContract.cs`
- `Licensing/Contracts/OfflineActivationService.cs`
- `Services/Licensing/LicenseService.cs`
- `Services/Licensing/OfflineActivationStore.cs`
- `Services/Licensing/LegacyLicenseMigrationInspector.cs`
- `Services/Licensing/ProGlassLicense.cs`
- `Services/Licensing/LicensePublicKey.cs`
- `Services/Licensing/TrialKeyVault.cs`
- `Views/Licensing/LicenseActivationView.xaml`
- `Views/Licensing/LicenseActivationView.xaml.cs`
- `Tools/LicenseGenerator/IssuerKeyMaterial.cs`
- `Tools/LicenseGenerator/LicenseGeneratorCore.cs`
- `Tools/LicenseGenerator/MainWindow.xaml`
- `Tools/LicenseGenerator/MainWindow.xaml.cs`
- `Docs/OFFLINE_MAX_SECURITY_*`
- updated licensing tests under `Tests/Licensing/*` and `Tests/Agent/*`

---

## H. Tests performed and results

What was validated in this sandbox:

- source-level brace balance on modified C# files
- XML parse on modified XAML files
- `git diff --check`
- grep/cross-check for critical hardening markers
- source tests updated to reflect the new architecture
- contract tests added/extended for:
  - tamper rejection
  - nonce mismatch
  - installation mismatch
  - machine mismatch
  - unknown key
  - expired response
  - suspended/inactive response
  - oversized request/response
  - legacy migration metadata extraction
  - protected-state failure handling

What could **not** be truthfully validated here:

- full `dotnet build`
- full automated test execution
- end-to-end runtime execution

Reason:

- this sandbox does not have the .NET SDK/runtime toolchain needed for build/test execution

---

## I. Remaining theoretical attack vectors

These still remain in principle because the system is fully offline and the customer controls Windows:

1. binary patching of the client
2. IL-level bypass of feature checks
3. debugger-assisted tampering on customer machine
4. spoofing or patching of machine fingerprint logic
5. deletion/corruption of local state followed by social/manual reissue abuse
6. key theft if operator mishandles `.pgkey` outside Git

This is normal for offline desktop licensing.

The redesign goal is to make attacks materially harder and more expensive, not impossible.

---

## J. Exact production workflow for safely generating customer licenses

1. Maintain production `.pgkey` outside the repository.
2. Embed only the matching public verification key in the app trust ring.
3. Release the app build.
4. Customer exports `.pgrequest` from the target machine.
5. Vendor loads the correct `.pgkey` into the generator.
6. Vendor imports `.pgrequest`.
7. Vendor chooses edition, features, expiry and grace policy.
8. Vendor generates signed response `.lic`.
9. Vendor sends the signed response to customer.
10. Customer imports the response in app.
11. App verifies and protects the installed activation state locally.

---

## Honest acceptance status right now

### Achieved in source

- production private key removed from generator source file
- client-side active self-signed trial flow removed
- request/response activation implemented in source
- protected local activation state implemented in source
- machine/install/nonce binding implemented in source
- legacy artifact moved to migration-only handling in source
- stronger failure classification implemented in source

### Not truthfully closed yet in this sandbox

- full solution build success
- all tests executed and passed
- final runtime/manual QA under real Windows execution
- release obfuscation integration
- release code-signing pipeline validation

So the redesign is **substantially hardened and architecturally redirected**, but I am not falsely claiming perfect final completion without build/test/runtime proof.

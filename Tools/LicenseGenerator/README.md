# ProGlass Automation — License Generator (standalone)

A **completely independent** production tool that creates signed
`.proglasslicense` files for ProGlass Automation customers.

It has **zero** dependency on the main application. It does not need:

- the ProGlassAutomation source, project, or build,
- a Visual Studio / MSBuild / dotnet build of the app,
- the app's EXE / DLL / database / configuration,
- app services or stubs (`AppLogServiceStub`, `AuditStub`, …),
- `System.Management` (it never fingerprints a machine itself).

## Architecture

```
        ┌──────────────────────────────────┐
        │  ProGlass.Licensing (contract)   │  stable format, canonical JSON,
        │  Licensing/ProGlass.Licensing    │  RSA-PSS sign/verify, validation,
        └───────────────┬──────────────────┘  error codes, key IDs, version
                        │                     policy, machine policy
        ┌───────────────┴──────────────────┐
        │                                 │
        ▼                                 ▼
LicenseGenerator                  ProGlassAutomation
(PRIVATE KEY · SIGNS)             (PUBLIC KEYS · VERIFIES)
```

The two sides reference **only** `ProGlass.Licensing`, never each other.
The private signing key lives **only** in the generator (DPAPI-protected at
rest); the app contains **only** trusted public keys.

## The stable contract

- `licenseFormatVersion = 1` — separate from any application version.
- `productId = PROGLASS_AUTOMATION` — permanent, never changes on a release.
- **Canonical serialization** — the signature is over exact, deterministic
  bytes (fixed key order, no whitespace), so verification is exact.
- **Version policy** — default is `minimum = 1.0.0`, `maximum = null`, i.e.
  *unlimited future compatibility*: a 2026 license works on 1.0 / 1.1 / 2.0 /
  3.0 builds. Restrictions are opt-in (range / exact / allowed-major).
- **Features** — stable IDs (`PROFORMA_INVOICE`, `JOB_ORDER`, …). Unknown
  future feature IDs are tolerated, never crash.
- **Device policy** — Unbound (default), SingleDevice, MultiDevice (N).
- **Key rotation** — every signature carries a `keyId`; the app can trust
  multiple keys, so old licenses survive rotation.
- **Self-verification** — every generated license is re-read and re-verified
  by the generator before it reports success.

## Build & run (independently)

```
dotnet build Licensing/ProGlass.Licensing.csproj
dotnet build Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj -c Release
dotnet run --project Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj
```

Output is `ProGlassAutomation.LicenseGenerator.exe`. Take it to a clean Windows
machine (no ProGlassAutomation) and it still generates licenses.

## Workflow

1. **Generate signing key** (once). The private key is stored DPAPI-protected
   on this machine; **COPY PUBLIC KEY (for app)** → paste into the app's
   `TrustedLicenseKeys` → rebuild the app.
2. **Fill the form** — license type, customer, validity, features, device
   policy, version policy.
3. **Generate** — the license is signed, written to a `.proglasslicense`
   file, re-read, re-verified, then reported valid.
4. **Send** the `.proglasslicense` file to the customer. They import it in
   the app (Settings → License) and it activates.
5. **Inspect** — open an existing license to see VALID / INVALID / EXPIRED /
   TAMPERED / WRONG PRODUCT and why.

## Security notes (honest)

RSA-2048 / PSS / SHA-256 protects license **authenticity and integrity** — a
normal user cannot edit the license without breaking the signature. It does
**not** make a locally-executed application impossible to reverse engineer
(that is not achievable with offline licensing, and we do not claim it).

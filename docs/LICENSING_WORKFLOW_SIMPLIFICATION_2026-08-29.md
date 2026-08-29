# Licensing Workflow Simplification — Design & Implementation Record (2026-08-29)

## 1. Why this change exists

The max-security offline redesign delivered excellent cryptographic hygiene
(vendor-signed offline activation, DPAPI-protected local state, anti-rollback
anchors) but produced a **workflow a fresh customer cannot complete**:

1. First run required a **"vendor license reference"** that a brand-new user
   cannot possibly have.
2. Activation required a request → vendor → response round-trip before the
   app was usable at all.
3. The automatic trial had been removed, so a fresh install landed in a hard
   **NotActivated** gate.

The user's directive (2026-08-29):

- **KEEP IT SECURED** — the cryptographic model must remain intact; only the
  workflow is simplified.
- Generator generates a key, the user uses it — **or** the user sends the
  PC ID and the developer generates. No upfront reference requirement.
- A fresh install must have an **active trial** within its period.
- If a used key is removed (e.g. reaches expiry) the user must **fall back to
  the trial** so the app keeps working within the trial period, with the
  license taking over when it arrives.

Product decisions confirmed with the user:

| Decision | Choice |
|---|---|
| Trial length | **30 days** (matches the existing `AutoTrialDays = 30`) |
| After trial ends | **Read-only mode** (data preserved, app still opens) |
| Trial scope | **Full features** (trial behaves like premium, as before) |
| Key binding | **Portable by default**; optional PC-ID binding per key |

---

## 2. The resulting three-track model

```
                        ┌──────────────────────────────────────────────┐
                        │   EVERY track ends in the same verifier       │
                        │   (RSA-2048-PSS signature, keyId ring,        │
                        │    product, dates, version + device policy)   │
                        └──────────────────────────────────────────────┘

 TRACK 1 — LICENSE KEY (primary, new)
   Vendor: License Generator → "LICENSE KEY" tab → GENERATE → one text
           block  PGKEY1.<base64url(signed .proglasslicense JSON)>
   Customer: Settings → License → paste → APPLY LICENSE KEY. Done.
   Optional: paste the customer's PC ID to bind the key to one machine.

 TRACK 2 — ACTIVATION REQUEST (advanced, kept)
   Customer: Settings → License → GENERATE REQUEST (no reference needed)
             → send .pgrequest to the vendor.
   Vendor:   Generator → load request → GENERATE RESPONSE → send back.
   Used when the vendor requires per-machine issuance / nonce binding.

 TRACK 3 — AUTOMATIC TRIAL (restored)
   Fresh install: 30-day trial issued automatically on first run,
   recorded in the tamper-evident 4-anchor TrialLedger.
   A valid signed license ALWAYS wins. Key removed / expired while the
   machine's trial window is open → trial resumes automatically.
   Window over → existing read-only mode (data preserved).
```

### Precedence rules (exact)

On every `LicenseService.Initialize`:

1. **Corrupt protected store** (`offline-activation.bin` / `license-key.bin` /
   pending request that exists but cannot be DPAPI-read) → **fail closed,
   Invalid**. Corruption is tampering evidence; the trial fallback applies
   only to *cleanly absent or expired* artifacts.
2. **Valid signed license key** → `Valid` / `ExpiringSoon` / `Trial`
   (vendor trial key). Authoritative.
3. **Valid signed activation response** → same statuses. Authoritative.
4. Legacy `license.lic` / `license.json` artifact found → migration prompt
   (unchanged from the max-security migration rule).
5. Nothing installed, or installed artifact expired / invalid / wrong
   machine and the **trial window is open** → `Trial` (full features).
6. Trial window passed → the signed artifact's own state stands:
   `GracePeriod` / `TrialExpired` (read-only), `Expired` / `Invalid` /
   `MachineMismatch` (overlay).
7. **Clock rollback detected by the ledger heartbeat → Invalid, always.**
   The anti-rollback anchors are untouched and outrank everything except a
   valid signed license load (which has its own signed rollback tolerance).

---

## 3. Security invariants — what did NOT change

| Invariant | Status |
|---|---|
| Private signing keys live only in vendor-held external `.pgkey` files; none embedded | unchanged |
| Every artifact the app trusts is RSA-2048-PSS-signed and verified against the embedded public-key ring (`TrustedLicenseKeys.All`) | unchanged |
| Installed state stored DPAPI-protected (LocalMachine), never plaintext | unchanged (new key store uses the same `WriteProtected` path) |
| Activation responses bind installation + machine fingerprint + request nonce | unchanged |
| Clock-rollback detection: signed `OfflinePolicy` tolerance (responses) and ledger tolerance (trial) | unchanged, now actively used again |
| Tampering with a key/response fails signature verification and stores nothing | unchanged; new install path rejects BEFORE storing |
| Legacy insecure artifacts are inspect-only, never auto-migrated | unchanged |

### Why restoring the trial does not weaken security

- The trial is an **app-enforced machine-scoped state**, not a self-signed
  license: the runtime payload it produces carries no signature and can
  **never outrank** a signed license (rule 2/3 above run first).
- The window is recorded by the existing `TrialLedger` — four anchors
  (Roaming config flag machine-scoped by fingerprint, `%ProgramData%`
  ledger, HKLM registry, DPAPI blob). Deleting app files or reinstalling
  does **not** reset the window.
- The ledger only ever moves timestamps **forwards**; `Heartbeat()`
  rollback detection hard-suspends the trial.
- The trial grants nothing a signed license would not also grant, and it
  terminates in read-only mode with all data preserved.
- A corrupt protected store still **fails closed** — fallback is only for
  clean absence / clean expiry.

---

## 4. The license key format — `PGKEY1`

- `PGKEY1.` + base64url (no padding) of the exact signed
  `.proglasslicense` JSON produced by `LicenseWriter.ToJson`.
- Decode tolerates whitespace anywhere (paste-wrapping safe). Raw JSON is
  also accepted, so existing `.proglasslicense` files work verbatim.
- The armor is **transport only** — every decoded document still goes
  through `VerifyAndValidate` (signature, keyId ring, product, dates,
  version policy, optional device binding). Implemented in
  `Licensing/Contracts/LicenseKeyArmor.cs` so generator and app share one
  definition.
- Generator writes the **same document** a `.proglasslicense` file would
  carry, so a key file saved from the generator is interchangeable with a
  pasted block.

### Edition display for keys

The signed document carries a license *type* (Trial / Subscription /
Lifetime / PerpetualVersion / Enterprise), not an edition label. To avoid
changing the canonical signature format (which would invalidate every
existing signature), the app derives the display edition deterministically
from type + features, matching the generator's own presets:

- type `Trial` → Trial; type `Enterprise` → Enterprise
- features contain `Cutting.*` → Factory
- features contain `Reports.*` or `AI.*` → Business
- otherwise → Basic

---

## 5. Implementation map

### Contract library (`ProGlass.Licensing`)

| File | Change |
|---|---|
| `Contracts/LicenseKeyArmor.cs` | NEW — `PGKEY1` encode / tolerant decode |
| `Contracts/LicenseResults.cs` | `LicenseGenerationResult.LicenseJson` added (the self-verified JSON, for armoring) |
| `Contracts/LicenseService.cs` | `Create` populates `LicenseJson` |
| `Contracts/OfflineActivationService.cs` | `ReadRequest` / `VerifyResponse` no longer require a non-empty `licenseId` (informational field; the security binding is installation + nonce + fingerprint + signature) |
| `Contracts/OfflineActivationContract.cs` | doc comment: `licenseId` now optional |

### App (`ProGlassAutomation`)

| File | Change |
|---|---|
| `Services/Licensing/AutoTrialManager.cs` | NEW — trial resolution, issuance, payload projection, rollback hard stop |
| `Services/Licensing/OfflineActivationStore.cs` | NEW `license-key.bin` protected store (same DPAPI machinery); store header updated |
| `Services/Licensing/TrialLedger.cs` | `SkipConfigAnchorForTests` hook (read+write) — production behavior unchanged |
| `Services/Licensing/LicenseService.cs` | `CreateActivationRequestJson` no longer throws on empty reference; `InstallKeyText` / `InstallKeyFile`; load state machine reworked (key → response → trial fallback, corrupt fails closed); `InstallText` clears a previous key (one active license); `InstallTrial` repurposed to resolve+reload; `AutoTrialSourceMarker = "auto-trial"` |
| `ViewModels/Licensing/LicenseStatusViewModel.cs` | source/activation-mode labels for auto-trial and keys; `IsLicenseRequired` doc updated |
| `Views/Licensing/LicenseUiText.cs` | key-first copy for every status |
| `Views/Licensing/LicenseActivationView.xaml(.cs)` | NEW "LICENSE KEY" card (paste / browse); reference box removed; request flow relabelled ADVANCED; PC ID label; status headlines |
| `Views/Settings/Sections/SettingsLicenseSection.xaml` | subtitles updated |
| `MainWindow.xaml` | overlay comment block corrected (fresh install lands in trial) |

### Generator (`Tools/LicenseGenerator`)

| File | Change |
|---|---|
| `LicenseGeneratorCore.cs` | `LicenseKeyIssueRequest/Result` + `GenerateLicenseKey` (contract `Create` → armor) |
| `MainWindow.xaml` | NEW "LICENSE KEY — SIMPLE FLOW" card (edition, days, customer, optional PC ID, generate/copy/save); request flow relabelled ADVANCED |
| `MainWindow.xaml.cs` | key-card handlers |

### Tests

- `LicenseServiceTests` — trial-anchor scoping; fresh-store → automatic
  trial; `InstallTrial` resolves the trial; empty-reference request; 6 new
  key tests (valid armored, raw JSON, tampered, unknown issuer,
  machine-bound-for-other-PC, non-key text).
- `LicenseStatusViewModelTests` — scoping; empty store now shows the
  automatic trial.
- `LicenseGateTests` — scoping; the Cutting-deny path is seeded with a
  real Business license (a fresh store is now premium via trial).
- Two pre-existing stale assertions aligned with actual behavior
  (corrupt protected pending request → `Invalid`; both messages now exist
  in production code, which previously never emitted them).

---

## 6. Operator notes

- **Issuing a license now**: Generator → load `.pgkey` → LICENSE KEY card
  → edition → days (0 = never expires; Trial fixed 30) → optional customer
  data → optional PC ID → GENERATE → copy the `PGKEY1…` block to the
  customer. Portable by default.
- **Machine-bound licenses**: customer reads their **PC ID** from
  Settings → License (PC ID / machine fingerprint field), sends it to the
  vendor, vendor pastes it in the key card. The key then refuses other
  machines with `Wrong machine`.
- **The advanced request flow still works** for the old operating
  procedure, now without any reference requirement.
- **Existing installed activations keep working** — the activation store
  path is unchanged and remains second in precedence behind a key.
- **Trial fallback for existing customers**: machines that already issued
  a legacy trial (pre-removal ledger) resume the remaining window; brand
  new machines get a fresh 30 days.
- Do NOT add `Setter Property="Template"` styles, do not change the
  canonical serializer order, and mirror any new type-scale token in
  `Tools/LicenseGenerator/Theme.xaml` (standalone assembly).

## 7. Verification performed (no .NET toolchain available in this sandbox)

- Python C# lexer (interpolated / verbatim / raw-string aware): **900
  files, 0 problems**.
- XML parse of all XAML: **0 failures**.
- Resource-resolution cross-check for the two changed views: **no missing
  StaticResource keys** (generator checked against its own Theme.xaml).
- x:Name ↔ code-behind and `Click=` ↔ handler cross-check for both
  changed views: **all wired** (the "unused name" list for the generator
  is the pre-existing pattern of buttons only referenced through their
  own Click handlers).
- `LicenseReferenceBox` references: 0 remaining.

Build-time verification (VS compile, unit tests) remains the user's step —
as always, their Visual Studio error list is authoritative for WPF
semantic errors that plain XML parsing cannot catch.

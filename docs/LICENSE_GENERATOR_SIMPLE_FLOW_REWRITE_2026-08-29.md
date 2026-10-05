# License Generator — Complete Rewrite as a Single-Purpose Key Signer (2026-08-29)

> **SUPERSEDED the same day** by
> [`PLAN_BASED_LICENSING_AND_NEVER_BLOCKING_STARTUP_2026-08-29.md`](./PLAN_BASED_LICENSING_AND_NEVER_BLOCKING_STARTUP_2026-08-29.md):
> the external `.pgkey` loading step described below was removed
> entirely — the generator now embeds the production signing pair and
> offers a single-click plan picker (7 days → 2 years). The sign-only
> security analysis below remains valid; only the key PROVISIONING
> model changed.
>
> Original record follows.

> Senior analysis & implementation record. Supersedes the "advanced toggle"
> approach recorded in `LICENSING_WORKFLOW_SIMPLIFICATION_2026-08-29.md §8`:
> the request/response UI no longer exists in the generator AT ALL — it was
> removed, not hidden.

## 1. Problem statement

The License Generator had grown a **second, parallel product** inside it: the
offline activation request/response flow (load .pgrequest → generate signed
response → copy response JSON). Even after being collapsed behind an
"ADVANCED" toggle, it doubled the tool's state, event handlers and mental
model. The vendor's actual job is one thing: **sign a license key and send
it**. Everything else is legacy machinery the app no longer needs.

## 2. What the flow must be

```
Vendor                     Customer
  │                            │
  │ 1. load external .pgkey    │
  │ 2. pick edition, days      │
  │ 3. (optional) paste PC ID  │
  │ 4. GENERATE LICENSE KEY    │
  │                            │
  │─── email/chat: PGKEY1… ───▶│
  │                            │ 5. Settings → License
  │                            │ 6. paste the block → APPLY
  │                            │ 7. INSTANT ACTIVE (verified, stored)
```

Two clicks on each side. No requests, no responses, no references, no
two-file ping-pong. The trial already covers the customer while the key is in
transit (see `LICENSING_WORKFLOW_SIMPLIFICATION_2026-08-29.md`).

## 3. Security model (unchanged — "KEEP IT SECURED")

The rewrite deletes UI, not cryptography. Every property of the secured
pattern is preserved, and the new code makes it *easier* to audit:

| Property | Where |
|---|---|
| Private key never embedded | `AppCompatibleIssuerKeys.cs` exposes public-key hints only (`TrustedPublicKeys`, fingerprint); the private half lives in an external `.pgkey` the operator supplies at runtime |
| Sign-only tool | `LicenseGeneratorCore.GenerateLicenseKey` delegates to contract `LicenseService.Create` — RSA-2048-PSS over canonical document bytes, keyId embedded in the envelope |
| Unknown keyId → rejected | app-side `TrustedLicenseKeys.All` maps keyId → public key; unknown keyId fails verification |
| Emitted key self-verified | contract `Create` re-reads and `VerifyAndValidate`s the exact JSON before the PGKEY1 block is produced — a misconfigured issuer key can never silently emit a key the app will reject |
| In-memory self-test | new `RunSelfTest` signs a synthetic 1-day key and verifies it against the loaded key's own public half — **nothing is written to disk** (the old self-test wrote a scratch file) |
| Tamper-evident armor | `PGKEY1.base64url(signed JSON)` — transport-only armor; every key still goes through full signature verification on install |
| Machine binding optional | empty PC ID → portable key (`Binding = Unbound`); pasted PC ID → `SingleDevice` — both signed into the document |

## 4. What was removed (complete rewrite, not a patch)

`Tools/LicenseGenerator/MainWindow.xaml` and `.xaml.cs` were **rewritten
from scratch**; `LicenseGeneratorCore.cs` was rewritten around the sign-only
job. Deleted in full:

- PC-ID activation request parsing (`ParseActivationRequestText`),
- response generation (`GenerateActivationResponse`,
  `OfflineActivationService.SignResponseJson`),
- response copy/save handlers and the `KeyOutputBox` response box,
- `AdvancedRequestPanel` + `AdvancedFlowToggleButton` (the previous "hide
  it" patch — the flow is now gone, not collapsed),
- any `_uiReady`/radio-guard pattern (the rewrite has exactly one
  `SelectionChanged` handler, which only refreshes the preview text and the
  trial-day hint).

The app side mirrors this: `LicenseActivationView` was rewritten as ONE card
— paste key → APPLY (or OPEN KEY FILE). Trial status and the read-only PC-ID
strip remain; the app-side ADVANCED panel and its toggle handler were
removed. `LicenseUiText` and the Settings license section were re-worded so
no "activation request" terminology appears anywhere in the UI.

## 5. New surface

- `LicenseGeneratorCore.LicenseKeyIssueRequest` (Edition, CustomerId,
  CompanyName, Notes, DaysValid — ≤0 ⇒ Lifetime, DeviceFingerprint —
  empty ⇒ portable, Features).
- `LicenseGeneratorCore.LicenseKeyIssueResult` (Success, Message,
  ArmoredKey, LicenseJson, SigningKeyId, Document).
- `LicenseGeneratorCore.RunSelfTest(IssuerKeyMaterial)` →
  `SelfTestResult` (in-memory sign + self-verify).
- `LicenseGeneratorCore.DefaultFeatures(edition)` — feature presets kept in
  sync with the app's `LicenseService.DeriveKeyEdition` (Trial/Basic/
  Business/Factory/Enterprise).
- `MainWindow` code-behind: `_loadedKeyMaterial`, `_lastLicenseKey`;
  handlers LoadKey/CreateKey/CopyPublicKey/SelfTest, Generate/Copy/Save
  license key; `ResolveBrush` fallback, `SelectedComboText`, `ParseInt`
  (clamped 0–3650), `SetStatus` with brush-kind mapping.

## 6. Result & verification

- Lexer audit: all 900 C# files clean (the 9 files my ad-hoc parser flags
  are pre-existing `#if`-style files untouched by this change — the session
  lexer baseline at `9f86b88` was clean and none of them changed here).
- 174 XAML files parse; no duplicate `x:Name`; every wired event handler
  (`Click`, `SelectionChanged`, `Loaded`) exists in its code-behind.
- All StaticResource/DynamicResource keys in both rewritten views resolve
  against the generator `Theme.xaml` closure and the app theme merge chain.
- Source-inspection tests updated to pin the new design:
  `Tests/Agent/LicenseGeneratorSourceTests.cs` (key-only window,
  `DefaultOutputFolder`, `RunSelfTest`, no `_uiReady`/request boxes) and the
  new `Tests/Agent/LicenseGeneratorDesignSourceTests.cs` (sign-only core,
  armored keys, no response-flow strings in UI or core).

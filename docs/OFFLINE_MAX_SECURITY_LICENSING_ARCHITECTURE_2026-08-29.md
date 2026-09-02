# ProGlassAutomation Offline Max-Security Licensing Architecture (2026-08-29)

## Objective

Design a stronger **offline-only**, **serverless** commercial licensing model for ProGlassAutomation.

This architecture does **not** claim to be uncrackable. The customer controls the Windows machine. The goal is to make:

- license editing
- license copying
- naive replay
- simple base64 decoding tricks
- casual binary patching
- crude local state reset

materially harder through multiple independent controls.

---

## Core decisions

### 1. One authority only

The authoritative contract becomes:

- `Licensing/Contracts/*` (`ProGlass.Licensing`)

The app must eventually consume a single verified offline license/activation context from that library.

Legacy `Services/Licensing/*` stays only as a transition surface until runtime cutover is complete.

---

### 2. No whole-license base64 workflow

Whole-document base64 blobs are retired as a primary activation mechanism.

Important nuance:

- **base64 as security** = rejected
- **base64 for binary signature bytes inside a JSON envelope** = still normal and acceptable

The target operator/customer exchange format is:

- structured signed JSON files
- deterministic canonical payloads
- explicit protocol versions

---

### 3. No private key in client app

The customer application must contain only:

- public verification keys
- key IDs
- validation rules
- local protected state logic

It must never contain:

- production signing private key
- customer-usable activation signing secret
- self-signing trial private key

---

### 4. Manual offline challenge / response

Offline activation flow:

1. Customer installation creates an **activation request**.
2. Customer sends the request to the vendor manually.
3. Vendor uses a secured issuer tool with an external private key.
4. Vendor returns a **signed activation response**.
5. Customer imports the signed response.
6. App verifies signature, protocol version, product, nonce, installation binding, machine binding, dates, and entitlements.
7. App stores a protected local activation record.

---

## Target artifact set

### A. Activation request

Unsigned but deterministic request JSON carrying:

- `protocolVersion`
- `productId`
- `licenseId`
- `installationId`
- `machineFingerprint`
- `requestNonce`
- `requestedAtUtc`
- `applicationVersion`

Why unsigned?

- before activation, the customer app has no private signing authority
- the request is evidence for the vendor, not an authority object
- the signed vendor response binds back to the request nonce and installation ID

---

### B. Signed activation response

Vendor-signed response JSON envelope carrying:

- `protocolVersion`
- `algorithm`
- `keyId`
- `payload`
- `signature`

Payload includes:

- `productId`
- `licenseId`
- `activationId`
- `installationId`
- `requestNonce`
- `machineFingerprint`
- `customerId`
- `companyName`
- `edition`
- `licenseType`
- `issuedAtUtc`
- `notBeforeUtc`
- `expiresAtUtc`
- `featureEntitlements`
- `licenseStatus`
- `offlinePolicy`

Any mutation of these fields must invalidate signature verification.

---

### C. Protected local activation state

Stored locally using Windows protection and integrity-aware metadata.

Protected state should bind together:

- activation ID
- license ID
- installation ID
- machine fingerprint
- last verified time
- last trusted status
- anti-rollback timestamp
- public key ID used

This local state must never contain a production private key.

---

## Security layers

### Layer 1 — Canonical signed payloads

All security-sensitive payloads use deterministic canonical serialization before signing.

### Layer 2 — Machine binding

Vendor-signed activation response binds to the app-computed machine fingerprint.

### Layer 3 — Installation binding

Each install gets its own installation ID, preventing blind reuse of one response across multiple installs.

### Layer 4 — Nonce binding

The signed response includes the request nonce so a response is linked to a specific request.

### Layer 5 — Protected local state

DPAPI/local protection makes local tampering and copying harder.

### Layer 6 — Feature entitlements in signed payload

Entitlements must come only from the verified signed response.

### Layer 7 — Key rotation support

Each response carries `keyId`. The app can trust multiple public keys during controlled migration.

---

## Key-management workflow

### Production model

- generate signing key pair on a vendor-controlled workstation
- keep the **private key outside Git**
- distribute only the public key into the application trust ring
- rotate by adding a new `keyId` + public key and retiring the old issuer key operationally

### Minimum acceptable operator workflow

1. Generate key pair on secured machine.
2. Export public key into app trust manifest.
3. Store private key in an operator-only folder outside repo.
4. Back it up encrypted/offline.
5. Issue licenses only from operator tooling.
6. Never ship issuer private key with customer software.

---

## Migration strategy

### Stage 1

Add the offline activation request/response protocol foundation.

### Stage 2

Teach runtime app to verify the new signed activation response path.

### Stage 3

Retire base64 whole-license activation UX.

### Stage 4

Remove client-side auto-trial signing and embedded trial private key path.

### Stage 5

Demote legacy `.lic` verification to migration/import only.

### Stage 6

Remove legacy trust path entirely after successful field migration.

---

## Honest security limitation

Without a server, the following remain fundamentally limited:

- real-time global activation counting
- remote revocation enforcement
- strong replay prevention across disconnected cloned machines
- true compromise recovery after customer-side binary patching

This architecture still materially improves security because it removes the biggest avoidable weaknesses:

- embedded production signing key
- client-side signing authority
- whole-license base64 workflow
- unsigned/loosely structured activation artifacts
- absence of nonce/installation binding in activation exchange

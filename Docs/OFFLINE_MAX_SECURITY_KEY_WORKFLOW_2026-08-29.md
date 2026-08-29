# ProGlassAutomation Offline Licensing — Key Management Workflow (2026-08-29)

> **UPDATED 2026-08-29 (same day, later change):** the WORKFLOW portion of
> this document is superseded in part by the licensing workflow
> simplification — see
> [`LICENSING_WORKFLOW_SIMPLIFICATION_2026-08-29.md`](./LICENSING_WORKFLOW_SIMPLIFICATION_2026-08-29.md).
> The vendor-license-reference requirement is gone, a signed **license key
> (PGKEY1)** is now the primary issuance path, and an automatic 30-day
> trial is active again. The KEY-SECURITY rules below (private keys only
> in operator-held external `.pgkey` files, public keys only in the app)
> are unchanged and still apply to both issuance paths.

## Purpose

Define the **safe operator workflow** for production offline-license issuance after the max-security redesign.

The critical rule is simple:

- **public keys** may live in the app
- **private signing keys** must live only in an operator-held external key file outside Git and outside customer machines

---

## File roles

### App-embedded trust

The customer app contains only public verification material:

- `production-key-2026`
- `legacy-production-key-2026`

These are compiled into the app trust ring.

### Operator-held key file

The vendor keeps a separate `.pgkey` file containing:

- `keyId`
- `algorithm`
- `publicKeyXml`
- `privateKeyXml`
- `purpose`
- `createdAtUtc`

This file must **never** be committed to the repository.

---

## Production procedure

### A. First-time production setup

1. Prepare a secured vendor workstation.
2. Generate or import the production `.pgkey` file there.
3. Confirm the `.pgkey` public half matches the app's embedded production public key.
4. Store the `.pgkey` outside the repository.
5. Make an encrypted offline backup.
6. Restrict access to authorized operators only.

### B. Customer activation issuance

1. Customer exports `.pgrequest` from the app.
2. Operator loads the production `.pgkey` into the generator.
3. Operator imports the customer `.pgrequest`.
4. Operator selects edition, expiry, grace policy, and feature entitlements.
5. Generator signs the activation response.
6. Operator returns the `.lic` signed activation response to the customer.
7. Customer imports the response in the app.

### C. Renewal

1. Customer exports a fresh `.pgrequest`.
2. Operator loads the same or rotated `.pgkey`.
3. Operator issues a new signed response with new validity dates.
4. Customer imports it; app overwrites the protected local activation state.

### D. Machine replacement / transfer

1. Customer exports a fresh `.pgrequest` from the new machine.
2. Operator treats it as a reissue event.
3. Operator issues a new signed response bound to the new machine fingerprint and installation ID.
4. Old activation can no longer be considered authoritative operationally.

---

## Rotation

### Planned rotation

1. Generate a new operator-held key file, e.g. `production-key-2027`.
2. Embed the new public key into the app trust ring.
3. Release the updated build.
4. During migration, trust both old and new public keys.
5. Issue new responses only with the new private key.

### Emergency rotation

1. Assume the old private key is compromised.
2. Generate a replacement key offline.
3. Ship a new app build with the new trust ring.
4. Stop issuing with the old key immediately.
5. Reissue customer activations under the new key.

---

## What must never happen

- never commit `.pgkey` to Git
- never embed production private key in app code
- never embed production private key in generator source
- never send `.pgkey` to customers
- never store production private key in appsettings/config
- never rely on base64 wrapping as security

---

## Security limitation

Because the model is fully offline and serverless, this workflow cannot provide:

- real-time global activation counting
- remote revocation push
- real-time fraud detection

It still materially improves security by enforcing:

- signed response authenticity
- machine + installation + nonce binding
- protected local activation state
- no embedded production signing key in the shipped app or generator source

# ProGlass Automation — Licensing

## How the licence works

A ProGlass Automation licence is a small text file (`.lic`) your vendor sends you. It contains:

- **Your customer code** (`CUST-2026-001` or whatever your vendor uses).
- **Your company name** as it should appear on the About page.
- **The edition** you bought (Basic / Business / Factory / Enterprise).
- **The issue and expiry dates** (issue is fixed; expiry is usually one year out).
- **The maximum number of devices** the licence can be activated on.
- **A list of features** the licence grants.
- **An RSA-2048 signature** that ties the whole thing to your vendor's signing key.

The app verifies the signature on every launch. If the signature does not match, the licence is rejected — you cannot use a tampered file, and you cannot share your vendor's key with a competitor because your vendor never gave it to them.

## Editions

| Edition    | Intended for                                          | Max devices | What you get                                         |
|------------|-------------------------------------------------------|-------------|------------------------------------------------------|
| **Trial**  | Evaluating the app for up to 30 days                 | 1           | All ERP modules, 30-day expiry, machine-bound.       |
| **Basic**  | Single-user glass shops, no cutting optimisation     | 1           | Inquiry, Proforma Invoice.                            |
| **Business** | 2-5 user glass manufacturers, no optimisation       | 5           | All ERP, no cutting optimisation.                    |
| **Factory** | Unlimited users, full cutting + nesting engine      | Unlimited   | All ERP + cutting optimisation.                      |
| **Enterprise** | Multi-site, custom integrations, SLA              | Unlimited   | All + custom features.                               |

The licence file's `edition` field is metadata only — the actual gate is the `features` list. A Basic-edition licence that has `"features": ["*"]` would behave like Enterprise for the duration of the licence. Your vendor decides what features to grant.

## Activating your licence

There are three ways, all of which land the `.lic` file in the right place:

1. **Welcome view (first run only)** — click **ACTIVATE LICENSE FILE…**, pick the file. Done.
2. **Settings → Data → Database Health** (after first run) — same button, same flow. The app verifies the file, reloads, and the About page refreshes.
3. **Manual drop** — paste the file at `%AppData%\ProGlassAutomation\license.lic`. The app picks it up on next launch.

## Where to find the current licence state

Two surfaces show the licence state live:

- **About → License** — the full card with edition, customer, expiry, days remaining, features, and both the licence-bound fingerprint and your current machine's fingerprint.
- **Settings → Data → Database Health** — a one-line summary that includes the status badge.

## Renewing a licence

Licences are issued for a fixed duration (typically one year). When the licence is within 14 days of expiry, the app shows a renewal banner; when it is within 7 days of expiry but still valid, the app runs in **grace mode** — full access to read, but writes / exports are disabled.

To renew, ask your vendor for a new `.lic`. Drop it in the standard location. The old one is overwritten; the audit log records both events.

If you let the licence expire and pass the grace period, the welcome view reappears with the licence-expired status. You can still start a Trial or drop in a new licence to continue.

## Activations

For **portable licences** (no machine fingerprint bound to the licence), the app counts distinct devices via the `activations.json` ledger. The ledger is per-device — your four office PCs each have their own. When you reach `maxActivations`, the next device shows the `ActivationLimit` status and refuses to run. To free a slot, manually delete the device's entry from any one of the four PCs' `activations.json`.

For **machine-bound licences** (a specific fingerprint is set in the licence), only that one device can run the licence. The fingerprint is computed from CPU count, OS, .NET version, volume serial, and the first non-loopback NIC MAC. Spoofing these is possible but defeats the purpose — they exist to prevent **accidental** copying, not piracy.

## What to do if you lose your licence

1. Check `%AppData%\ProGlassAutomation\license.lic` — the file is probably still there.
2. If you reinstalled Windows, the licence file is on your old `%AppData%` (if you backed it up). Restore it.
3. If the file is genuinely gone, ask your vendor for a re-issue. They will email a new `.lic` and you drop it in the standard location. **No data loss;** your records are in the SQLite files, not in the licence.

## What to do if the signature is invalid

The app shows a clear error dialog with the precise reason. The most common causes:

- The file was corrupted in transit (download again).
- The file is for a different version of the app (your vendor may have re-issued with a new signature after a major upgrade — ask them).
- The embedded public key in your app is the placeholder. This happens when the developer shipped a build without pasting the real public key into `LicensePublicKey.cs`. **Contact your vendor**; the fix is on their side, not yours.

## Telemetry

ProGlass Automation **does not** report licence activations back to your vendor. The activation ledger is a local file. If you want your vendor to know when you activated, send them the `.lic` filename or the `ErrorId` from the welcome view's error card.

## Audit log

Every licence event — file install, file replaced, expiry transition, trial start — is recorded in the activity log with a `Licensing` section. Open **Logs & Activity** from the shell rail to see the full history. Your vendor can request an export.

# ProGlassAutomation Offline Licensing — Legacy Migration Procedure (2026-08-29)

## Purpose

Define the safe migration path from the old direct `.lic` model to the new
secure offline activation-response model.

## Rule

A legacy `.lic` file is **not** treated as trusted runtime activation state in
this redesign.

It may only be used as:

- a metadata source for operator review
- a reissue aid for customer migration

## Safe migration flow

1. Customer opens the app on the target machine.
2. App detects a legacy artifact and refuses to trust it directly.
3. App tells the user to generate a fresh activation request.
4. Vendor inspects the old `.lic` metadata:
   - license ID
   - customer ID
   - company name
   - edition
   - machine-binding hint
5. Vendor loads the customer `.pgrequest` into the generator.
6. Vendor re-issues a signed offline activation response under the new trust model.
7. Customer imports the response.
8. App stores the trusted activation state in protected local storage.

## What the migration inspector does

The migration inspector reads old `.lic` JSON only to extract display metadata.

It does **not**:

- trust the old signature as current runtime authority
- activate the app
- bypass the new request/response flow
- copy the old artifact into the new protected state store

## Why this matters

Without this separation, the old legacy path remains a bypass surface forever.

The redesign therefore enforces:

- old artifact = inspect only
- new signed response = activate

## Operational note

If the old `.lic` is malformed or oversized, the vendor can still migrate the
customer by relying on commercial records and generating a fresh response from
a new customer request.

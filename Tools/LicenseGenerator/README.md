# ProGlass Automation License Generator

## Current status: issuance paused

**Do not use or distribute an existing License Generator binary.** The former production signing private key was exposed in tracked source and must be treated as compromised. The current source no longer contains that private value, and the generator now fails closed instead of reading an old DPAPI cache or embedded fallback.

This is an interim containment measure, not a completed key rotation:

- The customer app still trusts the old public key. A holder of a previously copied private key may still create signatures accepted by app versions that trust it.
- Existing generator binaries and copies of the old key are unaffected by these source changes.
- The old key must be revoked through a coordinated app release after a replacement key and customer-license migration plan are approved.
- No replacement private key was generated, stored, or requested in chat.

## Required owner-controlled rotation

1. Generate a replacement signing key in an owner-controlled HSM, certificate store, or approved secrets-management system. Keep the private half out of source control, build artifacts, and chat.
2. Add only the replacement public key to the app's trust ring and define how existing customers will receive replacement licenses.
3. Provision the replacement signer to the vendor tool through an approved secure integration; do not restore the old key or its DPAPI cache.
4. Test that replacement signatures are accepted and old-key signatures are rejected by the new app build.
5. Distribute the app update and replacement licenses, then retire the old signing identity and all known copies.

Until those steps are approved and tested, this tool cannot issue licenses. The remaining UI and plan catalog are retained for later integration with the replacement signer.

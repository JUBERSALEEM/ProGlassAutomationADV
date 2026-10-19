# Phase 5 — Updater, installer and software supply chain

**Status:** Partial hardening. Missing publisher identity now blocks classic updater verification, but signed release production and a complete end-to-end update test are still required.

## Implemented on the working branch

- CI runs a NuGet direct/transitive vulnerability audit and fails if the command cannot determine the dependency status or reports known vulnerable packages.
- Existing Dependabot configuration covers NuGet and GitHub Actions dependencies.
- The classic Authenticode verifier now returns an invalid result when neither a publisher thumbprint nor an exact publisher subject is configured.
- Added a regression test proving verification fails closed when no publisher pin is supplied.

## Deliberate limitation

The repository does not establish an approved production code-signing certificate identity. No thumbprint or subject has been guessed or hardcoded. Because the classic updater calls the verifier without pins today, that update path will reject downloads until the owner supplies and approves the real publisher identity and rotation policy. This is an intentional safe failure, not a completed updater rollout.

## Required before release

- [ ] Obtain a production code-signing certificate from the approved publisher and define certificate rotation.
- [ ] Store signing material in an approved protected secret store; never commit a PFX/private key/password.
- [ ] Configure approved thumbprint pin(s) through a controlled release configuration and supply them to every verification call site.
- [ ] Ensure every downloaded executable is verified immediately before the user is allowed to launch it; URL allowlisting is not authenticity verification.
- [ ] Verify Velopack package integrity/trust configuration and confirm the package/update source is the intended official repository.
- [ ] Sign release packages and verify the signature from a clean download on a clean Windows machine.
- [ ] Confirm an unsigned, tampered, expired, wrong-publisher, and correctly signed test package produce the expected block/allow decisions.
- [ ] Run the updater upgrade, cancellation, offline, rollback and data-preservation scenarios.
- [ ] Require a successful Windows test gate on the exact source SHA before release packaging.

**Release gate:** Until real signing and publisher pinning are configured and tested, do not publish the classic updater installer as production-ready.

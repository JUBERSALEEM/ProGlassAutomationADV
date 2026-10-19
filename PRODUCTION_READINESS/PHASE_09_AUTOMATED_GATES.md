# Phase 9 — Automated security testing and release gates

**Status:** CI configuration corrected to avoid a missing-lockfile setup failure; green verification has not yet been observed on the updated commits.

## Implemented in this phase

- Removed `actions/setup-dotnet` package-cache mode from CI and release workflows because this repository does not contain `packages.lock.json`. Previously all .NET jobs failed during SDK setup before restore/build/test could run.
- CI regenerates the derived documentation index and folder navigation before running the documentation consistency checks, so stale generated artefacts in the current branch do not block every subsequent check before source hygiene/build tests can run.
- The NuGet vulnerability job remains fail-closed: if the audit command itself fails, security status is unknown and the job fails; if it reports vulnerable packages, the job fails.

## Required release gates

- [ ] CI green on the exact release candidate SHA: static checks, portable tests, Windows WPF Release build, complete Windows test suite and NuGet vulnerability audit.
- [ ] Vulnerability findings reviewed individually; upgrades and exceptions have owners and expiry dates.
- [ ] Security tests cover publisher pinning, URL allowlisting, authorization negative cases, secret handling, migration failure and restore isolation.
- [ ] Release workflow requires the same successful test gate before packaging.
- [ ] Release signing is implemented using a real publisher certificate and securely stored secret; no placeholder thumbprint/certificate is permitted.
- [ ] Build provenance identifies the source SHA, version, dependency audit and test artefacts.
- [ ] Release assets are verified from a clean download before customer distribution.

## Important limitations

Regenerating derived documentation inside CI validates the generated tree but does not itself ensure the generated changes are committed back to Git. If committed-document freshness must be enforced, add a dedicated check after normalizing and committing generated files.

A green CI run is necessary but not sufficient for production readiness. It cannot replace real installer/upgrade testing, business workflow acceptance, signing verification or a restore rehearsal.

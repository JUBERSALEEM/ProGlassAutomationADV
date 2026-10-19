# Phase 10 — Independent verification and release decision

**Status:** Verification protocol prepared. This phase is not a release approval and cannot be closed until a reviewer examines a stable candidate SHA and attaches evidence.

## Reviewer independence

The reviewer must not rely solely on the implementation notes written by the same agent. Review the actual diff and verify each claim against code, tests and CI artefacts. The audited base branch must remain unchanged until the owner approves a merge.

## Required evidence packet

1. Exact candidate commit SHA, branch and PR diff.
2. Green GitHub Actions CI run for that exact SHA, including Windows WPF build/test and vulnerability audit.
3. List of all changed files and separate commit boundaries by phase.
4. Threat model and unresolved-risk register with severity, owner and disposition.
5. Authorization/licensing negative-test results.
6. Database migration-failure tests, online backup concurrency test and isolated restore drill.
7. PI/Job Order/Delivery/Tax Invoice/Credit Note/Statement reconciliation results using known fixtures.
8. Signed installer verification from a clean download, including exact publisher pin and rotation policy.
9. Clean-machine install, upgrade, rollback/recovery and user-data persistence results.
10. Final sign-off identifying the reviewer, date, exact SHA and decision.

## Mandatory no-go conditions

- CI is red, pending, missing, or for a different SHA.
- Installer packages are unsigned or the publisher is not pinned on every executable-verification path.
- Database migration failure can permit dependent writes.
- Backup/restore is not proven consistent and isolated.
- Critical business invariants are not protected at the persistence transaction boundary.
- A critical/high finding has no documented mitigation and owner approval.
- Any change is described as secure based only on a source scan or documentation update.

## Decision states

- **NO-GO:** one or more mandatory gates fail or evidence is missing.
- **CONDITIONAL:** only explicitly accepted non-critical risks remain, with written owner approval and an expiry/remediation date.
- **GO:** all mandatory gates pass for the exact candidate SHA and an independent reviewer signs off.

Until the evidence packet is complete, the only permitted status is **NO-GO / verification in progress**.

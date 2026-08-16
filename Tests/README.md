# ProGlassAutomation.Tests — Phase P

Security test suite for the ProGlass Agent.

## Running

```bash
dotnet test Tests/ProGlassAutomation.Tests.csproj
```

Or in Visual Studio: Test → Run All Tests. The project is in
`ProGlassAutomation.slnx`.

## Why this project exists

The agent spec required eight security cases. None were written. Worse:

> **No Write or Destructive tool has ever been registered.** The
> permission and confirmation branches that guard them had therefore
> *never executed* — in production or in a test. That code was unproven.

These tests exercise those branches with a **fake write tool that lives
only in the test assembly**. It is deliberately never added to
`AgentToolRegistry`, so the suite proves the gates without widening the
agent's real capability by a single tool.

## Coverage against the spec's eight cases

| Spec case | Where | Notes |
|-----------|-------|-------|
| offline | `SecurityBoundaryTests` | Local-Only blocks egress even to an allowed host |
| cloud-disabled | `SecurityBoundaryTests` | Consent withheld blocks egress; ordering asserted |
| no-API-key | `ProviderFallbackTests` | Consent given but vault empty → still local |
| unauthorized-write | `PermissionGateTests` | Role without permission is denied |
| missing-confirmation | `PermissionGateTests` | No prompt wired → denied; throwing prompt ≠ yes |
| prompt-injection | `SecurityBoundaryTests` | Injection phrases filtered; ordinary text survives |
| corrupted-model | `ProviderFallbackTests` | **See scope note below** |
| interrupted-download | `ProviderFallbackTests` | **See scope note below** |

### Honest scope note on the last two

The spec's *corrupted model* and *interrupted download* cases assume a
local model can be downloaded and loaded. **ProGlass deliberately does
not download models.** `LocalModelAdvisor` only *recommends* hardware, and
`LocalReasoningProvider` answers from the seeded knowledge base with no
model file at all.

So there is no download to interrupt and no model file to corrupt. The
equivalent property worth proving — and what the tests assert — is that
the agent stays **functional and truthful with no model present**, which
is this product's permanent state.

If model download is ever added, these tests must be extended with real
corruption and interruption cases. That is recorded in `Roadmap.md`
rather than being quietly claimed as covered.

## A production change this phase required

`AgentPermissionService` read `AuditService.CurrentUsername` and
`CurrentUser.Role` directly. Those are static, and the only way to set
them is `SwitchUserAsync`, which hits `audit.db` and verifies a password —
so the identity and role branches **could not be exercised by a test at
all**.

The constructor now takes an optional
`Func<(string Username, string Role)>` that **defaults to `AuditService`**.
Production behaviour is byte-for-byte identical; `AgentHost` always
constructs it with the default. This is a seam, not a backdoor: nothing
mutates `AuditService`, and the agent still cannot choose its own
identity.

## Tests that guard the tests

`Sanity_the_role_used_above_really_lacks_the_permission` exists because
the first draft used `AppRoles.Viewer` — which maps to `ViewAll()` and
**does** hold `ProformaView`, so the unauthorized-write test would have
passed while proving nothing. It now uses `Store`, and the sanity test
fails loudly if that ever changes.

## Settings mutation

`SecurityBoundaryTests` and `ProviderFallbackTests` change process-wide
agent settings, so both are in the non-parallel `AgentSettings`
collection. They write to the real `agent-settings.json` under
`%AppData%`, so **run them on a dev machine, not against a configuration
you care about.**

# ProGlass Agent — Security

## Principle map

| Principle | Where it is enforced |
|---|---|
| Least privilege | `AgentPermissionService` composes the app's own `RolePermissions`. The agent has no privileges of its own. |
| Identity binding | The acting username **must equal** `AuditService.CurrentUser`. A mismatch is denied and audited. |
| Fail closed | Every `catch` in the pipeline returns a refusal. No error path results in an action. |
| Explicit consent | Local-Only **and** cloud consent are two separate switches. Local-Only defaults to ON. |
| Allow-list, not block-list | `NetworkWhitelist` names the only reachable hosts; https is mandatory. |
| Untrusted input | DB rows and voice transcripts are sanitized before they can influence reasoning. |
| Secret hygiene | Keys live in DPAPI via `SecretVault`; payloads are redacted, then blocked if they still look secret. |
| Reversibility | Write/Destructive tools require interactive confirmation; destructive wording says **IRREVERSIBLE**. |
| Auditability | Every request, intent, tool decision, teaching change and security event is written to the existing audit trail. |

## Egress gate order

`AgentSecurityService.AllowExternalAsync` — first failure wins:

1. **Local-Only mode** on → deny
2. **Cloud consent** not given → deny
3. **Host** not on the allow-list → deny
4. **Payload** over the cap (default 4000 chars) → deny
5. **Redaction sanity** — payload still matches a secret pattern → deny

Step 5 is deliberate: a payload that still looks like it carries a key is
*blocked*, not cleaned again.

## Prompt injection

`SanitizeUntrusted` strips control characters and replaces known hijack
phrases ("ignore all previous instructions", "you are now", "developer
mode", …) with `[filtered]`, then caps at 8000 characters.

This matters because the agent reads **your database**. A customer name
containing an instruction is data, not a command.

## Secret scan

**0 hard-coded secrets** in `Agent/`. No API key, token or password
literal exists in the subsystem. Keys are only ever read from DPAPI at the
moment of use.

## Capability boundary

The agent subsystem contains:

- **No** `System.Diagnostics.Process` usage — it cannot launch anything
- **No** registry access
- **No** runtime compilation (`CodeDom`, `Roslyn`, `Reflection.Emit`)
- **No** filesystem writes outside `%AppData%/ProGlassAutomation/Agent/`
  and user-chosen export files
- **No** Write or Destructive tool registered in Phase F — the gates exist
  and are enforced, but nothing can currently mutate ERP data

## A realistic statement

This design constrains the agent to a small, reviewable surface. It is not
a proof of security. Specifically:

- The gates are only as good as each tool's declared `Risk`. A future tool
  that under-declares its risk would under-gate itself.
- Redaction is pattern-based. A novel secret format could pass it.
- Local-Only is honoured by the agent's own code paths; it does not
  firewall the process.
- The permission model inherits any weakness in the app's existing role
  definitions.

What it *does* guarantee is that every action is registered, gated,
audited, and refusable — and that the failure direction is always inward.

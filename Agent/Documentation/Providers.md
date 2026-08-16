# ProGlass Agent — Providers

## Selection rule

```
LOCAL, unless:
   Local-Only is OFF        and
   cloud consent is GIVEN   and
   a key is stored for the selected provider
```

Any failure, exception or missing piece → **local**. There is no path
where an error results in more data leaving the machine; the failure
direction is always inward.

## Providers

| Provider | Local | Host |
|---|---|---|
| `LocalReasoningProvider` | yes | — |
| Gemini | no | `generativelanguage.googleapis.com` |
| Groq | no | `api.groq.com` |
| Cohere | no | `api.cohere.com` |

The three cloud providers are **thin subclasses** of
`CloudProviderAdapter`. They exist to pin one thing: the host. All
transport, gating and error handling stay in the base class, so a new
provider cannot introduce a second unguarded network path.

## Cloud call order

1. Host on the allow-list? (else refuse before anything is prepared)
2. Redact the payload
3. Boundary check — `AllowExternalAsync`
4. Only then `IAiService.AskAsync`

The adapter wraps the app's **existing** `IAiService` rather than opening
its own sockets, inheriting proven HTTP handling.

## Credentials

Stored through `AgentSecretStore` → `Services/Security/SecretVault`
(Windows DPAPI, per-user), under the `agent.provider.` prefix. The wrapper
adds nothing cryptographic — deliberately, since re-implementing secret
storage would be a new attack surface. It adds only namespace isolation.

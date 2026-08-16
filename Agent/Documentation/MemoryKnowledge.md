# ProGlass Agent — Memory & Knowledge

## Two memory tiers, deliberately separate

| Tier | Where | Cap | Persisted |
|---|---|---|---|
| Conversation | RAM | 50 turns | **No** — session only |
| Approved facts | `agent.db` | — | Yes, via `AddApprovedAsync` only |

Nothing said in passing is written to disk. The only path to persistence is
an explicit "remember this". The two tiers clear independently, so "forget
this conversation" and "forget everything about me" are different, honest
actions.

## Knowledge base

Curated domain facts the **local** provider answers from. This is what
makes the agent useful with zero internet.

### Seeded facts (idempotent)

| Title | Substance |
|---|---|
| UAE VAT | 5%; net = gross / 1.05 |
| Glass thicknesses | 3, 4, 5, 6, 8, 10, 12, 15, 19 mm |
| Jumbo sheet sizes | 2440×1830, 3210×2250, 3660×2440; SQM = W×H / 1e6 |
| Quoting per SQM | Sell must cover purchase/SQM + processing + margin |
| DGU naming | outer + `<n>mm ASP` + inner |
| Master delivery note | Holds the running ordered-minus-delivered balance |

Seeding checks for the `proglass-core` category first, so calling it on
every page load is safe.

## Export / import

- **Export** — deliberate and audited; the user is choosing to take data out.
- **Import** — validated per row (title and content required) and capped at
  **500 rows**, so a pasted blob cannot balloon `agent.db`.

## Retrieval

Whole-phrase substring match first, then per-word (≥3 chars) fallback,
capped at 8 results. If nothing matches, the local provider **says so**
rather than guessing.

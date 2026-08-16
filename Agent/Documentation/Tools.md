# ProGlass Agent — Tools

Tools are the **only** way the agent touches the application. There is no
reflection-based discovery and no dynamic registration: the attack surface
is exactly `Tools/Registry/AgentToolRegistry.cs`.

## Registered tools

13 tools plus navigation. Verified against
`Tools/Registry/AgentToolRegistry.cs` — if this table and the registry
disagree, the registry is right and this file is stale.

### Read (12)

| Tool | Permission | What it does |
|---|---|---|
| `search_proforma` | `ProformaInvoice.View` | Proformas by number / customer / project |
| `search_joborder` | `JobOrder.View` | Job orders |
| `search_delivery` | `DeliveryNote.View` | Delivery notes |
| `search_taxinvoice` | `TaxInvoice.View` | Tax invoices |
| `search_customer` | `ProformaInvoice.View` | Saved clients |
| `get_inventory` | `SheetStore.View` | Types, available SQM, low stock (≤2) |
| `get_activity` | `Audit.View` | Last 5 audited actions |
| `get_profitability` | `ProformaInvoice.View` | Profit / cost / margin per job |
| `get_document_chain` | `ProformaInvoice.View` | PI → JO → DN → TI, with delivery progress |
| `get_statement` | `TaxInvoice.View` | Saved statements: invoiced, outstanding, overdue |
| `get_sales_analytics` | `ProformaInvoice.View` | Period revenue, cost, profit, margin; glass cost by type |
| `get_screen_context` | **none** | Active module, recent screens, current theme |

`get_screen_context` is the **only** permissionless tool, because it
exposes no ERP data — just the module name already on screen and the
theme. Inventing a permission for "may see the window you already have
open" would be security theatre. `OfflineBehaviourTests` asserts it is the
only one, so a future permissionless tool fails the build.

### Write (1)

| Tool | Permission | Confirmation | What it does |
|---|---|---|---|
| `update_sheet_stock` | `SheetStore.Edit` | **required** | Adds or consumes stock on ONE sheet |

Capped at 500 per call, 15s timeout, reversible by its inverse, audited
with before/after values. See `WriteTools.md`.

### Destructive (0)

**There is still no destructive tool.** Nothing the agent can reach
deletes a record.

## Reachability — a tool must be routable, not just registered

A registered tool that no sentence can reach is a dead feature. Two
shipped that way and neither failed a test:

- `update_sheet_stock` (Phase Q) — intent keywords were `"add stock"` /
  `"add to sheet"`, but its parser needs a quantity between verb and
  target (`"add 10 to sheet 42"`), so no keyword match could ever fire.
- The five `search_*` tools — matched only `"search X"` / `"find X"`, so
  every natural phrasing (`"show delivery notes"`, `"list job orders"`)
  fell through to the reasoning provider, which answers from general
  knowledge instead of the ERP.

Both were found by hand, because every other test called `ExecuteAsync`
directly and so proved the tool WORKS without proving it is REACHABLE.
`ToolReachabilityTests` now enumerates the registry and fails by name if
any tool cannot be reached from a realistic sentence.

## Answer provenance

Tools that compute rather than quote attach an `AnswerProvenance`
(`Known` / `Calculated` / `Inferred` / `Unknown`). `Unknown` is the
default so an unfilled provenance never reads as authoritative, and
`Known` carries no hedge — qualifying plain facts trains users to ignore
qualifiers.

## Risk tiers

| Risk | Permission | Confirmation |
|---|---|---|
| `Navigation` | none (view change only) | no |
| `Read` | module `.View` | no |
| `Write` | required, else denied | **always** |
| `Destructive` | required, else denied | **always**, worded IRREVERSIBLE |

Risk is a required field on `ToolDefinition`. A new tool cannot bypass the
gates by omission.

## Contracts every tool honours

- **Never throws** — returns `ToolResult.Fail` with a truthful message
- **Capped results** — 10 rows per search, 10 low-stock lines
- **Truthful failure** — "I could not read the job orders" rather than a
  silent empty list that reads like "there are none"

That last point is the one that matters most: an agent that says "no
results" when the database was unreachable is worse than useless.

## Inventory maths

Available SQM is computed exactly as the Sheet Store module does:

```
Σ sheetSqm × (stock − used)
```

so the agent can never quote a figure that disagrees with the screen.

## get_profitability (Phase N)

Reports profit, cost and margin for saved proforma invoices.

- **Risk:** Read · **Permission:** `ProformaView`
- **Argument:** optional. An invoice number, customer or project name.
  With no argument it reports the lowest-margin jobs and how many are
  running at a loss.

**Reads, never recalculates.** Step 9 already computes profit and the
mapper persists it; this tool deserialises the same snapshot the Job
Profitability module reads, via the same
`ProformaInvoiceMapper.DeserializeSnapshot` path. The agent and the module
therefore cannot quote different numbers.

**Truthfulness rules:**

| Case | Behaviour |
|------|-----------|
| Invoice saved before Step 9 existed | Reported as *not costed* and excluded — never as 100% margin |
| Repository unavailable | Says so, rather than "no results" (which reads as "no profitable jobs") |
| Negative profit | Stated as `LOSS AED n`, not buried in a signed number |

**Intent routing note:** the navigation catalog also owns the word
"profit" (it opens the Job Profitability module), and tools are matched
*before* navigation. The tool's keys are therefore phrased as questions —
"how much profit", "did we make money", "margin on" — so a question
returns figures while "open profitability" still navigates.

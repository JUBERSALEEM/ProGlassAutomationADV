# ProGlass Agent — Tools

Tools are the **only** way the agent touches the application. There is no
reflection-based discovery and no dynamic registration: the attack surface
is exactly `Tools/Registry/AgentToolRegistry.cs`.

## Registered in Phase F

| Tool | Risk | Permission | What it does |
|---|---|---|---|
| `navigate` | Navigation | — | Opens a module screen |
| `search_proforma` | Read | `ProformaInvoice.View` | Proformas by number / customer / project |
| `search_joborder` | Read | `JobOrder.View` | Job orders |
| `search_delivery` | Read | `DeliveryNote.View` | Delivery notes |
| `search_taxinvoice` | Read | `TaxInvoice.View` | Tax invoices |
| `search_customer` | Read | `ProformaInvoice.View` | Saved clients |
| `get_inventory` | Read | `SheetStore.View` | Types, available SQM, low stock (≤2) |
| `get_activity` | Read | `Audit.View` | Last 5 audited actions |

**No Write tool. No Destructive tool.** The gates that would govern them
are implemented and enforced, but nothing can currently mutate ERP data
through the agent.

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

# Write Tools (Phase Q)

## Status

The agent has **one** write tool: `update_sheet_stock`.
It has **no** destructive tool. Nothing the agent can reach deletes a record.

## Why this tool, and only this tool

Giving an agent write access is the point where a helpful assistant becomes
able to damage a business. The first write was chosen for the smallest
possible blast radius:

| Property | `update_sheet_stock` |
|---|---|
| Records touched | Exactly one glass sheet, per call |
| Fields touched | `Stock` and `Used` counts only |
| Reversible? | Yes — by the inverse operation |
| Bulk capable? | No, by construction |
| Deletes anything? | No |
| Touches pricing? | No |
| Touches customers or documents? | No |

Sheet creation, deletion, price edits and anything invoice-related are
deliberately **not** implemented. Each would need its own tool and its own
review.

## The gate chain

A stock change only happens if **every** one of these passes, in this order:

1. **Security** — the request is sanitized before intent detection.
2. **System** — the tool is registered in `AgentToolRegistry`.
3. **App permissions** — the acting user must equal the signed-in user
   (`AuditService.CurrentUser`) *and* hold `SheetStore.Edit`.
4. **Confirmation** — `RequiresConfirmation = true`, so the injected prompt
   must return an explicit Yes. No prompt wired up means denied.
5. **Business rules** — the parser, the per-call cap, and the repository's
   own balance check.

Failing any step changes nothing and says why.

## Input contract

Accepted forms only:

```
add 10 to sheet 42
buy 10 sheet 42
receive 25 to sheet 7
use 5 from sheet 42
consume 5 sheet 42
issue 3 from sheet 19
```

Anything else is refused. This is intentional: an ambiguous instruction that
changes inventory is worse than no answer. `add 10 to all sheets` does not
parse, and cannot.

Limits:

- Quantity must be a whole number, `1`–`500`.
- One sheet id per call.
- The operation is abandoned after 15 seconds.

## Intent precedence

`get_inventory` is matched **before** `update_sheet_stock` in
`IntentDetector`. Both mention "stock", and that ordering is the safe one:
a vague sentence resolves to the read tool. Only an explicit
add / buy / receive / use / consume / issue verb reaches the write tool.

## Audit

Every successful change writes an entry through
`AuditService.LogWithChanges` containing:

- Section `Sheet Store`, action naming **ProGlass Agent** as the actor
- Entity `GlassSheet` and the sheet id
- Before/after values for `Stock`, `Used` and `Balance`
- A reason recording that the user confirmed it in the agent

The before/after pair is what makes the change reversible from the log
alone.

## Failure behaviour

- **Sheet not found** — refuses, states the id, changes nothing.
- **Insufficient balance** — `UseStockAsync` throws; the tool catches it and
  reports the available quantity. Nothing is written.
- **No observable change** — reported as *not saved* rather than as success.
- **Timeout or unknown error** — the tool says it *could not confirm whether
  the change was saved* and tells the user to check Sheet Store. It does not
  claim success, and it does not claim failure.

That last case matters: after an ambiguous exception the honest answer is "I
don't know", not a guess in either direction.

## Testing

`Tests/Agent/WriteToolTests.cs` covers the declared contract and every
refusal path. All of those assertions return before a database is touched.

The **successful write path is not unit-tested** — it would need a real
`AppDbContext` and would mutate inventory. It is on the manual regression
checklist instead. A passing test that did not exercise the real write would
be a false assurance.

# ProGlass Agent — Deliverable Report

Answers the master prompt's §58 checklist. Written against the code as it
stands, not against what was planned.

**Rule applied throughout: nothing is listed as implemented unless it is
in the repository and reachable.** Anything planned, partial or advisory
is in *Known limitations* instead.

---

## 1. Existing agent capabilities

| Area | State |
|---|---|
| Deterministic intent detection | Shipped — no LLM in the routing path |
| Tool registry, risk-tiered | Shipped — 12 Read, 1 Write, 0 Destructive |
| Permission gate | Shipped — reuses the app's `AppPermissions` / `AuditService` |
| Confirmation gate | Shipped — mandatory for Write and Destructive |
| Audit | Shipped — every request, intent, tool run and denial |
| Teaching (approval-gated) | Shipped — inert until explicitly approved |
| Memory | Shipped — 50-turn session RAM; disk only on approval |
| Knowledge | Shipped — seeded, import capped at 500 rows |
| Security modules | Shipped — `DataBoundary` `Consent` `Redaction` `SecretProtection` `Network` `Integrity` |
| Local-only default | Shipped — ON by default; re-enabling clears consent |
| Voice | Shipped — Whisper readiness bridge, TTS, listen→handle→speak |
| Provider abstraction | Shipped — local default, cloud adapters gated |

## 2. New capabilities added in this phase set

| Capability | Tool / file |
|---|---|
| Document chain PI → JO → DN → TI | `get_document_chain` |
| Saved customer statements | `get_statement` |
| Period revenue / profit / margin | `get_sales_analytics` |
| Screen + theme awareness | `get_screen_context`, `Context/AgentScreenContext.cs` |
| "It" resolves to the open document | `Context/AgentReferenceResolver.cs` |
| Answer provenance and confidence | `Context/AnswerProvenance.cs` |
| Opt-in module context | `Context/IAgentContextAware.cs` (5 modules) |
| Stock adjustment | `update_sheet_stock` |

## 3. Missing capabilities

- **Local LLM reasoning (§39)** — `LocalModelAdvisor` recommends a model
  based on RAM and CPU; **no model is downloaded and none runs**. Local
  reasoning is knowledge-backed retrieval, not generation.
- **Margin per glass type (§33)** — deliberately not built. Revenue is
  recorded per invoice, not per pane, so attributing profit to a glass
  type needs an allocation rule the ERP does not define. Cost share per
  type is provided instead.
- **Write coverage beyond stock** — no document creation, no price edits,
  no deletion.
- **Payment status** — not reported anywhere, because the ERP does not
  record it. Invoice `Status` is labelled as the document's own field.

## 4. Files created

`Agent/` — 49 `.cs` across Configuration, Context, Core, Intent,
Interfaces, Knowledge, Memory, Permissions, Planning, Providers, Rules,
Security, Storage, Teaching, Tools, UI, Voice — plus 14 documentation
files.

`Tests/Agent/` — 11 files, 183 test cases.

## 5. Files modified (ERP side)

Kept as small as the work allowed:

| File | Why |
|---|---|
| `MainViewModel.cs` | Publishes screen context; supplies the live view-model for pull-based detail |
| `ProformaInvoiceViewModel.cs` | Opt-in context (proforma number only) |
| `SheetStoreViewModel.cs` | Opt-in context (sheet spec only, never price) |
| `JobOrderViewModel.cs` | Opt-in context, detail view only |
| `DeliveryNoteViewModel.cs` | Opt-in context, detail view only |
| `TaxInvoiceViewModel.cs` | Opt-in context, detail view only |
| `TaxInvoiceDetailViewModel.cs` | Added `EditingInvoiceNumber` read-only accessor |

No ERP behaviour was changed by any of these. Each adds a property that
is read only by the agent.

## 6. Files reused, not duplicated

`ProformaInvoiceRepository` · `JobOrderRepository` · `DeliveryNoteRepository`
· `TaxInvoiceRepository` · `StatementRepository` · `SheetStoreRepository`
· `SheetStorePricingService` · `AuditService` · `AppPermissions`
· `AiScreenContext` · `ThemeManager` · `AccentPresets`
· `StatementAggregator` (its stored output, never `BuildAsync`)

The agent holds **no ERP `DbContext`**, executes **no** SQL against ERP
data, and keeps no second copy of any business rule. It does own one
`AgentDbContext`, but that is `agent.db` — its own teaching, memory and
knowledge store — and it never touches `proglass.db`, `audit.db` or any
other ERP database.

(The first draft of this report claimed "no `DbContext`" outright. That
was checked against source before publishing and corrected: an overbroad
security claim is still a false one.)

## 7. Files removed

None.

## 8. Database changes

None to any ERP database. The agent owns `agent.db` (teaching, memory,
knowledge) at `%AppData%/ProGlassAutomation/Agent/`, created lazily on
first use.

## 9. Security changes

- `Security/` split into six named modules; `AgentSecurityService`
  delegates to `Redaction` so there is exactly one implementation.
- Egress order is fixed and fail-closed: local-only → consent →
  allow-list → payload cap → redaction sanity.
- Business data is blocked from cloud **even with consent** — agreeing to
  use a cloud model is not agreeing to upload the customer list.
- Context layer is RAM-only, capped, and cleared with the conversation.

## 10–12. Providers · Whisper · Theme

Providers unchanged in behaviour: local default, cloud gated.
Whisper unchanged; the chat now checks readiness **before** opening the
microphone so "model not downloaded" is distinguishable from "heard
nothing".
Theme is **read-only**: `ApplyTheme`, `ToggleTheme`, `ApplyAccent` and
`ResetAccent` are not referenced anywhere in `Agent/`.

## 13. Document relationship mapping

Read from the entities, not assumed:

```
SavedJobOrder.SourceProformaId          -> PI
SavedDeliveryNote.ProformaId/.JobOrderId-> PI / JO
SavedTaxInvoice.ProformaId              -> PI
TotalOrdered/Delivered/BalanceQty       -> delivery progress (Master DNs)
```

## 14. Business-data mapping

Profit uses the shipped Step 9 rule, unchanged and unduplicated. When the
snapshot has no usable net, **both** net and profit fall back to the
invoice header — never one from each.

## 15–16. Permissions · Privacy

Read requires the module's `.View`. Write requires `SheetStore.Edit`
**and** confirmation. `get_screen_context` is the only permissionless
tool, because it exposes no ERP data; a test asserts it stays the only
one.

Local-only is the default. Nothing leaves the machine without an explicit,
separate consent, and business data does not leave at all.

## 17. Tests performed

183 cases across 11 files. Static verification only — 275 C# files
brace-clean, 122 XAML files well-formed, all 13 tools verified reachable.

## 18. Build status

**NOT COMPILED.** There is no .NET SDK in the development environment
used for this work. Every claim above is from source inspection and
static analysis. One compile error (`CS1503`) did reach the user during
this work and was fixed on report; others may exist.

## 19. Known limitations

1. Not compiled or run — see §18.
2. **Saved invoices keep their pre-fix P&L figures.** The DGU inner-pane
   bug understated cost on every double-glazed job; the fix corrects
   future calculations only. Each affected invoice must be reopened,
   Step 9 recalculated, and saved. Stored financial history is not
   rewritten silently.
3. Successful write path is not unit-tested — it needs a live
   `AppDbContext` and would mutate inventory. Manual checklist instead.
4. No local LLM; `Integrity` is built and tested but not yet load-bearing.
5. Unit tests cannot prove no socket was opened. The real offline test
   (pull the cable, launch, use) stays manual.

## 20. Recommended next phase

1. **Build it.** Everything here is statically verified and unproven at
   runtime.
2. Walk `RegressionChecklist.md` on Windows.
3. Recalculate Step 9 on active invoices to correct stored profit.
4. Then, and only then, consider more write tools.

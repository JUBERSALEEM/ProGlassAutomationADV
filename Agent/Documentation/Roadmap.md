# ProGlass — Upcoming Phases

Written 16 Aug 2026 · HEAD `d371f63` · branch `arena/01a008cc-proglassautomationadv`

Every item below was verified against the repo, not recalled. Where the
work depends on a decision only you can make, it says so instead of
guessing.

---

## Where we actually are

**Delivered A–O.** Pricing (A–C), audit (D), global search (E), the Agent
subsystem (F), sheet provenance (G), LAM parity (J), Settings/layout
defect fixes, and the agent wiring + chat + profitability (K–O).

Verified state right now:

| Check | Result |
|-------|--------|
| Agent `.cs` files | 36 |
| Registered tools | 8 read + 1 navigation. **Zero Write, zero Destructive** |
| Agent touchpoints outside `Agent/` | 2 — `MainViewModel.cs`, `SettingsAgentSection.xaml.cs` |
| `Tests/` directory | **Does not exist** |
| Committed secrets | **None.** The two `AIza`/`bearer` hits are a UI placeholder and the redaction patterns themselves |
| Solution projects | 1 (`ProGlassAutomation.csproj`) — no test project |

---

## Phase P — `Tests/Agent/**` · the security test suite

**The largest remaining gap, and the one your spec was most explicit
about.** Eight required cases, none written:

offline · cloud-disabled · no-API-key · unauthorized-write ·
missing-confirmation · prompt-injection · corrupted-model ·
interrupted-download

**Why it matters more than it sounds:** the permission and confirmation
gates have *never executed against a Write tool*, because no Write tool
exists. That machinery is unproven. Phase P proves it with a test-only
fake Write tool that is never registered in the real registry.

Work: add `ProGlassAutomation.Tests` (xUnit, net8.0-windows), add it to
`ProGlassAutomation.slnx`, write the 8 cases plus redaction and
whitelist coverage.

**Decision needed from you:** adding a second project changes your build.
If you'd rather keep a single-project solution, say so and I'll write the
tests as a `#if DEBUG` self-check harness instead.

---

## Phase Q — Write tools, behind the gates

Only after Phase P proves the gates. Candidates, smallest blast radius
first:

1. `create_proforma_draft` — Write, `ProformaView`, confirmed
2. `update_sheet_stock` — Write, `SheetStoreView`, confirmed
3. `set_invoice_status` — Write, confirmed

Each: audited before and after, reversible, never bulk.

**Decision needed:** which of these you actually want the agent doing.
My recommendation is to start with **one** and live with it for a week.

---

## Phase R — Step 6 "Other Charges"

You deferred this pending the optimisation module. The folder exists
(`CustomCharges/`, `Wastages/`) and is wired into the wizard. Ready when
you unblock it — I won't touch it until you do.

---

## Phase S — DGU+LAM pricing

Still a deliberate placeholder: empty VM, "COMING SOON" view, APPLY
refuses to save. **This is a business decision, not a coding one.** I need
from you:

- Does the laminated pane replace the **outer** or the **inner** pane?
- Does the ASP-inclusive rule (45 AED @ 12mm covers cutting/tempering)
  still apply to the hybrid?
- Is the PVB interlayer priced per SQM as in LAM?

Give me those three answers and it's about an hour's work.

---

## Phase T — Agent hardening to the letter of your spec

Your spec named six security components under `Security/`. Present:
`Network/`, `SecretProtection/`. **Missing as named modules:**
`DataBoundary/`, `Consent/`, `Redaction/`, `Integrity/`.

Their *behaviour* mostly exists inside `AgentSecurityService`, so this is
partly a refactor for auditability — you should be able to point at one
file per guarantee. `Integrity/` is genuinely absent (model/file hash
verification) and only matters once local models are downloadable.

---

## Also outstanding (small, not phase-sized)

- **LAM production formula is my inference** — PVB + cutting + tempering
  on both panes, per SQM. Confirm or correct it.
- **Step 4 default sheet** is the *largest* stocked. Confirm, or switch to
  cheapest / last-used.
- **Deviation report** — shipped Phase F vs your 80-section agent script.
  Still owed to you.

---

## Recommended order

**P → (your answers on S) → Q → T**, with R when the optimisation module
lands.

P first because everything in Q rests on gates that have never run.

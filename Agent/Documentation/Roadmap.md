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

## ~~Phase P — `Tests/Agent/**`~~ · **DELIVERED**

Shipped. `Tests/ProGlassAutomation.Tests.csproj` (xUnit, net8.0-windows),
added to `ProGlassAutomation.slnx`, ~35 tests across three files covering
all eight spec cases. See `Tests/README.md`.

Two findings worth carrying forward:

1. **The gates were untestable.** `AgentPermissionService` read
   `AuditService` statics that only `SwitchUserAsync` (DB + password) can
   set. Added an optional identity provider defaulting to `AuditService` —
   production behaviour unchanged.
2. **`Tests/**` had to be excluded from the app project.** The app csproj
   sits at the repo root, so default globbing would have compiled the
   tests into the shipped binary and created a circular reference.

*Corrupted-model* and *interrupted-download* are covered as "no model
present", because this product never downloads models. If that changes,
these tests must be extended — recorded here rather than claimed.

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

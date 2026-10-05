# ProGlass Automation — Document-Lifecycle Hardening Analysis
### Delete-lock, immutability & the "force-update vs delete" decision
**Date:** 2026-09-09 · **Branch:** `arena/01a08606-proglassautomationadv` · **Baseline:** `8e454cd`
**Author role:** senior developer review — how I would design the PI → JO → DN → TI → Statement lifecycle if I owned ProGlass

---

## 0. TL;DR — the answer to your question

> *"…what should be able to delete, what should be delete-lock, keep force-update instead of deletion — as a senior developer, force-update or deletion?"*

**Neither "delete everything" nor "force-update everything." The correct industrial-ERP answer is a status-gated hybrid, and ProGlass already has 80% of the machinery to do it.**

The one-line rule an ERP lead enforces:

> **A document is freely editable and deletable while it is a DRAFT and has no children. The moment it is *posted* (issued / released / dispatched / approved) or gains a downstream child, it becomes immutable — you do not delete it and you do not silently overwrite it; you *reverse* it (cancel / void / credit-note) and the reversal is itself an audited, permanent record.**

So the honest senior recommendation is:

| Question | Answer |
|---|---|
| Force-update **or** delete? | **Force-update (posted docs) *and* delete (drafts only)** — the choice depends on the document's state, not a global preference. |
| Hard delete? | **Only for Draft, childless documents.** Everything else = soft-close (Cancel/Void) so books, VAT returns and audit trails stay intact. |
| What replaces "delete" for posted docs? | **Void / Cancel status + (for Tax Invoices) a Credit Note.** The number is *never* reused. |

This document proves that against the actual code and turns it into shippable phases.

---

## 1. What ProGlass already does right (verified in code, do NOT rebuild)

This codebase is far more mature than a typical "add ERP rules" request assumes. Before recommending anything I traced the real delete/update paths. The following already exist and work:

1. **Cross-database orphan protection** — `Services/Database/DependencyGuard.cs`. Before any PI/JO/DN delete, it checks the *real* dependent records in the other SQLite files (PI→JO/DN/TI, JO→DN/TI, DN→TI) and **blocks with an explanatory message**. It is wired into all four list/detail delete commands (verified: `SavedInvoicesListViewModel.Part1.cs:740`, `SavedJobOrdersListViewModel.Part1.cs:503`, `DeliveryNoteListViewModel.Part2.cs:215`, `DeliveryNoteDetailViewModel.Part3.cs:53`).
2. **Centralised status-transition state machine** — `Services/Workflow/StatusTransitions.cs` — one authoritative table per document type; invalid transitions are rejected at the service boundary (`TaxInvoiceValidationService`, `DeliveryNoteValidationService`, `TaxInvoiceLineService`) *and* the UI. This is the exact "everything works on points" backbone you're asking for.
3. **Multi-DB operation coordinator with compensation + reconciliation** — `Services/Workflow/DocumentOperationCoordinator.cs`: correlation IDs, `Result<T>`, idempotent DN-invoiced marking, and a startup reconciler (`TaxInvoiceDnReconciler`) that heals a half-finished TI↔DN link. This is genuinely senior-grade.
4. **Service-layer authorization that the UI cannot bypass** — every repository `DeleteAsync` calls `OperationGuard.Demand(permission)` **and** `ReadOnlyGate.Demand(...)` (license read-only mode). Role→permission matrix in `Services/Audit/Permissions.cs`.
5. **Immutable audit trail** — `AuditService.Log(section, action, ...)` writes to a separate `audit.db`; every delete already logs (`AuditSections.Proforma, "Invoice deleted", …`).
6. **Immutable historical snapshots** — JO stores an immutable `ProformaInvoiceSnapshot` at conversion; TI stores `TaxInvoiceSnapshot`; profitability pins a `GlassPriceStamp` so reports flag stale cost basis instead of lying.
7. **Upstream lifecycle sync on downstream delete** — deleting a JO or DN calls `ProformaLifecycleSync.AfterDownstreamChangeAsync(...)` so the parent PI's derived status recovers.

**Conclusion:** ProGlass is not missing the *plumbing*. It is missing a **consistent policy on top of the plumbing.** The gaps below are policy gaps, not architecture gaps — which is exactly why they're cheap to close.

---

## 2. The real gaps (evidence-based findings)

Ordered by financial/compliance risk. Each is a fact I verified in the tree, not a guess.

### 🔴 G-1 — All deletes are HARD deletes, including posted Tax Invoices
`TaxInvoiceRepository.DeleteAsync` does `ctx.TaxInvoices.Remove(entity)` — a physical row delete — gated only by `TaxInvoiceEdit` + `ReadOnlyGate`. A **Tax Invoice is a legal fiscal document** (UAE FTA: a business must retain tax invoices for 5 years; a issued invoice may only be corrected by a **Tax Credit Note**, never deleted). Today an Accountant can permanently erase an *Issued/Paid* invoice, leaving a hole in the VAT submission with **no trace of the number ever existing.**
*Impact:* audit/compliance failure; VAT return understated; number sequence has an unexplained gap.

### 🔴 G-2 — No "posted / locked" concept anywhere
Grep for `IsLocked / IsPosted / IsDeleted / Void` across `Models` + `Services` returns **nothing** (only `AppUser.LockedUntilUtc` for auth lockout). There is:
- no soft-delete column (`IsDeleted` / `DeletedAtUtc` / `DeletedBy`),
- no immutability flag that says "this document is posted; body edits are forbidden."

So the well-built `StatusTransitions` table controls the *status field* but nothing stops a user from **re-opening a saved TI and rewriting the line items / totals** of an already-Issued invoice. `InvoiceLifecycleStateMachine.IsEditable(state)` exists (`Services/ProformaInvoice/InvoiceLifecycleStateMachine.cs:247`) but is **enforced nowhere** — grep shows zero call sites. The lock is written but never armed.

### 🔴 G-3 — No Credit Note / reversal document
There is no way to *correct* an issued invoice except editing it (G-2) or deleting it (G-1). Every real ERP answers "the invoice is wrong but already sent" with a **Credit Note** that references the original. ProGlass has `InvoiceType = "Full"` on the model but no reversal type and no credit-note flow.

### 🟠 G-4 — DependencyGuard fails OPEN
`DependencyGuard.CheckAsync` wraps everything in `catch { return DependencyCheckResult.Ok(); }` (documented as deliberate "never block a valid delete because the check failed"). Defensible for a Draft, **dangerous for a posted parent**: if the dependency query throws (locked DB, transient), the guard *allows* a delete that would orphan a Tax Invoice. For fiscal parents the correct posture is **fail-closed** ("couldn't verify dependents — try again"), not fail-open.

### 🟠 G-5 — Number sequences are `MAX(existing)+1` → silent renumber after delete
DN/TI/Statement/OrderReport all compute the next number as `MAX(...) + 1` (`TaxInvoiceRepository.cs:254`, `DeliveryNoteRepository.Part1.cs:882`, `StatementRepository.cs:215`). Combined with hard delete (G-1), deleting the highest invoice and creating a new one **re-issues the same number** to a different document — the cardinal sin of fiscal numbering (two distinct invoices, same number, across time). Soft-delete/void fixes this automatically because the row (and its number) stays.

### 🟠 G-6 — TI delete has no dependency guard and no dedicated permission
`DependencyGuard` treats TaxInvoice as "a leaf" (`Kind.TaxInvoice => Ok()`), which is true *structurally*, but the TI delete path (`TaxInvoiceDetailViewModel.cs:184`, `TaxInvoiceListViewModel.cs:438`) also has **no status check** — an *Issued* or *Paid* invoice deletes as easily as a Draft. And there is no `TaxInvoice.Delete` permission; it rides on `TaxInvoiceEdit`, so anyone who can edit a line can destroy the whole fiscal document.

### 🟡 G-7 — No optimistic concurrency (lost-update risk)
No `RowVersion` / concurrency token in any DbContext. In multi-user shared-DB mode, two users editing the same PI → last-write-wins, silently clobbering the first. Low frequency, high surprise.

### 🟡 G-8 — Delete/void has no "reason" capture
Audit logs *that* a delete happened but not *why*. Industrial ERP requires a mandatory reason string on any cancel/void/delete of a posted document (it's the first thing an auditor asks).

---

## 3. The policy I would enforce (the "points" matrix)

This is the deletion/immutability contract, per document, per state. **This table is the spec.**

Legend: **DEL** = hard delete allowed · **VOID** = soft-close (status→Cancelled/Void, row kept) · **EDIT** = body editable · **LOCK** = body frozen, status-only moves · **CN** = correct via Credit Note.

| Document | Draft (no children) | Draft (has children) | Posted / issued | Terminal (Paid/Delivered/Received) |
|---|---|---|---|---|
| **Proforma Invoice** | DEL + EDIT | EDIT; delete blocked (guard) | Approved → LOCK; Cancel (VOID) allowed | Cancel only |
| **Job Order** | DEL + EDIT | delete blocked | Released/InProduction → LOCK; Reject (VOID) | Delivered → Reject only |
| **Delivery Note** | DEL + EDIT | delete blocked | Dispatched → LOCK; Cancel (VOID) | Signed/Received → **no change** (already correct in table) |
| **Tax Invoice** | DEL + EDIT | delete blocked | Issued → **LOCK, never DEL**; Void (VOID) + **CN** | Paid → **CN only** |
| **Statement** | regenerate freely (derived doc) | — | reissue supersedes | — |

Key senior calls encoded above:
- **Force-update vs delete is state-dependent, not global.** Drafts: delete. Posted: never delete — lock the body, move the status, reverse with Void/Credit Note.
- **A childless Draft is the *only* thing that should ever hard-delete.** Everything else soft-closes so the number, snapshot and audit line survive.
- **Statements are derived** (regenerated from live TI/DN data) so they don't need void semantics — reissue supersedes. That's why the DeliveryNote transition table already ends `Received → {Received}` (correctly terminal). Good instinct already in the code; we generalise it.

---

## 4. Phased delivery plan

Same discipline as the ERP roadmap already in this repo: **one phase per landing, traced before touched, statically verified, committed + pushed separately, nothing breaks the existing pipeline.** Phases are ordered by risk-reduction per unit of effort.

> Build constraint reminder: this repo is `TreatWarningsAsErrors`, no dotnet CLI in the sandbox → every phase verified by code review + brace/XML balance, and the existing xUnit suite is extended (never weakened).

### PHASE 0 — Policy spec + guard rails (this document) ✅
- This file is the authoritative policy (§3 table).
- **No code.** Deliverable is the shared contract so every later phase is measured against it.

### PHASE 1 — Soft-delete + Void foundation *(highest value, lowest blast radius)*
**Goal:** stop destroying fiscal history; make "delete" mean "void" for posted docs.
1. Add nullable lifecycle columns to the four saved-doc tables via the existing idempotent migration-runner pattern (`SchemaMigrations`): `IsVoid` (bool, default 0), `VoidedAtUtc`, `VoidedBy`, `VoidReason`. Additive, non-breaking; fresh DBs via `EnsureCreated`, existing via `CREATE ... IF NOT EXISTS`/`ADD COLUMN` guard.
2. Introduce `IDocumentLifecyclePolicy` (one class, pure/testable) that answers `CanHardDelete(kind, status, hasChildren)` and `CanEditBody(kind, status)` straight from the §3 table — the single authority, mirroring how `StatusTransitions` centralises transitions.
3. Repositories gain `VoidAsync(id, reason)` (status→Cancelled/Void, set audit columns, keep row). List/detail queries default to hiding voided rows with a "Show voided" toggle (reports/VAT still see them).
4. **Route the delete commands through the policy:** Draft+childless → existing hard `DeleteAsync`; everything else → `VoidAsync` with a **mandatory reason dialog** (closes G-8).
*Acceptance:* deleting an Issued TI becomes a Void (row + number retained, reason logged); deleting a Draft PI still hard-deletes; VAT submission still sees voided invoices as voided (not missing).

### PHASE 2 — Arm the body-edit lock (close G-2)
**Goal:** a posted document's body can no longer be silently rewritten.
1. Enforce `IDocumentLifecyclePolicy.CanEditBody` at **both** boundaries: repository `UpdateAsync` (the boundary that can't be bypassed) and the editor open path (so the UI shows read-only + a clear banner instead of failing on save).
2. Wire the already-written-but-dormant `InvoiceLifecycleStateMachine.IsEditable` into the PI editor as its PI-specific implementation.
3. Status-only moves (e.g. Issued→Paid) remain allowed via `StatusTransitions`; body fields (lines, totals, dates, party) freeze once posted.
*Acceptance:* opening an Issued TI or Approved PI shows a locked, read-only body with an explicit "posted — use Credit Note / Cancel" banner; Draft editing unchanged.

### PHASE 3 — Fail-closed dependency guard for fiscal parents (close G-4, G-6)
1. Add `TaxInvoice.Delete` permission (Manager/Admin only) and gate the TI delete/void path on it, plus a status check (Issued/Paid → Void path only).
2. Give `DependencyGuard` a `strict` mode: for **posted parents**, a failed dependency query returns `Blocked("could not verify dependents — retry")` instead of `Ok()`. Drafts keep the friendly fail-open.
*Acceptance:* an Accountant can't nuke a paid invoice; a transient DB error can't let a posted PI orphan its TI.

### PHASE 4 — Credit Note / reversal document (close G-1's correction gap, G-3)
**Goal:** the industrial-correct way to fix a wrong-but-issued invoice.
1. Extend `InvoiceType` with `CreditNote`; a credit note references `OriginalInvoiceNumber`, carries negative/return quantities, gets its own sequence, and links back for VAT.
2. "Void" of an Issued TI optionally spawns a linked Credit Note (guided flow).
3. Reporting (VAT submission + statements) already aggregates TI rows — extend to net credit notes against originals.
*Acceptance:* correcting an issued invoice produces an auditable original + credit-note pair; net VAT is correct; nothing is deleted.

### PHASE 5 — Immutable numbering + concurrency + audit polish (close G-5, G-7)
1. Move next-number generation to a monotonic sequence that **never reissues** a number even after void/delete (a persisted counter per series, not `MAX+1` over live rows). Voided numbers stay consumed.
2. Add a `RowVersion` concurrency token to the four DbContexts; on conflict, surface "this document changed since you opened it — reload" instead of last-write-wins.
3. Extend `AuditService` entries for lifecycle actions to always carry `{reason, fromStatus, toStatus, correlationId}` (the coordinator already threads correlation IDs).
*Acceptance:* delete-then-create can't collide a number; concurrent edits are detected; every void/cancel/delete has a reason + before/after status in `audit.db`.

---

## 5. Why this is the senior answer (and not just "add a lock")

- **It matches how real ERPs (SAP, Odoo, Zoho, QuickBooks) behave:** drafts are disposable; posted documents are immutable and corrected by reversal. UAE FTA rules make this non-optional for Tax Invoices specifically.
- **It reuses ProGlass's own strengths** — `StatusTransitions`, `DependencyGuard`, `DocumentOperationCoordinator`, the migration runner, `AuditService`, snapshots — instead of introducing a parallel mechanism that would drift (the same "one engine, one truth" principle the profitability phase already committed to).
- **It's incremental and safe:** every phase is additive (nullable columns, new methods, policy gates in front of existing calls). Phase 1 alone removes the worst compliance risk; each later phase is independently shippable.
- **It answers "force-update vs delete" honestly:** the correct production rule is *both, chosen by state* — hard-delete only childless drafts, force everything posted down a Void/Credit-Note path, and never reuse a number.

---

## 6. Recommended first landing

**Phase 1.** It converts the single most dangerous behaviour (hard-deleting fiscal documents) into a reversible, audited Void with a mandatory reason, without touching a single working create/edit path — and it lays the `IDocumentLifecyclePolicy` foundation every later phase plugs into.

*Open question for you before I start Phase 1:* do you want **soft-delete/void applied to all four documents at once**, or **Tax Invoice first** (highest risk) and roll outward? Either is fine — TI-first ships the compliance win fastest; all-four is one clean migration.

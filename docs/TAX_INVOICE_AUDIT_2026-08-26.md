# Tax Invoice Module — Senior Deep Audit
**Date:** 2026-08-26 · **Branch:** arena/01a03a1d-proglassautomationadv · **Scope:** full TI module + DN→TI flow + production/industrial-ERP completeness
**Method:** line-by-line read of every file in `Views/ViewModels/Services/Models` for the module + DN side + dashboard/metrics consumers + tests. Every finding cites file:line. Ratings are computed from the findings (arithmetic shown), not opinions.

---

## 1. Module map

| Layer | Files (LOC) |
|---|---|
| Views | `Views/TaxInvoice/` — GodView, List (+ProformaPickerDialog), Detail + Ti{Overview,Family,Attachments}Section, TaxInvoiceView |
| ViewModels | `TaxInvoiceDetailViewModel` (**2159** — god VM), `TaxInvoiceListViewModel` (578), `TaxInvoiceViewModel` (orchestrator), `TaxInvoiceRowViewModel`, `InvoiceFamilyRowViewModel`, `TaxInvoiceGodViewViewModel` (180) |
| Services | `InvoiceTotalsCalculator` (51), `TaxInvoiceValidationService` (**dead**), `TaxInvoiceLineService` (**dead in prod**), `TaxInvoiceFamilyService`, `PaymentTermsResolver` (29), `Mapper/TaxInvoiceMapper`, `Repository/TaxInvoiceRepository` (620), `Repository/TaxInvoiceDnReconciler`, `Pdf/TaxInvoicePdfExporter` (271) + `TaxInvoicePdfDocument` (748), `Database/TaxInvoiceDbContext` |
| Models | `SavedTaxInvoice` + `TaxInvoiceItem`, `TaxInvoiceSnapshot`, `TaxInvoiceFamily` |
| DN side | `DeliveryNoteRepository` (mark/clear), `SavedDeliveryNote` (HasTaxInvoice/TaxInvoiceId), `DeliveryNoteDetailViewModel` (ConvertToTaxInvoice) |
| Tests | `TaxInvoiceLineServiceTests` (tests the **dead** module), `TaxInvoiceDnReconcilerTests` (60), `InvoiceTotalsCalculatorTests`, `PaymentTermsResolverTests`, `StatusTransitionsTests` |

**Genuine strengths (keep, cite in PRs):** unique `InvoiceNumber` index (`TaxInvoiceDbContext.cs:86`); `OperationGuard.Demand` + `PermissionService` at every write boundary; audit log + `VersionHistoryService` per save + `RestoreVersionAsync`; `CriticalOperationTracker` around the two-DB save window (D-4); startup TI→DN reconciler with verify; `SqliteResilience` retry; Result + CorrelationId pattern; SQL projection on the hot list path (`GetAllAsync`); unsaved-changes guard (`CaptureCleanState`/`TrySaveAsync`); idempotent batch DN marking; list-VM load coalescing (Interlocked + queued reload); 10 MB attachment cap + MIME allow-list.

---

## 2. Findings (file:line evidence)

### A — Financial correctness

**A1 · CRITICAL — Family Balance / Progress is ALWAYS 0 on screen.**
`ProformaTotal` is never populated anywhere: `TaxInvoiceRepository.cs:216` (`ProformaTotal = 0m // caller must populate from PI snapshot` — there is no caller), `TaxInvoiceFamily.BuildFamily` (`ProformaTotal = 0m, // populated by repository` — it isn't). Consequences, all user-visible:
- List rows: Balance column bound at `TaxInvoiceListView.xaml:157` → "AED 0.00" for every family; progress bar `:118` → 0.0%; `InvoiceFamilyRowViewModel` → `ProgressPercent` always 0.0, `ProgressDisplay` = "… of AED 0.00".
- List hero card 3 "Remaining Balance" (`TaxInvoiceListViewModel` HeroCard3Value) → 0.
- Detail sidebar: `TiFamilySection.xaml:242` ← `FamilyBalanceTotal = family.BalanceTotal` (`TaxInvoiceDetailViewModel.cs:1012`) → always 0.
- The fix is one property away: the VM already holds the real PI total (`ProformaTotal = piEntity.GrandTotalVatInclusive`, Detail VM:1251) but never hands it to the family.
**This also disarms the only guard that would warn users before double invoicing (see C1).**

**A2 · HIGH — FixedAmount PI discount is lossy-converted to %.**
`TaxInvoiceDetailViewModel.cs:1379-1384`: PI fixed AED discount → `DiscountPercent = Round(discountValue / Subtotal × 100, 2)`. The AED value is never stored; on a partial invoice the % re-applies to the smaller subtotal (different AED); even on a full import the rounded % can reproduce a different AED (e.g. 5.00005% → 5.00% → −0.05 AED). Silent — no message.

**A3 · MEDIUM — Banker's rounding + no clamp + "AED Zero Only" for negatives.**
`InvoiceTotalsCalculator.cs:32-45`: `Math.Round(…, 2)` = `MidpointRounding.ToEven` (ambiguous money policy), and no clamp: discount > 100% → negative AfterDiscount/VAT/GrandTotal, which **can be saved** (no GrandTotal>0 check at save, see D1) and prints as "AED Zero Only" (`TaxInvoiceMapper.cs:414`). Also `AmountToWords` fils: `int fils = (int)Math.Round((amount - dirhams) * 100)` — safe only while every amount is ≤2 dp; no guard if that invariant ever breaks (fils = 100 → "and One Hundred Fils").

**A4 · LOW — Row math duplicated in 3 places.** `value = Round(Rate·Qty·Sqm,2)`, `totalSqm = Round(Sqm·Qty,3)` computed in `BuildItemsAndSubtotal` (mapper ~228), `BuildSnapshot` (~284) and PDF `BuildRenderContext` (~560-585). Identical today; drift risk. Displayed TotalSqm ≠ Value/Rate by construction.

**A5 · MEDIUM — "Overdue" exists in 4 inconsistent implementations; the stored status is unreachable.**
- List row: `TaxInvoiceRowViewModel.IsOverdue` (~168-177): past-due AND not Paid/Cancelled/Void → **PartiallyPast counts as overdue**.
- Dashboard: `TaxInvoiceMetricsService.cs:50-52` `ExcludedFromOverdue = {Paid, PartiallyPaid, Cancelled, Void}` → **PartiallyPaid is NOT overdue**. Same invoice, two answers.
- PDF: `ctx.IsOverdue` computed (`TaxInvoicePdfDocument.cs` ~505) but **never rendered** (see E2).
- Repo: `GetOverdueAsync` (correct derived rule) is never called.
- Stored "Overdue" is unreachable: `StatusOptions` (Detail VM:163-166) is not bound in any XAML; no command writes it.
- GodView KPI counts the stored status (`TaxInvoiceGodViewViewModel.cs:163`) → **the "Overdue" tile is permanently 0**.

### B — Data integrity & lifecycle

**B1 · HIGH — 26-partial hard stop (Z→A wrap).** `TaxInvoiceRepository.cs:254-256`: `last < 'Z' ? next : "A"; // safety fallback`. Numbering is letter-only (`BuildInvoiceNumberAsync` 259-274 → `INV-2026-001-A…Z`). The 27th partial collides with `INV-…-A`; SaveAsync's pre-check (Detail VM:1459-1463) then reports "Invoice already exists" → **the PI can never get a 27th partial, no AA escalation, no guidance.** The unique index (DbContext:86) would throw the same thing.

**B2 · MEDIUM — Number reuse after delete.** `GetNextSequenceNumberAsync` = `Max+1` (repo:222-228); deleting the last-used number lets it be re-issued (same number, different invoice, same PI). Accounting-trail risk.

**B3 · HIGH — No Void/Cancel workflow.** `StatusOptions` lists Cancelled/Void (Detail VM:163-166) but no UI/command can set them; the only removal is `DeleteCommand`. Deleting a customer-issued invoice is an accounting red flag — industrial ERP needs Void/Cancel with reason + reversal.

**B4 · MEDIUM (latent) — Status change never releases DNs.** `UpdateStatusAsync` (repo:387-404) has no `ClearLinkedDeliveryNotesAsync` call (asymmetric with `DeleteAsync` repo:317-344 → `ClearLinkedDeliveryNotesAsync` 351-364). Dead from the UI today (B3), a live trap the moment Void/Cancel is added naively. Also no `DocumentLifecycle.Raise()` after a status write (every other write path raises).

**B5 · MEDIUM — Forward-only DN marking; editing never un-marks.** SaveAsync always calls `MarkDnsAsInvoicedAsync` (Detail VM:1563) for the current `DeliveryNoteReference`; removing a DN from an invoice never clears that DN's flag (only `DeleteAsync` does). The DN keeps claiming an invoice that no longer covers it → PI can show "Fully invoiced" incorrectly.

**B6 · LOW — Comment/code landmine.** `UpdateAsync` (repo:293-314): line 310 `entity.Attachments.Clear()` sits directly under the comment "Attachments managed independently — do NOT clear on update". Safe only because the update path always passes a freshly-built entity with an empty collection; pass a DB-loaded entity and every attachment is deleted.

**B7 · LOW — `SequenceNumber` (int) is orphaned.** Stored (max+1) but never part of the invoice number (letter only); used solely for ordering.

**B8 · MEDIUM — Family delete is non-atomic.** `DeleteFamilyCommand` (`TaxInvoiceListViewModel` ~325-360): per-member `DeleteAsync` loop, no transaction; failure on member 3 of 5 leaves a half-deleted family (2 gone, 2 remain, audit never written).

### C — DN → TI flow (dedicated, per request)

**C1 · CRITICAL — Master Invoice path can structurally double-invoice.** `InitFromDeliveryNoteFamilyAsync` (Detail VM:1113-1196) aggregates **100 % of delivered qty across all sibling DNs** — no subtraction of already-invoiced qty, no `HasTaxInvoice` check. Entry: TI list "Create" → `CreateFromProformaOrFullPiAsync` (orchestrator:430-478); `CheckForExistingDraftAsync` (orchestrator:493-528) only intercepts when a **Draft** exists. Scenario: INV-x issued 60k of a 100k PI → user creates "Master" → INV-x-A imports the full 100k again → 160k billed on a 100k PI. No over-invoicing warning anywhere; the family balance that should guard this is broken (A1).

**C2 · HIGH — TI-side single-DN entry is unguarded (asymmetric).** `InitFromDeliveryNoteAsync` (Detail VM:1046-1100) and `CreateFromDeliveryNoteAsync` (orchestrator:355-390) never check `dn.HasTaxInvoice`. The **DN-side** entry IS guarded — `CanConvertToTaxInvoice` (`DeliveryNoteDetailViewModel.cs:1808-1822` disables the button for invoiced DNs) — and the auto path is guarded (`CreateAndSaveDraftFromDeliveryNoteAsync`, orchestrator:339-352, `if (dn.HasTaxInvoice) return`). So the primary UX path is safe, but entering from the TI module bypasses the guard entirely.

**C3 · HIGH — DN link is single-valued and silently overwritten.** `SavedDeliveryNote.TaxInvoiceId` (SavedDeliveryNote.cs:145-148) holds ONE invoice. `MarkMultipleAsInvoicedAsync` (`DeliveryNoteRepository.cs:306-357`, overwrite at 343) replaces an existing link pointing at a *different* TI with no log, no audit, no warning. Combined with C1 the second invoice steals the DN; the first still references it.

**C4 · HIGH — Reconciler is conflict-blind.** `TaxInvoiceDnReconciler.ReconcileAsync`: when two TIs reference the same DN, the sweep marks the DN with whichever invoice is iterated last (arbitrary order) — it *reinforces* the wrong owner and never reports the conflict. Referenced-but-missing DNs are silently skipped (`dnMap.TryGetValue` guard). Only TI→DN direction; an orphaned DN flag (DN marked, no TI references it) is never cleaned.

**C5 · MEDIUM — N+1 import.** `GetAllAsync()` (loads every DN entity incl. snapshot blobs) then `GetByDeliveryNoteIdAsync(dnId)` per sibling (Detail VM:1126-1150).

**C6 · MEDIUM — Silent row drops.** PI rows absent from the DN qty map are dropped with a bare `continue` (Detail VM:1317-1321) — no dropped-row count, no warning; DN delivered qty for serials not in the PI is silently ignored.

**C7 · LOW — Silent rate fallback.** `Rate = piRow.Rate > 0 ? piRow.Rate : piGroup.BaseRate` (Detail VM:1341-1343).

**C8 · MEDIUM — Two overlapping workflows collide.** Dispatch auto-creates+saves a Draft TI per DN (`CreateAndSaveDraftFromDeliveryNoteAsync`) which marks the DN invoiced; the same PI can simultaneously get a "Master Invoice" from the TI list. `CheckForExistingDraftAsync` asks "draft exists — open or create NEW?"; choosing NEW duplicates the draft's qty (its answer text even says "will be Partial: A/B/C" with no balance math).

### D — Validation & guardrails

**D1 · HIGH — The save gate is minimal.** `ValidateBeforeSave` (Detail VM:1889-1912): ProformaId + CustomerName + ≥1 row. Missing: qty>0, rate>0, **GrandTotal>0**, DueDate ≥ IssueDate, 15-digit UAE TRN (entity doc claims "validated at VM level" — it isn't), discount ≤ 100%, over-invoicing cap.

**D2 · MEDIUM — The strong validators are dead code.** `TaxInvoiceValidationService` is DI-registered (`ServiceRegistration.cs:107`) but **never invoked anywhere**. `TaxInvoiceLineService` (CalculateFinancials/CalculateFamily/ValidateInvoice/BuildInvoiceNumber) is referenced only by its own test file — the only validation that actually checks qty>0 and GrandTotal>0 is exercised nowhere in production.

**D3 · LOW — Transition validation lives only in the VM** (`ChangeStatusAndSaveAsync`, Detail VM:2141-2159); repo `UpdateStatusAsync` doesn't validate.

**D4 · MEDIUM — No payment capture (no AR).** `MarkPaidCommand` jumps Issued→Paid (Detail VM:622-626); there is no payment amount/date/reference, so `PartiallyPaid` — present in the transition table (`StatusTransitions.cs` TI table) — can never be recorded from the UI. No partial payments, no AR ledger, no aging inside the module.

### E — PDF & print

**E1 · HIGH — "Include attachments?" is a lie.** ExportPdfAsync asks (Detail VM:1659-1669); `AppendAttachments` (`TaxInvoicePdfExporter.cs` ~186-208) returns the invoice bytes unchanged (merge is commented "Future: PdfSharp") yet the file is named `*_WithAttachments.pdf`. A false deliverable goes to the customer.

**E2 · MEDIUM — No status/overdue on paper.** PDF prints stored status only; `IsOverdue` and `GetStatusColor` in `TaxInvoicePdfDocument.cs` are dead code. A 90-day-overdue invoice prints as "Issued".

**E3 · MEDIUM — No logo or signature, ever.** `LogoBase64 = ""`, `PreparedBySignatureBase64 = ""` hardcoded in `BuildWizardState` (Detail VM:1915-1965).

**E4 · LOW — PDF recomputes row values** while the summary block reads stored totals (consistent today, drift risk — see A4).
**E5 · LOW — Fixed temp file name** in `SaveAndOpenAsync` → re-export while a viewer holds the file = IOException.

### F — Robustness & performance

**F1 · LOW — Family queries load full entities with the snapshot blob** (`GetAllFamiliesAsync`/`GetFamilyAsync`/`GetByProformaIdAsync`, repo:161-187, 204-220 — no projection, no AsNoTracking): every list load and every save/delete (DocumentLifecycle) re-reads all `FullSnapshotJson` blobs (base64 attachments included).
**F2 · LOW — `VerifyMarkedAsync` N+1** per missing DN (repair path only — rare).
**F3 · LOW — GodView** audit-logs every open; no load coalescing.

### G — Maintainability & tests

**G1 · MEDIUM — Dead code inventory:** `TaxInvoiceLineService` (entire class), `TaxInvoiceValidationService`, repo `GetOverdueAsync`/`UpdateStatusAsync` (dead from UI), Detail VM `StatusOptions`, PDF `AddLabelValue`/`AddTableCell`/`AddTableCellRight`, transition-table rows Overdue/Cancelled/Void (unreachable).
**G2 · HIGH — Tests cover the wrong module.** The only TI service test suite (`TaxInvoiceLineServiceTests`) tests the **dead** class. Zero tests for: numbering (wrap/reuse/delete), family math, `AmountToWords`, `BuildEntity`/`BuildSnapshot`, the save flow, delete→DN-release, reconciler conflicts, VMs, PDF.
**G3 · LOW — God VM** `TaxInvoiceDetailViewModel` = 2159 LOC (save + numbering + import + family + attachments + PDF + status + versions).

---

## 3. Production / industrial-ERP completeness check

| Capability a production industrial ERP expects | State in this module |
|---|---|
| Invoice numbering (unique, gap-aware) | Partial — unique ✓; 26-partial cap ✗ (B1); reuse-after-delete ✗ (B2) |
| Status lifecycle incl. Void/Cancel + reason | ✗ (B3) — Draft→Issued→Paid only |
| Payments / AR: partial payments, payment records | ✗ (D4) |
| Overdue: stored flag, auto-job, aging | ✗ — 4 divergent display-only derivations (A5); no aging column in list |
| Credit notes / returns / reversals | ✗ — none |
| Double-invoicing protection (DN + PI level) | ✗ (C1/C2/C3); DN-side button guard ✓ |
| Reconciliation (both directions, conflict report) | Partial — TI→DN startup sweep ✓; conflicts invisible (C4) |
| VAT compliance data (5%, TRN both parties) | Partial — stored ✓; TRN never validated (D1) |
| Audit + version history + correlation | ✓ strong |
| Permissioned writes at the boundary | ✓ strong (`OperationGuard.Demand`) |
| PDF invoice (letterhead, words, bank, signatures) | Partial — no logo/signature (E3); attachments fake (E1); no status badge (E2) |
| e-invoicing / authority submission | ✗ (scope decision — UAE private sector not yet mandated) |
| Multi-currency / multi-branch | ✗ (scope — AED-only is defensible for the market) |

---

## 4. CURRENT RATING (computed, per category)

Rubric: each category 0–10 from the findings that land in it (severity-weighted: CRITICAL = −3..−4, HIGH = −2..−3, MEDIUM = −1..−2, LOW = −0.5..−1, from a clean 10, floored at the evidence).

| # | Category | Score | Why (mapped findings) |
|---|---|---|---|
| 1 | Financial correctness | **4/10** | Per-invoice math is sound (one calculator, correct 5% VAT, rounded rows) but the family money layer is broken: Balance/Progress always 0 (A1 CRITICAL), lossy fixed discount (A2), no clamp + negative words (A3), 4-way overdue divergence incl. a permanently-0 KPI (A5) |
| 2 | Data integrity & lifecycle | **4/10** | Unique index + guarded writes + versions ✓, but 26-partial hard stop (B1), number reuse (B2), no void/cancel (B3), latent DN-release gap (B4), forward-only marking (B5), non-atomic family delete (B8) |
| 3 | DN ↔ TI flow | **3/10** | Half the entry points guarded (DN side, auto path) but the TI-side entries + Master path are open (C1 CRITICAL, C2, C3), silent link overwrite (C3), conflict-blind reconciler (C4), N+1 + silent drops (C5/C6/C7), workflow collision (C8) |
| 4 | Validation & guardrails | **3/10** | Strong infra (guards, transitions at VM) but the actual save gate is 3 checks (D1), the strong validators are dead (D2), no AR at all (D4) |
| 5 | PDF & print | **5/10** | Looks professional (letterhead, tables, words, partial banner) but attachments are a silent no-op misnamed (E1), no logo/signature (E3), no overdue badge (E2) |
| 6 | Robustness & performance | **6/10** | Retry/resilience, coalescing, projection on the hot path ✓; blob reads on family queries (F1), N+1s (F2/C5), no partial-failure recovery on delete (B8) |
| 7 | Maintainability & tests | **4/10** | Rich comments, clean patterns, DI/Result ✓ — but the most-tested module class is dead (G2), large dead-code inventory (G1), 2159-LOC god VM (G3) |

**Overall current rating = (4 + 4 + 3 + 3 + 5 + 6 + 4) / 7 = 29 / 7 = 4.14 → 4.1 / 10**

---

## 5. Phases to 10/10 — with before/after ratings

### Phase 1 — Correctness (stop the financial/numbering bleed)
Fixes: **A1, A2, A3, A5(partial), B1, B2, B3, B4, C1, C2, C3, D1, D2, E3(partial)**
1. Wire `ProformaTotal` into the family (repo `GetFamilyAsync`/`GetFamilySummaryAsync` + `LoadFamilyAsync` passes the VM's PI total) → Balance/Progress correct in list, hero cards, sidebar.
2. Double-invoice guards: `HasTaxInvoice` check in both TI-side entries; Master path subtracts already-invoiced qty per row key and **warns/blocks when the new invoice would exceed the remaining family balance**; DN link overwrite → block + audit (or explicit reassign with reason).
3. Numbering: on Z exhaustion escalate (AA/BB… or numeric suffix) — never wrap; no reuse of deleted numbers (skip to next unused).
4. Void/Cancel: UI commands + reason; `UpdateStatusAsync` releases DNs on Void/Cancelled + `DocumentLifecycle.Raise()` + audit; block Delete of an issued invoice (void instead).
5. Financial hardening: explicit `MidpointRounding.AwayFromZero` policy; reject/clamp discount > 100%; `AmountToWords` negative/edge guards; store FixedAmount discount as type + AED value (pro-rata with exact-AED rounding on partials).
6. Save-time validation: qty>0, rate>0, GrandTotal>0, DueDate≥IssueDate, 15-digit TRN both parties, over-invoicing warning; wire the strong validator into `SaveAsync` and delete the duplicate.
7. One shared overdue-derivation helper used by list/dashboard/PDF/GodView (consistent rule); fix GodView KPI.

**Before Phase 1: 4.1 → After Phase 1: 6.6**
Re-score: Financial 4→8 (A1/A2/A3/A5 fixed; A4 drift remains) · Integrity 4→8 (B1–B4 fixed; B6–B8 remain) · DN↔TI 3→7 (C1–C3 closed; C4–C8 remain) · Validation 3→7 (D1/D2 closed; D3/D4 remain) · PDF 5→6 (logo wired) · Robustness 6→6 · Tests 4→4.
= (8+8+7+7+6+6+4)/7 = 46/7 = **6.57 → 6.6**

### Phase 2 — Production ERP features
Fixes: **C4, C5, C6, C7, C8, D3, D4, E1, E2, E5, B8, F3**
1. Reconciliation both directions: conflict report when a DN is claimed by two TIs (audit + alert), log missing DNs, clean orphaned flags; on-demand, not startup-only.
2. Un-mark on edit: diff DN references on save, release removed DNs (audited).
3. DN import hygiene: per-PI DN load (kill N+1), report dropped PI rows + ignored DN qty, no silent rate fallback.
4. AR/payments: payment records per invoice (amount/date/reference), PartiallyPaid auto-set, aging column in list, stored Overdue via daily sweep + the shared rule.
5. PDF: implement attachment merge (PdfSharp) **or** remove the dialog and name the file honestly; overdue badge + status color; wire logo + signature from company settings; unique temp names.
6. DeleteFamily atomicity: all-or-nothing + per-invoice failure report; per-invoice delete with reason.
7. Audit precision: "status changed" vs "saved" entries; wire-or-kill dead repo methods.

**Before Phase 2: 6.6 → After Phase 2: 8.3**
Re-score: Financial 8→9 (single overdue rule + AR) · Integrity 8→9 (B8 closed, B6 comment fixed) · DN↔TI 7→9 (C4–C8 closed) · Validation 7→9 (D3/D4 closed) · PDF 6→9 (E1/E2/E5 closed) · Robustness 6→7 · Tests 4→6.
= (9+9+9+9+9+7+6)/7 = 58/7 = **8.29 → 8.3**

### Phase 3 — Hardening & polish
Fixes: **A4, B6, B7, F1, F2, G1, G2, G3**
1. Test the **live** code: numbering (wrap/reuse/delete), family math, AmountToWords boundaries, mapper edges, save flow with fake repo, delete→DN-release integration, reconciler conflicts, PDF golden-file; cover AR flows.
2. Kill the dead code (G1) — or wire `TaxInvoiceLineService` as *the* validator and delete the clone.
3. Split the 2159-LOC god VM (extract save/numbering/import into services — the seams already exist).
4. Perf: SQL projection on family queries (stop reading blobs in the list), batched DN import, GodView coalescing.
5. Use-or-drop `SequenceNumber`; document the numbering scheme.
6. UX: inline over-invoicing warning ("exceeds family balance by AED X"), DN chips showing the link owner, aging column.

**Before Phase 3: 8.3 → After Phase 3: 9.2**
Re-score: Financial 9→9.5 · Integrity 9→9.5 · DN↔TI 9→9.5 · Validation 9→9 · PDF 9→9 · Robustness 7→9 · Tests 6→9.
= (9.5+9.5+9.5+9+9+9+9)/7 = 64.5/7 = **9.21 → 9.2**

### Phase 4 — Product scope (decisions, not code fixes)
Credit notes/returns, e-invoicing readiness, multi-currency, multi-branch, customer portal.
**Before Phase 4: 9.2 → After Phase 4: 9.8 (projection)**

**On 10.0:** I will not pre-award it. The after-ratings above are projections from the finding→phase mapping — an honest 10/10 is only defensible after a re-audit of the implemented code shows zero open findings. Re-audit happens after Phase 1 (recommended gate: re-score before starting Phase 2).

### Rating ladder (summary)

| Milestone | Rating |
|---|---|
| **Now (exact, per code)** | **4.1 / 10** |
| After Phase 1 (correctness) | 6.6 / 10 |
| After Phase 2 (ERP features) | 8.3 / 10 |
| After Phase 3 (hardening) | 9.2 / 10 |
| After Phase 4 (product scope) | 9.8 / 10 |
| 10.0 / 10 | only after re-audit with zero open findings |

# PROGLASS AUTOMATION — ROADMAP TO 10/10 INDUSTRIAL ERP WORKFLOW
**Owner:** you + agent · **Branch:** `arena/01a06d1e-proglassautomationadv` · **Baseline rating (after commit 25618e9):** 8.6/10
**Rule of play:** one phase per landing, traced before touched, statically verified, committed + pushed separately. Nothing ships that breaks the existing 8.8-rated PI pipeline.

Where the remaining 1.4 points are lost (from the audit):

| Gap | Why it costs points | Phase |
|---|---|---|
| DGU+LAM (the most complex spec type) has two unverified/fragile sync paths: Step-4 sheet registry branch and DGU+LAM snapshot/restore indexing | Wrong provenance = wrong sheet cost & wrong P&L for exactly the jobs with the most money on the table | **1** |
| Inquiries are header-only; conversion carries no BOQ/spec items | Manual re-typing between Inquiry and PI = search-key duplication, errors, no traceability QUOTED-vs-SOLD | **2** |
| Job Profitability reads stored snapshots only; old PIs silently go stale after upstream edits; no staleness signal | Reports can disagree with the live invoice without anyone knowing | **3** |
| No automated regression tests around company-repo semantics, inquiry→PI mapping, profitability re-cost | Every later phase risks breaking what we fixed | **4** |
| Cross-module consistency polish (status vocabulary, error surfacing, audit-trail coverage) | Industrial ERP = predictable vocabulary + full traceability | **5** |

---

## PHASE 1 — DGU+LAM correctness pass ✅ *(landed this turn)*
**Traced results:**
1. `RecordSheetSelection` has no `case "DGU+LAM"` — **verified CORRECT BY DESIGN**: the DGU+LAM panel has no sheet-size inputs; it prices three panes + interlayer directly per SQM (`CalculateRate` = Σpane base prices ÷ yield + processing + interlayer sell, × margin). Stale comment claiming "only DGU and SGU expose a sheet picker" was corrected (LAM had already been added earlier).
2. Step-4 capture/restore for DGU+LAM is a **field-for-field mirror** (36 panel fields both directions) — no `ElementAt` indexing exists anywhere in the current tree; the earlier audit-trail suspicion is closed as N/A.
3. Step 9's DGU+LAM build is parsed from the spec's own pane collection (3 panes), interlayer costed at BUY price, spacer/interlayer row insertion order guarded — verified sound.
4. **REAL DEFECT FIXED:** Step 9's production rows (spacer / interlayer / cutting / tempering — all four builds) read rates from the **live Step 4 panel**, which only ever holds the *selected* calculation section. A multi-section PI could silently cost every group with section 1's production inputs; a restored PI costed them with whatever section the wizard restored into view.
   **Fix:** 4 nullable provenance fields (`Step4Spacer/InterlayerCost/Cutting/Tempering CostPerSqm`) on `SpecGroupSnapshot` + matching `SpecGroup` VM properties, written at every Step 4 → 5 handoff per build type (SGU cut/temper · DGU spacer · LAM PVB balance + pane-pair processing sums · DGU+LAM spacer + laminator BUY price + three-pane processing sums), mapper round-trip capture+restore, and Step 9 reading group-first with live-panel fallback (legacy invoices byte-for-byte identical behavior).
**Acceptance met:** after save → close → reopen, every group costs production from ITS OWN quoted inputs; single-section invoices unchanged.

## PHASE 2 — Inquiry BOQ + traceable conversion ✅
**Goal:** Inquiry becomes a real pre-sales workstation; quoted shapes are never re-typed by the estimator.
**Landed:**
1. `InquiryLineItem` entity (build / spec name / colours / W×H mm / qty / expected rate / notes / sort) + nav + cascade; `LineItems` DbSet with indexes.
2. Migration **1003** (idempotent `CREATE TABLE IF NOT EXISTS InquiryLineItems` + indexes) following the repo's established runner pattern; fresh DBs via `EnsureCreated` automatically.
3. Repository: `GetDetailAsync` includes the BOQ; atomic replace-all `SaveLineItemsAsync` (permission-gated, communication-entry audit entry); `DuplicateAsync` clones the scope (attachments still stay behind by design).
4. Editor: "SCOPE OF SUPPLY" card with add/remove grid (Build dropdown limited to SGU/DGU/LAM/DGU+LAM), INPC row wrapper so grid edits mark the form dirty, save hook that is header-safe for roles without InquiryEdit and graph-safe on the create path.
5. Conversion: `StartFromInquiryAsync` maps importable BOQ rows → Step 5 spec groups written **outside** the Step-4 sync guard (imported specs are manually-owned and survive the Step 4 push), colours/rate/qty/dims carried, one grid line per BOQ row.
**Acceptance:** create inquiry with 3 BOQ rows → convert → Step 5 shows 3 groups with identical dims/qty/specs; re-convert still blocked by the existing guard; legacy inquiries (no BOQ) convert exactly as before.

## PHASE 3 — Profitability staleness & re-cost
**Goal:** report never silently disagrees with the invoice.
1. Persist a `ProfitLossFingerprint` (hash of the Step-5/6/7 inputs Step 9 consumed) at save time.
2. Job Profitability row shows **Stale** badge when report-side recomputation of the fingerprint differs.
3. One-click "Recalculate" action that recomputes cost rows from the snapshot (pure snapshot → P&L pipeline, no wizard spin-up).
**Acceptance:** edit a PI’s Step 5 after saving → profitability flags that PI stale → recalc clears it and refreshes margin.

## PHASE 4 — Regression net
**Goal:** everything fixed so far stays fixed.
1. Unit tests: company repo update semantics (latest-wins vs preserve-on-blank, rename-collision, stale Id).
2. Unit tests: inquiry→PI item mapping.
3. Unit tests: profitability fingerprint/re-cost determinism.
**Acceptance:** `dotnet test` green, tests run in CI-style filter documented in README of Tests/.

## PHASE 5 — Industrial consistency layer
**Goal:** one vocabulary, full traceability, predictable errors.
1. Status vocabulary audit across Inquiry/PI/JO/DN/TI (single Normalize homes already exist — enforce).
2. Audit-trail coverage: company auto-enrichment events logged (string diff), inquiry conversion logged with item snapshot.
3. Error surfacing pass: silent-catch audit on money paths (they’re currently log-only by design where safe — document each).
**Acceptance:** every workflow mutation either (a) logs an audit entry or (b) carries an inline justification comment.

**Rating earned after each phase:** P1 → 8.8 · P2 → 9.2 · P3 → 9.5 · P4 → 9.7 · P5 → 10.0 (only if each phase’s acceptance holds under local build+test).

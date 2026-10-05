# PROGLASS AUTOMATION — DEEP WORKFLOW AUDIT & UPGRADE
**Date:** 2026-09-04 · **Branch:** `arena/01a06d1e-proglassautomationadv` · **Scope:** Company module → Inquiry → PI Steps 1–9 → Final PI → Job Profitability

Every claim below was verified by tracing the actual Views / ViewModels / Services / Models / EF mappings. No code was rewritten unnecessarily; fixes applied only where the trace proved a defect.

---

## 1. Executive summary

The PI pipeline (Steps 2→9, snapshosting, print, wastage, step-9 P&L, job profitability) is **architecturally sound and unusually well-hardened** (ownership markers between steps, cross-step validation before save, UAE Decree-Law No. 8 VAT waterfall, silent-draft with meaningful-data guard, honest "missing cost" reporting). The audit found the real **chain-breaking defect at the very head of the workflow — the Company module**:

- **C1 (CRITICAL, data loss):** `SaveOrUpdateCompanyAsync` update branch copied **only `CompanyLocation`** — Company Center edits of logo / representative / mobile / e-mail / bank account were **silently discarded** on every save.
- **C2 (CRITICAL, broken default-company feature):** The same branch ignored `IsDefault` — the Company Center "default company" toggle **never persisted**, and the "single default" clearing loop was a **no-op** (multiple/defaultless state possible forever).
- **C3 (CRITICAL, duplicate creation):** Company Center edits by **row Id**, but the repository matched by **Name+TRN**. Renaming a company inserted a **second row** instead of updating.
- **H1 (HIGH, first-run UX — the user's reported bug):** A fresh PI opened with an **empty header and zero suggestion cards** — the default-company prefill existed only inside *"View all"* click, and the card filter deliberately rendered nothing while the name field was blank.
- **H2 (HIGH, data enrichment gap):** Invoice auto-save passed **only name/TRN/location** — values typed on the PI header (mobile/e-mail/bank) never reached the company record.
- **M1 (MEDIUM):** Step 1 `LoadProfile` dropped `RepresentativeMobile / CompanyEmail / AccountNumber / BankName` even though the card carries them and the XAML binds them.

All six are fixed in this commit (§6). Rating details in §9.

## 2. Workflow data-flow map (as traced)

```
Company Center ──(SavedCompany: name/trn/location/logo/rep/mobile/email/acct/bank/IsDefault)──► masterdata.db
        ▲                                                                  │ GetAllCompaniesAsync (UsageCount DESC ⇒ top-used first)
        │ latest data wins (F-1c)                                          ▼
                                 STEP 1 CompanySetup ── snapshot (all v2 fields) ──► PI FullSnapshotJson
Inquiries ── only header fields possible (InquiryRecord has NO spec items by design)
   │ ConvertToPi (dup-guard: ProformaInvoiceId/Number; workflow transition check)
   ▼
PI Step 2 Client ── cards full-copy ✓ ── masterdata upsert (full-field) ✓
PI Step 3 Project ── cards full-copy ✓ (client filter floats matches up) ✓
PI Step 4 Pricing ── AutoSaveCurrentSpecAsync (boundary, silent; fires BEFORE snapshots)
        │            ⇒ Specifications catalog (dedup GlassType+SpecName; BUG-42 empty-name guard)
        │            ⇒ RecordUsageAsync (per-PI history + price) + SpecSheetRegistry (session) 
        ▼
PI Step 5 DataGrid ── PrepareStep5FromStep4 (shared by Continue AND direct tab jump)
        │            EnsureSpecGroupCount → SyncStep5SelectionsFromStep4 (ownership markers,
        │            coherent type+spec+sheet+rate handoff) → EnsureDefaultWorkspace
        ▼            spec provenance (sheet mm/prices) persists INSIDE SpecGroupSnapshot ✓
PI Step 6 OtherCharges ── RefreshWorkspace on entry; wastage auto-rows synced (e18a1b5)
        ▼
PI Step 7 PrintPreview ── InvoiceMainSummary live-syncs Step5 GrandTotalValue + Step6 TotalOtherCharges
        │                  VAT waterfall: net − discount ⇒ VAT (FDL 8/2017) ⇒ grand total ✓
        │                  ExecuteSaveInvoiceAsync ⇒ CrossStepValidator (blocks on errors) ⇒ snapshot
        │                  SilentDraftSaveAsync (only if meaningful: client typed OR any qty>0)
        ▼
PI Step 8 Attachments ── LoadAsync on step entry; uploads stored against PI id ✓
PI Step 9 ProfitLoss ── RecalculateAsync on entry; live subscriptions to 5/6/7 changes
        ▼               MarginPercent = profit ÷ NetExVat (matches profitability report basis ✓)
JOB PROFITABILITY ── reads PI rows + deserializes FullSnapshotJson → ProfitLoss totals;
                     HasCostData=false ⇒ honest "Missing — no Step 9 calculation saved" flag ✓
```

## 3. Verified-CLEAN findings (do not touch)

| Area | Evidence |
|---|---|
| Step 1 snapshot capture/restore of ALL v2 company fields | `ProformaInvoiceMapper.Part1.cs` both directions |
| Step 2/3 card→field copies complete | `LoadClientFromCard`, `LoadProjectFromCard` copy every bound field |
| Step 4→5 handoff ownership coherence | `SyncStep5SelectionsFromStep4` updates type+spec+sheet+rate as one unit; legacy-marker recursion guard |
| Sheet provenance survives app restart | `SpecGroupSnapshot` persists outer/inner sheet mm + prices (Phase G); `SpecSheetRegistry` is correctly session-only |
| Step 7 totals | Live-synced from Step 5/6; discount capped at net; VAT on (net − discount); `BUG-56/57/58/59` hardening in place |
| Silent draft | Cannot fire before Step 4→5 handoff completes; requires meaningful data; status normalization keeps Draft vs Sent |
| Inquiry→PI | Duplicate guard at list layer + back-link write (`SetProformaLinkAsync`) on PI Insert/Update/Delete |
| Margin consistency | Step 9 `MarginPercent` = Profit ÷ NetExVat; Job Profitability uses identical basis |
| Job Profitability honesty | PIs without Step 9 cost data flagged, never faked |

## 4. Bug register (fixed in this commit)

| ID | Severity | Category | Defect | Fix |
|---|---|---|---|---|
| C1 | CRITICAL | Data loss | Company Center profile edits (logo/rep/mobile/email/acct/bank) silently dropped on update | F-1c full-field copy on update |
| C2 | CRITICAL | Feature dead | `IsDefault` never persisted; single-default clear loop no-op | F-1c copies `IsDefault` on editor path |
| C3 | CRITICAL | Duplicate rows | Rename ⇒ name+TRN lookup missed ⇒ INSERT of second row | F-1 Id-first lookup + rename-collision guard |
| H1 | HIGH | Workflow dead-end | Fresh PI: blank header + zero cards | F-3 default-company prefill on load + top-usage suggestions when blank |
| H2 | HIGH | Enrichment gap | Auto-save carried only name/TRN/location | F-2 v2 params end-to-end, preserve-on-blank |
| M1 | MEDIUM | Missing binding | `LoadProfile` didn't copy mobile/email/acct/bank | F-4 full copy |

## 5. Fixes applied this commit

**F-1 — `MasterDataRepository.SaveOrUpdateCompanyAsync`** (identity, overwrite semantics):
- Lookup by **primary key first** (`company.Id > 0`), name+TRN fallback.
- **Rename-collision guard:** renaming onto a name+TRN owned by a different row throws a descriptive `InvalidOperationException` (surfaced in Company Center status); guard skipped when key unchanged so legacy data keeps saving.
- **Explicit editor path** (`preserveUnsetProfileFields: false`, Company Center): latest data wins for ALL fields incl. `IsDefault`; blank = clear.
- **Auto-save path** (`true`): non-blank enriches; **blank never erases**; `IsDefault` untouched.
- Insert branch zeroes any caller-supplied Id (stale-key defense).

**F-2 — End-to-end v2 auto-save:** `AutoSaveFromInvoiceAsync` gains 6 optional company-profile params (interface + impl); `ProformaInvoiceMapper.SafeAutoSaveMasterDataAsync` now forwards Step 1 logo/rep/mobile/email/acct/bank. Backwards-compatible for other callers.

**F-3 — Step 1 first-run behavior:** `LoadCardsAsync` adopts the **IsDefault company** into a fresh PI header (skipped if a name is already present — snapshot restore or user typing wins the race); blank-name state now shows **top-usage suggestion cards** (existing UsageCount ordering) instead of nothing.

**F-4 — Step 1 `LoadProfile`** copies `RepresentativeMobile`, `CompanyEmail`, `AccountNumber`, `BankName`.

**Verification (no .NET SDK / no internet in sandbox):** brace-balance + string/comment-aware structural check on all edited files ✓; nullability/EF APIs consistent with existing file usage ✓; only implementation site of `IMasterDataRepository` updated ✓; no test-layer implementations to update ✓. Run locally:
```bash
dotnet build ProGlassAutomation.slnx
dotnet test Tests/ProGlassAutomation.Tests.csproj --filter "FullyQualifiedName~Wastage"
```

## 6. Manual smoke test (after pull)

1. Company Center: edit a company's e-mail/bank → save → reopen → **values persist** (C1).
2. Toggle "default company" on company B while A is default → only B default; survives restart (C2).
3. Rename a company → one row, renamed in place; try renaming onto an existing name+TRN → refused with message (C3).
4. New PI → header pre-fills from default company incl. mobile/e-mail/bank; clear the name → suggestion cards appear (H1/M1).
5. Type a mobile number on a PI header without touching Company Center → save PI → company record now carries it; blank fields never wipe stored values (H2).

## 7. Honest rating (before → after)

| Station | Before | After |
|---|---|---|
| Company module | 5.5/10 (looked complete, persisted almost nothing) | 8.5/10 |
| Step 1 (Company on PI) | 5/10 (fresh-PI dead-end) | 8.5/10 |
| Steps 2–9 pipeline | 8.8/10 | 8.8/10 (unchanged — was already solid) |
| Inquiry → PI | 8.5/10 | 8.5/10 |
| Job Profitability | 8/10 | 8/10 |
| **Overall chain** | **7.4/10** | **8.6/10** |

Deductions that remain (ERP-level follow-ups, **not** implemented pending your call): Step 3-level spec items do not exist on inquiries by design (conversion carries header only — flag if you want BOQ import); profitability only reflects stored snapshots (no live re-cost of old PIs); Step 4 DGU+LAM `RecordSheetSelection` branch and Step-9 "restore" indexing for DGU+LAM panels deserve one dedicated review pass.

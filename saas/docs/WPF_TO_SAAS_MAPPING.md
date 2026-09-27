# WPF → SAAS FEATURE PARITY MAPPING

> **Document status:** Phase 1 deliverable (STEP 3 of the SaaS master plan).
> Companion to `WPF_SYSTEM_AUDIT.md`. Status legend:
> `PLANNED` (designed, not built) · `IN PROGRESS` · `DONE` · `DEFERRED` (with reason)
> No SaaS module may be marked DONE until all 12 acceptance criteria of the master
> prompt (§62) pass.

---

## 0. Architectural translation layer (applies to every row)

| WPF concept | SaaS equivalent | Reason |
|---|---|---|
| One workstation / one company | **Tenant** (company) with `TenantId` on every business row | Multi-tenancy (master prompt §13) |
| 16 SQLite files under AppDataRoot | One PostgreSQL database, EF Core migrations, tenant-scoped global query filter | Server DB (§49) |
| LAN shared data root + presence service | Cloud API + background jobs | Architectural translation |
| MVVM ViewModel logic | Application services + domain services (server-authoritative) | §35 backend authoritative |
| `OperationGuard.Demand` in operations | ASP.NET Core authorization policies + permission claims | Same principle, new mechanism |
| Local SHA-256 user accounts | Identity provider (JWT + refresh), Argon2id hashes, lockout | §17/§24 |
| Machine-fingerprint license | Tenant subscription (plans, limits, trial, feature flags) | §19 |
| Local file attachments | Object-storage abstraction with generated keys | §26 |
| Velopack auto-update | CI/CD containers | §44 |
| WPF XAML views | React + TypeScript SPA (web-native UX, not XAML ports) — foundation + first screens delivered in Phase 28 (mapping §1.15); later modules land as pages against their already-shipped API | §15 |

---

## 1. Module-by-module matrix

### 1.1 Inquiry
| WPF | SaaS |
|---|---|
| `Views/Inquiry/*` (List/Editor/AttachmentPreview) | `/inquiries` list + editor pages, attachment viewer |
| `InquiryRecord`, `InquiryLifecycleService`, statuses `Received·Prepared·Sent·Confirmed·Hold·Cancelled` | `Inquiry` entity + same status set, pass-through unknown policy |
| `InquiryBoqConversion` → PI | `POST /api/v1/inquiries/{id}/convert-to-proforma` — DONE (Phase 7b, mapping §1.2c); the rest of the inquiry module (CRUD/list/attachments) is its own later phase |
| `InquiryAttachmentService` (local files) | Object-storage attachments, MIME/size validation |
| `IInquiryLifecycleService`, validation rules | `InquiryService` (application layer) + FluentValidation |

### 1.2 Proforma Invoice (9-step wizard)
| WPF | SaaS |
|---|---|
| `ProformaInvoiceView` + 9 step views | `/proforma/{id}` wizard (steps as routes), autosave |
| `PricingEstimatorViewModel.*` formulas (SGU/DGU/LAM/DGU+LAM) | `PricingCalculator` domain service (single source) + unit tests with WPF golden values |
| `SguParameters/DguParameters/LamParameters/DguLamParameters` VMs | Parameter DTOs + same dropdown catalogs (thickness, treatment, ASP widths, interlayers, polish options — tenant-editable config with WPF defaults) |
| `GlassPriceStamper`, `SpecificationUsage` | Spec stamping service (server-side) |
| Step 5 `SpecGroup` grid, `CutListGridIo` (Excel/CSV round-trip) | `/proforma/{id}/items` grid + `POST /api/v1/import/preview` → confirm → commit |
| Step 6 `OtherChargesViewModel` + `OtherChargeAutomationRules` + per-spec targeting (`SelectedMetricKey`, `CheckedSpecGroupNumbers`) | Other-charges engine with same metric bases (SQM/LM/Qty) and per-spec targeting |
| Step 6 `WastagesSectionViewModel` (racks: StockSqm, GlassSqm, UtilizationPercent, OptimizationPercent, FormulaPricePerSqm) | Wastage service consuming optimization results |
| Step 7 print preview (QuestPDF) | Server-side PDF generation + browser preview |
| Step 8 attachments | Object storage |
| Step 9 P&L (`ProfitLossStepViewModel.*`, `SheetProfitability`, `ServiceProfitability`, `GlassTypeComparison`) | P&L report service + `/proforma/{id}/profit-loss` |
| `SuggestNextIdAsync` `PI-{year}-NNN`, retry-on-collision, GUID fallback, seed reallocation | `ProformaNumberSequence` per tenant (DB sequence + retry), same format |
| Revision chain `RootProformaId`/`RevisionNumber`/-R{n} | Same fields on entity + chain endpoint |
| `InvoiceLifecycleStateMachine` (Draft→PendingApproval→Approved→Sent→Confirmed; Cancelled; Rejected→Draft) | Same state machine server-side; invalid transitions → 409 |
| Statuses `Draft·Sent·Approved·Rejected·Expired·Cancelled` + void (IsVoid/VoidedAtUtc/VoidedBy/VoidReason) | Same; soft-void API `POST /api/v1/proforma/{id}/void` |
| `RowVersion` optimistic concurrency | `xmin`/rowversion + 409 conflict responses |
| `ProformaDuplicateSnapshotScrub` duplicate action | `POST /api/v1/proforma/{id}/duplicate` |

### 1.2a Proforma document core (Phase 7a) — parity and deliberate differences

The wizard's document spine, ported 2026-09-27 from
`ProformaInvoiceRepository.cs`, `InvoiceLifecycleStateMachine.cs`,
`ProformaInvoiceStatus.cs`, `ProformaDuplicateSnapshotScrub` (read),
`SpecGroupService.cs` and `Step5DataGrid/GlassItemRow.cs`.

| WPF (source of truth) | SaaS (Phase 7a) | Difference? |
|---|---|---|
| `SuggestNextIdAsync`: batch-load prefix ids (BUG-08), pad-width from the seed, TryParse overflow degradation (V-1 "PI-99999999999"), 100k loop, GUID fallback (BUG-09), regex with named groups (BUG-11) | `Domain.Proforma.ProformaNumberSequence` — verbatim, pure over the id set | Same (goldens: PI-2026-000→001, gaps, INVOICE-42→43, ABC→ABC-001, case-insensitive existing set) |
| Seed `PI-{year}-000` unallocatable (`IsUnallocatedSeed` + blank); insert reallocates | Same on create; `PI-{year}-NNN` allocated per tenant | Same |
| Unique index on ProformaId + 5-attempt unique-violation retry (bump id, detach, retry) | `UNIQUE(tenant_id, proforma_no)` + same retry loop over `TryInsertAsync` false; `NumberExhausted` 409 after 5 | **Different by design** — uniqueness is per-tenant (two tenants may both own PI-2026-001; tested) |
| `InvoiceLifecycleStateMachine` — 7 states, 11 transitions, `RequiresApproval` flags, descriptions, legacy "Pending"→PendingApproval / "Active"→Sent | `Domain.Proforma.InvoiceLifecycle` — verbatim | Same |
| Status = free picker from `ProformaInvoiceStatus.All` (Draft·Sent·Approved·Rejected·Expired·Cancelled); Void is NOT a status | `status` column kept verbatim (6-word CHECK, unknown values never re-mapped on read) + `lifecycle_state` (machine, 7-word CHECK) | **Two columns where WPF had two loosely-coupled concepts** — machine is the system truth, status stays the operator's display field, written exactly where WPF wrote it (picker, void) |
| `VoidAsync`: demands `ProformaInvoice.Delete`, idempotent, blank reason → "(no reason given)", `Status = VoidStatus[ProformaInvoice] = "Cancelled"`, keeps row/number/snapshot/audit | Same, verbatim | Same; `VoidedBy` stores the acting user id (WPF stored a username string); lifecycle_state also → Cancelled (schema addition) |
| No lifecycle timestamps | `sent_at`/`approved_at`/`confirmed_at` stamped by the machine on the corresponding transitions | **New** — schema 0003 columns the WPF lacked |
| Invalid transition / revise-a-draft / revise-a-voided: silent no-op or dialog | Typed **409** with the valid transition set / an operator-readable reason | **Different (REST, §0)** |
| Revision: root = `RootProformaId` string; chain = root + "-R{n}" rows by `RevisionNumber`; `"{root}-R{n}"` with `"-{guard}"` collision suffix (≤5); gates (Draft → no-op, voided/Cancelled → terminal); copies header + totals + snapshot as a new Draft | Same, but root is a UUID FK (`root_proforma_id`); the NUMBER keeps the WPF `"{rootNo}-R{n}"` shape; gates now 409s | Same behavior; **different root representation** (normalized), documented |
| Step 5 `GlassItemRow`: Sqm1 = `SqmCalculator.CalculatePanel(W1,H1)` (3dp + 0.5 floor), TotalSqm = round3(Sqm1×Qty), Sqm2/TotalSqm2 for W2/H2, Lm = round3((W+H)/1000 × perimeterFactor × Qty) with factor default 2, Value = `MoneyRounding(Sqm1×Qty×Rate)` | `Domain.Proforma.SpecGroupRowMath` — verbatim; sqm/total_sqm stored, Sqm2/Lm derived on read | Same |
| Step 5 validation (`SpecGroupService`): phantom row (blank ref + zero dims) allowed + filtered; dims > 0, ≤ 10000 mm; qty > 0, ≤ 10000 | Same, verbatim (phantom = blank position + all four dims zero, extended for the dual-measurement row) | Same |
| Step 7: `GlassMaterialsSubtotal = grid.GrandTotalValue` (Σ row values) | `glass_materials_subtotal` = Σ stored row totals, updated in the SAME transaction as the grid replace | Same |
| Grid save = one unit of work (single SaveChanges) | Whole-grid replace: DELETE groups (items cascade) + INSERT + invoice-field update in ONE transaction | Same |
| "Draft bodies are editable" + `IsEditable(Draft/Rejected)` | Grid save refused (409) unless Draft/Rejected; voided always terminal | Same rule, typed response |
| `ProformaDuplicateSnapshotScrub` (duplicate detaches inquiry link + rewrites ProformaId in the snapshot JSON) | `Domain.Proforma.DuplicateSnapshotScrub` (JsonNode port) inside `POST /{id}/duplicate` — see §1.2c | Done (7b) |
| `GlassPriceStamper` (sheet-store basis stamp: `{max(LastUpdated).Ticks}:{count}`) | Deferred to 7b/8 (needs the sheet store) | Deferred, documented |
| Inquiry convert-to-PI, Step 4 APPLY → specifications registry, Step 6 other charges, Step 9 P&L incl. target pricing, Step 7 summary + round-to | Done (7b) — §1.2b/§1.2c/§1.3a/§1.3b/§1.3c/§1.3d; Step 7 PDF/print remains deferred (needs a render surface) | Done except PDF (deferred, documented) |

### 1.2b Step 7 — financial summary (Phase 7b)

Ported 2026-09-27 from
`Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryViewModel.Recalculate`
(BUG-56 discount cap, BUG-57 batch restore, BUG-58 UAE VAT law) and
`Services/Reporting/InvoiceRoundTo.cs`.

| WPF (source of truth) | SaaS (Phase 7b) | Difference? |
|---|---|---|
| Waterfall: net = glass + other charges; discount = Percentage ? Round(net×v/100) : v, clamped to [0, net]; beforeVat = net − discount; vat = applicable ? Round(beforeVat×pct/100) : 0; grand = Round(beforeVat + vat) | `Domain.Proforma.InvoiceSummaryMath.Calculate` — verbatim | Same (goldens: 1200/5%/5% → 60/1140/57/1197) |
| Defaults: `IsVatApplicable = true`, `VatPercent = 5` (setter clamps 0–100), `DiscountType = Percentage` | Migration 0011 column defaults are exactly these; null request fields keep the saved values | Same |
| Mapper persists DiscountType (string), DiscountValue, IsVatApplicable, VatPercent + computed amounts | Same four input columns (0011) + discount_amount/vat_amount/grand_total_vat_inclusive (0003) | Same |
| VAT law: Article 34 — discounts come off the value of supply BEFORE VAT (BUG-58); a fixed discount can never exceed the net (BUG-56) | Enforced in `InvoiceSummaryMath` + CHECK constraints (non-negative discount value, VAT percent 0–100, type vocabulary) | Same |
| Round-to tool (`InvoiceRoundTo.DiscountOnNet`): closed-form seed + ≤40 ±0.01 iterations; switches the discount to FixedAmount; refuses a target above the undiscounted grand ("Cannot round up. Undiscounted total is AED …") | `Domain.Proforma.InvoiceRoundTo` + `POST /{id}/step7/round-to` — verbatim incl. the refusal message | Same |
| The summary recalculated LIVE in the wizard VM | `PUT /{id}/step7` recomputes from the SAVED Step 5/Step 6 subtotals (server-side, like every other derived amount) | Same math, REST shape |
| Live VAT default from the open wizard context | Per-invoice persisted inputs only; `tenant_vat_config` is NOT consulted by Step 7 (the operator sets VAT per invoice — the WPF's default is the flat 5%) | **Documented**: tenant VAT config remains pricing-page master data |
| Step 7 PDF layout / print preview | Deferred to its own 7b sub-item (needs the render surface) | Deferred, documented |

### 1.2c Duplicate + inquiry → PI conversion (Phase 7b)

Ported 2026-09-27 from
`SavedInvoicesListViewModel.DuplicateAsync` / `ScrubIdsForDuplicate`,
`Services/ProformaInvoice/ProformaDuplicateSnapshotScrub.cs`,
`InquiryListViewModel.ConvertToPiAsync`,
`ProformaInvoiceViewModel.StartFromInquiryAsync`,
`Services/ProformaInvoice/InquiryBoqConversion.cs` and
`Services/ProformaInvoice/InquiryLinkSync.cs`.

| WPF (source of truth) | SaaS (Phase 7b) | Difference? |
|---|---|---|
| Duplicate: `SuggestNextIdAsync(original.ProformaId)` → next free id from the ORIGINAL's number, 5-attempt unique-violation retry | Same loop over `ProformaNumberSequence.SuggestNext` + one-transaction `TryDuplicateAsync` (fresh context per attempt) | Same |
| Copy = new `SavedProformaInvoice` with the 6 financial columns + `FullSnapshotJson` (which carries Steps 1–7 + Terms + Step 9 snapshot); Status Draft; dates now / now+7; names copied | Copy = new invoice row (financial columns + the Step 7 input columns + P&L KPI columns — the normalized equivalent of the snapshot's Step 7/9 blocks) + DEEP COPIES of spec groups/items, Step 6 rows and P&L rows; Status Draft; IssueDate today, ValidUntil +7 days; company/client/project/salesman links copied | **Normalized storage instead of a JSON blob** — the same content, addressable per section; documented |
| `InquiryId` deliberately NOT copied + snapshot scrub (`Company.InquiryId = null`, `Company.ProformaId = newId`) so the copy cannot inherit the source's inquiry back-link (senior audit fix Aug 2026); a snapshot that cannot be scrubbed REFUSES the duplicate | `InquiryId` not copied; `snapshot_json` (a WPF import artifact) scrubbed via the `DuplicateSnapshotScrub` JsonNode port; an unscrubbable snapshot → 409, never the original JSON | Same rule; the SaaS scrub is the JsonNode path (the WPF's own fallback — there is no mapper snapshot model server-side) |
| Root/revision chain NOT carried on the duplicate (a duplicate is a fresh quotation) | `root_proforma_id` null, `RevisionNumber` 0, `job_order_id` not copied | Same |
| Convert-to-PI guard: inquiry's denormalized `ProformaInvoiceId`/`ProformaNumber` (kept in sync by InquiryLinkSync — the guard was dead code before that fix) | `converted_proforma_id` PLUS a live `AnyAsync(proforma_invoices.inquiry_id)` check (the source of truth — no denormalized column to go stale) | **Stronger by design**: revisions/imports that link a PI also block |
| Eligibility: only Prepared or Confirmed inquiries; the VM guard throws `InvalidOperationException` | 409 with the WPF's message ("Only a Prepared or Confirmed Inquiry is eligible…") | Same rule, typed response |
| Convert pre-fills the WIZARD (Step 1 inquiry id, Step 2 customer/billing/salesman, Step 3 project reference, Step 5 grid from the BOQ) and the operator saves manually | Convert CREATES the linked Draft + saves the BOQ grid server-side in one call (clientId/projectId links are the normalized Step 2/3 context; salesman name has no SaaS link on inquiries yet) | **Different shape, same content**: an API cannot hold an unsaved wizard; the created Draft is exactly what the WPF wizard would have produced, and the operator continues editing it through the normal endpoints. The WPF's ProjectReference free-text (inquiry description fallback) is not carried — the SaaS grid + inquiry link carry the traceability |
| BOQ import decisions (`InquiryBoqConversion`): importable = spec name + W×H + qty; order = SortOrder; glass type normalized with DGU+LAM matched FIRST; unknown build REFUSED (never inherits a default); a non-positive rate means "not quoted" (grid keeps its own pricing) | `Domain.Proforma.InquiryBoqConversion` — verbatim port, same goldens; one spec group per importable line, one item row (W1/H1/qty), quoted rate → `price_source = manual` (the customer's own), unquoted → `formula` with rate 0 | Same decisions; "manual" is the SaaS normalization of the WPF's ImportedPrice (no default-type or free pricing source exists there) |
| Partial BOQ rows "stay on the inquiry as notes" | Not imported, reported as `SkippedBoqLines` in the convert response | Same behavior, explicit count |
| InquiryLinkSync.AfterInsertAsync: write the link columns + advance the inquiry to Sent (only when CanTransition allows — a Confirmed inquiry stays Confirmed); fire-and-forget | Same: `converted_proforma_id` write + status advance gated by the ported `InquiryWorkflow.CanTransition` table | Same |
| InquiryLinkSync.AfterUpdateAsync: PI Approved/Confirmed → inquiry Confirmed; Rejected/Cancelled → Cancelled (skips silently when the transition is invalid; never fails the PI op); PI deleted → inquiry reverts to Prepared | Same mapping on lifecycle transitions AND void (there is no PI delete in the SaaS — void is the terminal path); tolerant (try/catch, failures audited as `inquiry.link_sync_failed`) | Same minus the delete-revert path (no PI delete exists — documented) |
| Duplicate permission: `ProformaInvoice.Create` | `POST /{id}/duplicate` → `ProformaInvoice.Create`; `POST /api/v1/inquiries/{id}/convert-to-proforma` → `ProformaInvoice.Create` (the WPF convert path runs the PI-creation flow) | Same |
| Inquiry statuses `Received·Prepared·Sent·Confirmed·Hold·Cancelled` + "On Hold"/"Canceled" aliases; the transition table (Received→Prepared/Hold/Cancelled, …) | `Domain.Proforma.InquiryStatuses` + `InquiryWorkflow.CanTransition` — verbatim (0003's inquiries CHECK already carries the vocabulary) | Same |

### 1.3 Specifications (price registry)
| WPF | SaaS |
|---|---|
| `specifications.db` + `SpecificationsCenterView` | `Specification` entity per tenant |
| Step 4 APPLY → save priced spec; `SpecificationLedgerService`; `SpecPaneResolver` | `POST /api/v1/specifications`; stamping on reuse |
| `SpecificationTargetCalculator` / `TargetPricing` / `TargetSnapshotRecompute` | Target-pricing service, recompute on document load |

### 1.3a Specifications registry (Phase 7b) — parity and deliberate differences

The Step 4 library + the ledger-derived usage trail, ported 2026-09-27
from `SpecificationsRepository.cs` and `SpecificationLedgerService.cs`
(ground-truth comments preserved in the service).

| WPF (source of truth) | SaaS (Phase 7b) | Difference? |
|---|---|---|
| `SaveOrUpdateAsync` (Step 4 APPLY): dedup by GlassType + SpecName; rate ALWAYS refreshed; catalogue columns refreshed only when supplied (a valueless save must not wipe an earlier, better-informed save); demands ProformaEdit | `SpecificationsService.ApplyAsync` — verbatim, `POST /api/v1/specifications` | Same |
| `UsageCount` starts at 0; APPLY NEVER touches counts ("being built in Step 4 is not being used") | Same — 0008 changed the Phase 3 column default from 1 to 0 | Same (default corrected to match WPF) |
| `SpecificationLedgerService`: counts/history/provenance DERIVED from SAVED invoices' Step 5 groups only; purge orphans; `PricePerSqm` stays estimator-owned; NEVER THROWS | Set-based `ReconcileUsageAsync` over the normalized spec_groups rows (migration 0008 + one transaction): purge pairs that no longer exist, insert missing (spec, invoice) pairs with names from the SAVED invoice row, heal usage_count/last_used_at/last_proforma_no | Same ground truth, mechanical translation (no JSON snapshot walking needed); runs after every grid save (tolerantly — failure is audited, never a save failure) and on `POST /specifications/reconcile` (the Center's Refresh) |
| Usage rows appended per save (the inflation the WPF ledger had to purge) | `UNIQUE (spec_id, proforma_id)` — exactly one verified use per pair ("2 PIs × 2 specs = 4 uses"), making reconcile idempotent | **Different by design** (the WPF's own ledger treated per-save appends as the bug) |
| `GetAllAsync` smart sort: UsageCount DESC, LastUsedAt DESC; glass-type filter | Same | Same |
| `GetUsageHistoryAsync` (UsedAt DESC), `GetRecentUsageAsync(limit=200)` | Same (usage history 404s an unknown spec; recent usage default 200, `limit ≤ 0` → 200 as in WPF) | Same |
| Usage row carries the PI number string + customer/company/project names from the draft-time context | Usage row carries the invoice id; the number and names are joined from the SAVED invoice at read time ("authoritative, not from the draft-time context Step 4 saw") | Same intent, normalized |
| `DeleteAsync` demands ProformaDelete; silent no-op on missing id | Same permission; missing id → **404** (documented REST difference) | **Different (documented)** |
| Catalogue columns (2026-09-02): category/color/thickness/sheet size/sheet id/panes_json, read-only Sheet Store relationship for matching | Migration 0008 adds the same columns (nullable/defaulted — pre-existing rows read blank, like the WPF's additive migration); `sheet_id` → glass_sheets FK ON DELETE SET NULL | Same (types normalized: int sheet id → uuid FK) |
| Reconcile triggers: startup + debounced after every invoice write + Center Refresh | After every grid save + reconcile endpoint (SaaS has no long-lived app process to run startup passes) | Same triggers, SaaS-native timing |
| `SpecCatalogNotifier.NotifyChanged()` (UI dropdown refresh) | N/A — HTTP responses carry the fresh state | Architectural translation |

### 1.3b Step 6 — Other Charges / Wastages / Custom Charges (Phase 7b)

Ported 2026-09-27 from `OtherChargeRow` / `WastageRow` / `CustomChargeRow`,
`SpecTargetSelection` / `SpecCheckOption` / `MetricOption`,
`MetricDiscoveryHubViewModel`, `WastagesSectionViewModel.SyncSourcesForRow`,
`ProformaChargeMetricsCalculator` and the mapper's Step 6 snapshot contract.

| WPF (source of truth) | SaaS (Phase 7b) | Difference? |
|---|---|---|
| Metric registry: LM1/LM2/LM_COMBINED, SQM1/SQM2/SQM_COMBINED, DGU_CAVITY, QTY, DOUBLE_GLASS (+ legacy aliases TOTAL_PIECES/TOTAL_QTY→QTY) | `MetricKeys` — verbatim | Same |
| ComputedValue = Σ checked specs' metric (per-spec 2 dp read model over 3 dp grid footers) | `ChargeMetrics.TargetedMetric` — verbatim | Same |
| Pane factor: DGU+LAM=3, DGU/LAM=2, else 1; effective type = spec-name markers first, then the type field, SGU fallback | `SpecGrammar.ResolveEffectiveGlassType` — verbatim | Same |
| Polish: 12 sub-type formulas + Custom (manual qty); both panes of a double build; same-glass LAM doubles the single measurement; 3 dp total | `PolishSubTypes` + `ChargeMetrics.PolishLm` — verbatim | Same |
| DELIVERY row: MetricQty hardcoded 1 TRIP, target selection ignored (Chat 7 Bug 4c; multi-trip = custom charges) | Same | Same |
| Charge amount = IsActive ? MoneyRounding.Round(qty × rate) : 0 | `ChargeMetrics.ChargeAmount` — verbatim | Same |
| Wastage manual: Σ checked source SQM × rate, `Math.Round(x, 2)` (the WPF's plain banker's rounding — an in-WPF inconsistency vs MoneyRounding, preserved) | `WastageFormula.CalculateManual` | Same (documented quirk) |
| Wastage optimization: Section1 = price ÷ opt% (6 dp), Section2 = price ÷ 85% (6 dp), rate = gap, amount = Round(rate × sqm); any non-positive input zeroes the row; rate is DERIVED and overwrites the display | `WastageFormula.CalculateOptimization` (ported in Phase 4 with WPF goldens); the derived rate is stored at numeric(18,6) | Same |
| Wastage sources: per spec group — Outer always; Inner when DGU/LAM or the grid carries W2/H2 (inner SQM = ΣSQM2 if >0 else ΣSQM1); middle panes for 3+ pane builds ("Middle (W2/H2)" / "Middle n (W2/H2)"); DGU+LAM = Outer + Lam Outer + Lam Inner; persistence key "{spec}-{SideLabel}" | `ChargeMetrics.SourcesForGroup` + `WastageSource.Key` — verbatim | Same |
| Source colours: parsed from the spec name first, then the group's explicit colours, then the live Step 4 panel (UI-time) | Parsed name → group's SAVED colours (the Step 4 panel sync already ran when the grid was saved) | Same values, normalized provenance |
| Custom charges: qty × rate, MoneyRounding | `custom_charges` table + `ChargeMetrics.ChargeAmount` | Same |
| Subtotal (RecalculateTotal): Σ active predefined + wastage + custom, `Math.Round(x, 2)` (banker's — preserved verbatim) | `ChargeMetrics.OtherChargesSubtotal` → `proforma_invoices.other_charges_subtotal` | Same |
| Save contract (mapper snapshots): template identity, unit, metric key, checked spec numbers, rate, polish sub-type + custom polish fields; wastage: colour, checked source keys, is_manually_added, use_optimization_formula, formula price, optimization %, sheet qty; custom: name/qty/unit/rate | Migration 0009 columns on `other_charges`/`wastage_rows` + new `custom_charges` (RLS) — normalized instead of one JSON blob | Same fields, normalized storage |
| Metric qty is always RECOMPUTED live from the grid (never stored from the UI) | Recomputed SERVER-SIDE on every save from the saved Step 5 grid (never trusted from the client) | Same rule, enforced at the API boundary |
| Advanced automation (suggestions engine) | **DONE (7b):** `Domain.Proforma.ChargeAutomation` (WPF-verbatim `OtherChargeAutomationService` port) + `GET /{id}/step6/suggestions` — per saved spec group: implied predefined charges + wastage pane sides. The web client applies the decisions through the normal Step 6 save. Optimization feed + inventory auto-use + manual-override tracking remain later-phase items (they need the optimization/stock modules) | Suggestions done (documented); the rest deferred |
| Wastage rows survive Step 5 edits (sources re-synced, missing keys silently dropped) | Stale source keys at save → **400** ("re-fetch the section after saving the grid") | **Different (documented)** — a stale key would silently under-count; the client re-fetches sources after every grid save |
| Phase-3 schema guesses fixed by 0009: metric keys 'SQM'/'Fixed' → real registry; wastage stock_sqm/glass_sqm/spec_group_id dropped (no WPF counterpart); wastage rate widened to 6 dp (the derived rate truncated at 2 dp); spec_group_items.perimeter_factor added (LM round-trip — the 7a read path recomputed with the default factor); spec_groups Step 4 provenance columns (colours + per-side stock sheets, SpecGroupSnapshot Phase G) | | Schema correction, WPF-aligned |

### 1.3c Step 9 — Profit & Loss (Phase 7b)

Ported 2026-09-27 from `Step9ProfitLoss/ProfitLossStepViewModel.{Calculation,
QuoteBasis,CalculationSupport}.cs`, `PnLMeasurement.cs`, `PnLSpecParser.cs`
(grammar already ported as `SpecGrammar`), `Step6SheetAllocation.cs`,
`Services/ProformaInvoice/OtherChargeAutomationRules.cs` (the U-insert
suggestion) and `Services/ProformaInvoice/SheetStorePricingService.cs`.
Design facts 85–95 in `SAAS_MIGRATION_PROGRESS.md`.

| WPF (source of truth) | SaaS (Phase 7b) | Difference? |
|---|---|---|
| Live analysis over the saved document: rows rebuilt from Step 5 groups + Step 4 provenance + Sheet Store + Step 6 charges on every recalc; snapshot persisted | `PUT /{id}/step9` recalculates + persists (`pnl_rows` replace + KPI columns on the invoice, one transaction); `GET /{id}/step9` returns rows + KPIs | Same math, REST shape |
| Measurement: cutSqm = Σ (CalculatePanel(W1,H1) [+ (W2,H2)]) × qty (the Step 5 0.5 m² billable floor); AreaWithWastage round4; WholeSheets ceil, min 1; yield = 1 − w/100 floor 0.05 | `PnLCalculator.MeasureGroup` / `AreaWithWastage` / `WholeSheetsRequired` / `EffectiveYieldFactor` — verbatim | Same |
| Glass rows: Step 4 pane box price WINS over Sheet Store; exact store lookup = Norm(colour) + thickness + rotation-tolerant size, priced-first/cheapest; cost basis sell-vs-purchase; a Step 4-identified pane without a sheet handoff is a DATA-GAP row (never a catalogue-invented size); Step 6 wastage setup SHEET QTY is authoritative for the pane (shared rows split by the largest-remainder allocator; DISABLED rows still own the setup qty); SGU priced by required area, multi-pane by whole sheets; unpriced panes visible at amount 0, excluded from cost | `PnLCalculator.BuildGlassRow` + `SheetStockIndex` + `Step6SheetAllocation` — verbatim | Same |
| Panes parsed from the spec name in order; `EnforceStep4PaneIdentity` replaces parsed colours with the group's explicit colours; a missing inner of a two-pane build is synthesised from the LIVE Step 4 panel; DGU+LAM panes come from the live panel | Parsed + enforced exactly; NO synthesis from a live panel (no server-side Step 4 tab — the WPF itself calls it "legacy fallback only"): an unparsed spec name yields no glass rows, the operator priced that build manually | **Documented difference (§1.3c header)** — data-gap rows instead of panel guesses |
| Production rows: DGU+LAM = ASP per cavity + outsourced interlayer at BUY + processing on all three panes; DGU = ASP per SQM (includes production); LAM = interlayer + processing both panes; SGU = separate cutting + tempering; ALL rates read the GROUP's Step 4 provenance first (Phase 1-AUDIT) | Verbatim against the new nullable `spec_groups` provenance columns (0010) — a legacy group reads null → 0, never the live tab | Same; null-handling is the normalized form of "no handoff" |
| Step 6 charges → production costs: ACTIVE rows, per checked spec at the same metric qty (polish via sub-type); excluded supplemental keys ARGON_GAS/FAN_HOLES/CUTOUT/HOLES/COUNTERSUNK_HOLES; DELIVERY = one invoice-wide TRIP; custom charges invoice-wide; wastage NEVER a P&L cost (2026-09-07 rule); U-insert tracked to avoid double-count | `PnLCalculator.AppendStep6Costs` — verbatim | Same |
| KPIs: NetExVat = Round(gross / 1.05, 2) — VatDivisor hardcoded, the bound netRevenueProvider is vestigial (Calculation.cs:547 quirk); Profit; IsLoss only with revenue; three margins (cost-based on GROSS + basis, overall on NET + actual cost, net margin on net revenue); U-insert price-based (suggestion qty × seed→row→template-10 rate) else tracked production amount; basis = Σ QuoteBasis × area else actual − u-insert | `PnLCalculator.Recalculate` KPI block — verbatim, quirk preserved | Same (the /1.05 quirk documented, per §49) |
| QuoteBasisFor: DGU = (outer+inner)/yield + spacer×cavities; LAM = (outer+inner)/yield + interlayer+cutting+tempering; DGU+LAM = (outer+lamOuter+lamInner)/yield + processing + spacer×cavities; SGU = outer/yield + cutting+tempering; missing input → 0 | Verbatim; lamOuter reads 0 server-side (the WPF's live-panel column has no group counterpart) | **Documented**: the only live-panel rate the WPF still reads (LamOuterBasePrice) has no persisted source — the DGU+LAM basis under-counts that pane by design until a lam-outer provenance column exists |
| Advanced automation suggestion rows (AppendAdvancedSuggestedCharges for unchecked-but-present rows, UseAdvancedAutomation) | Still deferred (the UseAdvancedAutomation toggle is a WPF UI control with no persisted column; the SaaS costs only explicitly saved Step 6 rows). The decisions half is now live (§1.3b suggestions row) so the client can persist exactly what the WPF preselected. The U-insert KPI still recomputes from the suggestion rule verbatim | **Deferred (documented)** |
| Target-pricing model (SpecTargetRowViewModel, 1059 lines) | Deferred to its own 7b sub-item | Deferred, documented |
| `BuildStep9SpecDisplay`: spec-name colour tokens rewritten from the group's authoritative colours | Verbatim (glass/spacer/production rows); Step 6 charge rows keep the RAW spec name with the "SPEC n" fallback (WPF FindSpecName) | Same |
| WPF `PricePerSqm`/`UnitLabel` on rows; UnitLabel defaults "SQM" | `pnl_rows` columns price_per_unit/unit_label | Same |


### 1.3d Step 9 — Target pricing (Phase 7b)

Ported 2026-09-27 from `Services/ProformaInvoice/TargetPricing/*`
(TargetPriceModels, SpecificationTargetCalculator, TargetMarginTolerance,
Step5PriceSource, Step6ChargeResolver) and the Step 9 adapter
`ProfitLossStepViewModel.TargetPricing.cs` (RebuildTargetResults +
BuildRateCard + SpecRequiresUInsert + FindUInsertRow /
UInsertRowActiveForSpec). `Domain.Proforma.TargetPricing` carries the
model + calculator + resolver + aggregator verbatim;
`SpecificationTargetBuilder` is the SaaS adapter; the panel rides on the
step 9 GET/PUT responses as `targets` (per spec + job aggregate).

| WPF (source of truth) | SaaS (Phase 7b) | Difference? |
|---|---|---|
| Given price = SpecGroup.BaseRate (Step 5) | `spec_groups.rate_per_sqm` (SpecGroupInput.RatePerSqm) | Same |
| Base price = pre-uplift formula result rounded 2dp — (25+25)÷0.85+45 = 103.82; target = ROUND(base × 1.15) = 119.39 | Verbatim (MoneyRounding) | Same |
| KPI trigger: margin judged GIVEN vs BASE (never vs the required target); status pills judged vs the required target (±0.005 band) | Verbatim | Same |
| U-insert (revised 2026-09-04): the required target NEVER moves; Additional = billed as its own Step 6 line, rate display-only; IncludedInStep5Price = netted out of the judged given price; Unresolved when no rate anywhere (seed → row → template default) | Verbatim (Step6ChargeResolver + ApplyUInsert) | Same |
| Row semantics: FIRST U_INSERT row decides (rate read even when inactive); active-for-spec = row active AND group checked in the two-part targeting | Verbatim (BuildUInsertQuery reads the persisted Step 6 charges in SortOrder) | Same (an empty targeting list targets no spec — same as the WPF TargetSelection with nothing checked) |
| Yield = 1 − wastage%/100 (the Step 9 box), ≤0 floored at 0.05 | Same (SpecificationTargetBuilder.EffectiveYieldFactor, reads the saved pnl_wastage_percent) | Same |
| Rate card: group's Step 4 handoff columns authoritative; LEGACY rows fall back to the LIVE Step 4 panel VMs (and Step 6 wastage-source prices) | Handoff-only — a server has no live panel; missing handoff ⇒ honest Missing* status, never a guess | **Documented difference**: the legacy rescue paths are not portable; the WPF itself labels them "legacy fallback only" |
| DGU+LAM third pane: WPF passed groupRate 0 (no third-pane column on its SpecGroup) and used the live panel | `third_sheet_price_per_sqm` (persisted since 0010) — the handoff-first rule now covers the third pane | **Improvement, documented** |
| TargetUpliftPercent = the Step 9 "expected profit margin %" operator box (default 15, the approved model fixes 15) | Fixed at the approved 15% (the box is not a persisted SaaS input; the approved model pins 15) | **Documented difference** |
| Snapshot persistence: the VM saved a SpecTargetSnapshot block + TargetSnapshotRecompute rebuilt it on view (old invoices self-correct) | COMPUTED ON READ from the normalized inputs (grid rates + Step 4 handoff + Step 6 charges + Step 9 yield) — always current by construction, nothing to rebuild | **Documented difference (better: the normalized tables are the snapshot)** |
| Groups with cut area 0 skipped from the target panel | Same (PnLCalculator.MeasureGroup ≤ 0 → skipped) | Same |
| Aggregator: value-weighted job totals, area-weighted averages, null (never 0) when ANY spec incomplete; cost-based margin counts only an ACTIVE U-insert's cost | Verbatim | Same |

### 1.4 Pricing engine (cross-cutting — single service, master prompt §40)
| WPF | SaaS |
|---|---|
| `CalculateSguRate/DguRate/LamRate/DguLamRate` + `*Basis()` cost bases | `IPricingCalculator` with 4 methods + cost-basis methods; **one implementation** used by quotation, invoice, job, reports |
| `MoneyRounding` (2dp AwayFromZero) | `Money` value object; PostgreSQL `NUMERIC(18,2)`; same rounding |
| Yield ≤ 0 → fallback 1.0 + warning | Same, warning returned in API response meta |
| Interlayer unpriced → warning (not error) | Same, warning surfaced in UI |
| Colors only from Sheet Store | Same — colors endpoint sourced from stock |

### 1.4a Pricing engine — Phase 6 implementation (rate formulas, defaults, config endpoints)

Ports of the Step 4 estimator read 2026-09-27
(`PricingEstimatorViewModel.Part2.cs` L480–740,
`DguLamParametersViewModel.cs`, `Sgu/Dgu/LamParametersViewModel.cs`,
`Utilities/SqmCalculator.cs`, `WastageRow.RecalculateOptimizationResults`).
Every rate is `decimal`, ends in `MoneyRounding.Round`, and `SafeYield`
(yield ≤ 0 → 1) is applied identically to rate and cost basis.

| WPF (source of truth) | SaaS (Phase 6) | Difference? |
|---|---|---|
| SGU rate = (base/yield + cutting + tempering) × (1+margin/100) | `RateCalculator.Calculate(SguParameters)` — same steps 1–5, same variable names | Same (golden 46/0.85/15/25/15 → 108.24) |
| DGU rate = ((outer+inner)/yield + spacer) × (1+margin/100) | `RateCalculator.Calculate(DguParameters)` | Same (WPF in-code example (25+25)/0.85+45 → basis 103.82, ×1.15 → 119.40) |
| LAM rate = ((outer+inner)/yield + PVB + cuttings + temperings) × (1+margin/100) | `RateCalculator.Calculate(LamParameters)` | Same (WPF example → 228.65) |
| DGU+LAM rate = (98/0.85 + 30 + 32 + 110 + spacer) × 1.20 | `RateCalculator.Calculate(DguLamParameters)` — cutting/tempering per PANE (12+10+10), interlayer inside the margin (110 × 1.20 = 132), spacer opt-in at 0 | Same (the business chain 344.75 is pinned test-first) |
| `*Basis()` cost bases (SafeYield, no warning) | `RateResult.CostBasisPerSqm`, same fallback silently | Same |
| Yield ≤ 0 → fallback 1.0 + warning texts (BUG-40/41): `⚠  {SGU\|DGU\|LAM} yield factor must be > 0 (currently {0.###}). Using 1.0 as fallback.` / DGU+LAM: `⚠  Yield factor must be greater than 0 (currently {0.###}). Using 1.0 instead.` | Verbatim, returned in `POST /api/v1/pricing/preview` → `warnings[]` | Same texts, machine-readable array instead of a UI string |
| DGU+LAM unpriced interlayer → `⚠  No selling price set for the {InterlayerType} interlayer, so it is NOT included in this rate.` | Verbatim (SGP ships 0/0 on purpose — no price was ever quoted; warning, never an invented price) | Same |
| `CalculateSuggestedSpacerPrice`: 37.5 + ((mm−6)/2)×2.5 + surcharge {NMA 0, Black Alu 5, Warm Edge 15, Black Warm Edge 20, SS 10, TGI 12}; ParseWidthMm fallback 12 | `SpacerPricing.SuggestedPrice` — verbatim incl. case-sensitive material switch and "digits before mm" parse | Same (12mm ASP + NMA → 45.00) |
| `WastageRow` optimization: S1 = price/(opt%/100), S2 = price/(85%/100), rate = S1−S2 (6dp), amount = money(S1−S2)×sqm; manual = round(sqm×rate, 2) | `WastageFormula` — verbatim, `BaselineOptimizationPercent = 85` const | Same (golden 40/70%/10m² → 57.142857/47.058824/10.084033/100.84) |
| `SqmCalculator`: panel = round3(w×h/1e6) with 0.5 m² floor; total = panel×qty at 3dp; sheet = round4, NO floor | `Domain.Pricing.SqmCalculator` — verbatim | Same |
| Four panel VM constructors (hardcoded opening numbers) | `PricingDefaults` (Domain) + read-only `panelDefaults` in `GET /api/v1/pricing/config`; DGU+LAM defaults ARE the worked chain (fresh WPF VM quotes 344.75 unedited — WPF `DguLamPricingTests` pins this) | Same values; read-only because the WPF hardcodes them |
| `ApplyInterlayerDefaults`: PVB 100/110, EVA 75/95, SGP 0/0 | `PricingDefaults.InterlayerPrices`, exposed read-only in config | Same |
| Hardcoded dropdown lists in the four VMs | Seed of `tenant_pricing_config` catalogs — **tenant-editable now**, WPF lists as defaults. Merges (schema has ONE list per catalog, panels differed): thickness = union 3–19 **including both 15mm (SGU/DGU/LAM) and 16mm (DGU+LAM quirk)**; treatments = SGU display form ("FT Glass"; DGU/LAM/DGU+LAM spell it "FT" — same four treatments); interlayer thicknesses = union incl. LAM-only 3.04mm; panel extras = the DGU 18-list (superset of SGU's 13) | **Different by design** (multi-tenant editability, master prompt §40/API design); each merge listed here, nothing invented |
| Lists not in the schema's 7 editable catalogs: spacer widths/materials/extras, edge polish (13), PVB colors (7), interlayer colors (7) | Read-only `fixedCatalogs` in the config response (WPF-verbatim constants) | Same values; Phase 13+ may promote them to editable |
| Colors fed exclusively from Sheet Store (2026-09-10 note) | Unchanged — colors are NOT part of pricing config; stock endpoint (Phase 9/10) owns them | Same |
| No settings UI (lists hardcoded in code) | `GET/PUT /api/v1/pricing/config` (PUT = `Settings.Manage`, GET = authenticated); `POST /api/v1/pricing/preview` (authenticated; pure calculation, no rows written) | **New** — SaaS-native settings surface; a fresh tenant reads WPF defaults and NO row is created until the first PUT |
| VAT rate/currency hardcoded (5% AED in fiscal layer) | `tenant_vat_config` row (default 0.05/AED), editable in the same config endpoint | **Different by design** — per-tenant VAT is required for SaaS |
| `pricing/apply` → stamps rate onto Step 5 spec groups | Not in Phase 6 — arrives with the proforma wizard (Phase 7), where spec groups exist as entities | Deferred, documented |

### 1.5 Sheet Store
| WPF | SaaS |
|---|---|
| `GlassSheet` (+unique index), `SheetHistory`, `InventoryItem` (LM consumables) | `GlassSheet`, `StockTransaction`, `InventoryItem` entities; movements only via transactions |
| Buy/Use/History/Items/CSV views | `/stock` module: purchases, issues, history, items, CSV/Excel import (preview→confirm) |
| Low stock `GlassSheetLowStock` | Low-stock endpoint + dashboard widget |
| `SheetStorePricingService` / `SheetStockLinkService` | Server services linking specs ↔ live prices/balances |
| Balance = Stock − Used; `MaxSheets` caps w/ sentinels (0=∞ explicit, 99999999=default availability) | Same semantics on `StockSize` DTO for optimizer |

#### 1.5a Sheet Store — Phase 8 implementation (parity and deliberate differences)

Ported 2026-09-27 from `Services/SheetStore/SheetStoreRepository.cs`,
`InventoryItemRepository.cs`, `Models/SheetStore/GlassSheet.cs`,
`InventoryItem.cs`, `GlassSheetLowStock.cs`. `StockService` +
`StockStore` + `/api/v1/stock` (sheets: CRUD, filtered/sorted/paged
search, filters, buy, use, history, low-stock; items: CRUD, receive,
consume, history, low-stock, suppliers).

| WPF (source of truth) | SaaS (Phase 8) | Difference? |
|---|---|---|
| `SearchAsync`: category/color/supplier exact, thicknessMm (>0), free-text contains on category+color+supplier, sort keys newest/oldest/stockasc/stockdesc else Category→Color→Thickness, skip/take default 200 | Same filters + sort keys verbatim; SaaS `PagedResult` envelope (page/pageSize default 50, max 200 — the WPF's take-200 stays the cap); thickness searchable via the exact filter only (the WPF's own comment: the display is computed) | Envelope only (documented) |
| `AddAsync`: timestamps now; stock > 0 ⇒ LastPurchaseDate now | Same (UTC; DateOnly) | Same |
| (no identity constraint in SQLite) | `uq_glass_sheets_identity` (tenant, category, thickness, color, W, H, supplier) → typed 409 on duplicate | **Stricter by schema (0002), documented** — the WPF allowed silent duplicate rows |
| `UpdateAsync` / `DeleteAsync` (guarded SheetStore.Edit/.Delete) | Same permissions (`SheetStore.Create/.Edit/.Delete`, ported in Phase 4's vocabulary) | Same |
| `BuyStockAsync`: SQL-side `Stock += q` (the lost-update fix), optional price overwrite only when > 0, LastPurchaseDate now; quantity ≤ 0 silently ignored; missing sheet silently ignored | Same atomic increment + price rule + stamp, in ONE transaction WITH the ledger row; quantity ≤ 0 → 400 "Quantity must be greater than zero."; missing sheet → 404 | **Documented difference**: an API must not answer success for a write that did nothing (the WPF's own InventoryItemRepository throws for the same cases) |
| `UseStockAsync`: guarded atomic `Used += q` only when `Stock − Used ≥ q` (guard IN the UPDATE's WHERE); short stock throws "Not enough stock. Available: {balance}, Requested: {qty}"; missing sheet silent | Same guard verbatim + the same message (typed 409); missing sheet → 404 | Same message; the silent-missing case is a 404 (documented, same reason as above) |
| (no movement ledger — balance was the only record) | Every movement appends an immutable `stock_transactions` row (purchase/issue; receive/consume for items) with quantity_delta (signed), balance_after, unit_cost, performer, notes — in the SAME transaction as the increment | **Improvement by schema (0002, designed in Phase 3), documented** |
| `GlassSheetLowStock.Threshold = 5` (the ONE declaration; balance ≤ 5) | Same — `GET /stock/sheets/low-stock`, ordered by balance | Same |
| Computed read model: SqmPerSheet (mm→SQM), Balance, TotalSqm{InStock,Used,Balance}, Total{Purchase,Sell}Value, BalanceValue, ProfitValue, Used{Cost,Sell,Profit}Value, BalanceCostValue, ProfitPerSqm, Size/ThicknessDisplay | Same fields on `GlassSheetDto` (derived server-side, never client-posted) | Same |
| `InventoryItemRepository.ReceiveAsync`: quantity ≤ 0 throws "Received quantity must be greater than zero."; price overwrite only when > 0; missing item silent | Same message verbatim (typed 400); atomic SQL increment + ledger row; missing → 404 | Same message; 404 vs silent (documented) |
| `ConsumeAsync`: ≤ 0 throws "Consumed quantity must be greater than zero."; short stock throws "Not enough stock. Available: {balance:N2} {unit}, requested: {qty:N2} {unit}."; missing silent | Same messages verbatim (typed 400/409); guarded atomic + ledger | Same messages; 404 vs silent (documented) |
| `IsLowStock` = ReorderLevel > 0 && Balance ≤ ReorderLevel (a zero level never triggers), ordered by balance | Same | Same |
| `GetLowStockAsync` loaded the whole table (computed props can't translate) | `WHERE ReorderLevel > 0 AND Stock − Used <= ReorderLevel` in SQL | Same result, translated |
| Item categories: SpacerBar/Desiccant/PrimarySeal/SecondarySeal/Gas/Hardware/Film | 0002 CHECK: spacer/sealant/desiccant/gas/hardware (sealant covers the two seal types; film dropped) | **Schema parity decision from Phase 3 (0002 comment), documented** |
| `InventoryItem.LastPurchaseDate` set by Receive | No such column in 0002 — the ledger's purchase rows (created_at) are the queryable equivalent | Documented |
| `SheetHistory` (SQLite table) | `GET /stock/sheets/{id}/history` + `/stock/items/{id}/history` (paged, newest first) from the ledger | Same intent |
| SheetStore CSV/Excel import (preview→confirm), `SheetStorePricingService` / `SheetStockLinkService` | Later Phase 8 sub-items (import needs file storage; the pricing link is already served by the Step 4 price handoff + Step 9 re-costing) | Deferred, documented |

### 1.6 Optimization
| WPF | SaaS |
|---|---|
| `GuillotinePacker.*` (~11k lines), `OptimizationModels` | **Ported as-is** into `ProGlass.Saas.Domain.Optimization` (same algorithms, same names) |
| Enums: RotationPolicy(Locked/Free90), SplitMode, PlatePattern×6, CutComplexity×6, ScaleTier, ThicknessBucket | Identical enums |
| MarginProfile house table (15/30/50-50-0-50, kerf 3/4, breakout 15/30/50/40) + IsCustomized explicit | Tenant-editable margin config, WPF defaults; explicit customization flag |
| PackQuick / PackOptimize (multi-start, 147 plans) / PackSmart | Same entry points; runs as **background job** (`POST /optimization/jobs` → poll/SSE) |
| Oracles: Verify (geometry) + ValidateResult (conservation); failing plan never exported | Server re-validates every result; `POST /api/v1/optimization/validate` public; same export gating |
| Calibration benchmark (Low-E 17@82.77%, Clear 16@84.92%, Ramly 1×2500×1875) | Golden regression tests in CI |
| Layout visualization (SkiaSharp preview) | Server-rendered SVG/Canvas layout (sheet boundary, pieces, dims, labels, rotation, utilization, wastage, trims, kerf) matching validated result exactly |
| Sessions in `optimization.db`, document keys | `OptimizationSession` per tenant+document |
| CNC export, cut lists in stage order | Same exports (server-generated) |
| `ProductionOptimizationBridge` | Same bridge into production module |

### 1.6b Optimization — Phase 25a, the OPTIMIZE chassis (parity and deliberate differences)

Ported 2026-09-27 from `Services/Optimization/OptimizationSearchGovernor.cs`
(SearchTier/SearchPlan/governor, 437 lines) and the OPTIMIZE half of
`Services/Optimization/GuillotinePacker.cs` (PackOptimize L353–881:
the multi-start grid, the comparison chain, the bounded open-sheet
window, ConsolidateAll, the verification oracles, cut-sequence
derivation). Domain:
`OptimizationEngine.Optimize.cs` (partial GuillotinePacker);
Application: `OptimizationService` gains `effortLevel` + `platePattern`
on the run request.

| WPF (source of truth) | SaaS (Phase 25a) | Difference? |
|---|---|---|
| `PackOptimize` stage/effort split (maxStage > 0 ? maxStage : effortLevel; stage ≤ 1 = exactly PackQuick) | Same in `RunOptimizeGrid`; the API clamps effort to 1..5 (null → 3, the WPF signature default) | Same (clamp is input sanitation at the edge) |
| The multi-start grid: stage 2 = 3 orderings; 3 = 4 orderings × 3 splits; 4/5 = 6 × 3 × deterministic shuffles (`ShuffleCap` from the governor) | Same combos, same sort cache (one sort per ordering key), same candidate labelling (`GRID ({order} / {split} / shuffle {k})`, one-candidate runs keep the QUICK label) | Same |
| `ShuffleDeterministic` (LCG `state*1103515245+12345`, `j = (uint)state % (i+1)`) | Verbatim — reproducible, a permutation, seed = `17 + shuffle*131 + (int)order*7` | Same |
| `Sort` 6 orderings (MaxSide/Area/MinSide/Height/Width/Perimeter, each with GlassRef/PieceLabel ordinal tie-breaks) | Verbatim (Phase 11 carried Area only) | Same |
| `Better` — validation failure, then unplaced, then sheet count dominate; the plate pattern axis decides between equals; utilization last | Verbatim + the four axes (FewestCuts via `TotalCuts`, MinimiseStockChanges via `DistinctStockSizes`/`StockSwitches`, RemnantFirst via `LargestLeftover`, BalancedSpread via `UtilizationSpread`) and `PlatePattern` (6 values) exposed on the API | Same |
| `OptimizationSearchGovernor` — tier bands (Full ≤ 2,000 / Narrowed ≤ 15,000 / Minimum above), `BudgetFor` (8/15/25 min by effort; 20 Narrowed; 40 Minimum), `PlanForQuick` (sweeps 3/2, 5-min budget), `RecommendEffort` (stages 3/4/5 with operator-readable reasons), the memory-pressure guard (1 KB/part estimate, half-of-available threshold, `WithReducedBreadth`) | Verbatim (`SearchPlan`/`SearchTier`/`OptimizationSearchGovernor`) | Same |
| `ScaleProfile`/`ScaleTier` (Standard/Large/Huge; IsReduced note on the result) | Verbatim | Same |
| Time budget: checked at the candidate boundary, stops ADDING candidates and keeps the best found; a `TIME BUDGET` note lands in ScaleNotes | Verbatim | Same |
| `parallelEval` — the pool evaluated with `Parallel.For`, folded in candidate order (deterministic total order) | Verbatim (the switch is off by default; thread-safety of RunPass reasoned, not measured — same caveat as the WPF source records) | Same |
| `requireGuillotine` (E4.1) — a candidate that cannot be cut is refused at the boundary; the QUICK seed is guillotine by construction; refusal count reported | Verbatim | Same |
| `ConsolidateAll` (LiSEC-style squeeze: sparsest donor first, copy-on-write snapshots, one evacuation restarts the scan, bounded sweeps) + `SnapshotBeforeWrite` + the bounded open-sheet window (`PackingContext`, FIFO, seals the oldest) | Verbatim — Phase 11's RunPass had NO consolidation; QUICK now runs it with the governor's sweeps, which is the WPF behaviour all along | **Closes a Phase 11 documented gap** — QUICK results may improve (never worsen: consolidation only removes sheets) |
| The oracles: `Verify` (margins/rotation/dimensions/overlap/kerf), `AuditBreakout`, `ValidateResult`, `VerifyComprehensive` (quantity + stock caps + material), `CheckCandidate` gate, `RecordValidation` | Verbatim | Same |
| `TryDeriveGuillotineCuts`/`DeriveCuts`/`SplitAt` (PHASE 0 fallback for stamped layouts, 20,000-step budget, never invents cuts) + `AllSheetsCuttable` + the uncuttable-sheet WARNING note | Verbatim | Same |
| `OptimizationProgress` per-candidate reporting | Ported (the type + the reports); the SaaS API does not stream progress yet — a transport decision, not a rule change | **Documented difference** — no progress channel yet |
| At ≥ 100,000 parts the WPF hands off to the industrial pattern master (RepeatedPatterns/FillFirstPlate/Partitioned) | ~~ONE governor-bounded shelf pass + `INDUSTRIAL PATH` note~~ RESOLVED in 25c: the industrial handoff runs `PackIndustrialPatterns` verbatim (§1.6d) | **Resolved at 25c** (§1.6d) |
| The WPF's additional candidate families: capacity strips, bounded best-split pattern beam (ImprovePatterns/RunBestSplit/RunBestSplitBeam), sheet-fill/wall-seed/MaxRects (Advanced), smart placement (Smart/SmartPlacement), repeated patterns + repeat master, offcut reuse, local search, staged-cut enforcement (ValidateCutPattern), instrumentation | NOT in this slice — each is ported later, documented here; because `Better()` keeps the minimum, a grid-only OPTIMIZE never regresses QUICK (the NeverRegressesQuick contract is asserted in the tests) | **Deferred at 25a** — the grid families landed in 25b (§1.6c); repeated patterns, offcut reuse, local search, staged enforcement and instrumentation remain deferred |
| `CutPolicy`/`CutComplexity` (Free/SimpleFirstX/SimpleFirstY/Simple/ThreeStage/ComplexTrims) | The model is ported (types + `CutPolicy.For`); the staged ENFORCEMENT path (StagedPlacementAllowed/RectCanHost/ValidateCutPattern) is not wired yet — `PackOptimize` accepts the policy parameter and ignores it until that slice | **Deferred, documented** (parameter accepted, not honoured — never silent: the WPF's own stage-1/2 levels also run unconstrained) |

### 1.6c Optimization — Phase 25b, the grid candidate families (parity and deliberate differences)

Ported 2026-09-27 from `Services/Optimization/GuillotinePacker.Advanced.cs`
(342 lines), `GuillotinePacker.BestSplit.cs` (356),
`GuillotinePacker.CapacityStrips.cs` (352),
`GuillotinePacker.PatternSearch.cs` (101),
`GuillotinePacker.SmartPlacement.cs` (464 — `PackSmart`/`SmartOption`/
`TrySmartPlace`; `AuditBreakout` was already ported in 25a) plus the
`Orientations`/`TryPlace`(2-out)/`StagedPlacementAllowed` helpers from
`GuillotinePacker.cs`. Domain:
`OptimizationEngine.Candidates.cs` (new partial, ~1,150 lines); the
wiring lands in `PackQuick` and `RunOptimizeGrid`.

| WPF (source of truth) | SaaS (Phase 25b) | Difference? |
|---|---|---|
| `RunSheetFill` — sheet-at-a-time BSSF fill (comparison chain rank→fit→short-side→waste→input order, candidate window, consolidation, `SHEET-FILL`) | Verbatim | Same |
| `RunWallSeed` — 3-across tall walls (maxside ≥ 2050, minside 800–1200, desc), failed trio refunds the stock counter, leftover pass + `OpenNewSheet` per piece, `WALL-SEED` | Verbatim; the WPF passes a throwaway `new OptimizationResult{Rotation}` to `OpenNewSheet` (sheet numbering only) — the SaaS `OpenNewSheet` numbers from the live sheet list, same values | Same (mechanical) |
| `RunMaxRects`/`MaxRectsPlace` — non-guillotine BSSF with kerf inflation (occW = w+kerf), frame decomposition clipped to the host rect, depth ledger +1, `PruneFree` containment pruning, `RefreshAll` after mutation, NO consolidation, `MAXRECTS-BSSF` | Verbatim | Same |
| `FillOneSheet`/`RunBestSplit` — per-sheet split selection, key (area, remainder, count, −index) as ONE lexicographic tuple; `remainderAware` (E2.2) adds `LargestFreeRect` as the tie-break; `BEST-SPLIT` | Verbatim | Same |
| `RunBestSplitBeam` — `BeamState{Sheets, Per, Remaining, Rank = sheets + SheetsLowerBound}`, dedup signature = Per join + remaining W×H:AllowRotate multiset, order Rank→Sheets→desc PlacedArea, winner = min sheets then max area; stalled beam returns ALL pieces unplaced (loses on the unplaced axis) | Verbatim | Same |
| `RunCapacityStrips` — strip pool (intrusive doubly-linked frontier, alternating head/tail, window 128), `TryStripOrientation` capacitySeed/flushWidth modes, `WidenStrip` family widening preserving across/down counts, `StripEdgeFits` (remainder 0 or ≥ kerf+MinBreakout), own cut recording (PHASE 0: separator stage 1, in-strip stage 2, sliver stage 3), gap = MinBreakout+2·kerf when breakout > 0, sheets sorted UsedSqm desc, NO consolidation | Verbatim | Same |
| `ImprovePatterns`/`PatternCandidates` — bounded best-split pattern beam (window 256, tail sheets 6, levels gate the (width, ordering) pairs, `maxExpansions = window·width`), local mode clones retained floor sheets (pieces only — cuts re-derived by the caller's PHASE 0 net) | Verbatim | Same |
| `PackSmart` — capacity-preserving placement scored by what SURVIVES (LostToTrim, then surviving capacity, capacity side, legacy fit/short-side, split preference, piece index — a total order); both split directions per placement; breakout-aware commit; sheet-at-a-time; consolidation with the same breakout; `SMART PLACEMENT` | Verbatim; the `policy` parameter is accepted (the staged gate consults it) but `SheetState.Policy`/`SplitRemainder`'s policy plumbing is the staged-cuts slice, so `StagedPlacementAllowed` is always consulted with a null policy (exactly what every Free-level WPF run evaluates) | Same for every unconstrained run; staged runs are a documented later slice |
| Shared helpers: `Orientations`, `TryPlace` 2-out overload, `FinishFromSheets` (fullest-first + renumber), `OpenBlank` (smallest-area-fits stock preference), `PruneFree`, `StagedPlacementAllowed` | Verbatim; `OpenBlank` drops the WPF's `CutPolicy` parameter (every current caller passes null) | Same (mechanical) |
| `PackQuick` candidate sequence: shelf pass → `CheckCandidate` → capacity strips (columns, rows) → `ImprovePatterns(1,1)` budget-gated → repeated patterns → plate pattern → cut-derivation net → `DeriveLeftovers` → the no-valid-QUICK warning | Shelf + strips + pattern beam + derivation net + leftovers + warning VERBATIM (with the QUICK time-budget note); repeated patterns and plate-pattern application landed in 25c (§1.6d) — the full sequence is now verbatim | **Resolved at 25c** (§1.6d) |
| `PackOptimize` grid wiring: `ImprovePatterns(2, effortLevel)` over the QUICK seed (budget-gated, Accepts-gated), stage ≥ 2 → SheetFill + WallSeed per split + BestSplit(remainderAware) + PackSmart(H; +V at stage ≥ 4) with window 300/400/∞ by tier; stage ≥ 3 (and not staged) → MaxRects (min(stage≥5?6:3, MaxRectsOrderings) orderings) + beam 4 (+8 at stage ≥ 4, +16 at stage ≥ 5 behind `wideBeam`); `smartTotal` progress accounting | Verbatim; the budget-gated `RunRepeatedPatterns` after the seed improvement landed in 25c (§1.6d) | **Resolved at 25c** (§1.6d) |
| `RecordValidation` derives `ConservationVerified`/`GeometryVerified` (`Checked && !HasValidationFailure`) | Restored verbatim — the 25a port had dropped the two derivations, so clean plans validated through `CheckCandidate` (QUICK candidates, the grid) reported "Not verified" in cutting reports | **Fix** (25b reconciliation; WPF behaviour restored) |
| `GuillotinePacker.Smart.cs` (`RunSkyline`/`ImproveByRepack`) | NOT ported. The WPF file's own header records that it is UNREACHABLE (nothing calls it; its only call sites are internal to itself), was measured not to help (200 skyline runs over five orderings beat the candidate pool 0 times; 17 sheets on the Clear group vs BestSplit's 16) and carries a delete recommendation. Porting dead code would invent work; the WPF's decision is preserved | **Deliberate** — dead code, documented not ported |
| At ≥ 100,000 parts: industrial pattern master | RESOLVED in 25c: `PackIndustrialPatterns` verbatim + the SaaS cut/leftover derivation the persisted pipeline requires (§1.6d) | **Resolved at 25c** (§1.6d) |
| `OptimizationInstrumentation` (PHASE E1 measurements) | Not ported (unchanged from 25a) | **Deferred, documented** |
| Local search (E2), offcut reuse (PHASE 2 `reuseOffcuts`), `QuickShelfProgress` adapter | Not ported — off-by-default extras in the WPF; progress plumbing for RunPass awaits a transport | **Deferred, documented** |

### 1.6d Optimization — Phase 25c, the repeated-pattern / plate-pattern / partitioned family (parity and deliberate differences)

Ported 2026-09-27 from `Services/Optimization/GuillotinePacker.RepeatedPatterns.cs`
(264 lines), `GuillotinePacker.RepeatPricing.cs` (152),
`GuillotinePacker.RepeatMaster.cs` (313), `GuillotinePacker.RepeatInteger.cs`
(134), `GuillotinePacker.RepeatTail.cs` (71),
`GuillotinePacker.FillFirstPlate.cs` (686),
`GuillotinePacker.Partitioned.cs` (841) and
`OptimizationPartitionModels.cs` (72 — SmartSearchConfiguration/Telemetry).
Domain: `OptimizationEngine.Patterns.cs`, `OptimizationEngine.PlatePattern.cs`,
`OptimizationEngine.Partitioned.cs` (new partials); the wiring lands in
`PackQuick`, `RunOptimizeGrid`, the `PackOptimize` industrial branch and the
service route.

| WPF (source of truth) | SaaS (Phase 25c) | Difference? |
|---|---|---|
| `PackIndustrialPatterns` (≥ 100k): CheckCandidate(RunRepeatedPatterns(1)) → RunCapacityStrips both transposes → ImprovePatterns(1, clamp) ASSIGNED → deeper RunRepeatedPatterns(effort) → the "Industrial quantity route" note | Verbatim in `OptimizationEngine.Patterns.cs`; the `PackOptimize` industrial branch is the WPF branch verbatim (NoteFillSkipped + the staged-unsupported note) | Same |
| Industrial branch return: the WPF returns the pattern-master plan without deriving cut sequences or leftovers (its UI derives lazily through `TotalCuts` when the FewestCuts axis is compared) | The SaaS branch runs the PHASE 0 cut-derivation net + `DeriveLeftovers` on the industrial plan before returning — the SaaS pipeline persists cut lists with the session and replays them for reporting/CNC export, so every sheet ships cuttable; a failed derivation warns exactly as the QUICK/OPTIMIZE tails do | **Documented difference** (SaaS addition, same invariants, never invents cuts) |
| `RunRepeatedPatterns` — group key (W,H,rotate,Thickness,Norm(Color),Norm(MaterialKey)); `priceWindows` gate (pieces > groups && (groups ≤ 128 \|\| pieces ≥ 2·groups)); racks filtered (MaxSheets ≥ 0, thickness/color/material match, area order, take 32); shortage penalty count+1; add-closure cap 8192 with demand-subset filter + `RepeatPatternSeparationsValid` (same-axis gap ≥ kerf && gap−kerf ≥ MinBreakout) + "Stock\|Type:Count" dedup; pricing over gap ∈ {MinBreakout+2·kerf, kerf} × transpose × 2 value vectors; LP rounds ≤ 48+effort·8; expansion preserves identities (type.Next++, Rotated = p.W ≠ type.W \|\| p.H ≠ type.H); leftover strip tail per material on WithMaxSheets copies remapped to original stock indices | Verbatim; `WithMaxSheets`/`EffectivelyUnlimitedSheets` added to StockSize for it | Same |
| `PriceRepeatPatterns` — exact-integer-mm strip DP (axis cap 16,384, 256 panes/pattern), Edge predicate (remainder 0 or ≥ kerf+MinBreakout), across DP over strips, reserved-edge and homogeneous-strip emissions | Verbatim | Same |
| `RepeatMaster` — managed revised simplex with explicit B⁻¹, artificial shortage columns (cost = penalty), finite-rack rows, optional upper-bound rows, hashed tie-seed pivot priority, 6,000-pivot cap → Limited; `PatternFits`/`ConsumePattern` | Verbatim (no native solver/package — same as the WPF's shop-PC constraint) | Same |
| `IntegerRepeatPlan` — fractional floor → greedy Complete (hash-noise trials ≤ 96+effort·32) → RepairIntegerPatterns (DFS, demand ≤ 256, dual bound normalized over ALL allowed patterns) → BranchRepeatMaster at effort ≥ 3 → reopen passes (≤ 16 patterns / ≤ 160 parts) → alternative masters (8+effort·4, stop when ceil(Objective) ≥ score) | Verbatim | Same |
| `FinishRepeatRemainder` — remainder ≤ 2048; CapacityStrips × 2 + PackSmart(128) + BestSplitBeam(4,8) on the remainder; appends the winner's sheets as NEW patterns floor 1 only if the incumbent is not complete-and-better | Verbatim | Same |
| `PackFillFirstPlate` — lookahead 36, prefix-window cap 400 (WindowsForJob → {first, widest} above it), trial cap 96 (unlimited ≤ 24 pieces), stage windows {8}/{8,12}/…/{8,12,16,24,32}, FillSplits H/V, stage orders Area→+MaxSide/MinSide/Height/Width (extra orders only on the widest window when < pieces), RunFillFirstPass with OpenSheetForAny + PlaceNextFill (score = GreedyComplete deep-lookahead when ≤ 36 pieces && (≤ 18 \|\| sheet 0), else PlacedArea; snapshot/rollback via SheetSnap incl. Cuts), empty-sheet rollback, ClonePlan-preserved ConsolidateAll squeeze accepted only if FillBetter | Verbatim | Same |
| `ApplyPlatePattern` — only FillFirstPlate; ≥ IndustrialQuantityThreshold → NoteFillSkipped; policy.IsDepthConstrained → staged-skip note; else PackFillFirstPlate + CheckCandidate + PreferFillFirstPlate (decision note on every path); > 36 pieces → bounded-fill note | Verbatim; the WPF's `current == null` guard is dropped (the SaaS `current` parameter is non-nullable — every caller passes the stage winner) | Same (mechanical) |
| `FillBetter` — legal (VerifyComprehensive clean) > fewer unplaced > fewer sheets > leading-sheet area > total glass | Verbatim | Same |
| `PackOptimizePartitioned` — ≥ 100k → NoteFillSkipped(PackIndustrialPatterns); stage ≤ 1 → PackQuick; ≤ LargeJobThreshold (2000) OR any finite stock → PackOptimize; else stratified batches (target max(200,1500); anchors ≥ 18% usable area or maxSide ≥ 0.65·(maxStockW−50); fillers ≤ 5%; snake round-robin) → Parallel.For ≤ min(4, cpu) → global baselines (PackQuick + RunRepeatedPatterns; ≥ 2 MaxSide/LongerLeftover; ≥ 3 shuffle 1013/VerticalFirst; ≥ 4 shuffle 2027/HorizFirst + PackSmart(300); ≥ 5 shuffle 3041/Perimeter + RunBestSplit(300)) → combine → CrossWindowConsolidate (ReconstructFreeRects + ConsolidateAll sweeps 3) → OptimizeTailSheets (weakest 10% ≤ 30, RunPass 3 sweeps, accept iff sheets ≤) → RecoverUnplacedPieces (10 steps) → fullest-first + renumber → Better(bestGlobal, CheckCandidate(combined)) → VerifyComprehensive safety net (other candidate → PackOptimize(1)) → RecordValidation → ApplyPlatePattern → done | Verbatim (`OptimizationEngine.Partitioned.cs` + `SmartSearchConfiguration`/`SmartSearchTelemetry`); RunPass calls use the SaaS signature (no progress parameter; cancellation token as the trailing optional argument) | Same (mechanical) |
| `OptimizeBatch` — CapacityStrips × 2 → Area pass openSheetWindow int.MaxValue → ≥ 2 MaxSide → ≥ 3 shuffle 1013 → ≥ 4 shuffle 2027 + BestSplit/SheetFill/WallSeed (≤ 2500, window = config.CandidateWindowSize) → ≥ 5 PackSmart (≤ 1500) → ImprovePatterns(1, effort) assigned | Verbatim | Same |
| `ReconstructFreeRects` — frame decomposition, kerf-inflated piece box, clipped to the host rect, disjoint, Depth = r.Depth+1 | Verbatim | Same |
| UI routing (OptimizationViewModel.Run.cs): QUICK → PackQuick(…, platePattern, fillEffort: groupEffort, …); OPTIMIZE → (finiteStock \|\| stagedRun) ? PackOptimize : PackOptimizePartitioned | Service routing (the SaaS has no VM): effort 1 → PackQuick; effort ≥ 2 → finiteStock ? PackOptimize : PackOptimizePartitioned, where finiteStock = any stock MaxSheets > 0 && < EffectivelyUnlimitedSheets (the WPF's own test rack convention); the STAGED arm of the WPF route does not exist on the API (no policy parameter — every API run is unstaged; documented) | Same (staged axis N/A on the API, documented) |
| `RunPass(..., ct, sweeps, progress, window)` | `RunPass(..., sweeps, window, ct = default)` — the token is a trailing optional; no progress parameter (no transport yet); a ThrowIfCancellationRequested was added at the piece loop (the WPF checks through its progress plumbing) | Same (mechanical; progress still unstreamed as at 25a) |
| `GuillotinePacker.PieceCandidateIndex.cs` | NOT ported — zero callers outside its own file in the WPF (same situation as `GuillotinePacker.Smart.cs`, documented at 25b); porting dead code would invent work | **Deliberate** — dead code, documented not ported |
| `CompositionKey.cs` (cutting-group merge key: trim/lowercase/collapse spaces/drop trailing " glass") | Already equal at the SaaS grouping boundary: `OptimizationService` groups by `SpecLabel`, and `CutPiece`'s composition merge key (`PaneDisplayName` semantics, Phase 11) implements the same normalization for DGU outer/inner collapse | Same (verified, no port needed) |
| `Remnants.cs` (`RemnantRegion`, replay-based leftover derivation) | Already ported (Phase 11 `DeriveLeftovers` — cut-sequence replay, called by PackQuick/PackOptimize tails and the industrial branch since 25c) | Same (verified, no port needed) |
| `LocalSearch.cs` (E2 placement local search), `OffcutReuse.cs` (PHASE 2 part 2) | Not ported — off-by-default extras in the WPF (switches `localSearch`/`reuseOffcuts` default false), unchanged from 25a/25b | **Deferred, documented** |

### 1.7 Job Order / Production
| WPF | SaaS |
|---|---|
| `SavedJobOrder`, statuses `InProduction·Completed·Delivered·Approved` | `JobOrder` entity, same statuses |
| Stage work centers (Cutting, DoublingLamination, EdgeDeletion, HolesCutouts, Mitring, Pairing, Polish, Process/Control) | Production module stage screens + `/production/stages/{stage}` endpoints |
| Piece identity, QC, breakage, cancellation, release | `ProductionPiece` + services; explicit transitions |
| Material reservation/allocation; physical consumption contract | `MaterialReservation` service consuming stock via transactions |
| `ProductionDistributionEngine`, consistency scanner, snapshot integrity | Same services, server-side, scheduled consistency scans |

#### 1.7a Job Order core — Phase 9 implementation (parity and deliberate differences)

Ported 2026-09-27 from `Models/JobOrder/*` (SavedJobOrder,
JobOrderSnapshot, JobOrderStatusValues, JobPriority, ProductionDetails),
`Services/JobOrder/Mapper/JobOrderMapper.cs`,
`Services/JobOrder/Repository/JobOrderRepository.cs`,
`Services/Workflow/StatusTransitions.cs` (Kind.JobOrder) and the
convert guards in `SavedJobsListViewModel.Part1.cs`. Migration 0012
upgraded the Phase 3 first-pass table to the WPF-faithful shape;
`JobOrderService`/`JobOrderStore`/`/api/v1/job-orders`.

| WPF (source of truth) | SaaS (Phase 9) | Difference? |
|---|---|---|
| `BuildFromProformaInvoice`: JOB-no derived from the PI number (PI-2026-0042 → JOB-2026-0042, foreign prefix kept, empty → year form) | `Domain.Proforma.JobOrderNumbers` verbatim | Same |
| Number allocation: SuggestNext only when the derived no is taken; insert with 5-attempt unique-violation retry, fresh context per attempt; snapshot JobOrderId re-synced after a reallocation | Same loop (`TryInsertAsync` + `SuggestNextNoAsync`); the snapshot summary is rebuilt from the fresh number each attempt (nothing to re-sync — it is written after allocation) | Same |
| A CONFIRMED quote (PI status Approved) → job starts RELEASED to the factory floor; anything else → Draft; Priority default Medium | Same | Same |
| Grand total copied at conversion and NEVER updated afterwards (the embedded invoice snapshot is immutable) | `grand_total_vat_inclusive` copied at conversion; the update path never touches it | Same |
| Embedded snapshot = the FULL PI POCO tree (immutable historical record) | `snapshot_json` = the conversion-time SUMMARY (numbers, parties, dates) + the normalized link `proforma_id`; the source PI stays addressable through its own tables | **Documented difference**: the SaaS does not duplicate the whole PI tree; the recorded figures still never change after conversion. (A PI is void-only in the SaaS — no delete — so the link cannot dangle) |
| Voided PI hard guard: "…is void and cannot be converted to a Job Order." (every entry path) | Same message, typed 409 | Same |
| Existing jobs for the PI: WARN, not block (a revised order / split delivery is legitimate) — Yes/No dialog | 409 listing the existing job numbers; the confirmed re-post carries `allowExisting: true` | Same rule, explicit consent instead of a dialog |
| Status machine (StatusTransitions, Kind.JobOrder): Draft→{Draft,Released,InProduction,Completed,Delivered,Rejected}, Released→{Released,InProduction,Completed,Delivered,Rejected}, InProduction→{InProduction,Completed,Delivered,Rejected}, Completed→{Completed,Delivered,Rejected}, Delivered→{Delivered,Rejected}, Rejected→{Rejected,Draft} | `Domain.Proforma.JobOrderWorkflow` — verbatim table | Same |
| Status spelling: "InProduction" (enum) vs legacy "In Production" rows — JobOrderStatusValues.Is/IsInProduction/Canonical guard every comparison | One canonical spelling in the fresh schema ('InProduction'; Canonical still renders "In Production" for display); the legacy split cannot occur | **Documented** — the WPF defect class is designed out |
| `ChangeStatusAsync` validates through the centralized table before saving | `POST /{id}/transition` — same validation, typed 409 with the reason | Same |
| Optimistic RowVersion: counter + ConcurrencyConflictException on a stale expected version | `row_version` + 409 `concurrency_conflict`; the counter increments on every update | Same |
| Soft-close void (ERP hardening): IsVoid + VoidedAt/VoidedBy/VoidReason ("(no reason given)" default) + Status → void target (Rejected); idempotent; row survives | `POST /{id}/void` — same | Same |
| ProductionDetails editable fields (priority Low/Medium/High, delivery date, site contact/phone/address, assigned to, cutting instructions, drawing reference, internal notes) | `PUT /{id}` (null keeps the saved value) | Same |
| Master DN created alongside (EnsureForJobOrderAsync) | Deferred to the Phase 10 delivery module | **Deferred, documented** |
| Production stages / pieces / QC / material reservation (the factory-floor engine) | Schema exists (0003/0004: production_pieces, production_events, material_reservations); the module is the next Phase 9 slice | Deferred, documented |
| Permissions: JobOrder.Edit for create/update (OperationGuard) | Same (`JobOrder.View/.Edit/.Delete` vocabulary ported in Phase 4); reads open to authenticated users like every module | Same |

### 1.6a Optimization — Phase 11 core slice (parity and deliberate scope)

Ported 2026-09-27 from `Services/Optimization/OptimizationModels.cs` and
`Services/Optimization/GuillotinePacker.cs` (the QUICK base pass).
`OptimizationService`/`OptimizationStore`/`/api/v1/optimization`; the
Phase 3 schema (optimization_sessions/sheets/placements/cuts/leftovers)
already matched the WPF model — no migration.

| WPF (source of truth) | SaaS (Phase 11 core) | Difference? |
|---|---|---|
| QUICK base pass: sort by AREA (the 2026-09-16 calibration — MaxSide cost a whole sheet on the owner's benchmark), shelf pass over open sheets, lexicographic best fit (fitRank, shortSide, waste, orientation rank, first sheet), perfect-fit shortcut | `GuillotinePacker.PackQuick` — ported verbatim | Same |
| ADAPTIVE rotation (Free90): original orientation first, rotate only when needed; Locked policy | Same | Same |
| SplitRemainder — ONE source of truth for the free-rect ledger AND the cut list; kerf consumed from the remainder side; HorizontalFirst the calibrated default; LongerLeftover by longer leftover | Same, verbatim (incl. cut stages and the trim flag) | Same |
| OpenNewSheet: the SMALLEST stock that fits opens first (large racks saved for the glass that needs them); MaxSheets caps the pool (0 = the explicit ∞ sentinel) | Same | Same |
| MarginProfile house table (owner-confirmed 2026-09-21): ≤6mm 15/15/15/15 kerf 3; 7–8mm 30 all kerf 3; ≥9mm 50/50/0/50 kerf 4, breakout 50 switchable to 40; unknown thickness → thin fallback; unlisted rounds UP | Same, verbatim | Same |
| DeriveLeftovers: regions replayed through the cut sequence, kerf from the remainder side, piece-holding regions excluded (kerf tolerance), reusable = min side ≥ MinBreakout | Same, verbatim | Same |
| Industrial safety net: bounds, dimension match, no overlap, no double placement, conservation (placed + unplaced = input) | Same — validated BEFORE persist; failures recorded on the session | Same |
| Composition grouping: PaneDisplayName is the merge key (two specs sharing the same outer pane collapse into ONE cutting group) | Same — one session per composition; pieces derived from the job's CUTTING plan (the same pane routing as the floor, WPF ProductionOptimizationBridge) or supplied explicitly | Same |
| QUICK portfolio's OTHER candidates: capacity strips, bounded best-split pattern beam, repeated patterns, plate patterns (Yield/FillFirstPlate/FewestCuts/MinimiseStockChanges/RemnantFirst/BalancedSpread), industrial patterns above the quantity threshold, the consolidation sweep, offcut reuse, local search | NOT ported — the SaaS plan runs the base shelf pass only (a valid, cuttable, deterministic plan, flagged in every session's scale notes) | **Deferred, documented** — the risk table's "port must be mechanical, file-by-file" applies |
| OPTIMIZE multi-start (effort 1–5), staged cut policies (CutComplexity), remnant reuse | NOT ported | **Deferred, documented** |
| Session persistence (OptimizationSessionRepository) + 2D view reads | `/run` persists session + sheets + placements + cuts + leftovers in one transaction; `GET sessions/{id}` + `/sheets` | Same |

### 1.7b Production core — Phase 9b (parity and deliberate differences)

Ported 2026-09-27 from `Services/Production/ProductionDistributionEngine.cs`
(the pure, deterministic routing engine), `DguDimensionSemantics.cs`,
`Models/Production/ProductionModels.cs` (ProductionStage, WorkItemStatus),
`SavedProductionPhase14Entities.cs` (ProductionEntryType, QcResult),
`ProductionDbContext.cs` (the UNIQUE UniqueKey contract),
`ProductionQcService.cs`, `ProductionCancellationService.cs`,
`ProductionBreakageService.cs` (core) and `ProductionReleaseService.cs`.
Migration 0014 upgraded the Phase 3 first-pass tables (greenfield — no
rows had ever been written) to the WPF-faithful shape;
`ProductionService`/`ProductionStore`/`/api/v1/production`.

| WPF (source of truth) | SaaS (Phase 9b) | Difference? |
|---|---|---|
| Routing rules (owner-confirmed 2026-09-11): SGU → W1/H1 only; DGU → outer W1/H1 + inner W2/H2 (equal-size fallback); LAM → same split in lamination mode; DGU+LAM → mono outer + laminated pair; only the four families exist (no triple glazing) | `ProductionDistributionEngine` ported verbatim over the Phase 7b `SpecGrammar` parser | Same |
| Tempering item only when the pane treatment is FT / Heat Strengthened / Heat Soaked (note "{TREATMENT} CYCLE") | Same | Same |
| Build type precedence: the explicit Step 5 glass type first (the operator may know better than the name grammar), then the spec-name grammar; a CONTRADICTING selection is surfaced as a warning | Same, verbatim (incl. the mismatch warning text) | Same |
| Pane occurrences: DGU outer = first pane, inner = LAST pane; DguLam = outer + laminated pair; unparsable names degrade to treatment-less slots with the "assume ANNEALED" warning | Same | Same |
| Incomplete W2/H2 (0×500) → the pane is SKIPPED with a warning, never invented (DguDimensionSemantics) | Same, verbatim | Same |
| Qty ≤ 0 → produced as 1 with the "fix the Step 5 qty" warning (legacy snapshot compat only) | Same | Same |
| Charge fan-out: active FAN_HOLES/HOLES/CUTOUT/COUNTERSUNK_HOLES → Holes & Cutouts per pane (QTY metric, LM=0); POLISH/POLISH_MATCHED_LAM/EDGE_GRINDING → Polish (LM metric, sub-type label); MITRING → Mitring; ARGON/SILICON/U-INSERT/OVERLAP/SHUTTER → assembly notes; SANDBLASTING → manual-tracking warning; empty spec targets = ALL specs (Chat 20 contract) | Same, driven from the Step 6 `other_charges` rows (template key, metric qty, polish labels, checked specs CSV) | Same |
| UniqueKey = `jobRef|spec|serial|position|stageCode(|CHG:key)` — a re-release produces the SAME keys so statuses survive | Same, persisted as `production_pieces.unique_key` with a partial UNIQUE index; release inserts only missing items | Same |
| Release = plan → work items (SavedProductionRelease + WorkItems) | `POST /job-orders/{id}/release`; the plan itself is a pure read (`GET …/plan`) | Same contract; the release header row is not separately materialized (the job_orders row is the release identity) — documented |
| Floor entries (SavedProductionEntry): Normal/Breakage/Rework/Foc/QcPassed/QcFailed/Cancelled + QtyDone | `production_events` (same vocab + Stage) with the WPF entry columns; `POST /{id}/stage` auto-statuses (any progress → InProgress, QtyDone ≥ Qty → Completed) | Same; auto-status derivation is the SaaS expression of the WPF stage-screen save |
| QC (RecordQcAsync): cancelled work cannot be QC'd; deterministic sequence; Failed → QcFailed WITHOUT reverting QtyDone; Passed → Completed only when QtyDone ≥ Qty; Rework → Rework | Same rules; the QC history lives as typed events (sequence in the payload) instead of a dedicated table | Same behaviour, storage shape documented |
| Cancellation (CancelWorkItemAsync): idempotent, reason defaults "Cancelled", completed work cancellable but warned, entry appended | Same | Same |
| Breakage (RecordBreakageAsync core): qty + reason entry, qty > 0 | Same; the replacement-piece and FOC re-cut workflows are later slices | Core ported; **deferred, documented** |
| Permissions: the WPF production services carry NO OperationGuard — the UI gates the floor through the Production role | Writes gated `[HasPermission(JobOrder.Edit)]` — the Production role's floor permission | **Documented difference**: the SaaS enforces at the API boundary (the WPF's desktop UI was the only gate) |
| Process executions/pairings/assemblies, workstations, control holds/assignments/overrides, the queue/dashboard/progress readers | **Delivered in Phase 26a** — see §1.7c | Was deferred; now ported |
| Actual costs, the reservation→allocation ledger, replacements/FOC, the inventory consistency scanner, the snapshot-integrity check, the two production report services, the optimization bridge | **Delivered in Phase 26b** — see §1.7d | Was deferred; now ported |
| Material reservations (MaterialReservationService): idempotent Reserve on the deterministic ReservationId (JobRef|Spec|Serial|GlassSheetId|OptSession|SheetNo); a cancelled/released row is REACTIVATED after a fresh availability check; available = sheet Balance (Stock − Used) − other ACTIVE holds (Reserved/Allocated); "Not enough stock. Available: {n}, Requested: {m}"; consumed material is never released and never cancelled; the GlassSheet row is never written by a hold | `ReservationService` (Phase 9c) — same rules, same message; the key embeds the SaaS sheet GUID; `material_reservations` upgraded by 0015 with a partial UNIQUE index on the key; `/api/v1/reservations` (+ sheets/{id}/availability read model) | Same; the allocation ledger (SavedMaterialAllocation, which session consumed which sheet) is deferred to the optimization slice — documented |

### 1.7c Production process + control — Phase 26a (parity and deliberate differences)

Ported 2026-09-27 from the WPF Phase 16/17 cores (all read in full before
porting): `Services/Production/Process/ProductionProcessRoutingService.cs`,
`ProductionProcessExecutionService.cs`, `ProductionProcessProjection.cs`,
`PairingService.cs`, `DoublingLaminationService.cs`,
`ProcessExecutionFactory.cs` (+ `Models/Production/Process/ProcessDetails.cs`),
`Services/Production/Control/ProductionReadinessEngine.cs`,
`ProductionControlService.cs`, `ProductionQueueService.cs`,
`ProductionDashboardService.cs`, `ProductionProgressService.cs`,
`ProductionTimingService.cs`, `WorkstationService.cs` and
`Services/Production/ProductionPieceIdentity.cs` +
`Models/Production/Control/ProductionControlModels.cs`. SaaS:
`Domain/Proforma/ProductionProcess.cs` (the pure halves), the Phase 26a
services in `Application/Production`, `ProcessControlStore`, migration
**0021** (7 tables + the WPF WorkItem completion/rework columns added to
`production_pieces`), and the `api/v1/production` extensions
(executions/pairings/assemblies/control/queue/dashboard/progress/
workstations — 25 new endpoints; writes `JobOrder.Edit` as in 9b).

| WPF (source of truth) | SaaS (Phase 26a) | Difference? |
|---|---|---|
| Route per work item (BuildRouteForWorkItem): Cutting always first; optional stages only when the distribution engine created them (never UI guesses, never every job through every stage); canonical order 1..8; Pairing/Doubling only for the Unit position | `ProcessRouting.BuildRouteForWorkItem` verbatim over the same stage vocabulary | Same |
| Full job route (BuildFullJobRoute) for delivery gating | Same (Cutting inserted when missing) | Same |
| CanBegin gating: the route predecessor must be Completed; an unknown predecessor is NOT complete (fail closed) | Same | Same |
| ExecutionId `JobRef|Spec|Serial|Stage|Seq`; projected start id adds `|Position` (`|`→`/`) so Outer/Inner cannot collide; PairingId `…|Pair:seq`; AssemblyId `…|DGU:seq`/`LAM:seq` — deterministic, no GUID, no DateTime | Same builders in `ProcessRouting`; UNIQUE per tenant (0021 indexes) — idempotency rides on them | Same |
| Pane instance identity `JOB|SPEC|SERIAL|POSITION|WxH|P0001` (± `|RPL:n` for replacements), minted only by cutting expansion — never DB order, never GUID; Classify separates pane/execution/pairing/assembly/session ids | `ProductionPieceIdentity` verbatim (Classify included) | Same |
| Start (execution): gating via route + stage statuses derived from QtyDone; idempotent (work-item link → projected id → legacy id); Completed/InProgress return as-is; Paused/Cancelled re-activate; the released work item flips Pending → InProgress | `ProcessExecutionService.StartAsync` same lookup chain and states | Same; the two writes (execution + work-item status) save in separate contexts — the deterministic id keeps a retry safe (documented) |
| Pause: completed cannot pause; else status Paused + reason | Same | Same |
| Complete: QtyDone INCREMENTAL, clamped to QtyRequired (Required 10 / 8 passed / 1 failed / 1 remaining — no destructive overwrite); notes/detail only overwritten when non-empty; the work item's QtyDone is NEVER written here (SetDoneQtyAsync authoritative) | Same; the linked work item guards (cancelled/rejected/QC-failed) included | Same |
| Breakage during a process: exec → Rejected(1)/Rework(2) + reason + FOC; Phase 14 entry (qty 1); deterministic child execution `…|{ReworkSeq+1}` Pending QtyRequired 1 with OutputPieceId `{PieceId}-R{nn}` — Original → Breakage → Replacement lineage | Same, incl. the Breakage event row on the piece | **Partial**: the replacement WORK ITEM (the 26b replacement/FOC slice) is not created yet, so the child execution links the original piece until then — documented |
| Release projection (ProjectJobAsync): one Pending execution per live work item; re-link existing rows; refresh QtyRequired only on untouched Pending; remove orphaned untouched rows; re-release never resets floor status | `ProcessControlStore.ProjectPendingExecutionsAsync`, called from `ReleaseAsync` (failure only warns, exactly as the WPF release does); `ReleaseResultDto.ProjectedExecutions` | Same |
| Work item → execution sync (SyncLinkedExecutionQty): the stage row is authoritative; Qty/QtyDone/status mirror onto the sequence-0 execution; frozen statuses never overwritten; StartedAt backfilled; CompletedAt/By mirrored | `SyncLinkedExecutionsAsync` — also invoked by the stage flow (`POST /{id}/stage`), which now freezes QcFailed/Rework auto-status and stamps CompletedAt/By exactly like the WPF save | Same (a 9b gap closed with the WPF rule) |
| Pairing (PairAsync): serializable tx; guards pairing Wi cancelled/rejected/QC-failed; Outer+Inner of the SAME job/spec/serial paired BY PIECE INDEX (never dimension match, never cross-job); qty = pairing Wi.Qty ?? doubling Wi.Qty ?? min(cuttings) ?? 1; both cuttings must be complete; idempotent on PairingId; human Pair-001 labels; supporting rows never write the Wi's QtyDone | `PairingAssemblyService.PairAsync` + `InsertPairingsAsync` (serializable tx, re-gated under the lock) | **Documented difference**: the WPF resolves pane ids from its CuttingPlacedPieces table first — the SaaS has no placed-piece rows, so the chain is execution PieceId → `FromWorkItem(cutting Wi, index)` (same deterministic scheme) |
| Assembly (AssembleAsync): guards doubling Wi cancelled/rejected/QC-failed; gating = Pairing WORK ITEM PCS (not the supporting pairing rows); components preserve lineage (Outer + Inner + synthetic Spacer-/Interlayer-); labels DGU-000/LAM-000; idempotent on AssemblyId | Same; `ComponentsJson` + `GetTraceability` verbatim | Same |
| Assembly component lineage (AssemblyComponent: PieceId/GlassRef/Position/WorkItemId/ExecutionId/Role) | Same shape; WorkItemId is the SaaS piece GUID where the WPF had its int id | Same |
| Process details (ProcessDetails + Factory): per-stage detail payloads (holes/cutouts counts, polish type, edge deletion, mitring edges, tempering temperature…) + SummarizeDetail one-line digests | Detail JSON passes through Start/Complete unchanged (payload contract); `ProcessDetailSummary.Summarize` (holes/cutouts/polish/edge/edges/temperature digests) | Same summaries; the six typed DTO shapes are carried by the caller (the SaaS does not duplicate the WPF's per-stage form models) — documented |
| Readiness engine (Calculate): Cancelled → On Hold (reason) → Completed (status AND QtyDone≥Qty) → status map (Rework/Rejected→QcFailed/Paused/QcPending/QcFailed) → InProgress → Pending checks: Cutting needs an ACTIVE reservation (hasAllocation never blocks), predecessor per pane same Spec\|Serial\|Position Completed+QtyDone≥Qty, latest QC per (spec,serial,position,stage) Failed/Pending blocks (Rework does not); Unit Pairing = outer AND inner groups complete; Unit Doubling = Pairing Wi PCS | `ProductionReadinessEngine` in the Domain, verbatim incl. the combined "Outer & Inner" reason and the pairing-PCS rule | Same; the QC input derives from the typed QC events (payload sequence) instead of a dedicated QcEntries table — documented |
| Canonical predecessors: Holes/Polish/EdgeDel/Mitring ← Cutting; Tempering ← Cutting+all edge stages; Pairing ← Tempering+Cutting; Doubling ← Pairing | Same (GetPredecessorStages) | Same |
| Queue (GetQueueAsync): aggregates work items, never duplicates jobs; filters job/stage/readiness/priority/workstation/search (JobRef/Customer/Spec/GlassRef/PI)/customer/includeCompleted+Cancelled; deterministic order Ready→InProgress→other→Blocked, priority desc, due date, stage, JobRef, spec, serial, position, unique key (never DB order, never random) | `ProductionQueueService.GetQueueAsync` same filters + `Order()`; stage/post-filters run in memory after the batched load | **Documented difference**: the WPF pre-caps the fetch at take×2 (which can starve predecessor context); the SaaS loads the full filtered set and caps after ordering. Due date falls back to `job_orders.due_date` when no override exists (the WPF left null with a TODO) |
| Queue material columns: Required=Qty, Reserved/Available=Σ active reservations (sheets), Allocated/Consumed=allocation row counts, Shortage=1 when Cutting lacks a reservation | Same; Allocated/Consumed read 0 | **Deferred**: the allocation ledger is the 26b slice (flag preserved in the models) |
| Queue DGU columns: Outer/InnerPieceId + PairId from the unit's pairing; AssemblyId surfaces the assembly LABEL | Same (deterministic pick ordered by PairId — the WPF took an unordered First) | Same values; deterministic ordering documented |
| Timing (EnrichTimingAsync): Pending wait=now−CreatedAt; Completed processing=CompletedAt−CreatedAt, wait=UpdatedAt−CreatedAt; else wait=UpdatedAt−CreatedAt, processing=now−UpdatedAt — from existing timestamps, never fabricated | `ProductionTiming.EnrichTiming`, enriched inside the queue build | **Fixed with the WPF's intent**: the WPF dashboard never called the timing service (its AvgWait read 0); the SaaS enriches in the queue so pressure shows real wait times — documented |
| Control Start: guards (cancelled/completed/paused/rejected/QC-failed; InProgress idempotent; active hold refused) + readiness must be READY; serializable tx with re-fetch; entry Reason "Start by {operator}"; linked executions synced | `ProductionControlService.StartAsync` + `TransitionPieceAsync` (serializable, re-fetch, event, sync) | Same; the control event lands as a `Stage` ProductionEvent (the WPF writes its SavedProductionEntry) — same shape/reasons; HeldBy/operator carry the acting user id alongside the free-text name |
| Control Pause/Resume: only InProgress↔Paused; entries appended; sync | Same | Same |
| Hold/Release: idempotent while active; HoldId `…|HOLD:seq` (seq = prior hold count); Release appends `" | Release: {notes}"` and keeps the history row; entries appended | Same + `production_control_holds` (is_released + released columns) | Same |
| Assign/Unassign: idempotent on active workstation+operator; previous actives deactivated (never deleted); AssignmentId `…|ASSIGN:seq` | Same (`production_control_assignments`, is_active + unassigned columns) | Same |
| Priority/DueDate overrides: ControlId `…|CTRL` upsert; priority 0/1/2 influences ORDERING ONLY (never bypasses dependencies); idempotent when unchanged; notes overwrite only when non-empty | Same (`production_control_overrides`) | Same |
| Control Complete: delegates to the piece flow (SetDoneQtyAsync) | Delegates to the stage flow (`RecordStageAsync`) | Same contract |
| Dashboard KPIs: JobsInProduction (distinct jobs over live items), readiness counts from the queue, CompletedToday, OverdueJobs (distinct), DueSoon (DueSoon+AtRisk), MaterialShortages, ProcessLoad per stage, Bottleneck = largest READY stage | Same | **Partial**: ReadyForDelivery uses the WPF's own fallback formula (NextLabel "Ready" + Completed) — the dispatch gateway (GetUnitReadinessAsync) is a later SaaS slice; CompletedToday counts the stored `completed_at` (UTC day) |
| Queue pressure: per stage Ready/Blocked/Overdue/QcBacklog/ReworkBacklog + AvgWaitHours (1 dp); High>15 / Medium>8 / Low>0 / Idle | Same thresholds | Same |
| Workstation load: per active station assigned/ready/inprogress/blocked | Same | Same |
| Progress: job = distinct Spec\|Serial units, the final-stage item dictates the bucket (Completed+=Qty, others+=Remaining, cancelled counts units, Ready stays remaining); Required=Σ first-row Qty; Overall=Σ jobs; Process groups by stage (Completed=Σ Qty of completed) | Same (`GetJobProgressAsync`/`GetOverallProgressAsync`/`GetProcessProgressAsync`) | Same |
| Workstation master: ten defaults (CUT-01/02, CNC-01, POLISH-01, EDGEDEL-01, MITRING-01, TEMPER-01, PAIR-01, LAM-01, DGU-01) seeded only when empty; Create idempotent on upper-invariant code; ERP-level only — no PLC/CNC integration | Same, per tenant (`workstations` + seed endpoint) | Same; per-tenant master data (the WPF shared one production.db) |
| NotifyWorkItemsChanged live refresh after every floor write | No SaaS equivalent — clients re-poll the read endpoints | **Documented difference** (transport, not semantics) |
| Audit via AppLogService/App activity log | `audit_logs` rows per control action (`production.*` actions) | Same intent, SaaS audit sink |

### 1.7d Production cutting, costs + integrity — Phase 26b (parity and deliberate differences)

Ported 2026-09-27 from `Services/Production/Cutting/`
(`CuttingWorkflowService.cs`, `CuttingRequirementService.cs`,
`CuttingCompletionService.cs`, `MaterialGroupingService.cs`,
`StockSelectionService.cs`), `Services/Production/`
(`MaterialAllocationService.cs`, `ActualCostService.cs`,
`ProductionInventoryConsistencyScanner.cs`,
`ProductionSnapshotIntegrity.cs`, `ProductionOptimizationBridge.cs`,
the replacement half of `ProductionBreakageService.cs`) and
`Services/Reporting/ProductionProcessReportService.cs` +
`ProductionControlReportService.cs`, over the
`SavedProductionPhase14Entities.cs`/`SavedProductionPhase15Entities.cs`
shapes. SaaS: migration **0022** (`cutting_sessions`, `cutting_sheets`,
`cutting_placed_pieces`, `material_allocations`,
`production_actual_costs`, `production_replacements`),
`Domain/Proforma/ProductionCutting.cs` (pure ids/statuses/groups),
`CuttingService`/`AllocationCostService`/`ReplacementService`/
`ProductionIntegrityService`/`ProductionReportsService`,
`CuttingCostStore`, the `api/v1/cutting` controller + the
`api/v1/production` report/integrity endpoints (writes `JobOrder.Edit`
as in 9b/26a), and the migration-tool port of the 13 previously-deferred
tables.

| WPF (source of truth) | SaaS (Phase 26b) | Difference? |
|---|---|---|
| Cutting workflow: requirements → material groups → stock selection → optimization → session persist → allocations; Verify MUST pass before persist; session save failure is a warning, not a rollback | `CuttingService` — reserve → pack → `Verify` gate → persist → allocate, one transaction; a verification failure (or later group failure) releases the group's reservations (compensation by row Id); the optimization-session save failure warns only | Same |
| Optimization bridge: effort ≤ 1 → Quick; finite stock (a rack with limited sheets) → Optimize; else Partitioned | Same routing over the in-repo engine (`PackQuick`/`PackOptimize`/`PackOptimizePartitioned`) | Same; the SaaS runs the ported engine in-process (the WPF bridge called the identical algorithm family) |
| Plan limits refuse with "The current plan's optimization-job limit…" | `PlanLimitGuard` verbatim message | Same |
| Session ids: `CUT-{job}-{group}-{yyyyMMddHHmmss}`, sheet `{session}|S{n}`, placed `{sheet}|{pieceId}` | Same builders in `Domain/Proforma/ProductionCutting.cs` | Same |
| CompletePiece: QtyDone = min(Qty, QtyDone+1), linked executions synced, Stage event, consume cascade into the allocation ledger; breakage → replacement qty 1; remaining = max(0, required − completed − breakage − foc) | Same (`CuttingService.CompletePieceAsync` → `AllocationCostService.ConsumeAsync`) | Same |
| Benchmark regression check: BENCHMARK/LOW-E jobs whose plan uses <15 or >19 sheets warn "expected ~17 sheets … regression check (allowed)" — a soft check, never a refusal | Same warning verbatim, in the session warnings | Same |
| Allocation ledger (MaterialAllocationService): allocate is idempotent + live-reservation-only; consume is idempotent; the reservation flips to Consumed only when ALL its allocations are consumed; AllocationId = `{reservationKey}\|S{sheetNo}\|{label}` | Same (`material_allocations`, 0022; unique per tenant) | Same |
| Actual costs (ActualCostService): CostId = `{job}\|{spec}\|{serial}\|{type}` (+`\|{allocationId}` when derived, +`\|FOC`); selling value 0 for FOC; summary = material+production+wastage+processing | Same (`production_actual_costs`, 0022); `DeriveFromAllocation` prices at `GlassSheet.PurchasePricePerSqm` (0 when unknown) | Same |
| Replacement/FOC work items (ProductionBreakageService replacement half): sequence = max+1; ReplacementId = `{job}\|{spec}\|{serial}\|{pos}\|{stage int}\|RPL:{n}`; the replacement piece label `{orig} RPL:{n}`, Qty = break qty, TotalLm scaled; one transaction; the event lands on the ORIGINAL piece; Rework keeps status Rework unless already Completed; FOC via EntryType Foc | Same (`ReplacementService` + `production_replacements`, 0022) | Same |
| Execution-level breakage (26a) linked the ORIGINAL piece | The 26b rewire: execution-breakage creates the REAL replacement piece (child execution ProductionPieceId = the replacement); a replacement failure aborts the execution-breakage with its error code; re-release after breakage does NOT double-project | **Documented difference vs. the 26a stopgap** (now WPF-faithful) |
| Inventory consistency scanner: six factual findings + the PlanningConsumedNoMatchingHistory observation; RetryVsNewConsumption is NOT detectable from the data; six limitations listed | `ProductionIntegrityService` — same findings, same honest "cannot determine" answers | Same |
| Snapshot integrity: PI snapshot rows with W1>0 AND H1>0 are live; verdict Missing/Corrupt/ValidEmpty/Valid | Same | Same |
| Process report: rows ordered SpecName, ProcessType with the PI→…→QC trace; pairing report includes pair-less assemblies; control report routes via the work-item route builder; rework + QC queues from the unified queue | `ProductionReportsService` over the 0021/0022 tables; control reports ride the 26a endpoints (documented in Phase 22) | Same; the control-report family was already delivered as 26a queue/dashboard endpoints — only the process/pairing/rework/QC report readers are new |
| Permissions: WPF UI-gated (Production role) | Writes `[HasPermission(JobOrder.Edit)]`, reads tenant-authenticated | Same documented difference as 9b/26a (API-boundary enforcement) |
| Migration tool: the 13 tables were counted as §1.7b deferrals | Ported: executions/pairings/assemblies/workstations/holds/assignments/overrides (0021) + allocations/costs/replacements/cutting sessions/sheets/pieces (0022); int enums → the TEXT vocabularies; int work-item ids → migrated `production_pieces` uuids; WPF global workstations → per-tenant; holds/assignments/overrides on an unimported work item skip with an error (piece FK NOT NULL); an allocation whose reservation was not imported is KEPT with a null link (the WPF key preserved in `reservation_key`); the replacement OriginalEntryId link is not portable (fresh event uuids) — info finding, left null; `ProductionReleases` + `ProductionQcEntries` remain the documented deferrals | Same data, SaaS storage shape; link portability documented per table |

### 1.8 Delivery — Phase 10 core (parity and deliberate differences)

Ported 2026-09-27 from `Services/DeliveryNote/` —
`MasterDeliveryNoteService.cs`, `DeliveryNoteCreateService.cs` (the
one-path create-or-open), `Mapper/DeliveryNoteMapper.cs` (FromJobOrder /
FromMasterBalance), `Repository/DeliveryNoteRepository.Part1.cs`
(`BuildMasterDnIdAsync`/`BuildDeliveryNoteIdAsync`/`UpdateMasterDnAsync`),
`DeliveryNoteWorkflow.cs` and `Models/DeliveryNote/SavedDeliveryNote/*`.
Migration 0013 upgraded the Phase 3 first-pass tables to the WPF-faithful
shape; `DeliveryService`/`DeliveryNoteStore`/`/api/v1/delivery-notes`.

| WPF (source of truth) | SaaS (Phase 10) | Difference? |
|---|---|---|
| Master DN = the balance tracker, one per JO, `MDN-{core}` (core = JobOrderId minus `JOB-`), Status Draft, DeliveryType **Dummy**, note "Master DN — auto-generated reference tracker", created on first create-or-open (and after PI→JO convert in the UI flow) | `DeliveryService.EnsureMasterAsync` — same id derivation, same fields; created lazily on the first create-or-open / master read | Same (creation timing is lazy vs. eager — the master is content-identical either way) |
| Real DN id `DN-{core}-{seq:D2}`, seq = max+1 of the job's non-master notes | `DeliveryNoteNumbers` + `GetNextSequenceAsync`, UNIQUE(tenant, delivery_no) retry | Same |
| CreateOrOpenDispatching — ONE path: (1) never open a Dispatched-or-later DN (God View only), (2) existing open Dispatching/Draft non-master **non-Dummy** DN → open it, (3) else create from the master's remaining balance, (4) FullyDelivered result when the balance is 0 | Same three outcomes (`OpenExisting`/`Created`/`FullyDelivered`); same non-Dummy open filter (a master/scratch note is never reopened) | Same |
| FromMasterBalance lines: only `ComputedBalanceQty > 0` lines, `PreviouslyDeliveredQty` copied from the master, **DeliveredQty = 0** (operator fills before dispatch), Balance = remaining, DeliveryType **Original**, Status Dispatching | Same, verbatim | Same |
| Line identity = `SpecGroupNumber|SerialNumber` (SpecKey); the serial is the JO/master serial, NEVER reassigned on later notes | `DeliveryNoteWorkflow.LineKey` verbatim; serials copied from the master lines | Same |
| FromJobOrder line building: continuous global serial counter across spec groups; dedup by serial within a group taking MAX qty; skip empty rows (no GlassRef AND W1=0 AND H1=0) and qty ≤ 0 rows | `BuildLinesFromGrid` — same guards (the SaaS loader already drops phantom rows before save; the guard stays as a safety net) | Same |
| Master line semantics (`UpdateMasterDnAsync`): confirmed-so-far in `PreviouslyDeliveredQty`, `DeliveredQty = 0`, balance = Ordered − confirmed; totals recomputed | `RecomputeMasterAsync` — same, set-based; runs on create-or-open, master read, confirmed transitions and void | Same |
| Remaining qty = CONFIRMED deliveries only (Dispatched/Delivered/Signed/Received, non-master, non-Dummy, non-void) — in-progress drafts never consume the balance | Same filter (SQL CHECK-constrained status vocab) | Same |
| Master OrderedQty self-heal from the JO snapshot (master drift fix) | Not needed: the SaaS master is built from the source PI's grid and a PI is immutable/void-only, so OrderedQty cannot drift | **Documented difference** — the WPF heal patches a drift class that cannot occur in the SaaS |
| Status machine (StatusTransitions, Kind.DeliveryNote): forward chain Draft→Dispatching→Dispatched→Delivered→Signed→Received, self-transitions allowed, Cancelled from every non-terminal state + Cancelled→Draft recovery, Received terminal | `DeliveryNoteStateMachine` — verbatim table, typed 409 with the WPF reason text | Same |
| Void (soft-close): the row survives for the audit trail; reason recorded ("(no reason given)" default); idempotent | `POST /{id}/void` — same, plus Status → Cancelled and a master recompute | Same |
| Dispatch edit (driver/vehicle/receiver/date/time/notes/FOC + line delivered qty) on OPEN notes only; confirmed notes are history | `PUT /{id}` — null keeps the saved value; per-line guard 0 ≤ delivered ≤ Ordered − Previously (ComputedBalanceQty, BUG-05); 409 on confirmed/master | Same |
| `DeliveryNoteStatusValues.IsOpenForDispatch` / `IsConfirmedDelivery` | `DeliveryNoteWorkflow` — verbatim, case-insensitive | Same |
| Attachments, history/analytics, export/PDF snapshot (`FullSnapshotJson`), Tax Invoice link, weight calculator | `snapshot_json` column reserved; module deferred | **Deferred, documented** (PDF needs a render surface; weights need Step 4 glass composition) |
| Permissions: `DeliveryNote.Edit` for every write (OperationGuard) | Same (`DeliveryNote.View/.Edit` ported in Phase 4); reads open to authenticated users | Same |


### 1.9 Tax Invoice / Credit Note / Statement / VAT — Phases 12 + 17 (parity and deliberate differences)

Ported 2026-09-27 from `Services/TaxInvoice/` (`InvoiceTotalsCalculator.cs`,
`PaymentTermsResolver.cs`, `TaxInvoiceFamilyService.cs`,
`TaxInvoiceValidationService.cs`), `Services/Workflow/StatusTransitions.cs`
(the TaxInvoice + CreditNote tables), `Services/CreditNote/` +
`Models/CreditNote/SavedCreditNote/`, `Models/TaxInvoice/SavedTaxInvoice/`
and `Services/Reporting/` (`VatCalculator.cs`, `MoneyRounding.cs`,
`ReportingDataService.Vat.cs`/`.Statement.cs`/`.TaxInvoice.cs`). Migration
0016 upgraded the Phase 3 first-pass tables to the WPF-faithful shape
(status vocabularies, customer/project/terms/discount/void columns,
statement line ordinal); `FiscalService`/`FiscalStore`/
`/api/v1/fiscal`.

| WPF (source of truth) | SaaS (Phases 12 + 17) | Difference? |
|---|---|---|
| Status vocab Draft/Issued/Paid/PartiallyPaid/Overdue/Cancelled/Void (`SavedTaxInvoice`) | Same (SQL CHECK-constrained, migration 0016 replaced the Phase 3 `Issued/Paid/Overdue` guess + dropped `status_text`) | Same |
| THE FISCAL LOCK: a Tax Invoice is a FISCAL document — once Issued it is never edited, only voided or credited | `TaxInvoiceStateMachine` (WPF table verbatim) guards every transition; Issued→Draft is rejected 409; IsEditable = Draft only | Same |
| StatusTransitions Kind.TaxInvoice: Draft→{Draft,Issued,Cancelled,Void}; Issued→{Issued,Paid,PartiallyPaid,Overdue,Cancelled,Void}; PartiallyPaid→{…,Paid,Overdue,…}; Paid terminal; Overdue→{Overdue,Paid,PartiallyPaid,Cancelled,Void}; Cancelled/Void terminal | Verbatim port + SQL CHECK vocab | Same |
| StatusTransitions Kind.CreditNote: Draft→{Draft,Issued,Cancelled}; Issued→{Issued,Applied,Cancelled}; Applied terminal ("the value reduced the ledger"); Cancelled terminal | `CreditNoteStateMachine` verbatim + SQL CHECK (the Phase 3 lowercase draft/issued/void guess dropped) | Same |
| Line items (`TaxInvoiceItems` EF child table: group/serial identity, glass ref, dims, qty, sqm, total sqm, rate, value, notes) | `tax_invoice_items` (migration 0020) + `CreateTaxInvoiceRequest.Items` — when present the WPF `BuildItemsAndSubtotal` math DERIVES the subtotal (SqmCalculator 3 dp + 0.5 m² floor; value = MoneyRounding 2 dp away-from-zero of rate × qty × sqm) and the request's Subtotal is ignored; snapshot SpecGroups populated from them; the importer loads the WPF rows with the spec-group-item link resolved | **Same** (totals-only invoices remain supported — items optional; SpecGroups stays empty for them, documented below) |
| `InvoiceTotalsCalculator`: Discount = Round(subtotal×%/100), AfterDiscount = subtotal−discount, VAT = Round(After×5%), Grand = After+VAT (MoneyRounding 2dp midpoint away-from-zero — "a half-fil does not banker's-round down") | `InvoiceTotalsCalculator` verbatim (single source of truth; the API recomputes from subtotal+% and never trusts client totals) | Same |
| `PaymentTermsResolver`: Immediate 0, Net 7/15/30/45/60/90, unknown/Custom → 30; DueDate = IssueDate + days | Verbatim; default terms "Net 30" | Same |
| Family numbering: `INV-{year}-{seq:D3}` UNIQUE; ALL invoices for one PI share the family — the second invoice for a PI is a revision `-A`, then `-B` (`TaxInvoiceFamilyService`, `SequenceLetter`) | Same: base number allocated per year with UNIQUE(tenant, invoice_no) + retry; later invoices for the same PI take the next revision letter (race-safe: letter derived from every used number) | Same |
| A credit note NEVER exists on its own — `TaxInvoiceReference` required (`SavedCreditNote`) | `CreateCreditNoteAsync` rejects a missing invoice (404), requires a reason, and caps the credit at the invoice's after-discount amount | Same |
| Credit numbering `CN-{year}-{seq:D3}` UNIQUE | Same, retry-on-unique-violation | Same |
| Payments: PaidAmount/BalanceAmount tracked on the invoice; PartiallyPaid → Paid at zero balance | `POST /{id}/payments` — machine-guarded (Draft/Cancelled/Void/Paid reject); balance floors at 0 | Same |
| Applied credit note reduces the corrected invoice's balance | `TransitionCreditNoteAsync` …→Applied settles GrandTotal against the invoice balance (floored at 0) | Same |
| Void: reason recorded ("(no reason given)" default), the number is NEVER reused, row survives for the audit trail | Same: Void sets is_void/voided_at/void_reason; terminal; excluded from VAT + statements | Same |
| VAT submission (`ReportingDataService.Vat` + `VatSubmissionView`): standard-rated supplies & output VAT over vatable invoices − credit-note adjustments | `GET /fiscal/vat-report?from&to` — vatable = Issued/PartiallyPaid/Paid/Overdue, non-void; credit notes Issued/Applied; NetVatPayable = Σ invoice VAT − Σ CN VAT | Same (VAT rate is the WPF-fixed 5%, not per-tenant config — matches `InvoiceTotalsCalculator`) |
| Statement (`ReportingDataService.Statement`): the customer ledger — invoices as debits, payments + applied credit notes as credits, running balance; filters + persisted output | `POST /fiscal/statements` — same ledger shape, Draft/Cancelled/Void invoices excluded; persisted to statements + statement_lines with a line ordinal (migration 0016; the Phase 3 table had none) and read back via `GET /fiscal/statements/{id}` | **Documented difference** — the WPF also persists a rendered PDF (StorageKey); the SaaS persists the ledger rows only (PDF/print deferred) |
| WPF statement filters: company/customer/project/salesman contain (SafeContains), date range on IssueDate, status multi-select (empty = all), ≥1 axis required (HasAnyFilter gates Generate) | `POST /fiscal/statements` — all axes (Phase 17): company/customer case-insensitive contains, project **bidirectional** contains (Chat 20), salesman via the snapshot's `PreparedBy.Name` (post-query parse, like the WPF aggregator), date range on issue_date, status multi-select within the ledger universe; ≥1 axis required; all axes persisted (filter_* columns + filter_statuses jsonb); title = WPF `GetStatementTitle` priority (customer > project > company > salesman > range > "Full Account Statement") | **Same** (Phase 12 shipped customer as an EXACT match — corrected to the WPF's contains in Phase 17) |
| Tax Invoice link from Delivery Note (`DeliveryNoteReference` comma-separated DN list, empty = PI-only) | `delivery_note_reference` stored verbatim (string) + `job_order_id`/`proforma_id` family links | Same |
| Bank fields (BankName/AccountNo/Iban/Swift) + company TRN on the invoice print | Same fields as columns (migration 0019: company_name, company_trn, purchase_order_number, bank_name, bank_account_number, iban, swift_code) + carried in the snapshot's Bank/Company sections; populated at create from the optional `Print` request block; the migration tool fills them from the WPF entity | Same (the RENDERING of these fields remains deferred with PDF/print) |
| `FullSnapshotJson` (immutable print snapshot) | `snapshot_json` populated at create (Phase 17) with the WPF `TaxInvoiceSnapshot` shape VERBATIM — SchemaVersion 1, PascalCase JSON, Header/Company/Customer/Project/SpecGroups/Financial/Bank/PreparedBy/Attachments; `AmountToWords` ported verbatim for GrandTotalInWords; `PreviouslyInvoicedTotal` = the family's prior invoices, `RemainingBalance` = max(0, PI total − previously − this) per the WPF; status transitions rewrite Header.Status (WPF `SetHeaderStatus`) | **Documented difference** — SpecGroups is populated from the line items when the request carries them (Phase 19: groups by (group number, name, glass type) with the WPF row shape + group rollups) and stays empty for totals-only invoices; logo/signature/attachment sections keep their WPF defaults until the print/attachments slices, and Header.ProformaId/JobOrderId carry the linked document's NUMBER (the WPF's string identifiers) |
| PDF/print exports (`ReportingExportService` PDF engine, `TaxInvoicePdfService`), Excel exports (the ClosedXML engine), production-process + factory-control reports (queue/dashboard/progress/QC/pairings — they read the deferred production-extras tables) | NOT ported | **Deferred, documented** — the CSV export path + the reporting cache landed in Phase 23; the reporting DATA layer (Phase 20–22) is complete |
| Order reconciliation (`OrderReconciliationService` + `ReconciliationModels`) | `ReconciliationService` + `GET /fiscal/proformas/{id}/reconciliation` (Phase 19): ordered = PI spec_group_items.qty, delivered = Σ delivery_note_items.delivered_qty over the PI's job orders' dispatch DNs (excludes Draft, Dummy, master — the WPF "confirmed" filter), invoiced = Σ tax_invoice_items.qty over the family invoices (excludes Draft, Cancelled, Void); BalanceQty = max(0, O−D); InvoiceVsDeliveryDiff = I−D; DetermineStatus verbatim priority (NotYetStarted → **OverInvoiced** ★ → UnderInvoiced → OverDelivered → PartialDelivery → FullyMatched); all rollup flags + counts + OverallStatus priority | **Same** — enabled by migration 0020 (tax_invoice_items, the WPF TaxInvoiceItems table; the spec-group-item link resolves where the PI line exists). Row display: SpecGroupName from the normalized SpecName, GlassRef snapshot-only ("") — the composite key (group, serial) is what matches |
| Executive summary (`ReportingDataService.Executive` + `ExecutiveSummary`, slice 5) | `ReportingService.GetExecutiveSummaryAsync` + `GET /api/v1/reporting/executive-summary` (Phase 20): the COHORT order-to-cash funnel — Quoted → In production (JO exists) → Delivered (EVERY spec row complete) → Invoiced → Paid, distinct-ProformaId counts, SharePct of the first stage, StepConversionPct from the previous, IsBottleneck < 60%; composed from the same per-module summaries under ONE shared filter (pi/dn/ti) so the executive page cannot disagree with the module reports; headline money (Quoted/Invoiced/Outstanding), operational health (UnconvertedQuotes, UninvoicedConfirmed, OverdueInvoices, DeliveryCompletionPct, CriticalPendingQty), mix + costing-coverage gap | **Documented difference** — statuses mapped to the SaaS vocabularies (PI buckets: Draft / Sent→Released / Approved or lifecycle Confirmed→Confirmed / else Rejected; confirmed-quote pipeline = status Approved or lifecycle Confirmed), salesman/glass-type filters resolve through first-class FKs instead of the WPF's snapshot-JSON key resolution, qty/sqm read from the normalized spec_group_items instead of deserializing each snapshot (same source of truth, no 1000-row cap needed for correctness but the bound is kept), and funnel cohort ids are Guids not PI-number strings |
| Profitability (`ReportingDataService.Profitability`) | `GetProfitabilityRowsAsync`/`GetProfitabilitySummaryAsync` + `GET /api/v1/reporting/profitability{,/summary}` (Phase 20): one row per PI; the "costed" gate (Step 9 with rows/cost/net — never VAT-divide an uncosted header), revenue = PnlNetExVat, cost = glass + production, profit = the PERSISTED PnlProfit (charges/discount/rounding live in it — never recomputed), margin round 1, qty/sqm from spec items; DB-side sort keys (Amount/Customer/Company/Project/Status/Date/default CreatedAt-desc) + paging; summary AvgMargin + InvoicesWithoutCost + MissingCostSources | **Documented difference** — the P&L comes from the persisted KPI columns (Phase 7b Step 9) instead of deserializing FullSnapshotJson; cost-basis STALENESS is not classified (the WPF compares against the Sheet Store GlassPriceStamper; SaaS Step 9 persists no price stamp → IsStaleCost false, note empty — surfaced, not invented) |
| Analytics (`ReportingAnalyticsBuilder` + `ReportingDataService.Analytics`, slice 1) | `ReportingAnalytics` (pure, verbatim) + `GET /api/v1/reporting/analytics/{job-orders,delivery}` (Phase 20): Pareto mix (top 10 + "Other (n more)" tail, share by the ranking measure, sqm→qty→count fallbacks), delivery ageing buckets 0–7/8–14/15–30/over-30-critical with AgeInDays from the master-DN creation date, weekly throughput (ISO weeks starting Monday), SqmOf = physical area (no 0.5 m² floor) with W2/H2 bounding fallback | **Documented difference** — the colour Pareto reads the normalized spec_groups.outer_glass_color the row carries instead of parsing the specification string at report time (the WPF's SpecPaneResolver); rows without a linked group report "Unspecified" (the WPF's parser-miss fallback) |
| Row traceability (`RowTraceabilityService`) | `RowTraceabilityService` + `GET /reporting/trace/{rowId}` and `GET /reporting/reconciliation/{proformaNo}` (Phase 21): the unbroken audit chain for one glass row — PI → Job Order(s) (one step each, or the "Awaiting Conversion" placeholder) → Delivery Note lines (driver/vehicle/type notes) → Tax Invoice lines (billed amount notes) → Profitability (only when the Step 9 gate passes: glass or production cost > 0; revenue = NetExVat or grand/1.05, margin 1dp) — plus the live reconciliation (ordered / job-order / delivered / remaining / invoiced + DeliveryState/InvoiceState). Row id = the WPF composite "PI-2026-0842-G1-S1"; a bare proforma number traces the first row (the WPF fallback) | **Documented difference** — delivery/invoice lines match the quoted row on the (SpecGroupNumber, SerialNumber) composite key instead of the WPF's GlassRef + W1 + H1 text match (glass ref is snapshot-only in the SaaS); the quoted row set comes from spec_group_items and the P&L step from the persisted Step 9 KPIs. **Verbatim quirk kept**: the WPF's delivery query excludes master DNs but NOT voided ones — preserved and documented (the reconciliation endpoint DOES exclude void) |
| CNC export (`CncExport` — `CncDxfWriter` R12, `CncCutListWriter` RFC 4180, `CncExportFormats` honest contract) | `CncExportService` + `GET /reporting/cnc-formats` and `GET /reporting/cnc-export/{sessionId}/{formatId}` (Phase 21): DXF R12 (AC1009) with layers SHEET/PIECES/CUTS/REMNANTS/WASTE/TEXT — full stock outline + usable rectangle, every placed pane, cuts as single lines in execution order, racking-annotated offcuts, 500 mm plate stacking; cut list CSV — one row per cut (sheet, seq, axis, position, crossing rectangle, stage, TRIM/CUT, pieces on sheet). The plan is REBUILT from the persisted session (sheets + placements + cuts + leftovers) so an export is reproducible long after the run. The honest contract verbatim: Generic DXF + Cut list CSV ready; Lisec .DAT / Bottero .OPT / Intermac .ISO declared NOT implemented with exactly what finishes them (a sample file from the machine) — never guessed | **Documented difference** — source stamp carries the session's document type + composition key; kerf is stamped from the house MarginProfile for the session's thickness (run params persist no custom kerf) |
| Cutting reports (`CuttingReportService`: cutting sheet + material consumption) | `CuttingReportService` + `GET /reporting/cutting-sheet/{jobOrderNo}` and `GET /reporting/material-consumption/{jobOrderNo}` (Phase 22): one row per cut sheet — job, PI, spec, material/color/thickness, sheet dims + label, the piece list with rotations ("label WxH 0°/90°"), utilization/wastage (100−util), rotation summary (Mixed 0°/90°), verification, operator, created; one row per session for consumption — sheets used, pieces, areas, utilization/wastage, reservations, and the full traceability chain "PI → JO → MaterialGroup → Session → Sheets". Rendered from the PERSISTED optimization sessions (the WPF reads its CuttingSessions/Sheets/PlacedPieces tables — the same plan) | **Documented difference** — placements persist no SpecGroupNumber (identity = PieceRef + dims) so that column renders ""; verification is the SESSION's conservation+geometry verdicts (the WPF has per-sheet flags); the operator resolves from the user id to DisplayName; the allocation ledger is a deferred production extra so Allocated/Consumed render 0 (never invented) |
| Production process reports (`ProductionProcessReportService`: process status, work queue, QC/rework, pairing traceability, route) + factory control reports (`ProductionControlReportService`: queue, blocked, rework, QC, dashboard KPIs, progress, workstation load, queue pressure) | NOT ported | **Deferred, documented** — they read the WPF's process-execution/pairing/assembly and queue/dashboard tables, which are the deferred production-extras slice; the SaaS production core (pieces + events, Phase 9b) carries the data those reports will read once the extras land |
| CSV export (`ReportingExportService` generic CSV path + `ExportFileNamer`) | `ReportingCsv` + `ReportingCsvLayouts` + `GET /reporting/export/{profitability,delivery,job-orders}.csv` (Phase 23): the WPF GenericCsvLayout row-writing semantics — optional metadata block (REPORT + title, key/value rows, blank line), header row, data rows, sectioned reports grouped by the first column — as RFC 4180 (CRLF, comma/quote/newline quoted, inner quotes doubled); the WPF header/map sets for JO/DN (profitability gets its own set); ExportFileNamer verbatim (whitespace→single hyphen, non-alphanumerics REMOVED, hyphens collapsed, 48-char segments, `id_company_reference_dd-MM-yyyy.csv`) | **Documented difference** — the SaaS row models carry no Prepared By / Company / Salesman on the delivery row and no Prepared By on the JO row (those columns render ""); Excel + PDF engines stay deferred (ClosedXML dependency unverified without a build env; PDF user-deferred) |
| Reporting cache (`ReportingCacheService`, Phase 9: Performance) | `ReportingCacheService` (singleton) + the executive summary and JO analytics memoized through it (Phase 23): 30s sliding expiry, 60s prune timer, GetOrAddAsync that never caches failures, GetStats, and `POST /reporting/cache/invalidate` + `GET /reporting/cache/stats` for ops | **Documented difference** — keys are TENANT-PREFIXED (a security boundary the single-user WPF did not need); invalidation is TTL + the operator endpoint rather than the WPF's in-process DocumentLifecycle broadcast (the SaaS services do not raise document-change events — the 30s window bounds staleness the same way) |
| Permissions: TaxInvoice View/Edit/Export/Delete, Reports View/Export, StatementGenerate (Phase 4 catalog) | Same — writes gate `TaxInvoice.Edit`, reports gate `Reports.View`, statements gate `StatementGenerate` | Same |

### 1.9b Authentication (Phase 4) — parity and deliberate differences

The WPF behaviors are the default; every difference below is a multi-tenant
security translation, not a silent change.

| WPF (source of truth) | SaaS (Phase 4) | Difference? |
|---|---|---|
| Login = username + password, single company | Login = **company code (slug) + username/email + password** on one form | **Different** — multi-tenancy requires identifying the tenant; the company code is the natural WPF-compatible concept (the WPF app already has a company profile) |
| Login error names the problem ("wrong password", "no such user") | One GENERIC error: "Company code, username or password is incorrect." | **Different (deliberate, §49 documented)** — field-specific errors let attackers enumerate tenants and usernames in a multi-tenant SaaS; also dummy-hash timing equalization so response time does not reveal which part was wrong |
| SHA-256(salt+password) hashes | Argon2id `$argon2id$v=19$m=65536,t=3,p=2$…` | **Different (ADR-006)** — old hashes not portable: imported users get `must_reset_password=true`, login still succeeds and the flag travels in the response |
| `PasswordPolicy` (min 6, no max, blocklist, repeats, runs, identity) | Ported VERBATIM (`ProGlass.Saas.Application/Security/PasswordPolicy.cs`) + WPF test cases ported | Same |
| Lockout: 5 failed → 5 min, persisted, counter resets when window opens, success clears all (`AuditService.Part1.cs`) | Same constants, same semantics, same persistence (`failed_attempt_count`, `locked_until_utc`); policy unit-locked by tests; API answers **423 + Retry-After** | Same behavior, HTTP status is the SaaS-native expression |
| No session concept (desktop login lives for the app run) | JWT access (15 min) + rotating opaque refresh token (30 d sliding / **60 d absolute**); refresh reuse revokes the whole token family; session list + per-session revoke | **New** — SaaS has no equivalent WPF behavior to preserve |
| `PermissionService` + `OperationGuard.Demand` (UI dialogs, WPF-side) | `[HasPermission("Module.Capability")]` server-side policies over the SAME role→permission grant map (reflection-discovered catalog, no duplicate list); denial = 403 + audit | Same grants, new enforcement point (server, not UI) |
| Password change: verify current + policy on new | Same (verify current, policy, re-authenticate every other device by revoking all refresh tokens) | Same |
| Registration: none (users created in Users UI) | Public `POST /api/v1/auth/register` provisions tenant + first Administrator on the elevated platform connection (trial subscription deferred to Phase 13) | **New** — tenant onboarding has no WPF equivalent |
| Email verification / password reset by email / MFA | Deferred (§58) — columns exist (`mfa_secret_encrypted`, `email_confirmed_at`), endpoints not built | Deferred, documented |


| WPF | SaaS |
|---|---|
| Dashboard panels per module + Executive summary | `/dashboard` widgets from server aggregations (paginated/SQL-side) |
| All reports (§9 of audit) | `/reports/*` with date/customer/salesman/status/project/stock/supplier/VAT filters; PDF/Excel/CSV export server-side |
| `AppUser` + roles + permissions | Identity users + same role/permission catalog (`Module.Capability`), claims-based |
| Settings (data root, margins, currency, language, theme) | Tenant settings (margins, VAT rate, currency, fabrication catalogs); user prefs (language/theme) |

### 1.10 Master data (Phase 5) — parity and deliberate differences

Companies/clients/projects/salesmen, ported from
`Services/MasterData/Repository/MasterDataRepository.cs` (details in
`SAAS_MIGRATION_PROGRESS.md` facts 38–46).

| WPF (source of truth) | SaaS (Phase 5) | Difference? |
|---|---|---|
| Natural-key upserts, case-insensitive (company name+TRN, client name+TRN, project identifier+customer, salesman name); usage_count+1 + last_used_at on re-save | Same, in `MasterDataService` + EF store (`LOWER()` on both sides, like the WPF SQLite queries) | Same |
| Audit-fix F-1/F-1b/F-1c/F-2 (id-first lookup, rename-collision refusal, editor latest-data-wins, invoice auto-save preserve-on-blank) | All preserved; rename collision → 409 | Same |
| Single default company (others cleared on save) | Same, cleared in the SAME transaction as the save | Same |
| Salesman blank department never erases a stored value | Same quirk, verbatim | Same |
| DELETE of a missing row silently no-ops | **404** — REST + §0 "never reveal cross-tenant existence" | **Different (documented)** |
| Local `DateTime.Now` timestamps | UTC (`timestamptz`) | **Different (ADR-007)** |
| Logo = local file path | `logo_storage_key` → object storage | Architectural translation |
| Projects store customer NAME string | `customer_id` FK (normalization; same-identifier-different-customer preserved through the id) | **Different (documented)** |
| Unbounded lists | Paging (default 50, max 200) + 20-hit autocomplete search (WPF `SEARCH_LIMIT`) | Same limits, new envelope |

### 1.11 Licensing → Subscriptions — Phase 13 (parity and deliberate differences)

Ported 2026-09-27 from `Services/Licensing/` — `LicensePayload.cs` (the
edition tiers), `LicenseService.cs`/`.Part1`/`.Part2` (AutoTrialDays,
IsReadOnlyMode), `LicenseVerifier.cs` (ExpiryWarningDays, GracePeriod,
ReadOnly), `ReadOnlyGate.cs` + `ReadOnlyModeException` (the write gate
and its user-safe messaging), `AutoTrialManager.cs` (the automatic
evaluation trial), `LicenseGate.cs`. Migration 0017 seeds the plan/
feature catalog from the WPF edition tiers; `SubscriptionService`/
`SubscriptionStore`/`SubscriptionAdminStore`/
`SubscriptionGateMiddleware`/`/api/v1/subscriptions`.

| WPF (source of truth) | SaaS (Phase 13) | Difference? |
|---|---|---|
| Signed license key + RSA/Ed25519 signature + machine fingerprint + offline activation | Tenant subscription row (plans/features/plan_features/subscriptions/tenant_features, Phase 3 tables; 0017 seeds the catalog) | **Documented difference** — no cryptography/fingerprints: a tenant's subscription IS the license; the WPF's per-machine binding has no multi-tenant equivalent |
| `LicenseEdition` tiers: Trial "30-day, single-activation, all features"; Basic "single user, inquiry + proforma only"; Business "up to 5 users, full ERP without production optimisation"; Factory "unlimited users, includes cutting-optimisation engine"; Enterprise "unlimited + multi-site + custom integrations" | Plans trial/basic/business/factory/enterprise + 12 module feature keys + per-plan limits (seeded verbatim from the tier doc comments; e.g. Basic = users 1 + inquiries + proformas ONLY; Factory = unlimited + optimization; Enterprise = + multi_site + custom_integrations) | Same tiers |
| `LicenseService.AutoTrialDays = 30`; AutoTrialManager issues the trial on first run | `TenantProvisioner` inserts a 30-day `trialing` subscription on the 'trial' plan in the SAME registration transaction (all features on) | Same (first run → first registration) |
| `LicenseVerifier.ExpiryWarningDays = 14` (ExpiringSoon) / `GracePeriod` = 7 days read-only past expiry / `Expired` refuses to start | `SubscriptionGate.Resolve` — identical constants and the same state split, computed from the subscription status + period/trial end (trial past end = TrialExpired with NO grace; paid past end ≤7d = GracePeriod read-only; >7d = Expired hard block) | Same |
| `LicenseVerificationResult.ReadOnly` = GracePeriod \|\| TrialExpired | `IsReadOnly` = GracePeriod \|\| TrialExpired \|\| Cancelled (cancelled keeps reads, recoverable by re-subscribing) | **Documented difference** — the SaaS adds Cancelled to the read-only set (a cancelled SaaS tenant keeps its data readable; the WPF has no cancellation concept, only expiry) |
| `ReadOnlyGate.Demand`: reads pass, writes throw `ReadOnlyModeException` with "The app is in read-only mode. … Your data is preserved. Activate a full licence in Settings → License…"; denial audit-logged BEFORE the refusal; the licence settings page stays usable (the recovery path) | `SubscriptionGateMiddleware` — reads pass; non-GET writes return 403 `subscription.read_only` with the same sentences ("Your data is preserved. Choose a plan in Settings → Subscription…"); the denial is audit-logged (`subscription.gate_denied`) before the response; `/api/v1/subscriptions/*` and `/api/v1/auth/*` stay writable | Same (HTTP 403 instead of an exception — the service-layer throw becomes the middleware's response) |
| App refuses to start when Expired / MachineMismatch / ActivationLimit | Hard block: suspended (billing) or past-grace-expiry tenants get 403 on EVERYTHING (reads included) — `subscription.blocked` | Same behaviour, SaaS states |
| `LicensePlanCatalog` (per-customer plan knobs) | `tenant_features` overrides — an override wins over the plan (is_enabled + limit_value); set via `ISubscriptionAdminStore.SetOverrideAsync` | Same |
| LicenseGenerator tool (vendor issues a license) | `SubscriptionAdminStore.GrantPlanAsync`/`SuspendAsync`/`ReactivateAsync` on the elevated platform connection | **Documented difference** — implemented as a port, NOT HTTP-exposed (no platform-admin auth surface yet; isolated for the future payment provider per the plan) |
| Edition gating (Basic cannot run production, etc.) — UI-role-gated in the WPF | `ModuleFeatureMap` route→feature gate in the middleware: reads AND writes of a module's routes 403 `subscription.feature_disabled` when the plan lacks the feature | Same tiers, server-enforced |
| "Up to 5 users" (Business) | `users` feature limit checked on plan change (a downgrade below the current seat count is refused with the seat count); null limit = unlimited | Same |
| Per-period usage limits (invoices/month, reports/month, optimization jobs/month — architecture §6; the WPF has NO such limits, single-machine licensing) | `PlanLimitGuard` (Phase 18): resolves the effective limit (tenant override wins; NULL = unlimited) and the current billing period start; `FiscalService.CreateAsync` counts tax invoices since period start (`tax_invoices` key), `GenerateStatementAsync` counts statements (`reports`), `OptimizationService.RunAsync` counts sessions (`optimization`) — at the limit the write is refused with HTTP **402** `fiscal.subscription_limit` / `optimization.subscription_limit` and an operator-readable message; values live ONLY in the catalog (plan_features/tenant_features, hard-coded nowhere) and nothing is seeded except the WPF-derived users limits — an operator decision point by design | **Documented difference** — a SaaS platform concern with no WPF analogue; storage accounting: `GET /api/v1/attachments/usage` (Phase 24; a plan limit can gate it — operator decision point, no value seeded) |
| `ReadOnlyGate.Enabled` test kill-switch | Not needed — the middleware is composed in the pipeline, integration tests build the graph without it | **Documented difference** |
| Payments/billing | NOT ported (no payment provider; plan change is instant and free — a platform decision point) | **Deferred, documented** |



### 1.12a Migration tooling — Phases 14 + 16 (the WPF data root → SaaS tenants)

Built 2026-09-27 (`saas/migration/proglass_migrate`, plan document
`SAAS_MIGRATION_PLAN.md`). The plan sketches a .NET 8 console worker;
this environment has no .NET SDK, so the tool is implemented in Python
with the same CLI shape (`import --source --db-uri --tenant
--dry-run|--apply`, `validate`, `list-modules`), the same module order
and the same transform rules — fully executable and tested end-to-end
against live PostgreSQL (`saas/migration/tests/test_migration_tool.py`:
fixture SQLite → disposable PG, dry-run==apply counts, findings,
validate report). Porting the proven logic to the .NET worker later is
mechanical. Phase 14 delivered the engine + the first 7 modules; Phase
16 completed 15 plan §2 modules; Phase 24 added the 16th and last
(`attachments` — byte storage is the filesystem under
SAAS_ATTACHMENT_ROOT with the storage_key indirection, so the plan's
"object storage" concern is addressed without a cloud dependency).

| Plan §2 module | Status | Transform notes (WPF source → SaaS target) |
|---|---|---|
| 1 audit.db Users | **Implemented** | WPF SHA-256 hashes CANNOT migrate → valid Argon2id hash of a random unknowable secret + must_reset_password (plan §3); roles pass through (the SaaS ck_users_role is the WPF catalog verbatim, incl. legacy Operator); unknown roles are reported + skipped, never remapped; WPF `Email ""` → NULL |
| 2 masterdata.db | **Implemented** | Companies/clients/projects/salesmen → the Phase 5 tables; WPF projects reference clients by NAME → customer_id resolved by name (unresolved → null + warning finding, never a wrong link); usage counts preserved; Phase 16 additionally exposes the name AND int-id maps the document modules resolve against (inquiries carry WPF int FKs, documents carry names) |
| 3 proglass.db | **Implemented** | GlassSheets + SheetHistories → glass_sheets + the immutable stock_transactions ledger (Bought→purchase, Used→issue, balance_after reconstructed from history order); balances recomputed from history and COMPARED to stored Stock/Used (mismatch → warning finding, stored values import verbatim — the operator's ledger wins); InventoryItems with the category map (SpacerBar/Desiccant/PrimarySeal/SecondarySeal/Gas/Hardware → spacer/desiccant/sealant×2/gas/hardware); WPF 'Other'/'Film' categories have no SaaS bucket → error finding + skip; Phase 16 records the WPF int sheet ids (specifications.sheet_id, material_reservations.glass_sheet_id resolve against them) |
| 4 specifications.db | **Implemented (Phase 16)** | Library rows → saas.specifications: rate_per_sqm from PricePerSqm, the 0008 catalogue columns (category/color/thickness/sheet dims) direct, sheet_id resolved against imported sheets (unresolved → null + info), PanesJson validated (invalid → null + warning), last_proforma_no from ProformaId, usage_count/last_used_at verbatim (the operator's ledger; the SaaS API recomputes on live saves only); glass types outside ck_specifications_glass_type reported + skipped; the usage LEDGER (SpecificationUsages) is a separate module (`specusages`) that runs AFTER proformas — proforma_id is a NOT NULL FK, so usages of unimported PIs are error findings + skipped |
| 5 inquiries.db | **Implemented (Phase 16)** | InquiryRecord → saas.inquiries; status normalized by the WPF's OWN InquiryStatuses.Normalize (On Hold → Hold, Canceled → Cancelled, case-insensitive, unknown → Received — the WPF fallback) with the original spelling preserved in status_text; priorities lowercased to the CHECK vocabulary (WPF Urgent/High/Normal/Low), unknowns reported + skipped; customer/project resolved by WPF int id against the masterdata maps (unresolved → null + warning); the BOQ (InquiryLineItems) is REBUILT as boq_json in the exact InquiryBoqLine camelCase shape the SaaS ParseBoq reads — a converted inquiry stays convertible; communications + attachments have no SaaS tables yet (inquiry module beyond convert-to-PI is a later phase, §1.1) — counted + info findings |
| 6 proformainvoices.db | **Implemented (Phase 16)** | Header → saas.proforma_invoices (statuses verbatim; unknowns reported + skipped — same documented deviation as the fiscal docs; lifecycle_state = status when a member, 'Expired' → derived 'Sent' + info finding; parties resolved by NAME with warnings; salesman_id from the snapshot client SalesRepresentative when it matches an imported salesman + info; revision chain root_proforma_id resolved by number, blank/self → NULL = root per the WPF rule; VAT recomputed at the stored VatPercent and grand-total arithmetic CHECKED with mismatches reported, stored values verbatim; Summary → discount_type/discount_value/is_vat_applicable/vat_percent; ProfitLoss → the 0010 pnl_* columns incl. ComputedAtUtc as UTC); FullSnapshotJson preserved VERBATIM in snapshot_json (jsonb — invalid JSON is an error + row skip: the snapshot IS the document) + the additive normalized projection (plan §6 risk 1): SpecGroups → spec_groups (rate = the WPF SurchargeRate Round(base×(1+pct/100)); price_source manual iff HasManualStep5BaseRateOverride; Step 4 provenance columns direct; spec_id resolved against the imported library) and ItemRows → spec_group_items with the WPF row math ported (SqmCalculator 3 dp banker's + 0.5 m² floor, SpecGroupRowMath Value = MoneyRounding 2 dp away-from-zero; phantom qty≤0 rows dropped per the SaaS save contract); OtherCharges → other_charges/wastage_rows/custom_charges (metric key = SelectedMetricKey → legacy SelectedSourceKey → template default → unit-derived, every derivation reported; charge names/units from the ported ChargeTemplates registry; amounts verbatim); inquiry backlink (converted_proforma_id) written once both rows exist; GlassRef on item rows has no normalized column (0009 parity uses position) — preserved in the snapshot, counted + info |
| 7 optimization.db | **Implemented (Phase 16)** | **Documented difference from plan §2** ("re-run through both oracles"): the WPF persists only AGGREGATE results (ResultJson = sheets used, utilization, per-sheet piece counts) — placement geometry is not in optimization.db, so NEITHER oracle can re-run. Named sessions import as historical completed runs: params_json carries engine/effort/rotation/piece count/job refs + the parsed margins/stocks/pieces/result JSON, utilization/sheets/unplaced verbatim, conservation_verified = geometry_verified = FALSE (the quarantined-unless-verified rule — an imported run is never presented as oracle-passed) and validation_problems states the limitation; optimization_sheets/placements/cuts/leftovers stay empty (per-sheet detail not recoverable); reserved `__latest_run__*` rows are feed plumbing the WPF itself hides and prunes — skipped + counted with an info finding |
| 8 joborders/production.db | **Implemented (Phase 16)** | JobOrders → job_orders: proforma_id is NOT NULL (0012) so a job whose source PI did not import is an error + skip; the legacy "In Production" spelling collapses to InProduction (the WPF's own IsInProduction two-spelling equivalence, info finding); priority verbatim against the CHECK; ProductionDetails (site contact/phone/address, assigned to, cutting instructions, drawing ref, internal notes) from the snapshot's Production block; grand total/customer/project names verbatim; void block with VoidedBy username → user uuid; PI job_order_id backlink written once both rows exist. production.db → the FOUR §1.7b shapes: ProductionWorkItems → production_pieces (enum ints → the CHECK vocabularies verbatim; spec_group_item_id resolved via source PI + group + serial; status 'Cancelled' sets is_cancelled), ProductionEntries → production_events (event_type 'Stage', stage enum, done_by username → user uuid), MaterialReservations → material_reservations (reservation_key verbatim, sheet + optimization session + job resolved); ProductionReleases are NOT imported — the job_orders row IS the release identity (§1.7b); of the previously-deferred production tables, the 13 Phase 26a/26b shapes (process executions/pairings/assemblies, workstations, control holds/assignments/overrides, allocations, actual costs, replacements, cutting sessions/sheets/pieces) ARE ported since 26a/26b (§1.7c/§1.7d) — only ProductionReleases (the release header is the job_orders row) and ProductionQcEntries (QC history lives as typed production_events, §1.7b) stay deferred, each counted with an info finding, never silently dropped |
| 9 deliverynotes.db | **Implemented (Phase 16)** | DeliveryNotes → delivery_notes (status/delivery_type verbatim against the CHECKs, unknowns error + skip; job_order_id NOT NULL — a note whose job did not import is error + skip; master/dispatch flags, driver/vehicle/receiver/time, qty totals, void block, snapshot validated-else-null); DeliveryNoteItems → delivery_note_items (qty/sqm from the WPF panel math, spec-group-item link resolved via the note's PI + group + serial, weight per piece, line notes); items under a skipped note are counted + info |
| 10 taxinvoices.db / creditnotes.db | **Implemented** | Statuses pass through where the 0016 CHECK knows them (the WPF fiscal vocab IS the SaaS vocab); unknown statuses are reported + skipped — a deliberate difference from plan §3's "pass through" rule, which predates the CHECK-constrained vocabularies; totals re-verified by recomputation (VAT = round2(after×5%)) and mismatches REPORTED, stored values imported verbatim (a fiscal record is never "fixed" on import); WPF FullSnapshotJson preserved in tax_invoices.snapshot_json; credit notes carry no snapshot column (info finding); Phase 16: the ProformaId/JobOrderId string references now RESOLVE against the imported documents (unresolved → null + warning) |
| 11 statements.db | **Implemented** | WPF invoice-rows → the SaaS ledger (Invoice debit = GrandTotal, Payment credit = PaidAmount, running balance, line_no order); WPF totals (InvoiceCount/TotalInvoiced/TotalOutstanding/OverdueAmount) preserved in totals_json |
| 12 audit.db ActivityLog | **Implemented** | TimestampUtc passes through UTC; user_id resolved from the imported users (unknown → null + info finding); ChangesJson → after_json when valid JSON, else the free-text Details preserved inside after_json (the SaaS audit row has no Details column); only true UUIDs land in entity_id |
| 13 attachments | **Implemented (Phase 24)** | DeliveryNoteAttachments + InquiryAttachments → saas.attachments, with the bytes COPIED from the WPF data root into the tenant-scoped attachment storage (SAAS_ATTACHMENT_ROOT / --attachments-dir): files resolved by StoragePath then the WPF AttachmentService layouts ({Images\|PDFs}/{SanitizeName(DN)}/ and Attachments/{InquiryNumber}/); on-disk size wins over the registered size (drift = info finding); unimportable uploader → NULL + info; missing file / unimported owner → error + skip; the unique-name rule and the S-8 sanitizer are the WPF's own, in Python; dry-run writes no bytes |
| Machine licenses | NOT migrated | subscription created fresh at registration (Phase 13) — plan §3 |

`validate` (Phase 16 extension, plan §4): per-table counts for all 29
target tables + the stock-ledger tail check + PI financial integrity
(glass + other − discount → VAT at the stored vat_percent → grand,
±0.01 tolerance) + revision-chain orphans (a revision whose
root_proforma_id is null) + the delivery delivered-qty guard
(0 ≤ delivered ≤ ordered + previously).

Engine semantics: source opened `mode=ro` (never written); apply = one
transaction per module (a failed module rolls back only itself), a
rejected row rolls back only itself (savepoint) and becomes an error
finding — never half-loaded rows; dry-run = the whole run in ONE
transaction (later modules' FKs resolve exactly as an apply would),
discarded at the end; every finding (info/warning/error) lands in the
HTML + JSON report.

### 1.13 Attachments — Phase 24 (parity and deliberate differences)

Ported 2026-09-27 from `Services/DeliveryNote/Attachments/` —
`AttachmentService.cs` (311 lines: the upload path with extension +
size validation, the S-8 sanitizer, BUG-26 shared helper, BUG-27
delete semantics, BUG-28 unique-name resolution, FormatFileSize) and
`AttachmentType.cs` (`AttachmentLimits`: 10 MB, the image/PDF extension
sets, the combined dialog filter). Domain: `AttachmentRules`;
Application: `AttachmentService`/`IAttachmentStore`/`IAttachmentStorage`;
Infrastructure: `FileAttachmentStorage` (filesystem) + `AttachmentStore`
(EF); API: `AttachmentsController` (5 endpoints).

| WPF (source of truth) | SaaS (Phase 24) | Difference? |
|---|---|---|
| Upload path: extension first, then the 10 MB size limit — no byte is written for an invalid file | Same order in `AttachmentService.UploadAsync`; a non-seekable (chunked HTTP) stream is length-checked by the bytes actually written and an over-limit file is deleted immediately | Same |
| `AttachmentLimits.ImageExtensions` (.jpg/.jpeg/.png/.bmp/.gif/.webp), `PdfExtensions` (.pdf), `MaxFileSizeBytes` 10 MB | `AttachmentRules` — verbatim | Same |
| `SanitizeName` (S-8): blank → "_"; invalid filename chars + separators → '_'; all-dots segment (path traversal) → "_"; trailing dots/spaces trimmed (Windows silently drops them → two ids would collide on one folder); 120-char cap | `AttachmentRules.SanitizeName` — verbatim; the migration tool carries the same rule in Python so an imported file lands where a live upload would | Same |
| `ResolveUniquePath` (BUG-28): plain name first, then `name-1` … `name-999`, then an immediate GUID fallback — no silent overwrite, no unbounded disk I/O | `AttachmentRules.ResolveUniqueName` (returns a name, not a path) + `FileAttachmentStorage.SaveAsync` | Same |
| `DeleteFile` (BUG-27): returns bool — the DB row is authoritative, the file cleanup is best-effort (missing/locked → false, never throws) | `AttachmentService.DeleteAsync` deletes the row first, then best-effort `storage.Delete`; a failed row insert deletes the written bytes (no orphans) | Same |
| `FormatFileSize` (`0.#` KB / `0.##` MB) | `AttachmentRules.FormatFileSize` — verbatim | Same |
| Files under `AppData/DeliveryNotes/Attachments/{Images\|PDFs}/{SanitizeName(DN)}/` on the workstation | `FileAttachmentStorage` under `SAAS_ATTACHMENT_ROOT` (default `./attachments`): `{tenant}/{entity-type}/{entity-id}/{unique-name}`, with `attachments.storage_key` the indirection — an object-storage backend can drop in later without a schema change | **Documented difference** — multi-tenant keyed layout, no workstation AppData |
| DN attachments only (the WPF service is delivery-note-specific; inquiries carry their own `InquiryAttachments` table) | Entity-generic `attachments` table (0003) for six entity types — the five document families (delivery_note, proforma_invoice, tax_invoice, credit_note, job_order) + inquiry (the migration imports InquiryAttachments, so the API surface accepts them) — each gated by that entity's own permission pair (view = list/download, edit = upload/delete) | **Documented difference** — the table was entity-keyed from the start; the WPF's single-entity scope becomes a known-type allowlist |
| Uploads via the WPF file dialog; DN save writes the `DeliveryNoteAttachments` registry row | `POST /api/v1/attachments/{entityType}/{entityId}` (multipart, IFormFile) + list/download/delete/usage endpoints | Architectural translation |
| No storage accounting (single machine) | `GET /api/v1/attachments/usage` — total bytes + display for the tenant (Reports.View; the storage-GB counter for the subscription catalog) | **New** — SaaS platform concern |
| `DeliveryNoteAttachments` + `InquiryAttachments` registry rows + the files on disk | Migration module 13 `attachments`: registry rows read from both source tables, bytes resolved by StoragePath then the AttachmentService layouts, copied (never moved) into the tenant-scoped storage, on-disk size wins over the registered size (drift is an info finding), an unimportable uploader is stored as NULL with an info finding, missing files/skipped owners are error findings — counted, never silently dropped; dry-run writes NO bytes | Same semantics (row + bytes together) |

### 1.14 Production hardening — Phase 15 (parity and deliberate differences)

Built 2026-09-27. The WPF's hardening portfolio (SQLite WAL/busy_timeout/
foreign_keys via `SqliteResilience`, `SchemaHealthService`, the
`BackupService`/`RestoreDrillService` pair, the licensing gate) maps to
web/database equivalents; `SAAS_DEPLOYMENT.md` is the operator's guide.

| WPF (source of truth) | SaaS (Phase 15) | Difference? |
|---|---|---|
| `SqliteResilience` (WAL, busy_timeout 5s, foreign_keys ON) per connection | connection resiliency at the proxy/pool; the DB enforces FKs globally (PostgreSQL, all migrations FK-constrained) | Architectural translation |
| `SchemaHealthService` (startup schema checks) | `GET /health` + `hardening_audit.py` — a 14-check live audit (RLS on all 40 tenant tables + FORCE, per-tenant policies, role least-privilege, platform-table write denial for the runtime role, tenant-FK cascade coverage, orphan scan, unique fiscal numbers, 52 CHECK constraints, FK index coverage, Argon2id password shape, statement ordinal uniqueness, stock ledger chains) → `verification/HARDENING_AUDIT.md` | **Extended** — the audit found 49 unindexed FK columns; migration 0018 adds the indexes (48+1, CREATE INDEX IF NOT EXISTS only) and the audit now passes 14/14 |
| `BackupService`: backup SETS, retention 10, auto-throttle 1h, manual-warn 10min, never-throws facade | `devbox/backup_db.py` — pg_dump -Fc sets with the SAME constants (retention 10, warn window 10min, `--force`), manifests (BackupSetInfo: name/createdAt/bytes/files + md5 + per-table row counts + pg version) | Same policy; the SaaS backs up one PostgreSQL cluster (not a folder of .db files + attachment folders) |
| `RestoreDrillService` (a backup is proven only by restoring it) | `--restore-drill`: restores the newest set into a SCRATCH database, verifies the archive checksum and every per-table row count against the manifest, drops the scratch DB — verified end-to-end (45 tables, 84 rows, plus deliberate-corruption detection) | Same |
| Attachments in backup sets (Inquiries/PI/DN folders — the 2026-09-05 P0 audit fix) | Attachment bytes live under `SAAS_ATTACHMENT_ROOT` (Phase 24 `FileAttachmentStorage`, tenant-scoped keys) — the operator backs up that folder alongside `pg_dump` (the keys are relative paths, so the set restores cleanly); `devbox/backup_db.py` documents it | **Documented difference** — the folder is outside PostgreSQL, same as the WPF's was outside the .db files |
| Desktop attack surface (no headers/HTTP) | `SecurityHeadersMiddleware` (X-Frame-Options DENY, nosniff, no-referrer, CSP default-src 'none', COOP, cross-domain policies) + `GlobalExceptionHandlerMiddleware` (unhandled → the §0 `{code,message}` envelope with a correlation id; stack traces never leave the server) + `LoginRateLimitMiddleware` (fixed window 20/min per IP on login/register — the WPF per-username lockout's web complement) | **New** — web-only concerns |
| Machine fingerprint / license gate (read-only enforcement) | Phase 13 `SubscriptionGateMiddleware` | Already ported |
| Velopack updates, crash helpers | CI/CD + the reverse proxy; out of scope here | Architectural translation |
| One-click desktop installer → deployment story (Phase 27) | `saas/devops/`: docker-compose (db + one-shot migrate + api + nginx) with the role bootstrap script, the API Dockerfile (non-root, embedded migrations, healthcheck), the nginx edge (TLS-ready, 12 MB body guard, 600 s read timeouts for optimization runs, auth rate-limit zone, holding page until the Phase 28 SPA), systemd units (api + nightly backup + weekly restore drill) and the operator runbook; `SAAS_MIGRATE_AND_EXIT` release-step mode + `ASPNETCORE_FORWARDEDHEADERS_ENABLED` in Program.cs | **Architectural translation** — the desktop's "run setup.exe" becomes `docker compose up`; no WPF counterpart to port, the WPF-verbatim backup/drill constants ride the timers |

### 1.15 Front end — Phase 28 (the SPA foundation + first screens)

`saas/frontend/` — React 18 + TypeScript + Vite, react-router,
TanStack Query, one hand-rolled stylesheet (no UI kit, no XAML ports —
web-native UX per the stack decision). Verified with a REAL build:
`npm ci && npm run build` (tsc strict + vite, 89 modules). Same-origin
`/api/v1` only (the Vite dev proxy in development, the nginx edge in
production — no host is ever hardcoded); the compose nginx image now
carries the built bundle (`devops/docker/frontend.Dockerfile`; the
holding page stays as the bare-metal fallback).

| WPF (source of truth) | SaaS (Phase 28 slice) | Difference? |
|---|---|---|
| Login window (tenant-less local accounts) | LoginPage — POST /api/v1/auth/login { tenantSlug, username, password }; the 423 lockout and the { code, message } envelope render verbatim; imported WPF users see the must-reset note | Web-native: tenant slug + JWT (localStorage session, one silent refresh on 401, then sign-out) |
| The 9-step PI wizard, all XAML views | NOT ported in this slice — Phase 28 is the SPA foundation + the production-floor workflow | **Deferred, documented**: the SPA covers login, the dashboard (job-order / low-stock / stock stats from the real endpoints), job orders (list/search/filter/paging), the job-order workbench (plan preview → release → pieces) and the stock sheet grid. Later modules land as pages against their already-shipped API |
| Job order + production floor screens (Jobs/Daily Works) | JobOrderDetailPage — the plan preview (GET plan), Release (idempotent, with the projected-executions note), the pieces grid and the floor actions: +1 done (POST stage, auto-status), QC Passed/Rework (POST qc), breakage with qty + required reason (POST breakage), cancel (POST cancel) | Same server rules — the client only renders the server's own messages (gating/validation text comes from the API verbatim; the client never invents rules) |
| Stock screen (SheetStore grid) | StockSheetsPage — GET /api/v1/stock/sheets with the search/color/thickness filters + paging + low-stock balance highlighting; the sidebar badge shows the low-stock count | Same |
| NotifyFloor live refresh | TanStack Query invalidation after every action (explicit refetch, no sockets) | **Documented difference** (transport, not semantics — same as 26a/26b) |
| Role-gated UI (the Production role gates the floor) | Server-enforced (JobOrder.Edit on writes); the UI shows the actions and surfaces the 403 envelope when the role lacks the permission | Same as the 9b documented difference — the API boundary is the gate |

**Phase 29 — the proforma workbench (SPA slice 2).** Pages: the PI
list (search/status filters + paging + the create-draft form over the
company/client/project/salesman pickers and issue/valid-until dates) and
the PI detail workbench — header with id→name resolution, the totals
strip, the lifecycle action bar (transitions from the server's own
`validTransitions` with optional reason, void with reason, duplicate,
new revision) and five review tabs: Step 5 glass grid (group cards with
sheet colors/dimensions + the item rows with the server-computed
SQM/LM/prices), Step 6 charges/wastage/customs, the Step 7 pricing
waterfall, the Step 9 P&L (KPIs incl. the loss flag + cost rows in WPF
build order) and the revision chain. The Step 5/6/7/9 sections are
READ views over the persisted sections — the whole-grid PUT editors
(SaveSpecGroups/SaveStep6/SaveStep7/SaveStep9) are a later slice,
documented here. Writes in this slice: create draft, transitions,
void, duplicate, revision — all 1:1 API calls with the server's
messages rendered verbatim.

**Phase 30 — front end slice 3: delivery + fiscal (mapping §1.15).**
Delivery: the DN list (search/status + masters-only + include-void +
paging) and the one-path CREATE DN — `POST /delivery-notes/job-orders/{id}`
reuses the server's CreateOrOpen rule set and renders its outcome
message verbatim. The DN detail page is the workspace's dispatch
view: header with the master's balance tracker, the dispatch edit
form (driver/vehicle/receiver/date/time/notes/FOC + per-line delivered
quantities with 0…balance inputs, `ExpectedRowVersion` for the
concurrency guard), transitions and void with reason. Fiscal: one page,
four tabs — tax invoices (search/status/include-void, issue transition
for drafts, record payment against the balance), credit notes (list),
the UAE VAT return (from/to period → the six report numbers), and the
statement generator (the WPF StatementFilter axes — customer/company/
project/salesman/from/to, ≥1 required — rendering the ledger with the
running balance and totals). Credit-note creation from an invoice is
not surfaced in this slice (the WPF flow is invoice-correction-driven);
documented as a later slice alongside the PI grid editors.

**Phase 31 — front end slice 4: reports (+ one route bug fix).**
Reports page with the ONE shared filter (the WPF ReportingFilter axes:
from/to + customer/company/salesman/project + search text) and five
tabs: the executive summary (the cohort funnel with share/step
conversion/bottleneck flags, headline money, the health line, top
customers/specifications), profitability (summary + one row per
proforma from the persisted Step 9 P&L with the costed/missing gate
and the cost-source text verbatim), job-order analytics (totals,
dominant spec/colour, top-10 mix tables, weekly throughput),
delivery analytics (qty totals, critical pending, ageing buckets,
pending/delivered mix), and the row trace (composite row id →
audit chain + the live quantity reconciliation). CSV exports, the
cutting-sheet/CNC views and cache stats stay API-only (not
surfaced in the SPA). **Route fix (Phase 31a):** ReportingController
was missing its `[Route("api/v1/reporting")]` attribute, so all its
endpoints mounted at the bare templates (`/executive-summary`, …)
contradicting the documented contract — the attribute was added,
matching the other 19 controllers (dotnet build is devbox-gated;
the change is the same one-line declarative pattern).

### 1.12 Explicitly deferred / out of initial scope (documented, not dropped)
| WPF feature | SaaS decision | Reason |
|---|---|---|
| AI cloud/local LLM, Whisper voice, agent | DEFERRED to post-core phase | Not core ERP business logic; APIs designed to add later |
| Screen recording | DEFERRED | Desktop-specific compliance feature |
| Velopack updates, crash helpers, splash | N/A — replaced by web delivery | Architectural translation |
| Demo mode / sample data | Replaced by tenant trial seed data | SaaS-native equivalent |
| Theme system (15 themes) | Web design system, light/dark | UX translation |

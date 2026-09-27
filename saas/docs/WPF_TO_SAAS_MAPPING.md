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
| WPF XAML views | React + TypeScript SPA (web-native UX, not XAML ports) | §15 |

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

### 1.7 Job Order / Production
| WPF | SaaS |
|---|---|
| `SavedJobOrder`, statuses `InProduction·Completed·Delivered·Approved` | `JobOrder` entity, same statuses |
| Stage work centers (Cutting, DoublingLamination, EdgeDeletion, HolesCutouts, Mitring, Pairing, Polish, Process/Control) | Production module stage screens + `/production/stages/{stage}` endpoints |
| Piece identity, QC, breakage, cancellation, release | `ProductionPiece` + services; explicit transitions |
| Material reservation/allocation; physical consumption contract | `MaterialReservation` service consuming stock via transactions |
| `ProductionDistributionEngine`, consistency scanner, snapshot integrity | Same services, server-side, scheduled consistency scans |

### 1.8 Delivery
| WPF | SaaS |
|---|---|
| `SavedDeliveryNote`+items, statuses `Draft·Dispatching·Dispatched·Signed·Received·Delivered` | `DeliveryNote` entity, same state machine (invalid transition → 409) |
| Partial delivery, returns, balance qty, per-JO SQM summaries (`GetSqmSummariesByJobOrderAsync`) | Same fields + `/job-orders/{id}/delivery-status` endpoint |

### 1.9 Tax Invoice / Credit Note / Statement / VAT
| WPF | SaaS |
|---|---|
| `SavedTaxInvoice` (`Issued·Paid·Overdue`), family links PI/DN/JO, `InvoiceTotalsCalculator` | `TaxInvoice` entity + totals service (decimal, MoneyRounding) |
| `SavedCreditNote` (fiscal correction; delete Manager/Admin-only) | `CreditNote`; void/delete gated by `TaxInvoice.Delete`-equivalent permission |
| `SavedStatement` + filters + reconciliation (`OrderReconciliationService`) + workflow pipeline | Statement generator service + reconciliation; same filters |
| VAT submission (`ReportingDataService.Vat` + `VatSubmissionView`) | VAT report endpoint + exports; per-tenant VAT rate config (default 5%) |

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

### 1.11 Licensing → Subscriptions
| WPF | SaaS |
|---|---|
| Signed license key + machine fingerprint + offline activation | Tenant subscription: plans, trial, active/inactive, limits (users/invoices/storage/optimization jobs), feature flags |
| `ReadOnlyGate` (expired → read-only writes blocked at service layer) | Middleware blocking writes for suspended/expired tenants, same explicit messaging |
| `LicensePlanCatalog` | Plan/Feature/TenantFeature tables + admin UI |
| LicenseGenerator tool | Internal admin endpoint (isolated behind abstraction for future payment provider) |

### 1.12 Explicitly deferred / out of initial scope (documented, not dropped)
| WPF feature | SaaS decision | Reason |
|---|---|---|
| AI cloud/local LLM, Whisper voice, agent | DEFERRED to post-core phase | Not core ERP business logic; APIs designed to add later |
| Screen recording | DEFERRED | Desktop-specific compliance feature |
| Velopack updates, crash helpers, splash | N/A — replaced by web delivery | Architectural translation |
| Demo mode / sample data | Replaced by tenant trial seed data | SaaS-native equivalent |
| Theme system (15 themes) | Web design system, light/dark | UX translation |

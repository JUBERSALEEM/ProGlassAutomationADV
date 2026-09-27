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
| `InquiryBoqConversion` → PI | `POST /api/v1/inquiries/{id}/convert-to-proforma` |
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

### 1.3 Specifications (price registry)
| WPF | SaaS |
|---|---|
| `specifications.db` + `SpecificationsCenterView` | `Specification` entity per tenant |
| Step 4 APPLY → save priced spec; `SpecificationLedgerService`; `SpecPaneResolver` | `POST /api/v1/specifications`; stamping on reuse |
| `SpecificationTargetCalculator` / `TargetPricing` / `TargetSnapshotRecompute` | Target-pricing service, recompute on document load |

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

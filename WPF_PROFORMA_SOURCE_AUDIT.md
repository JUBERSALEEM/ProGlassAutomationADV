# WPF Proforma Invoice source audit

**Audit status:** Working source-flow audit; **not yet complete at the requested every-file/every-path depth**. The 204-path inventory was structurally inventoried and key bindings, commands, formulas, navigation/persistence paths and cross-step calls were traced, but this document does not claim that every line in every listed file or every alternate-branch file has received an exhaustive line-by-line review. Step 8’s attachment path and Step 9’s Sheets dependencies were specifically traced; remaining detailed review includes deeper Step 2/3 persistence/error paths, full Step 4/5/6 formula/validation branches, all Step 7 export/render code and remaining Step 9 helper/export branches. **Conversion baseline selected:** ADV `main` at `d862436c125c5a83ac86bdef9ccef34c587408b3`; rationale and compatibility differences are recorded in §1. This selection follows repository/source evidence, not a claim that every WPF file was exhaustively reviewed. No WPF branch was checked out or modified. The inspected ADV sources were downloaded read-only into `/tmp`.

## 1. Source identity and authority finding

The actual nine-step Proforma Invoice implementation is in `JUBERSALEEM/ProGlassAutomationADV`; the extracted legacy `JUBERSALEEM/ProGlassAutomation` `master` at `2d89b3414baf871d52245c61ff71a26e9b7ab385` has no `Proforma*` paths and cannot be the source for this workflow.

The source candidates were compared rather than treated as equivalent:

| Candidate | Evidence | Decision |
|---|---|---|
| `JUBERSALEEM/ProGlassAutomation` `master` at `2d89b3414baf871d52245c61ff71a26e9b7ab385` | The extracted archive contains 71 files and no `Proforma*` source paths. | Legacy repository is not the source for the current nine-step PI module. |
| ADV configured default `arena/b24b721c-proglassautomationadv`, SHA `324b4f0fc96ebc1a53704734e313f032923909fd` (2026-10-15) | GitHub API identifies this Arena-named ref as configured default. Its four PI source trees match tag `v2.0.15`. | Retained as a released/compatibility comparison, not the conversion baseline. |
| ADV tag `v2.0.15`, SHA `cfaf2236d09800290925d5529fa0edba2e5a7c38` | Reproducible tagged release tree; compared PI source trees match the configured default. | Compatibility baseline for understanding behavior already shipped in v2.0.15. |
| ADV named branch `main`, SHA `d862436c125c5a83ac86bdef9ccef34c587408b3` (2026-10-19) | Post-tag named product branch. The 19 PI-file deltas are concentrated in additive issuer identity/sticker behavior and persistence, lifecycle, concurrency, snapshot-integrity and inventory-transaction hardening. Related source/regression tests exist for snapshot canonicalization and concurrency contracts. | **Selected conversion baseline.** It is the named upstream product branch and the later post-release PI implementation; the changes improve data integrity and lost-update/stock consistency rather than change the quote formula contract. |
| ADV `feat/eight-theme-center-wpf-nextjs`, SHA `befb0c855e0e9ccca11e05e9aa48c5d3f7f1fc94` | Release-family/tag comparison was checked; the tag is on this feature line. | Not a competing PI authority over post-tag `main`. |
| ADV `fix/production-readiness-2026-10-19`, SHA `9e4f6a6be8814fc5c9c954ade7c75e9b6368f5d7` | GitHub compare against `main` shows divergent non-PI changes; no PI source paths differ in that comparison. | No separate PI behavior overrides the selected `main` baseline. |

### Resolution of the 19 ADV default/tag → `main` PI file differences

The 19 files are listed immediately below. The conversion follows `main` for these differences because the source implements a coherent, safer contract: additive company telephone/website and embedded-logo fallback; optional Step 5 sticker printing; loaded `RowVersion` optimistic concurrency across invoice/list/lifecycle writes; explicit create-vs-update semantics; fail-closed snapshot-header canonicalization; lifecycle edit/delete guards; safe deletion when downstream linked documents exist; and atomic stock movement/history/trace metadata. The “update creates if missing” fallback in the older tree is removed in `main`; callers use explicit `InsertAsync` for creation, and stale/deleted rows fail as concurrency conflicts rather than being silently recreated or overwritten. These are not used to infer customer-facing calculations.

**Selected source policy:** ADV `main` is the normative WPF source for web parity. Keep `v2.0.15` differences recorded for compatibility and migration; do not silently discard the additive fields or integrity safeguards. No remote WPF deployment state was queried, and this selection is based on the public source chronology and behavior. The source review itself remains incomplete at exhaustive every-file depth, so implementation must continue to trace each relevant method and add parity/security tests. The website implementation is now authorized by the user’s directive; the earlier audit-only restriction is superseded.

**Website branch scope:** website implementation work remains on the session branch `arena/470a8371-proglassautomationadv` (current starting commit `17a99046d8abd1d253356c109179658146109627`) and uses its current checked-out `website/` tree, including existing local modifications, as the target baseline. The inspected ADV `main` WPF archive contains no `website/` directory; the actual website is present on the Arena/default and release-family trees. Do not replace the current website with another branch snapshot: preserve its existing inquiry, PI/account, and authentication work and extend it additively.

Files that differ between the default/tag-equivalent source and `main` within the audited PI source trees:

- `Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml`
- `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml`
- `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs`
- `ViewModels/ProformaInvoice/Step1CompanySetup/CompanyProfileCard.cs`
- `ViewModels/ProformaInvoice/Step1CompanySetup/CompanySetupViewModel.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.Optimization.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs`
- `Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs`
- `Models/ProformaInvoice/SavedInvoice/ProformaListRow.cs`
- `Models/ProformaInvoice/SavedInvoice/SavedInvoiceListItem.cs`
- `Services/ProformaInvoice/InquiryLinkSync.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/IProformaInvoiceRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoicesDbContext.cs`
- `Services/ProformaInvoice/ProformaLifecycleSync.cs`
- `Services/ProformaInvoice/SheetStockLinkService.cs`

## 2. System shape and state lifecycle

`Views/ProformaInvoice/ProformaInvoiceView.xaml` is a nine-stage wizard: **1 Company details → 2 Client profile → 3 Project specs → 4 Glass type & specs → 5 Data grid → 6 Other charges → 7 Print preview → 8 Attachments → 9 Profit & Loss**. The shell owns all nine ViewModels. This is a staff workflow, not a simple public web form.

Navigation is behavior, not presentation only. In `ProformaInvoiceViewModel.GoToStep`, the shell publishes current invoice identity to `ActiveInvoiceContext`; synchronizes Step 4 into Step 5 across the 4/5 boundary; refreshes Step 6 before downstream use; loads Step 8 on entry; recalculates Step 9 on entry; and silently saves a draft after navigation. Step 4 data is therefore not merely a rate widget: it feeds Step 5 and Step 6, has a session handoff through `SpecSheetRegistry`, and is captured in the persisted invoice snapshot. Restore rebuilds Steps 1–7 and rebinds Step 9 to current Step 4/5/6 sources.

`ProformaInvoiceSnapshot` (schema version 2) persists company, client, project, all Step 4 pricing sections, Step 5 groups/rows, Step 6 charges and wastage, Step 7 summary and terms, prepared-by state, wizard progress, and a Step 9 `ProfitLossSnapshot`. `ProformaInvoiceMapper` serializes/restores the composite. The local WPF persistence is an EF/SQLite invoice repository; it is not the current website Supabase model.

## 3. Per-step findings

### Step 1 — Company / document identity

- Inputs: issuing company name, UAE TRN, location, representative/contact, company email, bank/IBAN, optional logo and logo layout/dimensions, Proforma ID, issue/valid-until dates, and status. Saved company cards can populate the fields; users can browse all saved profiles and choose/clear a logo.
- Company and Proforma ID have explicit inline required-field messages. The TRN has a format message. Cross-step validation also requires the company name and ID and checks the validity period (at least one day; over 365 days warns).
- The company profile cards and suggestions are loaded from master data. Proforma ID generation consults the invoice repository and is uniqueness-aware. New invoices default to Draft, issue date today and validity seven days, then load saved company/client/project/salesperson options. Existing snapshots preserve logo layout, sizing, and company record ID.
- The selected `main` baseline adds company telephone/website fields and embedded-logo fallback; these are additive issuer/document identity fields and must be represented in web state and customer-safe document output where configured.

### Step 2 — Client / billing profile

- Inputs: customer name, optional customer TRN, reference code, sales representative, document-prepared-by, attention executive, phone, billing address. Sales representative is an editable list populated from master data; a newly typed name is included in save handling. Saved client cards and refresh/view-all controls are present.
- Customer name is required. A nonempty TRN must be exactly 15 digits and contain digits only; blank TRN is allowed. `DocumentPreparedBy` defaults from the active audit user. External inquiry selection can be adopted/cleared by the shell.
- Selecting the client name drives the Step 3 project filter. Client values persist in the invoice snapshot; these are distinct from WPF customer/account records and the website's inquiry-submission profile.

### Step 3 — Project

- Inputs: project/site identifier, project reference (kept separate from project name), contract number, LPO/PO number, site delivery location. Saved project cards support refresh/view-all and selecting a prior project.
- The shell passes the selected Step 2 client to `ProjectSpecsViewModel.SetClientFilter`, but `LoadCardsAsync` sorts matching-client projects to the top rather than excluding other clients; the project-name text separately filters suggestions. Clearing the client filter removes that priority. The project fields are snapshot state. The cross-step validator does not make project name/contract/LPO mandatory.

### Step 4 — Glass type and pricing parameters

- Four distinct build parameter surfaces: **SGU, DGU, LAM, DGU+LAM**. Inputs include pane colors/thickness/treatment and sheet size/pricing, yield, margin, cutting/tempering, spacer/U-insert/panel extras, PVB/interlayer and edge/polish details appropriate to each build. Each calculation section has its own snapshot; multiple sections are supported and can be added/removed.
- Store data is read through `SheetStorePricingService` from `SheetStoreRepository.GetAllAsync()`. Its contract is read-only, guarded/non-throwing, fresh per async lookup (no async cache); it supplies colors, thicknesses, sizes, and per-SQM inventory prices. Sheet size parsing and store price resolution are shared with `SpecSheetRegistry` and `Step4MaterialIntegrity`.
- Verified selling-rate formulas (source ViewModels):
  - SGU: `(base price / safe yield + cutting + tempering) × (1 + margin%)`.
  - DGU: `((outer + inner base prices) / safe yield + spacer) × (1 + margin%)`.
  - LAM: `((outer + inner base prices) / safe yield + PVB + both panes’ cutting and tempering) × (1 + margin%)`.
  - DGU+LAM: three pane base prices divided by yield, plus cutting, tempering, interlayer selling price and spacer, then multiplied by `1 + margin%`.
- Nonpositive yield uses a warning/fallback; a missing interlayer price is called out. `Step4MaterialIntegrity.Evaluate` requires an explicit Sheet Store provenance flag, valid color/thickness/treatment/sheet dimensions, and a positive resolved rate; a stale nonzero default is not treated as a valid material identity. **This is not an Apply-command gate:** `ApplyRateCommand` checks active specs, positive `EstimatedRatePerSqm`, and a nonblank generated spec name, while `ApplyRateAsync` saves the spec and sheet selection. The validity verdict is captured on the Step 4 snapshot/Step 5 group and is used to refuse optimization when unresolved; do not conflate quote-rate application with optimizer material-integrity gating. `SpecSheetRegistry` is an in-memory, normalized spec-name handoff; the persisted equivalent is per-invoice `SpecGroupSnapshot` state, not the portable specification catalog.
- On Step 4 Apply, Step 5 receives the selected spec/build/rate/pane/sheet metadata. Shell synchronization preserves manual Step 5 overrides rather than overwriting them. The Step 4 snapshot is also exposed to Step 6 and Step 9.

### Step 5 — Data grid / cut list

- A document contains multiple spec groups. A group retains build type, selected spec name, imported reference price, base and surcharge rates, explicit Step 4 sheet/pane metadata and manual-override ownership markers; each group contains rows with reference, W1/H1, optional W2/H2, quantity, per-pane and total SQM/LM, rate and value.
- Area rule: `GlassItemRow` calls `SqmCalculator.CalculatePanel` for W1/H1 and W2/H2; invalid/nonpositive dimensions yield 0, otherwise billable panel area is rounded to 3 decimals and floored to **0.5 m² per panel**. Total is per-panel value × quantity, rounded to 3 decimals. Sheet area has no minimum and is rounded to 4 decimals. Row LM is `((W + H) / 1000) × perimeter factor × qty`, rounded to 3 decimals. LAM uses a perimeter factor of four; other types use two for the relevant perimeter calculations. `ProformaChargeMetricsCalculator` also calls `CalculatePanel` for Step 6 aggregates. The no-floor `CalculatePhysicalPanelArea` helper is a separate API and is not the Step 5 row calculation.
- Row value is first-pane billable SQM × quantity × row rate, rounded with `MoneyRounding`. Base/surcharge rate is `base × (1 + surchargePercent/100)`; automatic rate is the surcharge rate when first-pane billable area is at least 4 m², otherwise base. A selected library spec can autofill the base rate unless a manual Step 5 override exists. Section/group totals sum rows and round by their respective area/money rules.
- Commands include add/remove groups/rows, paste from clipboard, fill down, find/replace, import/export, share, and quick/full optimization. Import accepts XLSX, CSV/TXT and parser-supported JSON/scanner shapes; imported rows group by spec name where possible, otherwise the selected group. Export uses `CutListGridIo` for XLSX/CSV/PDF. Optimization uses the shared engine via a shell-bound `Step5OptimizationRunner`; it consumes spec snapshots/material validity and supplies a Step 6 wastage feed.
- `SpecGroupService.ValidateRow` characterizes phantom rows and guards dimensions/quantity up to 10,000; the shell-level cross-step validator separately requires at least one group, disallows negative totals and warns on zero area/pieces. Exact validation call sites and its applicability to all user-entry paths should be preserved in parity tests rather than inferred from the validator existing.

### Step 6 — Other charges and wastage

- Step 6 derives metrics from Step 5 snapshot groups (`ProformaChargeMetricsCalculator`) and Step 4 pricing snapshots. UI exposes metric discovery/targeting, predefined charge rows, manual/advanced automation, custom charges, and wastage rows. Metrics include per-pane and combined SQM/LM, piece counts, DGU cavity area, and pane/perimeter factors. Polish quantities are recalculated from selected spec groups and subtype; custom polish quantities can be entered separately.
- Active predefined charge result is `MoneyRounding.Round(MetricQty × Rate)`. Delivery is fixed at one trip. Polish uses calculated LM (or custom input); other rows use the selected target's computed metric. Active custom charge result is `MoneyRounding.Round(Qty × Rate)`. Section total is active predefined amounts + active wastage amounts + active custom amounts, rounded to two decimals.
- Wastage row SQM is the sum of checked Step 5 pane/color sources. Manual formula is active `TotalSqm × Rate`, rounded to cents. When the optimization formula is selected, with baseline 85%, it computes `Section1 = pricePerSqm/(utilization/100)`, `Section2 = pricePerSqm/0.85`, `rate = Section1 - Section2`, and `result = rate × selected source SQM` (intermediate values rounded to six decimals). Sheet quantity and utilization can be fed from the optimization run; manual values are preserved separately when the feed is absent/disabled.
- A user-committed Step 6 sheet quantity may auto-consume local Sheet Store stock. The service matches color + sheet dimensions (thickness as tie-breaker), verifies current balance, writes usage/history/audit, and displays a warning on misses/shortfall. The input text setter raises this event only for a committed user edit; programmatic restore/feed updates do not. This is an internal stock mutation and must never be reachable through public/customer APIs.
- Cross-step validation warns when a large order has zero other charges; Step 6 negative totals block save. No web-side calculation is currently equivalent to this complete charge/wastage model.

### Step 7 — Print preview, totals, terms, save/export

- Summary waterfall: `Net = Step5 glass subtotal + Step6 other charges`; discount can be fixed AED or percentage (nonnegative, percentage capped at 100%; actual discount capped at net); `Before VAT = Net - discount`; VAT defaults to 5% and is applied only when enabled to the discounted subtotal; grand total is rounded to money precision. Applying a customer target grand total calculates a fixed AED discount (never raises the undiscounted total) and iterates cent adjustments to match the requested VAT-inclusive total.
- Terms are editable, reorderable/activatable clauses with editable payment sub-lines and reset-to-default confirmation. The document carries a UAE glass-industry default set. Preview includes company logo/identity, client/project details, lines, charges, summary and terms.
- Save/load is via `IProformaInvoiceRepository` + `ProformaInvoiceMapper` full-snapshot JSON; navigation triggers silent draft autosave. Save checks ID, body-edit lifecycle lock, cross-step validation and duplicate/transaction behavior. PDF uses `ProformaInvoicePdfLayout`/QuestPDF export; Excel and CSV have separate layouts; share uses the chosen report format. The email action is shown as coming soon in the inspected UI.
- **Important coupling:** `CrossStepValidator` includes Step 9 total cost/revenue and margin thresholds. `PrintableInvoiceViewModel.BuildValidationSnapshot()` reads Step 9 margin/cost/revenue, and save/print/export gates invoke that validator. Thus customer-document readiness can currently be blocked by internal Step 9 profitability state even though the standard customer PI layouts do not render that data. The web conversion must intentionally break this coupling while preserving quote totals and validation; do not expose internal values as a workaround.

### Step 8 — Attachments and approval (excluded from customer PI journey)

- WPF screen has attachment list, type/name/size/date, open/rename/delete, refresh, and one “Approve invoice” action. `ProformaAttachmentService` copies image/PDF files to AppData under a Proforma-ID folder and stores a JSON manifest; it validates extension/size using shared attachment limits. Delete requires `ProformaEdit`; attachment actions log audit. Approval upload is coupled to the signed-PI/LPO/status/Job Order continuation and duplicate-document confirmation.
- This is an internal document-control/approval function, not a missing customer upload screen. The new customer PI journey excludes Step 8 entirely. Existing customer inquiry upload facilities remain inquiry supporting documents and must not be treated as Step 8 parity. Do not route approval or internal attachment manifests into the customer quotation.

### Step 9 — Profit & Loss; retain only tab 3 “Sheets” internally

- Four tabs in source order: Overview, Services, **Sheets**, Specs. The requested internal-only page is tab 3, the Sheets profitability analysis; other P&L screens/reports are not part of the PI Generator.
- `ProfitLossStepViewModel.Binding.Bind` is wired to Step 4 build ViewModels/snapshots, Step 5 spec groups, Step 6 charge/wastage, Step 7 gross/net revenue and document context. The four tabs are rendered by `ProfitLossStepView.xaml`: Overview (with target pricing and P&L export), Services, Sheets, Specs. The other three are excluded from the web PI Generator.
- The Sheets page reads Step 5 pane/spec rows, Step 4 chosen sheet dimensions/prices, Step 6 checked wastage source/sheet quantity/utilization/charge, and optimization-run figures when available. It prefers configured Step 6 sources. Active Step 6 setup rows missing sheet quantity, checked pane source or usable sheet sizes create visible setup-notice/review rows; the notice row itself contributes zero charge to totals to avoid double-counting. Valid shared setups allocate integer sheets over source demand; matching optimization-run pane/rack dimensions can replace the allocation count/utilization. Panes not handled by Step 6 are a separate Step 5 glass-row fallback using required area plus the Step 9 wastage percentage when no Step 6 quantity exists. Where a rack/sheet count is shared, `Step6SheetAllocation` apportions positive integer sheets by demand/useful-area weights and largest remainder; inferred utilization is capped at 100%.
- It calculates buy/used/leftover sheets and areas, material buy/used/wastage values, effective/landed rate per SQM, yield premium, Step 6 charge allocation by each source’s SQM share, usage efficiency, shortfalls and setup/review diagnostics. `PnLMeasurement.MeasureGroup` explicitly uses `SqmCalculator.CalculatePanel` for pane cut-area, matching Step 5’s 0.5 m² billable minimum; stock-sheet physical area is a separate no-floor measure. Do not substitute raw panel physical area for the WPF Step 5/Step 9 panel SQM. Shortfall is displayed when allocated/bought sheets are below `ceil(total selected source SQM / useful sheet SQM)`; the efficiency KPI considers Step 6-charged rows only, falling back to all material only when no Step 6 charge rows are posted. It supports row selection/detail, refresh and CSV/Excel/PDF/share exports. These amounts are internal purchase/cost/profitability data.
- **Isolation conflict in source:** Step 9 state is in the same `ProformaInvoiceSnapshot`, is wired to Step 7, and its profit summary participates in customer save/print validation. The Step 9 dossier exporters have an explicit opt-in P&L mode; defaults exclude P&L, but the new website must make this separation structural, not rely on callers leaving a boolean false. Retain only the Sheets tab behind staff authorization and a distinct internal API/document route; do not expose Steps 9 Overview, Services, or Specs or any internal sheet cost fields in customer payloads/PDFs.

## 4. Cross-step dependency and validation map

| Source → consumer | Observed dependency / implication |
|---|---|
| Step 1 → full snapshot / Step 7 | Issuer details, logo and PI identity appear in the customer document; uniqueness and date/status lifecycle matter. |
| Step 2 → Step 3 / Step 6 stock history / exports | Client selection filters projects; customer/project labels may be stamped into inventory history; customer fields appear in document. |
| Step 4 → Step 5 | Per-section build/spec/rate/panes/sheet selection is pushed into corresponding group, with override ownership and registry/persisted snapshot behavior. |
| Step 4 + Step 5 → Step 6 | Material panes, sheet sources, charge metrics, polish LM and wastage inputs derive from these two steps. |
| Step 5 → optimization → Step 6 + Step 9 | Optimization receives spec snapshots, then feeds sheet count/utilization; Step 9 consumes this feed when matching source information exists. |
| Step 6 → Step 7 + Step 9 | Active/custom/wastage charges feed quote subtotal; selected wastage rows, quantities/utilization and charges feed the internal Sheets analysis. |
| Step 7 ↔ Step 9 | Step 7 owns quote VAT/discount totals; Step 9 reads revenue/total and currently feeds profitability into Step 7 save/export validation. Must be split in web design. |
| Step 8 ↔ lifecycle | Approval and uploaded signed document can continue to status/Job Order; delete/cancel/reject lifecycle clears attachments. Excluded from customer PI journey. |
| Persist/restore | `ProformaInvoiceMapper` round-trips all wizard state. Step 9 needs explicit rebind/recalc after restore; Step 4 registry is session-only, so persisted group snapshots carry per-invoice data. |

Cross-step validation blocks missing PI ID/company/customer, invalid validity dates, no Step 5 group, negative totals, Step 7 waterfall mismatches, VAT out of range, discounts above net, and (when revenue/cost are positive) negative/under-5% profit margin. It warns for long validity, suspicious missing charges, large discounts, no pieces/area, zero-priced orders, and margins under 10%. Errors block save/print; warnings require operator confirmation. The website must preserve customer-safe validation and drop/relocate internal margin rules.

## 5. Exact source inventory and reviewed surfaces

The file index below is the 204-file inventory under the four ProformaInvoice source trees from the default/tag-equivalent baseline. All 204 unique paths were checked against the extracted baseline and exist; the list is scope inventory, not proof of exhaustive per-line review. The source inventory is at `/tmp/pi-full-files.txt` in this sandbox. Paths below are repository-relative and identify the files in scope for each step; the audit traced the key bindings, commands, formulas, state transitions and cross-step dependencies described above, but does not claim an exhaustive line-by-line review of every inventoried file. `README.md` files are contextual documentation, not executable behavior.

### Step 1
- `ViewModels/ProformaInvoice/Step1CompanySetup/CompanyProfileCard.cs`
- `ViewModels/ProformaInvoice/Step1CompanySetup/CompanySetupViewModel.cs`
- `Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml`
- `Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml.cs`

### Step 2
- `ViewModels/ProformaInvoice/Step2ClientProfile/ClientProfileCard.cs`
- `ViewModels/ProformaInvoice/Step2ClientProfile/ClientProfileViewModel.cs`
- `Views/ProformaInvoice/Step2ClientProfile/ClientProfileView.xaml`
- `Views/ProformaInvoice/Step2ClientProfile/ClientProfileView.xaml.cs`

### Step 3
- `ViewModels/ProformaInvoice/Step3ProjectSpecs/ProjectProfileCard.cs`
- `ViewModels/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsViewModel.cs`
- `Views/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsView.xaml`
- `Views/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsView.xaml.cs`

### Step 4
- `ViewModels/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersViewModel.SheetStore.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersViewModel.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersViewModel.SheetStore.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersViewModel.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/GlassTypeCard.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersViewModel.SheetStore.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersViewModel.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part1.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part2.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersViewModel.SheetStore.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersViewModel.cs`
- `ViewModels/ProformaInvoice/Step4PricingEstimator/SpecTargetGroup.cs`
- `Views/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersView.xaml`
- `Views/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersView.xaml.cs`
- `Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml`
- `Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml.cs`
- `Views/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersView.xaml`
- `Views/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersView.xaml.cs`
- `Views/ProformaInvoice/Step4PricingEstimator/PricingEstimatorView.xaml`
- `Views/ProformaInvoice/Step4PricingEstimator/PricingEstimatorView.xaml.cs`
- `Views/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersView.xaml`
- `Views/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersView.xaml.cs`

### Step 5
- `Models/ProformaInvoice/Step5DataGrid/GrandTotalSummary/SpecGroupSummaryRow.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.ImportExport.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.Optimization.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/GlassItemRow.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryViewModel.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part3.cs`
- `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.cs`
- `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml`
- `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml.cs`
- `Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryView.xaml`
- `Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryView.xaml.cs`
- `Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/README.md`

### Step 6
- `Models/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargeRow.cs`
- `Models/ProformaInvoice/Step6OtherCharges/GridSourceOption.cs`
- `Models/ProformaInvoice/Step6OtherCharges/LaminateGlassMatcher.cs`
- `Models/ProformaInvoice/Step6OtherCharges/MetricOption.cs`
- `Models/ProformaInvoice/Step6OtherCharges/OtherChargeRow.cs`
- `Models/ProformaInvoice/Step6OtherCharges/OtherChargeUnit.cs`
- `Models/ProformaInvoice/Step6OtherCharges/PolishSubType.cs`
- `Models/ProformaInvoice/Step6OtherCharges/PredefinedChargeTemplates.cs`
- `Models/ProformaInvoice/Step6OtherCharges/SpecCheckOption.cs`
- `Models/ProformaInvoice/Step6OtherCharges/SpecGroupMetric.cs`
- `Models/ProformaInvoice/Step6OtherCharges/SpecTargetSelection.cs`
- `Models/ProformaInvoice/Step6OtherCharges/Wastages/SpecGroupColorSide.cs`
- `Models/ProformaInvoice/Step6OtherCharges/Wastages/WastageRow.cs`
- `ViewModels/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionViewModel.cs`
- `ViewModels/ProformaInvoice/Step6OtherCharges/MetricDiscoveryHubViewModel.cs`
- `ViewModels/ProformaInvoice/Step6OtherCharges/OtherChargesViewModel.cs`
- `ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs`
- `Views/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml`
- `Views/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml.cs`
- `Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml`
- `Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml.cs`
- `Views/ProformaInvoice/Step6OtherCharges/README.md`
- `Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml`
- `Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml.cs`

### Step 7
- `Models/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/DiscountType.cs`
- `Models/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermClause.cs`
- `Models/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsDefaults.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryViewModel.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/PrintPreviewViewModel.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part2.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsViewModel.cs`
- `Views/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryView.xaml`
- `Views/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryView.xaml.cs`
- `Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml`
- `Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml.cs`
- `Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml`
- `Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml.cs`
- `Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/README.md`
- `Views/ProformaInvoice/Step7PrintPreview/README.md`
- `Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/README.md`
- `Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsView.xaml`
- `Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsView.xaml.cs`

### Step 8 (traced as excluded)
- `ViewModels/ProformaInvoice/Step8Attachments/AttachmentsStepViewModel.cs`
- `ViewModels/ProformaInvoice/Step8Attachments/ProformaAttachmentRowViewModel.cs`
- `Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml`
- `Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml.cs`

### Step 9 (only Sheets retained)
- `ViewModels/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/PnLMeasurement.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/PnLSpecParser.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossRow.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Binding.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CalculationSupport.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CostBasis.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Health.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Helpers.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.LiveRecalc.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Outputs.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Persistence.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.QuoteBasis.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/SpecCostGroup.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/SpecTargetRowViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/Step6SheetAllocation.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/Step9ReportExport.cs`
- `Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml.cs`
- `Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml.cs`
- `Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml.cs`
- `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml`
- `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml.cs`

### Shared invoice models and services
- `Models/ProformaInvoice/Attachments/ProformaAttachmentRecord.cs`
- `Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs`
- `Models/ProformaInvoice/SavedInvoice/ProformaAnalyticsRow.cs`
- `Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs`
- `Models/ProformaInvoice/SavedInvoice/ProformaListRow.cs`
- `Models/ProformaInvoice/SavedInvoice/ProformaSearchRow.cs`
- `Models/ProformaInvoice/SavedInvoice/SavedInvoiceListItem.cs`
- `Models/ProformaInvoice/SavedInvoice/SavedProformaInvoice.cs`
- `Services/ProformaInvoice/ActiveInvoiceContext.cs`
- `Services/ProformaInvoice/Attachments/ProformaAttachmentService.cs`
- `Services/ProformaInvoice/Export/PiPreviewParityRenderer.cs`
- `Services/ProformaInvoice/Export/ProformaChargeMetricsCalculator.cs`
- `Services/ProformaInvoice/Export/ProformaDossierExcelLayout.cs`
- `Services/ProformaInvoice/Export/ProformaDossierPdfLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoiceCsvLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoiceExcelLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoiceListExcelLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoicePdfLayout.cs`
- `Services/ProformaInvoice/Export/README.md`
- `Services/ProformaInvoice/GlassPriceStamper.cs`
- `Services/ProformaInvoice/InquiryBoqConversion.cs`
- `Services/ProformaInvoice/InquiryIntegrationService.cs`
- `Services/ProformaInvoice/InquiryLinkSync.cs`
- `Services/ProformaInvoice/InvoiceLifecycleStateMachine.cs`
- `Services/ProformaInvoice/InvoiceRoundTo.cs`
- `Services/ProformaInvoice/OtherChargeAutomationRules.cs`
- `Services/ProformaInvoice/ProfitLossIntegrityCheck.cs`
- `Services/ProformaInvoice/ProformaDuplicateSnapshotScrub.cs`
- `Services/ProformaInvoice/ProformaInvoiceNavigator.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/IProformaInvoiceRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoicesDbContext.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/README.md`
- `Services/ProformaInvoice/ProformaLifecycleSync.cs`
- `Services/ProformaInvoice/SheetStockLinkService.cs`
- `Services/ProformaInvoice/SheetStorePricingService.cs`
- `Services/ProformaInvoice/SpecGroupService.cs`
- `Services/ProformaInvoice/SpecSheetRegistry.cs`
- `Services/ProformaInvoice/SpecificationsRepository/ISpecificationsRepository.cs`
- `Services/ProformaInvoice/SpecificationsRepository/README.md`
- `Services/ProformaInvoice/SpecificationsRepository/SpecCatalogNotifier.cs`
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsDbContext.cs`
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsRepository.cs`
- `Services/ProformaInvoice/Step4MaterialIntegrity.cs`
- `Services/ProformaInvoice/TargetPricing/SpecificationTargetCalculator.cs`
- `Services/ProformaInvoice/TargetPricing/Step5PriceSource.cs`
- `Services/ProformaInvoice/TargetPricing/Step6ChargeResolver.cs`
- `Services/ProformaInvoice/TargetPricing/TargetMarginTolerance.cs`
- `Services/ProformaInvoice/TargetPricing/TargetPriceModels.cs`
- `Services/ProformaInvoice/TargetPricing/TargetSnapshotRecompute.cs`
- `Services/ProformaInvoice/Validation/CrossStepValidator.cs`
- `ViewModels/ProformaInvoice/InvoiceItem.cs`
- `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/InvoiceApprovalViewModel.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/ProformaStageChip.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.cs`
- `ViewModels/ProformaInvoice/WizardStep.cs`
- `Views/ProformaInvoice/ProformaInvoiceView.xaml`
- `Views/ProformaInvoice/ProformaInvoiceView.xaml.cs`
- `Views/ProformaInvoice/README.md`
- `Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml`
- `Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml.cs`
- `Views/ProformaInvoice/SavedInvoicesList/README.md`
- `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListDialog.xaml`
- `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListDialog.xaml.cs`
- `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListView.xaml`
- `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListView.xaml.cs`
- `Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml`
- `Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml.cs`

### Additional shared dependencies traced

- `Utilities/SqmCalculator.cs`; `Services/Reporting/MoneyRounding.cs`; `Services/SheetStore/SheetStoreRepository.cs` and its stock history repository; `Services/Export/Core` export engines/layout interfaces; `Services/Export/FileSystem` pickers/file naming; `Services/Sharing/ShareLauncher.cs`; `Services/Audit/AuditService.cs`; `Services/Database/AppDataRoot.cs`; master-data and inquiry repositories/services; the Step 5 optimization runner and optimizer host; `Tests/ProformaInvoice`, `Tests/Agent` PI characterization/regression tests.

## 6. Relevant test inventory and characterization evidence

Relevant source test inventory (39 baseline paths plus five selected-`main` PI/inventory/sticker tests = 44 unique paths; all listed paths exist in the selected `main` extract. This confirms file inventory only; the WPF test suite was not run and not every test body was reviewed): `Tests/Agent/CutListImportParserTests.cs`, `Tests/Agent/InvoiceRoundToTests.cs`, `Tests/Agent/OptimizationCutListTabTests.cs`, `Tests/Agent/OptimizationCutListWorkspaceTests.cs`, `Tests/Agent/ProformaStep4PersistenceSourceTests.cs`, `Tests/Agent/ProformaStep5BaseRateAndChargesRegressionTests.cs`, `Tests/Agent/SqmCalculatorTests.cs`, `Tests/Agent/Step4GlassTypeWiringTests.cs`, `Tests/Agent/Step4LiveSyncRepairTests.cs`, `Tests/Agent/Step4MaterialIntegrityTests.cs`, `Tests/Agent/Step5OptimizationRunTests.cs`, `Tests/Agent/Step5ProgressAndViewResultTests.cs`, `Tests/Agent/Step9ComparisonReportTests.cs`, `Tests/Agent/Step9SheetDetailTests.cs`, `Tests/Optimization/OptimizationManualCutListReentryTests.cs`, `Tests/OrderReport/OrderReportStep9PriceDecisionTests.cs`, `Tests/ProformaInvoice/AuthSessionServiceTests.cs`, `Tests/ProformaInvoice/ClientProfileSalesmanReloadTests.cs`, `Tests/ProformaInvoice/ClientProfileViewModelTests.cs`, `Tests/ProformaInvoice/CompanySetupViewModelTests.cs`, `Tests/ProformaInvoice/CrossStepValidatorFinancialWaterfallTests.cs`, `Tests/ProformaInvoice/InquiryFieldVisibilityTests.cs`, `Tests/ProformaInvoice/InquiryPiIntegrationTests.cs`, `Tests/ProformaInvoice/OtherChargeRowMetricQtyCharacterisationTests.cs`, `Tests/ProformaInvoice/PostAuditCleanupTests.cs`, `Tests/ProformaInvoice/PriceBasisSignalTests.cs`, `Tests/ProformaInvoice/ProformaDuplicateSnapshotScrubTests.cs`, `Tests/ProformaInvoice/ProformaFieldVisibilityTests.cs`, `Tests/ProformaInvoice/ProformaIdHardeningTests.cs`, `Tests/ProformaInvoice/ProformaInvoiceViewModelDisposeTests.cs`, `Tests/ProformaInvoice/ProformaStatusWorkflowSourceTests.cs`, `Tests/ProformaInvoice/ProjectSpecsViewModelTests.cs`, `Tests/ProformaInvoice/ReadOnlyGateTests.cs`, `Tests/ProformaInvoice/SnapshotRoundTripTests.cs`, `Tests/ProformaInvoice/SpecificationTargetBuildTypeTests.cs`, `Tests/ProformaInvoice/Step9PriceDecisionReportTests.cs`, `Tests/ProformaInvoice/Step9ReportIdentityTests.cs`, `Tests/ProformaInvoice/TargetMarginToleranceTests.cs`, and `Tests/ProformaInvoice/TargetPriceModelTests.cs`. The selected `main` comparison adds `Tests/Agent/ProformaInvoiceSnapshotHeaderSourceTests.cs`, `Tests/Database/InventoryStockConcurrencyTests.cs`, `Tests/Printing/Stickers/StickerLabelBuilderTests.cs`, `Tests/Printing/Stickers/StickerSheetLayoutTests.cs`, and `Tests/Printing/Stickers/StickerTemplateStoreTests.cs` to the relevant source set.

These tests are useful parity examples, not proof that the website already implements the same behavior. Before code work, establish a small approved golden-case set from the selected authoritative branch and compare results in both runtimes.

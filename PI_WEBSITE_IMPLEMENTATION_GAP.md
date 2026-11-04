# PI website implementation gap audit

**Purpose:** Compare the actual website on this branch with the selected WPF `main` baseline recorded in `WPF_PROFORMA_SOURCE_AUDIT.md`. This gap analysis is now an implementation checklist under the user's directive. It does not authorize speculative changes to remote Supabase/Vercel settings or imply that local SQL matches deployed state.

## 1. Current website surfaces (observed locally)

There are three related but different flows; they must not be mistaken for one converted WPF module.

1. **Customer inquiry / PI technical request:** `website/src/components/inquiries/CustomerInquiryPortal.tsx`, `ProformaTechnicalSpecFlow.tsx`, `customer-pi-attachments.tsx`, `customer-pi-processing.ts`, and `customer-cutlist.ts`. The PI intent gathers company/contact/project requirements, then a browser-local technical specification and cutting list. On submit, the UI generates one unpriced Inquiry PDF, allows optional supporting files, and finalizes an inquiry submission. Customer-side spreadsheet exports, layout previews, and offcut-reuse recommendations have been removed; technical specification entry and cutting-list import remain. It does not create a priced WPF-style PI quote.
2. **Staff inquiry-linked Proforma workspace:** `website/src/components/inquiries/ProformaWorkspace.tsx` and the `/api/inquiries/[id]/proforma` API. It edits grouped cut-list lines with manually entered rates and optional base-rate/surcharge settings; it saves revisions to `inquiry_proformas`, `inquiry_proforma_lines`, `inquiry_proforma_groups`, and `inquiry_proforma_saves`. It is a line editor, not the nine-stage WPF wizard.
3. **Former customer priced-proforma viewer (retired):** the account API routes and `CustomerProformas.tsx` previously exposed shared proforma headers, totals, approved rates and line values. The account page had a QuickLink and `/inquiries` had a shared-proformas card, but no matching `[locale]/account/proformas` page exists in the current route tree. The API endpoints now return no-store 410 responses, the compatibility service throws without querying Supabase, the stale component renders only a boundary notice, and links to the viewer are removed. Additive C11 SQL removes the customer RLS policies; it is not yet deployed to the live Supabase project.

Current worktree already has uncommitted PI/account changes. They are preserved. This report does not amend or revert them.

## 1.1 Required product boundary and Phase C11 security audit

- **Customer Inquiry Generator:** company/contact/project, SGU/DGU/LAMI/DGU+LAMI technical inputs, dimensions/quantity, processing requests and drawings/references. It submits an unpriced inquiry for staff review. The current generated customer attachments are technical-only and explicitly label the absence of rates/charges/margin/profit/PI value; no customer sell-rate calculator is present in that flow.
- **Internal Staff PI:** staff-role-gated review and existing narrow website PI workspace remain distinct. Verified WPF `d862436c125c5a83ac86bdef9ccef34c587408b3` remains the authoritative pricing and customer-priced PI release system. B05 stays internal-only; no public pricing engine is being added.
- **Confirmed previous exposure:** before this slice, `/api/account/proformas` and `/api/account/proformas/[id]` queried shared proformas and returned `total_value`, `approved_rate` and `line_value`. C7 granted customer SELECT on matching proformas, lines and the share ledger. A link/card implied customers could view those documents. This violated the clarified boundary even though the projection was revision-scoped and did not include purchase cost/P&L.
- **Local implementation:** customer list/detail routes are no-store 410 tombstones; the service fails closed without Supabase queries or priced DTOs; stale account components render a boundary notice; customer navigation no longer links to priced documents. Staff share POST is disabled, while legacy staff audit state and withdrawal remain. C11 removes customer RLS, reasserts staff-only policies, disables the share RPC and records old shares as migration-withdrawn without deleting audit rows. Existing original inquiry, staff PI, save history and WPF path are retained.
- **Verification boundary:** The local inquiry-security suite, full website Vitest suite, typecheck, and lint pass. C11 was rehearsed on a disposable Supabase PostgreSQL 17 project with customer/admin/auditor/no-role/estimator identities; the C11 SQL statements were re-applied idempotently and the exact migration source was also executed through Supabase's migration API; RLS/grants and staff RPC behavior were checked, and real v2 saves/idempotency/revision calculations were exercised. Prerequisite schemas and Phases 15/C1/C5/C7/C8 were scaffolded/reconstructed rather than replayed verbatim as a complete repository migration chain; HTTP PostgREST and live document-download endpoints were not exercised. The disposable project was deleted. C11 is **not deployed** to the active project; do not report production customer/internal separation complete.

## 2. Step-by-step gap

| WPF source behavior | Website behavior found | Gap / risk before conversion |
|---|---|---|
| **Step 1: company identity, letterhead, TRN, bank, logo, ID, issue/validity/status** | Customer intake collects company/contact basics; the staff workspace reads the linked inquiry header. There is no full issuing-company profile/letterhead state, WPF logo layout, PI validity/status lifecycle, or WPF unique ID flow in the workspace. | Need a dedicated issuer/client/project document state. Do not reuse the inquiry form/company profile as issuer data. Add date/status/ID rules and PDF parity using the selected ADV `main` baseline. |
| **Step 2: client billing profile, sales rep, attention, prepared-by** | Intake stores customer/contact data; operator workspace shows inquiry/customer info but does not reproduce client cards, salesman list, saved billing address, prepared-by audit identity, or external selection adoption. | Model customer identity and issuing company as distinct entities; carry full billing fields into saved quote and export. Do not use customer account auth identity as a Proforma contact field. |
| **Step 3: client-filtered project fields** | Intake has project name/location and inquiry metadata. No WPF project cards, contract/reference/LPO fields, client-based saved-project filter, or same project restore semantics were found in the PI workspace. | Add the remaining project fields and state linkage, retaining project reference separately from project name. |
| **Step 4: SGU/DGU/LAM/DGU+LAM pricing estimator** | Customer `ProformaTechnicalSpecFlow` captures technical pane details and sheet dimensions but does not compute a WPF selling rate. Staff `ProformaBuilder` accepts a manually entered line rate and group base/surcharge. `pricingRules.ts` also declares `outerSheetRates`, `buildRateAdders` and `step4RatePerSqm`, but the staff builder does not call that function; its fixed rates, colour multipliers, adders and 1.58 factor are not the WPF formulas or a live store lookup. | Major calculation and state gap, including a disconnected incorrect price function. `customer-pi-processing.ts` derives technical processing labels, not Step 6 price automation. No WPF-equivalent Sheet Store provenance or multi-section snapshots are wired into the staff workspace. |
| **Step 5: WPF grid, multi-spec groups, rate overrides, imports, optimization** | Staff workspace edits grouped lines, area/value preview, add/remove/duplicate/reorder, and revision saves. Customer technical flow retains its local cut-list and import/parser but no longer offers spreadsheet exports or browser layout previews. `website/src/lib/proforma-draft.ts:priceLine` and the additive C8 RPC migration now follow `SqmCalculator.CalculatePanel` (3 dp midpoint-to-even before floor), use a 3 dp quantity area, retain raw physical panel area, and enforce `SpecGroupService.ValidateRow` primary dimensions/quantity limits in the browser, API validator and both save RPCs. Remaining missing metrics are W2/H2 billable area, per-spec glass-type perimeter factors, `Lm1`/`Lm2`, and the Step 6 metric handoff. | The arithmetic/primary-limit parity slice is tested, but multiple implementations remain. Customer technical intake does not calculate price or show layout/optimization results. WPF exact import formats, row fill/find operations, Step 4 ownership-aware synchronization, complete pane/LM metrics and optimizer parity are still missing. |
| **Step 6: predefined/metric charges, polish, custom charges, wastage, stock link** | Customer technical flow may derive processing labels from stated requirements, but no longer recommends offcut reuse or calculates charges, quantities, amounts or financial totals. Staff workspace schema has no complete WPF Step 6 charge/wastage graph. | Full automation/formula/persistence gap. Keep inventory consumption and offcut-reuse recommendations out of customer requests and passive web autosave; if adopted, make them separately authorized, audited internal actions. |
| **Step 7: discount/VAT waterfall, terms, quote preview and exports** | Current website workspace sum is the line-value total. `proforma-document.ts` explicitly describes that total as glass materials subtotal, not full invoice total; checked-in C5 docs say no Step 6 charges, discount or VAT. Customer PI request generates one unpriced Inquiry PDF and accepts optional supporting files; it is not a priced quotation. | No quote discount/VAT/terms model or WPF financial waterfall; current staff-only saved-line printout is not WPF `PrintableInvoiceViewModel` parity. The customer Inquiry PDF is technical-only; spreadsheet exports have been removed. Do not add a priced customer-facing website PI endpoint or PDF: WPF remains authoritative for priced PI generation/release. The internal staff workspace still needs the documented PI parity work. |
| **Step 8: signed docs/approval/status/Job Order bridge** | Customer inquiry supports uploads, and the WPF Step 8 is local AppData attachment management plus internal approval continuation. | Do not call inquiry uploads “Step 8 conversion.” The requested PI journey excludes Step 8; no customer-facing approval stage or API should be added. Existing inquiry upload pipeline remains a separate workflow. |
| **Step 9: P&L screens** | No WPF P&L/Sheets parity was found in the website PI workspace. | Only WPF tab 3, Sheets, is in scope, for staff only. Must be added as a separate internal projection and API—not as fields on a customer inquiry/proforma object. Other Step 9 tabs remain excluded. |

## 3. Existing persistence/API gap

### Current staff proforma schema and API

The checked-in local SQL/contracts define a narrower model:

- `website/supabase-inquiry-proforma-phase15.sql` and `...phase15-write.sql`: proforma header, lines, save history, line calculations in `save_inquiry_proforma_v1`.
- `website/supabase-inquiry-proforma-phaseC5-rates-and-status.sql`: group base rate/surcharge settings and `save_inquiry_proforma_v2`; its comments explicitly exclude Step 6 charges, discount and VAT.
- `website/supabase-inquiry-proforma-phaseC7-customer-share.sql`: historical per-proforma customer share policies/RPCs; this additive migration is retained for reproducibility and then retired by C11.
- `website/supabase-inquiry-proforma-phaseC11-customer-pricing-boundary.sql`: additive policy/RPC retirement, staff-only PI reads, preserved-and-withdrawn legacy share audit, and explicit customer pricing denial. Local only; deployment still required.
- `website/src/lib/proforma-api.ts`: operator roles (`inquiry_admin` write, admin/auditor read), caller-token Supabase access, revision/idempotency handling, current workspace fetch and validation.
- `website/src/app/api/inquiries/[id]/proforma/route.ts`: private/no-store staff route for GET and POST.

The line model stores dimensions, quantity, approved selling rate, billable SQM, total SQM and line value; current group settings are only base rate/surcharge percent pinned to a revision. It has no full company/client/project snapshots, Step 4 per-pane price provenance, Step 6 charge/wastage rows, Step 7 summary/terms, full WPF state, or Step 9 Sheets projection. A saved line total is not a full quote grand total. Local SQL and docs are evidence of repository intent only, not proof of what is deployed remotely.

### Customer priced-proforma projection — retired by Phase C11

The pre-C11 audit confirmed that the customer API returned `total_value`, per-line `approved_rate` and `line_value`; a revision-scoped policy did not make those fields acceptable under the clarified boundary. The API routes now return HTTP 410 without data, the service has no database access or price-bearing DTO, and the stale UI component is a no-data notice. The `/account/proformas` customer navigation links are removed. Phase C11 removes customer SELECT on `inquiry_proformas`, `inquiry_proforma_lines` and `inquiry_proforma_shares`, while preserving administrator/auditor access for staff PI work and legacy share audit rows.

Customer API and direct-ID requests are locally tested to return no priced fields; the generated customer Inquiry PDF is technical-only, and optional customer uploads remain a separate intake feature. There is no website customer-priced PI download route. The staff `ProformaDocument` remains inside the staff workspace only. C11 rehearsal used a disposable Supabase database, but live PostgREST/API and document-download checks and production deployment remain outstanding; until deployment, the historical C7 policy may still be effective on the active project.

### Current customer PI technical intake

`CustomerInquiryPortal` stores the required inquiry fields and attaches one generated unpriced Inquiry PDF; customers may optionally upload supporting files. The technical JSON is kept client-side until submission, while inquiry description receives a short attachment summary. Technical work is mostly React component state until submit; failed upload retry retains a server-side inquiry draft/files, but it does not provide durable, step-by-step WPF workspace autosave and restore.

`ProformaTechnicalSpecFlow.tsx` stores local specs, rows and notes. It supports four glass labels, pane descriptors, sheet dimensions, spacer/interlayer details, extras, edge finish, notes, W1/H1/W2/H2 and quantity, with validation and cutting-list import. Customer spreadsheet exports, browser layout/optimization previews, and offcut-reuse recommendations have been removed. The retained flow is technical-only; it is not Step 4 pricing or WPF optimization parity.

## 4. Calculation, validation and document risks

- Website `proforma-draft.ts`, `pricingRules`, API validation and SQL are currently aligned to the simple line-value contract, not the complete WPF `SqmCalculator` + Step 4–7 pipeline. WPF marks 0.5 m² minimum per panel and W1/W2 metrics, with its own 3dp/4dp/money rules; compare all boundaries before substituting one helper for another.
- Website customer technical intake does not expose pricing; this is a deliberate safe boundary. Preserve it for technical request submissions even if staff gain the full guided quotation workspace.
- The staff proforma API retains role authorization and database-side calculation for the simple model. Customer share/read is disabled at the application layer and by the C11 migration when deployed. Any rich workspace/internal Sheet data must remain outside customer APIs/documents; WPF is authoritative for priced customer PI output.
- WPF's `CrossStepValidator` currently uses Step 9 profitability to gate save/print. The website cannot copy that into a customer quote endpoint: internal margin/cost can be unavailable, and returning a blocking reason or calculation could itself disclose internal state. Split internal validation from customer quote validation.
- Existing WPF dossier layouts can opt into P&L. Web quote export must not share that exporter/DTO; an internal Sheets export is a different artifact with staff-only permissions and explicit internal labeling.
- The prior CSP Wasm allowance was verified at the response-header level; actual post-fix browser generation of the customer PI PDF is still unconfirmed. The separate Supabase 401 remains unresolved. These are facts to carry as test/risk items, not assumptions about root causes. No remote settings were changed in this audit.

## 5. Relevant website files inspected

### Customer submission and technical-only PI flow
- `website/src/components/inquiries/CustomerInquiryPortal.tsx`
- `website/src/components/inquiries/ProformaTechnicalSpecFlow.tsx`
- `website/src/lib/customer-cutlist.ts`
- `website/src/lib/customer-pi-processing.ts`
- `website/src/lib/customer-pi-attachments.tsx`
- `website/src/app/api/inquiry-submissions/drafts/route.ts`
- `website/src/app/api/inquiry-submissions/[id]/documents/route.ts`
- `website/src/app/api/inquiry-submissions/[id]/finalize/route.ts`
- `website/src/lib/inquiry-submissions-api.ts`

### Staff workspace and staff API
- `website/src/components/inquiries/ProformaWorkspace.tsx`
- `website/src/lib/proforma-draft.ts`
- `website/src/lib/proforma-rates.ts`
- `website/src/lib/proforma-api.ts`
- `website/src/lib/proforma-document.ts`
- `website/src/types/proforma.ts`
- `website/src/app/api/inquiries/[id]/proforma/route.ts`
- `website/src/app/api/inquiries/[id]/proforma/share/route.ts`

### Retired customer priced-proforma routes and compatibility surfaces
- `website/src/lib/customer-proforma-api.ts` (fail-closed, no database access)
- `website/src/app/api/account/proformas/route.ts` (410, no data)
- `website/src/app/api/account/proformas/[id]/route.ts` (410, direct-ID denied)
- `website/src/components/account/CustomerProformas.tsx` (no-data compatibility notice)
- `website/supabase-inquiry-proforma-phaseC11-customer-pricing-boundary.sql` (local additive migration; not yet deployed)
- `website/supabase-tests/phaseC11-pricing-boundary-harness.sql` and `phaseC11-pricing-boundary-assertions.sql`

### Local schema and contracts (not remote deployment verification)
- `website/supabase-inquiry-proforma-phase15.sql`
- `website/supabase-inquiry-proforma-phase15-write.sql`
- `website/supabase-inquiry-proforma-phaseC1.sql`
- `website/supabase-inquiry-proforma-phaseC5-rates-and-status.sql`
- `website/supabase-inquiry-proforma-phaseC7-customer-share.sql`
- `website/docs/PHASE15_PROFORMA_CONTRACT.md`
- `website/docs/PHASE15_INBOX_PROFORMA.md`
- `website/docs/PHASE_B_PI_AUDIT_AND_ARCHITECTURE.md`

## 6. Gap disposition

The customer inquiry technical-spec flow remains a separate, unpriced intake channel. Continue WPF PI parity only inside the authorized staff workspace, preserving the selected `d862436c125c5a83ac86bdef9ccef34c587408b3` baseline and existing work. Do not add a public pricing engine or website customer-priced PI API/PDF; WPF remains authoritative for pricing and priced PI release. Keep Step 9 Sheets as a distinct internal-only projection, and do not stretch the simple line schema into an undocumented WPF snapshot or place internal financial data in customer APIs/documents.

This gap report does not authorize broad styling changes or speculative changes to remote settings. The user has authorized implementation in controlled phases; local SQL/schema changes still require repository-level migration design and tests, and do not establish or modify remote deployment state. The selected WPF source baseline is ADV `main` as documented in `WPF_PROFORMA_SOURCE_AUDIT.md`.

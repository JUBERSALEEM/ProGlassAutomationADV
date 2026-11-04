# PI website implementation gap audit

**Purpose:** Compare the actual website on this branch with the selected WPF `main` baseline recorded in `WPF_PROFORMA_SOURCE_AUDIT.md`. This gap analysis is now an implementation checklist under the user's directive. It does not authorize speculative changes to remote Supabase/Vercel settings or imply that local SQL matches deployed state.

## 1. Current website surfaces (observed locally)

There are three related but different flows; they must not be mistaken for one converted WPF module.

1. **Customer inquiry / PI technical request:** `website/src/components/inquiries/CustomerInquiryPortal.tsx`, `ProformaTechnicalSpecFlow.tsx`, `customer-pi-attachments.tsx`, `customer-pi-processing.ts`, and `customer-cutlist.ts`. The PI intent gathers company/contact/project requirements, then a browser-local technical specification and cutting list. On submit, the UI generates technical Excel/CSV/PDF files, adds them as supporting inquiry documents and finalizes an inquiry submission. The UI text says the customer is making a technical request and ProGlass confirms production layout. It does not create a priced WPF-style PI quote.
2. **Staff inquiry-linked Proforma workspace:** `website/src/components/inquiries/ProformaWorkspace.tsx` and the `/api/inquiries/[id]/proforma` API. It edits grouped cut-list lines with manually entered rates and optional base-rate/surcharge settings; it saves revisions to `inquiry_proformas`, `inquiry_proforma_lines`, `inquiry_proforma_groups`, and `inquiry_proforma_saves`. It is a line editor, not the nine-stage WPF wizard.
3. **Customer account shared-document viewer:** `website/src/app/[locale]/account/page.tsx`, `website/src/app/api/account/proformas/*`, and `customer-proforma-api.ts`. It returns a shared customer proforma and lines according to the share table/RLS. This is a customer document projection and must remain separate from internal P&L/Sheets output.

Current worktree already has uncommitted PI/account changes. They are preserved. This report does not amend or revert them.

## 2. Step-by-step gap

| WPF source behavior | Website behavior found | Gap / risk before conversion |
|---|---|---|
| **Step 1: company identity, letterhead, TRN, bank, logo, ID, issue/validity/status** | Customer intake collects company/contact basics; the staff workspace reads the linked inquiry header. There is no full issuing-company profile/letterhead state, WPF logo layout, PI validity/status lifecycle, or WPF unique ID flow in the workspace. | Need a dedicated issuer/client/project document state. Do not reuse the inquiry form/company profile as issuer data. Add date/status/ID rules and PDF parity using the selected ADV `main` baseline. |
| **Step 2: client billing profile, sales rep, attention, prepared-by** | Intake stores customer/contact data; operator workspace shows inquiry/customer info but does not reproduce client cards, salesman list, saved billing address, prepared-by audit identity, or external selection adoption. | Model customer identity and issuing company as distinct entities; carry full billing fields into saved quote and export. Do not use customer account auth identity as a Proforma contact field. |
| **Step 3: client-filtered project fields** | Intake has project name/location and inquiry metadata. No WPF project cards, contract/reference/LPO fields, client-based saved-project filter, or same project restore semantics were found in the PI workspace. | Add the remaining project fields and state linkage, retaining project reference separately from project name. |
| **Step 4: SGU/DGU/LAM/DGU+LAM pricing estimator** | Customer `ProformaTechnicalSpecFlow` captures technical pane details and sheet dimensions but does not compute a WPF selling rate. Staff `ProformaBuilder` accepts a manually entered line rate and group base/surcharge. `pricingRules.ts` also declares `outerSheetRates`, `buildRateAdders` and `step4RatePerSqm`, but the staff builder does not call that function; its fixed rates, colour multipliers, adders and 1.58 factor are not the WPF formulas or a live store lookup. | Major calculation and state gap, including a disconnected incorrect price function. `customer-pi-processing.ts` derives technical processing labels, not Step 6 price automation. No WPF-equivalent Sheet Store provenance or multi-section snapshots are wired into the staff workspace. |
| **Step 5: WPF grid, multi-spec groups, rate overrides, imports, optimization** | Staff workspace edits grouped lines, area/value preview, add/remove/duplicate/reorder, and revision saves. Customer technical flow has its own local cut-list, parser/export and browser rectangle-layout preview. `website/src/lib/proforma-draft.ts:priceLine` and the additive C8 RPC migration now follow `SqmCalculator.CalculatePanel` (3 dp midpoint-to-even before floor), use a 3 dp quantity area, retain raw physical panel area, and enforce `SpecGroupService.ValidateRow` primary dimensions/quantity limits in the browser, API validator and both save RPCs. Remaining missing metrics are W2/H2 billable area, per-spec glass-type perimeter factors, `Lm1`/`Lm2`, and the Step 6 metric handoff. | The arithmetic/primary-limit parity slice is tested, but multiple implementations remain. Customer-side packer is a lightweight preview and explicitly omits yield/cost; it is not the WPF optimizer. WPF exact import formats, row fill/find operations, Step 4 ownership-aware synchronization, complete pane/LM metrics and optimizer parity are still missing. |
| **Step 6: predefined/metric charges, polish, custom charges, wastage, stock link** | Customer technical flow derives descriptions/suggestions and offcut-review notes without rates, quantities, amounts or financial totals. Staff workspace schema has no complete WPF Step 6 charge/wastage graph. | Full automation/formula/persistence gap. Keep inventory consumption out of customer requests and passive web autosave; if adopted, make it a separately authorized, audited internal action. |
| **Step 7: discount/VAT waterfall, terms, quote preview and exports** | Current website workspace sum is the line-value total. `proforma-document.ts` explicitly describes that total as glass materials subtotal, not full invoice total; checked-in C5 docs say no Step 6 charges, discount or VAT. Customer PI request generates technical attachment files, not a priced quotation. | No quote discount/VAT/terms model or WPF financial waterfall; current PDF/XLSX/CSV are technical request attachment exports or simple saved-line document views, not WPF `PrintableInvoiceViewModel` parity. Need separate customer-facing PI document builder and full quote state. |
| **Step 8: signed docs/approval/status/Job Order bridge** | Customer inquiry supports uploads, and the WPF Step 8 is local AppData attachment management plus internal approval continuation. | Do not call inquiry uploads “Step 8 conversion.” The requested PI journey excludes Step 8; no customer-facing approval stage or API should be added. Existing inquiry upload pipeline remains a separate workflow. |
| **Step 9: P&L screens** | No WPF P&L/Sheets parity was found in the website PI workspace. | Only WPF tab 3, Sheets, is in scope, for staff only. Must be added as a separate internal projection and API—not as fields on a customer inquiry/proforma object. Other Step 9 tabs remain excluded. |

## 3. Existing persistence/API gap

### Current staff proforma schema and API

The checked-in local SQL/contracts define a narrower model:

- `website/supabase-inquiry-proforma-phase15.sql` and `...phase15-write.sql`: proforma header, lines, save history, line calculations in `save_inquiry_proforma_v1`.
- `website/supabase-inquiry-proforma-phaseC5-rates-and-status.sql`: group base rate/surcharge settings and `save_inquiry_proforma_v2`; its comments explicitly exclude Step 6 charges, discount and VAT.
- `website/supabase-inquiry-proforma-phaseC7-customer-share.sql`: per-proforma customer share and revision-limited reads.
- `website/src/lib/proforma-api.ts`: operator roles (`inquiry_admin` write, admin/auditor read), caller-token Supabase access, revision/idempotency handling, current workspace fetch and validation.
- `website/src/app/api/inquiries/[id]/proforma/route.ts`: private/no-store staff route for GET and POST.

The line model stores dimensions, quantity, approved selling rate, billable SQM, total SQM and line value; current group settings are only base rate/surcharge percent pinned to a revision. It has no full company/client/project snapshots, Step 4 per-pane price provenance, Step 6 charge/wastage rows, Step 7 summary/terms, full WPF state, or Step 9 Sheets projection. A saved line total is not a full quote grand total. Local SQL and docs are evidence of repository intent only, not proof of what is deployed remotely.

### Current customer account projection

`customer-proforma-api.ts` uses the customer's own token. The C7 SQL contract limits reads to a shared revision and the account page lists only documents with a share row. The customer document response includes quote lines/rates/totals, but no WPF internal purchase cost or Sheet Page metrics were found in that projection. Preserve this allowlist approach; do not expand it to `select *` from a richer staff snapshot when adding web state. A dedicated mapper/DTO should whitelist customer-safe fields.

### Current customer PI technical intake

`CustomerInquiryPortal` stores generated attachments and required inquiry fields; it does not post the complete technical JSON as a rich PI workspace record. The JSON and technical text are used to create the generated spreadsheet/CSV/PDF attachments, while inquiry description receives a short attachment summary. Technical work is mostly React component state until submit; failed upload retry retains a server-side inquiry draft/files, but it does not provide durable, step-by-step WPF workspace autosave and restore.

`ProformaTechnicalSpecFlow.tsx` stores one local component state for specs, rows, notes and layout state. It supports four glass labels, pane descriptors, sheet dimensions, spacer/interlayer details, extras, edge finish, notes, W1/H1/W2/H2 and quantity. It includes validation, import/export, and `createCustomerCutLayout` quick/optimize layouts. Those layouts are browser-side technical previews; the source code says no yield or cost is calculated. It is **not** acceptable to present as Step 4 pricing or WPF optimization parity.

## 4. Calculation, validation and document risks

- Website `proforma-draft.ts`, `pricingRules`, API validation and SQL are currently aligned to the simple line-value contract, not the complete WPF `SqmCalculator` + Step 4–7 pipeline. WPF marks 0.5 m² minimum per panel and W1/W2 metrics, with its own 3dp/4dp/money rules; compare all boundaries before substituting one helper for another.
- Website customer technical intake does not expose pricing; this is a deliberate safe boundary. Preserve it for technical request submissions even if staff gain the full guided quotation workspace.
- Current staff proforma API has authorization and database-side calculation for the simple model, and the customer share API is revision-scoped. Any rich workspace/internal sheet data should not be returned from these routes by default.
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

### Customer-facing shared document
- `website/src/lib/customer-proforma-api.ts`
- `website/src/app/api/account/proformas/route.ts`
- `website/src/app/api/account/proformas/[id]/route.ts`
- `website/src/app/[locale]/account/page.tsx`

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

The customer inquiry technical-spec flow can remain a separate intake channel. The conversion target is a **new/expanded staff-authorized guided PI workspace** that can issue a complete customer-safe quotation, plus an internal-only Sheets analysis surface. Reuse the website's authenticated request, revision and share infrastructure only where contracts match; do not stretch the present simple line schema into an undocumented WPF snapshot or put internal Sheet Page data into customer APIs/documents.

This gap report does not authorize broad styling changes or speculative changes to remote settings. The user has authorized implementation in controlled phases; local SQL/schema changes still require repository-level migration design and tests, and do not establish or modify remote deployment state. The selected WPF source baseline is ADV `main` as documented in `WPF_PROFORMA_SOURCE_AUDIT.md`.
